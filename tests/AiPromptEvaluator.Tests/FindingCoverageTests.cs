using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The two numbers printed beside every finding.
///
/// <b>Both are counted from the run, never asked for.</b> A model invited to rate its own
/// confidence rates it high, and the number then travels beside the finding looking like
/// evidence. Every deduction here is something the app established independently — a quotation
/// the verifier could not trace, a comparison the assessor said it could not make — so the tests
/// below are about what the run did, not about what it claimed.
///
/// They are also kept apart on purpose. A requirement can be fully covered and badly reasoned,
/// or thinly covered and reasoned impeccably about the little it had, and the second is often
/// the correct finding. One number would average those into something describing neither.
/// </summary>
public class FindingCoverageTests
{
    // ── agent coverage ────────────────────────────────────────────────────────

    [Fact]
    public void AFindingThatWentWrongNowhereScoresFullMarks()
    {
        var coverage = FindingCoverage.Agent(Group(Sound()));

        Assert.Equal(100, coverage.Score);
        Assert.Equal("High", coverage.Level);
        Assert.Empty(coverage.Reasons);
        Assert.Contains("traced back", coverage.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The failure the verifier exists for, so it carries the largest deduction.</summary>
    [Fact]
    public void CitationsThatTraceToNothingCostMoreThanAnythingElse()
    {
        var finding = Sound() with { UnverifiedQuotes = ["£ 1,200.00"] };

        Assert.Equal(CitationConfidence.Unverified, finding.Confidence);

        var coverage = FindingCoverage.Agent(Group(finding));

        Assert.Equal(60, coverage.Score);
        Assert.Equal("Medium", coverage.Level);
        Assert.Contains("could be traced", coverage.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PartiallyTracedCitationsCostHalfOfThat()
    {
        var finding = Sound() with
        {
            Citations =
            [
                new FindingCitation { PassageId = "P1", Source = "Fact Find.md", Quote = "£ 1,200.00" },
                new FindingCitation { PassageId = "P2", Source = "Fact Find.md", Quote = "£ 300.00" },
            ],
            UnverifiedQuotes = ["£ 1,200.00"],
        };

        Assert.Equal(CitationConfidence.PartiallyVerified, finding.Confidence);
        Assert.Equal(80, FindingCoverage.Agent(Group(finding)).Score);
    }

    [Fact]
    public void AComparisonTheAssessorCouldNotCloseIsDeductedAndNamed()
    {
        var finding = Sound() with
        {
            ComparisonPerformed = false,
            MissingInputs = ["no expenditure figure was retrieved"],
        };

        var coverage = FindingCoverage.Agent(Group(finding));

        Assert.Equal(75, coverage.Score);
        Assert.Contains("no expenditure figure", coverage.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A requirement that named differences and then passed. Most such differences are
    /// immaterial; one of them, in an earlier run, was a benchmark finding.
    /// </summary>
    [Fact]
    public void DifferencesRaisedAndNotCarriedAreDeducted()
    {
        var finding = Sound() with { Discrepancies = ["Address differs by a postcode."] };

        Assert.NotEmpty(finding.DiscardedDiscrepancies);

        var coverage = FindingCoverage.Agent(Group(finding));

        Assert.Equal(90, coverage.Score);
        Assert.Contains("and then passed", coverage.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeductionsAccumulateAndTheScoreNeverGoesBelowNothing()
    {
        var finding = Sound() with
        {
            UnverifiedQuotes = ["a", "b"],
            ComparisonPerformed = false,
            EvidenceShortfall = "no category B passage reached the pack",
            EchoedGroupId = "CHK-001",
            EchoedRequirement = "Something the plan never asked about",
        };

        var coverage = FindingCoverage.Agent(Group(finding));

        Assert.InRange(coverage.Score, 0, 20);
        Assert.Equal("Low", coverage.Level);
        Assert.True(coverage.Reasons.Count >= 4);
    }

    /// <summary>Nothing was assessed, which is not the same as assessed badly.</summary>
    [Fact]
    public void ARequirementWithNoFindingIsNotScoredAtAll()
    {
        var coverage = FindingCoverage.Agent(Group(null));

        Assert.Equal(0, coverage.Score);
        Assert.Equal("None", coverage.Level);
        Assert.Contains("No finding", coverage.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ── evidence coverage ─────────────────────────────────────────────────────

    [Fact]
    public void ACategoryTheAssessorReadCountsInFull()
    {
        var group = Group(Sound(), queries: [new RecordedQuery("Q1", "income", "B", 1)], passages:
        [
            Passage("Q1", "B", "Fact Find.md", inPack: true),
        ]);

        var coverage = FindingCoverage.Evidence(group);

        Assert.Equal(100, coverage.Score);
        Assert.Equal("Full", coverage.Level);
        Assert.Contains(coverage.Items, i => i.Signal == CoverageSignal.Read);
    }

    /// <summary>
    /// <b>The distinction a single percentage hides.</b> A requirement that reached nothing
    /// because the case file holds nothing has found something; one that reached nothing because
    /// ranking evicted it has found nothing and does not know.
    /// </summary>
    [Fact]
    public void ACategoryRetrievedAndThenEvictedIsHalfCoveredAndSaysSo()
    {
        var group = Group(Sound(), queries: [new RecordedQuery("Q1", "income", "B", 1)], passages:
        [
            Passage("Q1", "B", "Fact Find.md", inPack: false),
        ]);

        var coverage = FindingCoverage.Evidence(group);

        Assert.Equal(50, coverage.Score);
        Assert.Equal("Limited", coverage.Level);
        Assert.Contains(coverage.Items, i =>
            i.Signal == CoverageSignal.Partial
            && i.Label.Contains("evicted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ACategorySearchedForAndNeverFoundCountsForNothingAndIsListed()
    {
        var group = Group(Sound(), queries: [new RecordedQuery("Q1", "income", "B", 0)], passages: []);

        var coverage = FindingCoverage.Evidence(group);

        Assert.Equal(0, coverage.Score);
        Assert.Contains(coverage.Items, i =>
            i.Signal == CoverageSignal.Absent
            && i.Label.Contains("no passage", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A requirement with no evidence query is assessed from the report alone — a deliberate plan
    /// shape for internal contradictions and charge arithmetic, and scoring it as uncovered would
    /// turn a sound finding into a gap.
    /// </summary>
    [Fact]
    public void ARequirementThatSearchedForNothingIsFullyCoveredNotUncovered()
    {
        var coverage = FindingCoverage.Evidence(Group(Sound()));

        Assert.Equal(100, coverage.Score);
        Assert.Equal("Full", coverage.Level);
        Assert.Contains(coverage.Items, i =>
            i.Label.Contains("report alone", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// An unreadable canonical path is a broken extraction, not a thin case file, so it caps the
    /// score rather than sliding it — a finding cannot read "Full" while the model it compared
    /// against was missing a value.
    /// </summary>
    [Fact]
    public void ACanonicalPathMissingFromTheModelCapsTheScoreBelowFull()
    {
        var group = Group(
            Sound(),
            facts:
            [
                new RecordedFact("/financialPosition/income", "\"£300\"", true),
                new RecordedFact("/financialPosition/expenditure", string.Empty, false),
            ],
            queries: [new RecordedQuery("Q1", "income", "B", 1)],
            passages: [Passage("Q1", "B", "Fact Find.md", inPack: true)]);

        var coverage = FindingCoverage.Evidence(group);

        Assert.True(coverage.Score < 90);
        Assert.NotEqual("Full", coverage.Level);
        Assert.Contains(coverage.Items, i => i.Signal == CoverageSignal.Unreadable);
    }

    /// <summary>
    /// A contradiction sitting in a category nobody thought to name is exactly what the
    /// unrestricted half of every search exists to find, so it is credited rather than ignored.
    /// </summary>
    [Fact]
    public void ACategoryTheAssessorReachedWithoutBeingAskedToIsCredited()
    {
        var group = Group(Sound(), queries: [new RecordedQuery("Q1", "income", "B", 2)], passages:
        [
            Passage("Q1", "B", "Fact Find.md", inPack: true),
            Passage("Q1", "E", "Illustration.md", inPack: true),
        ]);

        var coverage = FindingCoverage.Evidence(group);

        Assert.Equal(100, coverage.Score);
        Assert.Contains(coverage.Items, i =>
            i.Label.Contains("not asked for", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A search that returned nothing leaves no passage behind, so without the recorded queries a
    /// requirement whose every search came back empty reads exactly like one that searched for
    /// nothing at all. Those are opposite findings.
    /// </summary>
    [Fact]
    public void SearchesThatReturnedNothingAreCountedEvenThoughTheyLeftNoPassage()
    {
        var group = Group(
            Sound(),
            queries:
            [
                new RecordedQuery("Q1", "income", "B", 1),
                new RecordedQuery("Q2", "outgoings", "B", 0),
            ],
            passages: [Passage("Q1", "B", "Fact Find.md", inPack: true)]);

        var coverage = FindingCoverage.Evidence(group);

        Assert.Contains(coverage.Items, i =>
            i.Label.Contains("of 2 search(es)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheArchivedGroupAndTheRendererComputeTheSameNumbers()
    {
        var group = Group(Sound(), queries: [new RecordedQuery("Q1", "income", "B", 1)], passages:
        [
            Passage("Q1", "B", "Fact Find.md", inPack: true),
        ]);

        Assert.Equal(FindingCoverage.Agent(group).Score, group.AgentCoverage.Score);
        Assert.Equal(FindingCoverage.Evidence(group).Score, group.EvidenceCoverage.Score);
    }

    [Fact]
    public void AScoreReadsAsAPercentage() => Assert.Equal("80%", FindingCoverage.Percent(80));

    // ── fixtures ──────────────────────────────────────────────────────────────

    private static GroupFinding Sound() => new()
    {
        GroupId = "G1.4",
        Requirement = "Income and expenditure figures are consistent with the evidence",
        Outcome = nameof(CheckOutcome.NoIssue),
        Citations = [new FindingCitation { PassageId = "P1", Source = "Fact Find.md", Quote = "£ 1,200.00" }],
    };

    private static RecordedPassage Passage(string queryId, string category, string document, bool inPack) =>
        new(queryId, "income", category, document, category, category, 0.8, "text", inPack, 0);

    private static RecordedGroup Group(
        GroupFinding? finding,
        IReadOnlyList<RecordedFact>? facts = null,
        IReadOnlyList<RecordedQuery>? queries = null,
        IReadOnlyList<RecordedPassage>? passages = null) =>
        new(
            CheckId: "CHK-001",
            GroupId: "G1.4",
            Requirement: "Income and expenditure figures are consistent with the evidence",
            Facts: facts ?? [],
            Queries: queries ?? [],
            Passages: passages ?? [],
            SystemPrompt: "system",
            UserPrompt: "user",
            RawResponse: "{}",
            Finding: finding);
}
