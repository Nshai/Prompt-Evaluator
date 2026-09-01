using System.ClientModel;

using Microsoft.Extensions.AI;

using OpenAI;

namespace AiPromptEvaluator;

/// <summary>
/// Builds the two Microsoft.Extensions.AI abstractions the app talks to — an
/// <see cref="IChatClient"/> for prompts and checks, and an
/// <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> for chunking and search.
///
/// Both are backed by an OpenAI-compatible endpoint whose base URL, key and model come
/// from settings, so pointing the app at a gateway or a self-hosted server is configuration
/// rather than code.
/// </summary>
public static class AiClientFactory
{
    /// <summary>
    /// A plain chat client. Nothing passes tools any more: a check's retrieval is driven by
    /// its query plan rather than by the model calling a search function, so there is no
    /// tool-invocation loop to wrap.
    /// </summary>
    public static IChatClient CreateChatClient(AppSettings settings)
    {
        RequireApiKey(settings.OpenAiApiKey, "API key");

        var model = RequireModel(settings.SelectedModel, "chat model");
        var baseUrl = settings.ResolveBaseUrl();

        // Bedrock's own API, which is the only route to Claude in an account whose gateway lists
        // every Anthropic model and serves none of them — and the only one that can be told to
        // cache the prompt prefix this pipeline deliberately repeats 88 times a run.
        if (EmbeddingProviders.IsBedrock(settings.ChatProvider))
        {
            return new BedrockChatClient(baseUrl, settings.OpenAiApiKey, model);
        }

        // Bedrock's native runtime does not speak the OpenAI protocol, and does not say so in a
        // way anything downstream can read: an unknown path comes back as HTTP 200 carrying a
        // Coral error. The embedding side reached this conclusion the expensive way.
        if (LooksLikeBedrock(baseUrl))
        {
            throw new InvalidOperationException(
                $"The chat endpoint is {baseUrl}, which is AWS Bedrock's native runtime. It does "
                + "not serve the OpenAI chat-completions API, and it answers an unknown path with "
                + "HTTP 200 rather than an error. Point the chat endpoint at an OpenAI-compatible "
                + "gateway.");
        }

        // The other half of the same mistake, and the one that actually happened: a Bedrock model
        // id sent to an OpenAI-compatible gateway. The two endpoints name the same model
        // differently — a gateway calls it "anthropic.claude-haiku-4-5" while Bedrock's runtime
        // calls it "eu.anthropic.claude-haiku-4-5-20251001-v1:0" — so the id is not portable
        // between them, and the gateway's answer is a bare "does not exist" that reads as though
        // the model were unavailable rather than misnamed.
        if (IsBedrockModelId(model))
        {
            throw new InvalidOperationException(
                $"The chat model \"{model}\" is an AWS Bedrock model or inference-profile id — the "
                + "region prefix and the \"-v1:0\" suffix are Bedrock's naming, not an "
                + $"OpenAI-compatible gateway's. {baseUrl} will report that it does not exist. Use "
                + "the id the gateway lists at /v1/models (usually the same model without the "
                + "region prefix or version suffix).");
        }

        return CreateOpenAiClient(settings.OpenAiApiKey, baseUrl)
            .GetChatClient(model)
            .AsIChatClient();
    }

    /// <summary>
    /// Whether a model id is Bedrock's rather than a gateway's.
    ///
    /// Keyed on the two things only Bedrock does: a cross-region inference-profile prefix
    /// (<c>eu.</c>, <c>us.</c>, <c>apac.</c>) and a versioned suffix (<c>-v1:0</c>). Deliberately
    /// not keyed on a bare colon — Ollama tags models <c>llama3:8b</c>, and refusing those would
    /// break a supported setup to catch an unsupported one.
    /// </summary>
    internal static bool IsBedrockModelId(string modelId)
    {
        var id = modelId.Trim();

        return System.Text.RegularExpressions.Regex.IsMatch(
                   id, @"-v\d+:\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
               || System.Text.RegularExpressions.Regex.IsMatch(
                   id, @"^(eu|us|apac)\.[a-z0-9-]+\.",
                   System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// The embedding generator, on <b>its own endpoint and key</b> where settings give it one.
    ///
    /// <b>This used to silently use the chat endpoint.</b> The settings, their resolvers and the
    /// two form fields all existed; nothing joined them to the client, so a setup that ran checks
    /// on a chat service with no embeddings endpoint — Anthropic being the case the settings
    /// themselves call out — sent its embedding calls to that service and failed with whatever
    /// the wrong endpoint happened to say. Configuring the embedding endpoint changed nothing,
    /// which is the worst shape a configuration bug can take: the fix a user tries first is the
    /// fix that is already correct.
    /// </summary>
    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(AppSettings settings)
    {
        // Said plainly and before the call, because no base path or model name makes it work.
        // Reaching this with a chat-only service configured is the single most likely way to get
        // here, and the underlying 404 does not say so.
        if (settings.EmbeddingEndpointCannotEmbed())
        {
            throw new InvalidOperationException(
                $"Embeddings are pointed at {settings.ResolveEmbeddingBaseUrl()}, which serves no "
                + "embeddings endpoint. Set an embedding base URL and key in Settings — OpenAI, "
                + "Voyage or a local Ollama — or the chat service's own if it has one.");
        }

        RequireApiKey(settings.ResolveEmbeddingApiKey(), "embedding API key");

        var model = RequireModel(settings.EmbeddingModel, "embedding model");

        if (EmbeddingProviders.IsBedrock(settings.EmbeddingProvider))
        {
            return new BedrockEmbeddingGenerator(
                settings.ResolveEmbeddingBaseUrl(),
                settings.ResolveEmbeddingApiKey(),
                model,
                settings.EmbeddingDimensions,
                settings.MaxParallelRequests);
        }

        // The mistake that produced "Expected the number of embeddings (0) to match the number of
        // inputs (1)". Bedrock's runtime answers an unknown path with HTTP 200, so the OpenAI
        // client reads a Coral error as a valid empty response and nothing on the way out says
        // which of the two protocols was wrong. Named here, before the call, because the symptom
        // never will.
        if (LooksLikeBedrock(settings.ResolveEmbeddingBaseUrl()))
        {
            throw new InvalidOperationException(
                $"Embeddings are pointed at {settings.ResolveEmbeddingBaseUrl()}, which is AWS "
                + "Bedrock's native runtime, but the embedding provider is set to "
                + $"{EmbeddingProviders.OpenAi}. Bedrock is not OpenAI-compatible — it answers an "
                + "unknown path with HTTP 200 and an empty result rather than an error. Set the "
                + $"embedding provider to {EmbeddingProviders.Bedrock} in Settings.");
        }

        return CreateOpenAiClient(settings.ResolveEmbeddingApiKey(), settings.ResolveEmbeddingBaseUrl())
            .GetEmbeddingClient(model)
            .AsIEmbeddingGenerator();
    }

    /// <summary>
    /// Whether a URL is Bedrock's native runtime. Matched on the service host rather than on the
    /// full name, so every region reads the same.
    /// </summary>
    internal static bool LooksLikeBedrock(string baseUrl) =>
        baseUrl.Contains("bedrock-runtime.", StringComparison.OrdinalIgnoreCase);

    private static OpenAIClient CreateOpenAiClient(string apiKey, string baseUrl) =>
        new(new ApiKeyCredential(apiKey.Trim()),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

    /// <summary>
    /// Names which key is missing. "Please configure an API key" sent a user to the chat key when
    /// the embedding service was the one refusing them.
    /// </summary>
    private static void RequireApiKey(string apiKey, string what)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"Please configure an {what} before calling the model.");
        }
    }

    private static string RequireModel(string model, string what) =>
        string.IsNullOrWhiteSpace(model)
            ? throw new InvalidOperationException($"Please configure a {what} in Settings.")
            : model.Trim();
}
