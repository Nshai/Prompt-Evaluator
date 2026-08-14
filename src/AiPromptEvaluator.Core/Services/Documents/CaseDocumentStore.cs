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

    private readonly QdrantClient _client;
    private readonly string _collection;

    public CaseDocumentStore(AppSettings settings)
    {
        var endpoint = new Uri(settings.ResolveQdrantEndpoint());
        var apiKey = string.IsNullOrWhiteSpace(settings.QdrantApiKey) ? null : settings.QdrantApiKey.Trim();

        _client = new QdrantClient(endpoint, apiKey);
        _collection = settings.ResolveCollection();
        Endpoint = endpoint.ToString();
    }

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
            return;
        }

        await _client.CreateCollectionAsync(
            _collection,
            new VectorParams { Size = (ulong)dimensions, Distance = Distance.Cosine },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _client.CreatePayloadIndexAsync(
            _collection, TenantKey, PayloadSchemaType.Integer, cancellationToken: cancellationToken).ConfigureAwait(false);
        await _client.CreatePayloadIndexAsync(
            _collection, CaseKey, PayloadSchemaType.Keyword, cancellationToken: cancellationToken).ConfigureAwait(false);
        await _client.CreatePayloadIndexAsync(
            _collection, CategoryCodeKey, PayloadSchemaType.Keyword, cancellationToken: cancellationToken).ConfigureAwait(false);
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
            var point = new PointStruct
            {
                Id = chunk.PointId,
                Vectors = vectors[i].ToArray(),
            };

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
    public async Task<IReadOnlyList<CaseDocumentSearchResult>> SearchAsync(
        string caseReference,
        int tenantId,
        ReadOnlyMemory<float> queryVector,
        int limit,
        IReadOnlyCollection<string>? categoryCodes = null,
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

        var points = await _client.QueryAsync(
            _collection,
            queryVector.ToArray(),
            filter: filter,
            limit: (ulong)Math.Max(1, limit),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return points.Select(ToResult).ToList();
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
