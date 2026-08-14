using System.Text;

namespace AiPromptEvaluator;

/// <summary>
/// Checks that a finding's quotations actually appear in the evidence that finding was given.
///
/// This exists because of an observed failure, not a hypothetical one. Asked to assess a risk
/// rating override, an assessor was handed a file note reading "You are happy to proceed with a
/// Risk rating of 6" and reported, inside quotation marks, "You are happy to proceed with a Risk
/// rating of 5" — which made the evidence agree with the recommendation and cleared the check.
/// Everything upstream had worked: the right passage was retrieved and presented.
///
/// No amount of instruction prevents this; the system prompt already demanded verbatim quotes.
/// What was missing was anyone checking. A quote is a claim about a document, and claims about
/// documents can be verified mechanically, without a model and without a judgement call.
///
/// The matching is deliberately generous. Whitespace is collapsed, typographic quotes and dashes
/// are folded to ASCII, and case is ignored, because a model reflowing a quote across lines is
/// not what this is looking for. Short quotes are skipped entirely: a five-word fragment appears
/// in too many places for its absence to mean anything, and a false accusation of fabrication is
/// its own kind of damage.
/// </summary>
public static class CitationVerifier
{
    /// <summary>
    /// Quotes shorter than this are not checked. Below roughly this length a quotation is a
    /// phrase rather than a claim, and near-misses are common enough that flagging them would
    /// train people to ignore the signal.
    /// </summary>
    public const int MinimumQuoteLength = 24;

    /// <summary>
    /// The quotes in <paramref name="citations"/> that do not appear in <paramref name="evidence"/>,
    /// in the order they were cited.
    /// </summary>
    public static IReadOnlyList<string> Unverified(
        IEnumerable<FindingCitation> citations,
        IEnumerable<string> evidence)
    {
        var haystack = Normalise(string.Join("\n", evidence));

        return citations
            .Select(c => c.Quote)
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Select(q => q!.Trim())
            .Where(q => q.Length >= MinimumQuoteLength)
            .Where(q => !IsPresent(q, haystack))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Whether one quote can be traced to the evidence, allowing for elision.
    ///
    /// A quote written as "the first part … the last part" is a claim about two spans, not one,
    /// and both are checked, in order. This is how a model quotes a long sentence without
    /// reproducing the middle of it, and it accounts for a share of the failures that were
    /// being reported as fabrication.
    ///
    /// **A near-miss rule was designed and then rejected here, and the reason is worth keeping.**
    /// Measuring the failures of one run suggested accepting a quote when a contiguous run
    /// covering most of it appears in the evidence — 39 of 105 failures looked like that. But
    /// the altered quotation this class was built to catch has a **96% contiguous run**: the
    /// evidence reads "You are happy to proceed with a Risk rating of 6" and the finding
    /// reported "…of 5", which differs by one character at the end. Any threshold loose enough
    /// to admit the near-misses admits that too, and admitting that is the whole failure. A
    /// quotation differing from its source by a digit is not a near-miss; it is the defect.
    ///
    /// So elision only — an explicit ellipsis is the model *saying* it has skipped something,
    /// which is a different claim from a silent alteration.
    /// </summary>
    private static bool IsPresent(string quote, string haystack)
    {
        var normalised = Normalise(quote);

        if (haystack.Contains(normalised, StringComparison.Ordinal))
        {
            return true;
        }

        var spans = normalised
            .Split(Ellipses, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToList();

        if (spans.Count < 2)
        {
            return false;
        }

        // In order, and each after the last — otherwise "A … B" would verify against evidence
        // reading "B … A", which reverses the meaning of most comparisons.
        var from = 0;

        foreach (var span in spans)
        {
            var at = haystack.IndexOf(span, from, StringComparison.Ordinal);

            if (at < 0)
            {
                return false;
            }

            from = at + span.Length;
        }

        return true;
    }

    private static readonly string[] Ellipses = ["...", "…"];

    /// <summary>
    /// Applies the check to a group finding, recording any quotes that could not be found.
    /// <see cref="GroupFinding.ParsedOutcome"/> then refuses to read the group as a pass.
    /// </summary>
    public static GroupFinding Verify(GroupFinding finding, IEnumerable<string> evidence)
    {
        var unverified = Unverified(finding.Citations, evidence);

        return unverified.Count == 0 ? finding : finding with { UnverifiedQuotes = unverified };
    }

    /// <summary>
    /// Folds away the differences that are formatting rather than substance: case, runs of
    /// whitespace, the typographic characters that survive a round trip through a document
    /// converter as something slightly different, and markdown table punctuation.
    ///
    /// Table punctuation is folded because so much of this evidence is tables. A quote lifted
    /// from a converted table carries its cell separators, and whether a model reproduces
    /// <c>| Savings | JS | 6,000 |</c> or <c>Savings JS 6,000</c> is a fact about markdown, not
    /// about whether it read the document. Collapsing the pipes to spaces — and the
    /// <c>|---|---|</c> separator rows with them — puts both spellings in the same shape.
    ///
    /// This cannot hide a substantive difference, because a digit or a word is not punctuation.
    /// </summary>
    internal static string Normalise(string text)
    {
        var sb = new StringBuilder(text.Length);
        var lastWasSpace = false;

        foreach (var raw in text)
        {
            var c = raw switch
            {
                '‘' or '’' or 'ʼ' or '`' => '\'',
                '“' or '”' => '"',
                '‐' or '‑' or '‒' or '–' or '—' or '−' => '-',
                ' ' or ' ' or ' ' => ' ',
                // Markdown table furniture. The pipe becomes a space so cells stay separated
                // as words; a run of them collapses with the surrounding whitespace below.
                '|' => ' ',
                _ => raw,
            };

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace && sb.Length > 0)
                {
                    sb.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            lastWasSpace = false;
            sb.Append(char.ToLowerInvariant(c));
        }

        return CollapseSeparatorRuns(sb.ToString()).TrimEnd();
    }

    /// <summary>
    /// Removes the dashes of a markdown separator row (<c>|---|:---:|</c>), which carry no
    /// content and survive the pipe fold as a run of hyphens and colons. Left in place they
    /// would be the only difference between two spellings of the same table.
    /// </summary>
    private static string CollapseSeparatorRuns(string text)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            if (text[i] is '-' or ':')
            {
                var start = i;

                while (i < text.Length && text[i] is '-' or ':')
                {
                    i++;
                }

                // Two or more is table furniture; a single hyphen is a hyphen, and a lone
                // colon punctuates a sentence.
                if (i - start >= 2)
                {
                    if (sb.Length > 0 && sb[^1] != ' ')
                    {
                        sb.Append(' ');
                    }

                    continue;
                }

                sb.Append(text, start, i - start);
                continue;
            }

            sb.Append(text[i]);
            i++;
        }

        return sb.ToString();
    }
}
