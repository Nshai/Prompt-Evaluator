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

    /// <summary>
    /// Filtering by document category, which is what lets a small category be found at all.
    ///
    /// Unfiltered, a search competes across the whole case file, so a category holding one short
    /// file note loses every time to the long policy documents beside it — in a real run the
    /// meetings-and-communications category reached five of fifty-seven requirement groups while
    /// sixteen asked for it, and the file note it holds was the decisive evidence for three
    /// checks. The scenario here is that shape in miniature: one weakly-matching note in
    /// category C against three strong matches in category E.
    /// </summary>
    [Fact]
    public async Task Search_CanBeRestrictedToDocumentCategories()
    {
        var settings = Settings();
        using var store = new CaseDocumentStore(settings);

        if (!await store.IsAvailableAsync())
        {
            _output.WriteLine($"Skipped: no Qdrant at {store.Endpoint}.");
            return;
        }

        await store.EnsureCollectionAsync(Dimensions);
        await store.DeleteCaseAsync("CASE-CAT", 99);

        var chunks = new List<CaseDocumentChunk>
        {
            Chunk("CASE-CAT", "policy-1.md", "E", 0, "Policy charges and fund details."),
            Chunk("CASE-CAT", "policy-2.md", "E", 0, "More policy charges."),
            Chunk("CASE-CAT", "policy-3.md", "E", 0, "Yet more policy charges."),
            Chunk("CASE-CAT", "file-note.md", "C", 0, "We agreed a risk rating of 5 rather than 4."),
        };

        // The file note is the weakest match by a distance no re-ranking could close.
        var vectors = new List<ReadOnlyMemory<float>>
        {
            Vector(1f, 0f, 0f),
            Vector(0.99f, 0.1f, 0f),
            Vector(0.98f, 0.2f, 0f),
            Vector(0f, 1f, 0f),
        };

        await store.UpsertAsync(chunks, vectors);

        // Unfiltered and capped at three, the file note never appears — this is the defect.
        var unfiltered = await store.SearchAsync("CASE-CAT", 99, Vector(1f, 0f, 0f), limit: 3);
        Assert.DoesNotContain(unfiltered, h => h.Chunk.CategoryCode == "C");

        // Asked for category C, it is the only thing that can come back.
        var targeted = await store.SearchAsync("CASE-CAT", 99, Vector(1f, 0f, 0f), limit: 3, ["C"]);
        Assert.NotEmpty(targeted);
        Assert.All(targeted, h => Assert.Equal("C", h.Chunk.CategoryCode));
        Assert.Contains(targeted, h => h.Chunk.Text.Contains("rating of 5", StringComparison.Ordinal));

        // Several categories at once, as a plan's targetCategories usually names.
        var both = await store.SearchAsync("CASE-CAT", 99, Vector(1f, 0f, 0f), limit: 10, ["C", "E"]);
        Assert.Equal(4, both.Count);

        // An empty filter is no filter, not a filter matching nothing.
        var unrestricted = await store.SearchAsync("CASE-CAT", 99, Vector(1f, 0f, 0f), limit: 10, []);
        Assert.Equal(4, unrestricted.Count);

        // A category nothing is filed under returns nothing rather than falling back.
        var absent = await store.SearchAsync("CASE-CAT", 99, Vector(1f, 0f, 0f), limit: 10, ["Z"]);
        Assert.Empty(absent);

        await store.DeleteCaseAsync("CASE-CAT", 99);
    }

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
