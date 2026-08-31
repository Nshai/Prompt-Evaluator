using AiPromptEvaluator;

namespace IQWorkflow;

/// <summary>
/// Everything one run knows, carried from stage to stage.
///
/// <b>Deliberately mutable in exactly two places</b> — the canonical model a later stage needs and
/// the findings the report stage writes — and immutable everywhere else. A context that any stage
/// could write anywhere would make the pipeline's data flow undiscoverable, which is the failure the
/// stage split exists to prevent; a context that was immutable everywhere would force each stage to
/// re-read from storage what the one before it had just produced.
/// </summary>
public sealed class WorkflowContext
{
    public WorkflowContext(
        string caseFolder,
        string caseReference,
        AppSettings settings,
        string runFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseReference);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(runFolder);

        CaseFolder = Path.GetFullPath(caseFolder);
        RunFolder = Path.GetFullPath(runFolder);
        CaseReference = caseReference.Trim();
        Settings = settings;
        StartedAt = DateTimeOffset.Now;
    }

    /// <summary>Where the case's documents are. Read by conversion, extraction and indexing.</summary>
    public string CaseFolder { get; }

    /// <summary>Where this run's artefacts go: the manifest, the report, the prompt log.</summary>
    public string RunFolder { get; }

    /// <summary>Which case this is, as every stage and the vector store filter agree on it.</summary>
    public string CaseReference { get; }

    /// <summary>
    /// Stages the operator has asked this run not to perform.
    ///
    /// <b>Distinct from a stage finding itself satisfied.</b> Satisfaction is the stage's own
    /// judgement about its output and is the resumption rule; this is an instruction from outside
    /// that overrides that judgement. It is recorded as <see cref="StageStatus.Skipped"/> with a
    /// summary saying it was bypassed, so a manifest never implies work happened that did not.
    ///
    /// <b>Bypassing does not remove the stage from the graph.</b> Its dependants still wait for it,
    /// because what they depend on is the artefact and not the running — a case whose documents are
    /// already Markdown has everything conversion would have produced.
    /// </summary>
    public IReadOnlySet<WorkflowStageId> Bypassed { get; init; } = new HashSet<WorkflowStageId>();

    /// <summary>Whether this run has been told not to perform <paramref name="stage"/>.</summary>
    public bool IsBypassed(WorkflowStageId stage) => Bypassed.Contains(stage);

    /// <summary>The live settings instance. Read by every stage; written by none.</summary>
    public AppSettings Settings { get; }

    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// The canonical model, once extraction has produced or loaded one.
    ///
    /// Null until then, and the assessment stage refuses to start rather than assessing against
    /// nothing — which would produce a full set of findings drawn from evidence alone, each one
    /// confidently reported and none of them checking the report at all.
    /// </summary>
    public CanonicalModelDocument? Model { get; internal set; }

    /// <summary>The findings, once assessment has produced them. Read by the report stage.</summary>
    public IReadOnlyList<CheckFinding> Findings { get; internal set; } = [];

    /// <summary>The checks that were assessed, so the report can name what was in scope.</summary>
    public IReadOnlyList<AssessmentCheck> Checks { get; internal set; } = [];

    /// <summary>
    /// The archived run, once assessment has built it.
    ///
    /// Carried rather than rebuilt by the report stage: the check-level outcome is computed from the
    /// group findings once, and recomputing it on the way to the report is how a report and the run
    /// it describes come to disagree about the same run.
    /// </summary>
    public CheckRunRecord? RunRecord { get; internal set; }

    /// <summary>
    /// A path inside this run's folder, guarded.
    ///
    /// Every artefact goes through here rather than being composed at the call site, so a file name
    /// derived from a case reference — which is user input, and reaches this from a folder name —
    /// cannot escape the run folder.
    /// </summary>
    public string ArtefactPath(string fileName) =>
        PathGuard.Require(RunFolder, fileName, "artefact path");

    /// <summary>
    /// A path inside the case folder, guarded. Same reasoning as <see cref="ArtefactPath"/>, for
    /// the reading side.
    /// </summary>
    public string CasePath(string relativePath) =>
        PathGuard.Require(CaseFolder, relativePath, "case file path");
}
