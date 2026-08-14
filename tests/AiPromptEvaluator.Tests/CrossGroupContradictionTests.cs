using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The contradictions no single group could see.
///
/// F1.1 of the benchmark is the case this was built for: the fact find's £1,200 net basic
/// monthly income was retrieved into a CHK-006 group's fileSays, and the report's claim of
/// £300 per week was quoted in two CHK-001 groups. Both halves were produced by the same run
/// and nothing ever held them together.
/// </summary>
public class CrossGroupContradictionTests
{
    [Fact]
    public void TheIncomeContradictionSplitAcrossTwoChecksIsFound()
    {
        var chk001 = CheckFinding.FromGroups("CHK-001", "Suitability completeness",
        [
            Group("G1.4", reportSays:
                "Report asserts the client receives £300 per week as an HGV driver, a monthly income of £1,300."),
        ]);

        var chk006 = CheckFinding.FromGroups("CHK-006", "Recommendation rationale",
        [
            Group("G6.3", fileSays:
                "Fact Find records net basic monthly income of £1,200 for the client."),
        ]);

        var found = Assert.Single(CrossGroupContradictions.In([chk001, chk006]));

        Assert.Equal("Client income", found.Subject);
        Assert.Contains("CHK-001/G1.4", found.Left + found.Right);
        Assert.Contains("CHK-006/G6.3", found.Left + found.Right);
    }

    /// <summary>
    /// Two groups agreeing is the normal case and must stay silent, or the addendum becomes
    /// something a reviewer learns to skip.
    /// </summary>
    [Fact]
    public void TwoGroupsQuotingTheSameFigureAreNotReported()
    {
        var a = CheckFinding.FromGroups("CHK-001", "One",
            [Group("G1.4", reportSays: "The client's monthly income is £1,200.")]);

        var b = CheckFinding.FromGroups("CHK-005", "Two",
            [Group("G5.1", fileSays: "Fact find records monthly income of £1,200.")]);

        Assert.Empty(CrossGroupContradictions.In([a, b]));
    }

    /// <summary>
    /// A disagreement inside one group is that group's own business — it has both halves and
    /// has already reported on them. This pass exists for the halves nothing holds together.
    /// </summary>
    [Fact]
    public void ADisagreementWithinASingleGroupIsLeftToThatGroup()
    {
        var check = CheckFinding.FromGroups("CHK-001", "One",
        [
            Group("G1.4",
                reportSays: "Report asserts monthly income of £1,300.",
                fileSays: "Fact find records monthly income of £1,200."),
        ]);

        Assert.Empty(CrossGroupContradictions.In([check]));
    }

    /// <summary>
    /// Numbers that are not money, and money that is not about a subject the checks turn on,
    /// are ignored. Pairing every figure against every other produces coincidences — a page
    /// number against a risk rating — and buries the real ones.
    /// </summary>
    [Fact]
    public void UnrelatedFiguresAreNotPaired()
    {
        var a = CheckFinding.FromGroups("CHK-003", "Risk",
            [Group("G3.1", reportSays: "The agreed risk rating is 5 on a scale of 1 to 10.")]);

        var b = CheckFinding.FromGroups("CHK-008", "Disadvantages",
            [Group("G8.1", reportSays: "Stated on page 11, the fund is rated 4 by the KIID.")]);

        Assert.Empty(CrossGroupContradictions.In([a, b]));
    }

    [Fact]
    public void TheTotalPensionValueContradictionIsFound()
    {
        var a = CheckFinding.FromGroups("CHK-001", "One",
            [Group("G1.8", reportSays: "The five plans sum to a total pension value of £116,998.47.")]);

        var b = CheckFinding.FromGroups("CHK-009", "Switch",
            [Group("G9.1", reportSays: "You hold pension arrangements with a combined value of £110,000.")]);

        var found = Assert.Single(CrossGroupContradictions.In([a, b]));

        Assert.Equal("Total pension value", found.Subject);
    }

    [Fact]
    public void TheAddendumExplainsWhatItIsAndIsNot()
    {
        var a = CheckFinding.FromGroups("CHK-001", "One",
            [Group("G1.4", reportSays: "Monthly income of £1,300 is stated.")]);

        var b = CheckFinding.FromGroups("CHK-005", "Two",
            [Group("G5.1", fileSays: "Fact find records monthly income of £1,200.")]);

        var text = CrossGroupContradictions.Format(CrossGroupContradictions.In([a, b]));

        Assert.Contains("FIGURES DESCRIBED DIFFERENTLY", text);
        Assert.Contains("They are not findings", text);
        Assert.Contains("Client income", text);
    }

    [Fact]
    public void NothingToReportProducesNoSection()
    {
        Assert.Equal(string.Empty, CrossGroupContradictions.Format(CrossGroupContradictions.In([])));
    }

    private static GroupFinding Group(string id, string reportSays = "", string fileSays = "") => new()
    {
        GroupId = id,
        Requirement = $"Requirement {id}",
        ReportSays = reportSays,
        FileSays = fileSays,
        Analysis = "Reasoning.",
        Outcome = nameof(CheckOutcome.PotentialConcern),
        ComparisonPerformed = true,
    };
}
