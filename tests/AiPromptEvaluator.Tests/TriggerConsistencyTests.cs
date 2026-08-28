using System.Text.Json.Nodes;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// A trigger the extraction set false that the model beside it contradicts.
///
/// <b>This exists because of the most expensive silent failure the project has had.</b> CHK-005 did
/// not run in Runs 15 and 16: <c>hasCapitalContributionsOrWithdrawals</c> came back false for a case
/// transferring £110,185 with a £3,305.55 initial fee, and both runs printed the check under
/// CHECKS CLEARED. A check that did not run looked exactly like one that passed, two analyses drew
/// conclusions from it before anyone noticed, and the recall figures either side are out of 32
/// findings rather than 36.
///
/// Run 17's extraction was genuine and got that trigger right, which fixed the instance and not the
/// class. The same artefact still has <c>hasComplexProduct</c> false beside a product the model
/// calls Complex with six named drivers, and it cost nothing only because no query plan reads that
/// trigger. Nothing guarantees the next one is as lucky.
///
/// <b>What this cannot see is worth stating.</b> It compares the model against itself, so it catches
/// a trigger contradicting the document it was derived from and never a trigger contradicting the
/// case file — the extraction reads the suitability report and nothing else, deliberately. A client
/// the report calls unimpaired while the file records no investment knowledge is a finding for
/// CHK-010 to make from retrieval, and there is nothing inconsistent in the model to find.
/// </summary>
public class TriggerConsistencyTests
{
    private static JsonObject Model(string triggers, string body = "") =>
        (JsonNode.Parse($"{{ \"checkTriggers\": {{ {triggers} }} {body} }}") as JsonObject)!;

    // ── the pairs ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The pair that cost CHK-005 two runs. A trigger saying no money moves, against an arrangement
    /// recording that it does.
    /// </summary>
    [Fact]
    public void AContributionAgainstATriggerSayingThereIsNoneIsCaught()
    {
        var model = Model(
            "\"hasCapitalContributionsOrWithdrawals\": false",
            ", \"existingArrangements\": [ { \"contributions\": [ { \"amount\": 100 } ] } ]");

        var found = Assert.Single(TriggerConsistency.Contradictions(model));

        Assert.Equal("hasCapitalContributionsOrWithdrawals", found.Trigger);
        Assert.Contains("records contributions", found.Evidence);
    }

    /// <summary>
    /// Run 17's own contradiction: the model calls the recommended product Complex, with six named
    /// drivers, while the trigger says the case has no complex product.
    /// </summary>
    [Fact]
    public void AProductTheModelCallsComplexAgainstATriggerSayingThereIsNoneIsCaught()
    {
        var model = Model(
            "\"hasComplexProduct\": false",
            ", \"knowledgeAndExperience\": { \"recommendedProductComplexity\": "
            + "[ { \"productName\": \"Aviva Platform Personal Pension\", \"complexity\": \"Complex\" } ] }");

        var found = Assert.Single(TriggerConsistency.Contradictions(model));

        Assert.Equal("hasComplexProduct", found.Trigger);
        Assert.Contains("Aviva Platform Personal Pension", found.Evidence);
    }

    /// <summary>
    /// The one that would have cost findings had CHK-010 been gated rather than an overlay.
    /// </summary>
    [Fact]
    public void ARecordedVulnerabilityAgainstATriggerSayingThereIsNoneIsCaught()
    {
        var model = Model(
            "\"hasVulnerabilityIndicators\": false",
            ", \"vulnerability\": { \"perClient\": [ { \"hasVulnerability\": \"Yes\" } ] }");

        var found = Assert.Single(TriggerConsistency.Contradictions(model));

        Assert.Equal("hasVulnerabilityIndicators", found.Trigger);
    }

    [Fact]
    public void AReplacedArrangementAgainstATriggerSayingNothingIsReplacedIsCaught()
    {
        var model = Model(
            "\"hasReplacementOrSwitch\": false",
            ", \"replacementAnalysis\": [ { \"cedingArrangementId\": \"EA4\" } ]");

        var found = Assert.Single(TriggerConsistency.Contradictions(model));

        Assert.Equal("hasReplacementOrSwitch", found.Trigger);
    }

    // ── what must not fire ────────────────────────────────────────────────────

    /// <summary>
    /// One way only. A trigger that is true where the model shows nothing costs a check that need
    /// not have run — one call, and visible in the output. Reporting it would put noise beside the
    /// signal that matters, which is a trigger switching a check off.
    /// </summary>
    [Fact]
    public void ATrueTriggerWithNothingBehindItIsNotReported()
    {
        var model = Model("\"hasComplexProduct\": true, \"hasReplacementOrSwitch\": true");

        Assert.Empty(TriggerConsistency.Contradictions(model));
    }

    /// <summary>
    /// A trigger the extraction omitted is a different problem. An absent trigger does not gate a
    /// check — the runner reports "no value for" it and falls through to the applicability rules —
    /// so reporting it here would describe a failure that did not happen.
    /// </summary>
    [Fact]
    public void AnAbsentTriggerIsNotAContradiction()
    {
        var model = Model(
            "\"hasReplacementOrSwitch\": null",
            ", \"replacementAnalysis\": [ { \"cedingArrangementId\": \"EA4\" } ]");

        Assert.Empty(TriggerConsistency.Contradictions(model));
    }

    [Fact]
    public void AFalseTriggerTheModelAgreesWithIsNotReported()
    {
        var model = Model(
            "\"hasReplacementOrSwitch\": false, \"hasComplexProduct\": false",
            ", \"replacementAnalysis\": []");

        Assert.Empty(TriggerConsistency.Contradictions(model));
    }

    /// <summary>
    /// "No" and "Unspecified" are the recorded answers for a client with no vulnerability, so a
    /// trigger agreeing with them is agreement and not a contradiction.
    /// </summary>
    [Theory]
    [InlineData("No")]
    [InlineData("Unspecified")]
    public void AClientRecordedWithoutVulnerabilityDoesNotContradictTheTrigger(string recorded)
    {
        var model = Model(
            "\"hasVulnerabilityIndicators\": false",
            $", \"vulnerability\": {{ \"perClient\": [ {{ \"hasVulnerability\": \"{recorded}\" }} ] }}");

        Assert.Empty(TriggerConsistency.Contradictions(model));
    }

    [Fact]
    public void AModelWithNoTriggersAtAllIsNotAnError()
    {
        Assert.Empty(TriggerConsistency.Contradictions(JsonNode.Parse("{}")));
        Assert.Empty(TriggerConsistency.Contradictions(null));
    }

    // ── the run that prompted it ──────────────────────────────────────────────

    /// <summary>
    /// Run 17's stored canonical model, which is in the repository. It carries one trigger this
    /// catches and the run said nothing: <c>hasComplexProduct</c> false beside a product the same
    /// section calls Complex with six named drivers.
    ///
    /// <b>And it draws the boundary of what this can see, which is worth pinning.</b> The Run 17
    /// analysis listed <c>hasVulnerabilityIndicators</c> alongside it as a wrong trigger. That was
    /// a conflation. The model records <c>hasVulnerability: "No"</c>, so the trigger agrees with the
    /// model it was derived from and there is nothing here to catch — the disagreement is with the
    /// case *file*, which records no investment knowledge and a monthly deficit, and which the
    /// extraction never reads by design. That is a finding for CHK-010 to make from retrieval, not
    /// a trigger defect. This check compares the model against itself and cannot do more.
    ///
    /// <c>hasCapitalContributionsOrWithdrawals</c> is the control: Run 17's live extraction got the
    /// trigger that cost CHK-005 two runs right, so it must not be reported here.
    /// </summary>
    [Fact]
    public void RunSeventeensStoredModelWouldHaveReportedItsWrongComplexityTrigger()
    {
        var path = Path.Combine(
            Repository(), "docs", "Test-Runs", "Test-Case 1", "Runtime-Logs", "latest", "Run-17",
            "canonical-model_ABC-99_20260827-095807.json");

        Assert.True(File.Exists(path), $"The archived model is missing: {path}");

        var found = TriggerConsistency.Contradictions(JsonNode.Parse(File.ReadAllText(path)))
            .Select(c => c.Trigger)
            .ToList();

        Assert.Contains("hasComplexProduct", found);

        // Consistent with the model, whatever the file says. See the summary.
        Assert.DoesNotContain("hasVulnerabilityIndicators", found);
        Assert.DoesNotContain("hasCapitalContributionsOrWithdrawals", found);
    }

    // ── deriving the triggers ─────────────────────────────────────────────────
    //
    // Same pairs, settled rather than reported. Upgrade-only: a false or absent trigger the data
    // supports is forced true, and a trigger the data does not support is never touched.

    /// <summary>
    /// The recurrence this change is for: hasComplexProduct false beside a Complex product is
    /// settled to true, and the stored model now carries the right value.
    /// </summary>
    [Fact]
    public void AFalseTriggerTheDataSupportsIsSettledToTrue()
    {
        var model = Model(
            "\"hasComplexProduct\": false",
            ", \"knowledgeAndExperience\": { \"recommendedProductComplexity\": "
            + "[ { \"productName\": \"Aviva Platform Personal Pension\", \"complexity\": \"Complex\" } ] }");

        var settled = Assert.Single(TriggerConsistency.Derive(model));

        Assert.Equal("hasComplexProduct", settled.Trigger);
        Assert.Equal("false", settled.From);
        Assert.Contains("Aviva Platform Personal Pension", settled.Evidence);
        Assert.True(model["checkTriggers"]!["hasComplexProduct"]!.GetValue<bool>());
    }

    /// <summary>An absent trigger the data supports is written true, not left for the runner to miss.</summary>
    [Fact]
    public void AnAbsentTriggerTheDataSupportsIsSettledToTrue()
    {
        var model = Model(
            "\"hasReplacementOrSwitch\": null",
            ", \"replacementAnalysis\": [ { \"cedingArrangementId\": \"EA4\" } ]");

        var settled = Assert.Single(TriggerConsistency.Derive(model));

        Assert.Equal("hasReplacementOrSwitch", settled.Trigger);
        Assert.Equal("absent", settled.From);
        Assert.True(model["checkTriggers"]!["hasReplacementOrSwitch"]!.GetValue<bool>());
    }

    /// <summary>A trigger already true and agreeing with the data is a no-op — nothing to settle, nothing logged.</summary>
    [Fact]
    public void ATrueTriggerTheDataSupportsIsLeftAloneAndNotReported()
    {
        var model = Model(
            "\"hasReplacementOrSwitch\": true",
            ", \"replacementAnalysis\": [ { \"cedingArrangementId\": \"EA4\" } ]");

        Assert.Empty(TriggerConsistency.Derive(model));
        Assert.True(model["checkTriggers"]!["hasReplacementOrSwitch"]!.GetValue<bool>());
    }

    /// <summary>
    /// The invariant that keeps this safe: a trigger the data does not support is left exactly as the
    /// model wrote it, never forced false. Code turns a check on, never silently off.
    /// </summary>
    [Fact]
    public void ATriggerTheDataDoesNotSupportIsNeverDowngraded()
    {
        var model = Model(
            "\"hasReplacementOrSwitch\": true, \"hasComplexProduct\": false",
            ", \"replacementAnalysis\": []");

        Assert.Empty(TriggerConsistency.Derive(model));

        // The true one the data does not corroborate stays true; the false one stays false.
        Assert.True(model["checkTriggers"]!["hasReplacementOrSwitch"]!.GetValue<bool>());
        Assert.False(model["checkTriggers"]!["hasComplexProduct"]!.GetValue<bool>());
    }

    [Fact]
    public void AModelWithNoTriggersIsANoOp()
    {
        Assert.Empty(TriggerConsistency.Derive(JsonNode.Parse("{}")));
        Assert.Empty(TriggerConsistency.Derive(null));
    }

    /// <summary>
    /// Run 17's stored model, the control: the code settles its wrong hasComplexProduct to true while
    /// leaving hasCapitalContributionsOrWithdrawals (already correct) untouched — the mirror of the
    /// contradiction test above.
    /// </summary>
    [Fact]
    public void RunSeventeensStoredModelHasItsComplexityTriggerSettledAndNothingElseDowngraded()
    {
        var path = Path.Combine(
            Repository(), "docs", "Test-Runs", "Test-Case 1", "Runtime-Logs", "latest", "Run-17",
            "canonical-model_ABC-99_20260827-095807.json");

        Assert.True(File.Exists(path), $"The archived model is missing: {path}");

        var model = JsonNode.Parse(File.ReadAllText(path));
        var settled = TriggerConsistency.Derive(model).Select(d => d.Trigger).ToList();

        Assert.Contains("hasComplexProduct", settled);
        Assert.True(model!["checkTriggers"]!["hasComplexProduct"]!.GetValue<bool>());

        // Already correct in the live extraction, so there was nothing to settle.
        Assert.DoesNotContain("hasCapitalContributionsOrWithdrawals", settled);
    }

    private static string Repository()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiPromptEvaluator.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        return dir!.FullName;
    }
}
