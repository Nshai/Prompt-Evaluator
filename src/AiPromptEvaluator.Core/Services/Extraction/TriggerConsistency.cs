using System.Text.Json.Nodes;

namespace AiPromptEvaluator;

/// <summary>
/// One trigger the extraction set false while the model it sits in says otherwise.
/// </summary>
/// <param name="Trigger">The <c>checkTriggers</c> field, without its prefix.</param>
/// <param name="Contradicts">What in the model implies the trigger should be true.</param>
/// <param name="Evidence">The value found there, quoted, so the reader need not go looking.</param>
public sealed record TriggerContradiction(string Trigger, string Contradicts, string Evidence)
{
    public override string ToString() =>
        $"checkTriggers.{Trigger} is false, but {Contradicts} says {Evidence}";
}

/// <summary>
/// Checks the derived triggers against the model they were derived from.
///
/// <b>A false trigger switches a check off, and a wrongly false one switches it off silently.</b>
/// CHK-005 did not run in two consecutive runs because <c>hasCapitalContributionsOrWithdrawals</c>
/// came back false for a case with £110,185 being transferred and a £3,305.55 fee, and both runs
/// printed the check under CHECKS CLEARED. Two analyses read a cache artefact as a clean check
/// before anyone noticed, and the recall figures either side of it are out of 32 findings rather
/// than 36.
///
/// The next run's extraction was genuine and got that trigger right, which fixed the instance and
/// not the class. Two others in the same artefact disagreed with the data beside them:
/// <c>hasComplexProduct</c> was false while <c>recommendedProductComplexity[0].complexity</c> read
/// "Complex" with six named drivers, and <c>hasVulnerabilityIndicators</c> was false for a file
/// recording no investment knowledge and a monthly deficit. Neither cost anything, for unrelated
/// reasons — no plan reads the first, and CHK-010 is an unconditional overlay that runs regardless.
/// Both were luck, and the second is the warning: CHK-010's <c>triggerProbe</c> names
/// <c>hasVulnerabilityIndicators</c>, so a gate there would have taken both Highest-severity
/// vulnerability findings with it.
///
/// So the pairs are checked mechanically. Each is a place the model states something a trigger is
/// meant to summarise, and the check runs one way only: a trigger false against data saying true is
/// reported, and the reverse is not. A trigger that is true costs a check that need not have run,
/// which is visible in the output and cheap; one that is wrongly false costs a check nobody sees
/// missing.
/// </summary>
public static class TriggerConsistency
{
    /// <summary>
    /// Every trigger in <paramref name="model"/> that is false while the model contradicts it.
    ///
    /// The pairs are named rather than derived, because the relationship between a trigger and the
    /// data behind it is a judgement about the check catalogue and not a property of the schema.
    /// A pair added here should be one where the data plainly settles the question — not one where
    /// a reader might reasonably read the model either way.
    /// </summary>
    public static IReadOnlyList<TriggerContradiction> Contradictions(JsonNode? model)
    {
        if (model is not JsonObject root)
        {
            return [];
        }

        var triggers = root["checkTriggers"] as JsonObject;

        if (triggers is null)
        {
            return [];
        }

        var found = new List<TriggerContradiction>();

        // A product the model calls Complex, against the trigger that says none is.
        Pair(
            "hasComplexProduct",
            "knowledgeAndExperience.recommendedProductComplexity[].complexity",
            FirstMatch(
                Array(root["knowledgeAndExperience"]?["recommendedProductComplexity"]),
                item => Text(item?["complexity"]) is { } c
                        && c.Equals("Complex", StringComparison.OrdinalIgnoreCase)
                    ? $"\"{Text(item?["productName"]) ?? "a recommended product"}\" is Complex"
                    : null));

        // Money moving into or out of a plan, against the trigger that says none does. This is the
        // pair that cost CHK-005 two runs.
        Pair(
            "hasCapitalContributionsOrWithdrawals",
            "existingArrangements[].contributions / withdrawals",
            FirstMatch(
                Array(root["existingArrangements"]),
                item => Array(item?["contributions"]).Count > 0
                    ? "an arrangement records contributions"
                    : Array(item?["withdrawals"]).Count > 0
                        ? "an arrangement records withdrawals"
                        : null)
            ?? FirstMatch(
                Array(root["costsAndCharges"]?["adviserFees"]),
                _ => "an adviser fee is charged"));

        // A vulnerability recorded per client, against the trigger that says there are none.
        Pair(
            "hasVulnerabilityIndicators",
            "vulnerability.perClient[].hasVulnerability",
            FirstMatch(
                Array(root["vulnerability"]?["perClient"]),
                item => Text(item?["hasVulnerability"]) is { } v
                        && !v.Equals("No", StringComparison.OrdinalIgnoreCase)
                        && !v.Equals("Unspecified", StringComparison.OrdinalIgnoreCase)
                    ? $"a client is recorded as \"{v}\""
                    : null));

        // A plan being replaced, against the trigger that says nothing is.
        Pair(
            "hasReplacementOrSwitch",
            "replacementAnalysis[]",
            Array(root["replacementAnalysis"]).Count > 0
                ? $"{Array(root["replacementAnalysis"]).Count} arrangement(s) are being replaced"
                : null);

        return found;

        void Pair(string trigger, string contradicts, string? evidence)
        {
            if (evidence is not null && IsFalse(triggers[trigger]))
            {
                found.Add(new TriggerContradiction(trigger, contradicts, evidence));
            }
        }
    }

    /// <summary>
    /// True only where the value is present and false. A trigger the extraction omitted is a
    /// different problem and is not this one: an absent trigger does not gate a check, because
    /// <c>CheckPlanRunner</c> reports "no value for" it and carries on.
    /// </summary>
    private static bool IsFalse(JsonNode? node) =>
        node is JsonValue value
        && bool.TryParse(value.ToString(), out var parsed)
        && !parsed;

    private static JsonArray Array(JsonNode? node) => node as JsonArray ?? [];

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.ToString() is { Length: > 0 } text ? text : null;

    /// <summary>The first item the predicate describes, or null where none does.</summary>
    private static string? FirstMatch(JsonArray items, Func<JsonNode?, string?> describe)
    {
        foreach (var item in items)
        {
            if (describe(item) is { } description)
            {
                return description;
            }
        }

        return null;
    }
}
