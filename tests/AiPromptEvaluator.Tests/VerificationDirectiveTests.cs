using System.IO;

using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The verification directives the plans now carry, and the line they must not cross.
///
/// <b>The distinction that makes them worth having.</b> Everything else under
/// <c>verification</c> is a string interpolated into a prompt, and the measured result of that is
/// one-sided: every judgement moved into code has held, and every judgement moved into a prompt
/// clause has been ignored by at least one model — a comparison-basis clause violated by both
/// models while they quoted both dates, three guards and a hint that never fired across eight runs.
/// <c>reconciliation</c> and <c>adjudication</c> are read by code and branched on. If they ever
/// reach the prompt as prose they have become the thing they replaced.
/// </summary>
public class VerificationDirectiveTests
{
    private readonly ITestOutputHelper _output;

    public VerificationDirectiveTests(ITestOutputHelper output) => _output = output;

    private static IReadOnlyList<CheckQueryPlan> Plans()
    {
        var (plans, failures) = CheckQueryPlanLoader.Load(
            Path.Combine(AppContext.BaseDirectory, "check-plan"));

        Assert.Empty(failures);

        return [.. plans.Values];
    }

    /// <summary>
    /// Every shipped group carries both blocks. A directive that is absent on some groups is worse
    /// than one absent everywhere: the reconciler's behaviour would then vary between requirements
    /// for a reason nobody chose.
    /// </summary>
    [Fact]
    public void EveryShippedGroupCarriesBothDirectives()
    {
        var groups = Plans().SelectMany(p => p.QueryGroups).ToList();

        Assert.Equal(88, groups.Count);

        var missing = groups
            .Where(g => g.Verification?.Reconciliation is null || g.Verification?.Adjudication is null)
            .Select(g => g.GroupId)
            .ToList();

        Assert.True(missing.Count == 0, $"Groups without directives: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// The values parse into the enums the code branches on, rather than into strings that happen
    /// to look right. A misspelled directive is silent: it takes the default and the requirement
    /// is reconciled differently from how its author intended.
    /// </summary>
    [Fact]
    public void TheDirectivesParseIntoTheVocabulariesCodeBranchesOn()
    {
        string[] detects = ["ValueDivergence", "SetDifference", "Absence", "RangeBreach", "None"];
        string[] modalities = ["CurrentOnly", "AnyModality"];
        string[] modes = ["Model", "Deterministic"];
        string[] severities = ["Low", "Moderate", "High", "Critical"];

        foreach (var group in Plans().SelectMany(p => p.QueryGroups))
        {
            Assert.Contains(group.Reconciliation.Detect, detects);
            Assert.Contains(group.Reconciliation.Modality, modalities);
            Assert.Contains(group.Adjudication.Mode, modes);

            if (group.Adjudication.SeverityCeiling is { } ceiling)
            {
                Assert.Contains(ceiling, severities);
            }
        }
    }

    /// <summary>
    /// A plan written before these fields existed keeps its behaviour exactly: compare nothing,
    /// cap nothing, ask the model. An old plan must not acquire a default somebody has to discover
    /// from a run.
    /// </summary>
    [Fact]
    public void APlanWithoutDirectivesReadsAsTheOldBehaviour()
    {
        var group = new PlanQueryGroup { GroupId = "G1.1" };

        Assert.True(group.Reconciliation.DetectsNothing);
        Assert.False(group.Reconciliation.CurrentOnly);
        Assert.False(group.Reconciliation.EmitOnePerPath);
        Assert.Null(group.Reconciliation.AsAtToleranceDays);
        Assert.False(group.Adjudication.IsDeterministic);
        Assert.Null(group.Adjudication.SeverityCeiling);
        Assert.False(group.Adjudication.RequiresCitation);
    }

    /// <summary>
    /// An Appropriateness limb is never Deterministic: code can establish the facts of a
    /// requirement but not the verdict on whether advice was suitable.
    /// </summary>
    [Fact]
    public void NoAppropriatenessRequirementIsSettledByCodeAlone()
    {
        var wrong = Plans()
            .SelectMany(p => p.QueryGroups)
            .Where(g => g.Adjudication.IsDeterministic
                        && g.Limb.Contains("Appropriateness", StringComparison.OrdinalIgnoreCase))
            .Select(g => g.GroupId)
            .ToList();

        Assert.True(wrong.Count == 0, $"Appropriateness settled without a model: {string.Join(", ", wrong)}");
    }

    /// <summary>
    /// <b>How many ceilings can actually bind.</b> The finding schema's severity enum is
    /// High/Moderate/Low — there is no Critical — so a ceiling of Critical or High cannot clamp
    /// anything a model is able to emit. Reported rather than asserted: the authors may have meant
    /// the ceiling as documentation of intent, and this is the number that says how much of it is
    /// currently load-bearing.
    /// </summary>
    [Fact]
    public void HowManySeverityCeilingsCanBind()
    {
        var groups = Plans().SelectMany(p => p.QueryGroups).ToList();

        var byCeiling = groups
            .GroupBy(g => g.Adjudication.SeverityCeiling ?? "(none)")
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var g in byCeiling)
        {
            _output.WriteLine($"{g.Key,-10} {g.Count(),3}");
        }

        // Only a ceiling below the top of the finding vocabulary can clamp anything.
        var binding = groups.Count(g =>
            g.Adjudication.SeverityCeiling is "Low" or "Moderate");

        _output.WriteLine($"\nCeilings that can bind a High/Moderate/Low finding: {binding} of {groups.Count}");

        Assert.True(binding <= groups.Count);
    }
}
