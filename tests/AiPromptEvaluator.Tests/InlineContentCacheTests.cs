using AiPromptEvaluator;
using Xunit;

namespace AiPromptEvaluator.Tests;

public sealed class InlineContentCacheTests : IDisposable
{
    private readonly string _root;

    public InlineContentCacheTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "aipe-inline-tests", Guid.NewGuid().ToString("N"));
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

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Get_EncodesBase64_FromTheFileBytes()
    {
        var path = WriteFile("doc.txt", "client objectives");
        var cache = new InlineContentCache();

        var content = cache.Get(path, InlineEncoding.Base64);

        Assert.Equal(InlineEncoding.Base64, content.Encoding);
        Assert.Equal("client objectives", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(content.Data)));
    }

    [Fact]
    public void Get_ReturnsTheSameInstance_ForAnUnchangedFile()
    {
        var path = WriteFile("doc.txt", "client objectives");
        var cache = new InlineContentCache();

        Assert.Same(cache.Get(path, InlineEncoding.Base64), cache.Get(path, InlineEncoding.Base64));
        Assert.Equal(1, cache.Count);
    }

    /// <summary>
    /// The whole point of keying on write time and size: an edited document must be re-read,
    /// or a run would send bytes the user has already replaced.
    /// </summary>
    [Fact]
    public void Get_RereadsTheFile_AfterItChanges()
    {
        var path = WriteFile("doc.txt", "original");
        var cache = new InlineContentCache();

        var before = cache.Get(path, InlineEncoding.Text);
        File.WriteAllText(path, "the client has since changed their objectives");
        var after = cache.Get(path, InlineEncoding.Text);

        Assert.Equal("original", before.Data);
        Assert.Equal("the client has since changed their objectives", after.Data);
    }

    /// <summary>Text and base64 of the same file are different payloads, not one entry.</summary>
    [Fact]
    public void Get_KeepsEncodingsApart()
    {
        var path = WriteFile("doc.txt", "objectives");
        var cache = new InlineContentCache();

        var text = cache.Get(path, InlineEncoding.Text);
        var base64 = cache.Get(path, InlineEncoding.Base64);

        Assert.NotEqual(text.Data, base64.Data);
        Assert.Equal(2, cache.Count);
    }

    /// <summary>The same file reached by differently-cased paths is one document, not two.</summary>
    [Fact]
    public void Get_TreatsPathsCaseInsensitively()
    {
        var path = WriteFile("Statement.txt", "x");
        var cache = new InlineContentCache();

        cache.Get(path, InlineEncoding.Text);
        cache.Get(path.ToUpperInvariant(), InlineEncoding.Text);

        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Get_EvictsTheLeastRecentlyUsed_OnceOverBudget()
    {
        // Each payload is ~200 chars of UTF-16, so ~400 bytes; three will not fit in 900.
        var cache = new InlineContentCache(byteBudget: 900);
        var first = WriteFile("a.txt", new string('a', 200));
        var second = WriteFile("b.txt", new string('b', 200));
        var third = WriteFile("c.txt", new string('c', 200));

        cache.Get(first, InlineEncoding.Text);
        cache.Get(second, InlineEncoding.Text);
        cache.Get(first, InlineEncoding.Text);   // first is now the most recently used
        cache.Get(third, InlineEncoding.Text);

        Assert.Equal(2, cache.Count);
        Assert.True(cache.ApproximateBytes <= cache.ByteBudget);
    }

    /// <summary>
    /// A single document larger than the whole budget still has to be sendable — evicting it
    /// the moment it is stored would make it unreadable rather than merely uncached.
    /// </summary>
    [Fact]
    public void Get_KeepsAFileThatExceedsTheWholeBudget()
    {
        var cache = new InlineContentCache(byteBudget: 100);
        var path = WriteFile("large.txt", new string('x', 5000));

        var content = cache.Get(path, InlineEncoding.Text);

        Assert.Equal(5000, content.Data.Length);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Clear_ForgetsEverything()
    {
        var cache = new InlineContentCache();
        cache.Get(WriteFile("a.txt", "a"), InlineEncoding.Text);

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.ApproximateBytes);
    }

    /// <summary>A missing file has to surface, not be cached as an empty document.</summary>
    [Fact]
    public void Get_ThrowsForAMissingFile()
    {
        var cache = new InlineContentCache();

        Assert.ThrowsAny<IOException>(
            () => cache.Get(Path.Combine(_root, "not-there.txt"), InlineEncoding.Text));
    }

    /// <summary>The case load runs eight files at a time against one cache.</summary>
    [Fact]
    public void Get_IsSafeFromManyThreadsAtOnce()
    {
        var cache = new InlineContentCache();
        var paths = Enumerable.Range(0, 40)
            .Select(i => WriteFile($"concurrent{i}.txt", new string('x', 100)))
            .ToList();

        Parallel.ForEach(paths, new ParallelOptions { MaxDegreeOfParallelism = 8 },
            path => cache.Get(path, InlineEncoding.Base64));

        Assert.Equal(40, cache.Count);
    }
}
