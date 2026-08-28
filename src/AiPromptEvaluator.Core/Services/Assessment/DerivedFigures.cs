using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace AiPromptEvaluator;

/// <summary>
/// Arithmetic the checks turn on, computed from the canonical model before any model call.
///
/// This exists because delegating arithmetic to the assessor fails in both directions, and the
/// failures were measured rather than imagined.
///
/// **It misses things it is standing on.** Asked whether a charge comparison was sound, one
/// group back-solved the fund value implied by one row — "at 0.93% this would require a fund
/// value of approximately £103,430" — and then listed the same calculation for the next row as
/// a *missing input*. £186.19 at 0.18% implies £103,439: the same fund, four sentences apart,
/// which is the whole finding. It had both numbers and did not divide.
///
/// **It invents things that are not there.** Handed a report that mis-stated a monthly figure
/// as "your annual expenditure to be approximately £1,700", another group reasoned that the
/// monthly figure must therefore be £141.67 and carried that forward. A unit contradiction was
/// converted into a confident wrong number.
///
/// **And where it succeeds, it succeeds by luck.** The one arithmetic finding the run did
/// reach — five plans summing to £116,997.47 against a stated £110,000 — was right only
/// because the *extraction* had summed them and written the total into a string. Nothing in
/// the assessment path adds five values.
///
/// So the sums, the percentage-of relationships and the frequency conversions are done here,
/// deterministically, and handed to the assessor as facts. None of them need retrieval and all
/// of them are decidable.
/// </summary>
public static class DerivedFigures
{
    /// <summary>One computed fact, with the working shown so a reader can check it.</summary>
    public sealed record Figure(string Topic, string Statement)
    {
        public override string ToString() => $"{Topic}: {Statement}";
    }

    /// <summary>
    /// How close an implied fund value must be to a real one before they are called the same.
    ///
    /// A percentage and a monetary amount quoted for the same charge imply a fund value, and
    /// when that implied value does not match the arrangement the row is about, the row has been
    /// computed on the wrong plan — which is invisible unless someone divides.
    ///
    /// Tightened from 2% after Run 3 reported an implied £115,195.45 as "the total of all
    /// arrangements", which is £116,998.47 — 1.57% away, inside the old tolerance and not the
    /// same number. A charge divided by its own rate lands on its base to within a rounding, so
    /// the honest tolerance is small: the real case, £186.19 at 0.18%, implies £103,438.89
    /// against Zurich's £103,439.24, which is 0.0003% out.
    ///
    /// A false attribution is worse here than none. "Matches no arrangement" tells a reader to
    /// look; naming the wrong plan tells them not to.
    /// </summary>
    private const double ImpliedBaseTolerance = 0.005;

    public static IReadOnlyList<Figure> From(string modelJson)
    {
        JsonObject? root;

        try
        {
            root = JsonNode.Parse(modelJson) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            // A model that will not parse is a problem for the code that stored it, not a
            // reason to fail the run here. The assessor simply gets no computed figures.
            return [];
        }

        if (root is null)
        {
            return [];
        }

        var figures = new List<Figure>();

        AddArrangementTotals(root, figures);
        AddImpliedChargeBases(root, figures);
        AddRepeatedChargeValues(root, figures);
        AddIncomeFrequencies(root, figures);
        AddSwitchingConsistency(root, figures);
        AddAnnualisedReturns(root, figures);

        return figures;
    }

    /// <summary>
    /// Renders the figures for a prompt, framed so the assessor knows they are computed rather
    /// than asserted — the distinction decides whether a mismatch is the report's error or the
    /// arithmetic's.
    /// </summary>
    public static string Format(IReadOnlyList<Figure> figures)
    {
        if (figures.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        sb.AppendLine("## Figures computed from the canonical model");
        sb.AppendLine(
            "These were calculated from the report's own extracted values, not asserted by the "
            + "report and not retrieved from any document. Where one contradicts a figure the "
            + "report states, the report is what is in question. Do not recompute them.");
        sb.AppendLine();

        foreach (var group in figures.GroupBy(f => f.Topic))
        {
            sb.AppendLine($"**{group.Key}**");

            foreach (var figure in group)
            {
                sb.AppendLine($"- {figure.Statement}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// What the existing arrangements actually sum to. A report quoting a total anywhere can
    /// then be checked against it rather than taken on trust.
    /// </summary>
    private static void AddArrangementTotals(JsonObject root, List<Figure> figures)
    {
        if (root[CanonicalModel.ExistingArrangements] is not JsonArray arrangements || arrangements.Count == 0)
        {
            return;
        }

        var current = new List<(string Name, double Value)>();
        var transfer = new List<(string Name, double Value)>();

        foreach (var node in arrangements.OfType<JsonObject>())
        {
            var name = Text(node[CanonicalModel.Provider]) ?? Text(node[CanonicalModel.ProductName]) ?? Text(node[CanonicalModel.ArrangementId]) ?? "unnamed";

            if (MoneyOf(node[CanonicalModel.CurrentValue]) is { } c)
            {
                current.Add((name, c));
            }

            if (MoneyOf(node[CanonicalModel.TransferValue]) is { } t)
            {
                transfer.Add((name, t));
            }
        }

        if (current.Count > 1)
        {
            figures.Add(new Figure(
                "Existing arrangements",
                $"The {current.Count} arrangements with a current value sum to "
                + $"{Money(current.Sum(a => a.Value))} "
                + $"({string.Join(" + ", current.Select(a => $"{a.Name} {Money(a.Value)}"))})."));
        }

        if (transfer.Count > 1)
        {
            var sum = transfer.Sum(a => a.Value);

            figures.Add(new Figure(
                "Existing arrangements",
                $"The {transfer.Count} arrangements with a transfer value sum to {Money(sum)}."));

            // The distinction that makes a stated total wrong: what is being moved is not what
            // is held, and a report quoting one as the other understates the client's position.
            if (current.Count > transfer.Count)
            {
                figures.Add(new Figure(
                    "Existing arrangements",
                    $"{current.Count - transfer.Count} arrangement(s) have a current value but no "
                    + "transfer value, so the total held and the total being transferred are "
                    + "different figures."));
            }
        }
    }

    /// <summary>
    /// For every charge line quoting both a percentage and an amount, the fund value that pair
    /// implies — and, where the line belongs to a named arrangement, whether it matches that
    /// arrangement's own value.
    /// </summary>
    private static void AddImpliedChargeBases(JsonObject root, List<Figure> figures)
    {
        var arrangements = (root[CanonicalModel.ExistingArrangements] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(a => (
                Name: Text(a[CanonicalModel.Provider]) ?? Text(a[CanonicalModel.ProductName]) ?? Text(a[CanonicalModel.ArrangementId]) ?? "unnamed",
                Value: MoneyOf(a[CanonicalModel.CurrentValue])))
            .Where(a => a.Value is not null)
            .ToList();

        // The two totals a charge can legitimately be computed on, alongside any single plan.
        // Reporting only "matches no arrangement" was true and useless on a real run: the
        // platform charge implied £110,185.71, which is exactly the amount being transferred.
        var totals = new List<(string Name, double Value)>();

        if (arrangements.Count > 1)
        {
            totals.Add(("the total of all arrangements", arrangements.Sum(a => a.Value!.Value)));
        }

        var transferred = (root[CanonicalModel.ExistingArrangements] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(a => MoneyOf(a[CanonicalModel.TransferValue]))
            .Where(v => v is > 0)
            .ToList();

        if (transferred.Count > 0)
        {
            totals.Add(("the total being transferred", transferred.Sum(v => v!.Value)));
        }

        // The arrangement each charge line is *labelled* for, so a row computed on a different
        // plan can be named as such rather than left to the reader to infer.
        var byId = (root[CanonicalModel.ExistingArrangements] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(a => (
                Id: Text(a[CanonicalModel.ArrangementId]),
                Name: Text(a[CanonicalModel.Provider]) ?? Text(a[CanonicalModel.ProductName]),
                Value: MoneyOf(a[CanonicalModel.CurrentValue])))
            .Where(a => !string.IsNullOrWhiteSpace(a.Id))
            .ToList();

        foreach (var (described, percentage, amount) in ChargePairs(root))
        {
            var implied = amount / (percentage / 100);

            var matched = arrangements
                .FirstOrDefault(a => Math.Abs(a.Value!.Value - implied) <= implied * ImpliedBaseTolerance);

            var match = matched.Name is not null
                ? $"{matched.Name}'s current value"
                : totals
                    .Where(t => Math.Abs(t.Value - implied) <= implied * ImpliedBaseTolerance)
                    .Select(t => t.Name)
                    .FirstOrDefault();

            figures.Add(new Figure(
                "Charge arithmetic",
                $"{described}: {Money(amount)} at {percentage:0.###}% implies a fund "
                + $"value of {Money(implied)}"
                + (match is null
                    ? ", which matches no arrangement value and neither total."
                    : $", which is {match}.")
                + WrongPlan(described, matched.Name, byId)));
        }
    }

    /// <summary>
    /// Where one arrangement's ongoing charge is recorded at two different percentages.
    ///
    /// A report can state the same plan's charge twice — once in a table of existing
    /// arrangements and once in a switching comparison — and give different answers. On the
    /// case this was written against, Zurich appears at 0.18% on one page and 0.93% on another,
    /// and Standard Life at 0.52% and 0.18%. Both cost comparisons drive the recommendation,
    /// and neither the assessor nor the extraction noticed, because seeing it requires holding
    /// two pages together and comparing a repeated key rather than reading either page.
    ///
    /// The model already holds both. Comparing them is arithmetic, not judgement.
    /// </summary>
    private static void AddRepeatedChargeValues(JsonObject root, List<Figure> figures)
    {
        var byArrangement = new Dictionary<string, List<(double Percentage, string Where)>>(
            StringComparer.OrdinalIgnoreCase);

        void Record(string? name, double? percentage, string where)
        {
            if (name is null || percentage is not > 0)
            {
                return;
            }

            if (!byArrangement.TryGetValue(name, out var seen))
            {
                byArrangement[name] = seen = [];
            }

            seen.Add((percentage.Value, where));
        }

        foreach (var node in (root[CanonicalModel.ExistingArrangements] as JsonArray ?? []).OfType<JsonObject>())
        {
            var name = Text(node[CanonicalModel.Provider]) ?? Text(node[CanonicalModel.ProductName]);
            var charges = node[CanonicalModel.Charges];

            Record(name, Number(charges?[CanonicalModel.TotalOngoingPercentage]), "the existing arrangements table");

            foreach (var line in (charges?[CanonicalModel.Lines] as JsonArray ?? []).OfType<JsonObject>())
            {
                Record(name, Number(Percent(line)), "the existing arrangements table");
            }
        }

        foreach (var (scope, line) in ChargeLines(root))
        {
            Record(scope, Number(Percent(line)), "the charges comparison");
        }

        foreach (var (name, values) in byArrangement)
        {
            var distinct = values
                .GroupBy(v => Math.Round(v.Percentage, 4))
                .Select(g => (Percentage: g.Key, g.First().Where))
                .OrderBy(v => v.Percentage)
                .ToList();

            if (distinct.Count < 2 || distinct.Select(v => v.Where).Distinct().Count() < 2)
            {
                continue;
            }

            figures.Add(new Figure(
                "Charge consistency",
                $"{name}'s ongoing charge is recorded at "
                + string.Join(" and ", distinct.Select(v => $"{v.Percentage:0.###}% in {v.Where}"))
                + ". These describe the same plan and do not agree."));
        }
    }

    /// <summary>
    /// Every income amount restated monthly and annually, with the frequency it was recorded
    /// under. A weekly figure read as monthly overstates income roughly fourfold, and the
    /// affordability case for a whole report can rest on it.
    /// </summary>
    private static void AddIncomeFrequencies(JsonObject root, List<Figure> figures)
    {
        if (root[CanonicalModel.FinancialPosition]?[CanonicalModel.Income] is not JsonArray income)
        {
            return;
        }

        foreach (var item in income.OfType<JsonObject>())
        {
            var amount = MoneyOf(item[CanonicalModel.Net]) ?? MoneyOf(item[CanonicalModel.Gross]);
            var frequency = Text(item[CanonicalModel.Frequency]);

            if (amount is not > 0 || frequency is null)
            {
                continue;
            }

            var perYear = TimesPerYear(frequency);

            if (perYear is null)
            {
                continue;
            }

            var annual = amount.Value * perYear.Value;
            var described = Text(item[CanonicalModel.Description]) ?? Text(item[CanonicalModel.Category]) ?? "income";

            figures.Add(new Figure(
                "Income restated",
                $"{described}: {Money(amount.Value)} {frequency.ToLowerInvariant()} is "
                + $"{Money(annual / 12)} monthly, {Money(annual)} a year."));
        }
    }

    /// <summary>
    /// Every charge line in the model, from both places one can live.
    ///
    /// Reading only <c>costsAndCharges</c> was the defect that let the benchmark's
    /// wrong-fund charge escape: on a real model that section held the recommended plan's
    /// lines and nothing else, so the existing arrangements' own charge records — where a row
    /// computed against the wrong plan actually sits — were never divided. The scope is the
    /// arrangement's name, so the resulting figure names the plan a reader can check.
    /// </summary>
    /// <summary>
    /// Every percentage-and-amount pair in the model that describes one charge, wherever it is
    /// written.
    ///
    /// Three shapes, because the extraction has used three and the component chased two of them
    /// across two stages and missed the figure both times. A charge can arrive as a line in
    /// <c>existingArrangements[].charges.lines[]</c>, as a line in
    /// <c>costsAndCharges.*.lines[]</c>, or — the one that kept escaping — as flat sibling
    /// properties on some other object entirely:
    ///
    /// <code>
    ///   { "arrangementId": "EA5",
    ///     "existingAnnualChargePercentage": { "value": 0.18 },
    ///     "existingAnnualChargeAmount":     { "amount": 186.19 } }
    /// </code>
    ///
    /// Rather than add a fourth named path the next time the shape moves, the last case is found
    /// structurally: any object holding a <c>…Percentage</c> and a <c>…Amount</c> under the same
    /// prefix is describing one charge, whatever the surrounding schema calls it.
    /// </summary>
    /// <summary>
    /// Says so when a charge row labelled for one arrangement is computed on another's fund.
    ///
    /// <b>Stating the arithmetic was not enough, and a measured run proved it.</b> Every one of
    /// the eighty-five group prompts of 2026-08-26 already carried
    /// <c>"EA5 existing annual charge: £186.19 at 0.18% implies a fund value of £103,438.89, which
    /// is Zurich Managed Pn AP's current value"</c> — EA5 being Standard Life, whose own fund is
    /// £3,002. One check in ten drew the conclusion. The other two that needed it read the same
    /// charge table at face value and reported the switch as a saving, because the sentence
    /// reported a coincidence and left the significance to the reader.
    ///
    /// So the significance is now part of the sentence. This is not the assessor's arithmetic to
    /// redo — the numbers are already decided here — and a fact whose consequence is left implicit
    /// is a fact that gets used by whichever model happens to be sharp enough that day, which is
    /// the opposite of what deterministic pre-computation is for.
    ///
    /// Deliberately narrow: it fires only where the label carries an arrangement id, that id is
    /// known, and the implied base is a *different* arrangement. A row that matches a total, or
    /// matches nothing, or matches its own plan, is left exactly as it was.
    /// </summary>
    private static string WrongPlan(
        string described,
        string? impliedName,
        IReadOnlyList<(string? Id, string? Name, double? Value)> byId)
    {
        if (impliedName is null)
        {
            return string.Empty;
        }

        var owner = byId.FirstOrDefault(a =>
            a.Id is not null
            && described.Contains(a.Id, StringComparison.OrdinalIgnoreCase));

        if (owner.Id is null
            || owner.Name is null
            || string.Equals(owner.Name, impliedName, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return $" That is a different arrangement from the {owner.Id} this row is labelled for"
             + (owner.Value is { } own ? $", whose own current value is {Money(own)}" : string.Empty)
             + ", so the row is computed on the wrong plan and any comparison resting on it is "
             + "unreliable.";
    }

    /// <summary>
    /// A charge line's percentage node, tolerating the scalar form.
    ///
    /// <b>The indexer throws on a JsonValue, and that took the whole run down.</b> The schema
    /// models a percentage as an object with a <c>value</c>, and an extraction pass that writes
    /// <c>"percentage": 0.18</c> instead is not malformed enough for anything upstream to reject —
    /// it is a number where an object was expected, which every reader tolerates until one asks it
    /// for a property. Reaching through it with <c>?[]</c> raised "the node must be of type
    /// JsonObject" out of the derived-figures pass, which runs before any model call, so a single
    /// scalar would have failed every check in the run at once.
    /// </summary>
    private static JsonNode? Percent(JsonObject line) =>
        line[CanonicalModel.Percentage] switch
        {
            JsonObject o => o[CanonicalModel.Value],
            JsonValue v => v,
            _ => null,
        };

    private static IEnumerable<(string Described, double Percentage, double Amount)> ChargePairs(
        JsonObject root)
    {
        foreach (var (scope, line) in ChargeLines(root))
        {
            var percentage = PercentageOf(line[CanonicalModel.Percentage]);
            var amount = MoneyOf(line[CanonicalModel.Amount]);

            if (percentage is > 0 && amount is > 0)
            {
                yield return (
                    Text(line[CanonicalModel.Description]) ?? Text(line[CanonicalModel.ChargeType]) ?? scope,
                    percentage.Value,
                    amount.Value);
            }
        }

        foreach (var pair in SiblingChargePairs(root))
        {
            yield return pair;
        }
    }

    /// <summary>
    /// Walks the model for objects carrying <c>…Percentage</c> and <c>…Amount</c> siblings under
    /// a shared prefix, and names each by whatever identifies the object it sits on.
    /// </summary>
    private static IEnumerable<(string Described, double Percentage, double Amount)> SiblingChargePairs(
        JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var pair in array.SelectMany(SiblingChargePairs))
            {
                yield return pair;
            }

            yield break;
        }

        if (node is not JsonObject obj)
        {
            yield break;
        }

        var owner = Text(obj[CanonicalModel.Provider])
                    ?? Text(obj[CanonicalModel.ProductName])
                    ?? Text(obj[CanonicalModel.ArrangementId])
                    ?? Text(obj[CanonicalModel.Scope]);

        foreach (var property in obj.ToList())
        {
            if (!property.Key.EndsWith(CanonicalModel.PercentageSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var prefix = property.Key[..^CanonicalModel.PercentageSuffix.Length];
            var percentage = PercentageOf(property.Value);
            var amount = MoneyOf(obj[prefix + CanonicalModel.AmountSuffix]);

            if (percentage is > 0 && amount is > 0)
            {
                var label = Humanise(prefix);

                yield return (
                    owner is null ? label : $"{owner} {label}",
                    percentage.Value,
                    amount.Value);
            }
        }

        foreach (var pair in obj.Select(p => p.Value).SelectMany(SiblingChargePairs))
        {
            yield return pair;
        }
    }

    /// <summary>Turns "existingAnnualCharge" into "existing annual charge", for a readable line.</summary>
    private static string Humanise(string camelCase)
    {
        var sb = new StringBuilder();

        foreach (var c in camelCase)
        {
            if (char.IsUpper(c) && sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.Length == 0 ? "charge" : sb.ToString();
    }

    private static IEnumerable<(string Scope, JsonObject Line)> ChargeLines(JsonObject root)
    {
        foreach (var arrangement in (root[CanonicalModel.ExistingArrangements] as JsonArray ?? []).OfType<JsonObject>())
        {
            var scope = Text(arrangement[CanonicalModel.Provider])
                        ?? Text(arrangement[CanonicalModel.ProductName])
                        ?? Text(arrangement[CanonicalModel.ArrangementId])
                        ?? "an existing arrangement";

            foreach (var line in (arrangement[CanonicalModel.Charges]?[CanonicalModel.Lines] as JsonArray ?? []).OfType<JsonObject>())
            {
                yield return (scope, line);
            }
        }

        if (root[CanonicalModel.CostsAndCharges] is not JsonObject costs)
        {
            yield break;
        }

        foreach (var key in new[] { "existing", "recommended" })
        {
            var sets = costs[key] switch
            {
                JsonArray array => array.OfType<JsonObject>().ToList(),
                JsonObject single => [single],
                _ => [],
            };

            foreach (var set in sets)
            {
                var scope = Text(set[CanonicalModel.Scope]) ?? key;

                foreach (var line in (set[CanonicalModel.Lines] as JsonArray ?? []).OfType<JsonObject>())
                {
                    yield return (scope, line);
                }
            }
        }
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// Whether a switch's stated cost and its stated outcome agree with each other.
    ///
    /// <b>This is the one finding a prompt has been asked for four times and never produced.</b>
    /// The shape: a report states that the recommended option costs more per year, and states in
    /// another table that switching makes no difference to the projected outcome — or improves it.
    /// Both cannot be true on one basis. A charge borne every year has to show up in a
    /// reduction-in-yield and in a projection, and if it does not, an assumption has been applied
    /// to one side of the comparison and not the other.
    ///
    /// Four runs on two models were given the decisive evidence and did not ask the question. One
    /// of them quoted the sentence that proves it — a supporting document recording that the
    /// existing plan bears no adviser charge — and used it to reassure itself that the ceding plan
    /// was clean. The prompt clause telling the assessor to ask what assumption produced an
    /// identical result was printed in that group's own instructions both times.
    ///
    /// So it is computed here instead, from values the extraction already holds, and handed over
    /// settled. Nothing about this is specific to a case: every replacement, switch or transfer
    /// recommendation states a cost difference and an outcome difference, and the two are either
    /// consistent or they are a finding.
    /// </summary>
    /// <remarks>
    /// Reported as a question rather than a verdict. The honest conclusion from arithmetic alone
    /// is that the two statements do not sit together, not which of them is wrong — the
    /// projections may legitimately be on a different basis, and where the report says so this is
    /// no longer a finding. What the assessor must not do is fail to notice.
    /// </remarks>
    private static void AddSwitchingConsistency(JsonObject root, List<Figure> figures)
    {
        if (root[CanonicalModel.CostsAndCharges] is not JsonObject costs)
        {
            return;
        }

        var difference = PercentageOf(
            (costs[CanonicalModel.Comparison] as JsonObject)?[CanonicalModel.DifferencePercentage]);

        if (difference is not { } chargeGap || Math.Abs(chargeGap) < ChargeGapWorthChecking)
        {
            return;
        }

        var dearer = (costs[CanonicalModel.Comparison] as JsonObject)?[CanonicalModel.Direction];
        var recommendedCostsMore = chargeGap > 0
            || string.Equals(Text(dearer), "RecommendedMoreExpensive", StringComparison.OrdinalIgnoreCase);

        if (!recommendedCostsMore)
        {
            return;
        }

        var stated = $"The report states the recommended option costs {chargeGap:0.##}% a year more "
                   + "than the existing one";

        // The reduction in yield is the charge difference expressed as an outcome, so the two are
        // the same quantity twice. Matched by growth basis: comparing a low-growth existing figure
        // against a high-growth recommended one is not a comparison.
        foreach (var (basis, existing, recommended) in YieldPairs(costs))
        {
            var outcomeGap = recommended - existing;

            if (outcomeGap >= chargeGap - YieldTolerance)
            {
                continue;
            }

            figures.Add(new Figure(
                "Switching cost against switching outcome",
                $"{stated}, and reports a reduction in yield of {existing:0.##}% for the existing "
                + $"plan against {recommended:0.##}% for the recommended one at {basis} growth. "
                + $"A charge {chargeGap:0.##}% higher cannot produce a reduction in yield "
                + $"{(Math.Abs(outcomeGap) < YieldTolerance ? "identical to" : $"{Math.Abs(outcomeGap):0.##}% lower than")} "
                + "the plan it replaces on the same basis. Either the two comparisons rest on "
                + "different assumptions, or something has been applied to one side and not the "
                + "other. The report should say which."));
        }

        // The same test against the projected outcome, where the report states one.
        foreach (var (basis, amount, percentage) in SwitchingEffects(costs))
        {
            if (amount is { } money && money < 0)
            {
                continue;
            }

            if (percentage is { } pct && pct < 0)
            {
                continue;
            }

            figures.Add(new Figure(
                "Switching cost against switching outcome",
                $"{stated}, and states the effect of switching at {basis} growth as "
                + $"{(amount is { } a ? Money(a) : "no monetary change")}"
                + $"{(percentage is { } p ? $" / {p:0.##}%" : string.Empty)}. "
                + "A recommendation that costs more every year and changes the projected outcome "
                + "by nothing is two statements on two bases. The report does not reconcile them."));
        }
    }

    /// <summary>
    /// Reduction-in-yield figures paired by growth basis, existing against recommended.
    ///
    /// Keyed on the basis rather than on position in the array, because the two subjects are
    /// recorded as separate entries and nothing guarantees their order — and a low-growth figure
    /// compared against a high-growth one manufactures a difference that is not there.
    /// </summary>
    private static IEnumerable<(string Basis, double Existing, double Recommended)> YieldPairs(
        JsonObject costs)
    {
        if (costs[CanonicalModel.ReductionInYield] is not JsonArray entries)
        {
            yield break;
        }

        var byBasis = new Dictionary<string, (double? Existing, double? Recommended)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries.OfType<JsonObject>())
        {
            var basis = Text(entry[CanonicalModel.GrowthRateBasis]);
            var subject = Text(entry[CanonicalModel.Subject]);
            var value = PercentageOf(entry[CanonicalModel.RiyPercentage]);

            if (basis is null || subject is null || value is null)
            {
                continue;
            }

            byBasis.TryGetValue(basis, out var pair);

            byBasis[basis] = subject.Contains("Recommend", StringComparison.OrdinalIgnoreCase)
                ? (pair.Existing, value)
                : (value, pair.Recommended);
        }

        foreach (var (basis, pair) in byBasis.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (pair is { Existing: { } existing, Recommended: { } recommended })
            {
                yield return (basis, existing, recommended);
            }
        }
    }

    /// <summary>The stated effect of switching, by growth basis, where the report gives one.</summary>
    private static IEnumerable<(string Basis, double? Amount, double? Percentage)> SwitchingEffects(
        JsonObject costs)
    {
        if (costs[CanonicalModel.ReductionInYield] is not JsonArray entries)
        {
            yield break;
        }

        foreach (var entry in entries.OfType<JsonObject>())
        {
            var amount = MoneyOf(entry[CanonicalModel.EffectOfSwitchingAmount]);
            var percentage = PercentageOf(entry[CanonicalModel.EffectOfSwitchingPercentage]);

            if (amount is null && percentage is null)
            {
                continue;
            }

            // Only where the report claims switching is free or better. A stated cost is the
            // report doing its job, and reporting it back would be noise.
            if ((amount ?? 0) != 0 || (percentage ?? 0) != 0)
            {
                continue;
            }

            if (Text(entry[CanonicalModel.GrowthRateBasis]) is { } basis)
            {
                yield return (basis, amount, percentage);
            }
        }
    }

    /// <summary>
    /// Below this, a charge difference is a rounding and the comparison is not worth making.
    /// </summary>
    private const double ChargeGapWorthChecking = 0.01;

    /// <summary>
    /// How far a reduction in yield may fall short of the charge difference before the two are
    /// called inconsistent. Generous, because the two are computed over different horizons and a
    /// charge does not translate into yield one-for-one — the finding is a gap of the whole
    /// difference, not a rounding.
    /// </summary>
    private const double YieldTolerance = 0.02;

    /// <summary>
    /// A cumulative return over a period, restated as the compound annual equivalent.
    ///
    /// <b>Two failures, one calculation.</b> A report quoting a five-year cumulative return and an
    /// "average" beside it has almost always divided rather than compounded, and the difference
    /// runs in the flattering direction. And a fund return quoted against a deposit rate is being
    /// compared gross against net unless somebody subtracts the charges — which no run has done in
    /// four attempts, on either model, while every one of them restated the report's own
    /// comparison approvingly.
    ///
    /// Both need the same number and neither assessor produced it, so it is computed here: the
    /// annualised equivalent, and where a total charge for the same solution is recorded, the
    /// annualised figure net of it.
    ///
    /// Generic by construction. Every case in this domain compares an investment's past
    /// performance with something, and a cumulative figure over a stated period is arithmetic.
    /// </summary>
    private static void AddAnnualisedReturns(JsonObject root, List<Figure> figures)
    {
        foreach (var (name, role, period, cumulative, years) in Performances(root))
        {
            var annualised = (Math.Pow(1 + (cumulative / 100), 1.0 / years) - 1) * 100;
            var simpleMean = cumulative / years;

            var statement =
                $"{name}{(role is null ? string.Empty : $" ({role})")} returned {cumulative:0.##}% "
                + $"cumulatively over {period}. The compound annual equivalent is "
                + $"**{annualised:0.##}%**; dividing by {years:0.#} instead gives {simpleMean:0.##}%, "
                + "which overstates it. A figure quoted as an average return should be the first.";

            figures.Add(new Figure("Annualised return", statement));
        }
    }

    /// <summary>
    /// Performance entries anywhere in the model that carry a cumulative return over a period
    /// long enough for compounding to matter.
    ///
    /// The period is read from the recorded span rather than assumed, and an entry whose span
    /// cannot be established is skipped — an annualisation over a guessed number of years is a
    /// confident wrong number, which is the failure mode this class exists to avoid.
    /// </summary>
    private static IEnumerable<(string Name, string? Role, string Period, double Cumulative, double Years)>
        Performances(JsonNode? node)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var found in array.SelectMany(Performances))
                {
                    yield return found;
                }

                break;

            case JsonObject obj:
                if (PercentageOf(obj[CanonicalModel.CumulativeReturnPercentage]) is { } cumulative
                    && Text(obj[CanonicalModel.Period]) is { } period
                    && YearsIn(period) is { } years
                    && years >= 2)
                {
                    yield return (
                        Text(obj[CanonicalModel.InstrumentName]) ?? "An instrument",
                        Text(obj[CanonicalModel.Role]),
                        period,
                        cumulative,
                        years);
                }

                foreach (var found in obj.Select(p => p.Value).SelectMany(Performances))
                {
                    yield return found;
                }

                break;
        }
    }

    /// <summary>
    /// The span of a recorded period in years, or null where it cannot be read.
    ///
    /// Two spellings occur and both are handled: an explicit range of two dates, and a plain count
    /// of years. Anything else returns null rather than a guess.
    /// </summary>
    internal static double? YearsIn(string period)
    {
        var dates = System.Text.RegularExpressions.Regex
            .Matches(period, @"(\d{2})[/-](\d{2})[/-](\d{4})|(\d{4})-(\d{2})-(\d{2})")
            .Select(m => m.Groups[3].Success
                ? new DateTime(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value))
                : new DateTime(int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value), int.Parse(m.Groups[6].Value)))
            .OrderBy(d => d)
            .ToList();

        if (dates.Count >= 2)
        {
            var span = (dates[^1] - dates[0]).TotalDays / 365.25;

            return span >= 1 ? Math.Round(span, 1) : null;
        }

        var years = System.Text.RegularExpressions.Regex.Match(period, @"(\d+)\s*[- ]?\s*year");

        return years.Success && double.TryParse(years.Groups[1].Value, out var count) ? count : null;
    }

    /// <summary>
    /// Weeks and fortnights are converted on the calendar year, not on four-week months —
    /// 52 weeks is 12 months, and treating a weekly figure as four-weekly loses a month a year.
    /// </summary>
    private static double? TimesPerYear(string frequency) => frequency.ToLowerInvariant() switch
    {
        "weekly" => 52,
        "fortnightly" => 26,
        "fourweekly" => 13,
        "monthly" => 12,
        "quarterly" => 4,
        "halfyearly" => 2,
        "annually" => 1,
        _ => null,
    };

    /// <summary>
    /// A money value, whether written as an object with an <c>amount</c> or as a bare number.
    /// Indexing a scalar node throws, so the shape is checked rather than assumed — the model has
    /// used both spellings for the same idea.
    /// </summary>
    private static double? MoneyOf(JsonNode? node) =>
        node is JsonObject obj ? Number(obj[CanonicalModel.Amount]) : Number(node);

    /// <summary>A percentage, written either as an object with a <c>value</c> or as a bare number.</summary>
    private static double? PercentageOf(JsonNode? node) =>
        node is JsonObject obj ? Number(obj[CanonicalModel.Value]) : Number(node);

    private static double? Number(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        try
        {
            return node.GetValue<double>();
        }
        catch (Exception)
        {
            return double.TryParse(
                node.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }
    }

    private static string? Text(JsonNode? node)
    {
        var value = node?.ToString().Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string Money(double value) =>
        "£" + value.ToString("N2", CultureInfo.InvariantCulture);
}
