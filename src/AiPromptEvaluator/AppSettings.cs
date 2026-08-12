using System.Text.Json.Serialization;

namespace AiPromptEvaluator;

public class AppSettings
{
    /// <summary>Where an OpenAI-compatible API lives by default. Any compatible gateway works.</summary>
    public const string DefaultBaseUrl = "https://api.openai.com/v1";

    /// <summary>Where the Docling container publishes its API by default.</summary>
    public const string DefaultDoclingEndpoint = "http://localhost:5001";

    /// <summary>Where a local Qdrant container publishes its gRPC API by default.</summary>
    public const string DefaultQdrantEndpoint = "http://localhost:6334";

    /// <summary>The collection every case document chunk is written to.</summary>
    public const string DefaultQdrantCollection = "case_documents";

    [JsonPropertyName("openAiApiKey")]
    public string OpenAiApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL of the OpenAI-compatible endpoint, e.g. https://api.openai.com/v1 or a
    /// self-hosted gateway. Leave empty to use <see cref="DefaultBaseUrl"/>.
    /// </summary>
    [JsonPropertyName("openAiBaseUrl")]
    public string OpenAiBaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("availableModels")]
    public string AvailableModels { get; set; } = "gpt-4.1,gpt-4.1-mini,gpt-4o,gpt-4o-mini";

    [JsonPropertyName("selectedModel")]
    public string SelectedModel { get; set; } = "gpt-4.1";

    /// <summary>The model used to embed document chunks and search text.</summary>
    [JsonPropertyName("embeddingModel")]
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Vector width of <see cref="EmbeddingModel"/>. It defines the Qdrant collection, so
    /// changing it means the collection has to be recreated.
    /// </summary>
    [JsonPropertyName("embeddingDimensions")]
    public int EmbeddingDimensions { get; set; } = 1536;

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

    /// <summary>
    /// Base URL of the Docling sidecar that converts spreadsheets to Markdown, e.g.
    /// http://localhost:5001. Leave empty to use <see cref="DefaultDoclingEndpoint"/>.
    /// </summary>
    [JsonPropertyName("doclingEndpoint")]
    public string DoclingEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Base URL of the Qdrant instance holding the indexed chunks — the gRPC port (6334),
    /// not the REST port (6333). Leave empty to use <see cref="DefaultQdrantEndpoint"/>.
    /// </summary>
    [JsonPropertyName("qdrantEndpoint")]
    public string QdrantEndpoint { get; set; } = string.Empty;

    /// <summary>API key for a secured Qdrant. Empty for a local container.</summary>
    [JsonPropertyName("qdrantApiKey")]
    public string QdrantApiKey { get; set; } = string.Empty;

    [JsonPropertyName("qdrantCollection")]
    public string QdrantCollection { get; set; } = DefaultQdrantCollection;

    /// <summary>
    /// The tenant every indexed chunk is stamped with and every search is filtered by.
    /// One instance serves one tenant at a time.
    /// </summary>
    [JsonPropertyName("tenantId")]
    public int TenantId { get; set; } = 99;

    /// <summary>Upper bound on a chunk; the semantic chunker splits earlier when the topic shifts.</summary>
    [JsonPropertyName("maxTokensPerChunk")]
    public int MaxTokensPerChunk { get; set; } = 600;

    /// <summary>How much of the previous chunk is repeated, so a split mid-sentence still retrieves.</summary>
    [JsonPropertyName("chunkOverlapTokens")]
    public int ChunkOverlapTokens { get; set; } = 100;

    /// <summary>How many chunks one search-tool call may return to the model.</summary>
    [JsonPropertyName("maxSearchResults")]
    public int MaxSearchResults { get; set; } = 8;

    /// <summary>The configured Docling endpoint, or the default, with any trailing slash removed.</summary>
    public string ResolveDoclingEndpoint() =>
        Resolve(DoclingEndpoint, DefaultDoclingEndpoint);

    /// <summary>The configured Qdrant endpoint, or the default, with any trailing slash removed.</summary>
    public string ResolveQdrantEndpoint() =>
        Resolve(QdrantEndpoint, DefaultQdrantEndpoint);

    /// <summary>The configured OpenAI-compatible base URL, or the default.</summary>
    public string ResolveBaseUrl() =>
        Resolve(OpenAiBaseUrl, DefaultBaseUrl);

    public string ResolveCollection() =>
        string.IsNullOrWhiteSpace(QdrantCollection) ? DefaultQdrantCollection : QdrantCollection.Trim();

    private static string Resolve(string configured, string fallback) =>
        (string.IsNullOrWhiteSpace(configured) ? fallback : configured).Trim().TrimEnd('/');

    public List<string> ParseModels() =>
        AvailableModels
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
