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
public sealed class CheckPlanRunner : ICheckPlanRunner
{
    private static readonly JsonSerializerOptions FindingOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new FlexibleStringListConverter() },
    };

    /// <summary>
    /// The caps that decide what reaches an assessor all live on <see cref="AppSettings"/> now,
    /// and every one of them takes <c>0</c> for unbounded via <see cref="AppSettings.Unbounded"/>:
    /// <see cref="AppSettings.MaxPassagesPerGroup"/>,
    /// <see cref="AppSettings.ReservedSlotsPerTargetedCategory"/>,
    /// <see cref="AppSettings.ReservedSlotsPerDeclaredSection"/>,
    /// <see cref="AppSettings.ExtractionReportMaxChars"/> and
    /// <see cref="AppSettings.DecisionMaxTokens"/>.
    ///
    /// <b>They were compile-time constants, and that is why one of them was never measured.</b>
    /// The passage cap is the binding constraint on coverage — raising the search limit from 8
    /// to 16 bought two extra passages across a whole run because every group already sat at it —
    /// and no run on record has varied it, because varying it meant a rebuild.
    /// </summary>
    internal static readonly AppSettings Defaults = new();

    private readonly AppSettings _settings;
    private readonly IChatCompletionClient _chat;
    private readonly ICaseDocumentSearchService _search;
    private readonly CanonicalModelDocument _model;
    private readonly CanonicalModelAccessor _accessor;
    private readonly IReadOnlyList<DerivedFigures.Figure> _derived;

    /// <summary>
    /// Every populated leaf of the canonical model, walked once for the run. Empty when the
    /// assertion digest is switched off, so a run that does not want it pays nothing for it.
    /// </summary>
    private readonly IReadOnlyList<CanonicalLeaf> _leaves;

    private readonly PromptLogWriter? _promptLog;
    private readonly CheckRunRecorder? _recorder;
    private readonly PromptCacheBypass _bypass;
    private readonly ConcurrencyGate _modelCalls;
    private readonly ConcurrencyGate _searches;
    private readonly bool _ownsGates;

    /// <param name="modelCalls">
    /// The run-wide budget for assessment calls. Shared across every check so the total in
    /// flight is bounded once, rather than once per level of parallelism. One is created for
    /// this runner alone when none is given.
    /// </param>
    /// <param name="searches">The same, for retrieval.</param>
    /// <param name="recorder">
    /// Collects the run for the archive. Optional, and null in every test that only cares what
    /// a check concluded — recording is a side effect of running, never a condition of it.
    /// </param>
    public CheckPlanRunner(
        AppSettings settings,
        IChatCompletionClient chat,
        ICaseDocumentSearchService search,
        CanonicalModelDocument model,
        PromptLogWriter? promptLog = null,
        ConcurrencyGate? modelCalls = null,
        ConcurrencyGate? searches = null,
        CheckRunRecorder? recorder = null)
    {
        _settings = settings;
        _chat = chat;
        _search = search;
        _model = model;
        _accessor = new CanonicalModelAccessor(model.Json);

        // Computed once for the run rather than per check: the arithmetic is a property of the
        // model, and asking the assessor to redo it per group is how it came to be done
        // differently in different groups.
        _derived = DerivedFigures.From(model.Json);

        // Same reasoning, and the same cost profile: one walk of the model for the run rather
        // than one per group. Eighty-eight groups would otherwise re-walk an identical document
        // eighty-eight times to print the same lines.
        _leaves = (settings.AssertionDigest && settings.AssertionDigestMaxChars > 0)
                  || settings.MaxJoinedAssertions > 0
            ? CanonicalPaths.Enumerate(model.Json)
            : [];

        _promptLog = promptLog;
        _recorder = recorder;

        // One nonce for the whole run, not one per group. The check header is identical at the
        // front of every group's prompt precisely so the provider's prefix cache covers it, and
        // varying per call would throw that away to buy nothing: a run that is a cache miss as a
        // whole is already a freshly generated run.
        _bypass = PromptCacheBypass.For(settings.BypassResponseCache);

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
            // IgnoreTriggerProbe overrides that word when the point of the run is to audit it.
            if (!_settings.IgnoreTriggerProbe && trigger is { Applies: false, Settles: true })
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
                    .RunAsync(
                        t => GatherAsync(plan.QueryGroups[i], plan.CheckId, plan.Retrieval?.ResultsPerCall, t),
                        token)
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
            // Nullable by element on purpose: an unwritten slot is a state this array can be in,
            // and the type saying so is what forces the reconciliation below to exist.
            var findings = new GroupFinding?[packs.Length];
            var usages = new TokenUsage[packs.Length];
            var assessed = 0;

            await ForEachAsync(packs.Length, async (i, token) =>
            {
                var pack = packs[i];
                var userPrompt = _bypass.Apply(header + BuildGroupPrompt(pack));

                var result = await _modelCalls
                    .RunAsync(t => _chat.RunRawAsync(
                        systemPrompt,
                        userPrompt,
                        AppSettings.Unbounded(_settings.DecisionMaxTokens),
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
                var verified = CitationVerifier.Verify(
                    ParseGroup(result.Response, pack.Group),
                    EvidenceTextOf(pack),
                    PassagesById(pack)) with
                {
                    // From the pack, not from the answer. The assessor is told about the
                    // shortfall and asked to weigh it, but whether it is recorded is not left
                    // to whether it chose to mention it.
                    EvidenceShortfall = pack.EvidenceShortfall,
                };

                // The plan's adjudication directive, applied after verification and only ever
                // downwards. Severity was steered in prose and produced ten High "contradictions"
                // on one undisclosed basis; a ceiling code applies cannot be argued with.
                var (adjudicated, applied) = Adjudication.Apply(verified, pack.Group);

                findings[i] = applied.Count == 0
                    ? adjudicated
                    : adjudicated with
                    {
                        Analysis = adjudicated.Analysis
                            + "\n\nApplied by the plan's adjudication directive: "
                            + string.Join(" ", applied),
                    };

                // After verification, so the archive holds the finding as the app understood it —
                // unverified quotes and all — beside the raw text the model actually returned.
                _recorder?.RecordResponse(
                    plan.CheckId, pack.Group.GroupId,
                    systemPrompt, userPrompt, result.Response, findings[i]);

                progress?.Report(
                    $"{plan.CheckId}: assessed {Interlocked.Increment(ref assessed)}/{packs.Length}");
            }, cancellationToken).ConfigureAwait(false);

            // Every group the plan declares must produce a finding, and the run must say so
            // rather than assume it.
            //
            // Six groups across three runs of one case were assessed and then appeared nowhere in
            // the output — a full retrieval pack, a prompt, and no line anywhere. G8.3 was one of
            // them: "tax consequences of the recommendation", silently absent from a compliance
            // report. Non-deterministic and model-independent, and worse than a failure, because
            // everything downstream is computed over what survived: a check's outcome is derived
            // from its groups, the benchmark is scored against the output, and a requirement that
            // vanished scores as a requirement that found nothing. Every measurement taken over a
            // dropped group is quietly wrong in the same direction.
            //
            // The array is allocated by position and written inside a parallel body, so a slot
            // that is still null here is a group whose assessment did not complete and did not
            // throw. That must not be silently compacted away by the aggregation below — so it
            // becomes a loud, per-group Error carrying the group's identity into the report.
            var assessments = findings
                .Select((finding, i) => finding ?? NotAssessed(packs[i].Group))
                .ToList();

            var usage = usages.Aggregate(TokenUsage.Empty, AddUsage);

            startedAt.Stop();

            // An overlay that ran with a missing trigger has not been excused anything, so its
            // summary must not open by implying the check did not really apply.
            var triggerNote =
                trigger.NarrowedButRan
                    ? "This check ran despite an applicability rule saying it should not: the "
                      + "canonical model's own trigger field says the check applies. Read the "
                      + "trigger detail — either the rule omits a value its vocabulary documents, "
                      + "or the trigger is wrong."
                : trigger.Applies ? null
                : plan.TriggerProbe?.ContinuesWithReducedScope == true
                    ? "No trigger was recorded; this check applies to every case and was assessed anyway."
                    : "The trigger appears absent.";

            var finding = CheckFinding.FromGroups(
                plan.CheckId, plan.CheckName, assessments, triggerNote);

            return finding with
            {
                SearchesRun = trigger.Searches + packs.Sum(p => p.Searches),
                PassagesRetrieved = trigger.Passages + packs.Sum(p => p.TotalPassages),
                CanonicalPathsResolved = packs.Sum(p => p.Fragments.Count(f => f.Found)),
                CanonicalPathsMissing = packs.Sum(p => p.Fragments.Count(f => !f.Found)),
                UnmatchedSections = packs
                    .SelectMany(p => p.SectionsReached.MatchedNothing
                        .Select(s => $"{p.Group.GroupId}: {s}"))
                    .ToList(),
                EvictedSections = packs
                    .SelectMany(p => p.SectionsReached.Evicted
                        .Select(s => $"{p.Group.GroupId}: {s}"))
                    .ToList(),
                PassagesRetrievedForGroups = packs.Sum(p => p.SectionsReached.Retrieved),
                PassagesDeliveredToGroups = packs.Sum(p => p.SectionsReached.Delivered),
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

    /// <param name="NarrowedButRan">
    /// The trigger field said the check applies and an applicability rule disagreed. The check
    /// ran anyway — see the switch in <see cref="ProbeTriggerAsync"/> — and this carries that
    /// fact out so the finding can say so rather than reading like an ordinary assessment.
    /// </param>
    private sealed record TriggerOutcome(
        bool Applies,
        bool Settles,
        string Detail,
        int Searches,
        int Passages,
        bool NarrowedButRan = false);

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

                // A false trigger the model itself contradicts does not get to switch a check
                // off. CHK-005 was skipped twice on hasCapitalContributionsOrWithdrawals reading
                // false for a case transferring £110,185, and both runs printed it under CHECKS
                // CLEARED — the check that did not run looked exactly like one that passed.
                //
                // The check runs instead of being skipped, which is the safe direction: a check
                // that need not have run is visible in the output and costs one call, and one that
                // silently did not run costs a finding nobody can see missing.
                var contradiction = _accessor.TriggerContradictions.FirstOrDefault(
                    c => probe.TriggerField.EndsWith(c.Trigger, StringComparison.OrdinalIgnoreCase));

                if (fromModel is false && contradiction is not null)
                {
                    fromModel = null;
                    detail.Append(
                        $"IGNORED — the model contradicts it: {contradiction.Contradicts} says "
                        + $"{contradiction.Evidence}. The check was assessed rather than skipped. ");
                }
            }
            else
            {
                detail.Append($"Canonical model has no value for {probe.TriggerField}. ");
            }
        }

        // Applicability rules, ANDed — and see EvaluateApplicability for the one case where a
        // rule neither passes nor fails.
        var applicability = EvaluateApplicability(probe, _accessor);
        var fromApplicability = applicability.Verdict;

        detail.Append(applicability.Detail);

        var searches = 0;
        var passages = 0;

        foreach (var query in probe.Queries.Where(q => q.IsEvidenceSearch)
                                   .Where(q => !_settings.CoreQueriesOnly || q.IsCore))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hits = await SearchAsync(query, cancellationToken, plan.Retrieval?.ResultsPerCall)
                .ConfigureAwait(false);
            searches++;
            passages += hits.Count;
        }

        detail.Append($"{passages} corroborating passage(s) from {searches} probe search(es).");

        var verdict = Combine(fromModel, fromApplicability, passages);

        if (verdict.Narrowed)
        {
            detail.Append(
                $"OVERRIDDEN — {probe.TriggerField} affirmatively says the check applies, so the "
                + "failed rule narrowed it rather than settling it, and the check was assessed "
                + "rather than skipped. Either the rule omits a legitimate value of its "
                + "vocabulary, or the trigger is wrong; both are visible here and neither is "
                + "silent. ");
        }

        return new TriggerOutcome(
            verdict.Applies, probe.ReturnsNotApplicable, detail.ToString(),
            searches, passages, verdict.Narrowed);
    }

    /// <summary>
    /// The document category of the suitability report itself — the thing under audit, and the
    /// one category that cannot corroborate a claim the report makes.
    /// </summary>
    private const string ReportCategory = "I";

    /// <summary>
    /// The extraction's recorded contradictions that bear on one group, by path overlap.
    ///
    /// Matched on the leading segments of a canonical path rather than on equality, because the
    /// two are written at different granularities and always have been: an inconsistency names
    /// <c>/financialPosition/expenditure/monthlyEssential</c> while the group that needs it asks
    /// for <c>/financialPosition/expenditure</c>. Requiring them to agree exactly would route
    /// nothing, which is indistinguishable from the behaviour this replaces.
    ///
    /// The extraction's own <c>relatedCheckIds</c> is honoured as well, since it is the
    /// extractor saying where it thought the contradiction mattered — but it is a steer and not
    /// the only route in, for the same reason a categorisation steer is not a menu.
    /// </summary>
    internal static IReadOnlyList<CanonicalModelAccessor.InternalInconsistency>
        RelevantInconsistenciesFor(CanonicalModelAccessor accessor, PlanQueryGroup group)
    {
        var paths = group.AllCanonicalPaths
            .Select(Normalise)
            .Where(p => p.Length > 1)
            .ToList();

        if (paths.Count == 0)
        {
            return [];
        }

        var relevant = accessor.InternalInconsistencies
            .Where(inconsistency => inconsistency.Paths
                .Select(Normalise)
                .Any(recorded => paths.Any(asked =>
                    recorded.StartsWith(asked, StringComparison.OrdinalIgnoreCase)
                    || asked.StartsWith(recorded, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        // The plan's `emitOnePerPath` directive, applied to the routing this method performs.
        //
        // Sixteen missing allocation rows are one observation, not sixteen — and this mechanism
        // was measured broadcasting a single item into 26 of 88 groups, 92 times over. Where the
        // plan asks for one per path, contradictions recorded against the same leading path are
        // collapsed to the first, which is the one the extraction wrote first.
        if (!group.Reconciliation.EmitOnePerPath)
        {
            return relevant;
        }

        return relevant
            .GroupBy(
                i => i.Paths.Select(Normalise).FirstOrDefault() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// A canonical path reduced to what two spellings of it have in common: array markers and
    /// element ids removed, so <c>/existingArrangements/EA1/riskLevel</c> and
    /// <c>/existingArrangements[]/riskLevel</c> are the same path.
    /// </summary>
    private static string Normalise(string path)
    {
        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Replace("[]", string.Empty, StringComparison.Ordinal))
            .Where(s => s.Length > 0)

            // An element identifier is addressing one member of a collection; the contradiction
            // and the group are about the collection.
            .Where(s => !s.All(c => char.IsUpper(c) || char.IsDigit(c)));

        return "/" + string.Join('/', segments);
    }

    /// <summary>Whether the check applies, and whether a rule was overruled to say so.</summary>
    internal sealed record TriggerVerdict(bool Applies, bool Narrowed);

    /// <summary>
    /// The two signals combined into one verdict.
    ///
    /// A rule can only ever narrow, never rescue a check whose trigger field says no. That
    /// direction is the point of having rules at all and is unchanged: they exist to keep a run
    /// from spending an embedding on a check the case plainly does not need, and a rule that
    /// could switch a check back on would be doing the opposite.
    ///
    /// <b>The other direction is the one that cost six findings, and it now warns instead of
    /// skipping.</b> Where the trigger field affirmatively says the check applies and a rule
    /// disagrees, the most likely explanation is that the rule enumerates a closed vocabulary and
    /// has omitted a legitimate member of it — which is indistinguishable here from a case the
    /// check genuinely does not cover. The two mistakes are not symmetrical. A check that need
    /// not have run is visible in the output and costs a handful of calls. One that silently did
    /// not run costs findings nobody can see missing, printed under a heading that reads like a
    /// pass.
    ///
    /// Observed, and not marginally: a plan's rule listed five members of an advice-action
    /// vocabulary the schema documents ten values for. Two runs on two different models both
    /// recorded the omitted sixth — because it was the correct value — and the check settled as
    /// not applicable before a single search ran, taking six material findings with it, three of
    /// them the most severe in the case.
    ///
    /// This is the same reasoning as the trigger-contradiction path in
    /// <see cref="ProbeTriggerAsync"/>, applied one signal later. Adding the missing value to the
    /// plan fixes that plan; this fixes the next omission, in a plan nobody has written yet, and
    /// <c>CheckPlanLint</c>'s L7 reports the exclusions before a run rather than after one.
    ///
    /// The probe searches are the fallback, used only where neither source spoke.
    /// </summary>
    internal static TriggerVerdict Combine(bool? fromModel, bool? fromApplicability, int probePassages)
    {
        var narrowed = fromModel is true && fromApplicability is false;

        var applies = (fromModel, fromApplicability) switch
        {
            (null, null) => probePassages > 0,
            (bool m, null) => m,
            (null, bool a) => a,
            (true, false) => true,
            (bool m, bool a) => m && a,
        };

        return new TriggerVerdict(applies, narrowed);
    }

    /// <summary>
    /// Every scalar in a resolved fragment, flattened.
    ///
    /// A fan-out path such as <c>/objectives[]/objectiveType</c> resolves to an array of the
    /// per-element values, a plain path to a single scalar, and an applicability rule should not
    /// have to care which it got.
    /// </summary>
    /// <summary>
    /// The verdict of a probe's applicability rules, and the sentence explaining it.
    ///
    /// <see cref="ApplicabilityOutcome.Verdict"/> is null where the probe declares no rules, so
    /// the caller can tell "the rules said the check applies" from "there were no rules" — the
    /// AND with the trigger field depends on the difference.
    /// </summary>
    internal sealed record ApplicabilityOutcome(
        bool? Verdict,
        string Detail,
        IReadOnlyList<string> Passed,
        IReadOnlyList<string> Failed,
        IReadOnlyList<string> Undetermined);

    /// <summary>
    /// Evaluates a probe's applicability rules against the stored model. Every rule must find one
    /// of its accepted values at one of its canonical paths, or the check does not apply.
    ///
    /// <b>A rule that could not be evaluated is not a rule that failed.</b> Where a path resolves
    /// to nothing *and* the extraction report says nobody read the section it lives in, those are
    /// opposite conclusions and the model carries nothing that distinguishes them — a failed pass
    /// writes no key, exactly like a value the report does not contain. Treating that as a failure
    /// turns an extraction defect into a clean-looking N/A, which is the most expensive error
    /// available: a missed concern leaves no trace in the output, while a spurious one is visible
    /// and can be discarded by a reviewer.
    ///
    /// Observed, and not marginally. CHK-009's second rule reads
    /// <c>/existingArrangements[]/adviceAction</c>, three runs lost that section to a duplicate
    /// property name, and the check would have settled as not applicable — dropping five material
    /// findings, three of them the most severe in the case, while reporting no problem at all.
    ///
    /// Runs before any group is gathered, so a check that genuinely does not apply costs nothing:
    /// not one embedding, not one vector query. It reads the stored model rather than searching,
    /// because whether a case has a switch is a fact the extraction settled when it read the
    /// report in full, and re-deriving it from passage similarity would be guessing at something
    /// already known.
    /// </summary>
    internal static ApplicabilityOutcome EvaluateApplicability(
        PlanTriggerProbe probe, CanonicalModelAccessor accessor)
    {
        if (probe.Applicability.Count == 0)
        {
            return new ApplicabilityOutcome(null, string.Empty, [], [], []);
        }

        var failed = new List<string>();
        var passed = new List<string>();
        var undetermined = new List<string>();

        foreach (var rule in probe.Applicability)
        {
            var found = rule.CanonicalPaths
                .Select(accessor.Resolve)
                .Where(f => f.Found)
                .SelectMany(f => ScalarsOf(f.Json))
                .ToList();

            if (rule.IsSatisfiedBy(found))
            {
                passed.Add(rule.Name);
            }
            else if (found.Count == 0 && rule.CanonicalPaths.All(accessor.WasNeverRead))
            {
                undetermined.Add(
                    $"{rule.Name} (extraction never read {string.Join(", ", rule.CanonicalPaths)})");
            }
            else
            {
                failed.Add(found.Count == 0
                    ? $"{rule.Name} (nothing at {string.Join(", ", rule.CanonicalPaths)})"
                    : $"{rule.Name} (found {string.Join(", ", found.Distinct().Take(6))})");
            }
        }

        var detail = new StringBuilder(failed.Count == 0
            ? $"Applicability satisfied: {string.Join(", ", passed.DefaultIfEmpty("none"))}. "
            : $"Applicability not satisfied: {string.Join("; ", failed)}. ");

        if (undetermined.Count > 0)
        {
            detail.Append(
                $"Applicability undetermined for {undetermined.Count} rule(s), so the check runs "
                + $"rather than skipping: {string.Join("; ", undetermined)}. "
                + "Its findings rest on a canonical model that is missing a section. ");
        }

        return new ApplicabilityOutcome(
            failed.Count == 0, detail.ToString(), passed, failed, undetermined);
    }

    internal static IReadOnlyList<string> ScalarsOf(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var found = new List<string>();
            Walk(document.RootElement, found);
            return found;
        }
        catch (JsonException)
        {
            return [];
        }

        static void Walk(JsonElement element, List<string> into)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Walk(item, into);
                    }

                    break;

                case JsonValueKind.String:
                    if (element.GetString() is { Length: > 0 } s)
                    {
                        into.Add(s);
                    }

                    break;

                case JsonValueKind.True:
                    into.Add("true");
                    break;

                case JsonValueKind.False:
                    into.Add("false");
                    break;

                case JsonValueKind.Number:
                    into.Add(element.ToString());
                    break;
            }
        }
    }

    internal sealed record GroupEvidence(
        PlanQueryGroup Group,
        IReadOnlyList<CanonicalFragment> Fragments,
        IReadOnlyList<CaseDocumentSearchMatch> Passages,
        int Searches,
        int TotalPassages,
        IReadOnlyList<string> CategoriesFound,
        IReadOnlyList<string> MissedSignals,
        SectionReach SectionsReached)
    {
        /// <summary>
        /// How many corroborating categories the plan asked for, where it said.
        /// </summary>
        public int? RequiredCategories => Group.Sufficiency?.MinEvidenceCategories;

        /// <summary>
        /// The plan asked for corroboration from more categories than the pack reached.
        ///
        /// A model-only group is exempt: it has no evidence side by design, and reporting a
        /// shortfall against a requirement that deliberately searches for nothing would turn a
        /// sound finding into a spurious gap.
        /// </summary>
        public bool IsUnderEvidenced =>
            !Group.IsModelOnly
            && RequiredCategories is { } required
            && CategoriesFound.Count < required;

        /// <summary>The shortfall in words, or null where there is none.</summary>
        public string? EvidenceShortfall =>
            IsUnderEvidenced
                ? $"The plan requires corroboration from {RequiredCategories} document "
                  + $"categor{(RequiredCategories == 1 ? "y" : "ies")}; the evidence reached "
                  + $"{CategoriesFound.Count} "
                  + $"({(CategoriesFound.Count == 0 ? "none" : string.Join(", ", CategoriesFound))})."
                : null;
    }

    /// <summary>
    /// Assembles one group's evidence: the assertion side from the stored model, the evidence
    /// side from the vector store, de-duplicated and trimmed to what is worth reading.
    /// </summary>
    private async Task<GroupEvidence> GatherAsync(
        PlanQueryGroup group, string checkId, int? resultsPerCall, CancellationToken cancellationToken)
    {
        var fragments = _accessor.Resolve(group.AllCanonicalPaths);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var passages = new List<CaseDocumentSearchMatch>();
        var searches = 0;
        var total = 0;
        var missedSignals = new List<string>();

        // Which query found which passage, kept only for the archive. The pack itself is a flat
        // ranked list by design — several wordings converge on the same chunk and the assessor
        // has no use for knowing which one got there first — but a reader of the finding asks
        // exactly that, and once the lists are merged there is no way back to it.
        var attributed = new List<(PlannedQuery Query, CaseDocumentSearchMatch Hit)>();

        // Every query that ran, with its hit count — including the ones that returned nothing,
        // which is the only place that fact is kept.
        var executed = new List<(PlannedQuery Query, int Hits)>();

        foreach (var query in group.QueriesToRun(_settings.CoreQueriesOnly).Where(q => q.IsEvidenceSearch))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hits = await SearchAsync(query, cancellationToken, resultsPerCall).ConfigureAwait(false);
            searches++;
            total += hits.Count;
            executed.Add((query, hits.Count));

            // The same chunk comes back for several wordings of the same question; keep the
            // first and drop the rest so the pack is evidence rather than repetition.
            foreach (var hit in hits)
            {
                if (seen.Add(DeduplicationKey(hit)))
                {
                    passages.Add(hit);
                    attributed.Add((query, hit));
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

        // Overlapping chunk windows over one long table come back as several passages that are
        // almost the same text and have nothing in common as far as exact de-duplication is
        // concerned. Collapsed before ranking, so the cap is spent on distinct evidence.
        var distinct = CollapseNearDuplicates(passages, _settings.NearDuplicateOverlap);

        var ranked = Rank(
            distinct, targeted, group.DeclaredEvidenceSections, group.DeclaredEvidenceCategories,
            _settings, group.Retrieval.MaxPassages);

        var categories = ranked
            .Select(p => p.CategoryCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _recorder?.RecordEvidence(
            checkId, group.GroupId, group.Requirement, fragments, executed, attributed, ranked);

        return new GroupEvidence(
            group, fragments, ranked, searches, total, categories, missedSignals,
            UnmatchedSections(passages, ranked, group.DeclaredEvidenceSections));
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
    /// Drops passages that are near-copies of a better-scoring passage from the same document.
    ///
    /// <b><see cref="DeduplicationKey"/> catches a chunk returned verbatim by two wordings of the
    /// same question, and nothing else.</b> What it cannot catch is what the chunk windows do to
    /// a long table: consecutive windows overlap by design, so one table comes back as several
    /// passages that share most of their rows, differ in a few, and hash to as many distinct
    /// keys as there are windows. Every one of them is admitted, and they compete for the cap
    /// against passages the group has no second route to.
    ///
    /// Measured: eight passages at the same score, from one query, over one cash-flow table,
    /// holding a third of a twenty-four slot pack while the group's best-scoring passage was
    /// evicted. Both runs, identically — this is plan-and-code behaviour, not the model's.
    ///
    /// Same document only. Two documents saying the same thing is corroboration and the whole
    /// point of the evidence side; two windows over the same rows is one document, counted twice.
    ///
    /// The comparison is word-level Jaccard overlap, which is enough and is stable. The threshold
    /// is deliberately high: rows of one table repeat their column labels, so moderately similar
    /// passages are routinely two different facts, and dropping one of those would cost exactly
    /// the kind of finding this method exists to protect.
    /// </summary>
    /// <param name="passages">Candidates, already exact-de-duplicated, in retrieval order.</param>
    /// <param name="overlap">
    /// The fraction two passages must share before the weaker is dropped. <b>1.0 or more
    /// disables the pass</b> and returns the input unchanged.
    /// </param>
    internal static List<CaseDocumentSearchMatch> CollapseNearDuplicates(
        IReadOnlyList<CaseDocumentSearchMatch> passages, double overlap)
    {
        if (overlap >= 1.0 || passages.Count < 2)
        {
            return [.. passages];
        }

        // Strongest first, so the passage that survives a cluster is the best-scoring member of
        // it rather than whichever query happened to return first. Ties break on document then
        // text — the same keys Rank uses — so a cluster of equal scores collapses to the same
        // member on every run, whatever order the store returned them in.
        var strongestFirst = passages
            .Select((passage, position) => (passage, position))
            .OrderByDescending(p => p.passage.Score)
            .ThenBy(p => p.passage.DocumentName, StringComparer.Ordinal)
            .ThenBy(p => p.passage.SearchedText, StringComparer.Ordinal)
            .ToList();

        var kept = new List<(CaseDocumentSearchMatch Passage, int Position, HashSet<string> Words)>();

        foreach (var (passage, position) in strongestFirst)
        {
            var words = Words(passage.SearchedText);

            if (words.Count == 0)
            {
                kept.Add((passage, position, words));
                continue;
            }

            var duplicate = kept.Any(k =>
                string.Equals(k.Passage.DocumentName, passage.DocumentName, StringComparison.Ordinal)
                && Overlap(k.Words, words) >= overlap);

            if (!duplicate)
            {
                kept.Add((passage, position, words));
            }
        }

        // Returned in the order they arrived, not in the order they were compared: everything
        // downstream reads this as "the candidates", and re-ordering it here would silently
        // change the ranking's own tie-breaks.
        return kept
            .OrderBy(k => k.Position)
            .Select(k => k.Passage)
            .ToList();
    }

    /// <summary>
    /// The share of the smaller passage's vocabulary that the larger one also has.
    ///
    /// Deliberately not symmetric-difference Jaccard: a chunk window that wholly contains a
    /// shorter one is a duplicate of it in every sense that matters here, and Jaccard would score
    /// that pair by the length difference rather than by the containment.
    /// </summary>
    private static double Overlap(IReadOnlySet<string> a, IReadOnlySet<string> b)
    {
        var (smaller, larger) = a.Count <= b.Count ? (a, b) : (b, a);

        return smaller.Count == 0 ? 0 : (double)smaller.Count(larger.Contains) / smaller.Count;
    }

    /// <summary>
    /// A passage's distinct words, lowercased, punctuation and table pipes discarded. Table
    /// markup is the noise here — every row of a Markdown table carries the same separators, and
    /// counting them would make any two rows of any table look alike.
    /// </summary>
    private static HashSet<string> Words(string text)
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        var current = new StringBuilder();

        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(char.ToLowerInvariant(c));
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }

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
    /// Reserved slots are served to the group's <c>declares.evidenceCategories</c> before the
    /// rest of its targeted ones. Lint holds declared to a subset of targeted, so this cannot
    /// widen a request — it settles who keeps a slot once the cap bites, which is the first
    /// retrieval effect that declaration has ever had.
    ///
    /// Within that, the original ordering still decides everything: targeted first, then score,
    /// then the passage itself. That last key is what makes the cut reproducible — scores collide
    /// often enough to matter, and a slice through a tie band otherwise depends on the order the
    /// vector store happened to return, which an approximate index is under no obligation to keep
    /// stable.
    /// </summary>
    /// <param name="planned">
    /// The group's own pack budget, where its plan sets one. The plan may narrow the global cap
    /// but never widen it: a budget is a ceiling somebody configured, and a plan file should not
    /// be able to raise the bill of a run without the run's consent.
    /// </param>
    internal static List<CaseDocumentSearchMatch> Rank(
        IEnumerable<CaseDocumentSearchMatch> passages,
        IReadOnlySet<string> targeted,
        IReadOnlyList<string>? sections = null,
        IReadOnlySet<string>? declared = null,
        AppSettings? settings = null,
        int? planned = null)
    {
        settings ??= Defaults;

        var configured = settings.MaxPassagesPerGroup;

        var cap = AppSettings.Unbounded(
            planned is { } budget && budget > 0
                ? configured <= 0 ? budget : Math.Min(budget, configured)
                : configured);
        var perSection = settings.ReservedSlotsPerDeclaredSection;
        var perCategory = settings.ReservedSlotsPerTargetedCategory;

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

        if ((targeted.Count == 0 && hints.Count == 0) || ordered.Count <= cap)
        {
            return cap == int.MaxValue ? ordered : ordered.Take(cap).ToList();
        }

        var keep = new HashSet<int>();

        // The group's own best answer, before anything else is rationed.
        //
        // **Every other reservation in this method serves something the plan guessed in advance.**
        // A declared section is a phrase somebody expected the document to carry; a declared
        // category is where somebody expected the answer to live. Both are useful and both are
        // guesses. Score is not: it is the group's own query, answered by the store, and until
        // now it was the only signal with no slot of its own — filled from what the floors left
        // over, which on a full pack is nothing.
        //
        // Measured across two runs whose retrieval extracts differ by two lines: a passage at
        // 0.737 evicted while eight near-duplicates at 0.627, all from one query, were kept. The
        // floors were working exactly as designed and the best evidence in the group did not
        // reach the assessor.
        //
        // Restricted to targeted categories and to passages carrying content, for the same
        // reasons the floors are: a hint promotes within what was requested, and an unfilled form
        // grid embeds near any query while asserting nothing. A blank form does not become a
        // group's best answer by scoring well, and neither does a document nobody asked for.
        // Chosen by score directly rather than by position in `ordered`, which sorts a
        // hint-matching passage above a better-scoring one — correctly, for filling the pack, and
        // wrongly for this: the slot exists precisely to carry the passage the other keys would
        // demote. Ties break on document then text, the same last two keys `ordered` uses, so the
        // choice is reproducible across runs where scores collide.
        var topScore = Math.Max(0, settings.ReservedSlotsForTopScore);

        if (topScore > 0)
        {
            var best = Enumerable.Range(0, ordered.Count)
                .Where(i => targeted.Count == 0 || targeted.Contains(ordered[i].CategoryCode))
                .Where(i => !ContentDensity.IsFormSkeleton(ordered[i].SearchedText))
                .OrderByDescending(i => ordered[i].Score)
                .ThenBy(i => ordered[i].DocumentName, StringComparer.Ordinal)
                .ThenBy(i => ordered[i].SearchedText, StringComparer.Ordinal)
                .Take(topScore);

            foreach (var i in best.TakeWhile(_ => keep.Count < cap))
            {
                keep.Add(i);
            }
        }

        // Sections next. A plan naming "Current Monthly Cash Flow" has asked for something more
        // specific than one naming category B, and the section slot usually satisfies the
        // category slot as a side effect. Ordinal order for the same reason as the categories
        // below: the pack must not depend on the order the plan happened to list them in.
        foreach (var hint in hints.OrderBy(h => h, StringComparer.Ordinal))
        {
            var held = 0;

            for (var i = 0; i < ordered.Count && held < perSection; i++)
            {
                if (keep.Count >= cap)
                {
                    break;
                }

                // Targeted categories only. A hint is matched as a substring of the passage
                // text, so an unrelated document using the same words — "Total Monthly
                // Disposable Income appears here too" — would otherwise claim the slot and
                // admit a category the group never asked for. A hint promotes within what was
                // requested; it does not widen the request.
                if (!keep.Contains(i)
                    && (targeted.Count == 0 || targeted.Contains(ordered[i].CategoryCode))
                    && ordered[i].SearchedText.Contains(hint, StringComparison.OrdinalIgnoreCase))
                {
                    keep.Add(i);
                    held++;
                }
            }
        }

        // The floor, declared categories first.
        //
        // `targeted` is where the queries look; `declared` is where the group says the answer
        // lives. Lint keeps declared a subset of targeted, so this never widens the request —
        // it decides who gets a reserved slot when the cap bites. A group naming four target
        // categories across its queries but declaring one is saying which of the four actually
        // answers the requirement, and until now that statement did nothing at all: the slots
        // went out in alphabetical order and a declared category could be squeezed out by a
        // category some query happened to mention.
        //
        // Within each tier, ordinal order, so a pack does not depend on the order the plan
        // happened to list its categories in.
        var floorOrder = (declared ?? (IReadOnlySet<string>)new HashSet<string>())
            .Where(targeted.Contains)
            .OrderBy(c => c, StringComparer.Ordinal)
            .Concat(targeted
                .Where(c => declared is null || !declared.Contains(c))
                .OrderBy(c => c, StringComparer.Ordinal))
            .ToList();

        foreach (var category in floorOrder)
        {
            var held = 0;

            for (var i = 0; i < ordered.Count && held < perCategory; i++)
            {
                if (keep.Count >= cap)
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
        for (var i = 0; i < ordered.Count && keep.Count < cap; i++)
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
    /// Section hints that matched none of the passages the searches returned.
    ///
    /// **A hint that matches nothing does nothing, silently, and that cost a run.** One commit
    /// added six section hints. Three named parts of the Fact Find's cash-flow table and moved
    /// four benchmark findings from missed to caught. The other three named <c>Residency</c> and
    /// <c>Contact Address</c>, matched no passage anywhere, and moved nothing — and the run gave
    /// no signal distinguishing "the hint worked and the group ignored the section" from "the
    /// hint never matched". The first is a reasoning problem and the second is a typo, and they
    /// were indistinguishable for a whole run.
    ///
    /// Measured over the candidates rather than the ranked pack, so this reports a hint that
    /// matched nothing at all — not one that matched and then lost its slot, which is a
    /// different problem and needs a different answer.
    /// </summary>
    /// <summary>
    /// Declared sections that did not reach the assessor, and which of the two reasons applies.
    ///
    /// <b>This was measured against the wrong list, and the wrong answer was believed twice.</b>
    /// It compared hints to <c>passages</c> — the de-duplicated candidates, before ranking and
    /// before the cap — so a hint whose passage was retrieved and then evicted was reported as
    /// having matched. On the run that prompted this, the report said the fact find's
    /// "Residency Status" hint now matched; the row reached no assessor at all, and two analyses
    /// concluded from that signal that a retrieval defect had been fixed and the remaining
    /// failure was one of reasoning. It was not.
    ///
    /// The two cases need different answers and used to look identical:
    ///
    /// <list type="bullet">
    /// <item><b>Matched nothing</b> — no retrieved passage contains the string at all. The hint
    /// is wrong, or the wording does not appear in the converted document. Fix the hint.</item>
    /// <item><b>Evicted</b> — a passage carrying it was retrieved and did not survive into the
    /// pack. The hint is right and the reservation did not hold it, which is a ranking or cap
    /// problem and no amount of rewording will help.</item>
    /// </list>
    /// </summary>
    /// <param name="Retrieved">Candidates a search returned, before ranking.</param>
    /// <param name="Delivered">Passages that survived into the pack the assessor read.</param>
    internal sealed record SectionReach(
        IReadOnlyList<string> MatchedNothing,
        IReadOnlyList<string> Evicted,
        int Retrieved = 0,
        int Delivered = 0)
    {
        /// <summary>Every hint that failed, however it failed — for callers that only count.</summary>
        public IReadOnlyList<string> All => [.. MatchedNothing, .. Evicted];

        /// <summary>
        /// Candidates the ranking dropped. Counted for every group, hints or none.
        ///
        /// <b>Both diagnostics above are unreachable for a group that declares no hints</b>, because
        /// each iterates the hints it declared — and that is how F3.1's loss was silent. The file
        /// note carrying "a Risk rating of 6" reached eight group prompts and no CHK-003 group,
        /// CHK-003 declared no sections at all, and the run reported nothing while a finding held
        /// for five runs disappeared. This is the floor under that: it cannot say which passage was
        /// dropped, but it can say that most of what was found never arrived.
        /// </summary>
        public int Discarded => Math.Max(0, Retrieved - Delivered);

        public static readonly SectionReach None = new([], []);
    }

    internal static SectionReach UnmatchedSections(
        IEnumerable<CaseDocumentSearchMatch> candidates,
        IEnumerable<CaseDocumentSearchMatch> pack,
        IReadOnlyList<string> sections)
    {
        var retrieved = candidates.Select(p => p.SearchedText).ToList();
        var delivered = pack.Select(p => p.SearchedText).ToList();

        var hints = sections.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();

        if (hints.Count == 0)
        {
            return new SectionReach([], [], retrieved.Count, delivered.Count);
        }

        var nothing = new List<string>();
        var evicted = new List<string>();

        foreach (var hint in hints)
        {
            if (delivered.Any(t => t.Contains(hint, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            (retrieved.Any(t => t.Contains(hint, StringComparison.OrdinalIgnoreCase))
                ? evicted
                : nothing).Add(hint);
        }

        return new SectionReach(nothing, evicted, retrieved.Count, delivered.Count);
    }

    /// <summary>
    /// Runs one planned query, telling the store which categories the plan expects the answer
    /// in. Those are no longer only a post-filter over whatever the search happened to return:
    /// a passage that never entered the candidate set could not be re-ranked into it.
    /// </summary>
    private async Task<IReadOnlyList<CaseDocumentSearchMatch>> SearchAsync(
        PlannedQuery query, CancellationToken cancellationToken, int? resultsPerCall) =>
        await _search
            .SearchAsync(query.Text, query.TargetCategories, cancellationToken, resultsPerCall)
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

    private static string BuildSystemPrompt() => Prompts.AssessorSystem;

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
        var extraction = _accessor.Resolve(CanonicalModel.Paths.ExtractionReport);

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
            sb.AppendLine(Truncate(
                extraction.Json, AppSettings.Unbounded(_settings.ExtractionReportMaxChars)));
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
                + "missingInputs rather than working around it. Neither case is something the "
                + "report did wrong: an extraction that failed to read a value is this pipeline's "
                + "defect, not the adviser's, and reporting it as a shortcoming of the report "
                + "spends a finding a reviewer has to read and then discard.");
        }

        // The rest of the assertion side, one line per path.
        //
        // Printed here, with the assertion side and above the evidence, deliberately. It is the
        // same kind of thing as the fragments above — the report's own claims, from the stored
        // model, needing no passage to establish — and putting it below the passages would invite
        // the assessor to read it as more evidence, which is exactly the confusion category [I]
        // passages already cause.
        //
        // Group-scoped rather than run-scoped in what it excludes: every group is shown the same
        // model minus its own paths, because those are printed in full a few lines earlier.
        if (_leaves.Count > 0
            && AssertionDigest.Render(
                _leaves, group.AllCanonicalPaths, _settings.AssertionDigestMaxChars) is { } digest)
        {
            sb.AppendLine();
            sb.Append(digest);
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

            // Printed where the category codes are, because that is where the mistake is made.
            //
            // The standing rules already say the report cannot corroborate itself. It is stated
            // in the abstract, three thousand characters earlier, and the assessor is here — with
            // a list of passages each carrying a category letter — when it decides whether a
            // claim is supported. Both models have written "the report confirms" of a passage
            // whose category is the report, and the weaker one did it on the same requirement in
            // two separate runs.
            //
            // The category of every passage the group was given is known, so this says which of
            // them are the document under audit rather than leaving it to be worked out.
            if (pack.Passages.Any(p => p.CategoryCode.Equals(ReportCategory, StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine(
                    $"Passages marked [{ReportCategory}] are the report under audit. They are the "
                    + "assertion side, not the evidence side: a claim supported only by passages "
                    + "marked that way has no support in the case file, whatever those passages "
                    + "say. Two of them agreeing is internal consistency; two disagreeing is a "
                    + "finding.");
            }

            for (var i = 0; i < pack.Passages.Count; i++)
            {
                var passage = pack.Passages[i];
                var category = string.IsNullOrWhiteSpace(passage.CategoryCode) ? "-" : passage.CategoryCode;
                sb.AppendLine();
                sb.AppendLine(
                    $"[{PassageId(i)}] [{category}] {passage.DocumentName} (score {passage.Score:0.000})");
                sb.AppendLine(Window(
                    passage.SearchedText, PassageRenderCharacters,
                    pack.Group.DeclaredEvidenceSections));
            }
        }

        if (pack.MissedSignals.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(
                $"Searches that returned nothing carrying their expected signals: {string.Join(", ", pack.MissedSignals)}. "
                + "Treat those data points as absent from the case file, not merely unretrieved.");
        }

        // The code-level join: assertions from elsewhere in the report that share an exact figure
        // with a passage this group was actually given.
        //
        // Printed after the passages because it names them by id — the assessor has just read
        // [P1]..[Pn] and can turn back to the one cited. This is the half of the routing problem
        // that is decidable in code: the plan decides what to retrieve, and this decides nothing,
        // it only introduces two facts that the plan happened to keep apart.
        //
        // Gated on the plan's `detect` directive. A requirement whose answer is narrative has no
        // value to diverge, and joining figures into it is the same mistake as comparing prose as
        // values: it manufactures findings. 18 of the 88 groups say `None`, and they mean it.
        if (_leaves.Count > 0 && !group.Reconciliation.DetectsNothing)
        {
            var joins = EvidenceJoin.For(
                _leaves, group.AllCanonicalPaths, PassagesById(pack), _settings.MaxJoinedAssertions,
                group.Reconciliation.CurrentOnly);

            if (EvidenceJoin.Format(joins) is { } joined)
            {
                sb.AppendLine();
                sb.Append(joined);
            }
        }

        // Contradictions the extraction found in the report, filtered to the ones this group's
        // own paths touch.
        //
        // The whole extraction report is printed once in the check header, ahead of every group.
        // That is the right place for a summary and the wrong place for a fact one requirement
        // turns on: a contradiction between two expenditure figures reaches eleven requirements,
        // ten of which have no use for it. Measured, the group that used the recorded
        // inconsistencies was not the group whose requirement rested on them.
        //
        // Printed here, beside this group's own evidence, and only where the paths overlap.
        var relevant = RelevantInconsistenciesFor(_accessor, group);

        if (relevant.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("#### Contradictions the extraction found in the report, on your paths");
            sb.AppendLine(
                "These were recorded when the report was read in full. They are the report "
                + "disagreeing with itself, not a document disagreeing with the report, so no "
                + "passage is needed to establish them and no guard about differing sources "
                + "explains them.");

            foreach (var inconsistency in relevant)
            {
                sb.AppendLine();
                sb.AppendLine($"- {inconsistency.Description}");

                if (inconsistency.Values.Count > 0)
                {
                    sb.AppendLine("  Values recorded: " + string.Join(" · ", inconsistency.Values));
                }
            }
        }

        // The numeric half of a ValueMatch or RangeMatch, settled in code before the assessor
        // is asked anything. Placed above "How to compare" so the guards read as qualifications
        // on an established result rather than instructions for work still to be done.
        // Also gated on `detect`: the plan's comparison *method* says how to compare, and the
        // directive says whether code should compare at all. A group set to None gets no computed
        // figures, however its method reads.
        var figures = group.Reconciliation.DetectsNothing
            ? null
            : NumericComparison.Format(
                group.Comparison?.Method,
                pack.Fragments.Where(f => f.Found).Select(f => f.Json),
                PassagesById(pack));

        if (figures is not null)
        {
            sb.AppendLine();
            sb.Append(figures);
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

            // Two rules about the shape of a comparison rather than about this one, printed here
            // rather than in the standing rules for two reasons: they only apply to a group that
            // has a comparison to make, and the standing prompt is at its length budget — a rule
            // added there is read on every group whether or not it can bite.
            //
            // Both were bought by the same observed failure, from opposite ends. Two runs, on two
            // models at a 3.3x cost difference, met a pair of tables stating one quantity on two
            // bases. Neither asked why the two columns of the like-for-like table were identical
            // — the answer was an ongoing charge applied to a side that does not pay it, stated
            // in three separate retrieved documents. And the same confusion produced the largest
            // false positive in either run: ten "contradictions" at High severity that are one
            // undisclosed basis, tabulated ten times. One reading, two failures, one on each side
            // of the ledger.
            sb.AppendLine(
                "- Establish what each figure is ON before calling two of them contradictory. Two "
                + "values on different bases — one including a charge, a tax or a fee the other "
                + "excludes, one at a different date, age or term — are not two answers to one "
                + "question. Name both bases in \"analysis\". A basis the report never states is "
                + "one finding about disclosure, not one finding per row of the table.");
            sb.AppendLine(
                "- And the reverse: where a comparison between an existing option and a "
                + "recommended one shows no difference, ask what assumption produced that. A "
                + "charge, a rate or a term applied to one side and not the other makes two "
                + "unlike things look alike. Say which assumptions were applied to each side, or "
                + "name what you could not establish in \"missingInputs\".");
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

        // Computed here rather than left to the model to spot: both numbers are known, and
        // whether a thinly evidenced requirement says so should not depend on the assessor
        // comparing two figures printed in different parts of a long prompt.
        if (pack.EvidenceShortfall is { } shortfall)
        {
            sb.AppendLine();
            sb.AppendLine("#### Evidence shortfall");
            sb.AppendLine(shortfall);
            sb.AppendLine(
                "This is established, not a matter for you to re-derive. Say so in your finding "
                + "and apply the sufficiency rule above. It does not by itself decide the "
                + "outcome — thin evidence that still settles the question is not a concern, "
                + "and a requirement the file genuinely cannot answer may be one.");
        }

        // The steer is printed with the requirement's own verification rules rather than with
        // the schema, because that is what it is: a statement about this requirement's known
        // failure mode, not a change to the vocabulary. The assessor is told plainly that it may
        // answer outside it — a plan that could forbid a category could forbid a finding.
        if (group.SteeredIssueCategories.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("#### Kinds of problem this requirement usually raises");
            sb.AppendLine(string.Join(", ", group.SteeredIssueCategories));
            sb.AppendLine(
                "This is where past findings on this requirement have landed, not a list to "
                + "choose from. If what you found is a different kind of problem, categorise it "
                + "as what it is. If the requirement is met, leave issueCategories empty.");
        }

        sb.AppendLine();
        sb.AppendLine("#### Return");
        sb.AppendLine(
            $"One JSON object for [{group.GroupId}] only. Fill the fields in the order given: "
            + "reportSays, fileSays, discrepancies, comparisonPerformed, missingInputs, analysis, "
            + "citations, issueCategories, severity, outcome. Decide last.");

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
        ParallelWork.ForEachAsync(count, count, body, cancellationToken);

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

    /// <summary>
    /// How much of one passage is written into the prompt.
    ///
    /// Not a coverage cap in the sense the settings use — it bounds one passage, not how many —
    /// but it removes evidence exactly as effectively, and it did so 907 times in a single run
    /// before anyone counted.
    /// </summary>
    public const int PassageRenderCharacters = 2400;

    /// <summary>
    /// Renders a passage, keeping the part the plan asked for when the whole will not fit.
    ///
    /// <b>Hint matching and passage rendering disagreed about what "the passage" was, and the
    /// gap between them silently discarded the evidence a group had specifically reserved a slot
    /// for.</b> <see cref="UnmatchedSections"/> and <c>Mentions</c> both test
    /// <c>SearchedText</c> — the whole passage — while the prompt wrote its first 2,400
    /// characters. So a hint could match, win its reserved slot, put its passage in the pack, and
    /// have the row it named cut before any model saw it. Every diagnostic said the hint had
    /// worked. The assessor had a truncated table.
    ///
    /// That is how the fact find's "Residency Status | Tenant - private" reached no assessor in
    /// four consecutive runs while three separate fixes were aimed at it: a query rewrite, a
    /// section hint, and three guards. All three worked. None of them could survive the render.
    ///
    /// So when a passage must be cut, the window is centred on the first declared section the
    /// passage carries, rather than taken from the start. A plan naming "Current Monthly Cash
    /// Flow" has said which part of a long document it needs, and that statement now survives all
    /// the way to the prompt instead of stopping one step short of it. Where a passage carries no
    /// declared section — or none was declared — the leading window is kept, which is the old
    /// behaviour and the right default: the top of a chunk is its heading.
    /// </summary>
    internal static string Window(string text, int max, IReadOnlyList<string>? sections = null)
    {
        if (text.Length <= max)
        {
            return text;
        }

        var at = -1;

        foreach (var hint in sections ?? [])
        {
            if (string.IsNullOrWhiteSpace(hint))
            {
                continue;
            }

            var found = text.IndexOf(hint, StringComparison.OrdinalIgnoreCase);

            if (found >= 0 && (at < 0 || found < at))
            {
                at = found;
            }
        }

        // Nothing named, or the named part is already inside the leading window.
        if (at < 0 || at < max)
        {
            return Truncate(text, max);
        }

        // Centre on it, then pull back inside the text. A quarter of the window ahead of the
        // hint keeps whatever heading or label introduces the row; the rest follows it.
        var start = Math.Max(0, at - (max / 4));
        var length = Math.Min(max, text.Length - start);

        return TableContext(text, start)
             + $"... [{start:N0} earlier characters omitted]"
             + Environment.NewLine
             + text.Substring(start, length)
             + (start + length < text.Length
                 ? Environment.NewLine + $"... [truncated, {text.Length - start - length:N0} more characters]"
                 : string.Empty);
    }

    /// <summary>
    /// The caption and column headings of the table a window opens inside, or the empty string
    /// where it does not open inside one.
    ///
    /// <b>A window that starts mid-table delivers a grid of numbers with no statement of what
    /// they are on, and the assessor has been observed reading exactly that and getting it
    /// backwards.</b> The centred window above was the fix for a row being cut off the end; this
    /// is the fix for the row arriving without its heading. They are the same defect at two ends
    /// of the same passage.
    ///
    /// The shape it protects is general and recurs across the domain: the same quantity tabulated
    /// twice on two different bases — before and after a charge, gross and net, two ages, two
    /// terms. The numbers alone cannot distinguish them. The caption above the table is the only
    /// thing that can, and it is the first thing a window drops.
    ///
    /// Measured: two runs, on two models at a 3.3× cost difference, both failed on one such pair.
    /// The better of the two named the pre-charge table as the post-charge one explicitly, and
    /// the same confusion is the largest false positive in either run — ten "contradictions"
    /// reported at High severity that are one undisclosed basis, tabulated ten times.
    ///
    /// Only the header row and the separator are carried, plus the nearest non-table line above
    /// the table — its caption, whether that is a Markdown heading, a bold line, or a sentence.
    /// The rows between the header and the window are not: they are what the window was cut to
    /// leave out, and re-admitting them would defeat the cap.
    /// </summary>
    internal static string TableContext(string text, int start)
    {
        // Walk back to the start of the line the window opens on, then up while the lines still
        // look like table rows. A Markdown table row begins with a pipe once the converter has
        // normalised it; a line that does not is where the table began.
        var lineStart = text.LastIndexOf('\n', Math.Min(start, text.Length - 1)) + 1;

        if (!IsTableRow(LineAt(text, lineStart)))
        {
            return string.Empty;
        }

        var header = lineStart;

        while (header > 0)
        {
            var previous = text.LastIndexOf('\n', header - 2) + 1;

            if (previous >= header || !IsTableRow(LineAt(text, previous)))
            {
                break;
            }

            header = previous;
        }

        // The window already opens on the first row of the table, so it has its own heading.
        if (header == lineStart)
        {
            return string.Empty;
        }

        var lines = new List<string> { LineAt(text, header) };

        // The separator, where there is one. It carries the column count and the alignment, and
        // without it a header row and a data row are indistinguishable to a reader.
        var afterHeader = text.IndexOf('\n', header) + 1;

        if (afterHeader > 0 && afterHeader < lineStart && IsTableSeparator(LineAt(text, afterHeader)))
        {
            lines.Add(LineAt(text, afterHeader));
        }

        // The caption: the nearest line above the table that says anything. A blank line between
        // the caption and the table is normal Markdown and is skipped over.
        var caption = string.Empty;
        var above = header;

        while (above > 0)
        {
            above = text.LastIndexOf('\n', above - 2) + 1;
            var line = LineAt(text, above).Trim();

            if (line.Length > 0)
            {
                caption = line;
                break;
            }

            if (above == 0)
            {
                break;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("... [this passage opens inside a table. Its caption and column headings:]");

        if (caption.Length > 0)
        {
            sb.AppendLine(caption);
        }

        foreach (var line in lines)
        {
            sb.AppendLine(line);
        }

        sb.AppendLine("... [rows between the headings and the window omitted]");

        return sb.ToString();
    }

    /// <summary>The line beginning at <paramref name="from"/>, without its terminator.</summary>
    private static string LineAt(string text, int from)
    {
        if (from < 0 || from >= text.Length)
        {
            return string.Empty;
        }

        var end = text.IndexOf('\n', from);

        return (end < 0 ? text[from..] : text[from..end]).TrimEnd('\r');
    }

    private static bool IsTableRow(string line) =>
        line.TrimStart().StartsWith('|');

    /// <summary>
    /// Whether a row is the <c>|---|---|</c> rule under a header rather than data. Cells hold
    /// only dashes and alignment colons, and there is at least one of them.
    /// </summary>
    private static bool IsTableSeparator(string line)
    {
        var cells = line.Trim().Trim('|').Split('|');

        return cells.Length > 0
            && cells.All(cell =>
            {
                var trimmed = cell.Trim();

                return trimmed.Length > 0 && trimmed.All(c => c is '-' or ':');
            });
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

                // Cleaned against the vocabulary here rather than trusted as returned. The
                // schema constrains the enum on endpoints that honour response formats, and
                // StructuredFindings can be turned off for one that does not — at which point
                // the field is whatever the model felt like writing. A near-miss is dropped
                // rather than mapped: a filter for a category has to find every finding in it,
                // and filing "Data inconsistencies" under "Data quality" breaks that more
                // quietly than dropping it would.
                IssueCategories = [.. IssueCategory.Clean(finding.IssueCategories)],
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

    /// <summary>
    /// The finding a group gets when no finding was produced for it at all.
    ///
    /// <b>Error rather than Indeterminate, and present rather than absent.</b> Indeterminate is a
    /// real assessment outcome — the assessor read the pack and could not close the comparison —
    /// and a requirement nothing assessed has not earned it. Error propagates to the check, so a
    /// run cannot report No Issue on a check one of whose requirements never ran.
    /// </summary>
    internal static GroupFinding NotAssessed(PlanQueryGroup group) => new()
    {
        GroupId = group.GroupId,
        Requirement = group.Requirement,
        Analysis =
            "No assessment completed for this requirement: its evidence was gathered and its "
            + "prompt built, but no finding came back. This is a defect in the run, not a "
            + "judgement about the advice — nothing here says the requirement is met or breached. "
            + "Re-run the check before relying on its outcome.",
        Outcome = nameof(CheckOutcome.Error),
        ComparisonPerformed = false,
        MissingInputs = ["an assessment of this requirement"],
    };

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
