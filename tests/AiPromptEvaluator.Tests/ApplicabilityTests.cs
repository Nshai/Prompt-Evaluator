using System.Text.Json;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Whether a check runs at all, decided from the stored canonical model before a single
/// embedding is paid for.
///
/// Rules are ANDed, which is the property that matters: a rule can narrow a check but never
/// rescue one another rule has ruled out. Getting that backwards would run checks on cases
/// they do not apply to, and every finding they produced would be noise.
/// </summary>
public class ApplicabilityTests
{
    private static PlanApplicability Rule(string name, params string[] values)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(values));

        return new PlanApplicability
        {
            CanonicalPaths = ["/objectives[]/objectiveType"],
            Named = new Dictionary<string, JsonElement> { [name] = doc.RootElement.Clone() },
        };
    }

    // ── the rule itself ───────────────────────────────────────────────────────

    [Fact]
    public void TheRuleIsNamedByItsOwnKey()
    {
        var rule = Rule("goalTypes", "Investment", "Pension");

        Assert.Equal("goalTypes", rule.Name);
        Assert.Equal(["Investment", "Pension"], rule.AcceptedValues);
    }

    [Fact]
    public void AnyMatchSatisfiesTheRule() =>
        Assert.True(Rule("goalTypes", "Investment", "Pension")
            .IsSatisfiedBy(["Protection", "Pension"]));

    [Fact]
    public void NoMatchDoesNotSatisfyIt() =>
        Assert.False(Rule("goalTypes", "Investment", "Pension")
            .IsSatisfiedBy(["Protection", "Mortgage"]));

    [Fact]
    public void NothingFoundDoesNotSatisfyIt() =>
        Assert.False(Rule("goalTypes", "Investment").IsSatisfiedBy([]));

    /// <summary>
    /// A plan author writing "investment" should not have to know the schema capitalises it.
    /// </summary>
    [Fact]
    public void MatchingIsCaseInsensitive() =>
        Assert.True(Rule("goalTypes", "investment").IsSatisfiedBy(["Investment"]));

    /// <summary>
    /// A rule with no values would otherwise be vacuously true and silently disable itself,
    /// which is the worst possible failure for something that decides whether a check runs.
    /// </summary>
    [Fact]
    public void ARuleWithNoAcceptedValuesIsNeverSatisfied() =>
        Assert.False(new PlanApplicability { CanonicalPaths = ["/x"] }.IsSatisfiedBy(["anything"]));

    [Fact]
    public void BooleanTriggerValuesAreReadAsStrings()
    {
        using var doc = JsonDocument.Parse("[true]");

        var rule = new PlanApplicability
        {
            CanonicalPaths = ["/checkTriggers/isAdvisedCase"],
            Named = new Dictionary<string, JsonElement> { ["advisedCase"] = doc.RootElement.Clone() },
        };

        Assert.Equal(["true"], rule.AcceptedValues);
        Assert.True(rule.IsSatisfiedBy(["true"]));
        Assert.False(rule.IsSatisfiedBy(["false"]));
    }

    // ── reading values out of a resolved fragment ─────────────────────────────

    /// <summary>
    /// A fan-out path resolves to an array of the per-element values, a plain path to a single
    /// scalar. A rule should not have to care which it got.
    /// </summary>
    [Theory]
    [InlineData("[\"Pension\",\"Investment\"]", new[] { "Pension", "Investment" })]
    [InlineData("\"Pension\"", new[] { "Pension" })]
    [InlineData("true", new[] { "true" })]
    [InlineData("false", new[] { "false" })]
    [InlineData("[]", new string[0])]
    [InlineData("null", new string[0])]
    public void ScalarsAreFlattenedOutOfWhateverShapeResolved(string json, string[] expected) =>
        Assert.Equal(expected, CheckPlanRunner.ScalarsOf(json));

    [Fact]
    public void NestedArraysAreFlattened() =>
        Assert.Equal(["a", "b"], CheckPlanRunner.ScalarsOf("[[\"a\"],[\"b\"]]"));

    [Fact]
    public void UnparseableJsonYieldsNothingRatherThanThrowing() =>
        Assert.Empty(CheckPlanRunner.ScalarsOf("{not json"));

    // ── Skip and its older spelling ───────────────────────────────────────────

    [Theory]
    [InlineData("Skip", true)]
    [InlineData("ReturnNA", true)]
    [InlineData("skip", true)]
    [InlineData("Continue", false)]
    [InlineData("ContinueWithReducedScope", false)]
    [InlineData(null, false)]
    public void SkipAndReturnNaBothSettleTheCheck(string? onAbsent, bool settles) =>
        Assert.Equal(settles, new PlanTriggerProbe { OnAbsent = onAbsent }.ReturnsNotApplicable);

    // ── the shipped plans ─────────────────────────────────────────────────────

    private static string PlanFolder => Path.Combine(AppContext.BaseDirectory, "check-plan");

    [Fact]
    public void EveryShippedPlanDeclaresItsApplicability()
    {
        var (plans, failures) = CheckQueryPlanLoader.Load(PlanFolder);

        Assert.Empty(failures);
        Assert.Equal(10, plans.Count);

        foreach (var plan in plans.Values)
        {
            var rules = plan.TriggerProbe?.Applicability ?? [];

            Assert.True(rules.Count > 0, $"{plan.CheckId} declares no applicability rules.");

            foreach (var rule in rules)
            {
                Assert.True(rule.CanonicalPaths.Count > 0, $"{plan.CheckId} {rule.Name}: no paths.");
                Assert.True(rule.AcceptedValues.Count > 0, $"{plan.CheckId} {rule.Name}: no values.");
            }
        }
    }

    /// <summary>
    /// Every path an applicability rule reads must exist in the canonical model. A typo here
    /// resolves to nothing, the rule fails, and the check is skipped on every case — silently,
    /// because a skipped check looks exactly like one that correctly did not apply.
    /// </summary>
    [Fact]
    public void EveryApplicabilityPathExistsInTheCanonicalModel()
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);
        var schema = JsonDocument.Parse(File.ReadAllText(SchemaPath));

        foreach (var plan in plans.Values)
        {
            foreach (var rule in plan.TriggerProbe?.Applicability ?? [])
            {
                foreach (var path in rule.CanonicalPaths)
                {
                    Assert.True(
                        PathExists(schema.RootElement, path),
                        $"{plan.CheckId} {rule.Name}: {path} is not in the canonical schema.");
                }
            }
        }
    }

    /// <summary>
    /// A check that runs on every case must not carry Skip: an unmet rule would then take it
    /// out of a run it belongs in, and CHK-010's own history is that a missing vulnerability
    /// flag is evidence nobody looked, not evidence there is nothing to find.
    /// </summary>
    [Theory]
    [InlineData("CHK-002")]
    [InlineData("CHK-004")]
    [InlineData("CHK-008")]
    [InlineData("CHK-010")]
    public void ChecksThatApplyToEveryCaseDoNotSkip(string checkId)
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);

        Assert.False(plans[checkId].TriggerProbe?.ReturnsNotApplicable);
    }

    private static string SchemaPath =>
        Path.Combine(AppContext.BaseDirectory, "canonical-suitability-model.schema.json");

    private static bool PathExists(JsonElement schema, string path)
    {
        var node = schema;

        foreach (var raw in path.Trim('/').Split('/'))
        {
            var name = raw.EndsWith("[]", StringComparison.Ordinal) ? raw[..^2] : raw;

            node = Deref(schema, node);

            if (!node.TryGetProperty("properties", out var properties)
                || !properties.TryGetProperty(name, out var next))
            {
                return false;
            }

            node = Deref(schema, next);

            if (raw.EndsWith("[]", StringComparison.Ordinal))
            {
                if (!node.TryGetProperty("items", out var items))
                {
                    return false;
                }

                node = Deref(schema, items);
            }
        }

        return true;
    }

    private static JsonElement Deref(JsonElement schema, JsonElement node)
    {
        while (node.ValueKind == JsonValueKind.Object
               && node.TryGetProperty("$ref", out var reference))
        {
            var name = reference.GetString()!.Split('/')[^1];
            node = schema.GetProperty("$defs").GetProperty(name);
        }

        return node;
    }
}
