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
        //
        // Two families, and both are read because the app is pointed at both. OpenAI-compatible
        // endpoints cache automatically and report a single "cached" figure. Anthropic caches on
        // explicit breakpoints and reports the two halves separately — the call that populates the
        // cache and the calls that read it — which is a distinction worth keeping, because they
        // are priced differently in opposite directions: a write costs 1.25x and a read 0.1x.
        var cachedRead = ReadAdditionalCount(usage, "InputTokenDetails.CachedTokenCount")
                      ?? ReadAdditionalCount(usage, "cached_tokens")
                      ?? ReadAdditionalCount(usage, "cache_read_input_tokens")
                      ?? 0;

        // Previously hard-coded to zero, which is right for a provider that never charges for a
        // cache write and wrong for the one family that does. A run through an Anthropic route
        // with caching enabled was billed for cache creation and reported it as ordinary input,
        // so the breakdown understated the first call and overstated every later one.
        var cachedWrite = ReadAdditionalCount(usage, "cache_creation_input_tokens")
                       ?? ReadAdditionalCount(usage, "InputTokenDetails.CacheCreationTokenCount")
                       ?? 0;

        var input = usage.InputTokenCount ?? 0;
        var cached = cachedRead + cachedWrite;

        // <b>The two families disagree about what "input" includes, and the numbers say which is
        // which.</b> OpenAI counts cached tokens inside its input total, so they have to come out
        // or they are billed twice. Bedrock's Converse reports them alongside it — a fully cached
        // call comes back as inputTokens 0 with cacheReadInputTokens 4,902 — so subtracting there
        // would erase tokens that were charged at the full rate.
        //
        // Containment is the test, because it is the thing that actually differs: a cached count
        // larger than the input total cannot have been inside it.
        return new TokenUsage(
            InputTokens: input >= cached ? input - cached : input,
            OutputTokens: usage.OutputTokenCount ?? 0,
            CacheWriteTokens: cachedWrite,
            CacheReadTokens: cachedRead);
    }

    private static long? ReadAdditionalCount(UsageDetails usage, string key) =>
        usage.AdditionalCounts is not null && usage.AdditionalCounts.TryGetValue(key, out var value)
            ? value
            : null;


}
