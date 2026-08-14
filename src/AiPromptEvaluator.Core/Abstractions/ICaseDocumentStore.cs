namespace AiPromptEvaluator;

/// <summary>The vector store holding a case's passages.</summary>
public interface ICaseDocumentStore : IDisposable
{
    string Endpoint { get; }
    string Collection { get; }

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task EnsureCollectionAsync(int dimensions, CancellationToken cancellationToken = default);

    Task UpsertAsync(
        IReadOnlyList<CaseDocumentChunk> chunks,
        IReadOnlyList<ReadOnlyMemory<float>> vectors,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CaseDocumentSearchResult>> SearchAsync(
        string caseReference,
        int tenantId,
        ReadOnlyMemory<float> queryVector,
        int limit,
        IReadOnlyCollection<string>? categoryCodes = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteCaseAsync(string caseReference, int tenantId, CancellationToken cancellationToken = default);

    Task<ulong> CountAsync(string caseReference, int tenantId, CancellationToken cancellationToken = default);
}
