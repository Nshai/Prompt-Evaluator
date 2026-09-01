using System.Text;
using System.Text.Json.Nodes;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Reading an extraction reply, including the replies that did not finish.
///
/// The case these are written against is a real one: a recommendations pass ran into the
/// 16,000-token output cap and stopped mid-string, and the pipeline reported it as "the model did
/// not return a JSON object" — sending whoever read it after a prompt problem that did not exist,
/// while discarding four complete recommendations that had already been written.
/// </summary>
public class ExtractionResponseReaderTests
{
    /// <summary>A reply shaped like a real section: one property holding an array of entities.</summary>
    private static string Section(int elements, bool truncateLast)
    {
        var sb = new StringBuilder("```json\n{\n  \"recommendations\": [\n");

        for (var i = 1; i <= elements; i++)
        {
            sb.Append(
                $$"""
                    {
                      "recommendationId": "REC-{{i:000}}",
                      "summary": "Switch arrangement {{i}}",
                      "provenance": { "assertionStatus": "Stated", "pageNumbers": [{{i}}] }
                    }
                """);

            if (i < elements)
            {
                sb.AppendLine(",");
            }
        }

        if (truncateLast)
        {
            // Cut mid-string, exactly as the observed failure did.
            sb.Append(",\n    {\n      \"recommendationId\": \"REC-999\",\n      \"summary\": \"Swi");
            return sb.ToString();
        }

        sb.Append("\n  ]\n}\n```");
        return sb.ToString();
    }

    [Fact]
    public void ParseObject_ReadsAFencedReply()
    {
        var parsed = ExtractionResponseReader.ParseObject(Section(2, truncateLast: false));

        Assert.NotNull(parsed);
        Assert.Equal(2, parsed!["recommendations"]!.AsArray().Count);
    }

    [Fact]
    public void ParseObject_ReadsAReplyWrappedInProse()
    {
        var parsed = ExtractionResponseReader.ParseObject(
            "Here is the section you asked for:\n{ \"objectives\": [] }\nLet me know if you need more.");

        Assert.NotNull(parsed);
        Assert.Empty(parsed!["objectives"]!.AsArray());
    }

    /// <summary>A complete reply must never be mistaken for a truncated one.</summary>
    [Fact]
    public void LooksTruncated_IsFalseForACompleteReply()
    {
        Assert.False(ExtractionResponseReader.LooksTruncated(Section(3, truncateLast: false)));
        Assert.False(ExtractionResponseReader.LooksTruncated("{ \"objectives\": [] }"));
    }

    /// <summary>
    /// The two signals the observed failure showed, and which nothing was looking at: a fence
    /// that opens and never closes, and brackets that never come back to zero.
    /// </summary>
    [Fact]
    public void LooksTruncated_IsTrueForACutOffReply()
    {
        Assert.True(ExtractionResponseReader.LooksTruncated(Section(4, truncateLast: true)));
    }

    /// <summary>Malformed is not truncated, and must still be reported as its own failure.</summary>
    [Fact]
    public void LooksTruncated_IsFalseForProseWithNoJson()
    {
        Assert.False(ExtractionResponseReader.LooksTruncated(
            "I could not find a recommendations section in this document."));
    }

    /// <summary>
    /// The finding this was built for: four whole recommendations before the cut are worth
    /// four recommendations, not nothing.
    /// </summary>
    [Fact]
    public void Salvage_KeepsEveryCompleteElementBeforeTheCut()
    {
        var salvaged = ExtractionResponseReader.Salvage(Section(4, truncateLast: true));

        Assert.NotNull(salvaged);
        Assert.Equal(4, ExtractionResponseReader.SalvagedCount(salvaged, "recommendations"));

        var ids = salvaged!["recommendations"]!.AsArray()
            .Select(r => r!["recommendationId"]!.GetValue<string>())
            .ToList();

        Assert.Equal(["REC-001", "REC-002", "REC-003", "REC-004"], ids);

        // The half-written element is dropped rather than repaired into something invented.
        Assert.DoesNotContain("REC-999", ids);
    }

    /// <summary>Nested structure inside an element must survive the salvage intact.</summary>
    [Fact]
    public void Salvage_PreservesNestedObjectsWithinSalvagedElements()
    {
        var salvaged = ExtractionResponseReader.Salvage(Section(2, truncateLast: true));

        Assert.NotNull(salvaged);
        var first = salvaged!["recommendations"]!.AsArray()[0]!;
        Assert.Equal("Stated", first["provenance"]!["assertionStatus"]!.GetValue<string>());
    }

    /// <summary>
    /// Cut before anything completed, there is nothing honest to return. The caller turns this
    /// into a failure rather than merging an empty section as though the report were silent.
    /// </summary>
    [Fact]
    public void Salvage_ReturnsNullWhenNothingCompleted()
    {
        Assert.Null(ExtractionResponseReader.Salvage("```json\n{\n  \"recommendations\": [\n    { \"reco"));
    }

    /// <summary>A brace inside a quoted string must not be counted as structure.</summary>
    [Fact]
    public void Salvage_IgnoresBracketsInsideStrings()
    {
        const string response =
            """
            ```json
            { "recommendations": [
                { "summary": "Uses a } brace and a \" quote and a ] bracket" },
                { "summary": "cut here
            """;

        var salvaged = ExtractionResponseReader.Salvage(response);

        Assert.NotNull(salvaged);
        Assert.Equal(1, ExtractionResponseReader.SalvagedCount(salvaged, "recommendations"));
        Assert.Contains("} brace", salvaged!["recommendations"]![0]!["summary"]!.GetValue<string>());
    }

    /// <summary>
    /// Reproduces the observed failure end to end, at the size it actually occurred: a reply
    /// large enough to hit a cap, cut inside a string, with most of its entities complete.
    /// </summary>
    [Fact]
    public void Salvage_HandlesTheObservedRecommendationsFailure()
    {
        var response = Section(5, truncateLast: true);

        Assert.Null(ExtractionResponseReader.ParseObject(response));
        Assert.True(ExtractionResponseReader.LooksTruncated(response));

        var salvaged = ExtractionResponseReader.Salvage(response);
        Assert.Equal(5, ExtractionResponseReader.SalvagedCount(salvaged, "recommendations"));
    }
}
