using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Arithmetic the checks turn on, done in code. Every case here is taken from the run scored
/// in docs/test-results/Runtime-Logs/latest, where the assessor either failed to do the sum or
/// did it and drew the wrong conclusion.
/// </summary>
public class DerivedFiguresTests
{
    /// <summary>
    /// The five arrangements of case ABC-99. The report quotes "approximately £110,000" and
    /// "£106,000" in two places; they actually sum to £116,998.47.
    ///
    /// **That figure is not the one the pipeline reported.** The extraction wrote £116,997.47
    /// into its own inconsistency note, and the assessment repeated it, because the sum was
    /// done in prose by a model rather than in code. It is out by a pound. The finding was
    /// right and the arithmetic behind it was not, which is the case for this class in one
    /// line — and it is why this test asserts the computed total rather than the reported one.
    /// </summary>
    [Fact]
    public void ExistingArrangementsAreSummed()
    {
        var figures = DerivedFigures.From(FiveArrangements);

        var total = Assert.Single(figures, f =>
            f.Topic == "Existing arrangements" && f.Statement.Contains("current value sum to"));

        Assert.Contains("£116,998.47", total.Statement);
        Assert.Contains("Zurich £103,439.24", total.Statement);
    }

    /// <summary>
    /// The total held and the total being moved are different figures, and quoting one as the
    /// other is what put "£110,185" and "£116,997.47" in the same report.
    /// </summary>
    [Fact]
    public void TheTotalHeldIsDistinguishedFromTheTotalBeingTransferred()
    {
        var figures = DerivedFigures.From(FiveArrangements);

        Assert.Contains(figures, f => f.Statement.Contains("transfer value sum to £110,185"));
        Assert.Contains(figures, f => f.Statement.Contains("different figures"));
    }

    /// <summary>
    /// F7.2, the finding the run missed by one division. The Standard Life row quotes £186.19
    /// at 0.18% — which implies a fund of £103,439, Zurich's value, not Standard Life's £3,002.
    /// The row was computed on the wrong plan.
    /// </summary>
    [Fact]
    public void AChargeComputedOnTheWrongFundIsNamed()
    {
        var figures = DerivedFigures.From(
            """
            {
              "existingArrangements": [
                { "provider": "Zurich", "currentValue": { "amount": 103439.24 } },
                { "provider": "Standard Life", "currentValue": { "amount": 3002.00 } }
              ],
              "costsAndCharges": {
                "existing": [{
                  "scope": "Standard Life",
                  "lines": [{
                    "description": "Standard Life annual recurring charge",
                    "percentage": { "value": 0.18 },
                    "amount": { "amount": 186.19 }
                  }]
                }]
              }
            }
            """);

        var charge = Assert.Single(figures, f => f.Topic == "Charge arithmetic");

        Assert.Contains("implies a fund value of £103,438.89", charge.Statement);
        Assert.Contains("which is Zurich's current value", charge.Statement);
    }

    [Fact]
    public void AChargeThatMatchesNoArrangementSaysSo()
    {
        var figures = DerivedFigures.From(
            """
            {
              "existingArrangements": [{ "provider": "Aviva", "currentValue": { "amount": 3744.36 } }],
              "costsAndCharges": {
                "recommended": {
                  "scope": "Aviva Platform",
                  "lines": [{ "chargeType": "Platform", "percentage": { "value": 0.21 }, "amount": { "amount": 231.39 } }]
                }
              }
            }
            """);

        var charge = Assert.Single(figures, f => f.Topic == "Charge arithmetic");

        Assert.Contains("matches no arrangement's current value", charge.Statement);
    }

    /// <summary>
    /// F1.2. The report reads a £230 state pension recorded weekly as though it were monthly.
    /// Stating both conversions beside the recorded frequency makes the difference arithmetic
    /// rather than inference.
    /// </summary>
    [Fact]
    public void IncomeIsRestatedAtEveryFrequency()
    {
        var figures = DerivedFigures.From(
            """
            {
              "financialPosition": {
                "income": [
                  { "description": "State Pension", "net": { "amount": 230 }, "frequency": "Weekly" },
                  { "description": "Kim employment", "net": { "amount": 1100 }, "frequency": "Monthly" }
                ]
              }
            }
            """);

        var weekly = Assert.Single(figures, f => f.Statement.StartsWith("State Pension"));

        Assert.Contains("£230.00 weekly is £996.67 monthly, £11,960.00 a year", weekly.Statement);

        var monthly = Assert.Single(figures, f => f.Statement.StartsWith("Kim employment"));

        Assert.Contains("£1,100.00 monthly is £1,100.00 monthly, £13,200.00 a year", monthly.Statement);
    }

    /// <summary>
    /// 52 weeks is 12 months. Converting weekly to monthly by multiplying by four loses a
    /// month a year, which on this case is roughly £1,000 of state pension.
    /// </summary>
    [Fact]
    public void WeeklyIsConvertedOnTheCalendarYearNotOnFourWeekMonths()
    {
        var figures = DerivedFigures.From(
            """
            {"financialPosition":{"income":[
              {"description":"Wages","net":{"amount":300},"frequency":"Weekly"}]}}
            """);

        var wages = Assert.Single(figures);

        Assert.Contains("£15,600.00 a year", wages.Statement);
        Assert.DoesNotContain("£1,200.00 monthly", wages.Statement);
    }

    /// <summary>
    /// F7.1. Zurich's ongoing charge appears at 0.18% in one table and 0.93% in another, and
    /// both comparisons drive the switch recommendation. Neither the assessor nor the
    /// extraction noticed, because seeing it means holding two pages together.
    /// </summary>
    [Fact]
    public void AnArrangementChargedAtTwoDifferentRatesIsNamed()
    {
        var figures = DerivedFigures.From(
            """
            {
              "existingArrangements": [
                { "provider": "Zurich", "currentValue": { "amount": 103439.24 },
                  "charges": { "totalOngoingPercentage": 0.18 } }
              ],
              "costsAndCharges": {
                "existing": [{ "scope": "Zurich", "lines": [{ "percentage": { "value": 0.93 } }] }]
              }
            }
            """);

        var clash = Assert.Single(figures, f => f.Topic == "Charge consistency");

        Assert.Contains("0.18% in the existing arrangements table", clash.Statement);
        Assert.Contains("0.93% in the charges comparison", clash.Statement);
        Assert.Contains("do not agree", clash.Statement);
    }

    /// <summary>
    /// The same figure recorded in both places is the normal case and must stay silent.
    /// </summary>
    [Fact]
    public void AnArrangementChargedConsistentlyIsNotReported()
    {
        var figures = DerivedFigures.From(
            """
            {
              "existingArrangements": [
                { "provider": "Zurich", "charges": { "totalOngoingPercentage": 0.93 } }
              ],
              "costsAndCharges": {
                "existing": [{ "scope": "Zurich", "lines": [{ "percentage": { "value": 0.93 } }] }]
              }
            }
            """);

        Assert.DoesNotContain(figures, f => f.Topic == "Charge consistency");
    }

    // ──────────────────────────────────────────────
    // It has to be safe on models that do not carry these values
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"existingArrangements":[]}""")]
    [InlineData("""{"existingArrangements":[{"provider":"Aviva"}]}""")]
    [InlineData("""{"costsAndCharges":{"existing":[{"lines":[{"percentage":{"value":0}}]}]}}""")]
    [InlineData("""{"financialPosition":{"income":[{"net":{"amount":100},"frequency":"OneOff"}]}}""")]
    [InlineData("not json at all")]
    public void AModelWithNothingToComputeProducesNothing(string json)
    {
        Assert.Empty(DerivedFigures.From(json));
    }

    /// <summary>
    /// A single arrangement is not a sum, and reporting "the 1 arrangement sums to £3,002"
    /// would be noise that trains a reader to skip the block.
    /// </summary>
    [Fact]
    public void OneArrangementIsNotSummed()
    {
        var figures = DerivedFigures.From(
            """{"existingArrangements":[{"provider":"Aviva","currentValue":{"amount":3744.36}}]}""");

        Assert.DoesNotContain(figures, f => f.Statement.Contains("sum to"));
    }

    [Fact]
    public void TheBlockNamesItselfAsComputedRatherThanAsserted()
    {
        var text = DerivedFigures.Format(DerivedFigures.From(FiveArrangements));

        Assert.Contains("computed from the canonical model", text);
        Assert.Contains("not asserted by the report", text);
        Assert.Contains("£116,998.47", text);
    }

    [Fact]
    public void NoFiguresMeansNoBlockAtAll()
    {
        Assert.Equal(string.Empty, DerivedFigures.Format(DerivedFigures.From("{}")));
    }

    // The five arrangements of case ABC-99, at the values the report tabulates. Three carry a
    // transfer value because three are being switched.
    private const string FiveArrangements =
        """
        {
          "existingArrangements": [
            { "provider": "People's Pension",  "currentValue": { "amount": 6601.55 } },
            { "provider": "Scottish Widows",   "currentValue": { "amount": 211.32 } },
            { "provider": "Aviva",             "currentValue": { "amount": 3744.36 },   "transferValue": { "amount": 3744.36 } },
            { "provider": "Zurich",            "currentValue": { "amount": 103439.24 }, "transferValue": { "amount": 103439.24 } },
            { "provider": "Standard Life",     "currentValue": { "amount": 3002.00 },   "transferValue": { "amount": 3002.00 } }
          ]
        }
        """;
}
