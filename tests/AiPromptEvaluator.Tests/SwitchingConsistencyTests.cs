using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// A switch that costs more every year and changes the outcome by nothing.
///
/// <b>This is the finding a prompt was asked for four times and never produced.</b> Four runs, two
/// models, and in every one the clause telling the assessor to ask what assumption produced an
/// identical result was printed in the responsible group's own instructions. One run quoted the
/// sentence that proves it — a supporting document recording that the existing plan bears no
/// adviser charge — and used it to reassure itself the ceding plan was clean.
///
/// The two halves were in the canonical model the whole time. A stated charge difference and a
/// stated reduction in yield are the same quantity written down twice; when the first says the
/// recommendation is dearer and the second says it is not, an assumption has been applied to one
/// side and not the other.
///
/// So it is arithmetic now, computed before any model call and handed over settled — which is what
/// <see cref="DerivedFigures"/> exists for.
/// </summary>
public class SwitchingConsistencyTests
{
    private readonly ITestOutputHelper _output;

    public SwitchingConsistencyTests(ITestOutputHelper output) => _output = output;

    // ── the defect ────────────────────────────────────────────────────────────

    /// <summary>
    /// Dearer by 0.08% a year, and an identical reduction in yield. Both cannot be true on one
    /// basis.
    /// </summary>
    [Fact]
    public void ADearerRecommendationWithAnIdenticalYieldIsReported()
    {
        var figures = DerivedFigures.From(Model(
            chargeGap: 0.08,
            yields: [("Low", 1.6, 1.6), ("Mid", 1.7, 1.7)]));

        var switching = figures.Where(f => f.Topic.StartsWith("Switching cost")).ToList();

        Assert.Equal(2, switching.Count);
        Assert.All(switching, f => Assert.Contains("cannot produce a reduction in yield", f.Statement));
        Assert.Contains(switching, f => f.Statement.Contains("identical to"));
    }

    /// <summary>
    /// Worse still: dearer, and a *lower* reduction in yield than the plan it replaces. The
    /// arithmetic cannot be reconciled at all without an assumption applied to one side.
    /// </summary>
    [Fact]
    public void ADearerRecommendationWithABetterYieldIsReported()
    {
        var figures = DerivedFigures.From(Model(chargeGap: 0.08, yields: [("High", 1.8, 1.7)]));

        var statement = Assert.Single(figures.Where(f => f.Topic.StartsWith("Switching cost"))).Statement;

        Assert.Contains("0.1% lower than", statement);
        Assert.Contains("applied to one side and not the other", statement);
    }

    /// <summary>
    /// The same test against the stated outcome. A recommendation that costs more and moves the
    /// projection by nothing is two statements on two bases.
    /// </summary>
    [Fact]
    public void ADearerRecommendationWithNoEffectOnTheOutcomeIsReported()
    {
        var figures = DerivedFigures.From(Model(
            chargeGap: 0.08,
            yields: [("Mid", 1.7, 1.7)],
            effectAmount: 0,
            effectPercentage: 0));

        Assert.Contains(
            figures,
            f => f.Statement.Contains("changes the projected outcome by nothing"));
    }

    // ── what must not be reported ─────────────────────────────────────────────

    /// <summary>
    /// A charge difference that shows up in the yield is the report doing its job. Reporting it
    /// back would be noise, and noise in a section framed as settled arithmetic is worse than
    /// silence.
    /// </summary>
    [Fact]
    public void ADearerRecommendationWhoseYieldReflectsItIsNotReported()
    {
        var figures = DerivedFigures.From(Model(chargeGap: 0.08, yields: [("Mid", 1.70, 1.78)]));

        Assert.DoesNotContain(figures, f => f.Topic.StartsWith("Switching cost"));
    }

    /// <summary>A cheaper recommendation is not this defect, whatever its yield does.</summary>
    [Fact]
    public void ACheaperRecommendationIsNotReported()
    {
        var figures = DerivedFigures.From(Model(chargeGap: -0.10, yields: [("Mid", 1.7, 1.7)]));

        Assert.DoesNotContain(figures, f => f.Topic.StartsWith("Switching cost"));
    }

    /// <summary>
    /// A rounding is not a charge difference. Below the threshold the comparison is not worth
    /// making and would fire on every case that states its charges to two decimal places.
    /// </summary>
    [Fact]
    public void ARoundingSizedChargeDifferenceIsNotReported()
    {
        var figures = DerivedFigures.From(Model(chargeGap: 0.005, yields: [("Mid", 1.7, 1.7)]));

        Assert.DoesNotContain(figures, f => f.Topic.StartsWith("Switching cost"));
    }

    /// <summary>
    /// Yields are paired by growth basis, never by position. The subjects are separate entries and
    /// nothing orders them; comparing a low-growth figure against a high-growth one manufactures a
    /// difference that is not in the document.
    /// </summary>
    [Fact]
    public void YieldsAreMatchedByGrowthBasisRatherThanByPosition()
    {
        // Consistent within each basis, inconsistent if the bases were crossed.
        var figures = DerivedFigures.From(Model(
            chargeGap: 0.08,
            yields: [("Low", 1.60, 1.68), ("High", 1.80, 1.88)]));

        Assert.DoesNotContain(figures, f => f.Topic.StartsWith("Switching cost"));
    }

    /// <summary>A model with no switch says nothing, rather than throwing.</summary>
    [Fact]
    public void AModelWithNoSwitchIsSilent()
    {
        Assert.DoesNotContain(
            DerivedFigures.From("""{"costsAndCharges":{}}"""),
            f => f.Topic.StartsWith("Switching cost"));
    }

    // ── the real case ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The regression that matters: the stored model from a run that missed this finding.</b>
    ///
    /// Run 3's canonical model records a like-for-like charge difference of +0.08% with direction
    /// RecommendedMoreExpensive, reductions in yield of 1.6/1.6, 1.7/1.7 and 1.8/1.7, and an effect
    /// of switching of £0 / 0.0% at every growth basis. The assessor was given all of it and
    /// reported the switch as adequately justified.
    ///
    /// If this test ever stops finding the contradiction, the derivation has stopped reading the
    /// shape the extraction actually produces — which is the failure that made this necessary.
    /// </summary>
    [Fact]
    public void TheStoredModelFromTheRunThatMissedItNowReportsIt()
    {
        if (StoredModel() is not { } json)
        {
            _output.WriteLine("Skipped: the archived canonical model is not in this working copy.");
            return;
        }

        var switching = DerivedFigures.From(json)
            .Where(f => f.Topic.StartsWith("Switching cost"))
            .ToList();

        foreach (var figure in switching)
        {
            _output.WriteLine(figure.ToString());
        }

        Assert.NotEmpty(switching);
        Assert.Contains(switching, f => f.Statement.Contains("0.08%"));
    }

    // ──────────────────────────────────────────────

    private static string? StoredModel()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiPromptEvaluator.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            return null;
        }

        var run = Path.Combine(dir.FullName, "docs", "Test-Runs", "Test-Case 2", "Run-3");

        if (!Directory.Exists(run))
        {
            return null;
        }

        var model = Directory.GetFiles(run, "canonical-model_*.json").FirstOrDefault();

        return model is null ? null : File.ReadAllText(model);
    }

    /// <summary>
    /// A switch stated the way the schema records one: a like-for-like charge comparison, and a
    /// reduction in yield per growth basis for each subject.
    /// </summary>
    private static string Model(
        double chargeGap,
        (string Basis, double Existing, double Recommended)[] yields,
        double? effectAmount = null,
        double? effectPercentage = null)
    {
        var effect =
            (effectAmount is null ? string.Empty : ",'effectOfSwitchingAmount':{'amount':" + effectAmount + "}")
            + (effectPercentage is null ? string.Empty : ",'effectOfSwitchingPercentage':{'value':" + effectPercentage + "}");

        var entries = yields.SelectMany(y => new[]
        {
            "{'subject':'Existing','growthRateBasis':'" + y.Basis
                + "','riyPercentage':{'value':" + y.Existing + "}}",
            "{'subject':'Recommended','growthRateBasis':'" + y.Basis
                + "','riyPercentage':{'value':" + y.Recommended + "}" + effect + "}",
        });

        var direction = chargeGap > 0 ? "RecommendedMoreExpensive" : "RecommendedCheaper";

        // Single quotes throughout and swapped at the end, so the fixture stays readable rather
        // than becoming a wall of escapes.
        return ("{'costsAndCharges':{"
            + "'comparison':{'basis':'LikeForLike','differencePercentage':{'value':" + chargeGap + "},"
            + "'direction':'" + direction + "'},"
            + "'reductionInYield':[" + string.Join(",", entries) + "]}}")
            .Replace('\'', '"');
    }
}
