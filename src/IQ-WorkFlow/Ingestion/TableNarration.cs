// Ported from IQFlow.Adapters.Ingest, apart from the namespace, `ModelRef` becoming the model id
// this pipeline already carries as a string, and this note.
//
// The implementation plan (§4) permits the conversion stage as the one exception to "build against
// v1". Narration is the half of that exception that costs money, so it is off unless
// AppSettings.TableNarration says otherwise — see the settings for why that default is the
// important part.
using System.Globalization;
using System.Text;

using Microsoft.Extensions.AI;

namespace IQWorkflow;

/// <summary>One chunk of a table to narrate, with its shared header and context.</summary>
/// <param name="DocumentName">For the prompt's context line.</param>
/// <param name="TableIndex">Which table in the document.</param>
/// <param name="Headers">Column headers, in order.</param>
/// <param name="Rows">The data rows in this chunk.</param>
/// <param name="ChunkNumber">1-based.</param>
/// <param name="TotalChunks">How many chunks this table was split into.</param>
/// <param name="CaptionOrContext">The section heading the table sits under.</param>
public sealed record TableNarrationRequest(
    string DocumentName,
    int TableIndex,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    int ChunkNumber,
    int TotalChunks,
    string? CaptionOrContext);

/// <summary>
/// Turns a table's rows into detail-preserving prose.
///
/// The contract is narrow and absolute: <b>no implementation may drop a value</b>. Every cell must
/// be traceable in the output. A narrative that summarises a charges table into "several fees apply"
/// has destroyed the only thing the table was for.
/// </summary>
public interface ITableNarrator
{
    /// <summary>Narrates one chunk.</summary>
    Task<string> NarrateAsync(TableNarrationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Whether and how tables are narrated.
///
/// <b>Off by default, and that is the important default.</b> The pipeline's design requires that
/// tables survive as tables — the chunker keeps a table whole and the extractor reads the grid —
/// because that is what the findings turning on a row read against its column heading depend on.
/// Narration is an override for documents whose tables are genuinely prose-shaped, not the normal
/// path.
/// </summary>
public sealed record TableNarrationOptions
{
    /// <summary>Off unless deliberately switched on.</summary>
    public bool Enabled { get; init; }

    /// <summary>Rows per call; a long table is chunked rather than truncated.</summary>
    public int MaxRowsPerChunk { get; init; } = 40;

    /// <summary>Below this many data rows, narration is not worth a round trip.</summary>
    public int MinRowsToNarrate { get; init; } = 1;

    /// <summary>The default: narration off.</summary>
    public static TableNarrationOptions Disabled { get; } = new();
}

/// <summary>
/// Deterministic, offline narration.
///
/// Not only a fallback. It is the safety net that makes the no-loss guarantee real: every
/// header/value pair is rendered explicitly, so the prose is mechanical but nothing is lost. When
/// the model is unreachable or unconfigured, the conversion degrades to this rather than failing —
/// losing polish, never data.
/// </summary>
public sealed class RuleBasedTableNarrator : ITableNarrator
{
    public Task<string> NarrateAsync(
        TableNarrationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var narrative = new StringBuilder();

        foreach (var row in request.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pairs = new List<string>();

            for (var column = 0; column < row.Count; column++)
            {
                var value = row[column];

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var header = column < request.Headers.Count
                    ? request.Headers[column]
                    : $"Column {column + 1}";

                pairs.Add($"{header.Trim()} is {value.Trim()}");
            }

            if (pairs.Count == 0)
            {
                continue;
            }

            narrative.Append("- ").Append(string.Join("; ", pairs)).Append(".\n");
        }

        return Task.FromResult(narrative.ToString());
    }
}

/// <summary>
/// Model-authored narration, with the dropped values put back.
///
/// After the model answers, every non-blank source cell is checked for presence in the narrative.
/// Anything the model left out is appended verbatim under "Also noted". <b>That check is what makes
/// this usable on regulated material at all:</b> a model asked to write prose will occasionally
/// summarise a row away, and a silently missing charge is exactly the class of loss this pipeline
/// exists to catch rather than create.
///
/// Any failure — unreachable endpoint, unusable answer — falls back to the deterministic narrator
/// rather than failing the conversion.
/// </summary>
public sealed class LlmTableNarrator : ITableNarrator
{
    /// <summary>The instruction half. Carries no case content.</summary>
    public const string SystemMessage = """
        You convert tabular data extracted from documents into clear, plain-language prose.

        Preserve every fact, figure, date, name and value exactly as given. Do not round, omit,
        summarise away or approximate any data point, and do not invent anything not present.

        Write flowing sentences or a tight bulleted narrative grouped by row, using the column
        headers as the label for each value. Do not reproduce the data as a table, grid or
        pipe-delimited text.

        Do not add a title or heading of your own — the surrounding document already supplies the
        section heading. Start directly with the narrative.
        """;

    private readonly IChatClient _client;
    private readonly string _model;
    private readonly RuleBasedTableNarrator _fallback = new();

    public LlmTableNarrator(IChatClient client, string model)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _model = string.IsNullOrWhiteSpace(model) ? throw new ArgumentException(
            "A narration model is required.", nameof(model)) : model;
    }

    private int _fallbacks;

    /// <summary>
    /// How many chunks fell back to the deterministic narrator.
    ///
    /// Counted rather than logged because this project's conversion stage reports through its
    /// outcome rather than through a logger, and "the narration you paid for silently became the
    /// free one" is exactly the fact a cost-conscious run needs told. No value is lost either way;
    /// what changes is that the prose is mechanical.
    /// </summary>
    public int Fallbacks => Volatile.Read(ref _fallbacks);

    public async Task<string> NarrateAsync(
        TableNarrationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? narrative = null;

        try
        {
            var response = await _client.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, SystemMessage),
                    new ChatMessage(ChatRole.User, BuildPrompt(request)),
                ],
                new ChatOptions { ModelId = _model, Temperature = 0 },
                cancellationToken).ConfigureAwait(false);

            narrative = response.Text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Losing polish is a smaller harm than losing the conversion, and the deterministic
            // narrator below preserves every value — so a failure here costs readability and
            // nothing any later stage depends on.
            _ = ex;
        }

        if (string.IsNullOrWhiteSpace(narrative))
        {
            Interlocked.Increment(ref _fallbacks);

            return await _fallback.NarrateAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return AppendMissingValues(narrative, request);
    }

    /// <summary>Builds the prompt for one chunk.</summary>
    internal static string BuildPrompt(TableNarrationRequest request)
    {
        var prompt = new StringBuilder();

        prompt.Append("Document: ").AppendLine(request.DocumentName);
        prompt.Append("Table index: ")
            .AppendLine(request.TableIndex.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(request.CaptionOrContext))
        {
            prompt.Append("Section this table belongs to (context only — do not repeat it as a heading): ")
                .AppendLine(request.CaptionOrContext);
        }

        if (request.TotalChunks > 1)
        {
            prompt.AppendLine(
                $"This is part {request.ChunkNumber} of {request.TotalChunks} of the same table; "
                + "rows continue from the previous part.");
        }

        prompt.Append("Column headers, in order: ").AppendLine(string.Join(" | ", request.Headers));
        prompt.AppendLine().AppendLine("Rows (pipe-delimited, aligned to the headers above):");

        foreach (var row in request.Rows)
        {
            prompt.AppendLine(string.Join(" | ", row));
        }

        prompt.AppendLine()
            .AppendLine("Rewrite the rows above as plain-language narrative preserving every value.");

        return prompt.ToString();
    }

    /// <summary>
    /// Appends any source value the narrative does not contain.
    ///
    /// Internal because "no value is ever lost" is a claim about this function, and it is the only
    /// reason the narration override is safe to offer at all.
    /// </summary>
    internal static string AppendMissingValues(string narrative, TableNarrationRequest request)
    {
        var normalised = Normalise(narrative);

        var missing = request.Rows
            .SelectMany(row => row)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Where(value => !normalised.Contains(Normalise(value), StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return missing.Length == 0
            ? narrative.Trim()
            : $"{narrative.Trim()}\n- Also noted: {string.Join("; ", missing)}.";
    }

    private static string Normalise(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
