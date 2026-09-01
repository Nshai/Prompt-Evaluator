namespace AiPromptEvaluator;

/// <summary>
/// What kind of problem a finding is, from the Compliance Assistant's fixed vocabulary.
///
/// <b>A closed list, because the value of a category is entirely in it being shared.</b> A
/// reviewer filtering to "every data inconsistency in this case" is asking a question the
/// findings can only answer if they agree on the words; free text would give ten spellings of
/// the same idea across ten requirements and a filter that finds none of them. The list is the
/// design's, unchanged: adding to it is a decision about the product, not about a check.
///
/// <b>The category is not the outcome and cannot substitute for it.</b> A requirement that
/// passes carries no category at all — the list says what a concern <em>is</em>, not how bad it
/// is or what should happen about it. Severity and outcome remain separate fields, decided
/// after, for the same reason they always were.
/// </summary>
public static class IssueCategory
{
    /// <summary>Something the requirement needed was not in the case file at all.</summary>
    public const string MissingEvidence = "Missing evidence";

    /// <summary>A recommendation is made, and the reason given does not support it.</summary>
    public const string WeakJustification = "Weak justification";

    /// <summary>Two documents state the same fact differently, or a document contradicts itself.</summary>
    public const string DataInconsistency = "Data inconsistency";

    /// <summary>A value is present but wrong in form — a frequency, a unit, a transposition.</summary>
    public const string DataQuality = "Data quality";

    /// <summary>A section, disclosure or step the report is required to contain is absent.</summary>
    public const string MissingComponent = "Missing component";

    /// <summary>Something was disclosed, but not fairly, clearly or prominently enough.</summary>
    public const string DisclosureShortfall = "Disclosure shortfall";

    /// <summary>The advice process itself has a gap — no record, no check, no comprehension tested.</summary>
    public const string ProcessRisk = "Process risk";

    /// <summary>A control that should have caught this did not exist or did not run.</summary>
    public const string ControlGap = "Control gap";

    /// <summary>A regulatory obligation is engaged and the file does not meet it.</summary>
    public const string ComplianceConcern = "Compliance concern";

    /// <summary>The whole vocabulary, in the order the design lists it.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        MissingEvidence,
        WeakJustification,
        DataInconsistency,
        DataQuality,
        MissingComponent,
        DisclosureShortfall,
        ProcessRisk,
        ControlGap,
        ComplianceConcern,
    ];

    private static readonly Dictionary<string, string> Canonical =
        All.ToDictionary(c => c, c => c, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="value"/> is one of the nine, ignoring case.</summary>
    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Canonical.ContainsKey(value.Trim());

    /// <summary>
    /// The vocabulary's spelling of <paramref name="value"/>, or null when it is not in the list.
    ///
    /// Case is forgiven and anything else is not. A near-miss is dropped rather than guessed at:
    /// the point of a closed list is that a filter for one category finds every finding in it,
    /// and a fuzzy match that files "Data inconsistencies" under "Data quality" breaks that more
    /// quietly than dropping it would.
    /// </summary>
    public static string? Normalise(string? value) =>
        value is not null && Canonical.TryGetValue(value.Trim(), out var canonical) ? canonical : null;

    /// <summary>
    /// The recognised categories of <paramref name="values"/>, de-duplicated and in vocabulary
    /// order, with anything unrecognised dropped.
    /// </summary>
    public static IReadOnlyList<string> Clean(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        var kept = values
            .Select(Normalise)
            .Where(c => c is not null)
            .Select(c => c!)
            .ToHashSet(StringComparer.Ordinal);

        return All.Where(kept.Contains).ToList();
    }

    /// <summary>Values that are not in the vocabulary, for a plan lint to name them.</summary>
    public static IReadOnlyList<string> Unknown(IEnumerable<string>? values) =>
        (values ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v) && !IsKnown(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>The vocabulary as a JSON array, for the response schema's enum.</summary>
    public static string SchemaEnum() =>
        "[" + string.Join(", ", All.Select(c => "\"" + c + "\"")) + "]";
}

/// <summary>
/// Why a finding wants a second pair of eyes, independently of what it concluded.
///
/// <b>Every flag here is counted from the run, never asked of the model.</b> That is the whole
/// distinction from <see cref="IssueCategory"/>: a category is a judgement about the case and
/// belongs to the assessor; this is a fact about how the assessment was made, and asking the
/// thing being audited to grade its own working is how a self-reported confidence score comes to
/// mean nothing.
///
/// Each of the three has cost this project a wrong conclusion at least once. A finding resting on
/// a quotation that is not in its own evidence cleared a check that should have failed. A
/// requirement whose pack reached no passage read as "the file says nothing" when the file said
/// plenty and ranking had dropped it. A canonical path resolving to nothing read as an absent
/// fact when the extraction had lost the section.
/// </summary>
public sealed record CloserLook(bool LowConfidence, bool PartialCoverage, bool UnreadableSource)
{
    /// <summary>Nothing to flag.</summary>
    public static readonly CloserLook None = new(false, false, false);

    /// <summary>The design's labels, so a filter rail and a finding agree on the words.</summary>
    public const string LowConfidenceLabel = "Low agent confidence";
    public const string PartialCoverageLabel = "Partial coverage";
    public const string UnreadableSourceLabel = "Unreadable source";

    /// <summary>True when at least one flag is set.</summary>
    public bool Any => LowConfidence || PartialCoverage || UnreadableSource;

    /// <summary>The flags that are set, by their labels, in a fixed order.</summary>
    public IReadOnlyList<string> Labels
    {
        get
        {
            var labels = new List<string>(3);

            if (LowConfidence)
            {
                labels.Add(LowConfidenceLabel);
            }

            if (PartialCoverage)
            {
                labels.Add(PartialCoverageLabel);
            }

            if (UnreadableSource)
            {
                labels.Add(UnreadableSourceLabel);
            }

            return labels;
        }
    }

    /// <summary>
    /// Reads the flags off one assessed requirement.
    ///
    /// <paramref name="passages"/> is everything retrieval returned, whether or not ranking kept
    /// it, and <paramref name="facts"/> is every canonical path the group asked the model for.
    /// Both are needed: a requirement can be thinly covered because nothing was found, or because
    /// what was found was evicted, and those are different problems with the same symptom.
    /// </summary>
    public static CloserLook From(
        GroupFinding? finding,
        IReadOnlyList<RecordedFact> facts,
        IReadOnlyList<RecordedPassage> passages)
    {
        var inPack = passages.Count(p => p.InPack);

        var lowConfidence =
            finding is not null
            && (finding.Confidence != CitationConfidence.Verified
                || finding is { ComparisonPerformed: false, Discrepancies.Count: 0 });

        var partialCoverage =
            (passages.Count > 0 && inPack == 0)
            || (finding?.EvidenceShortfall is { Length: > 0 })
            || (finding is not null && !finding.ComparisonPerformed);

        // A group that deliberately searches for nothing is not short of evidence; one that
        // searched and reached nothing is.
        if (passages.Count == 0 && finding is not null && !finding.ComparisonPerformed)
        {
            partialCoverage = true;
        }

        var unreadableSource = facts.Any(f => !f.Found);

        return new CloserLook(lowConfidence, partialCoverage, unreadableSource);
    }
}
