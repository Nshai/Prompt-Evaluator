using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.AI;

namespace AiPromptEvaluator;

/// <summary>The wire protocols an embedding endpoint may speak.</summary>
public static class EmbeddingProviders
{
    /// <summary>OpenAI's <c>POST {base}/embeddings</c>. Any compatible gateway.</summary>
    public const string OpenAi = "OpenAI";

    /// <summary>AWS Bedrock's <c>POST {base}/model/{id}/invoke</c>.</summary>
    public const string Bedrock = "Bedrock";

    /// <summary>The values a settings file may carry, for validation and the form.</summary>
    public static readonly IReadOnlyList<string> All = [OpenAi, Bedrock];

    public static bool IsBedrock(string? provider) =>
        string.Equals(provider?.Trim(), Bedrock, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The vector widths the Bedrock embedding models emit.
///
/// <b>Why this is worth hard-coding.</b> Ask Titan v2 for 1536 and Bedrock answers
/// <c>"Malformed input request: #: only 1 subschema matches out of 2, please reformat your input
/// and try again"</c> — a message about JSON schema branches that names neither the field, the
/// value, nor the widths that would have worked. The app knows the model and the configured width
/// before it makes the call, so it can say the useful thing instead of relaying that one.
///
/// 1536 is the value people arrive with, because it is Titan <i>v1</i>'s fixed width and OpenAI's
/// <c>text-embedding-3-small</c> default. There is no setting that makes v2 produce it.
/// </summary>
public static class BedrockEmbeddingModels
{
    private static readonly Dictionary<string, int[]> Widths =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["amazon.titan-embed-text-v2:0"] = [1024, 512, 256],
            ["cohere.embed-v4:0"] = [1536, 1024, 512, 256],
            ["cohere.embed-english-v3"] = [1024],
            ["cohere.embed-multilingual-v3"] = [1024],
        };

    /// <summary>
    /// The widths a model supports, or empty where this build has never heard of it — in which
    /// case nothing is asserted and the provider gets to answer for itself.
    /// </summary>
    public static IReadOnlyList<int> SupportedDimensions(string modelId) =>
        Widths.TryGetValue(modelId.Trim(), out var widths) ? widths : [];

    /// <summary>
    /// The complaint about a configured width, or null where there is nothing to complain about.
    /// </summary>
    public static string? DescribeUnsupportedDimensions(string modelId, int dimensions)
    {
        var supported = SupportedDimensions(modelId);

        if (supported.Count == 0 || supported.Contains(dimensions))
        {
            return null;
        }

        var alternative = supported.Contains(1536)
            ? string.Empty
            : dimensions == 1536
                ? " 1536 is Titan v1's fixed width and OpenAI's default; no setting makes this "
                  + "model produce it."
                : string.Empty;

        return $"\"{modelId}\" emits {string.Join(", ", supported)}-dimension vectors, but the "
            + $"configured embedding dimensions are {dimensions}.{alternative} Set the dimensions "
            + $"to {supported[0]} in Settings. The Qdrant collection is created at that width, so "
            + "an existing collection has to be recreated and the case re-indexed.";
    }
}

/// <summary>
/// Embeddings from Amazon Bedrock's native runtime, for Titan text embedding models.
///
/// <b>Why this is not just a base URL.</b> Bedrock is not OpenAI-compatible. It takes
/// <c>POST /model/{modelId}/invoke</c> with the model's own request shape and returns the model's
/// own response shape; there is no <c>/embeddings</c> route and no <c>data[]</c> envelope. Pointing
/// the OpenAI client at <c>bedrock-runtime</c> produces a uniquely unhelpful failure, and it is
/// worth writing down because it cost a session: <b>Bedrock answers an unrecognised path with HTTP
/// 200</b> and a body of <c>{"Output":{"__type":"com.amazon.coral.service#UnknownOperationException"}}</c>.
/// The OpenAI client parses that as a valid response holding no embeddings, and the user is told
/// "Expected the number of embeddings (0) to match the number of inputs (1)" — a message that names
/// neither the endpoint nor the protocol nor the mistake.
///
/// <b>Titan embeds one string per call.</b> There is no batch form, so a batch of chunks becomes a
/// request each. They are issued concurrently under a bound and written back <b>by position</b>,
/// because <c>CaseDocumentIndexer</c> zips the returned vectors against its chunk list by index —
/// reordering here would attach every vector to the wrong chunk, and nothing downstream could
/// detect it.
///
/// <b>Auth is a bearer token</b>, which an AWS Bedrock API key (<c>ABSK…</c>) satisfies directly.
/// SigV4 request signing is not implemented; a setup using raw IAM credentials needs a gateway or
/// a signing proxy in front.
/// </summary>
public sealed class BedrockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _endpoint;
    private readonly string _modelId;
    private readonly int? _dimensions;
    private readonly int _maxParallelism;

    /// <param name="dimensions">
    /// Requested vector width. Titan v2 accepts <b>1024, 512 or 256</b> and rejects anything else
    /// outright — 1536 is Titan v1's fixed width, and asking v2 for it fails with "Malformed input
    /// request", not with a wrong-sized vector. Null omits the field and takes the model's default.
    /// </param>
    public BedrockEmbeddingGenerator(
        string endpoint,
        string apiKey,
        string modelId,
        int? dimensions = null,
        int maxParallelism = 6,
        HttpClient? http = null)
    {
        _endpoint = endpoint.TrimEnd('/');
        _modelId = modelId.Trim();

        // Checked here, before a socket is opened, because the answer is knowable without asking
        // and Bedrock's own answer is not usable: a width this model cannot emit comes back as
        // "Malformed input request: #: only 1 subschema matches out of 2", which names neither the
        // field nor the value nor the widths that would have worked. A caller that is going to be
        // refused should be refused in words it can act on.
        if (dimensions is { } requested
            && BedrockEmbeddingModels.DescribeUnsupportedDimensions(_modelId, requested) is { } problem)
        {
            throw new InvalidOperationException(problem);
        }

        _dimensions = dimensions;
        _maxParallelism = Math.Max(1, maxParallelism);
        _ownsHttp = http is null;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

        if (_http.DefaultRequestHeaders.Authorization is null)
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }
    }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var inputs = values as IReadOnlyList<string> ?? [.. values];

        // By position, never appended. See the class remarks: the indexer pairs vectors with
        // chunks by index, so order is correctness rather than tidiness.
        var vectors = new Embedding<float>[inputs.Count];
        var tokens = new long[inputs.Count];

        await ParallelWork.ForEachAsync(
            inputs.Count,
            _maxParallelism,
            async (i, token) =>
            {
                var (vector, billed) = await EmbedAsync(inputs[i], token).ConfigureAwait(false);
                vectors[i] = new Embedding<float>(vector);
                tokens[i] = billed;
            },
            cancellationToken).ConfigureAwait(false);

        var generated = new GeneratedEmbeddings<Embedding<float>>(vectors);
        var total = tokens.Sum();

        // Titan reports what it billed on every call. Summing it here is what stops the run
        // report saying "embeddings billed but the provider reported no token count".
        if (total > 0)
        {
            generated.Usage = new UsageDetails
            {
                InputTokenCount = total,
                TotalTokenCount = total,
            };
        }

        return generated;
    }

    private async Task<(float[] Vector, long Tokens)> EmbedAsync(
        string input, CancellationToken cancellationToken)
    {
        var request = new JsonObject { ["inputText"] = input };

        if (_dimensions is { } d)
        {
            request["dimensions"] = d;
            request["normalize"] = true;
        }

        var url = $"{_endpoint}/model/{Uri.EscapeDataString(_modelId)}/invoke";

        using var content = new StringContent(request.ToJsonString(), Encoding.UTF8);

        // Bedrock rejects a Content-Type carrying a charset parameter — "The provided Content Type
        // is invalid or not supported for this model" — and the three-argument StringContent
        // constructor always appends "; charset=utf-8". So the header is set exactly, after the
        // fact. The body is UTF-8 regardless; Bedrock simply will not be told so in that header.
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http
            .SendAsync(message, cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Bedrock refused the embedding request for \"{_modelId}\" at {_endpoint} "
                + $"(HTTP {(int)response.StatusCode}). {Message(body)}");
        }

        var json = JsonNode.Parse(body) as JsonObject;

        // A 200 that carries no embedding. This is the case that has to be caught by hand: it is
        // how Bedrock reports an unknown operation, and reading it as "zero embeddings" is exactly
        // the failure this class exists to make impossible.
        if (json?["embedding"] is not JsonArray array)
        {
            throw new InvalidOperationException(
                $"Bedrock returned HTTP {(int)response.StatusCode} with no embedding for "
                + $"\"{_modelId}\" at {_endpoint}. {Message(body)} "
                + "A 200 with no embedding usually means the path or the model id is wrong — "
                + "Bedrock answers an unknown operation with 200 rather than 404.");
        }

        var vector = new float[array.Count];

        for (var i = 0; i < array.Count; i++)
        {
            vector[i] = array[i]?.GetValue<float>() ?? 0f;
        }

        var billed = json["inputTextTokenCount"]?.GetValue<long>() ?? 0L;

        return (vector, billed);
    }

    /// <summary>
    /// The provider's own words about a failure. Bedrock puts them under <c>message</c> for a
    /// refusal and under <c>Output.__type</c> for an unknown operation; anything else is passed
    /// through truncated, because a body nobody anticipated is still better than "an error".
    /// </summary>
    private static string Message(string body)
    {
        try
        {
            if (JsonNode.Parse(body) is JsonObject json)
            {
                if (json["message"]?.GetValue<string>() is { Length: > 0 } message)
                {
                    return message;
                }

                if (json["Output"]?["__type"]?.GetValue<string>() is { Length: > 0 } type)
                {
                    return $"The endpoint reported {type}.";
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON. The raw body below is the better answer anyway.
        }

        var trimmed = body.Trim();

        return trimmed.Length <= 300 ? trimmed : trimmed[..300] + "…";
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}
