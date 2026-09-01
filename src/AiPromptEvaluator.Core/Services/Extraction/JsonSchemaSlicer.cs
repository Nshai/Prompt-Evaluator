using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiPromptEvaluator;

/// <summary>
/// Cuts a section-sized schema out of the canonical model schema.
///
/// Extraction runs a section at a time, and sending the whole 40KB schema on every pass
/// would spend roughly ten thousand tokens per call describing fields the pass is not being
/// asked to fill. This walks the <c>$ref</c> graph from the requested properties and keeps
/// only the definitions that are actually reachable, which typically cuts a pass to a
/// fraction of the full schema while remaining a valid, self-contained schema document.
/// </summary>
public static class JsonSchemaSlicer
{
    private const string DefsKey = "$defs";
    private const string RefPrefix = "#/$defs/";

    /// <summary>
    /// A standalone schema describing only <paramref name="properties"/>, carrying the
    /// definitions those properties reach and nothing else.
    /// </summary>
    /// <exception cref="InvalidOperationException">A requested property is not in the schema.</exception>
    public static string Slice(string schemaJson, IReadOnlyList<string> properties)
    {
        var root = JsonNode.Parse(schemaJson)?.AsObject()
            ?? throw new InvalidOperationException("The canonical model schema is not a JSON object.");

        var sourceProperties = root["properties"]?.AsObject()
            ?? throw new InvalidOperationException("The canonical model schema has no \"properties\".");

        var sourceDefs = root[DefsKey]?.AsObject();

        var slicedProperties = new JsonObject();
        foreach (var name in properties)
        {
            var property = sourceProperties[name]
                ?? throw new InvalidOperationException(
                    $"The canonical model schema has no top-level property \"{name}\".");

            slicedProperties[name] = property.DeepClone();
        }

        var result = new JsonObject
        {
            ["$schema"] = root["$schema"]?.DeepClone() ?? "https://json-schema.org/draft/2020-12/schema",
            ["title"] = $"{root["title"]?.GetValue<string>() ?? "Canonical model"} — {string.Join(", ", properties)}",
            ["type"] = "object",
            ["properties"] = slicedProperties,
        };

        if (sourceDefs is not null)
        {
            var reachable = CollectReachableDefs(slicedProperties, sourceDefs);
            if (reachable.Count > 0)
            {
                var defs = new JsonObject();

                // Emit in the schema's own order rather than discovery order, so the same
                // section always slices to byte-identical text and stays promptcache-friendly.
                foreach (var (name, definition) in sourceDefs)
                {
                    if (reachable.Contains(name) && definition is not null)
                    {
                        defs[name] = definition.DeepClone();
                    }
                }

                result[DefsKey] = defs;
            }
        }

        return result.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Every definition reachable from <paramref name="start"/>, following refs transitively.
    /// A definition that refs another pulls it in too, so the slice never dangles.
    /// </summary>
    private static HashSet<string> CollectReachableDefs(JsonNode start, JsonObject sourceDefs)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<JsonNode>();
        pending.Enqueue(start);

        while (pending.Count > 0)
        {
            foreach (var name in RefsIn(pending.Dequeue()))
            {
                if (!reachable.Add(name))
                {
                    continue;
                }

                if (sourceDefs[name] is { } definition)
                {
                    pending.Enqueue(definition);
                }
            }
        }

        return reachable;
    }

    /// <summary>Every <c>$defs</c> name referenced anywhere beneath <paramref name="node"/>.</summary>
    private static IEnumerable<string> RefsIn(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == "$ref" && value is JsonValue reference &&
                        reference.TryGetValue<string>(out var target) &&
                        target.StartsWith(RefPrefix, StringComparison.Ordinal))
                    {
                        yield return target[RefPrefix.Length..];
                    }

                    foreach (var nested in RefsIn(value))
                    {
                        yield return nested;
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    foreach (var nested in RefsIn(item))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }
}
