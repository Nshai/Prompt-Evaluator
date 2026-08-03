using System.Text.Json.Serialization;

namespace AiPromptEvaluator;

public class AppSettings
{
    public const string DefaultBaseUrl = "https://api.anthropic.com/{0}/{1}";

    [JsonPropertyName("anthropicApiKey")]
    public string AnthropicApiKey { get; set; } = string.Empty;

    /// <summary>
    /// API URL format string. {0} is the API version, {1} the endpoint.
    /// Leave empty to use the SDK default.
    /// </summary>
    [JsonPropertyName("anthropicBaseUrl")]
    public string AnthropicBaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("availableModels")]
    public string AvailableModels { get; set; } =
        "claude-opus-5,claude-sonnet-5,claude-haiku-4-5";

    [JsonPropertyName("selectedModel")]
    public string SelectedModel { get; set; } = "claude-opus-5";

    [JsonPropertyName("documentFolder")]
    public string DocumentFolder { get; set; } = string.Empty;

    [JsonPropertyName("askClarification")]
    public bool AskClarification { get; set; } = true;

    [JsonPropertyName("maxTokens")]
    public int MaxTokens { get; set; } = 4096;

    public List<string> ParseModels() =>
        AvailableModels
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
