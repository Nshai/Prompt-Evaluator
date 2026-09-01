using System.IO;
using System.IO.Compression;
using System.Text;

using AiPromptEvaluator;

using IQWorkflow;

using Xunit;

namespace IQWorkflow.Tests;

/// <summary>
/// The ingestion fixes, and the stage that applies them.
///
/// <b>These repair losses nothing downstream can recover.</b> The pipeline analysis prices the DOCX
/// table loss at "a hard floor of 4 findings", and names the checkbox artefact verbatim — <c>- [x]
/// xInternal transfer</c>, on the certified annex carrying the most severe finding of Test Case 3.
/// A fix that silently stopped working would show up as a slightly worse recall number months later
/// and be attributed to something else entirely.
/// </summary>
public sealed class IngestionTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "iqworkflow-ingest", Guid.NewGuid().ToString("N"));

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
    // The checkbox glyph
    // ──────────────────────────────────────────────

    /// <summary>
    /// The artefact exactly as the analysis records it. The checkbox <em>state</em> survives
    /// conversion either way; what the stray letter costs is exact quotation — an extractor emits
    /// <c>xInternal transfer</c>, the citation verifier cannot find that string in the source, and a
    /// real finding is rejected as a fabricated quote.
    /// </summary>
    [Fact]
    public void TheOcrGlyphIsStrippedFromACheckboxLabel() =>
        Assert.Equal(
            "- [x] Internal transfer",
            CheckboxNormaliser.Normalise("- [x] xInternal transfer"));

    [Fact]
    public void AGlyphFollowedByASpaceIsStripped() =>
        Assert.Equal("- [X] Yes", CheckboxNormaliser.Normalise("- [X] X Yes"));

    /// <summary>
    /// <b>The half that matters more.</b> A normaliser that ate the first letter of real answers
    /// would be worse than the artefact it removes — it would corrupt quotations that were correct.
    /// </summary>
    [Theory]
    [InlineData("X-ray")]
    [InlineData("Xmas bonus")]
    [InlineData("xylophone")]
    [InlineData("Existing plan")]
    public void ARealLabelIsLeftAlone(string label) =>
        Assert.Equal(label, CheckboxNormaliser.StripGlyph(label));

    [Fact]
    public void TextWithNoCheckboxesIsUnchanged()
    {
        const string prose = "The client is a tenant and pays rent of £900 per month.";

        Assert.Equal(prose, CheckboxNormaliser.Normalise(prose));
    }

    // ──────────────────────────────────────────────
    // DOCX table recovery
    // ──────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal .docx — a zip with one <c>word/document.xml</c> holding a table. Enough to
    /// exercise the recovery, which reads the zip directly rather than going through Word.
    /// </summary>
    private string WriteDocx(string name, string tableXml)
    {
        var path = Path.Combine(Directory.CreateDirectory(_folder).FullName, name);

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("word/document.xml");

        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);

        writer.Write(
            """<?xml version="1.0" encoding="UTF-8"?>"""
            + """<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>"""
            + tableXml
            + "</w:body></w:document>");

        return path;
    }

    private static string Row(params string[] cells) =>
        "<w:tr>" + string.Concat(cells.Select(c =>
            $"<w:tc><w:p><w:r><w:t>{c}</w:t></w:r></w:p></w:tc>")) + "</w:tr>";

    [Fact]
    public void ATableIsReadStraightOutOfTheDocx()
    {
        var path = WriteDocx("risk.docx",
            "<w:tbl>" + Row("Fund", "Charge") + Row("Nest Higher Risk", "0.30%") + "</w:tbl>");

        var tables = DocxTableRecovery.Extract(path);
        var table = Assert.Single(tables);

        Assert.Contains("Nest Higher Risk", table.Markdown);
        Assert.Contains("0.30%", table.Markdown);

        // The header row is carried separately: a table segment without it is, in the fidelity
        // gate's words, a list of numbers nobody can interpret.
        Assert.Contains("Fund", table.Headers);
    }

    /// <summary>
    /// Appended rather than substituted, and only where the content is not already present — the
    /// converter's own rendering is the better one wherever it exists.
    /// </summary>
    [Fact]
    public void ATableTheConverterAlreadyReturnedIsNotRecoveredTwice()
    {
        var path = WriteDocx("dupe.docx",
            "<w:tbl>" + Row("Fund", "Charge") + Row("Nest Higher Risk", "0.30%") + "</w:tbl>");

        var all = DocxTableRecovery.Extract(path);

        var alreadyThere = DocxTableRecovery.Missing(
            all, "| Fund | Charge |\n| --- | --- |\n| Nest Higher Risk | 0.30% |");

        Assert.Empty(alreadyThere);
        Assert.Single(DocxTableRecovery.Missing(all, "The report says nothing about funds."));
    }

    [Theory]
    [InlineData("report.docx", true)]
    [InlineData("report.pdf", false)]
    [InlineData("notes.md", false)]
    public void RecoveryAppliesOnlyToWordDocuments(string name, bool expected) =>
        Assert.Equal(expected, DocxTableRecovery.Applies(Path.Combine(_folder, name)));

    // ──────────────────────────────────────────────
    // The conversion stage
    // ──────────────────────────────────────────────

    private WorkflowContext Context(string caseFolder) => new(
        caseFolder, "TC-1", new AppSettings { TenantId = 7 }, Path.Combine(_folder, "run"));

    /// <summary>
    /// A case folder of its own. Each call gets a distinct root: two cases sharing a directory
    /// would let one test's documents satisfy another's, which is exactly the confusion the
    /// satisfaction check exists to make.
    /// </summary>
    private string CaseFolder(params string[] relativePaths)
    {
        var root = Directory
            .CreateDirectory(Path.Combine(_folder, "case", Guid.NewGuid().ToString("N")[..8]))
            .FullName;

        foreach (var relative in relativePaths)
        {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "source");
        }

        return root;
    }

    /// <summary>
    /// A case arriving already converted is the normal path — every stored test case is kept that
    /// way — so the stage must recognise it rather than re-converting a whole case file.
    /// </summary>
    [Fact]
    public async Task ConversionIsSatisfiedWhenTheCaseAlreadyHoldsMarkdown()
    {
        var converted = CaseFolder(Path.Combine("A", "agreement.md"));
        var raw = CaseFolder(Path.Combine("A", "agreement.pdf"));

        var stage = new ConvertDocumentsStage(_ => throw new InvalidOperationException("not needed"));

        Assert.True(await stage.IsSatisfiedAsync(Context(converted)));
        Assert.False(await stage.IsSatisfiedAsync(Context(raw)));
    }

    [Fact]
    public async Task ConversionFailsWhenTheCaseFolderHoldsNothingConvertible()
    {
        var root = Directory.CreateDirectory(Path.Combine(_folder, "empty")).FullName;

        var outcome = await new ConvertDocumentsStage(_ => throw new InvalidOperationException())
            .ExecuteAsync(Context(root));

        Assert.Equal(StageStatus.Failed, outcome.Status);
        Assert.Contains("No convertible documents", outcome.Summary);
    }

    [Fact]
    public async Task ConversionFailsWhenTheCaseFolderDoesNotExist()
    {
        var outcome = await new ConvertDocumentsStage(_ => throw new InvalidOperationException())
            .ExecuteAsync(Context(Path.Combine(_folder, "missing")));

        Assert.Equal(StageStatus.Failed, outcome.Status);
        Assert.Contains("does not exist", outcome.Summary);
    }

    /// <summary>
    /// <b>A category decides which checks can retrieve a document at all.</b> Inventing one for a
    /// file loose in the case root would put a document in front of the wrong requirements, which
    /// is a worse outcome than leaving it out and saying so.
    /// </summary>
    [Fact]
    public async Task ADocumentWithNoCategoryFolderIsNotGivenOne()
    {
        var root = CaseFolder("loose.pdf");

        var outcome = await new ConvertDocumentsStage(_ => throw new InvalidOperationException())
            .ExecuteAsync(Context(root));

        Assert.Equal(StageStatus.Failed, outcome.Status);
        Assert.Contains("No convertible documents", outcome.Summary);
    }

    /// <summary>
    /// Markdown already in the case folder is passed through rather than sent to the converter:
    /// re-converting Markdown would be asking the sidecar to render its own output.
    /// </summary>
    [Theory]
    [InlineData("report.pdf", true, false)]
    [InlineData("report.docx", true, false)]
    [InlineData("notes.md", true, true)]
    [InlineData("archive.zip", false, false)]
    public void OnlyDocumentsWorthConvertingAreSent(string name, bool convertible, bool alreadyMarkdown)
    {
        Assert.Equal(convertible, DoclingMarkdownConverter.IsConvertible(name));
        Assert.Equal(alreadyMarkdown, DoclingMarkdownConverter.IsAlreadyMarkdown(name));
    }
}
