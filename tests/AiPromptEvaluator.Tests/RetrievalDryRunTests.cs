using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Finding a dead section hint without spending a run.
///
/// <b>The defect is silent by construction and has now happened twice.</b> A declared evidence
/// section that matches no retrieved passage reserves nothing and changes nothing; three hints
/// naming "Residency" and "Contact Address" did that once, and six of ten newly added hints did it
/// again — after a test was written specifically to stop it recurring.
///
/// That test checks a hint's string appears in a document of a category the group searches, which
/// proves the hint <i>could</i> match and cannot prove any query <i>retrieves</i> the chunk holding
/// it. Six hints passed it while firing on nothing. Only executing the searches answers the
/// question, and retrieval is deterministic and needs no model, so executing them is cheap.
/// </summary>
public class RetrievalDryRunTests
{
    private readonly ITestOutputHelper _output;

    public RetrievalDryRunTests(ITestOutputHelper output) => _output = output;

    // ── the defect it exists for ──────────────────────────────────────────────

    /// <summary>
    /// <b>The exact hole in the existing test, demonstrated.</b> The hint's string is in the
    /// corpus, in a category the group searches — so a corpus-level check passes it — but no
    /// query retrieves the passage that carries it. The dry run says so; existence checking
    /// cannot.
    /// </summary>
    [Fact]
    public async Task AHintWhoseChunkNoQueryRetrievesIsReportedDead()
    {
        var search = new FakeSearch
        {
            // What the searches return: category B, but not the cash-flow table.
            ["income"] = [Passage("Fact Find.md", "B", "Employment details and job title")],
        };

        var plan = Plan(Group("G1.1", "income", ["B"], sections: ["Total Monthly Disposable Income"]));

        var report = await new RetrievalDryRun(search, new AppSettings()).RunAsync([plan]);

        _output.WriteLine(report.Format());

        var dead = Assert.Single(report.DeadSections);
        Assert.Contains("Total Monthly Disposable Income", dead);
        Assert.Contains("G1.1", dead);
    }

    [Fact]
    public async Task AHintTheSearchActuallyReturnsIsNotReported()
    {
        var search = new FakeSearch
        {
            ["income"] =
            [
                Passage("Fact Find.md", "B", "Total Monthly Disposable Income | £ -288.00"),
            ],
        };

        var plan = Plan(Group("G1.1", "income", ["B"], sections: ["Total Monthly Disposable Income"]));

        var report = await new RetrievalDryRun(search, new AppSettings()).RunAsync([plan]);

        Assert.Empty(report.DeadSections);
        Assert.Empty(report.EmptyGroups);
    }

    /// <summary>
    /// A query that searched for something and did not find it. Distinct from a dead hint: the
    /// hint asks for part of a document, the signal asks whether the answer arrived at all.
    /// </summary>
    [Fact]
    public async Task AQueryWhoseExpectedSignalsNeverArriveIsReported()
    {
        var search = new FakeSearch
        {
            ["charges"] = [Passage("Report.md", "I", "General information about the plan")],
        };

        var plan = Plan(Group("G7.1", "charges", ["I"], signals: ["reduction in yield", "RIY"]));

        var report = await new RetrievalDryRun(search, new AppSettings()).RunAsync([plan]);

        var missed = Assert.Single(report.MissedSignals);
        Assert.Contains("Q-G7.1", missed);
    }

    [Fact]
    public async Task AGroupThatRetrievesNothingIsTheLoudestFailure()
    {
        var plan = Plan(Group("G9.9", "nothing matches this", ["G"]));

        var report = await new RetrievalDryRun(new FakeSearch(), new AppSettings()).RunAsync([plan]);

        Assert.Equal(["CHK-999 G9.9"], report.EmptyGroups);
    }

    // ── it must describe the pack an assessor would actually get ──────────────

    /// <summary>
    /// A dry run that assembled the pack differently would report on a pack no assessor ever
    /// sees, which is worse than not reporting at all. Same cap, same ranking.
    /// </summary>
    [Fact]
    public async Task ThePackObeysTheConfiguredCap()
    {
        var search = new FakeSearch
        {
            ["wide"] = [.. Enumerable.Range(0, 40)
                .Select(i => Passage($"doc{i:00}.md", "B", $"passage {i}", 0.9 - (i * 0.001)))],
        };

        var plan = Plan(Group("G1.1", "wide", ["B"]));

        var capped = await new RetrievalDryRun(
            search, new AppSettings { MaxPassagesPerGroup = 12 }).RunAsync([plan]);
        var wider = await new RetrievalDryRun(
            search, new AppSettings { MaxPassagesPerGroup = 24 }).RunAsync([plan]);
        var unbounded = await new RetrievalDryRun(
            search, new AppSettings { MaxPassagesPerGroup = 0 }).RunAsync([plan]);

        Assert.Equal(12, capped.Groups[0].PackSize);
        Assert.Equal(24, wider.Groups[0].PackSize);
        Assert.Equal(40, unbounded.Groups[0].PackSize);

        Assert.Equal(1, capped.GroupsAtCap);
        Assert.Equal(0, unbounded.GroupsAtCap);
    }

    /// <summary>
    /// The same de-duplication the runner applies, or the reported hit and pack counts would not
    /// be the ones a run produces.
    /// </summary>
    [Fact]
    public async Task TheSameChunkFromTwoQueriesIsCountedOnceInThePack()
    {
        var shared = Passage("Fact Find.md", "B", "Total Net Monthly Income | £1,430.00");

        var search = new FakeSearch { ["one"] = [shared], ["two"] = [shared] };

        var group = Group("G1.4", "one", ["B"]);
        group.Retrieval.Queries.Add(new PlannedQuery
        {
            Id = "Q-second",
            Text = "two",
            Side = "Evidence",
            TargetCategories = ["B"],
        });

        var report = await new RetrievalDryRun(search, new AppSettings()).RunAsync([Plan(group)]);

        Assert.Equal(2, report.Searches);
        Assert.Equal(2, report.Hits);
        Assert.Equal(1, report.Groups[0].PackSize);
    }

    /// <summary>An Assertion query resolves the stored model and must not be searched for.</summary>
    [Fact]
    public async Task AssertionQueriesAreNotSearched()
    {
        var group = Group("G1.1", "evidence side", ["B"]);
        group.Retrieval.Queries.Add(new PlannedQuery
        {
            Id = "Q-assertion",
            Text = "assertion side",
            Side = "Assertion",
            TargetCategories = ["I"],
        });

        var search = new FakeSearch { ["evidence side"] = [Passage("f.md", "B", "text")] };

        var report = await new RetrievalDryRun(search, new AppSettings()).RunAsync([Plan(group)]);

        Assert.Equal(1, report.Searches);
        Assert.DoesNotContain("assertion side", search.Asked);
    }

    // ── the report a reader acts on ───────────────────────────────────────────

    [Fact]
    public async Task ACleanRunSaysSoRatherThanPrintingNothing()
    {
        var search = new FakeSearch { ["ok"] = [Passage("f.md", "B", "Residency Status | Tenant")] };
        var plan = Plan(Group("G1.1", "ok", ["B"], sections: ["Residency Status"]));

        var report = await new RetrievalDryRun(search, new AppSettings()).RunAsync([plan]);

        Assert.Contains("every declared section matched", report.Format());
    }

    [Fact]
    public async Task TheReportNamesTheCheckAndGroupOfEveryProblem()
    {
        var search = new FakeSearch { ["q"] = [Passage("f.md", "B", "unrelated")] };
        var plan = Plan(Group("G5.5", "q", ["B"], sections: ["LIQUIDATED"], signals: ["LIQUIDATED"]));

        var text = (await new RetrievalDryRun(search, new AppSettings()).RunAsync([plan])).Format();

        _output.WriteLine(text);

        Assert.Contains("CHK-999 G5.5: LIQUIDATED", text);
        Assert.Contains("Declared sections matching no retrieved passage (1)", text);
        Assert.Contains("Queries whose expected signals never arrived (1)", text);
    }

    // ──────────────────────────────────────────────

    private static CheckQueryPlan Plan(params PlanQueryGroup[] groups) =>
        new()
        {
            CheckId = "CHK-999",
            CheckName = "Dry run",
            QueryGroups = [.. groups],
        };

    private static PlanQueryGroup Group(
        string id,
        string queryText,
        string[] categories,
        string[]? sections = null,
        string[]? signals = null) =>
        new()
        {
            GroupId = id,
            Requirement = "test",
            Retrieval = new PlanRetrieval
            {
                EvidenceSections = [.. sections ?? []],
                Queries =
                [
                    new PlannedQuery
                    {
                        Id = "Q-" + id,
                        Text = queryText,
                        Side = "Evidence",
                        TargetCategories = [.. categories],
                        ExpectSignals = [.. signals ?? []],
                    },
                ],
            },
            Declares = new PlanDeclares { EvidenceCategories = [.. categories] },
        };

    private static CaseDocumentSearchMatch Passage(
        string document, string category, string text, double score = 0.7) =>
        new(text, "CASE-1", 99, document, category, category, score);

    /// <summary>Returns whatever was registered against the query text, and nothing otherwise.</summary>
    private sealed class FakeSearch : ICaseDocumentSearchService
    {
        private readonly Dictionary<string, IReadOnlyList<CaseDocumentSearchMatch>> _byText = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Asked { get; } = [];

        public IReadOnlyList<CaseDocumentSearchMatch> this[string text]
        {
            set => _byText[text] = value;
        }

        public Task<IReadOnlyList<CaseDocumentSearchMatch>> SearchAsync(
            string searchText, CancellationToken cancellationToken = default) =>
            SearchAsync(searchText, null, cancellationToken);

        public Task<IReadOnlyList<CaseDocumentSearchMatch>> SearchAsync(
            string searchText,
            IReadOnlyCollection<string>? categoryCodes,
            CancellationToken cancellationToken = default,
            int? resultsPerCall = null)
        {
            Asked.Add(searchText);

            return Task.FromResult(
                _byText.TryGetValue(searchText, out var hits) ? hits : []);
        }
    }
}
