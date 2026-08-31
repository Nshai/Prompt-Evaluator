using System.Diagnostics;

using AiPromptEvaluator;

namespace IQWorkflow;

/// <summary>
/// <b>S1 — the suitability report to a canonical model.</b>
///
/// Twelve LLM passes over the report, producing the typed assertion side every later stage compares
/// against. It is 16% of a run's cost and it is computed once: the model it stores is read by all 88
/// requirement groups, which is why the analysis puts the better extraction model here and the
/// cheaper one at assessment.
///
/// <b>A stored model is reused rather than re-extracted.</b> That is the single largest saving the
/// workflow makes on a repeated run — re-extracting to produce a model that already exists is
/// paying twice for the same document — and it is also what makes the 2×2 experiment affordable:
/// four assessment configurations over one extraction.
/// </summary>
public sealed class ExtractModelStage : IWorkflowStage
{
    private readonly ICanonicalModelExtractor _extractor;
    private readonly ICanonicalModelStore _models;
    private readonly IPromptLogWriterFactory? _promptLogs;

    public ExtractModelStage(
        ICanonicalModelExtractor extractor,
        ICanonicalModelStore models,
        IPromptLogWriterFactory? promptLogs = null)
    {
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _promptLogs = promptLogs;
    }

    public WorkflowStageId Id => WorkflowStageId.Extract;

    /// <summary>Extraction reads the converted report, so it waits for conversion.</summary>
    public IReadOnlyList<WorkflowStageId> DependsOn => [WorkflowStageId.Convert];

    /// <summary>
    /// Satisfied when a model for this case and tenant is already stored — and the context is given
    /// it, because a later stage needs the model itself and not merely the knowledge that one
    /// exists.
    ///
    /// <b>This is the one satisfaction check that also loads.</b> Keeping it pure would mean the
    /// assessment stage re-reading what this had just proved was there, for no gain.
    /// </summary>
    public async Task<bool> IsSatisfiedAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var stored = await _models
            .LoadAsync(context.CaseReference, context.Settings.TenantId, cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return false;
        }

        context.Model = stored;

        return true;
    }

    public async Task<StageOutcome> ExecuteAsync(
        WorkflowContext context,
        IProgress<StageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();

        // Refused before twelve model passes rather than after: extraction is the second most
        // expensive thing a run does, and a case folder with no report in it cannot produce a model
        // however well the extractor works.
        var reports = CanonicalModelExtractor.FindReportFiles(context.CaseFolder);

        if (reports.Count == 0)
        {
            return StageOutcome.Failed(
                Id,
                $"No suitability report was found under {context.CaseFolder}. Extraction reads the "
                + "report; convert the case documents first.",
                clock.Elapsed);
        }

        // Written into the run's own folder rather than a shared log directory, so a run's prompts
        // sit beside the manifest that describes them and an unattended batch does not interleave
        // several runs into one file.
        using var promptLog = _promptLogs?.Create(
            context.RunFolder, context.CaseReference, context.StartedAt, "extraction");

        var relay = new Progress<ExtractionProgress>(p => progress?.Report(new StageProgress(
            Id,
            p.Error is { Length: > 0 } error ? $"{p.SectionName}: {error}" : p.SectionName,
            p.Done,
            p.Total)));

        var result = await _extractor
            .ExtractAsync(context.CaseFolder, context.CaseReference, relay, promptLog, cancellationToken)
            .ConfigureAwait(false);

        // Stored the moment it succeeds. Everything below is reporting, and a failure there must
        // never throw away an extraction that has already been paid for.
        await _models.SaveAsync(result.Document, cancellationToken).ConfigureAwait(false);
        context.Model = result.Document;

        clock.Stop();

        var detail = new Dictionary<string, string>
        {
            ["sourceDocuments"] = string.Join(", ", result.Document.SourceDocuments),
            ["schemaVersion"] = result.Document.SchemaVersion,
        };

        if (result.Failures.Count > 0)
        {
            // Recorded rather than fatal, and the distinction matters downstream: a section whose
            // pass failed writes no key, which is byte-for-byte what a value the report genuinely
            // does not contain looks like. The extraction report is the only record that they are
            // different answers, and this carries it into the manifest as well.
            detail["failedSections"] =
                string.Join("; ", result.Failures.Select(f => $"{f.Section}: {f.Error}"));
        }

        var summary = result.Failures.Count == 0
            ? $"Extracted the canonical model from {result.Document.SourceDocuments.Count} document(s)."
            : $"Extracted the canonical model with {result.Failures.Count} section(s) unread — "
              + "a miss on a path under one of them says nothing about the advice.";

        return StageOutcome.Succeeded(Id, summary, clock.Elapsed, detail, result.Breakdown.Usage);
    }
}
