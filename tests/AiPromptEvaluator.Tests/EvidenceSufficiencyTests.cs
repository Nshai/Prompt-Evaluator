using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Two plan fields that were authored, printed, and then left to the model to act on.
///
/// <c>sufficiency.minEvidenceCategories</c> was printed into the prompt beside the categories
/// the pack actually reached, and whether a thinly evidenced requirement said so depended on
/// the assessor comparing two numbers in different parts of a long prompt.
///
/// <c>declares.evidenceCategories</c> — the group's own statement of where its answer lives —
/// had no retrieval effect at all: reserved slots went out in alphabetical order, so a declared
/// category could be squeezed out of the cap by one some query happened to mention.
/// </summary>
public class EvidenceSufficiencyTests
{
    private static CaseDocumentSearchMatch Passage(
        string category, double score, string text = "text", string document = "doc") =>
        new(text, "CASE-1", 1, document, category, category, score);

    private static PlanQueryGroup Group(int? minimum) =>
        new()
        {
            GroupId = "G1.1",
            Requirement = "A requirement",
            Retrieval = new PlanRetrieval
            {
                Queries = [new PlannedQuery { Id = "Q1", Side = "Evidence", TargetCategories = ["B"] }],
            },
            Verification = new PlanVerification
            {
                Sufficiency = new PlanSufficiency { MinEvidenceCategories = minimum },
            },
        };

    private static CheckPlanRunner.GroupEvidence Pack(
        PlanQueryGroup group, params string[] categoriesFound) =>
        new(group, [], [], Searches: 1, TotalPassages: categoriesFound.Length,
            CategoriesFound: categoriesFound, MissedSignals: [], SectionsReached: CheckPlanRunner.SectionReach.None);

    // ── 2. the shortfall is established in code ───────────────────────────────

    [Fact]
    public void FewerCategoriesThanTheMinimumIsAShortfall()
    {
        var pack = Pack(Group(minimum: 2), "B");

        Assert.True(pack.IsUnderEvidenced);
        Assert.Contains("requires corroboration from 2", pack.EvidenceShortfall);
        Assert.Contains("reached 1", pack.EvidenceShortfall);
        Assert.Contains("(B)", pack.EvidenceShortfall);
    }

    [Fact]
    public void MeetingTheMinimumIsNotAShortfall()
    {
        var pack = Pack(Group(minimum: 2), "B", "C");

        Assert.False(pack.IsUnderEvidenced);
        Assert.Null(pack.EvidenceShortfall);
    }

    [Fact]
    public void NoMinimumMeansNoShortfallToReport()
    {
        var pack = Pack(Group(minimum: null), "B");

        Assert.False(pack.IsUnderEvidenced);
        Assert.Null(pack.EvidenceShortfall);
    }

    [Fact]
    public void NoCategoriesAtAllReadsAsNoneRatherThanAnEmptyList()
    {
        var pack = Pack(Group(minimum: 1));

        Assert.True(pack.IsUnderEvidenced);
        Assert.Contains("none", pack.EvidenceShortfall);
    }

    /// <summary>
    /// A model-only group has no evidence side by design — it compares the report against
    /// itself. Reporting a shortfall against a requirement that deliberately searches for
    /// nothing turns a sound finding into a spurious gap, which is the exact confusion the
    /// IsModelOnly distinction exists to prevent.
    /// </summary>
    [Fact]
    public void AModelOnlyGroupIsExemptFromTheMinimum()
    {
        var group = new PlanQueryGroup
        {
            GroupId = "G1.8",
            Requirement = "Internal contradictions",
            Retrieval = new PlanRetrieval
            {
                Queries = [new PlannedQuery { Id = "Q1", Side = "Assertion", TargetCategories = ["I"] }],
            },
            Verification = new PlanVerification
            {
                Sufficiency = new PlanSufficiency { MinEvidenceCategories = 2 },
            },
        };

        var pack = Pack(group);

        Assert.True(group.IsModelOnly);
        Assert.False(pack.IsUnderEvidenced);
        Assert.Null(pack.EvidenceShortfall);
    }

    // ── 3. declared categories hold the first reserved slots ──────────────────

    /// <summary>
    /// The declared category scores worst and would be cut by pure score; the floor keeps it.
    /// Without the declared tier the slots go out alphabetically, and here that is enough to
    /// lose it — which is the case the tier exists for.
    /// </summary>
    [Fact]
    public void ADeclaredCategorySurvivesTheCapEvenScoringLast()
    {
        var passages = new List<CaseDocumentSearchMatch>();

        // Enough high-scoring A and C passages to fill the cap on their own.
        for (var i = 0; i < CheckPlanRunner.Defaults.MaxPassagesPerGroup; i++)
        {
            passages.Add(Passage("A", 0.99 - (i * 0.001), $"a{i}"));
            passages.Add(Passage("C", 0.98 - (i * 0.001), $"c{i}"));
        }

        passages.Add(Passage("F", 0.10, "the declared one"));

        var ranked = CheckPlanRunner.Rank(
            passages,
            targeted: new HashSet<string> { "A", "C", "F" },
            sections: null,
            declared: new HashSet<string> { "F" });

        Assert.Equal(CheckPlanRunner.Defaults.MaxPassagesPerGroup, ranked.Count);
        Assert.Contains(ranked, p => p.CategoryCode == "F");
    }

    /// <summary>
    /// Lint holds declared to a subset of targeted, so the tier can only reorder who keeps a
    /// slot. A category nobody declared must not be admitted by declaring it.
    /// </summary>
    [Fact]
    public void DeclaringACategoryNoQueryTargetsDoesNotWidenTheRequest()
    {
        var passages = new List<CaseDocumentSearchMatch>();

        for (var i = 0; i < CheckPlanRunner.Defaults.MaxPassagesPerGroup + 4; i++)
        {
            passages.Add(Passage("B", 0.9 - (i * 0.01), $"b{i}"));
        }

        passages.Add(Passage("G", 0.99, "untargeted but declared"));

        var ranked = CheckPlanRunner.Rank(
            passages,
            targeted: new HashSet<string> { "B" },
            sections: null,
            declared: new HashSet<string> { "G" });

        Assert.All(ranked, p => Assert.Equal("B", p.CategoryCode));
    }

    [Fact]
    public void WithNoDeclarationTheFloorBehavesAsBefore()
    {
        var passages = new List<CaseDocumentSearchMatch>
        {
            Passage("B", 0.9, "b"),
            Passage("E", 0.1, "e"),
        };

        var targeted = new HashSet<string> { "B", "E" };

        var withNull = CheckPlanRunner.Rank(passages, targeted, sections: null, declared: null);
        var withEmpty = CheckPlanRunner.Rank(
            passages, targeted, sections: null, declared: new HashSet<string>());

        Assert.Equal(withEmpty.Select(p => p.SearchedText), withNull.Select(p => p.SearchedText));
        Assert.Equal(2, withNull.Count);
    }
}
