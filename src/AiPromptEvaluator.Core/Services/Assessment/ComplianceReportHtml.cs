using System.Globalization;
using System.Net;
using System.Text;

namespace AiPromptEvaluator;

/// <summary>
/// Renders a finished run as a single self-contained HTML page, in the layout of the
/// Compliance Assistant design.
///
/// <b>Server-rendered, with script only for showing and hiding.</b> The design was supplied as
/// a bundled component tree with its own runtime and twelve hard-coded demo findings, which
/// cannot be pointed at a real run: the data is inside the component. So the design is treated
/// as the design — its palette, its tag shapes, its cards and rails — and the markup is written
/// here from the archive. A page that renders without executing anything can be printed, mailed,
/// attached to a case file and opened in ten years, which a component bundle cannot.
///
/// <b>The three sides are shown together because separately they mislead.</b> A finding on its
/// own is an assertion; the canonical facts on their own are what the report claimed; the
/// retrieved passages on their own are what the case file holds. Every misdiagnosis worth
/// recording in this project came from having two of the three — a finding that looked like a
/// retrieval failure until the passage turned out to be in the prompt, an evidence gap that
/// turned out to be a canonical path resolving to nothing. Each requirement here carries all
/// three, side by side, and the passages include the ones ranking discarded.
/// </summary>
public static class ComplianceReportHtml
{
    private const string RulesetLabel = "Ruleset v3.0";

    // The palette, verbatim from the design. Named rather than inlined because several of them
    // appear a dozen times each and a one-digit drift between two of those is invisible.
    private const string Ink = "rgb(1,18,34)";
    private const string Ink2 = "rgb(52,65,78)";
    private const string Muted = "rgb(78,90,101)";
    private const string Line = "rgb(230,232,233)";
    private const string Stroke = "rgb(204,208,211)";
    private const string Link = "rgb(0,45,212)";
    private const string Blue = "rgb(53,186,251)";
    private const string Green = "rgb(135,210,102)";
    private const string Amber = "rgb(255,175,83)";
    private const string Red = "rgb(245,51,63)";
    private const string TagWarn = "rgb(252,172,108)";
    private const string TagOk = "rgb(147,231,220)";
    private const string TagGrey = "rgb(204,208,211)";

    /// <summary>The whole page for one run.</summary>
    public static string Render(CheckRunRecord run)
    {
        var html = new StringBuilder(1 << 20);

        html.Append("<!DOCTYPE html>\n<html lang=\"en\"><head><meta charset=\"utf-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("<title>Compliance check — ").Append(E(run.CaseReference)).Append("</title>");
        html.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        html.Append("<link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>");
        html.Append("<link rel=\"stylesheet\" href=\"https://fonts.googleapis.com/css2?");
        html.Append("family=Inter:wght@400;600;700&display=swap\">");
        html.Append("<style>").Append(Css()).Append("</style>");
        html.Append("</head><body>");

        html.Append("<div class=\"page\"><div class=\"panel\">");

        AppendHeader(html, run);
        AppendSummary(html, run);

        html.Append("<div class=\"board\">");
        AppendFilterRail(html, run);
        AppendFindings(html, run);
        AppendProvenanceRail(html, run);
        html.Append("</div>");

        AppendGrading(html, run);
        AppendRecord(html, run);

        html.Append("</div></div>");
        html.Append("<script>").Append(Script()).Append("</script>");
        html.Append("</body></html>");

        return html.ToString();
    }

    // ──────────────────────────────────────────────
    // Header and summary
    // ──────────────────────────────────────────────

    private static void AppendHeader(StringBuilder html, CheckRunRecord run)
    {
        html.Append("<div class=\"appHead\">");
        html.Append("<span class=\"logo\">◆</span>");
        html.Append("<span class=\"appTitle\">intelliflo Compliance Assistant</span>");
        html.Append("<span class=\"ruleset\">").Append(E(RulesetLabel)).Append(" · ")
            .Append(E(run.ChatModel)).Append("</span>");
        html.Append("</div>");
    }

    private static void AppendSummary(StringBuilder html, CheckRunRecord run)
    {
        var concerns = run.Count(CheckOutcome.PotentialConcern);
        var clear = run.Count(CheckOutcome.NoIssue);
        var notApplicable = run.Count(CheckOutcome.NotApplicable);
        var indeterminate = run.Count(CheckOutcome.Indeterminate);
        var errors = run.Count(CheckOutcome.Error);

        var coverage = Coverage(run);

        html.Append("<div class=\"summary\">");
        html.Append("<div class=\"tallies\">");
        Tag(html, $"{clear} no issue found", TagOk);
        Tag(html, $"{concerns} potential concern(s)", TagWarn);
        Tag(html, $"{notApplicable} not applicable", TagGrey);
        if (indeterminate > 0)
        {
            Tag(html, $"{indeterminate} not assessable", TagGrey);
        }

        if (errors > 0)
        {
            Tag(html, $"{errors} error(s)", TagWarn);
        }

        html.Append("</div><span class=\"spacer\"></span>");

        html.Append("<div class=\"coverage\"><div class=\"coverageLabel\">Evidence coverage</div>");
        html.Append("<div class=\"coverageRow\"><div class=\"bar\"><div class=\"barFill\" style=\"width:")
            .Append(coverage.Percent.ToString("0.#", CultureInfo.InvariantCulture)).Append("%\"></div></div>");
        html.Append("<span class=\"coverageText\">").Append(coverage.Label).Append("</span></div></div>");

        html.Append("<span class=\"rule\"></span>");
        html.Append("<span class=\"progress\">")
            .Append(run.Checks.Count).Append(" check(s) · ")
            .Append(run.AllGroups.Count()).Append(" requirement(s)</span>");
        html.Append("</div>");
    }

    /// <summary>
    /// How much of what the plans asked for actually arrived: canonical paths that resolved,
    /// weighted with requirements whose pack reached at least one passage.
    ///
    /// Deliberately not a self-assessed confidence. Every number here is counted from the run.
    /// </summary>
    private static (double Percent, string Label) Coverage(CheckRunRecord run)
    {
        var resolved = run.Checks.Sum(c => c.CanonicalPathsResolved);
        var missing = run.Checks.Sum(c => c.CanonicalPathsMissing);
        var paths = resolved + missing;

        var groups = run.AllGroups.ToList();
        var evidenced = groups.Count(g => g.Passages.Any(p => p.InPack));

        if (paths == 0 && groups.Count == 0)
        {
            return (0, "no coverage recorded");
        }

        var pathShare = paths == 0 ? 1d : (double)resolved / paths;
        var packShare = groups.Count == 0 ? 1d : (double)evidenced / groups.Count;
        var percent = (pathShare + packShare) / 2d * 100d;

        return (percent, $"{resolved}/{paths} canonical paths · {evidenced}/{groups.Count} packs evidenced");
    }

    // ──────────────────────────────────────────────
    // Rails and findings
    // ──────────────────────────────────────────────

    private static void AppendFilterRail(StringBuilder html, CheckRunRecord run)
    {
        html.Append("<aside class=\"filters\">");
        html.Append("<div class=\"filterTitle\">Findings</div>");

        html.Append("<button class=\"filterBtn on\" data-filter=\"all\">")
            .Append("<span>All checks</span><span>").Append(run.Checks.Count).Append("</span></button>");

        html.Append("<div class=\"filterHead\">Outcome</div>");

        foreach (var (label, key, count) in new[]
                 {
                     ("Potential concern", "concern", run.Count(CheckOutcome.PotentialConcern)),
                     ("No issue found", "clear", run.Count(CheckOutcome.NoIssue)),
                     ("Not applicable", "na", run.Count(CheckOutcome.NotApplicable)),
                     ("Not assessable", "indeterminate", run.Count(CheckOutcome.Indeterminate)),
                     ("Error", "error", run.Count(CheckOutcome.Error)),
                 })
        {
            html.Append("<button class=\"filterBtn\" data-filter=\"").Append(key).Append("\"><span>")
                .Append(E(label)).Append("</span><span class=\"")
                .Append(count > 0 ? "cnt" : "cnt zero").Append("\">").Append(count).Append("</span></button>");
        }

        // Issue category — what kind of problem the assessor said each requirement is. Every
        // one of the nine is listed whether or not this run found any, so a reader can see that
        // a category came back empty rather than wonder whether it was offered at all.
        html.Append("<div class=\"filterHead\">Issue category</div>");

        foreach (var (category, groups) in run.IssueCategoryCounts)
        {
            var checks = run.Checks.Count(c => c.Groups.Any(g => g.IssueCategories.Contains(category)));

            html.Append("<button class=\"filterBtn\" data-filter=\"cat:").Append(E(category))
                .Append("\"><span>").Append(E(category)).Append("</span><span class=\"")
                .Append(groups > 0 ? "cnt" : "cnt zero").Append("\" title=\"")
                .Append(groups).Append(" requirement(s)\">").Append(checks).Append("</span></button>");
        }

        // Needs a closer look — counted from the run, never asked of the model. Kept apart from
        // the categories above for that reason: one is a judgement about the case, the other is
        // a fact about how the judgement was reached.
        html.Append("<div class=\"filterHead\">Needs a closer look</div>");

        foreach (var (flag, groups) in run.CloserLookCounts)
        {
            var checks = run.Checks.Count(c => c.Groups.Any(g => g.CloserLook.Labels.Contains(flag)));

            html.Append("<button class=\"filterBtn\" data-filter=\"look:").Append(E(flag))
                .Append("\"><span>").Append(E(flag)).Append("</span><span class=\"")
                .Append(groups > 0 ? "cnt" : "cnt zero").Append("\" title=\"")
                .Append(groups).Append(" requirement(s)\">").Append(checks).Append("</span></button>");
        }

        html.Append("<div class=\"filterHead\">Working</div>");

        var discarded = run.Checks.Count(c =>
            c.Groups.Any(g => g.Finding is { } f && f.DiscardedDiscrepancies.Count > 0));
        var evicted = run.Checks.Count(c => c.Groups.Any(g => g.Passages.Any(p => !p.InPack)));

        foreach (var (label, key, count) in new[]
                 {
                     ("Raised and not carried", "discarded", discarded),
                     ("Passages evicted in ranking", "evicted", evicted),
                 })
        {
            html.Append("<button class=\"filterBtn\" data-filter=\"").Append(key).Append("\"><span>")
                .Append(E(label)).Append("</span><span class=\"")
                .Append(count > 0 ? "cnt" : "cnt zero").Append("\">").Append(count).Append("</span></button>");
        }

        html.Append("</aside>");
    }

    private static void AppendFindings(StringBuilder html, CheckRunRecord run)
    {
        html.Append("<main class=\"findings\">");

        var concerns = run.Checks.Where(c => Outcome(c) is CheckOutcome.PotentialConcern or CheckOutcome.Error).ToList();
        var rest = run.Checks.Except(concerns).ToList();

        html.Append("<div class=\"sectionHead\"><div class=\"sectionKicker\">")
            .Append(run.Checks.Count).Append(" check(s) assessed</div>");
        html.Append("<div class=\"sectionTitle\">Needs your decision</div>");
        html.Append("<div class=\"sectionSub\">")
            .Append(concerns.Count).Append(concerns.Count == 1 ? " potential concern" : " potential concerns")
            .Append("</div></div>");

        foreach (var check in concerns)
        {
            AppendCheck(html, check);
        }

        if (concerns.Count == 0)
        {
            html.Append("<div class=\"empty\">No check raised a concern in this run.</div>");
        }

        html.Append("<span class=\"hr\"></span>");
        html.Append("<div class=\"sectionHead\"><div class=\"sectionTitle\">Needs your review</div>");
        html.Append("<div class=\"sectionSub\">")
            .Append("Checks that came back clear, were not applicable, or could not be assessed. ")
            .Append("Open one to see what it searched for and what it read.</div></div>");

        foreach (var check in rest)
        {
            AppendCheck(html, check);
        }

        html.Append("</main>");
    }

    private static void AppendCheck(StringBuilder html, RecordedCheck check)
    {
        var outcome = Outcome(check);

        var categories = IssueCategory.Clean(check.Groups.SelectMany(g => g.IssueCategories));
        var closerLook = check.Groups.SelectMany(g => g.CloserLook.Labels).Distinct().ToList();

        html.Append("<section class=\"card\" data-outcome=\"").Append(OutcomeKey(outcome)).Append('"')
            .Append(" data-categories=\"").Append(E(string.Join("|", categories)))
            .Append("\" data-look=\"").Append(E(string.Join("|", closerLook)))
            .Append("\" data-evicted=\"")
            .Append(check.Groups.Any(g => g.Passages.Any(p => !p.InPack)) ? "1" : "0")
            .Append("\" data-discarded=\"")
            .Append(check.Groups.Any(g => g.Finding is { } f && f.DiscardedDiscrepancies.Count > 0) ? "1" : "0")
            .Append("\">");

        html.Append("<button class=\"cardHead\" type=\"button\">");
        html.Append("<div class=\"cardTags\">");
        Tag(html, OutcomeLabel(outcome), OutcomeHue(outcome));

        foreach (var category in categories)
        {
            Tag(html, category, TagWarn);
        }

        foreach (var flag in closerLook)
        {
            Tag(html, flag, TagGrey);
        }

        if (check.Error is not null)
        {
            Tag(html, "run failed", TagWarn);
        }

        html.Append("</div>");
        html.Append("<div class=\"cardTitle\">").Append(E(check.CheckId)).Append(" — ")
            .Append(E(check.CheckName)).Append("</div>");
        html.Append("<div class=\"cardMeta\">")
            .Append(check.Groups.Count).Append(" requirement(s) · ")
            .Append(check.SearchesRun).Append(" search(es) · ")
            .Append(check.PassagesRetrieved).Append(" passage(s) · ")
            .Append(check.CanonicalPathsResolved).Append('/')
            .Append(check.CanonicalPathsResolved + check.CanonicalPathsMissing).Append(" canonical paths · ")
            .Append(check.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append("s · ")
            .Append(check.Usage.OutputTokens.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" output tokens</div>");
        html.Append("<div class=\"cardSummary\">").Append(E(check.Summary)).Append("</div>");
        if (check.Error is not null)
        {
            html.Append("<div class=\"error\">").Append(E(check.Error)).Append("</div>");
        }

        html.Append("<span class=\"chev\">Show requirements</span>");
        html.Append("</button>");

        html.Append("<div class=\"cardBody\">");

        AppendBrief(html, check);

        foreach (var group in check.Groups)
        {
            AppendGroup(html, group);
        }

        if (check.Groups.Count == 0)
        {
            html.Append("<div class=\"empty\">This check settled before any requirement was assessed.</div>");
        }

        html.Append("</div></section>");
    }

    /// <summary>
    /// What the check was asking and under what rule, before any of its requirements are read.
    ///
    /// <b>Printed from the archive, not looked up.</b> The check sheet is edited between runs —
    /// wording is sharpened, a rule reference is corrected — so rendering a month-old run against
    /// today's sheet would show this month's question above last month's answer, and the answer
    /// would look wrong rather than the question having moved. Both were copied when the run was
    /// filed and travel with it.
    ///
    /// Empty for a run archived before the sheet was carried, which prints nothing rather than an
    /// empty frame.
    /// </summary>
    private static void AppendBrief(StringBuilder html, RecordedCheck check)
    {
        if (check.Narrative.Length == 0 && check.RegulatoryBasis.Length == 0
            && check.AppliesTo.Length == 0)
        {
            return;
        }

        html.Append("<div class=\"brief\">");

        if (check.Narrative.Length > 0)
        {
            html.Append("<div class=\"briefHead\">What this check asks</div>");
            html.Append("<div class=\"briefText\">").Append(E(check.Narrative)).Append("</div>");
        }

        if (check.AppliesTo.Length > 0)
        {
            html.Append("<div class=\"briefHead\">Applies to</div>");
            html.Append("<div class=\"briefText\">").Append(E(check.AppliesTo)).Append("</div>");
        }

        if (check.RegulatoryBasis.Length > 0)
        {
            html.Append("<div class=\"briefHead\">Regulatory basis</div><div class=\"rules\">");
            foreach (var rule in Rules(check.RegulatoryBasis))
            {
                html.Append("<span class=\"ruleRef\">").Append(E(rule)).Append("</span>");
            }

            html.Append("</div>");
        }

        html.Append("</div>");
    }

    /// <summary>
    /// Splits a regulatory basis into the individual rules it cites.
    ///
    /// The sheet writes them one per line, or run together with semicolons, depending on who
    /// filled the row in. Both are split; anything else is shown whole rather than cut at a
    /// separator that turns out to be part of a rule number.
    /// </summary>
    private static IReadOnlyList<string> Rules(string basis) =>
        basis.Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// One requirement, as three panes: what the report asserted, what was searched for and
    /// found, and what the assessor made of it.
    /// </summary>
    private static void AppendGroup(StringBuilder html, RecordedGroup group)
    {
        var finding = group.Finding;
        var outcome = finding?.ParsedOutcome ?? CheckOutcome.Indeterminate;

        html.Append("<article class=\"group\">");
        html.Append("<div class=\"groupHead\">");
        html.Append("<span class=\"groupId\">").Append(E(group.GroupId)).Append("</span>");
        html.Append("<span class=\"groupReq\">").Append(E(group.Requirement)).Append("</span>");
        Tag(html, OutcomeLabel(outcome), OutcomeHue(outcome));
        if (finding?.Severity is { Length: > 0 } severity)
        {
            Tag(html, "severity " + severity, TagGrey);
        }

        foreach (var category in group.IssueCategories)
        {
            Tag(html, category, TagWarn);
        }

        foreach (var flag in group.CloserLook.Labels)
        {
            Tag(html, flag, TagGrey);
        }

        if (finding is not null)
        {
            Tag(html, ConfidenceLabel(finding.Confidence), ConfidenceHue(finding.Confidence));
        }

        html.Append("</div>");

        AppendCoverage(html, group);

        html.Append("<div class=\"panes\">");

        // ── Canonical facts ──
        html.Append("<div class=\"pane\"><div class=\"paneHead\">Canonical facts</div>");
        html.Append("<div class=\"paneSub\">What the suitability report asserts, read from the ")
            .Append("extracted model by the path the plan names.</div>");

        if (group.Facts.Count == 0)
        {
            html.Append("<div class=\"empty\">This requirement reads no canonical path.</div>");
        }
        else
        {
            html.Append("<ul class=\"facts\">");
            foreach (var fact in group.Facts)
            {
                html.Append("<li class=\"fact\"><span class=\"dot ")
                    .Append(fact.Found ? "ok" : "bad").Append("\"></span>");
                html.Append("<div><code>").Append(E(fact.Path)).Append("</code>");
                html.Append("<div class=\"factValue\">")
                    .Append(fact.Found ? E(Shorten(fact.Json, 600)) : "<em>not present in the model</em>")
                    .Append("</div></div></li>");
            }

            html.Append("</ul>");
        }

        if (finding is { ReportSays.Length: > 0 })
        {
            html.Append("<div class=\"says\"><span class=\"saysLabel\">Report:</span> ")
                .Append(E(finding.ReportSays)).Append("</div>");
        }

        html.Append("</div>");

        // ── Evidence searched ──
        var inPack = group.Passages.Where(p => p.InPack).ToList();
        var dropped = group.Passages.Where(p => !p.InPack).ToList();

        html.Append("<div class=\"pane\"><div class=\"paneHead\">Evidence searched</div>");
        html.Append("<div class=\"paneSub\">")
            .Append(group.Queries.Count)
            .Append(" semantic search(es) · ").Append(inPack.Count).Append(" passage(s) in the pack");
        if (dropped.Count > 0)
        {
            html.Append(" · ").Append(dropped.Count).Append(" evicted in ranking");
        }

        html.Append("</div>");

        AppendQueries(html, group);

        if (group.Passages.Count == 0)
        {
            html.Append("<div class=\"empty\">No passage was retrieved for this requirement.</div>");
        }
        else
        {
            AppendPassages(html, inPack, evicted: false);
            AppendPassages(html, dropped, evicted: true);
        }

        if (finding is { FileSays.Length: > 0 })
        {
            html.Append("<div class=\"says\"><span class=\"saysLabel\">File:</span> ")
                .Append(E(finding.FileSays)).Append("</div>");
        }

        AppendEvidenceForFacts(html, group);

        html.Append("</div>");

        // ── Verification ──
        html.Append("<div class=\"pane\"><div class=\"paneHead\">Verification</div>");
        html.Append("<div class=\"paneSub\">What the assessor concluded, and how far its working ")
            .Append("could be traced back to the passages above.</div>");

        if (finding is null)
        {
            html.Append("<div class=\"empty\">No finding was recorded for this requirement.</div>");
        }
        else
        {
            AppendVerification(html, finding);
        }

        html.Append("</div>");
        html.Append("</div>");

        AppendTranscript(html, group);
        html.Append("</article>");
    }

    /// <summary>
    /// The two shields the design puts above every finding: how far the assessor's own working
    /// can be relied on, and how much of what the plan asked for actually reached it.
    ///
    /// Side by side because they fail in opposite directions and are constantly confused. A
    /// requirement can be fully covered and badly reasoned, or thinly covered and reasoned
    /// impeccably about the little it had — and the second is often the correct finding. One
    /// number for both would average those into something that describes neither.
    /// </summary>
    private static void AppendCoverage(StringBuilder html, RecordedGroup group)
    {
        var agent = group.AgentCoverage;
        var evidence = group.EvidenceCoverage;

        html.Append("<div class=\"shields\">");

        html.Append("<div class=\"shield\"><span class=\"shieldIcon ")
            .Append(Band(agent.Score, 80, 60)).Append("\">&#9670;</span>");
        html.Append("<div class=\"shieldBody\"><div class=\"shieldTop\">")
            .Append("<span class=\"shieldLabel\">Agent coverage</span>")
            .Append("<span class=\"shieldLevel\">").Append(E(agent.Level)).Append(" · ")
            .Append(FindingCoverage.Percent(agent.Score)).Append("</span></div>");
        html.Append("<div class=\"shieldReason\">").Append(E(agent.Reason)).Append("</div>");
        html.Append("</div></div>");

        html.Append("<div class=\"shield\"><span class=\"shieldIcon ")
            .Append(Band(evidence.Score, 90, 60)).Append("\">&#9670;</span>");
        html.Append("<div class=\"shieldBody\"><div class=\"shieldTop\">")
            .Append("<span class=\"shieldLabel\">Evidence coverage</span>")
            .Append("<span class=\"shieldLevel\">").Append(E(evidence.Level)).Append(" · ")
            .Append(FindingCoverage.Percent(evidence.Score)).Append("</span></div>");

        html.Append("<ul class=\"covItems\">");
        foreach (var item in evidence.Items)
        {
            html.Append("<li><span class=\"dot ").Append(SignalDot(item.Signal)).Append("\"></span>")
                .Append(E(item.Label)).Append("</li>");
        }

        html.Append("</ul></div></div>");
        html.Append("</div>");
    }

    /// <summary>Which of the design's three bands a score falls in.</summary>
    private static string Band(int score, int high, int medium) =>
        score >= high ? "good" : score >= medium ? "fair" : "poor";

    private static string SignalDot(CoverageSignal signal) => signal switch
    {
        CoverageSignal.Read => "ok",
        CoverageSignal.Partial => "warn",
        CoverageSignal.Unreadable => "bad",
        _ => string.Empty,
    };

    /// <summary>
    /// Every search the requirement ran, including the ones that came back empty.
    ///
    /// Read from the recorded queries rather than grouped out of the passages, because a search
    /// that found nothing leaves no passage behind — and a requirement whose every search came
    /// back empty then reads exactly like one that searched for nothing at all.
    /// </summary>
    private static void AppendQueries(StringBuilder html, RecordedGroup group)
    {
        if (group.Queries.Count == 0)
        {
            return;
        }

        html.Append("<ul class=\"queries\">");
        foreach (var query in group.Queries)
        {
            html.Append("<li class=\"query\"><span class=\"queryId\">")
                .Append(E(query.QueryId)).Append("</span>");
            html.Append("<span class=\"queryText\">“").Append(E(query.Text)).Append("”</span>");
            html.Append("<span class=\"queryMeta").Append(query.Hits == 0 ? " barren" : string.Empty)
                .Append("\">").Append(query.Hits).Append(" hit(s)");
            if (!string.IsNullOrWhiteSpace(query.TargetCategories))
            {
                html.Append(" · categories ").Append(E(query.TargetCategories));
            }

            html.Append("</span></li>");
        }

        html.Append("</ul>");
    }

    /// <summary>
    /// The evidence the assessor cited against the facts it read out of the canonical model —
    /// each quotation with the document it came from, and whether it could be traced there.
    ///
    /// <b>Printed on the evidence side, beside the passages, rather than only under the verdict.</b>
    /// A citation listed under a conclusion reads as support for the conclusion; the same
    /// citation printed next to the canonical facts and the passages that were searched reads as
    /// what it is — a claim about a document, checkable against the document named beside it.
    /// Reviewers reading these reports asked the same question of every finding: which file did
    /// that come from. It was two panes away.
    ///
    /// The pairing is by requirement, not by path: the assessor cites in support of the
    /// comparison as a whole, and inventing a fact-to-quotation link the model never made would
    /// be the report asserting something nothing in the run establishes.
    /// </summary>
    private static void AppendEvidenceForFacts(StringBuilder html, RecordedGroup group)
    {
        if (group.Finding is not { Citations.Count: > 0 } finding)
        {
            return;
        }

        var unverified = finding.UnverifiedQuotes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var paths = group.Facts.Where(f => f.Found).Select(f => f.Path).ToList();

        html.Append("<div class=\"trail\"><div class=\"trailHead\">Evidence cited for these facts</div>");

        html.Append("<div class=\"trailSub\">");
        html.Append(paths.Count > 0
            ? "Supporting " + E(string.Join(", ", paths)) + "."
            : "This requirement read no canonical path; the citations support the comparison itself.");
        html.Append("</div>");

        html.Append("<ul class=\"trailItems\">");
        foreach (var citation in finding.Citations)
        {
            var text = citation.Describe();
            var traced = !unverified.Contains(text);

            html.Append("<li><span class=\"dot ").Append(traced ? "ok" : "bad").Append("\"></span><div>");
            html.Append("<div class=\"trailQuote\">“").Append(E(Shorten(text, 500))).Append("”</div>");
            html.Append("<div class=\"trailFrom\"><strong>")
                .Append(E(citation.Source.Length > 0 ? citation.Source : "no document named"))
                .Append("</strong>");

            if (citation.Category is { Length: > 0 } category)
            {
                html.Append(" · ").Append(E(DocumentCategory.NameForCode(category)));
            }

            if (citation.PassageId is { Length: > 0 } passageId)
            {
                html.Append(" · ").Append(E(passageId));
            }

            html.Append(traced ? string.Empty : " · not traced to the pack");
            html.Append("</div></div></li>");
        }

        html.Append("</ul></div>");
    }

    private static void AppendPassages(StringBuilder html, IReadOnlyList<RecordedPassage> passages, bool evicted)
    {
        if (passages.Count == 0)
        {
            return;
        }

        if (evicted)
        {
            html.Append("<div class=\"evictedHead\">Retrieved and evicted before the assessor saw them</div>");
        }

        html.Append("<div class=\"passages\">");

        var index = 0;
        foreach (var passage in passages)
        {
            index++;
            html.Append("<details class=\"passage").Append(evicted ? " evicted" : string.Empty).Append("\">");
            html.Append("<summary><span class=\"dot ").Append(evicted ? "warn" : "ok").Append("\"></span>");
            html.Append("<span class=\"pid\">").Append(evicted ? "—" : "P" + index).Append("</span>");
            html.Append("<span class=\"pdoc\">").Append(E(passage.DocumentName)).Append("</span>");
            html.Append("<span class=\"pcat\">[").Append(E(passage.CategoryCode)).Append("] ")
                .Append(E(passage.CategoryName)).Append("</span>");
            html.Append("<span class=\"pscore\">")
                .Append(passage.Score.ToString("0.000", CultureInfo.InvariantCulture)).Append("</span>");
            html.Append("</summary>");
            html.Append("<div class=\"ptext\">").Append(E(passage.Text)).Append("</div>");
            html.Append("<div class=\"pfrom\">found by ").Append(E(passage.QueryId)).Append("</div>");
            html.Append("</details>");
        }

        html.Append("</div>");
    }

    private static void AppendVerification(StringBuilder html, GroupFinding finding)
    {
        if (finding.Discrepancies.Count > 0)
        {
            html.Append("<div class=\"vHead\">Discrepancies named</div><ul class=\"bullets\">");
            foreach (var discrepancy in finding.Discrepancies)
            {
                html.Append("<li>").Append(E(discrepancy)).Append("</li>");
            }

            html.Append("</ul>");
        }

        if (finding.DiscardedDiscrepancies.Count > 0)
        {
            html.Append("<div class=\"warnBox\"><strong>Raised and not carried.</strong> ")
                .Append("This requirement named ").Append(finding.DiscardedDiscrepancies.Count)
                .Append(" difference(s) and then passed. Most such differences are immaterial; ")
                .Append("one of them, in an earlier run, was a benchmark finding.</div>");
        }

        if (!finding.ComparisonPerformed)
        {
            html.Append("<div class=\"warnBox\"><strong>Comparison not performed.</strong> ");
            html.Append(finding.MissingInputs.Count > 0
                ? "Missing: " + E(string.Join("; ", finding.MissingInputs))
                : "No input was named.");
            html.Append("</div>");
        }

        if (finding.EvidenceShortfall is { Length: > 0 } shortfall)
        {
            html.Append("<div class=\"warnBox\"><strong>Evidence shortfall.</strong> ")
                .Append(E(shortfall)).Append("</div>");
        }

        if (finding.Analysis is { Length: > 0 })
        {
            html.Append("<div class=\"vHead\">Analysis</div>");
            html.Append("<div class=\"analysis\">").Append(E(finding.Analysis)).Append("</div>");
        }

        if (finding.Citations.Count > 0)
        {
            html.Append("<div class=\"vHead\">Citations</div><ul class=\"citations\">");

            var unverified = finding.UnverifiedQuotes
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var citation in finding.Citations)
            {
                var text = citation.Describe();
                var traced = !unverified.Contains(text);

                html.Append("<li><span class=\"dot ").Append(traced ? "ok" : "bad").Append("\"></span>");
                html.Append("<div><span class=\"citeQuote\">").Append(E(Shorten(text, 500))).Append("</span>");
                html.Append("<div class=\"citeFrom\">")
                    .Append(E(citation.PassageId ?? "no passage id")).Append(" · ")
                    .Append(E(citation.Source)).Append(traced ? string.Empty : " · not traced to the pack")
                    .Append("</div></div></li>");
            }

            html.Append("</ul>");
        }

        if (finding.UnverifiedQuotes.Count > 0)
        {
            html.Append("<div class=\"warnBox\"><strong>")
                .Append(finding.UnverifiedQuotes.Count)
                .Append(" quotation(s) could not be traced</strong> to the passages this requirement ")
                .Append("was given. Read this finding before the others.</div>");
        }

        if (finding.GroupIdDiverged || finding.RequirementDiverged)
        {
            html.Append("<div class=\"warnBox\"><strong>Prompt adherence.</strong> The response ");
            html.Append(finding.GroupIdDiverged ? "echoed a different requirement id" : string.Empty);
            html.Append(finding is { GroupIdDiverged: true, RequirementDiverged: true } ? " and " : string.Empty);
            html.Append(finding.RequirementDiverged ? "rewrote the requirement text" : string.Empty);
            html.Append(". The plan's values were used.</div>");
        }
    }

    private static void AppendTranscript(StringBuilder html, RecordedGroup group)
    {
        if (group.RawResponse.Length == 0 && group.UserPrompt.Length == 0)
        {
            return;
        }

        html.Append("<details class=\"transcript\"><summary>Prompt and raw response</summary>");

        if (group.SystemPrompt.Length > 0)
        {
            html.Append("<div class=\"tHead\">System prompt</div><pre>")
                .Append(E(group.SystemPrompt)).Append("</pre>");
        }

        if (group.UserPrompt.Length > 0)
        {
            html.Append("<div class=\"tHead\">User prompt</div><pre>")
                .Append(E(group.UserPrompt)).Append("</pre>");
        }

        if (group.RawResponse.Length > 0)
        {
            html.Append("<div class=\"tHead\">Raw response</div><pre>")
                .Append(E(group.RawResponse)).Append("</pre>");
        }

        html.Append("</details>");
    }

    // ──────────────────────────────────────────────
    // Provenance, grading and the record
    // ──────────────────────────────────────────────

    private static void AppendProvenanceRail(StringBuilder html, CheckRunRecord run)
    {
        html.Append("<aside class=\"provenance\">");
        html.Append("<div class=\"provHead\"><div class=\"provTitle\">Evidence</div>");
        html.Append("<div class=\"provSub\">How this outcome was generated</div></div>");

        Prov(html, "Case", run.CaseReference + " (tenant " + run.TenantId + ")");
        Prov(html, "Run", run.RunId);
        Prov(html, "Started", run.StartedAt.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        Prov(html, "Duration", run.Duration == TimeSpan.Zero
            ? "not recorded"
            : run.Duration.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + "s");
        Prov(html, "Chat model", run.ChatModel);
        Prov(html, "Response cache", run.CacheMode);
        Prov(html, "Canonical model", string.IsNullOrWhiteSpace(run.CanonicalModelId)
            ? "none"
            : run.CanonicalModelId + " (schema " + run.SchemaVersion + ")");
        Prov(html, "Report read", run.SourceDocuments.Count == 0
            ? "none recorded"
            : string.Join(", ", run.SourceDocuments));

        var output = run.Checks.Sum(c => c.Usage.OutputTokens);
        var authenticity = new RunAuthenticity(output, run.Duration);

        Prov(html, "Output tokens", output.ToString("N0", CultureInfo.InvariantCulture));
        Prov(html, "Generation rate", run.Duration == TimeSpan.Zero
            ? "not measured"
            : authenticity.TokensPerSecond.ToString("N0", CultureInfo.InvariantCulture) + " tok/s"
              + (authenticity.IsReplay ? " — implausible" : string.Empty));

        if (authenticity.IsReplay)
        {
            html.Append("<div class=\"warnBox\"><strong>This run was served from cache.</strong> ")
                .Append("Its output could not have been generated in the time it took, so it is a ")
                .Append("recording of an earlier run under a new name. Nothing below is a fresh ")
                .Append("measurement.</div>");
        }

        html.Append("<div class=\"provHead\"><div class=\"provTitle\">Run configuration</div></div>");
        html.Append("<pre class=\"fingerprint\">").Append(E(run.Fingerprint)).Append("</pre>");

        html.Append("</aside>");
    }

    private static void Prov(StringBuilder html, string label, string value)
    {
        html.Append("<div class=\"provRow\"><span class=\"provLabel\">").Append(E(label))
            .Append("</span><span class=\"provValue\">").Append(E(value)).Append("</span></div>");
    }

    /// <summary>
    /// The computed grade, and the rule that produced it.
    ///
    /// Computed from the outcomes rather than asked for, and stated with its rule beside it, so
    /// a reader can see the arithmetic rather than take the letter on trust. The thresholds are
    /// the design's: anything failing outright is a Fail, anything unassessable is a Refer.
    /// </summary>
    internal static (string Grade, string Rule) Grade(CheckRunRecord run)
    {
        if (run.Count(CheckOutcome.Error) > 0)
        {
            return ("Fail", "A check failed to run");
        }

        if (run.Count(CheckOutcome.PotentialConcern) > 0)
        {
            return ("Refer", "At least one check raised a potential concern");
        }

        if (run.Count(CheckOutcome.Indeterminate) > 0)
        {
            return ("Refer", "A check could not be assessed");
        }

        return ("Pass", "Every check that applied came back clear");
    }

    private static void AppendGrading(StringBuilder html, CheckRunRecord run)
    {
        var (grade, rule) = Grade(run);

        html.Append("<div class=\"grading\">");
        html.Append("<div class=\"gradeCard ").Append(grade.ToLowerInvariant()).Append("\">");
        html.Append("<span class=\"gradeLetter\">").Append(E(grade)).Append("</span>");
        html.Append("<span class=\"gradeCaption\">Computed grading</span></div>");

        html.Append("<div class=\"gradeBody\"><div class=\"sectionTitle\">Compliance grade</div>");
        html.Append("<div class=\"gradeRule\">Rule applied: ");
        Tag(html, rule, grade == "Pass" ? TagOk : TagWarn);
        html.Append("</div>");

        html.Append("<div class=\"gradeNote\">This grade is computed from the check outcomes above ")
            .Append("and is not a decision. No disposition has been recorded against any finding — ")
            .Append("the archive holds what the run saw, not what a checker concluded about it.</div>");
        html.Append("</div></div>");
    }

    private static void AppendRecord(StringBuilder html, CheckRunRecord run)
    {
        var usage = run.Checks.Aggregate(TokenUsage.Empty, (total, c) => new TokenUsage(
            total.InputTokens + c.Usage.InputTokens,
            total.OutputTokens + c.Usage.OutputTokens,
            total.CacheWriteTokens + c.Usage.CacheWriteTokens,
            total.CacheReadTokens + c.Usage.CacheReadTokens));

        var groups = run.AllGroups.ToList();

        html.Append("<div class=\"record\"><div class=\"sectionTitle\">Run record</div>");

        Row(html, "Checks assessed", $"{run.Checks.Count} check(s), {groups.Count} requirement(s)");
        Row(html, "Searches", $"{run.Checks.Sum(c => c.SearchesRun):N0} search(es) returning "
                              + $"{run.Checks.Sum(c => c.PassagesRetrieved):N0} passage(s)");
        Row(html, "Passages archived",
            $"{groups.Sum(g => g.Passages.Count(p => p.InPack)):N0} in packs, "
            + $"{groups.Sum(g => g.Passages.Count(p => !p.InPack)):N0} evicted in ranking");
        Row(html, "Canonical paths",
            $"{run.Checks.Sum(c => c.CanonicalPathsResolved):N0} resolved, "
            + $"{run.Checks.Sum(c => c.CanonicalPathsMissing):N0} absent");
        Row(html, "Untraceable citations",
            groups.Sum(g => g.Finding?.UnverifiedQuotes.Count ?? 0).ToString("N0", CultureInfo.InvariantCulture));

        var raised = run.IssueCategoryCounts.Where(c => c.Count > 0).ToList();
        Row(html, "Issue categories raised", raised.Count == 0
            ? "none"
            : string.Join(", ", raised.Select(c => $"{c.Category} ({c.Count})")));

        var flagged = run.CloserLookCounts.Where(c => c.Count > 0).ToList();
        Row(html, "Needs a closer look", flagged.Count == 0
            ? "no requirement flagged"
            : string.Join(", ", flagged.Select(c => $"{c.Flag} ({c.Count})")));
        Row(html, "Tokens",
            $"{usage.InputTokens:N0} in, {usage.OutputTokens:N0} out, {usage.CacheReadTokens:N0} cached");
        Row(html, "Generated", DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));

        html.Append("</div>");
    }

    private static void Row(StringBuilder html, string label, string value)
    {
        html.Append("<div class=\"recordRow\"><span class=\"recordLabel\">").Append(E(label))
            .Append("</span><span class=\"recordValue\">").Append(E(value)).Append("</span></div>");
    }

    // ──────────────────────────────────────────────
    // Small pieces
    // ──────────────────────────────────────────────

    private static void Tag(StringBuilder html, string label, string hue)
    {
        html.Append("<span class=\"tag\" style=\"--hue:").Append(hue).Append("\">")
            .Append(E(label)).Append("</span>");
    }

    private static CheckOutcome Outcome(RecordedCheck check) =>
        Enum.TryParse<CheckOutcome>(check.Outcome, ignoreCase: true, out var parsed)
            ? parsed
            : CheckOutcome.PotentialConcern;

    private static string OutcomeKey(CheckOutcome outcome) => outcome switch
    {
        CheckOutcome.NoIssue => "clear",
        CheckOutcome.PotentialConcern => "concern",
        CheckOutcome.NotApplicable => "na",
        CheckOutcome.Indeterminate => "indeterminate",
        _ => "error",
    };

    private static string OutcomeLabel(CheckOutcome outcome) => outcome switch
    {
        CheckOutcome.NoIssue => "No issue found",
        CheckOutcome.PotentialConcern => "Potential concern",
        CheckOutcome.NotApplicable => "Not applicable",
        CheckOutcome.Indeterminate => "Not assessable",
        _ => "Error",
    };

    private static string OutcomeHue(CheckOutcome outcome) => outcome switch
    {
        CheckOutcome.NoIssue => TagOk,
        CheckOutcome.PotentialConcern => TagWarn,
        _ => TagGrey,
    };

    private static string ConfidenceLabel(CitationConfidence confidence) => confidence switch
    {
        CitationConfidence.Verified => "citations verified",
        CitationConfidence.PartiallyVerified => "citations partly verified",
        _ => "citations unverified",
    };

    private static string ConfidenceHue(CitationConfidence confidence) => confidence switch
    {
        CitationConfidence.Verified => TagOk,
        CitationConfidence.PartiallyVerified => TagGrey,
        _ => TagWarn,
    };

    /// <summary>
    /// Trims a value for display, saying so where it cuts. Silent truncation is how a page comes
    /// to disagree with the archive it was rendered from.
    /// </summary>
    private static string Shorten(string value, int max) =>
        value.Length <= max ? value : value[..max] + $"… ({value.Length - max:N0} more characters)";

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    // ──────────────────────────────────────────────
    // Presentation
    // ──────────────────────────────────────────────

    private static string Css() =>
        $$"""
        *{box-sizing:border-box}
        body{margin:0;background:rgb(242,243,244);color:{{Ink}};-webkit-font-smoothing:antialiased;
          font-family:Inter,-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Arial,sans-serif;font-size:14px;line-height:22px}
        code,pre{font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace}
        .page{padding:24px}
        .panel{max-width:1680px;margin:0 auto;border-radius:8px;background:#fff;padding:32px;
          box-shadow:0 2px 3px rgba(0,0,0,.04),0 4px 16px rgba(0,0,0,.12)}
        .appHead{display:flex;align-items:center;gap:12px;height:34px;padding:0 8px;margin-bottom:16px}
        .logo{width:28px;height:28px;display:flex;align-items:center;justify-content:center;
          color:rgb(17,58,214);font-size:22px}
        .appTitle{font-weight:600;font-size:17.5px;line-height:28px;color:rgb(29,29,27)}
        .ruleset{font-size:11.2px;line-height:18px;color:{{Muted}}}

        .tag{display:inline-flex;align-items:center;gap:4px;border-radius:4px;padding:4px 8px;
          font-weight:700;font-size:12px;line-height:16px;letter-spacing:.02em;white-space:nowrap;color:rgb(58,92,88);
          box-shadow:inset 0 0 0 .5px rgba(0,0,0,.1);
          background:linear-gradient(rgba(255,255,255,.8),rgba(255,255,255,.8)),linear-gradient(var(--hue),var(--hue))}

        .summary{display:flex;align-items:center;gap:10px;flex-wrap:wrap;row-gap:8px;
          min-height:48px;border:1px solid {{Line}};border-radius:8px 8px 0 0;padding:8px 16px}
        .tallies{display:flex;gap:4px;flex-wrap:wrap}
        .spacer{flex:1}
        .coverageLabel{font-weight:600;font-size:14px;white-space:nowrap}
        .coverageRow{display:flex;align-items:center;gap:10px;height:18px}
        .bar{width:120px;height:8px;border-radius:999px;background:rgb(242,243,244);overflow:hidden}
        .barFill{height:100%;border-radius:999px;background:{{Blue}}}
        .coverageText{font-weight:600;font-size:11.2px;white-space:nowrap}
        .rule{width:1px;height:24px;background:{{Stroke}}}
        .progress{font-weight:600;font-size:14px;white-space:nowrap}

        .board{display:flex;align-items:flex-start;gap:0;border:1px solid {{Line}};border-top:0;
          border-radius:0 0 8px 8px;background:rgb(250,250,250)}
        .filters{width:236px;flex:none;padding:16px 8px;position:sticky;top:16px;
          max-height:calc(100vh - 32px);overflow:auto}
        .filterTitle{font-weight:600;font-size:17.5px;line-height:28px;padding:0 8px}
        .filterHead{font-weight:600;font-size:14px;padding:8px 8px 2px}
        .filterBtn{display:flex;align-items:center;gap:8px;width:100%;min-height:32px;border:0;border-radius:4px;
          padding:6px 8px;background:transparent;font-size:14px;line-height:20px;color:{{Ink}};text-align:left}
        .filterBtn span:first-child{flex:1;min-width:0}
        .filterBtn.on{background:{{Line}};font-weight:600}
        .filterBtn:hover{background:rgb(238,240,241)}
        .cnt{font-variant-numeric:tabular-nums}
        .cnt.zero{color:rgb(144,150,158)}

        .findings{flex:1;min-width:0;background:#fff;border-left:1px solid {{Line}};
          border-right:1px solid {{Line}};padding:0 24px 32px}
        .sectionHead{padding:24px 8px 12px}
        .sectionKicker{font-size:14px;color:{{Muted}}}
        .sectionTitle{font-weight:600;font-size:21.88px;line-height:32px}
        .sectionSub{font-size:14px;text-wrap:pretty}
        .hr{display:block;height:1px;background:{{Stroke}};margin:24px 8px}
        .empty{padding:16px;border-radius:12px;background:rgb(250,250,250);color:{{Muted}};font-size:14px}

        .card{border-radius:16px;background:#fff;box-shadow:inset 0 0 0 1px {{Line}};margin:0 0 16px}
        .card.open{box-shadow:inset 0 0 0 4px rgb(154,221,253),0 2px 8px rgba(15,23,41,.18)}
        .card[hidden]{display:none}
        .cardHead{display:block;width:100%;text-align:left;border:0;background:none;padding:24px}
        .cardTags{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:8px}
        .cardTitle{font-weight:600;font-size:17.5px;line-height:28px}
        .cardMeta{font-size:11.2px;line-height:18px;color:{{Muted}};margin:4px 0}
        .cardSummary{font-size:14px;line-height:22px;text-wrap:pretty}
        .chev{display:inline-block;margin-top:12px;font-weight:600;font-size:11.2px;color:{{Link}}}
        .cardBody{display:none;padding:0 24px 24px}
        .card.open .cardBody{display:block}
        .error{margin-top:8px;padding:8px 12px;border-radius:8px;background:rgb(255,244,245);
          color:rgb(120,20,26);font-size:13px}

        .group{border-radius:12px;background:rgb(250,250,250);box-shadow:inset 0 0 0 1px {{Line}};
          padding:16px;margin-bottom:16px}
        .groupHead{display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin-bottom:12px}
        .groupId{font-weight:700;font-size:12px;letter-spacing:.02em;padding:2px 6px;border-radius:4px;
          background:{{Line}}}
        .groupReq{flex:1;min-width:220px;font-weight:600;font-size:14px;line-height:22px}

        .panes{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:12px}
        .pane{background:#fff;border-radius:12px;box-shadow:inset 0 0 0 1px {{Line}};padding:12px 14px}
        .paneHead{font-weight:600;font-size:14px;line-height:22px;color:{{Muted}}}
        .paneSub{font-size:11.2px;line-height:18px;color:{{Ink2}};margin-bottom:8px;text-wrap:pretty}

        .brief{border-radius:16px;background:#fff;padding:14px 16px;margin-bottom:14px;
          box-shadow:inset 0 0 0 1px {{Line}}}
        .briefHead{font-size:11px;letter-spacing:.06em;text-transform:uppercase;color:{{Muted}};
          margin-bottom:4px}
        .briefHead+.briefText{margin-bottom:10px}
        .briefText{font-size:13px;line-height:20px;color:{{Ink2}};text-wrap:pretty;white-space:pre-wrap}
        .rules{display:flex;flex-wrap:wrap;gap:6px}
        .ruleRef{font-size:11.2px;line-height:18px;padding:2px 9px;border-radius:999px;
          background:rgb(242,243,244);color:{{Ink2}};box-shadow:inset 0 0 0 1px {{Line}}}

        .trail{margin-top:12px;border-radius:14px;background:rgb(250,250,250);padding:12px 14px;
          box-shadow:inset 0 0 0 1px {{Line}}}
        .trailHead{font-size:12.8px;font-weight:600;margin-bottom:2px}
        .trailSub{font-size:11.2px;line-height:18px;color:{{Muted}};margin-bottom:8px;text-wrap:pretty}
        .trailItems{list-style:none;margin:0;padding:0}
        .trailItems li{display:flex;gap:8px;align-items:flex-start;padding:6px 0;
          border-top:1px solid {{Line}}}
        .trailItems li:first-child{border-top:0}
        .trailItems .dot{margin-top:6px}
        .trailQuote{font-size:12.8px;line-height:20px;color:{{Ink}};text-wrap:pretty}
        .trailFrom{font-size:11.2px;line-height:18px;color:{{Muted}};margin-top:2px}

        .shields{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:12px;margin-bottom:12px}
        .shield{display:flex;gap:12px;border-radius:16px;background:rgb(250,250,250);padding:14px 16px;
          box-shadow:inset 0 0 0 4px #fff,0 2px 8px rgba(15,23,41,.08)}
        .shieldIcon{font-size:20px;line-height:22px;flex:none}
        .shieldIcon.good{color:{{Green}}}
        .shieldIcon.fair{color:{{Blue}}}
        .shieldIcon.poor{color:{{Amber}}}
        .shieldBody{flex:1;min-width:0}
        .shieldTop{display:flex;gap:8px;align-items:baseline}
        .shieldLabel{flex:1;min-width:0;font-size:14px;color:{{Muted}}}
        .shieldLevel{font-weight:600;font-size:14px;white-space:nowrap}
        .shieldReason{font-size:11.2px;line-height:18px;color:{{Ink2}};margin-top:6px;text-wrap:pretty}
        .covItems{list-style:none;margin:6px 0 0;padding:0}
        .covItems li{display:flex;gap:8px;align-items:center;font-size:11.2px;line-height:18px;
          color:{{Ink2}};padding:1px 0}
        .queryMeta.barren{color:{{Amber}}}

        .dot{width:10px;height:10px;border-radius:50%;flex:none;display:inline-block;background:{{Stroke}}}
        .dot.ok{background:{{Green}}}
        .dot.warn{background:{{Amber}}}
        .dot.bad{background:{{Red}}}

        .facts,.citations,.bullets,.queries{list-style:none;margin:0;padding:0}
        .fact,.citations li{display:flex;gap:8px;align-items:flex-start;padding:4px 0;
          border-top:1px solid {{Line}};font-size:12px;line-height:18px}
        .fact:first-child,.citations li:first-child{border-top:0}
        .fact code{font-size:11px;color:{{Ink2}};word-break:break-all}
        .factValue{color:{{Ink}};white-space:pre-wrap;word-break:break-word}
        .bullets li{padding:3px 0 3px 14px;position:relative;font-size:12.5px;line-height:19px;text-wrap:pretty}
        .bullets li:before{content:"–";position:absolute;left:0;color:{{Muted}}}
        .says{margin-top:10px;padding:8px 10px;border-radius:8px;background:rgb(250,250,250);
          font-size:12.5px;line-height:19px;text-wrap:pretty}
        .saysLabel{font-weight:600;color:{{Muted}}}

        .query{display:flex;flex-direction:column;padding:5px 0;border-top:1px solid {{Line}}}
        .query:first-child{border-top:0}
        .queryId{font-weight:700;font-size:11px;letter-spacing:.02em;color:{{Muted}}}
        .queryText{font-size:12.5px;line-height:19px;text-wrap:pretty}
        .queryMeta{font-size:11px;color:{{Muted}}}

        .evictedHead{margin-top:10px;font-weight:600;font-size:11.2px;color:{{Muted}}}
        .passages{margin-top:6px}
        .passage{border-top:1px solid {{Line}};font-size:12px}
        .passage:first-child{border-top:0}
        .passage.evicted summary{opacity:.72}
        .passage summary{display:flex;gap:8px;align-items:center;padding:5px 0;cursor:pointer;list-style:none}
        .passage summary::-webkit-details-marker{display:none}
        .pid{font-weight:700;font-size:11px;min-width:26px;color:{{Muted}}}
        .pdoc{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
        .pcat{font-size:11px;color:{{Muted}};white-space:nowrap}
        .pscore{font-variant-numeric:tabular-nums;font-size:11px;color:{{Muted}}}
        .ptext{white-space:pre-wrap;word-break:break-word;background:rgb(250,250,250);border-radius:8px;
          padding:8px 10px;margin:2px 0 4px;font-size:12px;line-height:18px;max-height:340px;overflow:auto}
        .pfrom{font-size:11px;color:{{Muted}};padding-bottom:6px}

        .vHead{font-weight:600;font-size:11.2px;color:{{Muted}};margin:8px 0 2px}
        .analysis{font-size:12.5px;line-height:19px;white-space:pre-wrap;text-wrap:pretty;
          max-height:360px;overflow:auto}
        .citeQuote{font-size:12px;line-height:18px}
        .citeFrom{font-size:11px;color:{{Muted}}}
        .warnBox{margin:8px 0;padding:8px 10px;border-radius:8px;font-size:12px;line-height:18px;
          background:rgb(255,250,223);border-top:3px solid rgb(255,225,55);text-wrap:pretty}

        .transcript{margin-top:12px;border-top:1px solid {{Line}};padding-top:8px}
        .transcript summary{cursor:pointer;font-weight:600;font-size:11.2px;color:{{Link}}}
        .tHead{font-weight:600;font-size:11px;color:{{Muted}};margin-top:8px}
        .transcript pre{white-space:pre-wrap;word-break:break-word;background:rgb(250,250,250);
          border-radius:8px;padding:10px;font-size:11.5px;line-height:17px;max-height:420px;overflow:auto}

        .provenance{width:320px;flex:none;padding:16px;position:sticky;top:16px;
          max-height:calc(100vh - 32px);overflow:auto}
        .provHead{padding:0 0 8px}
        .provTitle{font-weight:600;font-size:21.88px;line-height:32px}
        .provSub{font-size:14px;color:{{Muted}}}
        .provRow{display:flex;flex-direction:column;padding:6px 0;border-top:1px solid {{Line}}}
        .provLabel{font-size:11.2px;color:{{Muted}}}
        .provValue{font-size:13px;line-height:19px;word-break:break-word}
        .fingerprint{white-space:pre-wrap;word-break:break-word;background:#fff;border-radius:8px;
          box-shadow:inset 0 0 0 1px {{Line}};padding:10px;font-size:11px;line-height:17px}

        .grading{display:flex;gap:24px;align-items:flex-start;flex-wrap:wrap;
          margin-top:24px;padding:24px;border-radius:16px;box-shadow:inset 0 0 0 1px {{Line}}}
        .gradeCard{width:204px;height:142px;flex:none;border-radius:12px;padding:24px;display:flex;
          flex-direction:column;justify-content:center;align-items:center;
          box-shadow:inset 0 0 0 4px #fff,0 2px 8px rgba(15,23,41,.08)}
        .gradeCard.pass{background:linear-gradient(180deg,rgb(245,251,242),rgb(215,240,204))}
        .gradeCard.refer{background:linear-gradient(180deg,rgb(255,250,240),rgb(255,224,188))}
        .gradeCard.fail{background:linear-gradient(180deg,rgb(255,244,245),rgb(253,205,208))}
        .gradeLetter{font-weight:600;font-size:27.34px;line-height:40px;letter-spacing:-.25px}
        .gradeCaption{font-size:14px}
        .gradeBody{flex:1;min-width:260px}
        .gradeRule{display:flex;gap:6px;align-items:center;flex-wrap:wrap;margin:4px 0 8px}
        .gradeNote{font-size:12.5px;line-height:19px;color:{{Ink2}};text-wrap:pretty}

        .record{margin-top:16px;padding:24px;border-radius:16px;box-shadow:inset 0 0 0 1px {{Line}}}
        .recordRow{display:grid;grid-template-columns:220px 1fr;gap:16px;padding:10px 0;
          border-top:1px solid {{Line}}}
        .recordLabel{font-size:11.2px;color:{{Muted}}}
        .recordValue{font-size:14px;text-wrap:pretty}

        @media print{
          body{background:#fff}
          .page{padding:0}
          .panel{box-shadow:none;max-width:none;padding:0}
          .filters,.chev{display:none}
          .provenance,.findings{position:static;max-height:none;overflow:visible}
          .cardBody{display:block!important}
          details{open:open}
          .card{break-inside:avoid}
        }
        """;

    /// <summary>
    /// Expanding and filtering, and nothing else.
    ///
    /// The page is complete before this runs: every check, requirement, passage and response is
    /// already in the markup. Script that fails, or is disabled, costs the reader a collapse
    /// control — not the report.
    /// </summary>
    private static string Script() =>
        """
        document.querySelectorAll('.cardHead').forEach(function (head) {
          head.addEventListener('click', function () {
            var card = head.parentElement;
            var open = card.classList.toggle('open');
            head.querySelector('.chev').textContent = open ? 'Hide requirements' : 'Show requirements';
          });
        });

        var tests = {
          all: function () { return true; },
          clear: function (c) { return c.dataset.outcome === 'clear'; },
          concern: function (c) { return c.dataset.outcome === 'concern'; },
          na: function (c) { return c.dataset.outcome === 'na'; },
          indeterminate: function (c) { return c.dataset.outcome === 'indeterminate'; },
          error: function (c) { return c.dataset.outcome === 'error'; },
          evicted: function (c) { return c.dataset.evicted === '1'; },
          discarded: function (c) { return c.dataset.discarded === '1'; }
        };

        // Categories and closer-look flags are matched by name rather than by a fixed key, so
        // adding a category to the vocabulary needs no change here.
        function has(list, value) {
          return ('|' + list + '|').indexOf('|' + value + '|') >= 0;
        }

        function testFor(key) {
          if (key.indexOf('cat:') === 0) {
            var category = key.slice(4);
            return function (c) { return has(c.dataset.categories, category); };
          }

          if (key.indexOf('look:') === 0) {
            var flag = key.slice(5);
            return function (c) { return has(c.dataset.look, flag); };
          }

          return tests[key] || tests.all;
        }

        document.querySelectorAll('.filterBtn').forEach(function (button) {
          button.addEventListener('click', function () {
            document.querySelectorAll('.filterBtn').forEach(function (b) { b.classList.remove('on'); });
            button.classList.add('on');

            var test = testFor(button.dataset.filter);
            document.querySelectorAll('.card').forEach(function (card) {
              card.hidden = !test(card);
            });
          });
        });
        """;
}
