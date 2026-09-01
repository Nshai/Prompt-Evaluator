using AiPromptEvaluator;

using Microsoft.Extensions.DataIngestion;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Keeping a document out of the store because one piece of it was too big to embed.
///
/// A converted Zurich policy document was dropped from a real case with
/// "expected maxLength: 50000, actual: 156384". The chunker embeds a document's own elements to
/// decide where to cut, and the Markdown reader had returned the entire 99-heading file as a
/// single element — so the whole document went to the endpoint in one call and the whole
/// document was lost. Nothing downstream could tell that the evidence was missing rather than
/// merely unretrieved.
/// </summary>
public class EmbeddingInputSizeTests
{
    private const int Max = 1_000;

    // ──────────────────────────────────────────────
    // Splitting
    // ──────────────────────────────────────────────

    [Fact]
    public void SplitToFit_LeavesTextThatAlreadyFits()
    {
        Assert.Equal(["short enough"], CaseDocumentIndexer.SplitToFit("short enough", Max));
    }

    [Fact]
    public void SplitToFit_KeepsEveryPieceUnderTheLimit()
    {
        var text = string.Join("\n", Enumerable.Range(0, 400).Select(i => $"| row {i} | value {i} |"));

        var pieces = CaseDocumentIndexer.SplitToFit(text, Max).ToList();

        Assert.True(pieces.Count > 1);
        Assert.All(pieces, p => Assert.True(p.Length <= Max, $"a piece was {p.Length} characters"));
    }

    /// <summary>Splitting must not lose or reorder content — it is the document's only copy.</summary>
    [Fact]
    public void SplitToFit_PreservesEveryLineInOrder()
    {
        var lines = Enumerable.Range(0, 500).Select(i => $"line {i}").ToList();

        var rejoined = string.Join("\n", CaseDocumentIndexer.SplitToFit(string.Join("\n", lines), Max));

        Assert.Equal(lines, rejoined.Split('\n'));
    }

    /// <summary>Line boundaries are preferred, so a split lands between rows rather than mid-word.</summary>
    [Fact]
    public void SplitToFit_CutsOnLineBoundariesWhereItCan()
    {
        var text = string.Join("\n", Enumerable.Range(0, 300).Select(i => $"sentence number {i}."));

        Assert.All(
            CaseDocumentIndexer.SplitToFit(text, Max),
            piece => Assert.EndsWith(".", piece.TrimEnd()));
    }

    /// <summary>
    /// One enormous line has no boundary to prefer. An awkward cut still indexes; no cut means
    /// the provider refuses the call and the document is lost.
    /// </summary>
    [Fact]
    public void SplitToFit_FallsBackToCuttingOnLength()
    {
        var pieces = CaseDocumentIndexer.SplitToFit(new string('x', 5_000), Max).ToList();

        Assert.Equal(5, pieces.Count);
        Assert.All(pieces, p => Assert.Equal(Max, p.Length));
    }

    [Fact]
    public void SplitToFit_IgnoresEmptyText()
    {
        Assert.Empty(CaseDocumentIndexer.SplitToFit("   ", Max));
        Assert.Empty(CaseDocumentIndexer.SplitToFit(string.Empty, Max));
    }

    // ──────────────────────────────────────────────
    // The document as a whole
    // ──────────────────────────────────────────────

    [Fact]
    public void AsPlainText_BoundsEveryElement()
    {
        var markdown = string.Join(
            "\n\n",
            "a normal paragraph",
            new string('y', 8_000),
            string.Join("\n", Enumerable.Range(0, 300).Select(i => $"| {i} | value |")));

        var document = CaseDocumentIndexer.AsPlainText("big.md", markdown, Max);

        Assert.True(CaseDocumentIndexer.LargestElement(document) <= Max);
    }

    [Fact]
    public void AsPlainText_StillRefusesAnEmptyFile() =>
        Assert.Throws<InvalidOperationException>(() => CaseDocumentIndexer.AsPlainText("empty.md", "   ", Max));

    /// <summary>
    /// The reader's output is measured rather than trusted. A document it returns as one giant
    /// element is re-read as bounded plain text: losing the heading structure costs something,
    /// losing the document costs more.
    /// </summary>
    [Fact]
    public async Task ReadDocument_BoundsADocumentTheReaderWouldNotBreakUp()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eval-oversized-{Guid.NewGuid():N}.md");

        // Headings and prose, in the shape of the document that actually failed.
        var markdown = string.Join(
            "\n\n",
            Enumerable.Range(0, 120).Select(i =>
                $"## Section {i}\n\n" + string.Join(" ", Enumerable.Repeat($"policy wording {i}.", 120))));

        try
        {
            await File.WriteAllTextAsync(path, markdown);

            var document = await CaseDocumentIndexer.ReadDocumentAsync(new MarkdownReader(), path, Max);

            Assert.True(
                CaseDocumentIndexer.LargestElement(document) <= Max,
                $"largest element was {CaseDocumentIndexer.LargestElement(document)} characters, limit {Max}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The regression itself, against the document that caused it. Skipped where the sample case
    /// is not present, so the suite still runs on a clone without it.
    /// </summary>
    [Fact]
    public async Task EveryConvertedCaseDocument_FitsTheEmbeddingLimit()
    {
        var folder = LocateConvertedCase();

        if (folder is null)
        {
            return;
        }

        var reader = new MarkdownReader();
        var oversized = new List<string>();

        foreach (var file in Directory.GetFiles(folder, "*.md", SearchOption.AllDirectories))
        {
            var document = await CaseDocumentIndexer.ReadDocumentAsync(
                reader, file, CaseDocumentIndexer.DefaultMaxElementCharacters);

            var largest = CaseDocumentIndexer.LargestElement(document);

            if (largest > CaseDocumentIndexer.DefaultMaxElementCharacters)
            {
                oversized.Add($"{Path.GetFileName(file)}: {largest:N0} characters");
            }
        }

        Assert.Empty(oversized);
    }

    private static string? LocateConvertedCase()
    {
        var directory = AppContext.BaseDirectory;

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory, "docs", "QA-Checks", "Feasability-test-case-converted");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }
}
