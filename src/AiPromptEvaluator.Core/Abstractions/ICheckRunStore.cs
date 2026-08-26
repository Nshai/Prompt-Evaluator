namespace AiPromptEvaluator;

/// <summary>
/// The archive of finished runs: what each check searched for, what came back, and what the
/// assessor said about it.
///
/// <b>Separate from the prompt log because they answer different questions.</b> The log is a
/// transcript, read start to finish by someone reconstructing a run; this is a record, queried
/// by case and check id by something building a report. Both exist for the same reason — a
/// finding is only as good as the evidence behind it — and neither replaces the other: the log
/// keeps the extraction passes and the skipped checks this does not, and this keeps the
/// retrieval that the log never saw, because a passage that was retrieved and then evicted in
/// ranking never appears in any prompt.
/// </summary>
public interface ICheckRunStore
{
    /// <summary>Where the database file lives, for status messages.</summary>
    string DatabasePath { get; }

    /// <summary>Writes one run in full, replacing any earlier write of the same run id.</summary>
    Task SaveAsync(CheckRunRecord run, CancellationToken cancellationToken = default);

    /// <summary>Runs for one case, most recent first.</summary>
    Task<IReadOnlyList<CheckRunSummary>> ListAsync(
        string caseReference, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>One run in full, or null when the id is not in the archive.</summary>
    Task<CheckRunRecord?> LoadAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The most recent run for a case, which is what a report button means by "this case"
    /// nine times out of ten.
    /// </summary>
    Task<CheckRunRecord?> LoadLatestAsync(
        string caseReference, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Removes one run. Returns true when a row was actually deleted.</summary>
    Task<bool> DeleteAsync(string runId, CancellationToken cancellationToken = default);
}
