using System.Text.RegularExpressions;

namespace IQWorkflow;

/// <summary>
/// Removes the checkbox glyph that OCR bleeds into the label beside it.
///
/// <b>Ported from <c>IQFlow.Adapters.Ingest</c> unchanged.</b> It is one of the ingestion defect
/// fixes the implementation plan permits (§4), and it is here because the pipeline analysis names
/// the artefact verbatim: <c>- [x] xInternal transfer</c>, on the certified Pension Sharing Annex
/// that carries the most severe finding of Test Case 3.
///
/// <b>What the stray letter actually costs.</b> The checkbox <em>state</em> survives conversion, so
/// a careful reader gets the right answer either way. What is lost is exact quotation: an extractor
/// asked for a verbatim quote emits <c>xInternal transfer</c>, the citation verifier searches the
/// source for that string and finds nothing, and a real finding is rejected as a fabricated
/// quotation. The analysis records exactly that shape of loss — untraceable cited quotes running at
/// 99 on one model.
///
/// <b>Deliberately narrow.</b> Only a lone <c>x</c> or <c>X</c> immediately before a capital letter
/// or a space is removed, and only inside a checkbox line. That is the shape the glyph produces; a
/// genuine label — <c>X-ray</c>, <c>Xmas bonus</c> — does not match, because a hyphen and a
/// lower-case letter both fail the test. A normaliser that ate the first letter of real answers
/// would be worse than the artefact it removes.
/// </summary>
public static partial class CheckboxNormaliser
{
    /// <summary>Cleans every checkbox line in a Markdown document.</summary>
    public static string Normalise(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        return markdown.Length == 0 ? markdown : CheckboxLineRegex().Replace(markdown, match =>
        {
            var box = match.Groups["box"].Value;
            var label = match.Groups["label"].Value;

            return $"- [{box}] {StripGlyph(label)}";
        });
    }

    /// <summary>
    /// Removes a leading glyph artefact from one label.
    ///
    /// Exposed because which labels are left alone matters as much as which are cleaned.
    /// </summary>
    public static string StripGlyph(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        var trimmed = label.TrimStart();

        if (trimmed.Length < 2 || trimmed[0] is not ('x' or 'X'))
        {
            return trimmed;
        }

        var next = trimmed[1];

        // "X Yes" — the glyph, then a space, then the real label.
        if (char.IsWhiteSpace(next))
        {
            return trimmed[1..].TrimStart();
        }

        // "xInternal" — the glyph run straight into a capitalised label. A lower-case letter after
        // the x means it is a word ("xylophone"), and punctuation means it is a name ("X-ray").
        return char.IsUpper(next) ? trimmed[1..] : trimmed;
    }

    [GeneratedRegex(@"^-\s\[(?<box>[ xX])\]\s+(?<label>.*)$", RegexOptions.Multiline)]
    private static partial Regex CheckboxLineRegex();
}
