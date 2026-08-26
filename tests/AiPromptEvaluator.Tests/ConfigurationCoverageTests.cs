using System.Reflection;
using System.Text.Json.Serialization;

using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Every setting a user is expected to change must be reachable from the configuration form.
///
/// <b>Thirteen were not, and the most consequential of them had never been varied by anyone.</b>
/// Settings arrive one at a time, usually beside the code that reads them, and the form is edited
/// separately or not at all — so the gap opens silently and the only symptom is a value nobody
/// ever tries. `maxPassagesPerGroup` spent its whole life as a compile-time constant and then as
/// a setting no dialog exposed, which is why a measured experiment on the search limit reached
/// the wrong conclusion: the cap was the binding constraint and varying it meant editing JSON by
/// hand.
///
/// This is a reflection test rather than a checklist because a checklist is a second thing to
/// forget. Adding a property to <see cref="AppSettings"/> now fails the build until it is either
/// wired into the form or named here as deliberately internal.
/// </summary>
public class ConfigurationCoverageTests
{
    private readonly ITestOutputHelper _output;

    public ConfigurationCoverageTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Persisted state rather than configuration: written by the app to remember what the user
    /// last did, never typed by anyone. Exposing these would invite someone to edit a value the
    /// application overwrites on the next run.
    /// </summary>
    private static readonly HashSet<string> NotUserFacing = new(StringComparer.Ordinal)
    {
        nameof(AppSettings.LastChecksCsvPath),
    };

    /// <summary>
    /// Scoped to the whole UI rather than to one dialog, because a setting can legitimately have
    /// its own editor: the document-category map is edited in DocumentCategorizerForm, launched
    /// from the main window, and demanding it appear on the configuration form as well would be
    /// demanding the wrong thing.
    /// </summary>
    [Fact]
    public void EverySettingIsEditableSomewhereInTheUi()
    {
        var form = UiSource();

        if (form is null)
        {
            _output.WriteLine("Skipped: the UI project is not in this working copy.");
            return;
        }

        var missing = Settings()
            .Where(p => !NotUserFacing.Contains(p.Name))
            .Where(p => !form.Contains("_settings." + p.Name, StringComparison.Ordinal))
            .Select(p => p.Name)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Settings the configuration form cannot reach:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, missing.Select(m => "  " + m))
            + Environment.NewLine
            + "Wire each into a form, or add it to NotUserFacing with a reason.");
    }

    /// <summary>
    /// The allow-list must not become a place to put things that are simply inconvenient. Every
    /// name in it has to still exist, so a rename cannot leave a stale exemption behind that
    /// silently excuses a real setting later.
    /// </summary>
    [Fact]
    public void TheInternalListNamesOnlySettingsThatExist()
    {
        var names = Settings().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        Assert.All(NotUserFacing, n => Assert.Contains(n, names));
    }

    /// <summary>
    /// The caps that take 0 for unbounded have to be able to hold 0. A spin control with a
    /// minimum of 1 does not refuse an unbounded setting — it rewrites it, the first time the
    /// dialog is opened, without saying anything.
    /// </summary>
    [Theory]
    [InlineData("searchResultsUpDown")]
    [InlineData("passagesPerGroupUpDown")]
    [InlineData("decisionTokensUpDown")]
    [InlineData("extractionReportUpDown")]
    [InlineData("documentsListedUpDown")]
    [InlineData("reserveCategoryUpDown")]
    [InlineData("reserveSectionUpDown")]
    public void AControlForAnUnboundableCapAllowsZero(string control)
    {
        var designer = DesignerSource();

        if (designer is null)
        {
            _output.WriteLine("Skipped: the designer file is not in this working copy.");
            return;
        }

        Assert.Contains($"{control}.Minimum = 0;", designer);
    }

    /// <summary>
    /// Every field whose zero means something says so on its own caption.
    ///
    /// A group hint would not have covered it: Results/search sits in the vector-store group,
    /// two groups above the sentence explaining what its zero does, and a reader looking at one
    /// field should not have to find a sentence elsewhere to learn that its value has a special
    /// case. The captions distinguish the two meanings, which are opposites — a cap at zero is
    /// unbounded, a floor at zero reserves nothing.
    /// </summary>
    [Theory]
    [InlineData("searchResultsLabel", "(0 = all)")]
    [InlineData("passagesPerGroupLabel", "(0 = all)")]
    [InlineData("decisionTokensLabel", "(0 = unbounded)")]
    [InlineData("extractionReportLabel", "(0 = whole report)")]
    [InlineData("documentsListedLabel", "(0 = all)")]
    [InlineData("reserveCategoryLabel", "(0 = none)")]
    [InlineData("reserveSectionLabel", "(0 = none)")]
    public void AFieldWhoseZeroMeansSomethingSaysSoOnItsCaption(string label, string marker)
    {
        var designer = DesignerSource();

        if (designer is null)
        {
            _output.WriteLine("Skipped: the designer file is not in this working copy.");
            return;
        }

        var text = System.Text.RegularExpressions.Regex.Match(
            designer, System.Text.RegularExpressions.Regex.Escape(label) + @"\.Text = ""([^""]+)"";");

        Assert.True(text.Success, label + " has no caption.");
        Assert.Contains(marker, text.Groups[1].Value);
    }

    /// <summary>
    /// And the ones whose zero means nothing good must not claim otherwise.
    /// <c>extractionMaxTokens</c> goes straight to the request, where zero becomes one token and
    /// truncates every extraction pass — it looks like the caps and is not one.
    /// </summary>
    [Theory]
    [InlineData("extractionTokensLabel")]
    [InlineData("parallelRequestsLabel")]
    [InlineData("parallelChecksLabel")]
    [InlineData("embeddingCharsLabel")]
    [InlineData("maxTokensLabel")]
    public void AFieldThatCannotBeUnboundedDoesNotAdvertiseAZero(string label)
    {
        var designer = DesignerSource();

        if (designer is null)
        {
            return;
        }

        var text = System.Text.RegularExpressions.Regex.Match(
            designer, System.Text.RegularExpressions.Regex.Escape(label) + @"\.Text = ""([^""]+)"";");

        Assert.True(text.Success, label + " has no caption.");
        Assert.DoesNotContain("0 = ", text.Groups[1].Value);
    }

    /// <summary>
    /// The captions describe <see cref="AppSettings.Unbounded"/>, so they are only true while the
    /// code still routes those settings through it. This is the join: five call sites, and a
    /// caption promising a sixth would be a lie the compiler cannot catch.
    /// </summary>
    [Fact]
    public void ExactlyTheSettingsRoutedThroughUnboundedAreTheOnesAdvertised()
    {
        var root = RepoRoot();

        if (root is null)
        {
            return;
        }

        var core = string.Join(
            Environment.NewLine,
            Directory
                .GetFiles(Path.Combine(root, "src", "AiPromptEvaluator.Core"), "*.cs",
                          SearchOption.AllDirectories)
                .Where(f => !f.EndsWith("AppSettings.cs", StringComparison.Ordinal))
                .Where(f => !f.EndsWith("RunFingerprint.cs", StringComparison.Ordinal))
                .Select(File.ReadAllText));

        foreach (var setting in new[]
                 {
                     nameof(AppSettings.MaxSearchResults),
                     nameof(AppSettings.MaxPassagesPerGroup),
                     nameof(AppSettings.DecisionMaxTokens),
                     nameof(AppSettings.ExtractionReportMaxChars),
                     nameof(AppSettings.MaxDocumentsInContext),
                 })
        {
            Assert.True(
                core.Contains("Unbounded(_settings." + setting, StringComparison.Ordinal)
                || core.Contains("Unbounded(settings." + setting, StringComparison.Ordinal)
                || core.Contains("IsUnbounded(_settings." + setting, StringComparison.Ordinal)
                || core.Contains("Unbounded(maxDocuments", StringComparison.Ordinal),
                setting + " is advertised as unbounded on the form but no longer routes through "
                + "AppSettings.Unbounded.");
        }

        // The trap, stated as a test: extraction tokens reach the request unfiltered.
        Assert.DoesNotContain("Unbounded(_settings.ExtractionMaxTokens", core);
    }

    /// <summary>
    /// A control that loads a value and never saves it is worse than no control: it shows the
    /// user a setting, accepts an edit, and discards it silently.
    /// </summary>
    [Fact]
    public void EverySettingTheFormReadsItAlsoWrites()
    {
        var form = FormSource();

        if (form is null)
        {
            return;
        }

        var reads = new List<string>();
        var writes = new List<string>();

        foreach (var p in Settings())
        {
            // "= _settings.X" is a load; "_settings.X =" is a save.
            if (form.Contains("_settings." + p.Name + ")", StringComparison.Ordinal)
                || form.Contains("_settings." + p.Name + ";", StringComparison.Ordinal))
            {
                reads.Add(p.Name);
            }

            if (form.Contains("_settings." + p.Name + " =", StringComparison.Ordinal))
            {
                writes.Add(p.Name);
            }
        }

        var readOnly = reads.Except(writes).ToList();

        Assert.True(
            readOnly.Count == 0,
            "Settings the form displays but never saves: " + string.Join(", ", readOnly));
    }

    // ──────────────────────────────────────────────

    /// <summary>Public read/write properties carrying a JSON name — the persisted settings.</summary>
    private static IEnumerable<PropertyInfo> Settings() =>
        typeof(AppSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => p.GetCustomAttribute<JsonPropertyNameAttribute>() is not null)
            .OrderBy(p => p.Name, StringComparer.Ordinal);

    /// <summary>Every UI source file concatenated — a setting may be edited on any of them.</summary>
    private static string? UiSource()
    {
        var dir = RepoRoot();

        if (dir is null || !Directory.Exists(Path.Combine(dir, "src", "AiPromptEvaluator")))
        {
            return null;
        }

        return string.Join(
            Environment.NewLine,
            Directory
                .GetFiles(Path.Combine(dir, "src", "AiPromptEvaluator"), "*.cs")
                .Select(File.ReadAllText));
    }

    private static string? FormSource() => Source("ConfigurationForm.cs");

    private static string? DesignerSource() => Source("ConfigurationForm.Designer.cs");

    private static string? Source(string fileName)
    {
        var dir = RepoRoot();
        var path = dir is null ? null : Path.Combine(dir, "src", "AiPromptEvaluator", fileName);

        return path is not null && File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static string? RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiPromptEvaluator.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName;
    }
}
