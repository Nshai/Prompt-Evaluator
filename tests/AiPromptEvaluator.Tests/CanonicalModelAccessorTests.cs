using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Path resolution against a stored model. This is the half of a check that replaces
/// re-reading the suitability report, so a path that silently resolves to nothing would turn
/// "the report says X" into "the report is silent" — a wrong finding, not a missing one.
/// </summary>
public class CanonicalModelAccessorTests
{
    private const string ModelJson =
        """
        {
          "modelVersion": "1.0",
          "parties": {
            "clients": [
              { "clientId": "C1", "firstName": "John", "age": 69 },
              { "clientId": "C2", "firstName": "Kim", "age": 65 }
            ]
          },
          "riskAssessment": {
            "perClient": [
              { "clientId": "C1", "attitudeToRisk": { "assessedCode": "4", "agreedCode": "5" } }
            ]
          },
          "costsAndCharges": {
            "recommended": {
              "lines": [
                { "layer": "Platform", "percentage": { "value": 0.21 } },
                { "layer": "TransactionCost", "percentage": { "value": null } }
              ]
            }
          },
          "checkTriggers": { "hasReplacementOrSwitch": true, "hasDecumulation": false },
          "replacementAnalysis": []
        }
        """;

    private static CanonicalModelAccessor Accessor() => new(ModelJson);

    [Fact]
    public void Resolve_ReadsANestedScalar()
    {
        var fragment = Accessor().Resolve("/riskAssessment/perClient[]/attitudeToRisk/agreedCode");

        Assert.True(fragment.Found);
        Assert.Contains("5", fragment.Json);
    }

    /// <summary>A fan-out path yields every element, so a joint case cannot lose the second client.</summary>
    [Fact]
    public void Resolve_FansOutAcrossArrays()
    {
        var fragment = Accessor().Resolve("/parties/clients[]/firstName");

        Assert.True(fragment.Found);
        Assert.Contains("John", fragment.Json);
        Assert.Contains("Kim", fragment.Json);
    }

    [Fact]
    public void Resolve_SelectsArrayElementsByProperty()
    {
        var fragment = Accessor().Resolve("/costsAndCharges/recommended/lines[layer=TransactionCost]");

        Assert.True(fragment.Found);
        Assert.Contains("TransactionCost", fragment.Json);
        Assert.DoesNotContain("Platform", fragment.Json);
    }

    /// <summary>
    /// A path that matches nothing must report that plainly rather than throwing or coming
    /// back as an empty success — the runner counts these as "the report does not say this".
    /// </summary>
    [Fact]
    public void Resolve_ReportsAMissingPathRatherThanThrowing()
    {
        var accessor = Accessor();

        Assert.False(accessor.Resolve("/vulnerability/perClient[]/hasVulnerability").Found);
        Assert.False(accessor.Resolve("/parties/clients[]/nationalInsuranceNumber").Found);
        Assert.False(accessor.Resolve("/nothing/here/at/all").Found);
    }

    /// <summary>An empty array is present in the model and means something different from absent.</summary>
    [Fact]
    public void Resolve_DistinguishesAnEmptyArrayFromAMissingPath()
    {
        var accessor = Accessor();

        Assert.True(accessor.Resolve("/replacementAnalysis").Found);
        Assert.False(accessor.Resolve("/replacementAnalysis[]/cedingArrangementId").Found);
    }

    /// <summary>
    /// The runner reads trigger fields as dotted names turned into paths, and decides
    /// applicability on them — false must resolve as found-and-false, never as missing.
    /// </summary>
    [Fact]
    public void Resolve_ReadsBooleanTriggers()
    {
        var accessor = Accessor();

        var present = accessor.Resolve("/checkTriggers/hasReplacementOrSwitch");
        Assert.True(present.Found);
        Assert.Equal("true", present.Json.Trim());

        var absent = accessor.Resolve("/checkTriggers/hasDecumulation");
        Assert.True(absent.Found);
        Assert.Equal("false", absent.Json.Trim());
    }

    [Fact]
    public void PopulatedSections_ListsWhatWasExtracted()
    {
        var sections = Accessor().PopulatedSections;

        Assert.Contains("parties", sections);
        Assert.Contains("costsAndCharges", sections);
        Assert.DoesNotContain("solution", sections);
    }

    /// <summary>
    /// Every canonical path cited by the shipped query plans has to be syntactically
    /// resolvable. A typo in a plan would otherwise present as "the report never says this"
    /// on every case that plan is run against.
    /// </summary>
    [Fact]
    public void EveryPlanPath_ResolvesWithoutThrowing()
    {
        var folder = CheckQueryPlanTests.LocatePlanFolder();
        if (folder is null)
        {
            return;
        }

        var (plans, _) = CheckQueryPlanLoader.Load(folder);
        var accessor = Accessor();

        foreach (var path in plans.Values
                     .SelectMany(p => p.QueryGroups)
                     .SelectMany(g => g.AllCanonicalPaths))
        {
            var exception = Record.Exception(() => accessor.Resolve(path));
            Assert.Null(exception);
        }
    }
}
