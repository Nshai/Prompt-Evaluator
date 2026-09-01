namespace AiPromptEvaluator;

/// <summary>
/// Makes a run's prompts unique, so the gateway has to generate the answers rather than
/// serving them from cache.
///
/// <b>There is no cache header to set.</b> The endpoint is OpenAI-compatible and the cache
/// sits in front of it, keyed on the request; nothing the app sends can ask it to skip. The
/// only lever is the request body, which is why the extractor's JSON retry already varies its
/// prompt rather than re-sending the same one — an observed run re-issued an identical prompt
/// and was handed back the identical 41,580-character failed reply, in the same second, and
/// billed for both.
///
/// <b>The variation is per run, not per call.</b> One nonce for the whole run keeps the
/// provider's prefix cache working within it — the check header is identical across every
/// group of a check, which is what makes one call per requirement affordable — while making
/// the run as a whole a miss against anything cached earlier. A per-call nonce would defeat
/// both caches and roughly double the input bill for no extra confidence.
///
/// It goes at the <em>end</em> of the user prompt for the same reason. Providers cache on a
/// shared prefix; a marker at the front would invalidate from the first token and lose the
/// document that every section of an extraction re-reads.
/// </summary>
public sealed class PromptCacheBypass
{
    /// <summary>Sends prompts exactly as built. The default, and what every run did before this existed.</summary>
    public static readonly PromptCacheBypass Off = new(null);

    private readonly string? _marker;

    private PromptCacheBypass(string? marker) => _marker = marker;

    /// <summary>
    /// A bypass for one run, or <see cref="Off"/> when <paramref name="enabled"/> is false, so
    /// a caller writes <c>PromptCacheBypass.For(settings.BypassResponseCache)</c> once and
    /// applies the result unconditionally.
    /// </summary>
    public static PromptCacheBypass For(bool enabled) => enabled ? New() : Off;

    /// <summary>
    /// A bypass carrying a fresh nonce.
    ///
    /// Random rather than derived from the run's inputs, and deliberately so: a value computed
    /// from the case and the settings would be identical on the second run of an unchanged
    /// configuration, which is precisely the run this exists to force. Determinism is given up
    /// on purpose here — that is what the caller asked for.
    /// </summary>
    public static PromptCacheBypass New() =>
        new(Guid.NewGuid().ToString("n") + "-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

    /// <summary>True when this run's prompts are being varied.</summary>
    public bool IsEnabled => _marker is not null;

    /// <summary>The nonce, for the run log — so a report can say why its cost looks uncached.</summary>
    public string? Marker => _marker;

    /// <summary>
    /// <paramref name="userPrompt"/> with the run marker appended, or unchanged when off.
    ///
    /// Phrased as an instruction to ignore rather than as a bare token: the marker is inside the
    /// prompt whether the model attends to it or not, and a naked hex string invites it to be
    /// treated as data belonging to the case. Everything before it is byte-identical to the
    /// prompt that would otherwise have been sent.
    /// </summary>
    public string Apply(string userPrompt) =>
        _marker is null
            ? userPrompt
            : userPrompt
              + Environment.NewLine
              + Environment.NewLine
              + "<!-- Run marker " + _marker + ". Not part of the case; ignore it. -->";

    /// <summary>How the bypass reads on a run's configuration line.</summary>
    public string Describe() =>
        _marker is null ? "response cache allowed" : $"response cache bypassed ({_marker})";
}
