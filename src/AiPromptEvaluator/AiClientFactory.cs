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
        RequireApiKey(settings);

        return CreateOpenAiClient(settings)
            .GetChatClient(RequireModel(settings.SelectedModel, "chat model"))
            .AsIChatClient();
    }

    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(AppSettings settings)
    {
        RequireApiKey(settings);

        return CreateOpenAiClient(settings)
            .GetEmbeddingClient(RequireModel(settings.EmbeddingModel, "embedding model"))
            .AsIEmbeddingGenerator();
    }

    private static OpenAIClient CreateOpenAiClient(AppSettings settings) =>
        new(new ApiKeyCredential(settings.OpenAiApiKey.Trim()),
            new OpenAIClientOptions { Endpoint = new Uri(settings.ResolveBaseUrl()) });

    private static void RequireApiKey(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.OpenAiApiKey))
        {
            throw new InvalidOperationException("Please configure an API key before calling the model.");
        }
    }

    private static string RequireModel(string model, string what) =>
        string.IsNullOrWhiteSpace(model)
            ? throw new InvalidOperationException($"Please configure a {what} in Settings.")
            : model.Trim();
}
