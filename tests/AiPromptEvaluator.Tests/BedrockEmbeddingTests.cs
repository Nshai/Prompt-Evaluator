using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

using AiPromptEvaluator;

using Microsoft.Extensions.AI;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Embeddings against Bedrock's native runtime.
///
/// <b>The failure this replaces.</b> Pointing the OpenAI client at <c>bedrock-runtime</c> did not
/// return an error — Bedrock answers an unrecognised path with <b>HTTP 200</b> and a Coral
/// <c>UnknownOperationException</c> body, which the OpenAI client reads as a valid response holding
/// no embeddings. What surfaced was "Expected the number of embeddings (0) to match the number of
/// inputs (1)", a message naming neither the endpoint, the protocol, nor the mistake, on a run that
/// then reported "embeddings billed but the provider reported no token count".
///
/// Every request and response shape asserted here was verified against a live eu-west-1 endpoint
/// before it was written, including the ones that fail.
/// </summary>
public class BedrockEmbeddingTests
{
    /// <summary>Captures what was sent and replies with whatever the test wants back.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<string, (HttpStatusCode Status, string Body)> _reply;

        public StubHandler(Func<string, (HttpStatusCode, string)> reply) => _reply = reply;

        public List<string> Urls { get; } = [];

        public List<string> Bodies { get; } = [];

        public List<string?> ContentTypes { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            lock (Urls)
            {
                Urls.Add(request.RequestUri!.ToString());
                Bodies.Add(body);
                ContentTypes.Add(request.Content?.Headers.ContentType?.ToString());
            }

            var (status, reply) = _reply(body);

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(reply, Encoding.UTF8, "application/json"),
            };
        }
    }

    /// <summary>A Titan response for the width the request asked for.</summary>
    private static (HttpStatusCode, string) Titan(string requestBody)
    {
        var dimensions = JsonNode.Parse(requestBody)?["dimensions"]?.GetValue<int>() ?? 1024;
        var vector = new JsonArray([.. Enumerable.Range(0, dimensions).Select(i => JsonValue.Create(i * 0.001f))]);

        return (HttpStatusCode.OK,
            new JsonObject { ["embedding"] = vector, ["inputTextTokenCount"] = 3 }.ToJsonString());
    }

    private static BedrockEmbeddingGenerator Generator(StubHandler handler, int? dimensions = 1024) =>
        new("https://bedrock-runtime.eu-west-1.amazonaws.com", "ABSK-test",
            "amazon.titan-embed-text-v2:0", dimensions, maxParallelism: 4,
            http: new HttpClient(handler));

    [Fact]
    public async Task ItPostsToTitansInvokePathWithTitansRequestShape()
    {
        var handler = new StubHandler(Titan);
        using var generator = Generator(handler);

        await generator.GenerateAsync(["case document"]);

        Assert.Equal(
            "https://bedrock-runtime.eu-west-1.amazonaws.com/model/amazon.titan-embed-text-v2%3A0/invoke",
            Assert.Single(handler.Urls));

        var sent = JsonNode.Parse(Assert.Single(handler.Bodies))!;

        Assert.Equal("case document", sent["inputText"]!.GetValue<string>());
        Assert.Equal(1024, sent["dimensions"]!.GetValue<int>());
        Assert.True(sent["normalize"]!.GetValue<bool>());
    }

    /// <summary>
    /// <b>Found against the live endpoint, not in review.</b> Bedrock refuses a Content-Type that
    /// carries a charset parameter — "The provided Content Type is invalid or not supported for
    /// this model" — and <c>new StringContent(json, Encoding.UTF8, "application/json")</c>, the
    /// obvious spelling, always appends "; charset=utf-8". Every unit test here passed against a
    /// stub while the real call failed on its first request.
    /// </summary>
    [Fact]
    public async Task TheContentTypeCarriesNoCharsetParameter()
    {
        var handler = new StubHandler(Titan);
        using var generator = Generator(handler);

        await generator.GenerateAsync(["case document"]);

        Assert.Equal("application/json", Assert.Single(handler.ContentTypes));
    }

    [Fact]
    public async Task ItReturnsTheVectorAtTheRequestedWidth()
    {
        var handler = new StubHandler(Titan);
        using var generator = Generator(handler);

        var embeddings = await generator.GenerateAsync(["case document"]);

        Assert.Equal(1024, Assert.Single(embeddings).Vector.Length);
    }

    /// <summary>
    /// Titan takes one string per call and has no batch form, so a batch becomes a request each.
    /// </summary>
    [Fact]
    public async Task ABatchBecomesOneRequestPerInput()
    {
        var handler = new StubHandler(Titan);
        using var generator = Generator(handler);

        var embeddings = await generator.GenerateAsync(["one", "two", "three"]);

        Assert.Equal(3, handler.Urls.Count);
        Assert.Equal(3, embeddings.Count);
    }

    /// <summary>
    /// <b>Order is correctness here, not tidiness.</b> CaseDocumentIndexer zips the returned
    /// vectors against its chunk list by index, so a reordering would attach every vector to the
    /// wrong chunk — and nothing downstream could detect it. The requests are concurrent, so this
    /// is the property that has to be held deliberately.
    /// </summary>
    [Fact]
    public async Task VectorsComeBackInInputOrder()
    {
        // Each input gets a vector whose width encodes its position, so a shuffle is visible.
        var handler = new StubHandler(body =>
        {
            var text = JsonNode.Parse(body)!["inputText"]!.GetValue<string>();
            var width = int.Parse(text);
            // A fresh node per element: JsonArray refuses to adopt one instance twice.
            var vector = new JsonArray([.. Enumerable.Range(0, width).Select(_ => JsonValue.Create(0.5f))]);

            return (HttpStatusCode.OK,
                new JsonObject { ["embedding"] = vector, ["inputTextTokenCount"] = 1 }.ToJsonString());
        });

        using var generator = Generator(handler, dimensions: null);

        var inputs = Enumerable.Range(1, 24).Select(i => i.ToString()).ToList();
        var embeddings = await generator.GenerateAsync(inputs);

        Assert.Equal(inputs.Select(int.Parse), embeddings.Select(e => e.Vector.Length));
    }

    /// <summary>
    /// The run report said "embeddings billed but the provider reported no token count". Titan
    /// reports what it billed on every response; summing it is what makes the cost real.
    /// </summary>
    [Fact]
    public async Task BilledTokensAreSummedAcrossTheBatch()
    {
        var handler = new StubHandler(Titan);
        using var generator = Generator(handler);

        var embeddings = await generator.GenerateAsync(["one", "two", "three"]);

        Assert.Equal(9, embeddings.Usage?.InputTokenCount);
    }

    /// <summary>
    /// <b>The whole point.</b> A 200 carrying no embedding is how Bedrock reports an unknown
    /// operation, and reading it as "zero embeddings" is the original defect. It must be an error,
    /// and the error must say what came back.
    /// </summary>
    [Fact]
    public async Task AnUnknownOperationIsAnErrorEvenThoughItIsHttp200()
    {
        var handler = new StubHandler(_ => (HttpStatusCode.OK,
            """{"Output":{"__type":"com.amazon.coral.service#UnknownOperationException"},"Version":"1.0"}"""));

        using var generator = Generator(handler);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync(["case document"]));

        Assert.Contains("UnknownOperationException", error.Message);
        Assert.Contains("200", error.Message);
        Assert.Contains("Bedrock answers an unknown operation with 200", error.Message);
    }

    /// <summary>
    /// A refusal the app could not have predicted still carries the provider's own words — those
    /// are the useful thing when the app has nothing better to say.
    /// </summary>
    [Fact]
    public async Task ARefusalCarriesBedrocksOwnMessage()
    {
        var handler = new StubHandler(_ => (HttpStatusCode.BadRequest,
            """{"message":"You don't have access to the model with the specified model ID."}"""));

        using var generator = Generator(handler);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync(["case document"]));

        Assert.Contains("don't have access", error.Message);
        Assert.Contains("amazon.titan-embed-text-v2:0", error.Message);
    }

    // ──────────────────────────────────────────────
    // Dimensions the model cannot emit
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>Refused before a socket is opened.</b> Titan v2 emits 1024/512/256; asking for 1536
    /// returns "Malformed input request: #: only 1 subschema matches out of 2", which names
    /// neither the field, the value, nor the widths that would have worked. The app knows the
    /// model and the configured width in advance, so it says the useful thing instead of relaying
    /// that one — and says it without spending a call.
    /// </summary>
    [Fact]
    public void AWidthTheModelCannotEmitIsRefusedWithoutCallingBedrock()
    {
        var handler = new StubHandler(Titan);

        var error = Assert.Throws<InvalidOperationException>(() => Generator(handler, dimensions: 1536));

        Assert.Empty(handler.Urls);
        Assert.Contains("emits 1024, 512, 256-dimension vectors", error.Message);
        Assert.Contains("configured embedding dimensions are 1536", error.Message);
        Assert.Contains("Set the dimensions to 1024", error.Message);
        Assert.Contains("recreated", error.Message);
    }

    /// <summary>
    /// 1536 is the value people arrive with — it is Titan v1's fixed width and OpenAI's default —
    /// so the message says plainly that no setting produces it here, rather than leaving the
    /// reader to wonder which knob they missed.
    /// </summary>
    [Fact]
    public void TheMessageExplainsWhere1536ComesFrom() =>
        Assert.Contains(
            "no setting makes this model produce it",
            BedrockEmbeddingModels.DescribeUnsupportedDimensions("amazon.titan-embed-text-v2:0", 1536));

    [Theory]
    [InlineData("amazon.titan-embed-text-v2:0", 1024)]
    [InlineData("amazon.titan-embed-text-v2:0", 512)]
    [InlineData("cohere.embed-v4:0", 1536)]
    public void ASupportedWidthPassesWithoutComment(string model, int dimensions) =>
        Assert.Null(BedrockEmbeddingModels.DescribeUnsupportedDimensions(model, dimensions));

    /// <summary>
    /// A model this build has never heard of asserts nothing. Guessing on a model added to Bedrock
    /// after this shipped would refuse a configuration that works, which is worse than relaying an
    /// unhelpful error.
    /// </summary>
    [Fact]
    public void AnUnknownModelIsNotSecondGuessed() =>
        Assert.Null(BedrockEmbeddingModels.DescribeUnsupportedDimensions("amazon.titan-embed-text-v9", 999));

    [Fact]
    public async Task OmittingDimensionsTakesTheModelsDefault()
    {
        var handler = new StubHandler(Titan);
        using var generator = Generator(handler, dimensions: null);

        await generator.GenerateAsync(["case document"]);

        var sent = JsonNode.Parse(Assert.Single(handler.Bodies))!;

        Assert.Null(sent["dimensions"]);
        Assert.Null(sent["normalize"]);
    }

    // ──────────────────────────────────────────────
    // Provider selection
    // ──────────────────────────────────────────────

    /// <summary>
    /// The guard for the configuration that started this. The OpenAI client against
    /// bedrock-runtime cannot work and cannot say so, so it is refused by name before the call.
    /// </summary>
    [Fact]
    public void TheOpenAiClientIsRefusedAgainstBedrockRuntime()
    {
        var settings = new AppSettings
        {
            OpenAiApiKey = "ABSK-test",
            OpenAiBaseUrl = "https://bedrock-mantle.eu-west-1.api.aws/v1",
            EmbeddingBaseUrl = "https://bedrock-runtime.eu-west-1.amazonaws.com",
            EmbeddingModel = "amazon.titan-embed-text-v2:0",
            EmbeddingProvider = EmbeddingProviders.OpenAi,
        };

        var error = Assert.Throws<InvalidOperationException>(
            () => AiClientFactory.CreateEmbeddingGenerator(settings));

        Assert.Contains("not OpenAI-compatible", error.Message);
        Assert.Contains(EmbeddingProviders.Bedrock, error.Message);
    }

    [Fact]
    public void ChoosingBedrockBuildsTheBedrockGenerator()
    {
        var settings = new AppSettings
        {
            OpenAiApiKey = "ABSK-test",
            EmbeddingBaseUrl = "https://bedrock-runtime.eu-west-1.amazonaws.com",
            EmbeddingModel = "amazon.titan-embed-text-v2:0",
            EmbeddingDimensions = 1024,
            EmbeddingProvider = EmbeddingProviders.Bedrock,
        };

        using var generator = AiClientFactory.CreateEmbeddingGenerator(settings);

        Assert.IsType<BedrockEmbeddingGenerator>(generator);
    }

    /// <summary>
    /// Every region reads the same, so the guard is not a list of hostnames to maintain.
    /// </summary>
    [Theory]
    [InlineData("https://bedrock-runtime.eu-west-1.amazonaws.com", true)]
    [InlineData("https://bedrock-runtime.us-east-1.amazonaws.com", true)]
    [InlineData("https://bedrock-mantle.eu-west-1.api.aws/v1", false)]
    [InlineData("https://api.openai.com/v1", false)]
    public void BedrocksRuntimeIsRecognisedByItsService(string url, bool expected) =>
        Assert.Equal(expected, AiClientFactory.LooksLikeBedrock(url));

    // ──────────────────────────────────────────────
    // Model ids are not portable between the two endpoints
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>The observed misconfiguration.</b> The two endpoints name the same model differently: an
    /// OpenAI-compatible gateway lists <c>anthropic.claude-haiku-4-5</c>, Bedrock's runtime wants
    /// <c>eu.anthropic.claude-haiku-4-5-20251001-v1:0</c>. Sending the second to the first gets a
    /// bare "The model ... does not exist", which reads as though the model were unavailable
    /// rather than misnamed — so the reader goes looking for an entitlement problem.
    /// </summary>
    [Fact]
    public void ABedrockModelIdSentToAGatewayIsRefusedByName()
    {
        var settings = new AppSettings
        {
            OpenAiApiKey = "ABSK-test",
            OpenAiBaseUrl = "https://bedrock-mantle.eu-west-1.api.aws/v1",
            SelectedModel = "eu.anthropic.claude-haiku-4-5-20251001-v1:0",
        };

        var error = Assert.Throws<InvalidOperationException>(
            () => AiClientFactory.CreateChatClient(settings));

        Assert.Contains("Bedrock model or inference-profile id", error.Message);
        Assert.Contains("/v1/models", error.Message);
    }

    [Fact]
    public void TheChatEndpointIsRefusedAgainstBedrockRuntime()
    {
        var settings = new AppSettings
        {
            OpenAiApiKey = "ABSK-test",
            OpenAiBaseUrl = "https://bedrock-runtime.eu-west-1.amazonaws.com",
            SelectedModel = "anthropic.claude-haiku-4-5",
        };

        var error = Assert.Throws<InvalidOperationException>(
            () => AiClientFactory.CreateChatClient(settings));

        Assert.Contains("does not serve the OpenAI chat-completions API", error.Message);
    }

    [Theory]
    [InlineData("eu.anthropic.claude-haiku-4-5-20251001-v1:0", true)]
    [InlineData("us.anthropic.claude-sonnet-5-20250101-v1:0", true)]
    [InlineData("amazon.titan-embed-text-v2:0", true)]
    [InlineData("anthropic.claude-haiku-4-5", false)]
    [InlineData("gpt-4.1", false)]
    [InlineData("openai.gpt-oss-120b", false)]
    // Ollama tags models this way. Refusing a colon outright would break a setup the app
    // supports in order to catch one it does not.
    [InlineData("llama3:8b", false)]
    [InlineData("nomic-embed-text:latest", false)]
    public void BedrockIdsAreRecognisedByTheirNamingNotByAColon(string modelId, bool expected) =>
        Assert.Equal(expected, AiClientFactory.IsBedrockModelId(modelId));

    /// <summary>
    /// The provider changes which vectors a run produces, so it belongs in the fingerprint — two
    /// runs on different embedding protocols are not the same run.
    /// </summary>
    [Fact]
    public void TheProviderIsPartOfTheRunFingerprint() =>
        Assert.DoesNotContain(nameof(AppSettings.EmbeddingProvider), AppSettings.NotFingerprinted);
}
