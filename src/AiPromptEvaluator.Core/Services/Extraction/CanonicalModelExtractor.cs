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
    CostBreakdown Breakdown)
{
    /// <summary>
    /// Vocabulary values the extraction wrote that the schema does not document, and the
    /// near-misses that were corrected on the way in.
    ///
    /// Reported rather than silently applied, because the whole reason objectiveType drifted
    /// to "RetirementObjective" unnoticed is that nothing was looking.
    /// </summary>
    public IReadOnlyList<CanonicalVocabulary.Correction> VocabularyCorrections { get; init; } = [];
}

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
public sealed class CanonicalModelExtractor : ICanonicalModelExtractor
{
    /// <summary>
    /// The category holding the suitability report. The canonical model describes what the
    /// report asserts, so extraction reads category I and nothing else — evidence documents
    /// are what the report is later checked against, and folding them in here would erase
    /// the distinction the checks depend on.
    /// </summary>
    public const string SuitabilityReportCategoryCode = "I";

    private readonly AppSettings _settings;
    private readonly IChatCompletionClient _chat;

    public CanonicalModelExtractor(AppSettings settings, IChatCompletionClient chat)
    {
        _settings = settings;
        _chat = chat;
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
        PromptLogWriter? promptLog = null,
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

        // Read out of the schema itself, so there is never a second copy of the vocabulary to
        // fall out of step with the one the model is shown.
        var vocabularies = CanonicalVocabulary.Parse(schemaJson);
        var corrections = new List<CanonicalVocabulary.Correction>();
        var documentText = await ReadReportsAsync(reportFiles, cancellationToken).ConfigureAwait(false);

        var root = new JsonObject
        {
            ["modelVersion"] = SchemaVersionOf(schemaJson),
        };

        var failures = new List<(string Section, string Error)>();

        // The canonical properties of every pass that failed. Without this the self-report
        // pass cannot see them at all: a failed pass never writes its key, so it is neither
        // populated nor empty in the summary it is shown.
        var failedProperties = new List<string>();
        var usage = TokenUsage.Empty;
        var done = 0;

        // Grows as passes complete, and is shown to every pass that follows. See
        // CanonicalModelIdentityRegistry for why the passes cannot be trusted to agree without it.
        var identity = new CanonicalModelIdentityRegistry();

        // The loop is wrapped so that a run which does not reach the end still terminates its own
        // log. Cancellation is the expected way out — the per-section catch rethrows it rather
        // than recording twelve identical failures — and it is precisely the case that used to
        // leave a log indistinguishable from a completed one.
        try
        {
            foreach (var section in ExtractionSection.All)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var startedAt = Stopwatch.GetTimestamp();
                var length = 0;
                string? error = null;
                string? shortfallNote = null;

                try
                {
                    var (fragment, sectionUsage, shortfall) = await ExtractSectionAsync(
                        section, schemaJson, documentText, caseReference, identity, root,
                        failedProperties, promptLog, cancellationToken).ConfigureAwait(false);

                    usage = Add(usage, sectionUsage);

                    // Before the merge, so the stored model carries the documented spelling and a
                    // check reading it by value is not defeated by capitalisation.
                    corrections.AddRange(CanonicalVocabulary.Normalise(fragment, vocabularies));

                    length = Merge(root, fragment, section);

                    // Ids are adopted the moment the pass that defines them lands, so the next
                    // pass is choosing from a table rather than inventing its own naming.
                    identity.Adopt(root);

                    // A section that lost a value still succeeded. Recording it as a failure is
                    // what made a run announce eight broken sections when none was broken.
                    shortfallNote = shortfall;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                error = ex.Message.Trim();
                failures.Add((section.Name, error));
                failedProperties.AddRange(section.Properties);
            }

                done++;
                progress?.Report(new ExtractionProgress(
                    done, ExtractionSection.All.Count, section.Name, length,
                    Stopwatch.GetElapsedTime(startedAt), error, shortfallNote));
            }
        }
        catch (OperationCanceledException)
        {
            promptLog?.LogRunEnded(
                $"Cancelled after {done} of {ExtractionSection.All.Count} extraction passes.");
            throw;
        }
        catch (Exception ex)
        {
            promptLog?.LogRunEnded(
                $"Failed after {done} of {ExtractionSection.All.Count} extraction passes: "
                + ex.Message.Trim());
            throw;
        }

        if (failures.Count == ExtractionSection.All.Count)
        {
            throw new InvalidOperationException(
                $"Every extraction pass failed. First error: {failures[0].Error}");
        }

        // Passes that ran before an id was adopted, or that used an entity's old name anyway,
        // are repointed here; whatever still resolves to nothing is a broken link and is
        // reported rather than left for a check to discover as an unresolved path.
        identity.RewriteReferences(root);

        foreach (var dangling in identity.DanglingReferences(root))
        {
            failures.Add(("Cross-references", $"Reference resolves to nothing: {dangling}"));
        }

        StampSource(root, reportFiles);

        var document = new CanonicalModelDocument(
            CaseReference: caseReference,
            TenantId: _settings.TenantId,
            Json: root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            ModelId: _settings.SelectedModel,
            SchemaVersion: SchemaVersionOf(schemaJson),
            SourceDocuments: reportFiles.Select(Path.GetFileName).Select(n => n!).ToList(),
            ExtractedAt: DateTimeOffset.Now,
            Usage: usage);

        return new ExtractionResult(document, failures, CostBreakdown.Create(_settings.SelectedModel, usage))
        {
            VocabularyCorrections = corrections.Distinct().ToList(),
        };
    }

    /// <summary>
    /// Runs one pass and returns the section's JSON object, what it cost, and a note of
    /// anything the pass fell short of — a truncation it was salvaged from, or values dropped
    /// for not matching the schema's enums.
    /// </summary>
    private async Task<(JsonObject Fragment, TokenUsage Usage, string? Shortfall)> ExtractSectionAsync(
        ExtractionSection section,
        string schemaJson,
        string documentText,
        string caseReference,
        CanonicalModelIdentityRegistry identity,
        JsonObject modelSoFar,
        IReadOnlyCollection<string> failedProperties,
        PromptLogWriter? promptLog,
        CancellationToken cancellationToken)
    {
        var slice = StripCodeOwnedFields(JsonSchemaSlicer.Slice(schemaJson, section.Properties));
        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildSectionPrompt(
            section, slice, documentText, caseReference, identity, modelSoFar, failedProperties);

        var result = await _chat
            .RunRawAsync(
                systemPrompt,
                userPrompt,
                _settings.ExtractionMaxTokens,
                cancellationToken)
            .ConfigureAwait(false);

        // Logged before parsing, so a reply that fails to parse is still on disk to read —
        // which is how the truncated recommendations section came to be diagnosed at all.
        promptLog?.LogExchange("extract", section.Name, systemPrompt, userPrompt, result.Response);

        var shortfalls = new List<string>();
        var usage = result.Breakdown.Usage;
        var fragment = ExtractionResponseReader.ParseObject(result.Response, out var duplicates);

        // A malformed reply — as distinct from a truncated one — is usually one stray bracket
        // in an otherwise complete answer, and the same prompt run again normally lands. An
        // observed run lost financialPosition and recommendations to a single extra brace
        // apiece, and with them the payload of eight of the ten checks: CHK-006 reads 22
        // canonical paths under /recommendations, CHK-001 reads 21 under /financialPosition.
        //
        // Repairing the brackets was the alternative and was rejected: where an unmatched
        // closer belongs is a guess, and a wrongly re-nested fragment is worse than a retry,
        // because it would be merged and believed.
        if (fragment is null && !ExtractionResponseReader.LooksTruncated(result.Response))
        {
            var firstReply = result.Response;

            // The retry must not ask the identical question. An observed run sent the same
            // prompt again and the gateway served the identical failed reply from cache —
            // 41,580 characters, the same stray brace at the same offset, in the same second.
            // Both attempts were billed and the section was lost anyway.
            //
            // Varied by the digest of the reply that failed rather than by a random nonce: the
            // prompt differs from attempt one, which is all the cache key needs, while staying
            // reproducible for a run that pins its sampling. A random value would defeat the
            // cache and determinism together.
            var retryPrompt = userPrompt
                + Environment.NewLine
                + Environment.NewLine
                + "## Second attempt"
                + Environment.NewLine
                + Environment.NewLine
                + "The previous reply for this section was not valid JSON. Return the same "
                + "content, correctly nested. The two faults seen in practice are a closing "
                + "brace too many just after a long quote, and a property name written twice in "
                + "the same object — check both before finishing."
                + Environment.NewLine
                + $"(Attempt 2 of 2; previous reply {RunFingerprint.Digest(firstReply)}.)";

            result = await _chat
                .RunRawAsync(
                    systemPrompt,
                    retryPrompt,
                    _settings.ExtractionMaxTokens,
                    cancellationToken)
                .ConfigureAwait(false);

            promptLog?.LogExchange(
                "extract", section.Name + " (retry)", systemPrompt, retryPrompt, result.Response);

            // Both attempts were paid for, so both are counted. A retry that vanished from the
            // cost would make the run cheaper on paper than it was.
            usage = Add(usage, result.Breakdown.Usage);
            fragment = ExtractionResponseReader.ParseObject(result.Response, out duplicates);

            if (fragment is not null)
            {
                shortfalls.Add(
                    "The first reply was not valid JSON; the pass was retried once and the "
                    + "retry parsed. Both attempts are counted in the cost.");
            }
            else if (string.Equals(result.Response, firstReply, StringComparison.Ordinal))
            {
                // Worth saying plainly rather than reporting two independent failures: a
                // byte-identical reply to a prompt that differed is a cache serving the
                // failure, not the model reproducing it.
                shortfalls.Add(
                    "The retry returned a byte-identical reply to a prompt that differed, which "
                    + "means the failure was served from cache rather than regenerated. The "
                    + "second attempt was billed and could not have succeeded.");
            }
        }

        if (fragment is null)
        {
            if (!ExtractionResponseReader.LooksTruncated(result.Response))
            {
                throw new InvalidOperationException(
                    "The model did not return a JSON object for this section, on the first "
                    + "attempt or on the retry.");
            }

            // Cut off at the output cap. Whatever finished is worth keeping — an observed run
            // lost four complete recommendations because the fifth was half-written.
            fragment = ExtractionResponseReader.Salvage(result.Response)
                ?? throw new InvalidOperationException(
                    $"The response was cut off at the {_settings.ExtractionMaxTokens:N0}-token output "
                    + $"limit after {result.Response.Length:N0} characters, with nothing complete to "
                    + "salvage. Raise ExtractionMaxTokens or split this section.");

            var kept = section.Properties
                .Sum(p => ExtractionResponseReader.SalvagedCount(fragment, p));

            shortfalls.Add(
                $"The response was cut off at the {_settings.ExtractionMaxTokens:N0}-token output limit; "
                + $"{kept} complete entr{(kept == 1 ? "y was" : "ies were")} salvaged and the rest lost. "
                + "Raise ExtractionMaxTokens or split this section.");
        }

        // Reported, never silent. A repeated property name is a stutter the model made while
        // writing, so the value is usually identical and nothing is lost — but where the two
        // differ, the extraction report has to carry it, because the section is merged and
        // believed either way.
        if (duplicates.Count > 0)
        {
            var conflicting = duplicates.Where(d => d.ValuesDiffer).ToList();

            shortfalls.Add(
                $"The reply wrote {duplicates.Count} property name(s) twice in the same object; "
                + "the first of each was kept: "
                + string.Join("; ", duplicates.Take(5))
                + (duplicates.Count > 5 ? "; …" : string.Empty)
                + (conflicting.Count > 0
                    ? $" {conflicting.Count} of them disagreed, so a value may be wrong."
                    : " All agreed, so nothing was lost."));
        }

        var dropped = CanonicalModelValidator.StripEnumViolations(fragment, slice);
        if (dropped.Count > 0)
        {
            shortfalls.Add(
                $"Dropped {dropped.Count} value(s) the schema's enums do not allow: "
                + string.Join("; ", dropped.Take(5)) + (dropped.Count > 5 ? "; …" : string.Empty));
        }

        // These are losses, not failures: the fragment is usable and is kept. Reporting them
        // as failures is what made eight succeeding sections look broken.
        return (fragment, usage,
            shortfalls.Count == 0 ? null : string.Join(" ", shortfalls));
    }

    /// <summary>
    /// The extractor's standing instructions. The rules that matter are the ones that keep a
    /// later check honest: never invent a value, record contradictions instead of resolving
    /// them, and quote the text every assertion came from.
    /// </summary>
    private static string BuildSystemPrompt() => Prompts.ExtractorSystem;

    /// <summary>The property whose pass reports on the extraction as a whole.</summary>
    private const string ExtractionReportProperty = "extractionReport";

    private static string BuildSectionPrompt(
        ExtractionSection section,
        string schemaSlice,
        string documentText,
        string caseReference,
        CanonicalModelIdentityRegistry identity,
        JsonObject modelSoFar,
        IReadOnlyCollection<string> failedProperties)
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

        if (identity.HasEntities)
        {
            sb.AppendLine(identity.Table());
            sb.AppendLine();
        }

        // The self-report pass is the one pass that is about the extraction rather than the
        // report, and it used to be given no sight of what the other passes produced — it was
        // being asked to report on work it could not see.
        if (section.Properties.Contains(ExtractionReportProperty, StringComparer.Ordinal))
        {
            sb.AppendLine(SummariseExtraction(modelSoFar, failedProperties));
            sb.AppendLine();
        }

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
    /// What the previous passes produced, for the pass that reports on the extraction.
    ///
    /// Which paths came back empty is a walk over the merged object rather than a judgement,
    /// so the model is left to do the part that needs one: deciding whether an empty path is
    /// the report saying nothing or the extraction having missed it. Those mean opposite
    /// things to a check, and only one of them is a finding about the advice.
    /// </summary>
    internal static string SummariseExtraction(
        JsonObject modelSoFar, IReadOnlyCollection<string>? failedProperties = null)
    {
        var failed = failedProperties is null
            ? new List<string>()
            : failedProperties.OrderBy(x => x, StringComparer.Ordinal).ToList();

        var populated = new List<string>();
        var empty = new List<string>();

        foreach (var (name, value) in modelSoFar.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (name == "modelVersion")
            {
                continue;
            }

            (IsEmpty(value) ? empty : populated).Add(name);
        }

        var sb = new StringBuilder();
        sb.AppendLine("## What the previous passes produced");
        sb.AppendLine();
        sb.AppendLine($"Populated sections ({populated.Count}): {string.Join(", ", populated)}");
        sb.AppendLine(
            empty.Count == 0
                ? "Sections that came back empty: none."
                : $"Sections that came back empty ({empty.Count}): {string.Join(", ", empty)}");

        // A failed pass never wrote its key, so it was neither populated nor empty — it was
        // invisible here. In an observed run this summary said "came back empty: none" while
        // financialPosition and recommendations were missing outright, and the one pass whose
        // job is to report what the extraction missed reported neither, at 0.78 confidence.
        //
        // The distinction is the whole point: empty means the report was silent, failed means
        // we never read it. Those are opposite conclusions for a check to draw.
        sb.AppendLine(
            failed.Count == 0
                ? "Sections whose extraction pass failed: none."
                : $"Sections whose extraction pass failed ({failed.Count}): {string.Join(", ", failed)}");

        sb.AppendLine();
        sb.AppendLine(
            """
            Report on this extraction, not only on the report. A section that came back empty is
            "expectedButAbsent" only where the report was expected to say something — give the
            reason that applies. Where the report states two different figures for the same
            thing, that is an "internalInconsistency": list every one you can find, including
            any not implied by the summary above.

            A section whose pass FAILED is not the same as one that came back empty. The report
            was never read for it, so its absence says nothing about the advice. List each such
            property under "expectedButAbsent" with reason "PresentButUnparseable", and lower
            overallConfidence to reflect that those sections are missing altogether.
            """);

        return sb.ToString();
    }

    private static bool IsEmpty(JsonNode? node) => node switch
    {
        null => true,
        JsonArray array => array.Count == 0,
        JsonObject obj => obj.Count == 0,
        _ => false,
    };

    /// <summary>
    /// Records who produced the model and when, rather than asking the model to.
    ///
    /// Asked for these, an extraction run on 2026-08-13 using Claude Haiku 4.5 answered
    /// "2025-01-01T00:00:00Z" and "claude-opus" — a plausible guess in the two places the
    /// "never invent a value" rule could never have been enforced, because the model has no
    /// way to know either answer. The schema slice no longer asks (see
    /// <see cref="StripCodeOwnedFields"/>) and the process fills them in here.
    /// </summary>
    private void StampSource(JsonObject root, IReadOnlyList<string> reportFiles)
    {
        if (root["source"] is not JsonObject source)
        {
            root["source"] = source = new JsonObject();
        }

        source["extractedAt"] = DateTimeOffset.Now.ToString("O");
        source["extractorModel"] = _settings.SelectedModel;

        if (source["fileName"] is null && reportFiles.Count > 0)
        {
            source["fileName"] = Path.GetFileName(reportFiles[0]);
        }
    }

    /// <summary>
    /// Removes the fields the process owns from a schema slice, so the model is never asked
    /// for an answer it cannot have.
    /// </summary>
    internal static string StripCodeOwnedFields(string schemaSlice)
    {
        if (JsonNode.Parse(schemaSlice) is not JsonObject slice ||
            slice["properties"]?["source"]?["properties"] is not JsonObject source)
        {
            return schemaSlice;
        }

        source.Remove("extractedAt");
        source.Remove("extractorModel");

        return slice.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
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
    /// Reads the model's reply as a JSON object. Kept as the extractor's public entry point;
    /// the parsing itself lives in <see cref="ExtractionResponseReader"/> alongside the
    /// truncation handling it belongs with.
    /// </summary>
    public static JsonObject? ParseObject(string response) =>
        ExtractionResponseReader.ParseObject(response);

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
