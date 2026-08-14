using Microsoft.Extensions.DataIngestion;

namespace AiPromptEvaluator;

/// <summary>Chunks a case folder and writes it to the vector store.</summary>
public interface ICaseDocumentIndexer
{
    /// <summary>How the documents were split, for the run fingerprint.</summary>
    string ChunkingDescription { get; }

    IngestionChunker<string> CreateChunker();

    Task<CaseIndexResult> IndexAsync(
        string caseFolder,
        string caseReference,
        IProgress<CaseIndexProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
