using Microsoft.Extensions.AI;

namespace AiPromptEvaluator;

/// <summary>
/// Creates a search service scoped to one case.
///
/// The case reference is not known when the container is built, and threading it through as a
/// registration would mean rebuilding the container for every case.
/// </summary>
public interface ICaseDocumentSearchServiceFactory
{
    /// <summary>
    /// Scoped to one case, and to the store the caller is holding open.
    ///
    /// <paramref name="embeddings"/> is the caller's, not the container's, and that is
    /// load-bearing: a run wraps the generator to count what it spends, and every planned search
    /// embeds its text. Handing back a service bound to the registered generator instead would
    /// leave a few hundred embedding calls per run uncounted, and the cost line would quietly
    /// under-report. Omit it only where nothing is being measured.
    /// </summary>
    ICaseDocumentSearchService Create(
        string caseReference,
        ICaseDocumentStore store,
        IEmbeddingGenerator<string, Embedding<float>>? embeddings = null);
}
