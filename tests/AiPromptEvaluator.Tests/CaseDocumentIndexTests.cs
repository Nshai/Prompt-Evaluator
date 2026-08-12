using AiPromptEvaluator;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DataIngestion.Chunkers;

using Xunit;

namespace AiPromptEvaluator.Tests;

public class CaseDocumentIndexTests
{
    private static CaseDocumentChunk Chunk(int index = 0, string document = "fact-find.md") =>
        new("CASE-001", 99, document, "B", "Know Your Client", index, "Attitude to risk: balanced.");

    /// <summary>Only Markdown is indexed — anything else needs converting first.</summary>
    [Theory]
    [InlineData(@"C:\case\B\fact-find.md", true)]
    [InlineData(@"C:\case\B\FACT-FIND.MD", true)]
    [InlineData(@"C:\case\B\notes.markdown", true)]
    [InlineData(@"C:\case\B\holdings.xlsx", false)]
    [InlineData(@"C:\case\B\report.pdf", false)]
    [InlineData(@"C:\case\B\letter.docx", false)]
    public void IsIndexable_AcceptsOnlyMarkdown(string path, bool expected) =>
        Assert.Equal(expected, CaseDocumentIndexer.IsIndexable(path));

    /// <summary>The category is the sub-folder the document is filed under.</summary>
    [Fact]
    public void CategoryCode_IsTheFirstSegmentBelowTheCaseFolder()
    {
        var caseFolder = Path.Combine("C:", "cases", "CASE-001");

        Assert.Equal("B", CaseDocumentIndexer.CategoryCodeFor(
            caseFolder, Path.Combine(caseFolder, "B", "fact-find.md")));

        Assert.Equal("I", CaseDocumentIndexer.CategoryCodeFor(
            caseFolder, Path.Combine(caseFolder, "I", "nested", "advice.md")));
    }

    /// <summary>A document sitting at the case root has no category, not a made-up one.</summary>
    [Fact]
    public void CategoryCode_IsEmptyForDocumentsAtTheCaseRoot()
    {
        var caseFolder = Path.Combine("C:", "cases", "CASE-001");

        Assert.Equal(string.Empty, CaseDocumentIndexer.CategoryCodeFor(
            caseFolder, Path.Combine(caseFolder, "summary.md")));
    }

    [Fact]
    public void CaseReference_IsTheCaseFolderName_WithOrWithoutATrailingSeparator()
    {
        var caseFolder = Path.Combine("C:", "cases", "CASE-001");

        Assert.Equal("CASE-001", CaseDocumentIndexer.CaseReferenceFor(caseFolder));
        Assert.Equal("CASE-001", CaseDocumentIndexer.CaseReferenceFor(caseFolder + Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// A configured case reference overrides the folder name — a working copy called
    /// "case-files" would otherwise be indexed and searched under that name.
    /// </summary>
    [Fact]
    public void ResolveCaseReference_PrefersTheConfiguredValue()
    {
        var caseFolder = Path.Combine("C:", "cases", "case-files");

        Assert.Equal("CASE-001", new AppSettings { CaseReference = "CASE-001" }.ResolveCaseReference(caseFolder));
        Assert.Equal("CASE-001", new AppSettings { CaseReference = "  CASE-001  " }.ResolveCaseReference(caseFolder));
    }

    /// <summary>Left blank, the folder name still decides — nothing has to be configured to work.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveCaseReference_FallsBackToTheFolderName(string configured)
    {
        var caseFolder = Path.Combine("C:", "cases", "CASE-042");

        Assert.Equal("CASE-042", new AppSettings { CaseReference = configured }.ResolveCaseReference(caseFolder));
    }

    /// <summary>
    /// Point ids come from the chunk's natural key, so re-indexing an unchanged case
    /// overwrites its rows instead of doubling them.
    /// </summary>
    [Fact]
    public void PointId_IsStableForTheSameChunk()
    {
        Assert.Equal(Chunk().PointId, Chunk().PointId);
    }

    [Fact]
    public void PointId_DiffersPerChunkAndPerDocument()
    {
        Assert.NotEqual(Chunk(index: 0).PointId, Chunk(index: 1).PointId);
        Assert.NotEqual(Chunk(document: "fact-find.md").PointId, Chunk(document: "suitability.md").PointId);
    }

    /// <summary>Two tenants indexing the same case name must not collide on ids.</summary>
    [Fact]
    public void PointId_DiffersPerTenant()
    {
        var mine = Chunk();
        var theirs = mine with { TenantId = 100 };

        Assert.NotEqual(mine.PointId, theirs.PointId);
    }

    /// <summary>
    /// Retrieval quality rests on where the documents were cut. A fixed-width splitter would
    /// index and search just as happily, so pin the strategy: chunks are cut on semantic
    /// similarity, using the same embedding generator the search queries with.
    /// </summary>
    [Fact]
    public void Indexing_UsesSemanticChunking_DrivenByTheEmbeddingGenerator()
    {
        var settings = new AppSettings { MaxTokensPerChunk = 400, ChunkOverlapTokens = 50 };
        var embeddings = new StubEmbeddingGenerator();
        using var store = new CaseDocumentStore(settings);

        var indexer = new CaseDocumentIndexer(settings, embeddings, store);

        Assert.IsType<SemanticSimilarityChunker>(indexer.CreateChunker());
        Assert.Contains("semantic similarity", indexer.ChunkingDescription, StringComparison.Ordinal);
    }

    /// <summary>Overlap can't reach the chunk size, or a chunk would repeat the previous one whole.</summary>
    [Fact]
    public void Chunking_KeepsOverlapBelowTheChunkSize()
    {
        var settings = new AppSettings { MaxTokensPerChunk = 128, ChunkOverlapTokens = 9_000 };
        using var store = new CaseDocumentStore(settings);

        var indexer = new CaseDocumentIndexer(settings, new StubEmbeddingGenerator(), store);

        Assert.NotNull(indexer.CreateChunker());
    }

    [Theory]
    [InlineData("B", "Know Your Client")]
    [InlineData("I", "Recommendations & Advice")]
    [InlineData("", DocumentCategory.Uncategorised)]
    [InlineData("Z", "Z")]
    public void CategoryNames_MapCodesAndPassUnknownCodesThrough(string code, string expected) =>
        Assert.Equal(expected, DocumentCategory.NameForCode(code));

    /// <summary>
    /// Converted case documents are full of HTML entities — "Smith &amp; Jones", "&lt;1 year",
    /// escaped pipes in tables. The Markdown reader rejects the inline node they parse to, so
    /// they are decoded before parsing. Without this, most of a real case folder fails to index.
    /// </summary>
    [Fact]
    public async Task ReadDocument_HandlesHtmlEntities()
    {
        var path = Path.Combine(Path.GetTempPath(), $"entities-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path,
            """
            # Pension &amp; Investment Review

            Held for &lt;1 year with Smith &amp; Jones.

            | Provider | Charge &#124; Notes |
            | --- | --- |
            | Aviva | 0.45% |
            """);

        try
        {
            var document = await CaseDocumentIndexer.ReadDocumentAsync(new MarkdownReader(), path);

            var text = string.Join("\n", document.EnumerateContent().Select(e => e.GetMarkdown()));

            Assert.Contains("Pension & Investment Review", text, StringComparison.Ordinal);
            Assert.Contains("Smith & Jones", text, StringComparison.Ordinal);
            Assert.DoesNotContain("&amp;", text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>An empty file has nothing to index and should say so, not index a blank chunk.</summary>
    [Fact]
    public async Task ReadDocument_RejectsAnEmptyFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"empty-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "   \n\n  ");

        try
        {
            var document = await CaseDocumentIndexer.ReadDocumentAsync(new MarkdownReader(), path);
            Assert.Empty(document.EnumerateContent());
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Stands in for the embedding service; these tests never leave the process.</summary>
    private sealed class StubEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(
                values.Select(_ => new Embedding<float>(new float[] { 1f, 0f, 0f })).ToList()));

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
