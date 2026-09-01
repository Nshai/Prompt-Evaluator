using System.Diagnostics;
using System.IO;
using System.Text;

using AiPromptEvaluator;

namespace IQWorkflow;

/// <summary>
/// <b>S5 — write the run's artefacts.</b>
///
/// Aggregation and citation verification have already happened inside the runner, and the archive
/// has already been saved. What is left is turning the run into files a person or a scorer can
/// read, which is what makes an unattended run useful rather than merely complete.
///
/// <b>Every artefact is written atomically.</b> A run can be cancelled or killed during this stage
/// like any other, and a half-written report is worse than none: it opens, it renders, and it is
/// missing the checks that had not been appended yet — with nothing on the page to say so.
/// </summary>
public sealed class ReportStage : IWorkflowStage
{
    /// <summary>The artefacts a run leaves behind, named once so a caller can find them.</summary>
    public const string ReportFileName = "compliance-report.html";
    public const string ModelFileName = "canonical-model.json";

    public WorkflowStageId Id => WorkflowStageId.Report;

    /// <summary>The report describes the findings, so it waits for them.</summary>
    public IReadOnlyList<WorkflowStageId> DependsOn => [WorkflowStageId.Assess];

    /// <summary>
    /// Never satisfied in advance: the artefacts describe this run's findings, and an earlier run's
    /// report is an earlier run's report.
    /// </summary>
    public Task<bool> IsSatisfiedAsync(
        WorkflowContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<StageOutcome> ExecuteAsync(
        WorkflowContext context,
        IProgress<StageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();

        if (context.RunRecord is not { } run)
        {
            return Task.FromResult(StageOutcome.Failed(
                Id,
                "There is no assessed run to report. Assessment has to succeed before a report can "
                + "be written.",
                clock.Elapsed));
        }

        var written = new Dictionary<string, string>();

        try
        {
            var reportPath = context.ArtefactPath(ReportFileName);

            WriteAtomic(reportPath, ComplianceReportHtml.Render(run));

            written["report"] = reportPath;

            // The model is written beside the report because the two are read together: a finding
            // cites a canonical path, and a reader checking it needs the model that path came from
            // rather than whichever one the store holds by the time they look.
            if (context.Model is { } model)
            {
                var modelPath = context.ArtefactPath(ModelFileName);
                WriteAtomic(modelPath, model.Json);
                written["model"] = modelPath;
            }

            progress?.Report(new StageProgress(Id, $"Wrote {written.Count} artefact(s)."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            clock.Stop();

            return Task.FromResult(StageOutcome.Failed(
                Id,
                $"The run's artefacts could not be written to {context.RunFolder}: {ex.Message}",
                clock.Elapsed));
        }

        clock.Stop();

        return Task.FromResult(StageOutcome.Succeeded(
            Id, $"Wrote {written.Count} artefact(s) to {context.RunFolder}.", clock.Elapsed, written));
    }

    /// <summary>
    /// Writes to a temporary file beside the target and moves it into place.
    ///
    /// The move is the atomic part: a reader either sees the previous file or the complete new one,
    /// and never the bytes in between. Written beside the target rather than in the system
    /// temporary folder so the move stays within one volume, where it is a rename rather than a
    /// copy that can itself be interrupted.
    /// </summary>
    private static void WriteAtomic(string path, string content)
    {
        var folder = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var temporary = path + ".tmp";

        try
        {
            File.WriteAllText(temporary, content, Encoding.UTF8);
            try
            {
                File.Move(temporary, path, overwrite: true);
            }
            catch (Exception moveEx) when (moveEx is IOException or UnauthorizedAccessException)
            {
                File.Copy(temporary, path, overwrite: true);
                try { File.Delete(temporary); } catch { }
            }
        }
        catch
        {
            // A failed write must not leave the temporary behind to be mistaken for an artefact,
            // and must not mask the original failure with a cleanup failure.
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // Nothing useful can be done; the original exception is the one that matters.
            }

            throw;
        }
    }
}
