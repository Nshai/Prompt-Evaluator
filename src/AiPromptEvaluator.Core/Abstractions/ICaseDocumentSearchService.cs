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
    /// <param name="resultsPerCall">
    /// How many hits each query may return, overriding <see cref="AppSettings.MaxSearchResults"/>
    /// where a plan asks for more. Widening this costs vector lookups, not prompt tokens: the
    /// per-group cap downstream is unchanged, so a larger pool only gives ranking more to
    /// choose from.
    /// </param>
    Task<IReadOnlyList<CaseDocumentSearchMatch>> SearchAsync(
        string searchText,
        IReadOnlyCollection<string>? categoryCodes,
        CancellationToken cancellationToken = default,
        int? resultsPerCall = null);
}
