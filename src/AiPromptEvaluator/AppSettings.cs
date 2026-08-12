using System.IO;
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
    /// Where embeddings are generated, when that isn't the same service as the chat model.
    /// Anthropic serves no embeddings endpoint, for instance, so a setup that runs checks on
    /// Claude has to embed somewhere else — OpenAI, Voyage, or a local Ollama.
    /// Leave empty to use <see cref="OpenAiBaseUrl"/>.
    /// </summary>
    [JsonPropertyName("embeddingBaseUrl")]
    public string EmbeddingBaseUrl { get; set; } = string.Empty;

    /// <summary>Key for the embedding service. Leave empty to use <see cref="OpenAiApiKey"/>.</summary>
    [JsonPropertyName("embeddingApiKey")]
    public string EmbeddingApiKey { get; set; } = string.Empty;

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

    /// <summary>
    /// The case reference stamped on every chunk and used to scope every search. Leave empty
    /// to derive it from the case folder's name, which is right when the folder is named
    /// after the case and wrong when it isn't — a working copy called "case-files" would
    /// otherwise index and search under that name.
    /// </summary>
    [JsonPropertyName("caseReference")]
    public string CaseReference { get; set; } = string.Empty;

    /// <summary>Upper bound on a chunk; the semantic chunker splits earlier when the topic shifts.</summary>
    [JsonPropertyName("maxTokensPerChunk")]
    public int MaxTokensPerChunk { get; set; } = 600;

    /// <summary>How much of the previous chunk is repeated, so a split mid-sentence still retrieves.</summary>
    [JsonPropertyName("chunkOverlapTokens")]
    public int ChunkOverlapTokens { get; set; } = 100;

    /// <summary>How many chunks one search-tool call may return to the model.</summary>
    [JsonPropertyName("maxSearchResults")]
    public int MaxSearchResults { get; set; } = 8;

    /// <summary>
    /// The canonical model JSON Schema the suitability report is extracted into. Leave empty
    /// to look for <see cref="DefaultCanonicalSchemaFileName"/> beside the executable.
    /// </summary>
    [JsonPropertyName("canonicalSchemaPath")]
    public string CanonicalSchemaPath { get; set; } = string.Empty;

    /// <summary>
    /// Folder holding the per-check query plans (CHK-*.query-plan.json). Leave empty to look
    /// for a "check-plan" folder beside the executable.
    /// </summary>
    [JsonPropertyName("checkPlanFolder")]
    public string CheckPlanFolder { get; set; } = string.Empty;

    /// <summary>
    /// Where extracted canonical models are kept. Leave empty for a file in the app's local
    /// data folder, so models survive a restart without the user choosing a location.
    /// </summary>
    [JsonPropertyName("canonicalModelDbPath")]
    public string CanonicalModelDbPath { get; set; } = string.Empty;

    /// <summary>
    /// Output cap for one extraction pass. Extraction returns a JSON section rather than
    /// prose, and a truncated section is unusable, so it needs far more headroom than a
    /// check response — the app extracts a section at a time to stay inside this.
    /// </summary>
    [JsonPropertyName("extractionMaxTokens")]
    public int ExtractionMaxTokens { get; set; } = 16000;

    /// <summary>The file name looked for beside the executable when no schema path is set.</summary>
    public const string DefaultCanonicalSchemaFileName = "canonical-suitability-model.schema.json";

    /// <summary>The folder name looked for beside the executable when no plan folder is set.</summary>
    public const string DefaultCheckPlanFolderName = "check-plan";

    /// <summary>Where prompts sent to the LLM are logged by default, one file per run.</summary>
    public const string DefaultPromptLogFolder = @"C:\logs\apipromptevaluator";

    /// <summary>
    /// Folder every run's prompt log is written to. Leave empty to use
    /// <see cref="DefaultPromptLogFolder"/>.
    /// </summary>
    [JsonPropertyName("promptLogFolder")]
    public string PromptLogFolder { get; set; } = string.Empty;

    /// <summary>
    /// The configured schema path, or the copy deployed beside the executable. Returns the
    /// resolved path whether or not it exists — the caller reports a missing file with the
    /// path it looked for, which is more useful than an empty string.
    /// </summary>
    public string ResolveCanonicalSchemaPath() =>
        string.IsNullOrWhiteSpace(CanonicalSchemaPath)
            ? Path.Combine(AppContext.BaseDirectory, DefaultCanonicalSchemaFileName)
            : CanonicalSchemaPath.Trim();

    /// <summary>The configured plan folder, or the one deployed beside the executable.</summary>
    public string ResolveCheckPlanFolder() =>
        string.IsNullOrWhiteSpace(CheckPlanFolder)
            ? Path.Combine(AppContext.BaseDirectory, DefaultCheckPlanFolderName)
            : CheckPlanFolder.Trim();

    /// <summary>The configured prompt log folder, or <see cref="DefaultPromptLogFolder"/>.</summary>
    public string ResolvePromptLogFolder() =>
        string.IsNullOrWhiteSpace(PromptLogFolder)
            ? DefaultPromptLogFolder
            : PromptLogFolder.Trim();

    /// <summary>
    /// Where the canonical model database lives. The default sits next to settings.json so
    /// an extracted model is found again after a restart without any configuration.
    /// </summary>
    public string ResolveCanonicalModelDbPath() =>
        string.IsNullOrWhiteSpace(CanonicalModelDbPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AiPromptEvaluator",
                "canonical-models.db")
            : CanonicalModelDbPath.Trim();

    /// <summary>The configured Docling endpoint, or the default, with any trailing slash removed.</summary>
    public string ResolveDoclingEndpoint() =>
        Resolve(DoclingEndpoint, DefaultDoclingEndpoint);

    /// <summary>The configured Qdrant endpoint, or the default, with any trailing slash removed.</summary>
    public string ResolveQdrantEndpoint() =>
        Resolve(QdrantEndpoint, DefaultQdrantEndpoint);

    /// <summary>The configured OpenAI-compatible base URL, or the default.</summary>
    public string ResolveBaseUrl() =>
        Resolve(OpenAiBaseUrl, DefaultBaseUrl);

    /// <summary>Where embeddings are generated — its own endpoint when set, else the chat one.</summary>
    public string ResolveEmbeddingBaseUrl() =>
        string.IsNullOrWhiteSpace(EmbeddingBaseUrl) ? ResolveBaseUrl() : Resolve(EmbeddingBaseUrl, DefaultBaseUrl);

    /// <summary>The embedding service's key, falling back to the chat key when they're the same service.</summary>
    public string ResolveEmbeddingApiKey() =>
        string.IsNullOrWhiteSpace(EmbeddingApiKey) ? OpenAiApiKey.Trim() : EmbeddingApiKey.Trim();

    /// <summary>
    /// True when embeddings are pointed at a service that has no embeddings endpoint. Worth
    /// saying plainly: no base path or model name makes that call work.
    /// </summary>
    public bool EmbeddingEndpointCannotEmbed() =>
        ResolveEmbeddingBaseUrl().Contains("api.anthropic.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The configured case reference, or the case folder's name when none is set. This is the
    /// single answer to "which case is this", so indexing and searching can't disagree.
    /// </summary>
    public string ResolveCaseReference(string caseFolder) =>
        string.IsNullOrWhiteSpace(CaseReference)
            ? CaseDocumentIndexer.CaseReferenceFor(caseFolder)
            : CaseReference.Trim();

    public string ResolveCollection() =>
        string.IsNullOrWhiteSpace(QdrantCollection) ? DefaultQdrantCollection : QdrantCollection.Trim();

    private static string Resolve(string configured, string fallback) =>
        (string.IsNullOrWhiteSpace(configured) ? fallback : configured).Trim().TrimEnd('/');

    public List<string> ParseModels() =>
        AvailableModels
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
