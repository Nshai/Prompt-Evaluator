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
/// <item><b>L4</b> — every field with a fixed vocabulary must use a value from it. The
/// query-plan schema declares these as enums too, and the duplication is deliberate: nothing
/// in the build validates a plan against that schema, so this is the check that actually runs.
/// The failure it guards against is silent: a query written
/// <c>"priority": "Supporting"</c> is not Supplementary, so <c>IsCore</c> returns true and the
/// query runs even under CoreQueriesOnly; a category code outside A–I reaches the Qdrant
/// filter and matches nothing. Both were real — "Supporting" was found in CHK-007 and
/// CHK-008.</item>
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

        violations.AddRange(InspectVocabularies(plan));

        return violations;
    }

    /// <summary>The vocabularies the query-plan schema declares, mirrored here, keyed by field.</summary>
    private static readonly (string Field, string[] Allowed)[] Vocabularies =
    [
        ("side", ["Assertion", "Evidence", "Either"]),
        ("priority", ["Core", "Supplementary"]),
        ("onAbsent", ["Skip", "ReturnNA", "ContinueWithReducedScope", "Continue"]),
        ("limb", ["Consistency", "Appropriateness", "Both"]),
        ("comparison.method",
            ["ValueMatch", "SetCoverage", "PresenceOnly", "RangeMatch", "NarrativeAlignment"]),
    ];

    private static readonly string[] CategoryCodes =
        ["A", "B", "C", "D", "E", "F", "G", "H", "I"];

    /// <summary>
    /// L4. Every fixed-vocabulary field, and every category code, checked against its list.
    ///
    /// Empty is not a violation: an absent optional field takes its default. A value that is
    /// present and wrong is, because nothing downstream will refuse it — it will simply mean
    /// something other than what the plan author intended.
    /// </summary>
    private static IEnumerable<Violation> InspectVocabularies(CheckQueryPlan plan)
    {
        var probe = plan.TriggerProbe;

        if (probe is not null)
        {
            foreach (var v in Check(plan, string.Empty, "onAbsent", probe.OnAbsent))
            {
                yield return v;
            }

            foreach (var query in probe.Queries)
            {
                foreach (var v in CheckQuery(plan, "triggerProbe", query))
                {
                    yield return v;
                }
            }
        }

        foreach (var code in plan.PrimaryCategories)
        {
            foreach (var v in Check(plan, string.Empty, "primaryCategories", code, CategoryCodes))
            {
                yield return v;
            }
        }

        foreach (var group in plan.QueryGroups)
        {
            foreach (var v in Check(plan, group.GroupId, "limb", group.Verification?.Limb))
            {
                yield return v;
            }

            foreach (var v in Check(
                plan, group.GroupId, "comparison.method", group.Comparison?.Method))
            {
                yield return v;
            }

            // The categorisation steer is checked against the finding vocabulary rather than
            // against the document categories, and it fails loudly rather than quietly. An
            // unrecognised value is dropped at parse time — it cannot reach a finding, and a
            // reader filtering on it would see nothing and conclude the case is clean. A plan
            // that steers towards a category that does not exist is steering towards silence.
            foreach (var category in IssueCategory.Unknown(group.Verification?.IssueCategories))
            {
                yield return new Violation(
                    plan.CheckId, group.GroupId, "L4",
                    $"verification.issueCategories has \"{category}\", which is not one of "
                    + $"{string.Join(", ", IssueCategory.All)}. It would be dropped rather than "
                    + "steer anything.");
            }

            foreach (var code in group.DeclaredEvidenceCategories
                         .Concat(group.DeclaredAssertionCategories))
            {
                foreach (var v in Check(
                    plan, group.GroupId, "declares categories", code, CategoryCodes))
                {
                    yield return v;
                }
            }

            foreach (var query in group.Queries)
            {
                foreach (var v in CheckQuery(plan, group.GroupId, query))
                {
                    yield return v;
                }
            }
        }
    }

    private static IEnumerable<Violation> CheckQuery(
        CheckQueryPlan plan, string groupId, PlannedQuery query)
    {
        foreach (var v in Check(plan, groupId, "side", query.Side))
        {
            yield return v;
        }

        foreach (var v in Check(plan, groupId, "priority", query.Priority))
        {
            yield return v;
        }

        foreach (var code in query.TargetCategories)
        {
            foreach (var v in Check(plan, groupId, "targetCategories", code, CategoryCodes))
            {
                yield return v;
            }
        }
    }

    private static IEnumerable<Violation> Check(
        CheckQueryPlan plan, string groupId, string field, string? value, string[]? allowed = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        allowed ??= Vocabularies.First(v => v.Field == field).Allowed;

        if (allowed.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            yield break;
        }

        yield return new Violation(
            plan.CheckId, groupId, "L4",
            $"{field} is \"{value}\", which is not one of: {string.Join(", ", allowed)}. "
            + "Nothing downstream refuses it; it will simply mean something other than intended.");
    }

    private static string Describe(IReadOnlySet<string> categories) =>
        categories.Count == 0
            ? "none"
            : string.Join(", ", categories.OrderBy(c => c, StringComparer.Ordinal));
}
