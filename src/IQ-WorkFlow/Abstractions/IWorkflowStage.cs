namespace IQWorkflow;

/// <summary>
/// The stages of the pipeline, in the order the analysis numbers them.
///
/// Named rather than ordinal so a manifest written by one build is readable by the next: a stage
/// inserted in the middle would renumber every later one and silently invalidate every stored run.
/// </summary>
public enum WorkflowStageId
{
    /// <summary>S0 — case documents to Markdown.</summary>
    Convert,

    /// <summary>S1 — the suitability report to a canonical model.</summary>
    Extract,

    /// <summary>S2 — chunk, embed and write to the vector store.</summary>
    Index,

    /// <summary>S3/S4 — retrieve evidence and assess each requirement.</summary>
    Assess,

    /// <summary>S5 — verify, aggregate and write the run's artefacts.</summary>
    Report,

    /// <summary>
    /// Score the run against the case's benchmark, where it has one.
    ///
    /// Not a stage in the analysis, and here because of the objective the analysis serves: minimum
    /// cost, maximum findings against the benchmark. A run that reports only what it spent has
    /// measured the half that means nothing on its own.
    /// </summary>
    Score,
}

/// <summary>
/// One stage of the workflow.
///
/// <b>A stage decides for itself whether its work is already done.</b> The runner does not know what
/// makes an index current or a canonical model usable — only the stage does — so the resumption rule
/// lives beside the work it describes rather than in a scheduler comparing timestamps it does not
/// understand. That is the whole reason <see cref="IsSatisfiedAsync"/> is on this interface.
/// </summary>
public interface IWorkflowStage
{
    /// <summary>Which stage this is. Also its key in the manifest.</summary>
    WorkflowStageId Id { get; }

    /// <summary>
    /// The stages that must finish before this one starts. Empty means it depends on nothing and
    /// may start immediately.
    ///
    /// <b>Declared rather than implied by position, because wall-clock time is an objective.</b>
    /// Extraction and indexing both depend only on conversion and not on each other, and they bill
    /// against different services — extraction against the chat endpoint, indexing against the
    /// embedding one. Running them in sequence would spend the longer of the two twice for no
    /// reason. Stating the dependency lets the runner overlap what is genuinely independent, and
    /// keeps the ordering rule beside the stage that knows it rather than in the composition root's
    /// registration order.
    /// </summary>
    IReadOnlyList<WorkflowStageId> DependsOn => [];

    /// <summary>
    /// Whether this stage's output already exists and is current for the run's inputs, so the run
    /// may skip it.
    ///
    /// Answering true is a claim that re-running would produce the same thing, and it is what makes
    /// a resumed run cheap. Answering false is always safe; answering true wrongly is not, which is
    /// why every implementation checks the artefact rather than a flag it wrote earlier.
    /// </summary>
    Task<bool> IsSatisfiedAsync(WorkflowContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Does the work. Reports failure through <see cref="StageOutcome"/> rather than by throwing:
    /// a stage failing is an expected outcome of a run over real case files, and the manifest has
    /// to record it as data.
    /// </summary>
    Task<StageOutcome> ExecuteAsync(
        WorkflowContext context,
        IProgress<StageProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>What a stage is doing, for an operator watching a long run.</summary>
/// <param name="Stage">Which stage is reporting.</param>
/// <param name="Message">One line, already fit to print.</param>
/// <param name="Done">Units finished, where the stage counts in units.</param>
/// <param name="Total">Units expected, where that is known ahead of time.</param>
public sealed record StageProgress(
    WorkflowStageId Stage,
    string Message,
    int? Done = null,
    int? Total = null)
{
    public override string ToString() =>
        Done is { } done && Total is { } total
            ? $"[{Stage}] {Message} ({done}/{total})"
            : $"[{Stage}] {Message}";
}
