using System.Text.Json.Nodes;

using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Replays the extraction run of 2026-08-13 through the code that was written to fix it.
///
/// The unit tests either side of this use synthetic replies, which prove the logic and cannot
/// prove it was aimed at the right target. These read the artefacts the failure actually left
/// behind, so the claims in the gap analysis are checked rather than asserted.
///
/// The logs are not a build dependency: when they are absent the tests report that they skipped.
/// </summary>
public class ObservedExtractionFailureTests
{
    private readonly ITestOutputHelper _output;

    public ObservedExtractionFailureTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// The reported error was "the model did not return a JSON object for this section". It did:
    /// it returned 61,422 characters of well-formed JSON and was cut off at the 16,000-token cap
    /// with four of five recommendations complete. All four were discarded.
    /// </summary>
    [Fact]
    public void TheRecommendationsSection_WasTruncatedAndIsMostlyRecoverable()
    {
        if (SectionResponse("Recommendations") is not { } response)
        {
            _output.WriteLine("Skipped: the extraction log is not in this working copy.");
            return;
        }

        _output.WriteLine($"Response length: {response.Length:N0} characters.");

        // What the pipeline saw, and the wrong conclusion it drew from it.
        Assert.Null(ExtractionResponseReader.ParseObject(response));

        // What was actually wrong.
        Assert.True(
            ExtractionResponseReader.LooksTruncated(response),
            "The reply is cut off mid-string, which is not the same failure as malformed output.");

        var salvaged = ExtractionResponseReader.Salvage(response);
        Assert.NotNull(salvaged);

        var recovered = ExtractionResponseReader.SalvagedCount(salvaged, "recommendations");
        _output.WriteLine($"Recommendations recovered: {recovered}.");

        Assert.Equal(4, recovered);

        // Recovered entries must be whole, not repaired into something plausible.
        foreach (var recommendation in salvaged!["recommendations"]!.AsArray())
        {
            Assert.False(
                string.IsNullOrWhiteSpace(recommendation!["recommendationId"]?.GetValue<string>()),
                "A salvaged recommendation must carry its own id.");
        }
    }

    /// <summary>Every other section completed, and must not be mistaken for truncated.</summary>
    [Fact]
    public void EveryOtherSection_ParsesAndIsNotReportedAsTruncated()
    {
        var sections = AllSections();
        if (sections.Count == 0)
        {
            _output.WriteLine("Skipped: the extraction log is not in this working copy.");
            return;
        }

        foreach (var (name, response) in sections.Where(s => s.Name != "Recommendations"))
        {
            Assert.False(ExtractionResponseReader.LooksTruncated(response), $"{name} is complete.");
            Assert.NotNull(ExtractionResponseReader.ParseObject(response));
        }

        _output.WriteLine($"Checked {sections.Count - 1} completed sections.");
    }

    /// <summary>
    /// The silent failure. The stored model's recommendations point at objectives by invented
    /// labels — "LowerCosts", "ConsolidationAndClarity" — none of which exist in objectives.
    /// Nothing reported it, and CHK-006 could not be answered as a result.
    /// </summary>
    [Fact]
    public void TheStoredModel_HasCrossReferencesThatResolveToNothing()
    {
        if (StoredModel() is not { } root)
        {
            _output.WriteLine("Skipped: the stored canonical model is not in this working copy.");
            return;
        }

        var identity = new CanonicalModelIdentity();
        identity.Adopt(root);
        identity.RewriteReferences(root);

        var dangling = identity.DanglingReferences(root);

        foreach (var reference in dangling)
        {
            _output.WriteLine(reference);
        }

        Assert.NotEmpty(dangling);
        Assert.Contains(dangling, d => d.StartsWith("linkedObjectiveIds", StringComparison.Ordinal));
    }

    /// <summary>
    /// And that the fix would have held: with ids adopted, every entity the model defines has a
    /// canonical id, so a later pass has something stable to reference.
    /// </summary>
    [Fact]
    public void AdoptingIds_GivesEveryDefinedEntityACanonicalIdentifier()
    {
        if (StoredModel() is not { } root)
        {
            _output.WriteLine("Skipped: the stored canonical model is not in this working copy.");
            return;
        }

        var identity = new CanonicalModelIdentity();
        identity.Adopt(root);

        Assert.True(identity.HasEntities);

        var clients = root["parties"]?["clients"]?.AsArray() ?? [];
        Assert.All(clients, c => Assert.StartsWith("C", c!["clientId"]!.GetValue<string>(), StringComparison.Ordinal));

        var arrangements = root["existingArrangements"]?.AsArray() ?? [];
        Assert.All(arrangements, a =>
            Assert.StartsWith("EA", a!["arrangementId"]!.GetValue<string>(), StringComparison.Ordinal));

        _output.WriteLine(identity.Table());
    }

    /// <summary>
    /// The stored model records who extracted it as "claude-opus" at "2025-01-01T00:00:00Z".
    /// The run was Claude Haiku 4.5 on 2026-08-13. Both are now stamped by the process, and the
    /// schema no longer asks the model for either.
    /// </summary>
    [Fact]
    public void TheSchemaSliceForSource_NoLongerAsksTheModelForFieldsTheProcessOwns()
    {
        var slice = CanonicalModelExtractor.StripCodeOwnedFields(
            """
            {
              "type": "object",
              "properties": {
                "source": {
                  "type": "object",
                  "properties": {
                    "fileName": { "type": "string" },
                    "extractedAt": { "type": "string" },
                    "extractorModel": { "type": "string" }
                  }
                }
              }
            }
            """);

        var source = JsonNode.Parse(slice)!["properties"]!["source"]!["properties"]!.AsObject();

        Assert.False(source.ContainsKey("extractedAt"));
        Assert.False(source.ContainsKey("extractorModel"));
        Assert.True(source.ContainsKey("fileName"));
    }

    private static IReadOnlyList<(string Name, string Response)> AllSections()
    {
        if (Locate("extract_ABC-99_20260813_125630.log") is not { } path)
        {
            return [];
        }

        var separator = new string('=', 100);
        var parts = File.ReadAllText(path).Split(separator);
        var sections = new List<(string, string)>();

        for (var i = 1; i < parts.Length - 1; i += 2)
        {
            var header = parts[i].Trim();
            if (!header.StartsWith("extract", StringComparison.Ordinal))
            {
                continue;
            }

            var marker = parts[i + 1].IndexOf("[RESPONSE]", StringComparison.Ordinal);
            if (marker < 0)
            {
                continue;
            }

            // "extract — Recommendations  (2026-08-13 …)" → "Recommendations"
            var name = header.Split('(')[0].Split('—').Last().Trim();
            sections.Add((name, parts[i + 1][(marker + "[RESPONSE]".Length)..].Trim()));
        }

        return sections;
    }

    private static string? SectionResponse(string name) =>
        AllSections().FirstOrDefault(s => s.Name == name).Response;

    private static JsonObject? StoredModel()
    {
        if (Locate("ABC-Cononical-Model.txt") is not { } path)
        {
            return null;
        }

        var text = File.ReadAllText(path);
        var marker = text.IndexOf("[CANONICAL MODEL JSON]", StringComparison.Ordinal);

        return marker < 0
            ? null
            : JsonNode.Parse(text[(marker + "[CANONICAL MODEL JSON]".Length)..].Trim()) as JsonObject;
    }

    /// <summary>Walks up from the test binary to a file under the runtime extraction logs.</summary>
    private static string? Locate(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, "docs", "Runtime-Logs", "extraction", fileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
