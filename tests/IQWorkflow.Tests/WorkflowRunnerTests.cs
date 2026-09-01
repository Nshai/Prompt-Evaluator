using System.IO;

using AiPromptEvaluator;

using IQWorkflow;

using Xunit;

namespace IQWorkflow.Tests;

/// <summary>
/// The orchestration itself: order, resumption, and stopping at a failure.
///
/// <b>Every test here drives the whole pipeline with no Qdrant, no model and no network.</b> That is
/// not a convenience — it is the property the stage abstraction exists to provide, and a test here
/// that needed a container would mean the abstraction had leaked.
/// </summary>
public sealed class WorkflowRunnerTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "iqworkflow-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temporary folder is not worth failing a test over.
        }
    }

    /// <summary>A stage that records what it was asked to do and answers however the test says.</summary>
    private sealed class FakeStage(
        WorkflowStageId id,
        StageStatus status = StageStatus.Succeeded,
        bool satisfied = false,
        params WorkflowStageId[] dependsOn) : IWorkflowStage
    {
        public WorkflowStageId Id => id;

        public IReadOnlyList<WorkflowStageId> DependsOn => dependsOn;

        public int Executions { get; private set; }

        public int SatisfactionChecks { get; private set; }

        public Task<bool> IsSatisfiedAsync(WorkflowContext context, CancellationToken cancellationToken = default)
        {
            SatisfactionChecks++;
            return Task.FromResult(satisfied);
        }

        public Task<StageOutcome> ExecuteAsync(
            WorkflowContext context,
            IProgress<StageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Executions++;

            return Task.FromResult(status switch
            {
                StageStatus.Failed => StageOutcome.Failed(id, $"{id} failed."),
                StageStatus.Cancelled => StageOutcome.Cancelled(id),
                _ => StageOutcome.Succeeded(id, $"{id} did its work.", TimeSpan.FromMilliseconds(1)),
            });
        }
    }

    private WorkflowContext Context() => new(
        caseFolder: Directory.CreateDirectory(Path.Combine(_folder, "case")).FullName,
        caseReference: "TC-1",
        settings: new AppSettings { TenantId = 7 },
        runFolder: Path.Combine(_folder, "run"));

    [Fact]
    public async Task EveryStageIsRecordedInOrder()
    {
        var runner = new WorkflowRunner([
            new FakeStage(WorkflowStageId.Extract),
            new FakeStage(WorkflowStageId.Index),
            new FakeStage(WorkflowStageId.Assess),
        ]);

        var result = await runner.RunAsync(Context());

        Assert.True(result.Succeeded);
        Assert.Equal(
            [WorkflowStageId.Extract, WorkflowStageId.Index, WorkflowStageId.Assess],
            result.Outcomes.Select(o => o.Stage));

        // The manifest is the audit trail, so it has to carry the same order.
        Assert.Equal(
            result.Outcomes.Select(o => o.Stage),
            result.Manifest.Stages.Select(s => s.Stage));
    }

    /// <summary>
    /// A stage that says its work is current is not run. This is what makes a resumed run cheap,
    /// and it is the stage's decision rather than the runner's.
    /// </summary>
    [Fact]
    public async Task ASatisfiedStageIsSkipped()
    {
        var stage = new FakeStage(WorkflowStageId.Index, satisfied: true);

        var result = await new WorkflowRunner([stage]).RunAsync(Context());

        Assert.Equal(0, stage.Executions);
        Assert.Equal(StageStatus.Skipped, Assert.Single(result.Outcomes).Status);
        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// <b>The fail-safe.</b> A dependent stage reading a half-written artefact would produce a
    /// plausible result from an incomplete input, and the report would not say which of its inputs
    /// was missing.
    ///
    /// The successor declares its dependency, which is what puts it in a later wave. A stage that
    /// declared none would be independent by definition, and stopping it would be stopping
    /// something the failure could not have affected.
    /// </summary>
    [Fact]
    public async Task AFailedStageStopsThePipeline()
    {
        var after = new FakeStage(
            WorkflowStageId.Assess, StageStatus.Succeeded, false,
            WorkflowStageId.Extract, WorkflowStageId.Index);

        var result = await new WorkflowRunner([
            new FakeStage(WorkflowStageId.Extract),
            new FakeStage(WorkflowStageId.Index, StageStatus.Failed),
            after,
        ]).RunAsync(Context());

        Assert.False(result.Succeeded);
        Assert.Equal(WorkflowStageId.Index, result.Failure?.Stage);
        Assert.Equal(0, after.Executions);
        Assert.Equal(0, after.SatisfactionChecks);
    }

    /// <summary>
    /// <b>Wall-clock time is an objective, so independent stages run together.</b> Extraction and
    /// indexing depend only on conversion and bill against different services — chat and
    /// embeddings — so running them in sequence spends the shorter of the two twice over.
    /// </summary>
    [Fact]
    public void IndependentStagesShareAWave()
    {
        var waves = new WorkflowRunner([
            new FakeStage(WorkflowStageId.Extract),
            new FakeStage(WorkflowStageId.Index),
            new FakeStage(WorkflowStageId.Assess, StageStatus.Succeeded, false,
                WorkflowStageId.Extract, WorkflowStageId.Index),
            new FakeStage(WorkflowStageId.Report, StageStatus.Succeeded, false, WorkflowStageId.Assess),
        ]).Waves();

        Assert.Equal(3, waves.Count);
        Assert.Equal(
            [WorkflowStageId.Extract, WorkflowStageId.Index],
            waves[0].Select(s => s.Id));
        Assert.Equal(WorkflowStageId.Assess, Assert.Single(waves[1]).Id);
        Assert.Equal(WorkflowStageId.Report, Assert.Single(waves[2]).Id);
    }

    /// <summary>
    /// The real pipeline's shape, so a registration change that serialises what should overlap is
    /// caught here rather than as a run that takes twice as long for no stated reason.
    /// </summary>
    [Fact]
    public async Task ConcurrentStagesActuallyOverlap()
    {
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        var blocking = new BlockingStage(WorkflowStageId.Extract, started, release);
        var other = new BlockingStage(WorkflowStageId.Index, null, null, () => release.TrySetResult());

        // Index can only complete by releasing Extract, so the run finishing at all proves the two
        // were in flight together.
        var result = await new WorkflowRunner([blocking, other]).RunAsync(Context());

        Assert.True(result.Succeeded);
        Assert.True(started.Task.IsCompleted);
    }

    private sealed class BlockingStage(
        WorkflowStageId id,
        TaskCompletionSource? started,
        TaskCompletionSource? waitFor,
        Action? onExecute = null) : IWorkflowStage
    {
        public WorkflowStageId Id => id;

        public IReadOnlyList<WorkflowStageId> DependsOn => [];

        public Task<bool> IsSatisfiedAsync(WorkflowContext c, CancellationToken t = default) =>
            Task.FromResult(false);

        public async Task<StageOutcome> ExecuteAsync(
            WorkflowContext c, IProgress<StageProgress>? p = null, CancellationToken t = default)
        {
            started?.TrySetResult();
            onExecute?.Invoke();

            if (waitFor is not null)
            {
                await waitFor.Task.WaitAsync(TimeSpan.FromSeconds(10), t).ConfigureAwait(false);
            }

            return StageOutcome.Succeeded(id, "done", TimeSpan.Zero);
        }
    }

    /// <summary>
    /// A dependency on a stage nobody registered would leave the graph unsatisfiable. Named rather
    /// than deadlocked: a workflow that hangs tells nobody which dependency was wrong.
    /// </summary>
    [Fact]
    public void AnUnsatisfiableDependencyIsNamed()
    {
        var runner = new WorkflowRunner([
            new FakeStage(WorkflowStageId.Report, StageStatus.Succeeded, false, WorkflowStageId.Assess),
        ]);

        var error = Assert.Throws<InvalidOperationException>(() => runner.Waves());

        Assert.Contains("Report depends on Assess", error.Message);
    }

    /// <summary>
    /// A second attempt starts at the first stage that is not already done. The manifest is written
    /// after each stage precisely so that a run killed halfway has still recorded what it achieved.
    /// </summary>
    [Fact]
    public async Task AResumedRunSkipsWhatAlreadySucceeded()
    {
        var context = Context();

        var first = await new WorkflowRunner([
            new FakeStage(WorkflowStageId.Extract),
            new FakeStage(WorkflowStageId.Index, StageStatus.Failed),
        ]).RunAsync(context);

        Assert.False(first.Succeeded);

        // The second attempt: extraction is satisfied now, indexing works.
        var extract = new FakeStage(WorkflowStageId.Extract, satisfied: true);
        var index = new FakeStage(WorkflowStageId.Index);

        var second = await new WorkflowRunner([extract, index]).RunAsync(Context());

        Assert.True(second.Succeeded);
        Assert.Equal(0, extract.Executions);
        Assert.Equal(1, index.Executions);
    }

    /// <summary>
    /// <b>A configuration change discards the earlier stages.</b> Resuming across one would mix a
    /// model extracted under one configuration with an assessment run under another, and the result
    /// would carry a single fingerprint while being two runs.
    /// </summary>
    [Fact]
    public async Task AConfigurationChangeStartsAFreshManifest()
    {
        var runFolder = Path.Combine(_folder, "run");
        var caseFolder = Directory.CreateDirectory(Path.Combine(_folder, "case")).FullName;

        var before = new WorkflowContext(caseFolder, "TC-1", new AppSettings { MaxPassagesPerGroup = 24 }, runFolder);
        await new WorkflowRunner([new FakeStage(WorkflowStageId.Extract)]).RunAsync(before);

        var after = new WorkflowContext(caseFolder, "TC-1", new AppSettings { MaxPassagesPerGroup = 12 }, runFolder);
        var result = await new WorkflowRunner([new FakeStage(WorkflowStageId.Index)]).RunAsync(after);

        // The earlier extraction is gone from the manifest: it belongs to a different configuration.
        Assert.DoesNotContain(WorkflowStageId.Extract, result.Manifest.Stages.Select(s => s.Stage));
        Assert.NotEqual(before.Settings.MaxPassagesPerGroup, after.Settings.MaxPassagesPerGroup);
    }

    [Fact]
    public async Task CancellationStopsBetweenStages()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var stage = new FakeStage(WorkflowStageId.Extract);
        var result = await new WorkflowRunner([stage]).RunAsync(Context(), null, cts.Token);

        Assert.Equal(0, stage.Executions);
        Assert.Equal(StageStatus.Cancelled, Assert.Single(result.Outcomes).Status);
        Assert.False(result.Succeeded);
    }

    /// <summary>
    /// A stage that throws is a defect, not a bad input — but the run still ends with a manifest
    /// rather than an unhandled exception and no record of what happened.
    /// </summary>
    [Fact]
    public async Task AThrowingStageBecomesARecordedFailure()
    {
        var result = await new WorkflowRunner([new ThrowingStage()]).RunAsync(Context());

        var outcome = Assert.Single(result.Outcomes);

        Assert.Equal(StageStatus.Failed, outcome.Status);
        Assert.Contains("failed unexpectedly", outcome.Summary);
        Assert.True(File.Exists(result.ManifestPath));
    }

    private sealed class ThrowingStage : IWorkflowStage
    {
        public WorkflowStageId Id => WorkflowStageId.Extract;

        public Task<bool> IsSatisfiedAsync(WorkflowContext c, CancellationToken t = default) =>
            Task.FromResult(false);

        public Task<StageOutcome> ExecuteAsync(
            WorkflowContext c, IProgress<StageProgress>? p = null, CancellationToken t = default) =>
            throw new InvalidOperationException("a defect");
    }

    /// <summary>
    /// Two stages under one id would write two entries under one manifest key, and a resumed run
    /// would satisfy both from whichever was recorded last.
    /// </summary>
    [Fact]
    public void TwoStagesCannotShareAnId() =>
        Assert.Throws<ArgumentException>(() => new WorkflowRunner([
            new FakeStage(WorkflowStageId.Index),
            new FakeStage(WorkflowStageId.Index),
        ]));

    [Fact]
    public void AWorkflowNeedsAtLeastOneStage() =>
        Assert.Throws<ArgumentException>(() => new WorkflowRunner([]));

    /// <summary>Re-running a stage replaces its row rather than adding one per attempt.</summary>
    [Fact]
    public async Task ARetriedStageReplacesItsRecord()
    {
        var context = Context();

        await new WorkflowRunner([new FakeStage(WorkflowStageId.Index, StageStatus.Failed)]).RunAsync(context);
        var result = await new WorkflowRunner([new FakeStage(WorkflowStageId.Index)]).RunAsync(Context());

        Assert.Single(result.Manifest.Stages, s => s.Stage == WorkflowStageId.Index);
        Assert.Equal(StageStatus.Succeeded, result.Manifest.Stages.Single().Status);
    }
}
