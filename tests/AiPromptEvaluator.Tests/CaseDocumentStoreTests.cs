using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Round-trips the vector store against a real Qdrant. The payload filtering is the part
/// that decides whether a check sees the right case, and it can only be proved end to end.
///
/// Qdrant is not a build dependency: when nothing is listening the test reports that it
/// skipped rather than failing, so the suite still runs on a machine without Docker.
/// Start one with:
///   docker run -d -p 6333:6333 -p 6334:6334 -v qdrant_storage:/qdrant/storage qdrant/qdrant
/// </summary>
public class CaseDocumentStoreTests
{
    /// <summary>Three dimensions is enough to prove the plumbing and keeps the collection tiny.</summary>
    private const int Dimensions = 3;

    private readonly ITestOutputHelper _output;

    public CaseDocumentStoreTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static AppSettings Settings() => new()
    {
        EmbeddingDimensions = Dimensions,
        TenantId = 99,
        // A collection of its own, so a run never disturbs indexed case data.
        QdrantCollection = "case_documents_tests",
    };

    private static CaseDocumentChunk Chunk(
        string caseReference, string document, string category, int index, string text, int tenant = 99) =>
        new(caseReference, tenant, document, category, DocumentCategory.NameForCode(category), index, text);

    private static ReadOnlyMemory<float> Vector(float x, float y, float z) => new([x, y, z]);

    [Fact]
    public async Task Search_ReturnsMatchesAcrossDocuments_ScopedToOneCaseAndTenant()
    {
        var settings = Settings();
        using var store = new CaseDocumentStore(settings);

        if (!await store.IsAvailableAsync())
        {
            _output.WriteLine($"Skipped: no Qdrant at {store.Endpoint}.");
            return;
        }

        await store.EnsureCollectionAsync(Dimensions);
        await store.DeleteCaseAsync("CASE-A", 99);
        await store.DeleteCaseAsync("CASE-B", 99);
        await store.DeleteCaseAsync("CASE-A", 100);

        // The same point is evidenced in two documents of the case under test, and echoed in
        // a different case and a different tenant that must not leak into the results.
        var chunks = new List<CaseDocumentChunk>
        {
            Chunk("CASE-A", "fact-find.md", "B", 0, "Attitude to risk recorded as balanced."),
            Chunk("CASE-A", "suitability.md", "I", 0, "Recommendation reflects a balanced risk profile."),
            Chunk("CASE-B", "fact-find.md", "B", 0, "Another client, also balanced."),
            Chunk("CASE-A", "fact-find.md", "B", 0, "Same case name, other tenant.", tenant: 100),
        };

        var vectors = new List<ReadOnlyMemory<float>>
        {
            Vector(1f, 0f, 0f),
            Vector(0.9f, 0.1f, 0f),
            Vector(1f, 0f, 0f),
            Vector(1f, 0f, 0f),
        };

        await store.UpsertAsync(chunks, vectors);

        var hits = await store.SearchAsync("CASE-A", 99, Vector(1f, 0f, 0f), limit: 10);

        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal("CASE-A", hit.Chunk.CaseReference));
        Assert.All(hits, hit => Assert.Equal(99, hit.Chunk.TenantId));

        // Both documents come back — a check often needs the corroborating one.
        Assert.Contains(hits, h => h.Chunk.DocumentName == "fact-find.md" && h.Chunk.CategoryCode == "B");
        Assert.Contains(hits, h => h.Chunk.DocumentName == "suitability.md" && h.Chunk.CategoryCode == "I");

        // The metadata a citation is built from survives the round trip.
        var factFind = hits.Single(h => h.Chunk.DocumentName == "fact-find.md");
        Assert.Equal("Know Your Client", factFind.Chunk.CategoryName);
        Assert.Equal("Attitude to risk recorded as balanced.", factFind.Chunk.Text);

        await store.DeleteCaseAsync("CASE-A", 99);
        await store.DeleteCaseAsync("CASE-B", 99);
        await store.DeleteCaseAsync("CASE-A", 100);
    }

    /// <summary>
    /// What "Unload Docs" does: delete by case reference and tenant. Another tenant holding
    /// the same case reference must keep its embeddings.
    /// </summary>
    [Fact]
    public async Task Delete_RemovesOneCaseForOneTenantOnly()
    {
        var settings = Settings();
        using var store = new CaseDocumentStore(settings);

        if (!await store.IsAvailableAsync())
        {
            _output.WriteLine($"Skipped: no Qdrant at {store.Endpoint}.");
            return;
        }

        await store.EnsureCollectionAsync(Dimensions);

        var vector = new List<ReadOnlyMemory<float>> { Vector(1f, 0f, 0f) };
        await store.UpsertAsync([Chunk("CASE-DEL", "a.md", "B", 0, "ours")], vector);
        await store.UpsertAsync([Chunk("CASE-DEL", "a.md", "B", 0, "theirs", tenant: 100)], vector);
        await store.UpsertAsync([Chunk("CASE-KEEP", "a.md", "B", 0, "other case")], vector);

        await store.DeleteCaseAsync("CASE-DEL", 99);

        Assert.Equal(0ul, await store.CountAsync("CASE-DEL", 99));
        Assert.Equal(1ul, await store.CountAsync("CASE-DEL", 100));
        Assert.Equal(1ul, await store.CountAsync("CASE-KEEP", 99));

        await store.DeleteCaseAsync("CASE-DEL", 100);
        await store.DeleteCaseAsync("CASE-KEEP", 99);
    }

    /// <summary>Re-indexing an unchanged case must replace its chunks, not double them.</summary>
    [Fact]
    public async Task Upsert_IsIdempotentForTheSameChunk()
    {
        var settings = Settings();
        using var store = new CaseDocumentStore(settings);

        if (!await store.IsAvailableAsync())
        {
            _output.WriteLine($"Skipped: no Qdrant at {store.Endpoint}.");
            return;
        }

        await store.EnsureCollectionAsync(Dimensions);
        await store.DeleteCaseAsync("CASE-IDEMPOTENT", 99);

        var chunk = Chunk("CASE-IDEMPOTENT", "fact-find.md", "B", 0, "Attitude to risk: balanced.");
        var vector = new List<ReadOnlyMemory<float>> { Vector(1f, 0f, 0f) };

        await store.UpsertAsync([chunk], vector);
        await store.UpsertAsync([chunk], vector);

        Assert.Equal(1ul, await store.CountAsync("CASE-IDEMPOTENT", 99));

        await store.DeleteCaseAsync("CASE-IDEMPOTENT", 99);
        Assert.Equal(0ul, await store.CountAsync("CASE-IDEMPOTENT", 99));
    }
}
