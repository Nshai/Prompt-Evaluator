using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The benchmarks have to be readable by something other than a person.
///
/// <b>Four cases have hand-adjudicated benchmarks and one has ever been scored</b>, because
/// scoring meant reading a 65,000-line log against a 42,000-character document. Every argument
/// about this pipeline rests on that single scored case, and the arguments are only as good as a
/// comparison nobody can afford to repeat.
///
/// The parser reads what the benchmarks already contain — no new authoring, no second copy to
/// fall out of step — and the scorer settles the one question that needs no judgement: how many
/// expected findings sit in checks that never ran.
/// </summary>
public class BenchmarkScoreTests
{
    private readonly ITestOutputHelper _output;

    public BenchmarkScoreTests(ITestOutputHelper output) => _output = output;

    // ── parsing ───────────────────────────────────────────────────────────────

    [Fact]
    public void AFindingRowIsReadWithItsCheckTakenFromItsId()
    {
        var expectation = Assert.Single(BenchmarkScore.Parse(
            "| ID | Finding | Severity |\n"
            + "| --- | --- | --- |\n"
            + "| F7.3 | The comparison applies a charge the client does not pay | High |\n"));

        Assert.Equal("F7.3", expectation.Id);
        Assert.Equal("CHK-007", expectation.CheckId);
        Assert.Equal("High", expectation.Severity);
    }

    /// <summary>Two-digit checks keep their padding, so the id matches what a run emits.</summary>
    [Fact]
    public void ATwoDigitCheckIsPaddedTheSameWayTheRunPadsIt()
    {
        Assert.Equal("CHK-010", Assert.Single(BenchmarkScore.Parse(Row("F10.2"))).CheckId);
    }

    /// <summary>
    /// A benchmark lists each finding in its check's table and again in a scoring block, so ids
    /// repeat by design. The first carries the evidence.
    /// </summary>
    [Fact]
    public void ARepeatedIdIsCountedOnce()
    {
        var parsed = BenchmarkScore.Parse(
            Row("F1.1", "The full statement of the finding")
            + Row("F1.1", "F1.1  F1.2  F1.4"));

        Assert.Equal("The full statement of the finding", Assert.Single(parsed).Summary);
    }

    /// <summary>
    /// <b>A benchmark is a document that gets corrected.</b> One of these rows was written against
    /// a misreading — an asset shortfall confused with an income deficit — and struck through when
    /// a run produced the figures that disproved it. A scorer counting withdrawn rows marks a run
    /// down for not finding something nobody believes any more.
    /// </summary>
    [Fact]
    public void AWithdrawnRowIsNotExpectedOfAnybody()
    {
        Assert.Empty(BenchmarkScore.Parse(
            "| F3.3 | ~~The stated rationale is contradicted~~ — **WITHDRAWN 2026-08-28.** The "
            + "original entry conflated two things | ~~High~~ Withdrawn |\n"));
    }

    /// <summary>
    /// The benchmarks enumerate every requirement, not only the failures, and a pass is scored
    /// the opposite way round: a run that stays silent about it is right. Counting these as
    /// expectations marks a run down for agreeing with the benchmark.
    /// </summary>
    [Fact]
    public void ARequirementTheBenchmarkPassedIsNotExpectedOfAnybody()
    {
        Assert.Empty(BenchmarkScore.Parse(
            "| F6.11 | Who will implement the recommendation is stated | No issue |\n"));
    }

    [Fact]
    public void ProseAndOtherTablesAreIgnored()
    {
        Assert.Empty(BenchmarkScore.Parse(
            "Some prose about F7.3 and what it means.\n"
            + "| Provider | Charge | Value |\n"
            + "| Alpha | 0.41% | 57,120 |\n"));
    }

    // ── the worksheet ─────────────────────────────────────────────────────────

    /// <summary>
    /// The mechanical half of a score, and the half that mattered most: six findings were lost in
    /// one case to a check that settled Not Applicable before a single search ran. No reading of
    /// anything is needed to know those are missed.
    /// </summary>
    [Fact]
    public void ExpectationsInACheckThatDidNotRunAreMarkedUnreachable()
    {
        var rows = BenchmarkScore.Worksheet(
            BenchmarkScore.Parse(Row("F9.1") + Row("F9.2")),
            [new CheckFinding
            {
                CheckId = "CHK-009",
                CheckName = "Replacement or Switch Justification",
                Outcome = nameof(CheckOutcome.NotApplicable),
            }]);

        Assert.All(rows, r => Assert.False(r.CheckRan));
        Assert.Contains("2 of 2 sit in checks that did not run", BenchmarkScore.Format(rows));
    }

    /// <summary>A check absent from the run entirely counts the same way, and says so.</summary>
    [Fact]
    public void AnExpectationForACheckTheRunNeverAttemptedIsUnreachable()
    {
        var rows = BenchmarkScore.Worksheet(BenchmarkScore.Parse(Row("F9.1")), []);

        Assert.False(Assert.Single(rows).CheckRan);
        Assert.Equal("not in this run", rows[0].CheckOutcome);
    }

    /// <summary>
    /// Candidates are every group of the check that raised a concern, unfiltered. Narrowing them
    /// by keyword would hide the case worth knowing about — a run that reached the finding by a
    /// route the benchmark did not anticipate.
    /// </summary>
    [Fact]
    public void AGroupThatRaisedAConcernIsOfferedAsACandidate()
    {
        var rows = BenchmarkScore.Worksheet(
            BenchmarkScore.Parse(Row("F7.3")),
            [new CheckFinding
            {
                CheckId = "CHK-007",
                CheckName = "Cost",
                Outcome = nameof(CheckOutcome.PotentialConcern),
                Groups =
                [
                    new GroupFinding
                    {
                        GroupId = "G7.3",
                        Outcome = nameof(CheckOutcome.PotentialConcern),
                        Analysis = "The two tables sit on different bases.",
                    },
                    new GroupFinding
                    {
                        GroupId = "G7.8",
                        Outcome = nameof(CheckOutcome.NoIssue),
                        Analysis = "Nothing to report.",
                    },
                ],
            }]);

        var candidate = Assert.Single(Assert.Single(rows).Candidates);

        Assert.StartsWith("G7.3:", candidate);
    }

    [Fact]
    public void ACheckThatRanAndRaisedNothingSaysSo()
    {
        var rows = BenchmarkScore.Worksheet(
            BenchmarkScore.Parse(Row("F7.3")),
            [new CheckFinding { CheckId = "CHK-007", CheckName = "Cost", Outcome = nameof(CheckOutcome.NoIssue) }]);

        Assert.True(rows[0].CheckRan);
        Assert.Contains("No group of this check raised a concern", BenchmarkScore.Format(rows));
    }

    // ── the shipped benchmarks ────────────────────────────────────────────────

    /// <summary>
    /// Every benchmark in the repository parses, and parses to a plausible number of findings.
    ///
    /// This is the regression that matters: the parser reads documents written months before it,
    /// by hand, to a convention nobody was enforcing. If a future benchmark is written to a
    /// different shape, the failure should be here and not in a scoring session.
    /// </summary>
    [Fact]
    public void EveryBenchmarkInTheRepositoryParses()
    {
        var folder = TestRunsFolder();

        if (folder is null)
        {
            _output.WriteLine("Skipped: the Test-Runs folder is not in this working copy.");
            return;
        }

        var benchmarks = Directory.GetFiles(folder, "expected-results-benchmark.md", SearchOption.AllDirectories);

        if (benchmarks.Length == 0)
        {
            _output.WriteLine("Skipped: no benchmarks in this working copy.");
            return;
        }

        foreach (var path in benchmarks)
        {
            var parsed = BenchmarkScore.Parse(File.ReadAllText(path));
            var name = Path.GetFileName(Path.GetDirectoryName(path));

            _output.WriteLine($"{name}: {parsed.Count} finding(s)");

            Assert.True(
                parsed.Count >= 20,
                $"{name} parsed to only {parsed.Count} findings. These benchmarks hold dozens; "
                + "a number this low means the table convention has changed.");

            // Ids are what a scoring session refers to, so two rows sharing one is an ambiguity
            // in the benchmark itself rather than in the parser.
            Assert.Equal(parsed.Count, parsed.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

            // Every expectation must name a check that could exist. A benchmark id of F11.x
            // points at a check the catalogue does not have, which is a typo nobody would
            // otherwise see until the row scored as unreachable.
            Assert.All(parsed, p => Assert.InRange(int.Parse(p.CheckId[4..]), 1, 10));
        }
    }

    // ──────────────────────────────────────────────

    private static string Row(string id, string summary = "A finding the run should make") =>
        $"| {id} | {summary} | High |\n";

    private static string? TestRunsFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiPromptEvaluator.slnx")))
        {
            dir = dir.Parent;
        }

        var folder = dir is null ? null : Path.Combine(dir.FullName, "docs", "Test-Runs");

        return folder is not null && Directory.Exists(folder) ? folder : null;
    }
}
