using System.Text.Json.Nodes;

namespace AiPromptEvaluator;

/// <summary>One value that does not belong to the schema's enum for its property.</summary>
public sealed record EnumViolation(string Property, string Value)
{
    public override string ToString() => $"{Property} = \"{Value}\"";
}

/// <summary>
/// Checks an extraction fragment against the enums its schema slice declares, before the
/// fragment is merged into the model.
///
/// The system prompt already says enumerated fields must use a schema value or be omitted, and
/// in an observed run 47 values did neither — 42 of them on "basis", which drifted into a
/// free-text field holding things like "annual growth required at mid-growth rate 2.94% up to
/// age 75". An instruction nothing enforces is a suggestion, and every one of those values
/// reached a check as though the schema had sanctioned it.
///
/// The matching is by property name, unioned across the schema, which is deliberately lenient:
/// where two places declare different enums for the same property name, a value valid in either
/// passes here. That direction of error is the right one — this drops data when it fires, so it
/// should fire only when the schema plainly disallows the value.
/// </summary>
public static class CanonicalModelValidator
{
    /// <summary>
    /// Every value in <paramref name="fragment"/> that its property's enum does not allow.
    /// </summary>
    public static IReadOnlyList<EnumViolation> EnumViolations(JsonObject fragment, string schemaSlice)
    {
        var allowed = AllowedValues(schemaSlice);
        if (allowed.Count == 0)
        {
            return [];
        }

        var violations = new List<EnumViolation>();
        Walk(fragment, property: null, allowed, violations);

        return violations.Distinct().ToList();
    }

    /// <summary>
    /// Removes the offending values, leaving everything around them intact, and reports what
    /// it removed.
    ///
    /// A bad "basis" should not cost the charge figure standing beside it: the pass did the
    /// expensive work of reading the report, and one unusable enum is not a reason to discard
    /// the section. Dropping the property is what the schema asks for — "use a value from the
    /// enum, or be omitted" — applied after the fact.
    /// </summary>
    public static IReadOnlyList<EnumViolation> StripEnumViolations(JsonObject fragment, string schemaSlice)
    {
        var allowed = AllowedValues(schemaSlice);
        if (allowed.Count == 0)
        {
            return [];
        }

        var removed = new List<EnumViolation>();
        Strip(fragment, allowed, removed);

        return removed.Distinct().ToList();
    }

    /// <summary>Property name to the values any enum in the schema allows for it.</summary>
    internal static Dictionary<string, HashSet<string>> AllowedValues(string schemaSlice)
    {
        var allowed = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        if (JsonNode.Parse(schemaSlice) is not JsonObject schema)
        {
            return allowed;
        }

        Collect(schema, property: null, allowed);

        return allowed;
    }

    private static void Collect(JsonNode? node, string? property, Dictionary<string, HashSet<string>> allowed)
    {
        switch (node)
        {
            case JsonObject obj:
                if (property is not null && obj["enum"] is JsonArray values)
                {
                    var set = allowed.TryGetValue(property, out var existing)
                        ? existing
                        : allowed[property] = new HashSet<string>(StringComparer.Ordinal);

                    foreach (var value in values)
                    {
                        if (value is JsonValue element && element.TryGetValue<string>(out var text))
                        {
                            set.Add(text);
                        }
                    }
                }

                foreach (var (key, value) in obj)
                {
                    // Under "properties" the keys are property names; everywhere else — items,
                    // $defs, anyOf and the rest — the enclosing property name still applies.
                    if (key == "properties" && value is JsonObject properties)
                    {
                        foreach (var (name, definition) in properties)
                        {
                            Collect(definition, name, allowed);
                        }
                    }
                    else
                    {
                        Collect(value, property, allowed);
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    Collect(item, property, allowed);
                }

                break;
        }
    }

    private static void Walk(
        JsonNode? node, string? property,
        Dictionary<string, HashSet<string>> allowed, List<EnumViolation> violations)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    Walk(value, key, allowed, violations);
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    Walk(item, property, allowed, violations);
                }

                break;

            case JsonValue value
                when property is not null
                    && value.TryGetValue<string>(out var text)
                    && allowed.TryGetValue(property, out var permitted)
                    && permitted.Count > 0
                    && !permitted.Contains(text):
                violations.Add(new EnumViolation(property, text));
                break;
        }
    }

    private static void Strip(
        JsonNode? node, Dictionary<string, HashSet<string>> allowed, List<EnumViolation> removed)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (obj[key] is JsonValue value &&
                        value.TryGetValue<string>(out var text) &&
                        allowed.TryGetValue(key, out var permitted) &&
                        permitted.Count > 0 &&
                        !permitted.Contains(text))
                    {
                        removed.Add(new EnumViolation(key, text));
                        obj.Remove(key);
                        continue;
                    }

                    Strip(obj[key], allowed, removed);
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    Strip(item, allowed, removed);
                }

                break;
        }
    }
}
