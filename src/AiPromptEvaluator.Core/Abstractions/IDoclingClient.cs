namespace AiPromptEvaluator;

/// <summary>The document-conversion sidecar.</summary>
public interface IDoclingClient
{
    string Endpoint { get; }

    /// <summary>True when the sidecar answers its health probe. Never throws.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<DoclingConversionResult> ConvertToMarkdownAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
