using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace AiPromptEvaluator;

/// <summary>
/// What produced a run, recorded on the findings report.
///
/// Pinning sampling narrows disagreement between runs; it does not abolish it, and it does
/// nothing at all about the inputs changing underneath. A floating model alias rolls to a new
/// build, a query plan is edited, the canonical model is re-extracted, the result count is
/// nudged in settings — and the findings move for reasons that have nothing to do with the
/// case. Without this, all of those look identical to model variance.
///
/// So every input that can change an outcome is reduced to one line. Two runs that disagree
/// can then be diffed on their fingerprints first, which either names the cause or rules out
/// everything except the model itself.
/// </summary>
public sealed record RunFingerprint(
    string ChatModel,
    string EmbeddingModel,
    string Sampling,
    bool StructuredFindings,
    int MaxSearchResults,
    int MaxPassagesPerGroup,
    int ReservedSlotsPerTargetedCategory,
    int ReservedSlotsPerDeclaredSection,
    int ReservedSlotsForTopScore,
    double NearDuplicateOverlap,
    bool CoreQueriesOnly,
    bool IgnoreTriggerProbe,
    int ExtractionMaxTokens,
    int PlanCount,
    string PlanDigest,
    string CanonicalModelDigest,
    string SchemaVersion,
    string SettingsDigest)
{
    /// <summary>
    /// Everything the runner reads that is not the case file itself. The plan digest covers
    /// the plans as loaded from disk, so an edit to a query changes the fingerprint whether or
    /// not it changes the finding.
    /// </summary>
    public static RunFingerprint For(
        AppSettings settings,
        CanonicalModelDocument? model,
        string planFolder,
        int planCount,
        int maxPassagesPerGroup) =>
        new(
            ChatModel: settings.SelectedModel,
            EmbeddingModel: settings.EmbeddingModel,
            Sampling: DescribeSampling(settings),
            StructuredFindings: settings.StructuredFindings,
            MaxSearchResults: settings.MaxSearchResults,
            MaxPassagesPerGroup: maxPassagesPerGroup,
            ReservedSlotsPerTargetedCategory: settings.ReservedSlotsPerTargetedCategory,
            ReservedSlotsPerDeclaredSection: settings.ReservedSlotsPerDeclaredSection,
            ReservedSlotsForTopScore: settings.ReservedSlotsForTopScore,
            NearDuplicateOverlap: settings.NearDuplicateOverlap,
            CoreQueriesOnly: settings.CoreQueriesOnly,
            IgnoreTriggerProbe: settings.IgnoreTriggerProbe,
            ExtractionMaxTokens: settings.ExtractionMaxTokens,
            PlanCount: planCount,
            PlanDigest: DigestOfFolder(planFolder, CheckQueryPlanLoader.SearchPattern),
            CanonicalModelDigest: model is null ? "none" : Digest(model.Json),
            SchemaVersion: model?.SchemaVersion ?? "-",
            SettingsDigest: DigestOfSettings(settings));

    /// <summary>
    /// A digest over every setting that can move a finding.
    ///
    /// <b>The named fields above are what a reader acts on; this is what catches the ones nobody
    /// thought to name.</b> Settings arrive one at a time beside the code that reads them, and
    /// this record is edited separately or not at all — so the gap opens silently, and the symptom
    /// is two runs that differ for a reason the fingerprint swore was identical. It had already
    /// happened four times over when this was added, twice in one week.
    ///
    /// Opt-out rather than opt-in, so a setting added tomorrow is covered by default. See
    /// <see cref="AppSettings.NotFingerprinted"/> for the three categories that are excluded and
    /// why. Property names are sorted ordinally, so the digest does not depend on the order
    /// reflection happens to return them in.
    /// </summary>
    public static string DigestOfSettings(AppSettings settings)
    {
        var sb = new StringBuilder();

        foreach (var property in settings.GetType()
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.CanWrite)
                     .Where(p => p.GetCustomAttribute<JsonPropertyNameAttribute>() is not null)
                     .Where(p => !AppSettings.NotFingerprinted.Contains(p.Name))
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            sb.Append(property.Name)
              .Append('=')
              .Append(Convert.ToString(property.GetValue(settings), CultureInfo.InvariantCulture))
              .Append('\0');
        }

        return sb.Length == 0 ? "empty" : Digest(sb.ToString());
    }

    /// <summary>
    /// Each pinned parameter is releasable on its own — a gateway can reject one without
    /// objecting to the others — so the fingerprint states exactly which ones were sent rather
    /// than one all-or-nothing line.
    /// </summary>
    private static string DescribeSampling(AppSettings settings)
    {
        var parts = new List<string>();

        parts.Add(settings.PinTemperature ? $"temperature {settings.Temperature}" : "temperature default");
        parts.Add(settings.PinTopP ? $"top-p {settings.TopP}" : "top-p default");
        parts.Add(settings.PinSeed ? $"seed {settings.SamplingSeed}" : "seed not pinned");

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Two lines for the head of the report. Short enough that a reviewer reads them, complete
    /// enough that a difference between two runs is visible without opening either input.
    /// </summary>
    public string Format() =>
        $"Chat {ChatModel} · {Sampling} · findings {(StructuredFindings ? "schema-constrained" : "free-form")}"
        + Environment.NewLine
        + $"Embeddings {EmbeddingModel} · top {Cap(MaxSearchResults)}/search, "
        + $"{Cap(MaxPassagesPerGroup)}/group "
        + $"(reserving {ReservedSlotsPerDeclaredSection}/section, "
        + $"{ReservedSlotsPerTargetedCategory}/category, "
        + $"{ReservedSlotsForTopScore}/top score; "
        + $"near-duplicate {DescribeOverlap(NearDuplicateOverlap)}) · "
        + $"extraction cap {ExtractionMaxTokens:N0} tok · "
        + $"plans {PlanCount}@{PlanDigest} · model {CanonicalModelDigest} (schema v{SchemaVersion})"
        + Environment.NewLine
        + $"Settings {SettingsDigest} · "
        + $"scope {(CoreQueriesOnly ? "Core queries only" : "Core and Supplementary queries")}"
        + $" · {(IgnoreTriggerProbe ? "trigger probes bypassed — every check assessed" : "trigger probes honoured")}"
        + Environment.NewLine
        + Variance;

    /// <summary>
    /// What the reader has to know before comparing this run with another one.
    ///
    /// <b>Two runs of one case at this exact configuration disagreed on 3 of 78 outcomes and 12
    /// of 69 severities.</b> Same assessor, same plan digest, same model digest, same caps —
    /// and two groups moved from No Issue to Potential Concern, one moved back, and the two
    /// halves of a single check's largest finding moved in opposite directions.
    ///
    /// Every improvement anyone proposes to this pipeline claims an effect of about that size.
    /// So the floor is printed beside the fingerprint rather than left in a document, because a
    /// reviewer diffing two runs will otherwise read three moved findings as a result.
    ///
    /// Where sampling is pinned this says so and says less. Where it is not — some gateways
    /// reject the seed parameter outright, and the fingerprint's sampling line names which
    /// parameters were actually sent — it says what that costs.
    /// </summary>
    public string Variance =>
        Sampling.Contains("seed not pinned", StringComparison.Ordinal)
            ? "Sampling is not pinned on this route, so two runs of this configuration will "
              + "differ. Measured on one case: 3 of 78 outcomes and 12 of 69 severities moved "
              + "between two runs with identical fingerprints. Do not read a difference of that "
              + "size against another run as an effect; score two runs each side."
            : "Sampling is pinned, which narrows run-to-run disagreement without abolishing it. "
              + "A small difference against another run is still worth confirming twice.";

    /// <summary>How the near-duplicate pass reads, including when it is switched off.</summary>
    private static string DescribeOverlap(double overlap) =>
        overlap >= 1.0 ? "off" : overlap.ToString("0.00");

    /// <summary>
    /// How a cap reads in the fingerprint. A run at an unbounded cap and a run at a large one
    /// are different runs, and "unbounded" says which without the reader having to know what
    /// the largest sensible value would have been.
    /// </summary>
    private static string Cap(int value) =>
        AppSettings.IsUnbounded(value) ? "unbounded" : value.ToString("N0");

    /// <summary>
    /// A short, stable digest. Short because it is read by eye and only ever compared for
    /// equality; stable because <see cref="string.GetHashCode()"/> is seeded per process and
    /// would give a different answer every time the app started.
    /// </summary>
    public static string Digest(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12].ToLowerInvariant();

    /// <summary>
    /// A digest over every matching file in a folder — names and contents, in ordinal name
    /// order, so the answer does not depend on how the file system chose to enumerate them.
    /// </summary>
    public static string DigestOfFolder(string folder, string searchPattern)
    {
        if (!Directory.Exists(folder))
        {
            return "missing";
        }

        var sb = new StringBuilder();

        foreach (var file in Directory.GetFiles(folder, searchPattern)
                     .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal))
        {
            sb.Append(Path.GetFileName(file)).Append('\0');

            try
            {
                sb.Append(File.ReadAllText(file));
            }
            catch (IOException ex)
            {
                sb.Append("unreadable:").Append(ex.Message);
            }

            sb.Append('\0');
        }

        return sb.Length == 0 ? "empty" : Digest(sb.ToString());
    }
}
