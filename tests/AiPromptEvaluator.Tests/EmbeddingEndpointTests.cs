using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The embedding service is configured separately from the chat service, and the app has to
/// actually use it.
///
/// <b>It did not.</b> The settings existed, their resolvers existed, and both fields were on the
/// configuration form — and <c>AiClientFactory.CreateEmbeddingGenerator</c> built its client from
/// the chat base URL and the chat key, so nothing read either one. That is the worst shape a
/// configuration bug can take: the setting a user reaches for is the right setting, filling it in
/// changes nothing, and the failure that comes back is whatever the wrong endpoint said.
///
/// <b>Why these tests are about resolution rather than about HTTP.</b> The generator is an
/// <c>OpenAIClient</c> whose endpoint is not exposed for inspection, so asserting on the object
/// would mean asserting on reflection into someone else's type. What is testable, and what was
/// actually broken, is the pair of decisions the factory makes before it constructs anything:
/// which URL and which key. Those are pinned here, and the factory has one call site for each.
/// </summary>
public class EmbeddingEndpointTests
{
    private static AppSettings Settings() => new()
    {
        OpenAiApiKey = "chat-key",
        OpenAiBaseUrl = "https://chat.example/v1",
        EmbeddingModel = "text-embedding-3-small",
    };

    [Fact]
    public void AConfiguredEmbeddingEndpointIsUsed()
    {
        var settings = Settings();
        settings.EmbeddingBaseUrl = "https://embeddings.example/v1";

        Assert.Equal("https://embeddings.example/v1", settings.ResolveEmbeddingBaseUrl());
        Assert.NotEqual(settings.ResolveBaseUrl(), settings.ResolveEmbeddingBaseUrl());
    }

    [Fact]
    public void AConfiguredEmbeddingKeyIsUsed()
    {
        var settings = Settings();
        settings.EmbeddingApiKey = "embedding-key";

        Assert.Equal("embedding-key", settings.ResolveEmbeddingApiKey());
    }

    /// <summary>
    /// The fallback is the whole reason the fields may be left blank, and most installs do leave
    /// them blank — one service serves both.
    /// </summary>
    [Fact]
    public void BlankEmbeddingSettingsFallBackToTheChatService()
    {
        var settings = Settings();

        Assert.Equal("https://chat.example/v1", settings.ResolveEmbeddingBaseUrl());
        Assert.Equal("chat-key", settings.ResolveEmbeddingApiKey());
    }

    /// <summary>
    /// Independently: a setup may share the endpoint and split the key, or the reverse. Resolving
    /// one from the other's presence would surprise whoever configured only one.
    /// </summary>
    [Fact]
    public void TheEndpointAndTheKeyResolveIndependently()
    {
        var settings = Settings();
        settings.EmbeddingBaseUrl = "https://embeddings.example/v1";

        Assert.Equal("https://embeddings.example/v1", settings.ResolveEmbeddingBaseUrl());
        Assert.Equal("chat-key", settings.ResolveEmbeddingApiKey());
    }

    /// <summary>
    /// The case the settings themselves call out: Anthropic serves no embeddings endpoint, so a
    /// setup running checks on Claude must embed elsewhere. The guard for it existed and was
    /// called by nothing, so the failure arrived as a raw 404 from a path that will never exist.
    /// </summary>
    [Fact]
    public void AChatOnlyServiceIsRefusedByName()
    {
        var settings = Settings();
        settings.EmbeddingBaseUrl = "https://api.anthropic.com/v1";

        var error = Assert.Throws<InvalidOperationException>(
            () => AiClientFactory.CreateEmbeddingGenerator(settings));

        Assert.Contains("serves no embeddings endpoint", error.Message);
        Assert.Contains("api.anthropic.com", error.Message);
    }

    /// <summary>
    /// And it is the <em>embedding</em> endpoint that decides that. Running checks on Claude while
    /// embedding on OpenAI is a supported setup — in fact it is the setup the embedding fields
    /// were added for — so keying the refusal on the chat URL would refuse the very configuration
    /// it exists to enable.
    /// </summary>
    [Fact]
    public void ClaudeForChatWithEmbeddingsElsewhereIsAllowed()
    {
        var settings = Settings();
        settings.OpenAiBaseUrl = "https://api.anthropic.com/v1";
        settings.EmbeddingBaseUrl = "https://api.openai.com/v1";

        Assert.False(settings.EmbeddingEndpointCannotEmbed());
    }

    /// <summary>
    /// A missing key has to name which one. "Please configure an API key" sent a user to the chat
    /// key while the embedding service was the one refusing them.
    /// </summary>
    [Fact]
    public void AMissingEmbeddingKeySaysWhichKeyIsMissing()
    {
        var settings = Settings();
        settings.OpenAiApiKey = string.Empty;

        var error = Assert.Throws<InvalidOperationException>(
            () => AiClientFactory.CreateEmbeddingGenerator(settings));

        Assert.Contains("embedding API key", error.Message);
    }

    /// <summary>
    /// Both endpoints are excluded from the run fingerprint, with the credentials — they say where
    /// a run was executed, not what it did, and two runs against equivalent endpoints are the same
    /// run. The model and its dimensions are fingerprinted, and those are what change the vectors.
    /// </summary>
    [Fact]
    public void EndpointsAreNotPartOfTheRunFingerprint()
    {
        Assert.Contains(nameof(AppSettings.EmbeddingBaseUrl), AppSettings.NotFingerprinted);
        Assert.Contains(nameof(AppSettings.EmbeddingApiKey), AppSettings.NotFingerprinted);
        Assert.DoesNotContain(nameof(AppSettings.EmbeddingModel), AppSettings.NotFingerprinted);
        Assert.DoesNotContain(nameof(AppSettings.EmbeddingDimensions), AppSettings.NotFingerprinted);
    }
}
