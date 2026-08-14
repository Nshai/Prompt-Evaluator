namespace AiPromptEvaluator;

/// <summary>
/// A budget of concurrent calls, shared across everything running in one assessment.
///
/// Parallelism now happens at two levels — the checks of a run, and the requirements within a
/// check — and bounding each level separately multiplies rather than adds. Ten checks four at a
/// time, each assessing four requirements at a time, is sixteen requests in flight from settings
/// that both read "4". A provider's rate limit is a limit on the total, so the budget has to be
/// held on the total.
///
/// So the levels are left free to fan out and the gate is what actually bounds the work. That
/// also means a check with three requirements no longer leaves the budget idle: another check's
/// requirements take the free slots.
/// </summary>
public sealed class ConcurrencyGate : IDisposable
{
    private readonly SemaphoreSlim _slots;

    public ConcurrencyGate(int maxConcurrent)
    {
        Capacity = Math.Max(1, maxConcurrent);
        _slots = new SemaphoreSlim(Capacity, Capacity);
    }

    public int Capacity { get; }

    /// <summary>Runs <paramref name="work"/> once a slot is free, and gives the slot back after.</summary>
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await work(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _slots.Release();
        }
    }

    public void Dispose() => _slots.Dispose();
}
