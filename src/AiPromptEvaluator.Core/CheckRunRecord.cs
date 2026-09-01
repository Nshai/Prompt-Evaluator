namespace AiPromptEvaluator;

/// <summary>
/// One passage a plan's search actually returned, kept with the query that asked for it.
///
/// <b>The query is stored beside the passage rather than inferred from it.</b> A group runs
/// several searches and their results are merged, de-duplicated and ranked before an assessor
/// sees them, so by the time a passage is in the pack there is no way back to the wording that
/// found it. That is the question a reader of a finding asks first — <em>what did it look
/// for?</em> — and until now the only answer was to open the plan and guess.
/// </summary>
/// <param name="InPack">
/// False for a passage that was retrieved and then dropped in ranking. Kept, because an evicted
/// passage is evidence about the run rather than evidence about the case: three checks reached
/// the Fact Find in zero groups out of nineteen while every pack sat exactly at the cap, and
/// nothing on the report said so. A row that was retrieved and discarded is the record of that.
/// </param>
public sealed record RecordedPassage(
    string QueryId,
    string QueryText,
    string TargetCategories,
    string DocumentName,
    string CategoryCode,
    string CategoryName,
    double Score,
    string Text,
    bool InPack,
    int Ordinal);

/// <summary>What the canonical model gave a group, by the path the plan cited.</summary>
public sealed record RecordedFact(string Path, string Json, bool Found);

/// <summary>
/// One search a requirement ran, whether or not it found anything.
///
/// <b>A query that returned nothing is the part no other record keeps.</b> Passages are stored
/// as they arrive, so a search with no hits leaves no row and is indistinguishable from a search
/// that never ran — which makes a requirement assessed from the report alone look identical to
/// one whose every search came back empty. Those are opposite findings: the first has no
/// evidence side by design, the second has one and reached nothing.
/// </summary>
public sealed record RecordedQuery(string QueryId, string Text, string TargetCategories, int Hits);

/// <summary>
/// One requirement's complete record: what the report asserted, what was searched for, what
/// came back, what the assessor was sent, and what it answered.
///
/// The raw response is kept alongside the parsed finding because they are different claims.
/// The finding is what the app understood; the response is what the model said. Where those
/// two disagree — a rewritten requirement, an outcome word the parser did not recognise, a
/// citation the verifier could not trace — only the raw text can settle it.
/// </summary>
public sealed record RecordedGroup(
    string CheckId,
    string GroupId,
    string Requirement,
    IReadOnlyList<RecordedFact> Facts,
    IReadOnlyList<RecordedQuery> Queries,
    IReadOnlyList<RecordedPassage> Passages,
    string SystemPrompt,
    string UserPrompt,
    string RawResponse,
    GroupFinding? Finding)
{
    /// <summary>
    /// What kind of problem the assessor said this is, from <see cref="IssueCategory"/>.
    ///
    /// Stored on the row as well as inside the finding JSON, because that is what makes it a
    /// column a reader can filter on. Denormalised on purpose: "every data inconsistency in
    /// this case" is the question the vocabulary exists to answer, and a blob that has to be
    /// parsed first cannot answer it.
    /// </summary>
    public IReadOnlyList<string> IssueCategories { get; init; } = [];

    /// <summary>
    /// Why this finding wants a second pair of eyes, counted from the run rather than asked of
    /// the model. Computed when the run is filed, so it is fixed at the moment the evidence was
    /// what it was.
    /// </summary>
    public CloserLook CloserLook { get; init; } = CloserLook.None;

    /// <summary>
    /// How far this finding's own working can be relied on, scored from the run when it was
    /// filed. Recomputed on render from the same definition, so the archived number and the
    /// printed breakdown cannot describe different things.
    /// </summary>
    public AgentCoverage AgentCoverage => FindingCoverage.Agent(this);

    /// <summary>How much of what the plan asked for reached the assessor, source by source.</summary>
    public EvidenceCoverage EvidenceCoverage => FindingCoverage.Evidence(this);
}

/// <summary>One check's findings and everything the requirements underneath it saw.</summary>
public sealed record RecordedCheck(
    string CheckId,
    string CheckName,
    string Outcome,
    string Summary,
    int SearchesRun,
    int PassagesRetrieved,
    int CanonicalPathsResolved,
    int CanonicalPathsMissing,
    TimeSpan Elapsed,
    TokenUsage Usage,
    string? Error,
    IReadOnlyList<RecordedGroup> Groups)
{
    /// <summary>
    /// What this check was asked to establish, in the words of the check sheet.
    ///
    /// Archived rather than looked up when a report is rendered. The sheet is edited between
    /// runs — that is the point of it — so a report built next month from a run made today
    /// would otherwise print the question as it is worded then, beside an answer given to a
    /// different one.
    /// </summary>
    public string Narrative { get; init; } = string.Empty;

    /// <summary>The rules this check is grounded in, as the sheet cites them.</summary>
    public string RegulatoryBasis { get; init; } = string.Empty;

    /// <summary>The circumstances the check applies to.</summary>
    public string AppliesTo { get; init; } = string.Empty;

    /// <summary>Every citation the requirements under this check made, in plan order.</summary>
    public IEnumerable<FindingCitation> Citations =>
        Groups.Where(g => g.Finding is not null).SelectMany(g => g.Finding!.Citations);
}

/// <summary>
/// A finished run, reassembled from the archive.
///
/// This is what the HTML report is built from, and it is read back out of the database rather
/// than held in memory from the run that produced it. That is the point of storing it: a report
/// can be produced days later, from a different session, for a run nobody is still watching.
/// </summary>
public sealed record CheckRunRecord(
    string RunId,
    string CaseReference,
    int TenantId,
    string ChatModel,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Fingerprint,
    string CacheMode,
    string? CanonicalModelJson,
    string CanonicalModelId,
    string SchemaVersion,
    IReadOnlyList<string> SourceDocuments,
    IReadOnlyList<RecordedCheck> Checks)
{
    /// <summary>How long the run took, or zero for one that never recorded a finish.</summary>
    public TimeSpan Duration =>
        CompletedAt is { } done ? done - StartedAt : TimeSpan.Zero;

    /// <summary>Every requirement across every check, in check order.</summary>
    public IEnumerable<RecordedGroup> AllGroups => Checks.SelectMany(c => c.Groups);

    /// <summary>How many requirements raised each issue category, in vocabulary order.</summary>
    public IReadOnlyList<(string Category, int Count)> IssueCategoryCounts =>
        IssueCategory.All
            .Select(c => (Category: c, Count: AllGroups.Count(g => g.IssueCategories.Contains(c))))
            .ToList();

    /// <summary>How many requirements carry each closer-look flag, in a fixed order.</summary>
    public IReadOnlyList<(string Flag, int Count)> CloserLookCounts =>
    [
        (CloserLook.LowConfidenceLabel, AllGroups.Count(g => g.CloserLook.LowConfidence)),
        (CloserLook.PartialCoverageLabel, AllGroups.Count(g => g.CloserLook.PartialCoverage)),
        (CloserLook.UnreadableSourceLabel, AllGroups.Count(g => g.CloserLook.UnreadableSource)),
    ];

    /// <summary>Checks whose outcome parses to <paramref name="outcome"/>.</summary>
    public int Count(CheckOutcome outcome) =>
        Checks.Count(c => Enum.TryParse<CheckOutcome>(c.Outcome, ignoreCase: true, out var parsed)
                          && parsed == outcome);
}

/// <summary>One row of the run list, for a picker that must not read every finding to fill itself.</summary>
public sealed record CheckRunSummary(
    string RunId,
    string CaseReference,
    int TenantId,
    string ChatModel,
    DateTimeOffset StartedAt,
    int CheckCount,
    string Headline)
{
    /// <summary>How the run reads in a list.</summary>
    public override string ToString() =>
        $"{StartedAt:yyyy-MM-dd HH:mm}  {CaseReference}  {ChatModel}  —  {Headline}";
}
