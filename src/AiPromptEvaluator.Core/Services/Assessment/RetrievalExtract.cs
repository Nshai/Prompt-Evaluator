using System.Text;

namespace AiPromptEvaluator;

/// <summary>
/// Renders the passages a run's searches retrieved, per group, as a plain-text extract for
/// download.
///
/// <b>This is the evidence side of a run, written out to be read.</b> The findings say what an
/// assessor concluded; this says what it was given to conclude from. The two questions a reader
/// asks when a finding looks thin — <em>what did the search return?</em> and <em>did the passage
/// carrying the fact even reach the pack?</em> — are answerable from nowhere else, because by the
/// time a passage is in a pack the query that found it and the candidates that lost to it are gone.
///
/// There are two sources and one format. A dry run
/// (<see cref="RetrievalDryRun.Report"/>) has the ranked pack but no model call; an archived run
/// (<see cref="CheckRunRecord"/>) has more — the query that found each passage, and the passages
/// that were retrieved and then evicted before the pack. Both render through the same passage
/// helper so a reader moving between them is reading the same layout.
/// </summary>
public static class RetrievalExtract
{
    private const int Width = 78;

    /// <summary>The extract for a retrieval dry run — the ranked pack of every group.</summary>
    public static string FromDryRun(RetrievalDryRun.Report report)
    {
        var sb = new StringBuilder();

        Header(sb,
            "Retrieval dry run — searched extract",
            $"{report.Groups.Count} group(s), {report.Searches} search(es), {report.Hits} hit(s). "
            + $"Passage cap {Cap(report.Settings.MaxPassagesPerGroup)}.",
            "Each group lists the passages its queries retrieved and ranking kept, in pack order. "
            + "This is what an assessor would have been given; no model was called.");

        foreach (var group in report.Groups)
        {
            sb.AppendLine();
            sb.AppendLine($"{group.CheckId} {group.GroupId} — {group.Requirement}");
            sb.AppendLine(
                $"  {group.Searches} search(es), {group.Hits} hit(s), {group.PackSize} passage(s) in "
                + "the pack"
                + (group.CategoriesReached.Count > 0
                    ? $"; categories {string.Join(", ", group.CategoriesReached)}"
                    : string.Empty)
                + ".");

            List(sb, "  Section hints matching no retrieved passage", group.DeadSections);
            List(sb, "  Section hints retrieved but evicted before the pack", group.EvictedSections);
            List(sb, "  Queries whose expected signals never arrived", group.MissedSignals);

            if (group.PackSize == 0)
            {
                sb.AppendLine("  The pack is empty — no query for this group retrieved anything.");
                continue;
            }

            sb.AppendLine(new string('-', Width));

            var rank = 0;
            foreach (var passage in group.Pack)
            {
                Passage(sb, ++rank, passage.DocumentName, passage.CategoryCode, passage.Score,
                    passage.SearchedText, query: null, inPack: true);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// The extract for a finished, archived run. Richer than a dry run's: it carries the query that
    /// found each passage, and the passages that were retrieved and then dropped in ranking —
    /// marked, because an evicted passage is the record of a finding's evidence not reaching the
    /// assessor, which is invisible everywhere else.
    /// </summary>
    public static string FromRun(CheckRunRecord run)
    {
        var sb = new StringBuilder();

        Header(sb,
            "Run — searched extract",
            $"Case {run.CaseReference} (tenant {run.TenantId}), model {run.ChatModel}, "
            + $"run {run.StartedAt:yyyy-MM-dd HH:mm}.",
            "Each requirement lists the passages its queries retrieved. Those that reached the "
            + "assessor's pack are shown first; any retrieved and then evicted follow, marked.");

        foreach (var group in run.AllGroups)
        {
            sb.AppendLine();
            sb.AppendLine($"{group.CheckId} {group.GroupId} — {group.Requirement}");

            var inPack = group.Passages.Where(p => p.InPack).OrderBy(p => p.Ordinal).ToList();
            var evicted = group.Passages.Where(p => !p.InPack).ToList();

            var empty = group.Queries.Where(q => q.Hits == 0).Select(q => q.QueryId).ToList();
            List(sb, "  Queries that returned nothing", empty);

            sb.AppendLine(
                $"  {group.Queries.Count} search(es), {group.Passages.Count} passage(s) retrieved, "
                + $"{inPack.Count} in the pack.");

            if (group.Passages.Count == 0)
            {
                sb.AppendLine("  Nothing was retrieved for this requirement.");
                continue;
            }

            sb.AppendLine(new string('-', Width));

            var rank = 0;
            foreach (var passage in inPack)
            {
                Passage(sb, ++rank, passage.DocumentName, passage.CategoryCode, passage.Score,
                    passage.Text, passage.QueryId, inPack: true);
            }

            foreach (var passage in evicted.OrderByDescending(p => p.Score))
            {
                Passage(sb, null, passage.DocumentName, passage.CategoryCode, passage.Score,
                    passage.Text, passage.QueryId, inPack: false);
            }
        }

        return sb.ToString();
    }

    private static void Header(StringBuilder sb, string title, string summary, string note)
    {
        sb.AppendLine(title);
        sb.AppendLine(summary);
        sb.AppendLine(note);
        sb.AppendLine(new string('=', Width));
    }

    private static void List(StringBuilder sb, string heading, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        sb.AppendLine($"{heading} ({items.Count}):");
        foreach (var item in items)
        {
            sb.AppendLine("  " + item);
        }
    }

    /// <summary>
    /// One passage. <paramref name="rank"/> is its pack position, or null for an evicted one; the
    /// body is left verbatim — page markers and table pipes included — because that is the text the
    /// ranking and any section-hint match actually saw.
    /// </summary>
    private static void Passage(
        StringBuilder sb, int? rank, string document, string category, double score,
        string text, string? query, bool inPack)
    {
        var marker = rank is { } r ? $"[{r}]" : "[evicted]";
        var from = string.IsNullOrWhiteSpace(query) ? string.Empty : $" · found by {query}";

        sb.AppendLine($"  {marker} {document} [{category}] score {score:F3}{from}");

        foreach (var line in text.Split('\n'))
        {
            sb.AppendLine("      " + line.TrimEnd('\r'));
        }

        sb.AppendLine();
    }

    private static string Cap(int maxPassagesPerGroup) =>
        AppSettings.IsUnbounded(maxPassagesPerGroup)
            ? "unbounded"
            : maxPassagesPerGroup.ToString();
}
