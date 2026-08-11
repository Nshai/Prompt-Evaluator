using AiPromptEvaluator;
using Xunit;

namespace AiPromptEvaluator.Tests;

public class SpreadsheetToMarkdownConverterTests
{
    /// <summary>Points at a port nothing listens on, so no test ever depends on a running container.</summary>
    private static SpreadsheetToMarkdownConverter Offline() =>
        new(new DoclingClient(() => "http://127.0.0.1:1"));

    [Theory]
    [InlineData("holdings.xlsx")]
    [InlineData("holdings.xlsm")]
    [InlineData("template.xltx")]
    [InlineData("template.xltm")]
    [InlineData("legacy.xls")]
    [InlineData("legacy.xlt")]
    [InlineData("big.xlsb")]
    [InlineData("open.ods")]
    public void IsConvertible_IsTrue_ForSpreadsheetFormats(string fileName)
    {
        Assert.True(SpreadsheetToMarkdownConverter.IsConvertible(fileName));
    }

    [Theory]
    [InlineData("holdings.XLSX")]
    [InlineData("legacy.Xls")]
    public void IsConvertible_IgnoresCase(string fileName)
    {
        Assert.True(SpreadsheetToMarkdownConverter.IsConvertible(fileName));
    }

    [Theory]
    [InlineData("scan.pdf")]      // already uploadable as-is
    [InlineData("notes.txt")]     // already uploadable as-is
    [InlineData("data.csv")]      // plain text the Files API takes directly
    [InlineData("report.docx")]   // Word — handled by DocumentToPdfConverter
    [InlineData("deck.pptx")]
    [InlineData("chart.png")]
    [InlineData("noextension")]
    public void IsConvertible_IsFalse_ForEverythingElse(string fileName)
    {
        Assert.False(SpreadsheetToMarkdownConverter.IsConvertible(fileName));
    }

    /// <summary>Word and spreadsheet routing must not overlap, or the uploader's dispatch is ambiguous.</summary>
    [Theory]
    [InlineData("report.docx")]
    [InlineData("notes.rtf")]
    [InlineData("notes.odt")]
    [InlineData("holdings.xlsx")]
    [InlineData("open.ods")]
    public void ConverterFormats_DoNotOverlap(string fileName)
    {
        Assert.False(DocumentToPdfConverter.IsConvertible(fileName) &&
                     SpreadsheetToMarkdownConverter.IsConvertible(fileName));
    }

    [Fact]
    public async Task ConvertAsync_Fails_WhenFileDoesNotExist()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xlsx");

        var result = await Offline().ConvertAsync(missing);

        Assert.False(result.Success);
        Assert.Null(result.MarkdownPath);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task ConvertAsync_Fails_ForNonSpreadsheetFormat()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".docx");
        File.WriteAllText(tempFile, "not a spreadsheet");

        try
        {
            var result = await Offline().ConvertAsync(tempFile);

            Assert.False(result.Success);
            Assert.Contains("not a spreadsheet", result.Error);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>An unreachable sidecar must fail with an actionable message, not an unhandled exception.</summary>
    [Fact]
    public async Task ConvertAsync_Fails_WithGuidance_WhenDoclingIsUnreachable()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xlsx");
        File.WriteAllText(tempFile, "placeholder");

        try
        {
            var result = await Offline().ConvertAsync(tempFile);

            Assert.False(result.Success);
            Assert.Contains("Settings", result.Error);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task IsAvailableAsync_IsFalse_WhenNothingIsListening()
    {
        var client = new DoclingClient(() => "http://127.0.0.1:1");

        Assert.False(await client.IsAvailableAsync());
    }
}

public class SpreadsheetNormalizerTests
{
    /// <summary>
    /// Docling reads OOXML directly, so those files must pass straight through — no Excel
    /// round-trip, and therefore no dependency on Excel being installed.
    /// </summary>
    [Theory]
    [InlineData(".xlsx")]
    [InlineData(".xlsm")]
    [InlineData(".xltx")]
    [InlineData(".xltm")]
    public void Normalize_ReturnsTheSourceUnchanged_ForOoxml(string extension)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
        File.WriteAllText(tempFile, "placeholder");

        try
        {
            var result = SpreadsheetNormalizer.Normalize(tempFile);

            Assert.True(result.Success);
            Assert.Equal(tempFile, result.XlsxPath);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// The formats Docling rejects (.xls, .xlsb, .ods) must take the Excel path — either
    /// producing an .xlsx or failing with a message that names Excel.
    /// </summary>
    [Theory]
    [InlineData(".xls")]
    [InlineData(".xlsb")]
    [InlineData(".ods")]
    public void Normalize_RoutesLegacyFormatsThroughExcel(string extension)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
        File.WriteAllText(tempFile, "not a real workbook");

        try
        {
            var result = SpreadsheetNormalizer.Normalize(tempFile);

            if (result.Success)
            {
                // Whatever comes back must be an .xlsx rewrite, never the original —
                // handing the original to Docling is exactly what this step prevents.
                Assert.NotEqual(tempFile, result.XlsxPath);
                Assert.EndsWith(".xlsx", result.XlsxPath);
            }
            else
            {
                // A failure must name Excel, so the message points at the real dependency.
                Assert.Contains("Excel", result.Error);
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

public class DoclingEndpointSettingsTests
{
    [Fact]
    public void ResolveDoclingEndpoint_FallsBackToTheDefault_WhenUnset()
    {
        var settings = new AppSettings();

        Assert.Equal(AppSettings.DefaultDoclingEndpoint, settings.ResolveDoclingEndpoint());
    }

    [Theory]
    [InlineData("http://docker-host:5001/", "http://docker-host:5001")]
    [InlineData("  http://localhost:8080  ", "http://localhost:8080")]
    [InlineData("   ", AppSettings.DefaultDoclingEndpoint)]
    public void ResolveDoclingEndpoint_NormalisesTheConfiguredValue(string configured, string expected)
    {
        var settings = new AppSettings { DoclingEndpoint = configured };

        Assert.Equal(expected, settings.ResolveDoclingEndpoint());
    }
}
