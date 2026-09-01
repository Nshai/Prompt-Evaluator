using System.Text.Json;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// What the extractor is actually told, measured against what the fact library says it needs.
///
/// <b>The extraction prompt is built from a slice of the schema.</b> That makes a schema
/// <c>description</c> the only per-field channel that reaches the model — and it means a fact the
/// library documents in a paragraph can arrive at the extractor as a field name and a type, with
/// every trap the library warns about unmentioned. Twenty-eight of the forty-four facts were in
/// exactly that state: no guidance about preserving a stated period verbatim, about a negative
/// assertion being a claim rather than an absence, or about never rounding a composite risk label
/// to an adjacent one.
///
/// These tests hold the three properties that keep the extraction honest: every top-level section
/// of the model is claimed by a pass, every published fact carries guidance, and the modality
/// vocabulary the library validates against is one the extractor has been given.
/// </summary>
public class ExtractionCoverageTests
{
    private static string SchemaPath =>
        Path.Combine(AppContext.BaseDirectory, "canonical-suitability-model.schema.json");

    private static JsonDocument Schema() => JsonDocument.Parse(File.ReadAllText(SchemaPath));

    // ── every part of the model is extracted by some pass ─────────────────────

    /// <summary>
    /// A property added to the schema and not to a section is never extracted, and nothing says
    /// so: the run succeeds, every pass reports success, and the model comes back without it.
    /// The plans then read a path that is always absent, which reads exactly like a report that
    /// does not mention the subject.
    ///
    /// <c>modelVersion</c> is the one exception — it is stamped in code from the schema itself
    /// rather than read out of the document.
    /// </summary>
    [Fact]
    public void EveryTopLevelPropertyOfTheModelIsClaimedByAnExtractionPass()
    {
        using var schema = Schema();

        var top = schema.RootElement.GetProperty("properties").EnumerateObject()
            .Select(p => p.Name)
            .Where(n => !string.Equals(n, "modelVersion", StringComparison.Ordinal))
            .ToList();

        var claimed = ExtractionSection.All.SelectMany(s => s.Properties).ToList();

        var unextracted = top.Except(claimed, StringComparer.Ordinal).ToList();
        var invented = claimed.Except(top, StringComparer.Ordinal).ToList();

        Assert.True(
            unextracted.Count == 0,
            "These parts of the canonical model are in the schema and no extraction pass asks "
            + "for them, so they are never populated: " + string.Join(", ", unextracted));

        Assert.True(
            invented.Count == 0,
            "These extraction passes ask for properties the schema does not define, so they slice "
            + "to nothing: " + string.Join(", ", invented));
    }

    /// <summary>
    /// Two passes asking for the same property would extract it twice and let the second
    /// overwrite the first, which is a silent way to lose a section's work.
    /// </summary>
    [Fact]
    public void NoPropertyIsClaimedByTwoPasses()
    {
        var duplicates = ExtractionSection.All
            .SelectMany(s => s.Properties)
            .GroupBy(p => p, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(
            duplicates.Count == 0,
            "Claimed by more than one pass: " + string.Join(", ", duplicates));
    }

    // ── every published fact reaches the extractor with its guidance ──────────

    /// <summary>
    /// The regression this file exists for. Every fact the library publishes has a schema
    /// description somewhere on its path, so the extractor is told the thing that makes the
    /// difference between a right value and a plausible one.
    /// </summary>
    [Fact]
    public void EveryPublishedFactCarriesGuidanceTheExtractorCanRead()
    {
        using var schema = Schema();
        using var map = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fact-library.map.json")));

        var bare = map.RootElement.GetProperty("facts").EnumerateArray()
            .Select(f => (
                Id: f.GetProperty("id").GetString()!,
                Paths: f.GetProperty("canonicalPaths").EnumerateArray()
                    .Select(p => p.GetString()!).ToList()))
            .Where(f => !f.Paths.Any(p => !string.IsNullOrWhiteSpace(DescriptionAt(schema, p))))
            .Select(f => $"{f.Id} — {string.Join(", ", f.Paths)}")
            .ToList();

        Assert.True(
            bare.Count == 0,
            "These facts reach the extractor as a field name and a type, with none of the "
            + "library's guidance:" + Environment.NewLine
            + string.Join(Environment.NewLine, bare.Select(b => "  " + b)));
    }

    // ── modality ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The library validates every fact instance against a modality allowlist, and thirty of the
    /// forty-four facts allow three or more values. The model had no such field: a salary earned
    /// today and a retirement income projected thirty years out were both <c>assertionStatus:
    /// Stated</c> and otherwise identical.
    ///
    /// That mattered most where it was least visible. <c>income.source</c> is the only
    /// consistency-reconciled fact — the sole member of the SR-internal repeated-values check —
    /// so the one fact whose job is catching a report contradicting itself could not tell a
    /// projection from a present figure.
    /// </summary>
    [Fact]
    public void TheModelRecordsWhatKindOfClaimEachValueIs()
    {
        using var schema = Schema();

        var modality = schema.RootElement
            .GetProperty("$defs").GetProperty("Provenance")
            .GetProperty("properties").GetProperty("modality")
            .GetProperty("description").GetString()!;

        foreach (var value in (string[])
                 ["Actual", "Reported", "Intended", "Assumed", "Projected", "Recommended",
                  "Conditional", "Unknown"])
        {
            Assert.Contains(value, modality, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Documented as a closed vocabulary in the schema's own prose, so
    /// <see cref="CanonicalVocabulary"/> picks it up from there and corrects the near-misses —
    /// rather than a second copy of the eight values living in code and drifting.
    /// </summary>
    [Fact]
    public void TheModalityVocabularyIsReadOutOfTheSchemaLikeEveryOther()
    {
        var vocabularies = CanonicalVocabulary.Parse(File.ReadAllText(SchemaPath));

        Assert.True(vocabularies.ContainsKey("modality"),
            "modality is not being read as a documented vocabulary, so a value outside it would "
            + "be written and never reported.");

        Assert.Equal(
            ["Actual", "Reported", "Intended", "Assumed", "Projected", "Recommended",
             "Conditional", "Unknown"],
            vocabularies["modality"]);
    }

    /// <summary>
    /// The prompt has to say that modality and assertionStatus are different questions, because
    /// they are easy to conflate and conflating them is silent: both fields get filled, both look
    /// reasonable, and a projection is compared against a fact find as though it were a fact.
    /// </summary>
    [Fact]
    public void TheExtractorIsToldModalityIsADifferentQuestionFromAssertionStatus()
    {
        Assert.Contains("modality is a different question", Prompts.ExtractorSystem,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Projected", Prompts.ExtractorSystem, StringComparison.Ordinal);
        Assert.Contains("Conditional", Prompts.ExtractorSystem, StringComparison.Ordinal);
    }

    // ── walking the schema ────────────────────────────────────────────────────

    /// <summary>The description a canonical path carries, following $refs.</summary>
    private static string? DescriptionAt(JsonDocument schema, string path)
    {
        var defs = schema.RootElement.GetProperty("$defs");
        var node = schema.RootElement;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        JsonElement Resolve(JsonElement element)
        {
            while (element.ValueKind == JsonValueKind.Object
                   && element.TryGetProperty("$ref", out var reference))
            {
                var name = reference.GetString()!.Split('/').Last();

                if (!seen.Add(name) || !defs.TryGetProperty(name, out var target))
                {
                    return element;
                }

                element = target;
            }

            return element;
        }

        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            node = Resolve(node);

            if (!node.TryGetProperty("properties", out var properties)
                || !properties.TryGetProperty(part.Replace("[]", string.Empty), out var child))
            {
                return null;
            }

            node = child;

            if (part.EndsWith("[]", StringComparison.Ordinal))
            {
                var container = Resolve(node);

                if (container.TryGetProperty("items", out var items))
                {
                    node = items;
                }
            }
        }

        if (node.TryGetProperty("description", out var own))
        {
            return own.GetString();
        }

        var resolved = Resolve(node);

        return resolved.ValueKind == JsonValueKind.Object
               && resolved.TryGetProperty("description", out var shared)
            ? shared.GetString()
            : null;
    }
}
