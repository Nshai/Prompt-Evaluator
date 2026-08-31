using System.Globalization;
using System.Reflection;

using AiPromptEvaluator;

namespace IQWorkflow;

/// <summary>
/// Applies <c>--set Name=Value</c> to the settings a run will use.
///
/// <b>Reflection, deliberately, and bounded.</b> The alternative is a flag per setting — there are
/// more than forty, and every one added later would need a flag added here to be reachable. What
/// keeps that from becoming "the command line can write anything" is the refusal below: a credential
/// or an endpoint cannot be set this way at all.
///
/// <b>Why credentials are refused rather than supported.</b> A process's command line is readable by
/// every other process on the machine, is recorded in shell history, and is echoed by most schedulers
/// into their own logs. A key passed as an argument is a key disclosed. Credentials reach a run the
/// way they already do — the settings file and the environment — and this refuses rather than
/// quietly ignoring, so an operator who tries it finds out immediately.
/// </summary>
public static class SettingsOverrides
{
    /// <summary>
    /// Applies each override in place, and returns what could not be applied.
    ///
    /// An empty list means every override took effect. <b>A run does not start with a partial
    /// application:</b> the caller stops on any error, because a benchmark run configured by six
    /// flags of which five landed is a measurement of something nobody chose.
    /// </summary>
    public static IReadOnlyList<string> Apply(
        AppSettings settings, IReadOnlyDictionary<string, string> overrides)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(overrides);

        var errors = new List<string>();

        foreach (var (name, value) in overrides)
        {
            var property = typeof(AppSettings).GetProperty(
                name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property is null || !property.CanWrite)
            {
                errors.Add($"\"{name}\" is not a setting that can be given on the command line.");
                continue;
            }

            if (IsSecret(property.Name))
            {
                errors.Add(
                    $"\"{property.Name}\" is a credential and cannot be set on the command line, "
                    + "where it would be visible to other processes and recorded in shell history. "
                    + $"Use the settings file, or {CredentialEnvironment.ChatKey} and its siblings "
                    + "in the environment.");
                continue;
            }

            if (!TryConvert(property.PropertyType, value, out var converted))
            {
                errors.Add(
                    $"\"{property.Name}\" takes {Describe(property.PropertyType)}; "
                    + $"\"{value}\" is not one.");
                continue;
            }

            property.SetValue(settings, converted);
        }

        return errors;
    }

    /// <summary>
    /// Whether a setting names a credential.
    ///
    /// Matched on the name rather than listed, so a credential added to <c>AppSettings</c> later is
    /// refused by default. A new one that this does not catch is a naming problem worth fixing in
    /// the setting, not an exception worth adding here.
    ///
    /// <b>Endpoints are deliberately not refused.</b> They were, briefly, on the reasoning that they
    /// belong with credentials — and that was wrong twice over: a URL on a command line discloses
    /// nothing, and pointing a run at a different endpoint is one of the things an unattended host
    /// most obviously exists to do. The security argument is about secrecy, and it applies only to
    /// the things that are secret.
    ///
    /// <c>Token</c> is matched even though nothing currently named that is a credential, because
    /// the cost of refusing a hypothetical token budget is one clear error message and the cost of
    /// leaking a bearer token is not comparable. <c>MaxTokens</c> and its siblings are exempted by
    /// name for that reason.
    /// </summary>
    internal static bool IsSecret(string name) =>
        !TokenBudgets.Contains(name)
        && (name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Token", StringComparison.OrdinalIgnoreCase));

    /// <summary>Settings whose names contain "token" and which count tokens rather than carry one.</summary>
    private static readonly HashSet<string> TokenBudgets = new(StringComparer.Ordinal)
    {
        nameof(AppSettings.MaxTokens),
        nameof(AppSettings.MaxTokensPerChunk),
        nameof(AppSettings.ChunkOverlapTokens),
        nameof(AppSettings.DecisionMaxTokens),
        nameof(AppSettings.ExtractionMaxTokens),
    };

    /// <summary>
    /// The four types a setting is allowed to have here.
    ///
    /// <c>Convert.ChangeType</c> over the whole surface would accept things nobody meant — a date
    /// parsed by the current culture, an enum matched by ordinal — so the conversion is explicit and
    /// culture-invariant, which is what makes a flag mean the same thing on a developer's machine
    /// and on a build agent in another region.
    /// </summary>
    private static bool TryConvert(Type type, string value, out object? converted)
    {
        converted = null;

        if (type == typeof(string))
        {
            converted = value;
            return true;
        }

        if (type == typeof(int) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
        {
            converted = i;
            return true;
        }

        if (type == typeof(double) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            converted = d;
            return true;
        }

        if (type == typeof(bool) && bool.TryParse(value, out var b))
        {
            converted = b;
            return true;
        }

        return false;
    }

    private static string Describe(Type type) =>
        type == typeof(string) ? "text"
        : type == typeof(int) ? "a whole number"
        : type == typeof(double) ? "a number"
        : type == typeof(bool) ? "true or false"
        : "a value this host cannot set";
}
