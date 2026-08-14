using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The folds added after measuring one run's failures, and the guarantee they must not break.
///
/// Of 34 quote citations that failed the matcher in Run 5, 18 differed from their source by
/// punctuation alone — a sentence-ending full stop, a markdown bold marker, or a Wingdings
/// bullet the PDF converter leaves at <c>U+F0B7</c>. Each rejection downgraded a group, because
/// a pass resting on an untraceable quote is forced to Potential Concern.
///
/// **Every test in the second half exists to stop the fix going too far.** The failure this
/// whole class was built to catch is a quotation differing from its evidence by one digit, and a
/// matcher generous enough to forgive punctuation must still refuse that.
/// </summary>
public class CitationNormalisationTests
{
    private static bool Verifies(string quote, string evidence) =>
        CitationVerifier.Unverified([new FindingCitation { Quote = quote }], [evidence]).Count == 0;

    // ── The folds, each from an observed failure ──

    [Fact]
    public void ASentenceEndingFullStopIsNotASubstantiveDifference()
    {
        Assert.True(Verifies(
            "You receive state pension income of £11,973 per annum. Kim will receive the same.",
            "You receive state pension income of £11,973 per annum  Kim will receive the same"));
    }

    [Fact]
    public void AWingdingsBulletFromTheConverterIsIgnored()
    {
        Assert.True(Verifies(
            "Consolidation into a single platform will provide clearer understanding",
            " Consolidation into a single platform  will provide clearer understanding"));
    }

    [Fact]
    public void MarkdownEmphasisIsIgnored()
    {
        Assert.True(Verifies(
            "Total Monthly Disposable Income £-288.00",
            "**Total Monthly Disposable Income** £-288.00"));
    }

    /// <summary>
    /// A folded separator row left two spaces where the row had been, so the same table row
    /// quoted with and without its furniture no longer matched itself.
    /// </summary>
    [Fact]
    public void AFoldedSeparatorRowDoesNotLeaveADoubleSpace()
    {
        Assert.True(Verifies(
            "Total Net Monthly Income £1,430.00",
            "| Total Net Monthly Income |\n|---|---|\n| £1,430.00 |"));
    }

    // ── The guarantee ──

    /// <summary>
    /// The observed fabrication, and the reason this class exists: an assessor handed "a Risk
    /// rating of 6" reported "a Risk rating of 5" and cleared the check.
    /// </summary>
    [Fact]
    public void AQuotationAlteredByOneDigitStillFails()
    {
        const string evidence = "You are happy to proceed with a Risk rating of 6.";

        Assert.True(Verifies("You are happy to proceed with a Risk rating of 6.", evidence));
        Assert.False(Verifies("You are happy to proceed with a Risk rating of 5.", evidence));
    }

    /// <summary>
    /// The decimal point and the thousands separator are part of the figure. Folding them away
    /// with sentence punctuation would make these two claims the same claim.
    /// </summary>
    [Theory]
    [InlineData("The value of investment is £1,430.00 in total", "The value of investment is £143000 in total")]
    [InlineData("An ongoing charge of 0.18% applies here", "An ongoing charge of 018% applies here")]
    [InlineData("The agreed risk rating is 5.0 for this client", "The agreed risk rating is 50 for this client")]
    public void PunctuationInsideAFigureIsNotFoldedAway(string quote, string differentEvidence)
    {
        Assert.False(Verifies(quote, differentEvidence));
    }

    /// <summary>
    /// Folding the dots of an ellipsis broke elision, which is how a model quotes a long
    /// sentence without reproducing its middle. Caught by measurement, not by review.
    /// </summary>
    [Fact]
    public void AnEllipsisStillMarksAnElision()
    {
        Assert.True(Verifies(
            "The Fund seeks to provide long term growth...the Fund may be invested in equities.",
            "The Fund seeks to provide long term growth consistent with a balanced volatility "
            + "level over the long term. The Fund is actively managed and the Fund may be "
            + "invested in equities."));
    }

    /// <summary>Elided spans are still required in order — "A … B" must not verify against "B … A".</summary>
    [Fact]
    public void AnElisionStillRequiresItsSpansInOrder()
    {
        Assert.False(Verifies(
            "the Fund may be invested in equities...The Fund seeks long term growth",
            "The Fund seeks long term growth. The Fund may be invested in equities."));
    }

    [Fact]
    public void AQuotationAbsentFromTheEvidenceStillFails()
    {
        Assert.False(Verifies(
            "The client confirmed he understood the drawdown mechanics in full",
            "You are happy to proceed with a Risk rating of 6."));
    }
}
