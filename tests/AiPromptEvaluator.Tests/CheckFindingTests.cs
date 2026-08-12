using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Reading and consolidating findings. The rule that matters here is the failure direction:
/// anything the assessor returns that cannot be read as a clear outcome has to land as a
/// concern, because a QA tool that turns confusion into a pass is worse than no tool.
/// </summary>
public class CheckFindingTests
{
    [Theory]
    [InlineData("NoIssue", CheckOutcome.NoIssue)]
    [InlineData("No Issue", CheckOutcome.NoIssue)]
    [InlineData("no_issue", CheckOutcome.NoIssue)]
    [InlineData("PotentialConcern", CheckOutcome.PotentialConcern)]
    [InlineData("Potential Concern", CheckOutcome.PotentialConcern)]
    [InlineData("NotApplicable", CheckOutcome.NotApplicable)]
    [InlineData("N/A", CheckOutcome.NotApplicable)]
    [InlineData("n/a", CheckOutcome.NotApplicable)]
    public void ParseOutcome_ReadsTheOutcomesTheChecksAllow(string text, CheckOutcome expected)
    {
        Assert.Equal(expected, CheckFinding.ParseOutcome(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Inconclusive")]
    [InlineData("Mostly fine I think")]
    public void ParseOutcome_TreatsAnythingUnrecognisedAsAConcern(string? text)
    {
        Assert.Equal(CheckOutcome.PotentialConcern, CheckFinding.ParseOutcome(text));
    }

    [Fact]
    public void Failed_NeverReadsAsAPass()
    {
        var finding = CheckFinding.Failed("CHK-001", "Completeness", "boom", TimeSpan.FromSeconds(1));

        Assert.Equal(CheckOutcome.Error, finding.ParsedOutcome);
        Assert.NotEqual(CheckOutcome.NoIssue, finding.ParsedOutcome);
        Assert.Equal("boom", finding.Error);
    }

    private static CheckFinding Finding(string id, CheckOutcome outcome, string name = "Check") => new()
    {
        CheckId = id,
        CheckName = name,
        Outcome = outcome.ToString(),
        Summary = $"{id} summary",
        Groups =
        [
            new GroupFinding
            {
                GroupId = $"{id}-G1",
                Requirement = "A requirement",
                Outcome = outcome.ToString(),
                Severity = "High",
                Explanation = "Because of the evidence.",
                Citations = [new FindingCitation { Source = "Fact Find.md", Category = "B", Quote = "a quote" }],
            },
        ],
        SearchesRun = 4,
        PassagesRetrieved = 20,
        CanonicalPathsResolved = 3,
        CanonicalPathsMissing = 1,
        Usage = new TokenUsage(100, 20, 0, 0),
        Elapsed = TimeSpan.FromSeconds(2),
    };

    private static FindingsReport Report(params CheckFinding[] findings) =>
        new("CASE-1", 99, "gpt-4.1", DateTimeOffset.Now, findings, null);

    [Fact]
    public void Headline_CountsEachOutcome()
    {
        var report = Report(
            Finding("CHK-001", CheckOutcome.PotentialConcern),
            Finding("CHK-002", CheckOutcome.NoIssue),
            Finding("CHK-003", CheckOutcome.NotApplicable),
            Finding("CHK-004", CheckOutcome.NoIssue));

        Assert.Equal(1, report.Count(CheckOutcome.PotentialConcern));
        Assert.Equal(2, report.Count(CheckOutcome.NoIssue));
        Assert.Equal(1, report.Count(CheckOutcome.NotApplicable));
        Assert.Contains("1 potential concern(s)", report.Headline);
    }

    /// <summary>
    /// A reviewer opens the report to find what needs attention. Concerns get their groups
    /// and citations; cleared checks are listed but not expanded.
    /// </summary>
    [Fact]
    public void Format_DetailsConcernsAndFoldsAwayPasses()
    {
        var text = Report(
                Finding("CHK-001", CheckOutcome.PotentialConcern, "Completeness"),
                Finding("CHK-002", CheckOutcome.NoIssue, "Objectives"))
            .Format();

        Assert.Contains("FINDINGS REQUIRING ATTENTION", text);
        Assert.Contains("CHK-001", text);
        Assert.Contains("CHK-001-G1", text);
        Assert.Contains("Fact Find.md", text);
        Assert.Contains("a quote", text);

        Assert.Contains("CHECKS CLEARED", text);
        Assert.Contains("CHK-002", text);
        Assert.DoesNotContain("CHK-002-G1", text);
    }

    [Fact]
    public void Format_ReportsWhatRetrievalActuallyFound()
    {
        var text = Report(
                Finding("CHK-001", CheckOutcome.PotentialConcern),
                Finding("CHK-002", CheckOutcome.NoIssue))
            .Format();

        Assert.Contains("8 search(es)", text);
        Assert.Contains("40 passage(s)", text);
        Assert.Contains("6 canonical path(s) resolved", text);
        Assert.Contains("2 absent", text);
    }

    [Fact]
    public void TotalUsage_SumsEveryCheck()
    {
        var usage = Report(
                Finding("CHK-001", CheckOutcome.NoIssue),
                Finding("CHK-002", CheckOutcome.NoIssue))
            .TotalUsage;

        Assert.Equal(200, usage.InputTokens);
        Assert.Equal(40, usage.OutputTokens);
    }

    [Fact]
    public void Format_NamesTheModelTheAssertionsCameFrom()
    {
        var model = new CanonicalModelDocument(
            "CASE-1", 99, """{"modelVersion":"1.0"}""", "gpt-4.1", "1.0",
            ["Suitability Report Test 1.md"], DateTimeOffset.Now, TokenUsage.Empty);

        var text = new FindingsReport(
                "CASE-1", 99, "gpt-4.1", DateTimeOffset.Now,
                [Finding("CHK-001", CheckOutcome.NoIssue)], model)
            .Format();

        Assert.Contains("Suitability Report Test 1.md", text);
        Assert.Contains("schema v1.0", text);
    }
}
