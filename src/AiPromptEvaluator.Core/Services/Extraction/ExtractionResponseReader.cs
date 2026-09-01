using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiPromptEvaluator;

/// <summary>
/// Reads an extraction pass's reply, including the replies that did not finish.
///
/// A section that overruns the output cap stops mid-token: the JSON is valid up to the cut and
/// unparseable after it. The pipeline used to report that as "the model did not return a JSON
/// object", which sent whoever read it looking for a prompt-compliance problem — the one thing it
/// was not. In an observed run the recommendations pass was cut off 87% of the way through with
/// four of five recommendations already complete, and all four were discarded.
///
/// So this distinguishes the two failures, and recovers what a truncated reply did finish.
/// </summary>
public static class ExtractionResponseReader
{
    /// <summary>
    /// One property name a model wrote twice in the same object, and whether the two values
    /// agreed. <paramref name="Path"/> is the object's JSON Pointer, so a report can say which
    /// arrangement or recommendation it happened in rather than only which field.
    /// </summary>
    public sealed record DuplicateKey(string Path, string Key, bool ValuesDiffer)
    {
        /// <inheritdoc/>
        public override string ToString() =>
            $"{Path}/{Key}" + (ValuesDiffer ? " (values differ; kept the first)" : string.Empty);
    }

    /// <summary>
    /// The reply as a JSON object, tolerating the two things a chat model does even when told
    /// not to: wrapping the object in a markdown fence, and adding a sentence around it.
    /// </summary>
    public static JsonObject? ParseObject(string response) => ParseObject(response, out _);

    /// <summary>
    /// The reply as a JSON object, reporting any property name written twice in one object.
    ///
    /// <b>A duplicate key is the worst kind of malformed reply, because it parses.</b>
    /// <see cref="JsonNode.Parse(string, JsonNodeOptions?, JsonDocumentOptions)"/> accepts it and
    /// builds its dictionary lazily, so the object comes back non-null, the reader calls it well
    /// formed, and nothing retries. It detonates later — at whatever line first asks for
    /// <c>Count</c> or enumerates it — with an <see cref="ArgumentException"/> that names a key
    /// and nothing else.
    ///
    /// Observed three runs running, always the same shape: the model repeated a run of two
    /// properties verbatim inside one existing arrangement —
    /// <c>"numberOfFundsAvailable": "7", "allowsAdviserServicing": false,</c> written twice — with
    /// identical values, which is semantically harmless and structurally fatal. It cost the whole
    /// <c>existingArrangements</c> section every time: five pension plans, their values, charges,
    /// risk ratings and advice actions, and with them the identifier table every later pass needed,
    /// so every cross-reference dangled too.
    ///
    /// <b>The first occurrence wins.</b> Not the last, and not a merge: the model writes in
    /// document order, so the first is the one it wrote while reading the source, and a repeat is
    /// a stutter rather than a correction. Where the two values differ the first is still kept and
    /// the difference is reported, because choosing between them would be guessing at which
    /// reading of the document was meant — and a wrongly chosen value is worse than a flagged one,
    /// since it would be merged and believed.
    /// </summary>
    public static JsonObject? ParseObject(string response, out IReadOnlyList<DuplicateKey> duplicates)
    {
        duplicates = [];

        var text = Unfence(response);

        if (!text.StartsWith('{'))
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return null;
            }

            text = text[start..(end + 1)];
        }

        try
        {
            // JsonDocument, not JsonNode: it permits duplicate names and hands back both, which
            // is what makes the choice between them ours rather than the dictionary's.
            using var document = JsonDocument.Parse(text);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var found = new List<DuplicateKey>();
            var root = Rebuild(document.RootElement, string.Empty, found) as JsonObject;

            duplicates = found;
            return root;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Rebuilds an element as a node, keeping the first of any repeated property name.
    ///
    /// Everything is rebuilt rather than only the objects that repeat, because a duplicate
    /// anywhere in the tree poisons the node that contains it, and finding out where means
    /// walking the whole thing anyway.
    /// </summary>
    private static JsonNode? Rebuild(JsonElement element, string path, List<DuplicateKey> duplicates)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var node = new JsonObject();
                var seen = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (var property in element.EnumerateObject())
                {
                    var raw = property.Value.GetRawText();

                    if (seen.TryGetValue(property.Name, out var kept))
                    {
                        duplicates.Add(new DuplicateKey(
                            path.Length == 0 ? "/" : path,
                            property.Name,
                            !string.Equals(kept, raw, StringComparison.Ordinal)));

                        continue;
                    }

                    seen[property.Name] = raw;
                    node[property.Name] = Rebuild(
                        property.Value, $"{path}/{property.Name}", duplicates);
                }

                return node;

            case JsonValueKind.Array:
                var array = new JsonArray();
                var index = 0;

                foreach (var item in element.EnumerateArray())
                {
                    array.Add(Rebuild(item, $"{path}/{index++}", duplicates));
                }

                return array;

            case JsonValueKind.Null:
                return null;

            default:
                return JsonNode.Parse(element.GetRawText());
        }
    }

    /// <summary>
    /// Whether the reply looks cut off rather than malformed.
    ///
    /// Two signals, either of which is enough: a fence that opens and never closes, and a
    /// bracket depth that never returns to zero. Both were true of the observed failure and
    /// neither was being looked at.
    /// </summary>
    public static bool LooksTruncated(string response)
    {
        var trimmed = response.TrimEnd();

        if (trimmed.StartsWith("```", StringComparison.Ordinal) &&
            !trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            return true;
        }

        var text = Unfence(response);
        if (!text.StartsWith('{'))
        {
            return false;
        }

        return Scan(text).Depth > 0;
    }

    /// <summary>
    /// The longest valid prefix of a truncated reply, closed off — or null when nothing whole
    /// survived.
    ///
    /// Extraction sections are an object holding one or more arrays, so the useful unit to
    /// recover is a completed array element: four whole recommendations are worth four
    /// recommendations, where the fifth being half-written costs nothing but itself.
    ///
    /// The caller is expected to record the shortfall rather than merge this as though it were
    /// complete. A partial section presented as whole would turn a loud failure into a quiet
    /// one, which is the trade this pipeline exists to avoid.
    /// </summary>
    public static JsonObject? Salvage(string response)
    {
        var text = Unfence(response);
        if (!text.StartsWith('{'))
        {
            return null;
        }

        var scan = Scan(text);
        if (scan.LastElementEnd is not { } cut)
        {
            return null;
        }

        // Close whatever was still open at the cut, innermost first.
        var closers = string.Concat(
            Enumerable.Reverse(scan.OpenAtLastElementEnd).Select(c => c == '{' ? '}' : ']'));

        try
        {
            return JsonNode.Parse(text[..(cut + 1)] + closers) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>How many whole elements of <paramref name="property"/> survived a truncation.</summary>
    public static int SalvagedCount(JsonObject? salvaged, string property) =>
        salvaged?[property] is JsonArray array ? array.Count : 0;

    private static string Unfence(string response)
    {
        var text = response.Trim();

        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstNewline = text.IndexOf('\n');
        if (firstNewline >= 0)
        {
            text = text[(firstNewline + 1)..];
        }

        var fenceEnd = text.LastIndexOf("```", StringComparison.Ordinal);
        if (fenceEnd >= 0)
        {
            text = text[..fenceEnd];
        }

        return text.Trim();
    }

    /// <summary>
    /// Walks the text once, tracking bracket nesting outside of string literals, and records
    /// where the last array element at depth two closed — that being the last point the reply
    /// can be cut without losing a whole entity.
    /// </summary>
    private static ScanResult Scan(string text)
    {
        var stack = new List<char>();
        var openAtLastElementEnd = new List<char>();
        int? lastElementEnd = null;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            switch (c)
            {
                case '{' or '[':
                    stack.Add(c);
                    break;

                case '}' or ']':
                    if (stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }

                    // Depth 2 means: inside the root object, inside one of its arrays. What
                    // just closed is therefore a whole element of a section's array.
                    if (stack.Count == 2)
                    {
                        lastElementEnd = i;
                        openAtLastElementEnd = [.. stack];
                    }

                    break;
            }
        }

        return new ScanResult(stack.Count, lastElementEnd, openAtLastElementEnd);
    }

    /// <param name="Depth">Brackets still open at the end — above zero means truncated.</param>
    /// <param name="LastElementEnd">Index of the last whole array element's closing bracket.</param>
    /// <param name="OpenAtLastElementEnd">Brackets open at that point, outermost first.</param>
    private sealed record ScanResult(int Depth, int? LastElementEnd, List<char> OpenAtLastElementEnd);
}
