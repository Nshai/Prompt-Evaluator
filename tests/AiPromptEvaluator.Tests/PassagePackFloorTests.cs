using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// A category a group asked for must survive the cut.
///
/// Measured on Run 2 of case ABC-99: the query plans were repaired so that CHK-007, CHK-008 and
/// CHK-009 all ask for the Fact Find, the search duly retrieved it — and three of those checks
/// still reached it in **zero groups out of nineteen**, every affected pack sitting exactly at
/// the twelve-passage cap. Ranking by "is this category targeted" and then by score does nothing
/// once every candidate is targeted, and Fact Find prose embeds further from these queries than
/// research or report prose does. The passages were fetched and then thrown away.
///
/// Six of the eight benchmark findings missed by that run were facts recorded only in the Fact
/// Find.
/// </summary>
public class PassagePackFloorTests
{

    /// <summary>
    /// These tests are about what happens when the pack is full, so they pin the cap rather
    /// than inherit it. <see cref="AppSettings.MaxPassagesPerGroup"/> is a setting now, and a
    /// test that reshapes its fixtures every time the default moves is testing the default.
    /// </summary>
    private static readonly AppSettings Packed = new() { MaxPassagesPerGroup = 12 };

    private static List<CaseDocumentSearchMatch> Rank(
        IEnumerable<CaseDocumentSearchMatch> passages,
        IReadOnlySet<string> targeted,
        IReadOnlyList<string>? sections = null,
        IReadOnlySet<string>? declared = null) =>
        CheckPlanRunner.Rank(passages, targeted, sections, declared, Packed);
    /// <summary>
    /// CHK-009 G9.1 in miniature: five targeted categories, the declared one scoring worst, and
    /// more candidates than the pack holds. Before the floor this returned no B at all.
    /// </summary>
    [Fact]
    public void ADeclaredCategoryIsNotEvictedByBetterScoringNeighbours()
    {
        var passages = new List<CaseDocumentSearchMatch>();

        foreach (var category in new[] { "E", "G", "H", "I" })
        {
            for (var i = 0; i < 6; i++)
            {
                passages.Add(Passage($"{category}-doc{i:00}.md", category, 0.70 - (i * 0.01)));
            }
        }

        // The Fact Find, scoring below every one of them — which is the real case, not a
        // pessimistic one.
        passages.Add(Passage("Fact Find.md", "B", 0.51, "Total Monthly Disposable Income £-288.00"));

        var ranked = Rank(passages, Targets("B", "E", "G", "H", "I"));

        Assert.Equal(Packed.MaxPassagesPerGroup, ranked.Count);
        Assert.Contains(ranked, p => p.CategoryCode == "B");
        Assert.Contains(ranked, p => p.SearchedText.Contains("-288.00"));
    }

    /// <summary>
    /// Every targeted category with a candidate gets one, so a group cannot be blind to a
    /// document class it named.
    /// </summary>
    [Fact]
    public void EveryTargetedCategoryPresentGetsASlot()
    {
        var passages = new List<CaseDocumentSearchMatch>();

        for (var i = 0; i < 12; i++)
        {
            passages.Add(Passage($"loud{i:00}.md", "I", 0.9));
        }

        foreach (var category in new[] { "A", "B", "C", "D" })
        {
            passages.Add(Passage($"quiet-{category}.md", category, 0.51));
        }

        var ranked = Rank(passages, Targets("A", "B", "C", "D", "I"));

        foreach (var category in new[] { "A", "B", "C", "D", "I" })
        {
            Assert.Contains(ranked, p => p.CategoryCode == category);
        }
    }

    /// <summary>
    /// The floor holds one slot, not the pack. Eight of twelve slots still go to the ranking
    /// when four categories are declared.
    /// </summary>
    [Fact]
    public void TheFloorDoesNotDisplaceTheRanking()
    {
        var passages = new List<CaseDocumentSearchMatch>();

        for (var i = 0; i < 12; i++)
        {
            passages.Add(Passage($"best{i:00}.md", "I", 0.90 - (i * 0.001)));
        }

        foreach (var category in new[] { "A", "B", "C" })
        {
            passages.Add(Passage($"weak-{category}.md", category, 0.51));
        }

        var ranked = Rank(passages, Targets("A", "B", "C", "I"));

        Assert.Equal(Packed.MaxPassagesPerGroup, ranked.Count);
        Assert.Equal(9, ranked.Count(p => p.CategoryCode == "I"));
        Assert.Equal(3, ranked.Count(p => p.CategoryCode != "I"));
    }

    /// <summary>
    /// A category the group did not ask for has no claim on a slot, or the floor would undo the
    /// targeting it exists to protect.
    /// </summary>
    [Fact]
    public void AnUntargetedCategoryGetsNothingReserved()
    {
        var passages = new List<CaseDocumentSearchMatch>();

        for (var i = 0; i < 14; i++)
        {
            passages.Add(Passage($"wanted{i:00}.md", "B", 0.60));
        }

        passages.Add(Passage("unwanted.md", "H", 0.99));

        var ranked = Rank(passages, Targets("B"));

        Assert.DoesNotContain(ranked, p => p.CategoryCode == "H");
    }

    /// <summary>
    /// Nothing is displaced when nothing needs to be. A pack that fits comes back whole, in
    /// rank order.
    /// </summary>
    [Fact]
    public void APackThatFitsIsUnchanged()
    {
        var passages = new List<CaseDocumentSearchMatch>
        {
            Passage("a.md", "I", 0.9),
            Passage("b.md", "B", 0.5),
            Passage("c.md", "G", 0.7),
        };

        var ranked = Rank(passages, Targets("B", "G", "I"));

        Assert.Equal(3, ranked.Count);
        Assert.Equal(["a.md", "c.md", "b.md"], ranked.Select(p => p.DocumentName));
    }

    /// <summary>
    /// The pack still reads best-first. A passage admitted on the floor takes its place by
    /// score, not at the front — a reader meets the strongest evidence first either way.
    /// </summary>
    [Fact]
    public void ThePackIsStillEmittedInRankOrder()
    {
        var passages = new List<CaseDocumentSearchMatch>();

        for (var i = 0; i < 12; i++)
        {
            passages.Add(Passage($"strong{i:00}.md", "I", 0.90 - (i * 0.001)));
        }

        passages.Add(Passage("weak.md", "B", 0.51));

        var ranked = Rank(passages, Targets("B", "I"));

        Assert.Equal("weak.md", ranked[^1].DocumentName);
        Assert.True(
            ranked.Zip(ranked.Skip(1)).All(pair => pair.First.Score >= pair.Second.Score),
            "the pack is no longer ordered by score");
    }

    /// <summary>
    /// Determinism survives the floor. Which passages are held has to be a function of the
    /// passages and the targets, never of the order an approximate index returned them in, and
    /// never of the order the plan happened to list its categories.
    /// </summary>
    [Fact]
    public void TheFloorIsDeterministicWhateverOrderThingsArriveIn()
    {
        var passages = new List<CaseDocumentSearchMatch>();

        foreach (var category in new[] { "B", "E", "G", "I" })
        {
            for (var i = 0; i < 5; i++)
            {
                passages.Add(Passage($"{category}{i:00}.md", category, 0.60, $"text {category}{i:00}"));
            }
        }

        var forwards = Rank(passages, Targets("B", "E", "G", "I"));
        var backwards = Rank(Enumerable.Reverse(passages), Targets("B", "E", "G", "I"));
        var reordered = Rank(passages, Targets("I", "G", "E", "B"));

        Assert.Equal(forwards, backwards);
        Assert.Equal(forwards, reordered);
    }


    // ──────────────────────────────────────────────
    // Section routing — what Run 4 showed the floor alone cannot do
    // ──────────────────────────────────────────────

    private const string PensionFeatures =
        """
        | Lump Sum Cont. |  | Value | £116,555.45 |
        | Valuation Date | 23/07/2026 |
        | Protected Rights | No | BR19 Projection | Yes |
        """;

    private const string CashFlowSection =
        """
        ## Current Monthly Cash Flow
        | Total Net Monthly Income | £ 1,430.00 |
        | Total Monthly Expenditure | £ 1,718.00 |
        | Total Monthly Disposable Income | £ -288.00 |
        """;

    /// <summary>
    /// The measurement this exists for. In Run 4 the floor guaranteed the Fact Find a slot and
    /// the slot went to a pension-features table, while the section carrying the client's
    /// £-288.00 disposable income was handed to four groups that had no question it answered.
    /// Both passages are filled, both are the Fact Find, and only one is about income — so
    /// neither the floor nor content density can separate them. The plan has to say which part.
    /// </summary>
    [Fact]
    public void ASectionHintDecidesWhichPartOfADocumentWins()
    {
        var passages = new List<CaseDocumentSearchMatch>
        {
            Passage("Fact Find.md", "B", 0.64, PensionFeatures),
            Passage("Fact Find.md", "B", 0.55, CashFlowSection),
        };

        for (var i = 0; i < 12; i++)
        {
            passages.Add(Passage($"planning{i:00}.md", "F", 0.70 - (i * 0.01), $"cashflow row {i}"));
        }

        var ranked = Rank(
            passages,
            Targets("B", "F"),
            ["Total Monthly Disposable Income", "Total Net Monthly Income"]);

        var factFind = ranked.Where(p => p.CategoryCode == "B").ToList();

        Assert.NotEmpty(factFind);
        Assert.Contains("-288.00", factFind[0].SearchedText);
    }

    /// <summary>
    /// A hint promotes; it does not override the category targeting, or a group would start
    /// pulling in documents it never asked for because they happen to use the same words.
    /// </summary>
    [Fact]
    public void ASectionHintDoesNotAdmitAnUntargetedCategory()
    {
        var passages = new List<CaseDocumentSearchMatch>();

        for (var i = 0; i < 14; i++)
        {
            passages.Add(Passage($"wanted{i:00}.md", "B", 0.60, $"fact find row {i}"));
        }

        passages.Add(Passage("elsewhere.md", "H", 0.99, "Total Monthly Disposable Income appears here too"));

        var ranked = Rank(passages, Targets("B"), ["Total Monthly Disposable Income"]);

        Assert.DoesNotContain(ranked, p => p.CategoryCode == "H");
    }

    /// <summary>A plan that names no section ranks exactly as before.</summary>
    [Fact]
    public void NoHintChangesNothing()
    {
        var passages = new List<CaseDocumentSearchMatch>
        {
            Passage("a.md", "B", 0.9, "high"),
            Passage("b.md", "B", 0.5, "low"),
        };

        Assert.Equal(
            Rank(passages, Targets("B")),
            Rank(passages, Targets("B"), []));
    }

    /// <summary>
    /// An unfilled form matching the hint is still an unfilled form. Skeletons sink first, so a
    /// hint cannot drag one back up past a section that carries values.
    /// </summary>
    [Fact]
    public void AHintDoesNotRescueASkeleton()
    {
        var skeleton = Passage(
            "Fact Find.md", "B", 0.90,
            """
            | Total Monthly Disposable Income |  |  |
            | GMP Amount (p.a.) |  |  |
            | Enhanced Tax Free Cash |  |  |
            | Applicable Penalties |  |  |
            | Guaranteed Growth Rates |  |  |
            | Lifetime Allowance Used |  |  |
            | Options Available at Retirement |  |  |
            | Other Benefits and Material Features |  |  |
            | Lifestyling strategy |  |  |
            | Additional Notes |  |  |
            """);

        var ranked = Rank(
            [skeleton, Passage("Fact Find.md", "B", 0.50, CashFlowSection)],
            Targets("B"),
            ["Total Monthly Disposable Income"]);

        Assert.Contains("1,430.00", ranked[0].SearchedText);
    }

    private static IReadOnlySet<string> Targets(params string[] categories) =>
        new HashSet<string>(categories, StringComparer.OrdinalIgnoreCase);

    private static CaseDocumentSearchMatch Passage(
        string document, string category, double score, string? text = null) =>
        new(text ?? $"passage from {document}", "CASE-1", 99, document, category, category, score);
}
