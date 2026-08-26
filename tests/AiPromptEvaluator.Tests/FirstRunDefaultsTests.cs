using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using AiPromptEvaluator;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// On a machine with no settings file — or one written before a setting existed — the shipped
/// default applies, all the way to the number in the configuration screen.
///
/// <b>The failure this guards against is silent and rewrites the user's intent.</b> The
/// configuration form clamps every stored value into its control's range, which is right for a
/// hand-edited file and wrong the moment a legitimate value falls outside that range: the control
/// shows a number nobody chose, and saving the dialog persists it.
///
/// There are two ways to fall outside, and they need separate tests. A *default* outside its
/// control's range is what this file checks, and it has not happened yet. An *override* outside
/// it has — <c>searchResultsUpDown</c> had a minimum of 1 while <c>maxSearchResults</c> takes 0
/// for unbounded, so opening the dialog would have turned the widest configuration into the
/// narrowest. That case is covered by
/// <see cref="ConfigurationCoverageTests.AControlForAnUnboundableCapAllowsZero"/>, and both were
/// confirmed by restoring the defect and watching which test noticed: only that one did.
/// </summary>
public class FirstRunDefaultsTests
{
    private readonly ITestOutputHelper _output;

    public FirstRunDefaultsTests(ITestOutputHelper output) => _output = output;

    // ── loading ───────────────────────────────────────────────────────────────

    /// <summary>
    /// An empty document is the shape of a settings file that predates every setting. Every
    /// property must come back as its initialiser rather than as the type's zero.
    /// </summary>
    [Fact]
    public void AnEmptyDocumentYieldsEveryShippedDefault()
    {
        var loaded = JsonSerializer.Deserialize<AppSettings>("{}")!;
        var shipped = new AppSettings();

        foreach (var p in Settings())
        {
            Assert.Equal(p.GetValue(shipped), p.GetValue(loaded));
        }
    }

    /// <summary>
    /// The real case on the machine this was written on: a file written by an older build, with
    /// twenty keys and none of the caps. What it names is honoured; what it omits defaults.
    /// </summary>
    [Fact]
    public void AFileFromAnOlderBuildKeepsItsValuesAndDefaultsTheRest()
    {
        const string Older =
            """
            { "maxSearchResults": 8, "selectedModel": "claude-haiku-4-5-20251001",
              "tenantId": 99, "maxTokensPerChunk": 600 }
            """;

        var loaded = JsonSerializer.Deserialize<AppSettings>(
            Older, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // Honoured.
        Assert.Equal(8, loaded.MaxSearchResults);
        Assert.Equal("claude-haiku-4-5-20251001", loaded.SelectedModel);
        Assert.Equal(99, loaded.TenantId);

        // Defaulted, including every setting added after that file was written.
        Assert.Equal(24, loaded.MaxPassagesPerGroup);
        Assert.Equal(1, loaded.ReservedSlotsPerTargetedCategory);
        Assert.Equal(1, loaded.ReservedSlotsPerDeclaredSection);
        Assert.Equal(8000, loaded.DecisionMaxTokens);
        Assert.Equal(24_000, loaded.ExtractionReportMaxChars);
        Assert.Equal(50, loaded.MaxDocumentsInContext);

        // And sampling, which is pinned by default and is the one a run reports on its face.
        Assert.True(loaded.PinTemperature);
        Assert.True(loaded.PinTopP);
        Assert.True(loaded.PinSeed);
    }

    /// <summary>
    /// A setting explicitly present must win, even where it equals the type's zero — otherwise
    /// "unbounded" could not be expressed at all.
    /// </summary>
    [Fact]
    public void AnExplicitZeroIsAnOverrideAndNotAnAbsence()
    {
        var loaded = JsonSerializer.Deserialize<AppSettings>(
            """{ "maxPassagesPerGroup": 0, "maxSearchResults": 0 }""")!;

        Assert.Equal(0, loaded.MaxPassagesPerGroup);
        Assert.Equal(0, loaded.MaxSearchResults);
        Assert.True(AppSettings.IsUnbounded(loaded.MaxPassagesPerGroup));
    }

    /// <summary>Saving and reloading must not move anything.</summary>
    [Fact]
    public void TheDefaultsSurviveASaveAndReload()
    {
        var shipped = new AppSettings();

        var reloaded = JsonSerializer.Deserialize<AppSettings>(
            JsonSerializer.Serialize(shipped))!;

        foreach (var p in Settings())
        {
            Assert.Equal(p.GetValue(shipped), p.GetValue(reloaded));
        }
    }

    // ── and surviving the configuration screen ────────────────────────────────

    /// <summary>
    /// <b>The one that matters, and the one that had already failed.</b> The form clamps each
    /// stored value into its control's range on load. A default outside that range is therefore
    /// not shown, and is overwritten the moment the dialog is saved — so a first run would
    /// silently adopt a value nobody chose.
    ///
    /// Parsed from the designer because these are private fields of a form that cannot be
    /// constructed without a message loop. A control with no explicit Minimum or Maximum takes
    /// NumericUpDown's own defaults of 0 and 100, which is why both are checked rather than only
    /// the ones written down.
    /// </summary>
    [Fact]
    public void EveryDefaultIsInsideTheRangeOfTheControlThatShowsIt()
    {
        var (form, designer) = Sources();

        if (form is null || designer is null)
        {
            _output.WriteLine("Skipped: the UI project is not in this working copy.");
            return;
        }

        var shipped = new AppSettings();
        var problems = new List<string>();
        var checkedCount = 0;

        var bindings = System.Text.RegularExpressions.Regex.Matches(
            form, @"Clamp\((\w+),\s*_settings\.(\w+)\)");

        foreach (System.Text.RegularExpressions.Match b in bindings)
        {
            var control = b.Groups[1].Value;
            var setting = b.Groups[2].Value;

            var property = typeof(AppSettings).GetProperty(setting);

            if (property is null)
            {
                continue;
            }

            var value = Convert.ToDecimal(property.GetValue(shipped));
            var min = Bound(designer, control, "Minimum", 0m);
            var max = Bound(designer, control, "Maximum", 100m);

            checkedCount++;

            if (value < min || value > max)
            {
                problems.Add(
                    $"{setting} defaults to {value} but {control} allows {min}..{max} — "
                    + "the form would show a different number and save it.");
            }
        }

        _output.WriteLine($"Checked {checkedCount} default(s) against their controls.");

        Assert.True(checkedCount > 10, "The binding pattern matched almost nothing; check the regex.");
        Assert.True(
            problems.Count == 0,
            string.Join(Environment.NewLine, problems));
    }

    // ──────────────────────────────────────────────

    private static decimal Bound(string designer, string control, string which, decimal fallback)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            designer,
            System.Text.RegularExpressions.Regex.Escape(control) + @"\." + which + @" = (-?[\d.]+)");

        if (!m.Success)
        {
            // int.MaxValue and the like are written as expressions, not literals.
            var expression = System.Text.RegularExpressions.Regex.Match(
                designer,
                System.Text.RegularExpressions.Regex.Escape(control) + @"\." + which + @" = int\.MaxValue");

            return expression.Success ? int.MaxValue : fallback;
        }

        return decimal.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static IEnumerable<PropertyInfo> Settings() =>
        typeof(AppSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => p.GetCustomAttribute<JsonPropertyNameAttribute>() is not null)
            .Where(p => p.PropertyType != typeof(Dictionary<string, string>));

    private static (string? Form, string? Designer) Sources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiPromptEvaluator.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            return (null, null);
        }

        var folder = Path.Combine(dir.FullName, "src", "AiPromptEvaluator");
        var form = Path.Combine(folder, "ConfigurationForm.cs");
        var designer = Path.Combine(folder, "ConfigurationForm.Designer.cs");

        return (
            File.Exists(form) ? File.ReadAllText(form) : null,
            File.Exists(designer) ? File.ReadAllText(designer) : null);
    }
}
