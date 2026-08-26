using Microsoft.Extensions.AI;

namespace AiPromptEvaluator;

/// <summary>
/// Sends prompts to the chat model and reports what they cost.
///
/// The overload taking a response format is the one the assessment path uses: it constrains the
/// reply to the finding schema, which is what makes a verdict parseable rather than something to
/// be pattern-matched out of prose.
/// </summary>
public interface IChatCompletionClient
{
    /// <summary>Sends the prompt verbatim — no system preamble, no folder context.</summary>
    Task<ChatCompletionResult> RunRawAsync(string rawPrompt, CancellationToken cancellationToken = default);

    Task<ChatCompletionResult> RunRawAsync(
        string systemPrompt,
        string userPrompt,
        int maxOutputTokens,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The overload the assessment path uses: a response format constrains the reply to the
    /// finding schema, which is what makes the verdict parseable.
    /// </summary>
    Task<ChatCompletionResult> RunRawAsync(
        string systemPrompt,
        string userPrompt,
        int maxOutputTokens,
        ChatResponseFormat? responseFormat,
        CancellationToken cancellationToken = default);
}
