using Xunit;

namespace AiPromptEvaluator.Tests;

public class CostBreakdownTests
{
    [Fact]
    public void Create_PricesEachComponentSeparately()
    {
        // claude-opus-5: $5 input / $25 output per million tokens.
        var usage = new TokenUsage(
            InputTokens: 1_000_000,
            OutputTokens: 1_000_000,
            CacheWriteTokens: 1_000_000,
            CacheReadTokens: 1_000_000);

        var breakdown = CostBreakdown.Create("claude-opus-5", usage);

        Assert.Equal(4, breakdown.Lines.Count);
        Assert.Equal(5.00m, LineCost(breakdown, "Input (uncached)"));
        Assert.Equal(6.25m, LineCost(breakdown, "Cache write"));   // 1.25x input
        Assert.Equal(0.50m, LineCost(breakdown, "Cache read"));    // 0.1x input
        Assert.Equal(25.00m, LineCost(breakdown, "Output"));
        Assert.Equal(36.75m, breakdown.TotalCost);
        Assert.False(breakdown.RatesAreEstimated);
    }

    [Fact]
    public void Create_UsesModelSpecificRates()
    {
        var usage = new TokenUsage(1_000_000, 0, 0, 0);

        Assert.Equal(1.00m, CostBreakdown.Create("claude-haiku-4-5", usage).TotalCost);
        Assert.Equal(3.00m, CostBreakdown.Create("claude-sonnet-5", usage).TotalCost);
        Assert.Equal(10.00m, CostBreakdown.Create("claude-fable-5", usage).TotalCost);
    }

    [Fact]
    public void Create_FlagsUnknownModelsAsEstimated()
    {
        var breakdown = CostBreakdown.Create("some-future-model", new TokenUsage(1_000, 0, 0, 0));

        Assert.True(breakdown.RatesAreEstimated);
        Assert.True(breakdown.TotalCost > 0m);
    }

    [Fact]
    public void Create_MatchesDatedModelAliases()
    {
        var breakdown = CostBreakdown.Create("anthropic.claude-sonnet-5", new TokenUsage(1_000_000, 0, 0, 0));

        Assert.False(breakdown.RatesAreEstimated);
        Assert.Equal(3.00m, breakdown.TotalCost);
    }

    [Fact]
    public void Empty_HasZeroCostAndZeroTokens()
    {
        var breakdown = CostBreakdown.Empty("claude-opus-5");

        Assert.Equal(0m, breakdown.TotalCost);
        Assert.Equal(0, breakdown.Usage.TotalTokens);
        Assert.All(breakdown.Lines, line => Assert.Equal(0m, line.Cost));
    }

    private static decimal LineCost(CostBreakdown breakdown, string component) =>
        breakdown.Lines.Single(line => line.Component == component).Cost;
}
