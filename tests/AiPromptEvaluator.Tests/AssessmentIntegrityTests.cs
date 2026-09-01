using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The defences added after a real run cleared two checks it should have failed.
///
/// In one, the assessor was given a file note reading "proceed with a Risk rating of 6" and
/// reported, in quotation marks, "proceed with a Risk rating of 5" — which made the evidence
/// agree with the recommendation. In the other, it was asked to compare an age it had no date
/// of birth to derive from, and invented an arithmetic reconciliation in which the client got
/// a year younger between September and October.
///
/// Neither was a retrieval or extraction failure at the point of decision: the evidence was in
/// the pack. What the pipeline lacked was any way to say "I cannot tell", and anyone checking
/// that a quotation was real.
/// </summary>
public class AssessmentIntegrityTests
{
    // ──────────────────────────────────────────────
    // Reasoning before verdict
    // ──────────────────────────────────────────────

    /// <summary>
    /// A JSON-schema response format emits properties in schema order, and a model conditions on
    /// what it has already written. With the verdict first, everything after it is written to
    /// justify a token already committed — so the order is pinned here, and the verdict is last.
    /// </summary>
    [Fact]
    public void Schema_PutsTheVerdictAfterTheReasoning()
    {
        var order = FindingSchema.EmissionOrder;

        Assert.Equal(
            [
                "groupId", "requirement", "reportSays", "fileSays", "discrepancies",
                "comparisonPerformed", "missingInputs", "analysis", "citations",
                "issueCategories", "severity", "outcome",
            ],
            order);

        Assert.Equal(order.Count - 1, order.ToList().IndexOf("outcome"));
        Assert.True(order.ToList().IndexOf("discrepancies") < order.ToList().IndexOf("outcome"),
            "Discrepancies must be listed before the outcome is chosen, or the list is written to fit it.");
    }

    /// <summary>
    /// The check-level outcome is computed from the groups rather than asked for. Previously the
    /// model stated it, and being first in the schema, stated it before assessing anything.
    /// </summary>
    [Fact]
    public void CheckOutcome_IsDerivedFromTheGroups()
    {
        Assert.Equal(CheckOutcome.NoIssue, Check(Group("G1"), Group("G2")).ParsedOutcome);

        Assert.Equal(
            CheckOutcome.PotentialConcern,
            Check(Group("G1"), Group("G2", CheckOutcome.PotentialConcern)).ParsedOutcome);

        // Every group N/A means the check is N/A; one N/A among passes does not.
        Assert.Equal(
            CheckOutcome.NotApplicable,
            Check(Group("G1", CheckOutcome.NotApplicable), Group("G2", CheckOutcome.NotApplicable)).ParsedOutcome);

        Assert.Equal(
            CheckOutcome.NoIssue,
            Check(Group("G1", CheckOutcome.NotApplicable), Group("G2")).ParsedOutcome);
    }

    /// <summary>The summary is composed from the groups, so it cannot claim what they do not say.</summary>
    [Fact]
    public void CheckSummary_NamesTheGroupsThatRaisedConcerns()
    {
        var finding = Check(
            Group("G1"),
            Group("G2", CheckOutcome.PotentialConcern) with
            {
                Discrepancies = ["Report states age 69; fact find states age 70"],
            });

        Assert.Contains("G2", finding.Summary);
        Assert.Contains("age 69", finding.Summary);
        Assert.DoesNotContain("G1", finding.Summary);
    }

    // ──────────────────────────────────────────────
    // "I cannot tell" as an outcome
    // ──────────────────────────────────────────────

    /// <summary>
    /// The gap that caused the invented arithmetic: with only three outcomes, an assessor
    /// missing a value it was told to derive from had no way to report that, and filled the
    /// space instead.
    /// </summary>
    [Fact]
    public void AComparisonThatCouldNotBeMade_IsIndeterminate_WhateverTheModelConcluded()
    {
        var group = Group("G1.1") with
        {
            ComparisonPerformed = false,
            MissingInputs = ["/parties/clients[]/dateOfBirth"],
            Outcome = nameof(CheckOutcome.NoIssue),
        };

        Assert.Equal(CheckOutcome.Indeterminate, group.ParsedOutcome);
        Assert.Equal(CheckOutcome.Indeterminate, Check(group).ParsedOutcome);
    }

    /// <summary>Indeterminate is not a pass, so a concern elsewhere still wins.</summary>
    [Fact]
    public void Indeterminate_RanksBelowAConcernAndAboveAPass()
    {
        Assert.Equal(
            CheckOutcome.PotentialConcern,
            Check(Cannot("G1"), Group("G2", CheckOutcome.PotentialConcern)).ParsedOutcome);

        Assert.Equal(CheckOutcome.Indeterminate, Check(Cannot("G1"), Group("G2")).ParsedOutcome);
    }

    /// <summary>
    /// A requirement nobody could assess must not be folded away with the passes. That is how an
    /// unassessed point gets signed off as reviewed.
    /// </summary>
    [Fact]
    public void FindingsReport_ShowsIndeterminateChecksInFull()
    {
        var report = new FindingsReport(
            "CASE-1", 99, "gpt-4.1", DateTimeOffset.Now, [Check(Cannot("G1.1"))], null);

        var text = report.Format();

        Assert.Contains("FINDINGS REQUIRING ATTENTION", text);
        Assert.Contains("NOT ASSESSED", text);
        Assert.Contains("dateOfBirth", text);
        Assert.Contains("not assessable", report.Headline);
    }

    // ──────────────────────────────────────────────
    // Citation verification
    // ──────────────────────────────────────────────

    [Fact]
    public void Verifier_AcceptsAQuoteThatIsInTheEvidence()
    {
        var finding = CitationVerifier.Verify(
            WithQuote("You are happy to proceed with a Risk rating of 6."),
            ["We agreed a rating. You are happy to proceed with a Risk rating of 6. Signed."]);

        Assert.Empty(finding.UnverifiedQuotes);
        Assert.Equal(CheckOutcome.NoIssue, finding.ParsedOutcome);
    }

    /// <summary>The observed failure, reproduced: one digit changed inside quotation marks.</summary>
    [Fact]
    public void Verifier_CatchesAQuoteThatWasAltered()
    {
        var finding = CitationVerifier.Verify(
            WithQuote("You are happy to proceed with a Risk rating of 5."),
            ["We agreed a rating. You are happy to proceed with a Risk rating of 6. Signed."]);

        Assert.Single(finding.UnverifiedQuotes);

        // The conclusion may or may not have been right; the reasoning cannot be relied on.
        Assert.Equal(CheckOutcome.PotentialConcern, finding.ParsedOutcome);
    }

    /// <summary>
    /// Reflowing a quote across lines, or a converter turning a hyphen into an en dash, is not
    /// what this is looking for. False accusations of fabrication would train people to ignore it.
    /// </summary>
    [Fact]
    public void Verifier_ToleratesFormattingDifferences()
    {
        var finding = CitationVerifier.Verify(
            WithQuote("You are happy to proceed  with a\n  Risk rating of 6."),
            ["You are happy to proceed with a Risk rating of 6."]);

        Assert.Empty(finding.UnverifiedQuotes);
    }

    [Theory]
    [InlineData("‘smart quotes’ around the agreed rating value", "'smart quotes' around the agreed rating value")]
    [InlineData("an en–dash inside a reasonably long quotation", "an en-dash inside a reasonably long quotation")]
    [InlineData("CAPITALISED TEXT IN A REASONABLY LONG QUOTE", "capitalised text in a reasonably long quote")]
    public void Verifier_FoldsAwayPunctuationAndCase(string quoted, string source)
    {
        Assert.Empty(CitationVerifier.Unverified([new FindingCitation { Quote = quoted }], [source]));
    }

    /// <summary>
    /// A short fragment appears in too many places for its absence to mean anything, so it is
    /// not checked at all rather than checked badly.
    /// </summary>
    [Fact]
    public void Verifier_SkipsQuotesTooShortToBeAClaim()
    {
        var shortQuote = new string('x', CitationVerifier.MinimumQuoteLength - 1);

        Assert.Empty(CitationVerifier.Unverified([new FindingCitation { Quote = shortQuote }], ["nothing like it"]));
    }

    /// <summary>A finding may legitimately quote the report's own assertion from the model side.</summary>
    [Fact]
    public void Verifier_AcceptsAQuoteFromTheCanonicalModel()
    {
        Assert.Empty(CitationVerifier.Unverified(
            [new FindingCitation { Quote = "the recommended plan is a Standard Life SIPP" }],
            ["{\"solution\":\"the recommended plan is a Standard Life SIPP\"}"]));
    }

    [Fact]
    public void Verifier_ReportsEachBadQuoteOnce()
    {
        var repeated = new FindingCitation { Quote = "a quotation that does not appear anywhere" };

        var unverified = CitationVerifier.Unverified([repeated, repeated], ["unrelated evidence text"]);

        Assert.Single(unverified);
    }

    /// <summary>
    /// The run-level count exists so one bad citation reads as a defect and several read as a
    /// pattern worth stopping for.
    /// </summary>
    [Fact]
    public void FindingsReport_CountsAndCallsOutUnverifiedCitations()
    {
        var bad = CitationVerifier.Verify(
            WithQuote("a quotation that appears in none of the evidence"), ["something else entirely"]);

        var report = new FindingsReport(
            "CASE-1", 99, "gpt-4.1", DateTimeOffset.Now, [Check(bad)], null);

        var text = report.Format();

        Assert.Equal(1, report.UnverifiedCitations);
        Assert.Contains("QUOTE(S) NOT FOUND", text);
        Assert.Contains("were downgraded", text);
    }

    // ──────────────────────────────────────────────
    // Parsing one group
    // ──────────────────────────────────────────────

    [Fact]
    public void ParseGroup_ReadsAReplyAndTakesTheIdFromThePlan()
    {
        var group = new PlanQueryGroup { GroupId = "G3.6", Requirement = "The override is justified" };

        var finding = CheckPlanRunner.ParseGroup(
            """
            {
              "groupId": "wrong",
              "requirement": "",
              "reportSays": "Agreed code 5, assessed code 4.",
              "fileSays": "The file note records a rating of 6.",
              "discrepancies": ["Agreed code 5 against a file note stating 6"],
              "comparisonPerformed": true,
              "missingInputs": [],
              "analysis": "The two disagree and nothing reconciles them.",
              "citations": [{ "passageId": "P3", "source": "File notes.md", "category": "C", "quote": "rating of 6" }],
              "severity": "High",
              "outcome": "PotentialConcern"
            }
            """,
            group);

        Assert.Equal("G3.6", finding.GroupId);
        Assert.Equal("The override is justified", finding.Requirement);
        Assert.Equal("P3", finding.Citations[0].PassageId);
        Assert.Single(finding.Discrepancies);
        Assert.Equal(CheckOutcome.PotentialConcern, finding.ParsedOutcome);
    }

    /// <summary>An unreadable reply is a concern that could not be assessed — never a pass.</summary>
    [Fact]
    public void ParseGroup_TurnsAnUnreadableReplyIntoAnUnassessedConcern()
    {
        var finding = CheckPlanRunner.ParseGroup(
            "I'm afraid I can't help with that.",
            new PlanQueryGroup { GroupId = "G1.1", Requirement = "Essential facts" });

        Assert.False(finding.ComparisonPerformed);
        Assert.Equal(CheckOutcome.Indeterminate, finding.ParsedOutcome);
        Assert.Contains("I'm afraid", finding.Analysis);
    }

    [Fact]
    public void PassageId_IsOneBasedAndStable()
    {
        Assert.Equal("P1", CheckPlanRunner.PassageId(0));
        Assert.Equal("P12", CheckPlanRunner.PassageId(11));
    }

    // ──────────────────────────────────────────────

    private static GroupFinding Group(string id, CheckOutcome outcome = CheckOutcome.NoIssue) => new()
    {
        GroupId = id,
        Requirement = $"Requirement {id}",
        ReportSays = "The report states a value.",
        FileSays = "The file evidences a value.",
        Analysis = "They agree.",
        Outcome = outcome.ToString(),
    };

    private static GroupFinding Cannot(string id) => Group(id) with
    {
        ComparisonPerformed = false,
        MissingInputs = ["/parties/clients[]/dateOfBirth"],
    };

    private static GroupFinding WithQuote(string quote) => Group("G1") with
    {
        Citations = [new FindingCitation { PassageId = "P1", Source = "File notes.md", Quote = quote }],
    };

    private static CheckFinding Check(params GroupFinding[] groups) =>
        CheckFinding.FromGroups("CHK-001", "A check", groups);
}
