using System.Text;
using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;
using Messages = Anthropic.Models.Messages;

namespace AiPromptEvaluator;

/// <summary>Outcome of a single prompt run, including the cost breakdown.</summary>
public sealed record PromptResult(string Response, CostBreakdown Breakdown);

/// <summary>
/// UI-independent prompt execution: builds the prompt, calls the Anthropic API,
/// and converts the reported usage into a cost breakdown.
/// </summary>
public class PromptEvaluator
{
    private readonly AppSettings _settings;

    public PromptEvaluator(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>Sends <paramref name="rawPrompt"/> verbatim — no system preamble, no folder context injected.</summary>
    public async Task<PromptResult> RunRawAsync(string rawPrompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.AnthropicApiKey))
        {
            throw new InvalidOperationException("Please configure an Anthropic API key before running the prompt.");
        }

        var client = CreateClient();
        var parameters = new Messages.MessageCreateParams
        {
            Messages = [new Messages.MessageParam { Role = Messages.Role.User, Content = rawPrompt }],
            MaxTokens = _settings.MaxTokens,
            Model = _settings.SelectedModel,
        };

        var result = await client.Messages.Create(parameters, cancellationToken).ConfigureAwait(false);

        var usage = ReadUsage(result.Usage);
        var breakdown = CostBreakdown.Create(_settings.SelectedModel, usage);
        return new PromptResult(ExtractText(result.Content), breakdown);
    }

    public virtual async Task<PromptResult> RunAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException("Please enter a prompt.");
        }

        if (string.IsNullOrWhiteSpace(_settings.AnthropicApiKey))
        {
            throw new InvalidOperationException("Please configure an Anthropic API key before running the prompt.");
        }

        var folderContext = DocumentContextBuilder.BuildContext(_settings.DocumentFolder, _settings.DocumentCategories);
        var fullPrompt = BuildPrompt(prompt, folderContext);

        var client = CreateClient();
        var parameters = new Messages.MessageCreateParams
        {
            Messages = [new Messages.MessageParam { Role = Messages.Role.User, Content = fullPrompt }],
            MaxTokens = _settings.MaxTokens,
            Model = _settings.SelectedModel,
        };

        var result = await client.Messages.Create(parameters, cancellationToken).ConfigureAwait(false);

        var usage = ReadUsage(result.Usage);
        var breakdown = CostBreakdown.Create(_settings.SelectedModel, usage);
        return new PromptResult(ExtractText(result.Content), breakdown);
    }

    /// <summary>
    /// Sends inline document blocks followed by <paramref name="instructionText"/>.
    ///
    /// The order matters and is not cosmetic. Prompt caching is a prefix match, so the
    /// documents — the part that repeats across every check run against a case folder — go
    /// first and carry the cache breakpoint, and the per-check instruction goes last where
    /// changing it costs nothing already cached.
    ///
    /// This uses the plain Messages endpoint with no beta headers, so the same request works
    /// against api.anthropic.com and through a LiteLLM proxy to Bedrock.
    /// </summary>
    public async Task<PromptResult> RunWithDocumentsAsync(
        IReadOnlyList<ContentBlockParam> documentBlocks,
        string instructionText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.AnthropicApiKey))
        {
            throw new InvalidOperationException("Please configure an Anthropic API key before running the prompt.");
        }

        var client = CreateClient();

        var content = new List<ContentBlockParam>(documentBlocks)
        {
            new TextBlockParam { Text = instructionText },
        };

        var parameters = new Messages.MessageCreateParams
        {
            Messages = [new Messages.MessageParam { Role = Messages.Role.User, Content = content }],
            MaxTokens = _settings.MaxTokens,
            Model = _settings.SelectedModel,
        };

        var result = await client.Messages.Create(parameters, cancellationToken).ConfigureAwait(false);

        var usage = ReadUsage(result.Usage);
        var breakdown = CostBreakdown.Create(_settings.SelectedModel, usage);
        return new PromptResult(ExtractText(result.Content), breakdown);
    }

    /// <summary>
    /// Counts the input tokens the given content blocks would bill if they were sent.
    /// The count_tokens endpoint is itself free — this is an estimate, not a charge.
    /// </summary>
    public async Task<long> CountTokensAsync(
        IReadOnlyList<ContentBlockParam> blocks,
        CancellationToken cancellationToken = default)
    {
        if (blocks.Count == 0)
        {
            return 0;
        }

        var client = CreateClient();
        var parameters = new Messages.MessageCountTokensParams
        {
            Messages = [new Messages.MessageParam { Role = Messages.Role.User, Content = blocks.ToList() }],
            Model = _settings.SelectedModel,
        };

        var result = await client.Messages.CountTokens(parameters, cancellationToken).ConfigureAwait(false);
        return result.InputTokens;
    }

    internal static string ExtractText(IReadOnlyList<Messages.ContentBlock> content)
    {
        var sb = new StringBuilder();
        foreach (var block in content)
        {
            if (block.TryPickText(out var text))
            {
                sb.Append(text.Text);
            }
        }
        return sb.ToString();
    }

    internal static TokenUsage ReadUsage(Messages.Usage? usage)
    {
        if (usage is null)
        {
            return TokenUsage.Empty;
        }

        return new TokenUsage(
            InputTokens: usage.InputTokens,
            OutputTokens: usage.OutputTokens,
            CacheWriteTokens: usage.CacheCreationInputTokens ?? 0,
            CacheReadTokens: usage.CacheReadInputTokens ?? 0);
    }

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

    private AnthropicClient CreateClient()
    {
        // HTTP/1.1 avoids connection resets from proxies that don't support HTTP/2.
        var httpClient = new HttpClient(new HttpClientHandler())
        {
            DefaultRequestVersion = new Version(1, 1)
        };

        var options = new ClientOptions
        {
            ApiKey = _settings.AnthropicApiKey,
            HttpClient = httpClient,
        };

        if (!string.IsNullOrWhiteSpace(_settings.AnthropicBaseUrl))
        {
            options.BaseUrl = _settings.AnthropicBaseUrl.TrimEnd('/');
        }

        return new AnthropicClient(options);
    }
}
