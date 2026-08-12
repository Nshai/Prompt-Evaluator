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
            .Where(q => !haystack.Contains(Normalise(q), StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

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
    /// whitespace, and the typographic characters that survive a round trip through a document
    /// converter as something slightly different.
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

        return sb.ToString().TrimEnd();
    }
}
