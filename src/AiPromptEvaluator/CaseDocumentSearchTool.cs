using Microsoft.Extensions.AI;

namespace AiPromptEvaluator;

/// <summary>One passage retrieved from the case file, with everything needed to cite it.</summary>
public sealed record CaseDocumentSearchMatch(
    string SearchedText,
    string CaseReference,
    int TenantId,
    string DocumentName,
    string CategoryCode,
    string CategoryName,
    double Score);

/// <summary>
/// Semantic search over one case: embeds the search text and returns the closest passages,
/// scoped to the case and tenant it was constructed for.
///
/// Searches are chosen by the check's query plan, not by the model — so two runs of the same
/// check over the same case retrieve the same evidence, and a finding can be reproduced.
///
/// Matches from several documents come back together, which is the point: a check needs to
/// see the fact find and the suitability report side by side to tell whether they agree.
/// </summary>
public sealed class CaseDocumentSearchTool
{
    private readonly AppSettings _settings;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddings;
    private readonly CaseDocumentStore _store;
    private readonly string _caseReference;

    public CaseDocumentSearchTool(
        AppSettings settings,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        CaseDocumentStore store,
        string caseReference)
    {
        _settings = settings;
        _embeddings = embeddings;
        _store = store;
        _caseReference = caseReference;
    }

    /// <summary>
    /// The closest passages to <paramref name="searchText"/> within this case. The case and
    /// tenant come from construction and cannot be overridden per call, so a search can only
    /// ever reach the case the run is about.
    /// </summary>
    public async Task<IReadOnlyList<CaseDocumentSearchMatch>> SearchAsync(
        string searchText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [];
        }

        var vector = await _embeddings
            .GenerateVectorAsync(searchText, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var hits = await _store
            .SearchAsync(_caseReference, _settings.TenantId, vector, _settings.MaxSearchResults, cancellationToken)
            .ConfigureAwait(false);

        return hits
            .Select(hit => new CaseDocumentSearchMatch(
                SearchedText: hit.Chunk.Text,
                CaseReference: hit.Chunk.CaseReference,
                TenantId: hit.Chunk.TenantId,
                DocumentName: hit.Chunk.DocumentName,
                CategoryCode: hit.Chunk.CategoryCode,
                CategoryName: hit.Chunk.CategoryName,

                // Kept at full precision. Rounding here manufactured ties that ranking then
                // had to break arbitrarily; the score is rounded where it is displayed instead.
                Score: hit.Score))
            .ToList();
    }
}
