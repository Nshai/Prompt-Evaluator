// Ported verbatim from IQFlow.Adapters.Ingest, apart from the namespace and this note.
//
// The implementation plan (§4) permits the conversion stage as the one exception to "build against
// v1": these are defect fixes for losses the pipeline analysis names and nothing downstream can
// recover. Kept byte-faithful on purpose — a "tidied" copy is a second implementation to keep in
// step with the original, and the fixes are the product of measurement rather than of taste.
using System.Text.RegularExpressions;
using System.Net;

namespace IQWorkflow;

/// <summary>
/// Some source documents (typically PDF forms) lay fields out as free-floating positioned text
/// rather than ruled tables — e.g. a "Full Name" label box above a "Yvette McGealy" value box,
/// side-by-side with an "Age" label above "58". Docling has no table structure to anchor these,
/// so it emits them into the markdown export as a flat, reading-order sequence of paragraphs
/// that reads as a scrambled jumble of labels and values with no visible pairing.
///
/// Two separate scrambling effects have to be undone:
///  1. Docling sometimes concatenates several visually-separate text fragments (different rows
///     and/or columns) into a single JSON text item's <c>Text</c> string. Each fragment has its
///     own bounding box in <c>prov[]</c> (with a <c>charspan</c> identifying which substring of
///     <c>Text</c> it corresponds to), so we must operate at the granularity of individual
///     fragments, not whole items.
///  2. Docling's flat <c>texts[]</c> array order (and therefore which <c>section_header</c> an
///     item is nested under) does not always match true top-to-bottom visual position — text
///     belonging to one section can appear, in array order, underneath a *later* section's
///     heading. So fragments must be re-attributed to whichever heading is immediately above
///     them on the page (by vertical position), not whichever heading precedes them in the array.
///
/// This pass reconstructs true reading order from bounding boxes, re-attributes fragments to
/// their visually-correct section, clusters them into rows/columns, and pairs label-like rows
/// with value-like rows or same-row label/value pairs. It never fabricates a pairing it isn't
/// reasonably confident about — anything ambiguous is left as a plain line of source text.
/// </summary>
public static partial class FormFieldReconstructor
{
    private const double RowTolerance = 4.0;
    private const double ColumnTolerance = 15.0;

    [GeneratedRegex(@"^(?<hashes>#{1,6})[ \t]+(?<text>.+?)[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    private sealed record Fragment(int Page, double T, double L, string Text, bool IsCheckbox = false, bool CheckboxChecked = false);

    private sealed record HeaderMarker(int Page, double T, string Text);

    public static string Reconstruct(string markdown, DoclingDocument document)
    {
        var fragments = ExpandFragments(document.Texts, out var headers);
        if (headers.Count == 0 || fragments.Count == 0)
        {
            return markdown;
        }

        // Re-attribute every body fragment to the header immediately above it on its page
        // (smallest header T that is still <= the fragment's T... actually largest header T that
        // is >= the fragment's T, since higher T = higher on the page and headers precede body
        // text below them), not whichever header the original JSON item happened to be nested
        // under.
        var byHeader = new Dictionary<HeaderMarker, List<Fragment>>();
        foreach (var header in headers)
        {
            byHeader[header] = [];
        }

        foreach (var frag in fragments)
        {
            var candidates = headers.Where(h => h.Page == frag.Page && h.T >= frag.T - RowTolerance).ToList();
            var owner = candidates.Count > 0
                ? candidates.OrderBy(h => h.T).First()
                : headers.Where(h => h.Page == frag.Page).OrderByDescending(h => h.T).LastOrDefault();

            if (owner is null)
            {
                continue; // No header found on this page at all; leave the fragment's text alone.
            }

            byHeader[owner].Add(frag);
        }

        // Walk headers in original document order (matches markdown heading order 1:1, since
        // headings themselves are not subject to the body-text scrambling described above).
        // All matching/reconstruction decisions are made against the ORIGINAL, unmutated
        // markdown; replacements are then applied back-to-front (highest index first) so that
        // earlier heading offsets remain valid as later blocks are replaced.
        var headingOccurrence = 0;
        var headingMatches = HeadingRegex().Matches(markdown);
        var pendingReplacements = new List<(int HeadingIndex, List<Fragment> Items, List<string>? Lines)>();

        foreach (var header in headers)
        {
            var items = byHeader[header];
            if (headingOccurrence >= headingMatches.Count)
            {
                break;
            }

            var headingMatch = headingMatches[headingOccurrence];
            headingOccurrence++;

            var headingText = WebUtility.HtmlDecode(headingMatch.Groups["text"].Value.Trim());
            if (!string.Equals(headingText, header.Text, StringComparison.OrdinalIgnoreCase))
            {
                continue; // Heading order assumption didn't hold for this document; skip safely.
            }

            if (items.Count < 1)
            {
                // No fragments are visually attributed to this heading at all (they were all
                // re-attributed to other sections above). If the section's existing raw markdown
                // block is nothing but bare, unlabeled short values (e.g. "No\n\nNo\n\nNo" —
                // answers to questions that visually belong to a preceding section), it reads as
                // confusing orphaned content with no caption; suppress it rather than leave a
                // heading followed by meaningless bare values.
                pendingReplacements.Add((headingMatch.Index, items, null));
                continue;
            }

            var reconstructedLines = ReconstructSection(items);
            if (reconstructedLines is null)
            {
                continue;
            }

            pendingReplacements.Add((headingMatch.Index, items, reconstructedLines));
        }

        foreach (var (headingIndex, items, lines) in pendingReplacements.OrderByDescending(p => p.HeadingIndex))
        {
            markdown = items.Count == 0
                ? TrySuppressOrphanedBlock(markdown, headingIndex)
                : TryReplaceHeadingBlock(markdown, headingIndex, lines!);
        }

        return markdown;
    }

    /// <summary>
    /// Expands every "text"/"section_header" item into its constituent (fragment-of-text, bbox)
    /// pairs using each <c>prov</c> entry's charspan, since a single item's <c>Text</c> can be a
    /// concatenation of several visually-separate fragments.
    /// </summary>
    private static List<Fragment> ExpandFragments(List<DoclingTextItem> texts, out List<HeaderMarker> headers)
    {
        var fragments = new List<Fragment>();
        headers = [];

        foreach (var item in texts)
        {
            if (string.IsNullOrWhiteSpace(item.Text) || item.Prov is not { Count: > 0 })
            {
                continue;
            }

            var isHeader = string.Equals(item.Label, "section_header", StringComparison.OrdinalIgnoreCase);
            var isText = string.Equals(item.Label, "text", StringComparison.OrdinalIgnoreCase);
            var isCheckboxUnselected = string.Equals(item.Label, "checkbox_unselected", StringComparison.OrdinalIgnoreCase);
            var isCheckboxSelected = string.Equals(item.Label, "checkbox_selected", StringComparison.OrdinalIgnoreCase);
            var isCheckbox = isCheckboxUnselected || isCheckboxSelected;
            if (!isHeader && !isText && !isCheckbox)
            {
                continue; // Skip page headers/footers, captions, pictures, etc.
            }

            foreach (var prov in item.Prov)
            {
                if (prov.BBox is null)
                {
                    continue;
                }

                string fragText;
                if (prov.CharSpan is { Length: 2 } span &&
                    span[0] >= 0 && span[1] <= item.Text.Length && span[1] > span[0])
                {
                    fragText = item.Text[span[0]..span[1]];
                }
                else
                {
                    fragText = item.Text;
                }

                fragText = fragText.Trim();
                if (fragText.Length == 0)
                {
                    continue;
                }

                if (isHeader)
                {
                    headers.Add(new HeaderMarker(prov.PageNo, prov.BBox.T, fragText));
                }
                else
                {
                    fragments.Add(new Fragment(prov.PageNo, prov.BBox.T, prov.BBox.L, fragText, isCheckbox, isCheckboxSelected));
                }
            }
        }

        return fragments;
    }

    /// <summary>
    /// Clusters a section's fragments into visual rows (page + vertical position), pairs
    /// label-like content with value-like content either within the same row (side-by-side
    /// question/answer) or across a label row and the value row(s) immediately below it aligned
    /// to the same columns, and falls back to plain bullet lines for anything ambiguous.
    /// Returns null if nothing could be confidently paired, so the caller leaves the section's
    /// markdown untouched.
    /// </summary>
    private static List<string>? ReconstructSection(List<Fragment> fragments)
    {
        var rows = new List<List<Fragment>>();
        foreach (var pageGroup in fragments.GroupBy(f => f.Page).OrderBy(g => g.Key))
        {
            var ordered = pageGroup.OrderByDescending(f => f.T).ThenBy(f => f.L).ToList();
            foreach (var frag in ordered)
            {
                var row = rows.Count > 0 ? rows[^1] : null;
                if (row is not null && row[0].Page == frag.Page && Math.Abs(row[0].T - frag.T) <= RowTolerance)
                {
                    row.Add(frag);
                }
                else
                {
                    rows.Add([frag]);
                }
            }
        }

        foreach (var row in rows)
        {
            row.Sort((a, b) => a.L.CompareTo(b.L));
        }

        var consumed = new bool[rows.Count][];
        for (var r = 0; r < rows.Count; r++)
        {
            consumed[r] = new bool[rows[r].Count];
        }

        var lines = new List<string>();
        var didReconstructAny = false;

        // Checkbox fields render as an explicit "Label: [ ]"/"[x]" pair on their own and never
        // participate in label/value pairing with neighboring fragments (they have no separate
        // "value" fragment — the checked/unchecked state IS the value).
        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < rows[r].Count; c++)
            {
                if (rows[r][c].IsCheckbox)
                {
                    var mark = rows[r][c].CheckboxChecked ? "x" : " ";
                    lines.Add($"- **{rows[r][c].Text}:** [{mark}]");
                    consumed[r][c] = true;
                    didReconstructAny = true;
                }
            }
        }

        // Pass 1: within-row label/value pairs for genuine inline questions (e.g. "Do you have
        // any assets?" | "Yes"), and also for a label immediately followed by an unambiguous
        // ("strong") value such as a phone number, email address, date or currency amount (e.g.
        // "Mobile" | "07736927073") — these are safe to pair on content alone even without a "?"
        // because the value type itself is unambiguous. Generic title-case-looking values (e.g.
        // a person's name) are deliberately excluded here since they're indistinguishable from
        // labels by content alone; those are handled by pass 2 below, which aligns a label row
        // with the value row beneath it by column position instead.
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var c = 0; c < row.Count - 1; c++)
            {
                if (consumed[r][c] || consumed[r][c + 1])
                {
                    continue;
                }

                var isQuestion = row[c].Text.TrimEnd().EndsWith('?') && LooksLikeValue(row[c + 1].Text);
                var isStrongPair = LooksLikeLabel(row[c].Text) && IsStrongValue(row[c + 1].Text);

                // A row containing exactly two fragments is, in practice, always a label/value
                // pair laid out in two columns — even when the value's content alone is
                // ambiguous (e.g. "No Requirement", "Related Products/Services Only") and
                // wouldn't otherwise satisfy LooksLikeValue or IsStrongValue. Column-based Pass 2
                // below would otherwise cross-pair this label with an unrelated row above/below
                // it, so resolve simple two-item rows here instead.
                var isTwoColRow = row.Count == 2 && LooksLikeLabel(row[c].Text);

                if (isQuestion || isStrongPair || isTwoColRow)
                {
                    lines.Add($"- **{row[c].Text}:** {row[c + 1].Text}");
                    consumed[r][c] = true;
                    consumed[r][c + 1] = true;
                    didReconstructAny = true;
                }
            }
        }

        // Pass 2: label row followed by a value row, paired by matching column position. Only
        // applies to genuine multi-column header rows (>= 2 items) — a single, single-column
        // label per row (e.g. a plain vertical list of field names with no adjacent value
        // column) has no reliable "row below is its value" relationship and must be left alone
        // rather than guessing that the next label-only row is actually its value.
        for (var r = 0; r < rows.Count - 1; r++)
        {
            var row = rows[r];
            if (row.Count < 2)
            {
                continue;
            }

            var labelIdxs = Enumerable.Range(0, row.Count).Where(i => !consumed[r][i] && LooksLikeLabel(row[i].Text)).ToList();
            if (labelIdxs.Count == 0)
            {
                continue;
            }

            var next = rows[r + 1];
            var pairedHere = false;
            foreach (var li in labelIdxs)
            {
                var label = row[li];
                int? matchIdx = null;
        var bestDist = double.MaxValue;
                for (var vi = 0; vi < next.Count; vi++)
                {
                    if (consumed[r + 1][vi])
                    {
                        continue;
                    }

                    var dist = Math.Abs(next[vi].L - label.L);
                    if (dist <= ColumnTolerance && dist < bestDist)
                    {
                        bestDist = dist;
                        matchIdx = vi;
                    }
                }

                if (matchIdx is null)
                {
                    continue;
                }

                lines.Add($"- **{label.Text}:** {next[matchIdx.Value].Text}");
                consumed[r][li] = true;
                consumed[r + 1][matchIdx.Value] = true;
                didReconstructAny = true;
                pairedHere = true;
            }

            // A label row may have additional value rows stacked below it (e.g. a contact-info
            // block with two values under one label) — attach any immediately-following rows
            // whose sole content aligns to an already-paired label's column as extra values.
            if (pairedHere)
            {
                for (var r2 = r + 2; r2 < rows.Count; r2++)
                {
                    var extraRow = rows[r2];
                    var attachedAny = false;
                    foreach (var li in labelIdxs)
                    {
                        if (!consumed[r][li])
                        {
                            continue; // This label wasn't successfully paired in pass 2 above.
                        }

                        var label = row[li];
                        int? matchIdx = null;
                        for (var vi = 0; vi < extraRow.Count; vi++)
                        {
                            if (!consumed[r2][vi] && Math.Abs(extraRow[vi].L - label.L) <= ColumnTolerance)
                            {
                                matchIdx = vi;
                                break;
                            }
                        }

                        if (matchIdx is null)
                        {
                            continue;
                        }

                        lines.Add($"- **{label.Text} (additional):** {extraRow[matchIdx.Value].Text}");
                        consumed[r2][matchIdx.Value] = true;
                        attachedAny = true;
                    }

                    if (!attachedAny)
                    {
                        break;
                    }
                }
            }
        }

        // Anything left unconsumed is emitted verbatim. Unconsumed fragments that look like a
        // field label (question-ending or title-case field name) are rendered as an explicit
        // "Label: [blank]" pair, since these are almost always fields whose value truly wasn't
        // captured by Docling (e.g. checkbox/tickbox fields with no adjacent text) — this is far
        // clearer to a reader than a bare, structure-less list of labels with no separator at
        // all. Anything else is emitted as a plain bullet, never fabricated.
        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < rows[r].Count; c++)
            {
                if (!consumed[r][c])
                {
                    var text = rows[r][c].Text;
                    if (LooksLikeLabel(text))
                    {
                        lines.Add($"- **{text}:** [blank]");
                        didReconstructAny = true;
                    }
                    else
                    {
                        lines.Add($"- {text}");
                    }
                }
            }
        }

        return didReconstructAny ? lines : null;
    }

    /// <summary>
    /// A stricter subset of <see cref="LooksLikeValue"/> for values whose content is unambiguous
    /// enough (phone number, email, date, currency amount, plain Yes/No) to safely pair with an
    /// immediately-preceding same-row label even without the label ending in "?" — unlike a
    /// generic proper-noun/title-case value (e.g. a person's name), which is indistinguishable
    /// from a label by content alone and must instead be paired by column-position (pass 2).
    /// </summary>
    private static bool IsStrongValue(string text)
    {
        var t = text.Trim();
        if (t.Length == 0)
        {
            return false;
        }

        return t is "Yes" or "No" or "N/A" or "n/a"
            || Regex.IsMatch(t, @"^[£$€]?\s*[\d,]+(\.\d+)?%?$")
            || Regex.IsMatch(t, @"^\d{1,4}([/-]\d{1,4}){1,2}$")
            || Regex.IsMatch(t, @"^\+?[\d\s()-]{7,}$")
            || t.Contains('@');
    }

    private static bool LooksLikeValue(string text)
    {
        var t = text.Trim();
        if (t.Length == 0)
        {
            return false;
        }

        return t is "Yes" or "No" or "N/A" or "n/a"
            || Regex.IsMatch(t, @"^[£$€]?\s*[\d,]+(\.\d+)?%?$")
            || Regex.IsMatch(t, @"^\d{1,4}([/-]\d{1,4}){1,2}$")
            || t.Contains('@')
            || (t.Length <= 40 && !t.EndsWith('?') && char.IsUpper(t[0]) && t.Split(' ').Length <= 5 && !LooksLikeLabel(t));
    }

    private static bool LooksLikeLabel(string text)
    {
        var t = text.Trim();
        if (t.Length == 0 || t.Length > 220)
        {
            return false;
        }

        if (t.EndsWith('?'))
        {
            return true;
        }

        // Field-name-like: title case words, no currency/digits-only content, reasonably short.
        return !Regex.IsMatch(t, @"^[£$€]?\s*[\d,]+(\.\d+)?%?$")
            && t is not ("Yes" or "No")
            && !t.Contains('@')
            && !Regex.IsMatch(t, @"^\d{1,4}([/-]\d{1,4}){1,2}$")
            && t.Split(' ').Length <= 25;
    }

    /// <summary>
    /// Removes a heading and its block entirely when the heading has zero fragments attributed
    /// to it (all of its original content was re-attributed elsewhere by the visual re-attribution
    /// pass) AND its leftover raw markdown block is nothing but short, unlabeled bare values (no
    /// "label: value" structure, no markdown table/heading structure) — i.e. genuinely orphaned,
    /// meaningless content that would otherwise read as a confusing caption-less list. Leaves the
    /// markdown untouched if the block contains anything that looks like real information (longer
    /// free text, an existing table, etc.), erring on the side of not deleting real content.
    /// </summary>
    private static string TrySuppressOrphanedBlock(string markdown, int headingIndex)
    {
        var headingMatches = HeadingRegex().Matches(markdown);
        var headingMatch = headingMatches.FirstOrDefault(m => m.Index == headingIndex);
        if (headingMatch is null)
        {
            return markdown;
        }

        var blockStart = headingMatch.Index + headingMatch.Length;
        var nextHeading = headingMatches.FirstOrDefault(m => m.Index >= blockStart);
        var blockEnd = nextHeading?.Index ?? markdown.Length;

        var block = markdown[blockStart..blockEnd];
        var blockLines = block.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        if (blockLines.Count == 0)
        {
            // The heading has no attributed fragments AND no existing markdown content at all —
            // Docling extracted nothing for this section (e.g. a heading with a genuinely blank
            // form area beneath it). Make that explicit rather than leaving a heading followed by
            // nothing, which reads as if content was lost.
            var placeholder = "\n\n[No details]\n\n";
            return markdown[..blockStart] + placeholder + markdown[blockEnd..];
        }

        var allBareValues = blockLines.All(l =>
            !l.StartsWith('#') && !l.StartsWith('|') && !l.Contains(':') && l.Length <= 20);

        if (!allBareValues)
        {
            return markdown;
        }

        return string.Concat(markdown.AsSpan(0, headingMatch.Index), markdown.AsSpan(blockEnd));
    }

    /// <summary>
    /// Replaces the leading run of loose (non-table, non-heading) lines at the start of the
    /// markdown block belonging to the heading at <paramref name="headingIndex"/> with the
    /// reconstructed lines, leaving any subsequent markdown table/sub-heading content in that
    /// same section block untouched. Some sections mix free-floating scrambled form fields
    /// (which this pass fixes) with a genuine, separately-rendered markdown table further down
    /// (e.g. a summary question followed by a itemized table) — splicing only the leading loose
    /// portion lets us fix the fields without disturbing the legitimate table beneath them.
    /// </summary>
    private static string TryReplaceHeadingBlock(
        string markdown, int headingIndex, List<string> reconstructedLines)
    {
        var headingMatches = HeadingRegex().Matches(markdown);
        var headingMatch = headingMatches.FirstOrDefault(m => m.Index == headingIndex);
        if (headingMatch is null)
        {
            return markdown;
        }

        var blockStart = headingMatch.Index + headingMatch.Length;
        var nextHeading = headingMatches.FirstOrDefault(m => m.Index >= blockStart);
        var blockEnd = nextHeading?.Index ?? markdown.Length;

        var block = markdown[blockStart..blockEnd];

        // Find where the leading loose-text portion ends: the first line (ignoring blanks) that
        // looks like markdown structure (a table row/separator or a nested heading).
        var lineMatches = Regex.Matches(block, @"[^\n]*\n?");
        var looseEnd = block.Length;
        var sawAnyLooseLine = false;
        var cursor = 0;
        foreach (Match lm in lineMatches)
        {
            if (lm.Length == 0)
            {
                continue;
            }

            var trimmed = lm.Value.Trim();
            if (trimmed.Length > 0)
            {
                var isCheckboxLine = Regex.IsMatch(trimmed, @"^-\s*\[[ xX]?\]");
                if (!isCheckboxLine && (trimmed.StartsWith('#') || trimmed.StartsWith('|') || trimmed.StartsWith('•') || trimmed.StartsWith('-')))
                {
                    looseEnd = cursor;
                    break;
                }

                sawAnyLooseLine = true;
            }

            cursor += lm.Length;
        }

        if (!sawAnyLooseLine)
        {
            return markdown; // Nothing loose to replace (block was empty or pure structure already).
        }

        var loosePortion = block[..looseEnd];
        var looseLines = loosePortion.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (looseLines.Count == 0)
        {
            return markdown;
        }

        var replacement = "\n\n" + string.Join("\n", reconstructedLines) + "\n\n";
        var rest = block[looseEnd..];
        return markdown[..blockStart] + replacement + rest + markdown[blockEnd..];
    }
}