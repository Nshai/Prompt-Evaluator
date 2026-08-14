using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// A section hint that matches nothing must say so.
///
/// One commit added six hints. Three named parts of the Fact Find's cash-flow table and moved
/// four benchmark findings from missed to caught; the figure they targeted went from 0 mentions
/// in the previous run's output to 39. The other three named <c>Residency</c> and
/// <c>Contact Address</c>, matched no passage anywhere, and moved nothing.
///
/// **The run could not tell those two outcomes apart.** "The hint worked and the group ignored
/// the section" is a reasoning problem; "the hint never matched" is a typo. Distinguishing them
/// took re-reading the logs after the fact, which is exactly the sort of thing a run should say
/// out loud.
/// </summary>
public class SectionHintTests
{
    private static CaseDocumentSearchMatch Passage(string text) =>
        new(text, "ABC-99", 99, "Fact Find (Test 1).md", "B", "Fact find", 0.5);

    [Fact]
    public void AHintThatMatchesAPassageIsNotReported()
    {
        var unmatched = CheckPlanRunner.UnmatchedSections(
            [Passage("Current Monthly Cash Flow\nTotal Monthly Disposable Income | £-288.00")],
            ["Total Monthly Disposable Income"]);

        Assert.Empty(unmatched);
    }

    /// <summary>The observed case: a hint naming a heading no converted document carries.</summary>
    [Fact]
    public void AHintThatMatchesNothingIsReported()
    {
        var unmatched = CheckPlanRunner.UnmatchedSections(
            [Passage("Current Monthly Cash Flow\nTotal Net Monthly Income | £1,430.00")],
            ["Residency", "Contact Address"]);

        Assert.Equal(["Residency", "Contact Address"], unmatched);
    }

    [Fact]
    public void OnlyTheHintsThatMatchedNothingAreReported()
    {
        var unmatched = CheckPlanRunner.UnmatchedSections(
            [Passage("Current Monthly Cash Flow | Total Net Monthly Income | £1,430.00")],
            ["Total Net Monthly Income", "Residency"]);

        Assert.Equal(["Residency"], unmatched);
    }

    [Fact]
    public void MatchingIgnoresCaseBecauseTheHintIsAPhraseNotAQuery()
    {
        Assert.Empty(CheckPlanRunner.UnmatchedSections(
            [Passage("RESIDENCY STATUS: Tenant - private")],
            ["Residency"]));
    }

    [Fact]
    public void APlanThatNamedNoSectionReportsNothing()
    {
        Assert.Empty(CheckPlanRunner.UnmatchedSections([Passage("anything")], []));
        Assert.Empty(CheckPlanRunner.UnmatchedSections([Passage("anything")], ["  "]));
    }

    /// <summary>
    /// Measured over the candidates, not the ranked pack. A hint that matched and then lost its
    /// slot to the passage cap is a different problem needing a different answer, and reporting
    /// it here would send the reader to check the wording of a hint that was spelled correctly.
    /// </summary>
    [Fact]
    public void AHintThatMatchedIsNotReportedEvenIfItsPassageWouldNotSurviveRanking()
    {
        var candidates = Enumerable
            .Range(0, 40)
            .Select(i => Passage($"unrelated section {i}"))
            .Append(Passage("Residency Status: Tenant - private"))
            .ToList();

        Assert.Empty(CheckPlanRunner.UnmatchedSections(candidates, ["Residency"]));
    }
}
