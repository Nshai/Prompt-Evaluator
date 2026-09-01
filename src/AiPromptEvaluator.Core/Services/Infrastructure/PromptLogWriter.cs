using System.IO;
using System.Text;

namespace AiPromptEvaluator;

/// <summary>
/// Writes every prompt sent to the LLM, and the raw response it returned, to one log file per
/// run. A run assesses several checks against one case, so the file is named for the case and
/// the moment the run started rather than per check — everything CheckPlanRunner sent during
/// that run lands in the order it was sent, in one place an auditor can read start to finish.
/// </summary>
public sealed class PromptLogWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _gate = new();

    /// <summary>
    /// What the run's prompts had in common, accumulated as they are logged.
    ///
    /// Owned here rather than by the runner because this is the one object that already spans a
    /// whole run and sees every exchange. A ledger the caller had to remember to create, feed and
    /// flush would be a ledger that is empty in exactly the runs nobody set up carefully.
    /// </summary>
    public PromptPrefixLedger Prefixes { get; } = new();

    public string FilePath { get; }

    public PromptLogWriter(string logFolder, string caseReference, DateTimeOffset startedAt, string? filePrefix = null)
    {
        Directory.CreateDirectory(logFolder);

        var prefix = string.IsNullOrEmpty(filePrefix) ? "" : $"{filePrefix}_";
        var fileName = $"{prefix}{SanitiseForFileName(caseReference)}_{startedAt:yyyyMMdd_HHmmss}.log";
        FilePath = Path.Combine(logFolder, fileName);

        _writer = new StreamWriter(FilePath, append: false, Encoding.UTF8) { AutoFlush = true };
        _writer.WriteLine($"Case: {caseReference}");
        _writer.WriteLine($"Run started: {startedAt:yyyy-MM-dd HH:mm:ss zzz}");
        _writer.WriteLine();
    }

    /// <summary>
    /// Records the settings the run used, at the head of the log.
    ///
    /// Written because of a run that could not be explained from its own log. Retrieval had
    /// collapsed to two passages per search and the cause — <c>MaxSearchResults</c> set to 1 —
    /// had to be inferred from the arithmetic of hit counts, then cross-checked against a
    /// settings file that turned out to belong to a different environment entirely. The
    /// fingerprint that would have said so in one line existed already; it was only ever shown
    /// on screen, where nobody reading the log afterwards could see it.
    ///
    /// Every value here can change a finding, so a log without them is not a record of what
    /// happened — it is a record of half of it.
    /// </summary>
    public void LogRunConfiguration(RunFingerprint fingerprint)
    {
        lock (_gate)
        {
            _writer.WriteLine(new string('-', 100));
            _writer.WriteLine("[RUN CONFIGURATION]");
            _writer.WriteLine(fingerprint.Format());
            _writer.WriteLine(new string('-', 100));
            _writer.WriteLine();
        }
    }

    /// <summary>Appends one check's system prompt, user prompt and the model's raw response.</summary>
    public void LogExchange(string checkId, string checkName, string systemPrompt, string userPrompt, string response)
    {
        // The id arrives as "CHK-007/G7.3" — the ledger measures what a check's group prompts
        // share, so it is keyed on the check and not on the group.
        Prefixes.Record(checkId.Split('/')[0], systemPrompt, userPrompt);

        lock (_gate)
        {
            _writer.WriteLine(new string('=', 100));
            _writer.WriteLine($"{checkId} — {checkName}  ({DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss})");
            _writer.WriteLine(new string('=', 100));
            _writer.WriteLine();
            _writer.WriteLine("[SYSTEM PROMPT]");
            _writer.WriteLine(systemPrompt);
            _writer.WriteLine();
            _writer.WriteLine("[USER PROMPT]");
            _writer.WriteLine(userPrompt);
            _writer.WriteLine();
            _writer.WriteLine("[RESPONSE]");
            _writer.WriteLine(response);
            _writer.WriteLine();
        }
    }

    /// <summary>Records a check that failed before any prompt was sent, so the log accounts for every check in the run.</summary>
    public void LogSkipped(string checkId, string reason)
    {
        lock (_gate)
        {
            _writer.WriteLine(new string('=', 100));
            _writer.WriteLine($"{checkId} — skipped  ({DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss})");
            _writer.WriteLine(new string('=', 100));
            _writer.WriteLine(reason);
            _writer.WriteLine();
        }
    }

    /// <summary>
    /// Records that a run ended without producing anything, so a partial log says it is partial.
    ///
    /// <b>Without this, a cancelled run and a completed one end the same way: with the last
    /// section that happened to finish.</b> An observed log stopped after eight of twelve
    /// extraction passes, every one of them parsed and merged, and carried no summary block, no
    /// failure list and no marker of any kind — and a second run started fifteen seconds later,
    /// which is how the cancellation was eventually inferred rather than read. A log that a
    /// reader has to date-arithmetic their way into is not an audit trail.
    /// </summary>
    public void LogRunEnded(string reason)
    {
        lock (_gate)
        {
            _writer.WriteLine(new string('=', 100));
            _writer.WriteLine($"Run ended without completing  ({DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss})");
            _writer.WriteLine(new string('=', 100));
            _writer.WriteLine(reason);
            _writer.WriteLine();
            _writer.WriteLine(
                "Nothing was stored. The sections above completed but the model they would have "
                + "formed was never assembled, so this log records work done and not a result.");
            _writer.WriteLine();
        }
    }

    /// <summary>Records the canonical model an extraction run produced, for an audit trail outside the SQLite store.</summary>
    public void LogCanonicalModel(CanonicalModelDocument document, IReadOnlyList<(string Section, string Error)> failures)
    {
        lock (_gate)
        {
            _writer.WriteLine(new string('=', 100));
            _writer.WriteLine($"Canonical model extracted  ({document.ExtractedAt:yyyy-MM-dd HH:mm:ss})");
            _writer.WriteLine(new string('=', 100));
            _writer.WriteLine($"Model:          {document.ModelId}");
            _writer.WriteLine($"Schema version: {document.SchemaVersion}");
            _writer.WriteLine($"Source documents: {string.Join(", ", document.SourceDocuments)}");
            _writer.WriteLine($"Tokens: {document.Usage.TotalTokens:N0} total");

            if (failures.Count > 0)
            {
                _writer.WriteLine($"Failed sections ({failures.Count}):");
                foreach (var (section, error) in failures)
                {
                    _writer.WriteLine($"  {section} — {error}");
                }
            }

            _writer.WriteLine();
            _writer.WriteLine("[CANONICAL MODEL JSON]");
            _writer.WriteLine(document.Json);
            _writer.WriteLine();
        }
    }

    private static string SanitiseForFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var cleaned = new string(chars).Trim();
        return cleaned.Length == 0 ? "case" : cleaned;
    }

    /// <summary>
    /// Closes the log, writing the prefix ledger last.
    ///
    /// <b>On dispose rather than on a call somebody has to remember.</b> It is the only block that
    /// cannot be written until the run is over, and a summary that depends on a caller reaching
    /// the end of a happy path is a summary that is missing from every run that was cancelled —
    /// which are the runs whose cost anyone is most likely to be asking about.
    /// </summary>
    public void Dispose()
    {
        if (!Prefixes.IsEmpty)
        {
            lock (_gate)
            {
                _writer.WriteLine(new string('=', 100));
                _writer.WriteLine(Prefixes.Format());
                _writer.WriteLine(new string('=', 100));
                _writer.WriteLine();
            }
        }

        _writer.Dispose();
    }
}
