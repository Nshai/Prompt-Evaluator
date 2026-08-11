using AiPromptEvaluator;
using Xunit;

namespace AiPromptEvaluator.Tests;

public class CaseDocumentTests
{
    private static CaseDocument Doc(string path, string? fileId, string? error = null) =>
        new(path, Path.GetFileName(path), "B", "Know Your Client",
            fileId is null ? AnthropicFileKind.Unsupported : AnthropicFileKind.Document,
            fileId, ConversionTarget.None, error);

    /// <summary>
    /// A spreadsheet with no file_id never made it through Docling. Its bytes say nothing as
    /// inline text, so it must be dropped rather than sent.
    /// </summary>
    [Theory]
    [InlineData(@"C:\case\B\holdings.xlsx")]
    [InlineData(@"C:\case\B\legacy.xls")]
    [InlineData(@"C:\case\B\big.xlsb")]
    [InlineData(@"C:\case\B\open.ods")]
    public void Spreadsheets_WithoutAFileId_AreExcluded(string path)
    {
        var document = Doc(path, fileId: null, error: "Docling is not responding");

        Assert.True(document.IsExcludedSpreadsheet);
        Assert.False(document.IsSendable);
    }

    [Fact]
    public void Spreadsheets_ThatUploaded_AreSendable()
    {
        var document = Doc(@"C:\case\B\holdings.xlsx", fileId: "file_abc123");

        Assert.False(document.IsExcludedSpreadsheet);
        Assert.True(document.IsSendable);
    }

    /// <summary>Non-spreadsheets keep the inline-text fallback — text files are readable as-is.</summary>
    [Theory]
    [InlineData(@"C:\case\B\archive.zip")]
    [InlineData(@"C:\case\B\notes.log")]
    public void NonSpreadsheets_WithoutAFileId_AreStillSent(string path)
    {
        var document = Doc(path, fileId: null);

        Assert.False(document.IsExcludedSpreadsheet);
        Assert.True(document.IsSendable);
    }

    /// <summary>A failed Word conversion is reported, not silently dropped like a spreadsheet.</summary>
    [Fact]
    public void FailedWordConversions_AreStillSent_AsANote()
    {
        var document = Doc(@"C:\case\B\report.docx", fileId: null, error: "The file appears to be corrupted.");

        Assert.False(document.IsExcludedSpreadsheet);
        Assert.True(document.IsSendable);
        Assert.NotNull(document.Error);
    }

    [Fact]
    public void EquivalentCaseFolderPaths_AreTreatedAsTheSameLibrary()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ai-prompt-evaluator-tests", "case-folder");
        Directory.CreateDirectory(folder);

        try
        {
            var normalized = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var withTrailingSeparator = normalized + Path.DirectorySeparatorChar;

            Assert.True(CaseDocumentLibrary.AreSameFolder(normalized, withTrailingSeparator));
            Assert.True(CaseDocumentLibrary.AreSameFolder(normalized, Path.GetFullPath(withTrailingSeparator)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteUploadedFileAsync_IgnoresEmptyFileIds()
    {
        var uploader = new AnthropicFileUploader(
            new PromptEvaluator(new AppSettings()),
            new SpreadsheetToMarkdownConverter(new DoclingClient(() => "http://localhost")));

        await Record.ExceptionAsync(() => uploader.DeleteUploadedFileAsync(string.Empty));
    }
}
