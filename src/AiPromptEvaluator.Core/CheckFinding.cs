using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.AI;

namespace AiPromptEvaluator;

/// <summary>
/// How far a finding's quotations could be traced back to the evidence it was shown. Reported
/// beside the outcome rather than folded into it: what the evidence supports and how far the
/// working can be traced are different questions, and answering the second by overwriting the
/// first cost a whole run its No Issue verdicts.
/// </summary>
public enum CitationConfidence
{
    /// <summary>Every quotation of checkable length was found in the evidence.</summary>
    Verified,

    /// <summary>Some quotations were found and some were not.</summary>
    PartiallyVerified,

    /// <summary>No quotation could be traced. Read this finding before the others.</summary>
    Unverified,
}

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

    /// <summary>
    /// The individual values read out of a table, when the assertion is a row rather than a
    /// sentence — an alternative to <see cref="Quote"/>, not a companion to it.
    ///
    /// This exists because most of the evidence in these cases is converted tables, and a
    /// quotation is the wrong shape for one. Asked to support "the £6,000 savings are held by
    /// JS in a cash account", a model reproduced the row as prose — <c>"Savings JS Cash Account
    /// 6,000"</c> — which is an accurate reading and a fabricated quotation, because no such
    /// sentence exists. Measured across one run, **81% of all citation failures were this**:
    /// not invention, but a table restated in a form no verifier could match.
    ///
    /// Loosening the matcher to accept them was tried and rejected — a rule tolerant enough
    /// admits an altered digit, which is the failure the verifier exists for. So the model is
    /// given a way to say what it actually did instead: name the passage and list the cells it
    /// read. Each cell is then checked against that passage, which is a stronger claim than a
    /// reflowed quotation and a checkable one.
    /// </summary>
    [JsonPropertyName("cells")] public List<string>? Cells { get; init; }

    /// <summary>True when this citation reads a table rather than quoting a span.</summary>
    [JsonIgnore] public bool IsTableRead =>
        string.IsNullOrWhiteSpace(Quote) && Cells is { Count: > 0 };

    /// <summary>How the citation reads in a report, whichever form it took.</summary>
    public string Describe() =>
        IsTableRead ? string.Join("  |  ", Cells!) : Quote ?? string.Empty;
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
    /// What the model echoed back for <c>groupId</c> and <c>requirement</c>, kept after the
    /// plan's values have been substituted.
    ///
    /// Neither is used to decide anything — they are a prompt-adherence measure. In one run,
    /// 22 of 60 responses returned a check id where a group id was asked for, and 25 rewrote
    /// the requirement. That is a third of responses losing track of which requirement they
    /// were answering while still answering it, which is worth knowing before it costs
    /// something rather than after.
    /// </summary>
    /// <summary>
    /// Set by the runner, never by the assessor: the group's plan asked for corroboration from
    /// at least N document categories and the pack reached fewer.
    ///
    /// The minimum was printed into the prompt for months beside the categories actually
    /// reached, and the comparison left to prose — so whether a thinly evidenced requirement
    /// said so depended on the model noticing two numbers in different parts of a long prompt.
    /// Both operands are known in code, so the shortfall is stated in code.
    /// </summary>
    [JsonIgnore] public string? EvidenceShortfall { get; init; }

    [JsonIgnore] public string? EchoedGroupId { get; init; }

    /// <inheritdoc cref="EchoedGroupId"/>
    [JsonIgnore] public string? EchoedRequirement { get; init; }

    /// <summary>True when the model did not echo back the group id it was asked about.</summary>
    public bool GroupIdDiverged =>
        !string.IsNullOrWhiteSpace(EchoedGroupId)
        && !string.Equals(EchoedGroupId, GroupId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the model rewrote the requirement rather than echoing it. A leading "[G3.1] "
    /// is ignored: prefixing the identifier is a formatting habit, not a redefinition.
    /// </summary>
    public bool RequirementDiverged
    {
        get
        {
            if (string.IsNullOrWhiteSpace(EchoedRequirement) || string.IsNullOrWhiteSpace(Requirement))
            {
                return false;
            }

            var echoed = EchoedRequirement.AsSpan().Trim();

            if (echoed.StartsWith("[") && echoed.IndexOf(']') > 0)
            {
                echoed = echoed[(echoed.IndexOf(']') + 1)..].Trim();
            }

            return !echoed.StartsWith(Requirement.AsSpan().Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// True when the group could not complete its comparison but nonetheless named a
    /// difference between the two sides. Whatever it could not close, it compared something.
    ///
    /// The distinction matters because <c>comparisonPerformed</c> is a field the model fills
    /// in, and reading it as a veto let the model overrule its own verdict. Over sixty groups
    /// of one run, twenty set it false — and of those, thirteen had written Potential Concern
    /// and eleven had written it at High severity. Seventy-one discrepancies were reported and
    /// then filed as unassessable. One of them was the recommendation retaining a fund rated 9
    /// against an agreed attitude to risk of 5, discarded because the group could not establish
    /// how the provider's scale mapped onto the firm's.
    /// </summary>
    public bool ComparedSomething => Discrepancies.Count > 0;

    /// <summary>
    /// Differences this group named and then did not carry into its verdict.
    ///
    /// A group that lists discrepancies and answers No Issue is contradicting itself, and until
    /// now it did so silently: the report prints only groups that raised a concern, so the
    /// discrepancies went with the group. Measured across the four scored runs, **9 to 15 groups
    /// per run** passed while naming between 27 and 51 differences, and no analysis counted them.
    ///
    /// One of them is a benchmark finding. CHK-003/G3.6 wrote *"P11 file notes state 'Risk rating
    /// of 6' … contradicting all other sources"* — the contradiction, correctly, both sides named
    /// — and returned No Issue, so the string never reached the report. The same group found the
    /// same thing in the run before and answered Potential Concern, which is the only reason it
    /// was ever seen.
    ///
    /// **Most of these are immaterial and that is the point.** A group is entitled to notice a
    /// £0.02 variance and decide it is not a concern. What it may not do is decide that where a
    /// reader cannot see it. So this does not change the verdict — <see cref="ParsedOutcome"/> is
    /// untouched — it only refuses to let the working disappear with it.
    /// </summary>
    public IReadOnlyList<string> DiscardedDiscrepancies =>
        ParsedOutcome is CheckOutcome.NoIssue or CheckOutcome.NotApplicable
            ? Discrepancies
            : [];

    /// <summary>
    /// The outcome as it stands after the deterministic checks, which can only ever move it
    /// away from a pass:
    ///
    /// a comparison that could not be performed <em>and found nothing to report</em> is
    /// Indeterminate whatever the model concluded; and a finding resting on a quote that is not
    /// in its own evidence is a Potential Concern, because the reasoning cannot be relied on
    /// even where the conclusion happens to be right.
    ///
    /// A group that could not close its comparison but did name discrepancies keeps its
    /// outcome, and carries <see cref="MissingInputs"/> as a caveat rather than as a verdict.
    /// "I could not compare" and "I compared, and separately could not close one input" are
    /// different answers, and only the first is unassessable.
    /// </summary>
    /// A finding that <em>passes</em> on the strength of a quotation which is not in its own
    /// evidence is still forced to Potential Concern. That is the failure this whole mechanism
    /// exists for: an assessor handed "a Risk rating of 6" reported "a Risk rating of 5" and
    /// cleared the check. Moving that onto <see cref="Confidence"/> alone was tried and
    /// reverted — it let the altered quotation clear the check again, which is the entire
    /// defect.
    ///
    /// What did move is everything else. A finding that already reports a concern is not made
    /// more concerning by a citation that will not match, and the run where this rule converted
    /// all nine No Issue verdicts was suffering from a broken matcher, not from nine bad
    /// findings. The fix for the count is the matcher; the fix for a fabricated pass is this.
    public CheckOutcome ParsedOutcome
    {
        get
        {
            if (!ComparisonPerformed && !ComparedSomething)
            {
                return CheckOutcome.Indeterminate;
            }

            var stated = CheckFinding.ParseOutcome(Outcome);

            return UnverifiedQuotes.Count > 0
                   && stated is CheckOutcome.NoIssue or CheckOutcome.NotApplicable
                ? CheckOutcome.PotentialConcern
                : stated;
        }
    }

    /// <summary>
    /// How far this finding's quotations could be traced back to the evidence it was given.
    ///
    /// Confidence and verdict are different questions. The verdict is what the evidence
    /// supports; the confidence is how far the working can be traced. They were conflated:
    /// any unverified quote forced a Potential Concern, and at a 36% quote-failure rate that
    /// converted every one of a run's nine No Issue verdicts, so the report announced "0 no
    /// issue" and a reader could not tell an assessed concern from a formatting artefact.
    ///
    /// Reporting confidence separately makes the working visible without overwriting the
    /// verdict — but only for findings that already report something. A <em>pass</em> resting
    /// on an untraceable quotation is still downgraded; see
    /// <see cref="ParsedOutcome"/> for why that half cannot move here.
    /// </summary>
    public CitationConfidence Confidence =>
        UnverifiedQuotes.Count == 0 ? CitationConfidence.Verified
        : UnverifiedQuotes.Count >= Citations.Count ? CitationConfidence.Unverified
        : CitationConfidence.PartiallyVerified;
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

    /// <summary>
    /// Section hints from this check's plan that matched no retrieved passage, as
    /// <c>"G1.1: Residency"</c>. A hint that matches nothing is a typo wearing the costume of a
    /// working feature; see <see cref="CheckPlanRunner.UnmatchedSections"/>.
    /// </summary>
    [JsonIgnore] public IReadOnlyList<string> UnmatchedSections { get; init; } = [];

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

            // A group that could not close its comparison may still have named a difference on
            // the way. Those used to reach the detail body and nothing else, so the summary a
            // reviewer actually reads listed the group ids and threw the findings away.
            var noted = indeterminate.SelectMany(g => g.Discrepancies).Take(2).ToList();

            if (noted.Count > 0)
            {
                sb.Append("Those groups still noted: ")
                  .Append(string.Join(" ", noted.Select(d => d.TrimEnd('.') + "."))).Append(' ');
            }
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
                "required": ["passageId", "source", "category", "quote", "cells"],
                "properties": {
                  "passageId": { "type": ["string", "null"] },
                  "source": { "type": "string" },
                  "category": { "type": ["string", "null"] },
                  "quote": { "type": ["string", "null"] },
                  "cells": {
                    "type": ["array", "null"],
                    "items": { "type": "string" },
                    "description": "The values read from a table row, when the evidence is a table and quoting it as a sentence would not be a quotation. Use instead of quote, never as well."
                  }
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
    RunFingerprint? Fingerprint = null,
    TimeSpan RunDuration = default)
{
    public int Count(CheckOutcome outcome) => Findings.Count(f => f.ParsedOutcome == outcome);

    /// <summary>
    /// Whether the run's output could have been generated in the time the run took. A report
    /// built without a measured duration reports <see cref="RunAuthenticity.Unknown"/>, which
    /// accuses nothing.
    /// </summary>
    public RunAuthenticity Authenticity => new(TotalUsage.OutputTokens, RunDuration);

    public TokenUsage TotalUsage => Findings.Aggregate(TokenUsage.Empty, (total, f) => new TokenUsage(
        total.InputTokens + f.Usage.InputTokens,
        total.OutputTokens + f.Usage.OutputTokens,
        total.CacheWriteTokens + f.Usage.CacheWriteTokens,
        total.CacheReadTokens + f.Usage.CacheReadTokens));

    /// <summary>A one-line count of the outcomes, for the status bar.</summary>
    public string Headline =>
        (Authenticity.IsReplay ? "REPLAY (cached) — " : string.Empty)
        + $"{Count(CheckOutcome.PotentialConcern)} potential concern(s), "
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
    /// Groups that named a difference and then passed, and how many differences went with them.
    ///
    /// Surfaced because it was invisible. Recall is scored from the printed report, so a finding
    /// a group raised and discarded is indistinguishable from one it never found — and across
    /// four runs this happened 9 to 15 times each, carrying 27 to 51 differences with it. Until
    /// this number exists there is no way to tell which of the two failures a run is having.
    /// </summary>
    public (int Groups, int Discrepancies) DiscardedDiscrepancies
    {
        get
        {
            var discarding = Findings
                .SelectMany(f => f.Groups)
                .Where(g => g.DiscardedDiscrepancies.Count > 0)
                .ToList();

            return (discarding.Count, discarding.Sum(g => g.DiscardedDiscrepancies.Count));
        }
    }

    /// <summary>
    /// Responses that did not echo back the group id or the requirement they were asked about.
    /// Nothing downstream depends on either — the plan's values are used — but a climbing
    /// count means the assessor is losing track of the question, and that is worth a number.
    /// </summary>
    public (int GroupIds, int Requirements, int Total) Adherence
    {
        get
        {
            var groups = Findings.SelectMany(f => f.Groups).ToList();

            return (
                groups.Count(g => g.GroupIdDiverged),
                groups.Count(g => g.RequirementDiverged),
                groups.Count);
        }
    }

    /// <summary>
    /// The full report. Concerns come first and in full; passes are summarised. A reviewer
    /// opens this to find what needs attention, not to read ten clean checks.
    /// </summary>
    public string Format()
    {
        var sb = new StringBuilder();
        var rule = new string('=', 78);

        sb.AppendLine(rule);

        // Before anything else. A replay carries a fresh timestamp, a fresh cost line and
        // findings that look generated, so a reader who reaches the summary without being told
        // has already been misled.
        if (Authenticity.IsReplay)
        {
            sb.AppendLine(Authenticity.Banner);
            sb.AppendLine(rule);
        }

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

        if (RunDuration > TimeSpan.Zero)
        {
            sb.AppendLine(
                $"Wall clock: {RunDuration.TotalSeconds:N0}s for {TotalUsage.OutputTokens:N0} "
                + $"output token(s) — {Authenticity.TokensPerSecond:N0} tok/s."
                + (Authenticity.IsReplay ? "  *** REPLAY — see the head of this report. ***" : string.Empty));
        }

        if (UnverifiedCitations > 0)
        {
            // Framed as trust rather than as coverage, because it has repeatedly been read as
            // the latter. Across four runs this rate went 36% -> 32% -> 14% -> 11% while the
            // findings the pipeline reached did not change at all. It measures whether a
            // reviewer can follow the working, which is worth having and is not progress
            // toward finding more.
            sb.AppendLine(
                $"Citation trust: {UnverifiedCitations} quote(s) could not be traced to the "
                + "evidence they were drawn from. Those findings are marked and should be read "
                + "first; any that would otherwise have passed were downgraded. This measures "
                + "how far the working can be checked, not how much was found.");
        }

        var unmatched = Findings.SelectMany(f => f.UnmatchedSections).ToList();

        if (unmatched.Count > 0)
        {
            // Loud, because the failure mode is silence. A plan asking for a section that does
            // not exist behaves exactly like a plan not asking for one, and a whole run went by
            // before anyone could tell which had happened.
            sb.AppendLine(
                $"Section hints matching nothing: {unmatched.Count} — "
                + string.Join("; ", unmatched)
                + ". These asked for part of a document and reached no passage, so they had no "
                + "effect. Check the wording against the converted document.");
        }

        var (discarding, discarded) = DiscardedDiscrepancies;

        if (discarding > 0)
        {
            // Counted because it was invisible, and invisible in a way that corrupted the only
            // measure the project has: a difference a group raised and dropped reads, in the
            // printed report, exactly like one it never found.
            sb.AppendLine(
                $"Raised and not carried: {discarding} group(s) named {discarded} difference(s) "
                + "and then passed. Printed under the check that passed. Most are immaterial; "
                + "one of them, in an earlier run, was a benchmark finding.");
        }

        // Read across the finished findings rather than within any one of them, because the
        // check catalogue is exactly what splits these contradictions in half.
        sb.Append(CrossGroupContradictions.Format(CrossGroupContradictions.In(Findings)));

        var (ids, requirements, total) = Adherence;

        if (ids > 0 || requirements > 0)
        {
            sb.AppendLine(
                $"Prompt adherence: of {total} response(s), {ids} echoed the wrong requirement id "
                + $"and {requirements} rewrote the requirement text. The plan's values were used "
                + "for both.");
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
            // A cleared check is summarised rather than printed — but if one of its groups
            // named a difference on the way to passing, that goes in the summary. Folding it
            // away with the rest is exactly how a raised finding disappears.
            AppendDiscarded(sb, finding);
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
                + (string.IsNullOrWhiteSpace(group.Severity) ? string.Empty : $" — severity {group.Severity}")
                + (group.Confidence == CitationConfidence.Verified
                    ? string.Empty
                    : $" — citations {(group.Confidence == CitationConfidence.Unverified ? "unverified" : "partly verified")}"));

            // Established in code, so it is printed whether or not the assessor mentioned it.
            // A requirement judged on one category where the plan asked for two is a fact about
            // the run, and a reviewer should not have to infer it from the passage list.
            if (!string.IsNullOrWhiteSpace(group.EvidenceShortfall))
            {
                sb.AppendLine(Indent($"Evidence: {group.EvidenceShortfall}", "    "));
            }

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

                if (citation.IsTableRead)
                {
                    // Rendered as a row rather than in quotation marks, because it is not a
                    // quotation and presenting it as one is the habit this replaced.
                    sb.AppendLine(Indent($"table: {citation.Describe()}", "        "));
                }
                else if (!string.IsNullOrWhiteSpace(citation.Quote))
                {
                    sb.AppendLine(Indent($"\"{citation.Quote.Trim()}\"", "        "));
                }
            }
        }

        AppendDiscarded(sb, finding);

        sb.AppendLine();
        sb.AppendLine(
            $"  ({finding.SearchesRun} search(es), {finding.PassagesRetrieved} passage(s), "
            + $"{finding.CanonicalPathsResolved} model path(s) resolved, {finding.CanonicalPathsMissing} absent, "
            + $"{finding.Elapsed.TotalSeconds:0.0}s)");
        sb.AppendLine();
    }

    /// <summary>
    /// Differences a group named on its way to passing, printed under the check that passed.
    ///
    /// Deliberately not presented as findings. Most are immaterial — a £0.02 variance a group
    /// was right to wave through — and promoting them all to concerns would flood the report and
    /// destroy the check-level signal. What they are is *the group's own working*, and the
    /// failure this fixes is that it was being deleted along with the group.
    /// </summary>
    private static void AppendDiscarded(StringBuilder sb, CheckFinding finding)
    {
        var discarding = finding.Groups
            .Where(g => g.DiscardedDiscrepancies.Count > 0)
            .ToList();

        if (discarding.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("    RAISED AND NOT CARRIED — named by a group that then passed. Not");
        sb.AppendLine("    findings; read them to check nothing material was waved through.");

        foreach (var group in discarding)
        {
            sb.AppendLine(
                $"      [{group.GroupId}] {CheckFinding.Describe(group.ParsedOutcome)}");

            foreach (var discrepancy in group.DiscardedDiscrepancies)
            {
                sb.AppendLine(Indent($"- {discrepancy}", "        "));
            }
        }
    }

    /// <summary>Indents wrapped text so a multi-line explanation stays inside its section.</summary>
    private static string Indent(string text, string prefix) =>
        string.Join(
            Environment.NewLine,
            text.Replace("\r\n", "\n").Split('\n').Select(line => prefix + line.TrimEnd()));
}
