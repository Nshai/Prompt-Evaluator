using System.IO;

using AiPromptEvaluator;

using IQWorkflow;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;

using Xunit;

namespace IQWorkflow.Tests;

/// <summary>
/// The stages themselves, driven directly.
///
/// <b>No engine, no Qdrant, no model, no network.</b> A stage takes a context and returns an
/// outcome; that is the whole of its contract, and it is why the orchestration can be swapped —
/// sequential runner, Agent Framework graph — without touching any of this.
///
/// <b>Most of the code in a stage is its failure paths</b>, and those are what these cover: the
/// store that is down, the case folder with no report in it, the run with nothing to report. A
/// stage tested only on its happy path is a stage whose behaviour on a real case file is unknown.
/// </summary>
public sealed class StageTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "iqworkflow-stages", Guid.NewGuid().ToString("N"));

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

    private WorkflowContext Context(string? caseFolder = null) => new(
        caseFolder ?? Directory.CreateDirectory(Path.Combine(_folder, "case")).FullName,
        "TC-1",
        new AppSettings { TenantId = 7 },
        Path.Combine(_folder, "run"));

    private static CanonicalModelDocument Model(string json = """{"case":{"caseReference":"TC-1"}}""") =>
        new("TC-1", 7, json, "test-model", "1.0", ["Suitability report.md"],
            DateTimeOffset.Now, TokenUsage.Empty);

    // ──────────────────────────────────────────────
    // Index (S2)
    // ──────────────────────────────────────────────

    private sealed class FakeStore(bool available, ulong indexed) : ICaseDocumentStore
    {
        public string Endpoint => "http://localhost:6334";

        public string Collection => "case_documents";

        public Task<bool> IsAvailableAsync(CancellationToken t = default) => Task.FromResult(available);

        public Task EnsureCollectionAsync(int dimensions, CancellationToken t = default) => Task.CompletedTask;

        public Task UpsertAsync(
            IReadOnlyList<CaseDocumentChunk> chunks,
            IReadOnlyList<ReadOnlyMemory<float>> vectors,
            CancellationToken t = default) => Task.CompletedTask;

        public Task<IReadOnlyList<CaseDocumentSearchResult>> SearchAsync(
            string caseReference, int tenantId, ReadOnlyMemory<float> queryVector, int limit,
            IReadOnlyCollection<string>? categoryCodes = null, string? queryText = null,
            CancellationToken t = default) =>
            Task.FromResult<IReadOnlyList<CaseDocumentSearchResult>>([]);

        public Task<bool> DeleteCaseAsync(string caseReference, int tenantId, CancellationToken t = default) =>
            Task.FromResult(true);

        public Task<ulong> CountAsync(string caseReference, int tenantId, CancellationToken t = default) =>
            Task.FromResult(indexed);

        public void Dispose()
        {
        }
    }

    private sealed class FakeStoreFactory(bool available = true, ulong indexed = 0) : ICaseDocumentStoreFactory
    {
        public ICaseDocumentStore Create(AppSettings? settings = null) => new FakeStore(available, indexed);
    }

    private sealed class FakeIndexer(int documents, int chunks) : ICaseDocumentIndexer
    {
        public string ChunkingDescription => "fake chunking";

        public IngestionChunker<string> CreateChunker() => throw new NotSupportedException();

        public Task<CaseIndexResult> IndexAsync(
            string caseFolder, string caseReference,
            IProgress<CaseIndexProgress>? progress = null, CancellationToken t = default) =>
            Task.FromResult(new CaseIndexResult("TC-1", 7, documents, chunks, [], []));
    }

    private static IEmbeddingGenerator<string, Embedding<float>> Embeddings() => new StubEmbeddings();

    private sealed class StubEmbeddings : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values, EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(
                values.Select(_ => new Embedding<float>(new float[] { 0.1f })).ToList()));

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private IndexCaseStage IndexStage(
        bool storeAvailable = true, ulong alreadyIndexed = 0, int documents = 3, int chunks = 40) =>
        new(new FakeStoreFactory(storeAvailable, alreadyIndexed),
            _ => Embeddings(),
            (_, _, _) => new FakeIndexer(documents, chunks));

    [Fact]
    public async Task IndexReportsWhatItIndexed()
    {
        var outcome = await IndexStage().ExecuteAsync(Context());

        Assert.Equal(StageStatus.Succeeded, outcome.Status);
        Assert.Equal("40", outcome.Detail?["chunks"]);
        Assert.Equal("3", outcome.Detail?["documents"]);
    }

    /// <summary>
    /// Checked before anything is embedded. Discovering the store is down after paying for the
    /// embeddings would be paying for work nothing can use.
    /// </summary>
    [Fact]
    public async Task IndexFailsWhenTheStoreIsDownRatherThanEmbedding()
    {
        var outcome = await IndexStage(storeAvailable: false).ExecuteAsync(Context());

        Assert.Equal(StageStatus.Failed, outcome.Status);
        Assert.Contains("not responding", outcome.Summary);
    }

    /// <summary>
    /// <b>A case that indexed nothing is a failure however cleanly it ran.</b> Every later stage
    /// would retrieve nothing and report it as the case file being silent — a different and much
    /// more damaging claim than "the documents were never indexed".
    /// </summary>
    [Fact]
    public async Task IndexingNothingIsAFailure()
    {
        var outcome = await IndexStage(documents: 0, chunks: 0).ExecuteAsync(Context());

        Assert.Equal(StageStatus.Failed, outcome.Status);
        Assert.Contains("No passages were indexed", outcome.Summary);
    }

    [Fact]
    public async Task IndexIsSatisfiedWhenTheCaseIsAlreadyIndexed()
    {
        Assert.True(await IndexStage(alreadyIndexed: 585).IsSatisfiedAsync(Context()));
        Assert.False(await IndexStage(alreadyIndexed: 0).IsSatisfiedAsync(Context()));
    }

    /// <summary>An unreachable store is not "already indexed" — it is nothing known at all.</summary>
    [Fact]
    public async Task IndexIsNotSatisfiedWhenTheStoreIsUnreachable() =>
        Assert.False(await IndexStage(storeAvailable: false, alreadyIndexed: 585)
            .IsSatisfiedAsync(Context()));

    // ──────────────────────────────────────────────
    // Extract (S1)
    // ──────────────────────────────────────────────

    private sealed class FakeModelStore(CanonicalModelDocument? stored = null) : ICanonicalModelStore
    {
        public string DatabasePath => "(memory)";

        public CanonicalModelDocument? Saved { get; private set; }

        public Task SaveAsync(CanonicalModelDocument document, CancellationToken t = default)
        {
            Saved = document;
            return Task.CompletedTask;
        }

        public Task<CanonicalModelDocument?> LoadAsync(string caseReference, int tenantId, CancellationToken t = default) =>
            Task.FromResult(stored);

        public Task<bool> ExistsAsync(string caseReference, int tenantId, CancellationToken t = default) =>
            Task.FromResult(stored is not null);

        public Task<bool> DeleteAsync(string caseReference, int tenantId, CancellationToken t = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeExtractor : ICanonicalModelExtractor
    {
        public int Calls { get; private set; }

        public Task<ExtractionResult> ExtractAsync(
            string caseFolder, string caseReference,
            IProgress<ExtractionProgress>? progress = null, PromptLogWriter? promptLog = null,
            CancellationToken t = default)
        {
            Calls++;

            return Task.FromResult(new ExtractionResult(
                Model(), [], CostBreakdown.Create("test", new TokenUsage(1000, 200, 0, 0))));
        }
    }

    /// <summary>
    /// <b>The largest saving the workflow makes on a repeated run.</b> Re-extracting to produce a
    /// model that already exists is paying twice for the same document, and it is what makes four
    /// assessment configurations over one extraction affordable.
    /// </summary>
    [Fact]
    public async Task ExtractReusesAStoredModelRatherThanReExtracting()
    {
        var extractor = new FakeExtractor();
        var context = Context();

        var satisfied = await new ExtractModelStage(extractor, new FakeModelStore(Model()))
            .IsSatisfiedAsync(context);

        Assert.True(satisfied);
        Assert.Equal(0, extractor.Calls);

        // And the model is put on the context, because a later stage needs the model itself and
        // not merely the knowledge that one exists.
        Assert.NotNull(context.Model);
    }

    [Fact]
    public async Task ExtractIsNotSatisfiedWithoutAStoredModel() =>
        Assert.False(await new ExtractModelStage(new FakeExtractor(), new FakeModelStore())
            .IsSatisfiedAsync(Context()));

    /// <summary>
    /// Refused before twelve model passes rather than after: a case folder with no report in it
    /// cannot produce a model however well the extractor works.
    /// </summary>
    [Fact]
    public async Task ExtractFailsFastWhenThereIsNoReport()
    {
        var extractor = new FakeExtractor();

        var outcome = await new ExtractModelStage(extractor, new FakeModelStore())
            .ExecuteAsync(Context());

        Assert.Equal(StageStatus.Failed, outcome.Status);
        Assert.Contains("No suitability report", outcome.Summary);
        Assert.Equal(0, extractor.Calls);
    }

    // ──────────────────────────────────────────────
    // Report (S5)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Refused rather than writing an empty report. A report with no run behind it is a document
    /// that looks like a result.
    /// </summary>
    [Fact]
    public async Task ReportRefusesWithoutAnAssessedRun()
    {
        var outcome = await new ReportStage().ExecuteAsync(Context());

        Assert.Equal(StageStatus.Failed, outcome.Status);
        Assert.Contains("no assessed run", outcome.Summary);
    }

    [Fact]
    public async Task ReportWritesItsArtefactsIntoTheRunFolder()
    {
        var context = Context();
        context.Model = Model();
        context.RunRecord = new CheckRunRecord(
            "run-1", "TC-1", 7, "test-model", DateTimeOffset.Now, DateTimeOffset.Now,
            "fingerprint", "off", context.Model.Json, "test-model", "1.0",
            ["Suitability report.md"], []);

        var outcome = await new ReportStage().ExecuteAsync(context);

        Assert.Equal(StageStatus.Succeeded, outcome.Status);
        Assert.True(File.Exists(Path.Combine(context.RunFolder, ReportStage.ReportFileName)));
        Assert.True(File.Exists(Path.Combine(context.RunFolder, ReportStage.ModelFileName)));

        // No temporary left behind to be mistaken for an artefact.
        Assert.Empty(Directory.GetFiles(context.RunFolder, "*.tmp"));
    }

    // ──────────────────────────────────────────────
    // Score
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>Most cases have no benchmark and never will.</b> A run over a real client file is not
    /// scored against anything, and that must not be the reason a run reports failure.
    /// </summary>
    [Fact]
    public async Task ScoreSucceedsWhenThereIsNoBenchmark()
    {
        var outcome = await new ScoreBenchmarkStage().ExecuteAsync(Context());

        Assert.Equal(StageStatus.Succeeded, outcome.Status);
        Assert.Contains("No benchmark found", outcome.Summary);
    }

    /// <summary>
    /// The objective the stage exists for: a run reports hits, not only cost. Whether a candidate
    /// *is* the benchmark's finding stays a human judgement — what is counted is whether the check
    /// that owns each row ran and produced something to adjudicate.
    /// </summary>
    [Fact]
    public async Task ScoreProducesAWorksheetWhenABenchmarkIsPresent()
    {
        var caseFolder = Directory.CreateDirectory(Path.Combine(_folder, "scored")).FullName;

        await File.WriteAllTextAsync(
            Path.Combine(caseFolder, ScoreBenchmarkStage.BenchmarkFileName),
            // The shape the scorer reads: id, summary, severity. The check the row belongs to comes
            // from the id itself — F1.1 is CHK-001 — which is why the benchmark does not repeat it.
            """
            | Id | Finding | Severity |
            | --- | --- | --- |
            | F1.1 | The report states a charge the file contradicts | High |
            | F1.2 | A second expected finding on the same check | Moderate |
            """);

        var context = Context(caseFolder);
        context.Findings = [CheckFinding.Failed("CHK-001", "Charges", "not assessed", TimeSpan.Zero)];

        var outcome = await new ScoreBenchmarkStage().ExecuteAsync(context);

        Assert.Equal(StageStatus.Succeeded, outcome.Status);
        Assert.True(File.Exists(Path.Combine(context.RunFolder, ScoreBenchmarkStage.WorksheetFileName)));
        Assert.Equal(ScoreBenchmarkStage.BenchmarkFileName, outcome.Detail?["benchmark"]);
    }

    /// <summary>A benchmark cannot be read from outside the case folder.</summary>
    [Fact]
    public async Task ScoreDoesNotReachOutsideTheCaseFolder()
    {
        var context = Context();
        context.Findings = [CheckFinding.Failed("CHK-001", "x", "y", TimeSpan.Zero)];

        var outcome = await new ScoreBenchmarkStage().ExecuteAsync(context);

        Assert.Equal(StageStatus.Succeeded, outcome.Status);
        Assert.Contains("No benchmark found", outcome.Summary);
    }
}
