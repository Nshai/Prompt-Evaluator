using System.Diagnostics;

using AiPromptEvaluator;

using Microsoft.Extensions.AI;

namespace IQWorkflow;

/// <summary>
/// <b>S3/S4 — retrieve evidence for each requirement and assess it.</b>
///
/// The stage the rest exist to reach, and 78–84% of a run's tokens. One call per requirement rather
/// than one per check: a pack small enough to attend to in full, a failure isolated to the group
/// that caused it, and no group's reasoning conditioned on the groups written before it.
///
/// <b>The gates are created once and shared across every check.</b> Fan-out here is multiplicative —
/// checks in parallel, each with its groups in parallel — so a bound applied per check would not be
/// a bound at all. One gate for model calls and one for searches, run-wide, is what keeps an
/// unattended run from opening as many connections as the case has requirements.
/// </summary>
public sealed class AssessChecksStage : IWorkflowStage
{
    private readonly ICaseDocumentStoreFactory _stores;
    private readonly ICaseDocumentSearchServiceFactory _searches;
    private readonly ICheckPlanRunnerFactory _runners;
    private readonly ICheckRunStore _runs;
    private readonly Func<AppSettings, IEmbeddingGenerator<string, Embedding<float>>> _embeddings;
    private readonly IPromptLogWriterFactory? _promptLogs;

    public AssessChecksStage(
        ICaseDocumentStoreFactory stores,
        ICaseDocumentSearchServiceFactory searches,
        ICheckPlanRunnerFactory runners,
        ICheckRunStore runs,
        Func<AppSettings, IEmbeddingGenerator<string, Embedding<float>>> embeddings,
        IPromptLogWriterFactory? promptLogs = null)
    {
        _stores = stores ?? throw new ArgumentNullException(nameof(stores));
        _searches = searches ?? throw new ArgumentNullException(nameof(searches));
        _runners = runners ?? throw new ArgumentNullException(nameof(runners));
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        _promptLogs = promptLogs;
    }

    public WorkflowStageId Id => WorkflowStageId.Assess;

    /// <summary>
    /// Both, and this is the join that makes overlapping the two worth doing: assessment compares a
    /// canonical model against retrieved evidence and cannot start without either.
    /// </summary>
    public IReadOnlyList<WorkflowStageId> DependsOn =>
        [WorkflowStageId.Extract, WorkflowStageId.Index];

    /// <summary>
    /// Never satisfied in advance.
    ///
    /// <b>Assessment is the run.</b> An earlier run's findings are a different run's findings, and
    /// treating them as current would make the workflow's whole purpose — producing a result for
    /// this configuration — conditional on nobody having run it before. Re-running is also the only
    /// way to measure the noise floor, which the analysis says no configuration on two of three
    /// cases has ever had.
    /// </summary>
    public Task<bool> IsSatisfiedAsync(
        WorkflowContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public async Task<StageOutcome> ExecuteAsync(
        WorkflowContext context,
        IProgress<StageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();

        // Refused rather than assessed against nothing. Without a model every group would compare
        // the report's claims against an empty assertion side and report findings drawn from
        // evidence alone — a full set of confident verdicts that checked nothing.
        if (context.Model is not { } model)
        {
            return StageOutcome.Failed(
                Id,
                "No canonical model is available. Extraction has to succeed before assessment can "
                + "compare anything against the report.",
                clock.Elapsed);
        }

        var (checks, plans, failure) = LoadProgramme(context);

        if (failure is { } problem)
        {
            return StageOutcome.Failed(Id, problem, clock.Elapsed);
        }

        using var store = _stores.Create(context.Settings);

        if (!await store.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return StageOutcome.Failed(
                Id, $"The vector store at {store.Endpoint} is not responding.", clock.Elapsed);
        }

        var search = _searches.Create(
            context.CaseReference, store, _embeddings(context.Settings));

        using var promptLog = _promptLogs?.Create(
            context.RunFolder, context.CaseReference, context.StartedAt, "assessment");

        var recorder = CheckRunRecorder.ForRun(context.StartedAt, context.CaseReference);

        // One bound for the whole run, shared by every check's runner. See the class remarks.
        using var modelCalls = new ConcurrencyGate(context.Settings.MaxParallelRequests);
        using var searchCalls = new ConcurrencyGate(context.Settings.MaxParallelRequests);

        using var runner = _runners.Create(model, search, promptLog, modelCalls, searchCalls, recorder);

        // By position, so the report reads in check order however the work interleaves — the same
        // reason Core collects group findings by index rather than appending them.
        var findings = new CheckFinding[checks.Count];
        var done = 0;

        await ParallelWork.ForEachAsync(
            checks.Count,
            context.Settings.MaxParallelChecks,
            async (i, token) =>
            {
                var check = checks[i];

                if (!plans.TryGetValue(CheckQueryPlanLoader.NormaliseCheckId(check.CheckId), out var plan))
                {
                    // A check with no plan is reported as a check with no plan, not skipped
                    // silently: a run that assessed 9 of 10 checks and said nothing about the tenth
                    // is the silent-drop defect in another form.
                    findings[i] = CheckFinding.Failed(
                        check.CheckId, check.CheckName,
                        "No query plan was found for this check, so it could not be assessed.",
                        TimeSpan.Zero);
                }
                else
                {
                    findings[i] = await runner
                        .RunAsync(check, plan, null, token)
                        .ConfigureAwait(false);
                }

                progress?.Report(new StageProgress(
                    Id, check.CheckId, Interlocked.Increment(ref done), checks.Count));
            },
            cancellationToken).ConfigureAwait(false);

        clock.Stop();

        context.Findings = findings;
        context.Checks = checks;

        var record = recorder.Build(
            context.CaseReference,
            context.Settings.TenantId,
            context.Settings.SelectedModel,
            context.StartedAt,
            DateTimeOffset.Now,
            RunFingerprint.For(
                context.Settings, model, context.Settings.ResolveCheckPlanFolder(), plans.Count,
                context.Settings.MaxPassagesPerGroup),
            PromptCacheBypass.For(context.Settings.BypassResponseCache),
            model,
            findings,
            checks);

        await _runs.SaveAsync(record, cancellationToken).ConfigureAwait(false);
        context.RunRecord = record;

        var usage = findings.Aggregate(TokenUsage.Empty, (total, f) => new TokenUsage(
            total.InputTokens + f.Usage.InputTokens,
            total.OutputTokens + f.Usage.OutputTokens,
            total.CacheWriteTokens + f.Usage.CacheWriteTokens,
            total.CacheReadTokens + f.Usage.CacheReadTokens));

        var concerns = findings.Count(f => f.ParsedOutcome == CheckOutcome.PotentialConcern);
        var errors = findings.Count(f => f.ParsedOutcome == CheckOutcome.Error);

        return StageOutcome.Succeeded(
            Id,
            $"Assessed {checks.Count} check(s): {concerns} raised a concern"
            + (errors > 0 ? $", {errors} could not be assessed." : "."),
            clock.Elapsed,
            new Dictionary<string, string>
            {
                ["runId"] = recorder.RunId,
                ["checks"] = checks.Count.ToString(),
                ["concerns"] = concerns.ToString(),
                ["errors"] = errors.ToString(),
            },
            usage);
    }

    /// <summary>
    /// The checks and the plans, or the reason a run cannot start.
    ///
    /// <b>Both are loaded before anything is spent.</b> A missing plan folder or an unreadable
    /// check sheet discovered after the first assessment call has already cost money for a run that
    /// was never going to complete.
    /// </summary>
    private static (IReadOnlyList<AssessmentCheck> Checks,
                    IReadOnlyDictionary<string, CheckQueryPlan> Plans,
                    string? Failure)
        LoadProgramme(WorkflowContext context)
    {
        var checksPath = context.Settings.LastChecksCsvPath;

        if (string.IsNullOrWhiteSpace(checksPath) || !File.Exists(checksPath))
        {
            return ([], new Dictionary<string, CheckQueryPlan>(),
                $"The check sheet \"{checksPath}\" was not found. Set it in settings before running.");
        }

        List<AssessmentCheck> checks;

        try
        {
            checks = AssessmentCheckLoader.Load(checksPath);
        }
        catch (Exception ex) when (ex is IOException or FormatException or InvalidDataException)
        {
            return ([], new Dictionary<string, CheckQueryPlan>(),
                $"The check sheet \"{checksPath}\" could not be read: {ex.Message}");
        }

        if (checks.Count == 0)
        {
            return ([], new Dictionary<string, CheckQueryPlan>(),
                $"The check sheet \"{checksPath}\" holds no checks.");
        }

        var planFolder = context.Settings.ResolveCheckPlanFolder();
        var (plans, planFailures) = CheckQueryPlanLoader.Load(planFolder);

        if (plans.Count == 0)
        {
            var why = planFailures.Count > 0
                ? " " + string.Join("; ", planFailures.Select(f => $"{f.File}: {f.Error}"))
                : string.Empty;

            return ([], plans, $"No query plans loaded from \"{planFolder}\".{why}");
        }

        return (checks, plans, null);
    }
}
