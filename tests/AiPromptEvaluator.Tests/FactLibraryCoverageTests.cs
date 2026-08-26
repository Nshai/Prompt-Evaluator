using System.Text.Json;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The three artefacts that have to agree: the published fact library, the canonical model, and
/// the query plans.
///
/// <b>A fact the extractor publishes and no requirement reads is worse than one never extracted.</b>
/// It reaches the canonical model, it is shown on the page as an established fact, and nothing
/// ever compares it with the file — so a wrong figure there has the appearance of having been
/// checked and the substance of having been typed. Reconciling the three by hand found seven such
/// facts at once, among them the target retirement income, which is the number a whole retirement
/// recommendation is built on.
///
/// The map is <c>docs/artifacts/fact-library.map.json</c>, generated from the library and copied
/// to the build output. These tests make the reconciliation a build-time check rather than an
/// audit somebody remembers to run.
/// </summary>
public class FactLibraryCoverageTests
{
    private static string PlanFolder => Path.Combine(AppContext.BaseDirectory, "check-plan");

    private sealed record MappedFact(string Id, string Title, string Section,
        IReadOnlyList<string> CanonicalPaths);

    private static IReadOnlyList<MappedFact> Library()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fact-library.map.json")));

        return document.RootElement.GetProperty("facts").EnumerateArray()
            .Select(f => new MappedFact(
                f.GetProperty("id").GetString()!,
                f.GetProperty("title").GetString()!,
                f.GetProperty("section").GetString()!,
                f.GetProperty("canonicalPaths").EnumerateArray()
                    .Select(p => p.GetString()!).ToList()))
            .ToList();
    }

    /// <summary>Every canonical path a plan group names, group-level and per-query.</summary>
    private static IReadOnlyList<(string Group, string Path)> PlanPaths()
    {
        var (plans, failures) = CheckQueryPlanLoader.Load(PlanFolder);

        Assert.Empty(failures);

        return plans.Values
            .SelectMany(plan => plan.QueryGroups.Select(group => (plan, group)))
            .SelectMany(x =>
            {
                var label = x.plan.CheckId.Trim() + " " + x.group.GroupId;
                var paths = (x.group.Retrieval?.CanonicalPaths ?? [])
                    .Concat((x.group.Retrieval?.Queries ?? [])
                        .SelectMany(q => q.CanonicalPaths ?? []));

                return paths.Select(p => (Group: label, Path: p));
            })
            .ToList();
    }

    /// <summary>
    /// One path is the other, or an ancestor of it. A group naming <c>/parties/clients[]</c>
    /// reads the age underneath it; a group naming the age reads part of the client.
    /// </summary>
    private static bool Related(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
        || a.StartsWith(b + "/", StringComparison.OrdinalIgnoreCase)
        || b.StartsWith(a + "/", StringComparison.OrdinalIgnoreCase)
        || a.StartsWith(b + "[]", StringComparison.OrdinalIgnoreCase)
        || b.StartsWith(a + "[]", StringComparison.OrdinalIgnoreCase);

    // ── the reconciliation ────────────────────────────────────────────────────

    /// <summary>
    /// The regression this file exists for. Every published fact is read by at least one
    /// requirement, so nothing the pipeline extracts reaches a report unchecked.
    /// </summary>
    [Fact]
    public void EveryPublishedFactIsReadByAtLeastOneRequirement()
    {
        var paths = PlanPaths();

        var unread = Library()
            .Where(f => !f.CanonicalPaths.Any(w => paths.Any(p => Related(w, p.Path))))
            .Select(f => $"{f.Id} ({f.Title}) — {string.Join(", ", f.CanonicalPaths)}")
            .ToList();

        Assert.True(
            unread.Count == 0,
            "These facts are extracted and shown as established, and no requirement compares "
            + "them with anything:" + Environment.NewLine
            + string.Join(Environment.NewLine, unread.Select(u => "  " + u)));
    }

    /// <summary>
    /// Every fact has somewhere to live in the canonical model.
    ///
    /// Two did not, and both were quietly lossy rather than loudly broken: a pension sharing
    /// order has nothing in common with a Lifetime Allowance protection, and an annuity
    /// quotation's income per annum does not fit in a single comparative-cost amount.
    /// </summary>
    [Fact]
    public void EveryPublishedFactHasAHomeInTheCanonicalModel()
    {
        var schema = SchemaPaths();

        var homeless = Library()
            .Where(f => !f.CanonicalPaths.Any(w => schema.Any(s =>
                string.Equals(s, w, StringComparison.OrdinalIgnoreCase)
                || s.StartsWith(w + "/", StringComparison.OrdinalIgnoreCase)
                || s.StartsWith(w + "[]", StringComparison.OrdinalIgnoreCase))))
            .Select(f => $"{f.Id} — {string.Join(", ", f.CanonicalPaths)}")
            .ToList();

        Assert.True(
            homeless.Count == 0,
            "These facts are published with no field in the canonical model to hold them:"
            + Environment.NewLine + string.Join(Environment.NewLine, homeless.Select(h => "  " + h)));
    }

    /// <summary>
    /// The library's size, pinned. A fact added to the library and not to the map would otherwise
    /// pass both tests above by not being looked at.
    /// </summary>
    [Fact]
    public void TheMapCoversTheWholePublishedLibrary()
    {
        var library = Library();

        Assert.Equal(44, library.Count);
        Assert.Equal(library.Count, library.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(library, f => Assert.NotEmpty(f.CanonicalPaths));
    }

    /// <summary>
    /// A fact read by exactly one requirement is not a defect, but it is worth being able to see:
    /// the requirement is that fact's only reader, and dropping a path from it silently stops the
    /// fact being checked at all. This pins the count so such a change is visible in a diff.
    /// </summary>
    [Fact]
    public void FactsWithASingleReaderStayVisible()
    {
        var paths = PlanPaths();

        var single = Library()
            .Select(f => (f.Id, Readers: paths
                .Where(p => f.CanonicalPaths.Any(w => Related(w, p.Path)))
                .Select(p => p.Group)
                .Distinct(StringComparer.Ordinal)
                .ToList()))
            .Where(x => x.Readers.Count == 1)
            .Select(x => $"{x.Id} → {x.Readers[0]}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            single.Count <= 16,
            "More facts now rest on a single requirement than when this was last reviewed:"
            + Environment.NewLine + string.Join(Environment.NewLine, single.Select(s => "  " + s)));
    }

    // ── the model's own paths ─────────────────────────────────────────────────

    private static IReadOnlyList<string> SchemaPaths()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "canonical-suitability-model.schema.json")));

        var root = document.RootElement;
        var defs = root.TryGetProperty("$defs", out var d) ? d : default;
        var paths = new List<string>();

        Walk(root, string.Empty, [], paths, defs, 0);

        return paths;
    }

    private static void Walk(
        JsonElement node, string path, HashSet<string> seen, List<string> into,
        JsonElement defs, int depth)
    {
        if (depth > 8 || node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (node.TryGetProperty("$ref", out var reference))
        {
            var name = reference.GetString()!.Split('/').Last();

            if (!seen.Add(name) || defs.ValueKind != JsonValueKind.Object
                || !defs.TryGetProperty(name, out var target))
            {
                return;
            }

            Walk(target, path, seen, into, defs, depth + 1);
            return;
        }

        if (node.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                var child = path + "/" + property.Name;
                into.Add(child);
                Walk(property.Value, child, [.. seen], into, defs, depth + 1);
            }
        }

        if (node.TryGetProperty("items", out var items))
        {
            Walk(items, path + "[]", seen, into, defs, depth + 1);
        }
    }
}
