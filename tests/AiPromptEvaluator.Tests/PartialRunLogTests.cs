using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// A log that stops has to say it stopped.
///
/// <b>An observed extraction log ended after eight of twelve passes with no marker of any
/// kind.</b> Every one of the eight parsed and merged, and the file simply ran out — no summary
/// block, no failure list, no canonical model. The only evidence it had been cancelled was that
/// another run started fifteen seconds after its last response, which is a fact a reader has to
/// go looking for rather than one the log tells them. Downstream, a partial log is
/// indistinguishable from a complete one, and a partial model left behind by a killed run is
/// indistinguishable from a finished one.
/// </summary>
public class PartialRunLogTests
{
    [Fact]
    public void ACancelledRunSaysSoInItsOwnLog()
    {
        var (log, path) = NewLog();

        using (log)
        {
            log.LogRunEnded("Cancelled after 8 of 12 extraction passes.");
        }

        var text = File.ReadAllText(path);

        Assert.Contains("Run ended without completing", text);
        Assert.Contains("Cancelled after 8 of 12 extraction passes.", text);
        Assert.Contains("Nothing was stored.", text);
    }

    /// <summary>
    /// The marker has to be findable the same way the section banners are, so whatever reads
    /// these logs finds it without a second parsing rule.
    /// </summary>
    [Fact]
    public void TheMarkerUsesTheSameBannerAsEverySectionHeading()
    {
        var (log, path) = NewLog();

        using (log)
        {
            log.LogRunEnded("Failed after 3 of 12 extraction passes: the disk went away.");
        }

        var lines = File.ReadAllLines(path);
        var banner = new string('=', 100);
        var heading = Array.FindIndex(lines, l => l.StartsWith("Run ended without completing"));

        Assert.True(heading > 0, "The heading was not written.");
        Assert.Equal(banner, lines[heading - 1]);
        Assert.Equal(banner, lines[heading + 1]);
    }

    /// <summary>
    /// A run that finished writes the summary block instead, and must not also claim it stopped.
    /// </summary>
    [Fact]
    public void ACompletedRunCarriesNoSuchMarker()
    {
        var (log, path) = NewLog();

        using (log)
        {
            log.LogCanonicalModel(
                new CanonicalModelDocument(
                    CaseReference: "ABC-99",
                    TenantId: 99,
                    Json: "{}",
                    ModelId: "claude-haiku-4-5",
                    SchemaVersion: "1.0",
                    SourceDocuments: ["Suitability Report Test 1.md"],
                    ExtractedAt: DateTimeOffset.Now,
                    Usage: TokenUsage.Empty),
                []);
        }

        var text = File.ReadAllText(path);

        Assert.DoesNotContain("Run ended without completing", text);
        Assert.Contains("Canonical model extracted", text);
    }

    // ──────────────────────────────────────────────

    private static (PromptLogWriter Log, string Path) NewLog()
    {
        var folder = Path.Combine(Path.GetTempPath(), "apeval-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        var log = new PromptLogWriter(folder, "ABC-99", DateTimeOffset.Now, "extract");

        return (log, Directory.GetFiles(folder).Single());
    }
}
