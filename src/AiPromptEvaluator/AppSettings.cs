using System.Text.Json.Serialization;

namespace AiPromptEvaluator;

public class AppSettings
{
    [JsonPropertyName("anthropicApiKey")]
    public string AnthropicApiKey { get; set; } = string.Empty;

    [JsonPropertyName("anthropicBaseUrl")]
    public string AnthropicBaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("availableModels")]
    public string AvailableModels { get; set; } = "claude-3-5-sonnet-latest,claude-3-5-haiku-latest,claude-3-7-sonnet-latest";

    [JsonPropertyName("selectedModel")]
    public string SelectedModel { get; set; } = "claude-3-5-sonnet-latest";

    [JsonPropertyName("documentFolder")]
    public string DocumentFolder { get; set; } = string.Empty;

    [JsonPropertyName("askClarification")]
    public bool AskClarification { get; set; } = true;
}
