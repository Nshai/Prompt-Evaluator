using AiPromptEvaluator;
using Anthropic.Models.Messages;
using Xunit;

namespace AiPromptEvaluator.Tests;

public sealed class InlineDocumentBlocksTests : IDisposable
{
    private readonly string _root;
    private readonly InlineContentCache _cache = new();

    public InlineDocumentBlocksTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "aipe-blocks-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // The temp folder is disposable either way.
        }
    }

    private CaseDocument Doc(string name, byte[] bytes, string category = "B")
    {
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, bytes);

        return new CaseDocument(
            FilePath: path,
            FileName: name,
            CategoryCode: category,
            CategoryName: CaseDocumentLibrary.ResolveCategoryName(category),
            Kind: CaseFilePreparer.Classify(path),
            PreparedPath: path);
    }

    private CaseDocument TextDoc(string name, string content = "client objectives", string category = "B") =>
        Doc(name, System.Text.Encoding.UTF8.GetBytes(content), category);

    [Fact]
    public void BlockFor_SendsAPdfAsBase64()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };  // "%PDF-"
        var block = InlineDocumentBlocks.BlockFor(Doc("statement.pdf", pdf), _cache);

        var document = Assert.IsType<DocumentBlockParam>(block.Value);
        var source = Assert.IsType<Base64PdfSource>(document.Source.Value);
        Assert.Equal(Convert.ToBase64String(pdf), source.Data);
        Assert.Equal("[B] statement.pdf", document.Title);
    }

    [Fact]
    public void BlockFor_SendsTextAsPlainText()
    {
        var block = InlineDocumentBlocks.BlockFor(TextDoc("notes.txt", "the client is retiring"), _cache);

        var document = Assert.IsType<DocumentBlockParam>(block.Value);
        var source = Assert.IsType<PlainTextSource>(document.Source.Value);
        Assert.Equal("the client is retiring", source.Data);
    }

    [Fact]
    public void BlockFor_SendsAnImageAsBase64_WithItsMediaType()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var block = InlineDocumentBlocks.BlockFor(Doc("scan.png", bytes), _cache);

        var image = Assert.IsType<ImageBlockParam>(block.Value);
        var source = Assert.IsType<Base64ImageSource>(image.Source.Value);
        Assert.Equal(Convert.ToBase64String(bytes), source.Data);
        Assert.Equal(MediaType.ImagePng, source.MediaType.Value());
    }

    /// <summary>A conversion failure is reported in place rather than sent as binary noise.</summary>
    [Fact]
    public void BlockFor_ReportsAFailedConversion_AsText()
    {
        var doc = TextDoc("report.docx") with
        {
            PreparedPath = null,
            Error = "Word is not installed",
        };

        var text = Assert.IsType<TextBlockParam>(InlineDocumentBlocks.BlockFor(doc, _cache).Value);

        Assert.Contains("could not be converted", text.Text);
        Assert.Contains("Word is not installed", text.Text);
    }

    /// <summary>One unreadable file must not cost the check every other document.</summary>
    [Fact]
    public void BlockFor_ReportsAnUnreadableFile_RatherThanThrowing()
    {
        var doc = TextDoc("gone.txt");
        File.Delete(doc.FilePath);

        var text = Assert.IsType<TextBlockParam>(InlineDocumentBlocks.BlockFor(doc, _cache).Value);

        Assert.Contains("could not be read", text.Text);
    }

    [Fact]
    public void Build_MarksTheLastDocumentBlockForPromptCaching()
    {
        var blocks = InlineDocumentBlocks.Build(
            [TextDoc("a.txt"), TextDoc("b.txt")], _cache);

        var last = Assert.IsType<DocumentBlockParam>(blocks[^1].Value);
        Assert.NotNull(last.CacheControl);

        // Only the last one: the API allows four breakpoints per request in total.
        Assert.Single(blocks.Where(HasCacheControl));
    }

    [Fact]
    public void Build_LeavesBlocksUnmarked_WhenCachingIsOff()
    {
        var blocks = InlineDocumentBlocks.Build(
            [TextDoc("a.txt")], _cache, markForPromptCaching: false);

        Assert.DoesNotContain(blocks, HasCacheControl);
    }

    /// <summary>Documents are grouped under a category heading so the model can cite by category.</summary>
    [Fact]
    public void Build_GroupsByCategory()
    {
        var blocks = InlineDocumentBlocks.Build(
            [TextDoc("kyc.txt", category: "B"), TextDoc("authority.txt", category: "A")], _cache);

        var headings = blocks
            .Select(b => b.Value)
            .OfType<TextBlockParam>()
            .Select(t => t.Text)
            .Where(t => t.StartsWith("####", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, headings.Count);
        Assert.StartsWith("#### [A]", headings[0]);
        Assert.StartsWith("#### [B]", headings[1]);
    }

    /// <summary>A spreadsheet that never converted carries no meaning; it is dropped entirely.</summary>
    [Fact]
    public void Build_LeavesOutExcludedSpreadsheets()
    {
        var excluded = TextDoc("holdings.xlsx") with { PreparedPath = null, Error = "Docling is not responding" };

        var blocks = InlineDocumentBlocks.Build([excluded], _cache);

        Assert.Single(blocks);
        Assert.Contains("No documents are available", Assert.IsType<TextBlockParam>(blocks[0].Value).Text);
    }

    private static bool HasCacheControl(ContentBlockParam block) => block.Value switch
    {
        DocumentBlockParam document => document.CacheControl is not null,
        ImageBlockParam image => image.CacheControl is not null,
        TextBlockParam text => text.CacheControl is not null,
        _ => false,
    };
}
