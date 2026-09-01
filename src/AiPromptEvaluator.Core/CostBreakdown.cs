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
/// What one run spent on embeddings: the model, the tokens it billed, and whether the
/// provider actually said so.
/// </summary>
public sealed record EmbeddingUsage(string ModelId, long Tokens, bool UsageReported)
{
    public static readonly EmbeddingUsage None = new(string.Empty, 0, false);

    /// <summary>True when embeddings were generated at all, reported or not.</summary>
    public bool IsPresent => !string.IsNullOrWhiteSpace(ModelId);
}

/// <summary>
/// A per-component cost breakdown for a single run, derived from token usage and the
/// published rates of the models involved.
///
/// Chat and embeddings are priced separately because they are separate models on separate
/// rate cards, and a run can be almost entirely one or the other: indexing a case is pure
/// embedding spend, extracting the canonical model is pure chat.
/// </summary>
public sealed class CostBreakdown
{
    /// <summary>The component label carrying the embedding cost, used to split the totals.</summary>
    private const string EmbeddingComponentPrefix = "Embeddings";

    private CostBreakdown(
        string modelId,
        TokenUsage usage,
        EmbeddingUsage embeddings,
        IReadOnlyList<CostLine> lines,
        bool ratesAreEstimated)
    {
        ModelId = modelId;
        Usage = usage;
        Embeddings = embeddings;
        Lines = lines;
        RatesAreEstimated = ratesAreEstimated;
    }

    public string ModelId { get; }

    public TokenUsage Usage { get; }

    /// <summary>What the run spent on embeddings, priced on their own model's rate.</summary>
    public EmbeddingUsage Embeddings { get; }

    public IReadOnlyList<CostLine> Lines { get; }

    /// <summary>True when a model was not in the rate table and fallback rates were used.</summary>
    public bool RatesAreEstimated { get; }

    public decimal TotalCost => Lines.Sum(line => line.Cost);

    /// <summary>Cost of the chat model's tokens alone.</summary>
    public decimal ChatCost => Lines
        .Where(line => !line.Component.StartsWith(EmbeddingComponentPrefix, StringComparison.Ordinal))
        .Sum(line => line.Cost);

    /// <summary>Cost of the embedding calls alone — indexing, chunking and search.</summary>
    public decimal EmbeddingCost => Lines
        .Where(line => line.Component.StartsWith(EmbeddingComponentPrefix, StringComparison.Ordinal))
        .Sum(line => line.Cost);

    /// <summary>Every token billed, chat and embeddings together.</summary>
    public long TotalTokens => Usage.TotalTokens + Embeddings.Tokens;

    public static CostBreakdown Empty(string modelId) => Create(modelId, TokenUsage.Empty);

    public static CostBreakdown Create(string modelId, TokenUsage usage) =>
        Create(modelId, usage, EmbeddingUsage.None);

    public static CostBreakdown Create(string modelId, TokenUsage usage, EmbeddingUsage embeddings)
    {
        var rates = ModelPricing.ForModel(modelId);

        var lines = new List<CostLine>
        {
            Line("Input (uncached)", usage.InputTokens, rates.InputPerMillion),
            Line("Cache write", usage.CacheWriteTokens, rates.CacheWritePerMillion),
            Line("Cache read", usage.CacheReadTokens, rates.CacheReadPerMillion),
            Line("Output", usage.OutputTokens, rates.OutputPerMillion),
        };

        var estimated = !ModelPricing.IsKnownModel(modelId);

        // Only add the row when embeddings were actually generated. A permanent zero row on
        // a screen that never embeds is noise, not information.
        if (embeddings.IsPresent)
        {
            var embeddingRates = ModelPricing.ForModel(embeddings.ModelId);

            lines.Add(Line(
                $"{EmbeddingComponentPrefix} ({embeddings.ModelId})",
                embeddings.Tokens,
                embeddingRates.InputPerMillion));

            estimated |= !ModelPricing.IsKnownModel(embeddings.ModelId);
        }

        return new CostBreakdown(modelId, usage, embeddings, lines, estimated);
    }

    private static CostLine Line(string component, long tokens, decimal ratePerMillion) =>
        new(component, tokens, ratePerMillion, ModelPricing.CostFor(tokens, ratePerMillion));

    /// <summary>
    /// The headline figure. Where a run spent on both, the split is shown too — a total that
    /// hides which model consumed it is the thing this breakdown exists to avoid.
    /// </summary>
    public string FormatTotal()
    {
        var total = $"Total: {TotalCost:C4} for {TotalTokens:N0} tokens";

        if (!Embeddings.IsPresent)
        {
            return total;
        }

        if (!Embeddings.UsageReported)
        {
            return $"{total} — chat {ChatCost:C4}; embeddings billed but the provider "
                 + "reported no token count";
        }

        return $"{total} — chat {ChatCost:C4} ({Usage.TotalTokens:N0} tokens) · "
             + $"embeddings {EmbeddingCost:C4} ({Embeddings.Tokens:N0} tokens)";
    }
}
