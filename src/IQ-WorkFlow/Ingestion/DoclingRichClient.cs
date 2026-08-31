// Ported from IQFlow.Adapters.Ingest, apart from the namespace, the dropped Core using, and this
// note.
//
// v1's own DoclingClient returns Markdown alone. The form-field reconstructor and the backfiller
// both read the DoclingDocument JSON — that is the whole point of them, since the Markdown export
// is a lossy rendering of it — so the conversion stage needs a client that asks for both.
using System.Net.Http.Headers;
using System.Text.Json;

namespace IQWorkflow;

/// <summary>How the pipeline talks to the Docling sidecar.</summary>
/// <remarks>
/// Defaults match the <c>docling-sidecar</c> compose file, so a developer with the container up
/// needs no configuration at all.
/// </remarks>
public sealed class DoclingOptions
{
    /// <summary>Where docling-serve is listening.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5001";

    /// <summary>A forty-page PDF with OCR is not quick.</summary>
    public int TimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// OCR. On by default because scanned statements, photographed forms and the pictures embedded
    /// in DOCX files are ordinary case material, and their text is invisible without it.
    /// </summary>
    public bool DoOcr { get; set; } = true;

    /// <summary>Re-OCR even where extractable text exists.</summary>
    public bool ForceOcr { get; set; }

    /// <summary>OCR language(s), e.g. <c>en</c>.</summary>
    public string? OcrLang { get; set; }

    /// <summary>
    /// TableFormer mode.
    /// </summary>
    /// <remarks>
    /// <c>fast</c>, deliberately. The reference converter records that <c>accurate</c> misaligned
    /// columns and values on densely-packed form-style tables — and a table whose values have moved
    /// columns is worse than one that failed to convert, because nothing downstream can tell.
    /// </remarks>
    public string TableMode { get; set; } = "fast";

    /// <summary>How images are represented in the export.</summary>
    public string ImageExportMode { get; set; } = "placeholder";

    /// <summary>Docling pipeline: legacy | standard | vlm | asr.</summary>
    public string Pipeline { get; set; } = "standard";

    /// <summary>
    /// Return the embedded images themselves.
    /// </summary>
    /// <remarks>
    /// Needed only when picture description is on — a table pasted into a document as a bitmap has
    /// no structure Docling can extract, so the bitmap has to come back for something else to read.
    /// Off otherwise, because base64 images make the response an order of magnitude larger.
    /// </remarks>
    public bool IncludeImages { get; set; }

    /// <summary>Resolution multiplier for returned images. Higher costs size and legibility both.</summary>
    public double ImagesScale { get; set; } = 2;

    /// <summary>Ask Docling to classify each picture (chart, table, logo, signature…).</summary>
    public bool DoPictureClassification { get; set; }
}

/// <summary>
/// Thin client for docling-serve's synchronous single-file convert endpoint.
/// </summary>
/// <remarks>
/// <b>Both <c>md</c> and <c>json</c> are requested in one call</b>, and that is the whole trick.
/// Docling's Markdown exporter drops content its structured export keeps — OCR'd image text,
/// key/value pairs, form fields, and on at least one case-file document every table in it. The
/// JSON is the truth; the Markdown is a rendering of part of it. Asking for both is what lets the
/// converter reconcile one against the other instead of trusting the lossy half.
/// </remarks>
public sealed class DoclingClient
{
    private readonly HttpClient _http;
    private readonly DoclingOptions _options;

    /// <summary>Creates a client against <paramref name="options"/>.</summary>
    public DoclingClient(DoclingOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _http = httpClient ?? new HttpClient();
        _http.BaseAddress ??= new Uri(_options.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    /// <summary>Whether the sidecar is reachable.</summary>
    /// <remarks>
    /// Worth calling before a batch: discovering the container is down after forty documents have
    /// each failed their own way is a poor use of anyone's afternoon.
    /// </remarks>
    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync("/health", ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>Converts one file, returning both exports.</summary>
    public async Task<ConvertDocumentResponse> ConvertAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var form = new MultipartFormDataContent();

        await using var fileStream = File.OpenRead(filePath);
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "files", Path.GetFileName(filePath));

        // Both formats, one call. See the class remarks: the JSON is the truth.
        AddField(form, "to_formats", "md");
        AddField(form, "to_formats", "json");

        AddField(form, "do_ocr", _options.DoOcr ? "true" : "false");
        AddField(form, "force_ocr", _options.ForceOcr ? "true" : "false");

        if (!string.IsNullOrWhiteSpace(_options.OcrLang))
        {
            AddField(form, "ocr_lang", _options.OcrLang!);
        }

        AddField(form, "table_mode", _options.TableMode);
        AddField(form, "image_export_mode", _options.ImageExportMode);
        AddField(form, "pipeline", _options.Pipeline);

        if (_options.IncludeImages)
        {
            AddField(form, "include_images", "true");
            AddField(form, "images_scale", _options.ImagesScale.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (_options.DoPictureClassification)
        {
            AddField(form, "do_picture_classification", "true");
        }

        using var response = await _http.PostAsync("/v1/convert/file", form, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"docling-serve returned {(int)response.StatusCode} for '{Path.GetFileName(filePath)}': {Truncate(body)}");
        }

        var result = JsonSerializer.Deserialize<ConvertDocumentResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException(
                $"docling-serve returned an empty response for '{Path.GetFileName(filePath)}'.");

        if (!string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            var errors = result.Errors is { Count: > 0 }
                ? string.Join("; ", result.Errors.Select(e => e.ErrorMessage))
                : "unknown error";

            var name = Path.GetFileName(filePath);

            // A document nobody can open is an operational fact, not a defect in this software, and
            // the two must not be triaged alike.
            if (DescribeUnreadable(errors) is { } reason)
            {
                throw new DocumentUnreadableException(name, reason);
            }

            throw new InvalidOperationException(
                $"docling-serve conversion status '{result.Status}' for '{name}': {errors}");
        }

        return result;
    }

    /// <summary>
    /// Recognises the failures that mean "this file cannot be opened", not "conversion broke".
    /// </summary>
    /// <remarks>
    /// Matched on the message because that is what survives the sidecar boundary — the underlying
    /// PDFium and docling-parse errors do not reach us as types.
    /// </remarks>
    internal static string? DescribeUnreadable(string errors)
    {
        if (errors.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            return "the file is password-protected. Obtain an unprotected copy, or record the "
                + "evidence as unavailable — it cannot be read without one.";
        }

        if (errors.Contains("could not load document", StringComparison.OrdinalIgnoreCase)
            || errors.Contains("corrupt", StringComparison.OrdinalIgnoreCase)
            || errors.Contains("damaged", StringComparison.OrdinalIgnoreCase))
        {
            return "the file could not be opened; it may be corrupt or truncated.";
        }

        return null;
    }

    private static void AddField(MultipartFormDataContent form, string name, string value) =>
        form.Add(new StringContent(value), name);

    private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000] + "…";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
