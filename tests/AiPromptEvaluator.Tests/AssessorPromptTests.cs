using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The three standing rules added after reading what the assessor actually did with its pack.
///
/// A prompt is the one part of this pipeline with no compiler and no schema behind it: a rule can
/// be reworded into something that still reads well and no longer does its job, and nothing fails
/// until a run is scored weeks later. These pin the rules by the behaviour each was written for.
/// </summary>
public class AssessorPromptTests
{
    /// <summary>
    /// Whitespace-collapsed, because the prompt is a wrapped raw string literal and a rule can
    /// be broken across two lines. Matching the wrapping rather than the rule would make these
    /// tests fail on a reflow that changed nothing, which is the fastest way to have them
    /// deleted.
    /// </summary>
    private static string Prompt =>
        System.Text.RegularExpressions.Regex.Replace(Prompts.AssessorSystem, @"\s+", " ");

    private static string Raw => Prompts.AssessorSystem;

    // ── 1. The derived figures were in every prompt and named in none ─────────

    /// <summary>
    /// All eighty-five group prompts of 2026-08-26 carried the arithmetic proving a charge row
    /// was computed on the wrong plan. One check in ten used it. The standing rules — which say
    /// what to think about and in what order — did not mention the derived figures at all, so
    /// their only introduction was a heading in the middle of a long user prompt.
    /// </summary>
    [Fact]
    public void ThePackDescriptionNamesTheDerivedFigures()
    {
        Assert.Contains("DERIVED FIGURES", Prompt);
        Assert.Contains("the calculation the report should agree with", Prompt);
    }

    /// <summary>
    /// And says what to do with one, which is the part that was missing: a derived figure is a
    /// finding on its own terms and does not need a passage to corroborate it, because it was
    /// computed from the report's own numbers.
    /// </summary>
    [Fact]
    public void TheRulesSayADerivedFigureIsAFindingWithoutCorroboration()
    {
        Assert.Contains("Check the report against the derived figures rather than re-deriving them", Prompt);
        Assert.Contains("needs no passage to corroborate it", Prompt);
        Assert.Contains("computed from the report's own numbers", Prompt);
    }

    // ── 2. A guard explains a difference, not the report's own claim ──────────

    /// <summary>
    /// The People's Pension is retained at risk 9 against an agreed 5 while the report says that
    /// fund "aligns with your risk appetite". Every model in a three-model comparison discarded
    /// it, because the guard rule was unqualified and a guard about incomparable scales appeared
    /// to cover it. The guard covered the comparison; it never covered the claim.
    /// </summary>
    [Fact]
    public void TheGuardRuleIsBoundedToDifferences()
    {
        Assert.Contains("A guard explains a DIFFERENCE between two documents", Prompt);
        Assert.Contains("does not excuse an assertion the report makes for itself", Prompt);
        Assert.Contains("why that claim is unsupported, not a reason to drop it", Prompt);
    }

    /// <summary>
    /// The qualification must not swallow the rule. Guards exist because this comparison produces
    /// spurious mismatches, and a prompt that stopped saying so would trade a false negative for
    /// a flood of false positives on a dataset that cannot currently measure them.
    /// </summary>
    [Fact]
    public void TheGuardRuleStillStands()
    {
        Assert.Contains("Respect the false-positive guards", Prompt);
        Assert.Contains("a finding one of them explains is not a finding", Prompt);
    }

    // ── 3. Two halves in two fields is not a finding ──────────────────────────

    /// <summary>
    /// The most common Partial verdict in scoring, and the rubric's own worked example of one:
    /// the report's figure in "reportSays", the contradicting figure in "fileSays", and nothing
    /// in "discrepancies" relating them. "List every discrepancy you can see" is a request for
    /// diligence; this is a step.
    /// </summary>
    [Fact]
    public void TheRulesRequireReportSaysAndFileSaysToBeReconciled()
    {
        Assert.Contains("read \"reportSays\" and \"fileSays\" back against each other", Prompt);
        Assert.Contains("must appear in \"discrepancies\"", Prompt);

        // With the escape hatch that keeps it honest: sometimes two figures genuinely are not
        // comparable, and forcing a discrepancy would manufacture one.
        Assert.Contains("why the two are not comparable", Prompt);
    }

    // ── what must not be lost ─────────────────────────────────────────────────

    /// <summary>
    /// The parts that were doing measurable work before any of this. Field order as reasoning
    /// order is why the verdict is not written first and justified afterwards; listing every
    /// difference before weighing it is why a guard-suppressed finding still reaches the report
    /// through DiscardedDiscrepancies.
    /// </summary>
    [Theory]
    [InlineData("It is not evidence that the assertion is true")]
    [InlineData("Do not decide first and explain afterwards")]
    [InlineData("Put EVERY difference between the two sides in \"discrepancies\"")]
    [InlineData("A difference explained by a guard still goes in the list")]
    [InlineData("Do not soften, hedge or omit a contradiction")]
    [InlineData("Return one JSON object and nothing else")]
    public void TheRulesThatWereAlreadyEarningTheirPlaceSurvive(string rule)
    {
        Assert.Contains(rule, Prompt);
    }

    /// <summary>
    /// Three rules were added to a prompt that a model reads in full before every one of eighty-
    /// eight calls. Length is a cost paid eighty-eight times per run, and a prompt that grows
    /// without bound stops being read carefully at the end.
    ///
    /// <b>5,500 from 5,000, for the rule that category [I] cannot corroborate a claim the report
    /// makes.</b> The ceiling is a judgement rather than a limit, and it is raised here rather than
    /// met by trimming, because meeting it meant compressing rules each written for an observed
    /// failure. Two were compressed to pay for most of this one; the rest is admitted as cost.
    ///
    /// What bought it: 151 of Run 17's 558 cited passages — 27% — were the suitability report,
    /// and G1.11 wrote "the inflation assumptions … are corroborated by [P11] and [P12]" where both
    /// are the report. Run 15 did the same on income and expenditure and Run 16 got the same
    /// requirement right, so it fires intermittently, which is harder to catch in review than a
    /// consistent fault.
    ///
    /// Raising it again should need the same kind of evidence. If it reaches 6,000, the right move
    /// is probably to cut a rule that has stopped earning its place rather than to widen again.
    /// </summary>
    [Fact]
    public void ThePromptStaysShortEnoughToBeReadEveryTime()
    {
        Assert.InRange(Raw.Length, 2_000, 5_500);
    }
}
