using System.Text.Json.Nodes;

using AiPromptEvaluator;

using Microsoft.Extensions.AI;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Chat over Bedrock's Converse API.
///
/// <b>Two blockers, one client.</b> The OpenAI-compatible gateway in this account lists every
/// Claude model and serves none of them — <c>/v1/chat/completions</c> answers "does not support the
/// '/v1/chat/completions' API" for Sonnet, Opus and Haiku alike — so Converse is the only route to
/// the model. It is also the only route that can be <i>told</i> to cache: the assessor sends one
/// call per requirement and repeats an identical system prompt on all 88 of them, and until now
/// nothing ever asked a provider to keep it.
///
/// Every shape asserted here was verified against a live eu-west-1 endpoint first, including the
/// two sampling parameters that turn out to be unsendable.
/// </summary>
public class BedrockChatClientTests
{
    private static BedrockChatClient Client() =>
        new("https://bedrock-runtime.eu-west-1.amazonaws.com", "ABSK-test",
            "eu.anthropic.claude-haiku-4-5-20251001-v1:0");

    private static JsonObject Request(IEnumerable<ChatMessage> messages, ChatOptions? options = null)
    {
        using var client = Client();
        return client.BuildRequest(messages, options);
    }

    // ──────────────────────────────────────────────
    // Cache points — the reason this client exists
    // ──────────────────────────────────────────────

    /// <summary>
    /// The system prompt is identical on every call of a run, so a breakpoint after it is the one
    /// that pays across all of them rather than only within a check. Measured live: a 4,902-token
    /// system prompt written once and read back on every later call.
    /// </summary>
    [Fact]
    public void ACachePointFollowsTheSystemPrompt()
    {
        var request = Request([
            new(ChatRole.System, "standing rules"),
            new(ChatRole.User, "assess this"),
        ]);

        var system = request["system"]!.AsArray();

        Assert.Equal("standing rules", system[0]!["text"]!.GetValue<string>());
        Assert.Equal("default", system[1]!["cachePoint"]!["type"]!.GetValue<string>());
    }

    /// <summary>
    /// A caller that splits its user prompt into blocks is saying the split means something: the
    /// leading block is the check header every group of that check shares. The client caches
    /// between them rather than guessing where a header ends by inspecting text.
    /// </summary>
    [Fact]
    public void AMultiPartUserMessageIsCachedAfterItsFirstBlock()
    {
        var request = Request([
            new(ChatRole.User, [new TextContent("check header"), new TextContent("group pack")]),
        ]);

        var content = request["messages"]![0]!["content"]!.AsArray();

        Assert.Equal("check header", content[0]!["text"]!.GetValue<string>());
        Assert.NotNull(content[1]!["cachePoint"]);
        Assert.Equal("group pack", content[2]!["text"]!.GetValue<string>());
    }

    /// <summary>
    /// A single-block message gets no breakpoint. Caching a prefix that nothing else shares costs
    /// a 1.25x write and buys no read.
    /// </summary>
    [Fact]
    public void ASinglePartUserMessageIsNotCached()
    {
        var content = Request([new(ChatRole.User, "one block")])["messages"]![0]!["content"]!.AsArray();

        Assert.Single(content);
        Assert.Equal("one block", content[0]!["text"]!.GetValue<string>());
    }

    // ──────────────────────────────────────────────
    // Sampling parameters this route will not take
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>Found live, not in review.</b> Claude on Bedrock refuses both at once: "`temperature`
    /// and `top_p` cannot both be specified for this model. Please use only one." The app pins
    /// both by default, so every call would have failed.
    /// </summary>
    [Fact]
    public void TemperatureWinsWhenBothArePinned()
    {
        var request = Request(
            [new(ChatRole.User, "hello")],
            new ChatOptions { Temperature = 0f, TopP = 1f });

        var inference = request["inferenceConfig"]!;

        Assert.Equal(0f, inference["temperature"]!.GetValue<float>());
        Assert.Null(inference["topP"]);
    }

    /// <summary>
    /// Dropped, but never silently: a run whose sampling could not be pinned as configured has
    /// weaker reproducibility than its settings claim.
    /// </summary>
    [Fact]
    public void DroppedSamplingParametersAreReported()
    {
        using var client = Client();

        client.BuildRequest(
            [new(ChatRole.User, "hello")],
            new ChatOptions { Temperature = 0f, TopP = 1f, Seed = 1 });

        Assert.Contains(client.UnsupportedSamplingParameters, p => p.StartsWith("topP", StringComparison.Ordinal));
        Assert.Contains(client.UnsupportedSamplingParameters, p => p.StartsWith("seed", StringComparison.Ordinal));
    }

    /// <summary>Where only top-p is pinned there is no conflict, so it is sent unchanged.</summary>
    [Fact]
    public void TopPIsSentWhenTemperatureIsNotPinned()
    {
        var request = Request([new(ChatRole.User, "hello")], new ChatOptions { TopP = 0.9f });

        Assert.Equal(0.9f, request["inferenceConfig"]!["topP"]!.GetValue<float>());
    }

    // ──────────────────────────────────────────────
    // Structured output
    // ──────────────────────────────────────────────

    /// <summary>
    /// Converse has no <c>response_format</c>. A schema becomes one tool the model is forced to
    /// call, which is a stronger constraint than asking for JSON in prose — and the finding parser
    /// depends on not having to guess at the shape.
    /// </summary>
    [Fact]
    public void ASchemaBecomesAForcedToolCall()
    {
        var request = Request(
            [new(ChatRole.User, "assess")],
            new ChatOptions { ResponseFormat = FindingSchema.ResponseFormat(true) });

        var tools = request["toolConfig"]!;
        var name = tools["tools"]!.AsArray()[0]!["toolSpec"]!["name"]!.GetValue<string>();

        Assert.Equal(name, tools["toolChoice"]!["tool"]!["name"]!.GetValue<string>());
        Assert.NotNull(tools["tools"]!.AsArray()[0]!["toolSpec"]!["inputSchema"]!["json"]);
    }

    [Fact]
    public void NoSchemaMeansNoToolConfig() =>
        Assert.Null(Request([new(ChatRole.User, "assess")])["toolConfig"]);

    /// <summary>
    /// The forced tool call is unwrapped back to text. Every parser in this pipeline reads JSON out
    /// of the response body and has no notion of a tool call, so the Bedrock shape stops here.
    /// </summary>
    [Fact]
    public void AToolUseReplyIsReturnedAsItsJson()
    {
        var response = BedrockChatClient.ToResponse(JsonNode.Parse("""
            {
              "output": { "message": { "content": [
                { "toolUse": { "name": "respond", "input": { "outcome": "NoIssue" } } } ] } },
              "stopReason": "tool_use",
              "usage": { "inputTokens": 10, "outputTokens": 4 }
            }
            """)!.AsObject());

        Assert.Contains("\"outcome\":\"NoIssue\"", response.Text);
    }

    // ──────────────────────────────────────────────
    // Usage
    // ──────────────────────────────────────────────

    /// <summary>
    /// Converse reports the two halves of caching separately, and they are priced in opposite
    /// directions — a write at 1.25x, a read at 0.1x — so both are carried through under the names
    /// the cost breakdown already reads.
    /// </summary>
    [Fact]
    public void CacheTokensReachTheCostBreakdown()
    {
        var response = BedrockChatClient.ToResponse(JsonNode.Parse("""
            {
              "output": { "message": { "content": [ { "text": "ready" } ] } },
              "stopReason": "end_turn",
              "usage": { "inputTokens": 9, "outputTokens": 5,
                         "cacheReadInputTokens": 4902, "cacheWriteInputTokens": 0 }
            }
            """)!.AsObject());

        var usage = ChatCompletionClient.ReadUsage(response.Usage);

        Assert.Equal(4902, usage.CacheReadTokens);
        Assert.Equal(0, usage.CacheWriteTokens);
        Assert.Equal(9, usage.InputTokens);
        Assert.Equal(5, usage.OutputTokens);
    }

    [Fact]
    public void AStopReasonOfMaxTokensIsReportedAsLength()
    {
        var response = BedrockChatClient.ToResponse(JsonNode.Parse("""
            { "output": { "message": { "content": [ { "text": "cut" } ] } },
              "stopReason": "max_tokens", "usage": { "inputTokens": 1, "outputTokens": 1 } }
            """)!.AsObject());

        Assert.Equal(ChatFinishReason.Length, response.FinishReason);
    }

    /// <summary>
    /// The factory routes on the provider, not on the URL — the same endpoint serves both invoke
    /// and converse, so only the setting can say which protocol is meant.
    /// </summary>
    [Fact]
    public void ChoosingBedrockBuildsTheConverseClient()
    {
        using var client = AiClientFactory.CreateChatClient(new AppSettings
        {
            OpenAiApiKey = "ABSK-test",
            OpenAiBaseUrl = "https://bedrock-runtime.eu-west-1.amazonaws.com",
            SelectedModel = "eu.anthropic.claude-haiku-4-5-20251001-v1:0",
            ChatProvider = EmbeddingProviders.Bedrock,
        });

        Assert.IsType<BedrockChatClient>(client);
    }

    /// <summary>
    /// The provider decides which protocol a run speaks and therefore what it costs and how it
    /// caches, but not what it concludes. It is an endpoint fact, like the URL and the key.
    /// </summary>
    [Fact]
    public void TheChatProviderIsNotPartOfTheRunFingerprint() =>
        Assert.DoesNotContain(nameof(AppSettings.ChatProvider), AppSettings.NotFingerprinted);
}
