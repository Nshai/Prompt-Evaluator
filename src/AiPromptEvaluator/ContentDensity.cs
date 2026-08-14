using System.Text.RegularExpressions;

namespace AiPromptEvaluator;

/// <summary>
/// Whether a passage carries values or is an empty form.
///
/// This exists because of a measured failure that only appeared once retrieval started working.
/// Holding a slot for each document category a check declares fixed the coverage problem — every
/// check reached the Fact Find for the first time — and changed nothing about what the checks
/// found, because the passage each one got was the wrong part of the document:
///
/// <code>
///   CHK-005/G5.2   | | In force | In force | Status | | GMP Amount (p.a.) | |
///   CHK-008/G8.1   | Statement 5 Answer | Yes
///   CHK-009/G9.1   | Lump Sum Cont. | | | | | Value | £116,555.45
/// </code>
///
/// The Fact Find is a 42 KB form of mostly-unfilled tables. An unfilled row is short, generic and
/// structurally close to any query, so it embeds well while asserting nothing — and it beat the
/// income and expenditure section, which is where four of the five findings that run missed
/// actually live.
///
/// **Emptiness alone does not separate them.** Measured on that run, the useless passage was 80%
/// empty cells and the one carrying the client's income was 68% — both are sparse forms. What
/// separates them is whether the *filled* cells are answers or merely field labels:
///
/// <code>
///   the form skeleton     17 filled cells,  1 carrying a value    6%
///   the income table      32 filled cells, 10 carrying a value   31%
/// </code>
///
/// So the test is for values, not for blanks.
///
/// **Nothing is discarded on the strength of this.** A passage judged a skeleton is ranked below
/// its peers, never dropped: an unfilled section is sometimes exactly the finding — a risk profile
/// recording "No additional notes were recorded" under every heading is evidence that nobody
/// looked — and a filter that deletes evidence of absence would trade one silent loss for another.
/// It sinks; it does not vanish.
/// </summary>
public static class ContentDensity
{
    /// <summary>
    /// The share of filled cells that must carry a value before a table is treated as filled in.
    ///
    /// Set well below the lowest genuine passage measured (21%) and well above the skeleton (6%),
    /// because the cost of the two mistakes is not symmetric: demoting a real passage can lose a
    /// finding, while leaving a skeleton in place merely wastes a slot.
    /// </summary>
    public const double MinimumValueShare = 0.12;

    /// <summary>
    /// Below this many filled cells there is not enough to judge, and a short table is as likely
    /// to be a two-row summary as a form.
    /// </summary>
    public const int MinimumCellsToJudge = 8;

    /// <summary>
    /// The share of a passage's characters that must sit in table rows before it is judged as a
    /// table at all. Prose is not a form and is never demoted by this.
    /// </summary>
    public const double MinimumTabularShare = 0.4;

    private static readonly Regex SeparatorRow = new(@"^[\s|:\-]+$", RegexOptions.Compiled);

    /// <summary>
    /// True when a passage is a table whose filled cells are overwhelmingly field labels rather
    /// than answers — a form nobody completed.
    /// </summary>
    public static bool IsFormSkeleton(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var rows = text
            .Split('\n')
            .Where(line => line.Contains('|') && !SeparatorRow.IsMatch(line))
            .ToList();

        if (rows.Count == 0)
        {
            return false;
        }

        // Prose that happens to contain a table is judged on the whole passage, not on the table:
        // a paragraph carrying one summary row is not a form.
        if (rows.Sum(r => r.Length) / (double)text.Length < MinimumTabularShare)
        {
            return false;
        }

        var filled = rows
            .SelectMany(row => row.Split('|'))
            .Select(cell => cell.Trim())
            .Where(cell => cell.Length > 0)
            .ToList();

        if (filled.Count < MinimumCellsToJudge)
        {
            return false;
        }

        var values = filled.Count(CarriesAValue);

        return values / (double)filled.Count < MinimumValueShare;
    }

    /// <summary>
    /// Whether one cell holds an answer rather than the name of a question. Digits cover amounts,
    /// dates, percentages and reference numbers; yes and no cover the tick-box half of a form.
    /// </summary>
    private static bool CarriesAValue(string cell) =>
        cell.Any(char.IsDigit)
        || cell.Equals("yes", StringComparison.OrdinalIgnoreCase)
        || cell.Equals("no", StringComparison.OrdinalIgnoreCase);
}
