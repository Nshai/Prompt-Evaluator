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

public sealed record AnthropicFileRef(AnthropicFileKind Kind, string? FileId, string FilePath);

/// <summary>
/// Uploads case documents to the Anthropic Files API and caches the resulting file_id
/// for the lifetime of the session, so re-running a check against the same case folder
/// doesn't re-upload unchanged files.
/// </summary>
public sealed class AnthropicFileUploader
{
    private readonly PromptEvaluator _evaluator;
    private readonly ConcurrentDictionary<(string Path, DateTime WriteUtc), string> _cache = new();

    public AnthropicFileUploader(PromptEvaluator evaluator)
    {
        _evaluator = evaluator;
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
        var kind = Classify(filePath);
        if (kind == AnthropicFileKind.Unsupported)
        {
            return new AnthropicFileRef(kind, null, filePath);
        }

        var key = (filePath, File.GetLastWriteTimeUtc(filePath));
        if (_cache.TryGetValue(key, out var cachedFileId))
        {
            return new AnthropicFileRef(kind, cachedFileId, filePath);
        }

        using var stream = File.OpenRead(filePath);
        var fileId = await _evaluator.UploadFileAsync(
            stream, Path.GetFileName(filePath), MimeTypeFor(filePath, kind), cancellationToken).ConfigureAwait(false);

        _cache[key] = fileId;
        return new AnthropicFileRef(kind, fileId, filePath);
    }
}
