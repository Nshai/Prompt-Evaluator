using System.IO;
using System.Security.Cryptography;
using System.Text;

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
    int ExtractionMaxTokens,
    int PlanCount,
    string PlanDigest,
    string CanonicalModelDigest,
    string SchemaVersion)
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
            ExtractionMaxTokens: settings.ExtractionMaxTokens,
            PlanCount: planCount,
            PlanDigest: DigestOfFolder(planFolder, CheckQueryPlanLoader.SearchPattern),
            CanonicalModelDigest: model is null ? "none" : Digest(model.Json),
            SchemaVersion: model?.SchemaVersion ?? "-");

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
        + $"{ReservedSlotsPerTargetedCategory}/category) · "
        + $"extraction cap {ExtractionMaxTokens:N0} tok · "
        + $"plans {PlanCount}@{PlanDigest} · model {CanonicalModelDigest} (schema v{SchemaVersion})";

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
