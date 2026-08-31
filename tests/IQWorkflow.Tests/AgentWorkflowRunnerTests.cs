using System.Collections.Concurrent;
using System.IO;

using AiPromptEvaluator;

using IQWorkflow;

using Xunit;

namespace IQWorkflow.Tests;

/// <summary>
/// The pipeline running on the Agent Framework's own engine.
///
/// <b>These tests use the real engine, not a fake one.</b> The graph, the supersteps, the fan-in
/// barrier and the event stream are the framework's, and a test that substituted its own scheduler
/// would prove only that the substitute worked. The stages are still fakes — that is the point of
/// the adapter, and it is what keeps these tests free of Qdrant, a model and a network.
/// </summary>
public sealed class AgentWorkflowRunnerTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "iqworkflow-maf", Guid.NewGuid().ToString("N"));

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
        }
    }

    private WorkflowContext Context() => new(
        Directory.CreateDirectory(Path.Combine(_folder, "case")).FullName,
        "TC-1",
        new AppSettings { TenantId = 7 },
        Path.Combine(_folder, "run"));

    /// <summary>Records when it ran and what it was told to be, so ordering can be asserted.</summary>
    private sealed class RecordingStage(
        WorkflowStageId id,
        ConcurrentQueue<string> log,
        StageStatus status = StageStatus.Succeeded,
        bool satisfied = false,
        params WorkflowStageId[] dependsOn) : IWorkflowStage
    {
        public WorkflowStageId Id => id;

        public IReadOnlyList<WorkflowStageId> DependsOn => dependsOn;

        public int Executions;

        public Task<bool> IsSatisfiedAsync(WorkflowContext c, CancellationToken t = default) =>
            Task.FromResult(satisfied);

        public async Task<StageOutcome> ExecuteAsync(
            WorkflowContext c, IProgress<StageProgress>? p = null, CancellationToken t = default)
        {
            Interlocked.Increment(ref Executions);
            log.Enqueue($"start:{id}");

            // Long enough that a sequential runner would order the log strictly, so an interleaved
            // log is evidence of genuine overlap rather than of scheduling luck.
            await Task.Delay(60, t).ConfigureAwait(false);

            log.Enqueue($"end:{id}");

            return status == StageStatus.Failed
                ? StageOutcome.Failed(id, $"{id} failed.")
                : StageOutcome.Succeeded(id, $"{id} did its work.", TimeSpan.FromMilliseconds(60));
        }
    }

    [Fact]
    public async Task TheGraphRunsEveryStageAndRecordsThemInPipelineOrder()
    {
        var log = new ConcurrentQueue<string>();

        var result = await new AgentWorkflowRunner([
            new RecordingStage(WorkflowStageId.Extract, log),
            new RecordingStage(WorkflowStageId.Index, log),
            new RecordingStage(WorkflowStageId.Assess, log, StageStatus.Succeeded, false,
                WorkflowStageId.Extract, WorkflowStageId.Index),
            new RecordingStage(WorkflowStageId.Report, log, StageStatus.Succeeded, false,
                WorkflowStageId.Assess),
        ]).RunAsync(Context());

        Assert.True(result.Succeeded, result.Failure?.Summary);

        Assert.Equal(
            [WorkflowStageId.Extract, WorkflowStageId.Index, WorkflowStageId.Assess, WorkflowStageId.Report],
            result.Outcomes.Select(o => o.Stage));

        // The manifest is the audit trail, so it carries the same order.
        Assert.Equal(
            result.Outcomes.Select(o => o.Stage),
            result.Manifest.Stages.Select(s => s.Stage));
    }

    /// <summary>
    /// <b>The reason for using the framework's scheduler rather than running stages in a list.</b>
    /// Extraction and indexing declare no dependency on each other and bill against different
    /// services, so they belong in one superstep — and on a measured case extraction alone is
    /// 300–630 seconds, which sequencing would spend twice over.
    /// </summary>
    [Fact]
    public async Task IndependentStagesOverlapInOneSuperstep()
    {
        var log = new ConcurrentQueue<string>();

        await new AgentWorkflowRunner([
            new RecordingStage(WorkflowStageId.Extract, log),
            new RecordingStage(WorkflowStageId.Index, log),
        ]).RunAsync(Context());

        var order = log.ToList();

        // Both started before either finished. A sequential runner produces start,end,start,end.
        Assert.Equal(4, order.Count);
        Assert.StartsWith("start:", order[0], StringComparison.Ordinal);
        Assert.StartsWith("start:", order[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// The fan-in barrier: assessment starts once, after both of its dependencies, rather than once
    /// per dependency. Getting this wrong would assess the case twice and bill for it twice.
    /// </summary>
    [Fact]
    public async Task AFanInTargetRunsExactlyOnce()
    {
        var log = new ConcurrentQueue<string>();
        var assess = new RecordingStage(
            WorkflowStageId.Assess, log, StageStatus.Succeeded, false,
            WorkflowStageId.Extract, WorkflowStageId.Index);

        await new AgentWorkflowRunner([
            new RecordingStage(WorkflowStageId.Extract, log),
            new RecordingStage(WorkflowStageId.Index, log),
            assess,
        ]).RunAsync(Context());

        Assert.Equal(1, assess.Executions);
        Assert.Single(log, e => e == "start:Assess");
    }

    /// <summary>
    /// A failed stage produces no message, so its dependants are never invoked — the graph drains
    /// rather than aborting, and the failure is recorded rather than thrown.
    /// </summary>
    [Fact]
    public async Task AFailedStageStopsItsDependants()
    {
        var log = new ConcurrentQueue<string>();
        var after = new RecordingStage(
            WorkflowStageId.Report, log, StageStatus.Succeeded, false, WorkflowStageId.Assess);

        var result = await new AgentWorkflowRunner([
            new RecordingStage(WorkflowStageId.Assess, log, StageStatus.Failed),
            after,
        ]).RunAsync(Context());

        Assert.False(result.Succeeded);
        Assert.Equal(WorkflowStageId.Assess, result.Failure?.Stage);
        Assert.Equal(0, after.Executions);
    }

    /// <summary>A satisfied stage does no work — resumption survives the move to the engine.</summary>
    [Fact]
    public async Task ASatisfiedStageIsSkipped()
    {
        var log = new ConcurrentQueue<string>();
        var stage = new RecordingStage(WorkflowStageId.Index, log, StageStatus.Succeeded, satisfied: true);

        var result = await new AgentWorkflowRunner([stage]).RunAsync(Context());

        Assert.Equal(0, stage.Executions);
        Assert.Equal(StageStatus.Skipped, Assert.Single(result.Outcomes).Status);
    }

    /// <summary>
    /// The manifest is written as outcomes arrive, so a run killed halfway has still recorded what
    /// it achieved. This is what makes the next attempt a resumption rather than a restart.
    /// </summary>
    [Fact]
    public async Task TheManifestIsOnDiskAfterTheRun()
    {
        var log = new ConcurrentQueue<string>();

        var result = await new AgentWorkflowRunner([
            new RecordingStage(WorkflowStageId.Extract, log),
        ]).RunAsync(Context());

        Assert.True(File.Exists(result.ManifestPath));

        var read = WorkflowManifest.Read(result.ManifestPath);

        Assert.NotNull(read);
        Assert.Equal(WorkflowStageId.Extract, Assert.Single(read.Stages).Stage);
    }

    /// <summary>The graph's shape is derived from the stages and worth asserting directly.</summary>
    [Fact]
    public void WavesFollowTheDeclaredDependencies()
    {
        var log = new ConcurrentQueue<string>();

        var waves = new AgentWorkflowRunner([
            new RecordingStage(WorkflowStageId.Extract, log),
            new RecordingStage(WorkflowStageId.Index, log),
            new RecordingStage(WorkflowStageId.Assess, log, StageStatus.Succeeded, false,
                WorkflowStageId.Extract, WorkflowStageId.Index),
        ]).Waves();

        Assert.Equal(2, waves.Count);
        Assert.Equal(2, waves[0].Count);
        Assert.Equal(WorkflowStageId.Assess, Assert.Single(waves[1]).Id);
    }
}
