using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Phase 0 of the pipeline plan: <b>fix the silent group drop first.</b>
///
/// Six requirements across three runs of one case were assessed and then appeared nowhere in the
/// output — a full retrieval pack, a prompt, and no line anywhere. One of them was "tax
/// consequences of the recommendation", silently absent from a compliance report.
///
/// It is listed first not because it is the largest loss but because everything downstream is
/// computed over what survived it: a check's outcome is derived from its groups, the benchmark is
/// scored against the output, and a requirement that vanished scores as a requirement that found
/// nothing. <b>It silently corrupts every measurement taken over it</b>, including the ones that
/// decide whether the rest of the plan was worth building.
/// </summary>
public class SilentGroupDropTests
{
    private static PlanQueryGroup Group(string id) =>
        new() { GroupId = id, Requirement = "Tax consequences of the recommendation" };

    /// <summary>
    /// The requirement keeps its identity. A dropped group used to be indistinguishable from a
    /// group that never existed; this one names itself in the report.
    /// </summary>
    [Fact]
    public void AGroupWithNoFindingStillReachesTheReport()
    {
        var finding = CheckPlanRunner.NotAssessed(Group("G8.3"));

        Assert.Equal("G8.3", finding.GroupId);
        Assert.Equal("Tax consequences of the recommendation", finding.Requirement);
    }

    /// <summary>
    /// <b>Error, not Indeterminate.</b> Indeterminate is a real assessment outcome — the assessor
    /// read the pack and could not close the comparison — and a requirement nothing assessed has
    /// not earned it. Error is also the only outcome that cannot be mistaken for a judgement about
    /// the advice.
    /// </summary>
    [Fact]
    public void ItIsAnErrorRatherThanAVerdict()
    {
        var finding = CheckPlanRunner.NotAssessed(Group("G8.3"));

        Assert.Equal(CheckOutcome.Error, finding.ParsedOutcome);
        Assert.False(finding.ComparisonPerformed);
        Assert.NotEmpty(finding.MissingInputs);
    }

    /// <summary>
    /// And it must carry: a check cannot report No Issue on a set of requirements one of which
    /// never ran. This is the property that stops a dropped group scoring as a clean one.
    /// </summary>
    [Fact]
    public void ACheckCannotPassWithARequirementThatNeverRan()
    {
        var check = CheckFinding.FromGroups(
            "CHK-008",
            "Tax",
            [
                new GroupFinding { GroupId = "G8.1", Outcome = nameof(CheckOutcome.NoIssue) },
                CheckPlanRunner.NotAssessed(Group("G8.3")),
            ]);

        Assert.Equal(CheckOutcome.Error, check.ParsedOutcome);
        Assert.Equal(2, check.Groups.Count);
    }

    /// <summary>
    /// The words matter as much as the outcome. A reviewer reading this must not take it as a
    /// criticism of the advice — an extraction or run defect reported as a shortcoming of the
    /// report spends a finding somebody has to read and then discard.
    /// </summary>
    [Fact]
    public void ItSaysItIsADefectInTheRunRatherThanInTheAdvice()
    {
        var analysis = CheckPlanRunner.NotAssessed(Group("G8.3")).Analysis;

        Assert.Contains("defect in the run", analysis);
        Assert.Contains("Re-run", analysis);
    }
}
