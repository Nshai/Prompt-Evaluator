using System.Text.Json;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The archive holds what a run saw, and a report is built from what it holds.
///
/// <b>Everything scored in this project has been scored from a rendered report, and the report
/// prints only what a group carried into its verdict.</b> That is how a passage retrieved and
/// then evicted in ranking became invisible — it appears in no prompt, so the prompt log cannot
/// show it either — and how a requirement that raised a difference and passed came to look
/// identical to one that found nothing. Both were diagnosed months later by reading logs by hand.
///
/// So the round trip matters more than the schema: whatever survives a save and a load is what a
/// reader will ever be able to ask about the run.
/// </summary>
public class CheckRunArchiveTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"check-runs-{Guid.NewGuid():n}.db");

    public void Dispose()
    {
        // The pooled connection has to be dropped or the file stays locked on Windows.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (File.Exists(_path))
        {
            File.Delete(_path);
        }

        GC.SuppressFinalize(this);
    }

    private CheckRunStore Store() => new(_path);

    // ── the round trip ────────────────────────────────────────────────────────

    [Fact]
    public async Task ARunComesBackAsItWasSaved()
    {
        var store = Store();
        await store.SaveAsync(Run());

        var loaded = await store.LoadAsync("RUN-1");

        Assert.NotNull(loaded);
        Assert.Equal("ABC-99", loaded!.CaseReference);
        Assert.Equal(99, loaded.TenantId);
        Assert.Equal("claude-haiku", loaded.ChatModel);
        Assert.Equal("response cache bypassed (abc)", loaded.CacheMode);
        Assert.Equal(["Suitability Report.md"], loaded.SourceDocuments);

        var check = Assert.Single(loaded.Checks);
        Assert.Equal("CHK-001", check.CheckId);
        Assert.Equal(nameof(CheckOutcome.PotentialConcern), check.Outcome);
        Assert.Equal(2, check.Groups.Count);
    }

    /// <summary>
    /// The reason the run is stored at all: a report can be produced by a session that did not
    /// do the run, from a store on disk rather than from anything still in memory.
    /// </summary>
    [Fact]
    public async Task AnUnrelatedStoreOverTheSameFileReadsTheSameRun()
    {
        await Store().SaveAsync(Run());

        var loaded = await new CheckRunStore(_path).LoadAsync("RUN-1");

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.AllGroups.Count());
    }

    /// <summary>
    /// <b>The evidence nothing else keeps.</b> A passage that ranking discarded never reaches a
    /// prompt, so it is in no log; it is the first thing anyone asks about when a plan searched
    /// for something the finding does not mention.
    /// </summary>
    [Fact]
    public async Task PassagesEvictedInRankingSurviveWithTheReasonTheyWereFound()
    {
        await Store().SaveAsync(Run());

        var loaded = await Store().LoadAsync("RUN-1");
        var group = loaded!.Checks[0].Groups[0];

        Assert.Equal(2, group.Passages.Count);

        var kept = Assert.Single(group.Passages, p => p.InPack);
        var dropped = Assert.Single(group.Passages, p => !p.InPack);

        Assert.Equal("Fact Find.md", kept.DocumentName);
        Assert.Equal("Q1.4.1", kept.QueryId);
        Assert.Equal("income and expenditure", kept.QueryText);
        Assert.Equal("Research.md", dropped.DocumentName);
    }

    /// <summary>
    /// The raw response is kept beside the parsed finding because they are different claims.
    /// Where the two disagree — an outcome word the parser did not recognise, a rewritten
    /// requirement — only the raw text can settle it.
    /// </summary>
    [Fact]
    public async Task TheRawResponseAndTheParsedFindingAreBothKeptForEachCheckId()
    {
        await Store().SaveAsync(Run());

        var loaded = await Store().LoadAsync("RUN-1");
        var group = loaded!.Checks[0].Groups[0];

        Assert.Contains("\"outcome\"", group.RawResponse, StringComparison.Ordinal);
        Assert.NotNull(group.Finding);
        Assert.Equal("G1.4", group.Finding!.GroupId);
        Assert.Equal(nameof(CheckOutcome.PotentialConcern), group.Finding.Outcome);
        Assert.Contains("£1,200", group.Finding.Discrepancies[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task CanonicalFactsAreKeptWithTheirPathAndWhetherTheyResolved()
    {
        await Store().SaveAsync(Run());

        var loaded = await Store().LoadAsync("RUN-1");
        var facts = loaded!.Checks[0].Groups[0].Facts;

        Assert.Equal(2, facts.Count);
        Assert.True(facts[0].Found);
        Assert.Equal("/financialPosition/income", facts[0].Path);
        Assert.False(facts[1].Found);
    }

    /// <summary>Requirements read in plan order, not in whatever order the rows came back.</summary>
    [Fact]
    public async Task RequirementsComeBackInPlanOrder()
    {
        await Store().SaveAsync(Run());

        var loaded = await Store().LoadAsync("RUN-1");

        Assert.Equal(["G1.4", "G1.10"], loaded!.Checks[0].Groups.Select(g => g.GroupId));
    }

    /// <summary>
    /// A search that returned nothing leaves no passage behind, so it is stored in its own right.
    /// Without it, a requirement whose every search came back empty is indistinguishable from one
    /// that deliberately searched for nothing — and those are opposite findings.
    /// </summary>
    [Fact]
    public async Task SearchesThatFoundNothingAreArchivedAlongsideTheOnesThatDid()
    {
        await Store().SaveAsync(Run());

        var loaded = await Store().LoadAsync("RUN-1");
        var queries = loaded!.Checks[0].Groups[0].Queries;

        Assert.Equal(3, queries.Count);
        Assert.Equal(["Q1.4.1", "Q1.4.2", "Q1.4.3"], queries.Select(q => q.QueryId));

        var barren = Assert.Single(queries, q => q.Hits == 0);
        Assert.Equal("monthly outgoings", barren.Text);
        Assert.Equal("B", barren.TargetCategories);

        // The requirement that searched for nothing is a different thing again.
        Assert.Empty(loaded.Checks[0].Groups[1].Queries);
    }

    /// <summary>
    /// The check sheet is edited between runs — that is the point of it — so the question and the
    /// rule are copied into the archive when the run is filed. A report built next month from a
    /// run made today would otherwise print the question as it is worded then, beside an answer
    /// given to a different one.
    /// </summary>
    [Fact]
    public async Task TheCheckNarrativeAndRegulatoryBasisTravelWithTheRun()
    {
        await Store().SaveAsync(Run());

        var check = (await Store().LoadAsync("RUN-1"))!.Checks[0];

        Assert.Equal("Confirm the figures reconcile with the evidence on file.", check.Narrative);
        Assert.Equal("COBS 9.2.1R; PRIN 2.1.1R", check.RegulatoryBasis);
        Assert.Equal("All advised sales.", check.AppliesTo);
    }

    /// <summary>
    /// Both coverages are written as columns so a reviewer can ask "every finding under 60%"
    /// without reading every finding back — and both are recomputed from the same definition when
    /// a report is rendered, so the stored number and the printed breakdown cannot disagree.
    /// </summary>
    [Fact]
    public async Task BothCoveragesSurviveAndStillAgreeWithTheDefinitionThatWroteThem()
    {
        await Store().SaveAsync(Run());

        var group = (await Store().LoadAsync("RUN-1"))!.Checks[0].Groups[0];

        Assert.Equal(FindingCoverage.Agent(group).Score, group.AgentCoverage.Score);
        Assert.Equal(FindingCoverage.Evidence(group).Score, group.EvidenceCoverage.Score);
        Assert.InRange(group.EvidenceCoverage.Score, 0, 89);
    }

    // ── listing and replacing ────────────────────────────────────────────────

    [Fact]
    public async Task RunsForACaseAreListedNewestFirst()
    {
        var store = Store();
        await store.SaveAsync(Run("RUN-1", DateTimeOffset.Parse("2026-08-26T10:00:00+01:00")));
        await store.SaveAsync(Run("RUN-2", DateTimeOffset.Parse("2026-08-26T14:00:00+01:00")));

        var runs = await store.ListAsync("ABC-99", 99);

        Assert.Equal(["RUN-2", "RUN-1"], runs.Select(r => r.RunId));
        Assert.Contains("potential concern", runs[0].Headline, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What the report button means by "this case".</summary>
    [Fact]
    public async Task TheLatestRunIsTheMostRecentOne()
    {
        var store = Store();
        await store.SaveAsync(Run("RUN-1", DateTimeOffset.Parse("2026-08-26T10:00:00+01:00")));
        await store.SaveAsync(Run("RUN-2", DateTimeOffset.Parse("2026-08-26T14:00:00+01:00")));

        var latest = await store.LoadLatestAsync("ABC-99", 99);

        Assert.Equal("RUN-2", latest!.RunId);
    }

    [Fact]
    public async Task ACaseWithNoArchivedRunReportsNothingRatherThanThrowing()
    {
        Assert.Null(await Store().LoadLatestAsync("NOT-A-CASE", 99));
        Assert.Empty(await Store().ListAsync("NOT-A-CASE", 99));
        Assert.Null(await Store().LoadAsync("NOT-A-RUN"));
    }

    /// <summary>
    /// Re-saving replaces rather than merges. The one caller that does it is a run finishing
    /// after its own cancellation write, and a merge would leave the archive holding
    /// requirements from both attempts under one run id.
    /// </summary>
    [Fact]
    public async Task SavingTheSameRunIdTwiceReplacesItWholesale()
    {
        var store = Store();
        await store.SaveAsync(Run());

        var trimmed = Run() with
        {
            Checks = [Run().Checks[0] with { Groups = [Run().Checks[0].Groups[0]] }],
        };

        await store.SaveAsync(trimmed);

        var loaded = await store.LoadAsync("RUN-1");

        Assert.Single(loaded!.Checks[0].Groups);
        Assert.Single(await store.ListAsync("ABC-99", 99));
    }

    [Fact]
    public async Task DeletingARunRemovesItsEvidenceToo()
    {
        var store = Store();
        await store.SaveAsync(Run());

        Assert.True(await store.DeleteAsync("RUN-1"));
        Assert.Null(await store.LoadAsync("RUN-1"));
        Assert.False(await store.DeleteAsync("RUN-1"));
    }

    /// <summary>
    /// The default sits beside the canonical models, so neither half of a case's history needs
    /// configuring — and neither is inside the other, so pruning run history cannot take an
    /// extraction with it.
    /// </summary>
    [Fact]
    public void TheDefaultPathSitsBesideTheModelDatabaseWithoutBeingIt()
    {
        var settings = new AppSettings();

        var runs = settings.ResolveCheckRunDbPath();
        var models = settings.ResolveCanonicalModelDbPath();

        Assert.Equal(Path.GetDirectoryName(models), Path.GetDirectoryName(runs));
        Assert.NotEqual(models, runs);
        Assert.EndsWith("check-runs.db", runs, StringComparison.OrdinalIgnoreCase);
    }

    // ── fixtures ──────────────────────────────────────────────────────────────

    private static CheckRunRecord Run(string runId = "RUN-1", DateTimeOffset? startedAt = null)
    {
        var started = startedAt ?? DateTimeOffset.Parse("2026-08-26T10:00:00+01:00");

        var finding = new GroupFinding
        {
            GroupId = "G1.4",
            Requirement = "Income and expenditure figures are consistent with the evidence",
            ReportSays = "Client earns £300 per week net.",
            FileSays = "The fact find records net basic monthly income of £1,200.",
            Discrepancies = ["£300/week does not reconcile with £1,200/month."],
            Analysis = "A material mismatch on an income figure.",
            Outcome = nameof(CheckOutcome.PotentialConcern),
            Severity = "High",
            Citations =
            [
                new FindingCitation { PassageId = "P1", Source = "Fact Find.md", Quote = "£ 1,200.00" },
            ],
        };

        var groups = new List<RecordedGroup>
        {
            new(
                CheckId: "CHK-001",
                GroupId: "G1.4",
                Requirement: finding.Requirement!,
                Facts:
                [
                    new RecordedFact("/financialPosition/income", "\"£300 per week\"", true),
                    new RecordedFact("/financialPosition/expenditure", string.Empty, false),
                ],
                Queries:
                [
                    new RecordedQuery("Q1.4.1", "income and expenditure", "B", 1),
                    new RecordedQuery("Q1.4.2", "household budget", "B, G", 1),
                    new RecordedQuery("Q1.4.3", "monthly outgoings", "B", 0),
                ],
                Passages:
                [
                    new RecordedPassage(
                        "Q1.4.1", "income and expenditure", "B",
                        "Fact Find.md", "B", "Fact Find", 0.81,
                        "Basic Income | Employed | Monthly | £ 1,200.00", InPack: true, Ordinal: 0),
                    new RecordedPassage(
                        "Q1.4.2", "household budget", "B, G",
                        "Research.md", "G", "Research", 0.42,
                        "General planning assumptions", InPack: false, Ordinal: 1),
                ],
                SystemPrompt: "You are a financial services Quality Assurance assessor.",
                UserPrompt: "[G1.4] Income and expenditure...",
                RawResponse: JsonSerializer.Serialize(finding),
                Finding: finding),
            new(
                CheckId: "CHK-001",
                GroupId: "G1.10",
                Requirement: "Values expressed in different frequencies reconcile",
                Facts: [],
                Queries: [],
                Passages: [],
                SystemPrompt: "system",
                UserPrompt: "user",
                RawResponse: "{}",
                Finding: new GroupFinding { GroupId = "G1.10", Outcome = nameof(CheckOutcome.NoIssue) }),
        };

        return new CheckRunRecord(
            RunId: runId,
            CaseReference: "ABC-99",
            TenantId: 99,
            ChatModel: "claude-haiku",
            StartedAt: started,
            CompletedAt: started.AddMinutes(5),
            Fingerprint: "Chat claude-haiku · seed pinned",
            CacheMode: "response cache bypassed (abc)",
            CanonicalModelJson: """{"modelVersion":"1.0"}""",
            CanonicalModelId: "8a0120d90ea2",
            SchemaVersion: "1.0",
            SourceDocuments: ["Suitability Report.md"],
            Checks:
            [
                new RecordedCheck(
                    CheckId: "CHK-001",
                    CheckName: "Suitability completeness against client information",
                    Outcome: nameof(CheckOutcome.PotentialConcern),
                    Summary: "Two material discrepancies.",
                    SearchesRun: 4,
                    PassagesRetrieved: 2,
                    CanonicalPathsResolved: 1,
                    CanonicalPathsMissing: 1,
                    Elapsed: TimeSpan.FromSeconds(12),
                    Usage: new TokenUsage(1000, 200, 0, 50),
                    Error: null,
                    Groups: groups)
                {
                    Narrative = "Confirm the figures reconcile with the evidence on file.",
                    RegulatoryBasis = "COBS 9.2.1R; PRIN 2.1.1R",
                    AppliesTo = "All advised sales.",
                },
            ]);
    }
}
