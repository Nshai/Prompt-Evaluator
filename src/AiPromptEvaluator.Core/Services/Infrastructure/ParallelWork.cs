namespace AiPromptEvaluator;

/// <summary>
/// Runs indexed work concurrently under a bound.
///
/// It exists as its own type because two callers need it and they are on opposite sides of the
/// library boundary: <see cref="CheckPlanRunner"/> uses it to assess a check's requirement
/// groups in parallel, and a front end uses it to run several checks at once. It used to be a
/// private helper on the runner, which meant the desktop app reached into the runner's internals
/// to start a batch — the kind of coupling that only shows up when someone tries to write a
/// second front end.
///
/// Indexed rather than over a collection, because every caller writes results into a
/// pre-allocated array by position. That is what keeps output order identical to input order
/// however the work happens to interleave.
/// </summary>
public static class ParallelWork
{
    /// <summary>
    /// Runs <paramref name="body"/> for each index below <paramref name="count"/>, at most
    /// <paramref name="maxParallelism"/> at a time.
    /// </summary>
    public static Task ForEachAsync(
        int count,
        int maxParallelism,
        Func<int, CancellationToken, Task> body,
        CancellationToken cancellationToken) =>
        Parallel.ForEachAsync(
            Enumerable.Range(0, count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, maxParallelism),
                CancellationToken = cancellationToken,
            },
            async (index, token) => await body(index, token).ConfigureAwait(false));
}
