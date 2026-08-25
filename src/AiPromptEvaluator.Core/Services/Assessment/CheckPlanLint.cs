namespace AiPromptEvaluator;

/// <summary>
/// Holds the query plans to their own declarations, before a run spends anything.
///
/// This exists because of a measured failure. Scoring a run against the hand-adjudicated
/// benchmark, six of the eight missed findings turned out to sit behind a document category
/// that no query in the responsible check ever asked for. The retrieval was working; it was
/// being asked the wrong questions. Raising the search limit from 8 to 16 bought two extra
/// passages, because re-ranking can only reorder candidates — it cannot promote a passage the
/// store was never asked for.
///
/// The plans already knew. Every group carries an <c>expectedCategories.evidence</c> list
/// saying where its answer lives, and seventeen of sixty groups declared a category none of
/// their own queries targeted. Nothing read that field, so the contradiction sat in the data
/// for as long as the plans have existed.
///
/// Three rules, in increasing order of how hard they are to catch:
///
/// <list type="bullet">
/// <item><b>L1</b> — a group must query every evidence category it declares. Seventeen
/// violations when this was written.</item>
/// <item><b>L2</b> — a check's <c>primaryCategories</c> must be declared by at least one of its
/// groups. Category B was named as primary by CHK-007, CHK-008 and CHK-009 and declared by no
/// group in any of them, so L1 alone sees nothing wrong.</item>
/// <item><b>L3</b> — a plan that omits the category the evidence actually lives in is
/// internally consistent and simply wrong. <b>No rule here can catch that</b>; it needs review
/// against the check catalogue. CHK-010 was the observed case: the vulnerability overlay never
/// queried the risk profile report, where the client's recorded lack of investment knowledge —
/// a textbook FG21/1 capability driver — is written down four times.</item>
/// </list>
/// </summary>
public static class CheckPlanLint
{
    /// <summary>One breach of one rule, phrased so the fix is obvious from the message.</summary>
    public sealed record Violation(string CheckId, string GroupId, string Rule, string Detail)
    {
        public override string ToString() =>
            string.IsNullOrEmpty(GroupId)
                ? $"[{Rule}] {CheckId}: {Detail}"
                : $"[{Rule}] {CheckId}/{GroupId}: {Detail}";
    }

    /// <summary>Every violation across a set of plans, in check then group order.</summary>
    public static IReadOnlyList<Violation> Inspect(IEnumerable<CheckQueryPlan> plans) =>
        plans
            .OrderBy(p => p.CheckId, StringComparer.OrdinalIgnoreCase)
            .SelectMany(Inspect)
            .ToList();

    /// <summary>Every violation in one plan.</summary>
    public static IReadOnlyList<Violation> Inspect(CheckQueryPlan plan)
    {
        var violations = new List<Violation>();

        // A plan that survives parsing but carries no work is worse than a missing one: a
        // missing plan is reported as a skipped check, this one reports success having
        // assessed nothing.
        if (plan.QueryGroups.Count == 0)
        {
            violations.Add(new Violation(
                plan.CheckId, string.Empty, "L0",
                "The plan has no query groups, so the check would report success having assessed nothing."));

            return violations;
        }

        foreach (var group in plan.QueryGroups)
        {
            // A model-only group settles from the canonical model and has no evidence side to
            // search. Requiring it to query a document category would be requiring it to
            // retrieve something it has correctly decided does not exist.
            if (group.IsModelOnly)
            {
                continue;
            }

            var unqueried = group.DeclaredEvidenceCategories
                .Except(group.QueriedCategories, StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToList();

            if (unqueried.Count > 0)
            {
                violations.Add(new Violation(
                    plan.CheckId, group.GroupId, "L1",
                    $"declares evidence category {string.Join(", ", unqueried)} but no query targets "
                    + $"{(unqueried.Count == 1 ? "it" : "them")}. "
                    + $"Queried: {Describe(group.QueriedCategories)}."));
            }
        }

        // Assertion categories count here as well as evidence ones. A check names the
        // suitability report's own category (I) among its primaries because that is where the
        // assertions come from, and no group declares it as *evidence* — correctly, since the
        // report is the thing being tested rather than something that corroborates it.
        // Comparing against the evidence side alone reports every check as broken.
        var declaredAnywhere = plan.QueryGroups
            .SelectMany(g => g.DeclaredEvidenceCategories
                .Concat(g.DeclaredAssertionCategories))
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unusedPrimaries = plan.PrimaryCategories
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Where(c => !declaredAnywhere.Contains(c))
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        if (unusedPrimaries.Count > 0)
        {
            violations.Add(new Violation(
                plan.CheckId, string.Empty, "L2",
                $"names {string.Join(", ", unusedPrimaries)} as a primary category, but no group "
                + "declares it as evidence. Either a group should be reaching it, or the check's "
                + "primaryCategories is wrong."));
        }

        return violations;
    }

    private static string Describe(IReadOnlySet<string> categories) =>
        categories.Count == 0
            ? "none"
            : string.Join(", ", categories.OrderBy(c => c, StringComparer.Ordinal));
}
