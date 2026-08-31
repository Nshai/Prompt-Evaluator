// Ported verbatim from IQFlow.Adapters.Ingest, apart from the namespace and this note.
//
// The implementation plan (§4) permits the conversion stage as the one exception to "build against
// v1": these are defect fixes for losses the pipeline analysis names and nothing downstream can
// recover. Kept byte-faithful on purpose — a "tidied" copy is a second implementation to keep in
// step with the original, and the fixes are the product of measurement rather than of taste.
using System.Net;
using System.Text;

namespace IQWorkflow;

/// <summary>
/// Cross-checks Docling's markdown export against the richer DoclingDocument JSON and appends
/// anything present in the JSON but missing from the markdown — e.g. picture OCR/classification
/// annotations, key-value pairs, and form fields, which Docling's default markdown exporter
/// often omits or renders as placeholders.
/// </summary>
public static class MarkdownBackfiller
{
    public static string Backfill(string markdown, DoclingDocument document)
    {
        var normalizedMd = Normalize(markdown);
        var sections = new List<string>();

        // 1) Plain text items Docling produced but didn't emit into the markdown body
        //    (can happen for furniture-adjacent or reading-order edge cases). Page headers and
        //    footers (e.g. "Page 12") are structural page furniture, not content, and are always
        //    excluded. Remaining "text"/"section_header" items are checked on a word-overlap
        //    basis rather than exact substring: the form-field reconstruction pass legitimately
        //    re-orders/re-splits/re-labels this same content into "Label: Value" pairs, so the
        //    original concatenated string often no longer appears verbatim even though every
        //    word from it is still present in the output — an exact-substring check would
        //    therefore flag already-recovered content as "missing" and duplicate it here.
        var excludedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "page_header", "page_footer", "footnote", "checkbox_unselected", "checkbox_selected",
        };
        var missingTexts = document.Texts
            .Where(t => t.Label is null || !excludedLabels.Contains(t.Label))
            .Select(t => t.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!.Trim())
            .Where(t => t.Length > 3 && !IsAlreadyPresent(t, normalizedMd))
            .Distinct()
            .ToList();

        if (missingTexts.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("### Additional extracted text (not present in the base markdown export)");
            foreach (var t in missingTexts)
            {
                sb.AppendLine($"- {t}");
            }
            sections.Add(sb.ToString().TrimEnd());
        }

        // 2) OCR/classification annotations on pictures — markdown export typically only emits
        //    an <!-- image --> placeholder, dropping any OCR'd or captioned text entirely.
        var pictureNotes = new List<string>();
        for (var i = 0; i < document.Pictures.Count; i++)
        {
            var pic = document.Pictures[i];
            var texts = (pic.Annotations ?? [])
                .Select(a => a.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!.Trim())
                .Where(t => !normalizedMd.Contains(Normalize(t), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (texts.Count > 0)
            {
                pictureNotes.Add($"- Image {i + 1}: {string.Join(" ", texts)}");
            }
        }

        if (pictureNotes.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("### Image content recovered via OCR (not present in the base markdown export)");
            foreach (var n in pictureNotes)
            {
                sb.AppendLine(n);
            }
            sections.Add(sb.ToString().TrimEnd());
        }

        // 3) Key/value pairs (forms, structured field extraction) — usually excluded from markdown.
        var kvLines = new List<string>();
        foreach (var kv in document.KeyValueItems)
        {
            if (kv.Graph is null)
            {
                continue;
            }

            var cellsById = kv.Graph.Cells.ToDictionary(c => c.CellId);
            foreach (var link in kv.Graph.Links)
            {
                if (!cellsById.TryGetValue(link.SourceCellId, out var keyCell) ||
                    !cellsById.TryGetValue(link.TargetCellId, out var valueCell))
                {
                    continue;
                }

                var key = keyCell.Text?.Trim();
                var value = valueCell.Text?.Trim();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var line = $"{key}: {value}";
                if (!normalizedMd.Contains(Normalize(line), StringComparison.OrdinalIgnoreCase))
                {
                    kvLines.Add($"- {line}");
                }
            }
        }

        if (kvLines.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("### Key-value fields recovered from structured extraction");
            foreach (var l in kvLines.Distinct())
            {
                sb.AppendLine(l);
            }
            sections.Add(sb.ToString().TrimEnd());
        }

        // 4) Form field items — similarly often excluded from the default markdown export.
        var formLines = document.FormItems
            .Select(f => f.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!.Trim())
            .Where(t => !normalizedMd.Contains(Normalize(t), StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

        if (formLines.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("### Form fields recovered from structured extraction");
            foreach (var l in formLines)
            {
                sb.AppendLine($"- {l}");
            }
            sections.Add(sb.ToString().TrimEnd());
        }

        if (sections.Count == 0)
        {
            return markdown;
        }

        var result = new StringBuilder(markdown.TrimEnd());
        result.AppendLine();
        result.AppendLine();
        result.AppendLine("## Backfilled content (recovered from structured JSON export)");
        result.AppendLine(
            "_The following details were present in Docling's structured JSON output but omitted or " +
            "flattened by its default markdown export; they are appended here so no extracted " +
            "information is lost._");
        result.AppendLine();
        result.AppendLine(string.Join("\n\n", sections));

        return result.ToString();
    }

    private static string Normalize(string value) =>
        string.Join(' ', WebUtility.HtmlDecode(value).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// True if the exact normalized text is a substring of the markdown, OR if every
    /// "significant" word (length > 2, letters/digits) from the text already appears somewhere
    /// in the markdown — the latter catches content that survived reconstruction/reordering
    /// (e.g. split into separate "Label: Value" bullets) but is no longer contiguous.
    /// </summary>
    private static bool IsAlreadyPresent(string text, string normalizedMd)
    {
        var normalizedText = Normalize(text);
        if (normalizedMd.Contains(normalizedText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var words = normalizedText.Split(' ')
            .Where(w => w.Length > 2 && w.Any(char.IsLetterOrDigit))
            .ToList();

        if (words.Count == 0)
        {
            return false;
        }

        return words.All(w => normalizedMd.Contains(w, StringComparison.OrdinalIgnoreCase));
    }
}
