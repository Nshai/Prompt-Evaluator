using AiPromptEvaluator;

namespace IQWorkflow;

/// <summary>
/// Takes the run's credentials from environment variables.
///
/// <b>This is the supported way to give an unattended run a key.</b> The command line is public —
/// readable by other processes, kept in shell history, echoed by schedulers into their own logs —
/// which is why <see cref="SettingsOverrides"/> refuses a credential outright and points here. The
/// settings file is the other route, and it is a file on disk that a batch host may not have.
///
/// <b>The environment wins over the settings file.</b> A machine that has exported a key has been
/// deliberately configured, usually by the thing that scheduled the run; a stale key in a settings
/// file written months ago is the more likely of the two to be wrong. The host says which variable
/// each key came from — never the key.
/// </summary>
public static class CredentialEnvironment
{
    /// <summary>The chat service's key.</summary>
    public const string ChatKey = "IQFLOW_LLM_API_KEY";

    /// <summary>The embedding service's key, where it is a different service.</summary>
    public const string EmbeddingKey = "IQFLOW_EMBEDDING_API_KEY";

    /// <summary>A secured Qdrant's key.</summary>
    public const string VectorStoreKey = "IQFLOW_QDRANT_API_KEY";

    /// <summary>
    /// Applies whatever the environment holds, and names the variables it used.
    ///
    /// A variable that is absent or blank leaves the setting alone, so exporting one key does not
    /// blank the other two.
    /// </summary>
    public static IReadOnlyList<string> Apply(AppSettings settings) =>
        Apply(settings, Environment.GetEnvironmentVariable);

    /// <param name="read">
    /// How a variable is read. Injected so a test can drive this without touching the process
    /// environment, which is shared by every test running beside it.
    /// </param>
    internal static IReadOnlyList<string> Apply(AppSettings settings, Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(read);

        var used = new List<string>();

        if (Value(read, ChatKey) is { } chat)
        {
            settings.OpenAiApiKey = chat;
            used.Add(ChatKey);
        }

        if (Value(read, EmbeddingKey) is { } embedding)
        {
            settings.EmbeddingApiKey = embedding;
            used.Add(EmbeddingKey);
        }

        if (Value(read, VectorStoreKey) is { } store)
        {
            settings.QdrantApiKey = store;
            used.Add(VectorStoreKey);
        }

        return used;
    }

    private static string? Value(Func<string, string?> read, string name) =>
        read(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
