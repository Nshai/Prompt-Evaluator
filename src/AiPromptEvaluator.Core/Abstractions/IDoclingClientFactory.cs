namespace AiPromptEvaluator;

/// <summary>
/// Creates a client for the document-conversion sidecar.
///
/// The endpoint is read through a callback rather than captured, so a client outlives an edit to
/// the setting it points at.
/// </summary>
public interface IDoclingClientFactory
{
    /// <summary>
    /// The endpoint is read through a callback rather than captured, so a client outlives an
    /// edit to the setting it points at.
    /// </summary>
    IDoclingClient Create(Func<string>? endpointProvider = null);
}
