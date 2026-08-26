using System.Collections.Concurrent;

namespace AiPromptEvaluator;

/// <summary>
/// Collects what a run saw, so it can be written to the archive once the run has finished.
///
/// <b>In memory during the run, one transaction at the end.</b> Checks and their requirements
/// both fan out — a run has as many assessments in flight as <see cref="AppSettings.MaxParallelChecks"/>
/// times its request budget allows — and a store that wrote per group would serialise all of
/// them behind one SQLite writer. Buffering costs a few megabytes for a ten-check run and puts
/// no lock on the assessment path at all.
///
/// It is deliberately not a <c>PromptLogWriter</c>. The log is written as the run goes so that
/// a run which crashes still leaves a transcript; this is written at the end so that a run which
/// crashes does not leave a half-run in the archive that a report would present as complete.
/// The two failure modes want opposite answers, which is why there are two mechanisms.
/// </summary>
public sealed class CheckRunRecorder
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Entry>> _groups =
        new(StringComparer.OrdinalIgnoreCase);

    public CheckRunRecorder(string runId) => RunId = runId;

    /// <summary>A recorder for a run identified by when it started, so the id sorts and reads.</summary>
    public static CheckRunRecorder ForRun(DateTimeOffset startedAt, string caseReference) =>
        new($"{startedAt:yyyyMMdd-HHmmss}-{Sanitise(caseReference)}");

    /// <summary>The id this run is archived under.</summary>
    public string RunId { get; }

    private sealed class Entry
    {
        public string Requirement = string.Empty;
        public List<RecordedFact> Facts = [];
        public List<RecordedQuery> Queries = [];
        public List<RecordedPassage> Passages = [];
        public string SystemPrompt = string.Empty;
        public string UserPrompt = string.Empty;
        public string RawResponse = string.Empty;
        public GroupFinding? Finding;
    }

    private Entry EntryFor(string checkId, string groupId) =>
        _groups
            .GetOrAdd(checkId, _ => new ConcurrentDictionary<string, Entry>(StringComparer.OrdinalIgnoreCase))
            .GetOrAdd(groupId, _ => new Entry());

    /// <summary>
    /// What one requirement retrieved, recorded when the pack is assembled rather than when it
    /// is sent.
    ///
    /// <paramref name="ranked"/> is the pack the assessor will see; <paramref name="retrieved"/>
    /// is everything the searches returned. Both are kept, and the difference between them is
    /// the point: a passage evicted in ranking never appears in any prompt, so the prompt log
    /// cannot show it, and it is the first thing anyone asks about when a plan asked for
    /// something the finding does not mention.
    /// </summary>
    public void RecordEvidence(
        string checkId,
        string groupId,
        string requirement,
        IReadOnlyList<CanonicalFragment> facts,
        IReadOnlyList<(PlannedQuery Query, int Hits)> executed,
        IReadOnlyList<(PlannedQuery Query, CaseDocumentSearchMatch Hit)> retrieved,
        IReadOnlyList<CaseDocumentSearchMatch> ranked)
    {
        var inPack = ranked
            .Select(CheckPlanRunner.DeduplicationKey)
            .ToHashSet(StringComparer.Ordinal);

        var entry = EntryFor(checkId, groupId);
        var ordinal = 0;

        lock (entry)
        {
            entry.Requirement = requirement;
            entry.Facts = facts.Select(f => new RecordedFact(f.Path, f.Json, f.Found)).ToList();
            entry.Queries = executed
                .Select(e => new RecordedQuery(
                    e.Query.Id, e.Query.Text, string.Join(", ", e.Query.TargetCategories), e.Hits))
                .ToList();
            entry.Passages = retrieved
                .Select(r => new RecordedPassage(
                    QueryId: r.Query.Id,
                    QueryText: r.Query.Text,
                    TargetCategories: string.Join(", ", r.Query.TargetCategories),
                    DocumentName: r.Hit.DocumentName,
                    CategoryCode: r.Hit.CategoryCode,
                    CategoryName: r.Hit.CategoryName,
                    Score: r.Hit.Score,
                    Text: r.Hit.SearchedText,
                    InPack: inPack.Contains(CheckPlanRunner.DeduplicationKey(r.Hit)),
                    Ordinal: ordinal++))
                .ToList();
        }
    }

    /// <summary>What one requirement was asked, and what came back, verbatim.</summary>
    public void RecordResponse(
        string checkId,
        string groupId,
        string systemPrompt,
        string userPrompt,
        string rawResponse,
        GroupFinding? finding)
    {
        var entry = EntryFor(checkId, groupId);

        lock (entry)
        {
            entry.SystemPrompt = systemPrompt;
            entry.UserPrompt = userPrompt;
            entry.RawResponse = rawResponse;
            entry.Finding = finding;
        }
    }

    /// <summary>
    /// The run as a record, ready to save.
    ///
    /// The findings are passed in rather than accumulated here: the check-level outcome is
    /// computed from the group findings by <see cref="CheckFinding.FromGroups"/> and must not be
    /// recomputed differently on the way to the archive, or the report and the run would
    /// disagree about the same run.
    /// </summary>
    public CheckRunRecord Build(
        string caseReference,
        int tenantId,
        string chatModel,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt,
        RunFingerprint fingerprint,
        PromptCacheBypass bypass,
        CanonicalModelDocument? model,
        IReadOnlyList<CheckFinding> findings,
        IReadOnlyList<AssessmentCheck>? sheet = null)
    {
        var definitions = (sheet ?? [])
            .GroupBy(c => c.CheckId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var checks = findings.Select(finding =>
        {
            var recorded = _groups.TryGetValue(finding.CheckId, out var entries)
                ? entries
                : new ConcurrentDictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

            // Ordered by the finding's groups, not by the dictionary: the report reads in plan
            // order, and a hash order would shuffle the requirements of every check.
            var groups = finding.Groups
                .Select(group =>
                {
                    var entry = recorded.TryGetValue(group.GroupId, out var found) ? found : new Entry();

                    lock (entry)
                    {
                        return new RecordedGroup(
                            CheckId: finding.CheckId,
                            GroupId: group.GroupId,
                            Requirement: group.Requirement ?? entry.Requirement,
                            Facts: entry.Facts,
                            Queries: entry.Queries,
                            Passages: entry.Passages,
                            SystemPrompt: entry.SystemPrompt,
                            UserPrompt: entry.UserPrompt,
                            RawResponse: entry.RawResponse,
                            Finding: group)
                        {
                            IssueCategories = IssueCategory.Clean(group.IssueCategories),

                            // Computed here, at the moment of filing, from the evidence as it
                            // actually was. Deriving it later from the report would be deriving
                            // it from whatever survived into the report, which is the mistake
                            // the archive exists to stop.
                            CloserLook = CloserLook.From(group, entry.Facts, entry.Passages),
                        };
                    }
                })
                .ToList();

            definitions.TryGetValue(finding.CheckId, out var definition);

            return new RecordedCheck(
                CheckId: finding.CheckId,
                CheckName: finding.CheckName,
                Outcome: finding.ParsedOutcome.ToString(),
                Summary: finding.Summary,
                SearchesRun: finding.SearchesRun,
                PassagesRetrieved: finding.PassagesRetrieved,
                CanonicalPathsResolved: finding.CanonicalPathsResolved,
                CanonicalPathsMissing: finding.CanonicalPathsMissing,
                Elapsed: finding.Elapsed,
                Usage: finding.Usage,
                Error: finding.Error,
                Groups: groups)
            {
                Narrative = definition?.Prompt ?? string.Empty,
                RegulatoryBasis = definition?.RegulatoryBasis ?? string.Empty,
                AppliesTo = definition?.AppliesTo ?? string.Empty,
            };
        }).ToList();

        return new CheckRunRecord(
            RunId: RunId,
            CaseReference: caseReference,
            TenantId: tenantId,
            ChatModel: chatModel,
            StartedAt: startedAt,
            CompletedAt: completedAt,
            Fingerprint: fingerprint.Format(),
            CacheMode: bypass.Describe(),
            CanonicalModelJson: model?.Json,
            CanonicalModelId: model?.ModelId ?? string.Empty,
            SchemaVersion: model?.SchemaVersion ?? string.Empty,
            SourceDocuments: model?.SourceDocuments ?? [],
            Checks: checks);
    }

    private static string Sanitise(string value)
    {
        var cleaned = new string(value
            .Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-')
            .ToArray());

        return string.IsNullOrWhiteSpace(cleaned) ? "case" : cleaned;
    }
}
