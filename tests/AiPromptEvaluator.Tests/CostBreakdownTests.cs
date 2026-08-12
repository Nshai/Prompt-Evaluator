using Xunit;

namespace AiPromptEvaluator.Tests;

public class CostBreakdownTests
{
    [Fact]
    public void Create_PricesEachComponentSeparately()
    {
        // gpt-4.1: $2 input / $8 output per million tokens.
        var usage = new TokenUsage(
            InputTokens: 1_000_000,
            OutputTokens: 1_000_000,
            CacheWriteTokens: 1_000_000,
            CacheReadTokens: 1_000_000);

        var breakdown = CostBreakdown.Create("gpt-4.1", usage);

        Assert.Equal(4, breakdown.Lines.Count);
        Assert.Equal(2.00m, LineCost(breakdown, "Input (uncached)"));
        Assert.Equal(0m, LineCost(breakdown, "Cache write"));      // writing to the cache is free
        Assert.Equal(0.50m, LineCost(breakdown, "Cache read"));    // 0.25x input
        Assert.Equal(8.00m, LineCost(breakdown, "Output"));
        Assert.Equal(10.50m, breakdown.TotalCost);
        Assert.False(breakdown.RatesAreEstimated);
    }

    [Fact]
    public void Create_UsesModelSpecificRates()
    {
        var usage = new TokenUsage(1_000_000, 0, 0, 0);

        Assert.Equal(0.15m, CostBreakdown.Create("gpt-4o-mini", usage).TotalCost);
        Assert.Equal(2.00m, CostBreakdown.Create("gpt-4.1", usage).TotalCost);
        Assert.Equal(2.50m, CostBreakdown.Create("gpt-4o", usage).TotalCost);
        Assert.Equal(0.02m, CostBreakdown.Create("text-embedding-3-small", usage).TotalCost);
        Assert.Equal(1.00m, CostBreakdown.Create("claude-haiku-4-5", usage).TotalCost);
        Assert.Equal(3.00m, CostBreakdown.Create("claude-sonnet-4-6", usage).TotalCost);
        Assert.Equal(3.00m, CostBreakdown.Create("claude-sonnet-5", usage).TotalCost);
    }

    /// <summary>
    /// Cache multipliers are per provider, not global: OpenAI doesn't bill a write and
    /// serves cached input at 0.25x, Anthropic bills 1.25x to write and 0.1x to read.
    /// </summary>
    [Fact]
    public void Create_PricesCacheTokensByProvider()
    {
        var usage = new TokenUsage(0, 0, CacheWriteTokens: 1_000_000, CacheReadTokens: 1_000_000);

        var openAi = CostBreakdown.Create("gpt-4.1", usage);
        Assert.Equal(0m, LineCost(openAi, "Cache write"));
        Assert.Equal(0.50m, LineCost(openAi, "Cache read"));

        // claude-sonnet-5: $3 input → $3.75 to write, $0.30 to read.
        var anthropic = CostBreakdown.Create("claude-sonnet-5", usage);
        Assert.Equal(3.75m, LineCost(anthropic, "Cache write"));
        Assert.Equal(0.30m, LineCost(anthropic, "Cache read"));
    }

    [Fact]
    public void Create_FlagsUnknownModelsAsEstimated()
    {
        var breakdown = CostBreakdown.Create("some-future-model", new TokenUsage(1_000, 0, 0, 0));

        Assert.True(breakdown.RatesAreEstimated);
        Assert.True(breakdown.TotalCost > 0m);
    }

    /// <summary>Gateways commonly prefix or date-suffix the model id; the rates still apply.</summary>
    [Fact]
    public void Create_MatchesDatedModelAliases()
    {
        var breakdown = CostBreakdown.Create("openai/gpt-4o-2024-11-20", new TokenUsage(1_000_000, 0, 0, 0));

        Assert.False(breakdown.RatesAreEstimated);
        Assert.Equal(2.50m, breakdown.TotalCost);
    }

    [Fact]
    public void Empty_HasZeroCostAndZeroTokens()
    {
        var breakdown = CostBreakdown.Empty("gpt-4.1");

        Assert.Equal(0m, breakdown.TotalCost);
        Assert.Equal(0, breakdown.Usage.TotalTokens);
        Assert.All(breakdown.Lines, line => Assert.Equal(0m, line.Cost));
    }

    // ──────────────────────────────────────────────
    // Embeddings
    // ──────────────────────────────────────────────

    /// <summary>
    /// Embeddings are a different model on a different rate card, so they are priced on their
    /// own rate rather than the chat model's — pricing 1M embedding tokens at the chat rate
    /// would overstate the cost of indexing a case by two orders of magnitude.
    /// </summary>
    [Fact]
    public void Create_PricesEmbeddingsOnTheirOwnRate()
    {
        var breakdown = CostBreakdown.Create(
            "gpt-4.1",
            new TokenUsage(1_000_000, 0, 0, 0),
            new EmbeddingUsage("text-embedding-3-small", 1_000_000, UsageReported: true));

        Assert.Equal(2.00m, breakdown.ChatCost);          // gpt-4.1 input
        Assert.Equal(0.02m, breakdown.EmbeddingCost);     // text-embedding-3-small
        Assert.Equal(2.02m, breakdown.TotalCost);
        Assert.Equal(2_000_000, breakdown.TotalTokens);
    }

    /// <summary>Indexing bills no chat tokens at all — the whole cost is the embeddings.</summary>
    [Fact]
    public void Create_PricesAnIndexingRunWithNoChatSpend()
    {
        var breakdown = CostBreakdown.Create(
            "gpt-4.1",
            TokenUsage.Empty,
            new EmbeddingUsage("text-embedding-3-small", 5_000_000, UsageReported: true));

        Assert.Equal(0m, breakdown.ChatCost);
        Assert.Equal(0.10m, breakdown.EmbeddingCost);
        Assert.Equal(0.10m, breakdown.TotalCost);
    }

    /// <summary>A screen that never embeds should not carry a permanent zero row.</summary>
    [Fact]
    public void Create_OmitsTheEmbeddingRow_WhenNothingWasEmbedded()
    {
        var breakdown = CostBreakdown.Create("gpt-4.1", new TokenUsage(1_000, 0, 0, 0));

        Assert.Equal(4, breakdown.Lines.Count);
        Assert.Equal(0m, breakdown.EmbeddingCost);
        Assert.False(breakdown.Embeddings.IsPresent);
        Assert.DoesNotContain(breakdown.Lines, line => line.Component.StartsWith("Embeddings"));
    }

    [Fact]
    public void Create_NamesTheEmbeddingModelOnItsRow()
    {
        var breakdown = CostBreakdown.Create(
            "gpt-4.1",
            TokenUsage.Empty,
            new EmbeddingUsage("text-embedding-3-large", 1_000_000, UsageReported: true));

        var row = Assert.Single(breakdown.Lines, line => line.Component.StartsWith("Embeddings"));
        Assert.Contains("text-embedding-3-large", row.Component);
        Assert.Equal(0.13m, row.Cost);
    }

    /// <summary>An unknown embedding model makes the whole breakdown an estimate.</summary>
    [Fact]
    public void Create_FlagsAnUnknownEmbeddingModelAsEstimated()
    {
        var breakdown = CostBreakdown.Create(
            "gpt-4.1",
            TokenUsage.Empty,
            new EmbeddingUsage("some-local-embedder", 1_000, UsageReported: true));

        Assert.True(breakdown.RatesAreEstimated);
    }

    [Fact]
    public void FormatTotal_SplitsChatFromEmbeddings()
    {
        var text = CostBreakdown.Create(
                "gpt-4.1",
                new TokenUsage(1_000_000, 0, 0, 0),
                new EmbeddingUsage("text-embedding-3-small", 1_000_000, UsageReported: true))
            .FormatTotal();

        Assert.Contains("chat", text);
        Assert.Contains("embeddings", text);
        Assert.Contains("2,000,000 tokens", text);
    }

    /// <summary>
    /// A provider that returns no usage on an embeddings response leaves the cost unknown.
    /// Reporting $0.0000 would be a claim it never made.
    /// </summary>
    [Fact]
    public void FormatTotal_SaysWhenTheProviderReportedNoEmbeddingTokens()
    {
        var text = CostBreakdown.Create(
                "gpt-4.1",
                new TokenUsage(1_000, 0, 0, 0),
                new EmbeddingUsage("text-embedding-3-small", 0, UsageReported: false))
            .FormatTotal();

        Assert.Contains("reported no token count", text);
    }

    [Fact]
    public void FormatTotal_StaysSimple_WhenNothingWasEmbedded()
    {
        var text = CostBreakdown.Create("gpt-4.1", new TokenUsage(1_000, 0, 0, 0)).FormatTotal();

        Assert.DoesNotContain("embeddings", text);
    }

    private static decimal LineCost(CostBreakdown breakdown, string component) =>
        breakdown.Lines.Single(line => line.Component == component).Cost;
}
