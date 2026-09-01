using System.Text;

namespace AiPromptEvaluator;

/// <summary>
/// The whole assertion side of the case, compressed to one line per populated path, printed to
/// every group.
///
/// <b>Why this exists.</b> A group is shown the canonical fragments its own plan selected and
/// nothing else, so which of the report's claims may be compared with which piece of evidence is
/// decided in advance, by hand, in JSON. Measured, that decision is wrong often enough to dominate
/// every other source of loss:
///
/// <list type="bullet">
/// <item>The most severe finding of one case had its claim in <c>G9.2</c> and the certified
/// document that contradicts it in the packs of <c>G9.4</c> and <c>G9.10</c>. <b>No prompt in the
/// run contained both halves</b>, on either model, across three runs.</item>
/// <item>Two decisive passages sat at <b>rank 1</b> of a mandated group's pack and were missed by
/// both models — so neither reranking nor a bigger pack could have recovered them.</item>
/// <item>~20% of every model is selected by no group at all. See <see cref="PathCoverage"/>.</item>
/// </list>
///
/// <b>Why it is safe to add.</b> The one measured experiment on adding context to this prompt made
/// things worse: raising the pack from 24 to 27 passages — 13% more prompt — cost eleven points of
/// recall. The measured relationship between pack size and recall in this pipeline is negative.
/// So the digest is not additional volume. It is designed to be spent against the pack:
/// four hundred lines at ~25 tokens is ~8–10k tokens, and halving the pack from 24 passages to 12
/// returns ~6–7k more than that. <b>Net prompt size falls</b>, and what it falls by is the part
/// the measurements say is worth least.
///
/// <b>Why one line and not the fragment.</b> A canonical fragment is indented JSON with a
/// provenance block on every object; the group's own paths are printed that way and should stay
/// that way. This is the other 95% of the model, and it is here to be recognised, not read: the
/// assessor needs to be able to notice that a claim it is holding contradicts something stated
/// elsewhere in the report, and then say so. A path, a value and a heading is enough to notice
/// with and enough to cite.
/// </summary>
public static class AssertionDigest
{
    /// <summary>
    /// The heading the digest is printed under. Public because the prompt tests assert on it and
    /// because a reader of a logged prompt should be able to find the section by name.
    /// </summary>
    public const string Heading = "#### Everything else the report asserts (one line per value)";

    /// <summary>
    /// Renders the digest, or null where there is nothing to print or the budget is zero.
    /// </summary>
    /// <param name="leaves">Every populated leaf, from <see cref="CanonicalPaths.Enumerate(string)"/>.</param>
    /// <param name="alreadyShown">
    /// The paths this group is being given in full. Their lines are dropped: the group reads the
    /// fragment itself a few hundred characters earlier, and repeating a value the assessor has
    /// already been shown in its own section is the volume the pack cut was spent to buy.
    /// </param>
    /// <param name="maxCharacters">
    /// The budget. Applied by dropping whole lines from the end rather than by truncating the
    /// text, so the digest is never cut mid-path — a half-written path is worse than an absent
    /// one, because it looks like a value the model can cite.
    /// </param>
    public static string? Render(
        IReadOnlyList<CanonicalLeaf> leaves,
        IEnumerable<string> alreadyShown,
        int maxCharacters)
    {
        if (maxCharacters <= 0 || leaves.Count == 0)
        {
            return null;
        }

        var shown = alreadyShown
            .Select(p => CanonicalPaths.Normalise(p.Trim()))
            .Where(p => p.Length > 0)
            .ToList();

        var lines = leaves
            .Where(leaf => !shown.Any(s => PathCoverage.Covers(s, leaf.NormalisedPath)))
            .Select(Line)
            .ToList();

        if (lines.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();

        sb.AppendLine(Heading);
        sb.AppendLine(
            "This is the rest of the extracted report — every value it states, outside the paths "
            + "printed in full above. It is here for one purpose: a claim that contradicts the one "
            + "you are assessing is frequently recorded here rather than in your own paths, and "
            + "until now no prompt in a run contained both halves of such a pair. Read it for "
            + "contradictions with the requirement in front of you. Do not assess these values "
            + "against their own requirements — another group has them, and a finding raised twice "
            + "is a finding a reviewer reads twice.");
        sb.AppendLine();
        sb.AppendLine(
            "Format: `path` = value ⟨where the extraction read it⟩. To cite one, quote the value "
            + "and name its path; these are the report's own words and need no passage.");
        sb.AppendLine();

        var budget = maxCharacters;
        var written = 0;

        foreach (var line in lines)
        {
            if (line.Length + 1 > budget)
            {
                break;
            }

            sb.AppendLine(line);
            budget -= line.Length + 1;
            written++;
        }

        if (written == 0)
        {
            return null;
        }

        // A truncated digest that does not say it is truncated is the assertion-side version of
        // the eviction problem: the assessor concludes the report is silent on a point the model
        // records, and nothing in the output shows that it was simply not printed.
        if (written < lines.Count)
        {
            sb.AppendLine();
            sb.AppendLine(
                $"({lines.Count - written} further value(s) did not fit this budget. The report "
                + "states them; treat this list as incomplete rather than as the whole model.)");
        }

        return sb.ToString();
    }

    /// <summary>
    /// One line. Indexed path rather than the plan-shaped one, because a reader following a
    /// citation needs the element the value is actually in.
    /// </summary>
    private static string Line(CanonicalLeaf leaf) =>
        leaf.Source is { Length: > 0 } source
            ? $"`{leaf.Path}` = {leaf.Value} ⟨{source}⟩"
            : $"`{leaf.Path}` = {leaf.Value}";
}
