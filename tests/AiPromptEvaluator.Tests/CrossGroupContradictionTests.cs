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

        Assert.Equal("Client income (monthly)", found.Subject);
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

        Assert.Equal("Total pension value (unstated)", found.Subject);
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
        Assert.Contains("Client income", text);  // the recurrence is appended to it
    }

    [Fact]
    public void NothingToReportProducesNoSection()
    {
        Assert.Equal(string.Empty, CrossGroupContradictions.Format(CrossGroupContradictions.In([])));
    }


    // ──────────────────────────────────────────────
    // What Run 2 exposed
    // ──────────────────────────────────────────────

    /// <summary>
    /// The splitter used to break on every full stop, including the one inside £3,305.55, so
    /// fragments arrived beginning "55 is deducted from…" and were paired against whatever
    /// else shared a cue word. Of six pairs reported on a real run, none was genuine.
    /// </summary>
    [Fact]
    public void AFigureIsNotSplitAtItsDecimalPoint()
    {
        var a = CheckFinding.FromGroups("CHK-005", "Affordability",
            [Group("G5.1", reportSays: "The initial fee of £3,305.55 is deducted from the fund transfer value.")]);

        var b = CheckFinding.FromGroups("CHK-001", "Completeness",
            [Group("G1.3", reportSays: "Volunteer income of £20 per week is recorded on page 3.")]);

        var found = CrossGroupContradictions.In([a, b]);

        Assert.DoesNotContain(found, c => c.Left.Contains("55 is deducted") || c.Right.Contains("55 is deducted"));
        Assert.Empty(found);
    }

    /// <summary>
    /// A weekly income and a one-off fee are not in disagreement merely because both are money
    /// and both sentences say "income".
    /// </summary>
    [Fact]
    public void FiguresOnDifferentFootingsAreNotPaired()
    {
        var a = CheckFinding.FromGroups("CHK-001", "One",
            [Group("G1.3", reportSays: "Volunteer income of £20 per week.")]);

        var b = CheckFinding.FromGroups("CHK-005", "Two",
            [Group("G5.1", reportSays: "An initial fee of £3,305.55 is charged, not taken from income.")]);

        Assert.Empty(CrossGroupContradictions.In([a, b]));
    }

    /// <summary>
    /// Recurrence is read from the words around each figure, not from the sentence. F1.1's
    /// report sentence states a weekly amount and its monthly equivalent together, and the
    /// monthly half is what pairs against the fact find.
    /// </summary>
    [Fact]
    public void ASentenceCarryingTwoFootingsLabelsEachFigureSeparately()
    {
        var a = CheckFinding.FromGroups("CHK-001", "One",
            [Group("G1.4", reportSays:
                "Report asserts the client receives £300 per week as an HGV driver, a monthly income of £1,300.")]);

        var b = CheckFinding.FromGroups("CHK-006", "Two",
            [Group("G6.3", fileSays: "Fact Find records net basic monthly income of £1,200.")]);

        var found = Assert.Single(CrossGroupContradictions.In([a, b]));

        Assert.Equal("Client income (monthly)", found.Subject);
        Assert.Contains("£1,300", found.Left + found.Right);
        Assert.Contains("£1,200", found.Left + found.Right);
    }


    // ──────────────────────────────────────────────
    // What Run 3 exposed: the pass was coherent and chose the wrong pair
    // ──────────────────────────────────────────────

    /// <summary>
    /// The pair Run 3 had and passed over. G1.2 carried the fact find's £1,430 household total
    /// and G1.4 the report's £4,486.67, both under "Client income (monthly)" — and the widest-
    /// spread rule reported a different pair under the same key, because two larger figures
    /// happened to sit further apart.
    /// </summary>
    [Fact]
    public void TheReportAgainstTheFileOutranksTwoReadingsOfTheFile()
    {
        var a = CheckFinding.FromGroups("CHK-001", "Completeness",
            [Group("G1.2", fileSays: "Fact Find shows Total Net Monthly Income £1,430.00 for the household.")]);

        var b = CheckFinding.FromGroups("CHK-001", "Completeness",
            [Group("G1.4", reportSays: "Report derives total monthly income of £4,486.67.")]);

        // A second, larger pair that agrees with itself — the shape that used to win.
        var c = CheckFinding.FromGroups("CHK-005", "Resilience",
            [Group("G5.1", fileSays: "Planning shows monthly income of £3,484.")]);

        var found = CrossGroupContradictions.In([a, b, c]);
        var income = Assert.Single(found, f => f.Subject == "Client income (monthly)");

        Assert.Contains("£1,430.00", income.Left + income.Right);
        Assert.Contains("£4,486.67", income.Left + income.Right);
    }

    /// <summary>
    /// Disagreement is proportional. £127,000 against £128,000 is agreement expressed twice;
    /// £1,430 against £4,486.67 is a threefold difference, and the absolute spread ranks them
    /// the wrong way round.
    /// </summary>
    [Fact]
    public void ALargeAbsoluteGapDoesNotOutrankALargeProportionalOne()
    {
        var a = CheckFinding.FromGroups("CHK-009", "Switch",
            [Group("G9.1", reportSays: "Projected value at age 75 of £127,000.")]);

        var b = CheckFinding.FromGroups("CHK-007", "Costs",
            [Group("G7.3", fileSays: "The research shows a projected value of £128,000.")]);

        Assert.DoesNotContain(
            CrossGroupContradictions.In([a, b]),
            f => f.Subject.StartsWith("Projected value"));
    }

    /// <summary>
    /// Two figures within a rounding of each other are the same figure. Reporting them is how an
    /// addendum teaches a reviewer to skip it.
    /// </summary>
    [Fact]
    public void FiguresThatAgreeWithinARoundingAreNotReported()
    {
        var a = CheckFinding.FromGroups("CHK-001", "One",
            [Group("G1.4", reportSays: "Monthly income of £1,430.00 is stated.")]);

        var b = CheckFinding.FromGroups("CHK-005", "Two",
            [Group("G5.1", fileSays: "Fact find records monthly income of £1,432.00.")]);

        Assert.Empty(CrossGroupContradictions.In([a, b]));
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
