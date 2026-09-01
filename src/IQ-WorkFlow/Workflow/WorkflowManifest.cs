using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using AiPromptEvaluator;

namespace IQWorkflow;

/// <summary>One stage's entry in the manifest.</summary>
/// <param name="Stage">Which stage.</param>
/// <param name="Status">How it ended.</param>
/// <param name="Summary">The line a reader wants.</param>
/// <param name="ElapsedSeconds">Wall clock, in seconds, so the JSON is readable without conversion.</param>
/// <param name="CompletedAt">When it finished, for ordering across a resumed run.</param>
/// <param name="Detail">Counts and names the summary does not carry.</param>
/// <param name="InputTokens">What the stage billed, where it billed anything.</param>
/// <param name="UsageReported">
/// <b>False means the cost is unknown, not zero.</b> A provider that returns no token count has told
/// us nothing, and a manifest that recorded 0 would let a report claim the stage was free.
/// </param>
public sealed record StageRecord(
    WorkflowStageId Stage,
    StageStatus Status,
    string Summary,
    double ElapsedSeconds,
    DateTimeOffset CompletedAt,
    IReadOnlyDictionary<string, string>? Detail = null,
    long InputTokens = 0,
    long OutputTokens = 0,
    bool UsageReported = false);

/// <summary>
/// What a run did — as distinct from what it found, which is <see cref="ICheckRunStore"/>'s job.
///
/// <b>This is the audit trail and the resumption point in one file.</b> It answers "what happened,
/// in what order, how long did it take, what did it cost, and where did it stop" — and it is the
/// only thing that lets a second attempt start from the first stage that is not already done.
///
/// <b>No credential ever reaches it.</b> The settings digest is
/// <see cref="RunFingerprint"/>'s, which already excludes credentials and endpoints by name through
/// <see cref="AppSettings.NotFingerprinted"/>. Keeping a second list here would be a second thing to
/// forget, so the manifest defers to the one that already exists.
/// </summary>
public sealed record WorkflowManifest
{
    /// <summary>
    /// The manifest format. Checked on load: a manifest a build does not understand is refused by
    /// name rather than half-read, which would resume a run from a state nobody can vouch for.
    /// </summary>
    public const string SupportedVersion = "1.0";

    [JsonPropertyName("manifestVersion")]
    public string ManifestVersion { get; init; } = SupportedVersion;

    [JsonPropertyName("caseReference")]
    public string CaseReference { get; init; } = string.Empty;

    [JsonPropertyName("tenantId")]
    public int TenantId { get; init; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// The run fingerprint's settings digest — everything that can move a finding, and nothing that
    /// cannot. Two runs with the same digest were configured identically.
    /// </summary>
    [JsonPropertyName("settingsDigest")]
    public string SettingsDigest { get; init; } = string.Empty;

    [JsonPropertyName("chatModel")]
    public string ChatModel { get; init; } = string.Empty;

    [JsonPropertyName("embeddingModel")]
    public string EmbeddingModel { get; init; } = string.Empty;

    [JsonPropertyName("stages")]
    public List<StageRecord> Stages { get; init; } = [];

    /// <summary>The stages that ended in a way the pipeline could continue past.</summary>
    public IEnumerable<WorkflowStageId> Satisfied =>
        Stages.Where(s => s.Status is StageStatus.Succeeded or StageStatus.Skipped).Select(s => s.Stage);

    /// <summary>True when every stage recorded so far could be continued past.</summary>
    public bool Succeeded => Stages.Count > 0 && Stages.All(s => s.Status is StageStatus.Succeeded or StageStatus.Skipped);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },

        // The manifest is read by people at least as often as by code, and a case reference or a
        // document name carrying a non-ASCII character should read as itself rather than as an
        // escape. It is never interpolated into HTML or a script.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The manifest's file name inside a run folder. One name, so a resume can find it.</summary>
    public const string FileName = "workflow-manifest.json";

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>
    /// Reads a manifest, or null where there is none to read or it cannot be trusted.
    ///
    /// <b>A manifest that will not parse is treated as absent rather than as an error.</b> The cost
    /// of starting a run from the beginning is a run; the cost of resuming from a state nobody can
    /// vouch for is a result nobody can vouch for.
    /// </summary>
    public static WorkflowManifest? Read(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<WorkflowManifest>(File.ReadAllText(path), Options);

            return manifest?.ManifestVersion == SupportedVersion ? manifest : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the manifest so a reader never sees a half-written one.
    ///
    /// <b>Written to a temporary file and moved into place.</b> The manifest is updated after every
    /// stage, including the long ones, so a run cancelled mid-write is not a hypothetical — and a
    /// truncated manifest is worse than none, because a resume would read it and believe it.
    /// </summary>
    public void Write(string path)
    {
        var folder = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var temporary = path + ".tmp";

        File.WriteAllText(temporary, ToJson());
        try
        {
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            File.Copy(temporary, path, overwrite: true);
            try { File.Delete(temporary); } catch { }
        }
    }

    /// <summary>
    /// The manifest a run starts from: this run's identity, and whatever an earlier attempt on the
    /// same case and configuration already achieved.
    ///
    /// <b>A configuration change discards the earlier stages.</b> Resuming across a settings change
    /// would mix a model extracted under one configuration with an assessment run under another,
    /// and the result would carry one fingerprint while being two runs. The digest is the test
    /// because it already means exactly "everything that can move a finding".
    /// </summary>
    internal static WorkflowManifest StartOrResume(WorkflowManifest? previous, WorkflowContext context)
    {
        var digest = RunFingerprint.DigestOfSettings(context.Settings);

        var fresh = new WorkflowManifest
        {
            CaseReference = context.CaseReference,
            TenantId = context.Settings.TenantId,
            StartedAt = context.StartedAt,
            SettingsDigest = digest,
            ChatModel = context.Settings.SelectedModel,
            EmbeddingModel = context.Settings.EmbeddingModel,
        };

        if (previous is null
            || !string.Equals(previous.SettingsDigest, digest, StringComparison.Ordinal)
            || !string.Equals(previous.CaseReference, context.CaseReference, StringComparison.OrdinalIgnoreCase)
            || previous.TenantId != context.Settings.TenantId)
        {
            return fresh;
        }

        fresh.Stages.AddRange(previous.Stages);

        return fresh;
    }
}
