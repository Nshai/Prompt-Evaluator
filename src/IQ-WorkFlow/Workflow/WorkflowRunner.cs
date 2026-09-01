using System.Diagnostics;
using System.IO;

using AiPromptEvaluator;

namespace IQWorkflow;

/// <summary>What a whole run did.</summary>
/// <param name="Manifest">The record, as it was written to disk.</param>
/// <param name="Outcomes">Each stage's outcome, in the order the stages ran.</param>
/// <param name="ManifestPath">Where the record was written.</param>
public sealed record WorkflowResult(
    WorkflowManifest Manifest,
    IReadOnlyList<StageOutcome> Outcomes,
    string ManifestPath)
{
    /// <summary>True when every stage ended in a way the pipeline could continue past.</summary>
    public bool Succeeded => Outcomes.Count > 0 && Outcomes.All(o => o.CanContinue);

    /// <summary>The stage that stopped the run, where one did.</summary>
    public StageOutcome? Failure => Outcomes.FirstOrDefault(o => !o.CanContinue);
}

/// <summary>
/// Runs the stages in order, skipping what is already done and stopping at the first failure.
///
/// <b>This is the whole of the orchestration.</b> Everything it drives lives in
/// <c>AiPromptEvaluator.Core</c> and is tested there; what is here is the sequence, the resumption
/// and the record — the part that previously existed only as the order in which a person clicked
/// buttons, and which therefore could not be repeated, scheduled or audited.
///
/// <b>Stages are injected in order and the runner does not name them.</b> Adding a stage is a
/// registration, not an edit here, and a test can drive the whole pipeline with fakes because the
/// runner depends on the interface rather than on any stage.
/// </summary>
public sealed class WorkflowRunner
{
    private readonly IReadOnlyList<IWorkflowStage> _stages;

    /// <param name="stages">
    /// The stages, in execution order. Registration order is execution order — deliberately, so
    /// the sequence is readable in one place in the composition root rather than encoded as
    /// priorities each stage asserts about itself.
    /// </param>
    public WorkflowRunner(IEnumerable<IWorkflowStage> stages)
    {
        ArgumentNullException.ThrowIfNull(stages);

        _stages = [.. stages];

        if (_stages.Count == 0)
        {
            throw new ArgumentException("A workflow needs at least one stage.", nameof(stages));
        }

        var duplicates = _stages
            .GroupBy(s => s.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.ToString())
            .ToList();

        // Two stages sharing an id would write two entries under one key in the manifest, and a
        // resumed run would satisfy both from whichever was recorded last.
        if (duplicates.Count > 0)
        {
            throw new ArgumentException(
                $"Two stages share the id {string.Join(", ", duplicates)}. A stage id is its key in "
                + "the manifest and has to be unique.", nameof(stages));
        }
    }

    /// <summary>
    /// Runs the case through every stage.
    ///
    /// The manifest is written after each stage rather than at the end: a run that is cancelled or
    /// killed halfway has still recorded what it achieved, which is what makes the next attempt a
    /// resumption rather than a restart.
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
        var outcomes = new List<StageOutcome>();
        var manifestLock = new object();

        foreach (var wave in Waves())
        {
            // Checked between waves rather than only inside stages: a stage that has not started
            // cannot be interrupted, and an operator who cancelled should not wait for the next one
            // to begin before anything notices.
            if (cancellationToken.IsCancellationRequested)
            {
                foreach (var stage in wave)
                {
                    outcomes.Add(Record(manifest, StageOutcome.Cancelled(stage.Id), manifestPath, manifestLock));
                }

                break;
            }

            // Everything in a wave is independent by construction, so it runs together. On a real
            // case this is extraction and indexing overlapping, which is the difference between
            // paying for the longer of the two and paying for both.
            var running = wave
                .Select(stage => ExecuteAsync(stage, context, progress, cancellationToken))
                .ToArray();

            var completed = await Task.WhenAll(running).ConfigureAwait(false);

            // Recorded in the wave's declared order rather than completion order, so a manifest
            // reads the same however the work happened to interleave — the same reason Core
            // collects findings by index rather than appending them.
            foreach (var outcome in completed)
            {
                outcomes.Add(Record(manifest, outcome, manifestPath, manifestLock));
            }

            if (completed.Any(o => !o.CanContinue))
            {
                // Stop rather than carry on: a later stage reading a half-written artefact would
                // produce a plausible result from an incomplete input, which is worse than no
                // result at all — the report would not say which of its inputs was missing.
                break;
            }
        }

        return new WorkflowResult(manifest, outcomes, manifestPath);
    }

    /// <summary>
    /// The stages grouped into waves: everything in a wave is independent of everything else in it,
    /// and depends only on stages in earlier waves.
    ///
    /// <b>Wall-clock time is an objective, so what is independent runs together.</b> Extraction and
    /// indexing both depend only on conversion and not on each other; on a measured case extraction
    /// alone is 300–630 seconds, and running the two in sequence spends the shorter of them twice
    /// over for no reason. They also bill against different services — chat and embeddings — so
    /// overlapping them does not make either wait on the other's rate limit.
    ///
    /// <b>Declaration order breaks ties.</b> A stage that depends on nothing joins the first wave;
    /// this is not a scheduler and does not try to be. Every dependency must name a stage that is
    /// actually registered, or the graph would silently never satisfy it.
    /// </summary>
    internal IReadOnlyList<IReadOnlyList<IWorkflowStage>> Waves()
    {
        var waves = new List<IReadOnlyList<IWorkflowStage>>();
        var placed = new HashSet<WorkflowStageId>();
        var remaining = _stages.ToList();

        while (remaining.Count > 0)
        {
            var wave = remaining
                .Where(s => s.DependsOn.All(placed.Contains))
                .ToList();

            if (wave.Count == 0)
            {
                // Every remaining stage is waiting on something that will never arrive. Named
                // rather than deadlocked: a workflow that hangs tells nobody which dependency was
                // wrong.
                throw new InvalidOperationException(
                    "The stages cannot be ordered: "
                    + string.Join(", ", remaining.Select(s => $"{s.Id} depends on "
                        + string.Join("+", s.DependsOn.Where(d => !placed.Contains(d)))))
                    + ". A dependency names a stage that is not registered, or two stages depend on "
                    + "each other.");
            }

            waves.Add(wave);
            remaining.RemoveAll(wave.Contains);

            foreach (var stage in wave)
            {
                placed.Add(stage.Id);
            }
        }

        return waves;
    }

    /// <summary>
    /// One stage, with the timing, the satisfaction check and the failure handling that every
    /// stage needs and none should have to write.
    /// </summary>
    private static async Task<StageOutcome> ExecuteAsync(
        IWorkflowStage stage,
        WorkflowContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();

        try
        {
            if (context.IsBypassed(stage.Id))
            {
                var bypassed = StageOutcome.Bypassed(stage.Id);
                progress?.Report(new StageProgress(stage.Id, bypassed.Summary));

                return bypassed;
            }

            // The stage decides, not the runner: only the index stage knows what makes an index
            // current. A stage that cannot tell answers false, and the work is done again.
            if (await stage.IsSatisfiedAsync(context, cancellationToken).ConfigureAwait(false))
            {
                var skipped = StageOutcome.Skipped(stage.Id, "Already current for this case; nothing to do.");
                progress?.Report(new StageProgress(stage.Id, skipped.Summary));

                return skipped;
            }

            return await stage.ExecuteAsync(context, progress, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Puts an outcome in the manifest and writes it, replacing any earlier attempt at the same
    /// stage so a resumed run does not accumulate one row per attempt.
    /// </summary>
    /// <param name="gate">
    /// Held while the manifest is mutated and written. Stages in a wave finish concurrently, and
    /// two of them writing the same file at once would produce a manifest that is neither.
    /// </param>
    private static StageOutcome Record(
        WorkflowManifest manifest, StageOutcome outcome, string manifestPath, object gate)
    {
        lock (gate)
        {
            RecordCore(manifest, outcome, manifestPath);
        }

        return outcome;
    }

    private static void RecordCore(
        WorkflowManifest manifest, StageOutcome outcome, string manifestPath)
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

            // The distinction the report depends on: a stage that billed nothing and a stage whose
            // provider reported nothing look identical in the numbers and are not the same fact.
            outcome.Usage is not null));

        manifest.Write(manifestPath);
    }
}
