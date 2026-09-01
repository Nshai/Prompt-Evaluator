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
/// One trigger the extraction set false or omitted that the model settled to true, upgrade-only.
/// </summary>
/// <param name="Trigger">The <c>checkTriggers</c> field, without its prefix.</param>
/// <param name="From">What the model wrote before it was settled, quoted, or <c>absent</c>.</param>
/// <param name="Evidence">What in the model settles the trigger true, so the reader need not go looking.</param>
public sealed record TriggerDerivation(string Trigger, string From, string Evidence)
{
    public override string ToString() =>
        $"checkTriggers.{Trigger} settled to true (was {From}) because {Evidence}";
}

/// <summary>
/// Settles the mechanical triggers from the model they summarise, and watches whatever it does not
/// settle.
///
/// <b>A false trigger switches a check off, and a wrongly false one switches it off silently.</b>
/// CHK-005 did not run in two consecutive runs because <c>hasCapitalContributionsOrWithdrawals</c>
/// came back false for a case with £110,185 being transferred and a £3,305.55 fee, and both runs
/// printed the check under CHECKS CLEARED. Two analyses read a cache artefact as a clean check
/// before anyone noticed, and the recall figures either side of it are out of 32 findings rather
/// than 36.
///
/// A genuine re-run got that trigger right, which fixed the instance and not the class:
/// <c>hasComplexProduct</c> came back false beside a Complex product with six named drivers across
/// two further runs, because the pass that writes the triggers derives a boolean from sections it
/// wrote earlier and reasons over a summary. But these triggers are pure functions of data already
/// in the model, so the code settles them rather than asking. <see cref="Derive"/> does that,
/// upgrade-only: a trigger the data supports is forced true, one it does not support is left as the
/// model wrote it — never forced false, the same one-way direction the runtime net already ran.
///
/// <see cref="Contradictions"/> stays as defence in depth. After derivation the four settled pairs
/// report nothing, so it earns its keep on what is not derived — a future model-decided trigger, or
/// an older stored model loaded at run time that predates the derive step. It runs one way only: a
/// trigger false against data saying true is reported, and the reverse is not. A trigger wrongly
/// true costs a check that need not have run, which is visible in the output and cheap; one wrongly
/// false costs a check nobody sees missing.
/// </summary>
public static class TriggerConsistency
{
    /// <summary>
    /// Every trigger in <paramref name="model"/> that is false while the model contradicts it. See
    /// the type summary for why this still runs after <see cref="Derive"/> has settled the same pairs.
    /// </summary>
    public static IReadOnlyList<TriggerContradiction> Contradictions(JsonNode? model)
    {
        if (model is not JsonObject root || root["checkTriggers"] is not JsonObject triggers)
        {
            return [];
        }

        var found = new List<TriggerContradiction>();

        foreach (var pair in Pairs)
        {
            if (pair.Evidence(root) is { } evidence && IsFalse(triggers[pair.Trigger]))
            {
                found.Add(new TriggerContradiction(pair.Trigger, pair.Contradicts, evidence));
            }
        }

        return found;
    }

    /// <summary>
    /// Settles the mechanical triggers from the model they summarise, upgrade-only: a trigger the
    /// data supports is forced true, and one the data does not support is left exactly as the model
    /// wrote it — never forced false.
    ///
    /// This is the same one-way direction as the runtime net in <c>CheckPlanRunner.ProbeTriggerAsync</c>
    /// — code can turn a check on, never silently off — moved to the source so the stored model
    /// carries the right value instead of being rescued at run time. The pass-12 model kept getting
    /// these wrong (<c>hasComplexProduct</c> false beside a Complex product over two runs), because it
    /// derives a boolean from sections it wrote in earlier passes and reasons over a summary; these
    /// four are pure functions of data already in the model, so the code settles them.
    ///
    /// A no-op where the model carries no <c>checkTriggers</c> object: a failed pass 12 leaves the
    /// runner to fall through to applicability and probe search, which is the existing safe path.
    /// Returns what it changed, for logging.
    /// </summary>
    public static IReadOnlyList<TriggerDerivation> Derive(JsonNode? model)
    {
        if (model is not JsonObject root || root["checkTriggers"] is not JsonObject triggers)
        {
            return [];
        }

        var settled = new List<TriggerDerivation>();

        foreach (var pair in Pairs)
        {
            if (pair.Evidence(root) is not { } evidence)
            {
                continue;
            }

            var current = triggers[pair.Trigger];

            // Already true is the model and the data agreeing — nothing to settle, nothing to log.
            if (current is JsonValue value
                && bool.TryParse(value.ToString(), out var parsed)
                && parsed)
            {
                continue;
            }

            var from = current is null ? "absent" : current.ToJsonString();
            triggers[pair.Trigger] = true;
            settled.Add(new TriggerDerivation(pair.Trigger, from, evidence));
        }

        return settled;
    }

    /// <summary>
    /// The trigger/data pairs, single-sourced so <see cref="Contradictions"/> and <see cref="Derive"/>
    /// read the same predicates. Each <c>Evidence</c> returns a quoted description when the data
    /// supports the trigger being true, else null.
    ///
    /// The pairs are named rather than derived from the schema, because the relationship between a
    /// trigger and the data behind it is a judgement about the check catalogue and not a property of
    /// the schema. A pair added here should be one where the data plainly settles the question — not
    /// one where a reader might reasonably read the model either way.
    /// </summary>
    private static readonly IReadOnlyList<(string Trigger, string Contradicts, Func<JsonObject, string?> Evidence)> Pairs =
    [
        // A product the model calls Complex.
        (
            "hasComplexProduct",
            "knowledgeAndExperience.recommendedProductComplexity[].complexity",
            root => FirstMatch(
                Array(root["knowledgeAndExperience"]?["recommendedProductComplexity"]),
                item => Text(item?["complexity"]) is { } c
                        && c.Equals("Complex", StringComparison.OrdinalIgnoreCase)
                    ? $"\"{Text(item?["productName"]) ?? "a recommended product"}\" is Complex"
                    : null)),

        // Money moving into or out of a plan. This is the pair that cost CHK-005 two runs.
        (
            "hasCapitalContributionsOrWithdrawals",
            "existingArrangements[].contributions / withdrawals",
            root => FirstMatch(
                Array(root["existingArrangements"]),
                item => Array(item?["contributions"]).Count > 0
                    ? "an arrangement records contributions"
                    : Array(item?["withdrawals"]).Count > 0
                        ? "an arrangement records withdrawals"
                        : null)
                ?? FirstMatch(
                    Array(root["costsAndCharges"]?["adviserFees"]),
                    _ => "an adviser fee is charged")),

        // A vulnerability recorded per client.
        (
            "hasVulnerabilityIndicators",
            "vulnerability.perClient[].hasVulnerability",
            root => FirstMatch(
                Array(root["vulnerability"]?["perClient"]),
                item => Text(item?["hasVulnerability"]) is { } v
                        && !v.Equals("No", StringComparison.OrdinalIgnoreCase)
                        && !v.Equals("Unspecified", StringComparison.OrdinalIgnoreCase)
                    ? $"a client is recorded as \"{v}\""
                    : null)),

        // A plan being replaced.
        (
            "hasReplacementOrSwitch",
            "replacementAnalysis[]",
            root => Array(root["replacementAnalysis"]).Count > 0
                ? $"{Array(root["replacementAnalysis"]).Count} arrangement(s) are being replaced"
                : null),
    ];

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
