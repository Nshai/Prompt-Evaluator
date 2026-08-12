using AiPromptEvaluator;

using Microsoft.Extensions.AI;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Counting what embeddings cost. The wrapper sits under the chunker as well as the app's
/// own calls, which is the point: the chunker embeds every element of every document to find
/// its cut points, and that spend was invisible before.
/// </summary>
public class UsageTrackingEmbeddingGeneratorTests
{
    /// <summary>A generator that returns fixed vectors and whatever usage the test asks for.</summary>
    private sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly UsageDetails? _usage;

        public FakeEmbeddingGenerator(UsageDetails? usage) => _usage = usage;

        public int Calls { get; private set; }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            var embeddings = new GeneratedEmbeddings<Embedding<float>>(
                values.Select(_ => new Embedding<float>(new float[] { 1f, 0f, 0f })))
            {
                Usage = _usage,
            };

            return Task.FromResult(embeddings);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private static UsageTrackingEmbeddingGenerator Tracking(UsageDetails? usage, out FakeEmbeddingGenerator inner)
    {
        inner = new FakeEmbeddingGenerator(usage);
        return new UsageTrackingEmbeddingGenerator(inner);
    }

    [Fact]
    public async Task TotalTokens_AddsUpEveryCall()
    {
        using var tracking = Tracking(new UsageDetails { InputTokenCount = 120 }, out _);

        await tracking.GenerateAsync(["one", "two"]);
        await tracking.GenerateAsync(["three"]);

        Assert.Equal(240, tracking.TotalTokens);
        Assert.Equal(2, tracking.Calls);
        Assert.True(tracking.UsageReported);
    }

    /// <summary>Some endpoints report only a total; embeddings bill on input either way.</summary>
    [Fact]
    public async Task TotalTokens_FallsBackToTheTotalCount()
    {
        using var tracking = Tracking(new UsageDetails { TotalTokenCount = 75 }, out _);

        await tracking.GenerateAsync(["one"]);

        Assert.Equal(75, tracking.TotalTokens);
        Assert.True(tracking.UsageReported);
    }

    /// <summary>
    /// No usage on the response means the cost is unknown, not zero — the UI leans on this
    /// to say so rather than showing a confident $0.0000.
    /// </summary>
    [Fact]
    public async Task UsageReported_IsFalse_WhenTheProviderSaysNothing()
    {
        using var tracking = Tracking(usage: null, out _);

        await tracking.GenerateAsync(["one"]);

        Assert.Equal(0, tracking.TotalTokens);
        Assert.Equal(1, tracking.Calls);
        Assert.False(tracking.UsageReported);
    }

    [Fact]
    public async Task Calls_IsZero_BeforeAnythingIsEmbedded()
    {
        using var tracking = Tracking(new UsageDetails { InputTokenCount = 10 }, out _);

        Assert.Equal(0, tracking.Calls);
        Assert.False(tracking.UsageReported);

        await tracking.GenerateAsync(["one"]);

        Assert.Equal(1, tracking.Calls);
    }

    /// <summary>The wrapper must not change what the caller gets back.</summary>
    [Fact]
    public async Task GenerateAsync_PassesTheEmbeddingsThrough()
    {
        using var tracking = Tracking(new UsageDetails { InputTokenCount = 10 }, out var inner);

        var embeddings = await tracking.GenerateAsync(["one", "two", "three"]);

        Assert.Equal(3, embeddings.Count);
        Assert.Equal(1, inner.Calls);
    }

    /// <summary>
    /// The chunker calls the generator it was handed, so wrapping catches its traffic too —
    /// this is the spend the app never issues itself and could not otherwise see.
    /// </summary>
    [Fact]
    public async Task TotalTokens_CountsCallsMadeByCollaborators()
    {
        using var tracking = Tracking(new UsageDetails { InputTokenCount = 50 }, out _);

        IEmbeddingGenerator<string, Embedding<float>> handedToACollaborator = tracking;
        await handedToACollaborator.GenerateAsync(["chunker probe"]);

        Assert.Equal(50, tracking.TotalTokens);
    }
}
