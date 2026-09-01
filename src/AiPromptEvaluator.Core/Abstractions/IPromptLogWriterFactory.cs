namespace AiPromptEvaluator;

/// <summary>
/// Creates the log file a run writes its prompts to.
///
/// Named for the case and the moment the run started, so it cannot exist before there is a run to
/// name it after.
/// </summary>
public interface IPromptLogWriterFactory
{
    /// <summary>The caller disposes what it gets; the file is flushed on every write until then.</summary>
    PromptLogWriter Create(
        string logFolder,
        string caseReference,
        DateTimeOffset startedAt,
        string? filePrefix = null);
}
