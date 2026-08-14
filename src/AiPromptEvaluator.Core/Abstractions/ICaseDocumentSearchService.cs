namespace AiPromptEvaluator;

/// <summary>Semantic search over one case's indexed passages.</summary>
public interface ICaseDocumentSearchService
{
    Task<IReadOnlyList<CaseDocumentSearchMatch>> SearchAsync(
        string searchText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// As above, but telling the store which categories the plan expects the answer in, so a
    /// passage from a small category is not crowded out before it can be ranked.
    /// </summary>
    Task<IReadOnlyList<CaseDocumentSearchMatch>> SearchAsync(
        string searchText,
        IReadOnlyCollection<string>? categoryCodes,
        CancellationToken cancellationToken = default);
}
