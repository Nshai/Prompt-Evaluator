using System.Diagnostics;

using AiPromptEvaluator;

using Microsoft.Extensions.AI;

namespace IQWorkflow;

/// <summary>
/// <b>S2 — chunk the case's Markdown, embed it, and write it to the vector store.</b>
///
/// The cheapest stage that does real work and the one every later stage depends on: an assessment
/// retrieves from what this wrote, and a check whose evidence was never indexed reports confidently
/// on a pack that could not contain the answer.
/// </summary>
public sealed class IndexCaseStage : IWorkflowStage
{
    private readonly ICaseDocumentStoreFactory _stores;
    private readonly Func<AppSettings, IEmbeddingGenerator<string, Embedding<float>>> _embeddings;
    private readonly Func<AppSettings, IEmbeddingGenerator<string, Embedding<float>>, ICaseDocumentStore, ICaseDocumentIndexer> _indexers;

    /// <param name="embeddings">
    /// How an embedding generator is built for a run. A factory rather than an instance because
    /// building one requires a configured key, and the workflow is constructed before anything has
    /// validated that — the same reason Core registers it deferred.
    /// </param>
    /// <param name="indexers">
    /// How an indexer is built over a store and a generator. Injected so a test can drive this
    /// stage without Qdrant and without embedding anything.
    /// </param>
    public IndexCaseStage(
        ICaseDocumentStoreFactory stores,
        Func<AppSettings, IEmbeddingGenerator<string, Embedding<float>>> embeddings,
        Func<AppSettings, IEmbeddingGenerator<string, Embedding<float>>, ICaseDocumentStore, ICaseDocumentIndexer> indexers)
    {
        _stores = stores ?? throw new ArgumentNullException(nameof(stores));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        _indexers = indexers ?? throw new ArgumentNullException(nameof(indexers));
    }

    public WorkflowStageId Id => WorkflowStageId.Index;

    /// <summary>
    /// Indexing reads the converted Markdown, so it waits for conversion — and for nothing else.
    /// It shares a superstep with extraction, which is the overlap that matters: the two bill
    /// against different services and sequencing them spends the longer of the two twice over.
    /// </summary>
    public IReadOnlyList<WorkflowStageId> DependsOn => [WorkflowStageId.Convert];

    /// <summary>
    /// Satisfied when the store already holds passages for this case and tenant.
    ///
    /// <b>Deliberately a count and not a content comparison.</b> Knowing whether the index matches
    /// the folder exactly would mean hashing every document and storing the result — real work, and
    /// work that would have to stay in step with the chunker's settings to mean anything. A count
    /// answers the question a resumed run actually asks: is there an index to assess against. An
    /// operator who has changed the documents unloads the case, which is the existing gesture for
    /// exactly this and is already what the desktop app does.
    /// </summary>
    public async Task<bool> IsSatisfiedAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        using var store = _stores.Create(context.Settings);

        if (!await store.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var indexed = await store
            .CountAsync(context.CaseReference, context.Settings.TenantId, cancellationToken)
            .ConfigureAwait(false);

        return indexed > 0;
    }

    public async Task<StageOutcome> ExecuteAsync(
        WorkflowContext context,
        IProgress<StageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();

        using var store = _stores.Create(context.Settings);

        // Checked before anything is embedded. Discovering the store is down after paying for the
        // embeddings would be paying for work nothing can use.
        if (!await store.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return StageOutcome.Failed(
                Id,
                $"The vector store at {store.Endpoint} is not responding. Start it, or correct the "
                + "endpoint in settings, and run again.",
                clock.Elapsed);
        }

        // Wrapped so the chunker's own calls are counted: it embeds every element of every document
        // to find the cut points, which is the larger half of what indexing costs and used to be
        // reported as free.
        var embeddings = new UsageTrackingEmbeddingGenerator(_embeddings(context.Settings));
        var indexer = _indexers(context.Settings, embeddings, store);

        progress?.Report(new StageProgress(
            Id, $"Chunking: {indexer.ChunkingDescription}; store {store.Endpoint}/{store.Collection}"));

        var relay = new Progress<CaseIndexProgress>(p => progress?.Report(new StageProgress(
            Id,
            p.Error is { Length: > 0 } error ? $"{p.DocumentName}: {error}" : p.DocumentName,
            p.Done,
            p.Total)));

        var result = await indexer
            .IndexAsync(context.CaseFolder, context.CaseReference, relay, cancellationToken)
            .ConfigureAwait(false);

        clock.Stop();

        var detail = new Dictionary<string, string>
        {
            ["documents"] = result.Documents.ToString(),
            ["chunks"] = result.Chunks.ToString(),
            ["collection"] = store.Collection,
            ["chunking"] = indexer.ChunkingDescription,
        };

        if (result.Failures.Count > 0)
        {
            detail["failed"] = string.Join("; ", result.Failures.Select(f => $"{f.Document}: {f.Error}"));
        }

        // A case that indexed nothing is a failure however cleanly it ran: every later stage would
        // retrieve nothing and report it as the case file being silent, which is a different and
        // much more damaging claim than "the documents were never indexed".
        if (result.Chunks == 0)
        {
            return StageOutcome.Failed(
                Id,
                $"No passages were indexed from {context.CaseFolder}. The folder holds no readable "
                + "Markdown, or every document failed to convert.",
                clock.Elapsed,
                detail);
        }

        return StageOutcome.Succeeded(
            Id,
            $"Indexed {result.Chunks} passage(s) from {result.Documents} document(s).",
            clock.Elapsed,
            detail,

            // Null where the provider reported nothing, so the manifest records the cost as unknown
            // rather than as zero.
            embeddings.UsageReported
                ? new TokenUsage(embeddings.TotalTokens, 0, 0, 0)
                : null);
    }
}
