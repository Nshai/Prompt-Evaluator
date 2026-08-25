using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The arithmetic behind a ValueMatch or RangeMatch, which used to be left to the assessor
/// reading a dozen passages of prose. A figure present but unnoticed reads as an evidence gap;
/// a figure absent gets "confirmed" from a number that resembles it. Both are decidable
/// without a model.
/// </summary>
public class NumericComparisonTests
{
    // ── extraction ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("£2,450.00", 2450.00, NumericComparison.FigureKind.Money)]
    [InlineData("£110,000", 110000, NumericComparison.FigureKind.Money)]
    [InlineData("0.93%", 0.93, NumericComparison.FigureKind.Percentage)]
    [InlineData("age 67", 67, NumericComparison.FigureKind.Plain)]
    public void FiguresAreReadWithTheirKind(string text, decimal value, NumericComparison.FigureKind kind)
    {
        var figure = Assert.Single(NumericComparison.Extract(text));

        Assert.Equal(value, figure.Value);
        Assert.Equal(kind, figure.Kind);
    }

    /// <summary>
    /// A number written with a thousands separator is money in all but the symbol, and reports
    /// drop the symbol constantly once a column header carries it.
    /// </summary>
    [Fact]
    public void AThousandsSeparatorMakesAPlainNumberMoney()
    {
        var figure = Assert.Single(NumericComparison.Extract("116,997.47"));

        Assert.Equal(NumericComparison.FigureKind.Money, figure.Kind);
    }

    /// <summary>
    /// Policy numbers, NI numbers and dates are digit runs that are not quantities. A spurious
    /// match is worse than a miss: it would tell the assessor a figure was corroborated when
    /// nothing corroborated it.
    /// </summary>
    [Theory]
    [InlineData("Policy K1234567B")]
    [InlineData("Reference AB123456C")]
    [InlineData("dated 01/04/2024")]
    public void DigitRunsThatAreNotQuantitiesAreSkipped(string text) =>
        Assert.Empty(NumericComparison.Extract(text));

    [Fact]
    public void TheSameValueTwiceIsReadOnce()
    {
        var figures = NumericComparison.Extract("£1,000 and again £1,000 and 2%");

        Assert.Equal(2, figures.Count);
    }

    // ── agreement ─────────────────────────────────────────────────────────────

    [Fact]
    public void ValueMatchIsExact()
    {
        Assert.True(NumericComparison.Agree(2450m, 2450m, allowTolerance: false));
        Assert.False(NumericComparison.Agree(2450m, 2450.01m, allowTolerance: false));
    }

    [Fact]
    public void RangeMatchAllowsThePublishedTolerance()
    {
        Assert.True(NumericComparison.Agree(1000m, 1005m, allowTolerance: true));
        Assert.False(NumericComparison.Agree(1000m, 1200m, allowTolerance: true));
    }

    // ── comparison ────────────────────────────────────────────────────────────

    [Fact]
    public void AMatchedFigureNamesThePassageItCameFrom()
    {
        var result = NumericComparison.Format(
            "ValueMatch",
            ["{\"totalMonthlyIncomeNet\": \"£2,450.00\"}"],
            new Dictionary<string, string> { ["P1"] = "Total net monthly income £2,450.00" });

        Assert.Contains("corroborated by", result);
        Assert.Contains("[P1]", result);
    }

    /// <summary>
    /// The near miss is usually the finding. Five plans summing to £116,997.47 against a stated
    /// £110,000 is the discrepancy, and "no corroborating figure" would bury it.
    /// </summary>
    [Fact]
    public void ANearMissIsNamedWithItsDistanceRatherThanReportedAbsent()
    {
        var result = NumericComparison.Format(
            "ValueMatch",
            ["{\"transferValue\": \"£110,000.00\"}"],
            new Dictionary<string, string> { ["P4"] = "Combined transfer value of £116,997.47" });

        Assert.Contains("not matched", result);
        Assert.Contains("116,997.47", result);
        Assert.Contains("[P4]", result);
        Assert.Contains("6.4", result);
    }

    /// <summary>Like is only compared with like: a percentage never corroborates an amount.</summary>
    [Fact]
    public void APercentageDoesNotCorroborateAnAmount()
    {
        var result = NumericComparison.Format(
            "ValueMatch",
            ["{\"annualCost\": \"£950.00\"}"],
            new Dictionary<string, string> { ["P1"] = "An ongoing charge of 950%" });

        Assert.Contains("no comparable figure", result);
    }

    [Fact]
    public void TheBlockSaysTheArithmeticDoesNotDecideTheRequirement()
    {
        var result = NumericComparison.Format(
            "ValueMatch",
            ["{\"v\": \"£10.00\"}"],
            new Dictionary<string, string> { ["P1"] = "£10.00" });

        Assert.Contains("does not decide the requirement", result);
    }

    // ── scope ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ValueMatch", true)]
    [InlineData("RangeMatch", true)]
    [InlineData("NarrativeAlignment", false)]
    [InlineData("SetCoverage", false)]
    [InlineData("PresenceOnly", false)]
    [InlineData(null, false)]
    public void OnlyTheNumericMethodsAreHandled(string? method, bool handled) =>
        Assert.Equal(handled, NumericComparison.Handles(method));

    [Fact]
    public void AMethodThisDoesNotHandleProducesNoBlock() =>
        Assert.Null(NumericComparison.Format(
            "NarrativeAlignment",
            ["{\"v\": \"£10.00\"}"],
            new Dictionary<string, string> { ["P1"] = "£10.00" }));

    [Fact]
    public void NoAssertedFiguresProducesNoBlock() =>
        Assert.Null(NumericComparison.Format(
            "ValueMatch",
            ["{\"statedLevel\": \"Limited\"}"],
            new Dictionary<string, string> { ["P1"] = "£10.00" }));

    [Fact]
    public void NoPassagesProducesNoBlock() =>
        Assert.Null(NumericComparison.Format(
            "ValueMatch",
            ["{\"v\": \"£10.00\"}"],
            new Dictionary<string, string>()));
}
