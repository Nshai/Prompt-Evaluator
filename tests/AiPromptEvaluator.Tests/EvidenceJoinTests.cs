using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Move 5 of the pipeline plan: <b>the code-level join.</b>
///
/// The most severe finding of one case had its claim selected by <c>G9.2</c> and the document that
/// contradicts it retrieved into <c>G9.4</c> and <c>G9.10</c>. No prompt in the run held both
/// halves, on either model, across three runs — nothing was missing and nothing was mis-ranked, the
/// two halves were simply never introduced. This introduces them, in code, on arithmetic.
///
/// <b>Generic by construction.</b> Nothing here knows a provider, a document, a client or a case.
/// The only thing it matches on is a figure appearing in two places, which is a property of any
/// report; what a group is shown is still scoped by the paths its own plan selects.
/// </summary>
public class EvidenceJoinTests
{
    private const string Model = """
        {
          "research": {
            "comparisons": [
              {
                "finding": "Recommended funds returned 45.91% against Nest's 48.06% over 5 years",
                "provenance": { "sectionHeading": "Research", "documentId": "Report.md" }
              }
            ]
          },
          "costsAndCharges": { "ongoingAdviserFee": 0.19 },
          "solution": { "planCount": 2 }
        }
        """;

    private static IReadOnlyList<CanonicalLeaf> Leaves => CanonicalPaths.Enumerate(Model);

    /// <summary>
    /// The benchmark case, in miniature. The extraction wrote the finding out in English at
    /// <c>/research/comparisons[]/finding</c> — a path no group in any of the ten plans selects —
    /// while the corroborating passage sat at rank 1 in a group that could not see it.
    /// </summary>
    [Fact]
    public void AnAssertionNoGroupSelectsReachesTheGroupHoldingItsEvidence()
    {
        var joins = EvidenceJoin.For(
            Leaves,
            ownPaths: ["/costsAndCharges"],
            passagesById: new Dictionary<string, string>
            {
                ["P1"] = "Nest five-year performance to 31 March: 48.06% cumulative.",
            });

        var join = Assert.Single(joins);

        Assert.Equal("/research/comparisons[0]/finding", join.Path);
        Assert.Equal("P1", join.PassageId);
        // As written, percent sign and all: the assessor is being asked to find this literal in
        // the passage, so it has to read the way the passage spells it.
        Assert.Equal("48.06%", join.Matched);
        Assert.Contains("45.91", join.Value);
    }

    /// <summary>
    /// A group is not told about the assertions it is already being shown in full a few lines
    /// earlier — that spends the cap on something the assessor has, and the plan's own selection
    /// stays the thing that decides what a group owns.
    /// </summary>
    [Fact]
    public void AGroupsOwnPathsAreNotJoinedBackToIt()
    {
        var joins = EvidenceJoin.For(
            Leaves,
            ownPaths: ["/research"],
            passagesById: new Dictionary<string, string> { ["P1"] = "Nest returned 48.06%." });

        Assert.Empty(joins);
    }

    /// <summary>Exclusion follows the parent-covers-children rule the runner resolves by.</summary>
    [Fact]
    public void ExclusionCoversChildPaths()
    {
        var joins = EvidenceJoin.For(
            Leaves,
            ownPaths: ["/research/comparisons[]/finding"],
            passagesById: new Dictionary<string, string> { ["P1"] = "Nest returned 48.06%." });

        Assert.Empty(joins);
    }

    /// <summary>
    /// <b>Kind as well as value.</b> A 45.91 that is a percentage and a 45.91 that is a sum of
    /// money are not the same fact, and joining them would manufacture a contradiction out of a
    /// coincidence — the failure mode this whole section is most exposed to.
    /// </summary>
    [Fact]
    public void FiguresOfDifferentKindsDoNotJoin()
    {
        var joins = EvidenceJoin.For(
            CanonicalPaths.Enumerate("""{ "costsAndCharges": { "annualFee": "£1,234.56" } }"""),
            ownPaths: [],
            passagesById: new Dictionary<string, string> { ["P1"] = "Growth of 1,234.56% is implausible." });

        Assert.Empty(joins);
    }

    /// <summary>
    /// <b>The defect that made the join nearly inert, found by testing it.</b> The canonical model
    /// stores numbers stripped of symbols and separators, so a monetary amount is held as
    /// <c>116997.47</c> while the document evidencing it writes <c>£116,997.47</c>, and a charge is
    /// held as <c>0.19</c> against a document's <c>0.19%</c>. Requiring the two kinds to be equal
    /// rejected both — which is to say it rejected the two shapes the benchmark findings take.
    ///
    /// Plain is not a unit; it is the absence of one, so it cannot contradict a unit.
    /// </summary>
    [Theory]
    [InlineData("Transfer value £116,997.47 confirmed on the schedule.", "/existingArrangements[0]/transferValue")]
    [InlineData("An ongoing charge of 0.21% applies.", "/costsAndCharges/altFee")]
    public void AStrippedModelNumberJoinsAFormattedDocumentFigure(string passage, string expected)
    {
        var joins = EvidenceJoin.For(
            CanonicalPaths.Enumerate("""
                {
                  "existingArrangements": [ { "transferValue": 116997.47 } ],
                  "costsAndCharges": { "altFee": 0.21 }
                }
                """),
            [],
            new Dictionary<string, string> { ["P1"] = passage });

        Assert.Contains(joins, j => j.Path == expected);
    }

    /// <summary>
    /// Two <i>known and different</i> units still refuse each other, which is the protection the
    /// rule exists for.
    /// </summary>
    [Fact]
    public void TwoDifferentKnownUnitsStillDoNotJoin() =>
        Assert.False(EvidenceJoin.Comparable(
            NumericComparison.FigureKind.Money, NumericComparison.FigureKind.Percentage));

    /// <summary>
    /// A join on "2" or "2024" is a coincidence with a heading, and every one of them spends a
    /// slot a real match needed.
    /// </summary>
    [Theory]
    [InlineData("The plan covers 2 arrangements.")]
    [InlineData("Reviewed in 2024 as scheduled.")]
    public void RoundAndYearLikeFiguresDoNotJoin(string passage) =>
        Assert.Empty(EvidenceJoin.For(
            Leaves, [], new Dictionary<string, string> { ["P1"] = passage }));

    /// <summary>
    /// The distinctiveness rule is about the decimal place, not the magnitude: 0.19 and 0.21 are
    /// the two charges whose disagreement is a benchmark finding, and both are smaller than every
    /// whole number the filter rejects.
    /// </summary>
    [Fact]
    public void ASmallFractionalFigureIsStillDistinctive()
    {
        var joins = EvidenceJoin.For(
            Leaves,
            ownPaths: ["/research"],
            passagesById: new Dictionary<string, string>
            {
                ["P4"] = "The Aviva contract records an ongoing charge of 0.19 per cent.",
            });

        Assert.Equal("/costsAndCharges/ongoingAdviserFee", Assert.Single(joins).Path);
    }

    /// <summary>
    /// <b>The fan-out cap.</b> The mechanism this follows was measured broadcasting one item into
    /// 26 of 88 groups, 92 times over. A prompt that flags everything flags nothing.
    /// </summary>
    [Fact]
    public void TheCapIsHonouredAndKeepsTheMostDistinctive()
    {
        var model = "{" + string.Join(",", Enumerable.Range(1, 20)
            .Select(i => $"\"f{i}\":{1000 + i}.5")) + "}";

        var passages = new Dictionary<string, string>
        {
            ["P1"] = string.Join(" ", Enumerable.Range(1, 20).Select(i => $"{1000 + i}.5")),
        };

        var joins = EvidenceJoin.For(CanonicalPaths.Enumerate(model), [], passages, max: 3);

        Assert.Equal(3, joins.Count);

        // Largest fractional values first, so the cap keeps what is worth reading.
        Assert.Equal(["/f20", "/f19", "/f18"], joins.Select(j => j.Path));
    }

    // ──────────────────────────────────────────────
    // modality: CurrentOnly
    // ──────────────────────────────────────────────

    private const string Projected = """
        {
          "solution": {
            "projectedValue": 372000.75,
            "provenance": { "modality": "Projected", "sectionHeading": "Illustration" }
          },
          "existingArrangements": [
            {
              "transferValue": 116997.47,
              "provenance": { "modality": "Actual", "sectionHeading": "Existing Arrangements" }
            }
          ]
        }
        """;

    /// <summary>
    /// <b>A projection set against a figure the file records is not a discrepancy.</b> An assessor
    /// shown the pair will frequently call it one, which is the false positive this directive
    /// exists to prevent — and the model already records what it needs: a measured extract carries
    /// 60 Projected, 29 Assumed and 13 Conditional values.
    /// </summary>
    [Fact]
    public void ProjectedAssertionsAreNotJoinedWhenThePlanSaysCurrentOnly()
    {
        var passages = new Dictionary<string, string>
        {
            ["P1"] = "Illustration projects 372,000.75 at retirement; transfer value 116,997.47.",
        };

        var all = EvidenceJoin.For(CanonicalPaths.Enumerate(Projected), [], passages);
        var current = EvidenceJoin.For(
            CanonicalPaths.Enumerate(Projected), [], passages, currentOnly: true);

        Assert.Contains(all, j => j.Path == "/solution/projectedValue");
        Assert.DoesNotContain(current, j => j.Path == "/solution/projectedValue");
    }

    /// <summary>An actual value still joins — the directive narrows, it does not switch off.</summary>
    [Fact]
    public void ActualAssertionsStillJoinUnderCurrentOnly()
    {
        var joins = EvidenceJoin.For(
            CanonicalPaths.Enumerate(Projected),
            [],
            new Dictionary<string, string> { ["P1"] = "Transfer value 116,997.47 confirmed." },
            currentOnly: true);

        Assert.Contains(joins, j => j.Path == "/existingArrangements[0]/transferValue");
    }

    /// <summary>
    /// Modality is inherited from the nearest enclosing provenance block, because that is how the
    /// extraction writes it — one block governs every value on the object beneath it.
    /// </summary>
    [Fact]
    public void ModalityAndAsAtAreInheritedDownTheWalk()
    {
        var leaves = CanonicalPaths.Enumerate("""
            {
              "financialPosition": {
                "asAt": "2026-03-24",
                "provenance": { "modality": "Actual" },
                "expenditures": [ { "monthly": 2450.5 } ]
              }
            }
            """);

        var leaf = leaves.Single(l => l.Path == "/financialPosition/expenditures[0]/monthly");

        Assert.Equal("Actual", leaf.Modality);
        Assert.Equal("2026-03-24", leaf.AsAt);
        Assert.True(leaf.IsCurrent);
    }

    /// <summary>
    /// An unrecorded modality counts as current. The extraction leaves it off where the statement
    /// is an ordinary one, and treating silence as "projected" would refuse most of the model.
    /// </summary>
    [Fact]
    public void AnUnrecordedModalityIsTreatedAsCurrent() =>
        Assert.True(CanonicalPaths
            .Enumerate("""{ "costsAndCharges": { "fee": 1234.56 } }""")
            .Single()
            .IsCurrent);

    [Fact]
    public void ZeroTurnsTheJoinOff() =>
        Assert.Empty(EvidenceJoin.For(
            Leaves, [], new Dictionary<string, string> { ["P1"] = "48.06" }, max: 0));

    /// <summary>
    /// One join per assertion path: a value repeated down a table would otherwise fill the cap
    /// with one fact stated eleven times.
    /// </summary>
    [Fact]
    public void AnAssertionJoinsAtMostOnce()
    {
        var joins = EvidenceJoin.For(
            Leaves,
            ownPaths: [],
            passagesById: new Dictionary<string, string>
            {
                ["P1"] = "Nest 48.06%",
                ["P2"] = "Nest 48.06% again",
                ["P3"] = "and 45.91% for the recommendation",
            });

        Assert.Single(joins, j => j.Path == "/research/comparisons[0]/finding");
    }

    /// <summary>
    /// The prompt has to say the match is arithmetic rather than a judgement already made — an
    /// assessor told two figures "contradict" will report a contradiction whether or not one
    /// exists, which is the false-positive half of the same defect.
    /// </summary>
    [Fact]
    public void TheSectionSaysTheMatchIsArithmeticNotAVerdict()
    {
        var text = EvidenceJoin.Format(EvidenceJoin.For(
            Leaves, [], new Dictionary<string, string> { ["P1"] = "Nest returned 48.06%." }));

        Assert.NotNull(text);
        Assert.Contains("arithmetic, not judgement", text);
        Assert.Contains("not that either is wrong", text);
        Assert.Contains("[P1]", text);
    }

    [Fact]
    public void NothingJoinedPrintsNothing() =>
        Assert.Null(EvidenceJoin.Format([]));

    /// <summary>
    /// The join sits in a cached prompt prefix and a finding has to be repeatable, so the same
    /// model and pack must always produce the same list in the same order.
    /// </summary>
    [Fact]
    public void TheJoinIsDeterministic()
    {
        var passages = new Dictionary<string, string>
        {
            ["P1"] = "Nest returned 48.06% and the charge is 0.19 per cent.",
        };

        Assert.Equal(
            EvidenceJoin.For(Leaves, [], passages).Select(j => j.Path),
            EvidenceJoin.For(Leaves, [], passages).Select(j => j.Path));
    }
}
