using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace AiPromptEvaluator;

/// <summary>Outcome of a Word-to-PDF conversion.</summary>
public sealed record PdfConversionResult(bool Success, string? PdfPath, string? Error)
{
    public static PdfConversionResult Ok(string pdfPath) => new(true, pdfPath, null);
    public static PdfConversionResult Failed(string error) => new(false, null, error);
}

/// <summary>
/// Converts Word documents to PDF so they can be sent through the Files API, which
/// does not accept .doc/.docx directly.
///
/// Two engines are tried in order: Microsoft Word via COM automation (best fidelity,
/// needs Word installed) and then LibreOffice in headless mode. Converted PDFs are
/// cached under the user's temp folder and reused while the source file is unchanged.
/// </summary>
public static class DocumentToPdfConverter
{
    private static readonly string CacheRoot =
        Path.Combine(Path.GetTempPath(), "AiPromptEvaluator", "pdf-cache");

    // Word constants: wdFormatPDF, wdDoNotSaveChanges.
    private const int WdFormatPdf = 17;
    private const int WdDoNotSaveChanges = 0;

    private static readonly TimeSpan LibreOfficeTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Word-family formats the Files API can't take but Word/LibreOffice can render:
    /// the modern and legacy document formats, their macro-enabled and template
    /// variants, plus Rich Text and OpenDocument Text.
    /// </summary>
    public static bool IsConvertible(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() is
            ".doc" or ".docx" or ".docm" or
            ".dot" or ".dotx" or ".dotm" or
            ".rtf" or ".odt";

    /// <summary>True when at least one conversion engine is present on this machine.</summary>
    public static bool IsAvailable => IsWordAvailable() || FindLibreOffice() is not null;

    /// <summary>
    /// Returns a PDF rendering of <paramref name="sourcePath"/>, converting it if the
    /// cached copy is missing or stale. Blocking — call it from a background thread.
    /// </summary>
    public static PdfConversionResult Convert(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            return PdfConversionResult.Failed($"File not found: {sourcePath}");
        }

        if (!IsConvertible(sourcePath))
        {
            return PdfConversionResult.Failed(
                $"'{Path.GetExtension(sourcePath)}' is not a Word-family document.");
        }

        var cachedPdf = CachePathFor(sourcePath);
        if (IsCacheFresh(sourcePath, cachedPdf))
        {
            return PdfConversionResult.Ok(cachedPdf);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cachedPdf)!);

        var errors = new List<string>();

        if (IsWordAvailable())
        {
            var wordError = ConvertWithWord(sourcePath, cachedPdf);
            if (wordError is null && File.Exists(cachedPdf))
            {
                return PdfConversionResult.Ok(cachedPdf);
            }

            errors.Add($"Word: {wordError ?? "produced no output"}");
        }

        var soffice = FindLibreOffice();
        if (soffice is not null)
        {
            var loError = ConvertWithLibreOffice(soffice, sourcePath, cachedPdf);
            if (loError is null && File.Exists(cachedPdf))
            {
                return PdfConversionResult.Ok(cachedPdf);
            }

            errors.Add($"LibreOffice: {loError ?? "produced no output"}");
        }

        if (errors.Count == 0)
        {
            errors.Add("no converter found — install Microsoft Word or LibreOffice.");
        }

        return PdfConversionResult.Failed(string.Join("; ", errors));
    }

    // ──────────────────────────────────────────────
    // Cache
    // ──────────────────────────────────────────────

    /// <summary>Cache path keyed by the full source path, so same-named files don't collide.</summary>
    private static string CachePathFor(string sourcePath)
    {
        var hash = System.Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(sourcePath.ToLowerInvariant())))[..16];

        var name = Path.GetFileNameWithoutExtension(sourcePath);
        return Path.Combine(CacheRoot, hash, name + ".pdf");
    }

    private static bool IsCacheFresh(string sourcePath, string pdfPath) =>
        File.Exists(pdfPath) &&
        File.GetLastWriteTimeUtc(pdfPath) >= File.GetLastWriteTimeUtc(sourcePath);

    // ──────────────────────────────────────────────
    // Microsoft Word (COM automation)
    // ──────────────────────────────────────────────

    private static bool IsWordAvailable() => OfficeComClient.IsRegistered("Word.Application");

    /// <summary>Returns null on success, or the error message on failure.</summary>
    [SupportedOSPlatform("windows")]
    private static string? ConvertWithWord(string sourcePath, string pdfPath) =>
        OfficeComClient.Run("Word.Application", "WINWORD", word =>
        {
            object? documents = null;
            object? document = null;

            try
            {
                documents = OfficeComClient.GetProperty(word, "Documents");

                // Open(FileName, ConfirmConversions, ReadOnly, AddToRecentFiles)
                document = OfficeComClient.Invoke(documents!, "Open", sourcePath, false, true, false)
                    ?? throw new InvalidOperationException("Word could not open the document.");

                // SaveAs2(FileName, FileFormat) — wdFormatPDF writes a PDF rendering.
                OfficeComClient.Invoke(document, "SaveAs2", pdfPath, WdFormatPdf);
            }
            finally
            {
                OfficeComClient.TryInvoke(document, "Close", WdDoNotSaveChanges);
                OfficeComClient.Release(document);
                OfficeComClient.Release(documents);
            }
        });

    // ──────────────────────────────────────────────
    // LibreOffice (headless)
    // ──────────────────────────────────────────────

    private static string? FindLibreOffice()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "LibreOffice", "program", "soffice.exe"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>Returns null on success, or the error message on failure.</summary>
    private static string? ConvertWithLibreOffice(string soffice, string sourcePath, string pdfPath)
    {
        var outputDir = Path.GetDirectoryName(pdfPath)!;

        var startInfo = new ProcessStartInfo(soffice)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--norestore");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add("pdf");
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(outputDir);
        startInfo.ArgumentList.Add(sourcePath);

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return "could not start soffice.exe.";
            }

            if (!process.WaitForExit((int)LibreOfficeTimeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Already exiting.
                }

                return $"timed out after {LibreOfficeTimeout.TotalMinutes:0} minutes.";
            }

            if (process.ExitCode != 0)
            {
                var stderr = process.StandardError.ReadToEnd().Trim();
                return string.IsNullOrEmpty(stderr) ? $"exit code {process.ExitCode}." : stderr;
            }

            // LibreOffice names the output after the source file; move it into place.
            var produced = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(sourcePath) + ".pdf");
            if (!string.Equals(produced, pdfPath, StringComparison.OrdinalIgnoreCase) && File.Exists(produced))
            {
                File.Move(produced, pdfPath, overwrite: true);
            }

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message.Trim();
        }
    }
}
