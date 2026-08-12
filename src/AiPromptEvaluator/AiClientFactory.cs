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
    /// A chat client with automatic function invocation, so a tool the model calls is
    /// executed and fed back without the caller driving the loop.
    /// </summary>
    public static IChatClient CreateChatClient(AppSettings settings)
    {
        RequireApiKey(settings);

        return CreateOpenAiClient(settings)
            .GetChatClient(RequireModel(settings.SelectedModel, "chat model"))
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
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
