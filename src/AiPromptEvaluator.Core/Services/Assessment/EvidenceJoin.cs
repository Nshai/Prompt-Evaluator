using System.Text;

namespace AiPromptEvaluator;

/// <summary>
/// One assertion from elsewhere in the report that a passage in this group's pack bears on.
/// </summary>
/// <param name="Path">The canonical path the assertion sits at, indices and all.</param>
/// <param name="Value">What the report says there.</param>
/// <param name="Matched">The literal the passage and the assertion share.</param>
/// <param name="PassageId">The pack passage that carries it, as the assessor sees it numbered.</param>
/// <param name="Source">Where the extraction read the assertion, when it recorded that.</param>
public sealed record JoinedAssertion(
    string Path,
    string Value,
    string Matched,
    string PassageId,
    string? Source);

/// <summary>
/// Move 5 of the pipeline plan: <b>the code-level join.</b>
///
/// <b>The failure.</b> Which of the report's claims may be compared with which piece of evidence is
/// decided in advance, by hand, in ten plan files — and it is wrong often enough to dominate every
/// other source of loss. The most severe finding of one case had its claim selected by <c>G9.2</c>
/// and the certified document that contradicts it retrieved into <c>G9.4</c> and <c>G9.10</c>; no
/// prompt in the run held both halves, on either model, across three runs. Nothing was missing and
/// nothing was mis-ranked. The two halves were simply never introduced.
///
/// <b>The join.</b> Every passage a group actually received is matched against every assertion in
/// the canonical model — not just the ones this group's plan selected — and strong matches are
/// printed as "assertions elsewhere in the report this evidence may bear on". The assessor is then
/// holding both halves and can say the two disagree.
///
/// <b>Why numeric literals.</b> The joins that matter turn on an exact token: <c>48.06</c> against
/// <c>45.91</c>, <c>0.19</c> against <c>0.21</c>, <c>£9,838.36</c> against <c>£9,839.36</c>. Those
/// are decidable in code, they are the same literals <see cref="NumericComparison"/> already
/// extracts, and a match on one is evidence a reader can check. Matching on prose similarity would
/// need a threshold nobody can defend and would reintroduce the guessing this move exists to
/// remove.
///
/// <b>Why it is capped.</b> The routed-inconsistency mechanism this follows was measured
/// broadcasting one item into 26 of 88 groups, 92 times over. A join that fires on every figure
/// would be that failure again, and a prompt that flags everything flags nothing.
/// </summary>
public static class EvidenceJoin
{
    /// <summary>The heading the join is printed under.</summary>
    public const string Heading =
        "#### Assertions elsewhere in the report this evidence may bear on";

    /// <summary>
    /// How many joins one group may be shown, most distinctive first.
    ///
    /// Small on purpose. This is a prompt section competing for attention with the pack itself,
    /// and the measured relationship between prompt volume and recall in this pipeline is
    /// negative.
    /// </summary>
    public const int MaxPerGroup = 8;

    /// <summary>
    /// Figures too common to carry information. A join on "1" or "2024" is a coincidence with a
    /// heading, and every one of them spends a slot a real match needed.
    ///
    /// <b>The filter is on distinctiveness, not on size.</b> 0.19 and 0.21 are the two Aviva
    /// charges whose disagreement is a benchmark finding, and both are smaller than every value
    /// this rejects — what makes them joinable is the decimal place, not the magnitude.
    /// </summary>
    internal static bool IsDistinctive(NumericComparison.Figure figure)
    {
        var value = Math.Abs(figure.Value);

        // A fractional part is the strongest signal available: report prose is full of round
        // counts and years, and almost empty of two-decimal quantities that are not quantities.
        if (value != decimal.Truncate(value))
        {
            return true;
        }

        // A whole number has to be large to be distinctive, and must not be a plausible year —
        // dates are the single most common false join in a document set full of them.
        return value >= 1000m && !(value >= 1900m && value <= 2100m);
    }

    /// <summary>
    /// The assertions elsewhere in the report that this group's own passages touch.
    /// </summary>
    /// <param name="leaves">Every populated canonical leaf, from <see cref="CanonicalPaths"/>.</param>
    /// <param name="ownPaths">
    /// The paths this group's plan already selects. Excluded, with their children: an assertion the
    /// group is being shown in full a few lines earlier is not news, and repeating it spends the
    /// cap on something the assessor already has.
    /// </param>
    /// <param name="passagesById">The pack, keyed by the id the assessor sees.</param>
    /// <param name="max">The cap. See <see cref="MaxPerGroup"/>.</param>
    /// <param name="currentOnly">
    /// The plan's <c>modality: CurrentOnly</c> directive. A projection, an assumption or an
    /// intention set against a figure the file records is not a discrepancy, and joining them
    /// invites a finding that says it is. A measured extract carries 60 Projected, 29 Assumed and
    /// 13 Conditional values.
    /// </param>
    public static IReadOnlyList<JoinedAssertion> For(
        IReadOnlyList<CanonicalLeaf> leaves,
        IEnumerable<string> ownPaths,
        IReadOnlyDictionary<string, string> passagesById,
        int max = MaxPerGroup,
        bool currentOnly = false)
    {
        if (max <= 0 || leaves.Count == 0 || passagesById.Count == 0)
        {
            return [];
        }

        var own = ownPaths
            .Select(p => CanonicalPaths.Normalise(p.Trim()))
            .Where(p => p.Length > 0)
            .ToList();

        // The pack's figures, once. A group at the cap holds 24 passages and re-extracting per
        // leaf would be a few hundred thousand regex passes for the same answer.
        var inPack = passagesById
            .SelectMany(p => NumericComparison.Extract(p.Value)
                .Where(IsDistinctive)
                .Select(f => (PassageId: p.Key, Figure: f)))
            .ToList();

        if (inPack.Count == 0)
        {
            return [];
        }

        var joins = new List<JoinedAssertion>();
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var leaf in leaves)
        {
            if (own.Any(o => PathCoverage.Covers(o, leaf.NormalisedPath)))
            {
                continue;
            }

            // The plan's modality directive. Refusing here rather than downstream matters: an
            // assessor shown a projection beside a current figure will frequently call the pair a
            // contradiction, and that is one of the false positives this whole section is most
            // exposed to.
            if (currentOnly && !leaf.IsCurrent)
            {
                continue;
            }

            foreach (var figure in NumericComparison.Extract(leaf.Value).Where(IsDistinctive))
            {
                var match = inPack.FirstOrDefault(
                    c => c.Figure.Value == figure.Value && Comparable(c.Figure.Kind, figure.Kind));

                if (match.PassageId is null)
                {
                    continue;
                }

                // One join per assertion path. A value repeated down a table would otherwise fill
                // the cap with one fact stated eleven times.
                if (!claimed.Add(leaf.Path))
                {
                    continue;
                }

                joins.Add(new JoinedAssertion(
                    leaf.Path, leaf.Value, figure.AsWritten, match.PassageId, leaf.Source));

                break;
            }
        }

        // Most distinctive first, so the cap keeps the joins worth reading: a fractional value
        // outranks a round one, and a larger magnitude breaks the tie. Ordinal path as the final
        // key, so the same model and pack always produce the same list.
        return joins
            .OrderByDescending(j => Weight(j.Matched))
            .ThenBy(j => j.Path, StringComparer.Ordinal)
            .Take(max)
            .ToList();
    }

    /// <summary>
    /// Whether two figures of the same value could be the same fact.
    ///
    /// <b>Only two <i>known and different</i> units can refuse each other.</b> A 45.91 that is a
    /// percentage and a 45.91 that is a sum of money are not the same fact, and joining them
    /// manufactures a contradiction out of a coincidence.
    ///
    /// <b>Plain is not a unit — it is the absence of one</b>, and refusing it would have made this
    /// whole mechanism nearly inert. The canonical model stores numbers stripped of symbols and
    /// separators, so a monetary amount arrives as <c>116997.47</c> while the document that
    /// evidences it writes <c>£116,997.47</c>, and a charge arrives as <c>0.19</c> against a
    /// document's <c>0.19%</c>. Requiring the kinds to be equal rejected both — which is to say it
    /// rejected the two shapes the benchmark findings actually take.
    /// </summary>
    internal static bool Comparable(NumericComparison.FigureKind a, NumericComparison.FigureKind b) =>
        a == b
        || a == NumericComparison.FigureKind.Plain
        || b == NumericComparison.FigureKind.Plain;

    private static (int Fractional, decimal Magnitude) Weight(string asWritten)
    {
        var figure = NumericComparison.Extract(asWritten).FirstOrDefault();

        if (figure is null)
        {
            return (0, 0m);
        }

        var value = Math.Abs(figure.Value);

        return (value != decimal.Truncate(value) ? 1 : 0, value);
    }

    /// <summary>
    /// The section as it is printed, or null where nothing joined.
    /// </summary>
    public static string? Format(IReadOnlyList<JoinedAssertion> joins)
    {
        if (joins.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();

        sb.AppendLine(Heading);
        sb.AppendLine(
            "Each line is a claim made elsewhere in the report that shares an exact figure with a "
            + "passage you were given. The match is arithmetic, not judgement: it says the two "
            + "mention the same number, not that either is wrong.");
        sb.AppendLine(
            "Read each one against the passage named. Where the passage and the claim disagree "
            + "about that figure, that is a finding and it is yours to make — no other requirement "
            + "was given both halves. Where they agree, or the claim is not about your "
            + "requirement, ignore it and say nothing.");
        sb.AppendLine();

        foreach (var join in joins)
        {
            sb.AppendLine(
                $"- `{join.Path}` = {join.Value}"
                + (join.Source is { Length: > 0 } source ? $" ⟨{source}⟩" : string.Empty));
            sb.AppendLine($"  shares {join.Matched} with [{join.PassageId}]");
        }

        return sb.ToString();
    }
}
