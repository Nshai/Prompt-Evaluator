using System.IO;

using AiPromptEvaluator;

using IQWorkflow;

using Xunit;

namespace IQWorkflow.Tests;

/// <summary>
/// The manifest's two jobs — being the audit trail and being the resumption point — and the path
/// containment that keeps an unattended run inside the case it was given.
/// </summary>
public sealed class ManifestAndGuardTests : IDisposable
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
        }
    }

    // ──────────────────────────────────────────────
    // Security: path containment
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>A case folder is user input, and so is everything derived from it.</b> A traversal
    /// segment, an absolute path where a relative one was expected, or a sibling folder whose name
    /// merely starts with the root's — each turns "write into the run folder" into "write anywhere".
    /// </summary>
    [Theory]
    [InlineData(@"C:\runs\case", "report.html", true)]
    [InlineData(@"C:\runs\case", @"nested\report.html", true)]
    [InlineData(@"C:\runs\case", @"..\escaped.html", false)]
    [InlineData(@"C:\runs\case", @"..\..\Windows\System32\evil.dll", false)]
    [InlineData(@"C:\runs\case", @"C:\Windows\System32\evil.dll", false)]
    [InlineData(@"C:\runs\case", @"nested\..\..\escaped.html", false)]
    // The boundary that a naive prefix comparison gets wrong: a sibling whose name extends the
    // root's is not inside it, exactly as /objectives must not cover /objectivesReview.
    [InlineData(@"C:\runs\case", @"C:\runs\case-archive\leak.html", false)]
    public void APathIsHeldInsideItsRoot(string root, string candidate, bool allowed) =>
        Assert.Equal(allowed, PathGuard.Contain(root, candidate) is not null);

    /// <summary>Case-insensitive, because this targets Windows and the same folder has two spellings.</summary>
    [Fact]
    public void ContainmentIsNotDefeatedByCase() =>
        Assert.NotNull(PathGuard.Contain(@"C:\Runs\Case", @"c:\runs\case\report.html"));

    [Fact]
    public void TheRootItselfIsContained() =>
        Assert.NotNull(PathGuard.Contain(@"C:\runs\case", @"C:\runs\case"));

    [Fact]
    public void ARejectedPathNamesWhatWasRejectedAndNotTheFilesystem()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => PathGuard.Require(@"C:\runs\case", @"..\escaped.html", "artefact path"));

        Assert.Contains("artefact path", error.Message);
        Assert.Contains("resolves outside", error.Message);
    }

    /// <summary>The guard reaches the context, which is where every artefact path is composed.</summary>
    [Fact]
    public void TheContextRefusesAnArtefactPathOutsideTheRunFolder()
    {
        var context = new WorkflowContext(
            Directory.CreateDirectory(Path.Combine(_folder, "case")).FullName,
            "TC-1",
            new AppSettings(),
            Path.Combine(_folder, "run"));

        Assert.Throws<InvalidOperationException>(() => context.ArtefactPath(@"..\escaped.html"));
        Assert.NotNull(context.ArtefactPath("report.html"));
    }

    // ──────────────────────────────────────────────
    // Security: no credential reaches the manifest
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>The manifest is written to disk beside the case and read by whoever reads the run.</b> It
    /// records the settings digest, which excludes credentials and endpoints by name through
    /// <c>AppSettings.NotFingerprinted</c> — the manifest defers to that list rather than keeping a
    /// second one to forget.
    /// </summary>
    [Fact]
    public void NoCredentialReachesTheManifest()
    {
        var settings = new AppSettings
        {
            OpenAiApiKey = "sk-secret-key-value",
            QdrantApiKey = "qdrant-secret",
            EmbeddingApiKey = "embedding-secret",
            OpenAiBaseUrl = "https://internal.example/v1",
        };

        var context = new WorkflowContext(
            Directory.CreateDirectory(Path.Combine(_folder, "case")).FullName,
            "TC-1", settings, Path.Combine(_folder, "run"));

        var json = WorkflowManifest.StartOrResume(null, context).ToJson();

        Assert.DoesNotContain("sk-secret-key-value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("qdrant-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("embedding-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("internal.example", json, StringComparison.Ordinal);
    }

    // ──────────────────────────────────────────────
    // Reproducibility and durability
    // ──────────────────────────────────────────────

    /// <summary>
    /// Two runs configured identically produce the same digest, which is what lets anyone comparing
    /// two runs know whether they were comparing two pipelines.
    /// </summary>
    [Fact]
    public void TheFingerprintIsStableAcrossRuns()
    {
        var caseFolder = Directory.CreateDirectory(Path.Combine(_folder, "case")).FullName;

        string Digest(int passages) => WorkflowManifest.StartOrResume(
            null,
            new WorkflowContext(caseFolder, "TC-1",
                new AppSettings { MaxPassagesPerGroup = passages }, Path.Combine(_folder, "run")))
            .SettingsDigest;

        Assert.Equal(Digest(24), Digest(24));
        Assert.NotEqual(Digest(24), Digest(12));
    }

    /// <summary>
    /// <b>A manifest that will not parse is treated as absent.</b> The cost of starting again is a
    /// run; the cost of resuming from a state nobody can vouch for is a result nobody can vouch for.
    /// </summary>
    [Fact]
    public void AnUnreadableManifestIsTreatedAsAbsent()
    {
        var path = Path.Combine(Directory.CreateDirectory(_folder).FullName, "broken.json");
        File.WriteAllText(path, "{ this is not json");

        Assert.Null(WorkflowManifest.Read(path));
    }

    [Fact]
    public void AManifestFromAnotherFormatIsRefused()
    {
        var path = Path.Combine(Directory.CreateDirectory(_folder).FullName, "old.json");
        File.WriteAllText(path, """{ "manifestVersion": "0.9", "caseReference": "TC-1" }""");

        Assert.Null(WorkflowManifest.Read(path));
    }

    /// <summary>
    /// The manifest is updated after every stage including the long ones, so a run cancelled
    /// mid-write is not hypothetical — and a truncated manifest is worse than none, because a
    /// resume would read it and believe it.
    /// </summary>
    [Fact]
    public void AWrittenManifestReadsBackWhole()
    {
        var path = Path.Combine(_folder, "run", WorkflowManifest.FileName);

        var manifest = new WorkflowManifest { CaseReference = "TC-1", TenantId = 7 };
        manifest.Stages.Add(new StageRecord(
            WorkflowStageId.Index, StageStatus.Succeeded, "did the work", 1.5,
            DateTimeOffset.Now, new Dictionary<string, string> { ["chunks"] = "585" }));

        manifest.Write(path);

        var read = WorkflowManifest.Read(path);

        Assert.NotNull(read);
        Assert.Equal("TC-1", read.CaseReference);
        Assert.Equal("585", Assert.Single(read.Stages).Detail?["chunks"]);
        Assert.False(File.Exists(path + ".tmp"));
    }

    // ──────────────────────────────────────────────
    // Cost
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>Unknown cost is not zero.</b> A provider that reports no token count has told us nothing,
    /// and a manifest recording 0 would let a report claim the stage was free.
    /// </summary>
    [Fact]
    public void AnUnreportedCostIsNotZero()
    {
        var unknown = StageOutcome.Succeeded(WorkflowStageId.Index, "done", TimeSpan.Zero);
        var known = StageOutcome.Succeeded(
            WorkflowStageId.Index, "done", TimeSpan.Zero, null, new TokenUsage(1000, 0, 0, 0));

        Assert.Null(unknown.Usage);
        Assert.Equal(1000, known.Usage?.InputTokens);
    }

    [Theory]
    [InlineData(StageStatus.Succeeded, true)]
    [InlineData(StageStatus.Skipped, true)]
    [InlineData(StageStatus.Failed, false)]
    [InlineData(StageStatus.Cancelled, false)]
    public void OnlyASucceededOrSkippedStageLetsThePipelineContinue(StageStatus status, bool expected) =>
        Assert.Equal(expected, new StageOutcome(
            WorkflowStageId.Index, status, "x", TimeSpan.Zero).CanContinue);
}
