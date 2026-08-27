namespace AiPromptEvaluator;

/// <summary>
/// Executes every plan's retrieval and reports what came back empty — without calling a model.
///
/// <b>Retrieval is deterministic and model-free, and nothing was exploiting that.</b> Three runs
/// on 2026-08-26 returned byte-identical retrieval — 223 searches, 3,520 hits, 224 canonical paths
/// resolved, 31 absent — across three assessor models with recall from 19% to 78%. Whatever the
/// model does with the pack, the pack itself is a pure function of the plans, the index and the
/// settings. So a defect in the plans' retrieval can be found in seconds, for the price of the
/// embeddings, instead of being discovered in the findings of a run that cost pounds and took ten
/// minutes.
///
/// <b>The defect this exists for is silent by construction.</b> A declared evidence section that
/// matches no retrieved passage reserves nothing and changes nothing; a query whose
/// <c>expectSignals</c> never arrive has searched for something it did not find. Neither raises an
/// error, both leave the run looking healthy, and the only trace is a line in a report nobody
/// reads until recall disappoints. It has now happened twice: three hints naming "Residency" and
/// "Contact Address" matched nothing and moved nothing, and then — after a test was written
/// specifically to prevent a recurrence — six of ten newly added hints did the same.
///
/// <b>That test was necessary and not sufficient, which is the reason this class exists.</b>
/// Checking that a hint's string appears in a document of a category the group searches proves the
/// hint <i>could</i> match. It cannot prove any query <i>retrieves</i> the chunk holding it, and
/// six hints passed that test while firing on nothing. Only executing the searches answers the
/// question, so this executes the searches.
/// </summary>
public sealed class RetrievalDryRun
{
    private readonly ICaseDocumentSearchService _search;
    private readonly CanonicalModelAccessor? _accessor;
    private readonly AppSettings _settings;

    public RetrievalDryRun(
        ICaseDocumentSearchService search,
        AppSettings settings,
        CanonicalModelAccessor? accessor = null)
    {
        _search = search;
        _settings = settings;
        _accessor = accessor;
    }

    /// <summary>One group's retrieval, as it would reach an assessor.</summary>
    /// <param name="Requirement">The group's requirement text, so a downloaded extract reads without the plan open.</param>
    /// <param name="Pack">The ranked passages, in pack order — the evidence the assessor would have seen.</param>
    public sealed record GroupResult(
        string CheckId,
        string GroupId,
        string Requirement,
        int Searches,
        int Hits,
        int PackSize,
        IReadOnlyList<string> CategoriesReached,
        IReadOnlyList<string> DeadSections,
        IReadOnlyList<string> EvictedSections,
        IReadOnlyList<string> MissedSignals,
        IReadOnlyList<string> UnresolvedPaths,
        IReadOnlyList<CaseDocumentSearchMatch> Pack)
    {
        /// <summary>Whether the pack is at the cap, so ranking is discarding candidates.</summary>
        public bool AtCap(AppSettings settings) =>
            !AppSettings.IsUnbounded(settings.MaxPassagesPerGroup)
            && PackSize >= settings.MaxPassagesPerGroup;

        /// <summary>A group with nothing wrong to report.</summary>
        public bool IsClean =>
            DeadSections.Count == 0 && EvictedSections.Count == 0
            && MissedSignals.Count == 0 && PackSize > 0;
    }

    /// <summary>The whole run, and the summary a reader acts on.</summary>
    public sealed record Report(IReadOnlyList<GroupResult> Groups, AppSettings Settings)
    {
        public int Searches => Groups.Sum(g => g.Searches);

        public int Hits => Groups.Sum(g => g.Hits);

        /// <summary>
        /// Declared sections that reserved no slot because no retrieved passage contained them.
        /// <b>This is the number to gate on.</b> A dead hint is not a warning about a hint; it is
        /// a plan asking for part of a document and not being given it.
        /// </summary>
        public IReadOnlyList<string> DeadSections =>
            [.. Groups.SelectMany(g => g.DeadSections.Select(s => $"{g.CheckId} {g.GroupId}: {s}"))];

        /// <summary>
        /// Sections whose passage was retrieved and then did not survive into the pack. A
        /// different defect from a hint that matches nothing, and it needs a different answer:
        /// the wording is right and the reservation did not hold it.
        /// </summary>
        public IReadOnlyList<string> EvictedSections =>
            [.. Groups.SelectMany(g => g.EvictedSections.Select(s => $"{g.CheckId} {g.GroupId}: {s}"))];

        /// <summary>Queries whose expected signals never arrived, by query id.</summary>
        public IReadOnlyList<string> MissedSignals =>
            [.. Groups.SelectMany(g => g.MissedSignals.Select(q => $"{g.CheckId} {g.GroupId}: {q}"))];

        /// <summary>Groups that retrieved nothing at all — the loudest possible failure.</summary>
        public IReadOnlyList<string> EmptyGroups =>
            [.. Groups.Where(g => g.Searches > 0 && g.PackSize == 0)
                      .Select(g => $"{g.CheckId} {g.GroupId}")];

        /// <summary>
        /// How many groups are at the passage cap, so ranking is choosing what the assessor sees.
        /// Not a defect — a measurement. It went from 98% of groups at a cap of 12 to 52% at 24.
        /// </summary>
        public int GroupsAtCap => Groups.Count(g => g.AtCap(Settings));

        public string Format()
        {
            var lines = new List<string>
            {
                $"Retrieval dry run — {Groups.Count} group(s), {Searches} search(es), {Hits} hit(s).",
                $"Passage cap {(AppSettings.IsUnbounded(Settings.MaxPassagesPerGroup) ? "unbounded" : Settings.MaxPassagesPerGroup.ToString())}; "
                + $"{GroupsAtCap} group(s) at it.",
            };

            Append(lines, "Groups that retrieved nothing", EmptyGroups);
            Append(lines, "Declared sections matching no retrieved passage", DeadSections);
            Append(lines, "Declared sections retrieved but evicted before the pack", EvictedSections);
            Append(lines, "Queries whose expected signals never arrived", MissedSignals);

            if (EmptyGroups.Count == 0 && DeadSections.Count == 0 && EvictedSections.Count == 0
                && MissedSignals.Count == 0)
            {
                lines.Add("Every group retrieved something, every declared section matched, and "
                          + "every expected signal arrived.");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static void Append(List<string> lines, string heading, IReadOnlyList<string> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            lines.Add($"{heading} ({items.Count}):");
            lines.AddRange(items.Select(i => "  " + i));
        }

        /// <summary>
        /// The full retrieved extract: every group and the passages that reached its pack, in the
        /// order the assessor would have seen them. What to read when a hint fired on nothing and
        /// the question is what the group got instead. See <see cref="RetrievalExtract"/>.
        /// </summary>
        public string FormatExtract() => RetrievalExtract.FromDryRun(this);
    }

    /// <summary>
    /// Runs every group of every plan. Costs one embedding per query and no model call at all.
    /// </summary>
    public async Task<Report> RunAsync(
        IEnumerable<CheckQueryPlan> plans, CancellationToken cancellationToken = default)
    {
        var results = new List<GroupResult>();

        foreach (var plan in plans.OrderBy(p => p.CheckId, StringComparer.Ordinal))
        {
            foreach (var group in plan.QueryGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                results.Add(await GatherAsync(plan, group, cancellationToken).ConfigureAwait(false));
            }
        }

        return new Report(results, _settings);
    }

    /// <summary>
    /// Deliberately mirrors <see cref="CheckPlanRunner"/>'s gather: same de-duplication key, same
    /// targeted-category set, same <see cref="CheckPlanRunner.Rank"/> with the same settings. A
    /// dry run that assembled the pack differently would report on a pack no assessor ever sees,
    /// which is worse than not reporting at all.
    /// </summary>
    private async Task<GroupResult> GatherAsync(
        CheckQueryPlan plan, PlanQueryGroup group, CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var passages = new List<CaseDocumentSearchMatch>();
        var missedSignals = new List<string>();
        var searches = 0;
        var hits = 0;

        foreach (var query in group.QueriesToRun(_settings.CoreQueriesOnly)
                                   .Where(q => q.IsEvidenceSearch))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var found = await _search
                .SearchAsync(
                    query.Text,
                    query.TargetCategories.Count > 0 ? query.TargetCategories : null,
                    cancellationToken,
                    plan.Retrieval?.ResultsPerCall)
                .ConfigureAwait(false);

            searches++;
            hits += found.Count;

            foreach (var hit in found)
            {
                if (seen.Add(CheckPlanRunner.DeduplicationKey(hit)))
                {
                    passages.Add(hit);
                }
            }

            if (query.ExpectSignals.Count > 0
                && !query.ExpectSignals.Any(signal => found.Any(
                    h => h.SearchedText.Contains(signal, StringComparison.OrdinalIgnoreCase))))
            {
                missedSignals.Add(query.Id);
            }
        }

        var targeted = group.Queries
            .Where(q => q.IsEvidenceSearch)
            .SelectMany(q => q.TargetCategories)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ranked = CheckPlanRunner.Rank(
            passages, targeted, group.DeclaredEvidenceSections, group.DeclaredEvidenceCategories,
            _settings);

        // Resolved against the stored model where one was supplied. A path that resolves to
        // nothing is not always a defect — for many checks it is the finding — so it is reported
        // and never gated on.
        // Against the pack, not the candidates: a hint whose passage was retrieved and then
        // evicted has not done its job, and reporting it as matched is how a retrieval defect
        // came to be diagnosed as a reasoning one.
        var reach = CheckPlanRunner.UnmatchedSections(
            passages, ranked, group.DeclaredEvidenceSections);

        var unresolved = _accessor is null
            ? []
            : _accessor.Resolve(group.AllCanonicalPaths)
                .Where(f => !f.Found)
                .Select(f => f.Path)
                .ToList();

        return new GroupResult(
            plan.CheckId.Trim(),
            group.GroupId,
            group.Requirement,
            searches,
            hits,
            ranked.Count,
            [.. ranked.Select(p => p.CategoryCode)
                      .Where(c => !string.IsNullOrWhiteSpace(c))
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .OrderBy(c => c, StringComparer.Ordinal)],
            reach.MatchedNothing,
            reach.Evicted,
            missedSignals,
            unresolved,
            ranked);
    }
}
