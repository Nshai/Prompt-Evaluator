using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace AiPromptEvaluator;

/// <summary>Outcome of normalising a spreadsheet to .xlsx.</summary>
public sealed record NormalizeResult(bool Success, string? XlsxPath, string? Error)
{
    public static NormalizeResult Ok(string path) => new(true, path, null);
    public static NormalizeResult Failed(string error) => new(false, null, error);
}

/// <summary>
/// Rewrites spreadsheets to .xlsx when Docling can't read them as they are.
///
/// Docling's Excel backend only handles the OOXML formats. Verified against the running
/// sidecar: .xls and .xlsb are rejected outright, and .ods did not complete within three
/// minutes. Those go through Excel first, which is fast and lossless for cell data — the
/// only thing that reaches the model anyway, since Docling renders sheets as Markdown tables.
/// </summary>
public static class SpreadsheetNormalizer
{
    private static readonly string CacheRoot =
        Path.Combine(Path.GetTempPath(), "AiPromptEvaluator", "xlsx-cache");

    /// <summary>xlOpenXMLWorkbook — the plain, non-macro .xlsx format.</summary>
    private const int XlOpenXmlWorkbook = 51;

    /// <summary>Formats Docling reads directly, so they never need rewriting.</summary>
    private static bool IsDoclingReadable(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() is
            ".xlsx" or ".xlsm" or ".xltx" or ".xltm";

    public static bool IsExcelAvailable() => OfficeComHost.IsRegistered("Excel.Application");

    /// <summary>
    /// Returns a path Docling can read: the source itself when it's already OOXML, or a
    /// cached .xlsx rewrite. Blocking — call it from a background thread.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static NormalizeResult Normalize(string sourcePath)
    {
        if (IsDoclingReadable(sourcePath))
        {
            return NormalizeResult.Ok(sourcePath);
        }

        var cached = CachePathFor(sourcePath);
        if (File.Exists(cached) &&
            File.GetLastWriteTimeUtc(cached) >= File.GetLastWriteTimeUtc(sourcePath))
        {
            return NormalizeResult.Ok(cached);
        }

        if (!IsExcelAvailable())
        {
            return NormalizeResult.Failed(
                $"'{Path.GetExtension(sourcePath)}' needs Excel to be converted to .xlsx first, "
                + "and Excel is not installed on this machine.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cached)!);

        var error = OfficeComHost.Run("Excel.Application", "EXCEL", excel =>
        {
            object? workbooks = null;
            object? workbook = null;

            try
            {
                workbooks = OfficeComHost.GetProperty(excel, "Workbooks");

                // Open(FileName, UpdateLinks, ReadOnly) — never chase links to other files.
                workbook = OfficeComHost.Invoke(workbooks!, "Open", sourcePath, 0, true)
                    ?? throw new InvalidOperationException("Excel could not open the workbook.");

                OfficeComHost.Invoke(workbook, "SaveAs", cached, XlOpenXmlWorkbook);
            }
            finally
            {
                OfficeComHost.TryInvoke(workbook, "Close", false);
                OfficeComHost.Release(workbook);
                OfficeComHost.Release(workbooks);
            }
        });

        if (error is not null)
        {
            return NormalizeResult.Failed($"Excel: {error}");
        }

        return File.Exists(cached)
            ? NormalizeResult.Ok(cached)
            : NormalizeResult.Failed("Excel produced no .xlsx output.");
    }

    /// <summary>Cache path keyed by the full source path, so same-named files don't collide.</summary>
    private static string CachePathFor(string sourcePath)
    {
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(sourcePath.ToLowerInvariant())))[..16];

        var name = Path.GetFileNameWithoutExtension(sourcePath);
        return Path.Combine(CacheRoot, hash, name + ".xlsx");
    }
}
