using System.Collections.Concurrent;
using System.Text;

namespace AiPromptEvaluator;

public enum CheckRunState
{
    Queued,
    Running,
    Finished,
    Skipped,
}

/// <summary>One check's line on the board.</summary>
public sealed record CheckRunRow
{
    public required string CheckId { get; init; }
    public required string CheckName { get; init; }
    public CheckRunState State { get; init; } = CheckRunState.Queued;
    public string Detail { get; init; } = "queued";
    public CheckFinding? Finding { get; init; }

    /// <summary>
    /// The glyph, matching the checks list so the two read the same way. An error is not a
    /// concern and a requirement nobody could assess is not a pass, so each gets its own mark
    /// rather than borrowing a neighbour's.
    /// </summary>
    public string Glyph => State switch
    {
        CheckRunState.Queued => "·",
        CheckRunState.Running => "▶",
        CheckRunState.Skipped => "–",
        _ => Finding?.ParsedOutcome switch
        {
            CheckOutcome.NoIssue => "✓",
            CheckOutcome.PotentialConcern => "!",
            CheckOutcome.NotApplicable => "–",
            CheckOutcome.Indeterminate => "?",
            _ => "✕",
        },
    };
}

/// <summary>
/// What the response window shows while a run is in progress: one line per check, in the order
/// they were listed, each updating in place.
///
/// Checks now run several at a time, which made the old output — a line appended as each check
/// finished — actively misleading: lines arrived in completion order, nothing was said about the
/// checks in flight, and a run of ten looked like a run of however many had happened to finish.
/// A fixed list that updates in place says what is happening; an append-only log says what has
/// happened, which is a different and less useful thing while you are waiting.
///
/// Updates arrive from several threads at once, so state lives in a concurrent dictionary and
/// <see cref="Render"/> takes a snapshot rather than locking the whole board.
/// </summary>
public sealed class CheckRunBoard
{
    private readonly IReadOnlyList<AssessmentCheck> _order;
    private readonly ConcurrentDictionary<AssessmentCheck, CheckRunRow> _rows = new();

    public CheckRunBoard(IReadOnlyList<AssessmentCheck> checks)
    {
        _order = checks;

        foreach (var check in checks)
        {
            _rows[check] = new CheckRunRow
            {
                CheckId = CheckQueryPlanLoader.NormaliseCheckId(check.CheckId),
                CheckName = check.CheckName,
            };
        }
    }

    public IReadOnlyList<AssessmentCheck> Checks => _order;

    public CheckRunRow Row(AssessmentCheck check) => _rows[check];

    /// <summary>
    /// Marks everything not yet finished as stopped, and returns what was affected.
    ///
    /// Several checks are in flight at once, so a cancelled run leaves several rows mid-way.
    /// Leaving them showing the running glyph would say the run was still going.
    /// </summary>
    public IReadOnlyList<AssessmentCheck> StopOutstanding(string why)
    {
        var stopped = _order
            .Where(c => _rows[c].State is CheckRunState.Queued or CheckRunState.Running)
            .ToList();

        foreach (var check in stopped)
        {
            Skip(check, why);
        }

        return stopped;
    }

    public void Start(AssessmentCheck check) =>
        Update(check, row => row with { State = CheckRunState.Running, Detail = "starting" });

    public void Progress(AssessmentCheck check, string stage) =>
        Update(check, row => row with
        {
            State = CheckRunState.Running,

            // The runner prefixes its stages with the check id, which is already the second
            // column here. Repeating it would push the useful half off the line.
            Detail = Strip(stage, row.CheckId),
        });

    public void Finish(AssessmentCheck check, CheckFinding finding) =>
        Update(check, row => row with
        {
            State = CheckRunState.Finished,
            Finding = finding,
            Detail = Describe(finding),
        });

    public void Skip(AssessmentCheck check, string why) =>
        Update(check, row => row with { State = CheckRunState.Skipped, Detail = why });

    private void Update(AssessmentCheck check, Func<CheckRunRow, CheckRunRow> change) =>
        _rows.AddOrUpdate(check, _ => throw new KeyNotFoundException(), (_, row) => change(row));

    public int Count(CheckRunState state) => _order.Count(c => _rows[c].State == state);

    /// <summary>A one-line summary for the status bar.</summary>
    public string Headline
    {
        get
        {
            var finished = Count(CheckRunState.Finished);
            var running = Count(CheckRunState.Running);

            return running > 0
                ? $"{finished}/{_order.Count} complete, {running} running..."
                : $"{finished}/{_order.Count} complete";
        }
    }

    /// <summary>True once nothing is queued or running.</summary>
    public bool IsComplete =>
        Count(CheckRunState.Queued) == 0 && Count(CheckRunState.Running) == 0;

    public string Render()
    {
        var sb = new StringBuilder();
        var width = _order.Count == 0 ? 10 : _order.Max(c => _rows[c].CheckName.Length);

        sb.AppendLine($"ASSESSING {_order.Count} CHECK(S) — {Headline}");
        sb.AppendLine(new string('=', 78));
        sb.AppendLine();

        foreach (var check in _order)
        {
            var row = _rows[check];
            sb.AppendLine($"  {row.Glyph}  {row.CheckId,-9}  {Pad(row.CheckName, width)}  {row.Detail}");
        }

        sb.AppendLine();
        sb.AppendLine(new string('-', 78));
        sb.AppendLine(
            "·  queued    ▶  running    ✓  no issue    !  potential concern    "
            + "?  not assessable    –  N/A or skipped    ✕  error");
        sb.AppendLine();
        sb.AppendLine("The full findings appear here once every check has finished.");

        return sb.ToString();
    }

    private static string Describe(CheckFinding finding) =>
        $"{CheckFinding.Describe(finding.ParsedOutcome)}  "
        + $"({finding.Groups.Count} requirement(s), {finding.SearchesRun} search(es), "
        + $"{finding.Elapsed.TotalSeconds:0.0}s)";

    private static string Strip(string stage, string checkId) =>
        stage.StartsWith(checkId + ":", StringComparison.OrdinalIgnoreCase)
            ? stage[(checkId.Length + 1)..].Trim()
            : stage;

    /// <summary>Pads to a common width so the detail column lines up down the page.</summary>
    private static string Pad(string text, int width) =>
        text.Length >= width ? text : text + new string(' ', width - text.Length);
}
