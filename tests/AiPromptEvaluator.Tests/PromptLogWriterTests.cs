using System.IO;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// What a run's log has to say about the run itself.
///
/// Written after a run that could not be explained from its own log. Retrieval had collapsed to
/// two passages per search, and finding out why meant inferring the cause from the arithmetic of
/// hit counts and then checking a settings file that turned out to belong to a different
/// environment. The fingerprint that would have answered it in one line already existed — it was
/// only ever shown on screen.
/// </summary>
public class PromptLogWriterTests
{
    private static RunFingerprint Fingerprint(int maxSearchResults = 8, int extractionMaxTokens = 32000) =>
        new(
            ChatModel: "claude-haiku-4-5",
            EmbeddingModel: "text-embedding-3-small",
            Sampling: "temperature 0, top-p default, seed not pinned",
            StructuredFindings: true,
            MaxSearchResults: maxSearchResults,
            MaxPassagesPerGroup: 12,
            ReservedSlotsPerTargetedCategory: 1,
            ReservedSlotsPerDeclaredSection: 1,
            ReservedSlotsForTopScore: 1,
            NearDuplicateOverlap: 0.9,
            CoreQueriesOnly: false,
            IgnoreTriggerProbe: false,
            ExtractionMaxTokens: extractionMaxTokens,
            PlanCount: 10,
            PlanDigest: "abc123def456",
            CanonicalModelDigest: "0011223344ff",
            SchemaVersion: "1.0",
            SettingsDigest: "ffeeddccbbaa");

    private static string Write(Action<PromptLogWriter> use)
    {
        var folder = Path.Combine(Path.GetTempPath(), "AiPromptEvaluatorTests", Guid.NewGuid().ToString("N"));

        try
        {
            string path;
            using (var writer = new PromptLogWriter(folder, "ABC-99", DateTimeOffset.Now))
            {
                use(writer);
                path = writer.FilePath;
            }

            return File.ReadAllText(path);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    /// <summary>
    /// The settings that can change a finding must be readable from the log alone. Anyone
    /// reading it later has only the file.
    /// </summary>
    [Fact]
    public void LogRunConfiguration_RecordsTheSettingsThatCanChangeAFinding()
    {
        var log = Write(w => w.LogRunConfiguration(Fingerprint()));

        Assert.Contains("[RUN CONFIGURATION]", log, StringComparison.Ordinal);

        // The one that caused the investigation.
        Assert.Contains("top 8/search", log, StringComparison.Ordinal);

        // And the one that truncated an extraction section.
        Assert.Contains("extraction cap 32,000 tok", log, StringComparison.Ordinal);

        Assert.Contains("claude-haiku-4-5", log, StringComparison.Ordinal);
        Assert.Contains("temperature 0", log, StringComparison.Ordinal);
        Assert.Contains("12/group", log, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two runs that disagree should be separable by their configuration lines before anyone
    /// reads a single prompt — which is the whole point of recording them.
    /// </summary>
    [Fact]
    public void LogRunConfiguration_DistinguishesRunsThatDifferOnlyInSettings()
    {
        var starved = Write(w => w.LogRunConfiguration(Fingerprint(maxSearchResults: 1)));
        var normal = Write(w => w.LogRunConfiguration(Fingerprint(maxSearchResults: 8)));

        Assert.Contains("top 1/search", starved, StringComparison.Ordinal);
        Assert.Contains("top 8/search", normal, StringComparison.Ordinal);
    }

    /// <summary>
    /// Configuration first, so a run that is cancelled or fails still says what it was set up
    /// to do. A log that only records settings on success loses them exactly when they matter.
    /// </summary>
    [Fact]
    public void LogRunConfiguration_PrecedesTheFirstExchange()
    {
        var log = Write(w =>
        {
            w.LogRunConfiguration(Fingerprint());
            w.LogExchange("CHK-001", "Suitability completeness", "system", "user", "response");
        });

        Assert.True(
            log.IndexOf("[RUN CONFIGURATION]", StringComparison.Ordinal)
                < log.IndexOf("[SYSTEM PROMPT]", StringComparison.Ordinal),
            "The configuration must be written before the first prompt.");
    }

    /// <summary>The case and start time were already recorded; they must survive the addition.</summary>
    [Fact]
    public void Header_StillIdentifiesTheCaseAndWhenTheRunStarted()
    {
        var log = Write(w => w.LogRunConfiguration(Fingerprint()));

        Assert.Contains("Case: ABC-99", log, StringComparison.Ordinal);
        Assert.Contains("Run started:", log, StringComparison.Ordinal);
    }
}
