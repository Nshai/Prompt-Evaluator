using System.Text;
using System.Text.Json.Serialization;

namespace AiPromptEvaluator;

/// <summary>The three outcomes the assessment checks allow.</summary>
public enum CheckOutcome
{
    NoIssue,
    PotentialConcern,
    NotApplicable,

    /// <summary>The run itself failed. Kept distinct so a broken run never reads as a pass.</summary>
    Error,
}

/// <summary>One cited piece of evidence behind a finding.</summary>
public sealed record FindingCitation
{
    [JsonPropertyName("source")] public string Source { get; init; } = string.Empty;
    [JsonPropertyName("category")] public string? Category { get; init; }
    [JsonPropertyName("quote")] public string? Quote { get; init; }
}

/// <summary>What the assessor concluded about one group of the plan.</summary>
public sealed record GroupFinding
{
    [JsonPropertyName("groupId")] public string GroupId { get; init; } = string.Empty;
    [JsonPropertyName("requirement")] public string? Requirement { get; init; }
    [JsonPropertyName("outcome")] public string Outcome { get; init; } = nameof(CheckOutcome.NoIssue);
    [JsonPropertyName("severity")] public string? Severity { get; init; }
    [JsonPropertyName("explanation")] public string Explanation { get; init; } = string.Empty;
    [JsonPropertyName("citations")] public List<FindingCitation> Citations { get; init; } = [];

    public CheckOutcome ParsedOutcome => CheckFinding.ParseOutcome(Outcome);
}

/// <summary>
/// The result of assessing one check: the overall outcome, the reasoning, and the per-group
/// detail. Retrieval statistics travel with it because a finding is only as good as what was
/// actually retrieved — a "No Issue" from a run that found nothing is not the same claim as
/// a "No Issue" from a run that found corroboration in three categories.
/// </summary>
public sealed record CheckFinding
{
    [JsonPropertyName("checkId")] public string CheckId { get; init; } = string.Empty;
    [JsonPropertyName("checkName")] public string CheckName { get; init; } = string.Empty;
    [JsonPropertyName("outcome")] public string Outcome { get; init; } = nameof(CheckOutcome.NoIssue);
    [JsonPropertyName("summary")] public string Summary { get; init; } = string.Empty;
    [JsonPropertyName("groups")] public List<GroupFinding> Groups { get; init; } = [];

    [JsonIgnore] public int SearchesRun { get; init; }
    [JsonIgnore] public int PassagesRetrieved { get; init; }
    [JsonIgnore] public int CanonicalPathsResolved { get; init; }
    [JsonIgnore] public int CanonicalPathsMissing { get; init; }
    [JsonIgnore] public TokenUsage Usage { get; init; } = TokenUsage.Empty;
    [JsonIgnore] public TimeSpan Elapsed { get; init; }
    [JsonIgnore] public string? Error { get; init; }

    public CheckOutcome ParsedOutcome => ParseOutcome(Outcome);

    /// <summary>
    /// Reads an outcome from whatever the model wrote. It is prompted for one of three exact
    /// values, but "Potential Concern", "potential_concern" and "N/A" all mean the same
    /// thing and none of them should degrade into a silent pass.
    /// </summary>
    public static CheckOutcome ParseOutcome(string? text)
    {
        var squeezed = new string((text ?? string.Empty)
            .Where(char.IsLetter)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return squeezed switch
        {
            "noissue" or "noissues" or "pass" => CheckOutcome.NoIssue,
            "potentialconcern" or "concern" or "fail" => CheckOutcome.PotentialConcern,
            "na" or "notapplicable" => CheckOutcome.NotApplicable,
            "error" => CheckOutcome.Error,

            // Anything unrecognised is treated as a concern rather than a pass: an assessor
            // that could not state an outcome has not cleared the check.
            _ => CheckOutcome.PotentialConcern,
        };
    }

    public static string Describe(CheckOutcome outcome) => outcome switch
    {
        CheckOutcome.NoIssue => "No Issue",
        CheckOutcome.PotentialConcern => "Potential Concern",
        CheckOutcome.NotApplicable => "N/A",
        _ => "Error",
    };

    public static CheckFinding Failed(string checkId, string checkName, string error, TimeSpan elapsed) => new()
    {
        CheckId = checkId,
        CheckName = checkName,
        Outcome = nameof(CheckOutcome.Error),
        Summary = error,
        Error = error,
        Elapsed = elapsed,
    };
}

/// <summary>
/// The consolidated result of a run across one or more checks, and the report the user
/// reads. Kept separate from the UI so the same text can be written to a file or copied to
/// the clipboard without a form being involved.
/// </summary>
public sealed record FindingsReport(
    string CaseReference,
    int TenantId,
    string ModelId,
    DateTimeOffset RunAt,
    IReadOnlyList<CheckFinding> Findings,
    CanonicalModelDocument? Model)
{
    public int Count(CheckOutcome outcome) => Findings.Count(f => f.ParsedOutcome == outcome);

    public TokenUsage TotalUsage => Findings.Aggregate(TokenUsage.Empty, (total, f) => new TokenUsage(
        total.InputTokens + f.Usage.InputTokens,
        total.OutputTokens + f.Usage.OutputTokens,
        total.CacheWriteTokens + f.Usage.CacheWriteTokens,
        total.CacheReadTokens + f.Usage.CacheReadTokens));

    /// <summary>A one-line count of the outcomes, for the status bar.</summary>
    public string Headline =>
        $"{Count(CheckOutcome.PotentialConcern)} potential concern(s), "
        + $"{Count(CheckOutcome.NoIssue)} no issue, "
        + $"{Count(CheckOutcome.NotApplicable)} N/A"
        + (Count(CheckOutcome.Error) > 0 ? $", {Count(CheckOutcome.Error)} error(s)" : string.Empty);

    /// <summary>
    /// The full report. Concerns come first and in full; passes are summarised. A reviewer
    /// opens this to find what needs attention, not to read ten clean checks.
    /// </summary>
    public string Format()
    {
        var sb = new StringBuilder();
        var rule = new string('=', 78);

        sb.AppendLine(rule);
        sb.AppendLine($"QA FINDINGS — case {CaseReference} (tenant {TenantId})");
        sb.AppendLine($"Run {RunAt:yyyy-MM-dd HH:mm}    Model {ModelId}");

        if (Model is not null)
        {
            sb.AppendLine(
                $"Canonical model extracted {Model.ExtractedAt:yyyy-MM-dd HH:mm} from "
                + $"{string.Join(", ", Model.SourceDocuments)} (schema v{Model.SchemaVersion})");
        }

        sb.AppendLine(rule);
        sb.AppendLine();
        sb.AppendLine($"SUMMARY: {Headline}");
        sb.AppendLine();

        foreach (var finding in Findings)
        {
            sb.AppendLine(
                $"  {CheckFinding.Describe(finding.ParsedOutcome),-18} {finding.CheckId,-9} {finding.CheckName}");
        }

        sb.AppendLine();

        // Concerns and errors in full, in check order; everything else stays folded away.
        var detailed = Findings
            .Where(f => f.ParsedOutcome is CheckOutcome.PotentialConcern or CheckOutcome.Error)
            .ToList();

        if (detailed.Count > 0)
        {
            sb.AppendLine(rule);
            sb.AppendLine("FINDINGS REQUIRING ATTENTION");
            sb.AppendLine(rule);

            foreach (var finding in detailed)
            {
                AppendDetail(sb, finding, full: true);
            }
        }

        var cleared = Findings
            .Where(f => f.ParsedOutcome is CheckOutcome.NoIssue or CheckOutcome.NotApplicable)
            .ToList();

        if (cleared.Count > 0)
        {
            sb.AppendLine(rule);
            sb.AppendLine("CHECKS CLEARED");
            sb.AppendLine(rule);

            foreach (var finding in cleared)
            {
                AppendDetail(sb, finding, full: false);
            }
        }

        sb.AppendLine(rule);
        sb.AppendLine(
            $"Retrieval: {Findings.Sum(f => f.SearchesRun)} search(es), "
            + $"{Findings.Sum(f => f.PassagesRetrieved)} passage(s), "
            + $"{Findings.Sum(f => f.CanonicalPathsResolved)} canonical path(s) resolved, "
            + $"{Findings.Sum(f => f.CanonicalPathsMissing)} absent.");
        sb.AppendLine(
            $"Tokens: {TotalUsage.TotalTokens:N0} "
            + $"(the suitability report was read once at extraction, not per check).");

        return sb.ToString();
    }

    private static void AppendDetail(StringBuilder sb, CheckFinding finding, bool full)
    {
        sb.AppendLine();
        sb.AppendLine($"{finding.CheckId} — {finding.CheckName}");
        sb.AppendLine($"Outcome: {CheckFinding.Describe(finding.ParsedOutcome)}");

        if (!string.IsNullOrWhiteSpace(finding.Summary))
        {
            sb.AppendLine();
            sb.AppendLine(Indent(finding.Summary, "  "));
        }

        if (!full)
        {
            sb.AppendLine();
            return;
        }

        var groups = finding.Groups
            .Where(g => g.ParsedOutcome != CheckOutcome.NoIssue)
            .ToList();

        foreach (var group in groups)
        {
            sb.AppendLine();
            sb.AppendLine($"  [{group.GroupId}] {group.Requirement}");
            sb.AppendLine($"  {CheckFinding.Describe(group.ParsedOutcome)}"
                + (string.IsNullOrWhiteSpace(group.Severity) ? string.Empty : $" — severity {group.Severity}"));
            sb.AppendLine(Indent(group.Explanation, "    "));

            foreach (var citation in group.Citations)
            {
                var category = string.IsNullOrWhiteSpace(citation.Category) ? string.Empty : $" [{citation.Category}]";
                sb.AppendLine($"      · {citation.Source}{category}");

                if (!string.IsNullOrWhiteSpace(citation.Quote))
                {
                    sb.AppendLine(Indent($"\"{citation.Quote.Trim()}\"", "        "));
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine(
            $"  ({finding.SearchesRun} search(es), {finding.PassagesRetrieved} passage(s), "
            + $"{finding.CanonicalPathsResolved} model path(s) resolved, {finding.CanonicalPathsMissing} absent, "
            + $"{finding.Elapsed.TotalSeconds:0.0}s)");
        sb.AppendLine();
    }

    /// <summary>Indents wrapped text so a multi-line explanation stays inside its section.</summary>
    private static string Indent(string text, string prefix) =>
        string.Join(
            Environment.NewLine,
            text.Replace("\r\n", "\n").Split('\n').Select(line => prefix + line.TrimEnd()));
}
