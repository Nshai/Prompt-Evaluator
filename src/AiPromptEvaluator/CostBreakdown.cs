namespace AiPromptEvaluator;

/// <summary>One row of the cost breakdown shown on the main screen.</summary>
public sealed record CostLine(string Component, long Tokens, decimal RatePerMillion, decimal Cost);

/// <summary>
/// Token usage returned by the API, split into the four billed categories.
/// </summary>
public sealed record TokenUsage(
    long InputTokens,
    long OutputTokens,
    long CacheWriteTokens,
    long CacheReadTokens)
{
    public static readonly TokenUsage Empty = new(0, 0, 0, 0);

    public long TotalTokens => InputTokens + OutputTokens + CacheWriteTokens + CacheReadTokens;
}

/// <summary>
/// A per-component cost breakdown for a single response, derived from token
/// usage and the selected model's published rates.
/// </summary>
public sealed class CostBreakdown
{
    private CostBreakdown(string modelId, TokenUsage usage, IReadOnlyList<CostLine> lines, bool ratesAreEstimated)
    {
        ModelId = modelId;
        Usage = usage;
        Lines = lines;
        RatesAreEstimated = ratesAreEstimated;
    }

    public string ModelId { get; }

    public TokenUsage Usage { get; }

    public IReadOnlyList<CostLine> Lines { get; }

    /// <summary>True when the model was not in the rate table and fallback rates were used.</summary>
    public bool RatesAreEstimated { get; }

    public decimal TotalCost => Lines.Sum(line => line.Cost);

    public static CostBreakdown Empty(string modelId) => Create(modelId, TokenUsage.Empty);

    public static CostBreakdown Create(string modelId, TokenUsage usage)
    {
        var rates = ModelPricing.ForModel(modelId);

        var lines = new List<CostLine>
        {
            Line("Input (uncached)", usage.InputTokens, rates.InputPerMillion),
            Line("Cache write", usage.CacheWriteTokens, rates.CacheWritePerMillion),
            Line("Cache read", usage.CacheReadTokens, rates.CacheReadPerMillion),
            Line("Output", usage.OutputTokens, rates.OutputPerMillion),
        };

        return new CostBreakdown(modelId, usage, lines, !ModelPricing.IsKnownModel(modelId));
    }

    private static CostLine Line(string component, long tokens, decimal ratePerMillion) =>
        new(component, tokens, ratePerMillion, ModelPricing.CostFor(tokens, ratePerMillion));

    public string FormatTotal() => $"Total: {TotalCost:C4} for {Usage.TotalTokens:N0} tokens";
}
