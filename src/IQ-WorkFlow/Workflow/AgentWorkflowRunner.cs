using System.IO;

using Microsoft.Agents.AI.Workflows;

namespace IQWorkflow;

/// <summary>
/// Runs the pipeline as an Agent Framework workflow.
///
/// <b>What the framework supplies, and why that is worth taking.</b> The graph, the supersteps that
/// make independent stages overlap, the event stream and the checkpointing are all its. Every one of
/// those is something this project would otherwise have written and then had to test — and the
/// concurrency in particular is the part where a hand-rolled version is most likely to be subtly
/// wrong.
///
/// <b>What stays ours.</b> The stages, which know nothing about it; the manifest, which is the audit
/// trail and the resumption point; and the decision about what "already done" means, which belongs
/// to each stage. The framework's own checkpointing answers a different question — it resumes an
/// interrupted graph from a superstep — and this pipeline's expensive work is idempotent at a
/// coarser grain than that: a stored canonical model is reusable by any later run, not only by the
/// one that was interrupted.
/// </summary>
public sealed class AgentWorkflowRunner
{
    private readonly CaseWorkflowGraph _graph;

    public AgentWorkflowRunner(IEnumerable<IWorkflowStage> stages) =>
        _graph = new CaseWorkflowGraph(stages);

    /// <summary>The pipeline's shape, for a caller that wants to show or assert on it.</summary>
    public IReadOnlyList<IReadOnlyList<IWorkflowStage>> Waves() => _graph.Waves();

    /// <summary>
    /// Runs the case through the graph.
    ///
    /// The manifest is written as each stage's outcome arrives rather than at the end, so a run that
    /// is killed halfway has still recorded what it achieved — which is what makes the next attempt
    /// a resumption rather than a restart.
    /// </summary>
    public async Task<WorkflowResult> RunAsync(
        WorkflowContext context,
        IProgress<StageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        Directory.CreateDirectory(context.RunFolder);

        var manifestPath = Path.Combine(context.RunFolder, WorkflowManifest.FileName);
        var manifest = WorkflowManifest.StartOrResume(WorkflowManifest.Read(manifestPath), context);

        var workflow = _graph.Build(progress);
        var recorded = new HashSet<WorkflowStageId>();
        var outcomes = new List<StageOutcome>();

        await using var run = await InProcessExecution
            .RunStreamingAsync(
                workflow,
                WorkflowSignal.Start(context),
                sessionId: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await foreach (var evt in run.WatchStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            switch (evt)
            {
                // Each stage raises its own outcome as an event, whether or not it went on to send
                // a message. Recording from the event stream rather than from the final result is
                // what keeps the manifest current during a long run instead of only after it — and
                // it is the only way a failed stage, which sends nothing, still gets recorded.
                case StageOutcomeEvent staged:
                    Record(staged.Outcome, manifest, manifestPath, recorded, outcomes);
                    break;

                // A node that threw rather than returning an outcome. The stage adapter catches
                // everything a stage can raise, so this is the framework itself failing — a defect,
                // and one the run must not report as a clean finish.
                case ExecutorFailedEvent failed:
                    outcomes.Add(Fail(
                        failed.ExecutorId,
                        $"The workflow node \"{failed.ExecutorId}\" failed: "
                        + (failed.Data?.ToString() ?? "no detail was reported."),
                        manifest, manifestPath, recorded));
                    break;

                case WorkflowErrorEvent error:
                    outcomes.Add(Fail(
                        "workflow",
                        $"The workflow failed: {error.Exception?.Message ?? "no detail was reported."}",
                        manifest, manifestPath, recorded));
                    break;
            }
        }

        // Ordered by the pipeline's own shape rather than by when each node happened to finish, so
        // a manifest reads the same however the supersteps interleaved.
        var order = _graph.Waves().SelectMany(w => w).Select(s => s.Id).ToList();

        manifest.Stages.Sort((a, b) => Position(order, a.Stage).CompareTo(Position(order, b.Stage)));
        manifest.Write(manifestPath);

        return new WorkflowResult(
            manifest,
            [.. outcomes.OrderBy(o => Position(order, o.Stage))],
            manifestPath);
    }

    private static int Position(List<WorkflowStageId> order, WorkflowStageId stage)
    {
        var at = order.IndexOf(stage);

        // A stage the graph does not contain — a synthetic failure row — sorts last rather than
        // first, so it reads as something that happened to the run rather than something that
        // started it.
        return at < 0 ? int.MaxValue : at;
    }

    /// <summary>
    /// Files one stage's outcome, once.
    ///
    /// Filing by stage rather than by event because a fan-in delivers the shared history of both
    /// branches, and a stage recorded twice would appear twice in the manifest and be costed twice.
    /// </summary>
    private static void Record(
        StageOutcome outcome,
        WorkflowManifest manifest,
        string manifestPath,
        HashSet<WorkflowStageId> recorded,
        List<StageOutcome> outcomes)
    {
        if (!recorded.Add(outcome.Stage))
        {
            return;
        }

        outcomes.Add(outcome);
        Append(manifest, outcome);
        manifest.Write(manifestPath);
    }

    private static StageOutcome Fail(
        string nodeId,
        string summary,
        WorkflowManifest manifest,
        string manifestPath,
        HashSet<WorkflowStageId> recorded)
    {
        // A node id is a stage id where the node is a stage, and something else — a join, or the
        // workflow itself — where it is not. Either way the failure is recorded against a stage so
        // the manifest has one shape.
        var stage = Enum.TryParse<WorkflowStageId>(nodeId, out var parsed)
            ? parsed
            : WorkflowStageId.Assess;

        var outcome = StageOutcome.Failed(stage, summary);

        if (recorded.Add(outcome.Stage))
        {
            Append(manifest, outcome);
            manifest.Write(manifestPath);
        }

        return outcome;
    }

    private static void Append(WorkflowManifest manifest, StageOutcome outcome)
    {
        manifest.Stages.RemoveAll(s => s.Stage == outcome.Stage);

        manifest.Stages.Add(new StageRecord(
            outcome.Stage,
            outcome.Status,
            outcome.Summary,
            Math.Round(outcome.Elapsed.TotalSeconds, 3),
            DateTimeOffset.Now,
            outcome.Detail,
            outcome.Usage?.InputTokens ?? 0,
            outcome.Usage?.OutputTokens ?? 0,

            // The distinction a cost report depends on: a stage that billed nothing and a stage
            // whose provider reported nothing look identical in the numbers and are not the same
            // fact.
            outcome.Usage is not null));
    }
}
