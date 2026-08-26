using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Telling "the report does not say it" from "we never read it".
///
/// <b>The canonical model cannot tell them apart, and one of them is a finding about the adviser
/// while the other is a finding about the run.</b> A failed extraction pass writes no key, which
/// is byte-for-byte what a value the report genuinely does not contain looks like. The extraction
/// report is the only record that they differ.
///
/// It stopped being academic when an applicability rule read one. CHK-009's second rule reads
/// <c>/existingArrangements[]/adviceAction</c>; three observed runs lost that section to a
/// duplicate property name; the path resolved to nothing, the rule failed, <c>onAbsent</c> said
/// Skip, and the check settled as not applicable — dropping five material findings, three of them
/// the most severe in the case, while reporting no problem at all.
/// </summary>
public class UnreadSectionTests
{
    // ── reading the extraction report ─────────────────────────────────────────

    [Fact]
    public void ASectionMarkedUnparseableIsReportedAsNeverRead()
    {
        var accessor = new CanonicalModelAccessor(ModelWithUnread("/existingArrangements"));

        Assert.Equal(["/existingArrangements"], accessor.UnreadPaths);
        Assert.True(accessor.WasNeverRead("/existingArrangements"));
    }

    /// <summary>
    /// The rule's path is a fan-out under the failed section, not the section itself, so prefix
    /// matching is the whole mechanism.
    /// </summary>
    [Fact]
    public void APathUnderAFailedSectionIsNeverRead()
    {
        var accessor = new CanonicalModelAccessor(ModelWithUnread("/existingArrangements"));

        Assert.True(accessor.WasNeverRead("/existingArrangements[]/adviceAction"));
        Assert.True(accessor.WasNeverRead("/existingArrangements/0/riskRating"));
    }

    /// <summary>
    /// Matched on a segment boundary. Without that, a failed <c>/objectives</c> would silently
    /// excuse every miss on <c>/objectivesReview</c> as well.
    /// </summary>
    [Fact]
    public void AMerelySimilarPathIsNotCovered()
    {
        var accessor = new CanonicalModelAccessor(ModelWithUnread("/objectives"));

        Assert.True(accessor.WasNeverRead("/objectives[]/targetAge"));
        Assert.False(accessor.WasNeverRead("/objectivesReview"));
        Assert.False(accessor.WasNeverRead("/recommendations"));
    }

    /// <summary>
    /// Only <c>PresentButUnparseable</c>. The other reasons mean the extraction did read the
    /// report and found nothing, which is a genuine absence and must keep behaving like one.
    /// </summary>
    [Fact]
    public void AnAbsenceForAnyOtherReasonIsStillAnAbsence()
    {
        var json =
            """
            {
              "extractionReport": {
                "expectedButAbsent": [
                  { "path": "/vulnerability", "reason": "NotPresentInDocument" },
                  { "path": "/replacementAnalysis", "reason": "OutOfScopeForCaseType" }
                ]
              }
            }
            """;

        var accessor = new CanonicalModelAccessor(json);

        Assert.Empty(accessor.UnreadPaths);
        Assert.False(accessor.WasNeverRead("/vulnerability"));
        Assert.False(accessor.WasNeverRead("/replacementAnalysis"));
    }

    /// <summary>
    /// A model with no report at all leaves a caller exactly where it was before this existed,
    /// which is the right failure mode for something read out of an LLM's own self-report.
    /// </summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"extractionReport": {}}""")]
    [InlineData("""{"extractionReport": {"expectedButAbsent": []}}""")]
    public void AModelWithoutTheReportClaimsNothing(string json)
    {
        var accessor = new CanonicalModelAccessor(json);

        Assert.Empty(accessor.UnreadPaths);
        Assert.False(accessor.WasNeverRead("/existingArrangements"));
    }

    /// <summary>An entry missing its path or reason must not become a wildcard.</summary>
    [Fact]
    public void AMalformedEntryIsIgnoredRatherThanExcusingEverything()
    {
        var json =
            """
            {
              "extractionReport": {
                "expectedButAbsent": [
                  { "reason": "PresentButUnparseable" },
                  { "path": "", "reason": "PresentButUnparseable" },
                  { "path": "/costsAndCharges" }
                ]
              }
            }
            """;

        var accessor = new CanonicalModelAccessor(json);

        Assert.Empty(accessor.UnreadPaths);
        Assert.False(accessor.WasNeverRead("/costsAndCharges"));
        Assert.False(accessor.WasNeverRead("/anything"));
    }

    [Fact]
    public void SeveralFailedSectionsAreAllReported()
    {
        var json =
            """
            {
              "extractionReport": {
                "expectedButAbsent": [
                  { "path": "/existingArrangements", "reason": "PresentButUnparseable" },
                  { "path": "/replacementAnalysis", "reason": "presentbutunparseable" }
                ]
              }
            }
            """;

        var accessor = new CanonicalModelAccessor(json);

        Assert.Equal(2, accessor.UnreadPaths.Count);
        Assert.True(accessor.WasNeverRead("/replacementAnalysis[]/comparison"));
    }

    // ──────────────────────────────────────────────

    private static string ModelWithUnread(string path) =>
        $$"""
        {
          "extractionReport": {
            "expectedButAbsent": [
              { "path": "{{path}}", "reason": "PresentButUnparseable" }
            ]
          }
        }
        """;
}
