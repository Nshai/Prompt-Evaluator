using System.Collections.Concurrent;

namespace AiPromptEvaluator;

public enum AnthropicFileKind
{
    /// <summary>PDF or plain-text file — carried in a document content block.</summary>
    Document,

    /// <summary>Image file — carried in an image content block.</summary>
    Image,

    /// <summary>Format neither block type accepts (e.g. .zip) — sent as a text preview.</summary>
    Unsupported,
}

/// <summary>What a source file was rendered to before being sent.</summary>
public enum ConversionTarget
{
    /// <summary>Sent as-is.</summary>
    None,

    /// <summary>Word document rendered to PDF by <see cref="DocumentToPdfConverter"/>.</summary>
    Pdf,

    /// <summary>Spreadsheet rendered to Markdown tables by Docling.</summary>
    Markdown,
}

/// <param name="FilePath">The original case-folder file.</param>
/// <param name="PreparedPath">What will actually be sent — differs from <paramref name="FilePath"/> for converted files.</param>
/// <param name="ConversionError">Why the file could not be converted, if it couldn't.</param>
public sealed record PreparedFileRef(
    AnthropicFileKind Kind,
    string FilePath,
    string? PreparedPath = null,
    string? ConversionError = null,
    ConversionTarget ConvertedTo = ConversionTarget.None);

/// <summary>
/// Gets a case document into a form the Messages API accepts inline: Word documents become
/// PDFs, spreadsheets become Markdown, everything else is used as it stands. Nothing is
/// uploaded — documents travel with the request as base64 or text, which is what makes the
/// app work through a LiteLLM proxy to Bedrock, where the Files API does not exist.
///
/// Conversion results are remembered for the session, keyed by path and last-write time, so
/// a re-load of the same case folder doesn't drive Word or Docling again.
/// </summary>
public sealed class CaseFilePreparer
{
    private readonly SpreadsheetToMarkdownConverter _spreadsheets;
    private readonly ConcurrentDictionary<(string Path, DateTime WriteUtc), string> _converted = new();

    public CaseFilePreparer(SpreadsheetToMarkdownConverter spreadsheets)
    {
        _spreadsheets = spreadsheets;
    }

    public static AnthropicFileKind Classify(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".pdf" or ".txt" or ".md" or ".csv" or ".json" => AnthropicFileKind.Document,
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" => AnthropicFileKind.Image,
        _ => AnthropicFileKind.Unsupported,
    };

    /// <summary>
    /// Returns the file in sendable form, converting first where the source format needs it.
    /// A conversion failure is reported rather than thrown — the caller decides whether the
    /// document is worth sending as a text preview or dropping.
    /// </summary>
    public async Task<PreparedFileRef> PrepareAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var key = (filePath, SafeWriteTimeUtc(filePath));
        if (_converted.TryGetValue(key, out var alreadyConverted))
        {
            return Describe(filePath, alreadyConverted, null);
        }

        if (DocumentToPdfConverter.IsConvertible(filePath))
        {
            var conversion = await Task.Run(
                () => DocumentToPdfConverter.Convert(filePath), cancellationToken).ConfigureAwait(false);

            if (!conversion.Success)
            {
                return new PreparedFileRef(
                    AnthropicFileKind.Unsupported, filePath, ConversionError: conversion.Error);
            }

            _converted[key] = conversion.PdfPath!;
            return Describe(filePath, conversion.PdfPath!, null);
        }

        if (SpreadsheetToMarkdownConverter.IsConvertible(filePath))
        {
            var conversion = await _spreadsheets.ConvertAsync(filePath, cancellationToken).ConfigureAwait(false);

            if (!conversion.Success)
            {
                return new PreparedFileRef(
                    AnthropicFileKind.Unsupported, filePath, ConversionError: conversion.Error);
            }

            _converted[key] = conversion.MarkdownPath!;
            return Describe(filePath, conversion.MarkdownPath!, null);
        }

        return new PreparedFileRef(Classify(filePath), filePath, filePath);
    }

    private static PreparedFileRef Describe(string filePath, string preparedPath, string? error)
    {
        var kind = Classify(preparedPath);
        var convertedTo = kind switch
        {
            _ when preparedPath.Equals(filePath, StringComparison.OrdinalIgnoreCase) => ConversionTarget.None,
            _ when Path.GetExtension(preparedPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                => ConversionTarget.Pdf,
            _ => ConversionTarget.Markdown,
        };

        return new PreparedFileRef(kind, filePath, preparedPath, error, convertedTo);
    }

    private static DateTime SafeWriteTimeUtc(string filePath)
    {
        try
        {
            return File.GetLastWriteTimeUtc(filePath);
        }
        catch
        {
            // An unreadable timestamp just means this file never hits the conversion cache.
            return DateTime.MinValue;
        }
    }
}
