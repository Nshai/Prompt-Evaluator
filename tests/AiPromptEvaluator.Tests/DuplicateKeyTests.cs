using System.Text.Json.Nodes;

using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The duplicate property name that cost <c>existingArrangements</c> in three runs running.
///
/// <b>It is the worst kind of malformed reply, because it parses.</b> The reader called it well
/// formed, nothing retried, and it detonated at whatever line first asked the object for its
/// count — five pension plans, their values, charges, risk ratings and advice actions, and the
/// identifier table every later pass needed, lost to a two-line stutter with identical values on
/// both sides.
/// </summary>
public class DuplicateKeyTests
{
    private readonly ITestOutputHelper _output;

    public DuplicateKeyTests(ITestOutputHelper output) => _output = output;

    // ── the defect, minimally ─────────────────────────────────────────────────

    /// <summary>
    /// The behaviour that made this invisible: parsing succeeds, and the object is only built
    /// when something asks. This pins the old failure so the fix cannot quietly regress into it.
    /// </summary>
    [Fact]
    public void RawJsonNodeParsingAcceptsADuplicateAndThrowsLater()
    {
        var node = JsonNode.Parse("{\"a\": 1, \"a\": 1}") as JsonObject;

        Assert.NotNull(node);
        Assert.Throws<ArgumentException>(() => node!.Count);
    }

    [Fact]
    public void TheReaderKeepsTheFirstOccurrenceAndSaysSo()
    {
        var fragment = ExtractionResponseReader.ParseObject(
            "{\"numberOfFundsAvailable\": \"7\", \"allowsAdviserServicing\": false, "
            + "\"numberOfFundsAvailable\": \"7\"}",
            out var duplicates);

        Assert.NotNull(fragment);
        Assert.Equal(2, fragment!.Count);
        Assert.Equal("7", fragment["numberOfFundsAvailable"]!.GetValue<string>());

        var duplicate = Assert.Single(duplicates);
        Assert.Equal("numberOfFundsAvailable", duplicate.Key);
        Assert.False(duplicate.ValuesDiffer);
    }

    /// <summary>
    /// The first, not the last. The model writes in document order, so the first is what it wrote
    /// while reading the source; a repeat is a stutter, not a correction.
    /// </summary>
    [Fact]
    public void ADisagreeingDuplicateKeepsTheFirstAndIsFlagged()
    {
        var fragment = ExtractionResponseReader.ParseObject(
            "{\"currentValue\": 103439.24, \"currentValue\": 3002}",
            out var duplicates);

        Assert.Equal(103439.24, fragment!["currentValue"]!.GetValue<double>());

        var duplicate = Assert.Single(duplicates);
        Assert.True(duplicate.ValuesDiffer);
        Assert.Contains("values differ", duplicate.ToString());
    }

    /// <summary>
    /// The observed one was five levels down inside an array element, which is why the report
    /// names the containing object rather than only the field.
    /// </summary>
    [Fact]
    public void ADuplicateNestedInAnArrayIsFoundAndLocated()
    {
        var fragment = ExtractionResponseReader.ParseObject(
            "{\"existingArrangements\": [{\"provider\": \"Aviva\"}, "
            + "{\"provider\": \"Zurich\", \"riskRating\": 7, \"riskRating\": 7}]}",
            out var duplicates);

        Assert.NotNull(fragment);

        var duplicate = Assert.Single(duplicates);
        Assert.Equal("/existingArrangements/1", duplicate.Path);
        Assert.Equal("riskRating", duplicate.Key);
    }

    [Fact]
    public void AReplyWithNoDuplicatesReportsNone()
    {
        var fragment = ExtractionResponseReader.ParseObject(
            "{\"a\": 1, \"b\": {\"a\": 2}, \"c\": [{\"a\": 3}]}",
            out var duplicates);

        Assert.NotNull(fragment);
        Assert.Empty(duplicates);
    }

    /// <summary>
    /// Rebuilding the tree must not change it. Everything a well-formed reply carried has to
    /// survive, or the fix costs more than the defect.
    /// </summary>
    [Fact]
    public void RebuildingPreservesEveryValueKind()
    {
        var json =
            "{\"s\":\"text\",\"i\":42,\"d\":3.14,\"t\":true,\"f\":false,\"n\":null,"
            + "\"a\":[1,\"two\",null,{\"deep\":true},[]],\"o\":{\"nested\":{\"deeper\":[]}}}";

        var fragment = ExtractionResponseReader.ParseObject(json, out _);

        Assert.NotNull(fragment);
        Assert.Equal("text", fragment!["s"]!.GetValue<string>());
        Assert.Equal(42, fragment["i"]!.GetValue<int>());
        Assert.Equal(3.14, fragment["d"]!.GetValue<double>());
        Assert.True(fragment["t"]!.GetValue<bool>());
        Assert.False(fragment["f"]!.GetValue<bool>());
        Assert.Null(fragment["n"]);
        Assert.Equal(5, fragment["a"]!.AsArray().Count);
        Assert.True(fragment["a"]![3]!["deep"]!.GetValue<bool>());
        Assert.Empty(fragment["o"]!["nested"]!["deeper"]!.AsArray());
    }

    /// <summary>A fenced reply still parses; the fence handling runs before any of this.</summary>
    [Fact]
    public void AFencedReplyWithADuplicateStillParses()
    {
        var fragment = ExtractionResponseReader.ParseObject(
            "```json\n{\"a\": 1, \"a\": 2}\n```",
            out var duplicates);

        Assert.NotNull(fragment);
        Assert.Single(duplicates);
    }

    // ── the run that actually happened ────────────────────────────────────────

    /// <summary>
    /// The real reply, replayed. Three logs of 2026-08-26 carry it identically, and each one
    /// reported "An item with the same key has already been added. Key: numberOfFundsAvailable"
    /// and lost the section.
    ///
    /// The log is not a build dependency: when it is absent the test says it skipped.
    /// </summary>
    [Fact]
    public void TheObservedExistingArrangementsReply_NowParsesAndKeepsAllFivePlans()
    {
        if (SectionResponse("Existing arrangements") is not { } response)
        {
            _output.WriteLine("Skipped: the extraction log is not in this working copy.");
            return;
        }

        var fragment = ExtractionResponseReader.ParseObject(response, out var duplicates);

        Assert.NotNull(fragment);

        // What the run reported, now named rather than thrown — and there were two, not one.
        // The exception could only ever name the first, because it threw on it; the second was
        // invisible for as long as the failure was an exception rather than a report. The model
        // repeated a run of two properties, not a single key.
        _output.WriteLine("Duplicates: " + string.Join(", ", duplicates));

        Assert.Equal(2, duplicates.Count);
        Assert.Equal(
            ["numberOfFundsAvailable", "allowsAdviserServicing"],
            duplicates.Select(d => d.Key));

        // Both sides agreed, which is what makes this a stutter rather than a correction — and
        // what makes keeping the first lossless here.
        Assert.All(duplicates, d => Assert.False(d.ValuesDiffer));
        Assert.All(duplicates, d => Assert.Equal("/existingArrangements/0", d.Path));

        // What was being lost. The count is the whole point: five plans, not four.
        var arrangements = fragment!["existingArrangements"]!.AsArray();
        Assert.Equal(5, arrangements.Count);

        // And the payload the checks read. CHK-003 G3.7 needs the People's Pension at risk 9
        // against an agreed rating of 5 — one of the benchmark's Highest findings.
        var ratings = arrangements
            .Select(a => a!["riskRating"]?["value"]?.GetValue<string>())
            .ToList();

        _output.WriteLine("Risk ratings: " + string.Join(", ", ratings));
        Assert.Contains("9", ratings);

        // The identifier table every later pass needed, which is why the cross-references
        // dangled once this section was lost.
        Assert.All(arrangements, a => Assert.NotNull(a!["arrangementId"]));
    }

    // ──────────────────────────────────────────────

    private static string? SectionResponse(string name)
    {
        var log = Directory
            .GetFiles(RepoRoot(), "extract_ABC-99_20260826_*.log", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .LastOrDefault();

        if (log is null)
        {
            return null;
        }

        var text = File.ReadAllText(log);
        var banner = text.IndexOf("extract — " + name, StringComparison.Ordinal);

        if (banner < 0)
        {
            return null;
        }

        var start = text.IndexOf("[RESPONSE]", banner, StringComparison.Ordinal);
        var end = text.IndexOf(new string('=', 100), start, StringComparison.Ordinal);

        return text[(start + "[RESPONSE]".Length)..(end < 0 ? text.Length : end)];
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiPromptEvaluator.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
