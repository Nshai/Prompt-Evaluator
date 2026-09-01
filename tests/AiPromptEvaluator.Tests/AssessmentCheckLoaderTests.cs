using System.IO;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The checks CSV is chosen by the user at run time, and the workbook it comes from has
/// already changed shape once: the revised sheet drops Regulatory Basis and suffixes several
/// headers with "(revised)".
///
/// Read positionally, that file does not fail. It shifts — categories into RegulatoryBasis,
/// the prompt into PrimaryDocumentCategories, DecisionLogic empty — and every check then
/// assesses the wrong text with nothing in the output to say so. These tests pin the header
/// mapping that stops it.
/// </summary>
public class AssessmentCheckLoaderTests
{
    private static string Write(string name, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-{name}");
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>The revised sheet's shape: seven columns, no Regulatory Basis, "(revised)" suffixes.</summary>
    private const string RevisedCsv =
        "Check ID,Check Name,Applies To,Primary Document Categories (revised),Prompt (revised),What to Look For (revised),Decision Logic\r\n"
        + "\"Rules:\n1. Suitability report first perspective.\",,,,,,\r\n"
        + "CHK-001,Suitability completeness,All advised cases,\"Know Your Client (B)\nRecommendations & Advice (I)\",Are the facts consistent?,Personal details,No Issue: complete.\r\n";

    /// <summary>The original sheet: eight columns, Regulatory Basis at index 3.</summary>
    private const string OriginalCsv =
        "Check ID,Check Name,Applies To,Regulatory Basis,Primary Document Categories,Prompt,What to Look For,Decision Logic\r\n"
        + "CHK-001,Suitability completeness,All advised cases,COBS 9.2.1R,\"Know Your Client (B)\",Are the facts consistent?,Personal details,No Issue: complete.\r\n";

    [Fact]
    public void AMissingColumnComesBackEmptyRatherThanShiftingItsNeighbours()
    {
        var path = Write("revised.csv", RevisedCsv);

        var check = Assert.Single(AssessmentCheckLoader.Load(path));

        // The field the revised sheet does not carry.
        Assert.Equal(string.Empty, check.RegulatoryBasis);

        // Everything after it stays put instead of sliding up one.
        Assert.Contains("Know Your Client (B)", check.PrimaryDocumentCategories);
        Assert.Equal("Are the facts consistent?", check.Prompt);
        Assert.Equal("Personal details", check.WhatToLookFor);
        Assert.Equal("No Issue: complete.", check.DecisionLogic);
        Assert.Equal(["B", "I"], check.CategoryCodes);

        File.Delete(path);
    }

    [Fact]
    public void TheRulesRowIsNotAnEleventhCheck()
    {
        var path = Write("revised.csv", RevisedCsv);

        var checks = AssessmentCheckLoader.Load(path);

        Assert.Single(checks);
        Assert.StartsWith("CHK-", checks[0].CheckId);

        File.Delete(path);
    }

    [Fact]
    public void TheOriginalEightColumnSheetStillLoads()
    {
        var path = Write("original.csv", OriginalCsv);

        var check = Assert.Single(AssessmentCheckLoader.Load(path));

        Assert.Equal("COBS 9.2.1R", check.RegulatoryBasis);
        Assert.Equal("Are the facts consistent?", check.Prompt);
        Assert.Equal("No Issue: complete.", check.DecisionLogic);

        File.Delete(path);
    }

    /// <summary>
    /// Column order is not part of the contract any more. Reordering the sheet in Excel — which
    /// is how the file is edited — must not change what a check assesses.
    /// </summary>
    [Fact]
    public void ColumnOrderDoesNotMatter()
    {
        var path = Write("shuffled.csv",
            "Decision Logic,Prompt,Check ID,What to Look For,Check Name,Primary Document Categories,Applies To,Regulatory Basis\r\n"
            + "No Issue: complete.,Are the facts consistent?,CHK-001,Personal details,Suitability completeness,\"Know Your Client (B)\",All advised cases,COBS 9.2.1R\r\n");

        var check = Assert.Single(AssessmentCheckLoader.Load(path));

        Assert.Equal("CHK-001", check.CheckId);
        Assert.Equal("Suitability completeness", check.CheckName);
        Assert.Equal("COBS 9.2.1R", check.RegulatoryBasis);
        Assert.Equal("Are the facts consistent?", check.Prompt);
        Assert.Equal("Personal details", check.WhatToLookFor);
        Assert.Equal("No Issue: complete.", check.DecisionLogic);

        File.Delete(path);
    }

    /// <summary>
    /// A file with no recognisable header row is not necessarily broken — it may predate the
    /// header convention entirely. Positional reading is what it must have meant, so that is
    /// what it still gets.
    /// </summary>
    [Fact]
    public void AFileWithNoRecognisableHeaderFallsBackToPositional()
    {
        var path = Write("headerless.csv",
            "col0,col1,col2,col3,col4,col5,col6,col7\r\n"
            + "CHK-001,Name,Applies,Basis,Categories,Prompt,Look,Decision\r\n");

        var check = Assert.Single(AssessmentCheckLoader.Load(path));

        Assert.Equal("CHK-001", check.CheckId);
        Assert.Equal("Basis", check.RegulatoryBasis);
        Assert.Equal("Decision", check.DecisionLogic);

        File.Delete(path);
    }

    [Fact]
    public void AnEmptyFileLoadsNothingRatherThanThrowing()
    {
        var path = Write("empty.csv", string.Empty);

        Assert.Empty(AssessmentCheckLoader.Load(path));

        File.Delete(path);
    }

    /// <summary>
    /// The shipped revised sheet itself, read end to end. This is the file the plans were
    /// rebuilt against, so a regression here means the plans and the checks have diverged.
    /// </summary>
    [Theory]
    [InlineData("Revised checks.csv")]
    [InlineData("Revised checks (loadable).csv")]
    public void TheShippedRevisedSheetsLoadTenChecksWithTheirDecisionLogic(string fileName)
    {
        var path = Path.Combine(RepoRoot, "docs", "QA-Checks", fileName);

        Assert.True(File.Exists(path), $"Not found: {path}");

        var checks = AssessmentCheckLoader.Load(path);

        Assert.Equal(10, checks.Count);
        Assert.All(checks, c => Assert.StartsWith("CHK-", c.CheckId));
        Assert.All(checks, c => Assert.False(string.IsNullOrWhiteSpace(c.Prompt)));
        Assert.All(checks, c => Assert.False(string.IsNullOrWhiteSpace(c.DecisionLogic)));
        Assert.All(checks, c => Assert.NotEmpty(c.CategoryCodes));
    }

    /// <summary>
    /// Walks up from the test binary to the repository root, so the shipped-file tests do not
    /// depend on where the runner put the output.
    /// </summary>
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiPromptEvaluator.slnx")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? AppContext.BaseDirectory;
        }
    }
}
