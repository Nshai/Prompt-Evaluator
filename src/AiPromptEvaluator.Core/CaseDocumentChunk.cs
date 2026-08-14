namespace AiPromptEvaluator;

/// <summary>
/// One indexed passage of a case document, with the metadata every search is filtered
/// and reported by: which case it belongs to, which tenant owns it, and which document
/// and category it was cut from.
/// </summary>
public sealed record CaseDocumentChunk(
    string CaseReference,
    int TenantId,
    string DocumentName,
    string CategoryCode,
    string CategoryName,
    int ChunkIndex,
    string Text)
{
    /// <summary>
    /// A stable id for the chunk, so re-indexing the same case overwrites its rows
    /// instead of duplicating them.
    /// </summary>
    public Guid PointId => DeterministicGuid(
        $"{TenantId}|{CaseReference}|{CategoryCode}|{DocumentName}|{ChunkIndex}");

    /// <summary>Qdrant point ids must be a UUID or an integer, so hash the natural key into one.</summary>
    private static Guid DeterministicGuid(string key)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(key));

        return new Guid(hash.AsSpan(0, 16));
    }
}

/// <summary>A chunk the vector search matched, with the similarity score Qdrant gave it.</summary>
public sealed record CaseDocumentSearchResult(CaseDocumentChunk Chunk, float Score);

/// <summary>One document's indexing outcome, reported as the load progresses.</summary>
public sealed record CaseIndexProgress(
    int Done,
    int Total,
    string DocumentName,
    string CategoryCode,
    int Chunks,
    TimeSpan Elapsed,
    string? Error = null);
