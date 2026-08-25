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
public sealed class CaseDocumentSearchService : ICaseDocumentSearchService
{
    private readonly AppSettings _settings;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddings;
    private readonly ICaseDocumentStore _store;
    private readonly string _caseReference;

    public CaseDocumentSearchService(
        AppSettings settings,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        ICaseDocumentStore store,
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
        CancellationToken cancellationToken = default) =>
        await SearchAsync(searchText, categoryCodes: null, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// As above, but guaranteeing the plan's target categories get a hearing.
    ///
    /// The query is embedded once and run twice: once restricted to the categories the plan
    /// asked for, and once across the whole case. Restricting alone would be wrong — a plan's
    /// target categories are where the evidence is *expected*, not the only place it can be, and
    /// a hard filter would silently discard a contradiction sitting in a category nobody thought
    /// to name. Searching unfiltered alone is what the run already did, and small categories lose
    /// that competition every time.
    ///
    /// The second query costs almost nothing: the embedding is the expensive part and it is
    /// shared, leaving one more vector lookup against an indexed payload field.
    /// </summary>
    public async Task<IReadOnlyList<CaseDocumentSearchMatch>> SearchAsync(
        string searchText,
        IReadOnlyCollection<string>? categoryCodes,
        CancellationToken cancellationToken = default,
        int? resultsPerCall = null)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [];
        }

        // A plan may ask for a wider pool than the global setting. It may not ask for a
        // narrower one: the floor is what every other check is held to, and a plan quietly
        // retrieving less than the rest of the run is the kind of difference nothing in the
        // output would show.
        var limit = Math.Max(_settings.MaxSearchResults, resultsPerCall ?? 0);

        var vector = await _embeddings
            .GenerateVectorAsync(searchText, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var hits = await _store
            .SearchAsync(
                _caseReference, _settings.TenantId, vector, limit,
                categoryCodes: null, cancellationToken)
            .ConfigureAwait(false);

        if (categoryCodes is { Count: > 0 })
        {
            var targeted = await _store
                .SearchAsync(
                    _caseReference, _settings.TenantId, vector, limit,
                    categoryCodes, cancellationToken)
                .ConfigureAwait(false);

            // Targeted hits first, so the categories the plan asked for survive the per-group
            // cap even when they score below the bulk of the case file. De-duplication further
            // down keeps a chunk found by both from appearing twice.
            hits = [.. targeted, .. hits];
        }

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
