using System.Globalization;

namespace AiPromptEvaluator;

/// <summary>How well one source served a requirement.</summary>
public enum CoverageSignal
{
    /// <summary>The assessor read it.</summary>
    Read,

    /// <summary>It was retrieved and then dropped before the assessor saw it.</summary>
    Partial,

    /// <summary>It was searched for and nothing came back.</summary>
    Absent,

    /// <summary>It should have been readable and was not — an extraction gap, not a case gap.</summary>
    Unreadable,
}

/// <summary>One line of the coverage breakdown, in the words a reviewer would use.</summary>
public sealed record CoverageItem(string Label, CoverageSignal Signal);

/// <summary>
/// How far a finding's own working can be relied on, scored from the run.
///
/// <b>Counted, never asked for.</b> A model invited to rate its own confidence rates it high,
/// and the number then travels beside the finding looking like evidence. Every deduction here is
/// something the app established independently: a quotation the verifier could not find, a
/// comparison the assessor itself declared it could not make, a requirement it rewrote, a
/// difference it named and did not carry. A finding with none of those scores full marks because
/// nothing went wrong, not because it said so.
/// </summary>
public sealed record AgentCoverage(int Score, string Level, IReadOnlyList<string> Reasons)
{
    /// <summary>The one-line summary the report prints under the score.</summary>
    public string Reason =>
        Reasons.Count == 0
            ? "Both sides stated, the comparison closed, and every quotation traced back to the pack."
            : string.Join(" ", Reasons);

    /// <summary>A requirement with no finding at all — the run did not get that far.</summary>
    public static readonly AgentCoverage NotAssessed =
        new(0, "None", ["No finding was recorded for this requirement."]);
}

/// <summary>
/// How much of what the plan asked for actually reached the assessor.
///
/// <b>Per source, because a single percentage hides the only distinction that matters.</b> A
/// requirement that reached nothing because the case file holds nothing has found something; one
/// that reached nothing because ranking evicted it has found nothing and does not know. Those
/// two produce the same finding and the same score, and only the breakdown tells them apart —
/// which is why the design shows the list rather than the number.
/// </summary>
public sealed record EvidenceCoverage(int Score, string Level, IReadOnlyList<CoverageItem> Items);

/// <summary>
/// Computes both coverages for one archived requirement.
///
/// <b>One definition, used twice.</b> The scores are written to the archive when a run is filed,
/// so they can be queried without reading every finding; the breakdown is recomputed here when
/// a report is rendered, from the queries, passages and canonical paths the archive already
/// holds. Both call this, so they cannot disagree — and where an archived score was written by
/// an older build, the report shows the recomputed one, which is the one whose working is
/// visible on the page.
/// </summary>
public static class FindingCoverage
{
    // The design's bands. Agent coverage is green/blue/amber at 80 and 60; evidence coverage is
    // Full/Partial/Limited at 90 and 60.
    private const int AgentHigh = 80;
    private const int AgentMedium = 60;
    private const int EvidenceFull = 90;
    private const int EvidencePartial = 60;

    /// <summary>What the assessor's working is worth, and why.</summary>
    public static AgentCoverage Agent(RecordedGroup group)
    {
        if (group.Finding is not { } finding)
        {
            return AgentCoverage.NotAssessed;
        }

        var score = 100;
        var reasons = new List<string>();

        // The largest deduction, because it is the failure the verifier exists for: a finding
        // resting on a quotation that is not in its own evidence cannot be relied on even where
        // the conclusion happens to be right.
        switch (finding.Confidence)
        {
            case CitationConfidence.Unverified when finding.Citations.Count > 0:
                score -= 40;
                reasons.Add($"None of its {finding.Citations.Count} citation(s) could be traced to the pack.");
                break;

            case CitationConfidence.PartiallyVerified:
                score -= 20;
                reasons.Add(
                    $"{finding.UnverifiedQuotes.Count} of {finding.Citations.Count} citation(s) "
                    + "could not be traced to the pack.");
                break;
        }

        if (finding.Citations.Count == 0 && !group.Passages.Any(p => p.InPack))
        {
            score -= 10;
            reasons.Add("It cites nothing, and there was nothing in its pack to cite.");
        }

        if (!finding.ComparisonPerformed)
        {
            score -= 25;
            reasons.Add(finding.MissingInputs.Count > 0
                ? "It could not close the comparison: " + string.Join("; ", finding.MissingInputs) + "."
                : "It reported that the comparison could not be made.");
        }

        if (finding.EvidenceShortfall is { Length: > 0 })
        {
            score -= 15;
            reasons.Add("The pack reached fewer document categories than the plan asked for.");
        }

        if (finding.GroupIdDiverged || finding.RequirementDiverged)
        {
            score -= 10;
            reasons.Add(finding.GroupIdDiverged && finding.RequirementDiverged
                ? "It echoed a different requirement id and rewrote the requirement text."
                : finding.GroupIdDiverged
                    ? "It echoed a different requirement id."
                    : "It rewrote the requirement text.");
        }

        if (finding.DiscardedDiscrepancies.Count > 0)
        {
            score -= 10;
            reasons.Add(
                $"It named {finding.DiscardedDiscrepancies.Count} difference(s) and then passed.");
        }

        score = Math.Clamp(score, 0, 100);

        return new AgentCoverage(
            score,
            score >= AgentHigh ? "High" : score >= AgentMedium ? "Medium" : "Low",
            reasons);
    }

    /// <summary>What reached the assessor, source by source.</summary>
    public static EvidenceCoverage Evidence(RecordedGroup group)
    {
        var items = new List<CoverageItem>();

        // A requirement with no evidence query is assessed from the report alone. That is a
        // deliberate plan shape — internal contradictions and charge arithmetic are properties
        // of the report — and scoring it as uncovered would turn a sound finding into a gap.
        if (group.Queries.Count == 0)
        {
            items.Add(new CoverageItem(
                "Assessed from the report alone — no supporting document was searched for",
                CoverageSignal.Read));

            AppendCanonical(group, items);

            var modelOnlyScore = items.Any(i => i.Signal == CoverageSignal.Unreadable) ? 70 : 100;
            return new EvidenceCoverage(modelOnlyScore, Level(modelOnlyScore), items);
        }

        // What the plan asked for, in the order the categories were first named, so the list
        // reads like the plan rather than like a hash.
        var wanted = group.Queries
            .SelectMany(q => q.TargetCategories
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var earned = 0d;

        foreach (var code in wanted)
        {
            var fromCategory = group.Passages
                .Where(p => string.Equals(p.CategoryCode, code, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var name = DocumentCategory.NameForCode(code);

            if (fromCategory.Any(p => p.InPack))
            {
                items.Add(new CoverageItem($"{name} read", CoverageSignal.Read));
                earned += 1;
            }
            else if (fromCategory.Count > 0)
            {
                items.Add(new CoverageItem(
                    $"{name} retrieved, then evicted in ranking", CoverageSignal.Partial));
                earned += 0.5;
            }
            else
            {
                items.Add(new CoverageItem($"{name} returned no passage", CoverageSignal.Absent));
            }
        }

        // Categories the plan never asked for but the searches reached anyway. Counted as read
        // rather than ignored: a contradiction sitting in a category nobody thought to name is
        // exactly what the unrestricted half of every search exists to find.
        foreach (var extra in group.Passages
                     .Where(p => p.InPack)
                     .Select(p => p.CategoryCode)
                     .Where(c => !string.IsNullOrWhiteSpace(c))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Where(c => !wanted.Contains(c, StringComparer.OrdinalIgnoreCase))
                     .OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
        {
            items.Add(new CoverageItem(
                $"{DocumentCategory.NameForCode(extra)} read (not asked for)", CoverageSignal.Read));
        }

        var barren = group.Queries.Where(q => q.Hits == 0).ToList();
        if (barren.Count > 0)
        {
            items.Add(new CoverageItem(
                $"{barren.Count} of {group.Queries.Count} search(es) returned nothing",
                CoverageSignal.Absent));
        }

        AppendCanonical(group, items);

        var score = wanted.Count == 0
            ? (group.Passages.Any(p => p.InPack) ? 100 : 0)
            : (int)Math.Round(earned / wanted.Count * 100, MidpointRounding.AwayFromZero);

        // An unreadable canonical path is not a thin case file, it is a broken extraction — so
        // it caps the score rather than sliding it, and says which of the two this is.
        if (items.Any(i => i.Signal == CoverageSignal.Unreadable))
        {
            score = Math.Min(score, EvidenceFull - 1);
        }

        score = Math.Clamp(score, 0, 100);

        return new EvidenceCoverage(score, Level(score), items);
    }

    private static void AppendCanonical(RecordedGroup group, List<CoverageItem> items)
    {
        var missing = group.Facts.Count(f => !f.Found);

        if (missing == 0)
        {
            if (group.Facts.Count > 0)
            {
                items.Add(new CoverageItem(
                    $"{group.Facts.Count} canonical path(s) resolved", CoverageSignal.Read));
            }

            return;
        }

        items.Add(new CoverageItem(
            $"{missing} of {group.Facts.Count} canonical path(s) absent from the extracted model",
            CoverageSignal.Unreadable));
    }

    private static string Level(int score) =>
        score >= EvidenceFull ? "Full" : score >= EvidencePartial ? "Partial" : "Limited";

    /// <summary>The score as it reads on a card.</summary>
    public static string Percent(int score) =>
        score.ToString(CultureInfo.InvariantCulture) + "%";
}
