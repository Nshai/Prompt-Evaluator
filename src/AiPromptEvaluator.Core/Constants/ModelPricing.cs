namespace AiPromptEvaluator;

/// <summary>
/// Per-million-token rates for a model. Cache rates are multiples of the input rate, and
/// the multipliers differ by provider: OpenAI doesn't bill a cache write and serves cached
/// input at 0.25x, while Anthropic bills a write at 1.25x and a read at 0.1x.
/// </summary>
public sealed record ModelRates(
    string ModelId,
    decimal InputPerMillion,
    decimal OutputPerMillion,
    decimal CacheWriteMultiplier = 0m,
    decimal CacheReadMultiplier = 0.25m)
{
    public decimal CacheWritePerMillion => InputPerMillion * CacheWriteMultiplier;

    public decimal CacheReadPerMillion => InputPerMillion * CacheReadMultiplier;
}

public static class ModelPricing
{
    private const decimal Million = 1_000_000m;

    /// <summary>Anthropic's cache multipliers: writes bill at 1.25x input, reads at 0.1x.</summary>
    private const decimal AnthropicCacheWrite = 1.25m;
    private const decimal AnthropicCacheRead = 0.1m;

    private static readonly ModelRates Fallback = new("(unknown)", 2.50m, 10.00m);

    /// <summary>
    /// Rates for the models this app is pointed at. Anthropic models are here because an
    /// OpenAI-compatible gateway commonly serves them, so a run can be priced whichever
    /// provider is behind the configured base URL.
    /// </summary>
    private static readonly Dictionary<string, ModelRates> Rates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // OpenAI
            ["gpt-4.1"] = new("gpt-4.1", 2.00m, 8.00m),
            ["gpt-4.1-mini"] = new("gpt-4.1-mini", 0.40m, 1.60m),
            ["gpt-4.1-nano"] = new("gpt-4.1-nano", 0.10m, 0.40m),
            ["gpt-4o"] = new("gpt-4o", 2.50m, 10.00m),
            ["gpt-4o-mini"] = new("gpt-4o-mini", 0.15m, 0.60m),
            ["o4-mini"] = new("o4-mini", 1.10m, 4.40m),
            ["text-embedding-3-small"] = new("text-embedding-3-small", 0.02m, 0m),
            ["text-embedding-3-large"] = new("text-embedding-3-large", 0.13m, 0m),

            // Bedrock embeddings. Titan bills input only and reports the count on every
            // response, so a run priced with these is priced from what the provider said
            // rather than from an estimate.
            ["amazon.titan-embed-text-v2:0"] = new("amazon.titan-embed-text-v2:0", 0.02m, 0m),
            ["cohere.embed-v4:0"] = new("cohere.embed-v4:0", 0.12m, 0m),

            // Anthropic. Claude Sonnet 5 carries introductory pricing of $2/$10 until
            // 2026-08-31; list rates are used here so the figure doesn't silently
            // under-report once the introduction ends.
            ["claude-sonnet-5"] = Anthropic("claude-sonnet-5", 3.00m, 15.00m),
            ["claude-sonnet-4-6"] = Anthropic("claude-sonnet-4-6", 3.00m, 15.00m),
            ["claude-haiku-4-5"] = Anthropic("claude-haiku-4-5", 1.00m, 5.00m),
        };

    private static ModelRates Anthropic(string modelId, decimal input, decimal output) =>
        new(modelId, input, output, AnthropicCacheWrite, AnthropicCacheRead);

    /// <summary>
    /// Looks up rates for a model id. Unknown models fall back to Opus-tier rates
    /// so the breakdown still shows an estimate rather than zero.
    /// </summary>
    public static ModelRates ForModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return Fallback;
        }

        if (Rates.TryGetValue(modelId.Trim(), out var exact))
        {
            return exact;
        }

        // Aliases such as "claude-opus-5-20260101" or "anthropic.claude-opus-5".
        foreach (var pair in Rates)
        {
            if (modelId.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value with { ModelId = modelId };
            }
        }

        return Fallback with { ModelId = modelId };
    }

    public static bool IsKnownModel(string? modelId) =>
        !string.IsNullOrWhiteSpace(modelId) &&
        (Rates.ContainsKey(modelId.Trim()) ||
         Rates.Keys.Any(k => modelId.Contains(k, StringComparison.OrdinalIgnoreCase)));

    public static IReadOnlyCollection<string> KnownModels => Rates.Keys.ToList();

    public static decimal CostFor(long tokens, decimal ratePerMillion) =>
        tokens <= 0 ? 0m : tokens / Million * ratePerMillion;
}
