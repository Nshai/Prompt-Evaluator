using AiPromptEvaluator;
using Xunit;

namespace AiPromptEvaluator.Tests;

public class DocumentContextBuilderTests
{
    [Fact]
    public void BuildContext_ReturnsEmpty_WhenFolderDoesNotExist()
    {
        var result = DocumentContextBuilder.BuildContext(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildContext_IncludesFileContent_WhenFolderContainsTextFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "sample.txt");
        File.WriteAllText(filePath, "hello world");

        try
        {
            var result = DocumentContextBuilder.BuildContext(tempDir);

            Assert.Contains("hello world", result);
            Assert.Contains("sample.txt", result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
