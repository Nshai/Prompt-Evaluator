using AiPromptEvaluator;
using Xunit;

namespace AiPromptEvaluator.Tests;

public sealed class PreparedFileStoreTests : IDisposable
{
    private readonly string _root;
    private readonly PreparedFileStore _store;

    public PreparedFileStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "aipe-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _store = new PreparedFileStore(Path.Combine(_root, "uploads.db"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // SQLite may still hold the file; the temp folder is disposable either way.
        }
    }

    private string WriteFile(string name, string content = "hello")
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>A record for a file used as it stands — no conversion, so no separate output.</summary>
    private PreparedFile RecordFor(string path) =>
        new(FilePath: path,
            CaseFolder: _root,
            CategoryCode: "B",
            CategoryName: "Know Your Client",
            Kind: AnthropicFileKind.Document,
            ConvertedTo: ConversionTarget.None,
            PreparedPath: path,
            SourceWriteUtc: File.GetLastWriteTimeUtc(path),
            SourceSize: new FileInfo(path).Length,
            PreparedUtc: DateTime.UtcNow);

    [Fact]
    public void Save_ThenGet_RoundTripsEveryField()
    {
        var path = WriteFile("report.docx");
        var record = RecordFor(path) with
        {
            Kind = AnthropicFileKind.Image,
            ConvertedTo = ConversionTarget.Pdf,
            PreparedPath = @"C:\cache\report.pdf",
        };

        _store.Save(record);
        var loaded = _store.Get(path);

        Assert.NotNull(loaded);
        Assert.Equal("B", loaded!.CategoryCode);
        Assert.Equal("Know Your Client", loaded.CategoryName);
        Assert.Equal(AnthropicFileKind.Image, loaded.Kind);
        Assert.Equal(ConversionTarget.Pdf, loaded.ConvertedTo);
        Assert.Equal(@"C:\cache\report.pdf", loaded.PreparedPath);
        Assert.Equal(record.SourceSize, loaded.SourceSize);
    }

    [Fact]
    public void Save_Twice_UpdatesRatherThanDuplicating()
    {
        var path = WriteFile("notes.txt");

        _store.Save(RecordFor(path) with { CategoryCode = "A" });
        _store.Save(RecordFor(path) with { CategoryCode = "C" });

        Assert.Equal("C", _store.Get(path)!.CategoryCode);
        Assert.Single(_store.GetForCaseFolder(_root));
    }

    /// <summary>Windows paths are case-insensitive; the same file must not get two rows.</summary>
    [Fact]
    public void Get_IsCaseInsensitiveOnPath()
    {
        var path = WriteFile("Statement.pdf");
        _store.Save(RecordFor(path));

        Assert.NotNull(_store.Get(path.ToUpperInvariant()));
        Assert.NotNull(_store.Get(path.ToLowerInvariant()));
    }

    [Fact]
    public void GetIfCurrent_ReturnsTheRecord_WhenTheFileIsUnchanged()
    {
        var path = WriteFile("unchanged.txt");
        _store.Save(RecordFor(path));

        Assert.NotNull(_store.GetIfCurrent(path));
    }

    /// <summary>An edit changes the size, so the stale conversion must not be reused.</summary>
    [Fact]
    public void GetIfCurrent_ReturnsNull_WhenTheFileChanged()
    {
        var path = WriteFile("edited.txt", "original");
        _store.Save(RecordFor(path));

        File.WriteAllText(path, "the client has since changed their objectives");

        Assert.Null(_store.GetIfCurrent(path));
        Assert.NotNull(_store.Get(path));  // the row is still there, just not current
    }

    [Fact]
    public void GetIfCurrent_ReturnsNull_WhenTheFileIsGone()
    {
        var path = WriteFile("deleted.txt");
        _store.Save(RecordFor(path));
        File.Delete(path);

        Assert.Null(_store.GetIfCurrent(path));
    }

    [Fact]
    public void GetIfCurrent_ReturnsNull_WhenNothingWasEverRecorded()
    {
        Assert.Null(_store.GetIfCurrent(WriteFile("never-prepared.txt")));
    }

    /// <summary>
    /// Converted output lives in the temp folder, which Windows clears out. A record pointing
    /// at output that is no longer there has to re-convert, not hand back a missing path.
    /// </summary>
    [Fact]
    public void GetIfCurrent_ReturnsNull_WhenTheConvertedOutputIsGone()
    {
        var source = WriteFile("holdings.xlsx");
        var converted = WriteFile("holdings.md");

        _store.Save(RecordFor(source) with
        {
            ConvertedTo = ConversionTarget.Markdown,
            PreparedPath = converted,
        });

        Assert.NotNull(_store.GetIfCurrent(source));

        File.Delete(converted);

        Assert.Null(_store.GetIfCurrent(source));
    }

    [Fact]
    public void RemoveCaseFolder_ClearsOnlyThatFolder()
    {
        var mine = WriteFile("mine.txt");
        _store.Save(RecordFor(mine));

        var otherFolder = Path.Combine(_root, "other");
        Directory.CreateDirectory(otherFolder);
        var theirs = Path.Combine(otherFolder, "theirs.txt");
        File.WriteAllText(theirs, "x");
        _store.Save(RecordFor(theirs) with { CaseFolder = otherFolder });

        var removed = _store.RemoveCaseFolder(_root);

        Assert.Equal(1, removed);
        Assert.Null(_store.Get(mine));
        Assert.NotNull(_store.Get(theirs));
    }

    [Fact]
    public void GetForCaseFolder_ReturnsEveryRecordedFile()
    {
        foreach (var name in new[] { "a.txt", "b.txt", "c.txt" })
        {
            _store.Save(RecordFor(WriteFile(name)));
        }

        var paths = _store.GetForCaseFolder(_root).Select(f => Path.GetFileName(f.FilePath)).ToList();

        Assert.Equal(3, paths.Count);
        Assert.Contains("a.txt", paths);
    }

    /// <summary>The database is the point — a new instance must see what the last one wrote.</summary>
    [Fact]
    public void Records_SurviveReopeningTheDatabase()
    {
        var databasePath = Path.Combine(_root, "reopened.db");
        var path = WriteFile("persisted.txt");

        new PreparedFileStore(databasePath).Save(RecordFor(path) with { CategoryCode = "G" });

        Assert.Equal("G", new PreparedFileStore(databasePath).Get(path)!.CategoryCode);
    }

    /// <summary>The load runs eight files at a time, so concurrent writes must not deadlock.</summary>
    [Fact]
    public void Save_IsSafeFromManyThreadsAtOnce()
    {
        var paths = Enumerable.Range(0, 40).Select(i => WriteFile($"concurrent{i}.txt")).ToList();

        Parallel.ForEach(paths, new ParallelOptions { MaxDegreeOfParallelism = 8 },
            path => _store.Save(RecordFor(path)));

        Assert.Equal(40, _store.GetForCaseFolder(_root).Count);
    }
}
