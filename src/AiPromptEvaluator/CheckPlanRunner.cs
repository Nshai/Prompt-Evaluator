using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.AI;

namespace AiPromptEvaluator;

/// <summary>Progress for one check in a run.</summary>
public sealed record CheckRunProgress(int Done, int Total, string CheckId, string Stage);

/// <summary>
/// Runs a check from its query plan.
///
/// The plan splits every requirement into two sides, and this executes them from two
/// different places:
///
///   Assertion side — what the suitability report claims — is read out of the stored
///   canonical model by canonical path. The report is never sent to the model again.
///
///   Evidence side — what the rest of the case file holds — is retrieved from the vector
///   store using the plan's search text.
///
/// Retrieval is deterministic: the plan decides which searches run, not the model. The model is
/// called once per requirement, at the end, to judge an evidence pack it did not assemble. Two
/// runs of the same check over the same case therefore see exactly the same evidence.
///
/// The check's own outcome is never asked for. It is computed from the requirement findings, so
/// it cannot disagree with them — and, more to the point, cannot be stated before they exist.
/// </summary>
public sealed class CheckPlanRunner : IDisposable
{
    private static readonly JsonSerializerOptions FindingOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// How many passages of one group's retrieval are shown to the assessor. The searches in
    /// a group overlap heavily by design, and past a dozen passages the pack is mostly the
    /// same text scored slightly differently.
    /// </summary>
    public const int MaxPassagesPerGroup = 12;

    /// <summary>
    /// How much of the extraction's self-report reaches an assessor.
    ///
    /// The extraction report is what lets a group tell a report that is genuinely silent from
    /// one the extraction failed to read, and it carries the contradictions extraction found in
    /// the report itself — which is the only route CHK-001's internal-consistency requirement
    /// has to them. Sized to hold a whole report; the observed model is around 9,000 characters
    /// and the previous 4,000 dropped 55% of it.
    /// </summary>
    public const int ExtractionReportMaxChars = 24_000;

    /// <summary>Output cap for a decision. Enough for a structured finding across every group.</summary>
    private const int DecisionMaxTokens = 8000;

    private readonly AppSettings _settings;
    private readonly PromptEvaluator _evaluator;
    private readonly CaseDocumentSearchTool _search;
    private readonly CanonicalModelDocument _model;
    private readonly CanonicalModelAccessor _accessor;
    private readonly IReadOnlyList<DerivedFigures.Figure> _derived;
    private readonly PromptLogWriter? _promptLog;
    private readonly ConcurrencyGate _modelCalls;
    private readonly ConcurrencyGate _searches;
    private readonly bool _ownsGates;

    /// <param name="modelCalls">
    /// The run-wide budget for assessment calls. Shared across every check so the total in
    /// flight is bounded once, rather than once per level of parallelism. One is created for
    /// this runner alone when none is given.
    /// </param>
    /// <param name="searches">The same, for retrieval.</param>
    public CheckPlanRunner(
        AppSettings settings,
        PromptEvaluator evaluator,
        CaseDocumentSearchTool search,
        CanonicalModelDocument model,
        PromptLogWriter? promptLog = null,
        ConcurrencyGate? modelCalls = null,
        ConcurrencyGate? searches = null)
    {
        _settings = settings;
        _evaluator = evaluator;
        _search = search;
        _model = model;
        _accessor = new CanonicalModelAccessor(model.Json);

        // Computed once for the run rather than per check: the arithmetic is a property of the
        // model, and asking the assessor to redo it per group is how it came to be done
        // differently in different groups.
        _derived = DerivedFigures.From(model.Json);
        _promptLog = promptLog;

        _ownsGates = modelCalls is null && searches is null;
        _modelCalls = modelCalls ?? new ConcurrencyGate(settings.MaxParallelRequests);
        _searches = searches ?? new ConcurrencyGate(settings.MaxParallelRequests);
    }

    /// <summary>Assesses one check and returns its finding.</summary>
    public async Task<CheckFinding> RunAsync(
        AssessmentCheck check,
        CheckQueryPlan plan,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.StartNew();

        try
        {
            progress?.Report($"{plan.CheckId}: retrieving evidence");

            var trigger = await ProbeTriggerAsync(plan, cancellationToken).ConfigureAwait(false);

            // A plan that says a missing trigger settles the check is taken at its word. This
            // is the cheapest possible outcome and the most commonly wrong one to guess at,
            // which is exactly why the plan states it rather than leaving it to the model.
            if (trigger is { Applies: false, Settles: true })
            {
                startedAt.Stop();
                return new CheckFinding
                {
                    CheckId = plan.CheckId,
                    CheckName = plan.CheckName,
                    Outcome = nameof(CheckOutcome.NotApplicable),
                    Summary =
                        $"Trigger absent: {plan.TriggerProbe?.AbsentWhen ?? "the check does not apply to this case"}. "
                        + $"{trigger.Detail}",
                    SearchesRun = trigger.Searches,
                    PassagesRetrieved = trigger.Passages,
                    Elapsed = startedAt.Elapsed,
                };
            }

            // Retrieval for one group has nothing to do with retrieval for another, so it does
            // not wait for it. Results land in an array by position, which keeps the pack order
            // identical to the plan's however the searches happen to finish.
            var packs = new GroupEvidence[plan.QueryGroups.Count];
            var retrieved = 0;

            await ForEachAsync(plan.QueryGroups.Count, async (i, token) =>
            {
                packs[i] = await _searches
                    .RunAsync(t => GatherAsync(plan.QueryGroups[i], t), token)
                    .ConfigureAwait(false);

                progress?.Report(
                    $"{plan.CheckId}: retrieved {Interlocked.Increment(ref retrieved)}/{packs.Length}");
            }, cancellationToken).ConfigureAwait(false);

            // One call per requirement rather than one per check.
            //
            // A check with nine groups previously produced nine verdicts in a single generation
            // of several thousand lines, where each group's reasoning was conditioned on every
            // group written before it and none of them could be retried alone. Assessing one
            // requirement at a time gives the model a pack small enough to attend to in full,
            // isolates a failure to the group that caused it, and keeps the check header
            // identical at the front of every prompt so the provider's prefix cache covers it.
            var systemPrompt = BuildSystemPrompt();
            var header = BuildCheckHeader(check, plan, trigger);

            // Likewise the assessments. Each judges one requirement against its own pack and
            // knows nothing of the others, so the only thing serialising them bought was a
            // longer run. Findings and usage are collected by position for the same reason as
            // above: the report reads in plan order whatever order the answers arrive in.
            var findings = new GroupFinding[packs.Length];
            var usages = new TokenUsage[packs.Length];
            var assessed = 0;

            await ForEachAsync(packs.Length, async (i, token) =>
            {
                var pack = packs[i];
                var userPrompt = header + BuildGroupPrompt(pack);

                var result = await _modelCalls
                    .RunAsync(t => _evaluator.RunRawAsync(
                        systemPrompt,
                        userPrompt,
                        DecisionMaxTokens,
                        FindingSchema.ResponseFormat(_settings.StructuredFindings),
                        t), token)
                    .ConfigureAwait(false);

                _promptLog?.LogExchange(
                    $"{plan.CheckId}/{pack.Group.GroupId}", plan.CheckName,
                    systemPrompt, userPrompt, result.Response);

                usages[i] = result.Breakdown.Usage;

                // Verified against the evidence this group was actually given — never against
                // the whole check's evidence, or a quote lifted from a neighbouring group's
                // passages would verify and the check would be worthless.
                findings[i] = CitationVerifier.Verify(
                    ParseGroup(result.Response, pack.Group),
                    EvidenceTextOf(pack),
                    PassagesById(pack));

                progress?.Report(
                    $"{plan.CheckId}: assessed {Interlocked.Increment(ref assessed)}/{packs.Length}");
            }, cancellationToken).ConfigureAwait(false);

            var usage = usages.Aggregate(TokenUsage.Empty, AddUsage);

            startedAt.Stop();

            // An overlay that ran with a missing trigger has not been excused anything, so its
            // summary must not open by implying the check did not really apply.
            var triggerNote =
                trigger.Applies ? null
                : plan.TriggerProbe?.ContinuesWithReducedScope == true
                    ? "No trigger was recorded; this check applies to every case and was assessed anyway."
                    : "The trigger appears absent.";

            var finding = CheckFinding.FromGroups(
                plan.CheckId, plan.CheckName, findings, triggerNote);

            return finding with
            {
                SearchesRun = trigger.Searches + packs.Sum(p => p.Searches),
                PassagesRetrieved = trigger.Passages + packs.Sum(p => p.TotalPassages),
                CanonicalPathsResolved = packs.Sum(p => p.Fragments.Count(f => f.Found)),
                CanonicalPathsMissing = packs.Sum(p => p.Fragments.Count(f => !f.Found)),
                Usage = usage,
                Elapsed = startedAt.Elapsed,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            startedAt.Stop();
            return CheckFinding.Failed(plan.CheckId, plan.CheckName, ex.Message.Trim(), startedAt.Elapsed);
        }
    }

    // ──────────────────────────────────────────────
    // Retrieval
    // ──────────────────────────────────────────────

    private sealed record TriggerOutcome(bool Applies, bool Settles, string Detail, int Searches, int Passages);

    /// <summary>
    /// Establishes whether the check applies. The canonical model's own checkTriggers field
    /// is consulted first — it was derived when the report was read in full, which is better
    /// evidence than a similarity search — and searching is only used to corroborate it.
    /// </summary>
    private async Task<TriggerOutcome> ProbeTriggerAsync(CheckQueryPlan plan, CancellationToken cancellationToken)
    {
        var probe = plan.TriggerProbe;
        if (probe is null)
        {
            return new TriggerOutcome(true, false, "No trigger probe in the plan; the check always applies.", 0, 0);
        }

        var detail = new StringBuilder();
        bool? fromModel = null;

        if (!string.IsNullOrWhiteSpace(probe.TriggerField))
        {
            var fragment = _accessor.Resolve("/" + probe.TriggerField.Replace('.', '/'));
            if (fragment.Found)
            {
                fromModel = fragment.Json.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                detail.Append($"Canonical model {probe.TriggerField} = {fragment.Json.Trim()}. ");
            }
            else
            {
                detail.Append($"Canonical model has no value for {probe.TriggerField}. ");
            }
        }

        var searches = 0;
        var passages = 0;

        foreach (var query in probe.Queries.Where(q => q.IsEvidenceSearch))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hits = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
            searches++;
            passages += hits.Count;
        }

        detail.Append($"{passages} corroborating passage(s) from {searches} probe search(es).");

        // The model's trigger field decides when it has one; otherwise fall back to whether
        // the probe searches found anything at all.
        var applies = fromModel ?? passages > 0;

        return new TriggerOutcome(applies, probe.ReturnsNotApplicable, detail.ToString(), searches, passages);
    }

    internal sealed record GroupEvidence(
        PlanQueryGroup Group,
        IReadOnlyList<CanonicalFragment> Fragments,
        IReadOnlyList<CaseDocumentSearchMatch> Passages,
        int Searches,
        int TotalPassages,
        IReadOnlyList<string> CategoriesFound,
        IReadOnlyList<string> MissedSignals);

    /// <summary>
    /// Assembles one group's evidence: the assertion side from the stored model, the evidence
    /// side from the vector store, de-duplicated and trimmed to what is worth reading.
    /// </summary>
    private async Task<GroupEvidence> GatherAsync(PlanQueryGroup group, CancellationToken cancellationToken)
    {
        var fragments = _accessor.Resolve(group.AllCanonicalPaths);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var passages = new List<CaseDocumentSearchMatch>();
        var searches = 0;
        var total = 0;
        var missedSignals = new List<string>();

        foreach (var query in group.Queries.Where(q => q.IsEvidenceSearch))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hits = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
            searches++;
            total += hits.Count;

            // The same chunk comes back for several wordings of the same question; keep the
            // first and drop the rest so the pack is evidence rather than repetition.
            foreach (var hit in hits)
            {
                if (seen.Add(DeduplicationKey(hit)))
                {
                    passages.Add(hit);
                }
            }

            if (query.ExpectSignals.Count > 0 && !SignalPresent(query, hits))
            {
                missedSignals.Add(query.Id);
            }
        }

        // The search tool cannot filter by document category, so the plan's targetCategories
        // are applied here instead: a passage from a category this group is looking for
        // outranks a better-scoring one from a category it is not.
        var targeted = group.Queries
            .Where(q => q.IsEvidenceSearch)
            .SelectMany(q => q.TargetCategories)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ranked = Rank(passages, targeted, group.DeclaredEvidenceSections);

        var categories = ranked
            .Select(p => p.CategoryCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new GroupEvidence(group, fragments, ranked, searches, total, categories, missedSignals);
    }

    /// <summary>
    /// What makes two hits the same passage.
    ///
    /// Deliberately the text itself rather than its hash code: <see cref="string.GetHashCode()"/>
    /// is seeded per process, so a collision would drop a genuinely distinct passage — and a
    /// different one on the next launch. The evidence pack would then differ between sessions
    /// before the assessor had been called at all, which is the hardest kind of inconsistency
    /// to notice, because everything visible about the run is identical.
    /// </summary>
    /// <remarks>
    /// Length-prefixed rather than separated by a delimiter, so no choice of separator can
    /// appear in a document name and make two different hits look like one.
    /// </remarks>
    internal static string DeduplicationKey(CaseDocumentSearchMatch hit) =>
        $"{hit.DocumentName.Length}:{hit.DocumentName}{hit.SearchedText}";

    /// <summary>
    /// Slots held for each category a group says its evidence lives in, before the rest of the
    /// pack is filled by score.
    ///
    /// One, not two. A group targeting five categories would spend ten of its twelve slots on a
    /// floor of two, which stops being a floor and becomes the whole pack. One slot per category
    /// guarantees a hearing without displacing the ranking, and whether two is better is a
    /// question for a measured run rather than for this comment.
    /// </summary>
    public const int ReservedSlotsPerTargetedCategory = 1;

    /// <summary>
    /// Orders a group's passages and keeps the best <see cref="MaxPassagesPerGroup"/>, holding a
    /// slot for each category the group declared its evidence lives in.
    ///
    /// **The floor exists because the obvious ordering silently lost whole documents.** Ranking
    /// by "is this category targeted" and then by score looks like it favours the targeted ones,
    /// and it does — until every candidate is targeted, which is the normal case once a group
    /// names four or five categories. The first key then returns the same value for everything,
    /// the ordering collapses to pure score, and <c>Take</c> keeps whichever documents happen to
    /// embed closest to the query text.
    ///
    /// Measured, that meant the Fact Find. Its prose scores below research and report prose for
    /// almost any query, so passages retrieved *specifically because a plan asked for them* were
    /// evicted before the assessor saw them: three checks reached it in zero groups out of
    /// nineteen while every one of their packs sat exactly at the cap. Six of eight missed
    /// benchmark findings were facts recorded only there. Fixing the plans to ask for the
    /// category was necessary and did nothing on its own, because asking is not the same as
    /// keeping.
    ///
    /// Within that, the original ordering still decides everything: targeted first, then score,
    /// then the passage itself. That last key is what makes the cut reproducible — scores collide
    /// often enough to matter, and a slice through a tie band otherwise depends on the order the
    /// vector store happened to return, which an approximate index is under no obligation to keep
    /// stable.
    /// </summary>
    internal static List<CaseDocumentSearchMatch> Rank(
        IEnumerable<CaseDocumentSearchMatch> passages,
        IReadOnlySet<string> targeted,
        IReadOnlyList<string>? sections = null)
    {
        var hints = (sections ?? [])
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();

        var ordered = passages
            .OrderByDescending(p => targeted.Count == 0 || targeted.Contains(p.CategoryCode) ? 1 : 0)

            // An unfilled form sinks below anything that carries a value. Once the floor
            // guaranteed each declared category a slot, the passage it admitted turned out to be
            // a blank form grid in every case measured — short, generic, embeds near any query,
            // asserts nothing, and beat the section that would have settled four findings.
            // Ranked here rather than filtered at indexing, because an unfilled section is
            // sometimes the finding itself.
            .ThenBy(p => ContentDensity.IsFormSkeleton(p.SearchedText) ? 1 : 0)

            // Then the part of the document the plan actually named. A category is one document
            // and a document is a dozen unrelated sections, so without this the floor's slot goes
            // to whichever section embeds best for the group's wording — which delivered the
            // client's disposable income to four groups with no use for it and to none of the
            // groups that needed it.
            .ThenByDescending(p => Mentions(p.SearchedText, hints) ? 1 : 0)
            .ThenByDescending(p => p.Score)
            .ThenBy(p => p.DocumentName, StringComparer.Ordinal)
            .ThenBy(p => p.SearchedText, StringComparer.Ordinal)
            .ToList();

        if (targeted.Count == 0 || ordered.Count <= MaxPassagesPerGroup)
        {
            return ordered.Take(MaxPassagesPerGroup).ToList();
        }

        var keep = new HashSet<int>();

        // The floor, in ordinal category order so a pack does not depend on the order the plan
        // happened to list its categories in.
        foreach (var category in targeted.OrderBy(c => c, StringComparer.Ordinal))
        {
            var held = 0;

            for (var i = 0; i < ordered.Count && held < ReservedSlotsPerTargetedCategory; i++)
            {
                if (keep.Count >= MaxPassagesPerGroup)
                {
                    break;
                }

                if (!keep.Contains(i)
                    && string.Equals(ordered[i].CategoryCode, category, StringComparison.OrdinalIgnoreCase))
                {
                    keep.Add(i);
                    held++;
                }
            }
        }

        // Then the best of the rest, which is the whole pack whenever nothing was displaced.
        for (var i = 0; i < ordered.Count && keep.Count < MaxPassagesPerGroup; i++)
        {
            keep.Add(i);
        }

        // Emitted in rank order rather than in the order they were chosen: a reader should meet
        // the best evidence first, whether or not it got in on the floor.
        return ordered.Where((_, i) => keep.Contains(i)).ToList();
    }

    /// <summary>
    /// Whether a passage looks like the section a plan named. Converted documents carry their
    /// headings inline, so the hint is matched against the passage text — a plain substring,
    /// because the hint is a phrase from the document rather than a query to be interpreted.
    /// </summary>
    private static bool Mentions(string text, IReadOnlyList<string> hints) =>
        hints.Count > 0
        && hints.Any(h => text.Contains(h, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Runs one planned query, telling the store which categories the plan expects the answer
    /// in. Those are no longer only a post-filter over whatever the search happened to return:
    /// a passage that never entered the candidate set could not be re-ranked into it.
    /// </summary>
    private async Task<IReadOnlyList<CaseDocumentSearchMatch>> SearchAsync(
        PlannedQuery query, CancellationToken cancellationToken) =>
        await _search
            .SearchAsync(query.Text, query.TargetCategories, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Whether any of a query's expected signals turned up. Where none do across every hit,
    /// the data point is genuinely absent from the case file rather than merely unretrieved —
    /// which for most checks is the finding, so it is reported to the assessor explicitly.
    /// </summary>
    private static bool SignalPresent(PlannedQuery query, IReadOnlyList<CaseDocumentSearchMatch> hits) =>
        hits.Any(hit => query.ExpectSignals.Any(signal =>
            hit.SearchedText.Contains(signal, StringComparison.OrdinalIgnoreCase)));

    // ──────────────────────────────────────────────
    // Prompting
    // ──────────────────────────────────────────────

    private static string BuildSystemPrompt() =>
        """
        You are a financial services Quality Assurance assessor. You assess ONE requirement
        against a pre-assembled evidence pack and return a structured finding.

        The pack has two sides, and the distinction matters:

        - CANONICAL MODEL — what the suitability report asserts. It was extracted from the
          report itself, so treat it as an accurate record of what the report says. It is not
          evidence that the assertion is true.
        - RETRIEVED PASSAGES — what the rest of the case file holds, quoted verbatim from the
          supporting documents, each with an id like [P3] and a category. This is the evidence.

        A consistency requirement is met when the report's assertion is corroborated by the
        evidence. It fails when they contradict each other, or when the report asserts
        something no document supports.

        Answer the fields in the order they are given. That order is the order to think in:
        set out what each side says, list every discrepancy you can see, establish whether the
        comparison can be made at all, reason about it, cite — and decide last. Do not decide
        first and explain afterwards.

        Rules:
        - Judge only on the pack. Do not use outside knowledge of the case, and do not assume
          a document exists because it usually would.
        - Put EVERY difference between the two sides in "discrepancies", before you consider
          whether any of them matters. A difference explained by a guard still goes in the list;
          say in "analysis" which guard explains it.
        - If a value the comparison depends on is not in the pack, set "comparisonPerformed" to
          false and name what is missing. Do NOT estimate, derive around, or assume it. A
          comparison you could not make is a legitimate answer; an invented one is not.
        - Quote only text that appears in the passages given, verbatim, and name the passage id
          it came from. Quotations are checked against the pack automatically. Do not adjust a
          quotation to fit your reasoning: if the evidence contradicts the report, that is the
          finding.
        - Where the evidence is a TABLE and what you rely on is a row rather than a sentence,
          do not rewrite the row as prose. Leave "quote" empty, name the passage id, and list
          the values you read in "cells" — for example
          ["Savings", "JS", "Cash Account", "6,000"]. A table restated as a sentence is not a
          quotation and will be rejected, however accurately you read it. Every value you list
          is checked against that passage, so list what is there and nothing more.
        - Respect the false-positive guards. They describe specific ways this comparison
          produces spurious mismatches, and a finding one of them explains is not a finding.
        - Do not soften, hedge or omit a contradiction to make the finding read more favourably.
          Where a genuine mismatch stands after the guards, the outcome is Potential Concern.
        - Return one JSON object and nothing else. No prose outside it, no markdown fences.
        """;

    /// <summary>
    /// The part of the prompt that is the same for every group of a check — the check itself,
    /// the trigger, and how to decide.
    ///
    /// Built once and placed identically at the front of each group's prompt, so the provider's
    /// prefix cache covers it. Assessing a nine-group check therefore costs nine short prompts
    /// rather than nine long ones.
    /// </summary>
    private string BuildCheckHeader(
        AssessmentCheck check,
        CheckQueryPlan plan,
        TriggerOutcome trigger)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Check {plan.CheckId} — {plan.CheckName}");
        sb.AppendLine();
        sb.AppendLine($"Case: {_model.CaseReference}    Tenant: {_model.TenantId}");

        if (!string.IsNullOrWhiteSpace(check.RegulatoryBasis))
        {
            sb.AppendLine($"Regulatory basis: {check.RegulatoryBasis}");
        }

        sb.AppendLine();
        sb.AppendLine("## The check");
        sb.AppendLine(check.Prompt);
        sb.AppendLine();
        sb.AppendLine("### What to look for");
        sb.AppendLine(check.WhatToLookFor);
        sb.AppendLine();
        sb.AppendLine("### Decision logic");
        sb.AppendLine(check.DecisionLogic);
        sb.AppendLine();

        sb.AppendLine("## Trigger");
        sb.AppendLine(trigger.Detail);
        sb.AppendLine(trigger.Applies
            ? "The check applies to this case."
            : plan.TriggerProbe?.ContinuesWithReducedScope == true
                ? "The trigger was not found, and this check runs anyway — it applies to every "
                  + "case. Do not read the missing trigger as evidence that the check's subject "
                  + "is absent: whether it was ever considered is part of what you are assessing. "
                  + "Assess the requirements below on the evidence supplied."
                : "The trigger appears absent; say so if the evidence below bears that out.");
        sb.AppendLine();

        if (plan.Decision is not null)
        {
            sb.AppendLine("## How to decide");
            Append(sb, "No Issue", plan.Decision.NoIssue);
            Append(sb, "Potential Concern", plan.Decision.PotentialConcern);
            Append(sb, "N/A", plan.Decision.NotApplicable);

            if (plan.Decision.SeverityHints.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Severity guidance:");
                foreach (var hint in plan.Decision.SeverityHints)
                {
                    sb.AppendLine($"- {hint}");
                }
            }

            if (!string.IsNullOrWhiteSpace(plan.Decision.OverlayInstruction))
            {
                sb.AppendLine();
                sb.AppendLine($"Overlay: {plan.Decision.OverlayInstruction}");
            }

            sb.AppendLine();
        }

        // What the extraction itself reported about its own gaps. Carried so the assessor can
        // tell a report that is genuinely silent from one the extraction failed to read — the
        // two look identical from an absent canonical path, and they mean opposite things.
        var extraction = _accessor.Resolve("/extractionReport");

        if (extraction.Found)
        {
            sb.AppendLine("## What the extraction said about itself");
            sb.AppendLine(
                "Use this to tell report silence from extraction failure where a canonical path "
                + "below is absent.");
            sb.AppendLine("```json");

            // Sized to hold the whole report rather than a prefix of it. At 4,000 characters
            // the cut landed 45% of the way through an observed model, mid-key inside the
            // fourth recorded inconsistency — so every assessor read half a sentence and none
            // of them saw the ambiguities section at all.
            sb.AppendLine(Truncate(extraction.Json, ExtractionReportMaxChars));
            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Sums, percentage-of relationships and frequency conversions, done in code. Asking
        // the model for these produced both misses and inventions; see DerivedFigures.
        sb.Append(DerivedFigures.Format(_derived));

        return sb.ToString();
    }

    /// <summary>The one requirement being assessed, and everything bearing on it.</summary>
    private string BuildGroupPrompt(GroupEvidence pack)
    {
        var sb = new StringBuilder();
        var group = pack.Group;

        sb.AppendLine("## The requirement to assess");
        sb.AppendLine();
        sb.AppendLine($"### [{group.GroupId}] {group.Requirement}");
        sb.AppendLine($"Limb: {group.Limb}");

        // Assertion side, from the stored model.
        sb.AppendLine();
        sb.AppendLine("#### What the suitability report asserts (canonical model)");

        var found = pack.Fragments.Where(f => f.Found).ToList();
        var missing = pack.Fragments.Where(f => !f.Found).ToList();

        if (found.Count == 0)
        {
            sb.AppendLine("The canonical model holds no value at any of this group's paths.");
        }

        foreach (var fragment in found)
        {
            sb.AppendLine();
            sb.AppendLine($"`{fragment.Path}`:");
            sb.AppendLine("```json");
            sb.AppendLine(Truncate(fragment.Json, 6000));
            sb.AppendLine("```");
        }

        if (missing.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(
                "Absent from the extracted model: "
                + string.Join(", ", missing.Select(m => $"`{m.Path}`")));
            sb.AppendLine(
                "This means one of two things and they are not the same: the report is silent on "
                + "the point, or the extraction failed to capture it. The extraction's own report "
                + "above is the place to look. Where the comparison needs one of these values and "
                + "you cannot establish it, set comparisonPerformed to false and name it in "
                + "missingInputs rather than working around it.");
        }

        // Evidence side, from the vector store.
        sb.AppendLine();

        if (group.IsModelOnly)
        {
            sb.AppendLine("#### What the case file evidences");
            sb.AppendLine(
                "This requirement is assessed from the report alone — it compares the report against "
                + "itself, so there is no supporting document to corroborate it and none was searched "
                + "for. Do not treat the absence of passages here as an evidence gap.");
        }
        else if (pack.Passages.Count == 0)
        {
            sb.AppendLine($"#### What the case file evidences ({pack.Searches} search(es), 0 hit(s))");
            sb.AppendLine(
                "Nothing was retrieved. The searches ran and returned no relevant passage, so treat "
                + "this as the case file not evidencing the point.");
        }
        else
        {
            sb.AppendLine($"#### What the case file evidences ({pack.Searches} search(es), {pack.TotalPassages} hit(s))");
            sb.AppendLine($"Categories represented: {string.Join(", ", pack.CategoriesFound)}");
            sb.AppendLine("Cite by passage id. Quotes are checked against these passages.");

            for (var i = 0; i < pack.Passages.Count; i++)
            {
                var passage = pack.Passages[i];
                var category = string.IsNullOrWhiteSpace(passage.CategoryCode) ? "-" : passage.CategoryCode;
                sb.AppendLine();
                sb.AppendLine(
                    $"[{PassageId(i)}] [{category}] {passage.DocumentName} (score {passage.Score:0.000})");
                sb.AppendLine(Truncate(passage.SearchedText, 2400));
            }
        }

        if (pack.MissedSignals.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(
                $"Searches that returned nothing carrying their expected signals: {string.Join(", ", pack.MissedSignals)}. "
                + "Treat those data points as absent from the case file, not merely unretrieved.");
        }

        if (group.Comparison is { } comparison)
        {
            sb.AppendLine();
            sb.AppendLine("#### How to compare");

            if (!string.IsNullOrWhiteSpace(comparison.Method))
            {
                sb.AppendLine($"Method: {comparison.Method}");
            }

            if (!string.IsNullOrWhiteSpace(comparison.Tolerance))
            {
                sb.AppendLine($"Tolerance: {comparison.Tolerance}");
            }

            foreach (var guard in comparison.FalsePositiveGuards)
            {
                sb.AppendLine($"- Guard: {guard}");
            }
        }

        if (group.Sufficiency is { } sufficiency)
        {
            sb.AppendLine();
            sb.AppendLine("#### If evidence is missing");
            Append(sb, "Assertion absent", sufficiency.IfAssertionAbsent);
            Append(sb, "Evidence absent", sufficiency.IfEvidenceAbsent);
            Append(sb, "Both absent", sufficiency.IfBothAbsent);

            if (sufficiency.MinEvidenceCategories is { } min)
            {
                sb.AppendLine($"- Minimum corroborating categories: {min}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("#### Return");
        sb.AppendLine(
            $"One JSON object for [{group.GroupId}] only. Fill the fields in the order given: "
            + "reportSays, fileSays, discrepancies, comparisonPerformed, missingInputs, analysis, "
            + "citations, severity, outcome. Decide last.");

        return sb.ToString();
    }

    /// <summary>
    /// The id a passage is presented under. Stable within a group and derived from position, so
    /// the same pack always numbers the same way and a citation can be checked against it.
    /// </summary>
    internal static string PassageId(int index) => $"P{index + 1}";

    /// <summary>
    /// The text a group's citations are checked against: the passages it was shown, plus the
    /// canonical model fragments it was given, since a finding may legitimately quote the
    /// report's own assertion.
    /// </summary>
    /// <summary>
    /// The group's passages keyed by the id the assessor saw them under, so a citation that
    /// reads a table can be checked against the table it names rather than against the pack as
    /// a whole.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> PassagesById(GroupEvidence pack) =>
        pack.Passages
            .Select((passage, i) => (Id: PassageId(i), passage.SearchedText))
            .ToDictionary(p => p.Id, p => p.SearchedText, StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlyList<string> EvidenceTextOf(GroupEvidence pack) =>
    [
        .. pack.Passages.Select(p => p.SearchedText),
        .. pack.Fragments.Where(f => f.Found).Select(f => f.Json),
    ];

    /// <summary>
    /// Runs <paramref name="body"/> for each index, up to
    /// <see cref="AppSettings.MaxParallelRequests"/> at a time.
    ///
    /// Callers write results into an array by index rather than appending to a list, so
    /// concurrency changes how long the run takes and never what it produces — which matters
    /// here more than usual, since a good deal of work has gone into making two runs of the
    /// same check agree with each other.
    /// </summary>
    /// <summary>
    /// Fans out over a check's requirements without bounding them. The bound lives in the gates
    /// instead, so a check with three requirements does not hold three slots of a budget of six
    /// while another check waits its turn.
    /// </summary>
    private static Task ForEachAsync(
        int count, Func<int, CancellationToken, Task> body, CancellationToken cancellationToken) =>
        ForEachAsync(count, count, body, cancellationToken);

    internal static Task ForEachAsync(
        int count,
        int maxParallelism,
        Func<int, CancellationToken, Task> body,
        CancellationToken cancellationToken) =>
        Parallel.ForEachAsync(
            Enumerable.Range(0, count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, maxParallelism),
                CancellationToken = cancellationToken,
            },
            async (index, token) => await body(index, token).ConfigureAwait(false));

    private static TokenUsage AddUsage(TokenUsage a, TokenUsage b) => new(
        a.InputTokens + b.InputTokens,
        a.OutputTokens + b.OutputTokens,
        a.CacheWriteTokens + b.CacheWriteTokens,
        a.CacheReadTokens + b.CacheReadTokens);

    private static void Append(StringBuilder sb, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            sb.AppendLine($"- {label}: {value}");
        }
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + $"\n... [truncated, {text.Length - max:N0} more characters]";

    /// <summary>
    /// Reads one group's reply. A reply that will not parse becomes a Potential Concern carrying
    /// the raw text — never a pass, and never silently discarded.
    /// </summary>
    internal static GroupFinding ParseGroup(string response, PlanQueryGroup group)
    {
        var json = CanonicalModelExtractor.ParseObject(response);

        if (json is null)
        {
            return Unreadable(group, "The assessor did not return a parseable finding.", response);
        }

        try
        {
            var finding = json.Deserialize<GroupFinding>(FindingOptions)
                ?? throw new JsonException("The finding deserialised to null.");

            // The plan is the authority on which requirement this is, and on what it says. The
            // model only echoes both.
            //
            // The identifier was always taken from the plan. The requirement text was not, and
            // in one measured run 25 of 60 findings printed and stored the model's wording
            // rather than the catalogue's. Most were harmless expansions of a short label, but
            // the mechanism permits scope change and one group took it — asked about "Existing
            // products (pensions and investments)", it answered, and recorded, "Existing
            // products (pensions, investments and protection plans)". A requirement the
            // assessor can redefine is not auditable, so both fields now come from the plan and
            // the divergence is counted instead.
            return finding with
            {
                GroupId = group.GroupId,
                Requirement = group.Requirement,
                EchoedGroupId = finding.GroupId,
                EchoedRequirement = finding.Requirement,
            };
        }
        catch (JsonException ex)
        {
            return Unreadable(group, $"The finding could not be read ({ex.Message}).", response);
        }
    }

    /// <summary>Releases the gates, but only the ones this runner created for itself.</summary>
    public void Dispose()
    {
        if (!_ownsGates)
        {
            return;
        }

        _modelCalls.Dispose();
        _searches.Dispose();
    }

    private static GroupFinding Unreadable(PlanQueryGroup group, string why, string response) => new()
    {
        GroupId = group.GroupId,
        Requirement = group.Requirement,
        Analysis = $"{why} Raw response:\n\n{response.Trim()}",
        Outcome = nameof(CheckOutcome.PotentialConcern),
        ComparisonPerformed = false,
        MissingInputs = ["a readable answer from the assessor"],
    };
}
