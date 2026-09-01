using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// A section a plan named must survive into the pack, not merely rank above the passages it beats.
///
/// Stage 7 added section hints and ordered matches higher. Four benchmark findings landed. Two did
/// not, and the instrumentation added in Stage 8 said why: the declarations naming the client's
/// residency **matched a candidate passage and were evicted before the pack was built**. The floor
/// guarantees one slot per *category*, so a named section competes on score against every other
/// passage of the same category and loses.
///
/// This is the same defect as the category floor, one level down. Ranking is not keeping — the
/// lesson Stage 5 learned about categories and Stage 7 did not carry down to sections.
/// </summary>
public class SectionSlotTests
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
    private static CaseDocumentSearchMatch Passage(
        string document, string category, double score, string? text = null) =>
        new(text ?? $"passage from {document}", "ABC-99", 99, document, category, category, score);

    /// <summary>
    /// The observed shape: one quiet Fact Find passage carrying the named section, buried under a
    /// full pack of louder passages from the same category.
    /// </summary>
    [Fact]
    public void ANamedSectionSurvivesAFullPackOfBetterScoringSiblings()
    {
        var passages = Enumerable
            .Range(0, 20)
            .Select(i => Passage($"loud{i:00}.md", "B", 0.90 - (i * 0.001), $"unrelated section {i}"))
            .Append(Passage("Fact Find (Test 1).md", "B", 0.31,
                "## Personal Details\nResidency Status | Tenant - private"))
            .ToList();

        var ranked = Rank(passages, new HashSet<string> { "B" }, ["Residency"]);

        Assert.Contains(ranked, p => p.SearchedText.Contains("Tenant - private", StringComparison.Ordinal));
    }

    /// <summary>Without the declaration it is evicted — which is what happened for two runs.</summary>
    [Fact]
    public void WithoutTheDeclarationTheSamePassageIsEvicted()
    {
        var passages = Enumerable
            .Range(0, 20)
            .Select(i => Passage($"loud{i:00}.md", "B", 0.90 - (i * 0.001), $"unrelated section {i}"))
            .Append(Passage("Fact Find (Test 1).md", "B", 0.31,
                "## Personal Details\nResidency Status | Tenant - private"))
            .ToList();

        var ranked = Rank(passages, new HashSet<string> { "B" });

        Assert.DoesNotContain(ranked, p => p.SearchedText.Contains("Tenant - private", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryDeclaredSectionPresentGetsASlot()
    {
        var passages = Enumerable
            .Range(0, 20)
            .Select(i => Passage($"loud{i:00}.md", "B", 0.90, $"unrelated {i}"))
            .Append(Passage("ff-a.md", "B", 0.20, "## Current Monthly Cash Flow | -288.00"))
            .Append(Passage("ff-b.md", "B", 0.20, "## Total Net Monthly Income | 1,430.00"))
            .Append(Passage("ff-c.md", "B", 0.20, "## Money Purchase Pension Schemes | Aviva"))
            .ToList();

        var ranked = Rank(
            passages,
            new HashSet<string> { "B" },
            ["Current Monthly Cash Flow", "Total Net Monthly Income", "Money Purchase Pension Schemes"]);

        Assert.Equal(3, ranked.Count(p => p.Score == 0.20));
    }

    /// <summary>
    /// The category floor still holds. A section slot usually satisfies the category slot as a
    /// side effect, but a category with no named section must not lose its guarantee because
    /// another category's section took the reservation first.
    /// </summary>
    [Fact]
    public void TheCategoryFloorStillHolds()
    {
        var passages = Enumerable
            .Range(0, 20)
            .Select(i => Passage($"loud{i:00}.md", "I", 0.90, $"report prose {i}"))
            .Append(Passage("ff.md", "B", 0.20, "## Current Monthly Cash Flow | -288.00"))
            .Append(Passage("provider.md", "E", 0.10, "policy schedule"))
            .ToList();

        var ranked = Rank(
            passages, new HashSet<string> { "B", "E", "I" }, ["Current Monthly Cash Flow"]);

        Assert.Contains(ranked, p => p.CategoryCode == "B");
        Assert.Contains(ranked, p => p.CategoryCode == "E");
        Assert.Contains(ranked, p => p.CategoryCode == "I");
    }

    /// <summary>
    /// Reserving does not displace the ranking beyond the slots it claims. The rest of the pack
    /// is still the best of what is left, in order.
    /// </summary>
    [Fact]
    public void TheReservationDoesNotDisplaceTheRestOfTheRanking()
    {
        var passages = Enumerable
            .Range(0, 20)
            .Select(i => Passage($"loud{i:00}.md", "B", 0.90 - (i * 0.01), $"unrelated {i}"))
            .Append(Passage("ff.md", "B", 0.05, "## Current Monthly Cash Flow | -288.00"))
            .ToList();

        var ranked = Rank(passages, new HashSet<string> { "B" }, ["Current Monthly Cash Flow"]);

        Assert.Equal(Packed.MaxPassagesPerGroup, ranked.Count);
        Assert.Contains(ranked, p => p.Score == 0.05);

        // The other eleven are the eleven best, still in descending score order.
        var rest = ranked.Where(p => p.Score > 0.05).ToList();
        Assert.Equal(rest.OrderByDescending(p => p.Score).ToList(), rest);
    }

    /// <summary>
    /// A section naming nothing costs nothing. G1.7 declared a heading no document carries for
    /// five runs; the failure mode must be "no effect", never "an empty slot".
    /// </summary>
    [Fact]
    public void ADeclarationMatchingNothingDoesNotConsumeASlot()
    {
        var passages = Enumerable
            .Range(0, 20)
            .Select(i => Passage($"doc{i:00}.md", "B", 0.90 - (i * 0.01), $"unrelated {i}"))
            .ToList();

        var withHint = Rank(passages, new HashSet<string> { "B" }, ["Existing Pension Provision"]);
        var without = Rank(passages, new HashSet<string> { "B" });

        Assert.Equal(without.Select(p => p.SearchedText), withHint.Select(p => p.SearchedText));
    }

    /// <summary>
    /// Reproducible regardless of the order the plan lists its sections in — the same reason the
    /// category floor iterates in ordinal order.
    /// </summary>
    [Fact]
    public void ThePackDoesNotDependOnTheOrderSectionsWereListed()
    {
        var passages = Enumerable
            .Range(0, 20)
            .Select(i => Passage($"loud{i:00}.md", "B", 0.90, $"unrelated {i}"))
            .Append(Passage("a.md", "B", 0.20, "## Current Monthly Cash Flow"))
            .Append(Passage("b.md", "B", 0.20, "## Total Net Monthly Income"))
            .ToList();

        var one = Rank(passages, new HashSet<string> { "B" },
            ["Current Monthly Cash Flow", "Total Net Monthly Income"]);
        var two = Rank(passages, new HashSet<string> { "B" },
            ["Total Net Monthly Income", "Current Monthly Cash Flow"]);

        Assert.Equal(one.Select(p => p.SearchedText), two.Select(p => p.SearchedText));
    }
}
