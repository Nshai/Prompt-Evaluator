using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The walk two separate measurements are computed from — the coverage figure and the assertion
/// digest — so a mistake here would move the number and the prompt together and look consistent.
/// </summary>
public class CanonicalPathsTests
{
    private const string Model = """
        {
          "modelVersion": "1.0",
          "case": { "caseReference": "ABC-99", "adviser": "" },
          "objectives": [
            {
              "summary": "Receive the Pension Sharing Order funds",
              "isClientStated": true,
              "provenance": {
                "sectionHeading": "Your Objectives",
                "documentId": "Suitability report.md",
                "quote": "You have been awarded a share of your ex-husband's pension."
              }
            },
            { "summary": "Consolidate two small pots" }
          ],
          "extractionReport": { "internalInconsistencies": [ { "description": "noise" } ] }
        }
        """;

    [Fact]
    public void EveryPopulatedScalarIsALeaf()
    {
        var paths = CanonicalPaths.Enumerate(Model).Select(l => l.Path).ToList();

        Assert.Contains("/case/caseReference", paths);
        Assert.Contains("/objectives[0]/summary", paths);
        Assert.Contains("/objectives[0]/isClientStated", paths);
        Assert.Contains("/objectives[1]/summary", paths);
    }

    /// <summary>
    /// An empty string is the report being silent, which is what an absent key already means.
    /// Counting it as populated would report a coverage gap at a path no check could ever find
    /// anything at, and print a digest line with nothing on the right of the equals sign.
    /// </summary>
    [Fact]
    public void EmptyValuesAreNotPopulated() =>
        Assert.DoesNotContain(
            "/case/adviser",
            CanonicalPaths.Enumerate(Model).Select(l => l.Path));

    /// <summary>
    /// The pipeline talking about its own work is not the report making a claim. Counting these
    /// would inflate the denominator of every coverage figure with paths no group should select.
    /// </summary>
    [Fact]
    public void PipelineMetadataIsNotAnAssertion()
    {
        var paths = CanonicalPaths.Enumerate(Model).Select(l => l.Path).ToList();

        Assert.DoesNotContain(paths, p => p.StartsWith("/extractionReport", StringComparison.Ordinal));
        Assert.DoesNotContain(paths, p => p.Contains("provenance", StringComparison.Ordinal));
        Assert.DoesNotContain("/modelVersion", paths);
    }

    /// <summary>
    /// The heading survives even though the provenance block itself does not: it is what makes a
    /// digest line checkable, and dropping it would leave the assessor a value it cannot trace.
    /// </summary>
    [Fact]
    public void ProvenanceBecomesTheSourceOfItsSiblings()
    {
        var leaves = CanonicalPaths.Enumerate(Model);

        Assert.Equal(
            "Your Objectives — Suitability report.md",
            leaves.Single(l => l.Path == "/objectives[0]/summary").Source);

        Assert.Null(leaves.Single(l => l.Path == "/objectives[1]/summary").Source);
    }

    /// <summary>
    /// Plans are written in the <c>[]</c> form, so coverage has to be measured in it — and one
    /// case's forty holdings must not read as forty times the coverage problem of another's one.
    /// </summary>
    [Fact]
    public void PopulatedPathsCollapseArrayIndices()
    {
        var populated = CanonicalPaths.PopulatedPaths(CanonicalPaths.Enumerate(Model));

        Assert.Contains("/objectives[]/summary", populated);
        Assert.Single(populated, p => p == "/objectives[]/summary");
    }

    [Theory]
    [InlineData("/objectives[3]/summary", "/objectives[]/summary")]
    [InlineData("/lines[layer=Fund]/charge", "/lines[]/charge")]
    [InlineData("/taxTreatment/allowancesUsed", "/taxTreatment/allowancesUsed")]
    public void NormaliseWritesThePlansForm(string resolved, string expected) =>
        Assert.Equal(expected, CanonicalPaths.Normalise(resolved));

    /// <summary>
    /// Document order, not sorted: the digest goes into a prompt whose prefix the provider
    /// caches, and reproducibility is a first-class requirement in a regulated QA tool.
    /// </summary>
    [Fact]
    public void TheWalkIsStable()
    {
        var once = CanonicalPaths.Enumerate(Model).Select(l => l.Path);
        var twice = CanonicalPaths.Enumerate(Model).Select(l => l.Path);

        Assert.Equal(once, twice);
    }

    /// <summary>
    /// A newline in a value would break the one-line-per-path contract the digest's density
    /// depends on, and a long narrative would spend the budget forty lines earlier than intended.
    /// </summary>
    [Fact]
    public void ValuesAreOneLineAndBounded()
    {
        var leaf = CanonicalPaths
            .Enumerate($$"""{ "note": "{{new string('x', 400)}}\nsecond line" }""")
            .Single();

        Assert.DoesNotContain('\n', leaf.Value);
        Assert.True(leaf.Value.Length < 200, $"A value was {leaf.Value.Length} characters.");
        Assert.EndsWith("…", leaf.Value, StringComparison.Ordinal);
    }
}
