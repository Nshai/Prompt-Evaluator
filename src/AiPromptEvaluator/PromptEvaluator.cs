using System.Text;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
// Disambiguates from System.Windows.Forms.Message.
using ClaudeMessage = Anthropic.SDK.Messaging.Message;

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

        var folderContext = DocumentContextBuilder.BuildContext(_settings.DocumentFolder);
        var fullPrompt = BuildPrompt(prompt, folderContext);

        var client = CreateClient();
        var parameters = new MessageParameters
        {
            Messages = new List<ClaudeMessage> { new(RoleType.User, fullPrompt) },
            MaxTokens = _settings.MaxTokens,
            Model = _settings.SelectedModel,
            Stream = false,
            Temperature = 0.2m
        };

        var result = await client.Messages.GetClaudeMessageAsync(parameters, cancellationToken)
            .ConfigureAwait(false);

        var usage = ReadUsage(result);
        var breakdown = CostBreakdown.Create(_settings.SelectedModel, usage);
        return new PromptResult(result.Message?.ToString() ?? string.Empty, breakdown);
    }

    internal static TokenUsage ReadUsage(MessageResponse? response)
    {
        var usage = response?.Usage;
        if (usage is null)
        {
            return TokenUsage.Empty;
        }

        return new TokenUsage(
            InputTokens: usage.InputTokens,
            OutputTokens: usage.OutputTokens,
            CacheWriteTokens: usage.CacheCreationInputTokens,
            CacheReadTokens: usage.CacheReadInputTokens);
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
        var client = new AnthropicClient(new APIAuthentication(_settings.AnthropicApiKey));

        // ApiUrlFormat is a format string: {0} is the API version, {1} the endpoint.
        if (!string.IsNullOrWhiteSpace(_settings.AnthropicBaseUrl))
        {
            client.ApiUrlFormat = _settings.AnthropicBaseUrl;
        }

        return client;
    }
}
