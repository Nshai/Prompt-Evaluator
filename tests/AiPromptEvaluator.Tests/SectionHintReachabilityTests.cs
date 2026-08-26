using System.Text.Json;

using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Every declared evidence section must match text in a document of a category its group targets.
///
/// <b>A hint that matches nothing does nothing, silently.</b> One commit added six: three named
/// parts of the fact find's cash-flow table and moved four benchmark findings from missed to
/// caught; the other three named "Residency" and "Contact Address", matched no passage, and moved
/// nothing — and the run gave no signal separating "the hint worked and the group ignored the
/// section" from "the hint never matched".
///
/// There are two ways to write one that cannot fire, and this catches both. The string may simply
/// not be in the case file. Or — the subtler one, which caught three hints the first time this
/// test was run — the string exists but only in a category the group never *retrieves* from,
/// because <see cref="CheckPlanRunner.Rank"/> matches hints within targeted categories only, and
/// an Assertion query resolves the stored model rather than searching. A hint added beside an
/// assertion-only query is dead on arrival.
///
/// <b>This test is necessary and it is not sufficient, and the difference cost a run.</b> It
/// proves a hint <i>could</i> match. It cannot prove any query <i>retrieves</i> the chunk that
/// carries the string, because that depends on embeddings and an index rather than on the corpus.
/// Six hints passed this test and then fired on nothing in three consecutive runs — the string was
/// there, in a searched category, and no search returned the passage holding it.
///
/// <see cref="RetrievalDryRun"/> is what answers the remaining question. It executes the searches
/// with no model call, so a dead hint is found in seconds rather than in the findings of a run
/// that cost pounds. Keep both: this one runs anywhere and catches typos and mis-scoped hints,
/// and the dry run needs an index and catches everything else.
/// </summary>
public class SectionHintReachabilityTests
{
    private readonly ITestOutputHelper _output;

    public SectionHintReachabilityTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void EveryDeclaredSectionIsReachableFromACategoryItsGroupSearches()
    {
        if (CaseFolder() is not { } folder)
        {
            _output.WriteLine("Skipped: the converted case folder is not in this working copy.");
            return;
        }

        var documents = Directory
            .GetDirectories(folder)
            .ToDictionary(
                dir => Path.GetFileName(dir),
                dir => Directory
                    .GetFiles(dir, "*.md", SearchOption.AllDirectories)
                    .Select(File.ReadAllText)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var (plans, failures) = CheckQueryPlanLoader.Load(
            Path.Combine(AppContext.BaseDirectory, "check-plan"));

        Assert.Empty(failures);

        var unreachable = new List<string>();
        var checked_ = 0;

        foreach (var plan in plans.Values)
        {
            foreach (var group in plan.QueryGroups)
            {
                if (group.DeclaredEvidenceSections.Count == 0)
                {
                    continue;
                }

                // The same set Rank builds: evidence-side queries only, because an Assertion
                // query retrieves nothing and so can never bring the hint's category into play.
                var targeted = group.Queries
                    .Where(q => q.IsEvidenceSearch)
                    .SelectMany(q => q.TargetCategories)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var hint in group.DeclaredEvidenceSections)
                {
                    checked_++;

                    var reachable = targeted.Any(code =>
                        documents.TryGetValue(code, out var texts)
                        && texts.Any(text => text.Contains(hint, StringComparison.OrdinalIgnoreCase)));

                    if (reachable)
                    {
                        continue;
                    }

                    var elsewhere = documents
                        .Where(pair => pair.Value.Any(
                            text => text.Contains(hint, StringComparison.OrdinalIgnoreCase)))
                        .Select(pair => pair.Key)
                        .ToList();

                    unreachable.Add(
                        $"{plan.CheckId} {group.GroupId} declares \"{hint}\", "
                        + $"searches [{string.Join(",", targeted.OrderBy(c => c))}], "
                        + (elsewhere.Count > 0
                            ? $"but the text is only in [{string.Join(",", elsewhere.OrderBy(c => c))}]."
                            : "and the text is nowhere in the case."));
                }
            }
        }

        _output.WriteLine($"Checked {checked_} declared section(s).");

        Assert.True(
            unreachable.Count == 0,
            "Section hints that can never fire:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, unreachable.Select(u => "  " + u)));
    }

    /// <summary>
    /// The hints added for the ranking table, the knowledge questionnaire, the modelled residence,
    /// the liquidation order and the switch effect, pinned by name.
    ///
    /// Each one is the whole mechanism behind a benchmark finding the plans could not previously
    /// reach, and each is a plain string that a later edit could reword into something that still
    /// reads correctly and matches nothing.
    /// </summary>
    [Theory]
    [InlineData("CHK-002", "G2.2", "ranked by Maturity Value")]
    [InlineData("CHK-006", "G6.4", "ranked by Maturity Value")]
    [InlineData("CHK-008", "G8.8", "ranked by Maturity Value")]
    [InlineData("CHK-009", "G9.7", "ranked by Maturity Value")]
    [InlineData("CHK-004", "G4.1", "No understanding / knowledge")]
    [InlineData("CHK-001", "G1.11", "incorporates the assets of your main residence")]
    [InlineData("CHK-005", "G5.2", "incorporates the assets of your main residence")]
    [InlineData("CHK-005", "G5.5", "LIQUIDATED")]
    [InlineData("CHK-008", "G8.5", "Effect of switching")]
    [InlineData("CHK-007", "G7.4", "Existing Arrangements")]
    public void TheHintsThatCarryABenchmarkFindingAreStillDeclared(
        string checkId, string groupId, string hint)
    {
        var (plans, _) = CheckQueryPlanLoader.Load(
            Path.Combine(AppContext.BaseDirectory, "check-plan"));

        var group = plans[checkId].QueryGroups.Single(g => g.GroupId == groupId);

        Assert.Contains(hint, group.DeclaredEvidenceSections);
    }

    // ──────────────────────────────────────────────

    private static string? CaseFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiPromptEvaluator.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            return null;
        }

        var folder = Path.Combine(
            dir.FullName, "docs", "QA-Checks", "Feasability-test-case-converted");

        return Directory.Exists(folder) ? folder : null;
    }
}
