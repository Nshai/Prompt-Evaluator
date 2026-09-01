using System.Text.Json.Nodes;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// What a run says about the sections it could not read.
///
/// From an observed run of case ABC-99: two passes failed with a single extra closing brace
/// apiece, taking financialPosition and recommendations with them. Those are the payload of
/// eight of the ten checks — CHK-006 reads 22 canonical paths under /recommendations and
/// CHK-001 reads 21 under /financialPosition — and the extraction report that followed said
/// "Sections that came back empty: none" and gave 0.78 confidence.
///
/// A failed section is not a silent report. Reading it as one turns a pipeline failure into a
/// finding about the advice.
/// </summary>
public class ExtractionFailureReportingTests
{
    private static JsonObject ModelWith(params string[] populated)
    {
        var root = new JsonObject { ["modelVersion"] = "1.0" };

        foreach (var name in populated)
        {
            root[name] = new JsonArray(JsonNode.Parse("""{"x":1}""")!);
        }

        return root;
    }

    [Fact]
    public void AFailedSectionIsNamedRatherThanOmitted()
    {
        var summary = CanonicalModelExtractor.SummariseExtraction(
            ModelWith("objectives", "solution"),
            ["financialPosition", "recommendations"]);

        Assert.Contains("Sections whose extraction pass failed (2)", summary);
        Assert.Contains("financialPosition", summary);
        Assert.Contains("recommendations", summary);
    }

    /// <summary>
    /// The regression the observed run is: a section that failed outright was reported as
    /// nothing at all, and the summary's only statement about absence was "none".
    /// </summary>
    [Fact]
    public void AFailedSectionIsNotReportedAsNothingMissing()
    {
        var summary = CanonicalModelExtractor.SummariseExtraction(
            ModelWith("objectives"),
            ["financialPosition"]);

        Assert.DoesNotContain("Sections whose extraction pass failed: none.", summary);
    }

    /// <summary>
    /// Empty and failed mean opposite things — the report was silent, or we never read it —
    /// so they are reported on separate lines and never merged.
    /// </summary>
    [Fact]
    public void EmptyAndFailedAreReportedSeparately()
    {
        var root = ModelWith("objectives");
        root["vulnerability"] = new JsonArray();

        var summary = CanonicalModelExtractor.SummariseExtraction(root, ["recommendations"]);

        Assert.Contains("Sections that came back empty (1): vulnerability", summary);
        Assert.Contains("Sections whose extraction pass failed (1): recommendations", summary);
    }

    [Fact]
    public void ARunWithNothingFailedSaysSo()
    {
        var summary = CanonicalModelExtractor.SummariseExtraction(ModelWith("objectives"), []);

        Assert.Contains("Sections whose extraction pass failed: none.", summary);
    }

    /// <summary>
    /// The self-report pass is told what to do with a failed section, because left to itself
    /// it read the absence as the report saying nothing.
    /// </summary>
    [Fact]
    public void TheSelfReportIsToldAFailedSectionIsNotSilence()
    {
        var summary = CanonicalModelExtractor.SummariseExtraction(ModelWith("objectives"), ["x"]);

        Assert.Contains("PresentButUnparseable", summary);
        Assert.Contains("overallConfidence", summary);
    }

    // ── telling a malformed reply from a truncated one ────────────────────────

    /// <summary>
    /// The shape that actually failed: a complete, fenced reply carrying one extra closing
    /// brace deep inside a provenance object. It is not truncated — nothing was cut off — so
    /// salvage does not apply and a retry is the only recovery.
    /// </summary>
    private const string MalformedLikeTheObservedRun = """
        ```json
        {
          "financialPosition": {
            "incomes": [
              {
                "category": "Employment",
                "provenance": {
                  "assertionStatus": "Stated",
                  "extractionNote": "..."
                  }
                }
              }
            ]
          }
        }
        ```
        """;

    [Fact]
    public void AnExtraBraceIsNotMistakenForTruncation()
    {
        Assert.Null(ExtractionResponseReader.ParseObject(MalformedLikeTheObservedRun));
        Assert.False(ExtractionResponseReader.LooksTruncated(MalformedLikeTheObservedRun));
    }

    /// <summary>
    /// And it must not be salvaged. Salvage closes off whatever was still open at the last
    /// whole element, which is sound for a cut-off reply and wrong for a mis-nested one — the
    /// fragment would be merged and believed.
    /// </summary>
    [Fact]
    public void AMisNestedReplyIsNotSalvagedIntoSomethingBelievable()
    {
        var salvaged = ExtractionResponseReader.Salvage(MalformedLikeTheObservedRun);

        Assert.True(
            salvaged is null || salvaged["financialPosition"] is null,
            "A mis-nested reply must not be presented as a usable fragment.");
    }

    [Fact]
    public void ATruncatedReplyIsStillRecognisedAsTruncated()
    {
        const string cut = """
            {
              "recommendations": [
                { "recommendationId": "REC-001", "summary": "done" },
                { "recommendationId": "REC-002", "summ
            """;

        Assert.True(ExtractionResponseReader.LooksTruncated(cut));
        Assert.Equal(1, ExtractionResponseReader.SalvagedCount(
            ExtractionResponseReader.Salvage(cut), "recommendations"));
    }
}
