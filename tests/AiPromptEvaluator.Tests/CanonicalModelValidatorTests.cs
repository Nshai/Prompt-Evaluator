using System.Text.Json.Nodes;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Enforcing the schema's enums, which the system prompt asks for and nothing checked.
///
/// A real extraction put 47 values into enum slots that the schema does not allow, 42 of them on
/// "basis" — including the sentence "annual growth required at mid-growth rate 2.94% up to age 75".
/// Every one reached a check as though the schema had sanctioned it.
/// </summary>
public class CanonicalModelValidatorTests
{
    private const string Schema =
        """
        {
          "type": "object",
          "properties": {
            "costsAndCharges": {
              "type": "object",
              "properties": {
                "adviserFees": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "amount": { "type": "number" },
                      "basis": { "enum": ["PercentageOfFundValue", "FixedAmount", "Unspecified"] }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private static JsonObject Fragment(string basis) => JsonNode.Parse(
        $$"""
        {
          "costsAndCharges": {
            "adviserFees": [ { "amount": 3305.55, "basis": {{basis}} } ]
          }
        }
        """)!.AsObject();

    [Fact]
    public void EnumViolations_AcceptsAValueTheSchemaAllows() =>
        Assert.Empty(CanonicalModelValidator.EnumViolations(
            Fragment("\"PercentageOfFundValue\""), Schema));

    /// <summary>The observed defect: free text in an enum slot.</summary>
    [Fact]
    public void EnumViolations_ReportsFreeTextInAnEnumField()
    {
        var violations = CanonicalModelValidator.EnumViolations(
            Fragment("\"annual growth required at mid-growth rate 2.94% up to age 75\""), Schema);

        Assert.Single(violations);
        Assert.Equal("basis", violations[0].Property);
    }

    /// <summary>
    /// A bad enum must not cost the figure standing beside it. The pass did the expensive work
    /// of reading the report; one unusable value is not a reason to discard the section.
    /// </summary>
    [Fact]
    public void StripEnumViolations_RemovesTheValueAndKeepsTheRest()
    {
        var fragment = Fragment("\"of fund value pa\"");

        var removed = CanonicalModelValidator.StripEnumViolations(fragment, Schema);

        Assert.Single(removed);
        Assert.Equal("basis = \"of fund value pa\"", removed[0].ToString());

        var fee = fragment["costsAndCharges"]!["adviserFees"]![0]!.AsObject();
        Assert.False(fee.ContainsKey("basis"));
        Assert.Equal(3305.55m, fee["amount"]!.GetValue<decimal>());
    }

    /// <summary>A property with no enum anywhere in the schema is never touched.</summary>
    [Fact]
    public void StripEnumViolations_LeavesUnconstrainedPropertiesAlone()
    {
        var fragment = JsonNode.Parse(
            """{ "costsAndCharges": { "adviserFees": [ { "amount": 12, "note": "anything at all" } ] } }""")!
            .AsObject();

        Assert.Empty(CanonicalModelValidator.StripEnumViolations(fragment, Schema));
        Assert.Equal(
            "anything at all",
            fragment["costsAndCharges"]!["adviserFees"]![0]!["note"]!.GetValue<string>());
    }

    /// <summary>
    /// The real schema slices must not flag their own reference model. If they did, this would
    /// be deleting good data on every run.
    /// </summary>
    [Fact]
    public void AllowedValues_ReadsEnumsOutOfTheRealSchema()
    {
        var schemaPath = LocateSchema();
        if (schemaPath is null)
        {
            return;
        }

        var allowed = CanonicalModelValidator.AllowedValues(File.ReadAllText(schemaPath));

        Assert.NotEmpty(allowed);
        Assert.True(
            allowed.ContainsKey("assertionStatus"),
            "assertionStatus is an enum in the canonical schema and should be picked up.");
        Assert.Contains("Stated", allowed["assertionStatus"]);
        Assert.Contains("Absent", allowed["assertionStatus"]);
    }

    /// <summary>Walks up from the test binary to the repository's canonical model schema.</summary>
    private static string? LocateSchema()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, "docs", "artifacts", "canonical-suitability-model.schema.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
