using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiPromptEvaluator;

/// <summary>What one extraction run produced.</summary>
public sealed record ExtractionResult(
    CanonicalModelDocument Document,
    IReadOnlyList<(string Section, string Error)> Failures,
    CostBreakdown Breakdown);

/// <summary>
/// Turns the suitability report into a canonical model instance.
///
/// This runs once per case. Everything a check later needs from the report is read out of
/// the stored model, so the report is never sent to a model again — a check run costs
/// retrieval over the evidence documents plus one decision call, not a re-parse of a
/// twenty-page report per check.
///
/// The report is parsed a section at a time (see <see cref="ExtractionSection.All"/>) with
/// the document held constant at the front of every prompt, so the provider's prefix cache
/// covers the expensive part and only the section instruction and its schema slice change.
/// </summary>
public sealed class CanonicalModelExtractor
{
    /// <summary>
    /// The category holding the suitability report. The canonical model describes what the
    /// report asserts, so extraction reads category I and nothing else — evidence documents
    /// are what the report is later checked against, and folding them in here would erase
    /// the distinction the checks depend on.
    /// </summary>
    public const string SuitabilityReportCategoryCode = "I";

    private readonly AppSettings _settings;
    private readonly PromptEvaluator _evaluator;

    public CanonicalModelExtractor(AppSettings settings, PromptEvaluator evaluator)
    {
        _settings = settings;
        _evaluator = evaluator;
    }

    /// <summary>
    /// The suitability report files for a case: the Markdown under the category I folder.
    /// </summary>
    public static IReadOnlyList<string> FindReportFiles(string caseFolder)
    {
        var folder = Path.Combine(caseFolder, SuitabilityReportCategoryCode);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory
            .GetFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(CaseDocumentIndexer.IsIndexable)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Extracts the model for one case and returns it. Writing it to the store is the
    /// caller's job, so a run that the user cancels or that fails every section does not
    /// overwrite a good model with a worse one.
    /// </summary>
    public async Task<ExtractionResult> ExtractAsync(
        string caseFolder,
        string caseReference,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var reportFiles = FindReportFiles(caseFolder);
        if (reportFiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"No Markdown suitability report was found under \"{Path.Combine(caseFolder, SuitabilityReportCategoryCode)}\". "
                + "The canonical model is extracted from the category I documents.");
        }

        var schemaPath = _settings.ResolveCanonicalSchemaPath();
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException(
                $"The canonical model schema was not found at \"{schemaPath}\". "
                + "Set its location in Settings, or place the schema file beside the executable.",
                schemaPath);
        }

        var schemaJson = await File.ReadAllTextAsync(schemaPath, cancellationToken).ConfigureAwait(false);
        var documentText = await ReadReportsAsync(reportFiles, cancellationToken).ConfigureAwait(false);

        var root = new JsonObject
        {
            ["modelVersion"] = SchemaVersionOf(schemaJson),
        };

        var failures = new List<(string Section, string Error)>();
        var usage = TokenUsage.Empty;
        var done = 0;

        foreach (var section in ExtractionSection.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var startedAt = Stopwatch.GetTimestamp();
            var length = 0;
            string? error = null;

            try
            {
                var (fragment, sectionUsage) = await ExtractSectionAsync(
                    section, schemaJson, documentText, caseReference, cancellationToken).ConfigureAwait(false);

                usage = Add(usage, sectionUsage);
                length = Merge(root, fragment, section);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                error = ex.Message.Trim();
                failures.Add((section.Name, error));
            }

            done++;
            progress?.Report(new ExtractionProgress(
                done, ExtractionSection.All.Count, section.Name, length,
                Stopwatch.GetElapsedTime(startedAt), error));
        }

        if (failures.Count == ExtractionSection.All.Count)
        {
            throw new InvalidOperationException(
                $"Every extraction pass failed. First error: {failures[0].Error}");
        }

        var document = new CanonicalModelDocument(
            CaseReference: caseReference,
            TenantId: _settings.TenantId,
            Json: root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            ModelId: _settings.SelectedModel,
            SchemaVersion: SchemaVersionOf(schemaJson),
            SourceDocuments: reportFiles.Select(Path.GetFileName).Select(n => n!).ToList(),
            ExtractedAt: DateTimeOffset.Now,
            Usage: usage);

        return new ExtractionResult(document, failures, CostBreakdown.Create(_settings.SelectedModel, usage));
    }

    /// <summary>Runs one pass and returns the section's JSON object plus what it cost.</summary>
    private async Task<(JsonObject Fragment, TokenUsage Usage)> ExtractSectionAsync(
        ExtractionSection section,
        string schemaJson,
        string documentText,
        string caseReference,
        CancellationToken cancellationToken)
    {
        var slice = JsonSchemaSlicer.Slice(schemaJson, section.Properties);

        var result = await _evaluator
            .RunRawAsync(
                BuildSystemPrompt(),
                BuildSectionPrompt(section, slice, documentText, caseReference),
                _settings.ExtractionMaxTokens,
                cancellationToken)
            .ConfigureAwait(false);

        var fragment = ParseObject(result.Response)
            ?? throw new InvalidOperationException(
                "The model did not return a JSON object for this section.");

        return (fragment, result.Breakdown.Usage);
    }

    /// <summary>
    /// The extractor's standing instructions. The rules that matter are the ones that keep a
    /// later check honest: never invent a value, record contradictions instead of resolving
    /// them, and quote the text every assertion came from.
    /// </summary>
    private static string BuildSystemPrompt() =>
        """
        You extract structured data from UK financial services suitability reports into a
        canonical JSON model. You are building the record a compliance assessor will later
        rely on, so accuracy about what the document does NOT say matters as much as accuracy
        about what it does.

        Rules:
        - Return one JSON object and nothing else. No prose, no markdown fences.
        - Populate only the properties named in the request. Omit anything you cannot fill.
        - Never invent a value. If the report does not state something, either omit the field
          or set its provenance assertionStatus to "Absent" — a plausible guess is worse than
          a gap, because it will read as evidence.
        - assertionStatus: "Stated" when it is explicit in the text; "Inferred" when you read
          it out of narrative prose; "Derived" when you calculated it from other values;
          "Absent" when the model expects it and the report does not provide it.
        - Every Stated or Inferred provenance needs a verbatim quote from the document and the
          page number from the nearest "<!-- page: N -->" marker above it.
        - Keep the document's own units. "£300 per week net" is amount 300, basis "Net",
          frequency "Weekly" — do not convert to monthly and lose the original.
        - Record contradictions rather than resolving them. If the report gives two different
          figures for the same thing, capture both where the model allows it and note the
          conflict; do not quietly pick one.
        - Boilerplate is data. Capture generic paragraphs with isClientSpecific false rather
          than skipping them — their presence without personalisation is itself a finding.
        - Enumerated fields must use a value from the schema's enum, or be omitted.
        """;

    private static string BuildSectionPrompt(
        ExtractionSection section, string schemaSlice, string documentText, string caseReference)
    {
        var sb = new StringBuilder();

        // The document goes first and is identical on every pass, so the provider's prefix
        // cache covers it and only the tail below is billed at the full input rate.
        sb.AppendLine($"# Suitability report for case {caseReference}");
        sb.AppendLine();
        sb.AppendLine(documentText);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"## Extract: {section.Name}");
        sb.AppendLine();
        sb.AppendLine(section.Description);
        sb.AppendLine();
        sb.AppendLine("Return a JSON object containing only these top-level properties, conforming to the schema below:");
        sb.AppendLine(string.Join(", ", section.Properties.Select(p => $"\"{p}\"")));
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(schemaSlice);
        sb.AppendLine("```");

        return sb.ToString();
    }

    /// <summary>
    /// Concatenates the report files, keeping the page markers the converter left behind —
    /// they are what lets a provenance record cite a page rather than an offset.
    /// </summary>
    private static async Task<string> ReadReportsAsync(
        IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        foreach (var file in files)
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            sb.AppendLine($"<!-- document: {Path.GetFileName(file)} -->");
            sb.AppendLine(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Copies a section's properties into the model being assembled, and reports how much
    /// JSON arrived. Only the properties the section asked for are taken: a pass that
    /// volunteers a neighbouring section's data would otherwise overwrite a better answer
    /// from the pass that was actually looking at it.
    /// </summary>
    private static int Merge(JsonObject root, JsonObject fragment, ExtractionSection section)
    {
        var length = 0;

        foreach (var property in section.Properties)
        {
            if (fragment[property] is not { } value)
            {
                continue;
            }

            var clone = value.DeepClone();
            length += clone.ToJsonString().Length;
            root[property] = clone;
        }

        return length;
    }

    /// <summary>
    /// Reads the model's reply as a JSON object, tolerating the two things a chat model does
    /// even when told not to: wrapping the object in a markdown fence, and adding a sentence
    /// before or after it.
    /// </summary>
    public static JsonObject? ParseObject(string response)
    {
        var text = response.Trim();

        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0)
            {
                text = text[(firstNewline + 1)..];
            }

            var fenceEnd = text.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceEnd >= 0)
            {
                text = text[..fenceEnd];
            }

            text = text.Trim();
        }

        if (!text.StartsWith('{'))
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return null;
            }

            text = text[start..(end + 1)];
        }

        try
        {
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The schema's declared model version, so a stored model records which contract it was
    /// built against and a later run can tell that the schema has moved on.
    /// </summary>
    private static string SchemaVersionOf(string schemaJson)
    {
        try
        {
            var version = JsonNode.Parse(schemaJson)?["properties"]?["modelVersion"]?["const"];
            return version?.GetValue<string>() ?? "unknown";
        }
        catch (JsonException)
        {
            return "unknown";
        }
    }

    private static TokenUsage Add(TokenUsage a, TokenUsage b) => new(
        a.InputTokens + b.InputTokens,
        a.OutputTokens + b.OutputTokens,
        a.CacheWriteTokens + b.CacheWriteTokens,
        a.CacheReadTokens + b.CacheReadTokens);
}
