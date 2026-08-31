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
/// <item><b>L5</b> — a group may not declare the suitability report as its only evidence. The
/// report cannot corroborate itself: a requirement whose evidence side is the document under
/// audit has no evidence side, and the outcome does not show it. Run 17 has a group writing "the
/// inflation assumptions … are corroborated by [P11]" where [P11] is the report. Two groups do
/// read only their own document on purpose — mismatches "within the report itself" and whether
/// disadvantages are "prominently presented" — and the exemption is keyed on that wording, so a
/// group claiming it has to say so where a reader of the plan will see it.</item>
/// <item><b>L6</b> — nothing in a plan may belong to one case only. The plans were written against
/// one file and drifted toward it: a provider name in an <c>expectSignals</c> list, a risk-profiler
/// vendor, one client's sentence as a section hint. Every one is tuned to that case and mistuned to
/// the next, and in <c>expectSignals</c> it is actively wrong — a signal that does not appear makes
/// the runner report the data point as "absent from the case file", so a value from one case
/// manufactures a false absence on every other.</item>
/// <item><b>L7</b> — an applicability rule enumerating a closed vocabulary is asserting that
/// every value it omits does <i>not</i> trigger the check, and until now nothing said which
/// values those were. The failure is silent and total: the rule is ANDed into the trigger, a
/// check whose vocabulary member was left out never runs, and the output shows a check that
/// did not run beside checks that ran and passed. Observed on a rule listing five members of
/// an advice-action vocabulary the schema documents ten values for; the omitted sixth was the
/// value two separate extractions recorded, and six material findings were lost to it without
/// a search being made. <see cref="CheckPlanRunner"/> now runs such a check with a warning
/// rather than skipping it — this is the half that makes the omission visible before the run
/// rather than after. Reported at every load, and <b>not an error</b>: an exclusion is
/// frequently correct, and the point is that it should be a decision somebody made.</item>
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
    /// <param name="plans">The loaded plans.</param>
    /// <param name="vocabularies">
    /// The canonical schema's documented vocabularies, from
    /// <see cref="CanonicalVocabulary.Parse"/>. Optional: without it L7 is skipped, because a
    /// rule can only be compared against a vocabulary somebody has read.
    /// </param>
    public static IReadOnlyList<Violation> Inspect(
        IEnumerable<CheckQueryPlan> plans,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? vocabularies = null) =>
        plans
            .OrderBy(p => p.CheckId, StringComparer.OrdinalIgnoreCase)
            .SelectMany(p => Inspect(p, vocabularies))
            .ToList();

    /// <summary>Every violation in one plan.</summary>
    public static IReadOnlyList<Violation> Inspect(
        CheckQueryPlan plan,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? vocabularies = null)
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

            // L5. The report is not evidence about itself. A group whose evidence side is the
            // report and nothing else has no evidence side: whatever it concludes, it concluded
            // from the document under audit, and nothing in the outcome shows that. Two groups do
            // this deliberately — CHK-001's mismatches "within the report itself" and CHK-008's
            // prominence, both of which are questions about the report's own internal consistency
            // — so the rule is not that [I] may never be declared as evidence, but that a group
            // declaring it alone must say in its requirement that this is what it is doing.
            if (group.DeclaredEvidenceCategories.Count > 0
                && group.DeclaredEvidenceCategories.All(
                    c => c.Equals(ReportCategory, StringComparison.OrdinalIgnoreCase))
                && !ReadsItsOwnDocumentOnPurpose(group.Requirement))
            {
                violations.Add(new Violation(
                    plan.CheckId, group.GroupId, "L5",
                    $"declares category {ReportCategory} as its only evidence, so the suitability "
                    + "report is the sole support for a claim the report makes. Either name the "
                    + "supporting documents this requirement should be checked against, or say in "
                    + "the requirement that it is about the report's own internal consistency."));
            }

            // L8. A comparison needs two sides, and the plan has to supply both.
            //
            // The declared method says what the group will do with what it is given, and three of
            // the five methods are binary: ValueMatch, RangeMatch and SetCoverage each compare a
            // stated figure or set against something the file evidences. A group declaring one of
            // them with no assertion paths, or with no evidence-side query, has declared a
            // comparison it cannot make — and the outcome does not show it. It reports on whatever
            // half it did receive, in the confident register of a comparison that closed.
            //
            // This is the cheap, decidable half of the collidability problem. The expensive half —
            // whether a group's paths and its evidence categories are two sides of *one* question,
            // rather than two sides of two — is not decidable from the plan alone: G9.10 selects
            // assertion paths about exit penalties while declaring evidence category B, where
            // certified court orders live, and nothing in either declaration says they do not
            // belong together. That needs the assertion digest to route around rather than a rule
            // to forbid.
            var method = group.Comparison?.Method ?? string.Empty;

            if (TwoSidedMethods.Contains(method))
            {
                if (group.AllCanonicalPaths.Count == 0)
                {
                    violations.Add(new Violation(
                        plan.CheckId, group.GroupId, "L8",
                        $"declares comparison method {method}, which compares the report's own "
                        + "figure against the file, but selects no canonical paths. There is no "
                        + "assertion side to compare, and the group will report on the evidence "
                        + "alone without saying so. Name the paths the claim lives at, or state a "
                        + "method that does not need one."));
                }

                if (!group.IsModelOnly && group.QueriedCategories.Count == 0)
                {
                    violations.Add(new Violation(
                        plan.CheckId, group.GroupId, "L8",
                        $"declares comparison method {method} but no query targets a document "
                        + "category, so nothing corroborates the assertion. Either target the "
                        + "categories the evidence lives in, or make the group model-only and say "
                        + "in the requirement that it compares the report against itself."));
                }
            }

            // L6. Nothing in a plan may belong to one case only. See OneCaseOnly.
            foreach (var value in group.DeclaredEvidenceSections
                         .Concat(group.Queries.SelectMany(q => q.ExpectSignals ?? []))
                         .Concat(group.Queries.Select(q => q.Text ?? string.Empty)))
            {
                foreach (var named in OneCaseOnly
                             .Where(n => value.Contains(n, StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add(new Violation(
                        plan.CheckId, group.GroupId, "L6",
                        $"names \"{named}\", which belongs to one case rather than to the domain, in "
                        + $"\"{value}\". The next case has different providers, a different client and "
                        + "different sentences. Name a heading, a field label or a tool's own wording "
                        + "instead."));
                }
            }
        }

        // Assertion categories count here as well as evidence ones. A check names the
        // suitability report's own category (I) among its primaries because that is where the
        // assertions come from, and almost no group declares it as *evidence* — correctly, since
        // the report is the thing being tested rather than something that corroborates it. The two
        // that do are the internal-consistency requirements L5 exempts by name.
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
        violations.AddRange(InspectApplicabilityCoverage(plan, vocabularies));

        return violations;
    }

    /// <summary>
    /// L7. How each applicability rule lines up against the vocabulary it is enumerating, in
    /// both directions.
    ///
    /// The rule is read as an assertion — "these values trigger the check, the rest do not" — and
    /// that assertion is reported so it can be agreed with or corrected. Nothing is inferred
    /// about whether an exclusion is right; most are.
    ///
    /// <list type="bullet">
    /// <item><b>excludes</b> — documented values the rule does not accept. Each is a case the
    /// check will decline to run on, decided before any search.</item>
    /// <item><b>accepts undocumented</b> — values the rule accepts that the schema does not
    /// document. These are usually defensive, added against an extraction that was drifting off
    /// the vocabulary, and they are worth seeing for the same reason: once the drift is fixed
    /// upstream they match nothing and quietly stop being the safety net somebody intended.</item>
    /// </list>
    ///
    /// Only rules naming a canonical path that resolves to a documented vocabulary are examined.
    /// A boolean rule (<c>"replacementOrSwitch": ["true"]</c>) has a two-value vocabulary nothing
    /// documents as prose and would report "excludes false" on every plan, which is noise; it is
    /// skipped by requiring more than two documented members.
    /// </summary>
    /// <remarks>
    /// Every message opens with a clause of the form
    /// <c>rule/property excludes A, B</c> and then a dash and the advice, so a caller approving
    /// these can key on the assertion without re-parsing the prose around it.
    /// </remarks>
    private static IEnumerable<Violation> InspectApplicabilityCoverage(
        CheckQueryPlan plan,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? vocabularies)
    {
        if (vocabularies is null || plan.TriggerProbe is not { } probe)
        {
            yield break;
        }

        foreach (var rule in probe.Applicability)
        {
            var accepted = rule.AcceptedValues;

            if (accepted.Count == 0)
            {
                continue;
            }

            foreach (var property in rule.CanonicalPaths
                         .Select(LeafProperty)
                         .Where(p => p.Length > 0)
                         .Distinct(StringComparer.Ordinal))
            {
                if (!vocabularies.TryGetValue(property, out var documented) || documented.Count <= 2)
                {
                    continue;
                }

                var excluded = documented
                    .Where(d => !accepted.Contains(d, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (excluded.Count > 0)
                {
                    yield return new Violation(
                        plan.CheckId, string.Empty, "L7",
                        $"{rule.Name}/{property} excludes {string.Join(", ", excluded)}"
                        + $" — the rule accepts {accepted.Count} of the {documented.Count} values "
                        + "the schema documents, so it asserts that the rest do not trigger this "
                        + "check. Confirm that is intended: a value omitted by accident settles "
                        + "the check before any search runs.");
                }

                var undocumented = accepted
                    .Where(a => !documented.Contains(a, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (undocumented.Count > 0)
                {
                    yield return new Violation(
                        plan.CheckId, string.Empty, "L7",
                        $"{rule.Name}/{property} accepts undocumented "
                        + $"{string.Join(", ", undocumented)}"
                        + " — the schema's vocabulary for this property does not include "
                        + $"{(undocumented.Count == 1 ? "it" : "them")}. Either the vocabulary "
                        + "should, or these entries are covering for an extraction that writes "
                        + "off-vocabulary values and will match nothing once it stops.");
                }
            }
        }
    }

    /// <summary>
    /// The property name a canonical path ends in — <c>/existingArrangements[]/adviceAction</c>
    /// is <c>adviceAction</c>. Array markers are the path's own syntax and carry no name.
    /// </summary>
    internal static string LeafProperty(string canonicalPath) =>
        canonicalPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => segment.Replace("[]", string.Empty, StringComparison.Ordinal))
            .LastOrDefault(segment => segment.Length > 0)
        ?? string.Empty;

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

    /// <summary>
    /// Comparison methods that put a stated value against an evidenced one, so a group declaring
    /// them needs both sides. PresenceOnly and NarrativeAlignment do not: the first asks whether
    /// something was said at all, the second whether two pieces of prose agree in substance.
    /// </summary>
    private static readonly HashSet<string> TwoSidedMethods =
        new(["ValueMatch", "RangeMatch", "SetCoverage"], StringComparer.OrdinalIgnoreCase);

    private static readonly string[] CategoryCodes =
        ["A", "B", "C", "D", "E", "F", "G", "H", "I"];

    /// <summary>The suitability report's own category — the document every check is testing.</summary>
    private const string ReportCategory = "I";

    /// <summary>
    /// Things that belong to one case and not to the domain: providers, risk-profiler vendors, and
    /// verbatim sentences from the file the plans were written against.
    ///
    /// <b>The pipeline validates whichever suitability report it is given against whichever evidence
    /// came with it, and both change per case.</b> A plan naming one case's provider or quoting one
    /// client's sentence is tuned to that file and mistuned to the next — and in
    /// <c>expectSignals</c> it is worse than useless, because a signal that does not appear makes the
    /// runner tell the assessor the data point is "absent from the case file rather than merely
    /// unretrieved". A value from case 1 therefore manufactures a false absence on case 2.
    ///
    /// Deliberately not listed: statutory amounts (the £60,000 annual allowance, the £10,000 MPAA),
    /// domain vocabulary ("main residence", "a fact find"), a tool's own column headings ("ranked by
    /// Maturity Value"), and a profiler's fixed option labels ("No understanding / knowledge"). Those
    /// recur across cases, which is the whole test.
    /// </summary>
    private static readonly string[] OneCaseOnly =
    [
        "Standard Life", "Scottish Widows", "Zurich", "Aviva", "Peoples Pension", "People's Pension",
        "Defaqto", "Dynamic Planner", "Sullivan", "Sea Cadets", "CDH Recruitment",
        "happy to proceed with a Risk rating", "I have no understanding of investments",
    ];

    /// <summary>
    /// Whether a requirement is asking about the report's own internal consistency, which is the
    /// one case where the report is legitimately the whole of the evidence side.
    ///
    /// Matched on the requirement text rather than a flag in the plan, deliberately: the exemption
    /// then has to be stated where a reader of the plan will see it, and a group that quietly
    /// drops its supporting documents cannot keep the exemption by leaving a boolean set.
    /// </summary>
    private static bool ReadsItsOwnDocumentOnPurpose(string? requirement) =>
        requirement is not null
        && (requirement.Contains("within the report itself", StringComparison.OrdinalIgnoreCase)
            || requirement.Contains("prominently presented", StringComparison.OrdinalIgnoreCase));

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
