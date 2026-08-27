using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The searched extract that can be downloaded from a dry run or an archived check run.
///
/// The extract answers the two questions a thin finding raises — what did the search return, and did
/// the passage carrying the fact reach the pack — so the things worth pinning are that the passage
/// body comes out verbatim (a reader is checking wording and a section-hint match against exactly
/// what the ranking saw), that a run's evicted passages are shown and marked (that is the record of
/// evidence not reaching the assessor), and that a group's failing hints travel with its passages.
/// </summary>
public class RetrievalExtractTests
{
    // ── dry run ───────────────────────────────────────────────────────────────

    [Fact]
    public void TheDryRunExtractRendersEachPassageBodyVerbatim()
    {
        var report = DryRun(GroupResult("CHK-001", "G1.1",
            pack: [Passage("Fact Find.md", "B", "Total Monthly Disposable Income | £-288.00")]));

        var text = RetrievalExtract.FromDryRun(report);

        Assert.Contains("CHK-001 G1.1", text);
        Assert.Contains("Fact Find.md [B]", text);
        Assert.Contains("Total Monthly Disposable Income | £-288.00", text);
    }

    [Fact]
    public void TheDryRunExtractCarriesAGroupsFailingHintsBesideItsPassages()
    {
        var report = DryRun(GroupResult("CHK-003", "G3.6",
            pack: [Passage("Risk profile.md", "D", "Agreed risk level 5")],
            deadSections: ["ATR Wording"]));

        var text = RetrievalExtract.FromDryRun(report);

        Assert.Contains("Section hints matching no retrieved passage", text);
        Assert.Contains("ATR Wording", text);
    }

    [Fact]
    public void ADryRunGroupThatRetrievedNothingSaysSo()
    {
        var report = DryRun(GroupResult("CHK-002", "G2.2", pack: []));

        Assert.Contains("The pack is empty", RetrievalExtract.FromDryRun(report));
    }

    // ── archived run ──────────────────────────────────────────────────────────

    [Fact]
    public void TheRunExtractShowsInPackPassagesAndMarksEvictedOnes()
    {
        var run = Run(RecordedGroup("CHK-007", "G7.4",
        [
            new RecordedPassage("Q7.4.1", "existing charges", "I", "Report.md", "I",
                "Recommendations", 0.80, "Standard Life 0.52%", InPack: true, Ordinal: 1),
            new RecordedPassage("Q7.4.3", "provider AMC", "E", "Zurich.pdf", "E",
                "Provider", 0.40, "annual management charge 0.75%", InPack: false, Ordinal: 2),
        ]));

        var text = RetrievalExtract.FromRun(run);

        Assert.Contains("Standard Life 0.52%", text);
        Assert.Contains("found by Q7.4.1", text);
        Assert.Contains("[evicted]", text);
        Assert.Contains("annual management charge 0.75%", text);
    }

    [Fact]
    public void TheRunExtractReportsAQueryThatReturnedNothing()
    {
        var group = RecordedGroup("CHK-005", "G5.5", []) with
        {
            Queries = [new RecordedQuery("Q5.5.3", "liquidation order", "F", 0)],
        };

        var text = RetrievalExtract.FromRun(Run(group));

        Assert.Contains("Queries that returned nothing", text);
        Assert.Contains("Q5.5.3", text);
    }

    // ──────────────────────────────────────────────

    private static RetrievalDryRun.Report DryRun(params RetrievalDryRun.GroupResult[] groups) =>
        new([.. groups], new AppSettings());

    private static RetrievalDryRun.GroupResult GroupResult(
        string checkId,
        string groupId,
        IReadOnlyList<CaseDocumentSearchMatch> pack,
        IReadOnlyList<string>? deadSections = null) =>
        new(checkId, groupId, "test requirement",
            Searches: 1,
            Hits: pack.Count,
            PackSize: pack.Count,
            CategoriesReached: [.. pack.Select(p => p.CategoryCode).Distinct()],
            DeadSections: deadSections ?? [],
            EvictedSections: [],
            MissedSignals: [],
            UnresolvedPaths: [],
            Pack: pack);

    private static CaseDocumentSearchMatch Passage(string document, string category, string text) =>
        new(text, "CASE-1", 99, document, category, category, 0.7);

    private static RecordedGroup RecordedGroup(
        string checkId, string groupId, IReadOnlyList<RecordedPassage> passages) =>
        new(checkId, groupId, "test requirement",
            Facts: [],
            Queries: [],
            Passages: passages,
            SystemPrompt: string.Empty,
            UserPrompt: string.Empty,
            RawResponse: string.Empty,
            Finding: null);

    private static CheckRunRecord Run(params RecordedGroup[] groups) =>
        new("run-1", "CASE-1", 99, "test-model",
            StartedAt: DateTimeOffset.UnixEpoch,
            CompletedAt: DateTimeOffset.UnixEpoch,
            Fingerprint: string.Empty,
            CacheMode: string.Empty,
            CanonicalModelJson: null,
            CanonicalModelId: string.Empty,
            SchemaVersion: "1.0",
            SourceDocuments: [],
            Checks:
            [
                new RecordedCheck("CHK", "Check", "PotentialConcern", string.Empty,
                    SearchesRun: 0, PassagesRetrieved: 0, CanonicalPathsResolved: 0,
                    CanonicalPathsMissing: 0, Elapsed: TimeSpan.Zero, Usage: TokenUsage.Empty,
                    Error: null, Groups: [.. groups]),
            ]);
}
