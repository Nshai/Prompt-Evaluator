using System.Text.RegularExpressions;

using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Nothing the pipeline says to a model may belong to one case.
///
/// <b>The pipeline validates whichever suitability report it is given, against whichever evidence
/// came with it.</b> Both change per case: a provider central to one file appears nowhere in the
/// next, and a client's name appears in exactly one. Text naming either teaches the model to look
/// for it — a bias toward the case the rule was discovered in and away from the case in front of
/// it.
///
/// <see cref="AssessorPromptTests"/> holds the two standing prompts to this. **It does not cover
/// everything a model reads**, and the surface has grown: the group prompt now prints comparison
/// directives, a report-category warning and routed contradictions; two analysis passes print
/// their own headings and advice. All of it is written from one case and all of it is sent to a
/// model.
///
/// So this scans the emitted string literals of every class that composes model input or reader
/// output, and holds them to the same rule.
///
/// <b>Comments are deliberately exempt</b>, and that exemption is the point rather than a
/// loophole: the reason a rule exists is a specific failure in a specific file, and recording it
/// concretely is what stops the rule being deleted by someone who cannot see what it was for.
/// Illustrate the shape in the text; keep the instance in the comment.
/// </summary>
public class GenericDirectiveTests
{
    private readonly ITestOutputHelper _output;

    public GenericDirectiveTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Instances, not types. A document <b>type</b> — "a fact find", "the suitability report", "a
    /// risk profile questionnaire" — is domain vocabulary that holds for every case and the
    /// directives need it. What must not appear is a particular provider, client, fund or file.
    /// </summary>
    private static readonly string[] BelongsToOneCase =
    [
        // Providers and tools.
        "Aviva", "Standard Life", "Scottish Widows", "Zurich", "Quilter", "Phoenix", "Prudential",
        "Royal London", "Peoples Pension", "People's Pension", "Moneybox", "MoneyBox",
        "Defaqto", "Dynamic Planner", "FE Analytics", "HSBC", "Vanguard", "Aon", "Origo",

        // Funds and products.
        "LifeStrategy", "Multi-Asset Plus", "Universal MAP", "Global Strategy",

        // Clients, files and identifiers.
        "Barnes", "Anness", "Sullivan", "Yvette", "Anthony", "Sea Cadets", "CDH Recruitment",
        "Chartered Savings", "AV2075926", "ABC-99", "Test-Case",
    ];

    /// <summary>
    /// Every class whose string literals reach a model or a reader as instruction. Named
    /// explicitly rather than swept from the folder, so adding one is a decision — a new class
    /// that composes model input and is not on this list is not covered, and nothing would say so.
    /// </summary>
    private static readonly string[] ComposesModelInput =
    [
        "Constants/Prompts.cs",
        "Services/Assessment/CheckPlanRunner.cs",
        "Services/Assessment/DerivedFigures.cs",
        "Services/Assessment/CrossGroupContradictions.cs",
        "Services/Assessment/BenchmarkScore.cs",
        "Services/Extraction/CanonicalModelExtractor.cs",
        "RunFingerprint.cs",
        "CheckFinding.cs",
    ];

    [Fact]
    public void NoDirectiveSentToAModelNamesAnythingFromOneCase()
    {
        if (CoreFolder() is not { } core)
        {
            _output.WriteLine("Skipped: the Core project is not in this working copy.");
            return;
        }

        var offences = new List<string>();

        foreach (var relative in ComposesModelInput)
        {
            var path = Path.Combine(core, relative.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(path), $"{relative} is on the list and not in the tree.");

            foreach (var literal in LiteralsIn(File.ReadAllText(path)))
            {
                foreach (var named in BelongsToOneCase
                             .Where(n => literal.Contains(n, StringComparison.OrdinalIgnoreCase)))
                {
                    offences.Add($"{relative}: \"{named}\" in {Shorten(literal)}");
                }
            }
        }

        Assert.True(
            offences.Count == 0,
            "Text sent to a model, or printed as instruction, names something belonging to one "
            + "case. Describe the shape instead and keep the instance in a comment:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offences.Select(o => "  " + o)));
    }

    /// <summary>
    /// The list has to name things that could plausibly appear, or it is decoration. This is the
    /// canary: the names are drawn from the cases on file, so at least one must be findable in
    /// the benchmarks it was drawn from.
    /// </summary>
    [Fact]
    public void TheProhibitedListIsDrawnFromRealCases()
    {
        if (BenchmarkText() is not { } text)
        {
            _output.WriteLine("Skipped: no benchmark in this working copy.");
            return;
        }

        var found = BelongsToOneCase
            .Where(n => text.Contains(n, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            found.Count >= 5,
            "The prohibited list should be drawn from the cases actually on file; only "
            + $"{found.Count} of its entries appear in any benchmark.");
    }

    // ──────────────────────────────────────────────

    /// <summary>
    /// String and raw-string literals, with comments removed first.
    ///
    /// Crude by design — this is a guard, not a parser, and a guard that over-reports is read and
    /// corrected while one that under-reports is trusted and wrong.
    /// </summary>
    private static IEnumerable<string> LiteralsIn(string source)
    {
        var withoutComments = Regex.Replace(
            Regex.Replace(source, @"^\s*(///|//).*$", string.Empty, RegexOptions.Multiline),
            @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        foreach (Match match in Regex.Matches(withoutComments, "\"\"\"(.*?)\"\"\"", RegexOptions.Singleline))
        {
            yield return match.Groups[1].Value;
        }

        var withoutRaw = Regex.Replace(withoutComments, "\"\"\".*?\"\"\"", string.Empty, RegexOptions.Singleline);

        foreach (Match match in Regex.Matches(withoutRaw, @"""((?:[^""\\\r\n]|\\.)*)"""))
        {
            yield return match.Groups[1].Value;
        }
    }

    private static string Shorten(string literal)
    {
        var flat = Regex.Replace(literal, @"\s+", " ").Trim();

        return flat.Length <= 120 ? flat : flat[..120] + "…";
    }

    private static string? CoreFolder()
    {
        var root = Repository();
        var core = root is null ? null : Path.Combine(root, "src", "AiPromptEvaluator.Core");

        return core is not null && Directory.Exists(core) ? core : null;
    }

    private static string? BenchmarkText()
    {
        var root = Repository();

        if (root is null || !Directory.Exists(Path.Combine(root, "docs", "Test-Runs")))
        {
            return null;
        }

        var benchmarks = Directory.GetFiles(
            Path.Combine(root, "docs", "Test-Runs"),
            "expected-results-benchmark.md",
            SearchOption.AllDirectories);

        return benchmarks.Length == 0
            ? null
            : string.Join(Environment.NewLine, benchmarks.Select(File.ReadAllText));
    }

    private static string? Repository()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiPromptEvaluator.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName;
    }
}
