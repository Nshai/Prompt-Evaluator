using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiPromptEvaluator;

/// <summary>
/// One planned search. <c>Side</c> decides where the answer comes from: an Assertion query
/// is satisfied from the stored canonical model by its canonical paths, an Evidence query is
/// run against the vector store. That split is what stops a check re-reading the suitability
/// report it has already been extracted from.
/// </summary>
public sealed record PlannedQuery
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("text")] public string Text { get; init; } = string.Empty;
    [JsonPropertyName("side")] public string Side { get; init; } = "Either";
    [JsonPropertyName("targetCategories")] public List<string> TargetCategories { get; init; } = [];
    [JsonPropertyName("expectSignals")] public List<string> ExpectSignals { get; init; } = [];
    [JsonPropertyName("canonicalPaths")] public List<string> CanonicalPaths { get; init; } = [];
    /// <summary>
    /// <c>Core</c> or <c>Supplementary</c>. Honoured when
    /// <see cref="AppSettings.CoreQueriesOnly"/> is set: a Supplementary query is then not run
    /// at all. The plans mark roughly one query in seven that way, so the switch is the
    /// cheapest retrieval saving available, and the plan authors have already chosen what it
    /// costs rather than leaving it to a threshold.
    /// </summary>
    [JsonPropertyName("priority")] public string Priority { get; init; } = "Core";

    /// <summary>True unless the plan marked this query Supplementary.</summary>
    public bool IsCore =>
        !Priority.Equals("Supplementary", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when this query should be run against the vector store. "Either" counts: those
    /// phrasings are written to retrieve from both sides, and the evidence half still has to
    /// be searched for.
    /// </summary>
    public bool IsEvidenceSearch =>
        !Side.Equals("Assertion", StringComparison.OrdinalIgnoreCase);
}

public sealed record PlanComparison
{
    [JsonPropertyName("method")] public string? Method { get; init; }
    [JsonPropertyName("tolerance")] public string? Tolerance { get; init; }
    [JsonPropertyName("falsePositiveGuards")] public List<string> FalsePositiveGuards { get; init; } = [];
}

public sealed record PlanSufficiency
{
    [JsonPropertyName("minEvidenceCategories")] public int? MinEvidenceCategories { get; init; }
    [JsonPropertyName("ifAssertionAbsent")] public string? IfAssertionAbsent { get; init; }
    [JsonPropertyName("ifEvidenceAbsent")] public string? IfEvidenceAbsent { get; init; }
    [JsonPropertyName("ifBothAbsent")] public string? IfBothAbsent { get; init; }
}

/// <summary>
/// Everything that decides <b>what the assessor is allowed to see</b>. A mistake in this
/// block is invisible in the output: the check reports confidently on a pack that never
/// contained the answer.
/// </summary>
public sealed record PlanRetrieval
{
    /// <summary>
    /// Where the answer lives in the canonical model. This is the assertion side — retrieval
    /// from the stored extract rather than from the vector store.
    /// </summary>
    [JsonPropertyName("canonicalPaths")] public List<string> CanonicalPaths { get; init; } = [];

    /// <summary>
    /// Words naming the part of a document this group's answer lives in — "disposable income",
    /// "residency", "existing pension provision".
    ///
    /// A category is too coarse a target for a form. The Fact Find is one 42 KB document with a
    /// dozen unrelated sections, and once a floor guaranteed it a slot the slot went to whichever
    /// section embedded best for the group's wording. Measured on a real run, the income section
    /// was delivered to four groups assessing personal details, tax status, employment and the
    /// emergency fund, and to none of the groups that needed it. Not one mentioned it.
    ///
    /// Matched against the passage text rather than a stored section id, because converted
    /// documents carry their headings inline and a heading is what the hint is really naming.
    /// </summary>
    [JsonPropertyName("evidenceSections")] public List<string> EvidenceSections { get; init; } = [];

    /// <summary>
    /// How many passages this group's assessor may be shown, overriding
    /// <see cref="AppSettings.MaxPassagesPerGroup"/>.
    ///
    /// <b>Groups are not alike, and one global cap prices them as though they were.</b> 53 of 88
    /// sit at the cap; CHK-007 reaches eight document categories across eleven groups while
    /// CHK-010 reaches three across five. The pack is 54–65% of a run's chat tokens, so a cap set
    /// for the widest group is paid by every narrow one — and the measured relationship between
    /// pack size and recall in this pipeline is <i>negative</i>, so the wide setting is not even
    /// buying quality for the groups that do not need it.
    ///
    /// The plan is the right place for this because the plan already knows the shape: it declares
    /// the categories, the sections and the comparison. Absent means the global setting, so a plan
    /// that says nothing behaves exactly as it did.
    /// </summary>
    [JsonPropertyName("maxPassages")] public int? MaxPassages { get; init; }

    [JsonPropertyName("queries")] public List<PlannedQuery> Queries { get; init; } = [];
}

/// <summary>
/// What code may establish for this requirement before any model is asked.
///
/// <b>A directive, not a steer.</b> Everything else under <see cref="PlanVerification"/> is a
/// string interpolated into a prompt, and the measured result of that is unambiguous: every
/// judgement moved into code has held, and every judgement moved into a prompt clause has been
/// ignored by at least one model — a comparison-basis clause violated by both models while they
/// quoted both dates, three guards and a hint that never fired across eight runs. These fields are
/// read by the reconciler and branched on. Nothing here reaches the prompt as prose.
/// </summary>
public sealed record PlanReconciliation
{
    /// <summary>
    /// What makes a candidate here.
    ///
    /// <c>None</c> is the important value and the one that is easy to under-use: narrative has no
    /// value to diverge, and comparing prose as values manufactures findings. The largest false
    /// positive in either measured run was ten "contradictions" at High severity that were one
    /// undisclosed charge basis, tabulated ten times.
    /// </summary>
    [JsonPropertyName("detect")] public string Detect { get; init; } = "None";

    /// <summary>
    /// One candidate for the path rather than one per array element. Sixteen missing allocation
    /// rows are one observation, not sixteen findings — and the routing mechanism this governs was
    /// measured delivering one item into 26 of 88 groups, 92 times over.
    /// </summary>
    [JsonPropertyName("emitOnePerPath")] public bool EmitOnePerPath { get; init; }

    /// <summary>
    /// <c>CurrentOnly</c> refuses to compare a projection against a current figure, which is not a
    /// discrepancy. The canonical model already records this: its provenance carries a modality,
    /// and a measured extract holds 60 Projected, 29 Assumed and 13 Conditional values that a
    /// current-figure comparison has no business touching.
    /// </summary>
    [JsonPropertyName("modality")] public string Modality { get; init; } = "AnyModality";

    /// <summary>
    /// How far apart two dated values may be before the difference is an as-at artefact rather
    /// than a conflict — £600 at 24/03 against £800 at 21/07 is not a contradiction.
    /// </summary>
    [JsonPropertyName("asAtToleranceDays")] public int? AsAtToleranceDays { get; init; }

    [JsonPropertyName("note")] public string? Note { get; init; }

    /// <summary>True when the plan says code should compare nothing for this requirement.</summary>
    public bool DetectsNothing =>
        string.IsNullOrWhiteSpace(Detect)
        || Detect.Equals("None", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when a projected or assumed value may not stand in for a current one.</summary>
    public bool CurrentOnly =>
        Modality.Equals("CurrentOnly", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Whether this requirement needs judgement at all, and how far its severity may reach.
///
/// <b>A directive, not a steer</b> — see <see cref="PlanReconciliation"/>. The adjudicator branches
/// on these.
/// </summary>
public sealed record PlanAdjudication
{
    /// <summary>
    /// <c>Deterministic</c> where code has already proved the finding and only a sentence is
    /// wanted. An Appropriateness limb is never Deterministic: code can establish the facts but
    /// not the verdict.
    /// </summary>
    [JsonPropertyName("mode")] public string Mode { get; init; } = "Model";

    /// <summary>
    /// The highest severity this requirement may report. An absence is held below the severities
    /// reserved for a demonstrated contradiction.
    /// </summary>
    [JsonPropertyName("severityCeiling")] public string? SeverityCeiling { get; init; }

    /// <summary>Whether a finding here must carry a locatable quote to be reported.</summary>
    [JsonPropertyName("requiresCitation")] public bool RequiresCitation { get; init; }

    [JsonPropertyName("note")] public string? Note { get; init; }

    /// <summary>True when the plan says code settles this requirement without asking a model.</summary>
    public bool IsDeterministic =>
        Mode.Equals("Deterministic", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Everything that decides <b>what the assessor makes of what it was given</b>.
///
/// The block is now two kinds of thing, and the difference is the whole point. <c>limb</c>,
/// <c>comparison</c>, <c>sufficiency</c> and <c>issueCategories</c> are strings interpolated into
/// one group's prompt — nothing branches on them, and the measured result of that is that a model
/// may ignore any of them. <see cref="Reconciliation"/> and <see cref="Adjudication"/> are
/// directives: code reads them, code branches on them, and no model is asked to comply.
/// </summary>
public sealed record PlanVerification
{
    [JsonPropertyName("limb")] public string Limb { get; init; } = "Consistency";
    [JsonPropertyName("comparison")] public PlanComparison? Comparison { get; init; }
    [JsonPropertyName("sufficiency")] public PlanSufficiency? Sufficiency { get; init; }

    /// <summary>What code may establish before a model is asked. See <see cref="PlanReconciliation"/>.</summary>
    [JsonPropertyName("reconciliation")] public PlanReconciliation? Reconciliation { get; init; }

    /// <summary>Whether judgement is needed, and its ceiling. See <see cref="PlanAdjudication"/>.</summary>
    [JsonPropertyName("adjudication")] public PlanAdjudication? Adjudication { get; init; }

    /// <summary>
    /// The kinds of problem this requirement can raise, from <see cref="IssueCategory"/>.
    ///
    /// <b>A steer, not a constraint.</b> The plan already decides what is searched for and what
    /// is compared, because deciding those in advance is what makes a finding reproducible. It
    /// does not decide what is found — so this narrows the vocabulary the assessor is shown for
    /// a requirement whose failure mode is known in advance, and says nothing at all where it is
    /// not. A requirement about a frequency mismatch is a data-quality problem or it is nothing;
    /// listing all nine there invites a category chosen for variety.
    ///
    /// The assessor may still answer outside the steer. A plan that could forbid a category
    /// could forbid a finding, and the whole point of the retrieval/verification split is that
    /// the plan governs the first and not the second.
    /// </summary>
    [JsonPropertyName("issueCategories")] public List<string> IssueCategories { get; init; } = [];
}

/// <summary>
/// What the group says about itself. Checked at build time by <see cref="CheckPlanLint"/> and
/// never read at run time.
///
/// This was authored into every plan and then never read by anything, which is how seventeen
/// groups came to declare an evidence category none of their queries asked for. Declaring
/// where the answer lives and then not looking there is silent under-assessment: the group
/// reports what it found in the categories it did search, and nothing reports the omission.
/// </summary>
public sealed record PlanDeclares
{
    [JsonPropertyName("assertionCategories")] public List<string> AssertionCategories { get; init; } = [];
    [JsonPropertyName("evidenceCategories")] public List<string> EvidenceCategories { get; init; } = [];
}

/// <summary>
/// Retrieval settings for the plan as a whole, as distinct from one group's
/// <see cref="PlanRetrieval"/> block.
/// </summary>
public sealed record PlanRetrievalSettings
{
    /// <summary>
    /// How many hits each of this check's queries may return, where the check needs a wider
    /// pool than <see cref="AppSettings.MaxSearchResults"/> gives it.
    ///
    /// Checks are not alike in how far they fan out. CHK-007 reaches eight document categories
    /// across eleven groups; CHK-010 reaches three across five. Held to the same eight hits per
    /// query, the wide checks compete for slots against a much larger case file and lose
    /// passages the narrow ones never had to.
    ///
    /// Raising it costs vector lookups, not prompt tokens. MaxPassagesPerGroup still caps what
    /// reaches the assessor, so a wider pool changes what ranking gets to choose from and not
    /// how much the model reads. Values below the global setting are ignored rather than
    /// honoured — see CaseDocumentSearchService.
    /// </summary>
    [JsonPropertyName("resultsPerCall")] public int? ResultsPerCall { get; init; }
}

public sealed record PlanQueryGroup
{
    [JsonPropertyName("groupId")] public string GroupId { get; init; } = string.Empty;
    [JsonPropertyName("requirement")] public string Requirement { get; init; } = string.Empty;
    [JsonPropertyName("declares")] public PlanDeclares? Declares { get; init; }
    [JsonPropertyName("retrieval")] public PlanRetrieval Retrieval { get; init; } = new();
    [JsonPropertyName("verification")] public PlanVerification? Verification { get; init; }

    // ── Flattened access. The nesting exists to make the retrieval/verification split legible
    //    to whoever edits a plan, not to make it awkward to read one.

    public List<PlannedQuery> Queries => Retrieval.Queries;
    public List<string> CanonicalPaths => Retrieval.CanonicalPaths;
    public string Limb => Verification?.Limb ?? "Consistency";

    /// <summary>
    /// The categories the plan says this requirement is likely to raise, cleaned against the
    /// vocabulary. Empty means the plan is not steering, which is a legitimate answer and the
    /// default for every plan written before the field existed.
    /// </summary>
    public IReadOnlyList<string> SteeredIssueCategories =>
        IssueCategory.Clean(Verification?.IssueCategories);
    public PlanComparison? Comparison => Verification?.Comparison;
    public PlanSufficiency? Sufficiency => Verification?.Sufficiency;

    /// <summary>
    /// What code may establish for this group before a model is asked.
    ///
    /// Never null: a plan written before these fields existed reads as "detect nothing, ceiling
    /// nothing, ask the model" — which is exactly what those plans did, so an old plan keeps its
    /// behaviour rather than acquiring a default somebody has to discover from a run.
    /// </summary>
    public PlanReconciliation Reconciliation => Verification?.Reconciliation ?? new PlanReconciliation();

    /// <inheritdoc cref="Reconciliation"/>
    public PlanAdjudication Adjudication => Verification?.Adjudication ?? new PlanAdjudication();

    /// <summary>
    /// The pack cap for this group: the plan's own budget where it sets one, else the global
    /// setting. A plan may only narrow the cap, never widen it past what the run was configured
    /// to afford — a budget is a ceiling somebody chose, and a plan file should not be able to
    /// raise the bill of a run without the run's consent.
    /// </summary>
    public int PassageCap(int configured)
    {
        if (Retrieval.MaxPassages is not { } planned || planned <= 0)
        {
            return configured;
        }

        // 0 means unbounded on the setting, so anything the plan names is narrower than that.
        return configured <= 0 ? planned : Math.Min(planned, configured);
    }

    /// <summary>The queries that will actually run, after the Core-only switch.</summary>
    public IEnumerable<PlannedQuery> QueriesToRun(bool coreOnly) =>
        coreOnly ? Queries.Where(q => q.IsCore) : Queries;

    /// <summary>Evidence categories this group's queries actually ask the store for.</summary>
    public IReadOnlySet<string> QueriedCategories =>
        Queries
            .Where(q => q.IsEvidenceSearch)
            .SelectMany(q => q.TargetCategories)
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Words naming the sections this group's evidence lives in, if the plan says.</summary>
    public IReadOnlyList<string> DeclaredEvidenceSections =>
        Retrieval.EvidenceSections
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

    /// <summary>Evidence categories this group declares its answer lives in.</summary>
    public IReadOnlySet<string> DeclaredEvidenceCategories =>
        (Declares?.EvidenceCategories ?? [])
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Categories this group declares the report's own claim comes from.</summary>
    public IReadOnlyList<string> DeclaredAssertionCategories =>
        (Declares?.AssertionCategories ?? [])
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .ToList();

    /// <summary>Paths cited on the group and on its queries, de-duplicated in first-seen order.</summary>
    public IReadOnlyList<string> AllCanonicalPaths =>
        Retrieval.CanonicalPaths
            .Concat(Queries.SelectMany(q => q.CanonicalPaths))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// True when every query is assertion-side, so the group is settled from the canonical
    /// model alone. That is a real and deliberate shape: internal contradictions, charge
    /// arithmetic and the prominence of a risk section are all properties of the report
    /// compared against itself, with no supporting document to corroborate them.
    ///
    /// The distinction has to reach the assessor. An empty evidence section means "nothing
    /// to retrieve here" for these groups and "we looked and found nothing" everywhere else,
    /// and confusing the two turns a sound finding into a spurious evidence gap.
    /// </summary>
    public bool IsModelOnly => Queries.Count > 0 && !Queries.Any(q => q.IsEvidenceSearch);
}

/// <summary>
/// One applicability rule: a set of values the case must show at one or more canonical paths
/// before the check applies.
///
/// The value set is named by its own key, so a rule reads as a sentence —
/// <c>{ "goalTypes": ["Investment", "Pension"], "canonicalPaths": ["/objectives[]/objectiveType"] }</c>
/// is "the client's goals include an investment or a pension". The name is carried into the
/// N/A summary, so a skipped check says which condition it failed rather than only that one did.
///
/// A rule passes when <b>any</b> value found at <b>any</b> of its paths matches <b>any</b> of the
/// accepted values, case-insensitively. Rules are ANDed: every rule must pass for the check to
/// run. Widening a rule therefore widens the check, and adding one narrows it.
/// </summary>
public sealed record PlanApplicability
{
    /// <summary>Where in the canonical model to look. Fan-out paths are read element by element.</summary>
    [JsonPropertyName("canonicalPaths")] public List<string> CanonicalPaths { get; init; } = [];

    /// <summary>
    /// The named value set. Exactly one property other than <c>canonicalPaths</c> is expected,
    /// and its name is the rule's own label.
    /// </summary>
    [JsonExtensionData] public Dictionary<string, JsonElement> Named { get; init; } = [];

    /// <summary>What this rule is called, for the summary a skipped check writes.</summary>
    public string Name => Named.Keys.FirstOrDefault() ?? "values";

    /// <summary>The values that satisfy this rule.</summary>
    public IReadOnlyList<string> AcceptedValues
    {
        get
        {
            if (Named.Count == 0)
            {
                return [];
            }

            var element = Named.Values.First();

            return element.ValueKind switch
            {
                JsonValueKind.Array => [.. element.EnumerateArray().Select(Scalar).Where(v => v.Length > 0)],
                _ => Scalar(element) is { Length: > 0 } single ? [single] : [],
            };
        }
    }

    private static string Scalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Number => element.ToString(),
        _ => string.Empty,
    };

    /// <summary>
    /// Whether any of <paramref name="found"/> satisfies this rule. Case-insensitive, because
    /// a plan author writing "investment" should not have to know the schema capitalises it.
    /// </summary>
    public bool IsSatisfiedBy(IEnumerable<string> found) =>
        AcceptedValues.Count > 0
        && found.Any(v => AcceptedValues.Contains(v, StringComparer.OrdinalIgnoreCase));
}

public sealed record PlanTriggerProbe
{
    [JsonPropertyName("triggerField")] public string? TriggerField { get; init; }

    /// <summary>
    /// Rules the case must satisfy for this check to run, ANDed together. Evaluated against the
    /// stored canonical model before any group is gathered, so a check that does not apply
    /// costs nothing rather than retrieving evidence it will not use.
    /// </summary>
    [JsonPropertyName("applicability")] public List<PlanApplicability> Applicability { get; init; } = [];
    [JsonPropertyName("queries")] public List<PlannedQuery> Queries { get; init; } = [];
    /// <summary>Quoted verbatim in the N/A summary, so a skipped check says why.</summary>
    [JsonPropertyName("absentWhen")] public string? AbsentWhen { get; init; }

    [JsonPropertyName("onAbsent")] public string? OnAbsent { get; init; }

    /// <summary>
    /// True when the plan says a missing trigger settles the check without assessing it.
    ///
    /// <c>Skip</c> is the current spelling; <c>ReturnNA</c> is the older one and still accepted,
    /// because a plan should not stop loading over a synonym.
    /// </summary>
    public bool ReturnsNotApplicable =>
        string.Equals(OnAbsent, "Skip", StringComparison.OrdinalIgnoreCase)
        || string.Equals(OnAbsent, "ReturnNA", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the plan says the check runs anyway with a missing trigger — an overlay that
    /// applies to every case, where the absence of an assessment is the thing being assessed.
    ///
    /// The distinction has to reach the assessor. Told only that "the trigger appears absent",
    /// CHK-010 returned Indeterminate on all four of its requirements and argued in its own
    /// analysis that the case presented no vulnerability drivers — in a file recording "No
    /// understanding / knowledge" of pensions four times over, which is the textbook FG21/1
    /// low-capability driver. A missing vulnerability flag is not evidence that the client is
    /// not vulnerable; it is evidence that nobody looked.
    /// </summary>
    public bool ContinuesWithReducedScope =>
        string.Equals(OnAbsent, "ContinueWithReducedScope", StringComparison.OrdinalIgnoreCase);
}

public sealed record PlanDecision
{
    [JsonPropertyName("noIssue")] public string? NoIssue { get; init; }
    [JsonPropertyName("potentialConcern")] public string? PotentialConcern { get; init; }
    [JsonPropertyName("notApplicable")] public string? NotApplicable { get; init; }
    [JsonPropertyName("severityHints")] public List<string> SeverityHints { get; init; } = [];
    [JsonPropertyName("overlayInstruction")] public string? OverlayInstruction { get; init; }
}

/// <summary>
/// A pre-computed search programme for one check, loaded from artifacts/check-plan. Each
/// group corresponds to one row of the check coverage matrix, so a group with no evidence
/// is a named gap rather than something the assessor has to notice for itself.
/// </summary>
public sealed record CheckQueryPlan
{
    /// <summary>
    /// The plan format this file is written for. Checked at load: a plan the runner does not
    /// understand is refused by name rather than half-read, which is what "const 1.0" always
    /// implied and never did.
    /// </summary>
    [JsonPropertyName("planVersion")] public string PlanVersion { get; init; } = string.Empty;

    [JsonPropertyName("checkId")] public string CheckId { get; init; } = string.Empty;
    [JsonPropertyName("checkName")] public string CheckName { get; init; } = string.Empty;

    /// <summary>
    /// Category codes from the check's Primary Document Categories column. Read by
    /// <see cref="CheckPlanLint"/> at build time and by nothing at run time.
    /// </summary>
    [JsonPropertyName("primaryCategories")] public List<string> PrimaryCategories { get; init; } = [];

    /// <summary>Retrieval settings for the whole plan. Absent means the global defaults.</summary>
    [JsonPropertyName("retrieval")] public PlanRetrievalSettings? Retrieval { get; init; }
    [JsonPropertyName("triggerProbe")] public PlanTriggerProbe? TriggerProbe { get; init; }
    [JsonPropertyName("queryGroups")] public List<PlanQueryGroup> QueryGroups { get; init; } = [];
    [JsonPropertyName("decision")] public PlanDecision? Decision { get; init; }

    public int QueryCount =>
        QueryGroups.Sum(g => g.Queries.Count) + (TriggerProbe?.Queries.Count ?? 0);
}

/// <summary>Loads the query plans from disk and matches them to checks by id.</summary>
public static class CheckQueryPlanLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>File name pattern the plans are published under.</summary>
    public const string SearchPattern = "*.query-plan.json";

    /// <summary>
    /// The only plan format this runner understands. A plan declaring anything else is
    /// refused with its version named, rather than loaded and silently misread — the fields
    /// a future format moves or renames would otherwise come back empty and the check would
    /// assess a pack assembled from half a plan.
    /// </summary>
    public const string SupportedPlanVersion = "1.0";

    /// <summary>
    /// Every plan in the folder, keyed by check id. A file that will not parse is skipped
    /// with its reason rather than failing the load: nine usable plans beat none.
    /// </summary>
    public static (Dictionary<string, CheckQueryPlan> Plans, List<(string File, string Error)> Failures) Load(
        string folder)
    {
        var plans = new Dictionary<string, CheckQueryPlan>(StringComparer.OrdinalIgnoreCase);
        var failures = new List<(string File, string Error)>();

        if (!Directory.Exists(folder))
        {
            return (plans, failures);
        }

        foreach (var file in Directory.GetFiles(folder, SearchPattern).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var plan = JsonSerializer.Deserialize<CheckQueryPlan>(File.ReadAllText(file), Options);

                if (plan is null || string.IsNullOrWhiteSpace(plan.CheckId))
                {
                    failures.Add((Path.GetFileName(file), "No checkId in the plan."));
                    continue;
                }

                if (!string.Equals(plan.PlanVersion, SupportedPlanVersion, StringComparison.Ordinal))
                {
                    failures.Add((
                        Path.GetFileName(file),
                        $"planVersion \"{plan.PlanVersion}\" is not supported; this build reads "
                        + $"\"{SupportedPlanVersion}\"."));
                    continue;
                }

                plans[NormaliseCheckId(plan.CheckId)] = plan;
            }
            catch (Exception ex)
            {
                failures.Add((Path.GetFileName(file), ex.Message.Trim()));
            }
        }

        return (plans, failures);
    }

    /// <summary>
    /// Check ids arrive from the CSV with stray newlines and spacing ("CHK-001\n"), so both
    /// sides of the lookup are squeezed to the same shape before matching.
    /// </summary>
    public static string NormaliseCheckId(string checkId) =>
        new(checkId.Where(c => !char.IsWhiteSpace(c)).ToArray());
}
