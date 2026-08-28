using System.Reflection;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Every setting that can change a finding must reach the fingerprint.
///
/// <b>The fingerprint exists so that two runs which disagree can be diffed on their inputs before
/// anyone blames the model, and a setting it omits defeats that silently, in the direction that
/// wastes the most time.</b> A run with core-only queries and a run with all of them retrieve
/// different passages and printed the same fingerprint. Anyone comparing them was comparing two
/// pipelines while reading a line that said they were the same one.
///
/// Four settings were in that position when this was written, two added the same week.
///
/// The answer is a digest computed by reflection with an <b>opt-out</b> list, so a setting added
/// next month is covered by default. Named fields stay on the record as well, because a reader
/// acts on "24/group, reserving 1/top score" and cannot act on a hash — the hash is what catches
/// the fields nobody thought to name.
/// </summary>
public class FingerprintCoverageTests
{
    // ── the digest ────────────────────────────────────────────────────────────

    /// <summary>
    /// The property this whole class is about: change something that reaches retrieval, and the
    /// digest moves without anyone having remembered to add it anywhere.
    /// </summary>
    [Fact]
    public void ChangingASettingThatReachesRetrievalMovesTheDigest()
    {
        var baseline = RunFingerprint.DigestOfSettings(new AppSettings());

        Assert.NotEqual(baseline, RunFingerprint.DigestOfSettings(new AppSettings { CoreQueriesOnly = true }));
        Assert.NotEqual(baseline, RunFingerprint.DigestOfSettings(new AppSettings { IgnoreTriggerProbe = true }));
        Assert.NotEqual(baseline, RunFingerprint.DigestOfSettings(new AppSettings { ReservedSlotsForTopScore = 0 }));
        Assert.NotEqual(baseline, RunFingerprint.DigestOfSettings(new AppSettings { NearDuplicateOverlap = 1.0 }));
        Assert.NotEqual(baseline, RunFingerprint.DigestOfSettings(new AppSettings { MaxTokensPerChunk = 400 }));
        Assert.NotEqual(baseline, RunFingerprint.DigestOfSettings(new AppSettings { MaxPassagesPerGroup = 12 }));
    }

    /// <summary>
    /// And the exclusions hold: the same run against the same models through a different gateway,
    /// or on a different machine's folders, is the same run.
    /// </summary>
    [Fact]
    public void ChangingACredentialOrAPathDoesNotMoveTheDigest()
    {
        var baseline = RunFingerprint.DigestOfSettings(new AppSettings());

        Assert.Equal(baseline, RunFingerprint.DigestOfSettings(new AppSettings { OpenAiApiKey = "secret" }));
        Assert.Equal(baseline, RunFingerprint.DigestOfSettings(new AppSettings { OpenAiBaseUrl = "https://elsewhere" }));
        Assert.Equal(baseline, RunFingerprint.DigestOfSettings(new AppSettings { PromptLogFolder = @"D:\logs" }));
        Assert.Equal(baseline, RunFingerprint.DigestOfSettings(new AppSettings { CaseReference = "OTHER-1" }));
        Assert.Equal(baseline, RunFingerprint.DigestOfSettings(new AppSettings { MaxParallelChecks = 2 }));
    }

    /// <summary>
    /// The digest must not depend on the order reflection returns properties in — that order is
    /// unspecified, and a fingerprint that differed between two processes running the same build
    /// would be worse than none.
    /// </summary>
    [Fact]
    public void TheDigestIsStableAcrossCalls()
    {
        Assert.Equal(
            RunFingerprint.DigestOfSettings(new AppSettings()),
            RunFingerprint.DigestOfSettings(new AppSettings()));
    }

    /// <summary>
    /// The exclusions have to name settings that still exist. A rename would otherwise leave a
    /// stale entry excusing nothing while the real setting quietly rejoined the digest — or, if
    /// the name were reused, excusing something nobody argued for.
    /// </summary>
    [Fact]
    public void TheExclusionsNameSettingsThatExist()
    {
        var names = typeof(AppSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(AppSettings.NotFingerprinted, n => Assert.Contains(n, names));
    }

    /// <summary>
    /// The exclusion list is a claim about three categories, and this is the reminder that it is
    /// one. It fixes the size of the list, so growing it is a deliberate edit here with a reason
    /// beside it rather than a quiet addition somebody makes to get a test to pass.
    /// </summary>
    [Fact]
    public void TheExclusionListHasNotGrownWithoutAnArgument()
    {
        Assert.Equal(20, AppSettings.NotFingerprinted.Count);
    }

    /// <summary>
    /// Nothing named as a cap, floor, threshold, scope or budget may be excluded. That is the
    /// whole shape the fingerprint is for, and the exclusion list's three categories —
    /// credentials, case identity, concurrency — contain none of it.
    /// </summary>
    [Theory]
    [InlineData("Max")]
    [InlineData("Reserved")]
    [InlineData("Overlap")]
    [InlineData("Only")]
    [InlineData("Pin")]
    public void NoTuningSettingIsExcluded(string shape)
    {
        var excluded = AppSettings.NotFingerprinted
            .Where(n => n.Contains(shape, StringComparison.Ordinal))
            // The concurrency limits are the argued exception, and they are the only one.
            .Where(n => n is not (nameof(AppSettings.MaxParallelRequests)
                              or nameof(AppSettings.MaxParallelChecks)))
            .ToList();

        Assert.Empty(excluded);
    }

    // ── the readable fields ───────────────────────────────────────────────────

    /// <summary>
    /// The run scope reaches the reader as words rather than as two booleans. "Core queries only"
    /// is something a reviewer can act on; a flag inside a record is not on the page at all.
    /// </summary>
    [Fact]
    public void TheRunScopeIsPrinted()
    {
        Assert.Contains("Core queries only", Fingerprint(coreOnly: true).Format());
        Assert.Contains("Core and Supplementary", Fingerprint().Format());
        Assert.Contains("trigger probes bypassed", Fingerprint(ignoreTrigger: true).Format());
        Assert.Contains("trigger probes honoured", Fingerprint().Format());
    }

    /// <summary>A pass that is switched off says "off" rather than printing a number.</summary>
    [Fact]
    public void ADisabledNearDuplicatePassReadsAsOff()
    {
        Assert.Contains("near-duplicate off", Fingerprint(overlap: 1.0).Format());
        Assert.Contains("near-duplicate 0.90", Fingerprint().Format());
    }

    // ── the variance note ─────────────────────────────────────────────────────

    /// <summary>
    /// A run whose sampling is not pinned says what that costs, in the numbers it was measured at.
    ///
    /// Two runs of one case with identical fingerprints disagreed on 3 of 78 outcomes and 12 of 69
    /// severities. Every change anyone proposes to this pipeline claims an effect of about that
    /// size, so the floor belongs beside the fingerprint rather than in a document — a reviewer
    /// diffing two runs will otherwise read three moved findings as a result.
    ///
    /// It is not always pinnable: some gateways reject the seed parameter outright, which is why
    /// the sampling line names the parameters that were actually sent rather than the ones that
    /// were configured.
    /// </summary>
    [Fact]
    public void AnUnpinnedRunWarnsThatTwoOfThemWillDisagree()
    {
        var unpinned = Fingerprint("temperature default, top-p default, seed not pinned");

        Assert.Contains("two runs of this configuration will differ", unpinned.Variance);
        Assert.Contains("score two runs each side", unpinned.Variance);
        Assert.Contains(unpinned.Variance, unpinned.Format());
    }

    [Fact]
    public void APinnedRunSaysSoAndSaysLess()
    {
        var pinned = Fingerprint("temperature 0, top-p default, seed 1");

        Assert.Contains("Sampling is pinned", pinned.Variance);
        Assert.DoesNotContain("3 of 78", pinned.Variance);
    }

    // ──────────────────────────────────────────────

    private static RunFingerprint Fingerprint(
        string sampling = "temperature 0, top-p default, seed 1",
        bool coreOnly = false,
        bool ignoreTrigger = false,
        double overlap = 0.9) =>
        new(
            ChatModel: "a-model",
            EmbeddingModel: "an-embedding-model",
            Sampling: sampling,
            StructuredFindings: true,
            MaxSearchResults: 8,
            MaxPassagesPerGroup: 24,
            ReservedSlotsPerTargetedCategory: 1,
            ReservedSlotsPerDeclaredSection: 1,
            ReservedSlotsForTopScore: 1,
            NearDuplicateOverlap: overlap,
            CoreQueriesOnly: coreOnly,
            IgnoreTriggerProbe: ignoreTrigger,
            ExtractionMaxTokens: 32_000,
            PlanCount: 10,
            PlanDigest: "abc123def456",
            CanonicalModelDigest: "0011223344ff",
            SchemaVersion: "1.0",
            SettingsDigest: "ffeeddccbbaa");
}
