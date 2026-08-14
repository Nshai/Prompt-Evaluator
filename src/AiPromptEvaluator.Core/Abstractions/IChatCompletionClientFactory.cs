namespace AiPromptEvaluator;

/// <summary>
/// Creates a chat client, optionally against settings that have not been saved.
///
/// The parameter exists for one caller: the configuration screen tests a connection using the
/// endpoint and key the user has just typed, which are not yet the live settings and must not
/// become them until the test passes.
/// </summary>
public interface IChatCompletionClientFactory
{
    /// <summary>
    /// A client on the live settings, or on <paramref name="settings"/> when testing a
    /// configuration that has not been saved.
    /// </summary>
    IChatCompletionClient Create(AppSettings? settings = null);
}
