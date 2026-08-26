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
            [Passage("Current Monthly Cash Flow\nTotal Monthly Disposable Income | £-288.00")],
            ["Total Monthly Disposable Income"]).All;

        Assert.Empty(unmatched);
    }

    /// <summary>The observed case: a hint naming a heading no converted document carries.</summary>
    [Fact]
    public void AHintThatMatchesNothingIsReported()
    {
        var unmatched = CheckPlanRunner.UnmatchedSections(
            [Passage("Current Monthly Cash Flow\nTotal Net Monthly Income | £1,430.00")],
            [Passage("Current Monthly Cash Flow\nTotal Net Monthly Income | £1,430.00")],
            ["Residency", "Contact Address"]).All;

        Assert.Equal(["Residency", "Contact Address"], unmatched);
    }

    [Fact]
    public void OnlyTheHintsThatMatchedNothingAreReported()
    {
        var unmatched = CheckPlanRunner.UnmatchedSections(
            [Passage("Current Monthly Cash Flow | Total Net Monthly Income | £1,430.00")],
            [Passage("Current Monthly Cash Flow | Total Net Monthly Income | £1,430.00")],
            ["Total Net Monthly Income", "Residency"]).All;

        Assert.Equal(["Residency"], unmatched);
    }

    [Fact]
    public void MatchingIgnoresCaseBecauseTheHintIsAPhraseNotAQuery()
    {
        var pack = new[] { Passage("RESIDENCY STATUS: Tenant - private") };

        Assert.Empty(CheckPlanRunner.UnmatchedSections(pack, pack, ["Residency"]).All);
    }

    [Fact]
    public void APlanThatNamedNoSectionReportsNothing()
    {
        var pack = new[] { Passage("anything") };

        Assert.Empty(CheckPlanRunner.UnmatchedSections(pack, pack, []).All);
        Assert.Empty(CheckPlanRunner.UnmatchedSections(pack, pack, ["  "]).All);
    }

    /// <summary>
    /// <b>This test used to assert the opposite, and the reasoning in its own comment was the
    /// defect.</b> It argued that a hint which matched a candidate and then lost its slot should
    /// not be reported, because reporting it would send a reader to check wording that was
    /// already correct. True as far as it went, and it left the reader with no report at all —
    /// the run said the hint matched, and the passage reached no assessor.
    ///
    /// That signal was then believed twice. Two analyses concluded from "section hints matching
    /// nothing: 4" that a retrieval fix had landed and the remaining failure was one of
    /// reasoning; the fact find's residency row had in fact reached no pack at all, and the
    /// guards written for it ran against evidence that was never there.
    ///
    /// Both cases are now reported, separately, because they need opposite answers: reword the
    /// hint, or widen the pack.
    /// </summary>
    [Fact]
    public void AHintWhosePassageWasEvictedIsReportedAsEvictedRatherThanMatched()
    {
        var carrier = Passage("Residency Status: Tenant - private");

        var candidates = Enumerable
            .Range(0, 40)
            .Select(i => Passage($"unrelated section {i}"))
            .Append(carrier)
            .ToList();

        // The pack the assessor actually got: everything except the passage that carried it.
        var pack = candidates.Where(p => p != carrier).ToList();

        var reach = CheckPlanRunner.UnmatchedSections(candidates, pack, ["Residency"]);

        Assert.Empty(reach.MatchedNothing);
        Assert.Equal(["Residency"], reach.Evicted);
    }

    /// <summary>
    /// And the distinction holds in the other direction: a hint nothing carries is a wording
    /// problem, and must not be reported as an eviction, or the reader widens a pack that was
    /// never going to contain it.
    /// </summary>
    [Fact]
    public void AHintNoPassageCarriesIsReportedAsMatchingNothing()
    {
        var candidates = new[] { Passage("Total Net Monthly Income | £1,430.00") };

        var reach = CheckPlanRunner.UnmatchedSections(candidates, candidates, ["Residency"]);

        Assert.Equal(["Residency"], reach.MatchedNothing);
        Assert.Empty(reach.Evicted);
    }

    /// <summary>Both failures at once, each in its own bucket.</summary>
    [Fact]
    public void TheTwoFailuresAreReportedSeparately()
    {
        var carrier = Passage("Residency Status: Tenant - private");
        var candidates = new[] { Passage("Total Net Monthly Income"), carrier };
        var pack = new[] { candidates[0] };

        var reach = CheckPlanRunner.UnmatchedSections(
            candidates, pack, ["Residency", "Contact Address"]);

        Assert.Equal(["Contact Address"], reach.MatchedNothing);
        Assert.Equal(["Residency"], reach.Evicted);
        Assert.Equal(2, reach.All.Count);
    }
}
