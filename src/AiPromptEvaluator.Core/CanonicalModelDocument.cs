using System.Text.Json.Nodes;

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
        // forwards, so the identifier table (see CanonicalModelIdentityRegistry) always holds the ids a
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

    /// <summary>
    /// The passes grouped into dependency waves: every section in a wave may run concurrently, and
    /// the waves themselves run in order. A section lands in a strictly later wave than the section
    /// defining any id it references, so the identifier table a pass is shown (see
    /// <see cref="CanonicalModelIdentityRegistry"/>) already holds every id the pass may cite — a
    /// reference to a kind not yet defined would be dropped rather than resolved, and that dropped
    /// link is exactly what CHK-006 checks.
    ///
    /// <b>Case and parties is wave 0 alone, deliberately.</b> It defines the clients nearly
    /// everything references, so it has to precede the fan-out on the dependency graph — and it is
    /// also the pass that warms the provider's prefix cache over the report, so running it alone
    /// first is what keeps every later pass in the run affordable. Firing the passes cold together
    /// would re-bill the document on each. The self-report pass is the final wave alone, because it
    /// summarises the whole model the other passes produced.
    ///
    /// The edges are read from the schema rather than hand-listed, so they cannot drift from it: a
    /// section depends on a kind when its schema slice carries one of that kind's reference keys.
    /// Edges are kept only where the defining section comes earlier in <see cref="All"/>, which
    /// both breaks the one cycle in the model — an objective links to recommendations while a
    /// recommendation links back to objectives — and pins the direction to the canonical order the
    /// sequential passes already ran in.
    /// </summary>
    /// <param name="schemaJson">The canonical model schema, sliced per section to read its references.</param>
    public static IReadOnlyList<IReadOnlyList<ExtractionSection>> Waves(string schemaJson)
    {
        var sections = All;
        var last = sections.Count - 1;

        // The section index that defines each kind, found by the collection's root property. A kind
        // whose collection is not owned by any section (none today) simply produces no edges.
        var definerOf = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kind in CanonicalModelIdentityRegistry.Kinds)
        {
            var rootProperty = kind.CollectionPath[0];
            for (var i = 0; i < sections.Count; i++)
            {
                if (sections[i].Properties.Contains(rootProperty, StringComparer.Ordinal))
                {
                    definerOf[kind.Prefix] = i;
                    break;
                }
            }
        }

        var level = new int[sections.Count];

        for (var i = 0; i < sections.Count; i++)
        {
            // Case and parties warms the cache and defines the widest kind, so it is wave 0 alone;
            // the self-report pass reads the whole model, so it is forced to the last wave below.
            if (i == 0 || i == last)
            {
                continue;
            }

            var referenceKeys = PropertyNamesIn(JsonSchemaSlicer.Slice(schemaJson, sections[i].Properties));

            var deepest = 0;
            foreach (var kind in CanonicalModelIdentityRegistry.Kinds)
            {
                if (!definerOf.TryGetValue(kind.Prefix, out var definer) || definer >= i)
                {
                    // Self-definer, or a back-edge to a later section: not a dependency this pass
                    // waits on. The back-edge is the objective/recommendation cycle, cut here.
                    continue;
                }

                if (kind.ReferenceKeys.Any(key => referenceKeys.Contains(key)))
                {
                    deepest = Math.Max(deepest, level[definer]);
                }
            }

            // Minimum wave 1: nothing but Case runs in wave 0, so the cache is warm before any
            // fan-out pass starts.
            level[i] = deepest + 1;
        }

        level[last] = level.Max() + 1;

        return level
            .Select((wave, index) => (wave, section: sections[index]))
            .GroupBy(x => x.wave)
            .OrderBy(g => g.Key)
            .Select(g => (IReadOnlyList<ExtractionSection>)g.Select(x => x.section).ToList())
            .ToList();
    }

    /// <summary>Every property name appearing anywhere in a schema document, for a reference-key scan.</summary>
    private static HashSet<string> PropertyNamesIn(string schemaJson)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        Walk(JsonNode.Parse(schemaJson));
        return names;

        void Walk(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    if (obj["properties"] is JsonObject properties)
                    {
                        foreach (var (name, _) in properties)
                        {
                            names.Add(name);
                        }
                    }

                    foreach (var (_, value) in obj)
                    {
                        Walk(value);
                    }

                    break;

                case JsonArray array:
                    foreach (var item in array)
                    {
                        Walk(item);
                    }

                    break;
            }
        }
    }
}

/// <summary>Progress for one extraction pass, reported as the run proceeds.</summary>
public sealed record ExtractionProgress(
    int Done,
    int Total,
    string SectionName,
    int JsonLength,
    TimeSpan Elapsed,
    string? Error = null,
    string? Shortfall = null)
{
    /// <summary>
    /// True when the pass produced nothing usable. Distinguished from <see cref="Shortfall"/>,
    /// which is a pass that worked and lost something on the way.
    ///
    /// The two were reported identically and it was actively misleading: one run announced
    /// eight of twelve sections "FAILED" when eight of twelve had succeeded and merely dropped a
    /// value the schema's enum did not allow. A reader looking for the reason a charge line was
    /// missing had eight false leads to rule out first.
    /// </summary>
    public bool Failed => Error is not null;

    /// <summary>How the pass went, for one line of a log.</summary>
    public string Describe() =>
        Failed ? $"FAILED — {Error}"
        : Shortfall is not null ? $"{JsonLength:N0} chars — {Shortfall}"
        : $"{JsonLength:N0} chars";
}
