using System.Text.Json.Nodes;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The canonical schema documents 64 closed vocabularies as prose rather than as JSON Schema
/// enums, deliberately: an enum is enforced by StripEnumViolations, which deletes values it
/// does not recognise, and that deleted the vocabulary the documents actually use.
///
/// Guidance drifts. objectiveType is documented Pension / Investment / …, and extraction was
/// observed writing "RetirementObjective" with nothing noticing. These tests cover the
/// mechanical correction that now looks — and, just as importantly, what it refuses to do.
/// </summary>
public class CanonicalVocabularyTests
{
    private static readonly string[] ObjectiveTypes =
        ["Pension", "Investment", "Protection", "Mortgage", "EstatePlanning", "Budget", "EquityRelease", "Other"];

    // ── mapping ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Pension", "Pension")]
    [InlineData("pension", "Pension")]
    [InlineData("PENSION", "Pension")]
    [InlineData("Estate Planning", "EstatePlanning")]
    [InlineData("estate_planning", "EstatePlanning")]
    [InlineData("estate-planning", "EstatePlanning")]
    [InlineData("PensionObjective", "Pension")]
    [InlineData("pension goal", "Pension")]
    [InlineData("InvestmentType", "Investment")]
    public void NearMissesAreCorrectedToTheDocumentedSpelling(string written, string expected) =>
        Assert.Equal(expected, CanonicalVocabulary.Map(written, ObjectiveTypes));

    /// <summary>
    /// The line the correction will not cross. "RetirementObjective" is not a misspelling of
    /// any documented value — deciding it means Pension is a judgement about what the author
    /// meant, and the extractor is explicitly told not to quietly pick one. It is reported
    /// instead, which is the part that was missing.
    /// </summary>
    [Theory]
    [InlineData("RetirementObjective")]
    [InlineData("Retirement")]
    [InlineData("IncomeInRetirement")]
    [InlineData("Something entirely new")]
    public void AValueNeedingAJudgementIsNotMapped(string written) =>
        Assert.Null(CanonicalVocabulary.Map(written, ObjectiveTypes));

    /// <summary>
    /// A partial-word match would collapse distinctions the vocabulary exists to keep.
    /// "PensionTransfer" is not "Pension" with a generic word after it.
    /// </summary>
    [Fact]
    public void APartialWordMatchIsNotAMatch() =>
        Assert.Null(CanonicalVocabulary.Map("PensionTransfer", ObjectiveTypes));

    // ── correcting a fragment ─────────────────────────────────────────────────

    [Fact]
    public void CorrectingRewritesTheValueAndReportsIt()
    {
        var fragment = JsonNode.Parse("""{"objectives":[{"objectiveType":"pension"}]}""")!.AsObject();

        var corrections = CanonicalVocabulary.Normalise(
            fragment,
            new Dictionary<string, IReadOnlyList<string>> { ["objectiveType"] = ObjectiveTypes });

        Assert.Equal("Pension", fragment["objectives"]![0]!["objectiveType"]!.GetValue<string>());
        var correction = Assert.Single(corrections);
        Assert.True(correction.WasMapped);
        Assert.Equal("pension", correction.Written);
        Assert.Equal("Pension", correction.Corrected);
    }

    /// <summary>
    /// The rule the enums broke: never delete. An unmappable value stays exactly as written,
    /// so a check can still see what the report said, and the correction records that nothing
    /// was done to it.
    /// </summary>
    [Fact]
    public void AnUnmappableValueIsLeftAloneAndReported()
    {
        var fragment = JsonNode.Parse("""{"objectives":[{"objectiveType":"RetirementObjective"}]}""")!.AsObject();

        var corrections = CanonicalVocabulary.Normalise(
            fragment,
            new Dictionary<string, IReadOnlyList<string>> { ["objectiveType"] = ObjectiveTypes });

        Assert.Equal(
            "RetirementObjective",
            fragment["objectives"]![0]!["objectiveType"]!.GetValue<string>());

        var correction = Assert.Single(corrections);
        Assert.False(correction.WasMapped);
        Assert.Null(correction.Corrected);
    }

    [Fact]
    public void AValueAlreadyCorrectIsNotReportedAsAChange()
    {
        var fragment = JsonNode.Parse("""{"objectives":[{"objectiveType":"Pension"}]}""")!.AsObject();

        Assert.Empty(CanonicalVocabulary.Normalise(
            fragment,
            new Dictionary<string, IReadOnlyList<string>> { ["objectiveType"] = ObjectiveTypes }));
    }

    [Fact]
    public void APropertyWithNoDocumentedVocabularyIsUntouched()
    {
        var fragment = JsonNode.Parse("""{"objectives":[{"summary":"anything at all"}]}""")!.AsObject();

        Assert.Empty(CanonicalVocabulary.Normalise(
            fragment,
            new Dictionary<string, IReadOnlyList<string>> { ["objectiveType"] = ObjectiveTypes }));

        Assert.Equal("anything at all", fragment["objectives"]![0]!["summary"]!.GetValue<string>());
    }

    // ── reading the vocabularies out of the real schema ───────────────────────

    private static string SchemaPath =>
        Path.Combine(AppContext.BaseDirectory, "canonical-suitability-model.schema.json");

    [Fact]
    public void TheRealSchemaYieldsItsDocumentedVocabularies()
    {
        var vocabularies = CanonicalVocabulary.Parse(File.ReadAllText(SchemaPath));

        Assert.True(
            vocabularies.Count >= 40,
            $"Expected the schema's One-of lists to be found; got {vocabularies.Count}.");

        Assert.Equal(ObjectiveTypes, vocabularies["objectiveType"]);
    }

    /// <summary>
    /// The name-collision the parser used to have, measured on Run 19: it keyed vocabularies by
    /// property name across the whole schema, and the schema reuses names. So a value legitimate on
    /// one field was flagged against a same-named field's enum elsewhere. Of nineteen values a run
    /// reported "outside the documented vocabulary", eighteen were correct for the field they were
    /// on and only one — modality "Derived" — was a genuine slip.
    ///
    /// A name that carries free text anywhere cannot be validated by name, so it is dropped
    /// entirely. `basis` is the clearest case: the schema uses it for Gross/Net/Unspecified, for
    /// LikeForLike/…, and for the free text "what the percentage is of" — and nearly every flagged
    /// value was the last of those.
    /// </summary>
    [Theory]
    [InlineData("basis")]      // Gross/Net/Unspecified · LikeForLike/… · free text
    [InlineData("role")]       // client role · research-option role · free text
    [InlineData("category")]   // income category · free text
    [InlineData("planType")]   // the pension-type enum · free text
    public void ANameThatIsFreeTextAnywhereIsNotValidatedByName(string name)
    {
        var vocabularies = CanonicalVocabulary.Parse(File.ReadAllText(SchemaPath));

        Assert.False(
            vocabularies.ContainsKey(name),
            $"\"{name}\" carries free text somewhere in the schema, so validating it by name would "
            + "flag a value legitimate on the free-text field. It must be dropped, not half-enforced.");
    }

    /// <summary>
    /// A name reused for two <i>different</i> vocabularies, with no free-text use, is unioned — a
    /// value valid for either is accepted, which is the same leniency the enum stripper applies.
    /// `direction` is Increased/Decreased/Unchanged on a risk reconciliation and
    /// RecommendedCheaper/RecommendedMoreExpensive/Equivalent on a cost comparison; both must pass.
    /// </summary>
    [Fact]
    public void ANameReusedForTwoVocabulariesAcceptsAValueFromEither()
    {
        var vocabularies = CanonicalVocabulary.Parse(File.ReadAllText(SchemaPath));

        Assert.True(vocabularies.TryGetValue("direction", out var direction));
        Assert.Contains("Decreased", direction!);          // the risk-reconciliation vocabulary
        Assert.Contains("RecommendedCheaper", direction!);  // the cost-comparison vocabulary
    }

    /// <summary>
    /// The one true positive from Run 19 survives the fix: modality has a single vocabulary and no
    /// free-text use, so "Derived" — an assertionStatus value written into the modality field — is
    /// still reported. The parser fix removes the false alarms without silencing the real one.
    /// </summary>
    [Fact]
    public void AGenuineCrossFieldSlipIsStillReported()
    {
        var vocabularies = CanonicalVocabulary.Parse(File.ReadAllText(SchemaPath));

        var fragment = JsonNode.Parse(
            """{"riskAssessment":{"perClient":[{"attitudeToRisk":{"provenance":{"modality":"Derived"}}}]}}""")!
            .AsObject();

        var corrections = CanonicalVocabulary.Normalise(fragment, vocabularies);

        Assert.Contains(corrections, c => c.Property == "modality" && c.Written == "Derived" && !c.WasMapped);
    }

    /// <summary>
    /// The value that started this: the schema documents Pension, extraction wrote
    /// RetirementObjective, and the worked example still carries it. It must be reported
    /// rather than corrected or deleted.
    /// </summary>
    [Fact]
    public void TheWorkedExampleDriftIsReportedNotSilentlyRewritten()
    {
        var vocabularies = CanonicalVocabulary.Parse(File.ReadAllText(SchemaPath));
        var example = JsonNode.Parse(File.ReadAllText(ExamplePath))!.AsObject();

        var corrections = CanonicalVocabulary.Normalise(example, vocabularies);

        var drift = corrections.Where(c => c.Property == "objectiveType").ToList();

        Assert.NotEmpty(drift);
        Assert.All(drift, c => Assert.False(c.WasMapped));
        Assert.All(drift, c => Assert.Equal("RetirementObjective", c.Written));
    }

    /// <summary>
    /// Read from the repository rather than the build output: the worked example is a published
    /// artifact, not something deployed beside the executable.
    /// </summary>
    private static string ExamplePath =>
        Path.Combine(RepoRoot, "docs", "artifacts", "examples", "suitability-report-test-1.extract.json");

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiPromptEvaluator.slnx")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? AppContext.BaseDirectory;
        }
    }
}
