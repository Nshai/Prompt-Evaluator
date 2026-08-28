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

    // ── absence discipline ────────────────────────────────────────────────────

    /// <summary>
    /// The sharpest single result in the two-model comparison, and the rule it bought.
    ///
    /// Both runs received byte-identical packs. Both reported a questionnaire as absent from the
    /// case file while a passage carrying it sat at rank 6 of 24 in the very pack being read. The
    /// stronger model contradicted itself inside one check: one group cited the passage id the
    /// document had arrived under, and another, one pack away, said the file held no such
    /// document.
    ///
    /// The attractive explanation was requirement confusion — the weaker run misidentified which
    /// requirement it was answering in 49 of 78 responses. The stronger run scored 0 of 78 and
    /// missed it anyway, which rules that out and leaves the assessor's own reading of the pack.
    ///
    /// So the rule is narrow and mechanical: check before declaring an absence, and separate "it
    /// is here and does not answer the question" from "it is not here". They are different
    /// findings with different remedies, and the first is far more common than the runs suggest.
    /// </summary>
    [Fact]
    public void TheRulesRequireCheckingThePackBeforeDeclaringADocumentAbsent()
    {
        Assert.Contains("Before reporting that the file holds no document", Prompt);
        Assert.Contains("Where a passage of that kind is present you may not call it absent", Prompt);
    }

    /// <summary>
    /// Severity is where uncertainty goes, not the decision to report at all.
    ///
    /// The model swap that fixed several false positives also <b>lost three findings the cheaper
    /// model caught</b>, one of them a headline defect of the case, by raising less. Better
    /// calibration should buy fewer spurious findings and not fewer real ones, and this is the
    /// clause that separates the two. Merged into the existing "do not soften" rule rather than
    /// added beside it: they are one instruction about the same reflex.
    /// </summary>
    [Fact]
    public void TheRulesPutUncertaintyInSeverityRatherThanInSilence()
    {
        Assert.Contains("a difference you cannot weigh is a Low finding, not a silence", Prompt);
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
    ///
    /// <b>6,000 from 5,500, and this time the raise was paid for.</b> What bought it: one rule,
    /// on not calling a document absent when a passage of that kind is in the pack. Two runs of
    /// one case, on two models at a 3.3× cost difference, received byte-identical packs, and both
    /// reported a questionnaire as missing from the file while it sat at rank 6 of 24 in the pack
    /// they were reading. The stronger run contradicted itself inside one check — one group naming
    /// the passage id the document arrived under, another saying the file held no such document.
    ///
    /// Prompt adherence rules out the obvious explanation: it went from 49 of 78 responses
    /// misidentifying their own requirement to 0 of 78, and the miss survived unchanged.
    ///
    /// What paid for it, per the note above: the clause telling the assessor that a plan's
    /// categorisation steer is "a steer and not a menu". That is printed verbatim by
    /// <c>CheckPlanRunner.BuildGroupPrompt</c> directly beneath the steer itself, where it is
    /// adjacent to the thing it qualifies. Two copies of one rule, and the standing one was the
    /// weaker placement.
    ///
    /// Two further rules bought by the same comparison are deliberately <i>not</i> here. They
    /// govern how to read a comparison whose two sides sit on different bases, they can only bite
    /// on a group that has a comparison, and they are printed in that group's "How to compare"
    /// block. A rule that cannot apply to two thirds of the groups reading it does not belong in
    /// the text every group reads.
    /// </summary>
    [Fact]
    public void ThePromptStaysShortEnoughToBeReadEveryTime()
    {
        Assert.InRange(Raw.Length, 2_000, 6_000);
    }

    /// <summary>
    /// No prompt may name a provider, a client or a document from any particular case.
    ///
    /// <b>The pipeline validates whichever suitability report it is given, against whichever
    /// evidence came with it.</b> Both change per case: a provider central to one file appears
    /// nowhere in the next. A prompt naming one teaches the model to look for it, which is a bias
    /// toward the case the rule was written from and away from the case in front of it — and it is
    /// an easy mistake to make, because the rule is nearly always discovered by reading one case.
    ///
    /// It was made here. A rule about the same charge being tabulated twice was added to the
    /// extractor prompt with the provider, the two percentages and the direction of the error all
    /// named, on the reasoning that a concrete example teaches better than an abstract one. True in
    /// a code comment, where <see cref="DerivedFigures"/> and half this test file name the case
    /// freely; wrong in a prompt, which is input to a model rather than documentation for a reader.
    ///
    /// Illustrate the *shape* of the defect in the prompt and keep the instance in the comment.
    ///
    /// Note what is <i>not</i> banned. A document <b>type</b> — "a fact find", "the suitability
    /// report", "a risk profile questionnaire" — is domain vocabulary that holds for every case, and
    /// the prompt needs it to say which side of a comparison it is talking about. What must not
    /// appear is a particular <b>instance</b>: a provider, a client, a filename.
    /// </summary>
    [Theory]
    [InlineData("Standard Life")]
    [InlineData("Scottish Widows")]
    [InlineData("Zurich")]
    [InlineData("Aviva")]
    [InlineData("People's Pension")]
    [InlineData("Peoples Pension")]
    [InlineData("Sullivan")]
    [InlineData("Sea Cadets")]
    [InlineData("CDH Recruitment")]
    [InlineData("Defaqto")]
    [InlineData("Dynamic Planner")]
    [InlineData("Suitability Report Test")]
    [InlineData("Fact Find (Test")]
    public void NoPromptNamesAnythingFromOneParticularCase(string fromOneCase)
    {
        Assert.DoesNotContain(fromOneCase, Prompts.AssessorSystem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fromOneCase, Prompts.ExtractorSystem, StringComparison.OrdinalIgnoreCase);
    }
}
