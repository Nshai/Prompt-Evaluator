using System.Text.Json.Nodes;

namespace AiPromptEvaluator;

/// <summary>
/// One populated scalar in a stored canonical model, with where it came from.
/// </summary>
/// <param name="Path">
/// The path as it actually resolves, array indices and all — <c>/objectives[0]/summary</c>. This
/// is what a reader follows back into the model.
/// </param>
/// <param name="NormalisedPath">
/// The same path with every index replaced by <c>[]</c> — <c>/objectives[]/summary</c>. This is
/// the form the query plans are written in, so it is the form coverage is measured against.
/// </param>
/// <param name="Value">The scalar, rendered as the assessor should read it.</param>
/// <param name="Source">
/// The section heading and document the extraction recorded for the object this leaf sits on, or
/// null where the extraction recorded no provenance. Carried rather than the whole quote: the
/// quote averages a hundred characters and the heading is what makes a value checkable.
/// </param>
/// <param name="Modality">
/// What kind of statement the enclosing object is, from its provenance — <c>Actual</c>,
/// <c>Projected</c>, <c>Assumed</c>, <c>Recommended</c> and so on. Inherited from the nearest
/// enclosing provenance block, because that is how the extraction records it: one block describes
/// every value on the object beneath it.
///
/// <b>This is what makes a comparison refusable.</b> A projection set against a current figure is
/// not a discrepancy, and a measured extract carries 60 Projected, 29 Assumed and 13 Conditional
/// values that a current-figure comparison has no business touching.
/// </param>
/// <param name="AsAt">
/// The date the enclosing section was stated as at, where the extraction recorded one. Inherited
/// the same way — <c>/financialPosition/asAt</c> governs every figure in that section.
/// </param>
public sealed record CanonicalLeaf(
    string Path,
    string NormalisedPath,
    string Value,
    string? Source,
    string? Modality = null,
    string? AsAt = null)
{
    /// <summary>
    /// Whether this value states something that is the case, as opposed to something projected,
    /// assumed, intended or conditional.
    ///
    /// An unrecorded modality counts as current: the extraction leaves it off where the statement
    /// is an ordinary one, and treating silence as "projected" would refuse most of the model.
    /// </summary>
    public bool IsCurrent =>
        Modality is null
        || Modality is "Actual" or "Reported" or "Recommended"
        || !NonCurrent.Contains(Modality);

    private static readonly IReadOnlySet<string> NonCurrent =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Projected", "Assumed", "Intended", "Conditional",
        };
}

/// <summary>
/// Enumerates every populated leaf of a canonical model.
///
/// <b>Two separate measurements in the pipeline analysis need exactly this list and neither could
/// take it.</b> The path-coverage measurement — ~20% of the model is selected by no query group,
/// on both cases measured — was taken by hand against a saved extract. The assertion digest
/// cannot be built at all without it. Both are now computed from one walk, so the number the
/// coverage test reports and the lines the assessor reads can never describe different models.
///
/// <b>What is deliberately not a leaf.</b> Provenance blocks, the extraction's self-report and
/// the model's own version stamp are the pipeline talking about itself, not the report making a
/// claim. Counting them would inflate the denominator of every coverage figure with paths no
/// check should ever select, and printing them would triple the digest with the same heading
/// repeated under every field. The heading is kept — as <see cref="CanonicalLeaf.Source"/> on the
/// leaves the provenance block describes — which is the part that makes a value traceable.
/// </summary>
public static class CanonicalPaths
{
    /// <summary>
    /// Segments that are the pipeline describing its own work rather than the report making a
    /// claim. Matched on the property name at any depth.
    /// </summary>
    public static readonly IReadOnlySet<string> Metadata = new HashSet<string>(StringComparer.Ordinal)
    {
        "provenance",
        "extractionReport",
        "modelVersion",
    };

    /// <summary>How much of one scalar is worth carrying into a digest line.</summary>
    private const int MaxValueCharacters = 160;

    /// <summary>
    /// Every populated leaf, in document order.
    ///
    /// Document order rather than sorted, so the digest reads in the order the schema declares
    /// and two runs of the same model produce byte-identical output — a first-class requirement
    /// here, since the digest goes into a prompt whose prefix the provider caches.
    /// </summary>
    public static IReadOnlyList<CanonicalLeaf> Enumerate(string modelJson)
    {
        var root = JsonNode.Parse(modelJson) as JsonObject
            ?? throw new InvalidOperationException("The stored canonical model is not a JSON object.");

        return Enumerate(root);
    }

    /// <inheritdoc cref="Enumerate(string)"/>
    public static IReadOnlyList<CanonicalLeaf> Enumerate(JsonObject root)
    {
        var leaves = new List<CanonicalLeaf>();
        Walk(root, string.Empty, string.Empty, default, leaves);
        return leaves;
    }

    /// <summary>
    /// The distinct plan-shaped paths a model populates — <c>/objectives[]/summary</c> rather
    /// than one entry per array element.
    ///
    /// This is the left-hand side of the coverage intersection. Distinct, because a model with
    /// forty holdings does not have forty times the coverage problem of one with one.
    /// </summary>
    public static IReadOnlyList<string> PopulatedPaths(IEnumerable<CanonicalLeaf> leaves) =>
        leaves
            .Select(l => l.NormalisedPath)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Replaces every array index in a resolved path with the plan's <c>[]</c> marker, and
    /// removes any <c>[key=value]</c> selector for the same reason: both are ways of naming one
    /// element of an array the plan names as a whole.
    /// </summary>
    public static string Normalise(string path)
    {
        if (path.IndexOf('[') < 0)
        {
            return path;
        }

        var built = new System.Text.StringBuilder(path.Length);
        var depth = 0;

        foreach (var c in path)
        {
            if (c == '[')
            {
                if (depth++ == 0)
                {
                    built.Append("[]");
                }

                continue;
            }

            if (c == ']')
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }

            if (depth == 0)
            {
                built.Append(c);
            }
        }

        return built.ToString();
    }

    /// <summary>
    /// What an enclosing object says about every value beneath it: where it was read, what kind of
    /// statement it is, and the date it was stated as at.
    ///
    /// Inherited rather than looked up per leaf, because that is how the extraction writes it — one
    /// provenance block and one <c>asAt</c> govern a whole section, and a figure two levels down
    /// carries them without repeating them.
    /// </summary>
    private readonly record struct Inherited(string? Source, string? Modality, string? AsAt);

    private static void Walk(
        JsonNode? node,
        string path,
        string normalised,
        Inherited inherited,
        List<CanonicalLeaf> into)
    {
        switch (node)
        {
            case JsonObject obj:
                // Read before descending, so every leaf on this object carries them — a sibling
                // property is what the provenance block is provenance for.
                var here = inherited with
                {
                    Source = SourceOf(obj) ?? inherited.Source,
                    Modality = Text(obj["provenance"]?["modality"]) is { Length: > 0 } modality
                        ? modality
                        : inherited.Modality,
                    AsAt = Text(obj["asAt"]) is { Length: > 0 } asAt ? asAt : inherited.AsAt,
                };

                foreach (var (name, child) in obj)
                {
                    if (Metadata.Contains(name))
                    {
                        continue;
                    }

                    Walk(child, $"{path}/{name}", $"{normalised}/{name}", here, into);
                }

                break;

            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    Walk(array[i], $"{path}[{i}]", $"{normalised}[]", inherited, into);
                }

                break;

            case JsonValue value:
                var scalar = Render(value);

                // An empty string and a null are the report being silent, which is what an
                // absent key already means. Counting them as populated would report coverage of
                // paths no check could ever find anything at.
                if (scalar.Length > 0)
                {
                    into.Add(new CanonicalLeaf(
                        path, normalised, scalar, inherited.Source, inherited.Modality, inherited.AsAt));
                }

                break;
        }
    }

    /// <summary>
    /// The heading and document an object's provenance block names, as one short string.
    /// </summary>
    private static string? SourceOf(JsonObject obj)
    {
        if (obj["provenance"] is not JsonObject provenance)
        {
            return null;
        }

        var heading = Text(provenance["sectionHeading"]);
        var document = Text(provenance["documentId"]);

        return (heading.Length, document.Length) switch
        {
            (0, 0) => null,
            (0, _) => document,
            (_, 0) => heading,
            _ => $"{heading} — {document}",
        };
    }

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text.Trim()
            : string.Empty;

    /// <summary>
    /// One scalar as a digest line should carry it: unquoted, single-line, and short enough that
    /// four hundred of them are still a page rather than a document.
    /// </summary>
    private static string Render(JsonValue value)
    {
        var text = value.TryGetValue<string>(out var s)
            ? s
            : value.ToJsonString();

        text = text.Trim();

        if (text.Length == 0)
        {
            return string.Empty;
        }

        // Newlines inside a value would break the one-line-per-path contract the digest's
        // density depends on.
        var flattened = string.Join(
            " ",
            text.Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return flattened.Length <= MaxValueCharacters
            ? flattened
            : flattened[..MaxValueCharacters].TrimEnd() + "…";
    }
}
