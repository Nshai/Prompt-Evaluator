using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// A cumulative return restated as the compound annual equivalent.
///
/// <b>One calculation, two failures, four runs.</b>
///
/// A report quoting a multi-year cumulative return and an "average" beside it has usually divided
/// rather than compounded, and the error runs in the flattering direction. And a fund's past
/// return set against a deposit rate is gross against net unless somebody subtracts the charges.
///
/// Every run on record restated the report's own comparison of a fund's five-year average against
/// a cash rate — approvingly, on both models — and none of them annualised, compounded or
/// netted anything. The number they all needed is arithmetic on values the extraction already
/// records, so it is computed here and handed over settled.
/// </summary>
public class AnnualisedReturnTests
{
    private readonly ITestOutputHelper _output;

    public AnnualisedReturnTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The compound equivalent, and the simple mean named as the thing it is not. 23.61% over five
    /// years is 4.33% a year compounded; dividing gives 4.72%.
    /// </summary>
    [Fact]
    public void ACumulativeReturnIsRestatedAsItsCompoundEquivalent()
    {
        var figure = Assert.Single(Annualised(Model(23.61, "02/12/2020-02/12/2025")));

        Assert.Contains("4.33%", figure.Statement);
        Assert.Contains("4.72%", figure.Statement);
        Assert.Contains("overstates it", figure.Statement);
    }

    /// <summary>A period written as a count of years works as well as a date range.</summary>
    [Fact]
    public void APeriodStatedInYearsIsRead()
    {
        Assert.Single(Annualised(Model(23.61, "5 year cumulative")));
    }

    /// <summary>
    /// <b>A period that cannot be read produces nothing.</b> Annualising over a guessed number of
    /// years is a confident wrong number, which is the exact failure this class was built to stop
    /// — one run converted a unit contradiction into a derived figure and carried it forward.
    /// </summary>
    [Fact]
    public void APeriodThatCannotBeReadIsSkipped()
    {
        Assert.Empty(Annualised(Model(23.61, "since launch")));
    }

    /// <summary>
    /// Under two years compounding barely differs from dividing, and reporting it would be noise
    /// in a section a reader is told to treat as settled.
    /// </summary>
    [Fact]
    public void AShortPeriodIsNotWorthAnnualising()
    {
        Assert.Empty(Annualised(Model(4.0, "02/12/2024-02/12/2025")));
    }

    [Theory]
    [InlineData("02/12/2020-02/12/2025", 5.0)]
    [InlineData("2020-12-02 to 2025-12-02", 5.0)]
    [InlineData("5 years", 5.0)]
    [InlineData("3-year", 3.0)]
    [InlineData("since inception", null)]
    [InlineData("", null)]
    public void PeriodsAreReadOrRefused(string period, double? expected)
    {
        Assert.Equal(expected, DerivedFigures.YearsIn(period));
    }

    /// <summary>
    /// The stored model from a run that missed the comparison. Every performance figure it records
    /// should now arrive annualised.
    /// </summary>
    [Fact]
    public void TheStoredModelYieldsAnnualisedFigures()
    {
        if (StoredModel() is not { } json)
        {
            _output.WriteLine("Skipped: the archived canonical model is not in this working copy.");
            return;
        }

        var figures = DerivedFigures.From(json)
            .Where(f => f.Topic == "Annualised return")
            .ToList();

        foreach (var figure in figures)
        {
            _output.WriteLine(figure.ToString());
        }

        Assert.NotEmpty(figures);
    }

    // ──────────────────────────────────────────────

    private static IReadOnlyList<DerivedFigures.Figure> Annualised(string json) =>
        DerivedFigures.From(json).Where(f => f.Topic == "Annualised return").ToList();

    private static string Model(double cumulative, string period) =>
        ("{'existingArrangements':[{'performance':[{'instrumentName':'A strategy',"
        + "'role':'Existing','period':'" + period + "',"
        + "'cumulativeReturnPercentage':" + cumulative + "}]}]}").Replace('\'', '"');

    private static string? StoredModel()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiPromptEvaluator.slnx")))
        {
            dir = dir.Parent;
        }

        var run = dir is null
            ? null
            : Path.Combine(dir.FullName, "docs", "Test-Runs", "Test-Case 2", "Run-3");

        if (run is null || !Directory.Exists(run))
        {
            return null;
        }

        var model = Directory.GetFiles(run, "canonical-model_*.json").FirstOrDefault();

        return model is null ? null : File.ReadAllText(model);
    }
}
