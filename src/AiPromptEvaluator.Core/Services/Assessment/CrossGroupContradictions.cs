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

    // ── documents one group has and another says are missing ──────────────────

    /// <summary>One kind of document two groups disagreed about the existence of.</summary>
    public sealed record DocumentDispute(string DocumentKind, string Present, string Absent);

    /// <summary>
    /// Kinds of document, as the domain names them.
    ///
    /// <b>Types, never instances.</b> "A capacity for loss questionnaire" recurs in every case in
    /// this domain; a provider, a client or a filename does not, and a list naming one would tune
    /// this pass to the case it was written from. Each entry is a phrase a finding would use about
    /// any file.
    ///
    /// Ordered longest first so "capacity for loss questionnaire" is recognised as itself rather
    /// than as a "questionnaire", which keeps two genuinely different documents from being paired.
    /// </summary>
    private static readonly string[] DocumentKinds =
    [
        "capacity for loss questionnaire",
        "attitude to risk questionnaire",
        "risk profile questionnaire",
        "key features document",
        "letter of authority",
        "annual statement",
        "cash flow model",
        "fund factsheet",
        "risk profile report",
        "provider illustration",
        "comparison report",
        "research report",
        "questionnaire",
        "illustration",
        "fact find",
        "factfind",
        "kiid",
        "cashflow",
    ];

    /// <summary>Ways a finding says a document is not there.</summary>
    private static readonly string[] AbsenceCues =
    [
        "no ", "not on file", "absent", "missing", "does not contain", "is not present",
        "holds no", "contains no", "nothing on file", "was not provided", "not been provided",
        "no evidence of", "could not be located", "is not in the",
    ];

    /// <summary>Ways a finding says a document is there.</summary>
    private static readonly string[] PresenceCues =
    [
        "on file", "is present", "provided", "holds a", "contains a", "records", "shows",
        "in the pack", "was retrieved", "documents", "sets out", "is available",
    ];

    /// <summary>
    /// Where one group says a kind of document is on file and another says it is not.
    ///
    /// <b>This is the machine-detectable signature of the most expensive failure on record, and
    /// unlike a prompt rule it does not depend on the assessor taking instruction.</b>
    ///
    /// Measured: two runs of one case, on two models at a 3.3× cost difference, received
    /// byte-identical evidence packs. Both reported a questionnaire as missing from the file while
    /// a passage carrying it sat at rank 6 of 24 in the pack being read. In the stronger run the
    /// two halves were one check apart — one group cited the passage id the document had arrived
    /// under, the next said the file held no such document — and nothing in the pipeline held both
    /// sentences at once, so nothing could see it.
    ///
    /// Costs no retrieval and no model call, like the figures pass above, and is reported the same
    /// way: as something to look at rather than as a finding. One side may be talking about a
    /// different document, and only a reader can settle that.
    /// </summary>
    public static IReadOnlyList<DocumentDispute> Documents(IEnumerable<CheckFinding> findings)
    {
        var present = new Dictionary<string, (string Where, string Sentence)>(StringComparer.Ordinal);
        var absent = new Dictionary<string, (string Where, string Sentence)>(StringComparer.Ordinal);

        foreach (var finding in findings)
        {
            foreach (var group in finding.Groups)
            {
                var where = $"{finding.CheckId}/{group.GroupId}";

                foreach (var sentence in DocumentSentences(group))
                {
                    if (KindIn(sentence) is not { } kind)
                    {
                        continue;
                    }

                    var denied = Denials(sentence, kind).ToList();

                    foreach (var key in denied)
                    {
                        absent.TryAdd(key, (where, Shorten(sentence)));
                    }

                    // A sentence that denies the document does not also assert it, whatever cues
                    // it carries. "No risk profile report was provided" contains "provided", and
                    // reading it both ways puts one sentence on both sides of its own dispute.
                    // A denial of a *part* is not such a sentence: "the questionnaire is on file
                    // and records no rating" asserts the document and denies the rating, and both
                    // halves are true and worth keeping.
                    if (!denied.Contains(Key(kind, string.Empty), StringComparer.Ordinal)
                        && Claims(sentence, PresenceCues))
                    {
                        // Both keys. A sentence saying the responses are on file says the
                        // questionnaire is too, and a group denying either should meet it.
                        present.TryAdd(Key(kind, string.Empty), (where, Shorten(sentence)));

                        if (ObjectAfter(sentence, sentence.IndexOf(kind, StringComparison.OrdinalIgnoreCase) + kind.Length) is { } carried)
                        {
                            present.TryAdd(Key(kind, carried), (where, Shorten(sentence)));
                        }
                    }
                }
            }
        }

        return present.Keys
            .Where(absent.ContainsKey)
            .Where(key => present[key].Where != absent[key].Where)
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => new DocumentDispute(
                key,
                $"[{present[key].Where}] {present[key].Sentence}",
                $"[{absent[key].Where}] {absent[key].Sentence}"))
            .ToList();
    }

    /// <summary>The addendum for the report.</summary>
    public static string Format(IReadOnlyList<DocumentDispute> disputes)
    {
        if (disputes.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        sb.AppendLine(new string('=', 78));
        sb.AppendLine("DOCUMENTS ONE CHECK FOUND AND ANOTHER CALLED MISSING");
        sb.AppendLine(new string('=', 78));
        sb.AppendLine();
        sb.AppendLine(
            "One requirement says a kind of document is on file and another says it is not. Both "
            + "cannot be right, and the difference matters: a document wrongly called missing "
            + "turns evidence the file holds into a gap the adviser is asked to explain. Check "
            + "the passage ids — the group reporting the absence may simply not have been given "
            + "the passage, which is a retrieval problem rather than an advice one.");
        sb.AppendLine();

        foreach (var dispute in disputes)
        {
            sb.AppendLine($"  {dispute.DocumentKind}");
            sb.AppendLine($"    found:   {dispute.Present}");
            sb.AppendLine($"    missing: {dispute.Absent}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// The sentences a claim about a document's existence is made in.
    ///
    /// A wider set than <see cref="Sentences"/>, and for a reason. That method reads only the two
    /// sided fields, because for a figure it is the side — assertion or evidence — that turns two
    /// numbers into a contradiction. A statement about whether a document exists has no such
    /// structure and is usually written in "analysis", which is exactly where both observed halves
    /// of the measured case were: <i>"the file holds no capacity for loss questionnaire
    /// responses"</i> and <i>"the questionnaire responses in [P17] are on file"</i>.
    ///
    /// "reportSays" is included and rarely contributes. It is left in because a group occasionally
    /// records the report's own claim about what the file contains, and that claim disagreeing
    /// with another group is the same defect seen from the other side.
    /// </summary>
    private static IEnumerable<string> DocumentSentences(GroupFinding group)
    {
        var sources = new[] { group.FileSays, group.Analysis, group.ReportSays }
            .Concat(group.Discrepancies);

        foreach (var text in sources.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            foreach (var sentence in SentenceBoundary.Split(text))
            {
                var trimmed = sentence.Trim();

                if (trimmed.Length > 12)
                {
                    yield return trimmed;
                }
            }
        }
    }

    /// <summary>The document kind a sentence is about, longest match first, or null.</summary>
    private static string? KindIn(string sentence) =>
        DocumentKinds.FirstOrDefault(
            kind => sentence.Contains(kind, StringComparison.OrdinalIgnoreCase));

    private static bool Claims(string sentence, string[] cues) =>
        cues.Any(cue => sentence.Contains(cue, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Parts of a document a finding can separately say are missing.
    ///
    /// <b>A claim about a document is really a claim about a document and a part of it, and
    /// collapsing the two inverts the result.</b> "The questionnaire is on file and records no
    /// capacity for loss rating" is the most useful sentence an assessor can write about a thin
    /// document: an absence cue, a presence cue, and no contradiction at all. Keyed on the
    /// document alone it pairs against every presence claim about that document — including, in
    /// the first draft of this pass, a sentence from the same finding.
    ///
    /// Keyed on the part, it pairs only against a claim about the same part, which is what a
    /// contradiction is. Generic to the domain, like the document kinds: every case file has
    /// documents with responses, notes and ratings in them.
    /// </summary>
    /// <remarks>
    /// "record" and "records" are deliberately absent. In these findings they are almost always
    /// verbs — "the report records", "[P8] records no additional notes" — and as a part they
    /// matched inside "recorded", which turned "has no recorded responses" into a denial of
    /// something called a record rather than of the responses. That un-paired the one real
    /// contradiction this pass was built from.
    /// </remarks>
    private static readonly string[] DocumentParts =
    [
        "responses", "response", "answers", "answer", "notes", "entries", "rating", "ratings",
        "score", "scores", "content", "contents", "data",
    ];

    /// <summary>
    /// How far from a cue a document or a part may sit and still be what the cue is about. Long
    /// enough to carry "the case file contains no", short enough not to reach across a clause
    /// boundary into a negation about something else entirely.
    /// </summary>
    private const int CueReach = 40;

    /// <summary>What a sentence denies about <paramref name="kind"/>, as zero or more keys.</summary>
    /// <remarks>
    /// Two shapes, and both were observed in one run of one check:
    ///
    /// <list type="bullet">
    /// <item><b>Before</b> — "the file holds no capacity for loss questionnaire". The document
    /// itself is denied, so the key names the document and nothing else.</item>
    /// <item><b>After</b> — "the questionnaire has no recorded responses". A part is denied, so
    /// the key names the part; a group saying the responses are on file meets it, and a group
    /// saying the questionnaire is on file does not.</item>
    /// </list>
    ///
    /// The second shape is why this returns a set rather than a boolean. One sentence can deny
    /// two parts — "shows the responses but no derived rating" — and only one of them may have a
    /// counterpart.
    /// </remarks>
    private static IEnumerable<string> Denials(string sentence, string kind)
    {
        var at = sentence.IndexOf(kind, StringComparison.OrdinalIgnoreCase);

        if (at < 0)
        {
            yield break;
        }

        var after = at + kind.Length;

        foreach (var cue in AbsenceCues)
        {
            var from = 0;

            while (sentence.IndexOf(cue, from, StringComparison.OrdinalIgnoreCase) is var cueAt and >= 0)
            {
                var cueEnd = cueAt + cue.Length;
                from = cueEnd;

                if (cueAt < at)
                {
                    // Before the document, and near enough to be about it — but only if nothing
                    // else is named in between, or "no rating was recorded in the questionnaire"
                    // would read as a denial of the questionnaire.
                    if (at - cueEnd <= CueReach && ObjectAfter(sentence, cueEnd, at) is null)
                    {
                        yield return Key(kind, string.Empty);
                    }
                }
                else if (cueAt >= after && ObjectAfter(sentence, cueEnd) is { } part)
                {
                    yield return Key(kind, part);
                }
            }
        }
    }

    /// <summary>
    /// The first document part named within <see cref="CueReach"/> of <paramref name="from"/>,
    /// or null. <paramref name="limit"/> bounds the search where the caller needs it to stop at a
    /// known position rather than at the usual reach.
    /// </summary>
    private static string? ObjectAfter(string sentence, int from, int? limit = null)
    {
        if (from < 0 || from >= sentence.Length)
        {
            return null;
        }

        var to = Math.Min(limit ?? from + CueReach, sentence.Length);

        if (to <= from)
        {
            return null;
        }

        var window = sentence[from..to];

        // Whole words. Matched as substrings, "record" is inside "recorded" and "answer" inside
        // "answered", so a denial of the responses became a denial of something else and the two
        // halves of a real contradiction stopped meeting.
        return DocumentParts
            .Select(part => (part, at: WordAt(window, part)))
            .Where(found => found.at >= 0)
            .OrderBy(found => found.at)
            .ThenByDescending(found => found.part.Length)
            .Select(found => Singular(found.part))
            .FirstOrDefault();
    }

    /// <summary>Where <paramref name="word"/> appears in <paramref name="text"/> as a whole word, or -1.</summary>
    private static int WordAt(string text, string word)
    {
        for (var at = 0; at <= text.Length - word.Length; at++)
        {
            if (string.Compare(text, at, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0)
            {
                continue;
            }

            var before = at == 0 || !char.IsLetter(text[at - 1]);
            var after = at + word.Length == text.Length || !char.IsLetter(text[at + word.Length]);

            if (before && after)
            {
                return at;
            }
        }

        return -1;
    }

    /// <summary>
    /// Plural and singular are the same part. "has no recorded response" and "shows the
    /// responses" are about one thing, and keying them apart would silently un-pair them.
    /// </summary>
    private static string Singular(string part) =>
        part.EndsWith('s') ? part[..^1] : part;

    /// <summary>A claim's key: the document, and the part of it being claimed about.</summary>
    private static string Key(string kind, string part) =>
        part.Length == 0 ? kind : $"{kind} ({part})";

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
