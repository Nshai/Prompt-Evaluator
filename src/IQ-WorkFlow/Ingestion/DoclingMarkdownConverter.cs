using System.IO;
using System.Text;

namespace IQWorkflow;

/// <summary>What one document's conversion produced.</summary>
/// <param name="Source">The path relative to the case folder, as the summary records it.</param>
/// <param name="CategoryCode">Which category folder it came from.</param>
/// <param name="Markdown">The converted text.</param>
/// <param name="RecoveredTables">Tables read straight from the .docx that the converter never returned.</param>
/// <param name="Error">Why it failed, where it did. Null on success.</param>
/// <param name="OutputName">
/// The file name the Markdown should be written under, without an extension.
///
/// Null for the ordinary case, where the name is the source document's. It is set only where one
/// source file produces several documents — an email and the things it carried — because two
/// attachments on one message would otherwise write over each other and over the message.
/// </param>
/// <param name="TranscribedPictures">Pictures a model read, where picture narration was on.</param>
/// <param name="NarratedTables">Tables a model re-expressed as prose, where table narration was on.</param>
public sealed record DocumentConversion(
    string Source,
    string CategoryCode,
    string? Markdown,
    int RecoveredTables = 0,
    string? Error = null,
    string? OutputName = null,
    int TranscribedPictures = 0,
    int NarratedTables = 0)
{
    public bool Succeeded => Error is null && !string.IsNullOrWhiteSpace(Markdown);
}

/// <summary>
/// Converts one case document to Markdown, applying the ingestion fixes.
///
/// <b>The fix pipeline is v2's, in v2's order; the output is v1's.</b> That split is deliberate and
/// it is the whole of the exception the implementation plan grants. The passes below repair losses
/// the pipeline analysis names and nothing downstream can recover — so they come across as they
/// were written and measured. What does not come across is v2's data model: this emits Markdown
/// into category folders, which is the contract v1's indexer, extractor and every stored run
/// already read. Importing segments would be importing the other pipeline.
///
/// <b>The order matters and is not arbitrary:</b>
///
/// <list type="number">
/// <item><b>Reconstruct form fields</b> from the JSON's bounding boxes. PDF forms lay labels and
/// values out as free-floating positioned text, which the converter flattens into a scrambled
/// reading-order jumble.</item>
/// <item><b>Backfill</b> anything the JSON holds and the Markdown dropped — OCR'd image text,
/// key/value pairs, form items. The Markdown export is a lossy rendering of the JSON, so the JSON
/// is the more complete document of the two.</item>
/// <item><b>Normalise checkboxes</b>, and only now: reconstruction is what produces the checkbox
/// lines in the first place, so normalising earlier would find nothing to clean.</item>
/// <item><b>Recover .docx tables</b> the converter never returned. The backfiller can only restore
/// what reached the JSON; on the Test Case 3 risk-profile report the sidecar reports four tables
/// where the document holds fifteen, and the eleven it drops carry the agreed risk level and all
/// three questionnaires. The analysis prices this loss at a hard floor of four findings.</item>
/// </list>
///
/// <b>Two further passes are off unless configured</b> — transcribing pictures and narrating
/// tables. They are the only parts of conversion that spend money, so they are opt-in on a pipeline
/// whose stated objective is minimum cost. See <c>AppSettings.PictureNarration</c> and
/// <c>AppSettings.TableNarration</c>.
/// </summary>
public sealed class DoclingMarkdownConverter : ICaseDocumentConverter
{
    private readonly DoclingClient _docling;
    private readonly PictureNarrationOptions _pictures;
    private readonly TableNarrationOptions _tables;
    private readonly IPictureDescriber? _describer;
    private readonly ITableNarrator? _narrator;

    /// <param name="docling">The sidecar client. Both exports are requested; the JSON is the truth.</param>
    /// <param name="pictures">Whether pictures are transcribed, and the cost guards around it.</param>
    /// <param name="tables">Whether tables are narrated, and how a long one is chunked.</param>
    /// <param name="describer">
    /// How a picture is read, where <paramref name="pictures"/> switches it on. Null leaves the
    /// pass off however the options read — a describer that does not exist cannot be enabled by a
    /// setting, which is what keeps a misconfigured run from failing rather than costing nothing.
    /// </param>
    /// <param name="narrator">How a table is re-expressed, where <paramref name="tables"/> switches it on.</param>
    public DoclingMarkdownConverter(
        DoclingClient docling,
        PictureNarrationOptions? pictures = null,
        IPictureDescriber? describer = null,
        TableNarrationOptions? tables = null,
        ITableNarrator? narrator = null)
    {
        _docling = docling ?? throw new ArgumentNullException(nameof(docling));
        _pictures = pictures ?? PictureNarrationOptions.Disabled;
        _tables = tables ?? TableNarrationOptions.Disabled;
        _describer = describer;
        _narrator = narrator;
    }

    /// <summary>True when a model will be asked to read this document's pictures.</summary>
    private bool DescribingPictures => _pictures.Enabled && _describer is not null;

    /// <summary>True when a model will be asked to re-express this document's tables.</summary>
    private bool NarratingTables => _tables.Enabled && _narrator is not null;

    /// <summary>
    /// File types worth sending to the converter.
    ///
    /// A Markdown file already in the case folder is passed through rather than converted: a case
    /// may arrive part-converted, and re-converting Markdown would be asking the sidecar to render
    /// its own output.
    /// </summary>
    public static bool IsConvertible(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is
            ".pdf" or ".docx" or ".doc" or ".xlsx" or ".xls" or ".pptx" or ".ppt"
            or ".png" or ".jpg" or ".jpeg" or ".tiff" or ".bmp" or ".html" or ".htm" or ".md";

    /// <summary>True when the file is already Markdown and needs only copying.</summary>
    public static bool IsAlreadyMarkdown(string path) =>
        Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentConversion>> ConvertAsync(
        string path, string relativeSource, string categoryCode, CancellationToken cancellationToken = default) =>
        [await ConvertOneAsync(path, relativeSource, categoryCode, cancellationToken).ConfigureAwait(false)];

    /// <summary>
    /// One file, one document. The bundle form above exists for the email decorator, which is the
    /// only converter where that is not true.
    /// </summary>
    public async Task<DocumentConversion> ConvertOneAsync(
        string path, string relativeSource, string categoryCode, CancellationToken cancellationToken = default)
    {
        if (IsAlreadyMarkdown(path))
        {
            return new DocumentConversion(
                relativeSource, categoryCode,
                await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
        }

        string markdown;

        // The richer of the two representations. The Markdown export is a lossy rendering of this,
        // which is why both fix passes below read the document rather than the text.
        DoclingDocument? json;

        try
        {
            var response = await _docling.ConvertAsync(path, cancellationToken).ConfigureAwait(false);

            markdown = response.Document?.MdContent ?? string.Empty;
            json = response.Document?.JsonContent;
        }
        catch (DocumentUnreadableException ex)
        {
            // Kept distinct from "converted to nothing". A coverage gap over a document nobody
            // could open reads downstream as "the case file does not evidence this", which is a
            // claim about the advice rather than about the run.
            return new DocumentConversion(relativeSource, categoryCode, null, 0, ex.Reason);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return new DocumentConversion(relativeSource, categoryCode, null, 0, ex.Message);
        }

        if (json is not null)
        {
            markdown = FormFieldReconstructor.Reconstruct(markdown, json);
            markdown = MarkdownBackfiller.Backfill(markdown, json);

            // After reconstruction, because that pass is what produces the checkbox lines in the
            // first place — normalising before it would find nothing to clean.
            markdown = CheckboxNormaliser.Normalise(markdown);
        }

        var recovered = 0;

        if (DocxTableRecovery.Applies(path))
        {
            var missing = DocxTableRecovery.Missing(DocxTableRecovery.Extract(path), markdown);

            if (missing.Count > 0)
            {
                markdown = AppendRecovered(markdown, missing);
                recovered = missing.Count;
            }
        }

        // The two paid passes, last and only when switched on. Both append: the document the
        // deterministic passes produced is not edited, so turning either off returns the earlier
        // output exactly.
        var transcribed = 0;
        var narrated = 0;

        if (json is not null && DescribingPictures)
        {
            var (block, count) = await TranscribePicturesAsync(json, cancellationToken).ConfigureAwait(false);

            markdown += block;
            transcribed = count;
        }

        if (json is not null && NarratingTables)
        {
            var (block, count) = await NarrateTablesAsync(
                json, Path.GetFileName(path), cancellationToken).ConfigureAwait(false);

            markdown += block;
            narrated = count;
        }

        return string.IsNullOrWhiteSpace(markdown)
            ? new DocumentConversion(
                relativeSource, categoryCode, null, 0,
                "The converter returned no text. The document may be an image with no readable "
                + "content, or empty.")
            : new DocumentConversion(
                relativeSource, categoryCode, markdown, recovered,
                TranscribedPictures: transcribed, NarratedTables: narrated);
    }

    /// <summary>
    /// Transcribes the pictures that carry content, as an appendix.
    ///
    /// <b>Appended, and labelled as a transcription rather than as the document's own text.</b> A
    /// reading of a table is not the table: a quotation verifier checking a citation against the
    /// source needs to be able to tell which it has, and a transcription silently mixed into the
    /// prose would read as something the document said.
    ///
    /// Each image gets its own heading, so nothing below inherits one of them — the same reasoning
    /// that keeps the recovered-table appendix under a comment rather than a heading.
    /// </summary>
    private async Task<(string Block, int Count)> TranscribePicturesAsync(
        DoclingDocument json, CancellationToken cancellationToken)
    {
        var block = new StringBuilder();
        var count = 0;

        for (var i = 0; i < json.Pictures.Count && count < _pictures.MaxImagesPerDocument; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // No image data usually means include_images was left off, which is a configuration
            // fact rather than a document that happened to contain nothing.
            if (!DataUri.TryParse(json.Pictures[i].Image?.Uri, out var bytes, out var mediaType))
            {
                continue;
            }

            // A vision call apiece to be told something is a logo costs real money for nothing.
            if (bytes.Length < _pictures.MinimumImageBytes)
            {
                continue;
            }

            var text = await _describer!
                .DescribeAsync(bytes, mediaType, null, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (count == 0)
            {
                block.Append(
                    "\n\n<!-- Images below were transcribed by a vision model; they are a reading "
                    + "of the picture, not text the document itself carries. -->\n");
            }

            block.Append("\n### Image ").Append(i + 1).Append(" (transcribed)\n\n")
                .Append(text.Trim()).Append('\n');

            count++;
        }

        return (block.ToString(), count);
    }

    /// <summary>
    /// Re-expresses each table as prose, as an appendix.
    ///
    /// <b>Appended in an appendix rather than spliced in beside each table, and that is a
    /// concession this implementation makes knowingly.</b> The reference implementation attaches a
    /// narrative to the table segment it belongs to, because it works on segments and knows which
    /// is which. This one emits Markdown, where the only way to pair a JSON table with a rendered
    /// one is to count table blocks in the text — and the Markdown export is exactly the artefact
    /// known to drop tables, which is why <see cref="DocxTableRecovery"/> exists. A narrative
    /// attached to the wrong table is worse than one in an appendix, so each block names its table
    /// index and repeats the column headers instead.
    ///
    /// The grid itself is untouched: the narrative is a second reading of the same rows for
    /// anything that wants prose, never a replacement for the rows.
    /// </summary>
    private async Task<(string Block, int Count)> NarrateTablesAsync(
        DoclingDocument json, string fileName, CancellationToken cancellationToken)
    {
        var block = new StringBuilder();
        var count = 0;

        for (var i = 0; i < json.Tables.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var grid = TableGridBuilder.Build(json.Tables[i]);
            var headers = TableGridBuilder.GetHeaderLabels(grid);
            var headerRows = grid.HeaderRowIndexes.ToHashSet();

            var dataRows = Enumerable.Range(0, grid.NumRows)
                .Where(r => !headerRows.Contains(r))
                .Select(r => (IReadOnlyList<string>)[.. grid.Cells[r].Select(v => v ?? string.Empty)])
                .ToList();

            if (dataRows.Count < _tables.MinRowsToNarrate)
            {
                continue;
            }

            // Chunked rather than truncated: a long charges table is exactly the one worth reading.
            var chunks = dataRows.Chunk(Math.Max(1, _tables.MaxRowsPerChunk)).ToList();
            var parts = new List<string>(chunks.Count);

            for (var chunk = 0; chunk < chunks.Count; chunk++)
            {
                parts.Add(await _narrator!.NarrateAsync(
                    new TableNarrationRequest(
                        fileName, i, headers, chunks[chunk], chunk + 1, chunks.Count, null),
                    cancellationToken).ConfigureAwait(false));
            }

            var narrative = string.Join("\n", parts).Trim();

            if (narrative.Length == 0)
            {
                continue;
            }

            if (count == 0)
            {
                block.Append(
                    "\n\n<!-- Narrative readings below re-express the tables above; the tables "
                    + "themselves are unchanged. -->\n");
            }

            block.Append("\n### Narrative reading of table ").Append(i + 1).Append("\n\n");

            if (headers.Count > 0)
            {
                block.Append("Columns: ").Append(string.Join(" | ", headers)).Append("\n\n");
            }

            block.Append(narrative).Append('\n');

            count++;
        }

        return (block.ToString(), count);
    }

    /// <summary>
    /// Appends the tables the converter never returned.
    ///
    /// <b>A comment, not a heading.</b> A passage takes its heading from the nearest one above it,
    /// so a heading here would be inherited by whatever follows and label ordinary tables
    /// "recovered" when they were not. Each recovered table carries its own caption instead.
    ///
    /// Appended rather than substituted, and only where the content is not already present — the
    /// converter's own rendering is the better one wherever it exists.
    /// </summary>
    private static string AppendRecovered(string markdown, IReadOnlyList<RecoveredTable> missing)
    {
        var appendix = new StringBuilder(markdown);

        appendix.Append(
            "\n\n<!-- Tables below were read directly from word/document.xml; "
            + "the converter did not return them. -->\n\n");

        foreach (var table in missing)
        {
            appendix.Append(table.Markdown).Append("\n\n");
        }

        return appendix.ToString();
    }
}
