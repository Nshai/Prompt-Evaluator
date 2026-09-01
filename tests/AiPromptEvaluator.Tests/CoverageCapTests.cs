using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The caps that decide what reaches an assessor, and the unbounded setting each of them takes.
///
/// <b>These were compile-time constants, and that is why the most consequential one was never
/// measured.</b> Raising the search limit from 8 to 16 bought two extra passages across a whole
/// run, because every group already sat at the twelve-passage cap and 86% of hits were discarded.
/// The cap was the untested lever and varying it meant a rebuild.
/// </summary>
public class CoverageCapTests
{
    // ── the unbounded convention ──────────────────────────────────────────────

    /// <summary>
    /// Zero, not -1. Every cap here counts things shown to a model, and zero of them is never a
    /// configuration anyone wants — so the value that would otherwise be a footgun carries the
    /// meaning that has no other spelling.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ZeroOrLessMeansUnbounded(int configured)
    {
        Assert.True(AppSettings.IsUnbounded(configured));
        Assert.Equal(int.MaxValue, AppSettings.Unbounded(configured));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(int.MaxValue)]
    public void APositiveCapIsItself(int configured)
    {
        Assert.False(AppSettings.IsUnbounded(configured));
        Assert.Equal(configured, AppSettings.Unbounded(configured));
    }

    // ── the defaults ──────────────────────────────────────────────────────────

    /// <summary>
    /// 24 rather than the 12 it was, because reserved slots ration the pack before ranking
    /// begins: the widest group declares four evidence categories and two evidence sections, so
    /// half its slots are spoken for. Reasoned, not measured — no run on record has varied it.
    /// </summary>
    [Fact]
    public void TheDefaultsAreTheDocumentedOnes()
    {
        var settings = new AppSettings();

        Assert.Equal(8, settings.MaxSearchResults);
        Assert.Equal(24, settings.MaxPassagesPerGroup);
        Assert.Equal(1, settings.ReservedSlotsPerTargetedCategory);
        Assert.Equal(1, settings.ReservedSlotsPerDeclaredSection);
        Assert.Equal(24_000, settings.ExtractionReportMaxChars);
        Assert.Equal(8000, settings.DecisionMaxTokens);
    }

    /// <summary>
    /// Every one of them has to survive a round trip through the settings file, or configuring
    /// it does nothing and the run reports a value it did not use.
    /// </summary>
    [Fact]
    public void EveryCapRoundTripsThroughTheSettingsFile()
    {
        var written = new AppSettings
        {
            MaxSearchResults = 0,
            MaxPassagesPerGroup = 0,
            ReservedSlotsPerTargetedCategory = 2,
            ReservedSlotsPerDeclaredSection = 0,
            ExtractionReportMaxChars = 0,
            DecisionMaxTokens = 0,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(written);
        var read = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json)!;

        Assert.Equal(0, read.MaxSearchResults);
        Assert.Equal(0, read.MaxPassagesPerGroup);
        Assert.Equal(2, read.ReservedSlotsPerTargetedCategory);
        Assert.Equal(0, read.ReservedSlotsPerDeclaredSection);
        Assert.Equal(0, read.ExtractionReportMaxChars);
        Assert.Equal(0, read.DecisionMaxTokens);

        Assert.Contains("\"maxPassagesPerGroup\"", json);
        Assert.Contains("\"reservedSlotsPerDeclaredSection\"", json);
    }

    // ── the cap that actually decides coverage ────────────────────────────────

    /// <summary>
    /// The whole point of making it a setting: a wider cap admits passages a narrower one drops.
    /// </summary>
    [Fact]
    public void AWiderCapKeepsMorePassages()
    {
        var passages = Passages(40, "B");

        Assert.Equal(12, CheckPlanRunner.Rank(passages, Targets("B"), settings: Cap(12)).Count);
        Assert.Equal(24, CheckPlanRunner.Rank(passages, Targets("B"), settings: Cap(24)).Count);
    }

    /// <summary>
    /// Unbounded returns the group's whole de-duplicated pool. Expensive in prompt tokens, and
    /// the only way to find out what the cap has been costing.
    /// </summary>
    [Fact]
    public void AnUnboundedCapKeepsEveryPassage()
    {
        var passages = Passages(40, "B");

        var ranked = CheckPlanRunner.Rank(passages, Targets("B"), settings: Cap(0));

        Assert.Equal(40, ranked.Count);
    }

    /// <summary>
    /// Unbounded must not disturb the ordering. A pack that keeps everything still has to hand
    /// the assessor the best evidence first.
    /// </summary>
    [Fact]
    public void AnUnboundedCapStillRanks()
    {
        var passages = Enumerable.Reverse(Passages(20, "B")).ToList();

        var ranked = CheckPlanRunner.Rank(passages, Targets("B"), settings: Cap(0));

        Assert.Equal(
            ranked.Select(p => p.Score).OrderByDescending(s => s),
            ranked.Select(p => p.Score));
    }

    /// <summary>
    /// The floor is a floor. Reserved slots survive a cap wide enough that nothing competes for
    /// them, and a run that turns them off gets pure score ordering back.
    /// </summary>
    [Fact]
    public void ReservedSlotsCanBeTurnedOff()
    {
        var passages = Passages(20, "I");
        passages.Add(Passage("Fact Find.md", "B", 0.01));

        var withFloor = CheckPlanRunner.Rank(
            passages, Targets("B", "I"), settings: Cap(12));
        var without = CheckPlanRunner.Rank(
            passages,
            Targets("B", "I"),
            settings: new AppSettings
            {
                MaxPassagesPerGroup = 12,
                ReservedSlotsPerTargetedCategory = 0,
                ReservedSlotsPerDeclaredSection = 0,
            });

        Assert.Contains(withFloor, p => p.CategoryCode == "B");
        Assert.DoesNotContain(without, p => p.CategoryCode == "B");
    }

    // ── the fingerprint has to say which run this was ─────────────────────────

    /// <summary>
    /// A run at an unbounded cap and a run at a large one are different runs. The reserved-slot
    /// counts are in the fingerprint for the same reason: they change what an assessor saw.
    /// </summary>
    [Fact]
    public void TheFingerprintNamesTheCapsAndSaysWhenOneIsUnbounded()
    {
        var line = RunFingerprint
            .For(new AppSettings { MaxPassagesPerGroup = 0 }, null, "nowhere", 10, 0)
            .Format();

        Assert.Contains("unbounded/group", line);
        Assert.Contains("reserving 1/section, 1/category", line);
    }

    // ──────────────────────────────────────────────

    private static AppSettings Cap(int maxPassagesPerGroup) =>
        new() { MaxPassagesPerGroup = maxPassagesPerGroup };

    private static IReadOnlySet<string> Targets(params string[] codes) =>
        codes.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static List<CaseDocumentSearchMatch> Passages(int count, string category) =>
        Enumerable
            .Range(0, count)
            .Select(i => Passage($"doc{i:00}.md", category, 0.90 - (i * 0.001)))
            .ToList();

    private static CaseDocumentSearchMatch Passage(string document, string category, double score) =>
        new($"passage from {document} with figures 1,234.00 and 5,678.00",
            "CASE-1", 99, document, category, category, score);
}
