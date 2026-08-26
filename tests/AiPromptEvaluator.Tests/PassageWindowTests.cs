using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Keeping the part of a passage the plan asked for, when the whole will not fit.
///
/// <b>Hint matching and passage rendering disagreed about what "the passage" was.</b>
/// <c>UnmatchedSections</c> and <c>Rank</c>'s hint matching both test the whole
/// <c>SearchedText</c>; the prompt wrote its first 2,400 characters. So a hint could match, win
/// its reserved slot, put its passage in the pack — and have the row it named cut before any
/// model saw it, with every diagnostic reporting success.
///
/// That is how the fact find's "Residency Status | Tenant - private" reached no assessor across
/// four runs while three separate fixes were aimed at it: a rewritten query, a section hint, and
/// three guards. All three worked. None could survive the render. One run truncated 907 passages.
/// </summary>
public class PassageWindowTests
{
    private readonly ITestOutputHelper _output;

    public PassageWindowTests(ITestOutputHelper output) => _output = output;

    private const int Max = 100;

    /// <summary>The address table, in miniature: the wanted row sits past the leading window.</summary>
    private static string FactFindShaped() =>
        new string('a', 250)
        + "| Residency Status | Tenant - private |"
        + new string('b', 250);

    // ── the defect ────────────────────────────────────────────────────────────

    [Fact]
    public void TheDeclaredSectionSurvivesEvenWhenItSitsPastTheLeadingWindow()
    {
        var rendered = CheckPlanRunner.Window(FactFindShaped(), Max, ["Residency Status"]);

        _output.WriteLine(rendered);

        Assert.Contains("Residency Status", rendered);
        Assert.Contains("Tenant - private", rendered);
    }

    /// <summary>What used to happen, pinned so the regression is visible if it returns.</summary>
    [Fact]
    public void WithoutTheDeclarationTheSameRowIsStillLost()
    {
        var rendered = CheckPlanRunner.Window(FactFindShaped(), Max);

        Assert.DoesNotContain("Residency Status", rendered);
    }

    // ── it must not damage the common case ────────────────────────────────────

    [Fact]
    public void APassageThatFitsIsUntouched()
    {
        const string Short = "| Residency Status | Tenant - private |";

        Assert.Equal(Short, CheckPlanRunner.Window(Short, Max, ["Residency Status"]));
        Assert.Equal(Short, CheckPlanRunner.Window(Short, Max));
    }

    /// <summary>
    /// The top of a chunk is its heading, so a hint already inside the leading window changes
    /// nothing — moving the window would lose the heading to gain text it already had.
    /// </summary>
    [Fact]
    public void AHintInsideTheLeadingWindowKeepsTheLeadingWindow()
    {
        var text = "| Residency Status | Tenant |" + new string('x', 500);

        var rendered = CheckPlanRunner.Window(text, Max, ["Residency Status"]);

        Assert.StartsWith("| Residency Status |", rendered);
        Assert.DoesNotContain("earlier characters omitted", rendered);
    }

    [Fact]
    public void APassageCarryingNoDeclaredSectionKeepsTheLeadingWindow()
    {
        var text = new string('a', 500);

        Assert.Equal(
            CheckPlanRunner.Window(text, Max),
            CheckPlanRunner.Window(text, Max, ["Current Monthly Cash Flow"]));
    }

    [Fact]
    public void NoUsableHintIsTheOldBehaviour()
    {
        var text = new string('a', 500);
        var plain = CheckPlanRunner.Window(text, Max);

        Assert.Equal(plain, CheckPlanRunner.Window(text, Max, null));
        Assert.Equal(plain, CheckPlanRunner.Window(text, Max, []));
        Assert.Equal(plain, CheckPlanRunner.Window(text, Max, ["   "]));
    }

    // ── the window itself ─────────────────────────────────────────────────────

    /// <summary>
    /// Never longer than the cap. The point is to choose which part is shown, not to smuggle a
    /// bigger prompt past a limit the settings control.
    /// </summary>
    [Fact]
    public void TheWindowNeverExceedsTheCap()
    {
        var text = new string('a', 5000) + "Residency Status" + new string('b', 5000);

        var body = CheckPlanRunner.Window(text, Max, ["Residency Status"])
            .Split('\n')
            .Single(l => l.Contains("Residency Status", StringComparison.Ordinal));

        Assert.True(body.Length <= Max + 2, $"window body was {body.Length}");
    }

    /// <summary>
    /// Some of the window sits ahead of the hint, because the label introducing a row is
    /// usually the line above it.
    /// </summary>
    [Fact]
    public void SomeContextBeforeTheHintIsKept()
    {
        var text = new string('a', 200) + "HEADING-ABOVE| Residency Status |" + new string('b', 200);

        var rendered = CheckPlanRunner.Window(text, Max, ["Residency Status"]);

        Assert.Contains("HEADING-ABOVE", rendered);
    }

    /// <summary>A reader has to be able to tell that they are seeing the middle of something.</summary>
    [Fact]
    public void TheReaderIsToldTextWasOmittedOnBothSides()
    {
        var text = new string('a', 500) + "Residency Status" + new string('b', 500);

        var rendered = CheckPlanRunner.Window(text, Max, ["Residency Status"]);

        Assert.Contains("earlier characters omitted", rendered);
        Assert.Contains("more characters", rendered);
    }

    /// <summary>The first declared section wins, so the pack does not depend on hint order.</summary>
    [Fact]
    public void TheEarliestDeclaredSectionInThePassageDecidesTheWindow()
    {
        var text = new string('a', 200) + "FIRST-HINT" + new string('b', 400) + "SECOND-HINT"
                   + new string('c', 200);

        var one = CheckPlanRunner.Window(text, Max, ["FIRST-HINT", "SECOND-HINT"]);
        var two = CheckPlanRunner.Window(text, Max, ["SECOND-HINT", "FIRST-HINT"]);

        Assert.Equal(one, two);
        Assert.Contains("FIRST-HINT", one);
    }

    [Fact]
    public void MatchingIsCaseInsensitiveLikeEveryOtherHintComparison()
    {
        var text = new string('a', 300) + "RESIDENCY STATUS: Tenant" + new string('b', 300);

        Assert.Contains("RESIDENCY STATUS", CheckPlanRunner.Window(text, Max, ["residency status"]));
    }
}
