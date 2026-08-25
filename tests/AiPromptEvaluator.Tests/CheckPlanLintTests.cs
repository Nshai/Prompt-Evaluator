using System.IO;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The plans have to ask for the evidence they say they need.
///
/// Six of the eight benchmark findings missed by the run scored in
/// docs/test-results/Runtime-Logs/latest sat behind a document category no query asked for.
/// The plans declared those categories in <c>expectedCategories.evidence</c> and then did not
/// search them, and nothing read the field — so the defect was invisible in the data for as
/// long as the plans existed.
/// </summary>
public class CheckPlanLintTests
{
    // ──────────────────────────────────────────────
    // The rules, on synthetic plans
    // ──────────────────────────────────────────────

    [Fact]
    public void AGroupThatQueriesWhatItDeclaresIsClean()
    {
        var plan = Plan(
            primary: ["B", "F"],
            Group("G1.1", declares: ["B", "F"], queries: ["B", "F", "I"]));

        Assert.Empty(CheckPlanLint.Inspect(plan));
    }

    /// <summary>
    /// G5.2 as it shipped. Its requirement is "Financial situation plus cashflow supports the
    /// recommendation" — the group that has to find the Fact Find's £-288 disposable income —
    /// and it declared category B while querying only F and I.
    /// </summary>
    [Fact]
    public void AGroupThatDeclaresACategoryItNeverQueriesIsCaught()
    {
        var plan = Plan(
            primary: ["B", "F"],
            Group("G5.2", declares: ["B", "F"], queries: ["F", "I"]));

        var violation = Assert.Single(CheckPlanLint.Inspect(plan));

        Assert.Equal("L1", violation.Rule);
        Assert.Equal("G5.2", violation.GroupId);
        Assert.Contains("declares evidence category B", violation.Detail);
    }

    /// <summary>
    /// CHK-007, CHK-008 and CHK-009 all named B as primary while no group declared it, so the
    /// group-level rule sees nothing wrong. The check's own scope and its groups' scope have
    /// to be reconciled separately.
    /// </summary>
    [Fact]
    public void APrimaryCategoryNoGroupDeclaresIsCaught()
    {
        var plan = Plan(
            primary: ["B", "G"],
            Group("G7.1", declares: ["G"], queries: ["G", "I"]));

        var violation = Assert.Single(CheckPlanLint.Inspect(plan));

        Assert.Equal("L2", violation.Rule);
        Assert.Contains("names B as a primary category", violation.Detail);
    }

    /// <summary>
    /// Internal-consistency groups compare the report against itself and have no evidence side.
    /// Demanding a document search from them would be demanding they retrieve something they
    /// have correctly decided does not exist.
    /// </summary>
    [Fact]
    public void AModelOnlyGroupIsNotAskedToRetrieveAnything()
    {
        var plan = Plan(
            primary: [],
            new PlanQueryGroup
            {
                GroupId = "G1.8",
                Declares = new PlanDeclares { EvidenceCategories = ["I"] },
                Retrieval = new PlanRetrieval
                {
                    Queries =
                    [
                        new PlannedQuery { Id = "Q1", Side = "Assertion", TargetCategories = ["I"] },
                    ],
                },
            });

        Assert.Empty(CheckPlanLint.Inspect(plan));
    }

    [Fact]
    public void APlanWithNoQueryGroupsIsCaught()
    {
        var violation = Assert.Single(CheckPlanLint.Inspect(Plan(primary: ["B"])));

        Assert.Equal("L0", violation.Rule);
        Assert.Contains("assessed nothing", violation.Detail);
    }

    // ──────────────────────────────────────────────
    // The rules, on the plans that actually ship
    // ──────────────────────────────────────────────

    /// <summary>
    /// The regression that matters. Every shipped plan must satisfy L1 and L2 — this is what
    /// stops a future plan edit silently reintroducing a category the check declares and never
    /// reads.
    /// </summary>
    [Fact]
    public void EveryShippedPlanSatisfiesTheLint()
    {
        var (plans, failures) = CheckQueryPlanLoader.Load(PlanFolder);

        Assert.Empty(failures);
        Assert.Equal(10, plans.Count);

        var violations = CheckPlanLint.Inspect(plans.Values);

        Assert.True(
            violations.Count == 0,
            "The shipped query plans declare evidence they never search:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations.Select(v => "  " + v)));
    }

    /// <summary>
    /// The plan set's shape, pinned. A dropped query group is otherwise silent: the check runs,
    /// reports success, and assesses one requirement fewer than the catalogue says it has.
    /// The stale build output that prompted this had CHK-001 at eight groups against nine.
    /// Re-pinned when the plans were rebuilt against "Revised checks.csv": 60 groups to 85.
    /// </summary>
    [Fact]
    public void TheShippedPlanSetHasTheGroupsTheCatalogueExpects()
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);

        var counts = plans.Values.ToDictionary(
            p => p.CheckId.Trim(),
            p => p.QueryGroups.Count,
            StringComparer.OrdinalIgnoreCase);

        Assert.Equal(11, counts["CHK-001"]);
        Assert.Equal(7, counts["CHK-002"]);
        Assert.Equal(10, counts["CHK-003"]);
        Assert.Equal(7, counts["CHK-004"]);
        Assert.Equal(8, counts["CHK-005"]);
        Assert.Equal(8, counts["CHK-006"]);
        Assert.Equal(11, counts["CHK-007"]);
        Assert.Equal(8, counts["CHK-008"]);
        Assert.Equal(10, counts["CHK-009"]);
        Assert.Equal(5, counts["CHK-010"]);

        Assert.Equal(85, counts.Values.Sum());
    }

    /// <summary>
    /// L3 is the class no rule can catch, and CHK-010 was the observed case: the vulnerability
    /// overlay never reached category D, where the client's recorded "No understanding /
    /// knowledge" answers live. Pinned by name because the fix is a judgement about where
    /// evidence lives, and a later edit could undo it without breaking anything else.
    /// </summary>
    [Fact]
    public void TheVulnerabilityOverlayReachesTheRiskProfileReport()
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);
        var overlay = plans["CHK-010"];

        Assert.Contains("D", overlay.PrimaryCategories);
        Assert.Contains(overlay.QueryGroups, g => g.QueriedCategories.Contains("D"));
    }

    /// <summary>
    /// The three checks that reached the Fact Find in zero groups out of nineteen.
    /// </summary>
    [Theory]
    [InlineData("CHK-005")]
    [InlineData("CHK-007")]
    [InlineData("CHK-008")]
    [InlineData("CHK-009")]
    public void TheChecksThatNeedTheFactFindNowQueryIt(string checkId)
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);

        Assert.Contains(plans[checkId].QueryGroups, g => g.QueriedCategories.Contains("B"));
    }

    // ──────────────────────────────────────────────

    private static string PlanFolder =>
        Path.Combine(AppContext.BaseDirectory, "check-plan");

    private static CheckQueryPlan Plan(string[] primary, params PlanQueryGroup[] groups) =>
        new()
        {
            CheckId = "CHK-999",
            CheckName = "Test",
            PrimaryCategories = [.. primary],
            QueryGroups = [.. groups],
        };

    private static PlanQueryGroup Group(string id, string[] declares, string[] queries) =>
        new()
        {
            GroupId = id,
            Declares = new PlanDeclares { EvidenceCategories = [.. declares] },
            Retrieval = new PlanRetrieval
            {
                Queries =
                [
                    new PlannedQuery { Id = "Q1", Side = "Evidence", TargetCategories = [.. queries] },
                ],
            },
        };
}
