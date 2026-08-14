using Microsoft.Extensions.AI;

namespace AiPromptEvaluator;

/// <summary>
/// Wraps an embedding generator and adds up the tokens it bills for.
///
/// Every embedding the app pays for passes through one method, so wrapping here catches all
/// of it — including the calls the semantic chunker makes internally while deciding where to
/// cut a document, which the app never issues itself and would otherwise never see. Indexing
/// a case is usually the largest embedding spend of a session and it used to report as free.
///
/// Providers are not obliged to return usage on an embeddings response. When one doesn't,
/// the count stays at zero and <see cref="UsageReported"/> is false, so the UI can say the
/// cost is unknown rather than implying it was nothing.
/// </summary>
public sealed class UsageTrackingEmbeddingGenerator
    : DelegatingEmbeddingGenerator<string, Embedding<float>>
{
    private long _tokens;
    private int _calls;
    private int _responsesWithUsage;

    public UsageTrackingEmbeddingGenerator(IEmbeddingGenerator<string, Embedding<float>> inner)
        : base(inner)
    {
    }

    /// <summary>Tokens billed across every embedding call made through this generator.</summary>
    public long TotalTokens => Interlocked.Read(ref _tokens);

    /// <summary>How many embedding requests were made, for the run report.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>
    /// False when no response carried usage details — the tokens are then unknown rather
    /// than zero, and a cost of $0.0000 would be a claim the provider never made.
    /// </summary>
    public bool UsageReported => Volatile.Read(ref _responsesWithUsage) > 0;

    public override async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = await base
            .GenerateAsync(values, options, cancellationToken)
            .ConfigureAwait(false);

        Interlocked.Increment(ref _calls);

        // Embeddings bill on input only, but providers report the same count under either
        // name depending on the endpoint, so take whichever is present.
        var usage = embeddings.Usage;
        var tokens = usage?.InputTokenCount ?? usage?.TotalTokenCount;

        if (tokens is > 0)
        {
            Interlocked.Add(ref _tokens, tokens.Value);
            Interlocked.Increment(ref _responsesWithUsage);
        }

        return embeddings;
    }
}
