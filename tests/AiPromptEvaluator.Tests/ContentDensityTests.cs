using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Telling an unfilled form from a filled one.
///
/// Both passages below are real, both come from the same Fact Find, and both are sparse tables.
/// The first is what the per-category floor admitted to the group that had to find the client's
/// disposable income; the second is the section that would have settled it. Emptiness does not
/// separate them — 80% against 68% empty cells — so the test is for values instead.
/// </summary>
public class ContentDensityTests
{
    /// <summary>
    /// CHK-005/G5.2's Fact Find passage, as retrieved. Seventeen filled cells, of which one
    /// carries a value; the rest name fields nobody completed.
    /// </summary>
    private const string FormSkeleton =
        """
        |                                                | In force | In force | Status    |
        | GMP Amount (p.a.)                              |          |          |           |
        | Enhanced Tax Free Cash                         |          |          |           |
        | Guaranteed Annuity Rate / Pension (or similar) |          |          |           |
        | Applicable Penalties                           |          |          |           |
        | EFI/Loyalty Bonus/Terminal Bonus               |          |          |           |
        | Guaranteed Growth Rates                        |          |          |           |
        | Death in service spousal benefits              | £ 0.00   |          |           |
        | Lifetime Allowance Used (%)                    |          |          |           |
        | Lifestyling strategy?                          |          |          |           |
        | Options Available at Retirement                |          |          |           |
        | Other Benefits and/or Material Features        |          |          |           |
        | Additional Notes                               |          |          |           |
        """;

    /// <summary>
    /// The income and expenditure section — the passage four missed findings depend on. Just as
    /// sparse, and full of answers.
    /// </summary>
    private const string IncomeTable =
        """
        | Entertainment                  |  |  | £ 100.00   | Monthly |
        | Life/General Assurance Premium |  |  |            |         |
        | Total Net Monthly Income       |  |  | £ 1,430.00 |         |
        | Total Monthly Expenditure      |  |  | £ 1,718.00 |         |
        | Total Monthly Disposable Income|  |  | £ -288.00  |         |
        """;

    [Fact]
    public void AnUnfilledFormIsRecognised()
    {
        Assert.True(ContentDensity.IsFormSkeleton(FormSkeleton));
    }

    [Fact]
    public void TheSectionThatMattersIsNot()
    {
        Assert.False(ContentDensity.IsFormSkeleton(IncomeTable));
    }

    /// <summary>
    /// The two other Fact Find passages the floor admitted on that run. Both are sparse and both
    /// carry values — one of them a pension total the benchmark does not yet account for — so
    /// neither may be demoted.
    /// </summary>
    [Theory]
    [InlineData("| Lump Sum Cont. |  |  |  |  | Value | £ 116,555.45 |\n|  |  |  | Valuation Date | 23/07/2026 |  |\n| Protected Rights |  | No |  | BR19 Projection | Yes |\n| Transfer Penalty |  | No |  | Waiver |  |")]
    [InlineData("| Statement 5 Answer | Yes |\n| Title | Mr |\n| Date of Birth | 07/06/1956 |\n| Age | 70 |\n| NI Number | YW377231C |\n| Gender | Male |")]
    public void ASparseTableThatCarriesAnswersIsNotASkeleton(string passage)
    {
        Assert.False(ContentDensity.IsFormSkeleton(passage));
    }

    // ──────────────────────────────────────────────
    // What it must not touch
    // ──────────────────────────────────────────────

    /// <summary>Prose is not a form, whatever it is about.</summary>
    [Fact]
    public void ProseIsNeverASkeleton()
    {
        Assert.False(ContentDensity.IsFormSkeleton(
            "You are in receipt of a full State Pension of approximately two hundred and thirty "
            + "pounds per week, and continue to work as an HGV driver. Your combined income "
            + "comfortably meets your household expenditure."));
    }

    /// <summary>
    /// A paragraph carrying one summary row is prose with a table in it, not a form, and the
    /// tabular-share guard is what keeps the two apart.
    /// </summary>
    [Fact]
    public void ProseWithATableInItIsJudgedOnTheWhole()
    {
        Assert.False(ContentDensity.IsFormSkeleton(
            "The adviser recorded the following against each heading of the risk questionnaire, "
            + "and in every case the note was left at its default, which is itself the point "
            + "worth recording here because it means no discussion was documented at all.\n"
            + "| Capacity for loss | |\n| Agreed risk level | |"));
    }

    /// <summary>
    /// A short table has not earned a judgement — two blank rows are as likely to be a summary
    /// as a form.
    /// </summary>
    [Fact]
    public void ATableTooShortToJudgeIsLeftAlone()
    {
        Assert.False(ContentDensity.IsFormSkeleton("| Capacity for loss |  |\n| Agreed risk level |  |"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no pipes here at all, just words")]
    public void NothingToJudgeIsNotASkeleton(string? text)
    {
        Assert.False(ContentDensity.IsFormSkeleton(text));
    }

    // ──────────────────────────────────────────────
    // And the effect on a pack
    // ──────────────────────────────────────────────

    /// <summary>
    /// The whole point. The floor holds a slot for the Fact Find; before this, the slot went to
    /// the skeleton because it scored higher. Now it goes to the section that carries the
    /// answers, and the skeleton is still there if nothing else is.
    /// </summary>
    [Fact]
    public void TheFloorNowAdmitsTheFilledSectionRatherThanTheForm()
    {
        var passages = new List<CaseDocumentSearchMatch>();

        for (var i = 0; i < 12; i++)
        {
            passages.Add(Passage($"planning{i:00}.md", "F", 0.70 - (i * 0.01), $"cashflow row {i}"));
        }

        // The skeleton embeds better than the section that matters, which is the real ordering.
        passages.Add(Passage("Fact Find.md", "B", 0.62, FormSkeleton));
        passages.Add(Passage("Fact Find.md", "B", 0.55, IncomeTable));

        var ranked = CheckPlanRunner.Rank(
            passages, new HashSet<string>(["B", "F"], StringComparer.OrdinalIgnoreCase));

        var factFind = ranked.Where(p => p.CategoryCode == "B").ToList();

        Assert.NotEmpty(factFind);
        Assert.Contains("-288.00", factFind[0].SearchedText);
    }

    private static CaseDocumentSearchMatch Passage(
        string document, string category, double score, string text) =>
        new(text, "CASE-1", 99, document, category, category, score);
}
