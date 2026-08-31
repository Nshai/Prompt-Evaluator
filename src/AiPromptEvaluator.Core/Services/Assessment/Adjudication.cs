namespace AiPromptEvaluator;

/// <summary>
/// Applies a group's <c>adjudication</c> directive to the finding it produced.
///
/// <b>Why this is code and not a prompt clause.</b> The two things it enforces were both put to
/// models as instructions and both were ignored. Severity was steered in prose and produced ten
/// "contradictions" at High on one undisclosed charge basis, tabulated ten times — the largest
/// false positive in either measured run. Citation discipline was stated in the standing rules and
/// still produced 99 untraceable quotes on one model. A ceiling that code applies cannot be argued
/// with; a ceiling in a sentence has been.
///
/// <b>It can only ever reduce.</b> Nothing here raises a severity, converts an outcome towards a
/// concern, or invents a finding — a plan that could do those could manufacture a finding, and the
/// whole point of the retrieval/verification split is that the plan governs what is looked at and
/// not what is concluded.
/// </summary>
public static class Adjudication
{
    /// <summary>Severity vocabulary, weakest first. Matches the finding schema's enum.</summary>
    private static readonly string[] Order = ["Low", "Moderate", "High"];

    /// <summary>
    /// Where a ceiling sits in that order, or null when it names a level the schema does not
    /// have.
    ///
    /// <b>A ceiling above the vocabulary binds nothing, and that is worth knowing rather than
    /// silently tolerating.</b> The plans as authored set <c>Critical</c> on 49 groups and
    /// <c>High</c> on 33; the finding schema's enum is High/Moderate/Low, so 82 of the 88 ceilings
    /// cannot clamp anything a model is able to emit. Only the 6 at <c>Moderate</c> do any work.
    /// </summary>
    internal static int? Rank(string? severity) =>
        severity is null
            ? null
            : Array.FindIndex(Order, s => s.Equals(severity.Trim(), StringComparison.OrdinalIgnoreCase))
                is var index && index >= 0
                    ? index
                    : null;

    /// <summary>
    /// The finding as the plan permits it to be reported, and what was changed.
    /// </summary>
    /// <param name="finding">The finding as the assessor returned it, after citation verification.</param>
    /// <param name="group">The requirement, carrying its directive.</param>
    public static (GroupFinding Finding, IReadOnlyList<string> Applied) Apply(
        GroupFinding finding, PlanQueryGroup group)
    {
        var directive = group.Adjudication;
        var applied = new List<string>();
        var result = finding;

        // Severity ceiling. Only downwards, and only where both the finding and the ceiling name a
        // level the schema actually has.
        if (Rank(directive.SeverityCeiling) is { } ceiling
            && Rank(result.Severity) is { } stated
            && stated > ceiling)
        {
            applied.Add(
                $"severity {result.Severity} lowered to {Order[ceiling]}: the plan caps this "
                + "requirement there.");

            result = result with { Severity = Order[ceiling] };
        }

        // Citation discipline. A requirement whose plan says a finding must be locatable does not
        // get to report one that is not — but the working is kept and the outcome is moved to
        // Indeterminate rather than to No Issue, because "we could not evidence this" is not the
        // same answer as "there is nothing here", and collapsing the two is how an absence becomes
        // a clean pass.
        if (directive.RequiresCitation
            && result.ParsedOutcome == CheckOutcome.PotentialConcern
            && result.Citations.Count == 0)
        {
            applied.Add(
                "a concern was raised with no citation, and this requirement's plan requires a "
                + "locatable quote; reported as Indeterminate instead.");

            result = result with
            {
                Outcome = nameof(CheckOutcome.Indeterminate),
                MissingInputs = [.. result.MissingInputs, "a locatable quote for the discrepancy"],
            };
        }

        return (result, applied);
    }
}
