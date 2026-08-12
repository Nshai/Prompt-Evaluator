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

    private static decimal LineCost(CostBreakdown breakdown, string component) =>
        breakdown.Lines.Single(line => line.Component == component).Cost;
}
