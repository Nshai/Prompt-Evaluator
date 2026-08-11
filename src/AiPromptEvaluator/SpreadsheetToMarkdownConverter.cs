using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AiPromptEvaluator;

/// <summary>Outcome of a spreadsheet-to-Markdown conversion.</summary>
public sealed record MarkdownConversionResult(bool Success, string? MarkdownPath, string? Error)
{
    public static MarkdownConversionResult Ok(string path) => new(true, path, null);
    public static MarkdownConversionResult Failed(string error) => new(false, null, error);
}

/// <summary>
/// Converts spreadsheets to Markdown through the Docling sidecar, because no content block
/// takes .xlsx and a spreadsheet sent as raw bytes is unreadable to the model.
/// Docling renders each sheet as a Markdown table, which reads cleanly and costs far fewer
/// tokens than a PDF rendering of the same grid.
///
/// Converted files are cached under the user's temp folder and reused while the source is
/// unchanged — the same contract as <see cref="DocumentToPdfConverter"/>.
/// </summary>
public sealed class SpreadsheetToMarkdownConverter
{
    private static readonly string CacheRoot =
        Path.Combine(Path.GetTempPath(), "AiPromptEvaluator", "markdown-cache");

    private readonly DoclingClient _docling;

    public SpreadsheetToMarkdownConverter(DoclingClient docling)
    {
        _docling = docling;
    }

    /// <summary>
    /// Spreadsheet formats the API can't take but Docling can render: the modern and
    /// legacy Excel formats, their macro-enabled and binary variants, and OpenDocument.
    /// </summary>
    public static bool IsConvertible(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() is
            ".xlsx" or ".xlsm" or ".xltx" or ".xltm" or
            ".xls" or ".xlt" or ".xlsb" or ".ods";

    /// <summary>
    /// Returns a Markdown rendering of <paramref name="sourcePath"/>, converting it if the
    /// cached copy is missing or stale.
    /// </summary>
    public async Task<MarkdownConversionResult> ConvertAsync(
        string sourcePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            return MarkdownConversionResult.Failed($"File not found: {sourcePath}");
        }

        if (!IsConvertible(sourcePath))
        {
            return MarkdownConversionResult.Failed(
                $"'{Path.GetExtension(sourcePath)}' is not a spreadsheet.");
        }

        var cached = CachePathFor(sourcePath);
        if (IsCacheFresh(sourcePath, cached))
        {
            return MarkdownConversionResult.Ok(cached);
        }

        // Docling's Excel backend only reads OOXML, so rewrite the older formats first.
        var normalized = await Task.Run(
            () => SpreadsheetNormalizer.Normalize(sourcePath), cancellationToken).ConfigureAwait(false);

        if (!normalized.Success)
        {
            return MarkdownConversionResult.Failed(normalized.Error!);
        }

        var conversion = await _docling.ConvertToMarkdownAsync(normalized.XlsxPath!, cancellationToken)
            .ConfigureAwait(false);

        if (!conversion.Success)
        {
            return MarkdownConversionResult.Failed(conversion.Error!);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cached)!);

        // A heading keeps the source obvious once the file is one block among many.
        var markdown = new StringBuilder()
            .AppendLine($"# {Path.GetFileName(sourcePath)}")
            .AppendLine()
            .Append(conversion.Markdown)
            .ToString();

        await File.WriteAllTextAsync(cached, markdown, Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);

        return MarkdownConversionResult.Ok(cached);
    }

    /// <summary>Cache path keyed by the full source path, so same-named files don't collide.</summary>
    private static string CachePathFor(string sourcePath)
    {
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(sourcePath.ToLowerInvariant())))[..16];

        var name = Path.GetFileNameWithoutExtension(sourcePath);
        return Path.Combine(CacheRoot, hash, name + ".md");
    }

    private static bool IsCacheFresh(string sourcePath, string markdownPath) =>
        File.Exists(markdownPath) &&
        File.GetLastWriteTimeUtc(markdownPath) >= File.GetLastWriteTimeUtc(sourcePath);
}
