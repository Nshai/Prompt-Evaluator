namespace AiPromptEvaluator;

/// <summary>One populated path and the groups, if any, that select it.</summary>
/// <param name="Path">The plan-shaped canonical path, e.g. <c>/taxTreatment/allowancesUsed[]</c>.</param>
/// <param name="Readers">
/// Every <c>CHK-nnn/Gn.n</c> whose selection covers this path. Empty is the finding: the
/// extraction wrote a value that no prompt in the run will contain.
/// </param>
public sealed record PathReaders(string Path, IReadOnlyList<string> Readers)
{
    public bool IsRead => Readers.Count > 0;
}

/// <summary>
/// The coverage measurement for one model against one set of plans.
/// </summary>
/// <param name="Populated">Distinct plan-shaped paths the model populates.</param>
/// <param name="Unread">Those paths no group selects, in document order.</param>
/// <param name="Selected">Every path the plans select, whether or not the model populates it.</param>
/// <param name="Unpopulated">
/// Paths the plans select that this model holds nothing at. Not a defect — most are a check
/// asking a question this case does not raise — but a path no case has ever populated is a plan
/// selecting a path the schema does not have, which is the same silent miss seen from the other
/// end.
/// </param>
public sealed record PathCoverageReport(
    IReadOnlyList<PathReaders> Populated,
    IReadOnlyList<string> Unread,
    IReadOnlyList<string> Selected,
    IReadOnlyList<string> Unpopulated)
{
    public int PopulatedCount => Populated.Count;

    public int ReadCount => Populated.Count - Unread.Count;

    /// <summary>The headline: what share of the extraction some check will actually read.</summary>
    public double ReadFraction =>
        Populated.Count == 0 ? 1d : (double)ReadCount / Populated.Count;

    /// <summary>
    /// Unread paths grouped by their first segment, largest root first.
    ///
    /// The roots are the unit a fix is authored in: the measurement found the same six —
    /// <c>recommendations</c>, <c>taxTreatment</c>, <c>replacementAnalysis</c>,
    /// <c>clientAuthority</c>, <c>ongoingService</c>, <c>research</c> — dominating on two
    /// unrelated cases, and a group added for a root closes dozens of paths at once.
    /// </summary>
    public IReadOnlyList<(string Root, IReadOnlyList<string> Paths)> UnreadByRoot =>
        Unread
            .GroupBy(Root, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (g.Key, (IReadOnlyList<string>)g.ToList()))
            .ToList();

    private static string Root(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Split('[')[0]
        ?? string.Empty;

    /// <summary>The report as the coverage test prints it when it fails.</summary>
    public string Describe(int maxPathsPerRoot = 6)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine(
            $"{ReadCount} of {PopulatedCount} populated canonical paths ({ReadFraction:P0}) are "
            + "selected by at least one query group.");

        foreach (var (root, paths) in UnreadByRoot)
        {
            sb.AppendLine($"  /{root} — {paths.Count} unread");

            foreach (var path in paths.Take(maxPathsPerRoot))
            {
                sb.AppendLine($"      {path}");
            }

            if (paths.Count > maxPathsPerRoot)
            {
                sb.AppendLine($"      … and {paths.Count - maxPathsPerRoot} more");
            }
        }

        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Which canonical paths the query plans actually read, and which they do not.
///
/// <b>This is the measurement that changed the pipeline's diagnosis.</b> Nineteen runs were spent
/// tuning retrieval, ranking, pack size and model choice against a case that plateaued after the
/// third; the loss that dominates all of them turned out to be upstream of every one. One fifth
/// of everything the extractor produces — 253 of 1,333 populated paths on one case, 328 of 1,661
/// on another — is selected by no group in any of the ten plans, so it is extracted, validated,
/// stored, paid for and then read by nothing.
///
/// It is not a subtle failure. The report applying a Residence Nil Rate Band to a client the file
/// records as a tenant sat in <c>/taxTreatment/allowancesUsed</c> for eight consecutive runs while
/// three prompt guards and a hint were added against it; no group selects that path, so no prompt
/// in any of those runs contained the claim. On another case the extraction wrote the finding out
/// in English — <c>"Recommended funds underperformed against Nest over 5 years: 45.91% vs
/// 48.06%"</c> — at <c>/research/comparisons[]/finding</c>, which no group selects either.
///
/// <b>The rule is a build-time assertion, not a run-time filter.</b> Nothing here changes what a
/// run does. It fails a test, in the words of the fix, before anyone spends money discovering the
/// same thing from an output.
/// </summary>
public static class PathCoverage
{
    /// <summary>
    /// Measures one canonical model against a set of plans.
    /// </summary>
    /// <param name="modelJson">A stored canonical model.</param>
    /// <param name="plans">The loaded query plans.</param>
    public static PathCoverageReport Measure(string modelJson, IEnumerable<CheckQueryPlan> plans) =>
        Measure(CanonicalPaths.PopulatedPaths(CanonicalPaths.Enumerate(modelJson)), plans);

    /// <summary>
    /// Measures a list of plan-shaped populated paths against a set of plans.
    /// </summary>
    public static PathCoverageReport Measure(
        IReadOnlyList<string> populatedPaths, IEnumerable<CheckQueryPlan> plans)
    {
        // Selections are held with the group that made them, so an unread path can be fixed by
        // naming the group that should have had it rather than by searching ten files for the
        // one that nearly does.
        var selections = plans
            .SelectMany(plan => plan.QueryGroups.SelectMany(group => group
                .AllCanonicalPaths
                .Select(path => (Path: CanonicalPaths.Normalise(path.Trim()),
                                 Reader: $"{plan.CheckId}/{group.GroupId}"))))
            .Where(s => s.Path.Length > 0)
            .ToList();

        var populated = populatedPaths
            .Select(path => new PathReaders(
                path,
                selections
                    .Where(s => Covers(s.Path, path))
                    .Select(s => s.Reader)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(r => r, StringComparer.Ordinal)
                    .ToList()))
            .ToList();

        var selected = selections
            .Select(s => s.Path)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        return new PathCoverageReport(
            populated,
            [.. populated.Where(p => !p.IsRead).Select(p => p.Path)],
            selected,
            [.. selected.Where(s => !populatedPaths.Any(p => Covers(s, p)))]);
    }

    /// <summary>
    /// Whether selecting <paramref name="selected"/> puts <paramref name="populated"/> in front of
    /// the assessor.
    ///
    /// <b>A selected parent covers its children</b>, because that is what the runner does:
    /// <c>CanonicalModelAccessor.Resolve("/objectives[]")</c> returns whole objects, so a group
    /// naming the parent reads every field under it. Measuring coverage on exact equality would
    /// report as unread hundreds of paths the assessor demonstrably reads, and the 20% figure
    /// would be an artefact rather than a finding.
    ///
    /// The boundary matters: <c>/objectives</c> must not cover <c>/objectivesReview</c>, so a
    /// match has to end at a separator or at the end of the path.
    /// </summary>
    internal static bool Covers(string selected, string populated)
    {
        if (selected.Length == 0)
        {
            return false;
        }

        if (!populated.StartsWith(selected, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return populated.Length == selected.Length || populated[selected.Length] is '/' or '[';
    }
}
