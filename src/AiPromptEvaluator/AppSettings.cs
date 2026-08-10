using System.Text.Json.Serialization;

namespace AiPromptEvaluator;

public class AppSettings
{
    public const string DefaultBaseUrl = "https://api.anthropic.com";

    [JsonPropertyName("anthropicApiKey")]
    public string AnthropicApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Plain base URL (no path/version placeholders), e.g. https://api.anthropic.com.
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

    /// <summary>Maps file name (not full path) to category label.</summary>
    [JsonPropertyName("documentCategories")]
    public Dictionary<string, string> DocumentCategories { get; set; } = new();

    [JsonPropertyName("lastChecksCsvPath")]
    public string LastChecksCsvPath { get; set; } = string.Empty;

    public List<string> ParseModels() =>
        AvailableModels
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
