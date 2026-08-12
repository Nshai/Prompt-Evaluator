using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The canonical model store, against a real SQLite file in the temp folder. Unlike the
/// vector store this has no external dependency, so it is always exercised.
/// </summary>
public class CanonicalModelStoreTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"canonical-model-tests-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        // SQLite pools connections, so the file can still be held after the last command.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }

        GC.SuppressFinalize(this);
    }

    private static CanonicalModelDocument Document(
        string caseReference = "CASE-1", int tenantId = 99, string json = """{"modelVersion":"1.0"}""") =>
        new(
            CaseReference: caseReference,
            TenantId: tenantId,
            Json: json,
            ModelId: "gpt-4.1",
            SchemaVersion: "1.0",
            SourceDocuments: ["Suitability Report.md"],
            ExtractedAt: new DateTimeOffset(2026, 8, 12, 9, 30, 0, TimeSpan.Zero),
            Usage: new TokenUsage(1000, 200, 0, 50));

    [Fact]
    public async Task SaveAndLoad_RoundTripsEveryField()
    {
        var store = new CanonicalModelStore(_databasePath);
        var saved = Document();

        await store.SaveAsync(saved);
        var loaded = await store.LoadAsync("CASE-1", 99);

        Assert.NotNull(loaded);
        Assert.Equal(saved.Json, loaded.Json);
        Assert.Equal(saved.ModelId, loaded.ModelId);
        Assert.Equal(saved.SchemaVersion, loaded.SchemaVersion);
        Assert.Equal(saved.SourceDocuments, loaded.SourceDocuments);
        Assert.Equal(saved.ExtractedAt, loaded.ExtractedAt);

        // Cache read and cache write are adjacent columns of the same type — the kind of
        // pair that silently swaps and only shows up as a wrong cost months later.
        Assert.Equal(1000, loaded.Usage.InputTokens);
        Assert.Equal(200, loaded.Usage.OutputTokens);
        Assert.Equal(50, loaded.Usage.CacheReadTokens);
        Assert.Equal(0, loaded.Usage.CacheWriteTokens);
    }

    [Fact]
    public async Task Load_ReturnsNull_WhenNothingExtracted()
    {
        var store = new CanonicalModelStore(_databasePath);

        Assert.Null(await store.LoadAsync("NOT-EXTRACTED", 99));
        Assert.False(await store.ExistsAsync("NOT-EXTRACTED", 99));
    }

    /// <summary>
    /// Re-extracting is how a bad parse is corrected, so a second save has to replace the
    /// first rather than leaving a check to choose between two models for one case.
    /// </summary>
    [Fact]
    public async Task Save_ReplacesTheModelForTheSameCase()
    {
        var store = new CanonicalModelStore(_databasePath);

        await store.SaveAsync(Document(json: """{"modelVersion":"1.0","first":true}"""));
        await store.SaveAsync(Document(json: """{"modelVersion":"1.0","second":true}"""));

        var loaded = await store.LoadAsync("CASE-1", 99);

        Assert.NotNull(loaded);
        Assert.Contains("second", loaded.Json);
        Assert.DoesNotContain("first", loaded.Json);
    }

    /// <summary>
    /// The same case reference under two tenants is two different cases. Getting this wrong
    /// would leak one tenant's extracted client data into another's assessment.
    /// </summary>
    [Fact]
    public async Task Models_AreScopedByTenant()
    {
        var store = new CanonicalModelStore(_databasePath);

        await store.SaveAsync(Document(tenantId: 1, json: """{"modelVersion":"1.0","tenant":1}"""));
        await store.SaveAsync(Document(tenantId: 2, json: """{"modelVersion":"1.0","tenant":2}"""));

        Assert.Contains("\"tenant\":1", (await store.LoadAsync("CASE-1", 1))!.Json);
        Assert.Contains("\"tenant\":2", (await store.LoadAsync("CASE-1", 2))!.Json);

        await store.DeleteAsync("CASE-1", 1);

        Assert.Null(await store.LoadAsync("CASE-1", 1));
        Assert.NotNull(await store.LoadAsync("CASE-1", 2));
    }

    [Fact]
    public async Task Delete_ReportsWhetherAnythingWasThere()
    {
        var store = new CanonicalModelStore(_databasePath);
        await store.SaveAsync(Document());

        Assert.True(await store.DeleteAsync("CASE-1", 99));
        Assert.False(await store.DeleteAsync("CASE-1", 99));
    }

    /// <summary>Case references come from folder names, where casing is not meaningful.</summary>
    [Fact]
    public async Task CaseReference_IsMatchedCaseInsensitively()
    {
        var store = new CanonicalModelStore(_databasePath);
        await store.SaveAsync(Document(caseReference: "Case-Alpha"));

        Assert.True(await store.ExistsAsync("case-alpha", 99));
        Assert.NotNull(await store.LoadAsync("CASE-ALPHA", 99));
    }
}
