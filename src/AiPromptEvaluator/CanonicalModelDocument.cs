namespace AiPromptEvaluator;

/// <summary>
/// One extracted canonical model, as stored. The model itself is kept as JSON rather than
/// as typed properties: the schema is the contract, it will move faster than this app, and
/// a check reads it by canonical path rather than by member access — so mirroring 60-odd
/// nested types here would buy nothing and break on every schema revision.
/// </summary>
public sealed record CanonicalModelDocument(
    string CaseReference,
    int TenantId,
    string Json,
    string ModelId,
    string SchemaVersion,
    IReadOnlyList<string> SourceDocuments,
    DateTimeOffset ExtractedAt,
    TokenUsage Usage)
{
    /// <summary>Rough size indicator for the UI, so a user can see something was actually captured.</summary>
    public int JsonLength => Json.Length;
}

/// <summary>
/// One extraction pass. The report is parsed a section at a time rather than in one call:
/// a full canonical model runs to tens of thousands of tokens of JSON, and a response that
/// hits the output cap halfway through an array is not partially useful — it is unparseable.
///
/// Sections are also the unit of retry, so one section failing costs one pass rather than
/// the whole document.
/// </summary>
public sealed record ExtractionSection(string Name, string Description, IReadOnlyList<string> Properties)
{
    /// <summary>
    /// The passes, in the order they run. Ordering follows the document's own shape — who the
    /// client is, then their money, then what was recommended — so each pass reads the report
    /// in roughly the order a human would.
    /// </summary>
    public static readonly IReadOnlyList<ExtractionSection> All =
    [
        new("Case and parties",
            "The document itself, the advice case, the clients, their dependants and the client authority.",
            ["source", "case", "parties", "clientAuthority"]),

        new("Financial position",
            "Incomes, expenditures, assets, liabilities, totals, emergency fund, liquidity and access needs, affordability and foreseeable changes.",
            ["financialPosition"]),

        new("Objectives",
            "The client's goals, demands and needs, with priority, target and time horizon.",
            ["objectives"]),

        new("Risk, knowledge and vulnerability",
            "Attitude to risk (assessed and agreed), capacity for loss, tolerance for loss, the risk reconciliation, knowledge and experience, and any vulnerability.",
            ["riskAssessment", "knowledgeAndExperience", "vulnerability"]),

        new("Existing arrangements",
            "Every existing plan or policy described in the report, with values, contributions, charges, holdings, safeguarded benefits, penalties and the advice action taken on it.",
            ["existingArrangements"]),

        new("Needs analysis and research",
            "Cashflow scenarios, stress tests, shortfalls and assumptions; the options considered and comparisons made.",
            ["needsAnalysis", "research"]),

        // Solution runs before Recommendations because a recommendation carries a targetPlanId
        // pointing into solution.recommendedPlans. Every cross-reference in the model now runs
        // forwards, so the identifier table (see CanonicalModelIdentity) always holds the ids a
        // pass needs by the time that pass runs.
        new("Solution",
            "The recommended plans, the investment strategy, the provider selection and implementation.",
            ["solution"]),

        new("Recommendations",
            "One entry per discrete recommended action, with its own rationale, benefits, disadvantages, risks, tax implications and alternatives.",
            ["recommendations"]),

        new("Costs and charges",
            "Layered charges for existing and recommended arrangements, adviser fees, the cost comparison, reduction in yield, value assessment and disclosure completeness.",
            ["costsAndCharges"]),

        new("Replacement analysis",
            "One entry per plan being switched, transferred, encashed or surrendered. Leave as an empty array when nothing is being replaced.",
            ["replacementAnalysis"]),

        new("Disclosures",
            "Risk warnings, disadvantages, loss scenarios, prominence, tax treatment and the ongoing service.",
            ["riskWarningsAndDisadvantages", "taxTreatment", "ongoingService"]),

        new("Triggers and extraction report",
            "The derived check triggers, and the extraction's own account of what was missing, ambiguous or internally contradictory.",
            ["checkTriggers", "extractionReport"]),
    ];
}

/// <summary>Progress for one extraction pass, reported as the run proceeds.</summary>
public sealed record ExtractionProgress(
    int Done,
    int Total,
    string SectionName,
    int JsonLength,
    TimeSpan Elapsed,
    string? Error = null);
