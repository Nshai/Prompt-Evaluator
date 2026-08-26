using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Naming the consequence of a charge row computed on the wrong plan.
///
/// <b>Stating the arithmetic was not enough, and a measured run proved it.</b> All eighty-five
/// group prompts of 2026-08-26 already carried "EA5 existing annual charge: £186.19 at 0.18%
/// implies a fund value of £103,438.89, which is Zurich Managed Pn AP's current value" — EA5 being
/// Standard Life, whose own fund is £3,002. One check in ten drew the conclusion (F7.2, and only
/// on Sonnet). Two others read the same table at face value and reported the switch as a saving,
/// which is how F6.3 and F9.3 went uncaught by every model.
///
/// This was originally diagnosed as cross-check isolation — a fact established in CHK-007 failing
/// to reach CHK-006 and CHK-009. That diagnosis was wrong: the fact reached every check already.
/// The defect was that the sentence reported a coincidence and left its significance to the
/// reader, so it was used by whichever model happened to be sharp enough.
/// </summary>
public class WrongPlanChargeTests
{
    private readonly ITestOutputHelper _output;

    public WrongPlanChargeTests(ITestOutputHelper output) => _output = output;

    /// <summary>The case as it is: EA5's row computed on EA4's fund.</summary>
    private const string TransposedRow =
        """
        {
          "existingArrangements": [
            { "arrangementId": "EA4", "provider": "Zurich",
              "currentValue": { "amount": 103439.24, "currency": "GBP" } },
            { "arrangementId": "EA5", "provider": "Standard Life",
              "currentValue": { "amount": 3002.00, "currency": "GBP" } }
          ],
          "costsAndCharges": {
            "recommended": {
              "lines": [
                { "description": "EA5 existing annual charge",
                  "percentage": { "value": 0.18 }, "amount": { "amount": 186.19, "currency": "GBP" } }
              ]
            }
          }
        }
        """;

    [Fact]
    public void ARowComputedOnAnotherArrangementsFundSaysSo()
    {
        var figure = Single(TransposedRow);

        _output.WriteLine(figure);

        // The arithmetic, which was already there.
        Assert.Contains("£186.19 at 0.18% implies a fund value of £103,438", figure);
        Assert.Contains("Zurich's current value", figure);

        // The consequence, which was not.
        Assert.Contains("different arrangement from the EA5 this row is labelled for", figure);
        Assert.Contains("whose own current value is £3,002.00", figure);
        Assert.Contains("computed on the wrong plan", figure);
        Assert.Contains("unreliable", figure);
    }

    /// <summary>
    /// The narrow part. A row that lands on its own plan is correct, and saying anything more
    /// about it would be manufacturing a finding out of arithmetic that agrees.
    /// </summary>
    [Fact]
    public void ARowComputedOnItsOwnFundIsLeftAlone()
    {
        var json = TransposedRow
            .Replace("\"EA5 existing annual charge\"", "\"EA4 existing annual charge\"");

        var figure = Single(json);

        Assert.Contains("Zurich's current value", figure);
        Assert.DoesNotContain("wrong plan", figure);
    }

    /// <summary>
    /// A charge on the amount being transferred is legitimate and extremely common — the platform
    /// charge on this very case implies £110,185.71, the transfer total. Flagging it would bury
    /// the real finding in noise.
    /// </summary>
    [Fact]
    public void ARowThatMatchesATotalRatherThanAPlanIsLeftAlone()
    {
        var json =
            """
            {
              "existingArrangements": [
                { "arrangementId": "EA4", "provider": "Zurich",
                  "currentValue": { "amount": 100000.00, "currency": "GBP" } },
                { "arrangementId": "EA5", "provider": "Standard Life",
                  "currentValue": { "amount": 10000.00, "currency": "GBP" } }
              ],
              "costsAndCharges": {
                "recommended": {
                  "lines": [
                    { "description": "EA5 platform charge",
                      "percentage": { "value": 1.0 }, "amount": { "amount": 1100.00, "currency": "GBP" } }
                  ]
                }
              }
            }
            """;

        var figure = Single(json);

        Assert.Contains("the total of all arrangements", figure);
        Assert.DoesNotContain("wrong plan", figure);
    }

    /// <summary>
    /// A label with no arrangement id cannot be attributed, and guessing at one would risk telling
    /// a reader a correct row is wrong — which is worse than silence, because it is actionable.
    /// </summary>
    [Fact]
    public void ARowWhoseLabelNamesNoArrangementIsLeftAlone()
    {
        var json = TransposedRow
            .Replace("\"EA5 existing annual charge\"", "\"existing annual charge\"");

        var figure = Single(json);

        Assert.Contains("Zurich's current value", figure);
        Assert.DoesNotContain("wrong plan", figure);
    }

    [Fact]
    public void ARowMatchingNothingKeepsItsOriginalWording()
    {
        var json = TransposedRow.Replace("186.19", "999.99");

        var figure = Single(json);

        Assert.Contains("matches no arrangement value and neither total", figure);
        Assert.DoesNotContain("wrong plan", figure);
    }

    // ──────────────────────────────────────────────

    private static string Single(string modelJson)
    {
        var charges = DerivedFigures.From(modelJson)
            .Where(f => f.Topic == "Charge arithmetic")
            .ToList();

        return Assert.Single(charges).Statement;
    }
}
