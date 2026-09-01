using System.IO;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The plan's shape, and the two levers it grew when the field analysis showed them inert.
///
/// A query plan does two unrelated jobs: <c>retrieval</c> decides what the assessor is allowed
/// to see, <c>verification</c> decides what it may make of it. A mistake in the first is
/// invisible in the output — the check reports confidently on a pack that never held the
/// answer — so the split is structural rather than a naming convention.
/// </summary>
public class CheckPlanStructureTests
{
    private static string PlanFolder => Path.Combine(AppContext.BaseDirectory, "check-plan");

    private static string WritePlan(string json)
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "CHK-999.query-plan.json"), json);
        return folder;
    }

    private const string MinimalPlan = """
        {
          "planVersion": "%VERSION%",
          "checkId": "CHK-999",
          "checkName": "Test",
          "queryGroups": [
            {
              "groupId": "G1.1",
              "requirement": "A requirement",
              "retrieval": {
                "canonicalPaths": ["/case/caseReference"],
                "evidenceSections": ["Total Net Monthly Income"],
                "queries": [
                  { "id": "Q1", "text": "core one",  "side": "Evidence", "targetCategories": ["B"], "priority": "Core" },
                  { "id": "Q2", "text": "extra one", "side": "Evidence", "targetCategories": ["B"], "priority": "Supplementary" },
                  { "id": "Q3", "text": "asserted",  "side": "Assertion", "targetCategories": ["I"] }
                ]
              },
              "verification": {
                "limb": "Consistency",
                "comparison": { "method": "ValueMatch" }
              }
            }
          ],
          "decision": { "noIssue": "fine" }
        }
        """;

    // ── planVersion is now enforced ────────────────────────────────────────────

    [Fact]
    public void ASupportedPlanVersionLoads()
    {
        var folder = WritePlan(MinimalPlan.Replace("%VERSION%", CheckQueryPlanLoader.SupportedPlanVersion));

        var (plans, failures) = CheckQueryPlanLoader.Load(folder);

        Assert.Empty(failures);
        Assert.Single(plans);

        Directory.Delete(folder, recursive: true);
    }

    /// <summary>
    /// A plan from a format this build does not know is refused by name. Loading it anyway
    /// would leave the fields a future format renames silently empty, and the check would
    /// assess a pack assembled from half a plan without anything saying so.
    /// </summary>
    [Fact]
    public void AnUnsupportedPlanVersionIsRefusedByNameRatherThanHalfRead()
    {
        var folder = WritePlan(MinimalPlan.Replace("%VERSION%", "2.0"));

        var (plans, failures) = CheckQueryPlanLoader.Load(folder);

        Assert.Empty(plans);
        var (file, error) = Assert.Single(failures);
        Assert.Equal("CHK-999.query-plan.json", file);
        Assert.Contains("2.0", error);
        Assert.Contains(CheckQueryPlanLoader.SupportedPlanVersion, error);

        Directory.Delete(folder, recursive: true);
    }

    // ── priority is now honoured ───────────────────────────────────────────────

    [Fact]
    public void EveryQueryRunsByDefault()
    {
        var group = LoadSingleGroup();

        Assert.Equal(3, group.QueriesToRun(coreOnly: false).Count());
    }

    /// <summary>
    /// The cheapest retrieval saving available, and the plan authors already chose what it
    /// costs: roughly one query in seven is marked Supplementary.
    /// </summary>
    [Fact]
    public void CoreOnlyDropsTheSupplementaryQueries()
    {
        var group = LoadSingleGroup();

        var kept = group.QueriesToRun(coreOnly: true).Select(q => q.Id).ToList();

        Assert.Equal(["Q1", "Q3"], kept);
    }

    [Fact]
    public void AQueryWithNoPriorityCountsAsCore()
    {
        Assert.True(new PlannedQuery { Id = "Q" }.IsCore);
        Assert.True(new PlannedQuery { Id = "Q", Priority = "Core" }.IsCore);
        Assert.False(new PlannedQuery { Id = "Q", Priority = "Supplementary" }.IsCore);
    }

    [Fact]
    public void CoreQueriesOnlyIsOffByDefault()
    {
        Assert.False(new AppSettings().CoreQueriesOnly);
    }

    // ── the nested shape ───────────────────────────────────────────────────────

    [Fact]
    public void RetrievalAndVerificationAreReadableThroughTheFlattenedAccessors()
    {
        var group = LoadSingleGroup();

        Assert.Equal(["/case/caseReference"], group.CanonicalPaths);
        Assert.Equal(["Total Net Monthly Income"], group.DeclaredEvidenceSections);
        Assert.Equal("Consistency", group.Limb);
        Assert.Equal("ValueMatch", group.Comparison?.Method);
        Assert.Equal(3, group.Queries.Count);
    }

    /// <summary>
    /// A group with no verification block still has a limb, because the prompt prints one
    /// unconditionally. Consistency is the safe default: it is what nine of the ten checks are.
    /// </summary>
    [Fact]
    public void AGroupWithNoVerificationBlockDefaultsToConsistency()
    {
        var group = new PlanQueryGroup { GroupId = "G", Requirement = "R" };

        Assert.Equal("Consistency", group.Limb);
        Assert.Null(group.Comparison);
        Assert.Null(group.Sufficiency);
        Assert.Empty(group.Queries);
    }

    // ── the shipped plans keep the shape ───────────────────────────────────────

    /// <summary>
    /// Every shipped group must carry a retrieval block with at least one query. A group with
    /// none would assess a requirement having searched for nothing, and report success.
    /// </summary>
    [Fact]
    public void EveryShippedGroupHasRetrievalQueries()
    {
        var (plans, failures) = CheckQueryPlanLoader.Load(PlanFolder);

        Assert.Empty(failures);
        Assert.Equal(10, plans.Count);

        foreach (var plan in plans.Values)
        {
            foreach (var group in plan.QueryGroups)
            {
                Assert.True(
                    group.Queries.Count > 0,
                    $"{plan.CheckId} {group.GroupId} has no queries.");
            }
        }
    }

    /// <summary>
    /// The dead fields are gone from the shipped plans, not merely undocumented. `tool` and
    /// `budget` in particular were never deserialised, and `tool.categoryFilterAvailable`
    /// asserted something about the search path that had stopped being true.
    /// </summary>
    [Theory]
    [InlineData("\"tool\"")]
    [InlineData("\"budget\"")]
    [InlineData("\"appliesTo\"")]
    [InlineData("\"regulatoryBasis\"")]
    [InlineData("\"presentWhen\"")]
    [InlineData("\"expectedCategories\"")]
    public void TheShippedPlansCarryNoDeadFields(string token)
    {
        foreach (var file in Directory.GetFiles(PlanFolder, CheckQueryPlanLoader.SearchPattern))
        {
            Assert.DoesNotContain(token, File.ReadAllText(file));
        }
    }

    // ── plan-level retrieval settings ─────────────────────────────────────────

    [Fact]
    public void APlanMaySetItsOwnResultsPerCall()
    {
        var folder = WritePlan(MinimalPlan
            .Replace("%VERSION%", CheckQueryPlanLoader.SupportedPlanVersion)
            .Replace(
                "\"checkName\": \"Test\",",
                "\"checkName\": \"Test\", \"retrieval\": { \"resultsPerCall\": 16 },"));

        var (plans, failures) = CheckQueryPlanLoader.Load(folder);

        Assert.Empty(failures);
        Assert.Equal(16, Assert.Single(plans).Value.Retrieval?.ResultsPerCall);

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void APlanWithNoRetrievalBlockTakesTheGlobalDefaults()
    {
        var folder = WritePlan(MinimalPlan.Replace("%VERSION%", CheckQueryPlanLoader.SupportedPlanVersion));

        var (plans, _) = CheckQueryPlanLoader.Load(folder);

        Assert.Null(Assert.Single(plans).Value.Retrieval);

        Directory.Delete(folder, recursive: true);
    }

    /// <summary>
    /// The shipped plans deliberately set nothing. The measured candidate pool already exceeds
    /// the per-group cap for every check — the narrowest is CHK-006 at roughly 22 candidates
    /// against a cap of 12 — so a value here would be a guess, and a guess that silently
    /// changes what a check retrieves is worse than the default it replaced.
    /// </summary>
    [Fact]
    public void NoShippedPlanOverridesResultsPerCall()
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);

        Assert.All(plans.Values, p => Assert.Null(p.Retrieval?.ResultsPerCall));
    }

    private static PlanQueryGroup LoadSingleGroup()
    {
        var folder = WritePlan(MinimalPlan.Replace("%VERSION%", CheckQueryPlanLoader.SupportedPlanVersion));

        try
        {
            var (plans, _) = CheckQueryPlanLoader.Load(folder);
            return Assert.Single(plans).Value.QueryGroups[0];
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
