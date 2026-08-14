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
    [JsonPropertyName("priority")] public string Priority { get; init; } = "Core";
    [JsonPropertyName("note")] public string? Note { get; init; }

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
/// Where a group expects each side of its comparison to come from: the assertion from the
/// canonical model's own category, the evidence from one or more document categories.
///
/// This was authored into every plan and then never read by anything, which is how seventeen
/// groups came to declare an evidence category none of their queries asked for. Declaring
/// where the answer lives and then not looking there is silent under-assessment: the group
/// reports what it found in the categories it did search, and nothing reports the omission.
/// Modelled here so <see cref="CheckPlanLint"/> can hold the plans to their own word.
/// </summary>
public sealed record PlanExpectedCategories
{
    [JsonPropertyName("assertion")] public List<string> Assertion { get; init; } = [];
    [JsonPropertyName("evidence")] public List<string> Evidence { get; init; } = [];
}

public sealed record PlanQueryGroup
{
    [JsonPropertyName("groupId")] public string GroupId { get; init; } = string.Empty;
    [JsonPropertyName("requirement")] public string Requirement { get; init; } = string.Empty;
    [JsonPropertyName("limb")] public string Limb { get; init; } = "Consistency";
    [JsonPropertyName("canonicalPaths")] public List<string> CanonicalPaths { get; init; } = [];
    [JsonPropertyName("expectedCategories")] public PlanExpectedCategories? ExpectedCategories { get; init; }
    [JsonPropertyName("queries")] public List<PlannedQuery> Queries { get; init; } = [];
    [JsonPropertyName("comparison")] public PlanComparison? Comparison { get; init; }
    [JsonPropertyName("sufficiency")] public PlanSufficiency? Sufficiency { get; init; }

    /// <summary>Evidence categories this group's queries actually ask the store for.</summary>
    public IReadOnlySet<string> QueriedCategories =>
        Queries
            .Where(q => q.IsEvidenceSearch)
            .SelectMany(q => q.TargetCategories)
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Evidence categories this group declares its answer lives in.</summary>
    public IReadOnlySet<string> DeclaredEvidenceCategories =>
        (ExpectedCategories?.Evidence ?? [])
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Paths cited on the group and on its queries, de-duplicated in first-seen order.</summary>
    public IReadOnlyList<string> AllCanonicalPaths =>
        CanonicalPaths
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

public sealed record PlanTriggerProbe
{
    [JsonPropertyName("triggerField")] public string? TriggerField { get; init; }
    [JsonPropertyName("queries")] public List<PlannedQuery> Queries { get; init; } = [];
    [JsonPropertyName("presentWhen")] public string? PresentWhen { get; init; }
    [JsonPropertyName("absentWhen")] public string? AbsentWhen { get; init; }
    [JsonPropertyName("onAbsent")] public string? OnAbsent { get; init; }
    [JsonPropertyName("note")] public string? Note { get; init; }

    /// <summary>True when the plan says a missing trigger settles the check as N/A.</summary>
    public bool ReturnsNotApplicable =>
        string.Equals(OnAbsent, "ReturnNA", StringComparison.OrdinalIgnoreCase);

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
    [JsonPropertyName("planVersion")] public string PlanVersion { get; init; } = string.Empty;
    [JsonPropertyName("checkId")] public string CheckId { get; init; } = string.Empty;
    [JsonPropertyName("checkName")] public string CheckName { get; init; } = string.Empty;
    [JsonPropertyName("appliesTo")] public string? AppliesTo { get; init; }
    [JsonPropertyName("regulatoryBasis")] public string? RegulatoryBasis { get; init; }
    [JsonPropertyName("primaryCategories")] public List<string> PrimaryCategories { get; init; } = [];
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
