using System.Text;

using Microsoft.ML.Tokenizers;

namespace AiPromptEvaluator;

/// <summary>What one check's prompts share, and what that repetition costs.</summary>
/// <param name="CheckId">The check.</param>
/// <param name="Calls">How many group prompts it sent.</param>
/// <param name="SharedPrefixTokens">
/// Tokens common to the front of every one of those prompts — in practice the check header, since
/// the runner builds it once and prefixes it to each group.
/// </param>
/// <param name="TotalPromptTokens">Every prompt token the check sent, system prompts included.</param>
public sealed record CheckPrefix(
    string CheckId,
    int Calls,
    int SharedPrefixTokens,
    long TotalPromptTokens)
{
    /// <summary>
    /// Tokens that were sent more than once and need not have been: the prefix, times every call
    /// after the first.
    /// </summary>
    public long RepeatedTokens => Calls <= 1 ? 0 : (long)SharedPrefixTokens * (Calls - 1);
}

/// <summary>
/// How much of a run's prompt spend is the same text sent again.
///
/// <b>Why this is measured rather than estimated.</b> The assessor makes one call per requirement
/// rather than one per check, which was a deliberate trade: a pack small enough to attend to in
/// full, a failure isolated to the group that caused it, and no group's reasoning conditioned on
/// the groups written before it. The runner's own note says what pays for that shape — <i>"keeps
/// the check header identical at the front of every prompt so the provider's prefix cache covers
/// it"</i>.
///
/// <b>That cache was never requested.</b> The client reports a cached-token count where a provider
/// volunteers one and otherwise prices the whole input at the uncached rate; nothing has ever sent
/// a cache breakpoint. So the design has been paying for the repetition it expected to be
/// forgiven, and how much has never been a number.
///
/// This makes it one. The shared prefix is computed from the prompts actually sent — the longest
/// common prefix across a check's calls — so it measures what a breakpoint would really have
/// covered rather than what the header was supposed to be.
/// </summary>
public sealed class PromptPrefixLedger
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<string>> _byCheck = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _systemPrompts = [];

    private static readonly Lazy<Tokenizer> Encoding =
        new(() => TiktokenTokenizer.CreateForEncoding("o200k_base"));

    /// <summary>
    /// Records one exchange's prompts. Cheap: the text is held and counted once at the end, so a
    /// run pays one tokenisation pass rather than one per call.
    /// </summary>
    public void Record(string checkId, string systemPrompt, string userPrompt)
    {
        lock (_gate)
        {
            if (!_byCheck.TryGetValue(checkId, out var prompts))
            {
                prompts = [];
                _byCheck[checkId] = prompts;
            }

            prompts.Add(userPrompt);
            _systemPrompts.Add(systemPrompt);
        }
    }

    /// <summary>True when nothing has been recorded, so a caller can skip the summary entirely.</summary>
    public bool IsEmpty
    {
        get { lock (_gate) { return _systemPrompts.Count == 0; } }
    }

    /// <summary>Per-check prefixes, in check order.</summary>
    public IReadOnlyList<CheckPrefix> Checks
    {
        get
        {
            lock (_gate)
            {
                return _byCheck
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => new CheckPrefix(
                        pair.Key,
                        pair.Value.Count,
                        CountTokens(LongestCommonPrefix(pair.Value)),
                        pair.Value.Sum(p => (long)CountTokens(p))))
                    .ToList();
            }
        }
    }

    /// <summary>
    /// The system prompt, which is identical on every call in the run and is therefore the one
    /// prefix a single breakpoint can cover across all of them.
    /// </summary>
    public int SystemPromptTokens
    {
        get
        {
            lock (_gate)
            {
                return _systemPrompts.Count == 0
                    ? 0
                    : CountTokens(LongestCommonPrefix(_systemPrompts));
            }
        }
    }

    public int Calls
    {
        get { lock (_gate) { return _systemPrompts.Count; } }
    }

    /// <summary>
    /// The summary a run writes to its prompt log: what was sent, what of it was a repeat, and
    /// what a cache breakpoint would have made of the difference.
    /// </summary>
    /// <param name="cacheReadMultiplier">
    /// What a cached input token costs against an uncached one. Anthropic reads at 0.1×; a
    /// provider that does not cache at all is 1.0× and the saving is correctly reported as zero.
    /// </param>
    /// <param name="cacheWriteMultiplier">What the first, cache-populating call costs. Anthropic writes at 1.25×.</param>
    public string Format(double cacheReadMultiplier = 0.1, double cacheWriteMultiplier = 1.25)
    {
        var checks = Checks;

        if (checks.Count == 0)
        {
            return "No assessment prompts were sent.";
        }

        var calls = Calls;
        var system = SystemPromptTokens;
        var total = checks.Sum(c => c.TotalPromptTokens);

        // The system prompt is identical on every call, so one breakpoint covers all but the
        // first. The check header repeats only within its own check.
        var repeatedSystem = (long)system * Math.Max(0, calls - 1);
        var repeatedHeaders = checks.Sum(c => c.RepeatedTokens);
        var repeated = repeatedSystem + repeatedHeaders;

        // A repeated token becomes a cache read; the first copy becomes a cache write.
        var written = (long)system + checks.Sum(c => (long)c.SharedPrefixTokens);
        var saving = (repeated * (1 - cacheReadMultiplier)) - (written * (cacheWriteMultiplier - 1));

        var sb = new StringBuilder();

        sb.AppendLine("[PROMPT PREFIX LEDGER]");
        sb.AppendLine(
            "How much of this run's prompt spend was the same text sent again, and what a cache "
            + "breakpoint would recover. Prefixes are measured from the prompts actually sent.");
        sb.AppendLine();
        sb.AppendLine($"Calls                     {calls,12:N0}");
        sb.AppendLine($"Prompt tokens sent        {total,12:N0}");
        sb.AppendLine($"System prompt (each call) {system,12:N0}");
        sb.AppendLine($"Repeated system tokens    {repeatedSystem,12:N0}");
        sb.AppendLine($"Repeated header tokens    {repeatedHeaders,12:N0}");
        sb.AppendLine(
            $"Repeated in total         {repeated,12:N0}"
            + (total > 0 ? $"   ({(double)repeated / total:P1} of prompt tokens)" : string.Empty));
        sb.AppendLine();
        sb.AppendLine(
            $"At {cacheReadMultiplier:0.##}x read and {cacheWriteMultiplier:0.##}x write, breakpoints "
            + $"would net {saving,0:N0} input tokens"
            + (total > 0 ? $" — {saving / total:P1} of this run's prompt spend." : "."));
        sb.AppendLine();
        sb.AppendLine("Per check:");
        sb.AppendLine("  check        calls    prefix     repeated       total");

        foreach (var c in checks)
        {
            sb.AppendLine(
                $"  {c.CheckId,-10} {c.Calls,7:N0} {c.SharedPrefixTokens,9:N0} "
                + $"{c.RepeatedTokens,12:N0} {c.TotalPromptTokens,11:N0}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// The text every one of these prompts begins with.
    ///
    /// Compared by character and then counted as tokens, rather than compared token by token: a
    /// cache breakpoint is placed in the text, so the character prefix is what a provider would
    /// actually be able to match.
    /// </summary>
    internal static string LongestCommonPrefix(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return string.Empty;
        }

        var prefix = values[0];

        foreach (var value in values.Skip(1))
        {
            var length = Math.Min(prefix.Length, value.Length);
            var i = 0;

            while (i < length && prefix[i] == value[i])
            {
                i++;
            }

            prefix = prefix[..i];

            if (prefix.Length == 0)
            {
                break;
            }
        }

        return prefix;
    }

    internal static int CountTokens(string text) =>
        text.Length == 0 ? 0 : Encoding.Value.CountTokens(text);
}
