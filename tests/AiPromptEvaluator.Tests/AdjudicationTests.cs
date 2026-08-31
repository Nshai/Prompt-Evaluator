using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The <c>adjudication</c> directive, applied by code to a finding the model returned.
///
/// <b>Both rules here were tried as prompt clauses first and both were ignored.</b> Severity was
/// steered in prose and produced ten "contradictions" at High severity on one undisclosed charge
/// basis, tabulated ten times — the largest false positive in either measured run. Citation
/// discipline is in the standing rules and one model still returned 99 untraceable quotes. A
/// ceiling that code applies cannot be argued with.
/// </summary>
public class AdjudicationTests
{
    private static PlanQueryGroup Group(
        string? ceiling = null, bool requiresCitation = false, string mode = "Model") => new()
    {
        GroupId = "G7.2",
        Requirement = "All material costs disclosed",
        Verification = new PlanVerification
        {
            Adjudication = new PlanAdjudication
            {
                Mode = mode,
                SeverityCeiling = ceiling,
                RequiresCitation = requiresCitation,
            },
        },
    };

    private static GroupFinding Concern(string? severity, params FindingCitation[] citations) => new()
    {
        GroupId = "G7.2",
        Outcome = nameof(CheckOutcome.PotentialConcern),
        Severity = severity,
        Discrepancies = ["Report states £9,838.36; Client Agreement states £9,839.36"],
        Citations = [.. citations],
        ComparisonPerformed = true,
    };

    [Fact]
    public void ASeverityAboveTheCeilingIsLowered()
    {
        var (finding, applied) = Adjudication.Apply(Concern("High"), Group(ceiling: "Moderate"));

        Assert.Equal("Moderate", finding.Severity);
        Assert.Contains(applied, a => a.Contains("lowered to Moderate"));
    }

    /// <summary>Only downwards. A plan that could raise a severity could manufacture a finding.</summary>
    [Fact]
    public void ASeverityBelowTheCeilingIsUntouched()
    {
        var (finding, applied) = Adjudication.Apply(Concern("Low"), Group(ceiling: "High"));

        Assert.Equal("Low", finding.Severity);
        Assert.Empty(applied);
    }

    /// <summary>
    /// <b>82 of the 88 shipped ceilings cannot bind.</b> The finding schema's enum is
    /// High/Moderate/Low; the plans set Critical on 49 groups and High on 33. A ceiling above the
    /// vocabulary clamps nothing, and pretending otherwise would report a change that never
    /// happened.
    /// </summary>
    [Theory]
    [InlineData("Critical")]
    [InlineData("High")]
    public void ACeilingAtOrAboveTheTopOfTheVocabularyBindsNothing(string ceiling)
    {
        var (finding, applied) = Adjudication.Apply(Concern("High"), Group(ceiling));

        Assert.Equal("High", finding.Severity);
        Assert.Empty(applied);
    }

    [Fact]
    public void AGroupWithNoCeilingIsUntouched()
    {
        var (finding, applied) = Adjudication.Apply(Concern("High"), Group());

        Assert.Equal("High", finding.Severity);
        Assert.Empty(applied);
    }

    /// <summary>
    /// A concern with nothing to point at, on a requirement whose plan demands a locatable quote.
    /// </summary>
    [Fact]
    public void AnUncitedConcernBecomesIndeterminateWhereThePlanRequiresAQuote()
    {
        var (finding, applied) = Adjudication.Apply(
            Concern("High"), Group(requiresCitation: true));

        Assert.Equal(CheckOutcome.Indeterminate, finding.ParsedOutcome);
        Assert.Contains("a locatable quote for the discrepancy", finding.MissingInputs);
        Assert.Contains(applied, a => a.Contains("no citation"));
    }

    /// <summary>
    /// <b>Indeterminate, not No Issue.</b> "We could not evidence this" and "there is nothing here"
    /// are different answers, and collapsing them is how an absence becomes a clean pass — which is
    /// the absence-discipline failure the analysis records from the other direction.
    /// </summary>
    [Fact]
    public void TheDiscrepancyIsKeptRatherThanCleared()
    {
        var (finding, _) = Adjudication.Apply(Concern("High"), Group(requiresCitation: true));

        Assert.NotEqual(CheckOutcome.NoIssue, finding.ParsedOutcome);
        Assert.Single(finding.Discrepancies);
    }

    [Fact]
    public void ACitedConcernIsReportedAsItStands()
    {
        var cited = Concern("High", new FindingCitation { PassageId = "P4", Quote = "£9,839.36" });

        var (finding, applied) = Adjudication.Apply(cited, Group(requiresCitation: true));

        Assert.Equal(CheckOutcome.PotentialConcern, finding.ParsedOutcome);
        Assert.Empty(applied);
    }

    /// <summary>
    /// The rule bites on concerns only. A No Issue verdict carries no claim that needs locating,
    /// and rewriting one would be the directive inventing a finding rather than capping one.
    /// </summary>
    [Fact]
    public void ANoIssueVerdictIsNotTouchedByTheCitationRule()
    {
        var clear = new GroupFinding
        {
            GroupId = "G7.2",
            Outcome = nameof(CheckOutcome.NoIssue),
            ComparisonPerformed = true,
            Discrepancies = ["a difference that did not matter"],
        };

        var (finding, applied) = Adjudication.Apply(clear, Group(requiresCitation: true));

        Assert.Equal(CheckOutcome.NoIssue, finding.ParsedOutcome);
        Assert.Empty(applied);
    }

    /// <summary>Every shipped group survives its own directive without the app throwing.</summary>
    [Fact]
    public void EveryShippedGroupsDirectiveApplies()
    {
        var (plans, _) = CheckQueryPlanLoader.Load(
            System.IO.Path.Combine(AppContext.BaseDirectory, "check-plan"));

        foreach (var group in plans.Values.SelectMany(p => p.QueryGroups))
        {
            var (finding, _) = Adjudication.Apply(Concern("High"), group);

            Assert.NotNull(finding);
            Assert.Contains(finding.Severity, new[] { "Low", "Moderate", "High" });
        }
    }
}
