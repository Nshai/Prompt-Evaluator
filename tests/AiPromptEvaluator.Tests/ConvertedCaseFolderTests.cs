using AiPromptEvaluator;

using Microsoft.Extensions.DataIngestion;

using Xunit;
using Xunit.Abstractions;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Reads the converted case documents checked into the repository. They are the real thing —
/// entities, tables, headings and all — and the reason the reader needs to tolerate what a
/// converter actually emits rather than what clean Markdown looks like.
///
/// Skipped when the folder isn't present, so the suite doesn't depend on sample data.
/// </summary>
public class ConvertedCaseFolderTests
{
    private readonly ITestOutputHelper _output;

    public ConvertedCaseFolderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task EveryConvertedDocument_Parses()
    {
        var folder = LocateCaseFolder();
        if (folder is null)
        {
            _output.WriteLine("Skipped: no converted case folder in the repository.");
            return;
        }

        var files = Directory
            .GetFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(CaseDocumentIndexer.IsIndexable)
            .ToList();

        Assert.NotEmpty(files);

        var reader = new MarkdownReader();
        var failures = new List<string>();
        var empty = new List<string>();

        foreach (var file in files)
        {
            try
            {
                var document = await CaseDocumentIndexer.ReadDocumentAsync(reader, file);
                if (!document.EnumerateContent().Any())
                {
                    empty.Add(Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(file)} — {ex.Message}");
            }
        }

        _output.WriteLine($"{files.Count - failures.Count}/{files.Count} documents parsed.");

        Assert.Empty(failures);

        // A document that parses to nothing indexes to nothing — just as useless as a failure,
        // and quieter about it.
        Assert.Empty(empty);
    }

    /// <summary>Walks up from the test binary to the repository's sample case folder.</summary>
    private static string? LocateCaseFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, "docs", "QA-Checks", "Feasability-test-case-converted");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
