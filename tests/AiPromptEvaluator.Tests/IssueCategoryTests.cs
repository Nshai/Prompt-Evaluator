using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// What kind of problem a finding is, and why it wants a second pair of eyes.
///
/// <b>The two are deliberately different kinds of claim.</b> A category is a judgement about the
/// case and belongs to the assessor; a closer-look flag is a fact about how the assessment was
/// made and is counted from the run. Asking the thing being audited to grade its own working is
/// how a self-reported confidence score comes to mean nothing, so nothing here is asked for.
///
/// The vocabulary is closed for a reason a free-text field cannot serve: a reviewer filtering to
/// "every data inconsistency in this case" is asking a question the findings can only answer if
/// they agree on the words.
/// </summary>
public class IssueCategoryTests
{
    // ── the vocabulary ────────────────────────────────────────────────────────

    [Fact]
    public void TheVocabularyIsTheNineTheDesignLists() => Assert.Equal(9, IssueCategory.All.Count);

    [Fact]
    public void CaseIsForgiven()
    {
        Assert.Equal(IssueCategory.DataInconsistency, IssueCategory.Normalise("DATA INCONSISTENCY"));
        Assert.Equal(IssueCategory.ProcessRisk, IssueCategory.Normalise("  process risk  "));
    }

    /// <summary>
    /// A near-miss is dropped rather than guessed at. Filing "Data inconsistencies" under "Data
    /// quality" breaks the filter more quietly than dropping it would.
    /// </summary>
    [Fact]
    public void ANearMissIsDroppedRatherThanGuessedAt()
    {
        Assert.Null(IssueCategory.Normalise("Data inconsistencies"));
        Assert.Null(IssueCategory.Normalise("inconsistent data"));
        Assert.False(IssueCategory.IsKnown("Something else entirely"));
    }

    [Fact]
    public void CleaningKeepsVocabularyOrderAndDropsDuplicatesAndStrangers()
    {
        var cleaned = IssueCategory.Clean(
        [
            IssueCategory.ComplianceConcern,
            "data inconsistency",
            IssueCategory.DataInconsistency,
            "Not a category",
        ]);

        Assert.Equal([IssueCategory.DataInconsistency, IssueCategory.ComplianceConcern], cleaned);
    }

    [Fact]
    public void NothingAtAllIsAValidAnswer()
    {
        Assert.Empty(IssueCategory.Clean(null));
        Assert.Empty(IssueCategory.Clean([]));
        Assert.Empty(IssueCategory.Clean(["", "   "]));
    }

    /// <summary>What a plan lint needs in order to name the value it rejected.</summary>
    [Fact]
    public void AnUnrecognisedValueIsReportedRatherThanSilentlyDropped()
    {
        var unknown = IssueCategory.Unknown([IssueCategory.DataQuality, "Vibes", "vibes"]);

        Assert.Equal(["Vibes"], unknown);
    }

    [Fact]
    public void TheSchemaEnumOffersExactlyTheVocabulary()
    {
        var json = IssueCategory.SchemaEnum();

        foreach (var category in IssueCategory.All)
        {
            Assert.Contains("\"" + category + "\"", json, StringComparison.Ordinal);
        }
    }

    // ── the steer a plan may give ─────────────────────────────────────────────

    /// <summary>
    /// The plan decides what is searched for; it never decides what the evidence means. A steer
    /// is the kinds of problem this requirement usually raises — the assessor is free to name a
    /// different one, or none.
    /// </summary>
    [Fact]
    public void APlansSteerIsCleanedToTheVocabularyLikeAnyOtherSource()
    {
        var group = new PlanQueryGroup
        {
            GroupId = "G1.4",
            Requirement = "Income and expenditure figures are consistent",
            Verification = new PlanVerification
            {
                IssueCategories = ["data inconsistency", "Not a category", IssueCategory.DataQuality],
            },
        };

        Assert.Equal(
            [IssueCategory.DataInconsistency, IssueCategory.DataQuality],
            group.SteeredIssueCategories);
    }

    [Fact]
    public void APlanThatSteersNowhereSteersNowhere()
    {
        var group = new PlanQueryGroup { GroupId = "G1.4", Requirement = "Something" };

        Assert.Empty(group.SteeredIssueCategories);
    }

    // ── the closer-look flags ─────────────────────────────────────────────────

    /// <summary>
    /// A finding resting on a quotation that is not in its own evidence once cleared a check that
    /// should have failed.
    /// </summary>
    [Fact]
    public void AQuotationThatTracedToNothingFlagsLowConfidence()
    {
        var finding = Finding() with { UnverifiedQuotes = ["£ 1,200.00"] };

        Assert.True(CloserLook.From(finding, [], []).LowConfidence);
    }

    /// <summary>
    /// A requirement whose pack reached no passage read as "the file says nothing" when the file
    /// said plenty and ranking had dropped it.
    /// </summary>
    [Fact]
    public void EvidenceRetrievedAndThenEvictedEntirelyFlagsPartialCoverage()
    {
        var passages = new[] { Passage(inPack: false) };

        Assert.True(CloserLook.From(Finding(), [], passages).PartialCoverage);
    }

    /// <summary>
    /// A canonical path resolving to nothing read as an absent fact when the extraction had lost
    /// the section — a broken extraction, not a thin case file.
    /// </summary>
    [Fact]
    public void ACanonicalPathThatResolvedToNothingFlagsAnUnreadableSource()
    {
        var facts = new[] { new RecordedFact("/financialPosition/income", string.Empty, false) };

        Assert.True(CloserLook.From(Finding(), facts, []).UnreadableSource);
    }

    [Fact]
    public void ASoundFindingIsFlaggedForNothing()
    {
        var look = CloserLook.From(Finding(), [new RecordedFact("/p", "1", true)], [Passage(inPack: true)]);

        Assert.False(look.Any);
        Assert.Empty(look.Labels);
        Assert.Equal(CloserLook.None, look);
    }

    [Fact]
    public void TheLabelsReadInAFixedOrderSoAFilterAndAFindingAgree()
    {
        var look = new CloserLook(LowConfidence: true, PartialCoverage: true, UnreadableSource: true);

        Assert.Equal(
            [CloserLook.LowConfidenceLabel, CloserLook.PartialCoverageLabel, CloserLook.UnreadableSourceLabel],
            look.Labels);
    }

    // ── fixtures ──────────────────────────────────────────────────────────────

    private static GroupFinding Finding() => new()
    {
        GroupId = "G1.4",
        Requirement = "Income and expenditure figures are consistent",
        Outcome = nameof(CheckOutcome.NoIssue),
        Citations = [new FindingCitation { PassageId = "P1", Source = "Fact Find.md", Quote = "£ 1,200.00" }],
    };

    private static RecordedPassage Passage(bool inPack) =>
        new("Q1", "income", "B", "Fact Find.md", "B", "Fact Find", 0.8, "text", inPack, 0);
}
