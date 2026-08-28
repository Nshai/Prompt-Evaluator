using System.Text;
using System.Text.RegularExpressions;

namespace AiPromptEvaluator;

/// <summary>
/// Reads a case's expected-results benchmark and lines it up against a run.
///
/// <b>Scoring a run has been a manual read of a 65,000-line log against a 42,000-character
/// document, and that is why it has been done for one case out of four.</b> Four benchmarks
/// exist; one has ever been scored. Every improvement to this pipeline is argued from that single
/// scored case, and the arguments are only as good as a comparison nobody can afford to repeat.
///
/// This does the bookkeeping and stops there. <b>Whether a run's finding is the benchmark's
/// finding is a judgement, and it stays a human one</b> — a group can reach the right conclusion
/// by the wrong route, or state half of it, and no string match settles that. What a machine can
/// do is put the benchmark row and the candidate findings side by side, and count the one thing
/// that needs no judgement at all: how many expected findings sit in checks that never ran.
///
/// <b>Nothing new has to be authored.</b> The benchmarks already carry their findings as
/// <c>| ID | Finding | Severity |</c> tables, and the id says which check the finding belongs to —
/// <c>F7.3</c> is CHK-007. So a benchmark written months before this class is readable by it, and
/// a new case is scorable the day it has a benchmark.
/// </summary>
public static class BenchmarkScore
{
    /// <summary>One finding the benchmark says a run should make.</summary>
    /// <param name="Id">The benchmark's own id, e.g. <c>F7.3</c>.</param>
    /// <param name="CheckId">The check that should raise it, from the id.</param>
    public sealed record Expectation(string Id, string CheckId, string Severity, string Summary);

    /// <summary>One expectation, and what the run has to offer against it.</summary>
    /// <param name="CheckRan">
    /// False where the check settled as Not Applicable or produced no finding at all. <b>This is
    /// the mechanical half of the score</b> — an expectation in a check that never ran is missed,
    /// and no reading of anything is required to know it.
    /// </param>
    /// <param name="Candidates">
    /// The groups of that check that raised a concern, for a reader to match against the
    /// expectation. Deliberately unfiltered: narrowing by keyword would hide exactly the case
    /// where a run reached the finding by a route the benchmark did not anticipate.
    /// </param>
    public sealed record Row(
        Expectation Expectation,
        bool CheckRan,
        string CheckOutcome,
        IReadOnlyList<string> Candidates);

    /// <summary>
    /// A benchmark row: an id, the finding, and a severity. Ids allow a trailing letter so a
    /// benchmark can split one finding without renumbering the rest.
    /// </summary>
    private static readonly Regex FindingRow = new(
        @"^\|\s*(?<id>F(?<check>\d{1,2})\.\d{1,2}[a-z]?)\s*\|(?<summary>.*)\|(?<severity>[^|]*)\|\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Every finding a benchmark expects, in document order, first mention winning.
    ///
    /// A benchmark lists each finding once in its own check's table and again in a scoring block
    /// at the end, so ids repeat by design. The first is the one carrying the evidence.
    ///
    /// <b>Withdrawn rows are dropped.</b> A benchmark is a document that gets corrected — one of
    /// these was written against a misreading and struck through when a run produced the rows
    /// that disproved it — and a scorer that counted withdrawn entries would mark a run down for
    /// not finding something nobody believes any more.
    /// </summary>
    public static IReadOnlyList<Expectation> Parse(string benchmarkMarkdown)
    {
        var found = new Dictionary<string, Expectation>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        foreach (var line in benchmarkMarkdown.Split('\n'))
        {
            if (FindingRow.Match(line.TrimEnd('\r')) is not { Success: true } match)
            {
                continue;
            }

            var summary = match.Groups["summary"].Value.Trim();
            var severity = match.Groups["severity"].Value.Trim();

            if (IsWithdrawn(summary) || IsWithdrawn(severity) || IsPass(severity))
            {
                continue;
            }

            var id = match.Groups["id"].Value;

            if (found.ContainsKey(id))
            {
                continue;
            }

            order.Add(id);
            found[id] = new Expectation(
                id,
                $"CHK-{int.Parse(match.Groups["check"].Value):000}",
                match.Groups["severity"].Value.Trim(),
                Shorten(summary));
        }

        return order.Select(id => found[id]).ToList();
    }

    /// <summary>
    /// Whether a row has been struck from the benchmark. Both conventions the benchmarks use:
    /// the word, and the strikethrough that usually accompanies it.
    /// </summary>
    /// <summary>
    /// Whether a row records a requirement the benchmark examined and found nothing wrong with.
    ///
    /// <b>The benchmarks enumerate every requirement, not only the failures</b>, and a pass is
    /// scored the opposite way round from a finding: a run that stays silent about it is right.
    /// Counting these as expectations would mark a run down for agreeing with the benchmark.
    ///
    /// They are not simply discarded — a run raising a concern where the benchmark records a pass
    /// is a false positive, and that is worth counting too. It is a different worksheet, and one
    /// nobody has asked for yet.
    /// </summary>
    private static bool IsPass(string severity) =>
        severity.Equals("No issue", StringComparison.OrdinalIgnoreCase)
        || severity.Equals("None", StringComparison.OrdinalIgnoreCase)
        || severity.Equals("-", StringComparison.Ordinal);

    private static bool IsWithdrawn(string text) =>
        text.Contains("WITHDRAWN", StringComparison.OrdinalIgnoreCase)
        || text.Contains("~~Withdrawn", StringComparison.OrdinalIgnoreCase)
        || text.Trim().Equals("Withdrawn", StringComparison.OrdinalIgnoreCase);

    /// <summary>The benchmark and the run, side by side, in benchmark order.</summary>
    public static IReadOnlyList<Row> Worksheet(
        IReadOnlyList<Expectation> expectations, IEnumerable<CheckFinding> findings)
    {
        var byCheck = findings
            .GroupBy(f => f.CheckId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return expectations
            .Select(expectation =>
            {
                if (!byCheck.TryGetValue(expectation.CheckId, out var finding))
                {
                    return new Row(expectation, false, "not in this run", []);
                }

                var ran = finding.ParsedOutcome is not CheckOutcome.NotApplicable;

                var candidates = finding.Groups
                    .Where(g => g.ParsedOutcome is CheckOutcome.PotentialConcern)
                    .Select(g => $"{g.GroupId}: {Shorten(g.Analysis)}")
                    .ToList();

                return new Row(expectation, ran, finding.Outcome, candidates);
            })
            .ToList();
    }

    /// <summary>
    /// The worksheet as text, with the part that needs no judgement summarised at the top.
    ///
    /// Written for a reader working through it with the run open, so the counts a machine can
    /// settle come first and the rows needing a decision follow.
    /// </summary>
    public static string Format(IReadOnlyList<Row> rows)
    {
        var sb = new StringBuilder();
        var unreachable = rows.Where(r => !r.CheckRan).ToList();

        sb.AppendLine($"# Benchmark worksheet — {rows.Count} expected finding(s)");
        sb.AppendLine();

        if (unreachable.Count > 0)
        {
            sb.AppendLine(
                $"**{unreachable.Count} of {rows.Count} sit in checks that did not run.** These are "
                + "missed, and nothing in the run had a chance to find them. Settle this before "
                + "reading anything below it: a check that did not run is a plan or trigger "
                + "defect, and it is cheaper to fix than anything the assessor did.");
            sb.AppendLine();

            foreach (var group in unreachable
                         .GroupBy(r => r.Expectation.CheckId, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine(
                    $"- **{group.Key}** ({group.First().CheckOutcome}): "
                    + string.Join(", ", group.Select(r => r.Expectation.Id)));
            }

            sb.AppendLine();
        }

        sb.AppendLine(
            "Mark each row caught, partial or missed. The candidates are every group of the "
            + "responsible check that raised a concern — unfiltered, because a run that reached "
            + "the finding by an unexpected route still reached it.");
        sb.AppendLine();

        foreach (var row in rows.Where(r => r.CheckRan))
        {
            sb.AppendLine($"## {row.Expectation.Id} [{row.Expectation.Severity}] {row.Expectation.CheckId}");
            sb.AppendLine();
            sb.AppendLine($"Expected: {row.Expectation.Summary}");
            sb.AppendLine();

            if (row.Candidates.Count == 0)
            {
                sb.AppendLine("No group of this check raised a concern. Missed.");
            }
            else
            {
                foreach (var candidate in row.Candidates)
                {
                    sb.AppendLine($"- {candidate}");
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>Enough of a row to recognise it, without reproducing the benchmark.</summary>
    private static string Shorten(string text)
    {
        const int Enough = 240;

        var flat = Whitespace.Replace(text, " ").Trim();

        return flat.Length <= Enough ? flat : flat[..Enough] + "…";
    }

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
}
