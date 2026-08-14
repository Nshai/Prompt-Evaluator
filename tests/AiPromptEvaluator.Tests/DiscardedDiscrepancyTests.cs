using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// A group may decide a difference does not matter. It may not decide that invisibly.
///
/// Measured across four scored runs, 9 to 15 groups per run answered No Issue while their own
/// <c>discrepancies</c> array named between 27 and 51 differences — and the report prints only
/// groups that raised a concern, so all of it went with them. One was a benchmark finding: a
/// group wrote "file notes state 'Risk rating of 6' … contradicting all other sources" and
/// returned No Issue, and the string never reached the report.
///
/// That made two different failures indistinguishable in the only measure the project has. A
/// finding never found and a finding found and dropped read identically in the printed output.
/// </summary>
public class DiscardedDiscrepancyTests
{
    private static GroupFinding Group(string id, string outcome, params string[] discrepancies) => new()
    {
        GroupId = id,
        Requirement = $"Requirement {id}",
        Outcome = outcome,
        ComparisonPerformed = true,
        Discrepancies = [.. discrepancies],
    };

    private static FindingsReport Report(params CheckFinding[] findings) =>
        new("ABC-99", 99, "test-model", DateTimeOffset.UnixEpoch, findings, null);

    private static CheckFinding Check(string id, params GroupFinding[] groups) =>
        CheckFinding.FromGroups(id, $"Check {id}", groups);

    [Fact]
    public void APassingGroupThatNamedNothingDiscardsNothing()
    {
        Assert.Empty(Group("G1.1", "NoIssue").DiscardedDiscrepancies);
    }

    [Fact]
    public void AConcernCarriesItsDiscrepanciesRatherThanDiscardingThem()
    {
        var group = Group("G1.1", "PotentialConcern", "Report says 5, file says 6");

        Assert.Empty(group.DiscardedDiscrepancies);
    }

    [Theory]
    [InlineData("NoIssue")]
    [InlineData("NotApplicable")]
    public void APassingGroupThatNamedADifferenceDiscardsIt(string outcome)
    {
        var group = Group("G3.6", outcome, "file notes state 'Risk rating of 6'");

        Assert.Equal(["file notes state 'Risk rating of 6'"], group.DiscardedDiscrepancies);
    }

    /// <summary>
    /// The observed case, end to end: a check that reports a concern overall, containing one
    /// group that passed while naming the contradiction. Before this, the group was filtered
    /// out of the rendered check and the sentence below appeared nowhere in the run.
    /// </summary>
    [Fact]
    public void TheDiscardedDifferenceReachesTheReport()
    {
        var report = Report(Check(
            "CHK-003",
            Group("G3.2", "PotentialConcern", "Capacity for loss is not quantified"),
            Group("G3.6", "NoIssue", "P11 file notes state 'Risk rating of 6', contradicting all other sources")));

        var text = report.Format();

        Assert.Contains("Risk rating of 6", text, StringComparison.Ordinal);
        Assert.Contains("RAISED AND NOT CARRIED", text, StringComparison.Ordinal);
        Assert.Contains("[G3.6]", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The harder half. A check where *every* group passes is summarised rather than printed,
    /// so a difference raised inside it had two layers of folding to disappear behind.
    /// </summary>
    [Fact]
    public void ADifferenceInsideAWhollyClearedCheckAlsoReachesTheReport()
    {
        var report = Report(Check(
            "CHK-004",
            Group("G4.1", "NoIssue", "Report says Moderate; questionnaire says no understanding"),
            Group("G4.2", "NoIssue")));

        var text = report.Format();

        Assert.Contains("CHECKS CLEARED", text, StringComparison.Ordinal);
        Assert.Contains("questionnaire says no understanding", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Surfacing them must not promote them. Most are immaterial — a £0.02 variance a group was
    /// right to wave through — and turning every one into a concern would flood the report and
    /// destroy the check-level signal, which is why this is reporting and not a veto.
    /// </summary>
    [Fact]
    public void SurfacingADiscardedDifferenceDoesNotChangeTheVerdict()
    {
        var group = Group("G1.7", "NoIssue", "Transfer value £3,002.00 against £3,002.02");

        Assert.Equal(CheckOutcome.NoIssue, group.ParsedOutcome);
        Assert.Equal(CheckOutcome.NoIssue, Check("CHK-001", group).ParsedOutcome);
    }

    [Fact]
    public void TheRunCountsGroupsAndDifferences()
    {
        var report = Report(
            Check("CHK-001", Group("G1.1", "NoIssue", "a", "b"), Group("G1.2", "PotentialConcern", "c")),
            Check("CHK-003", Group("G3.6", "NotApplicable", "d")));

        Assert.Equal((2, 3), report.DiscardedDiscrepancies);
        Assert.Contains("2 group(s) named 3 difference(s)", report.Format(), StringComparison.Ordinal);
    }

    [Fact]
    public void ARunThatDiscardedNothingSaysNothing()
    {
        var report = Report(Check("CHK-001", Group("G1.1", "PotentialConcern", "a")));

        Assert.Equal((0, 0), report.DiscardedDiscrepancies);
        Assert.DoesNotContain("Raised and not carried", report.Format(), StringComparison.Ordinal);
    }
}
