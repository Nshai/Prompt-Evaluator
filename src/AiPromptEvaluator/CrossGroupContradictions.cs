using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AiPromptEvaluator;

/// <summary>
/// Contradictions that no single group could see, found by reading the finished findings
/// against each other.
///
/// Each group is assessed against its own evidence pack and never revisited, so a contradiction
/// whose two halves fall in different groups is structurally unreachable however good retrieval
/// becomes. This is not a hypothetical shape. Scoring one run against the benchmark, four
/// findings failed for exactly this reason, and the clearest was the client's income:
///
/// <list type="bullet">
/// <item>the fact find's <b>£1,200 net basic monthly income</b> was retrieved, and written into
/// the <c>fileSays</c> of a group in CHK-006;</item>
/// <item>the report's claim of <b>£300 per week</b> was quoted twice, in two groups of
/// CHK-001.</item>
/// </list>
///
/// Both were in the same run, in the same log, four thousand lines apart, and no component ever
/// held both. Nothing was missing except somewhere to put them side by side.
///
/// This runs after every group has answered, costs no retrieval and no model call, and reads
/// only what is already on <see cref="CheckFinding.Groups"/>. It is deliberately conservative:
/// it reports where the same quantity was described differently, and leaves the judgement of
/// which is right to the reviewer.
/// </summary>
public static class CrossGroupContradictions
{
    /// <summary>A quantity two groups described differently, and where each said it.</summary>
    public sealed record Contradiction(string Subject, string Left, string Right)
    {
        public override string ToString() => $"{Subject}: {Left} vs {Right}";
    }

    /// <summary>
    /// Money as it appears in these findings — "£1,200", "£116,998.47", "£110,185". Percentages
    /// and bare integers are deliberately not matched: the false-positive rate on years, ages,
    /// page numbers and risk ratings would bury the signal.
    /// </summary>
    private static readonly Regex MoneyPattern = new(
        @"£\s?(\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d{2})?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Subjects worth pairing. Restricted to the quantities the checks actually turn on,
    /// because pairing every number against every other produces mostly coincidences — two
    /// figures being different is not a contradiction unless they are meant to be the same
    /// thing.
    /// </summary>
    private static readonly (string Subject, string[] Cues)[] Subjects =
    [
        ("Client income", ["net income", "monthly income", "income of", "earns", "earning", "per week", "weekly income"]),
        ("Household expenditure", ["expenditure", "outgoings", "spends"]),
        ("Disposable income", ["disposable"]),
        ("Total pension value", ["pension arrangements", "combined value", "total pension", "plans sum", "pension value"]),
        ("Transfer value", ["transfer value", "being transferred", "amount invested"]),
        ("Adviser charge", ["initial fee", "initial advice fee", "adviser charge", "ongoing fee"]),
        ("Projected value", ["projected value", "maturity value", "at age 75"]),
    ];

    /// <summary>
    /// Every quantity two findings described with different figures. Groups are compared
    /// across checks as well as within one, because the split that hides these is the check
    /// catalogue itself.
    /// </summary>
    public static IReadOnlyList<Contradiction> In(IEnumerable<CheckFinding> findings)
    {
        var claims = new List<(string Subject, string Where, string Sentence, decimal Value)>();

        foreach (var finding in findings)
        {
            foreach (var group in finding.Groups)
            {
                var where = $"{finding.CheckId}/{group.GroupId}";

                foreach (var sentence in Sentences(group))
                {
                    var subject = SubjectOf(sentence);

                    if (subject is null)
                    {
                        continue;
                    }

                    // Two figures are only in disagreement if they are the same kind of
                    // quantity. A £20-per-week volunteer income and a £3,305 one-off fee both
                    // sit in sentences containing the word "income", and pairing them says
                    // nothing. Keying the subject by recurrence as well as topic keeps weekly
                    // against weekly and one-off against one-off.
                    foreach (var (value, recurrence) in MoneyIn(sentence))
                    {
                        claims.Add(($"{subject} ({recurrence})", where, Shorten(sentence), value));
                    }
                }
            }
        }

        var contradictions = new List<Contradiction>();

        foreach (var bySubject in claims.GroupBy(c => c.Subject))
        {
            var distinct = bySubject
                .GroupBy(c => c.Value)
                .Select(g => g.First())
                .OrderBy(c => c.Value)
                .ToList();

            if (distinct.Count < 2)
            {
                continue;
            }

            // The widest disagreement between two *different* groups.
            //
            // Taking the overall extremes looked equivalent and is not: a single group often
            // states several figures for one subject — a weekly amount and its monthly
            // equivalent in the same sentence — so both extremes can belong to it, and the
            // cross-group disagreement sitting between them is passed over. That is precisely
            // the case this class exists for, and the first version of it silently found
            // nothing.
            (string Where, string Sentence, decimal Value)? low = null;
            (string Where, string Sentence, decimal Value)? high = null;

            foreach (var a in distinct)
            {
                foreach (var b in distinct)
                {
                    if (a.Where == b.Where || b.Value <= a.Value)
                    {
                        continue;
                    }

                    if (low is null || b.Value - a.Value > high!.Value.Value - low.Value.Value)
                    {
                        low = (a.Where, a.Sentence, a.Value);
                        high = (b.Where, b.Sentence, b.Value);
                    }
                }
            }

            if (low is null)
            {
                continue;
            }

            contradictions.Add(new Contradiction(
                bySubject.Key,
                $"[{low.Value.Where}] {low.Value.Sentence}",
                $"[{high!.Value.Where}] {high.Value.Sentence}"));
        }

        return contradictions;
    }

    /// <summary>
    /// The addendum for the report. Written as an invitation to look rather than as a finding,
    /// because this pass knows two figures differ and nothing about which is right.
    /// </summary>
    public static string Format(IReadOnlyList<Contradiction> contradictions)
    {
        if (contradictions.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        sb.AppendLine(new string('=', 78));
        sb.AppendLine("FIGURES DESCRIBED DIFFERENTLY IN DIFFERENT CHECKS");
        sb.AppendLine(new string('=', 78));
        sb.AppendLine();
        sb.AppendLine(
            "Each check assesses its own requirement against its own evidence, so a contradiction "
            + "split across two of them is invisible to both. These pairs were found by reading "
            + "the finished findings against each other. They are not findings — one side may "
            + "simply be a different quantity — but each is worth a look.");
        sb.AppendLine();

        foreach (var contradiction in contradictions)
        {
            sb.AppendLine($"  {contradiction.Subject}");
            sb.AppendLine($"    {contradiction.Left}");
            sb.AppendLine($"    {contradiction.Right}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// Splits a finding into sentences, without splitting money in half.
    ///
    /// The first version split on every <c>.</c> and therefore split every decimal, so a
    /// sentence about a £3,305.55 fee arrived as a fragment beginning "55 is deducted from…"
    /// and got paired against an unrelated figure. Of six pairs reported on a real run, none
    /// was a genuine contradiction and several began mid-number.
    ///
    /// A full stop between two digits is a decimal point, not a sentence ending.
    /// </summary>
    private static IEnumerable<string> Sentences(GroupFinding group)
    {
        var text = string.Join(
            " ",
            new[] { group.ReportSays, group.FileSays }
                .Concat(group.Discrepancies)
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        return SentenceBoundary
            .Split(text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 12);
    }

    /// <summary>A full stop or semicolon that is not sitting between two digits.</summary>
    private static readonly Regex SentenceBoundary = new(
        @"(?<!\d)[.;]|[.;](?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static string? SubjectOf(string sentence)
    {
        foreach (var (subject, cues) in Subjects)
        {
            if (cues.Any(cue => sentence.Contains(cue, StringComparison.OrdinalIgnoreCase)))
            {
                return subject;
            }
        }

        return null;
    }

    /// <summary>
    /// How often one figure recurs, judged from the words immediately around it rather than
    /// from the sentence as a whole.
    ///
    /// Per-sentence was tried and is wrong: *"the client receives £300 per week as an HGV
    /// driver, a monthly income of £1,300"* carries both markers, so whichever matched first
    /// won and the other figure was mislabelled. That sentence is the exact shape this class
    /// exists to pair against the fact find's monthly figure, and labelling it "weekly" made
    /// the pairing impossible.
    ///
    /// The window reaches further back than forward because the qualifier usually precedes the
    /// amount — "a monthly income of £1,300" — while a trailing "per week" sits right after it.
    /// </summary>
    private static string RecurrenceNear(string sentence, int at, int length)
    {
        var from = Math.Max(0, at - 40);
        var to = Math.Min(sentence.Length, at + length + 12);
        var window = sentence[from..to];

        if (Says(window, "per week", "weekly", "a week", "/week")) return "weekly";
        if (Says(window, "per month", "monthly", "a month", "/month", "p/m")) return "monthly";
        if (Says(window, "per annum", "annually", "annual", "a year", "p.a", "yearly")) return "annual";
        if (Says(window, "one-off", "one off", "initial fee", "initial advice fee", "lump sum")) return "one-off";

        return "unstated";
    }

    private static bool Says(string text, params string[] markers) =>
        markers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<(decimal Value, string Recurrence)> MoneyIn(string sentence) =>
        MoneyPattern.Matches(sentence)
            .Select(m => decimal.TryParse(
                m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                ? (Value: value, Recurrence: RecurrenceNear(sentence, m.Index, m.Length))
                : default)
            .Where(c => c.Value > 0)
            .Distinct();

    private static string Shorten(string sentence) =>
        sentence.Length <= 160 ? sentence : sentence[..157] + "...";
}
