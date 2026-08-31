using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AiPromptEvaluator;

namespace IQWorkflow;

/// <summary>One line of the conversion summary, in the shape existing converted cases already use.</summary>
public sealed record ConversionSummaryEntry(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("characters")] int Characters,
    [property: JsonPropertyName("recoveredTables")] int RecoveredTables,
    [property: JsonPropertyName("converter")] string Converter,
    [property: JsonPropertyName("error")] string? Error = null,
    [property: JsonPropertyName("transcribedPictures")] int TranscribedPictures = 0,
    [property: JsonPropertyName("narratedTables")] int NarratedTables = 0);

/// <summary>
/// <b>S0 — the case's documents to Markdown.</b>
///
/// <b>The output contract is v1's and is not negotiable</b>: a mirror of the case folder, category
/// subfolders preserved, one <c>.md</c> per source document, plus a <c>_conversion-summary.json</c>
/// beside them. Every existing converted case on disk has that shape, and the indexer, the
/// extractor and every stored run read it. The conversion *logic* is v2's, because the losses it
/// repairs are ones the analysis names and nothing downstream can recover.
///
/// <b>A document that fails to convert does not fail the run.</b> A case of thirty-seven documents
/// where one is an encrypted PDF should produce thirty-six converted documents and a summary line
/// saying why the thirty-seventh is missing — not nothing. The stage fails only when it converted
/// nothing at all, because that is the case where every later stage would report the file as silent
/// rather than as unread.
/// </summary>
public sealed class ConvertDocumentsStage : IWorkflowStage
{
    /// <summary>Where the summary is written, matching the existing converted cases.</summary>
    public const string SummaryFileName = "_conversion-summary.json";

    private readonly Func<AppSettings, ICaseDocumentConverter> _converters;

    /// <param name="converters">
    /// How a converter is built for a run. A factory rather than an instance because it needs a
    /// configured endpoint, and the workflow is composed before anything has validated one.
    /// </param>
    public ConvertDocumentsStage(Func<AppSettings, ICaseDocumentConverter> converters) =>
        _converters = converters ?? throw new ArgumentNullException(nameof(converters));

    public WorkflowStageId Id => WorkflowStageId.Convert;

    /// <summary>Nothing precedes conversion; it reads the case folder as it was given.</summary>
    public IReadOnlyList<WorkflowStageId> DependsOn => [];

    /// <summary>
    /// Satisfied when the case folder already holds Markdown in its category folders.
    ///
    /// <b>A presence check, not a freshness check</b>, and for the same reason the index stage
    /// counts rather than hashes: knowing whether the Markdown matches the sources would mean
    /// hashing every source and keeping that in step with the converter's own version. A case
    /// arriving already converted is the normal path — the existing test cases are all stored that
    /// way — and an operator who has changed the sources deletes the Markdown.
    /// </summary>
    public Task<bool> IsSatisfiedAsync(
        WorkflowContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(
            Directory.Exists(context.CaseFolder)
            && Directory.EnumerateFiles(context.CaseFolder, "*.md", SearchOption.AllDirectories).Any());

    public async Task<StageOutcome> ExecuteAsync(
        WorkflowContext context,
        IProgress<StageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();

        if (!Directory.Exists(context.CaseFolder))
        {
            return StageOutcome.Failed(
                Id, $"The case folder {context.CaseFolder} does not exist.", clock.Elapsed);
        }

        var sources = Sources(context.CaseFolder).ToList();

        if (sources.Count == 0)
        {
            return StageOutcome.Failed(
                Id,
                $"No convertible documents were found under {context.CaseFolder}. A case is a folder "
                + "of category subfolders holding its documents.",
                clock.Elapsed);
        }

        var converter = _converters(context.Settings);
        var entries = new List<ConversionSummaryEntry>();
        var converted = 0;
        var recovered = 0;
        var transcribed = 0;
        var narrated = 0;
        var done = 0;

        foreach (var (path, relative, category) in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new StageProgress(Id, relative, done++, sources.Count));

            // A bundle, because one file is not always one document: an email produces the covering
            // note and a document per attachment it carried, and in a case bundle the attachment is
            // routinely the evidence the note is only referring to.
            var results = await converter
                .ConvertAsync(path, relative, category, cancellationToken)
                .ConfigureAwait(false);

            foreach (var result in results)
            {
                if (result.Succeeded)
                {
                    WriteMarkdown(context.CaseFolder, path, result.OutputName, result.Markdown!);
                    converted++;
                    recovered += result.RecoveredTables;
                    transcribed += result.TranscribedPictures;
                    narrated += result.NarratedTables;
                }

                entries.Add(new ConversionSummaryEntry(
                    result.Source,
                    result.CategoryCode,
                    result.Markdown?.Length ?? 0,
                    result.RecoveredTables,
                    "docling-serve",
                    result.Error,
                    result.TranscribedPictures,
                    result.NarratedTables));
            }
        }

        WriteSummary(context.CaseFolder, entries);

        clock.Stop();

        var failed = entries.Count(e => e.Error is not null);

        // Nothing converted is a failure however cleanly it ran: every later stage would find an
        // empty case and report the file as silent, which is a claim about the advice rather than
        // about the run.
        if (converted == 0)
        {
            return StageOutcome.Failed(
                Id,
                $"None of the {sources.Count} document(s) under {context.CaseFolder} could be "
                + "converted. Check that the Docling endpoint is reachable.",
                clock.Elapsed,
                new Dictionary<string, string> { ["failed"] = failed.ToString() });
        }

        var detail = new Dictionary<string, string>
        {
            ["documents"] = sources.Count.ToString(),
            ["converted"] = converted.ToString(),
            ["failed"] = failed.ToString(),
            ["recoveredTables"] = recovered.ToString(),
            ["summary"] = Path.Combine(context.CaseFolder, SummaryFileName),
        };

        // Recorded only when they happened, so a manifest for the default configuration does not
        // carry two rows of zeroes describing passes that were never switched on.
        if (transcribed > 0)
        {
            detail["transcribedPictures"] = transcribed.ToString();
        }

        if (narrated > 0)
        {
            detail["narratedTables"] = narrated.ToString();
        }

        // Documents, not source files: an email and its attachments are several documents from one
        // file, and reporting them against the file count would read as more than 100% converted.
        var summary = $"Converted {converted} document(s) from {sources.Count} file(s)";

        if (recovered > 0)
        {
            summary += $", recovering {recovered} table(s) the converter did not return";
        }

        if (transcribed > 0)
        {
            summary += $", transcribing {transcribed} picture(s)";
        }

        if (narrated > 0)
        {
            summary += $", narrating {narrated} table(s)";
        }

        // Named rather than counted only: a document nobody could read is a fact about the run that
        // every later stage would otherwise present as a fact about the case.
        if (failed > 0)
        {
            summary += $". {failed} could not be read — see {SummaryFileName}";
        }

        return StageOutcome.Succeeded(Id, summary + ".", clock.Elapsed, detail);
    }

    /// <summary>
    /// Every convertible document under the case folder, with the category it sits in.
    ///
    /// The category is the top-level folder name, which is how every converted case on disk is
    /// arranged and how the indexer reads a document's category back.
    /// </summary>
    /// <summary>
    /// Whether a file in the case folder is something to convert.
    ///
    /// Email is included and Markdown is not: a case may arrive part-converted, and re-converting
    /// Markdown would be asking the sidecar to render its own output.
    /// </summary>
    internal static bool IsSource(string path) =>
        (DoclingMarkdownConverter.IsConvertible(path) || EmailDocumentConverter.IsEmail(path))
        && !DoclingMarkdownConverter.IsAlreadyMarkdown(path);

    private static IEnumerable<(string Path, string Relative, string Category)> Sources(string caseFolder)
    {
        foreach (var path in Directory
            .EnumerateFiles(caseFolder, "*", SearchOption.AllDirectories)
            .Where(IsSource)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(caseFolder, path);
            var category = relative.Split(Path.DirectorySeparatorChar)[0];

            // A file loose in the case root has no category. Skipped rather than guessed: a
            // category decides which checks can retrieve a document at all, and inventing one
            // would put a document in front of the wrong requirements.
            if (!string.Equals(category, relative, StringComparison.Ordinal))
            {
                yield return (path, relative.Replace(Path.DirectorySeparatorChar, '/'), category);
            }
        }
    }

    /// <summary>
    /// Writes the Markdown beside its source, named for it.
    ///
    /// Beside rather than into a parallel tree, because that is what the converted cases on disk
    /// look like and what the indexer walks — the category folder holds both.
    /// </summary>
    /// <param name="caseFolder">The run's case folder; nothing may be written outside it.</param>
    /// <param name="sourcePath">The document this came from, which decides the category folder.</param>
    /// <param name="markdown">The converted text.</param>
    /// <param name="outputName">
    /// The name to write under, without an extension, where the source produced more than one
    /// document. Null takes the source document's own name, which is the ordinary case.
    /// </param>
    private static void WriteMarkdown(
        string caseFolder, string sourcePath, string? outputName, string markdown)
    {
        var folder = Path.GetDirectoryName(sourcePath)!;

        var name = (string.IsNullOrWhiteSpace(outputName)
            ? Path.GetFileNameWithoutExtension(sourcePath)
            : outputName) + ".md";

        // Guarded even though it is derived from a path we enumerated: the file name comes from the
        // case folder's own contents, which is user input by another route.
        var target = PathGuard.Require(caseFolder, Path.Combine(folder, name), "converted document");

        WriteAtomic(target, markdown);
    }

    private static void WriteSummary(string caseFolder, IReadOnlyList<ConversionSummaryEntry> entries) =>
        WriteAtomic(
            Path.Combine(caseFolder, SummaryFileName),
            JsonSerializer.Serialize(
                new Dictionary<string, object> { ["converted"] = entries },
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                }));

    /// <summary>
    /// Temporary file, then move. A conversion run is long and interruptible, and a half-written
    /// Markdown file is worse than a missing one: the indexer would read it and the case would be
    /// quietly short of the half that never arrived.
    /// </summary>
    private static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temporary = path + ".tmp";

        try
        {
            File.WriteAllText(temporary, content, Encoding.UTF8);
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // The original failure is the one that matters.
            }

            throw;
        }
    }
}
