using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace AiPromptEvaluator;

/// <summary>Outcome of a Docling conversion.</summary>
public sealed record DoclingConversionResult(bool Success, string? Markdown, string? Error)
{
    public static DoclingConversionResult Ok(string markdown) => new(true, markdown, null);
    public static DoclingConversionResult Failed(string error) => new(false, null, error);
}

/// <summary>
/// Talks to the Docling sidecar — a docling-serve container that renders spreadsheets and
/// other rich documents to Markdown. The endpoint comes from settings, so the container can
/// live anywhere reachable (localhost by default, or a shared host).
/// </summary>
public sealed class DoclingClient : IDoclingClient
{
    private static readonly TimeSpan ConvertTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(10);

    private readonly Func<string> _endpointProvider;
    private readonly HttpClient _http;

    /// <param name="endpointProvider">
    /// Reads the endpoint fresh on each call, so a change in settings takes effect without a restart.
    /// </param>
    public DoclingClient(Func<string> endpointProvider)
    {
        _endpointProvider = endpointProvider;
        _http = new HttpClient { Timeout = ConvertTimeout };
    }

    public string Endpoint => _endpointProvider().TrimEnd('/');

    /// <summary>True when the sidecar answers its health probe. Never throws.</summary>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Endpoint}/health");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(HealthTimeout);

            using var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Uploads <paramref name="filePath"/> to the sidecar and returns its Markdown rendering.
    /// Spreadsheets come back as Markdown tables, which is what makes them readable to the model.
    /// </summary>
    public async Task<DoclingConversionResult> ConvertToMarkdownAsync(
        string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            await using var stream = File.OpenRead(filePath);

            var fileContent = new StreamContent(stream);
            content.Add(fileContent, "files", Path.GetFileName(filePath));
            content.Add(new StringContent("md"), "to_formats");

            // Spreadsheets carry no scanned pages, so skip OCR — it only costs time here.
            content.Add(new StringContent("false"), "do_ocr");

            using var response = await _http
                .PostAsync($"{Endpoint}/v1/convert/file", content, cancellationToken)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return DoclingConversionResult.Failed(
                    $"Docling returned {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body)}");
            }

            return ReadMarkdown(body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return DoclingConversionResult.Failed(
                $"Could not reach Docling at {Endpoint} ({ex.Message.Trim()}). "
                + "Check the endpoint in Settings and that the container is running.");
        }
        catch (TaskCanceledException)
        {
            return DoclingConversionResult.Failed(
                $"Docling did not respond within {ConvertTimeout.TotalMinutes:0} minutes.");
        }
        catch (Exception ex)
        {
            return DoclingConversionResult.Failed(ex.Message.Trim());
        }
    }

    /// <summary>
    /// Pulls md_content out of the conversion response. Docling reports a per-document status
    /// separately from the HTTP status, so a 200 can still describe a failed conversion.
    /// </summary>
    private static DoclingConversionResult ReadMarkdown(string body)
    {
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        if (root.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is { } text &&
            !text.Equals("success", StringComparison.OrdinalIgnoreCase) &&
            !text.Equals("partial_success", StringComparison.OrdinalIgnoreCase))
        {
            return DoclingConversionResult.Failed($"Docling reported status '{text}'. {ReadErrors(root)}".Trim());
        }

        if (!root.TryGetProperty("document", out var document) ||
            !document.TryGetProperty("md_content", out var markdown) ||
            markdown.ValueKind != JsonValueKind.String)
        {
            return DoclingConversionResult.Failed($"Docling returned no Markdown. {ReadErrors(root)}".Trim());
        }

        var value = markdown.GetString();
        return string.IsNullOrWhiteSpace(value)
            ? DoclingConversionResult.Failed("Docling produced an empty document.")
            : DoclingConversionResult.Ok(value);
    }

    private static string ReadErrors(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        return string.Join("; ", errors.EnumerateArray().Select(e => e.ToString()));
    }

    private static string Truncate(string value) =>
        value.Length > 400 ? value[..400] + "..." : value;
}
