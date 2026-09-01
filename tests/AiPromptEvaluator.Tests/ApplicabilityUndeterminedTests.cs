using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// What an applicability rule does when it cannot be evaluated at all.
///
/// <b>A check that skips costs nothing and says nothing, which is exactly what makes it dangerous
/// as the answer to a question nobody managed to ask.</b> The rules were built to narrow a run
/// before it spends a single embedding, and they read the stored canonical model to do it — so a
/// section the extraction failed on takes the check with it, silently, and the run reports N/A.
/// </summary>
public class ApplicabilityUndeterminedTests
{
    private readonly ITestOutputHelper _output;

    public ApplicabilityUndeterminedTests(ITestOutputHelper output) => _output = output;

    // ── the three verdicts ────────────────────────────────────────────────────

    [Fact]
    public void ARuleThatFindsItsValueApplies()
    {
        var outcome = Evaluate(
            SwitchRule(),
            Model("""{"existingArrangements": [{"adviceAction": "Switch"}]}"""));

        Assert.True(outcome.Verdict);
        Assert.Equal(["adviceActions"], outcome.Passed);
        Assert.Empty(outcome.Undetermined);
    }

    /// <summary>
    /// The behaviour that must survive: a section the extraction read, that genuinely says
    /// something else, still narrows the run. This is what the rules are for.
    /// </summary>
    [Fact]
    public void ARuleThatFindsTheWrongValueStillFails()
    {
        var outcome = Evaluate(
            SwitchRule(),
            Model("""{"existingArrangements": [{"adviceAction": "Retain"}]}"""));

        Assert.False(outcome.Verdict);
        Assert.Single(outcome.Failed);
        Assert.Empty(outcome.Undetermined);
        Assert.Contains("found Retain", outcome.Detail);
    }

    /// <summary>
    /// And so must this one: a section that was read and is genuinely empty is a real absence,
    /// and a real absence is still a failure. Nothing here excuses a check from applying because
    /// the report was silent — only because nobody read it.
    /// </summary>
    [Fact]
    public void ARuleThatFindsNothingInAReadSectionStillFails()
    {
        var outcome = Evaluate(SwitchRule(), Model("""{"existingArrangements": []}"""));

        Assert.False(outcome.Verdict);
        Assert.Single(outcome.Failed);
        Assert.Empty(outcome.Undetermined);
        Assert.Contains("nothing at", outcome.Detail);
    }

    /// <summary>
    /// The fix. The path resolves to nothing and the extraction report says why: the pass failed,
    /// so the miss says nothing about the advice. The check runs.
    /// </summary>
    [Fact]
    public void ARuleWhoseSectionWasNeverReadDoesNotSkipTheCheck()
    {
        var outcome = Evaluate(SwitchRule(), ModelWithFailedExtraction());

        _output.WriteLine(outcome.Detail);

        Assert.True(outcome.Verdict);
        Assert.Empty(outcome.Failed);
        Assert.Single(outcome.Undetermined);
    }

    /// <summary>
    /// And says so. A run whose findings rest on a model that is missing a section has to carry
    /// that on its face, or the output is indistinguishable from a clean one.
    /// </summary>
    [Fact]
    public void AnUndeterminedRuleIsReportedRatherThanPassedOver()
    {
        var outcome = Evaluate(SwitchRule(), ModelWithFailedExtraction());

        Assert.Contains("undetermined", outcome.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("runs rather than skipping", outcome.Detail);
        Assert.Contains("missing a section", outcome.Detail);
        Assert.Contains("/existingArrangements[]/adviceAction", outcome.Detail);
    }

    // ── the AND, which is where a skip actually comes from ────────────────────

    /// <summary>
    /// Undetermined must not rescue a check whose other rule genuinely failed. The rules are
    /// ANDed, and the point of the AND is that a rule can only ever narrow.
    /// </summary>
    [Fact]
    public void OneUndeterminedRuleDoesNotRescueAnotherThatFailed()
    {
        var probe = new PlanTriggerProbe
        {
            Applicability =
            [
                SwitchRule().Applicability[0],
                Rule("replacementOrSwitch", "/checkTriggers/hasReplacementOrSwitch", "true"),
            ],
        };

        var outcome = Evaluate(
            probe,
            Model(
                """
                {
                  "checkTriggers": { "hasReplacementOrSwitch": false },
                  "extractionReport": {
                    "expectedButAbsent": [
                      { "path": "/existingArrangements", "reason": "PresentButUnparseable" }
                    ]
                  }
                }
                """));

        Assert.False(outcome.Verdict);
        Assert.Single(outcome.Failed);
        Assert.Single(outcome.Undetermined);
    }

    /// <summary>
    /// A rule reading two paths is only undetermined when *every* one of them was unread. One
    /// readable path is enough to make the miss a real answer.
    /// </summary>
    [Fact]
    public void ARuleIsOnlyUndeterminedWhenEveryPathWasUnread()
    {
        var probe = new PlanTriggerProbe
        {
            Applicability =
            [
                Rule(
                    "adviceActions",
                    ["/existingArrangements[]/adviceAction", "/recommendations[]/action"],
                    "Switch"),
            ],
        };

        var outcome = Evaluate(probe, ModelWithFailedExtraction());

        Assert.False(outcome.Verdict);
        Assert.Empty(outcome.Undetermined);
    }

    [Fact]
    public void AProbeWithNoRulesHasNoVerdictAtAll()
    {
        var outcome = CheckPlanRunner.EvaluateApplicability(
            new PlanTriggerProbe(), Model("{}"));

        Assert.Null(outcome.Verdict);
        Assert.Empty(outcome.Detail);
    }

    // ── the shipped plan this was found on ────────────────────────────────────

    /// <summary>
    /// CHK-009 as it ships, against the model three observed runs actually produced. Before this
    /// fix the check settled as not applicable and five material findings — F9.1 to F9.5, three
    /// of them the benchmark's most severe — went unreported with no trace in the output.
    /// </summary>
    [Fact]
    public void TheShippedReplacementCheckRunsWhenItsSectionFailedToExtract()
    {
        var (plans, _) = CheckQueryPlanLoader.Load(
            Path.Combine(AppContext.BaseDirectory, "check-plan"));

        var probe = plans["CHK-009"].TriggerProbe;
        Assert.NotNull(probe);

        // The trigger itself survived: checkTriggers is its own extraction pass and succeeded.
        var model = Model(
            """
            {
              "checkTriggers": { "hasReplacementOrSwitch": true },
              "extractionReport": {
                "expectedButAbsent": [
                  { "path": "/existingArrangements", "reason": "PresentButUnparseable" }
                ]
              }
            }
            """);

        var outcome = CheckPlanRunner.EvaluateApplicability(probe!, model);

        _output.WriteLine(outcome.Detail);

        Assert.True(outcome.Verdict);
        Assert.Contains("adviceActions", string.Join(" ", outcome.Undetermined));

        // And the plan really would have skipped, which is what made this worth fixing.
        Assert.True(probe!.ReturnsNotApplicable);
    }

    // ──────────────────────────────────────────────

    private static CheckPlanRunner.ApplicabilityOutcome Evaluate(
        PlanTriggerProbe probe, CanonicalModelAccessor accessor) =>
        CheckPlanRunner.EvaluateApplicability(probe, accessor);

    private static CanonicalModelAccessor Model(string json) => new(json);

    private static CanonicalModelAccessor ModelWithFailedExtraction() =>
        Model(
            """
            {
              "extractionReport": {
                "expectedButAbsent": [
                  { "path": "/existingArrangements", "reason": "PresentButUnparseable" }
                ]
              }
            }
            """);

    private static PlanTriggerProbe SwitchRule() =>
        new()
        {
            Applicability =
            [
                Rule("adviceActions", "/existingArrangements[]/adviceAction", "Switch", "Encash"),
            ],
        };

    private static PlanApplicability Rule(string name, string path, params string[] values) =>
        Rule(name, [path], values);

    private static PlanApplicability Rule(
        string name, string[] paths, params string[] values) =>
        new()
        {
            CanonicalPaths = [.. paths],
            Named = new Dictionary<string, System.Text.Json.JsonElement>
            {
                [name] = System.Text.Json.JsonSerializer.SerializeToElement(values),
            },
        };
}
