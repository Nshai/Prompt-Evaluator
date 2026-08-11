using AiPromptEvaluator;
using Xunit;

namespace AiPromptEvaluator.Tests;

public class DocumentToPdfConverterTests
{
    [Theory]
    [InlineData("report.doc")]
    [InlineData("report.docx")]
    [InlineData("report.docm")]
    [InlineData("template.dot")]
    [InlineData("template.dotx")]
    [InlineData("template.dotm")]
    [InlineData("notes.rtf")]
    [InlineData("notes.odt")]
    public void IsConvertible_IsTrue_ForWordFamilyFormats(string fileName)
    {
        Assert.True(DocumentToPdfConverter.IsConvertible(fileName));
    }

    [Theory]
    [InlineData("report.DOCX")]
    [InlineData("report.Doc")]
    public void IsConvertible_IgnoresCase(string fileName)
    {
        Assert.True(DocumentToPdfConverter.IsConvertible(fileName));
    }

    [Theory]
    [InlineData("scan.pdf")]      // already uploadable as-is
    [InlineData("notes.txt")]     // already uploadable as-is
    [InlineData("chart.png")]     // uploadable as an image
    [InlineData("data.xlsx")]     // not a Word format
    [InlineData("deck.pptx")]     // not a Word format
    [InlineData("archive.zip")]
    [InlineData("noextension")]
    public void IsConvertible_IsFalse_ForEverythingElse(string fileName)
    {
        Assert.False(DocumentToPdfConverter.IsConvertible(fileName));
    }

    [Fact]
    public void Convert_Fails_WhenFileDoesNotExist()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".docx");

        var result = DocumentToPdfConverter.Convert(missing);

        Assert.False(result.Success);
        Assert.Null(result.PdfPath);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public void Convert_Fails_ForNonWordFormat()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xlsx");
        File.WriteAllText(tempFile, "not a word document");

        try
        {
            var result = DocumentToPdfConverter.Convert(tempFile);

            Assert.False(result.Success);
            Assert.Contains("not a Word-family document", result.Error);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
