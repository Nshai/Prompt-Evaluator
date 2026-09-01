using System.Text.Json.Nodes;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Keeping cross-reference identifiers consistent across extraction passes.
///
/// The failure these are written against was silent. Extracting a real report, the objectives
/// pass assigned OBJ-001 to OBJ-003 and the recommendations pass — a separate call that never saw
/// them — wrote linkedObjectiveIds of "LowerCosts" and "ConsolidationAndClarity". Seventeen
/// references, none resolving, no error raised, and CHK-006 left unable to answer the one question
/// it exists to ask: whether each recommendation ties to a stated objective.
/// </summary>
public class CanonicalModelIdentityTests
{
    private static JsonObject Model(string json) => JsonNode.Parse(json)!.AsObject();

    /// <summary>Ids the model invented become the code's ids, so two passes cannot disagree.</summary>
    [Fact]
    public void Adopt_NormalisesDefinitionIds()
    {
        var root = Model(
            """
            {
              "parties": { "clients": [
                { "clientId": "John Sullivan", "fullName": "John Sullivan" },
                { "clientId": "client-2", "fullName": "Kim Sullivan" }
              ] },
              "objectives": [
                { "objectiveId": "OBJ-001", "summary": "Retire in two years" },
                { "objectiveId": "ConsolidatePensions", "summary": "Consolidate pensions" }
              ]
            }
            """);

        new CanonicalModelIdentityRegistry().Adopt(root);

        var clients = root["parties"]!["clients"]!.AsArray();
        Assert.Equal("C1", clients[0]!["clientId"]!.GetValue<string>());
        Assert.Equal("C2", clients[1]!["clientId"]!.GetValue<string>());

        var objectives = root["objectives"]!.AsArray();
        Assert.Equal("OBJ1", objectives[0]!["objectiveId"]!.GetValue<string>());
        Assert.Equal("OBJ2", objectives[1]!["objectiveId"]!.GetValue<string>());
    }

    /// <summary>
    /// The table is what a later pass chooses from, so it has to be readable — C1 alone gives
    /// the model no way to map an id onto the person in the report.
    /// </summary>
    [Fact]
    public void Table_NamesEachEntitySoItCanBeChosen()
    {
        var root = Model(
            """
            {
              "parties": { "clients": [ { "clientId": "x", "fullName": "John Sullivan" } ] },
              "existingArrangements": [ { "arrangementId": "z", "provider": "Zurich" } ]
            }
            """);

        var identity = new CanonicalModelIdentityRegistry();
        identity.Adopt(root);

        var table = identity.Table();

        Assert.Contains("C1 = John Sullivan", table, StringComparison.Ordinal);
        Assert.Contains("EA1 = Zurich", table, StringComparison.Ordinal);
        Assert.Contains("never put a name, a label or a description in an id field", table, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_IsEmptyBeforeAnythingIsAdopted() =>
        Assert.False(new CanonicalModelIdentityRegistry().HasEntities);

    /// <summary>
    /// A pass that used an entity's old id is repointed rather than left dangling — which is
    /// what makes this work for passes that ran before the table existed.
    /// </summary>
    [Fact]
    public void RewriteReferences_RepointsReferencesOntoCanonicalIds()
    {
        var root = Model(
            """
            {
              "parties": { "clients": [ { "clientId": "John Sullivan", "fullName": "John Sullivan" } ] },
              "objectives": [ { "objectiveId": "OBJ-001", "summary": "Consolidate" } ],
              "existingArrangements": [ { "arrangementId": "Aviva-SP50163762", "provider": "Aviva" } ],
              "recommendations": [
                {
                  "recommendationId": "REC-001",
                  "ownerClientIds": ["John Sullivan"],
                  "linkedObjectiveIds": ["OBJ-001"],
                  "sourceArrangementIds": ["Aviva-SP50163762"]
                }
              ]
            }
            """);

        var identity = new CanonicalModelIdentityRegistry();
        identity.Adopt(root);
        identity.RewriteReferences(root);

        var recommendation = root["recommendations"]!.AsArray()[0]!;
        Assert.Equal("C1", recommendation["ownerClientIds"]![0]!.GetValue<string>());
        Assert.Equal("OBJ1", recommendation["linkedObjectiveIds"]![0]!.GetValue<string>());
        Assert.Equal("EA1", recommendation["sourceArrangementIds"]![0]!.GetValue<string>());
        Assert.Empty(identity.DanglingReferences(root));
    }

    /// <summary>
    /// The observed defect exactly: a label where an objective id belongs. It cannot be
    /// repointed, because it names nothing — so it has to be reported.
    /// </summary>
    [Fact]
    public void DanglingReferences_ReportsAReferenceThatNamesNothing()
    {
        var root = Model(
            """
            {
              "objectives": [ { "objectiveId": "OBJ-001", "summary": "Consolidate pensions" } ],
              "recommendations": [
                { "recommendationId": "REC-001", "linkedObjectiveIds": ["LowerCosts", "OBJ-001"] }
              ]
            }
            """);

        var identity = new CanonicalModelIdentityRegistry();
        identity.Adopt(root);
        identity.RewriteReferences(root);

        var dangling = identity.DanglingReferences(root);

        Assert.Single(dangling);
        Assert.Contains("linkedObjectiveIds → LowerCosts", dangling[0], StringComparison.Ordinal);

        // The reference that did resolve is repointed and not reported.
        Assert.Contains("OBJ1", root["recommendations"]![0]!["linkedObjectiveIds"]!.ToJsonString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A reference must point at the right *kind* of thing, not merely at something that
    /// exists. This is not hypothetical: a real extraction wrote linkedObjectiveIds of
    /// ["REC5"] — a recommendation id in an objective slot. Validating against every id in the
    /// model would pass it, because REC5 is a real id; it is just not an objective.
    /// </summary>
    [Fact]
    public void DanglingReferences_RejectsARealIdOfTheWrongKind()
    {
        var root = Model(
            """
            {
              "objectives": [ { "objectiveId": "OBJ-1", "summary": "Consolidate pensions" } ],
              "recommendations": [
                { "recommendationId": "R-9", "summary": "Switch Zurich", "linkedObjectiveIds": ["R-9"] }
              ]
            }
            """);

        var identity = new CanonicalModelIdentityRegistry();
        identity.Adopt(root);
        identity.RewriteReferences(root);

        var dangling = identity.DanglingReferences(root);

        // Rewriting is kind-scoped too, so an objective slot is never repointed using the
        // recommendation map: the value stays as written and is reported for what it is.
        Assert.Single(dangling);
        Assert.Contains("linkedObjectiveIds → R-9", dangling[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// Adopting is idempotent. A later pass restating an entity must not renumber it out from
    /// under a reference already written against it.
    /// </summary>
    [Fact]
    public void Adopt_KeepsIdsStableAcrossPasses()
    {
        var root = Model(
            """
            { "objectives": [ { "objectiveId": "a", "summary": "First" } ] }
            """);

        var identity = new CanonicalModelIdentityRegistry();
        identity.Adopt(root);
        identity.Adopt(root);

        Assert.Equal("OBJ1", root["objectives"]![0]!["objectiveId"]!.GetValue<string>());

        // A genuinely new entity from a later pass numbers on from there.
        root["objectives"]!.AsArray().Add(Model("""{ "objectiveId": "b", "summary": "Second" }"""));
        identity.Adopt(root);

        Assert.Equal("OBJ1", root["objectives"]![0]!["objectiveId"]!.GetValue<string>());
        Assert.Equal("OBJ2", root["objectives"]![1]!["objectiveId"]!.GetValue<string>());
    }

    /// <summary>An id the model copied straight out of the table is already right.</summary>
    [Fact]
    public void Adopt_LeavesIdsThatAreAlreadyCanonical()
    {
        var root = Model(
            """
            { "objectives": [ { "objectiveId": "OBJ1", "summary": "Retire" } ] }
            """);

        var identity = new CanonicalModelIdentityRegistry();
        identity.Adopt(root);
        Assert.Equal("OBJ1", root["objectives"]![0]!["objectiveId"]!.GetValue<string>());
        Assert.Empty(identity.DanglingReferences(root));
    }

    /// <summary>A section the report has nothing for must not break the walk.</summary>
    [Fact]
    public void Adopt_ToleratesMissingAndEmptyCollections()
    {
        var root = Model("""{ "objectives": [], "recommendations": [] }""");

        var identity = new CanonicalModelIdentityRegistry();
        identity.Adopt(root);

        Assert.False(identity.HasEntities);
        Assert.Empty(identity.DanglingReferences(root));
    }

    /// <summary>
    /// Every cross-reference must run forwards through the pass order, or the table cannot
    /// hold the ids a pass needs when it runs. Recommendations carry a targetPlanId into
    /// solution.recommendedPlans, so Solution has to precede Recommendations.
    /// </summary>
    [Fact]
    public void SectionOrder_DefinesEveryIdBeforeAPassCanReferenceIt()
    {
        var order = ExtractionSection.All.Select(s => s.Name).ToList();

        Assert.True(
            order.IndexOf("Solution") < order.IndexOf("Recommendations"),
            "Solution defines planId, which Recommendations references as targetPlanId.");

        Assert.True(
            order.IndexOf("Objectives") < order.IndexOf("Recommendations"),
            "Objectives defines objectiveId, which Recommendations references.");

        Assert.True(
            order.IndexOf("Existing arrangements") < order.IndexOf("Recommendations"),
            "Existing arrangements defines arrangementId, which Recommendations references.");

        Assert.True(
            order.IndexOf("Existing arrangements") < order.IndexOf("Replacement analysis"),
            "Replacement analysis references the arrangements being replaced.");
    }
}
