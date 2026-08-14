using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiPromptEvaluator;

/// <summary>One canonical path resolved against a stored model.</summary>
public sealed record CanonicalFragment(string Path, string Json, bool Found)
{
    public static CanonicalFragment Missing(string path) => new(path, string.Empty, false);
}

/// <summary>
/// Reads values out of a stored canonical model by the canonical paths the query plans cite,
/// e.g. <c>/riskAssessment/perClient[]/attitudeToRisk/agreedCode</c>.
///
/// This is the half of a check that used to require re-reading the suitability report. The
/// plan's assertion-side queries name paths rather than search text, so what the report
/// claims is looked up here, and only the evidence documents are searched.
///
/// The syntax is JSON Pointer with two additions the plans use: <c>[]</c> to fan out across
/// an array, and <c>[key=value]</c> to select array elements by a property value.
/// </summary>
public sealed class CanonicalModelAccessor
{
    /// <summary>
    /// Indented so a fragment is readable in a prompt, and unescaped so it is quotable.
    ///
    /// The encoder matters more than it looks. By default <c>System.Text.Json</c> escapes
    /// anything outside a conservative ASCII set, so a fragment reaches the assessor reading
    /// <c>£116,997.47</c> where the document said <c>£116,997.47</c>. The model quotes it
    /// back the way a person would write it, the citation check compares two different strings,
    /// and the finding is reported as a fabricated quotation. That is not hypothetical: it is
    /// what happened to the group that caught the report's own £6,997 inconsistency in
    /// pension totals — the best assertion-side finding of the run, flagged as unsupported for
    /// citing the place it came from.
    ///
    /// Relaxed escaping is safe here because the destination is a prompt, not a browser: these
    /// strings are never interpolated into HTML or a script.
    /// </summary>
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly JsonObject _root;

    public CanonicalModelAccessor(string modelJson)
    {
        _root = JsonNode.Parse(modelJson) as JsonObject
            ?? throw new InvalidOperationException("The stored canonical model is not a JSON object.");
    }

    /// <summary>The top-level properties the stored model actually carries.</summary>
    public IReadOnlyList<string> PopulatedSections =>
        _root.Where(pair => pair.Value is not null).Select(pair => pair.Key).ToList();

    /// <summary>
    /// Resolves one path. A path that matches nothing comes back with Found false rather
    /// than throwing — "the model has no value here" is a legitimate and frequent answer,
    /// and for most checks it is the finding.
    /// </summary>
    public CanonicalFragment Resolve(string path)
    {
        var matches = ResolveNodes(path).ToList();

        if (matches.Count == 0)
        {
            return CanonicalFragment.Missing(path);
        }

        // A fan-out path yields one node per array element; present them as an array so the
        // shape the assessor sees matches the shape the path describes.
        var value = matches.Count == 1 && !path.Contains("[]", StringComparison.Ordinal)
            ? matches[0]
            : new JsonArray(matches.Select(m => m.DeepClone()).ToArray());

        return new CanonicalFragment(path, value.ToJsonString(Indented), true);
    }

    /// <summary>Resolves several paths, keeping their order and any misses.</summary>
    public IReadOnlyList<CanonicalFragment> Resolve(IEnumerable<string> paths) =>
        paths.Select(Resolve).ToList();

    private IEnumerable<JsonNode> ResolveNodes(string path)
    {
        IEnumerable<JsonNode> current = [_root];

        foreach (var rawSegment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var (name, selector) = SplitSelector(rawSegment.Trim());

            current = current
                .SelectMany(node => Step(node, name, selector))
                .ToList();

            if (!current.Any())
            {
                return [];
            }
        }

        return current;
    }

    /// <summary>Takes one path segment: a property, then any array selector attached to it.</summary>
    private static IEnumerable<JsonNode> Step(JsonNode node, string name, string? selector)
    {
        if (node is not JsonObject obj || obj[name] is not { } value)
        {
            yield break;
        }

        if (selector is null)
        {
            yield return value;
            yield break;
        }

        if (value is not JsonArray array)
        {
            // A selector on a non-array is a plan/model mismatch, not a crash: yield the
            // value so the assessor still sees what is there.
            yield return value;
            yield break;
        }

        foreach (var item in array)
        {
            if (item is null)
            {
                continue;
            }

            if (selector.Length == 0 || Matches(item, selector))
            {
                yield return item;
            }
        }
    }

    /// <summary>Tests a <c>[key=value]</c> selector against an array element.</summary>
    private static bool Matches(JsonNode item, string selector)
    {
        var split = selector.IndexOf('=');
        if (split <= 0 || item is not JsonObject obj)
        {
            return false;
        }

        var key = selector[..split].Trim();
        var expected = selector[(split + 1)..].Trim().Trim('"', '\'');

        return obj[key] is { } actual &&
               string.Equals(Scalar(actual), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string Scalar(JsonNode node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : node.ToJsonString().Trim('"');

    /// <summary>Splits "clients[]" into ("clients", "") and "lines[layer=Fund]" into ("lines", "layer=Fund").</summary>
    private static (string Name, string? Selector) SplitSelector(string segment)
    {
        var open = segment.IndexOf('[');
        if (open < 0 || !segment.EndsWith(']'))
        {
            return (segment, null);
        }

        return (segment[..open], segment[(open + 1)..^1]);
    }
}
