using AiPromptEvaluator;

using Microsoft.Extensions.AI;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The three cost levers that do not trade against quality.
///
/// One call per requirement is a deliberate cost pessimisation: it buys a pack small enough to
/// attend to in full, a failure isolated to the group that caused it, and no group's reasoning
/// conditioned on the groups before it. The runner's own note says what was supposed to pay for
/// it — <i>"keeps the check header identical at the front of every prompt so the provider's prefix
/// cache covers it"</i> — and nothing has ever measured whether it did.
/// </summary>
public class PromptCostTests
{
    // ──────────────────────────────────────────────
    // The prefix ledger — what the repetition costs
    // ──────────────────────────────────────────────

    [Fact]
    public void TheSharedPrefixIsMeasuredFromThePromptsActuallySent()
    {
        var ledger = new PromptPrefixLedger();
        var header = new string('h', 4000);

        ledger.Record("CHK-001", "system", header + " group one asks about charges");
        ledger.Record("CHK-001", "system", header + " group two asks about risk");
        ledger.Record("CHK-001", "system", header + " group three asks about costs");

        var check = Assert.Single(ledger.Checks);

        Assert.Equal(3, check.Calls);
        Assert.True(check.SharedPrefixTokens > 0, "The shared header should have been detected.");

        // The prefix was sent three times and needed sending once.
        Assert.Equal(check.SharedPrefixTokens * 2, check.RepeatedTokens);
    }

    /// <summary>
    /// Two checks share no header, so neither may be credited with the other's prefix — a ledger
    /// that pooled them would report a saving a single breakpoint could not deliver.
    /// </summary>
    [Fact]
    public void PrefixesAreMeasuredPerCheckNotAcrossTheRun()
    {
        var ledger = new PromptPrefixLedger();

        ledger.Record("CHK-001", "system", new string('a', 2000) + " one");
        ledger.Record("CHK-001", "system", new string('a', 2000) + " two");
        ledger.Record("CHK-002", "system", new string('b', 2000) + " three");

        var checks = ledger.Checks.ToDictionary(c => c.CheckId);

        Assert.Equal(2, checks["CHK-001"].Calls);
        Assert.Equal(1, checks["CHK-002"].Calls);

        // A single call repeats nothing, however long its prompt.
        Assert.Equal(0, checks["CHK-002"].RepeatedTokens);
    }

    /// <summary>
    /// The system prompt is identical on every call in a run, so it is the one prefix a single
    /// breakpoint covers across all 88 of them.
    /// </summary>
    [Fact]
    public void TheSystemPromptIsCountedOncePerRunNotOncePerCheck()
    {
        var ledger = new PromptPrefixLedger();
        var system = new string('s', 3000);

        ledger.Record("CHK-001", system, "one");
        ledger.Record("CHK-002", system, "two");

        Assert.True(ledger.SystemPromptTokens > 0);
        Assert.Equal(2, ledger.Calls);
    }

    /// <summary>
    /// A provider that does not cache is reported as saving nothing, rather than as saving what it
    /// would have saved somewhere else.
    /// </summary>
    [Fact]
    public void AProviderThatDoesNotCacheIsReportedAsSavingNothing()
    {
        var ledger = new PromptPrefixLedger();
        var header = new string('h', 4000);

        ledger.Record("CHK-001", "system", header + " one");
        ledger.Record("CHK-001", "system", header + " two");

        var text = ledger.Format(cacheReadMultiplier: 1.0, cacheWriteMultiplier: 1.0);

        Assert.Contains("would net 0 input tokens", text);
    }

    [Fact]
    public void AnEmptyLedgerWritesNothing() =>
        Assert.True(new PromptPrefixLedger().IsEmpty);

    [Theory]
    [InlineData(new[] { "abcdef", "abcxyz" }, "abc")]
    [InlineData(new[] { "same", "same" }, "same")]
    [InlineData(new[] { "abc", "xyz" }, "")]
    [InlineData(new[] { "abc" }, "abc")]
    public void TheCommonPrefixIsTheTextEveryPromptBeginsWith(string[] values, string expected) =>
        Assert.Equal(expected, PromptPrefixLedger.LongestCommonPrefix(values));

    // ──────────────────────────────────────────────
    // Cache accounting
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>Cache writes were hard-coded to zero.</b> That is right for a provider that never
    /// charges for populating a cache and wrong for the one family that does — Anthropic bills a
    /// write at 1.25x and a read at 0.1x, so a run that cached was reporting its first call as
    /// ordinary input and every later one as free.
    /// </summary>
    [Fact]
    public void CacheCreationTokensAreReadAndPricedSeparately()
    {
        var usage = new UsageDetails
        {
            InputTokenCount = 10_000,
            OutputTokenCount = 500,
            AdditionalCounts = new AdditionalPropertiesDictionary<long>
            {
                ["cache_creation_input_tokens"] = 4_000,
                ["cache_read_input_tokens"] = 3_000,
            },
        };

        var read = ChatCompletionClient.ReadUsage(usage);

        Assert.Equal(4_000, read.CacheWriteTokens);
        Assert.Equal(3_000, read.CacheReadTokens);
        Assert.Equal(3_000, read.InputTokens);
        Assert.Equal(500, read.OutputTokens);
    }

    /// <summary>
    /// The OpenAI-compatible spelling still works, and reports no write — those endpoints cache
    /// automatically and bill nothing for it.
    /// </summary>
    [Fact]
    public void TheOpenAiCachedTokenCountStillReads()
    {
        var usage = new UsageDetails
        {
            InputTokenCount = 8_000,
            AdditionalCounts = new AdditionalPropertiesDictionary<long> { ["cached_tokens"] = 5_000 },
        };

        var read = ChatCompletionClient.ReadUsage(usage);

        Assert.Equal(5_000, read.CacheReadTokens);
        Assert.Equal(0, read.CacheWriteTokens);
        Assert.Equal(3_000, read.InputTokens);
    }

    /// <summary>
    /// <b>The two families disagree about what "input" includes.</b> OpenAI counts cached tokens
    /// inside its input total; Bedrock reports them alongside it — a fully cached Converse call
    /// comes back as inputTokens 0 with cacheReadInputTokens 4,902. Subtracting there would erase
    /// tokens that were charged at the full rate, so containment decides, and a cached count
    /// larger than the input total cannot have been inside it.
    /// </summary>
    [Fact]
    public void CachedCountsReportedAlongsideTheInputTotalAreNotSubtracted()
    {
        var usage = new UsageDetails
        {
            InputTokenCount = 100,
            AdditionalCounts = new AdditionalPropertiesDictionary<long>
            {
                ["cache_read_input_tokens"] = 9_000,
            },
        };

        Assert.Equal(100, ChatCompletionClient.ReadUsage(usage).InputTokens);
    }

    // ──────────────────────────────────────────────
    // Per-group pack budgets
    // ──────────────────────────────────────────────

    private static PlanQueryGroup Group(int? maxPassages) => new()
    {
        GroupId = "G1.1",
        Retrieval = new PlanRetrieval { MaxPassages = maxPassages },
    };

    /// <summary>
    /// 53 of 88 groups sit at the cap while CHK-007 reaches eight categories and CHK-010 reaches
    /// three. One global number prices them as though they were alike, and the pack is 54–65% of
    /// a run's chat tokens.
    /// </summary>
    [Fact]
    public void APlanMayNarrowTheGlobalCap() =>
        Assert.Equal(8, Group(8).PassageCap(24));

    /// <summary>
    /// <b>But never widen it.</b> A budget is a ceiling somebody configured; a plan file must not
    /// be able to raise the bill of a run without the run's consent.
    /// </summary>
    [Fact]
    public void APlanMayNotWidenIt() =>
        Assert.Equal(12, Group(40).PassageCap(12));

    [Fact]
    public void APlanThatSaysNothingTakesTheGlobalSetting() =>
        Assert.Equal(24, Group(null).PassageCap(24));

    /// <summary>Zero means unbounded on the setting, so any plan budget is narrower.</summary>
    [Fact]
    public void APlanBudgetBindsWhenTheGlobalCapIsUnbounded() =>
        Assert.Equal(10, Group(10).PassageCap(0));

    /// <summary>The plan's budget actually reaches ranking, not just the accessor.</summary>
    [Fact]
    public void TheBudgetIsHonouredWhenThePackIsBuilt()
    {
        var passages = Enumerable.Range(0, 30)
            .Select(i => new CaseDocumentSearchMatch(
                SearchedText: $"passage {i}",
                CaseReference: "C",
                TenantId: 1,
                DocumentName: $"doc{i}.md",
                CategoryCode: "B",
                CategoryName: "Fact find",
                Score: 1d - (i / 100d)))
            .ToList();

        var settings = new AppSettings { MaxPassagesPerGroup = 24 };

        Assert.Equal(24, CheckPlanRunner.Rank(passages, new HashSet<string>(), settings: settings).Count);
        Assert.Equal(6, CheckPlanRunner.Rank(passages, new HashSet<string>(), settings: settings, planned: 6).Count);
    }
}
