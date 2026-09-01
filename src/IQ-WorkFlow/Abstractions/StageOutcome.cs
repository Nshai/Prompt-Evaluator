using AiPromptEvaluator;

namespace IQWorkflow;

/// <summary>How a stage ended.</summary>
public enum StageStatus
{
    /// <summary>It ran and produced what it was asked for.</summary>
    Succeeded,

    /// <summary>Its output was already current, so it did no work. Not a failure and not a success.</summary>
    Skipped,

    /// <summary>It could not complete. The pipeline stops here.</summary>
    Failed,

    /// <summary>An operator stopped the run.</summary>
    Cancelled,
}

/// <summary>
/// What one stage did, as data.
///
/// <b>A result object rather than an exception</b>, because a stage failing is an ordinary outcome
/// of running over real case files — an unreachable vector store, a report the extractor cannot
/// parse, a plan that will not load — and the manifest has to record it in the same shape as a
/// success. Reserving exceptions for defects keeps a stack trace meaning "this is a bug" rather
/// than "the container is not running".
/// </summary>
/// <param name="Stage">Which stage this describes.</param>
/// <param name="Status">How it ended.</param>
/// <param name="Summary">One line for a human: what happened, in the words a reader needs.</param>
/// <param name="Elapsed">Wall-clock time the stage took.</param>
/// <param name="Detail">
/// Anything worth keeping that is not the summary — counts, file names, the provider's own words on
/// a failure. Never case content and never a credential.
/// </param>
/// <param name="Usage">
/// What the stage billed, where the stage bills anything.
///
/// <b>Null means "not known", which is not the same as zero</b> and must not be rendered as
/// £0.0000. A provider that reports no token count has told us nothing, and a run report that turns
/// that into "free" is making a claim the provider never made.
/// </param>
public sealed record StageOutcome(
    WorkflowStageId Stage,
    StageStatus Status,
    string Summary,
    TimeSpan Elapsed,
    IReadOnlyDictionary<string, string>? Detail = null,
    TokenUsage? Usage = null)
{
    /// <summary>True when the pipeline may continue past this stage.</summary>
    public bool CanContinue => Status is StageStatus.Succeeded or StageStatus.Skipped;

    public static StageOutcome Succeeded(
        WorkflowStageId stage,
        string summary,
        TimeSpan elapsed,
        IReadOnlyDictionary<string, string>? detail = null,
        TokenUsage? usage = null) =>
        new(stage, StageStatus.Succeeded, summary, elapsed, detail, usage);

    public static StageOutcome Skipped(WorkflowStageId stage, string summary) =>
        new(stage, StageStatus.Skipped, summary, TimeSpan.Zero);

    /// <summary>
    /// A stage the operator told this run not to perform.
    ///
    /// <b>Worded so a manifest cannot be misread as "it was already current".</b> Those are
    /// different facts: one is the stage's own judgement about its output, the other is an
    /// instruction that overrode it, and a run whose conversion was bypassed against a case that
    /// was not in fact converted fails downstream for a reason this line is the only record of.
    /// </summary>
    public static StageOutcome Bypassed(WorkflowStageId stage) =>
        new(stage, StageStatus.Skipped, "Bypassed: this run was told not to perform this stage.", TimeSpan.Zero);

    /// <summary>
    /// A stage that could not complete.
    ///
    /// The summary is the message an operator will act on, so it names what failed and what to do,
    /// and never echoes a credential or the case's own content.
    /// </summary>
    public static StageOutcome Failed(
        WorkflowStageId stage,
        string summary,
        TimeSpan elapsed = default,
        IReadOnlyDictionary<string, string>? detail = null) =>
        new(stage, StageStatus.Failed, summary, elapsed, detail);

    public static StageOutcome Cancelled(WorkflowStageId stage, TimeSpan elapsed = default) =>
        new(stage, StageStatus.Cancelled, "The run was cancelled.", elapsed);

    public override string ToString() => $"{Stage}: {Status} — {Summary}";
}
