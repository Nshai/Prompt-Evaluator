using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Contradictions the extraction found, routed to the group whose requirement rests on them.
///
/// <b>These were already being found and already reaching the assessor, and that was not
/// enough.</b> The whole extraction report is printed once in a check's header, ahead of every
/// group — so a contradiction between two expenditure figures arrives in front of eleven
/// requirements, ten of which have no use for it, and competes with everything else the extraction
/// wanted to say.
///
/// Measured on a run that missed the finding: one group quoted the recorded expenditure and charge
/// inconsistencies and used them well. The groups whose requirements actually turned on those
/// figures did not mention them. Nothing was missing from the run; nothing routed it.
///
/// This is the routing. It is a path-overlap test, so it works on any case whose extraction
/// records a contradiction — the shape is the report disagreeing with itself, which is the
/// canonical model's territory and needs no retrieval at all.
/// </summary>
public class InconsistencyRoutingTests
{
    private readonly ITestOutputHelper _output;

    public InconsistencyRoutingTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void AnInconsistencyIsReadWithItsPathsValuesAndChecks()
    {
        var found = Assert.Single(new CanonicalModelAccessor(Model()).InternalInconsistencies);

        Assert.Contains("differ by", found.Description);
        Assert.Equal(2, found.Paths.Count);
        Assert.Equal(2, found.Values.Count);
        Assert.Equal(["CHK-001"], found.CheckIds);
    }

    [Fact]
    public void AModelWithNoExtractionReportHasNone()
    {
        Assert.Empty(new CanonicalModelAccessor("""{"case":{}}""").InternalInconsistencies);
    }

    /// <summary>
    /// <b>The match that makes routing possible.</b> The two are written at different
    /// granularities and always have been — an inconsistency names a leaf, the group that needs
    /// it asks for the section. Requiring exact equality routes nothing, which is
    /// indistinguishable from the behaviour this replaces.
    /// </summary>
    /// <remarks>
    /// The fixture records <c>/financialPosition/expenditure/monthlyEssential</c> and
    /// <c>/existingArrangements/EA1/riskLevel</c>. Each case below asks at a different depth, or
    /// in a different spelling, for one of those two places.
    /// </remarks>
    [Theory]
    [InlineData("/financialPosition/expenditure")]                    // shallower than recorded
    [InlineData("/financialPosition/expenditure/monthlyEssential")]   // exactly as recorded
    [InlineData("/financialPosition")]                                // shallower still
    [InlineData("/existingArrangements[]/riskLevel")]                 // fan-out against an element id
    [InlineData("/existingArrangements[]")]                           // the collection
    public void PathsThatDescribeTheSamePlaceAtDifferentDepthsMatch(string asked)
    {
        Assert.NotEmpty(Routed(asked));
    }

    /// <summary>
    /// And paths that describe different places do not. A contradiction routed everywhere is the
    /// check header again, which is what this replaces.
    /// </summary>
    [Theory]
    [InlineData("/objectives")]
    [InlineData("/vulnerability/assessment")]
    [InlineData("/financialPositionSummary")]
    public void PathsThatDescribeSomewhereElseDoNotMatch(string asked)
    {
        Assert.Empty(Routed(asked));
    }

    /// <summary>
    /// A group that asks for nothing from the model gets nothing. It has no paths to overlap, and
    /// routing everything to it would be the undifferentiated behaviour again.
    /// </summary>
    [Fact]
    public void AGroupWithNoCanonicalPathsGetsNothing()
    {
        Assert.Empty(Routed());
    }

    /// <summary>
    /// The root is not a path. A group asking for <c>/</c> — or a normalisation that reduced a
    /// path to it — would match every inconsistency in the model.
    /// </summary>
    [Fact]
    public void TheRootMatchesNothing()
    {
        Assert.Empty(Routed("/"));
    }

    /// <summary>
    /// The stored model from a run that missed the findings these contradictions bear on. If this
    /// stops finding them, the extraction has changed shape and the routing is reading a field
    /// that no longer exists — silently, which is how the original defect survived.
    /// </summary>
    [Fact]
    public void TheStoredModelCarriesRoutableInconsistencies()
    {
        if (StoredModel() is not { } json)
        {
            _output.WriteLine("Skipped: the archived canonical model is not in this working copy.");
            return;
        }

        var found = new CanonicalModelAccessor(json).InternalInconsistencies;

        foreach (var inconsistency in found)
        {
            _output.WriteLine($"{string.Join(", ", inconsistency.Paths)} — {inconsistency.Description}");
        }

        Assert.NotEmpty(found);
        Assert.All(found, i => Assert.NotEmpty(i.Paths));
    }

    // ──────────────────────────────────────────────

    private static IReadOnlyList<CanonicalModelAccessor.InternalInconsistency> Routed(
        params string[] canonicalPaths)
    {
        var group = new PlanQueryGroup
        {
            GroupId = "G1.1",
            Retrieval = new PlanRetrieval { CanonicalPaths = [.. canonicalPaths] },
        };

        return CheckPlanRunner.RelevantInconsistenciesFor(
            new CanonicalModelAccessor(Model()), group);
    }

    private static string Model() =>
        ("""
         {
           "extractionReport": {
             "internalInconsistencies": [
               {
                 "paths": ["/financialPosition/expenditure/monthlyEssential",
                           "/existingArrangements/EA1/riskLevel"],
                 "description": "Two figures for the same quantity differ by a material amount.",
                 "valuesObserved": ["one figure", "another figure"],
                 "relatedCheckIds": ["CHK-001"]
               }
             ]
           }
         }
         """);

    private static string? StoredModel()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiPromptEvaluator.slnx")))
        {
            dir = dir.Parent;
        }

        var run = dir is null
            ? null
            : Path.Combine(dir.FullName, "docs", "Test-Runs", "Test-Case 2", "Run-3");

        if (run is null || !Directory.Exists(run))
        {
            return null;
        }

        var model = Directory.GetFiles(run, "canonical-model_*.json").FirstOrDefault();

        return model is null ? null : File.ReadAllText(model);
    }
}
