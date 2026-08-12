using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.ML.Tokenizers;

namespace AiPromptEvaluator;

/// <summary>What one "Load Docs" produced.</summary>
public sealed record CaseIndexResult(
    string CaseReference,
    int TenantId,
    int Documents,
    int Chunks,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<(string Document, string Error)> Failures);

/// <summary>
/// Indexes a case folder's Markdown files into the vector store so a check can retrieve
/// passages instead of attaching whole documents.
///
/// Splitting is semantic rather than fixed-width: <see cref="SemanticSimilarityChunker"/>
/// embeds the document's elements and cuts where the topic shifts, which keeps a finding
/// and its supporting sentence in the same chunk. Every chunk is stamped with the case
/// reference, tenant, document name and category so the search tool can filter and cite.
/// </summary>
public sealed class CaseDocumentIndexer
{
    /// <summary>
    /// How many chunk texts are embedded per request. Large enough to keep the round-trip
    /// count down on a case folder of a few hundred documents, small enough to stay inside
    /// per-request input limits.
    /// </summary>
    private const int EmbeddingBatchSize = 64;

    private readonly AppSettings _settings;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddings;
    private readonly CaseDocumentStore _store;

    public CaseDocumentIndexer(
        AppSettings settings,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        CaseDocumentStore store)
    {
        _settings = settings;
        _embeddings = embeddings;
        _store = store;
    }

    /// <summary>Markdown is the only format indexed — everything else is reported as skipped.</summary>
    public static bool IsIndexable(string filePath) =>
        Path.GetExtension(filePath).Equals(".md", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(filePath).Equals(".markdown", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The case folder's own name, used as the case reference when Settings doesn't specify
    /// one. Callers should go through <see cref="AppSettings.ResolveCaseReference"/> rather
    /// than calling this directly, so a configured reference is honoured.
    /// </summary>
    public static string CaseReferenceFor(string caseFolder) =>
        new DirectoryInfo(caseFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name;

    /// <summary>The first path segment below the case folder, e.g. "A" for caseFolder/A/x.md.</summary>
    public static string CategoryCodeFor(string caseFolder, string filePath)
    {
        var relative = Path.GetRelativePath(caseFolder, filePath);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Length > 1 ? segments[0] : string.Empty;
    }

    /// <summary>
    /// Chunks and indexes every Markdown file under <paramref name="caseFolder"/>.
    /// Previously indexed chunks for the same case are cleared first, so a reload after an
    /// edit doesn't leave stale passages behind for the model to cite.
    ///
    /// One document failing is recorded and the load continues — a single unreadable file
    /// shouldn't cost the user the rest of the case.
    /// </summary>
    public async Task<CaseIndexResult> IndexAsync(
        string caseFolder,
        string caseReference,
        IProgress<CaseIndexProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(caseFolder))
        {
            throw new DirectoryNotFoundException($"Case folder not found: {caseFolder}");
        }

        var allFiles = Directory
            .GetFiles(caseFolder, "*.*", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var markdown = allFiles.Where(IsIndexable).ToList();
        var skipped = allFiles.Where(f => !IsIndexable(f)).Select(Path.GetFileName).Select(n => n!).ToList();

        // Prove the embedding endpoint answers, and answers with the width the collection is
        // built for, before touching the store. Chunking embeds as it goes, so without this
        // a misconfigured endpoint produces one identical failure per document and no index.
        await VerifyEmbeddingsAsync(cancellationToken).ConfigureAwait(false);

        await _store.EnsureCollectionAsync(_settings.EmbeddingDimensions, cancellationToken).ConfigureAwait(false);
        await _store.DeleteCaseAsync(caseReference, _settings.TenantId, cancellationToken).ConfigureAwait(false);

        var reader = new MarkdownReader();
        var chunker = CreateChunker();

        var failures = new List<(string Document, string Error)>();
        var totalChunks = 0;
        var done = 0;

        foreach (var file in markdown)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var startedAt = Stopwatch.GetTimestamp();
            var documentName = Path.GetFileName(file);
            var code = CategoryCodeFor(caseFolder, file);
            var chunkCount = 0;
            string? error = null;

            try
            {
                var chunks = await ChunkAsync(reader, chunker, file, caseReference, code, cancellationToken)
                    .ConfigureAwait(false);

                await IndexChunksAsync(chunks, cancellationToken).ConfigureAwait(false);

                chunkCount = chunks.Count;
                totalChunks += chunkCount;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                error = ex.Message.Trim();
                failures.Add((documentName, error));
            }

            done++;
            progress?.Report(new CaseIndexProgress(
                done, markdown.Count, documentName, code, chunkCount,
                Stopwatch.GetElapsedTime(startedAt), error));
        }

        return new CaseIndexResult(
            caseReference, _settings.TenantId, markdown.Count - failures.Count, totalChunks, skipped, failures);
    }

    /// <summary>
    /// Reads one Markdown file and cuts it into chunks, each carrying its case, tenant,
    /// document and category. The document's heading context is prefixed to the chunk text
    /// so a passage lifted out of the middle of a report still says what it is about.
    /// </summary>
    private async Task<IReadOnlyList<CaseDocumentChunk>> ChunkAsync(
        MarkdownReader reader,
        IngestionChunker<string> chunker,
        string filePath,
        string caseReference,
        string categoryCode,
        CancellationToken cancellationToken)
    {
        var documentName = Path.GetFileName(filePath);

        var document = await ReadDocumentAsync(reader, filePath, cancellationToken).ConfigureAwait(false);

        var chunks = new List<CaseDocumentChunk>();
        var index = 0;

        await foreach (var chunk in chunker.ProcessAsync(document, cancellationToken).ConfigureAwait(false))
        {
            var text = Compose(chunk.Context, chunk.Content);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            chunks.Add(new CaseDocumentChunk(
                CaseReference: caseReference,
                TenantId: _settings.TenantId,
                DocumentName: documentName,
                CategoryCode: categoryCode,
                CategoryName: DocumentCategory.NameForCode(categoryCode),
                ChunkIndex: index++,
                Text: text));
        }

        return chunks;
    }

    /// <summary>
    /// Parses one Markdown file into the structure the chunker walks.
    ///
    /// Two things get in the way of real-world converted documents. HTML entities
    /// (<c>&amp;amp;</c>, <c>&amp;lt;</c>, <c>&amp;#124;</c>) parse to an inline node the reader
    /// rejects outright, so they are decoded to the characters they stand for first — which is
    /// what the text meant anyway. Anything the reader still refuses falls back to treating the
    /// file as plain text: a document indexed as flat paragraphs is worth far more to a search
    /// than a document dropped from the case entirely.
    /// </summary>
    public static async Task<IngestionDocument> ReadDocumentAsync(
        MarkdownReader reader, string filePath, CancellationToken cancellationToken = default)
    {
        var documentName = Path.GetFileName(filePath);
        var markdown = WebUtility.HtmlDecode(
            await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false));

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
            return await reader
                .ReadAsync(stream, documentName, "text/markdown", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return AsPlainText(documentName, markdown);
        }
    }

    /// <summary>
    /// The last-resort document: blank-line-separated blocks as paragraphs, structure
    /// discarded. The semantic chunker still groups them by meaning.
    /// </summary>
    private static IngestionDocument AsPlainText(string documentName, string markdown)
    {
        var document = new IngestionDocument(documentName);
        var section = new IngestionDocumentSection();

        foreach (var block in markdown.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var text = block.Trim();
            if (text.Length > 0)
            {
                section.Elements.Add(new IngestionDocumentParagraph(text));
            }
        }

        if (section.Elements.Count == 0)
        {
            throw new InvalidOperationException("The file is empty.");
        }

        document.Sections.Add(section);
        return document;
    }

    /// <summary>
    /// One probe embedding before any work starts. It catches the two configuration mistakes
    /// that otherwise surface as a wall of identical per-document failures: an endpoint or
    /// model that isn't there, and a model whose vectors are a different width than the
    /// collection was created for.
    /// </summary>
    private async Task VerifyEmbeddingsAsync(CancellationToken cancellationToken)
    {
        ReadOnlyMemory<float> probe;
        try
        {
            probe = await _embeddings
                .GenerateVectorAsync("case document", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var baseUrl = _settings.ResolveBaseUrl();

            // A 404 here is nearly always the base URL missing its version segment: the client
            // appends "/embeddings" to whatever is configured, so ".../v1" is what it needs.
            var hint = ex.Message.Contains("404", StringComparison.Ordinal)
                ? $"\n\nA 404 usually means the base URL is missing its version segment — try "
                  + $"\"{baseUrl.TrimEnd('/')}/v1\" — or that the gateway does not serve this "
                  + "embedding model."
                : string.Empty;

            throw new InvalidOperationException(
                $"The embedding model \"{_settings.EmbeddingModel}\" could not be reached at "
                + $"{baseUrl}. Check the base URL, API key and embedding model in Settings."
                + $"{hint}\n\n{ex.Message.Trim()}", ex);
        }

        if (probe.Length != _settings.EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"\"{_settings.EmbeddingModel}\" returns {probe.Length}-dimension vectors, but the "
                + $"configured embedding dimensions are {_settings.EmbeddingDimensions}. Set the "
                + "dimensions to match in Settings; if the collection already exists at the old "
                + "width, unload the case or use a new collection.");
        }
    }

    /// <summary>Embeds the chunk texts in batches and writes them to the vector store.</summary>
    private async Task IndexChunksAsync(
        IReadOnlyList<CaseDocumentChunk> chunks, CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < chunks.Count; offset += EmbeddingBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = chunks.Skip(offset).Take(EmbeddingBatchSize).ToList();
            var embeddings = await _embeddings
                .GenerateAsync(batch.Select(c => c.Text).ToList(), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await _store
                .UpsertAsync(batch, embeddings.Select(e => e.Vector).ToList(), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>How the documents are split, for the load report.</summary>
    public string ChunkingDescription =>
        $"semantic similarity ({_settings.EmbeddingModel}), max {Math.Max(64, _settings.MaxTokensPerChunk)} "
        + $"tokens per chunk, {_settings.ChunkOverlapTokens} overlap";

    /// <summary>
    /// The semantic chunker, driven by the same embedding generator the search uses — the
    /// cut points and the query therefore live in the same vector space.
    ///
    /// Public so a test can assert the strategy: a fixed-width splitter would still index
    /// and still search, and the regression would only show up as worse retrieval.
    /// </summary>
    public IngestionChunker<string> CreateChunker()
    {
        var options = new IngestionChunkerOptions(TiktokenTokenizer.CreateForEncoding("o200k_base"))
        {
            MaxTokensPerChunk = Math.Max(64, _settings.MaxTokensPerChunk),
            OverlapTokens = Math.Clamp(_settings.ChunkOverlapTokens, 0, Math.Max(64, _settings.MaxTokensPerChunk) - 1),
        };

        return new SemanticSimilarityChunker(_embeddings, options);
    }

    private static string Compose(string? context, string content) =>
        string.IsNullOrWhiteSpace(context) ? content.Trim() : $"{context.Trim()}\n\n{content.Trim()}";
}
