// Ported verbatim from IQFlow.Adapters.Ingest, apart from the namespace and this note.
//
// The implementation plan (§4) permits the conversion stage as the one exception to "build against
// v1": these are defect fixes for losses the pipeline analysis names and nothing downstream can
// recover. Kept byte-faithful on purpose — a "tidied" copy is a second implementation to keep in
// step with the original, and the fixes are the product of measurement rather than of taste.
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace IQWorkflow;

/// <summary>One table recovered straight from a Word document.</summary>
/// <param name="Ordinal">Position in the document, for stable ordering.</param>
/// <param name="Markdown">The table as a Markdown grid.</param>
/// <param name="Text">Its cell text concatenated, for deciding whether it is already present.</param>
/// <param name="Headers">
/// The first row. Carried separately because a table segment without its header is, in the
/// fidelity gate's words, a list of numbers nobody can interpret.
/// </param>
public sealed record RecoveredTable(
    int Ordinal,
    string Markdown,
    string Text,
    IReadOnlyList<string> Headers);

/// <summary>
/// Reads tables directly out of a <c>.docx</c>, for the ones the converter never saw.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> On the Test Case 3 risk-profile report the sidecar reports four tables;
/// the document contains fifteen. The eleven it drops are the ones that matter — the agreed risk
/// level and all three questionnaires — and they are not exotic: the value <c>7</c> and the word
/// <c>Growth</c> sit in an ordinary <c>w:tc</c>. The benchmark records this as a conversion gap and
/// excuses a run from three findings because of it. This closes the gap rather than living with it.
/// </para>
/// <para>
/// <b>A Word document is a zip.</b> Reading <c>word/document.xml</c> needs no Word, no COM, no
/// network and no additional package — <c>System.IO.Compression</c> and <c>XDocument</c> are enough.
/// That makes recovery cheap enough to run on every <c>.docx</c> rather than only when something
/// looks wrong.
/// </para>
/// <para>
/// It recovers <em>tables only</em>, and is not a second converter. Docling remains the source of
/// prose, headings, reading order and images; this fills one specific hole, additively, in the
/// same spirit as the Markdown backfiller.
/// </para>
/// </remarks>
public static class DocxTableRecovery
{
    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>Whether recovery can apply to this file at all.</summary>
    public static bool Applies(string path) =>
        Path.GetExtension(path).Equals(".docx", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads every table in the document, in document order.</summary>
    /// <remarks>
    /// Returns an empty list rather than throwing when the file is not a readable Word document.
    /// This runs as an enrichment pass over a conversion that already succeeded, and failing the
    /// whole document because an optional extra could not be read would be a poor trade.
    /// </remarks>
    public static IReadOnlyList<RecoveredTable> Extract(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            using var archive = ZipFile.OpenRead(path);
            var entry = archive.GetEntry("word/document.xml");

            if (entry is null)
            {
                return [];
            }

            using var stream = entry.Open();

            return Tables(XDocument.Load(stream));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or System.Xml.XmlException
            or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static List<RecoveredTable> Tables(XDocument document)
    {
        var tables = new List<RecoveredTable>();
        var ordinal = 0;

        foreach (var table in document.Descendants(W + "tbl"))
        {
            var rows = new List<List<string>>();

            // Direct rows only. A nested table's rows belong to the nested table, and taking them
            // here would duplicate every cell and misalign the columns of both.
            foreach (var row in table.Elements(W + "tr"))
            {
                var cells = row.Elements(W + "tc").Select(CellText).ToList();

                if (cells.Count > 0)
                {
                    rows.Add(cells);
                }
            }

            if (rows.Count == 0 || rows.All(r => r.All(string.IsNullOrWhiteSpace)))
            {
                continue;
            }

            // A blank first cell is normal in these documents — the corner of a labelled grid — so
            // the header row is used as it stands rather than rejected for being partly empty.
            tables.Add(new RecoveredTable(
                ordinal++,
                ToMarkdown(rows),
                string.Join(" ", rows.SelectMany(r => r).Where(c => c.Length > 0)),
                [.. rows[0].Select((c, i) => c.Length > 0 ? c : $"column {i + 1}")]));
        }

        return tables;
    }

    /// <summary>The text of one cell, excluding anything belonging to a table nested inside it.</summary>
    private static string CellText(XElement cell)
    {
        var text = new StringBuilder();
        var owner = cell.Ancestors(W + "tbl").FirstOrDefault();

        // Walked in document order, so a break between two runs becomes a space in the right place.
        // "7<br/><br/>Growth" is one cell holding a level and its label; run it together and it
        // reads "7Growth", split it across rows and the grid gains a row that is not there.
        foreach (var element in cell.Descendants())
        {
            if (element.Name == W + "t"
                && element.Ancestors(W + "tbl").FirstOrDefault() == owner)
            {
                text.Append(element.Value);
            }
            else if (element.Name == W + "br" || element.Name == W + "tab")
            {
                text.Append(' ');
            }
        }

        return Collapse(text.ToString());
    }

    private static string Collapse(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Replace("|", "\\|", StringComparison.Ordinal);

    /// <summary>Renders rows as a Markdown grid, padded to the widest row.</summary>
    private static string ToMarkdown(List<List<string>> rows)
    {
        var width = rows.Max(r => r.Count);
        var markdown = new StringBuilder();

        for (var i = 0; i < rows.Count; i++)
        {
            var cells = rows[i];

            markdown.Append("| ");

            for (var c = 0; c < width; c++)
            {
                markdown.Append(c < cells.Count ? cells[c] : string.Empty).Append(" | ");
            }

            markdown.Length -= 1;
            markdown.Append('\n');

            // A Markdown table needs a separator after its first row or nothing downstream reads it
            // as a table — including the fidelity gate that counts them.
            if (i == 0)
            {
                markdown.Append("| ").Append(string.Join(" | ", Enumerable.Repeat("---", width)))
                    .Append(" |\n");
            }
        }

        return markdown.ToString().TrimEnd();
    }

    /// <summary>
    /// The tables whose content is not already in the converted Markdown.
    /// </summary>
    /// <remarks>
    /// Compared on a normalised sample of the cell text rather than on counts. The converter
    /// legitimately finds some of these tables, and appending a duplicate of one it already has
    /// would double every value in it — which the reconciler would then read as the document
    /// agreeing with itself twice.
    /// </remarks>
    public static IReadOnlyList<RecoveredTable> Missing(
        IReadOnlyList<RecoveredTable> tables, string markdown)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(markdown);

        var haystack = Normalise(markdown);

        return
        [
            .. tables.Where(t =>
            {
                var probe = Normalise(t.Text);

                // Short tables are page furniture — a footer, a page number — and a substring test
                // over them matches almost anything. They are not worth recovering either way.
                return probe.Length >= 24 && !haystack.Contains(Sample(probe), StringComparison.Ordinal);
            }),
        ];
    }

    /// <summary>A distinctive middle slice, so a table is matched by content rather than by edges.</summary>
    private static string Sample(string text)
    {
        const int length = 40;

        return text.Length <= length ? text : text.Substring((text.Length - length) / 2, length);
    }

    private static string Normalise(string text)
    {
        var normalised = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                normalised.Append(char.ToLowerInvariant(c));
            }
        }

        return normalised.ToString();
    }
}
