using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AiPromptEvaluator;

/// <summary>
/// The numeric half of a <c>ValueMatch</c> or <c>RangeMatch</c> comparison, done in code.
///
/// <c>comparison.method</c> looked like an enum with behaviour behind it and had none:
/// <c>ValueMatch</c> reached the assessor as the word "ValueMatch" and the actual comparing —
/// does the figure the report states appear anywhere in the evidence — was left to a model
/// reading a dozen passages of prose.
///
/// That is the same delegation <see cref="DerivedFigures"/> exists to undo, and it fails the
/// same two ways: a figure present in a passage but not noticed reads as an evidence gap, and
/// a figure absent from every passage gets "confirmed" from a number that resembles it. Both
/// are decidable without a model, so both are decided here.
///
/// What this does <b>not</b> do is reach a verdict. A matched figure is not a passed check —
/// the right number in the wrong document, or as at the wrong date, is still a finding, and
/// the plan's <c>falsePositiveGuards</c> exist precisely for those. This narrows the assessor's
/// job to judgement by taking the arithmetic off it.
/// </summary>
public static class NumericComparison
{
    /// <summary>
    /// How close two figures must be before <c>RangeMatch</c> calls them the same.
    ///
    /// A plan's own <c>tolerance</c> is prose written for the assessor ("convert to a common
    /// frequency first", "agreement within £1 per month"), and parsing it into a number would
    /// be guessing at what the author meant. One published tolerance is used instead, and the
    /// prompt says what it is, so a reader can disagree with a specific figure rather than an
    /// unstated one.
    /// </summary>
    public const decimal RangeTolerance = 0.01m;

    /// <summary>What kind of quantity a number is, so like is only compared with like.</summary>
    public enum FigureKind
    {
        /// <summary>Carried a currency symbol, or was written with a thousands separator.</summary>
        Money,

        /// <summary>Carried a percent sign.</summary>
        Percentage,

        /// <summary>Everything else: ages, terms, counts, scale positions.</summary>
        Plain,
    }

    /// <summary>One number found in a piece of text, with the way it was written kept.</summary>
    public sealed record Figure(decimal Value, FigureKind Kind, string AsWritten)
    {
        public override string ToString() => AsWritten;
    }

    /// <summary>What became of one figure the report asserts.</summary>
    public sealed record Corroboration(
        Figure Asserted,
        Figure? Nearest,
        string? PassageId,
        bool IsMatch);

    // A currency amount, a percentage, or a bare number. Ordered so the longest form wins.
    private static readonly Regex FigurePattern = new(
        @"(?<money>[£$€]\s?-?\d[\d,]*(?:\.\d+)?)"
        + @"|(?<percent>-?\d[\d,]*(?:\.\d+)?\s?%)"
        + @"|(?<plain>-?\d[\d,]*(?:\.\d+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Policy numbers, sort codes, dates and NI numbers are digit runs that mean nothing as
    // quantities. A number touching a letter or a slash on either side is one of those.
    private static readonly Regex Adjacent = new(
        @"[A-Za-z/\\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Every quantity in a piece of text, de-duplicated on value and kind.
    ///
    /// Deliberately conservative: a number that abuts a letter or a slash is skipped, because
    /// policy numbers, NI numbers and dates are digit runs that are not quantities, and a
    /// spurious match is worse here than a miss — it would tell the assessor a figure was
    /// corroborated when nothing corroborated it.
    /// </summary>
    public static IReadOnlyList<Figure> Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var found = new List<Figure>();
        var seen = new HashSet<(decimal, FigureKind)>();

        foreach (Match m in FigurePattern.Matches(text))
        {
            if (TouchesLetters(text, m))
            {
                continue;
            }

            var kind =
                m.Groups["money"].Success ? FigureKind.Money
                : m.Groups["percent"].Success ? FigureKind.Percentage
                : FigureKind.Plain;

            var raw = m.Value.Trim();
            var digits = raw.Trim('£', '$', '€', '%', ' ').Replace(",", string.Empty);

            if (!decimal.TryParse(digits, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            // A plain number written with a thousands separator is money in all but the symbol,
            // and reports drop the symbol constantly once a column header carries it.
            if (kind == FigureKind.Plain && raw.Contains(','))
            {
                kind = FigureKind.Money;
            }

            if (seen.Add((value, kind)))
            {
                found.Add(new Figure(value, kind, raw));
            }
        }

        return found;
    }

    private static bool TouchesLetters(string text, Match m)
    {
        var before = m.Index > 0 ? text[m.Index - 1].ToString() : string.Empty;
        var afterIndex = m.Index + m.Length;
        var after = afterIndex < text.Length ? text[afterIndex].ToString() : string.Empty;

        return Adjacent.IsMatch(before) || Adjacent.IsMatch(after);
    }

    /// <summary>
    /// Whether two figures of the same kind agree, under the method the plan named.
    /// <c>ValueMatch</c> is exact; <c>RangeMatch</c> allows <see cref="RangeTolerance"/>.
    /// </summary>
    public static bool Agree(decimal asserted, decimal candidate, bool allowTolerance)
    {
        if (asserted == candidate)
        {
            return true;
        }

        if (!allowTolerance || asserted == 0m)
        {
            return false;
        }

        return Math.Abs((candidate - asserted) / asserted) <= RangeTolerance;
    }

    /// <summary>
    /// Checks each asserted figure against the evidence, keeping the closest candidate of the
    /// same kind so a near miss can be named rather than merely reported absent. A near miss is
    /// usually the finding — £110,000 stated against £116,997.47 on file is the discrepancy,
    /// and "no corroborating figure" would bury it.
    /// </summary>
    public static IReadOnlyList<Corroboration> Compare(
        IEnumerable<Figure> asserted,
        IReadOnlyDictionary<string, string> passagesById,
        bool allowTolerance)
    {
        var candidates = passagesById
            .SelectMany(p => Extract(p.Value).Select(f => (PassageId: p.Key, Figure: f)))
            .ToList();

        var results = new List<Corroboration>();

        foreach (var figure in asserted)
        {
            var sameKind = candidates.Where(c => c.Figure.Kind == figure.Kind).ToList();

            if (sameKind.Count == 0)
            {
                results.Add(new Corroboration(figure, null, null, false));
                continue;
            }

            var (passageId, nearest) = sameKind
                .OrderBy(c => Math.Abs(c.Figure.Value - figure.Value))
                .ThenBy(c => c.PassageId, StringComparer.Ordinal)
                .First();

            results.Add(new Corroboration(
                figure, nearest, passageId, Agree(figure.Value, nearest.Value, allowTolerance)));
        }

        return results;
    }

    /// <summary>Whether a plan's comparison method is one this can act on.</summary>
    public static bool Handles(string? method) =>
        string.Equals(method, "ValueMatch", StringComparison.OrdinalIgnoreCase)
        || string.Equals(method, "RangeMatch", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The block handed to the assessor, or null where there is nothing decidable to say —
    /// no figures asserted, or a method this does not handle.
    /// </summary>
    public static string? Format(
        string? method,
        IEnumerable<string> assertionTexts,
        IReadOnlyDictionary<string, string> passagesById)
    {
        if (!Handles(method))
        {
            return null;
        }

        var allowTolerance = string.Equals(method, "RangeMatch", StringComparison.OrdinalIgnoreCase);

        var asserted = assertionTexts
            .SelectMany(Extract)
            .GroupBy(f => (f.Value, f.Kind))
            .Select(g => g.First())
            .ToList();

        if (asserted.Count == 0 || passagesById.Count == 0)
        {
            return null;
        }

        var compared = Compare(asserted, passagesById, allowTolerance);
        var sb = new StringBuilder();

        sb.AppendLine("#### Figures checked in code");
        sb.AppendLine(
            allowTolerance
                ? $"Method {method}: figures agree within {RangeTolerance:P0} of the asserted value."
                : $"Method {method}: figures must agree exactly.");
        sb.AppendLine(
            "Every number the report asserts, checked against every number in the passages above. "
            + "This arithmetic is established — do not redo it. It does not decide the "
            + "requirement: a figure can match and still be the wrong figure, from the wrong "
            + "document or as at the wrong date, which is what the guards below are for.");
        sb.AppendLine();

        foreach (var c in compared)
        {
            if (c.IsMatch)
            {
                sb.AppendLine($"- {c.Asserted.AsWritten} — corroborated by {c.Nearest!.AsWritten} in [{c.PassageId}].");
            }
            else if (c.Nearest is null)
            {
                sb.AppendLine($"- {c.Asserted.AsWritten} — no comparable figure in any passage.");
            }
            else
            {
                var delta = c.Asserted.Value == 0m
                    ? null
                    : $", {Math.Abs((c.Nearest.Value - c.Asserted.Value) / c.Asserted.Value):P1} away";

                sb.AppendLine(
                    $"- {c.Asserted.AsWritten} — not matched. Nearest is {c.Nearest.AsWritten} "
                    + $"in [{c.PassageId}]{delta}.");
            }
        }

        return sb.ToString();
    }
}
