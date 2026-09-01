using System.Text.Json;
using System.Text.Json.Nodes;

using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Slicing the canonical model schema per extraction pass. A slice that drops a definition
/// it still references sends the model a schema it cannot follow, and the section comes back
/// malformed — so self-containment is the property worth proving.
/// </summary>
public class JsonSchemaSlicerTests
{
    private readonly ITestOutputHelper _output;

    public JsonSchemaSlicerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string Schema =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Test",
          "type": "object",
          "properties": {
            "wanted":   { "$ref": "#/$defs/Wanted" },
            "unwanted": { "$ref": "#/$defs/Unwanted" }
          },
          "$defs": {
            "Wanted":     { "type": "object", "properties": { "nested": { "$ref": "#/$defs/Nested" } } },
            "Nested":     { "type": "object", "properties": { "deep": { "$ref": "#/$defs/Deep" } } },
            "Deep":       { "type": "string" },
            "Unwanted":   { "type": "object", "properties": { "other": { "$ref": "#/$defs/Orphan" } } },
            "Orphan":     { "type": "string" }
          }
        }
        """;

    [Fact]
    public void Slice_KeepsOnlyTheRequestedProperties()
    {
        var slice = JsonNode.Parse(JsonSchemaSlicer.Slice(Schema, ["wanted"]))!.AsObject();
        var properties = slice["properties"]!.AsObject();

        Assert.True(properties.ContainsKey("wanted"));
        Assert.False(properties.ContainsKey("unwanted"));
    }

    /// <summary>Refs are followed transitively: Wanted → Nested → Deep all have to survive.</summary>
    [Fact]
    public void Slice_KeepsDefinitionsReachedThroughOtherDefinitions()
    {
        var slice = JsonNode.Parse(JsonSchemaSlicer.Slice(Schema, ["wanted"]))!.AsObject();
        var defs = slice["$defs"]!.AsObject();

        Assert.True(defs.ContainsKey("Wanted"));
        Assert.True(defs.ContainsKey("Nested"));
        Assert.True(defs.ContainsKey("Deep"));

        // Cutting the unreachable half is the entire point — it is what makes a pass cheap.
        Assert.False(defs.ContainsKey("Unwanted"));
        Assert.False(defs.ContainsKey("Orphan"));
    }

    [Fact]
    public void Slice_RejectsAPropertyTheSchemaDoesNotHave()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => JsonSchemaSlicer.Slice(Schema, ["nonexistent"]));

        Assert.Contains("nonexistent", exception.Message);
    }

    /// <summary>
    /// The same section must slice to identical text every time, or the provider's prefix
    /// cache misses on every pass and extraction quietly costs several times more.
    /// </summary>
    [Fact]
    public void Slice_IsDeterministic()
    {
        Assert.Equal(
            JsonSchemaSlicer.Slice(Schema, ["wanted"]),
            JsonSchemaSlicer.Slice(Schema, ["wanted"]));
    }

    /// <summary>
    /// Against the real schema: every section slices, stays valid JSON, and leaves no ref
    /// pointing at a definition the slice dropped.
    /// </summary>
    [Fact]
    public void EverySection_SlicesTheRealSchemaWithoutDanglingRefs()
    {
        var schemaPath = LocateSchema();
        if (schemaPath is null)
        {
            _output.WriteLine("Skipped: canonical model schema not found.");
            return;
        }

        var schema = File.ReadAllText(schemaPath);
        var full = schema.Length;

        foreach (var section in ExtractionSection.All)
        {
            var sliced = JsonSchemaSlicer.Slice(schema, section.Properties);
            var node = JsonNode.Parse(sliced)!.AsObject();
            var defs = node["$defs"]?.AsObject();

            foreach (var reference in RefsIn(node))
            {
                Assert.True(
                    defs is not null && defs.ContainsKey(reference),
                    $"{section.Name} references \"{reference}\" but the slice does not define it.");
            }

            _output.WriteLine(
                $"{section.Name,-38} {sliced.Length,7:N0} chars ({(double)sliced.Length / full:P0} of the full schema)");
        }
    }

    private static IEnumerable<string> RefsIn(JsonNode? node)
    {
        const string prefix = "#/$defs/";

        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == "$ref" && value is JsonValue v &&
                        v.TryGetValue<string>(out var target) && target.StartsWith(prefix))
                    {
                        yield return target[prefix.Length..];
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

    private static string? LocateSchema()
    {
        var deployed = Path.Combine(AppContext.BaseDirectory, AppSettings.DefaultCanonicalSchemaFileName);
        if (File.Exists(deployed))
        {
            return deployed;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, "docs", "artifacts", AppSettings.DefaultCanonicalSchemaFileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
