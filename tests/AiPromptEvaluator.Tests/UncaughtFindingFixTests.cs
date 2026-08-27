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

    // ── F3.1 / F2.2 — the passage that reached eight groups and not the one ───

    /// <summary>
    /// F3.1 was caught in every genuine run from Run 7 to Run 16 and lost in Run 17. The file
    /// note's ATR paragraph ends "You are happy to proceed with a Risk rating of 6" where the
    /// report reproduces the same paragraph with 5.
    ///
    /// <b>It was not a retrieval failure.</b> The passage reached eight group prompts in Run 17 —
    /// G2.1, G2.7, G4.5, G4.7, G6.7, G9.9, G10.3, G10.5 — and not one CHK-003 group, while G3.6's
    /// pack grew from 123 passages to 164. Routing, not retrieval: the sentence was read by eight
    /// groups with no requirement to check it and withheld from the one that has.
    ///
    /// CHK-003 declared no section hints at all, which is also why both hint diagnostics printed
    /// nothing while the finding vanished — each iterates the hints a group declared.
    ///
    /// The hint is the block's heading, not the sentence inside it. A hint quoting one client's
    /// wording works on that client and nothing else, and this pipeline validates whichever report
    /// it is given — so it names where the wording lives and lets retrieval find it there.
    ///
    /// Apostrophes are avoided for a separate reason: the match is a plain substring test with no
    /// unicode folding, and the source writes "didn’t" with a curly apostrophe.
    /// </summary>
    [Theory]
    [InlineData("G3.6")]
    [InlineData("G3.8")]
    public void TheRiskGroupsReserveASlotForTheFileNotesAtrWording(string groupId)
    {
        var sections = Group("CHK-003", groupId).Retrieval?.EvidenceSections ?? [];

        Assert.Contains("ATR Wording", sections);
        Assert.DoesNotContain(sections, s => s.Contains('\''));
        Assert.DoesNotContain(sections, s => s.Contains('’'));

        // No hint may quote a sentence from one case's documents.
        Assert.DoesNotContain(sections, s => s.Contains("happy to proceed", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// F2.2 has never been caught in seventeen runs. Its evidence is the capacity-for-loss
    /// questionnaire's investment horizon of "Less than 3 years" and the fact find's Time Horizon
    /// of "Short Term" — <b>neither string appears anywhere in Run 17's output.</b> Every run
    /// reaches the two-year conflict from the risk profile report's projection basis instead, which
    /// scores Partial under R4: the right conclusion by the wrong route.
    ///
    /// The questionnaires are evidence documents, so retrieval is the correct channel for them and
    /// not a workaround — the canonical model reads category I and nothing else, by design.
    /// </summary>
    [Fact]
    public void TheCapacityForLossGroupReservesASlotForTheQuestionnaire()
    {
        var sections = Group("CHK-003", "G3.2").Retrieval?.EvidenceSections ?? [];

        Assert.Contains("Capacity For Loss questionnaire responses", sections);
    }

    /// <summary>
    /// The horizon half of the same questionnaire, plus the fact find's own row, on the group whose
    /// requirement is the time horizon used to support the risk profile.
    ///
    /// The fact find hint is the field's <i>label</i> and not this client's answer. "Short Term" is
    /// what one case recorded; "Time Horizon" is the row it recorded it in, and is there whatever the
    /// answer turns out to be.
    /// </summary>
    [Fact]
    public void TheTimeHorizonGroupReservesASlotForBothRecordedHorizons()
    {
        var sections = Group("CHK-003", "G3.4").Retrieval?.EvidenceSections ?? [];

        Assert.Contains("my investment horizon", sections);
        Assert.Contains("Time Horizon", sections);
        Assert.DoesNotContain("Short Term", sections);
    }

    // ── the report is not evidence about itself ────────────────────────────────

    /// <summary>
    /// 27% of Run 17's cited passages — 151 of 558 — were the suitability report, and one group
    /// wrote "the inflation assumptions … are corroborated by [P11] and [P12]" where both are the
    /// report. Run 15 did the same on income and expenditure; Run 16 got the same requirement right
    /// on the same passage, so whether it fires is a coin toss, which is worse to leave than a
    /// consistent fault: an intermittent false corroboration is harder to catch in review.
    ///
    /// The prompt already separated the canonical model from the retrieved passages and called the
    /// passages "the rest of the case file". That was not enough, because a passage from category I
    /// <i>is</i> the report and arrives looking like any other. The case is now named.
    /// </summary>
    [Fact]
    public void TheAssessorIsToldTheReportCannotCorroborateItself()
    {
        Assert.Contains("A passage in category [I] is the suitability report itself", Prompts.AssessorSystem);
        Assert.Contains("The report cannot corroborate itself", Prompts.AssessorSystem);
        Assert.Contains("the evidence is absent", Prompts.AssessorSystem);
    }

    // ── F3.4 — the plan the advice keeps, which nothing asked about ───────────

    /// <summary>
    /// The People's Pension is retained while invested in a fund rated 9 against an agreed rating of
    /// 5, and the report says that fund "aligns with your risk appetite" while stopping at "you
    /// should consider" switching it.
    ///
    /// <b>Six runs missed it, four of them after a guard was written for exactly this.</b> Run 17
    /// showed why: no requirement asked. The word "retained" appeared once in the whole of CHK-003,
    /// inside a G3.7 guard, and G3.7's requirement is the <i>recommended</i> strategy. With nothing
    /// asking, G4.6 picked the rating up incidentally and read it as a fund-naming problem, then
    /// used the holding as evidence the client had self-directed equity investments — the opposite
    /// of a risk mismatch, and a false negative rather than a miss.
    ///
    /// A guard can stop a group reaching the wrong conclusion. It cannot make a group ask.
    /// </summary>
    [Fact]
    public void ARetainedPlansFundsAreAskedAboutByARequirementOfTheirOwn()
    {
        var group = Group("CHK-003", "G3.11");

        Assert.Contains("retained", group.Requirement, StringComparison.OrdinalIgnoreCase);

        var paths = group.Retrieval?.CanonicalPaths ?? [];

        Assert.Contains("/existingArrangements[]/adviceAction", paths);
        Assert.Contains("/existingArrangements[]/riskRating", paths);
        Assert.Contains("/riskAssessment/perClient[]/attitudeToRisk/agreedCode", paths);
    }

    /// <summary>
    /// The two ways the finding was given away before, closed as guards on the group that now owns
    /// it: a rationale for keeping the plan is not an answer about the fund inside it, and a
    /// suggestion the report never turns into a recommendation leaves the client where they were.
    /// </summary>
    [Fact]
    public void TheRetentionGroupRefusesBothWaysOutTakenBefore()
    {
        var guards = Guards(Group("CHK-003", "G3.11"));

        Assert.Contains("does not answer for the fund inside it", guards);
        Assert.Contains("is not a fix for the mismatch", guards);
        Assert.Contains("does not excuse an assertion the report makes for itself", guards);
    }

    // ── F7.1 — the two charge tables, and the reading that was reconciled ─────

    /// <summary>
    /// The report states each existing plan's charge twice: an arrangements table on page 5 and a
    /// switch-charges table on page 11. They disagree — Zurich 0.18% against 0.93%, Standard Life
    /// 0.52% against 0.18% — and the disagreement <i>is</i> F7.1.
    ///
    /// <b>Both tables were already extracted, and one cell was quietly made to agree.</b> Run 17's
    /// model holds the page-5 reading in <c>costsAndCharges.existing.lines</c> and the page-11 one
    /// in <c>comparison.perArrangement</c>, correctly disagreeing for Zurich. Standard Life's
    /// page-11 cell, printed 0.18%, came out as 0.52% — the page-5 figure, carried across. A
    /// reconciled pair looks like clean data and has destroyed the finding, which is worse than a
    /// missing row because nothing marks it.
    ///
    /// So the array needed the instruction, not a new shape. Its description is the only place the
    /// extractor is told these two tables are meant to disagree.
    /// </summary>
    [Fact]
    public void ThePerArrangementChargeTableIsDocumentedAsASecondReadingAndNotAReconciliation()
    {
        var schema = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "canonical-suitability-model.schema.json"));

        using var document = System.Text.Json.JsonDocument.Parse(schema);

        var description = document.RootElement
            .GetProperty("$defs").GetProperty("CostsAndCharges")
            .GetProperty("properties").GetProperty("comparison")
            .GetProperty("properties").GetProperty("perArrangement")
            .GetProperty("description").GetString()!;

        Assert.Contains("Do not reconcile", description);
        Assert.Contains("do not carry a figure across", description);
        Assert.Contains("The disagreement is what a check is looking for", description);
    }

    /// <summary>
    /// The general rule, in the shared extractor prompt rather than one property's description,
    /// because the same substitution is available anywhere a report tabulates a quantity twice.
    /// "Record contradictions rather than resolving them" was already there and was not enough:
    /// it reads as a rule about prose, and the failure was in a table cell.
    /// </summary>
    [Fact]
    public void TheExtractorIsToldNotToCarryAFigureBetweenTwoTablesOfTheSameQuantity()
    {
        Assert.Contains("tabulated twice", Prompts.ExtractorSystem);
        Assert.Contains("Read every cell from the table in front of you", Prompts.ExtractorSystem);
        Assert.Contains("Two readings quietly made to agree look", Prompts.ExtractorSystem);

        // The rule has to be about the shape and not about the case it was found in. A prompt
        // naming a provider teaches the extractor to look for that provider, and every case has
        // different ones — the pipeline is generic and the prompt has to stay that way.
        Assert.DoesNotContain("Standard Life", Prompts.ExtractorSystem);
        Assert.DoesNotContain("0.18%", Prompts.ExtractorSystem);
    }

    /// <summary>
    /// G7.4 held the page-11 table and not the page-5 one, so the group that owns the cost
    /// comparison could not see the pair it is supposed to compare. Both readings now reach it.
    /// </summary>
    [Fact]
    public void TheCostComparisonGroupReadsBothChargeTables()
    {
        var paths = Group("CHK-007", "G7.4").Retrieval?.CanonicalPaths ?? [];

        Assert.Contains("/costsAndCharges/existing", paths);
        Assert.Contains("/costsAndCharges/comparison/perArrangement", paths);
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

    /// <summary>
    /// The signal was <c>"I have no understanding of investments"</c> — one client's sentence,
    /// verbatim. <c>expectSignals</c> drives a claim to the assessor that a data point is "absent
    /// from the case file rather than merely unretrieved", so a signal quoting one file reports a
    /// false absence on every other, and the pipeline validates whichever report it is given.
    ///
    /// Shortened to the clause that carries the meaning rather than dropped: an ATR questionnaire
    /// asking about understanding will be answered in words close to these whoever wrote them, and
    /// the substring scan is case-insensitive.
    /// </summary>
    [Fact]
    public void TheAtrQueryInCheckFourExpectsTheWordingThatCarriesTheFinding()
    {
        var q = Group("CHK-004", "G4.1").Queries.Single(x => x.Id == "Q4.1.4");

        Assert.Contains("no understanding of investments", q.ExpectSignals);
        Assert.DoesNotContain("I have no understanding of investments", q.ExpectSignals);
    }

    // ── the shape must survive ────────────────────────────────────────────────

    /// <summary>
    /// Almost every fix here was added inside an existing group, and the exceptions are worth
    /// naming. The catalogue's group count is pinned elsewhere; this says the same thing from the
    /// other direction, because a fix that quietly added a group changes what a check reports.
    ///
    /// The number moved from 85 to 87 once, and not for a fix: reconciling the published fact
    /// library against the plans found two facts — the target retirement income and the ancillary
    /// estate-planning recommendations — that no requirement read at all, and each needed a
    /// requirement of its own rather than a path bolted onto a neighbour.
    ///
    /// 88 is G3.11, and it is the same kind of gap rather than a new fix in an old place. A
    /// retained plan's funds were read by no requirement in CHK-003, so F3.4 was unreachable by
    /// wording: six runs missed it, four of them after a guard was written for it. A guard can only
    /// stop a group drawing the wrong conclusion; it cannot make a group ask.
    /// </summary>
    [Fact]
    public void OnlyAGapWithNoRequirementAddedAGroup()
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);

        Assert.Equal(88, plans.Values.Sum(p => p.QueryGroups.Count));
    }
}
