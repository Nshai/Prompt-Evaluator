using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// A heading a converter emitted as bold text.
///
/// <b>Every layer below this behaves correctly and the evidence still does not arrive.</b> The
/// document has an appendix; the reader sees an emphasised paragraph; the chunker gives the block
/// no section context; the plan's hint naming the appendix matches nothing; the reserved slot
/// goes unclaimed; and the run's diagnostic reports a hint that matched nothing — which reads as
/// a typo in the plan and sends the next fix to the wrong place.
///
/// Fourteen section hints matched nothing in two consecutive runs, identically, because hints are
/// plan data and this is a conversion defect. Four of them named the evidence behind four
/// benchmark findings.
///
/// The promotion is deliberately narrow. Inventing structure where a document has none is a worse
/// failure than missing some, because it is invisible: a wrongly promoted line silently changes
/// which chunk a passage belongs to, and nothing downstream can tell that the heading was never
/// in the document.
/// </summary>
public class PseudoHeadingTests
{
    [Fact]
    public void ABoldLineStandingAloneBecomesAHeading()
    {
        var promoted = CaseDocumentIndexer.PromoteBoldPseudoHeadings(
            "Some prose.\n\n**Appendix**\n\n| Provider | Charge |\n");

        Assert.Contains("#### Appendix", promoted);
        Assert.DoesNotContain("**Appendix**", promoted);
    }

    /// <summary>A caption for the table below it is the case this matters most for.</summary>
    [Fact]
    public void ATableCaptionBecomesAHeadingTheHintsCanSee()
    {
        var promoted = CaseDocumentIndexer.PromoteBoldPseudoHeadings(
            "**Projected values after adviser charges**\n\n| A | B |\n| --- | --- |\n");

        Assert.StartsWith("#### Projected values after adviser charges", promoted);
    }

    // ── what must not be promoted ─────────────────────────────────────────────

    /// <summary>
    /// A bold lead-in against the paragraph it introduces is that paragraph's emphasis. Promoting
    /// it would cut the paragraph away from its own first words.
    /// </summary>
    [Fact]
    public void ABoldLineTouchingItsParagraphIsLeftAlone()
    {
        const string markdown = "**Important**\nThe client declined the recommendation.\n";

        Assert.Equal(markdown, CaseDocumentIndexer.PromoteBoldPseudoHeadings(markdown));
    }

    /// <summary>Two spans on one line is emphasis, however the line begins and ends.</summary>
    [Fact]
    public void ALineWithTwoBoldSpansIsLeftAlone()
    {
        const string markdown = "Prose.\n\n**Total** for the year was **£4,000**\n\nMore prose.\n";

        Assert.Equal(markdown, CaseDocumentIndexer.PromoteBoldPseudoHeadings(markdown));
    }

    /// <summary>A bold sentence is a sentence.</summary>
    [Fact]
    public void ABoldSentenceIsLeftAlone()
    {
        const string markdown =
            "Prose.\n\n**Your fund will run out when you are 71.**\n\nMore prose.\n";

        Assert.Equal(markdown, CaseDocumentIndexer.PromoteBoldPseudoHeadings(markdown));
    }

    /// <summary>And so is a bold paragraph too long to be a title.</summary>
    [Fact]
    public void ALongBoldLineIsLeftAlone()
    {
        var markdown = "Prose.\n\n**" + new string('x', 100) + "**\n\nMore prose.\n";

        Assert.Equal(markdown, CaseDocumentIndexer.PromoteBoldPseudoHeadings(markdown));
    }

    /// <summary>
    /// A document with no bold text at all comes back as the same string, not merely an equal
    /// one. This runs over every Markdown file in every case on every index, and a rewrite that
    /// re-joins lines would silently normalise line endings across the whole corpus.
    /// </summary>
    [Fact]
    public void ADocumentWithNothingToPromoteIsUntouched()
    {
        const string markdown = "# Real heading\r\n\r\nProse with no emphasis.\r\n";

        Assert.Same(markdown, CaseDocumentIndexer.PromoteBoldPseudoHeadings(markdown));
    }

    /// <summary>
    /// Carriage returns survive. Converted documents arrive with Windows line endings, and a
    /// promotion that ate them would rewrite every line of every file it touched.
    /// </summary>
    [Fact]
    public void WindowsLineEndingsSurvive()
    {
        var promoted = CaseDocumentIndexer.PromoteBoldPseudoHeadings(
            "Prose.\r\n\r\n**Appendix**\r\n\r\nMore prose.\r\n");

        Assert.Contains("#### Appendix", promoted);
        Assert.Contains("Prose.\r\n", promoted);
    }

    /// <summary>A heading on the first line has nothing above it and is still a heading.</summary>
    [Fact]
    public void ABoldLineAtTheStartOfTheDocumentIsPromoted()
    {
        Assert.StartsWith(
            "#### Appendix",
            CaseDocumentIndexer.PromoteBoldPseudoHeadings("**Appendix**\n\nProse.\n"));
    }
}
