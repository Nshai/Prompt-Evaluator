using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// What happens when a check's two applicability signals disagree.
///
/// <b>The asymmetry is the whole subject.</b> A check that runs when it need not have is visible
/// in the output and costs a handful of calls. A check that silently did not run costs findings
/// nobody can see missing, and prints beside the checks that ran and passed.
///
/// This was measured. A plan's applicability rule enumerated five members of an advice-action
/// vocabulary the canonical schema documents ten values for. Two runs of the same case, on two
/// different models with two independent extractions, both recorded the omitted sixth value —
/// because it was the correct value for that case — and the check settled as Not Applicable
/// before a single search ran. Six material findings went with it, three of them the most severe
/// in the case, and neither run showed anything wrong.
///
/// So the rule these tests pin is: <b>an applicability rule may narrow a check, never overrule a
/// trigger field that affirmatively says the check applies.</b> The narrowing direction that
/// makes rules worth having is pinned here too, because it must survive.
/// </summary>
public class TriggerNarrowingTests
{
    // ── the direction that changed ────────────────────────────────────────────

    /// <summary>
    /// The observed failure, and the one line that decides it. The trigger field says the check
    /// applies; a rule disagrees; the check runs, and says why.
    /// </summary>
    [Fact]
    public void AFailedRuleDoesNotOverruleATriggerFieldThatSaysTheCheckApplies()
    {
        var verdict = CheckPlanRunner.Combine(fromModel: true, fromApplicability: false, probePassages: 0);

        Assert.True(verdict.Applies);
        Assert.True(verdict.Narrowed);
    }

    /// <summary>
    /// And it is reported. A check that ran over the top of its own plan must not read like an
    /// ordinary assessment — the run has to say which of the two signals to go and look at.
    /// </summary>
    [Fact]
    public void OnlyTheOverruledCaseIsFlaggedAsNarrowed()
    {
        Assert.False(CheckPlanRunner.Combine(true, true, 0).Narrowed);
        Assert.False(CheckPlanRunner.Combine(false, false, 0).Narrowed);
        Assert.False(CheckPlanRunner.Combine(false, true, 0).Narrowed);
        Assert.False(CheckPlanRunner.Combine(null, false, 0).Narrowed);
        Assert.False(CheckPlanRunner.Combine(true, null, 0).Narrowed);
    }

    // ── the direction that must not change ────────────────────────────────────

    /// <summary>
    /// A rule may not rescue a check whose trigger field says no. This is what the rules are for:
    /// they narrow a run before it spends an embedding, and a rule that could switch a check back
    /// on would be doing the opposite of its job.
    /// </summary>
    [Fact]
    public void ARuleCannotRescueACheckWhoseTriggerFieldSaysNo()
    {
        var verdict = CheckPlanRunner.Combine(fromModel: false, fromApplicability: true, probePassages: 0);

        Assert.False(verdict.Applies);
        Assert.False(verdict.Narrowed);
    }

    [Fact]
    public void BothAgreeingIsBelieved()
    {
        Assert.True(CheckPlanRunner.Combine(true, true, 0).Applies);
        Assert.False(CheckPlanRunner.Combine(false, false, 0).Applies);
    }

    // ── one source, or neither ────────────────────────────────────────────────

    /// <summary>
    /// A probe with no rules is answered by its trigger field alone, in both directions. Null is
    /// "there were no rules", which is a different thing from a rule that ran and said no — and
    /// the distinction is exactly what stops a plan without rules being treated as a plan whose
    /// rules all failed.
    /// </summary>
    [Fact]
    public void WithNoRulesTheTriggerFieldDecides()
    {
        Assert.True(CheckPlanRunner.Combine(true, null, 0).Applies);
        Assert.False(CheckPlanRunner.Combine(false, null, 99).Applies);
    }

    /// <summary>
    /// A probe with no trigger field is answered by its rules alone.
    /// </summary>
    [Fact]
    public void WithNoTriggerFieldTheRulesDecide()
    {
        Assert.True(CheckPlanRunner.Combine(null, true, 0).Applies);
        Assert.False(CheckPlanRunner.Combine(null, false, 99).Applies);
    }

    /// <summary>
    /// Where neither source spoke, the probe searches are the fallback — and only there. A
    /// passage count must never be able to overturn a source that did speak, which is what the
    /// two tests above hold: 99 passages does not rescue either "no".
    /// </summary>
    [Fact]
    public void WithNeitherSourceThePassagesDecide()
    {
        Assert.True(CheckPlanRunner.Combine(null, null, 1).Applies);
        Assert.False(CheckPlanRunner.Combine(null, null, 0).Applies);
    }
}
