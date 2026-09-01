using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Move 3 of the pipeline plan: <b>hold prompt size constant and change its composition.</b>
///
/// The load-bearing experiment. If widening the assertion slice at constant prompt size does not
/// raise recall, the diagnosis behind Moves 4–6 is wrong and they should not be built — so what
/// this renders has to be exactly what the design says it is: dense, complete, bounded, and
/// honest about its own truncation.
/// </summary>
public class AssertionDigestTests
{
    private const string Model = """
        {
          "taxTreatment": {
            "allowancesUsed": [
              {
                "name": "Main Residence Nil Rate Band",
                "amount": 175000,
                "provenance": { "sectionHeading": "Inheritance Tax", "documentId": "Report.md" }
              }
            ]
          },
          "financialPosition": { "expenditures": [ { "note": "rent, utilities" } ] }
        }
        """;

    private static IReadOnlyList<CanonicalLeaf> Leaves => CanonicalPaths.Enumerate(Model);

    /// <summary>
    /// The whole point. The report's Nil Rate Band claim and the expenditure line that
    /// contradicts it are both in the model and neither is selected by the group that could
    /// notice — this is the mechanism that puts them in one prompt.
    /// </summary>
    [Fact]
    public void BothHalvesOfAContradictionReachOnePrompt()
    {
        var digest = AssertionDigest.Render(Leaves, alreadyShown: [], maxCharacters: 40_000);

        Assert.NotNull(digest);
        Assert.Contains("/taxTreatment/allowancesUsed[0]/name", digest);
        Assert.Contains("Main Residence Nil Rate Band", digest);
        Assert.Contains("/financialPosition/expenditures[0]/note", digest);
        Assert.Contains("rent, utilities", digest);
    }

    /// <summary>
    /// A line carries where the extraction read it, so a value the assessor decides to raise can
    /// be cited without a passage — these are the report's own words.
    /// </summary>
    [Fact]
    public void ALineNamesItsSection() =>
        Assert.Contains(
            "⟨Inheritance Tax — Report.md⟩",
            AssertionDigest.Render(Leaves, [], 40_000));

    /// <summary>
    /// Repeating a fragment the group already has in full is exactly the volume the pack cut was
    /// spent to buy back, and Run 14 measured what spending it costs: 13% more prompt, eleven
    /// points of recall.
    /// </summary>
    [Fact]
    public void PathsThePromptAlreadyPrintsInFullAreDropped()
    {
        var digest = AssertionDigest.Render(Leaves, ["/taxTreatment"], 40_000);

        Assert.NotNull(digest);
        Assert.DoesNotContain("Main Residence Nil Rate Band", digest);
        Assert.Contains("rent, utilities", digest);
    }

    /// <summary>
    /// Exclusion follows the same parent-covers-children rule the runner resolves by, and is
    /// written in the plans' <c>[]</c> form rather than the resolved one.
    /// </summary>
    [Fact]
    public void ExclusionMatchesThePlansPathForm() =>
        Assert.DoesNotContain(
            "Main Residence Nil Rate Band",
            AssertionDigest.Render(Leaves, ["/taxTreatment/allowancesUsed[]/name"], 40_000) ?? string.Empty);

    /// <summary>
    /// Whole lines, never a truncated one: a half-written path looks like a value the model can
    /// cite, and it would fail citation verification having been manufactured by the renderer.
    /// </summary>
    [Fact]
    public void TheBudgetDropsWholeLines()
    {
        var digest = AssertionDigest.Render(Leaves, [], maxCharacters: 150);

        Assert.NotNull(digest);

        foreach (var line in digest.Split('\n').Where(l => l.TrimStart().StartsWith('`')))
        {
            Assert.EndsWith("⟩", line.Trim(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A digest that is silently short is the assertion-side version of eviction: the assessor
    /// concludes the report is silent on a point the model records, and nothing in the output
    /// shows it was merely not printed.
    /// </summary>
    [Fact]
    public void TruncationSaysSoInThePrompt() =>
        Assert.Contains(
            "treat this list as incomplete",
            AssertionDigest.Render(Leaves, [], maxCharacters: 150));

    [Fact]
    public void ADigestThatFitsDoesNotClaimToBeTruncated() =>
        Assert.DoesNotContain(
            "did not fit this budget",
            AssertionDigest.Render(Leaves, [], 40_000));

    /// <summary>
    /// Zero is off, and off means the prompt is byte-identical to what it was before this
    /// existed — the experiment has to be attributable to one change.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ZeroBudgetRendersNothing(int budget) =>
        Assert.Null(AssertionDigest.Render(Leaves, [], budget));

    [Fact]
    public void AGroupThatAlreadyHasEverythingGetsNoDigest() =>
        Assert.Null(AssertionDigest.Render(Leaves, ["/taxTreatment", "/financialPosition"], 40_000));

    /// <summary>
    /// The digest is the front of a prompt the provider caches a prefix of, and reproducibility
    /// is a first-class requirement here: a finding has to be defensible and repeatable.
    /// </summary>
    [Fact]
    public void TheSameModelRendersTheSameDigest() =>
        Assert.Equal(
            AssertionDigest.Render(Leaves, [], 40_000),
            AssertionDigest.Render(CanonicalPaths.Enumerate(Model), [], 40_000));
}
