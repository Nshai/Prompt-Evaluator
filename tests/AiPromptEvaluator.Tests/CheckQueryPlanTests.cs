using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The shipped query plans, loaded as the app loads them. These are data files that decide
/// what a check retrieves, so a plan that stops parsing silently degrades every run against
/// it — the app skips it and assesses one check fewer.
/// </summary>
public class CheckQueryPlanTests
{
    private readonly ITestOutputHelper _output;

    public CheckQueryPlanTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// The plan folder as deployed beside the test assembly, falling back to the authoring
    /// location in the repository. Null when neither is present, so the suite still runs.
    /// </summary>
    public static string? LocatePlanFolder()
    {
        var deployed = Path.Combine(AppContext.BaseDirectory, AppSettings.DefaultCheckPlanFolderName);
        if (Directory.Exists(deployed) &&
            Directory.GetFiles(deployed, CheckQueryPlanLoader.SearchPattern).Length > 0)
        {
            return deployed;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "artifacts", "check-plan");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    [Fact]
    public void EveryShippedPlan_Loads()
    {
        var folder = LocatePlanFolder();
        if (folder is null)
        {
            _output.WriteLine("Skipped: no check plan folder found.");
            return;
        }

        var (plans, failures) = CheckQueryPlanLoader.Load(folder);

        Assert.Empty(failures);
        Assert.NotEmpty(plans);

        foreach (var plan in plans.Values.OrderBy(p => p.CheckId, StringComparer.Ordinal))
        {
            _output.WriteLine($"{plan.CheckId}: {plan.QueryGroups.Count} group(s), {plan.QueryCount} quer(ies)");

            Assert.NotEmpty(plan.QueryGroups);
            Assert.All(plan.QueryGroups, group =>
            {
                Assert.False(string.IsNullOrWhiteSpace(group.GroupId));
                Assert.False(string.IsNullOrWhiteSpace(group.Requirement));
                Assert.NotEmpty(group.Queries);
                Assert.All(group.Queries, query => Assert.False(string.IsNullOrWhiteSpace(query.Text)));
            });
        }
    }

    /// <summary>
    /// A group is answerable either by searching the case file or from the canonical model
    /// alone. What it must never be is neither: a group with no evidence query and no
    /// canonical path has nothing to assess and could only ever report an evidence gap.
    ///
    /// The model-only groups are the ones comparing the report against itself — internal
    /// contradictions, charge arithmetic, the prominence of a risk section — and the runner
    /// tells the assessor so rather than presenting them as failed retrieval.
    /// </summary>
    [Fact]
    public void EveryGroup_HasSomethingToAssess()
    {
        var folder = LocatePlanFolder();
        if (folder is null)
        {
            return;
        }

        var (plans, _) = CheckQueryPlanLoader.Load(folder);
        var modelOnly = new List<string>();

        foreach (var plan in plans.Values)
        {
            foreach (var group in plan.QueryGroups)
            {
                if (group.IsModelOnly)
                {
                    modelOnly.Add($"{plan.CheckId} {group.GroupId}");

                    Assert.True(
                        group.AllCanonicalPaths.Count > 0,
                        $"{plan.CheckId} {group.GroupId} searches nothing and cites no canonical path.");
                }
                else
                {
                    Assert.True(
                        group.Queries.Any(q => q.IsEvidenceSearch),
                        $"{plan.CheckId} {group.GroupId} has no evidence-side query.");
                }
            }
        }

        foreach (var group in modelOnly)
        {
            _output.WriteLine($"Assessed from the canonical model alone: {group}");
        }
    }

    /// <summary>
    /// "Assertion" is the only side answered from the stored model; "Either" phrasings are
    /// written to retrieve from both and still have to be searched for.
    /// </summary>
    [Theory]
    [InlineData("Assertion", false)]
    [InlineData("Evidence", true)]
    [InlineData("Either", true)]
    public void IsEvidenceSearch_FollowsTheSide(string side, bool expected)
    {
        var query = new PlannedQuery { Side = side };

        Assert.Equal(expected, query.IsEvidenceSearch);
    }

    /// <summary>
    /// Check ids arrive from the CSV carrying the newline the source file wraps them with,
    /// so the lookup that pairs a check with its plan has to squeeze both sides.
    /// </summary>
    [Fact]
    public void NormaliseCheckId_StripsTheWhitespaceTheCsvCarries()
    {
        Assert.Equal("CHK-001", CheckQueryPlanLoader.NormaliseCheckId("CHK-001\n"));
        Assert.Equal("CHK-001", CheckQueryPlanLoader.NormaliseCheckId(" CHK-001 "));
        Assert.Equal("CHK-010", CheckQueryPlanLoader.NormaliseCheckId("CHK-010"));
    }

    [Fact]
    public void Load_SkipsAnUnparseablePlanRatherThanFailingTheLot()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"plan-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            File.WriteAllText(
                Path.Combine(folder, "CHK-001.query-plan.json"),
                """{"planVersion":"1.0","checkId":"CHK-001","checkName":"Good","queryGroups":[]}""");
            File.WriteAllText(Path.Combine(folder, "CHK-002.query-plan.json"), "{ not json");

            var (plans, failures) = CheckQueryPlanLoader.Load(folder);

            Assert.Single(plans);
            Assert.True(plans.ContainsKey("CHK-001"));
            Assert.Single(failures);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenTheFolderIsNotThere()
    {
        var (plans, failures) = CheckQueryPlanLoader.Load(
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}"));

        Assert.Empty(plans);
        Assert.Empty(failures);
    }
}
