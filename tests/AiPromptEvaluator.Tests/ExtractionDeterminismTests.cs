using System.Text.RegularExpressions;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The passes run in dependency waves, concurrently within a wave, to cut a run's wall-clock. The
/// promise that makes that safe is that it changes a run's duration and never its conclusions: the
/// model built from six passes in flight at once must be byte-for-byte the model built from twelve
/// run in series.
///
/// This drives the real <see cref="CanonicalModelExtractor"/> with a scripted chat client — no
/// network — so the only thing varying between the two runs is <see cref="AppSettings.MaxParallelRequests"/>.
/// The scripted client delays each section by a different amount so the concurrent run genuinely
/// interleaves the passes out of their canonical order; if the merge or id-adoption depended on
/// completion order, this is where the two models would diverge.
/// </summary>
public class ExtractionDeterminismTests
{
    /// <summary>
    /// A chat client that answers each extraction pass with a fixed fragment for its section,
    /// after a per-section delay. The fragments carry the cross-references the identity table is
    /// there to keep consistent — a recommendation points back at an objective — so the run
    /// exercises id adoption across waves and not just independent sections.
    /// </summary>
    private sealed class ScriptedExtractor(AppSettings settings) : ChatCompletionClient(settings)
    {
        private readonly string _model = settings.SelectedModel;

        public override async Task<ChatCompletionResult> RunRawAsync(
            string systemPrompt,
            string userPrompt,
            int maxOutputTokens,
            Microsoft.Extensions.AI.ChatResponseFormat? responseFormat,
            CancellationToken cancellationToken = default)
        {
            var section = Regex.Match(userPrompt, @"## Extract: (.+)").Groups[1].Value.Trim();

            // Out of canonical order on purpose: the section defined first is answered slowest, so a
            // concurrent wave finishes its passes in a different order than it started them.
            await Task.Delay(DelayFor(section), cancellationToken).ConfigureAwait(false);

            return new ChatCompletionResult(FragmentFor(section), CostBreakdown.Create(_model, TokenUsage.Empty));
        }

        private static int DelayFor(string section) => 40 - Math.Min(30, section.Length);

        private static string FragmentFor(string section) => section switch
        {
            "Case and parties" =>
                """{ "parties": { "clients": [ { "clientId": "P1", "fullName": "A. Client" } ] } }""",
            "Objectives" =>
                """{ "objectives": [ { "objectiveId": "O1", "summary": "Retire at 60" } ] }""",
            "Recommendations" =>
                """{ "recommendations": [ { "recommendationId": "R1", "linkedObjectiveIds": ["O1"], "summary": "Consolidate" } ] }""",
            "Solution" =>
                """{ "solution": { "recommendedPlans": [ { "planId": "N1", "productName": "A Plan" } ] } }""",
            "Existing arrangements" =>
                """{ "existingArrangements": [ { "arrangementId": "E1", "planName": "Old Plan" } ] }""",
            "Triggers and extraction report" =>
                """{ "checkTriggers": { "hasComplexProduct": false }, "extractionReport": { "overallConfidence": 0.9 } }""",
            // The remaining passes contribute a small, valid, cross-reference-free fragment each.
            _ => "{ }",
        };
    }

    private static AppSettings SettingsWith(int maxParallel) => new()
    {
        SelectedModel = "test-model",
        TenantId = 99,
        MaxParallelRequests = maxParallel,
        BypassResponseCache = false,
        CanonicalSchemaPath = LocateSchema(),
    };

    private static string WriteCaseFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "extract-determinism-" + Guid.NewGuid().ToString("n"));
        var reports = Path.Combine(folder, CanonicalModelExtractor.SuitabilityReportCategoryCode);
        Directory.CreateDirectory(reports);
        File.WriteAllText(Path.Combine(reports, "report.md"), "# Suitability report\n\nA. Client wishes to retire at 60.\n");
        return folder;
    }

    [Fact]
    public async Task TheParallelRunProducesTheSameModelAsTheSequentialRun()
    {
        var caseFolder = WriteCaseFolder();
        try
        {
            var sequential = new ScriptedExtractor(SettingsWith(1));
            var parallel = new ScriptedExtractor(SettingsWith(6));

            var serialResult = await new CanonicalModelExtractor(SettingsWith(1), sequential)
                .ExtractAsync(caseFolder, "ABC-99");
            var parallelResult = await new CanonicalModelExtractor(SettingsWith(6), parallel)
                .ExtractAsync(caseFolder, "ABC-99");

            // The stored model is byte-identical: same merge, same canonical ids, same order. The
            // one field that legitimately differs between any two runs is source.extractedAt, the
            // wall-clock stamp StampSource writes — normalised away here so the comparison is about
            // what the passes produced, not when they ran.
            Assert.Equal(
                WithoutExtractedAt(serialResult.Document.Json),
                WithoutExtractedAt(parallelResult.Document.Json));

            // And the run's other conclusions are the same — the failure list and the corrections
            // are built in the wave's fixed order, not completion order.
            Assert.Equal(
                serialResult.Failures.Select(f => f.Section),
                parallelResult.Failures.Select(f => f.Section));
            Assert.Equal(
                serialResult.VocabularyCorrections.Select(c => c.ToString()),
                parallelResult.VocabularyCorrections.Select(c => c.ToString()));
        }
        finally
        {
            Directory.Delete(caseFolder, recursive: true);
        }
    }

    /// <summary>The cross-reference survives the concurrent run: the recommendation still points at the objective.</summary>
    [Fact]
    public async Task ACrossReferenceIsResolvedTheSameWayUnderParallelism()
    {
        var caseFolder = WriteCaseFolder();
        try
        {
            var parallel = new ScriptedExtractor(SettingsWith(6));
            var result = await new CanonicalModelExtractor(SettingsWith(6), parallel)
                .ExtractAsync(caseFolder, "ABC-99");

            // No dangling-reference failure: the objective the recommendation links to resolved.
            Assert.DoesNotContain(result.Failures, f => f.Section == "Cross-references");
        }
        finally
        {
            Directory.Delete(caseFolder, recursive: true);
        }
    }

    /// <summary>The model JSON with the wall-clock stamp removed, so two runs can be compared on their content.</summary>
    private static string WithoutExtractedAt(string modelJson) =>
        Regex.Replace(modelJson, "\"extractedAt\": \"[^\"]*\",?\\s*", string.Empty);

    private static string LocateSchema()
    {
        var deployed = Path.Combine(AppContext.BaseDirectory, AppSettings.DefaultCanonicalSchemaFileName);
        if (File.Exists(deployed))
        {
            return deployed;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, "docs", "artifacts", AppSettings.DefaultCanonicalSchemaFileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the canonical model schema for the test.");
    }
}
