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

        foreach (var (described, percentage, amount) in ChargePairs(root))
        {
            var implied = amount / (percentage / 100);

            var match = arrangements
                .Where(a => Math.Abs(a.Value!.Value - implied) <= implied * ImpliedBaseTolerance)
                .Select(a => $"{a.Name}'s current value")
                .Concat(totals
                    .Where(t => Math.Abs(t.Value - implied) <= implied * ImpliedBaseTolerance)
                    .Select(t => t.Name))
                .FirstOrDefault();

            figures.Add(new Figure(
                "Charge arithmetic",
                $"{described}: {Money(amount)} at {percentage:0.###}% implies a fund "
                + $"value of {Money(implied)}"
                + (match is null
                    ? ", which matches no arrangement value and neither total."
                    : $", which is {match}.")));
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
                Record(name, Number(line[CanonicalModel.Percentage]?[CanonicalModel.Value]), "the existing arrangements table");
            }
        }

        foreach (var (scope, line) in ChargeLines(root))
        {
            Record(scope, Number(line[CanonicalModel.Percentage]?[CanonicalModel.Value]), "the charges comparison");
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
