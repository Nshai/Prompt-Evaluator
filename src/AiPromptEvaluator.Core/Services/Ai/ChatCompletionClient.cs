using System.Text;

using Microsoft.Extensions.AI;

namespace AiPromptEvaluator;

/// <summary>Outcome of a single prompt run, including the cost breakdown.</summary>
public sealed record ChatCompletionResult(string Response, CostBreakdown Breakdown);

/// <summary>
/// UI-independent prompt execution: builds the prompt, calls the configured
/// <see cref="IChatClient"/>, and converts the reported usage into a cost breakdown.
/// </summary>
public class ChatCompletionClient : IChatCompletionClient
{
    private readonly AppSettings _settings;

    public ChatCompletionClient(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>Sends <paramref name="rawPrompt"/> verbatim — no system preamble, no folder context injected.</summary>
    public async Task<ChatCompletionResult> RunRawAsync(string rawPrompt, CancellationToken cancellationToken = default)
    {
        using var client = AiClientFactory.CreateChatClient(_settings);

        var response = await client
            .GetResponseAsync(rawPrompt, ChatOptions(), cancellationToken)
            .ConfigureAwait(false);

        return ToResult(response);
    }

    /// <summary>
    /// A system-plus-user call with no tools and its own output cap. Extraction and check
    /// decisions both return JSON rather than prose, where the default response cap is the
    /// wrong size: a truncated JSON document is unusable, not merely short.
    /// </summary>
    public virtual async Task<ChatCompletionResult> RunRawAsync(
        string systemPrompt,
        string userPrompt,
        int maxOutputTokens,
        CancellationToken cancellationToken = default) =>
        await RunRawAsync(systemPrompt, userPrompt, maxOutputTokens, responseFormat: null, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// As above, with the reply constrained to <paramref name="responseFormat"/>. Callers that
    /// have to parse the reply pass the schema they parse against, so the model is prevented
    /// from returning a shape the app would have to guess at.
    /// </summary>
    public virtual async Task<ChatCompletionResult> RunRawAsync(
        string systemPrompt,
        string userPrompt,
        int maxOutputTokens,
        ChatResponseFormat? responseFormat,
        CancellationToken cancellationToken = default)
    {
        using var client = AiClientFactory.CreateChatClient(_settings);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt),
        };

        var options = ChatOptions();
        options.MaxOutputTokens = Math.Max(1, maxOutputTokens);
        options.ResponseFormat = responseFormat;

        var response = await client
            .GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        return ToResult(response);
    }

    public virtual async Task<ChatCompletionResult> RunAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException("Please enter a prompt.");
        }

        var folderContext = DocumentContextBuilder.BuildContext(
            _settings.DocumentFolder, _settings.DocumentCategories, _settings.MaxDocumentsInContext);

        using var client = AiClientFactory.CreateChatClient(_settings);

        var response = await client
            .GetResponseAsync(BuildPrompt(prompt, folderContext), ChatOptions(), cancellationToken)
            .ConfigureAwait(false);

        return ToResult(response);
    }

    /// <summary>
    /// Options for every call. Temperature, top-p and seed are pinned independently: a QA
    /// finding is a judgement that should not change because the sampler rolled differently,
    /// and the provider default is a creative temperature rather than a neutral one. Each is
    /// releasable on its own because a gateway or model can reject one parameter without
    /// objecting to the others — Bedrock's Anthropic route rejects seed outright, and some
    /// Bedrock inference profiles accept only temperature 1.
    /// </summary>
    internal ChatOptions ChatOptions()
    {
        var options = new ChatOptions
        {
            MaxOutputTokens = _settings.MaxTokens,
            ModelId = _settings.SelectedModel,
        };

        if (_settings.PinTemperature)
        {
            options.Temperature = _settings.Temperature;
        }

        if (_settings.PinTopP)
        {
            options.TopP = _settings.TopP;
        }

        if (_settings.PinSeed)
        {
            options.Seed = _settings.SamplingSeed;
        }

        return options;
    }

    private ChatCompletionResult ToResult(ChatResponse response) =>
        new(response.Text, CostBreakdown.Create(_settings.SelectedModel, ReadUsage(response.Usage)));

    internal static TokenUsage ReadUsage(UsageDetails? usage)
    {
        if (usage is null)
        {
            return TokenUsage.Empty;
        }

        // Providers that report a cached-input count expose it as additional usage; anything
        // that doesn't simply prices the whole input at the uncached rate.
        var cachedRead = ReadAdditionalCount(usage, "InputTokenDetails.CachedTokenCount")
                      ?? ReadAdditionalCount(usage, "cached_tokens")
                      ?? 0;

        var input = usage.InputTokenCount ?? 0;

        return new TokenUsage(
            InputTokens: Math.Max(0, input - cachedRead),
            OutputTokens: usage.OutputTokenCount ?? 0,
            CacheWriteTokens: 0,
            CacheReadTokens: cachedRead);
    }

    private static long? ReadAdditionalCount(UsageDetails usage, string key) =>
        usage.AdditionalCounts is not null && usage.AdditionalCounts.TryGetValue(key, out var value)
            ? value
            : null;

    internal string BuildPrompt(string userPrompt, string folderContext)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are a helpful assistant. Answer the user's request clearly and concisely.");
        if (_settings.AskClarification)
        {
            builder.AppendLine("If the request is ambiguous, ask a clarifying question before making a decision.");
        }

        if (!string.IsNullOrWhiteSpace(folderContext))
        {
            builder.AppendLine(folderContext);
        }

        builder.AppendLine();
        builder.AppendLine($"User request: {userPrompt}");
        return builder.ToString();
    }
}
