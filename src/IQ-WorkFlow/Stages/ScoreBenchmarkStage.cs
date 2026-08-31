using System.Diagnostics;
using System.IO;
using System.Text;

using AiPromptEvaluator;

namespace IQWorkflow;

/// <summary>
/// <b>Scores the run against the case's benchmark, so a run reports hits as well as cost.</b>
///
/// This stage exists because of the pipeline's objective: minimum cost, maximum findings against
/// the expected benchmark. A run that reports only what it spent measures half of that, and the
/// half it measures is the half that does not matter on its own — £3.00 is cheap or ruinous
/// depending entirely on what it found.
///
/// <b>The bottleneck this removes is stated by the scorer itself:</b> scoring has been a manual read
/// of a 65,000-line log against a 42,000-character document, *"which is why it has been done for one
/// case out of four"*. Four benchmarks exist and one has ever been scored — so every argument about
/// this pipeline rests on a single case, and the comparison that would settle it is one nobody can
/// afford to repeat by hand. Doing the bookkeeping automatically on every run is what makes
/// £-per-finding a number a run produces rather than a study somebody commissions.
///
/// <b>It does the bookkeeping and stops there.</b> Whether a run's finding *is* the benchmark's
/// finding remains a human judgement — a group can reach the right conclusion by the wrong route,
/// or state half of it, and no string match settles that. What this produces is the worksheet:
/// benchmark row beside candidate findings, and the one thing a machine can count, which is whether
/// the check that owns the row ran at all.
///
/// <b>Absent benchmark is not a failure.</b> Most cases have no benchmark and never will; a run
/// over a real client file is not scored against anything. The stage reports that it had nothing to
/// score and the pipeline continues.
/// </summary>
public sealed class ScoreBenchmarkStage : IWorkflowStage
{
    /// <summary>The worksheet's file name in the run folder.</summary>
    public const string WorksheetFileName = "benchmark-score.md";

    /// <summary>
    /// What a benchmark file is called, looked for in the case folder.
    ///
    /// Convention rather than configuration: a benchmark belongs to a case, so it travels with the
    /// case folder, and a setting pointing at one would be a setting to keep in step with whichever
    /// case is loaded.
    /// </summary>
    public const string BenchmarkFileName = "expected-results.md";

    public WorkflowStageId Id => WorkflowStageId.Score;

    /// <summary>
    /// After the report rather than beside it: a missing benchmark must never be the reason a run
    /// has no report, and ordering it last makes that impossible rather than merely unlikely.
    /// </summary>
    public IReadOnlyList<WorkflowStageId> DependsOn => [WorkflowStageId.Report];

    /// <summary>
    /// Never satisfied in advance: a score describes this run's findings, and an earlier run's score
    /// is an earlier run's score.
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

        var benchmarkPath = FindBenchmark(context);

        if (benchmarkPath is null)
        {
            clock.Stop();

            return StageOutcome.Succeeded(
                Id,
                $"No benchmark found for this case, so nothing was scored. Add "
                + $"\"{BenchmarkFileName}\" to the case folder to score future runs.",
                clock.Elapsed);
        }

        if (context.Findings.Count == 0)
        {
            clock.Stop();

            return StageOutcome.Failed(
                Id, "There are no findings to score. Assessment has to succeed first.", clock.Elapsed);
        }

        IReadOnlyList<BenchmarkScore.Expectation> expectations;

        try
        {
            expectations = BenchmarkScore.Parse(
                await File.ReadAllTextAsync(benchmarkPath, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            clock.Stop();

            return StageOutcome.Failed(
                Id, $"The benchmark at {benchmarkPath} could not be read: {ex.Message}", clock.Elapsed);
        }

        if (expectations.Count == 0)
        {
            clock.Stop();

            return StageOutcome.Succeeded(
                Id,
                $"The benchmark at {benchmarkPath} holds no expected findings in the shape the "
                + "scorer reads, so nothing was scored.",
                clock.Elapsed);
        }

        var rows = BenchmarkScore.Worksheet(expectations, context.Findings);

        // The one thing a machine can count without judgement. Named "reachable" rather than
        // "caught" deliberately: a check that ran and reported a candidate is a row somebody can
        // adjudicate, not a row that is confirmed found.
        var reachable = rows.Count(r => r.CheckRan && r.Candidates.Count > 0);
        var unreached = rows.Count(r => !r.CheckRan);

        var worksheetPath = context.ArtefactPath(WorksheetFileName);

        try
        {
            var content = new StringBuilder()
                .AppendLine($"# Benchmark score — {context.CaseReference}")
                .AppendLine()
                .AppendLine($"Run started {context.StartedAt:yyyy-MM-dd HH:mm:ss zzz}. "
                            + $"Benchmark: {Path.GetFileName(benchmarkPath)}.")
                .AppendLine()
                .AppendLine("**This is a worksheet, not a score.** Whether a candidate is the "
                            + "benchmark's finding is a judgement and stays a human one; what is "
                            + "counted here is only whether the check that owns each row ran and "
                            + "produced something to adjudicate.")
                .AppendLine()
                .AppendLine(BenchmarkScore.Format(rows))
                .ToString();

            // Created here rather than relied upon: a stage that only works when something else
            // happened to make its output folder first is a stage that cannot be run on its own,
            // and the run folder is this stage's to write into.
            Directory.CreateDirectory(context.RunFolder);

            var temporary = worksheetPath + ".tmp";
            await File.WriteAllTextAsync(temporary, content, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporary, worksheetPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            clock.Stop();

            return StageOutcome.Failed(
                Id, $"The benchmark worksheet could not be written: {ex.Message}", clock.Elapsed);
        }

        clock.Stop();

        progress?.Report(new StageProgress(
            Id, $"Scored {rows.Count} benchmark row(s)", reachable, rows.Count));

        return StageOutcome.Succeeded(
            Id,
            $"{reachable} of {rows.Count} benchmark row(s) have a candidate to adjudicate"
            + (unreached > 0 ? $"; {unreached} were not reached by any check that ran." : "."),
            clock.Elapsed,
            new Dictionary<string, string>
            {
                ["benchmark"] = Path.GetFileName(benchmarkPath),
                ["expectations"] = rows.Count.ToString(),
                ["withCandidates"] = reachable.ToString(),
                ["checkDidNotRun"] = unreached.ToString(),
                ["worksheet"] = worksheetPath,
            });
    }

    /// <summary>
    /// The benchmark for this case, or null where there is none.
    ///
    /// Searched through the guard so a benchmark cannot be read from outside the case folder, and
    /// only at the top level: a benchmark is a property of the case, and picking one up from a
    /// subfolder of converted documents would score a run against something that happened to be
    /// filed with the evidence.
    /// </summary>
    private static string? FindBenchmark(WorkflowContext context)
    {
        var candidate = PathGuard.Contain(context.CaseFolder, BenchmarkFileName);

        return candidate is not null && File.Exists(candidate) ? candidate : null;
    }
}
