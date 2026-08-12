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
public sealed class CheckPlanRunner
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

    /// <summary>Output cap for a decision. Enough for a structured finding across every group.</summary>
    private const int DecisionMaxTokens = 8000;

    private readonly AppSettings _settings;
    private readonly PromptEvaluator _evaluator;
    private readonly CaseDocumentSearchTool _search;
    private readonly CanonicalModelDocument _model;
    private readonly CanonicalModelAccessor _accessor;
    private readonly PromptLogWriter? _promptLog;

    public CheckPlanRunner(
        AppSettings settings,
        PromptEvaluator evaluator,
        CaseDocumentSearchTool search,
        CanonicalModelDocument model,
        PromptLogWriter? promptLog = null)
    {
        _settings = settings;
        _evaluator = evaluator;
        _search = search;
        _model = model;
        _accessor = new CanonicalModelAccessor(model.Json);
        _promptLog = promptLog;
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

            var packs = new List<GroupEvidence>();
            foreach (var group in plan.QueryGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"{plan.CheckId}: {group.GroupId}");
                packs.Add(await GatherAsync(group, cancellationToken).ConfigureAwait(false));
            }

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
            var usage = TokenUsage.Empty;
            var findings = new List<GroupFinding>();

            foreach (var pack in packs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"{plan.CheckId}: assessing {pack.Group.GroupId}");

                var userPrompt = header + BuildGroupPrompt(pack);

                var result = await _evaluator
                    .RunRawAsync(
                        systemPrompt,
                        userPrompt,
                        DecisionMaxTokens,
                        FindingSchema.ResponseFormat(_settings.StructuredFindings),
                        cancellationToken)
                    .ConfigureAwait(false);

                _promptLog?.LogExchange(
                    $"{plan.CheckId}/{pack.Group.GroupId}", plan.CheckName,
                    systemPrompt, userPrompt, result.Response);

                usage = AddUsage(usage, result.Breakdown.Usage);

                // Verified against the evidence this group was actually given — never against
                // the whole check's evidence, or a quote lifted from a neighbouring group's
                // passages would verify and the check would be worthless.
                findings.Add(CitationVerifier.Verify(
                    ParseGroup(result.Response, pack.Group), EvidenceTextOf(pack)));
            }

            startedAt.Stop();

            var finding = CheckFinding.FromGroups(
                plan.CheckId, plan.CheckName, findings,
                trigger.Applies ? null : "The trigger appears absent.");

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

        var ranked = Rank(passages, targeted);

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
    /// Orders a group's passages and keeps the best <see cref="MaxPassagesPerGroup"/>.
    ///
    /// Targeted categories first, then score — and then the passage itself. That last part is
    /// what makes the cut reproducible: scores collide often enough to matter, and
    /// <c>Take</c> slices straight through the tie band, so without a final key which passages
    /// survive depends on the order the vector store happened to return them in. An approximate
    /// index is under no obligation to keep that order stable.
    /// </summary>
    internal static List<CaseDocumentSearchMatch> Rank(
        IEnumerable<CaseDocumentSearchMatch> passages,
        IReadOnlySet<string> targeted) =>
        passages
            .OrderByDescending(p => targeted.Count == 0 || targeted.Contains(p.CategoryCode) ? 1 : 0)
            .ThenByDescending(p => p.Score)
            .ThenBy(p => p.DocumentName, StringComparer.Ordinal)
            .ThenBy(p => p.SearchedText, StringComparer.Ordinal)
            .Take(MaxPassagesPerGroup)
            .ToList();

    private async Task<IReadOnlyList<CaseDocumentSearchMatch>> SearchAsync(
        PlannedQuery query, CancellationToken cancellationToken) =>
        await _search.SearchAsync(query.Text, cancellationToken).ConfigureAwait(false);

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
            sb.AppendLine(Truncate(extraction.Json, 4000));
            sb.AppendLine("```");
            sb.AppendLine();
        }

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
    internal static IReadOnlyList<string> EvidenceTextOf(GroupEvidence pack) =>
    [
        .. pack.Passages.Select(p => p.SearchedText),
        .. pack.Fragments.Where(f => f.Found).Select(f => f.Json),
    ];

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

            // The plan is the authority on which requirement this is; the model only echoes it.
            return finding with
            {
                GroupId = group.GroupId,
                Requirement = string.IsNullOrWhiteSpace(finding.Requirement)
                    ? group.Requirement
                    : finding.Requirement,
            };
        }
        catch (JsonException ex)
        {
            return Unreadable(group, $"The finding could not be read ({ex.Message}).", response);
        }
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
