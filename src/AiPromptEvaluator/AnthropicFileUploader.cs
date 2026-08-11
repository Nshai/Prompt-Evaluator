using System.Collections.Concurrent;

namespace AiPromptEvaluator;

public enum AnthropicFileKind
{
    /// <summary>PDF or plain-text file — referenced via a document content block.</summary>
    Document,

    /// <summary>Image file — referenced via an image content block.</summary>
    Image,

    /// <summary>Format the Files API document/image blocks don't accept (e.g. .docx, .xlsx, .zip).</summary>
    Unsupported,
}

/// <summary>What a source file was rendered to before being uploaded.</summary>
public enum ConversionTarget
{
    /// <summary>Uploaded as-is.</summary>
    None,

    /// <summary>Word document rendered to PDF by <see cref="DocumentToPdfConverter"/>.</summary>
    Pdf,

    /// <summary>Spreadsheet rendered to Markdown tables by Docling.</summary>
    Markdown,
}

/// <param name="FilePath">The original case-folder file.</param>
/// <param name="UploadedPath">What was actually sent — differs from <paramref name="FilePath"/> for converted files.</param>
/// <param name="ConversionError">Why the file could not be converted, if it couldn't.</param>
public sealed record AnthropicFileRef(
    AnthropicFileKind Kind,
    string? FileId,
    string FilePath,
    string? UploadedPath = null,
    string? ConversionError = null,
    ConversionTarget ConvertedTo = ConversionTarget.None);

/// <summary>
/// Uploads case documents to the Anthropic Files API and caches the resulting file_id
/// for the lifetime of the session, so re-running a check against the same case folder
/// doesn't re-upload unchanged files.
/// </summary>
public sealed class AnthropicFileUploader
{
    private readonly PromptEvaluator _evaluator;
    private readonly SpreadsheetToMarkdownConverter _spreadsheets;
    private readonly ConcurrentDictionary<(string Path, DateTime WriteUtc), string> _cache = new();

    public AnthropicFileUploader(PromptEvaluator evaluator, SpreadsheetToMarkdownConverter spreadsheets)
    {
        _evaluator = evaluator;
        _spreadsheets = spreadsheets;
    }

    public static AnthropicFileKind Classify(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".pdf" or ".txt" or ".md" or ".csv" or ".json" => AnthropicFileKind.Document,
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" => AnthropicFileKind.Image,
        _ => AnthropicFileKind.Unsupported,
    };

    private static string MimeTypeFor(string filePath, AnthropicFileKind kind) => (kind, Path.GetExtension(filePath).ToLowerInvariant()) switch
    {
        (AnthropicFileKind.Document, ".pdf") => "application/pdf",
        (AnthropicFileKind.Document, _) => "text/plain",
        (AnthropicFileKind.Image, ".png") => "image/png",
        (AnthropicFileKind.Image, ".jpg" or ".jpeg") => "image/jpeg",
        (AnthropicFileKind.Image, ".gif") => "image/gif",
        (AnthropicFileKind.Image, ".webp") => "image/webp",
        _ => "application/octet-stream",
    };

    /// <summary>
    /// Returns a file_id reference for documents/images (uploading on cache miss), or an
    /// unsupported-kind reference with no file_id for formats the Files API can't take directly.
    /// </summary>
    public async Task<AnthropicFileRef> GetOrUploadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Neither Word documents nor spreadsheets are accepted by the Files API — render
        // them to a format that is (PDF via Word, Markdown via Docling) before uploading.
        var uploadPath = filePath;
        string? conversionError = null;
        var convertedTo = ConversionTarget.None;

        if (DocumentToPdfConverter.IsConvertible(filePath))
        {
            var conversion = await Task.Run(
                () => DocumentToPdfConverter.Convert(filePath), cancellationToken).ConfigureAwait(false);

            if (conversion.Success)
            {
                uploadPath = conversion.PdfPath!;
                convertedTo = ConversionTarget.Pdf;
            }
            else
            {
                conversionError = conversion.Error;
            }
        }
        else if (SpreadsheetToMarkdownConverter.IsConvertible(filePath))
        {
            var conversion = await _spreadsheets.ConvertAsync(filePath, cancellationToken).ConfigureAwait(false);

            if (conversion.Success)
            {
                uploadPath = conversion.MarkdownPath!;
                convertedTo = ConversionTarget.Markdown;
            }
            else
            {
                conversionError = conversion.Error;
            }
        }

        var kind = Classify(uploadPath);
        if (kind == AnthropicFileKind.Unsupported)
        {
            return new AnthropicFileRef(kind, null, filePath, ConversionError: conversionError);
        }

        // Cache against the original file, so an edited source is re-converted and re-uploaded.
        var key = (filePath, File.GetLastWriteTimeUtc(filePath));
        if (_cache.TryGetValue(key, out var cachedFileId))
        {
            return new AnthropicFileRef(kind, cachedFileId, filePath, uploadPath, ConvertedTo: convertedTo);
        }

        using var stream = File.OpenRead(uploadPath);
        var fileId = await _evaluator.UploadFileAsync(
            stream, Path.GetFileName(uploadPath), MimeTypeFor(uploadPath, kind), cancellationToken).ConfigureAwait(false);

        _cache[key] = fileId;
        return new AnthropicFileRef(kind, fileId, filePath, uploadPath, ConvertedTo: convertedTo);
    }

    public async Task DeleteUploadedFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return;
        }

        await _evaluator.DeleteFileAsync(fileId, cancellationToken).ConfigureAwait(false);

        foreach (var key in _cache.Keys.ToList())
        {
            if (_cache[key] == fileId)
            {
                _cache.TryRemove(key, out _);
            }
        }
    }
}
