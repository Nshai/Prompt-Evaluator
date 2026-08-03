namespace AiPromptEvaluator;

/// <summary>
/// Per-million-token rates for a model. Cache write/read are derived from the
/// input rate: writes cost 1.25x input (5 minute TTL), reads cost 0.1x input.
/// </summary>
public sealed record ModelRates(string ModelId, decimal InputPerMillion, decimal OutputPerMillion)
{
    public decimal CacheWritePerMillion => InputPerMillion * 1.25m;

    public decimal CacheReadPerMillion => InputPerMillion * 0.1m;
}

public static class ModelPricing
{
    private const decimal Million = 1_000_000m;

    private static readonly ModelRates Fallback = new("(unknown)", 5.00m, 25.00m);

    private static readonly Dictionary<string, ModelRates> Rates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-fable-5"] = new("claude-fable-5", 10.00m, 50.00m),
            ["claude-opus-5"] = new("claude-opus-5", 5.00m, 25.00m),
            ["claude-opus-4-8"] = new("claude-opus-4-8", 5.00m, 25.00m),
            ["claude-opus-4-7"] = new("claude-opus-4-7", 5.00m, 25.00m),
            ["claude-opus-4-6"] = new("claude-opus-4-6", 5.00m, 25.00m),
            ["claude-sonnet-5"] = new("claude-sonnet-5", 3.00m, 15.00m),
            ["claude-sonnet-4-6"] = new("claude-sonnet-4-6", 3.00m, 15.00m),
            ["claude-haiku-4-5"] = new("claude-haiku-4-5", 1.00m, 5.00m),
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
