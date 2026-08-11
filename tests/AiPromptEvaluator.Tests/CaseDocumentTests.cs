using AiPromptEvaluator;
using Xunit;

namespace AiPromptEvaluator.Tests;

public class CaseDocumentTests
{
    private static CaseDocument Doc(string path, string? preparedPath, string? error = null) =>
        new(path, Path.GetFileName(path), "B", "Know Your Client",
            preparedPath is null ? AnthropicFileKind.Unsupported : AnthropicFileKind.Document,
            preparedPath, ConversionTarget.None, error);

    /// <summary>
    /// A spreadsheet with no prepared output never made it through Docling. Its bytes say
    /// nothing as inline text, so it must be dropped rather than sent.
    /// </summary>
    [Theory]
    [InlineData(@"C:\case\B\holdings.xlsx")]
    [InlineData(@"C:\case\B\legacy.xls")]
    [InlineData(@"C:\case\B\big.xlsb")]
    [InlineData(@"C:\case\B\open.ods")]
    public void Spreadsheets_ThatFailedToConvert_AreExcluded(string path)
    {
        var document = Doc(path, preparedPath: null, error: "Docling is not responding");

        Assert.True(document.IsExcludedSpreadsheet);
        Assert.False(document.IsSendable);
    }

    [Fact]
    public void Spreadsheets_ThatConverted_AreSendable()
    {
        var document = Doc(@"C:\case\B\holdings.xlsx", preparedPath: @"C:\temp\holdings.md");

        Assert.False(document.IsExcludedSpreadsheet);
        Assert.True(document.IsSendable);
        Assert.Equal(@"C:\temp\holdings.md", document.BilledPath);
    }

    /// <summary>Non-spreadsheets keep the text-preview fallback — text files are readable as-is.</summary>
    [Theory]
    [InlineData(@"C:\case\B\archive.zip")]
    [InlineData(@"C:\case\B\notes.log")]
    public void NonSpreadsheets_WithoutPreparedOutput_AreStillSent(string path)
    {
        var document = Doc(path, preparedPath: null);

        Assert.False(document.IsExcludedSpreadsheet);
        Assert.True(document.IsSendable);
    }

    /// <summary>A failed Word conversion is reported, not silently dropped like a spreadsheet.</summary>
    [Fact]
    public void FailedWordConversions_AreStillSent_AsANote()
    {
        var document = Doc(@"C:\case\B\report.docx", preparedPath: null, error: "The file appears to be corrupted.");

        Assert.False(document.IsExcludedSpreadsheet);
        Assert.True(document.IsSendable);
        Assert.NotNull(document.Error);
    }

    /// <summary>Without a conversion the source file itself is what travels with the request.</summary>
    [Fact]
    public void BilledPath_FallsBackToTheSourceFile()
    {
        Assert.Equal(@"C:\case\B\notes.txt", Doc(@"C:\case\B\notes.txt", preparedPath: null).BilledPath);
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

    /// <summary>
    /// A format the API takes as it stands needs no conversion step at all — it is sent
    /// straight from the case folder, which is also what keeps a load fast.
    /// </summary>
    [Fact]
    public async Task PrepareAsync_PassesThroughFormatsThatNeedNoConversion()
    {
        var preparer = new CaseFilePreparer(
            new SpreadsheetToMarkdownConverter(new DoclingClient(() => "http://localhost")));

        var path = Path.Combine(Path.GetTempPath(), $"aipe-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "client objectives");

        try
        {
            var prepared = await preparer.PrepareAsync(path);

            Assert.Equal(AnthropicFileKind.Document, prepared.Kind);
            Assert.Equal(path, prepared.PreparedPath);
            Assert.Equal(ConversionTarget.None, prepared.ConvertedTo);
            Assert.Null(prepared.ConversionError);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
