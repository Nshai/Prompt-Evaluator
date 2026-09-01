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

    /// <param name="queryText">
    /// The search text as written, for the lexical half of a hybrid query. Ignored by a
    /// dense-only store, so a caller that has only a vector may leave it null and get exactly the
    /// behaviour it had before hybrid retrieval existed.
    /// </param>
    Task<IReadOnlyList<CaseDocumentSearchResult>> SearchAsync(
        string caseReference,
        int tenantId,
        ReadOnlyMemory<float> queryVector,
        int limit,
        IReadOnlyCollection<string>? categoryCodes = null,
        string? queryText = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteCaseAsync(string caseReference, int tenantId, CancellationToken cancellationToken = default);

    Task<ulong> CountAsync(string caseReference, int tenantId, CancellationToken cancellationToken = default);
}
