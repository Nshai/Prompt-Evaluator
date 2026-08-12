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

    public string FilePath { get; }

    public PromptLogWriter(string logFolder, string caseReference, DateTimeOffset startedAt)
    {
        Directory.CreateDirectory(logFolder);

        var fileName = $"{SanitiseForFileName(caseReference)}_{startedAt:yyyyMMdd_HHmmss}.log";
        FilePath = Path.Combine(logFolder, fileName);

        _writer = new StreamWriter(FilePath, append: false, Encoding.UTF8) { AutoFlush = true };
        _writer.WriteLine($"Case: {caseReference}");
        _writer.WriteLine($"Run started: {startedAt:yyyy-MM-dd HH:mm:ss zzz}");
        _writer.WriteLine();
    }

    /// <summary>Appends one check's system prompt, user prompt and the model's raw response.</summary>
    public void LogExchange(string checkId, string checkName, string systemPrompt, string userPrompt, string response)
    {
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

    private static string SanitiseForFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var cleaned = new string(chars).Trim();
        return cleaned.Length == 0 ? "case" : cleaned;
    }

    public void Dispose() => _writer.Dispose();
}
