using AiPromptEvaluator;
using Xunit;

namespace AiPromptEvaluator.Tests;

public sealed class UploadedFileStoreTests : IDisposable
{
    private readonly string _root;
    private readonly UploadedFileStore _store;

    public UploadedFileStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "aipe-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _store = new UploadedFileStore(Path.Combine(_root, "uploads.db"));
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

    private UploadedFile RecordFor(string path, string fileId = "file_abc") =>
        new(FilePath: path,
            CaseFolder: _root,
            CategoryCode: "B",
            CategoryName: "Know Your Client",
            FileId: fileId,
            Kind: AnthropicFileKind.Document,
            ConvertedTo: ConversionTarget.None,
            UploadedPath: null,
            SourceWriteUtc: File.GetLastWriteTimeUtc(path),
            SourceSize: new FileInfo(path).Length,
            UploadedUtc: DateTime.UtcNow);

    [Fact]
    public void Save_ThenGet_RoundTripsEveryField()
    {
        var path = WriteFile("report.docx");
        var record = RecordFor(path) with
        {
            Kind = AnthropicFileKind.Image,
            ConvertedTo = ConversionTarget.Pdf,
            UploadedPath = @"C:\cache\report.pdf",
        };

        _store.Save(record);
        var loaded = _store.Get(path);

        Assert.NotNull(loaded);
        Assert.Equal(record.FileId, loaded!.FileId);
        Assert.Equal("B", loaded.CategoryCode);
        Assert.Equal("Know Your Client", loaded.CategoryName);
        Assert.Equal(AnthropicFileKind.Image, loaded.Kind);
        Assert.Equal(ConversionTarget.Pdf, loaded.ConvertedTo);
        Assert.Equal(@"C:\cache\report.pdf", loaded.UploadedPath);
        Assert.Equal(record.SourceSize, loaded.SourceSize);
    }

    [Fact]
    public void Save_Twice_UpdatesRatherThanDuplicating()
    {
        var path = WriteFile("notes.txt");

        _store.Save(RecordFor(path, "file_first"));
        _store.Save(RecordFor(path, "file_second"));

        Assert.Equal("file_second", _store.Get(path)!.FileId);
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

    /// <summary>An edit changes the size, so the stale file_id must not be reused.</summary>
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
        Assert.Null(_store.GetIfCurrent(WriteFile("never-uploaded.txt")));
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
    public void GetForCaseFolder_ReturnsEveryFileIdForCleanup()
    {
        foreach (var name in new[] { "a.txt", "b.txt", "c.txt" })
        {
            var path = WriteFile(name);
            _store.Save(RecordFor(path, "file_" + name));
        }

        var ids = _store.GetForCaseFolder(_root).Select(f => f.FileId).ToList();

        Assert.Equal(3, ids.Count);
        Assert.Contains("file_a.txt", ids);
    }

    /// <summary>The database is the point — a new instance must see what the last one wrote.</summary>
    [Fact]
    public void Records_SurviveReopeningTheDatabase()
    {
        var databasePath = Path.Combine(_root, "reopened.db");
        var path = WriteFile("persisted.txt");

        new UploadedFileStore(databasePath).Save(RecordFor(path, "file_persisted"));

        Assert.Equal("file_persisted", new UploadedFileStore(databasePath).Get(path)!.FileId);
    }

    /// <summary>The load runs eight files at a time, so concurrent writes must not deadlock.</summary>
    [Fact]
    public void Save_IsSafeFromManyThreadsAtOnce()
    {
        var paths = Enumerable.Range(0, 40).Select(i => WriteFile($"concurrent{i}.txt")).ToList();

        Parallel.ForEach(paths, new ParallelOptions { MaxDegreeOfParallelism = 8 },
            path => _store.Save(RecordFor(path, "file_" + Path.GetFileName(path))));

        Assert.Equal(40, _store.GetForCaseFolder(_root).Count);
    }
}
