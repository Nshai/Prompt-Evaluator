using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.AI;

namespace AiPromptEvaluator;

/// <summary>The outcomes a check can carry.</summary>
public enum CheckOutcome
{
    NoIssue,
    PotentialConcern,
    NotApplicable,

    /// <summary>
    /// The comparison could not be made — a value the requirement depends on was not available,
    /// so there is nothing to compare.
    ///
    /// This exists because its absence was causing invented answers. Given only three outcomes,
    /// an assessor missing the date of birth it was told to derive an age from has no way to say
    /// so, and the observed behaviour was to manufacture a reconciliation instead. A schema with
    /// no slot for "I cannot tell" guarantees that the space gets filled.
    /// </summary>
    Indeterminate,

    /// <summary>The run itself failed. Kept distinct so a broken run never reads as a pass.</summary>
    Error,
}

/// <summary>
/// One cited piece of evidence behind a finding.
///
/// <see cref="PassageId"/> is what makes a citation checkable: passages are handed to the
/// assessor already numbered, so a citation names one rather than describing it, and the quote
/// can be verified against that passage's actual text.
/// </summary>
public sealed record FindingCitation
{
    [JsonPropertyName("passageId")] public string? PassageId { get; init; }
    [JsonPropertyName("source")] public string Source { get; init; } = string.Empty;
    [JsonPropertyName("category")] public string? Category { get; init; }
    [JsonPropertyName("quote")] public string? Quote { get; init; }
}

/// <summary>
/// What the assessor concluded about one group of the plan.
///
/// The property order is load-bearing. A JSON-schema response format emits properties in schema
/// order, so the order here is the order the model generates in — and the model conditions on
/// what it has already written. With the verdict first, everything after it is written to
/// justify a token already committed, which is exactly how a contradiction gets explained away.
///
/// So the sequence is: state what each side says, list the discrepancies, declare whether the
/// comparison could be made at all, reason, cite — and only then decide.
/// </summary>
public sealed record GroupFinding
{
    [JsonPropertyName("groupId")] public string GroupId { get; init; } = string.Empty;
    [JsonPropertyName("requirement")] public string? Requirement { get; init; }

    /// <summary>What the canonical model asserts, in the assessor's own words.</summary>
    [JsonPropertyName("reportSays")] public string ReportSays { get; init; } = string.Empty;

    /// <summary>What the retrieved passages evidence.</summary>
    [JsonPropertyName("fileSays")] public string FileSays { get; init; } = string.Empty;

    /// <summary>
    /// Every difference between the two sides, listed before any judgement is made about
    /// whether they matter. Suppressing a finding then means omitting an item from a list
    /// rather than softening a paragraph — a more conspicuous act, and an auditable one.
    /// </summary>
    [JsonPropertyName("discrepancies")] public List<string> Discrepancies { get; init; } = [];

    /// <summary>False when a value the comparison depends on was not available.</summary>
    [JsonPropertyName("comparisonPerformed")] public bool ComparisonPerformed { get; init; } = true;

    /// <summary>What was missing, when <see cref="ComparisonPerformed"/> is false.</summary>
    [JsonPropertyName("missingInputs")] public List<string> MissingInputs { get; init; } = [];

    [JsonPropertyName("analysis")] public string Analysis { get; init; } = string.Empty;
    [JsonPropertyName("citations")] public List<FindingCitation> Citations { get; init; } = [];
    [JsonPropertyName("severity")] public string? Severity { get; init; }
    [JsonPropertyName("outcome")] public string Outcome { get; init; } = nameof(CheckOutcome.NoIssue);

    /// <summary>
    /// Quotes that could not be found in the passages this group was given. Set by
    /// <see cref="CitationVerifier"/> after the model has answered.
    /// </summary>
    [JsonIgnore] public IReadOnlyList<string> UnverifiedQuotes { get; init; } = [];

    /// <summary>
    /// The outcome as it stands after the deterministic checks, which can only ever move it
    /// away from a pass:
    ///
    /// a comparison that could not be performed is Indeterminate whatever the model concluded;
    /// and a finding resting on a quote that is not in its own evidence is a Potential Concern,
    /// because the reasoning cannot be relied on even where the conclusion happens to be right.
    /// </summary>
    public CheckOutcome ParsedOutcome =>
        !ComparisonPerformed ? CheckOutcome.Indeterminate
        : UnverifiedQuotes.Count > 0 ? CheckOutcome.PotentialConcern
        : CheckFinding.ParseOutcome(Outcome);
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
            "indeterminate" or "cannotassess" or "unknown" => CheckOutcome.Indeterminate,
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
        CheckOutcome.Indeterminate => "Indeterminate",
        _ => "Error",
    };

    /// <summary>
    /// Builds the check's finding from its groups.
    ///
    /// The check-level outcome is computed here rather than asked for. Previously the model
    /// stated it — and because a schema emits properties in order, it stated it *before*
    /// assessing a single group. Deriving it removes both the ordering problem and the
    /// possibility of a check outcome that does not follow from its own groups.
    ///
    /// Precedence runs worst-first: any concern makes the check a concern; failing that, a
    /// comparison that could not be made leaves the check Indeterminate rather than clear.
    /// </summary>
    public static CheckFinding FromGroups(
        string checkId,
        string checkName,
        IReadOnlyList<GroupFinding> groups,
        string? triggerNote = null)
    {
        var outcomes = groups.Select(g => g.ParsedOutcome).ToList();

        var outcome =
            outcomes.Contains(CheckOutcome.Error) ? CheckOutcome.Error
            : outcomes.Contains(CheckOutcome.PotentialConcern) ? CheckOutcome.PotentialConcern
            : outcomes.Contains(CheckOutcome.Indeterminate) ? CheckOutcome.Indeterminate
            : outcomes.Count > 0 && outcomes.All(o => o == CheckOutcome.NotApplicable) ? CheckOutcome.NotApplicable
            : CheckOutcome.NoIssue;

        return new CheckFinding
        {
            CheckId = checkId,
            CheckName = checkName,
            Groups = [.. groups],
            Summary = Summarise(groups, outcome, triggerNote),
            Outcome = outcome.ToString(),
        };
    }

    /// <summary>
    /// The check's headline, composed from the groups rather than written by the model — so it
    /// cannot say something the group findings do not support.
    /// </summary>
    private static string Summarise(
        IReadOnlyList<GroupFinding> groups, CheckOutcome outcome, string? triggerNote)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(triggerNote))
        {
            sb.Append(triggerNote!.Trim()).Append(' ');
        }

        var concerns = groups.Where(g => g.ParsedOutcome == CheckOutcome.PotentialConcern).ToList();
        var indeterminate = groups.Where(g => g.ParsedOutcome == CheckOutcome.Indeterminate).ToList();

        sb.Append(Describe(outcome)).Append(" across ").Append(groups.Count).Append(" requirement(s). ");

        if (concerns.Count > 0)
        {
            sb.Append(concerns.Count).Append(" raised a concern (")
              .Append(string.Join(", ", concerns.Select(g => g.GroupId))).Append("). ");

            var discrepancies = concerns.SelectMany(g => g.Discrepancies).Take(3).ToList();

            if (discrepancies.Count > 0)
            {
                sb.Append(string.Join(" ", discrepancies.Select(d => d.TrimEnd('.') + "."))).Append(' ');
            }
        }

        if (indeterminate.Count > 0)
        {
            sb.Append(indeterminate.Count)
              .Append(" could not be assessed for want of a value the comparison needs (")
              .Append(string.Join(", ", indeterminate.Select(g => g.GroupId))).Append("). ");
        }

        var unverified = groups.Sum(g => g.UnverifiedQuotes.Count);

        if (unverified > 0)
        {
            sb.Append(unverified)
              .Append(" cited quote(s) could not be found in the evidence supplied and were rejected. ");
        }

        return sb.ToString().TrimEnd();
    }

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
/// The JSON schema a check finding must satisfy, sent to the model as a response format so
/// the reply cannot arrive in a shape the app has to guess at.
///
/// This matters more than tidiness. <see cref="CheckFinding.ParseOutcome"/> reads anything it
/// does not recognise as a Potential Concern, which is the safe direction but means a rewording
/// of the outcome silently becomes a different outcome. Constraining the enum removes that
/// path: the model can only return one of the three values the checks define.
///
/// Written out by hand rather than generated from the type, because the strict dialect the
/// providers enforce is narrower than the type: every property has to be required, unset
/// values have to be nullable rather than omitted, and additional properties have to be
/// refused explicitly.
/// </summary>
public static class FindingSchema
{
    private const string Json =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["groupId", "requirement", "reportSays", "fileSays", "discrepancies",
                       "comparisonPerformed", "missingInputs", "analysis", "citations",
                       "severity", "outcome"],
          "properties": {
            "groupId": { "type": "string" },
            "requirement": { "type": "string" },
            "reportSays": { "type": "string" },
            "fileSays": { "type": "string" },
            "discrepancies": { "type": "array", "items": { "type": "string" } },
            "comparisonPerformed": { "type": "boolean" },
            "missingInputs": { "type": "array", "items": { "type": "string" } },
            "analysis": { "type": "string" },
            "citations": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["passageId", "source", "category", "quote"],
                "properties": {
                  "passageId": { "type": ["string", "null"] },
                  "source": { "type": "string" },
                  "category": { "type": ["string", "null"] },
                  "quote": { "type": ["string", "null"] }
                }
              }
            },
            "severity": {
              "anyOf": [
                { "type": "string", "enum": ["High", "Moderate", "Low"] },
                { "type": "null" }
              ]
            },
            "outcome": { "type": "string", "enum": ["NoIssue", "PotentialConcern", "NotApplicable"] }
          }
        }
        """;

    /// <summary>The schema as a <see cref="JsonElement"/>, parsed once.</summary>
    public static JsonElement Element { get; } = JsonDocument.Parse(Json).RootElement.Clone();

    /// <summary>
    /// The order the model will generate in — which is the order it reasons in, since each
    /// property is written conditioned on the ones before it. Exposed so a test can pin it.
    /// </summary>
    public static IReadOnlyList<string> EmissionOrder { get; } = Element
        .GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToList();

    /// <summary>The response format to send, or null when the endpoint should not be asked for one.</summary>
    public static ChatResponseFormat? ResponseFormat(bool enabled) =>
        enabled
            ? ChatResponseFormat.ForJsonSchema(
                Element,
                "qa_group_finding",
                "The assessment of one requirement: both sides, the discrepancies, then the outcome.")
            : null;
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
    CanonicalModelDocument? Model,
    RunFingerprint? Fingerprint = null)
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
        + (Count(CheckOutcome.Indeterminate) > 0
            ? $", {Count(CheckOutcome.Indeterminate)} not assessable"
            : string.Empty)
        + (Count(CheckOutcome.Error) > 0 ? $", {Count(CheckOutcome.Error)} error(s)" : string.Empty);

    /// <summary>
    /// Group findings whose citations could not be traced back to the evidence they were given.
    /// Surfaced at run level because one is a defect worth investigating and several is a
    /// pattern worth stopping the run for.
    /// </summary>
    public int UnverifiedCitations => Findings.Sum(f => f.Groups.Sum(g => g.UnverifiedQuotes.Count));

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

        // Everything that could make this run differ from the last one, on two lines. Diff
        // these before concluding that the assessor changed its mind.
        if (Fingerprint is not null)
        {
            sb.AppendLine(new string('-', 78));
            sb.AppendLine(Fingerprint.Format());
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
        // Indeterminate belongs here rather than with the cleared checks. A comparison that
        // could not be made is not a pass, and a report that folds it away with the passes is
        // how an unassessed requirement gets signed off as reviewed.
        var detailed = Findings
            .Where(f => f.ParsedOutcome is CheckOutcome.PotentialConcern
                                        or CheckOutcome.Indeterminate
                                        or CheckOutcome.Error)
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

        if (UnverifiedCitations > 0)
        {
            sb.AppendLine(
                $"Citations: {UnverifiedCitations} quote(s) could not be found in the evidence they "
                + "were drawn from. Those findings were downgraded and should be read first.");
        }

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

            // Both sides before the reasoning, mirroring the order the assessor answered in —
            // a reviewer checking a finding wants the two claims side by side first.
            if (!string.IsNullOrWhiteSpace(group.ReportSays))
            {
                sb.AppendLine(Indent($"Report:   {group.ReportSays}", "    "));
            }

            if (!string.IsNullOrWhiteSpace(group.FileSays))
            {
                sb.AppendLine(Indent($"File:     {group.FileSays}", "    "));
            }

            if (group.Discrepancies.Count > 0)
            {
                sb.AppendLine("    Discrepancies:");
                foreach (var discrepancy in group.Discrepancies)
                {
                    sb.AppendLine(Indent($"- {discrepancy}", "      "));
                }
            }

            if (!group.ComparisonPerformed)
            {
                sb.AppendLine(
                    "    NOT ASSESSED — the comparison needs a value that was not available: "
                    + (group.MissingInputs.Count > 0 ? string.Join(", ", group.MissingInputs) : "unspecified"));
            }

            if (!string.IsNullOrWhiteSpace(group.Analysis))
            {
                sb.AppendLine(Indent(group.Analysis, "    "));
            }

            // The loudest thing on the page when it happens. A quote that is not in the evidence
            // means the reasoning cannot be relied on, whatever conclusion it reached.
            if (group.UnverifiedQuotes.Count > 0)
            {
                sb.AppendLine(
                    $"    ** {group.UnverifiedQuotes.Count} QUOTE(S) NOT FOUND IN THE EVIDENCE SUPPLIED **");
                foreach (var quote in group.UnverifiedQuotes)
                {
                    sb.AppendLine(Indent($"\"{quote}\"", "        "));
                }
            }

            foreach (var citation in group.Citations)
            {
                var category = string.IsNullOrWhiteSpace(citation.Category) ? string.Empty : $" [{citation.Category}]";
                var passage = string.IsNullOrWhiteSpace(citation.PassageId) ? string.Empty : $" ({citation.PassageId})";
                sb.AppendLine($"      · {citation.Source}{category}{passage}");

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
