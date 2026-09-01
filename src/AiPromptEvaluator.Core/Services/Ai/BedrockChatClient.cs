using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.AI;

namespace AiPromptEvaluator;

/// <summary>
/// Chat against Amazon Bedrock's <c>Converse</c> API.
///
/// <b>Why this exists rather than a base URL.</b> Two things the OpenAI-compatible path cannot do
/// were blocking the pipeline at once, and Converse settles both.
///
/// <list type="number">
/// <item><b>It reaches the model at all.</b> The gateway in front of Bedrock lists every Claude
/// model and serves none of them: <c>/v1/chat/completions</c> answers "the model
/// 'anthropic.claude-sonnet-5' does not support the '/v1/chat/completions' API", and
/// <c>/v1/messages</c> and <c>/v1/responses</c> are 404 and 400. Claude is reachable in this
/// account only through the runtime's own API.</item>
/// <item><b>It can be told to cache.</b> The assessor makes one call per requirement — a
/// deliberate trade for a pack small enough to attend to, a failure isolated to one group, and no
/// group conditioned on the groups before it — and the note in the runner says what pays for the
/// repetition that shape costs: <i>"keeps the check header identical at the front of every prompt
/// so the provider's prefix cache covers it"</i>. Nothing ever asked for that cache. OpenAI's
/// caching is automatic and unaskable; Anthropic's is explicit, and Converse spells it
/// <c>cachePoint</c>. Measured against this account: a 4,902-token system prompt was written once
/// and read back on every later call.</item>
/// </list>
///
/// <b>Authentication is a bearer token</b> — an AWS Bedrock API key (<c>ABSK…</c>) — which is what
/// the account already uses. SigV4 signing is not implemented.
///
/// <b>Two sampling pins cannot be honoured here</b>, and both are reported through
/// <see cref="UnsupportedSamplingParameters"/> rather than dropped in silence. Converse has no seed
/// field at all; and Claude on Bedrock refuses temperature and top-p together — <i>"`temperature`
/// and `top_p` cannot both be specified for this model"</i> — so a run pinned to temperature 0
/// keeps the temperature and loses the top-p, which at temperature 0 was deciding nothing anyway.
/// </summary>
public sealed class BedrockChatClient : IChatClient
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _endpoint;
    private readonly string _modelId;

    private static readonly JsonSerializerOptions Compact = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public BedrockChatClient(
        string endpoint, string apiKey, string modelId, HttpClient? http = null)
    {
        _endpoint = endpoint.TrimEnd('/');
        _modelId = modelId.Trim();
        _ownsHttp = http is null;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        if (_http.DefaultRequestHeaders.Authorization is null)
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }
    }

    /// <summary>
    /// Sampling parameters the caller pinned that this route will not accept, most recent call
    /// first heard.
    ///
    /// <b>Recorded rather than silently dropped.</b> Every one of them is a lever the app pins on
    /// purpose — a QA finding is a judgement that should not change because the sampler rolled
    /// differently — so a run that could not pin one has weaker reproducibility than its settings
    /// claim, and something has to be able to say so.
    /// </summary>
    public IReadOnlyList<string> UnsupportedSamplingParameters => _unsupported;

    private readonly List<string> _unsupported = [];

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(messages, options);

        using var content = new StringContent(request.ToJsonString(Compact), Encoding.UTF8);

        // Bedrock refuses a Content-Type carrying a charset parameter, and the three-argument
        // StringContent constructor always appends one.
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var url = $"{_endpoint}/model/{Uri.EscapeDataString(_modelId)}/converse";

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
                $"Bedrock refused the request for \"{_modelId}\" at {_endpoint} "
                + $"(HTTP {(int)response.StatusCode}). {Detail(body)}");
        }

        var json = JsonNode.Parse(body) as JsonObject
            ?? throw new InvalidOperationException(
                $"Bedrock returned a reply that is not a JSON object for \"{_modelId}\".");

        return ToResponse(json);
    }

    /// <summary>
    /// The Converse request body.
    ///
    /// <b>Cache points are placed where the text actually repeats</b>, which is the only place they
    /// pay: after the system prompt, identical on every call in a run, and after the first block of
    /// a multi-part user message, which is how a caller hands over a shared check header without
    /// this class having to guess where one ends.
    /// </summary>
    internal JsonObject BuildRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var system = new JsonArray();
        var conversation = new JsonArray();

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                var text = message.Text;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    system.Add(new JsonObject { ["text"] = text });
                }

                continue;
            }

            var content = new JsonArray();
            var blocks = message.Contents.OfType<TextContent>().ToList();

            for (var i = 0; i < blocks.Count; i++)
            {
                content.Add(new JsonObject { ["text"] = blocks[i].Text });

                // A caller that split its prompt into blocks is telling us the split is meaningful:
                // the leading block is the part every call in the check shares.
                if (i == 0 && blocks.Count > 1)
                {
                    content.Add(CachePoint());
                }
            }

            if (content.Count == 0 && !string.IsNullOrWhiteSpace(message.Text))
            {
                content.Add(new JsonObject { ["text"] = message.Text });
            }

            conversation.Add(new JsonObject
            {
                ["role"] = message.Role == ChatRole.Assistant ? "assistant" : "user",
                ["content"] = content,
            });
        }

        var request = new JsonObject { ["messages"] = conversation };

        if (system.Count > 0)
        {
            // The system prompt is identical on every call of a run, so this is the one breakpoint
            // that pays across all of them rather than only within a check.
            system.Add(CachePoint());
            request["system"] = system;
        }

        var inference = new JsonObject();

        if (options?.MaxOutputTokens is { } max)
        {
            inference["maxTokens"] = Math.Max(1, max);
        }

        _unsupported.Clear();

        // Claude on Bedrock refuses both at once — "`temperature` and `top_p` cannot both be
        // specified for this model. Please use only one." Temperature is kept because it is the
        // lever that actually pins the sampler here: at temperature 0 the nucleus is a single
        // token and any top-p is inert, so dropping top-p costs nothing that temperature has not
        // already decided. Where only top-p was pinned it is sent unchanged.
        var temperature = options?.Temperature;
        var topP = options?.TopP;

        if (temperature is { } t)
        {
            inference["temperature"] = t;

            if (topP is not null)
            {
                _unsupported.Add("topP (Bedrock accepts temperature or topP, not both)");
            }
        }
        else if (topP is { } p)
        {
            inference["topP"] = p;
        }

        if (inference.Count > 0)
        {
            request["inferenceConfig"] = inference;
        }

        // Converse has no seed field at all. This is a real loss of reproducibility for a tool
        // whose findings must be repeatable, not a tidy-up.
        if (options?.Seed is not null)
        {
            _unsupported.Add("seed (Converse has no seed parameter)");
        }

        if (ToolConfig(options?.ResponseFormat) is { } tools)
        {
            request["toolConfig"] = tools;
        }

        return request;
    }

    private static JsonObject CachePoint() =>
        new() { ["cachePoint"] = new JsonObject { ["type"] = "default" } };

    /// <summary>
    /// A JSON-schema response format, expressed the way Converse accepts one.
    ///
    /// Converse has no <c>response_format</c>. The equivalent is a single tool whose input schema
    /// is the schema wanted, with the model forced to call it — the reply then arrives as a
    /// <c>toolUse</c> block whose input is the object, which is a stronger constraint than asking
    /// for JSON in prose and is what <see cref="CheckFinding.ParseOutcome"/> depends on not having
    /// to guess at.
    /// </summary>
    internal static JsonObject? ToolConfig(ChatResponseFormat? format)
    {
        if (format is not ChatResponseFormatJson { Schema: { } schema })
        {
            return null;
        }

        var name = "respond";

        return new JsonObject
        {
            ["tools"] = new JsonArray(
                new JsonObject
                {
                    ["toolSpec"] = new JsonObject
                    {
                        ["name"] = name,
                        ["description"] = "Return the finding in this shape.",
                        ["inputSchema"] = new JsonObject
                        {
                            ["json"] = JsonNode.Parse(schema.ToString()),
                        },
                    },
                }),
            ["toolChoice"] = new JsonObject
            {
                ["tool"] = new JsonObject { ["name"] = name },
            },
        };
    }

    /// <summary>
    /// The reply, with the tool-use object flattened back to text.
    ///
    /// Callers of this pipeline parse JSON out of the response text and have no notion of a tool
    /// call, so a forced tool call is unwrapped here rather than leaking a Bedrock shape into
    /// every parser.
    /// </summary>
    internal static ChatResponse ToResponse(JsonObject json)
    {
        var blocks = json["output"]?["message"]?["content"] as JsonArray ?? [];
        var text = new StringBuilder();

        foreach (var block in blocks.OfType<JsonObject>())
        {
            if (block["text"]?.GetValue<string>() is { Length: > 0 } plain)
            {
                text.Append(plain);
            }
            else if (block["toolUse"]?["input"] is { } input)
            {
                text.Append(input.ToJsonString(Compact));
            }
        }

        var usage = json["usage"] as JsonObject;

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text.ToString()))
        {
            FinishReason = json["stopReason"]?.GetValue<string>() switch
            {
                "max_tokens" => ChatFinishReason.Length,
                "content_filtered" => ChatFinishReason.ContentFilter,
                _ => ChatFinishReason.Stop,
            },
            Usage = new UsageDetails
            {
                InputTokenCount = Count(usage, "inputTokens"),
                OutputTokenCount = Count(usage, "outputTokens"),
                TotalTokenCount = Count(usage, "totalTokens"),
                AdditionalCounts = new AdditionalPropertiesDictionary<long>
                {
                    ["cache_read_input_tokens"] = Count(usage, "cacheReadInputTokens") ?? 0,
                    ["cache_creation_input_tokens"] = Count(usage, "cacheWriteInputTokens") ?? 0,
                },
            },
        };
    }

    private static long? Count(JsonObject? usage, string key) =>
        usage?[key] is { } node && node.GetValueKind() == JsonValueKind.Number
            ? node.GetValue<long>()
            : null;

    private static string Detail(string body)
    {
        try
        {
            if (JsonNode.Parse(body) is JsonObject json
                && json["message"]?.GetValue<string>() is { Length: > 0 } message)
            {
                return message;
            }
        }
        catch (JsonException)
        {
            // Not JSON; the raw body below is the better answer.
        }

        var trimmed = body.Trim();

        return trimmed.Length <= 300 ? trimmed : trimmed[..300] + "…";
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Streaming is not implemented for Bedrock Converse; this pipeline reads whole replies.");

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
