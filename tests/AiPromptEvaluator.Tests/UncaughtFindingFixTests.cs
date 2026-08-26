using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The plan changes aimed at the eight benchmark findings no model caught.
///
/// <b>The union of three assessor models on 2026-08-26 was 28 of 36 — exactly what the best model
/// caught alone.</b> Haiku found nothing Sonnet missed and Nova found nothing either of them
/// missed, across a capability range from 19% to 78% recall. So the remaining eight are not a
/// model problem, and no better model, larger pack or longer answer reaches them. They divide into
/// three causes, and this pins the fixes for the first two.
///
/// Each assertion here is a plain string in a JSON file that a later edit could reword into
/// something that still reads correctly and no longer does the job — which is precisely what
/// happened to the guard in <see cref="TheRetainedPlanGuardNoLongerRequiresAWholePortfolioClaim"/>.
/// </summary>
public class UncaughtFindingFixTests
{
    private static string PlanFolder => Path.Combine(AppContext.BaseDirectory, "check-plan");

    private static PlanQueryGroup Group(string checkId, string groupId)
    {
        var (plans, failures) = CheckQueryPlanLoader.Load(PlanFolder);
        Assert.Empty(failures);
        return plans[checkId].QueryGroups.Single(g => g.GroupId == groupId);
    }

    private static string Guards(PlanQueryGroup group) =>
        string.Join(" ", group.Comparison?.FalsePositiveGuards ?? []);

    // ── F3.4 — the guard that discarded evidence already in the pack ──────────

    /// <summary>
    /// The People's Pension is retained while invested at risk 9 against an agreed rating of 5,
    /// and the report says that fund "aligns with your risk appetite". The rating reached the
    /// assessor in all three runs and was discarded every time, because the guard excused any
    /// retained plan unless the report claimed alignment for the <i>whole portfolio</i> — which
    /// it does not, and need not, to be wrong.
    /// </summary>
    [Fact]
    public void TheRetainedPlanGuardNoLongerRequiresAWholePortfolioClaim()
    {
        var guards = Guards(Group("CHK-003", "G3.7"));

        Assert.DoesNotContain("only a concern where the report claims alignment for the whole portfolio", guards);
        Assert.Contains("or for that plan alone", guards);
    }

    /// <summary>
    /// The second half of the same suppression. Incomparable scales are a real reason not to
    /// declare a breach, and not a reason to leave the report's own assertion unassessed — where
    /// the report claims a specific fund aligns, incomparability is why the claim is unsupported.
    /// </summary>
    [Fact]
    public void IncomparableScalesNoLongerExcuseTheReportsOwnClaim()
    {
        var guards = Guards(Group("CHK-003", "G3.7"));

        Assert.Contains("does not excuse an assertion the report makes for itself", guards);
        Assert.Contains("testable on its own terms", guards);
    }

    // ── F1.9 — the question a value comparison cannot ask ─────────────────────

    /// <summary>
    /// Zurich, Standard Life and Scottish Widows are tabulated in the report and absent from the
    /// fact find's pension schedule. G1.7 is a ValueMatch group whose every guard concerned a
    /// difference in value, and "this plan is not in the other document" is not a value
    /// difference — so nothing asked, and all three models were silent.
    ///
    /// The evidence was never the problem: G1.7's three section hints all matched in the observed
    /// runs, so the fact find's schedule was in the pack the whole time.
    /// </summary>
    [Fact]
    public void TheExistingProductsGroupNowTestsMembershipAndNotOnlyValue()
    {
        var group = Group("CHK-001", "G1.7");

        Assert.Contains("Membership is part of consistency", Guards(group));
        Assert.Contains("present in one document and absent from the other", Guards(group));

        // And it asks the fact find for its own schedule, rather than only the provider files.
        Assert.Contains(
            group.Queries,
            q => q.Id == "Q1.7.8" && q.TargetCategories.Contains("B"));
    }

    // ── F1.6 / F5.4 / F5.3 / F4.2 — the passage that never arrived ────────────

    /// <summary>
    /// "Tenant" appears nowhere in any of the three runs' output. The report's half reached the
    /// assessor — the "incorporates the assets of your main residence" hint matched — and the fact
    /// find's half never did, so all three models correctly declined to make half a contradiction.
    ///
    /// <b>The query text is written to resemble the chunk, not the question.</b> The row lives in
    /// an address table, and "main residence home value owned rented" has never retrieved it.
    /// </summary>
    [Theory]
    [InlineData("CHK-001", "G1.5", "Q1.5.5")]
    [InlineData("CHK-005", "G5.2", "Q5.2.7")]
    public void TheFactFindsResidencyRowIsAskedForInTheWordingOfItsOwnTable(
        string checkId, string groupId, string queryId)
    {
        var q = Group(checkId, groupId).Queries.Single(x => x.Id == queryId);

        Assert.Contains("residency status", q.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("postcode", q.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("B", q.TargetCategories);
        Assert.Contains("Tenant", q.ExpectSignals);
    }

    /// <summary>
    /// The £6,000 emergency fund is marked LIQUIDATED 2ND in the cashflow scenario. G5.5's hint
    /// matched nothing and its query described the question rather than the page; the page is a
    /// scenario asset tree of headings, amounts and two-column tables.
    /// </summary>
    [Fact]
    public void TheLiquidationOrderIsAskedForInTheWordingOfTheScenarioTree()
    {
        var q = Group("CHK-005", "G5.5").Queries.Single(x => x.Id == "Q5.5.4");

        Assert.Contains("surplus account", q.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cash account", q.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("F", q.TargetCategories);
        Assert.Contains("LIQUIDATED", q.ExpectSignals);
    }

    /// <summary>
    /// Every run reached "No understanding / knowledge" from the investor-experience questionnaire
    /// and none reached "I have no understanding of investments or how they work" from the ATR
    /// one, so F4.2 scored Partial in all three under R4 — the right conclusion by the wrong
    /// route. The ATR row sits in a long question-and-answer table that also carries F3.5's
    /// cautious answers.
    /// </summary>
    [Theory]
    [InlineData("CHK-004", "G4.1", "Q4.1.4")]
    [InlineData("CHK-003", "G3.10", "Q3.10.4")]
    public void TheAttitudeToRiskQuestionTableIsAskedForByItsOwnQuestions(
        string checkId, string groupId, string queryId)
    {
        var q = Group(checkId, groupId).Queries.Single(x => x.Id == queryId);

        Assert.Contains("compared to other people", q.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("D", q.TargetCategories);
    }

    [Fact]
    public void TheAtrQueryInCheckFourExpectsTheSentenceThatCarriesTheFinding()
    {
        var q = Group("CHK-004", "G4.1").Queries.Single(x => x.Id == "Q4.1.4");

        Assert.Contains("I have no understanding of investments", q.ExpectSignals);
    }

    // ── the shape must survive ────────────────────────────────────────────────

    /// <summary>
    /// Everything above was added inside an existing group. The catalogue's group count is pinned
    /// elsewhere; this says the same thing from the other direction, because a fix that quietly
    /// added a group would change what every check reports.
    ///
    /// The number moved from 85 to 87 once, and not for a fix: reconciling the published fact
    /// library against the plans found two facts — the target retirement income and the ancillary
    /// estate-planning recommendations — that no requirement read at all, and each needed a
    /// requirement of its own rather than a path bolted onto a neighbour.
    /// </summary>
    [Fact]
    public void NoFixAddedAGroup()
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);

        Assert.Equal(87, plans.Values.Sum(p => p.QueryGroups.Count));
    }
}
