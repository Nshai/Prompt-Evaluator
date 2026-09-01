// Ported from IQFlow.Adapters.Ingest, apart from the namespace, `ModelRef` becoming the model id
// this pipeline already carries as a string, and this note.
//
// The implementation plan (§4) permits the conversion stage as the one exception to "build against
// v1". Like table narration, this is the half of that exception that costs money, so it is off
// unless AppSettings.PictureNarration says otherwise.
using Microsoft.Extensions.AI;

namespace IQWorkflow;

/// <summary>
/// Whether and how pictures are transcribed.
///
/// <b>Off by default, and the sibling of <see cref="TableNarrationOptions"/> on purpose.</b> Both
/// are the same decision in two places: whether to spend a model call reading something the
/// deterministic path could not.
///
/// The two are not interchangeable, though. Table narration re-expresses a grid the pipeline
/// already has; picture transcription recovers content it does not have at all. Where a document's
/// tables are bitmaps — the Test Case 3 risk-profile report, where every risk level and all three
/// questionnaires are pictures — this is the only route to them, and OCR does not reach it.
/// </summary>
public sealed record PictureNarrationOptions
{
    /// <summary>Off unless deliberately switched on.</summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Smallest picture worth a model call.
    ///
    /// Signature logos, icons and decorative rules arrive on nearly every document, and a vision
    /// call apiece costs real money to be told it is a logo. Measured across the three cases, the
    /// pictures carrying content ran from about 19 KB to 75 KB while the decoration sat near 2 KB,
    /// so this sits deliberately below the content and above the noise.
    /// </summary>
    public int MinimumImageBytes { get; init; } = 4096;

    /// <summary>Most pictures to read in one document, so a slide deck cannot run away with a run.</summary>
    public int MaxImagesPerDocument { get; init; } = 40;

    /// <summary>The default: transcription off.</summary>
    public static PictureNarrationOptions Disabled { get; } = new();
}

/// <summary>
/// Reads a picture that carries content.
///
/// A seam, because there is more than one credible way to do it: a vision model, a local OCR
/// engine, or Docling's own <c>picture_description_api</c>. The implementation here uses this
/// pipeline's own chat client, so the credential never leaves the process and the call inherits
/// whatever policy is already wrapped around it.
/// </summary>
public interface IPictureDescriber
{
    /// <summary>Describes one picture, or returns null when it carries nothing worth reading.</summary>
    Task<string?> DescribeAsync(
        ReadOnlyMemory<byte> image, string mediaType, string? context, CancellationToken cancellationToken);
}

/// <summary>
/// Reads pictures with a vision model.
///
/// <b>This is what recovers a table that was pasted into a document as a bitmap.</b> Docling can
/// extract structure from a table; it cannot extract structure from a picture of one, and on the
/// Test Case 3 risk-profile report every risk level and all three questionnaires are pictures. OCR
/// does not reach them, so without this the content is simply absent — which the benchmark records
/// as a conversion loss no downstream stage can recover.
///
/// The instruction is deliberately narrow: transcribe, do not interpret. A model asked to explain a
/// risk-profile chart will write about risk; a model asked to transcribe it will write down the
/// numbers, which is the only thing worth having.
/// </summary>
public sealed class LlmPictureDescriber : IPictureDescriber
{
    /// <summary>The instruction half. Carries no case content.</summary>
    public const string SystemMessage = """
        You transcribe images taken from client case documents.

        Many are tables, charts or forms that were pasted in as pictures, so their content exists
        nowhere else in the document.

        Transcribe what the image contains, exactly:
        - Reproduce every label, figure, date and value you can read, verbatim.
        - Where the image is a table, give it row by row, naming the column for each value.
        - Where it is a chart, give the axis labels and every plotted or annotated value.
        - Preserve the units and symbols as shown: £, %, decimal places.

        Do not interpret, summarise, or explain what the content means. Do not guess at anything
        illegible — say "illegible" instead. If the image carries no readable content — a logo, a
        decorative rule, an icon — reply with exactly: NO CONTENT
        """;

    /// <summary>What the model says when a picture carries nothing.</summary>
    internal const string NothingToRead = "NO CONTENT";

    private readonly IChatClient _client;
    private readonly string _model;
    private int _failures;

    public LlmPictureDescriber(IChatClient client, string model)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _model = string.IsNullOrWhiteSpace(model) ? throw new ArgumentException(
            "A narration model is required.", nameof(model)) : model;
    }

    /// <summary>
    /// Pictures the model could not be asked about.
    ///
    /// Counted for the same reason the narrator counts its fallbacks: a picture that failed to
    /// transcribe reads downstream exactly like a picture that carried nothing, and those are not
    /// the same fact.
    /// </summary>
    public int Failures => Volatile.Read(ref _failures);

    public async Task<string?> DescribeAsync(
        ReadOnlyMemory<byte> image,
        string mediaType,
        string? context,
        CancellationToken cancellationToken)
    {
        if (image.IsEmpty)
        {
            return null;
        }

        var prompt = string.IsNullOrWhiteSpace(context)
            ? "Transcribe this image."
            : $"Transcribe this image. It appears under the heading: {context}";

        var message = new ChatMessage(ChatRole.User,
        [
            new TextContent(prompt),
            new DataContent(image, mediaType),
        ]);

        try
        {
            var response = await _client.GetResponseAsync(
                [new ChatMessage(ChatRole.System, SystemMessage), message],
                new ChatOptions { ModelId = _model, Temperature = 0 },
                cancellationToken).ConfigureAwait(false);

            var text = response.Text?.Trim();

            return string.IsNullOrWhiteSpace(text)
                   || text.StartsWith(NothingToRead, StringComparison.OrdinalIgnoreCase)
                ? null
                : text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One unreadable picture must not lose the document. The conversion continues without
            // it, and the count is what tells anyone it happened.
            _ = ex;

            Interlocked.Increment(ref _failures);

            return null;
        }
    }
}

/// <summary>Decodes the data URI Docling returns for an embedded image.</summary>
internal static class DataUri
{
    /// <summary>Reads a <c>data:image/png;base64,…</c> URI into bytes and a media type.</summary>
    public static bool TryParse(string? uri, out ReadOnlyMemory<byte> bytes, out string mediaType)
    {
        bytes = default;
        mediaType = "image/png";

        if (string.IsNullOrWhiteSpace(uri) || !uri.StartsWith("data:", StringComparison.Ordinal))
        {
            return false;
        }

        var comma = uri.IndexOf(',', StringComparison.Ordinal);

        if (comma < 0)
        {
            return false;
        }

        var header = uri[5..comma];

        if (!header.Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var semicolon = header.IndexOf(';', StringComparison.Ordinal);

        if (semicolon > 0)
        {
            mediaType = header[..semicolon];
        }

        try
        {
            bytes = Convert.FromBase64String(uri[(comma + 1)..]);

            return !bytes.IsEmpty;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
