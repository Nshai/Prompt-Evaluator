using System.IO;

using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Move 1 of the pipeline plan: <b>guarantee every populated canonical path a reader.</b>
///
/// The pipeline decides in advance, by hand, in ten JSON files, which of the report's claims may
/// be compared with which piece of evidence. Measured against two unrelated cases, about a fifth
/// of everything the extractor produces is selected by no group at all — extracted, validated,
/// stored, paid for, and read by nothing. That bucket is not empty: it contains a claim that a
/// Residence Nil Rate Band applies to a client the file records as a tenant, which absorbed three
/// prompt guards and a hint across eight runs while no prompt in any of them contained it.
///
/// <b>A ratchet, not a pass/fail gate.</b> The rule cannot simply be "every path has a reader"
/// today: it would be red on every build from the moment it was written, and a permanently red
/// test is one nobody reads. So the measurement is asserted against a recorded baseline. Coverage
/// may not fall, the breakdown is printed on every run so the next root to fix is always visible,
/// and closing one raises the baseline — which is the only thing that makes the number mean
/// anything.
/// </summary>
public class PathCoverageTests
{
    private readonly ITestOutputHelper _output;

    public PathCoverageTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The share of populated paths the shipped plans read, as last measured.
    ///
    /// <b>Raise this when a root is closed. Never lower it to make a build pass.</b> A fall means
    /// either a plan stopped selecting something it used to, or the extraction started populating
    /// a root no check asks about — and both are the defect this test exists to catch, arriving
    /// from opposite directions.
    /// </summary>
    private const double Baseline = 0.78;

    public static TheoryData<string, string> StoredModels => new()
    {
        { "Test-Case 1", Path.Combine("Test-Case 1", "Runtime-Logs", "latest", "Run-18") },
        { "Test-Case 2", Path.Combine("Test-Case 2", "Run-3") },
        { "Test-Case 3", Path.Combine("Test-case 3", "Run-3") },
    };

    [Theory]
    [MemberData(nameof(StoredModels))]
    public void EveryPopulatedPathHasAReader(string name, string runFolder)
    {
        var model = StoredModel(runFolder);
        var plans = Plans();

        if (model is null || plans.Count == 0)
        {
            _output.WriteLine($"Skipped: no stored model or plans for {name} in this working copy.");
            return;
        }

        var coverage = PathCoverage.Measure(model, plans);

        _output.WriteLine(name);
        _output.WriteLine(coverage.Describe());

        Assert.True(
            coverage.ReadFraction >= Baseline,
            $"{name}: path coverage fell to {coverage.ReadFraction:P1}, below the recorded "
            + $"{Baseline:P0}. Either a plan stopped selecting a path it used to, or the "
            + "extraction now populates a root no check reads. The breakdown above names the "
            + $"roots.{Environment.NewLine}{coverage.Describe()}");
    }

    /// <summary>
    /// The other direction, and the cheaper defect to fix: a path every group agrees to read that
    /// no case has ever held a value at is a plan citing a path the schema does not have. It costs
    /// nothing at run time — the accessor answers "not found", which is a legitimate result — and
    /// it means a requirement whose assertion side was authored against a typo has silently had no
    /// assertion side for as long as the plan has existed.
    /// </summary>
    [Fact]
    public void PathsNoStoredModelHasEverPopulatedAreReported()
    {
        var plans = Plans();
        var models = StoredModels
            .Select(row => StoredModel((string)row[1]))
            .Where(m => m is not null)
            .ToList();

        if (plans.Count == 0 || models.Count == 0)
        {
            _output.WriteLine("Skipped: no stored models or plans in this working copy.");
            return;
        }

        // Unpopulated on *every* case, not on one. A check that does not apply to a case is
        // supposed to find nothing there, and reporting that would be reporting the pipeline
        // working.
        var neverPopulated = models
            .Select(m => PathCoverage.Measure(m!, plans).Unpopulated.ToHashSet(StringComparer.Ordinal))
            .Aggregate((a, b) => { a.IntersectWith(b); return a; })
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        _output.WriteLine(
            $"{neverPopulated.Count} selected path(s) held no value in any of the "
            + $"{models.Count} stored models:");

        foreach (var path in neverPopulated)
        {
            _output.WriteLine("  " + path);
        }
    }

    /// <summary>
    /// The runner resolves a selected parent into whole objects, so a group naming the parent
    /// reads every field under it. Measuring on exact equality would report hundreds of paths as
    /// unread that the assessor demonstrably reads, and the headline figure would be an artefact.
    /// </summary>
    [Fact]
    public void ASelectedParentCoversItsChildren()
    {
        Assert.True(PathCoverage.Covers("/objectives[]", "/objectives[]/summary"));
        Assert.True(PathCoverage.Covers("/taxTreatment", "/taxTreatment/allowancesUsed[]/name"));
        Assert.True(PathCoverage.Covers("/case/caseReference", "/case/caseReference"));
    }

    /// <summary>
    /// And the boundary that makes it safe: a prefix is not a parent unless it ends at a
    /// separator. <c>/objectives</c> covering <c>/objectivesReview</c> would report a root as read
    /// on the strength of a different root's name.
    /// </summary>
    [Fact]
    public void APrefixIsNotAParent() =>
        Assert.False(PathCoverage.Covers("/objectives", "/objectivesReview[]/date"));

    [Fact]
    public void APathNoGroupSelectsIsReportedUnread()
    {
        var plan = new CheckQueryPlan
        {
            CheckId = "CHK-001",
            QueryGroups =
            [
                new PlanQueryGroup
                {
                    GroupId = "G1.1",
                    Retrieval = new PlanRetrieval { CanonicalPaths = ["/objectives[]"] },
                },
            ],
        };

        var coverage = PathCoverage.Measure(
            ["/objectives[]/summary", "/taxTreatment/allowancesUsed[]/name"], [plan]);

        Assert.Equal(["/taxTreatment/allowancesUsed[]/name"], coverage.Unread);
        Assert.Equal(
            ["CHK-001/G1.1"],
            coverage.Populated.Single(p => p.Path == "/objectives[]/summary").Readers);
    }

    private static IReadOnlyList<CheckQueryPlan> Plans() =>
        [.. CheckQueryPlanLoader
            .Load(Path.Combine(AppContext.BaseDirectory, "check-plan"))
            .Plans.Values];

    private static string? StoredModel(string runFolder)
    {
        var root = RepoRoot();

        if (root is null)
        {
            return null;
        }

        var run = Path.Combine(root, "docs", "Test-Runs", runFolder);

        if (!Directory.Exists(run))
        {
            return null;
        }

        var model = Directory.GetFiles(run, "canonical-model_*.json").FirstOrDefault();

        return model is null ? null : File.ReadAllText(model);
    }

    private static string? RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiPromptEvaluator.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName;
    }
}
