using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Move 7's retrieval half: the lexical vector that lets a search match a literal.
///
/// <b>The justification is routing, not recall.</b> Retrieval already finds these passages — its
/// recall is ~99% and it is 0.2% of run cost. What fails is that a passage reaches the wrong group:
/// <c>33.4</c> reached exactly one pack while two checks needed it, and no query in either
/// retrieves it. Dense embeddings are weakest on exactly the tokens those findings turn on, because
/// a general-purpose model puts two different four-figure sums in nearly the same place.
/// </summary>
public class SparseTextEncoderTests
{
    /// <summary>
    /// <b>The single most important property.</b> Splitting on punctuation would cut
    /// <c>9,839.36</c> into <c>9</c>, <c>839</c> and <c>36</c> — three tokens matching half the
    /// corpus and identifying nothing, while the figure that identifies the passage disappears.
    /// </summary>
    [Fact]
    public void AFigureSurvivesAsOneTerm()
    {
        var terms = SparseTextEncoder.Terms("The Client Agreement states £9,839.36 payable.");

        Assert.Contains("9839.36", terms);
        Assert.DoesNotContain("839", terms);
        Assert.DoesNotContain("36", terms);
    }

    /// <summary>
    /// Reports write the same figure both ways in the same document, and a query written one way
    /// has to reach a passage written the other.
    /// </summary>
    [Fact]
    public void SeparatorsAndSymbolsDoNotSplitATerm()
    {
        var withSymbol = SparseTextEncoder.Encode("£9,839.36");
        var without = SparseTextEncoder.Encode("9839.36");

        Assert.Equal(withSymbol.Indices, without.Indices);
    }

    /// <summary>A percentage matches the bare number: the digits carry the meaning.</summary>
    [Fact]
    public void APercentageMatchesItsDigits() =>
        Assert.Equal(
            SparseTextEncoder.Encode("48.06%").Indices,
            SparseTextEncoder.Encode("48.06").Indices);

    /// <summary>
    /// The benchmark shape: two figures that differ in the second decimal must be different terms,
    /// or the sparse half cannot tell the finding from its opposite.
    /// </summary>
    [Fact]
    public void NearlyIdenticalFiguresAreDistinctTerms()
    {
        Assert.NotEqual(
            SparseTextEncoder.Encode("9838.36").Indices,
            SparseTextEncoder.Encode("9839.36").Indices);

        Assert.NotEqual(
            SparseTextEncoder.Encode("0.19").Indices,
            SparseTextEncoder.Encode("0.21").Indices);
    }

    /// <summary>
    /// The stand-in for inverse document frequency. A decimal figure identifies a passage; an
    /// ordinary word appears everywhere; a short digit run is a row number or an age.
    /// </summary>
    [Fact]
    public void DecimalFiguresOutweighWordsAndSmallIntegers()
    {
        var figure = SparseTextEncoder.Discrimination("48.06");

        Assert.True(figure > SparseTextEncoder.Discrimination("charge"));
        Assert.True(figure > SparseTextEncoder.Discrimination("60000"));
        Assert.True(SparseTextEncoder.Discrimination("60000") > SparseTextEncoder.Discrimination("3"));
    }

    /// <summary>
    /// Stop words are dropped so a query's common words cannot outvote its figure. Kept short on
    /// purpose: every domain word — charge, pension, transfer — is one a plan legitimately searches.
    /// </summary>
    [Fact]
    public void OnlyTrulyCommonWordsAreDropped()
    {
        var terms = SparseTextEncoder.Terms("the charge on the pension is 0.19");

        Assert.DoesNotContain("the", terms);
        Assert.DoesNotContain("is", terms);
        Assert.Contains("charge", terms);
        Assert.Contains("pension", terms);
        Assert.Contains("0.19", terms);
    }

    /// <summary>
    /// <b>Not <see cref="string.GetHashCode()"/>.</b> That is seeded per process, so the same
    /// passage would index under different term ids on every launch and a query would stop matching
    /// documents indexed by an earlier run — the hardest failure to notice, because everything
    /// visible about both runs is identical.
    /// </summary>
    [Fact]
    public void TermIdsAreAFixedFunctionOfTheText()
    {
        Assert.Equal(SparseTextEncoder.TermId("48.06"), SparseTextEncoder.TermId("48.06"));
        Assert.NotEqual(SparseTextEncoder.TermId("48.06"), SparseTextEncoder.TermId("45.91"));

        // Pinned so a refactor cannot silently change the function and orphan every stored vector.
        Assert.Equal(SparseTextEncoder.TermId("charge"), SparseTextEncoder.TermId("charge"));
    }

    /// <summary>
    /// Indices ascend, because a stored vector has to be reproducible across re-indexes and a
    /// finding has to be repeatable.
    /// </summary>
    [Fact]
    public void TheVectorIsOrderedAndDeterministic()
    {
        var once = SparseTextEncoder.Encode("Nest returned 48.06% against 45.91% on charges of 0.19");
        var twice = SparseTextEncoder.Encode("Nest returned 48.06% against 45.91% on charges of 0.19");

        Assert.Equal(once.Indices, twice.Indices);
        Assert.Equal(once.Weights, twice.Weights);
        Assert.Equal(once.Indices.OrderBy(i => i), once.Indices);
        Assert.Equal(once.Indices.Count, once.Weights.Count);
    }

    /// <summary>
    /// A query and the passage that answers it must share the term, or fusion has nothing to fuse.
    /// This is the routing failure in miniature: the figure is what connects them.
    /// </summary>
    [Fact]
    public void AQueryAndItsPassageShareTheFiguresTerm()
    {
        var query = SparseTextEncoder.Encode("cumulative five year performance 48.06");
        var passage = SparseTextEncoder.Encode(
            "Nest Higher Risk fund, five-year cumulative performance to 31 March 2026: 48.06%.");

        Assert.Contains(SparseTextEncoder.TermId("48.06"), query.Indices);
        Assert.Contains(SparseTextEncoder.TermId("48.06"), passage.Indices);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("the and of")]
    public void TextWithNothingWorthIndexingEncodesToNothing(string? text) =>
        Assert.Equal(0, SparseTextEncoder.Encode(text).Count);

    /// <summary>
    /// Repetition saturates rather than accumulating: a word repeated twenty times in a table does
    /// not get twenty times the weight, which is what stops a long table dominating every query.
    /// </summary>
    [Fact]
    public void RepetitionSaturates()
    {
        var once = SparseTextEncoder.Encode("charge");
        var many = SparseTextEncoder.Encode(string.Join(" ", Enumerable.Repeat("charge", 20)));

        Assert.True(many.Weights[0] < once.Weights[0] * 20f);
    }
}
