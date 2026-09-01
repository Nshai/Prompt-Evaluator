using System.Diagnostics;

using Microsoft.Agents.AI.Workflows;

namespace IQWorkflow;

/// <summary>
/// What travels between nodes: the run's context, and what has happened to it so far.
///
/// <b>One instance per run, passed by reference.</b> The stages already share a
/// <see cref="WorkflowContext"/> — extraction puts the canonical model on it and assessment reads it
/// — so serialising a copy between nodes would either lose that or force every stage to re-read from
/// storage what the previous one had just produced.
/// </summary>
/// <param name="Context">The run.</param>
/// <param name="Outcomes">
/// What each stage returned, accumulated. Held on the signal rather than in the node so a fan-in can
/// merge two branches without either having written to a shared list concurrently.
/// </param>
public sealed record WorkflowSignal(
    WorkflowContext Context,
    IReadOnlyList<StageOutcome> Outcomes)
{
    public static WorkflowSignal Start(WorkflowContext context) => new(context, []);

    public WorkflowSignal With(StageOutcome outcome) => this with { Outcomes = [.. Outcomes, outcome] };

    /// <summary>True when every stage so far ended in a way the pipeline may continue past.</summary>
    public bool CanContinue => Outcomes.All(o => o.CanContinue);
}

/// <summary>
/// Where the graph begins.
///
/// A node of its own rather than making the first stage the entry point, because the first wave has
/// more than one stage in it — extraction and indexing are independent — and a fan-out needs
/// something to fan out from.
/// </summary>
internal sealed class StartNode() : Executor<WorkflowSignal, WorkflowSignal>("start")
{
    public override ValueTask<WorkflowSignal> HandleAsync(
        WorkflowSignal message, IWorkflowContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(message);
}

/// <summary>
/// One stage's outcome, raised as a workflow event.
///
/// <b>The outcome travels as an event rather than as the node's return value.</b> A stage that fails
/// must not send a message — that is how its dependants are prevented from running — but its outcome
/// still has to reach the manifest, and a node that returned nothing would have nothing to report
/// it with. Separating the two means the record of what happened does not depend on whether the
/// pipeline continued.
/// </summary>
public sealed class StageOutcomeEvent(StageOutcome outcome) : WorkflowEvent(outcome)
{
    public StageOutcome Outcome { get; } = outcome;
}

/// <summary>
/// Collapses the branches of a fan-in back into one signal.
///
/// <b>Its own node so that <see cref="StageExecutorNode"/> stays uniform.</b> Every stage node takes
/// one signal and returns one; without this, a stage that happened to have two dependencies would
/// need a different shape from one that had a single dependency, and the graph builder would be
/// choosing node types rather than describing a pipeline.
///
/// The contexts are the same object on every branch, so merging is a matter of concatenating the
/// outcomes and keeping their declared order.
/// </summary>
[SendsMessage(typeof(WorkflowSignal))]
internal sealed partial class JoinNode(string id) : Executor<WorkflowSignal>(id)
{
    private readonly List<WorkflowSignal> _branches = [];

    /// <summary>
    /// <b>A barrier releases its held messages one at a time, not as a batch.</b> The builder's own
    /// words are that they "will be streamed to the target executor" once every source has produced
    /// one — so this is invoked once per branch and has to accumulate, rather than being handed a
    /// list. Getting that wrong is silent: the node is simply never called, the graph drains, and
    /// the run reports the stages that did happen with no indication that one never started.
    /// </summary>
    public override ValueTask HandleAsync(
        WorkflowSignal message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        _branches.Add(message);

        return default;
    }

    /// <summary>
    /// Emits once, after every branch has been delivered. This is the join proper: the point where
    /// two concurrent supersteps become one again.
    /// </summary>
    protected override ValueTask OnMessageDeliveryFinishedAsync(
        IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (_branches.Count == 0)
        {
            return default;
        }

        // Distinct by stage, because every branch carries the whole history from before the split;
        // without this the shared prefix would be recorded once per branch and costed twice.
        var merged = _branches
            .SelectMany(b => b.Outcomes)
            .GroupBy(o => o.Stage)
            .Select(g => g.First())
            .ToList();

        var signal = _branches[0] with { Outcomes = merged };

        _branches.Clear();

        return context.SendMessageAsync(signal, null, cancellationToken);
    }
}

/// <summary>
/// Runs one <see cref="IWorkflowStage"/> as a node in the graph.
///
/// <b>This adapter is the whole of the framework's reach into the pipeline.</b> The stages know
/// nothing about workflows, executors or edges: they take a context and return an outcome, which is
/// why every stage test drives them directly with no engine at all. What the framework supplies is
/// the graph, the concurrency, the event stream and the checkpointing — and none of that is
/// something worth reimplementing.
///
/// <b>A failed stage stops its branch by not producing a message.</b> The graph has no notion of a
/// pipeline-wide abort, and inventing one by throwing would turn an expected outcome — an
/// unreachable store, an unparseable report — into an error the caller has to interpret. Yielding
/// nothing lets the run drain naturally, with the failure recorded and every downstream node simply
/// never invoked.
/// </summary>
internal sealed class StageExecutorNode(
    IWorkflowStage stage,
    IProgress<StageProgress>? progress)
    : Executor<WorkflowSignal, WorkflowSignal>(stage.Id.ToString())
{
    public override async ValueTask<WorkflowSignal> HandleAsync(
        WorkflowSignal message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        // <b>The fail-stop.</b> A signal carrying a failure is forwarded untouched: this stage does
        // not run, raises no outcome, and every stage after it does the same. The graph drains to
        // its end rather than aborting, which keeps an expected outcome — an unreachable store, an
        // unparseable report — from becoming an exception the caller has to interpret, while still
        // guaranteeing that nothing downstream reads a half-written artefact.
        if (!message.CanContinue)
        {
            return message;
        }

        var outcome = await ExecuteAsync(message.Context, cancellationToken).ConfigureAwait(false);

        // Raised as an event rather than inferred from the returned signal, so the manifest records
        // this stage the moment it finishes rather than when some later node happens to complete.
        await context
            .AddEventAsync(new StageOutcomeEvent(outcome), cancellationToken)
            .ConfigureAwait(false);

        return message.With(outcome);
    }

    /// <summary>
    /// The stage, with the timing, the satisfaction check and the failure handling that every stage
    /// needs and none should have to write.
    /// </summary>
    private async Task<StageOutcome> ExecuteAsync(
        WorkflowContext runContext, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();

        try
        {
            if (runContext.IsBypassed(stage.Id))
            {
                var bypassed = StageOutcome.Bypassed(stage.Id);

                progress?.Report(new StageProgress(stage.Id, bypassed.Summary));

                return bypassed;
            }

            // The stage decides, not the graph: only the index stage knows what makes an index
            // current. A stage that cannot tell answers false and the work is done again.
            if (await stage.IsSatisfiedAsync(runContext, cancellationToken).ConfigureAwait(false))
            {
                var skipped = StageOutcome.Skipped(
                    stage.Id, "Already current for this case; nothing to do.");

                progress?.Report(new StageProgress(stage.Id, skipped.Summary));

                return skipped;
            }

            return await stage
                .ExecuteAsync(runContext, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            clock.Stop();

            return StageOutcome.Cancelled(stage.Id, clock.Elapsed);
        }
        catch (Exception ex)
        {
            clock.Stop();

            // A stage is expected to report its own failures as outcomes. Reaching here means a
            // defect rather than a bad input, so the type is kept — it is the only clue — while the
            // run still ends with a manifest rather than an unhandled exception and no record.
            return StageOutcome.Failed(
                stage.Id,
                $"{stage.Id} failed unexpectedly: {ex.Message}",
                clock.Elapsed,
                new Dictionary<string, string> { ["exception"] = ex.GetType().Name });
        }
    }
}
