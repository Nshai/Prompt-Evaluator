using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// One check finds a document; another says the file does not have it.
///
/// <b>The most expensive failure on record, and the only one visible without a model call.</b>
/// Two runs of one case, on two models at a 3.3× cost difference, received byte-identical evidence
/// packs. Both reported a questionnaire as missing from the file while a passage carrying it sat
/// at rank 6 of 24 in the pack they were reading. In the stronger run the two halves were one
/// check apart — one group cited the passage id the document had arrived under, the next said the
/// file held no such document — and no component ever held both sentences, so nothing could see
/// the contradiction.
///
/// A prompt rule was written for the same defect, and it depends on the assessor following it.
/// This does not: it reads the finished findings, costs nothing, and reports the pair.
///
/// <b>It reports rather than judges.</b> One side may be talking about a different document, and
/// the group claiming the absence may simply never have been given the passage — which is a
/// retrieval defect, not an advice one. Only a reader can tell those apart.
/// </summary>
public class DocumentDisputeTests
{
    /// <summary>The observed pair, in miniature.</summary>
    [Fact]
    public void OneGroupFindingADocumentAndAnotherMissingItIsReported()
    {
        var disputes = CrossGroupContradictions.Documents(
        [
            Finding("CHK-003",
                Group("G3.3", "The capacity for loss questionnaire responses in [P17] are on file."),
                Group("G3.2", "The file holds no capacity for loss questionnaire responses.")),
        ]);

        var dispute = Assert.Single(disputes);

        Assert.Equal("capacity for loss questionnaire", dispute.DocumentKind);
        Assert.Contains("CHK-003/G3.3", dispute.Present);
        Assert.Contains("CHK-003/G3.2", dispute.Absent);
    }

    /// <summary>
    /// It reaches across checks as well as within one. The split that hides these is the check
    /// catalogue itself, so a pass that only looked inside a check would miss most of them.
    /// </summary>
    [Fact]
    public void TheTwoHalvesMayBeInDifferentChecks()
    {
        var disputes = CrossGroupContradictions.Documents(
        [
            Finding("CHK-001", Group("G1.2", "The provider illustration is on file.")),
            Finding("CHK-007", Group("G7.4", "There is no provider illustration in the case file.")),
        ]);

        Assert.Single(disputes);
    }

    /// <summary>
    /// <b>The real pair, quoted from the run that prompted all of this.</b> Two groups of one
    /// check, four thousand lines apart in the same log, disagreeing about whether a
    /// questionnaire's responses exist — and no component ever held both sentences.
    ///
    /// Note the shape. Neither says "there is no questionnaire": one says the responses are
    /// visible in a named passage, the other that the questionnaire has no recorded responses.
    /// A pass keyed on the document alone sees no disagreement here, which is why claims are
    /// keyed on the document <i>and the part of it being claimed about</i>.
    /// </summary>
    [Fact]
    public void TheObservedPairFromTheRunIsReported()
    {
        var disputes = CrossGroupContradictions.Documents(
        [
            Finding("CHK-003",
                Group("G3.10",
                    "[P16] shows the raw CFL questionnaire responses but no derived rating or "
                    + "label is visible in those passages."),
                Group("G3.2",
                    "The CFL questionnaire in the risk profile tool has no recorded responses "
                    + "for the capacity for loss section.")),
        ]);

        var dispute = Assert.Single(disputes);

        Assert.Equal("questionnaire (response)", dispute.DocumentKind);
        Assert.Contains("CHK-003/G3.10", dispute.Present);
        Assert.Contains("CHK-003/G3.2", dispute.Absent);
    }

    /// <summary>
    /// The same sentence denies a rating and asserts the responses. Only the part with a
    /// counterpart is reported — the unmatched denial is a finding that group already made, not
    /// a disagreement with anybody.
    /// </summary>
    [Fact]
    public void ADeniedPartWithNoCounterpartIsNotReported()
    {
        var disputes = CrossGroupContradictions.Documents(
        [
            Finding("CHK-003",
                Group("G3.10", "[P16] shows the questionnaire responses but no derived rating."),
                Group("G3.2", "The questionnaire has no recorded responses.")),
        ]);

        Assert.Single(disputes);
        Assert.DoesNotContain(disputes, d => d.DocumentKind.Contains("rating"));
    }

    // ── what must not be reported ─────────────────────────────────────────────

    /// <summary>
    /// <b>The sentence the whole pass turns on.</b> "It is on file and records no rating" carries
    /// a presence cue and an absence cue at once, and it is the single most useful sentence an
    /// assessor can write about a thin document. Read as an absence it would be paired against
    /// itself, and the pass would report a contradiction between one group and no other.
    /// </summary>
    [Fact]
    public void ASentenceSayingADocumentIsPresentAndEmptyIsNotADispute()
    {
        var disputes = CrossGroupContradictions.Documents(
        [
            Finding("CHK-003",
                Group("G3.2", "The questionnaire is on file and records no capacity for loss rating."),
                Group("G3.3", "The questionnaire in [P4] is on file.")),
        ]);

        Assert.Empty(disputes);
    }

    /// <summary>One group agreeing with itself is not a dispute.</summary>
    [Fact]
    public void OneGroupIsNeverInDisputeWithItself()
    {
        var disputes = CrossGroupContradictions.Documents(
        [
            Finding("CHK-003", Group("G3.2", "There is no fact find. The fact find is on file.")),
        ]);

        Assert.Empty(disputes);
    }

    /// <summary>
    /// Two groups agreeing that a document is missing is a finding they have already made, not a
    /// contradiction between them.
    /// </summary>
    [Fact]
    public void TwoGroupsAgreeingADocumentIsMissingIsNotADispute()
    {
        var disputes = CrossGroupContradictions.Documents(
        [
            Finding("CHK-003",
                Group("G3.2", "There is no risk profile report in the case file."),
                Group("G3.4", "No risk profile report was provided.")),
        ]);

        Assert.Empty(disputes);
    }

    /// <summary>
    /// A specific document is matched as itself rather than as the general kind it contains, so
    /// two genuinely different documents are not paired against each other.
    /// </summary>
    [Fact]
    public void ASpecificQuestionnaireIsNotConfusedWithQuestionnairesInGeneral()
    {
        var disputes = CrossGroupContradictions.Documents(
        [
            Finding("CHK-003",
                Group("G3.1", "The attitude to risk questionnaire is on file."),
                Group("G3.2", "The file holds no capacity for loss questionnaire.")),
        ]);

        Assert.Empty(disputes);
    }

    // ── the report ────────────────────────────────────────────────────────────

    /// <summary>
    /// The addendum says what to do about it, including the possibility that the pipeline is at
    /// fault rather than the file — which is what it was, both times it was measured.
    /// </summary>
    [Fact]
    public void TheAddendumPointsAtRetrievalAsWellAsAtTheFile()
    {
        var text = CrossGroupContradictions.Format(
            CrossGroupContradictions.Documents(
            [
                Finding("CHK-003",
                    Group("G3.3", "The fact find is on file."),
                    Group("G3.2", "There is no fact find.")),
            ]));

        Assert.Contains("DOCUMENTS ONE CHECK FOUND AND ANOTHER CALLED MISSING", text);
        Assert.Contains("may simply not have been given the passage", text);
    }

    [Fact]
    public void NothingToReportPrintsNothing()
    {
        Assert.Empty(CrossGroupContradictions.Format(
            Array.Empty<CrossGroupContradictions.DocumentDispute>()));
    }

    // ──────────────────────────────────────────────

    private static CheckFinding Finding(string checkId, params GroupFinding[] groups) =>
        new() { CheckId = checkId, CheckName = "Test", Groups = [.. groups] };

    private static GroupFinding Group(string groupId, string analysis) =>
        new() { GroupId = groupId, Analysis = analysis };
}
