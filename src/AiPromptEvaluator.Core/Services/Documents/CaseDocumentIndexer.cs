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
public sealed class CaseDocumentIndexer : ICaseDocumentIndexer
{
    /// <summary>
    /// How many chunk texts are embedded per request. Large enough to keep the round-trip
    /// count down on a case folder of a few hundred documents, small enough to stay inside
    /// per-request input limits.
    /// </summary>
    private const int EmbeddingBatchSize = 64;

    private readonly AppSettings _settings;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddings;
    private readonly ICaseDocumentStore _store;

    public CaseDocumentIndexer(
        AppSettings settings,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        ICaseDocumentStore store)
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
    /// <summary>
    /// Fallback for callers that have no settings to hand. Matches
    /// <see cref="AppSettings.MaxEmbeddingInputCharacters"/>, which is what the app uses.
    /// </summary>
    internal const int DefaultMaxElementCharacters = 20_000;

    private async Task<IReadOnlyList<CaseDocumentChunk>> ChunkAsync(
        MarkdownReader reader,
        IngestionChunker<string> chunker,
        string filePath,
        string caseReference,
        string categoryCode,
        CancellationToken cancellationToken)
    {
        var documentName = Path.GetFileName(filePath);

        var document = await ReadDocumentAsync(
            reader, filePath, _settings.MaxEmbeddingInputCharacters, cancellationToken).ConfigureAwait(false);

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
        MarkdownReader reader,
        string filePath,
        int maxElementCharacters = DefaultMaxElementCharacters,
        CancellationToken cancellationToken = default)
    {
        var documentName = Path.GetFileName(filePath);
        var markdown = PromoteBoldPseudoHeadings(WebUtility.HtmlDecode(
            await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false)));

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
            var document = await reader
                .ReadAsync(stream, documentName, "text/markdown", cancellationToken)
                .ConfigureAwait(false);

            // The reader does not always find the structure a document appears to have. One
            // converted policy document with 99 headings came back as a single element of
            // 156,384 characters — which the chunker then tried to embed in one call, and the
            // provider refused. The document was lost from the case entirely, and no check that
            // needed it could say why.
            //
            // So the result is measured rather than trusted, and a document the reader could not
            // break up is re-read as bounded plain text. Losing the heading structure costs
            // something; losing the document costs more.
            return LargestElement(document) <= maxElementCharacters
                ? document
                : AsPlainText(documentName, markdown, maxElementCharacters);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return AsPlainText(documentName, markdown, maxElementCharacters);
        }
    }

    /// <summary>
    /// Rewrites a line that is nothing but bold text as a real heading.
    ///
    /// <b>A heading a converter emitted as bold text is not a heading to anything downstream, and
    /// four section hints failed on that in every run they were measured in.</b> The document has
    /// an appendix; the reader sees a paragraph that happens to be emphasised; the chunker gives
    /// it no section context; the plan's hint naming the appendix matches nothing; the reserved
    /// slot goes unclaimed; and the diagnostic correctly reports a hint that matched nothing,
    /// which reads as a typo in the plan. Every layer behaves properly and the evidence does not
    /// arrive.
    ///
    /// The same defect puts a table's caption out of reach. A caption is usually the bold line
    /// immediately above the table, and where two tables state the same quantity on two different
    /// bases it is the only thing distinguishing them — which is a shape both assessors on record
    /// have got backwards.
    ///
    /// Deliberately conservative, because the alternative is inventing structure. A line
    /// qualifies only if it is <i>entirely</i> one bold span, short enough to be a title, not
    /// ending in sentence punctuation, and standing alone between blank lines. Emphasis inside a
    /// paragraph, a bold lead-in followed by prose on the same line, and a long bold sentence are
    /// all left exactly as they are.
    ///
    /// Level four, so a promoted heading nests under a document's real headings rather than
    /// competing with them: the aim is to give the block a name the chunker and the hints can
    /// see, not to reorganise a document that already has a structure.
    /// </summary>
    internal static string PromoteBoldPseudoHeadings(string markdown)
    {
        if (!markdown.Contains("**", StringComparison.Ordinal))
        {
            return markdown;
        }

        var lines = markdown.Split('\n');
        var promoted = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.Trim();

            if (!IsBoldOnlyLine(trimmed))
            {
                continue;
            }

            // Standing alone: nothing but blank lines on either side. A bold line butting
            // directly against a paragraph is that paragraph's emphasis, not its heading.
            if (!IsBlank(lines, i - 1) || !IsBlank(lines, i + 1))
            {
                continue;
            }

            lines[i] = "#### " + trimmed[2..^2].Trim();
            promoted = true;
        }

        return promoted ? string.Join('\n', lines) : markdown;
    }

    /// <summary>
    /// Whether a line is one bold span and nothing else, short enough to be a title, and not a
    /// sentence. The inner-span test is what keeps <c>**Total** for the year was **£4,000**</c>
    /// out: two spans on one line is emphasis, however the line begins and ends.
    /// </summary>
    private static bool IsBoldOnlyLine(string trimmed)
    {
        const int LongestPlausibleHeading = 80;

        if (trimmed.Length <= 4
            || trimmed.Length > LongestPlausibleHeading
            || !trimmed.StartsWith("**", StringComparison.Ordinal)
            || !trimmed.EndsWith("**", StringComparison.Ordinal))
        {
            return false;
        }

        var inner = trimmed[2..^2].Trim();

        return inner.Length > 0
            && !inner.Contains("**", StringComparison.Ordinal)
            && !inner.EndsWith('.')
            && !inner.EndsWith(':')
            && !inner.EndsWith('?');
    }

    /// <summary>
    /// Whether the line at <paramref name="index"/> is blank, treating the edges of the document
    /// as blank — a heading on the first line has nothing above it and is still a heading.
    /// </summary>
    private static bool IsBlank(string[] lines, int index) =>
        index < 0 || index >= lines.Length || string.IsNullOrWhiteSpace(lines[index]);

    /// <summary>The size of the largest leaf element — what actually gets sent as one embedding call.</summary>
    internal static int LargestElement(IngestionDocument document) =>
        document.Sections
            .SelectMany(Leaves)
            .Select(e => e.GetMarkdown()?.Length ?? 0)
            .DefaultIfEmpty(0)
            .Max();

    private static IEnumerable<IngestionDocumentElement> Leaves(IngestionDocumentElement element)
    {
        if (element is not IngestionDocumentSection section)
        {
            yield return element;
            yield break;
        }

        foreach (var leaf in section.Elements.SelectMany(Leaves))
        {
            yield return leaf;
        }
    }

    /// <summary>
    /// The last-resort document: blank-line-separated blocks as paragraphs, structure
    /// discarded. The semantic chunker still groups them by meaning.
    ///
    /// Blocks larger than the embedding limit are split again — a single table or a run of
    /// unbroken prose can easily exceed it on its own, and one oversized paragraph fails the
    /// whole document just as surely as one oversized element did.
    /// </summary>
    internal static IngestionDocument AsPlainText(
        string documentName, string markdown, int maxElementCharacters = DefaultMaxElementCharacters)
    {
        var document = new IngestionDocument(documentName);
        var section = new IngestionDocumentSection();

        foreach (var block in markdown.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var piece in SplitToFit(block.Trim(), maxElementCharacters))
            {
                section.Elements.Add(new IngestionDocumentParagraph(piece));
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
    /// Breaks text into pieces no larger than <paramref name="maxCharacters"/>, preferring line
    /// boundaries so a split lands between table rows or sentences rather than mid-word. Text
    /// with no usable boundary — one enormous line — is cut on length, because an awkward split
    /// still indexes and no split does not.
    /// </summary>
    internal static IEnumerable<string> SplitToFit(string text, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        if (text.Length <= maxCharacters)
        {
            yield return text;
            yield break;
        }

        var current = new StringBuilder();

        foreach (var line in text.Split('\n'))
        {
            foreach (var piece in HardSplit(line, maxCharacters))
            {
                // +1 for the newline that will rejoin them.
                if (current.Length > 0 && current.Length + piece.Length + 1 > maxCharacters)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.Append('\n');
                }

                current.Append(piece);
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static IEnumerable<string> HardSplit(string line, int maxCharacters)
    {
        for (var start = 0; start < line.Length; start += maxCharacters)
        {
            yield return line.Substring(start, Math.Min(maxCharacters, line.Length - start));
        }

        if (line.Length == 0)
        {
            yield return string.Empty;
        }
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
        // An exception the app raised about itself already names the field to change and the
        // reason. Wrapping it adds a sentence saying the model "could not be reached" — which is
        // false, the endpoint answered — followed by advice to check the URL, the key and the
        // model, none of which is wrong. The precise message then appears at the bottom of a
        // paragraph telling the reader to look somewhere else.
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The embedding endpoint, not the chat one. This message exists to catch a
            // misconfigured embedding service and it used to print the chat base URL — so it
            // named a URL that was very likely correct, and its "add /v1" hint pointed at the
            // field that was not the problem.
            var baseUrl = _settings.ResolveEmbeddingBaseUrl();

            // A 404 here is nearly always the base URL missing its version segment: the client
            // appends "/embeddings" to whatever is configured, so ".../v1" is what it needs.
            var hint = ex.Message.Contains("404", StringComparison.Ordinal)
                ? $"\n\nA 404 usually means the base URL is missing its version segment — try "
                  + $"\"{baseUrl.TrimEnd('/')}/v1\" — or that the gateway does not serve this "
                  + "embedding model."
                : string.Empty;

            throw new InvalidOperationException(
                $"The embedding model \"{_settings.EmbeddingModel}\" could not be reached at "
                + $"{baseUrl}. Check the embedding base URL, embedding API key and embedding model "
                + "in Settings; where those are blank the chat endpoint and key are used instead."
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
        + $"tokens per chunk, {_settings.ChunkOverlapTokens} overlap"
        + (_settings.TableAwareChunking ? ", tables kept whole" : string.Empty);

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

        var semantic = new SemanticSimilarityChunker(_embeddings, options);

        // Tables are cut by a similarity-and-budget chunker wherever the budget runs out, which
        // delivers a header row without figures or figures without a header row. The findings that
        // turn on reading one row against its column heading are the ones the benchmark counts.
        return _settings.TableAwareChunking
            ? new TableAwareChunker(semantic)
            : semantic;
    }

    private static string Compose(string? context, string content) =>
        string.IsNullOrWhiteSpace(context) ? content.Trim() : $"{context.Trim()}\n\n{content.Trim()}";
}
