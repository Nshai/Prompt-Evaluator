namespace AiPromptEvaluator;

/// <summary>Reads a suitability report into the canonical model.</summary>
public interface ICanonicalModelExtractor
{
    Task<ExtractionResult> ExtractAsync(
        string caseFolder,
        string caseReference,
        IProgress<ExtractionProgress>? progress = null,
        PromptLogWriter? promptLog = null,
        CancellationToken cancellationToken = default);
}
