using System.Net.Http.Headers;
using System.Text;
using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using BetaMessages = Anthropic.Models.Beta.Messages;
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
    /// Sends <paramref name="textPrompt"/> alongside file-reference content blocks
    /// (documents/images uploaded via the Files API). Requires the beta Messages endpoint.
    /// </summary>
    public async Task<PromptResult> RunWithFilesAsync(
        string textPrompt,
        IReadOnlyList<BetaContentBlockParam> fileBlocks,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.AnthropicApiKey))
        {
            throw new InvalidOperationException("Please configure an Anthropic API key before running the prompt.");
        }

        var client = CreateClient();

        var content = new List<BetaContentBlockParam> { new BetaTextBlockParam { Text = textPrompt } };
        content.AddRange(fileBlocks);

        var parameters = new BetaMessages.MessageCreateParams
        {
            Betas = [AnthropicBeta.FilesApi2025_04_14],
            Messages = [new BetaMessageParam { Role = Role.User, Content = content }],
            MaxTokens = _settings.MaxTokens,
            Model = _settings.SelectedModel,
        };

        var result = await client.Beta.Messages.Create(parameters, cancellationToken).ConfigureAwait(false);

        var usage = ReadUsage(result.Usage);
        var breakdown = CostBreakdown.Create(_settings.SelectedModel, usage);
        return new PromptResult(ExtractText(result.Content), breakdown);
    }

    /// <summary>Uploads a document/image file so its content can be referenced by file_id.</summary>
    public async Task<string> UploadFileAsync(Stream stream, string fileName, string mimeType, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var uploaded = await client.Beta.Files.Upload(
            new Anthropic.Models.Beta.Files.FileUploadParams
            {
                File = new BinaryContent
                {
                    Stream = stream,
                    FileName = fileName,
                    ContentType = new MediaTypeHeaderValue(mimeType)
                }
            },
            cancellationToken).ConfigureAwait(false);

        return uploaded.ID;
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

    internal static string ExtractText(IReadOnlyList<BetaContentBlock> content)
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

    internal static TokenUsage ReadUsage(BetaUsage? usage)
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
