using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace AiPromptEvaluator;

/// <summary>
/// The closed vocabularies the canonical schema documents, and a mechanical correction of the
/// near-misses extraction writes against them.
///
/// Sixty-four properties carry a vocabulary as prose — <c>"One of: Pension, Investment, …"</c> —
/// rather than as a JSON Schema <c>enum</c>. That is deliberate and was bought with a measured
/// finding: an enum is enforced by <see cref="CanonicalModelValidator.StripEnumViolations"/>,
/// which <b>deletes</b> values it does not recognise, and the vocabulary it deleted was the one
/// the documents actually use. Three runs in a row could not reach a charge finding because the
/// basis strings never survived extraction.
///
/// So the vocabulary is guidance, and guidance drifts. <c>objectiveType</c> is documented as
/// Pension / Investment / …, and extraction has been observed writing <c>RetirementObjective</c>.
/// Nothing noticed, because nothing was looking.
///
/// This class looks, and corrects only what can be corrected without a judgement:
/// capitalisation, spacing, separators, and a documented value wearing a generic suffix
/// (<c>PensionObjective</c> → <c>Pension</c>). A value it cannot map is <b>left exactly as
/// written</b> and reported. Deleting it would repeat the mistake the enums were removed to fix,
/// and guessing at it would be the "quietly pick one" the extractor is explicitly told not to do.
/// </summary>
public static class CanonicalVocabulary
{
    /// <summary>One value corrected, or one that could not be.</summary>
    /// <param name="Property">The property name, e.g. "objectiveType".</param>
    /// <param name="Written">What extraction wrote.</param>
    /// <param name="Corrected">The documented value it was mapped to, or null where none fits.</param>
    public sealed record Correction(string Property, string Written, string? Corrected)
    {
        public bool WasMapped => Corrected is not null;

        public override string ToString() =>
            WasMapped
                ? $"{Property}: \"{Written}\" → \"{Corrected}\""
                : $"{Property}: \"{Written}\" is not in the documented vocabulary";
    }

    /// <summary>
    /// Words a model adds to a documented value without changing its meaning. "PensionObjective"
    /// and "Pension" are the same answer; only one of them is the vocabulary's.
    /// </summary>
    private static readonly string[] GenericSuffixes =
        ["objective", "goal", "type", "category", "status", "basis", "code", "value"];

    private static readonly Regex OneOf = new(
        @"One of:\s*(?<values>[^.]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Property name to the values its description documents, read out of the schema itself so
    /// there is no second copy of the vocabulary to fall out of step.
    /// </summary>
    public static Dictionary<string, IReadOnlyList<string>> Parse(string schemaJson)
    {
        var found = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        if (JsonNode.Parse(schemaJson) is not JsonNode root)
        {
            return found;
        }

        Walk(root, found);
        return found;
    }

    private static void Walk(JsonNode? node, Dictionary<string, IReadOnlyList<string>> into)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var item in array)
                {
                    Walk(item, into);
                }

                break;

            case JsonObject obj:
                // A property name only means anything directly under "properties"; everywhere
                // else the keys are schema plumbing.
                if (obj["properties"] is JsonObject properties)
                {
                    foreach (var (propertyName, propertySchema) in properties)
                    {
                        if (ValuesIn(propertySchema) is { Count: > 1 } values
                            && !into.ContainsKey(propertyName))
                        {
                            into[propertyName] = values;
                        }

                        Walk(propertySchema, into);
                    }
                }

                foreach (var (key, value) in obj)
                {
                    if (key != "properties")
                    {
                        Walk(value, into);
                    }
                }

                break;
        }
    }

    /// <summary>The vocabulary a property's description documents, if it documents one.</summary>
    private static List<string>? ValuesIn(JsonNode? propertySchema)
    {
        if (propertySchema?["description"]?.GetValue<string>() is not { } description
            || OneOf.Match(description) is not { Success: true } match)
        {
            return null;
        }

        return match.Groups["values"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(v => v.Length > 0 && v.All(char.IsLetterOrDigit))
            .ToList();
    }

    /// <summary>
    /// Corrects what can be corrected in place, and reports everything it touched or could not.
    /// Never removes a value.
    /// </summary>
    public static IReadOnlyList<Correction> Normalise(
        JsonNode? fragment, IReadOnlyDictionary<string, IReadOnlyList<string>> vocabularies)
    {
        var corrections = new List<Correction>();
        Fix(fragment, vocabularies, corrections);
        return corrections.Distinct().ToList();
    }

    private static void Fix(
        JsonNode? node,
        IReadOnlyDictionary<string, IReadOnlyList<string>> vocabularies,
        List<Correction> corrections)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var item in array)
                {
                    Fix(item, vocabularies, corrections);
                }

                break;

            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    var value = obj[key];

                    if (value is JsonValue leaf
                        && leaf.TryGetValue<string>(out var written)
                        && !string.IsNullOrWhiteSpace(written)
                        && vocabularies.TryGetValue(key, out var allowed))
                    {
                        var corrected = Map(written, allowed);

                        if (corrected is null)
                        {
                            corrections.Add(new Correction(key, written, null));
                        }
                        else if (!string.Equals(corrected, written, StringComparison.Ordinal))
                        {
                            obj[key] = corrected;
                            corrections.Add(new Correction(key, written, corrected));
                        }
                    }
                    else
                    {
                        Fix(value, vocabularies, corrections);
                    }
                }

                break;
        }
    }

    /// <summary>
    /// The documented spelling of <paramref name="written"/>, or null where no mechanical rule
    /// reaches one. Deliberately conservative: three rules, none of them a judgement about what
    /// the author meant.
    /// </summary>
    public static string? Map(string written, IReadOnlyList<string> allowed)
    {
        var trimmed = written.Trim();

        // 1. Already right, or right but for capitalisation.
        var exact = allowed.FirstOrDefault(a => string.Equals(a, trimmed, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        // 2. Right but for spacing, hyphens or underscores: "Estate Planning", "estate_planning".
        var squeezed = Squeeze(trimmed);
        var bySqueeze = allowed.FirstOrDefault(a => string.Equals(Squeeze(a), squeezed, StringComparison.OrdinalIgnoreCase));
        if (bySqueeze is not null)
        {
            return bySqueeze;
        }

        // 3. A documented value wearing a generic word: "PensionObjective", "pension_goal".
        //    Only where the remainder is one of those words — never a partial-word match, which
        //    would map "PensionTransfer" onto "Pension" and lose the distinction.
        foreach (var candidate in allowed.OrderByDescending(a => a.Length))
        {
            var squeezedCandidate = Squeeze(candidate);

            if (squeezed.Length <= squeezedCandidate.Length
                || !squeezed.StartsWith(squeezedCandidate, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var remainder = squeezed[squeezedCandidate.Length..];

            if (GenericSuffixes.Contains(remainder, StringComparer.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string Squeeze(string value)
    {
        var sb = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }
}
