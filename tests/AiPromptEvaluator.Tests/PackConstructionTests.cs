using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Three defects in how a group's evidence pack is assembled, all of them model-independent.
///
/// <b>That last word is what makes them worth fixing first.</b> Two runs of the same case, on two
/// models at a 3.3× cost difference, produced retrieval extracts differing by two lines — the
/// header naming the model. Same passages, same ranks, same evictions, same packs. So every
/// defect below survives any assessor upgrade, and the stronger model read past exactly what the
/// weaker one read past.
///
/// <list type="number">
/// <item><b>The group's best answer had no slot.</b> Sections and categories are reserved, both
/// of them guesses the plan made in advance; score is the group's own query answered, and it was
/// filled from what the floors left over — which on a full pack is nothing. A passage at 0.737
/// was evicted while eight near-duplicates at 0.627 were kept.</item>
/// <item><b>Those eight near-duplicates.</b> De-duplication compares text verbatim, so
/// overlapping chunk windows over one table are eight distinct keys and a third of the cap.</item>
/// <item><b>A window opening mid-table arrives without its caption.</b> Two tables of the same
/// quantity on different bases are indistinguishable from their numbers, and both runs got such
/// a pair wrong — the better one named the pre-charge table as the post-charge one.</item>
/// </list>
/// </summary>
public class PackConstructionTests
{
    /// <summary>
    /// A small cap, pinned rather than inherited: these tests are about what happens when the
    /// pack is full, and one that reshapes itself when the default moves is testing the default.
    /// </summary>
    private static readonly AppSettings Packed = new() { MaxPassagesPerGroup = 12 };

    // ── 1. the group's own best answer ────────────────────────────────────────

    /// <summary>
    /// The observed eviction, in miniature. Every category floor is satisfied by lower-scoring
    /// passages and the cap is full; the top-scoring passage must still be in the pack.
    /// </summary>
    [Fact]
    public void TheBestScoringPassageSurvivesAFullPack()
    {
        var passages = new List<CaseDocumentSearchMatch>();

        // Eight near-identical siblings, exactly as one query over one table returns them, each
        // distinct enough to survive exact de-duplication.
        for (var i = 0; i < 8; i++)
            passages.Add(Passage($"Cashflow.md", "F", 0.627, $"row {i} of the cash flow table"));

        for (var i = 0; i < 8; i++)
            passages.Add(Passage($"Research{i}.md", "G", 0.60));

        var best = Passage("Fact Find.md", "B", 0.737, "Capacity for loss questionnaire responses");
        passages.Add(best);

        var ranked = Rank(passages, Targets("B", "F", "G"));

        Assert.Contains(ranked, p => p.SearchedText.Contains("Capacity for loss"));
    }

    /// <summary>
    /// The slot is a floor, not a licence. It promotes within what the group asked for — a
    /// better-scoring passage from a category no query targeted does not get in on it, for the
    /// same reason a section hint may not widen a request.
    /// </summary>
    [Fact]
    public void TheReservationDoesNotAdmitAnUntargetedCategory()
    {
        var passages = new List<CaseDocumentSearchMatch>();

        for (var i = 0; i < 20; i++)
            passages.Add(Passage($"Research{i:00}.md", "G", 0.60));

        passages.Add(Passage("Unasked.md", "H", 0.99));

        var ranked = Rank(passages, Targets("G"));

        Assert.DoesNotContain(ranked, p => p.CategoryCode == "H");
    }

    /// <summary>
    /// And a blank form does not become a group's best answer by embedding well. An unfilled
    /// grid is short, generic, asserts nothing and scores near any query — it was measured
    /// taking the category floor's slot in every case examined, and it must not take this one.
    /// </summary>
    [Fact]
    public void TheReservationSkipsAFormSkeleton()
    {
        var skeleton = Passage("Blank.md", "B", 0.99, FormSkeleton);

        var passages = new List<CaseDocumentSearchMatch> { skeleton };

        for (var i = 0; i < 20; i++)
            passages.Add(Passage($"Filled{i:00}.md", "B", 0.60, $"a real answer number {i}"));

        // The premise: this text is what the ranker calls an empty form. If it stops being one,
        // the test above it proves nothing.
        Assert.True(ContentDensity.IsFormSkeleton(FormSkeleton));

        var ranked = CheckPlanRunner.Rank(passages, Targets("B"), null, null, Packed);

        Assert.NotEqual(skeleton.SearchedText, ranked[0].SearchedText);
    }

    /// <summary>Turned off, the reservation does nothing at all.</summary>
    [Fact]
    public void TheReservationCanBeTurnedOff()
    {
        var passages = new List<CaseDocumentSearchMatch>();

        for (var i = 0; i < 20; i++)
            passages.Add(Passage($"Research{i:00}.md", "G", 0.60));

        var off = new AppSettings { MaxPassagesPerGroup = 12, ReservedSlotsForTopScore = 0 };

        Assert.Equal(12, CheckPlanRunner.Rank(passages, Targets("G"), null, null, off).Count);
    }

    // ── 2. near-duplicate collapse ────────────────────────────────────────────

    /// <summary>
    /// Overlapping windows over one table collapse to the best-scoring one. This is what frees
    /// the slots the eviction above was competing for.
    /// </summary>
    [Fact]
    public void OverlappingWindowsOverOneTableCollapseToTheBestOfThem()
    {
        var rows = Enumerable.Range(0, 40).Select(i => $"| item {i} | value {i} |").ToList();

        // Three windows over the same forty rows, overlapping heavily, as chunking produces them.
        var passages = new List<CaseDocumentSearchMatch>
        {
            Passage("Cashflow.md", "F", 0.60, string.Join("\n", rows.Take(30))),
            Passage("Cashflow.md", "F", 0.71, string.Join("\n", rows.Skip(2).Take(30))),
            Passage("Cashflow.md", "F", 0.55, string.Join("\n", rows.Skip(4).Take(30))),
        };

        var collapsed = CheckPlanRunner.CollapseNearDuplicates(passages, 0.9);

        Assert.Equal(0.71, Assert.Single(collapsed).Score);
    }

    /// <summary>
    /// Two documents saying the same thing is corroboration, and the whole point of having an
    /// evidence side. Only one document's own overlapping windows collapse.
    /// </summary>
    [Fact]
    public void TwoDocumentsSayingTheSameThingBothSurvive()
    {
        var text = string.Join("\n", Enumerable.Range(0, 40).Select(i => $"| item {i} | value {i} |"));

        var passages = new List<CaseDocumentSearchMatch>
        {
            Passage("Fact Find.md", "B", 0.60, text),
            Passage("Research.md", "G", 0.55, text),
        };

        Assert.Equal(2, CheckPlanRunner.CollapseNearDuplicates(passages, 0.9).Count);
    }

    /// <summary>
    /// Two different rows of one table are two different facts, however alike the markup makes
    /// them look. The threshold is high for exactly this reason, and a pass that dropped these
    /// would cost the findings the whole method exists to protect.
    /// </summary>
    [Fact]
    public void TwoDifferentRowsOfOneTableBothSurvive()
    {
        var passages = new List<CaseDocumentSearchMatch>
        {
            Passage("Plans.md", "E", 0.60, "| Provider | Charge | Value |\n| Alpha | 0.41% | 57,120 |"),
            Passage("Plans.md", "E", 0.58, "| Provider | Charge | Value |\n| Beta | 0.10% | 26,480 |"),
        };

        Assert.Equal(2, CheckPlanRunner.CollapseNearDuplicates(passages, 0.9).Count);
    }

    /// <summary>
    /// The survivors come back in retrieval order, not in the order they were compared.
    /// Everything downstream reads this list as "the candidates", and re-ordering it here would
    /// quietly change the ranking's own tie-breaks.
    /// </summary>
    [Fact]
    public void SurvivorsKeepTheirRetrievalOrder()
    {
        var passages = new List<CaseDocumentSearchMatch>
        {
            Passage("A.md", "B", 0.10, "first, and the weakest"),
            Passage("B.md", "B", 0.90, "second, and the strongest"),
        };

        var collapsed = CheckPlanRunner.CollapseNearDuplicates(passages, 0.9);

        Assert.Equal("A.md", collapsed[0].DocumentName);
    }

    /// <summary>At 1.0 the pass is off and the input comes back untouched.</summary>
    [Fact]
    public void TheCollapseCanBeTurnedOff()
    {
        var text = string.Join("\n", Enumerable.Range(0, 40).Select(i => $"| item {i} | value {i} |"));

        var passages = new List<CaseDocumentSearchMatch>
        {
            Passage("Cashflow.md", "F", 0.60, text),
            Passage("Cashflow.md", "F", 0.71, text + "\n| item 40 | value 40 |"),
        };

        Assert.Equal(2, CheckPlanRunner.CollapseNearDuplicates(passages, 1.0).Count);
    }

    // ── 3. the table's caption and headings ───────────────────────────────────

    /// <summary>
    /// The defect this exists for: a window that opens on row twenty of a comparison table
    /// arrives as a grid of numbers, and the only thing that says what basis they are on is the
    /// caption above the table.
    /// </summary>
    [Fact]
    public void AWindowOpeningMidTableCarriesItsCaptionAndHeadings()
    {
        var text = Table("### Projected values after adviser charges");

        var windowed = CheckPlanRunner.Window(text, 400, ["| Beta |"]);

        Assert.Contains("Projected values after adviser charges", windowed);
        Assert.Contains("| Provider | Existing | Recommended |", windowed);
    }

    /// <summary>
    /// The rows between the headings and the window are not re-admitted. They are what the cap
    /// exists to leave out, and carrying them back would defeat it.
    /// </summary>
    [Fact]
    public void TheRowsBeforeTheWindowAreNotCarriedBack()
    {
        var text = Table("### Projected values after adviser charges");

        var windowed = CheckPlanRunner.Window(text, 400, ["| Beta |"]);

        Assert.DoesNotContain("filler row 3 ", windowed);
        Assert.Contains("rows between the headings and the window omitted", windowed);
    }

    /// <summary>
    /// A window that opens on prose is left exactly as it was. This runs on every long passage
    /// in every pack, and a passage that is not a table must be untouched by it.
    /// </summary>
    [Fact]
    public void AWindowOpeningOnProseIsUnchanged()
    {
        var text = new string('x', 500) + "\nThe client's attitude to risk was assessed as balanced.\n"
            + new string('y', 500);

        var windowed = CheckPlanRunner.Window(text, 300, ["attitude to risk"]);

        Assert.DoesNotContain("opens inside a table", windowed);
    }

    /// <summary>
    /// And a window that already opens on the table's first row has its headings; nothing is
    /// prepended, because there is nothing missing.
    /// </summary>
    [Fact]
    public void AWindowOpeningOnTheHeaderRowIsUnchanged()
    {
        Assert.Empty(CheckPlanRunner.TableContext(Table("### Caption"), 0));
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// A captioned table long enough that a window into its later rows must cut the heading.
    /// The row named "Beta" is deep enough in to be past any leading window the tests use.
    /// </summary>
    private static string Table(string caption)
    {
        var rows = string.Join(
            "\n",
            Enumerable.Range(0, 30).Select(i => $"| filler row {i} | {i * 100} | {i * 110} |"));

        return caption
            + "\n\n| Provider | Existing | Recommended |\n| --- | --- | --- |\n"
            + rows
            + "\n| Beta | 86,800 | 86,700 |\n";
    }

    /// <summary>
    /// An unfilled form grid: labels, separators, and no answers. Long enough to clear
    /// <see cref="ContentDensity.MinimumCellsToJudge"/> — below that a short table is as likely
    /// to be a two-row summary as a form, and is correctly left alone.
    /// </summary>
    private const string FormSkeleton =
        "| Field | Value |\n| --- | --- |\n| Name | |\n| Address | |\n| Occupation | |\n"
        + "| Employer | |\n| Telephone | |\n| Email | |\n| Adviser | |\n| Reviewed by | |\n";

    private static List<CaseDocumentSearchMatch> Rank(
        IEnumerable<CaseDocumentSearchMatch> passages,
        IReadOnlySet<string> targeted,
        IReadOnlyList<string>? sections = null,
        IReadOnlySet<string>? declared = null) =>
        CheckPlanRunner.Rank(passages, targeted, sections, declared, Packed);

    private static IReadOnlySet<string> Targets(params string[] categories) =>
        new HashSet<string>(categories, StringComparer.OrdinalIgnoreCase);

    private static CaseDocumentSearchMatch Passage(
        string document, string category, double score, string? text = null) =>
        new(text ?? $"passage from {document}", "CASE-1", 99, document, category, category, score);
}
