using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Assessing each requirement separately turned ten model calls into sixty, and sequentially
/// that is a twenty-minute run for work with no ordering constraint in it. The calls now run
/// concurrently — which is only safe if concurrency cannot change what the run concludes.
///
/// These pin that. A good deal of effort has gone into making two runs of the same check agree
/// with each other, and "it depends which answer came back first" would undo all of it.
/// </summary>
public class ParallelAssessmentTests
{
    /// <summary>
    /// The invariant the whole design rests on: results are written by index, so the order they
    /// finish in cannot reach the report. Completion order here is deliberately the reverse of
    /// the input order.
    /// </summary>
    [Fact]
    public async Task ForEachAsync_KeepsResultsInIndexOrderHoweverTheyFinish()
    {
        const int count = 24;
        var results = new int[count];

        await ParallelWork.ForEachAsync(count, maxParallelism: 8, async (i, token) =>
        {
            // Later indices finish first.
            await Task.Delay((count - i) * 4, token);
            results[i] = i;
        }, CancellationToken.None);

        Assert.Equal(Enumerable.Range(0, count), results);
    }

    [Fact]
    public async Task ForEachAsync_RunsEveryIndexExactlyOnce()
    {
        const int count = 50;
        var counts = new int[count];

        await ParallelWork.ForEachAsync(count, maxParallelism: 8, (i, _) =>
        {
            Interlocked.Increment(ref counts[i]);
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.All(counts, c => Assert.Equal(1, c));
    }

    /// <summary>The ceiling is usually a provider rate limit, so it has to be a real ceiling.</summary>
    [Fact]
    public async Task ForEachAsync_NeverExceedsTheConfiguredParallelism()
    {
        var inFlight = 0;
        var peak = 0;
        var gate = new object();

        await ParallelWork.ForEachAsync(40, maxParallelism: 4, async (_, token) =>
        {
            lock (gate)
            {
                peak = Math.Max(peak, ++inFlight);
            }

            await Task.Delay(5, token);

            lock (gate)
            {
                inFlight--;
            }
        }, CancellationToken.None);

        Assert.InRange(peak, 1, 4);
    }

    /// <summary>A nonsense setting degrades to running one at a time rather than throwing.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task ForEachAsync_TreatsANonsenseCeilingAsSequential(int configured)
    {
        var order = new List<int>();

        await ParallelWork.ForEachAsync(6, configured, (i, _) =>
        {
            lock (order)
            {
                order.Add(i);
            }

            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal([0, 1, 2, 3, 4, 5], order);
    }

    /// <summary>Cancelling a long run stops it rather than waiting for every call to finish.</summary>
    [Fact]
    public async Task ForEachAsync_StopsWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var started = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ParallelWork.ForEachAsync(200, maxParallelism: 4, async (_, token) =>
            {
                if (Interlocked.Increment(ref started) == 4)
                {
                    await cancellation.CancelAsync();
                }

                await Task.Delay(20, token);
            }, cancellation.Token));

        Assert.True(started < 200, $"the run should have stopped early; it started {started} of 200.");
    }

    [Fact]
    public async Task ForEachAsync_DoesNothingForAnEmptyRun()
    {
        var ran = false;

        await ParallelWork.ForEachAsync(0, 4, (_, _) =>
        {
            ran = true;
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.False(ran);
    }

    /// <summary>The default is chosen against provider rate limits, not local cores.</summary>
    [Fact]
    public void DefaultParallelism_IsModest() =>
        Assert.InRange(new AppSettings().MaxParallelRequests, 2, 8);
}
