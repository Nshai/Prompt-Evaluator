using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AiPromptEvaluator;

/// <summary>
/// The Qdrant collection holding case-document chunks. Every point carries the tenant,
/// case reference, document name and category as payload, so a search can be narrowed to
/// one case within one tenant before the vectors are compared.
/// </summary>
public sealed class CaseDocumentStore : ICaseDocumentStore
{
    // Payload keys. They are also the fields the payload indexes are built on, so keep
    // them in one place rather than spelling them out at each call site.
    private const string TenantKey = "tenant_id";
    private const string CaseKey = "case_reference";
    private const string DocumentKey = "document_name";
    private const string CategoryCodeKey = "category_code";
    private const string CategoryNameKey = "category_name";
    private const string ChunkIndexKey = "chunk_index";
    private const string TextKey = "text";

    /// <summary>
    /// The names a hybrid collection gives its two vectors.
    ///
    /// A dense-only collection uses Qdrant's unnamed default instead, and the two layouts are not
    /// interchangeable: a point written for one cannot be read by the other. That is why turning
    /// hybrid on or off requires the collection to be dropped and the case re-indexed, rather than
    /// migrated in place.
    /// </summary>
    internal const string DenseVectorName = "dense";
    internal const string SparseVectorName = "sparse";

    private readonly QdrantClient _client;
    private readonly string _collection;
    private readonly bool _hybrid;

    public CaseDocumentStore(AppSettings settings)
    {
        var endpoint = new Uri(settings.ResolveQdrantEndpoint());
        var apiKey = string.IsNullOrWhiteSpace(settings.QdrantApiKey) ? null : settings.QdrantApiKey.Trim();

        _client = new QdrantClient(endpoint, apiKey);
        _collection = settings.ResolveCollection();
        _hybrid = settings.HybridRetrieval;
        Endpoint = endpoint.ToString();
    }

    /// <summary>Whether this store reads and writes a hybrid (dense + sparse) collection.</summary>
    public bool IsHybrid => _hybrid;

    /// <summary>Where this store is pointed, for status messages.</summary>
    public string Endpoint { get; }

    public string Collection => _collection;

    /// <summary>True when Qdrant answers — probed before a load so a stopped container is one clear error.</summary>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates the collection if it doesn't exist, with payload indexes on the fields every
    /// search filters by. Cosine distance matches how the embedding models are normalised.
    /// </summary>
    public async Task EnsureCollectionAsync(int dimensions, CancellationToken cancellationToken = default)
    {
        if (await _client.CollectionExistsAsync(_collection, cancellationToken).ConfigureAwait(false))
        {
            await RequireMatchingLayoutAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // Hybrid collections name their vectors; dense-only ones use the unnamed default. The two
        // layouts are not interchangeable, and a point written for one cannot be read by the other
        // — which is why a collection created under one setting has to be dropped and rebuilt
        // under the other rather than migrated. Load() reports that rather than failing obscurely.
        if (_hybrid)
        {
            await _client.CreateCollectionAsync(
                _collection,
                new VectorParamsMap
                {
                    Map =
                    {
                        [DenseVectorName] = new VectorParams
                        {
                            Size = (ulong)dimensions,
                            Distance = Distance.Cosine,
                        },
                    },
                },
                sparseVectorsConfig: new SparseVectorConfig
                {
                    Map = { [SparseVectorName] = new SparseVectorParams() },
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _client.CreateCollectionAsync(
                _collection,
                new VectorParams { Size = (ulong)dimensions, Distance = Distance.Cosine },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await _client.CreatePayloadIndexAsync(
            _collection, TenantKey, PayloadSchemaType.Integer, cancellationToken: cancellationToken).ConfigureAwait(false);
        await _client.CreatePayloadIndexAsync(
            _collection, CaseKey, PayloadSchemaType.Keyword, cancellationToken: cancellationToken).ConfigureAwait(false);
        await _client.CreatePayloadIndexAsync(
            _collection, CategoryCodeKey, PayloadSchemaType.Keyword, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Refuses an existing collection whose vector layout does not match the configured mode.
    ///
    /// <b>The two layouts are not interchangeable and the failure is otherwise unreadable.</b> A
    /// hybrid collection names its vectors; a dense-only one uses Qdrant's unnamed default. Writing
    /// named vectors to an unnamed collection — or the reverse — fails deep inside the client with
    /// a message about vector names that says nothing about the setting that caused it, halfway
    /// through indexing a case. Said here instead, before anything is written, naming the setting
    /// and the fix.
    ///
    /// There is no migration: the vectors themselves are stored differently, so the collection has
    /// to be dropped and the case re-indexed. That is a real cost and the message states it rather
    /// than implying the switch is free.
    /// </summary>
    private async Task RequireMatchingLayoutAsync(CancellationToken cancellationToken)
    {
        var info = await _client
            .GetCollectionInfoAsync(_collection, cancellationToken)
            .ConfigureAwait(false);

        var named = info.Config?.Params?.VectorsConfig?.ParamsMap is not null;

        if (named == _hybrid)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The collection \"{_collection}\" was built "
            + (named ? "for hybrid retrieval" : "for dense-only retrieval")
            + $", but hybrid retrieval is currently {(_hybrid ? "on" : "off")}. The two store their "
            + "vectors differently and cannot be mixed, so the collection has to be deleted and the "
            + "case re-indexed — either unload the case and load it again with this setting as you "
            + "want it, or point Settings at a different collection name.");
    }

    /// <summary>
    /// Writes chunks and their vectors. Point ids are derived from the chunk's natural key,
    /// so re-indexing a case replaces its rows rather than adding duplicates.
    /// </summary>
    public async Task UpsertAsync(
        IReadOnlyList<CaseDocumentChunk> chunks,
        IReadOnlyList<ReadOnlyMemory<float>> vectors,
        CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        if (chunks.Count != vectors.Count)
        {
            throw new ArgumentException(
                $"Got {vectors.Count} vectors for {chunks.Count} chunks.", nameof(vectors));
        }

        var points = new List<PointStruct>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var point = new PointStruct { Id = chunk.PointId };

            if (_hybrid)
            {
                // The sparse side is derived from the chunk's own text rather than passed in: it is
                // a pure function of what is being stored, so computing it anywhere else would
                // create two places for the index and the query to disagree.
                var sparse = SparseTextEncoder.Encode(chunk.Text);

                var named = new NamedVectors();
                named.Vectors[DenseVectorName] = vectors[i].ToArray();

                if (sparse.Count > 0)
                {
                    var lexical = new SparseVector();
                    lexical.Values.AddRange(sparse.Weights);
                    lexical.Indices.AddRange(sparse.Indices);

                    named.Vectors[SparseVectorName] = new Vector { Sparse = lexical };
                }

                point.Vectors = new Vectors { Vectors_ = named };
            }
            else
            {
                point.Vectors = vectors[i].ToArray();
            }

            point.Payload[TenantKey] = chunk.TenantId;
            point.Payload[CaseKey] = chunk.CaseReference;
            point.Payload[DocumentKey] = chunk.DocumentName;
            point.Payload[CategoryCodeKey] = chunk.CategoryCode;
            point.Payload[CategoryNameKey] = chunk.CategoryName;
            point.Payload[ChunkIndexKey] = chunk.ChunkIndex;
            point.Payload[TextKey] = chunk.Text;

            points.Add(point);
        }

        await _client.UpsertAsync(_collection, points, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Nearest chunks to <paramref name="queryVector"/> within one case and tenant. Results
    /// can span several documents — that is the point: one search answers "where in this
    /// case file is this discussed", not "where in this document".
    /// </summary>
    /// <param name="categoryCodes">
    /// Restricts the search to these document categories. Empty or null searches everything.
    ///
    /// This is what makes a small category reachable. Unfiltered, a search competes across the
    /// whole case file, and a category holding one short file note loses every time to the ten
    /// long policy documents next to it — in a real run the meetings-and-communications category
    /// reached five of fifty-seven requirement groups while sixteen of them asked for it. The
    /// query plans have always carried the categories each search wants; until now nothing acted
    /// on them at the point where it would have made a difference.
    /// </param>
    /// <param name="queryText">
    /// The search text as written, for the sparse half of a hybrid query. Ignored on a dense-only
    /// collection. Null falls back to dense alone, which is what a caller that has only a vector
    /// gets — correct, and quieter than throwing.
    /// </param>
    public async Task<IReadOnlyList<CaseDocumentSearchResult>> SearchAsync(
        string caseReference,
        int tenantId,
        ReadOnlyMemory<float> queryVector,
        int limit,
        IReadOnlyCollection<string>? categoryCodes = null,
        string? queryText = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new Filter();
        filter.Must.Add(Conditions.Match(TenantKey, tenantId));
        filter.Must.Add(Conditions.MatchKeyword(CaseKey, caseReference));

        if (categoryCodes is { Count: > 0 })
        {
            // Qdrant's keyword match takes a set, so one condition covers "any of these".
            // The payload index on category_code has existed since the collection was first
            // created; this is the first thing to use it.
            filter.Must.Add(Conditions.Match(CategoryCodeKey, categoryCodes.ToList()));
        }

        var wanted = (ulong)Math.Max(1, limit);
        var sparse = _hybrid ? SparseTextEncoder.Encode(queryText) : SparseText.Empty;

        if (!_hybrid || sparse.Count == 0)
        {
            var dense = await _client.QueryAsync(
                _collection,
                queryVector.ToArray(),
                usingVector: _hybrid ? DenseVectorName : null,
                filter: filter,
                limit: wanted,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return dense.Select(ToResult).ToList();
        }

        // The client reads a sparse query as (weights, term ids).
        var lexical = (sparse.Weights.ToArray(), sparse.Indices.ToArray());

        // Both halves fetch deeper than the caller asked for, because fusion only has something to
        // work with where the two lists overlap and differ: a passage ranked 20th by meaning and
        // 1st by its literals is exactly the one this exists to promote, and prefetching only the
        // final limit would have discarded it before the fusion saw it.
        var depth = wanted * 4;

        var prefetch = new List<PrefetchQuery>
        {
            new()
            {
                Query = queryVector.ToArray(),
                Using = DenseVectorName,
                Filter = filter,
                Limit = depth,
            },
            new()
            {
                Query = lexical,
                Using = SparseVectorName,
                Filter = filter,
                Limit = depth,
            },
        };

        // Reciprocal rank fusion rather than a score blend: the two scores are not on one scale —
        // cosine similarity and a lexical dot product have no common unit — so combining the
        // numbers would weight whichever happened to be larger. RRF combines the ranks, which is
        // the only thing the two lists genuinely share.
        var fused = await _client.QueryAsync(
            _collection,
            Fusion.Rrf,
            prefetch: prefetch,
            limit: wanted,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return fused.Select(ToResult).ToList();
    }

    /// <summary>Forgets everything indexed for one case, so a reload starts from a clean slate.</summary>
    public async Task<bool> DeleteCaseAsync(
        string caseReference, int tenantId, CancellationToken cancellationToken = default)
    {
        if (!await _client.CollectionExistsAsync(_collection, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var filter = new Filter();
        filter.Must.Add(Conditions.Match(TenantKey, tenantId));
        filter.Must.Add(Conditions.MatchKeyword(CaseKey, caseReference));

        await _client.DeleteAsync(_collection, filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>How many chunks are indexed for one case — 0 when nothing has been loaded yet.</summary>
    public async Task<ulong> CountAsync(
        string caseReference, int tenantId, CancellationToken cancellationToken = default)
    {
        if (!await _client.CollectionExistsAsync(_collection, cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        var filter = new Filter();
        filter.Must.Add(Conditions.Match(TenantKey, tenantId));
        filter.Must.Add(Conditions.MatchKeyword(CaseKey, caseReference));

        return await _client.CountAsync(_collection, filter, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static CaseDocumentSearchResult ToResult(ScoredPoint point)
    {
        string Text(string key) =>
            point.Payload.TryGetValue(key, out var value) ? value.StringValue : string.Empty;

        long Number(string key) =>
            point.Payload.TryGetValue(key, out var value) ? value.IntegerValue : 0;

        var chunk = new CaseDocumentChunk(
            CaseReference: Text(CaseKey),
            TenantId: (int)Number(TenantKey),
            DocumentName: Text(DocumentKey),
            CategoryCode: Text(CategoryCodeKey),
            CategoryName: Text(CategoryNameKey),
            ChunkIndex: (int)Number(ChunkIndexKey),
            Text: Text(TextKey));

        return new CaseDocumentSearchResult(chunk, point.Score);
    }

    public void Dispose() => _client.Dispose();
}
