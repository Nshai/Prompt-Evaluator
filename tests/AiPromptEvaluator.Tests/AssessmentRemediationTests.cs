using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The defects found by scoring a run against the hand-adjudicated benchmark in
/// docs/test-results. Each of these threw away work the pipeline had already done: a concern
/// the assessor reached and the runner filed as unassessable, a requirement the assessor
/// rewrote and the runner stored, and quotations rejected for carrying table punctuation.
/// </summary>
public class AssessmentRemediationTests
{
    // ──────────────────────────────────────────────
    // A group that named a discrepancy compared something
    // ──────────────────────────────────────────────

    /// <summary>
    /// G3.7 as it ran. It listed the existing arrangements' risk ratings, wrote Potential
    /// Concern at High severity, and set comparisonPerformed false because it could not map the
    /// provider's scale onto the firm's. The whole finding was filed as unassessable.
    /// </summary>
    [Fact]
    public void AConcernThatNamedDiscrepanciesSurvivesAnUnclosedComparison()
    {
        var group = Group() with
        {
            Outcome = nameof(CheckOutcome.PotentialConcern),
            Severity = "High",
            ComparisonPerformed = false,
            Discrepancies = ["Existing arrangements carry provider ratings 9, 4, 2, 7, 4 against an agreed 5."],
            MissingInputs = ["Mapping from the provider scale to the firm's scale"],
        };

        Assert.True(group.ComparedSomething);
        Assert.Equal(CheckOutcome.PotentialConcern, group.ParsedOutcome);
    }

    /// <summary>
    /// The other half has to keep working: a group that genuinely could not compare, and found
    /// nothing to say, is still Indeterminate. That is the answer the pipeline was missing
    /// before it existed, and widening the previous rule must not remove it.
    /// </summary>
    [Fact]
    public void AGroupWithNothingToReportIsStillIndeterminate()
    {
        var group = Group() with
        {
            ComparisonPerformed = false,
            MissingInputs = ["/parties/clients[]/dateOfBirth"],
        };

        Assert.False(group.ComparedSomething);
        Assert.Equal(CheckOutcome.Indeterminate, group.ParsedOutcome);
    }

    /// <summary>
    /// Discrepancies from a group that could not close its comparison used to reach the detail
    /// body and nothing else, so the summary a reviewer reads listed group ids and dropped the
    /// findings.
    ///
    /// Most such groups are now concerns rather than Indeterminate, which is the point of the
    /// rule above. What remains is the group that <em>states</em> Indeterminate for itself and
    /// still noticed something on the way — and that observation must reach the summary.
    /// </summary>
    [Fact]
    public void TheSummaryCarriesWhatAnIndeterminateGroupStillNoticed()
    {
        var stuck = Group() with
        {
            ComparisonPerformed = false,
            MissingInputs = ["The client's marginal rate of tax"],
            Discrepancies = [],
        };

        var noticed = Group() with
        {
            GroupId = "G7.7",
            Outcome = nameof(CheckOutcome.Indeterminate),
            Discrepancies = ["The switch reduces projected value by 9.4% at mid growth."],
            MissingInputs = ["The report's own reasoning for recommending it anyway"],
        };

        Assert.Equal(CheckOutcome.Indeterminate, noticed.ParsedOutcome);

        var check = CheckFinding.FromGroups("CHK-007", "Costs and charges", [stuck, noticed]);

        Assert.Contains("could not be assessed", check.Summary);
        Assert.Contains("reduces projected value by 9.4%", check.Summary);
    }

    // ──────────────────────────────────────────────
    // The requirement is the plan's to state
    // ──────────────────────────────────────────────

    /// <summary>
    /// G1.7 as it ran: asked about "Existing products (pensions and investments)", it answered —
    /// and recorded — "Existing products (pensions, investments and protection plans)". A
    /// requirement the assessor can redefine is not auditable.
    /// </summary>
    [Fact]
    public void TheRequirementComesFromThePlanEvenWhenTheModelRewritesIt()
    {
        var plan = new PlanQueryGroup
        {
            GroupId = "G1.7",
            Requirement = "Existing products (pensions and investments) are consistent with the evidence",
        };

        var parsed = CheckPlanRunner.ParseGroup(
            """
            {"groupId":"CHK-001",
             "requirement":"Existing products (pensions, investments and protection plans) are consistent with the evidence",
             "outcome":"PotentialConcern","comparisonPerformed":true}
            """,
            plan);

        Assert.Equal("G1.7", parsed.GroupId);
        Assert.Equal(plan.Requirement, parsed.Requirement);

        // Both divergences are kept, because a third of responses losing track of the question
        // is worth measuring even when nothing downstream depends on it.
        Assert.True(parsed.GroupIdDiverged);
        Assert.True(parsed.RequirementDiverged);
    }

    /// <summary>
    /// Prefixing the identifier into the requirement — "[G3.1] Risk rating and summary" — is a
    /// formatting habit, not a redefinition, and counting it as one would bury the real signal.
    /// </summary>
    [Fact]
    public void EchoingTheIdentifierInsideTheRequirementIsNotDivergence()
    {
        var plan = new PlanQueryGroup { GroupId = "G3.1", Requirement = "Risk rating and summary" };

        var parsed = CheckPlanRunner.ParseGroup(
            """
            {"groupId":"G3.1","requirement":"[G3.1] Risk rating and summary",
             "outcome":"NoIssue","comparisonPerformed":true}
            """,
            plan);

        Assert.False(parsed.GroupIdDiverged);
        Assert.False(parsed.RequirementDiverged);
    }

    [Fact]
    public void TheReportCountsPromptAdherence()
    {
        var plan = new PlanQueryGroup { GroupId = "G8.2", Requirement = "Product-specific risks explained" };

        var diverged = CheckPlanRunner.ParseGroup(
            """{"groupId":"CHK-008","requirement":"Something else entirely","outcome":"NoIssue","comparisonPerformed":true}""",
            plan);

        var report = new FindingsReport(
            "ABC-99", 99, "m", DateTimeOffset.Now,
            [CheckFinding.FromGroups("CHK-008", "Disadvantages", [diverged])], null);

        Assert.Equal((1, 1, 1), report.Adherence);
        Assert.Contains("Prompt adherence", report.Format());
    }

    // ──────────────────────────────────────────────
    // Citation matching
    // ──────────────────────────────────────────────

    /// <summary>
    /// Most of this evidence is converted tables, and whether a model reproduces the cell
    /// separators is a fact about markdown rather than about whether it read the document.
    /// </summary>
    [Fact]
    public void AQuoteCarryingTablePunctuationVerifies()
    {
        var evidence =
            """
            Savings & Investments
            | Investment Description   | Owner   | Product Type    |   Opening Value (£) |
            |--------------------------|---------|-----------------|---------------------|
            | Savings                  | JS      | Cash Account    |               6,000 |
            """;

        Assert.Empty(CitationVerifier.Unverified(
            [Cite("Savings JS Cash Account 6,000")], [evidence]));

        Assert.Empty(CitationVerifier.Unverified(
            [Cite("| Savings | JS | Cash Account | 6,000 |")], [evidence]));
    }

    /// <summary>An ellipsis is the model saying it skipped something, and both spans are checked.</summary>
    [Fact]
    public void AnElidedQuoteVerifiesWhenBothSpansArePresentInOrder()
    {
        var evidence = new[]
        {
            "Your natural risk level is based on the answers given in the Attitude to Risk "
            + "Questionnaire which was completed on 18/09/2025 and the result was 4 Cautious Balanced.",
        };

        Assert.Empty(CitationVerifier.Unverified(
            [Cite("Your natural risk level is based on the answers given ... 4 Cautious Balanced")],
            evidence));
    }

    [Fact]
    public void AnElidedQuoteWhoseSpansAreOutOfOrderDoesNotVerify()
    {
        var evidence = new[] { "The existing plan projects £127,000 and the new solution £115,000." };

        Assert.Single(CitationVerifier.Unverified(
            [Cite("the new solution £115,000 ... The existing plan projects £127,000")],
            evidence));
    }

    /// <summary>
    /// The reason a near-miss rule was rejected. This altered quotation shares a 96% contiguous
    /// run with its source, so any tolerance loose enough to accept the elisions accepts this —
    /// and this is the failure the verifier exists for.
    /// </summary>
    [Fact]
    public void AQuotationAlteredByOneDigitStillFails()
    {
        var evidence = new[] { "We agreed a rating. You are happy to proceed with a Risk rating of 6. Signed." };

        Assert.Single(CitationVerifier.Unverified(
            [Cite("You are happy to proceed with a Risk rating of 5.")], evidence));
    }

    /// <summary>
    /// Canonical fragments reach the prompt as JSON, and the default encoder escapes a pound
    /// sign. The model quotes it back rendered, and the finding that caught the report's own
    /// pension-total inconsistency was reported as a fabricated quotation for citing the place
    /// it came from.
    /// </summary>
    [Fact]
    public void AFragmentQuotedBackWithItsCurrencySymbolVerifies()
    {
        var model =
            """
            {"extractionReport":{"internalInconsistencies":[
              {"description":"The table on page 5 sums to £116,997.47 but page 3 states £110,000."}]}}
            """;

        var fragment = new CanonicalModelAccessor(model)
            .Resolve("/extractionReport/internalInconsistencies");

        Assert.True(fragment.Found);
        Assert.Contains("£116,997.47", fragment.Json);

        Assert.Empty(CitationVerifier.Unverified(
            [Cite("The table on page 5 sums to £116,997.47 but page 3 states £110,000.")],
            [fragment.Json]));
    }

    // ──────────────────────────────────────────────
    // Confidence is reported without overwriting the verdict
    // ──────────────────────────────────────────────

    [Fact]
    public void AConcernKeepsItsVerdictAndReportsItsConfidence()
    {
        var group = CitationVerifier.Verify(
            Group() with
            {
                Outcome = nameof(CheckOutcome.PotentialConcern),
                Citations = [new FindingCitation { PassageId = "P1", Quote = "a quote that is not in the evidence at all" }],
            },
            ["something else entirely"]);

        Assert.Equal(CheckOutcome.PotentialConcern, group.ParsedOutcome);
        Assert.Equal(CitationConfidence.Unverified, group.Confidence);
    }

    /// <summary>A pass resting on an untraceable quotation is still downgraded.</summary>
    [Fact]
    public void APassRestingOnAnUntraceableQuoteIsStillDowngraded()
    {
        var group = CitationVerifier.Verify(
            Group() with
            {
                Outcome = nameof(CheckOutcome.NoIssue),
                Citations = [new FindingCitation { PassageId = "P1", Quote = "a quote that is not in the evidence at all" }],
            },
            ["something else entirely"]);

        Assert.Equal(CheckOutcome.PotentialConcern, group.ParsedOutcome);
    }

    // ──────────────────────────────────────────────

    private static FindingCitation Cite(string quote) =>
        new() { PassageId = "P1", Source = "doc.md", Quote = quote };

    private static GroupFinding Group() => new()
    {
        GroupId = "G3.7",
        Requirement = "A requirement",
        ReportSays = "The report states a value.",
        FileSays = "The file evidences a value.",
        Analysis = "Reasoning.",
        Outcome = nameof(CheckOutcome.NoIssue),
        ComparisonPerformed = true,
    };
}
