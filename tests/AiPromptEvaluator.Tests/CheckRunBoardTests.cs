using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The board is what the response window shows while a run is in progress.
///
/// Checks run several at a time now, which made an append-as-they-finish log actively
/// misleading: lines arrived in completion order, nothing was said about the checks in flight,
/// and a run of ten looked like a run of however many had happened to finish. A fixed list that
/// updates in place says what is happening rather than what has happened.
/// </summary>
public class CheckRunBoardTests
{
    private static AssessmentCheck Check(string id, string name) =>
        new(id, name, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    private static CheckRunBoard Board(params AssessmentCheck[] checks) => new(checks);

    [Fact]
    public void EveryCheckStartsQueued()
    {
        var a = Check("CHK-001", "First");
        var board = Board(a, Check("CHK-002", "Second"));

        Assert.Equal(CheckRunState.Queued, board.Row(a).State);
        Assert.Equal("·", board.Row(a).Glyph);
        Assert.Equal(2, board.Count(CheckRunState.Queued));
        Assert.False(board.IsComplete);
    }

    /// <summary>
    /// The list keeps the order the checks were given, not the order they finish. That is the
    /// whole point of a board over a log.
    /// </summary>
    [Fact]
    public void RenderKeepsTheGivenOrderHoweverChecksFinish()
    {
        var a = Check("CHK-001", "First");
        var b = Check("CHK-002", "Second");
        var c = Check("CHK-003", "Third");
        var board = Board(a, b, c);

        board.Start(c);
        board.Finish(b, Finding(CheckOutcome.NoIssue));

        var text = board.Render();

        Assert.True(
            text.IndexOf("CHK-001", StringComparison.Ordinal)
            < text.IndexOf("CHK-002", StringComparison.Ordinal));
        Assert.True(
            text.IndexOf("CHK-002", StringComparison.Ordinal)
            < text.IndexOf("CHK-003", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(CheckOutcome.NoIssue, "✓")]
    [InlineData(CheckOutcome.PotentialConcern, "!")]
    [InlineData(CheckOutcome.NotApplicable, "–")]
    [InlineData(CheckOutcome.Indeterminate, "?")]
    [InlineData(CheckOutcome.Error, "✕")]
    public void EachOutcomeGetsItsOwnGlyph(CheckOutcome outcome, string glyph)
    {
        var a = Check("CHK-001", "First");
        var board = Board(a);

        board.Finish(a, Finding(outcome));

        Assert.Equal(glyph, board.Row(a).Glyph);
    }

    /// <summary>An error is not a concern and an unassessable check is not a pass.</summary>
    [Fact]
    public void GlyphsForFailureAndDoubtAreDistinctFromThePass()
    {
        var glyphs = new[]
        {
            CheckOutcome.NoIssue, CheckOutcome.PotentialConcern,
            CheckOutcome.Indeterminate, CheckOutcome.Error,
        }.Select(o =>
        {
            var check = Check("CHK-001", "First");
            var board = Board(check);
            board.Finish(check, Finding(o));
            return board.Row(check).Glyph;
        }).ToList();

        Assert.Equal(glyphs.Count, glyphs.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void RunningShowsTheStageWithoutRepeatingTheCheckId()
    {
        var a = Check("CHK-001", "First");
        var board = Board(a);

        board.Progress(a, "CHK-001: assessed 3/9");

        Assert.Equal(CheckRunState.Running, board.Row(a).State);
        Assert.Equal("assessed 3/9", board.Row(a).Detail);
    }

    [Fact]
    public void HeadlineCountsWhatIsDoneAndWhatIsRunning()
    {
        var a = Check("CHK-001", "First");
        var b = Check("CHK-002", "Second");
        var board = Board(a, b);

        board.Start(a);
        Assert.Contains("1 running", board.Headline);

        board.Finish(a, Finding(CheckOutcome.NoIssue));
        board.Finish(b, Finding(CheckOutcome.NoIssue));

        Assert.Equal("2/2 complete", board.Headline);
        Assert.True(board.IsComplete);
    }

    /// <summary>A skipped check counts as settled — the run is not waiting on it.</summary>
    [Fact]
    public void SkippedChecksDoNotHoldTheRunOpen()
    {
        var a = Check("CHK-001", "First");
        var board = Board(a);

        board.Skip(a, "No query plan");

        Assert.True(board.IsComplete);
        Assert.Equal("–", board.Row(a).Glyph);
        Assert.Contains("No query plan", board.Render(), StringComparison.Ordinal);
    }

    /// <summary>
    /// With several checks in flight, cancelling leaves several rows mid-way. Leaving them
    /// showing the running glyph would say the run was still going.
    /// </summary>
    [Fact]
    public void StopOutstanding_ClearsEverythingStillInFlight()
    {
        var a = Check("CHK-001", "First");
        var b = Check("CHK-002", "Second");
        var c = Check("CHK-003", "Third");
        var board = Board(a, b, c);

        board.Finish(a, Finding(CheckOutcome.NoIssue));
        board.Start(b);

        var stopped = board.StopOutstanding("Cancelled");

        Assert.Equal([b, c], stopped);
        Assert.Equal(CheckOutcome.NoIssue.ToString(), board.Row(a).Finding!.Outcome);
        Assert.Equal(CheckRunState.Skipped, board.Row(b).State);
        Assert.True(board.IsComplete);
    }

    [Fact]
    public void RenderCarriesTheLegendAndTheWaitingNotice()
    {
        var text = Board(Check("CHK-001", "First")).Render();

        Assert.Contains("queued", text, StringComparison.Ordinal);
        Assert.Contains("potential concern", text, StringComparison.Ordinal);
        Assert.Contains("once every check has finished", text, StringComparison.Ordinal);
    }

    /// <summary>Updates arrive from several threads at once; the board has to survive that.</summary>
    [Fact]
    public async Task BoardToleratesConcurrentUpdates()
    {
        var checks = Enumerable.Range(0, 20)
            .Select(i => Check($"CHK-{i:000}", $"Check {i}"))
            .ToArray();

        var board = new CheckRunBoard(checks);

        await Parallel.ForEachAsync(checks, async (check, _) =>
        {
            board.Start(check);
            board.Progress(check, "assessing");
            await Task.Yield();
            board.Finish(check, Finding(CheckOutcome.NoIssue));
        });

        Assert.True(board.IsComplete);
        Assert.Equal(20, board.Count(CheckRunState.Finished));

        // Counted on the check rows only — the legend at the foot carries every glyph too.
        Assert.Equal(
            20,
            board.Render().Split('\n').Count(l => l.TrimStart().StartsWith("✓  CHK-", StringComparison.Ordinal)));
    }

    private static CheckFinding Finding(CheckOutcome outcome) => new()
    {
        CheckId = "CHK-001",
        CheckName = "First",
        Outcome = outcome.ToString(),
        Elapsed = TimeSpan.FromSeconds(3),
    };
}

/// <summary>
/// The run-wide request budget. Two levels of parallelism multiply if each is bounded on its
/// own — ten checks four at a time, each assessing four requirements at a time, is sixteen
/// requests from two settings that both read "4" — so the bound is held on the total instead.
/// </summary>
public class ConcurrencyGateTests
{
    [Fact]
    public async Task NeverExceedsItsCapacity()
    {
        using var gate = new ConcurrencyGate(3);
        var inFlight = 0;
        var peak = 0;
        var padlock = new object();

        await Task.WhenAll(Enumerable.Range(0, 40).Select(async _ =>
            await gate.RunAsync(async token =>
            {
                lock (padlock)
                {
                    peak = Math.Max(peak, ++inFlight);
                }

                await Task.Delay(5, token);

                lock (padlock)
                {
                    inFlight--;
                }

                return 0;
            }, CancellationToken.None)));

        Assert.InRange(peak, 1, 3);
    }

    /// <summary>A slot has to come back even when the work throws, or the run deadlocks.</summary>
    [Fact]
    public async Task ReleasesItsSlotWhenTheWorkFails()
    {
        using var gate = new ConcurrencyGate(1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.RunAsync<int>(_ => throw new InvalidOperationException("boom"), CancellationToken.None));

        // Would hang here if the slot had been lost.
        Assert.Equal(42, await gate.RunAsync(_ => Task.FromResult(42), CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ANonsenseCapacityBecomesOne(int configured) =>
        Assert.Equal(1, new ConcurrencyGate(configured).Capacity);

    [Fact]
    public async Task StopsWaitingWhenCancelled()
    {
        using var gate = new ConcurrencyGate(1);
        using var cancellation = new CancellationTokenSource();

        var held = gate.RunAsync(async _ =>
        {
            await Task.Delay(5_000, CancellationToken.None);
            return 0;
        }, CancellationToken.None);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gate.RunAsync(_ => Task.FromResult(1), cancellation.Token));

        Assert.False(held.IsCompleted);
    }
}
