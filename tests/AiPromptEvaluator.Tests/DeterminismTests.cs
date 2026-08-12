using System.Text.Json;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// What keeps two runs of the same check over the same case reaching the same finding.
///
/// The app cannot make a language model deterministic, but almost everything that fed the
/// variation was under its own control: sampling left at the provider's creative default, a
/// reply shape the model reinvented each time, a de-duplication key seeded per process, and a
/// ranking cut that fell through a band of tied scores in whatever order the vector store
/// happened to return them.
/// </summary>
public class DeterminismTests
{
    // ──────────────────────────────────────────────
    // Sampling
    // ──────────────────────────────────────────────

    /// <summary>
    /// The provider default is temperature 1. A QA finding that changes because the sampler
    /// rolled differently is not a finding, so the app asks for a pinned sample by default.
    /// </summary>
    [Fact]
    public void ChatOptions_PinSamplingByDefault()
    {
        var options = new PromptEvaluator(new AppSettings()).ChatOptions();

        Assert.Equal(0f, options.Temperature);
        Assert.Equal(1f, options.TopP);
        Assert.Equal(1L, options.Seed);
    }

    [Fact]
    public void ChatOptions_UseTheConfiguredSeed()
    {
        var settings = new AppSettings { SamplingSeed = 4242 };

        Assert.Equal(4242L, new PromptEvaluator(settings).ChatOptions().Seed);
    }

    /// <summary>
    /// Some models reject these parameters outright, so each pin has to be releasable on its
    /// own — a run that errors is worse than a run that varies.
    /// </summary>
    [Fact]
    public void ChatOptions_SendNothing_WhenSamplingIsNotPinned()
    {
        var settings = new AppSettings { PinTemperature = false, PinTopP = false, PinSeed = false };

        var options = new PromptEvaluator(settings).ChatOptions();

        Assert.Null(options.Temperature);
        Assert.Null(options.TopP);
        Assert.Null(options.Seed);
    }

    /// <summary>
    /// The reason each parameter has its own checkbox: a gateway can reject one of the three
    /// without objecting to the others (a Bedrock inference profile rejecting temperature=0
    /// while still accepting top-p and seed, for instance), so the pins are independent.
    /// </summary>
    [Fact]
    public void ChatOptions_PinsEachSamplingParameterIndependently()
    {
        var settings = new AppSettings { PinTemperature = false, PinTopP = true, PinSeed = false };

        var options = new PromptEvaluator(settings).ChatOptions();

        Assert.Null(options.Temperature);
        Assert.Equal(1f, options.TopP);
        Assert.Null(options.Seed);
    }

    [Fact]
    public void ChatOptions_UsesTheConfiguredTemperatureAndTopP()
    {
        var settings = new AppSettings { Temperature = 1f, TopP = 0.9f };

        var options = new PromptEvaluator(settings).ChatOptions();

        Assert.Equal(1f, options.Temperature);
        Assert.Equal(0.9f, options.TopP);
    }

    // ──────────────────────────────────────────────
    // The finding schema
    // ──────────────────────────────────────────────

    /// <summary>
    /// The point of constraining the reply: an outcome the app does not recognise is read as a
    /// Potential Concern, so an unconstrained model rewording "No Issue" turns a pass into a
    /// concern. The enum makes that unreachable.
    /// </summary>
    [Fact]
    public void FindingSchema_AllowsOnlyTheThreeOutcomes()
    {
        var outcomes = FindingSchema.Element
            .GetProperty("properties").GetProperty("outcome").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString())
            .ToList();

        Assert.Equal(["NoIssue", "PotentialConcern", "NotApplicable"], outcomes);

        // Every allowed value has to round-trip to itself, or constraining the reply would
        // simply move the ambiguity rather than remove it.
        foreach (var outcome in outcomes)
        {
            Assert.Equal(outcome, CheckFinding.ParseOutcome(outcome).ToString());
        }
    }

    /// <summary>The strict dialect the providers enforce: no extra properties, nothing optional.</summary>
    [Fact]
    public void FindingSchema_IsStrict()
    {
        AssertStrict(FindingSchema.Element);

        static void AssertStrict(JsonElement node)
        {
            if (node.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (node.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && type.GetString() == "object")
            {
                Assert.False(
                    node.GetProperty("additionalProperties").GetBoolean(),
                    "Every object in the schema must refuse additional properties.");

                var declared = node.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToHashSet();
                var required = node.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToHashSet();

                Assert.Equal(declared, required);
            }

            foreach (var child in node.EnumerateObject())
            {
                if (child.Value.ValueKind == JsonValueKind.Object)
                {
                    AssertStrict(child.Value);

                    foreach (var grandchild in child.Value.EnumerateObject())
                    {
                        AssertStrict(grandchild.Value);
                    }
                }
            }
        }
    }

    /// <summary>A group's severity is genuinely optional, so it is nullable rather than omitted.</summary>
    [Fact]
    public void FindingSchema_MakesUnsetValuesNullableRatherThanOptional()
    {
        var severity = FindingSchema.Element
            .GetProperty("properties").GetProperty("groups")
            .GetProperty("items").GetProperty("properties").GetProperty("severity")
            .GetProperty("type")
            .EnumerateArray().Select(e => e.GetString())
            .ToList();

        Assert.Equal(["string", "null"], severity);
    }

    [Fact]
    public void FindingSchema_ResponseFormat_IsOmittedWhenDisabled()
    {
        Assert.NotNull(FindingSchema.ResponseFormat(enabled: true));
        Assert.Null(FindingSchema.ResponseFormat(enabled: false));
    }

    // ──────────────────────────────────────────────
    // The evidence pack
    // ──────────────────────────────────────────────

    private static CaseDocumentSearchMatch Passage(
        string document, string category, double score, string text = "text") =>
        new(text, "CASE-1", 99, document, category, category, score);

    /// <summary>
    /// The same passage retrieved by two wordings of the same question is one passage, and two
    /// different passages are two — whichever process is asking.
    /// </summary>
    [Fact]
    public void DeduplicationKey_IdentifiesThePassage()
    {
        var hit = Passage("Fact Find.md", "B", 0.9, "the client is 69");

        Assert.Equal(CheckPlanRunner.DeduplicationKey(hit), CheckPlanRunner.DeduplicationKey(hit with { Score = 0.4 }));
        Assert.NotEqual(
            CheckPlanRunner.DeduplicationKey(hit),
            CheckPlanRunner.DeduplicationKey(hit with { SearchedText = "the client is 65" }));
        Assert.NotEqual(
            CheckPlanRunner.DeduplicationKey(hit),
            CheckPlanRunner.DeduplicationKey(hit with { DocumentName = "Fact Find v2.md" }));
    }

    /// <summary>
    /// The key has to be injective across the boundary between the two fields, or a document
    /// name that ends where the text begins would collide with its neighbour.
    /// </summary>
    [Fact]
    public void DeduplicationKey_DoesNotConfuseTheFieldBoundary()
    {
        Assert.NotEqual(
            CheckPlanRunner.DeduplicationKey(Passage("ab", "B", 1, "c")),
            CheckPlanRunner.DeduplicationKey(Passage("a", "B", 1, "bc")));
    }

    [Fact]
    public void Rank_PutsTargetedCategoriesAboveBetterScoringOnes()
    {
        var ranked = CheckPlanRunner.Rank(
            [Passage("Report.md", "I", 0.99), Passage("Fact Find.md", "B", 0.40)],
            new HashSet<string>(["B"], StringComparer.OrdinalIgnoreCase));

        Assert.Equal("Fact Find.md", ranked[0].DocumentName);
    }

    /// <summary>
    /// The reason a final tiebreak exists. Take() cuts through the tie band, so without one,
    /// which passages survive depends on the order the vector store returned them in — and an
    /// approximate index is under no obligation to keep that stable between calls.
    /// </summary>
    [Fact]
    public void Rank_BreaksTiedScoresTheSameWayWhateverOrderTheyArriveIn()
    {
        var passages = Enumerable.Range(0, 20)
            .Select(i => Passage($"Doc{i:00}.md", "B", 0.5, $"passage {i:00}"))
            .ToList();

        var forwards = CheckPlanRunner.Rank(passages, EmptyTargets);
        var backwards = CheckPlanRunner.Rank(Enumerable.Reverse(passages), EmptyTargets);
        var shuffled = CheckPlanRunner.Rank(passages.OrderBy(p => p.SearchedText.Length).ThenBy(p => p.DocumentName[3]), EmptyTargets);

        Assert.Equal(CheckPlanRunner.MaxPassagesPerGroup, forwards.Count);
        Assert.Equal(forwards, backwards);
        Assert.Equal(forwards, shuffled);
    }

    /// <summary>Score still leads. The tiebreak decides ties, not the ranking.</summary>
    [Fact]
    public void Rank_KeepsTheHighestScoresFirst()
    {
        var ranked = CheckPlanRunner.Rank(
            [Passage("Zebra.md", "B", 0.9), Passage("Alpha.md", "B", 0.1)],
            EmptyTargets);

        Assert.Equal("Zebra.md", ranked[0].DocumentName);
    }

    /// <summary>
    /// Rounding the score before ranking manufactured ties that then had to be broken
    /// arbitrarily. It is rounded where it is displayed instead, so genuinely different
    /// passages stay ordered by how well they actually matched.
    /// </summary>
    [Fact]
    public void Rank_SeparatesScoresThatWouldTieIfRounded()
    {
        var ranked = CheckPlanRunner.Rank(
            [Passage("Alpha.md", "B", 0.500001), Passage("Zebra.md", "B", 0.500002)],
            EmptyTargets);

        Assert.Equal("Zebra.md", ranked[0].DocumentName);
    }

    private static readonly IReadOnlySet<string> EmptyTargets =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // ──────────────────────────────────────────────
    // The run fingerprint
    // ──────────────────────────────────────────────

    /// <summary>
    /// The reason the digest is not <see cref="string.GetHashCode()"/>: that is seeded per
    /// process, so a fingerprint recorded on Monday could not be compared with one recorded on
    /// Tuesday. Pinned here as a literal so a change of algorithm is a deliberate act.
    /// </summary>
    [Fact]
    public void Digest_IsStableAcrossProcesses()
    {
        Assert.Equal("2c26b46b68ff", RunFingerprint.Digest("foo"));
        Assert.Equal(RunFingerprint.Digest("foo"), RunFingerprint.Digest("foo"));
        Assert.NotEqual(RunFingerprint.Digest("foo"), RunFingerprint.Digest("bar"));
    }

    [Fact]
    public void DigestOfFolder_ChangesWhenAPlanChanges()
    {
        var folder = Directory.CreateTempSubdirectory("plan-digest").FullName;

        try
        {
            File.WriteAllText(Path.Combine(folder, "CHK-001.query-plan.json"), "{\"checkId\":\"CHK-001\"}");
            File.WriteAllText(Path.Combine(folder, "CHK-002.query-plan.json"), "{\"checkId\":\"CHK-002\"}");

            var before = RunFingerprint.DigestOfFolder(folder, CheckQueryPlanLoader.SearchPattern);

            Assert.Equal(before, RunFingerprint.DigestOfFolder(folder, CheckQueryPlanLoader.SearchPattern));

            File.WriteAllText(Path.Combine(folder, "CHK-002.query-plan.json"), "{\"checkId\":\"CHK-002\",\"x\":1}");

            Assert.NotEqual(before, RunFingerprint.DigestOfFolder(folder, CheckQueryPlanLoader.SearchPattern));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A missing plan folder is said plainly rather than digested into a plausible hash.</summary>
    [Fact]
    public void DigestOfFolder_SaysWhenThereIsNothingToDigest()
    {
        Assert.Equal(
            "missing",
            RunFingerprint.DigestOfFolder(Path.Combine(Path.GetTempPath(), "no-such-plan-folder"), "*.json"));

        var empty = Directory.CreateTempSubdirectory("empty-plans").FullName;
        try
        {
            Assert.Equal("empty", RunFingerprint.DigestOfFolder(empty, "*.query-plan.json"));
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    /// <summary>
    /// The fingerprint exists to answer "did anything change underneath?", so every input that
    /// can move an outcome has to be visible in it.
    /// </summary>
    [Fact]
    public void Fingerprint_RecordsEveryInputThatCanMoveAnOutcome()
    {
        var settings = new AppSettings
        {
            SelectedModel = "gpt-4.1",
            EmbeddingModel = "text-embedding-3-small",
            MaxSearchResults = 8,
            SamplingSeed = 7,
        };

        var model = new CanonicalModelDocument(
            "CASE-1", 99, "{\"a\":1}", "gpt-4.1", "1.0.0", ["Suitability Report.md"],
            DateTimeOffset.UnixEpoch, TokenUsage.Empty);

        var text = RunFingerprint.For(settings, model, "no-such-folder", planCount: 10, maxPassagesPerGroup: 12)
            .Format();

        Assert.Contains("gpt-4.1", text);
        Assert.Contains("text-embedding-3-small", text);
        Assert.Contains("seed 7", text);
        Assert.Contains("schema-constrained", text);
        Assert.Contains("8/search", text);
        Assert.Contains("12/group", text);
        Assert.Contains("plans 10@", text);
        Assert.Contains(RunFingerprint.Digest(model.Json), text);
        Assert.Contains("v1.0.0", text);
    }

    /// <summary>
    /// Unpinned sampling is stated rather than implied by absence. A report that simply omits
    /// the line reads the same as one where the setting was never available.
    /// </summary>
    [Fact]
    public void Fingerprint_SaysWhenSamplingIsNotPinned()
    {
        var settings = new AppSettings
        {
            PinTemperature = false,
            PinTopP = false,
            PinSeed = false,
            StructuredFindings = false,
        };

        var text = RunFingerprint.For(settings, null, "x", 0, 12).Format();

        Assert.Contains("temperature default", text);
        Assert.Contains("top-p default", text);
        Assert.Contains("seed not pinned", text);
        Assert.Contains("free-form", text);
        Assert.Contains("model none", text);
    }

    /// <summary>The report carries the fingerprint, which is the only place a reviewer sees it.</summary>
    [Fact]
    public void FindingsReport_PrintsTheFingerprint()
    {
        var fingerprint = RunFingerprint.For(new AppSettings(), null, "x", 3, 12);

        var text = new FindingsReport(
            "CASE-1", 99, "gpt-4.1", DateTimeOffset.Now, [], null, fingerprint).Format();

        Assert.Contains(fingerprint.Format(), text);
    }

    /// <summary>An older report with no fingerprint still formats, rather than printing a blank rule.</summary>
    [Fact]
    public void FindingsReport_OmitsTheFingerprintWhenThereIsNone()
    {
        var text = new FindingsReport("CASE-1", 99, "gpt-4.1", DateTimeOffset.Now, [], null).Format();

        Assert.Contains("QA FINDINGS", text);
        Assert.DoesNotContain("seed", text);
    }
}
