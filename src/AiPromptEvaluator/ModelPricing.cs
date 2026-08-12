namespace AiPromptEvaluator;

/// <summary>
/// Per-million-token rates for a model. Cached input is derived from the input rate at
/// 0.25x; writing to the cache is not billed separately, so that rate is zero.
/// </summary>
public sealed record ModelRates(string ModelId, decimal InputPerMillion, decimal OutputPerMillion)
{
    public decimal CacheWritePerMillion => 0m;

    public decimal CacheReadPerMillion => InputPerMillion * 0.25m;
}

public static class ModelPricing
{
    private const decimal Million = 1_000_000m;

    private static readonly ModelRates Fallback = new("(unknown)", 2.50m, 10.00m);

    private static readonly Dictionary<string, ModelRates> Rates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-4.1"] = new("gpt-4.1", 2.00m, 8.00m),
            ["gpt-4.1-mini"] = new("gpt-4.1-mini", 0.40m, 1.60m),
            ["gpt-4.1-nano"] = new("gpt-4.1-nano", 0.10m, 0.40m),
            ["gpt-4o"] = new("gpt-4o", 2.50m, 10.00m),
            ["gpt-4o-mini"] = new("gpt-4o-mini", 0.15m, 0.60m),
            ["o4-mini"] = new("o4-mini", 1.10m, 4.40m),
            ["text-embedding-3-small"] = new("text-embedding-3-small", 0.02m, 0m),
            ["text-embedding-3-large"] = new("text-embedding-3-large", 0.13m, 0m),
        };

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
