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
    /// <summary>
    /// How far apart two figures must be, in proportion, before they are worth a reviewer's
    /// attention. Below this they are the same number rounded differently, or the same number
    /// read from two documents — agreement, reported as disagreement, which is how an addendum
    /// teaches people to skip it.
    /// </summary>
    public const double MinimumDisagreement = 0.05;

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
        var claims = new List<(string Subject, string Where, string Sentence, decimal Value, string Side)>();

        foreach (var finding in findings)
        {
            foreach (var group in finding.Groups)
            {
                var where = $"{finding.CheckId}/{group.GroupId}";

                foreach (var (sentence, side) in Sentences(group))
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
                        claims.Add(($"{subject} ({recurrence})", where, Shorten(sentence), value, side));
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

            // The pair that most disagrees about the same claim.
            //
            // Widest absolute spread was tried and reports the wrong pair. Measured on a real
            // run, five pairs came back and none was a contradiction: they were restatements of
            // the same figures from different checks, chosen because the largest numbers were
            // furthest apart. Meanwhile the one pair that mattered — the fact find's £1,430
            // household total against the report's £4,486.67, both filed under
            // "Client income (monthly)" — was available under the right key and passed over.
            //
            // Three changes. A disagreement is *relative*, so £127,000 against £128,000 stops
            // outranking £1,430 against £4,486.67. Figures within a rounding of each other are
            // agreement and are not reported at all. And a pair straddling the report and the
            // file outranks one that does not, because an assertion contradicting the evidence
            // is the thing being looked for, while two readings of the same evidence are not.
            var best = (Low: default((string Where, string Sentence, decimal Value)),
                        High: default((string Where, string Sentence, decimal Value)),
                        CrossSide: false,
                        Disagreement: 0.0);

            foreach (var a in distinct)
            {
                foreach (var b in distinct)
                {
                    if (a.Where == b.Where || b.Value <= a.Value || a.Value <= 0)
                    {
                        continue;
                    }

                    var disagreement = (double)((b.Value - a.Value) / a.Value);

                    if (disagreement < MinimumDisagreement)
                    {
                        continue;
                    }

                    var crossSide = !string.Equals(a.Side, b.Side, StringComparison.Ordinal);

                    var better = best.Disagreement == 0
                                 || (crossSide, disagreement).CompareTo((best.CrossSide, best.Disagreement)) > 0;

                    if (better)
                    {
                        best = ((a.Where, a.Sentence, a.Value), (b.Where, b.Sentence, b.Value),
                                crossSide, disagreement);
                    }
                }
            }

            if (best.Disagreement == 0)
            {
                continue;
            }

            contradictions.Add(new Contradiction(
                bySubject.Key,
                $"[{best.Low.Where}] {best.Low.Sentence}",
                $"[{best.High.Where}] {best.High.Sentence}"));
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
    private static IEnumerable<(string Sentence, string Side)> Sentences(GroupFinding group)
    {
        // Which side a figure came from is what turns two numbers into a contradiction. An
        // assertion disagreeing with the evidence is the finding; two restatements of the same
        // evidence are not, however far apart they look.
        var sources = new (string Text, string Side)[]
        {
            (group.ReportSays, "report"),
            (group.FileSays, "file"),
        };

        foreach (var (text, side) in sources)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (var sentence in SentenceBoundary.Split(text))
            {
                var trimmed = sentence.Trim();

                if (trimmed.Length > 12)
                {
                    yield return (trimmed, side);
                }
            }
        }

        foreach (var discrepancy in group.Discrepancies.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            foreach (var sentence in SentenceBoundary.Split(discrepancy))
            {
                var trimmed = sentence.Trim();

                if (trimmed.Length > 12)
                {
                    yield return (trimmed, "discrepancy");
                }
            }
        }
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
