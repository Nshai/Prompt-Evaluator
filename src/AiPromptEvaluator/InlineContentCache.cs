using System.IO;

namespace AiPromptEvaluator;

/// <summary>How a file's bytes are carried in a content block.</summary>
public enum InlineEncoding
{
    /// <summary>Base64 of the raw bytes — PDFs and images.</summary>
    Base64,

    /// <summary>The file's text, decoded as UTF-8.</summary>
    Text,
}

/// <summary>A file rendered into the form a content block carries it in.</summary>
/// <param name="Data">Base64 or text, per <paramref name="Encoding"/>.</param>
public sealed record InlineContent(string Data, InlineEncoding Encoding)
{
    /// <summary>Roughly how much memory this payload occupies — .NET strings are UTF-16.</summary>
    public long ApproximateBytes => (long)Data.Length * sizeof(char);
}

/// <summary>
/// Keeps the encoded form of case-folder files in memory, keyed by path and the file's
/// last-write time and size, so a document attached to twenty checks is read and base64'd
/// once rather than twenty times.
///
/// Base64 encoding a case folder is not free — a 20 MB PDF costs ~27 MB of string plus the
/// read — and every check re-sends the same documents, so without this the same bytes are
/// re-encoded on every run. Keying on write time and size together means an edited file
/// misses and is re-read, including an in-place edit that preserved the timestamp.
///
/// The cache is bounded: once <see cref="ByteBudget"/> is exceeded, the least recently used
/// entries are dropped. Losing an entry costs a re-read, nothing more.
/// </summary>
public sealed class InlineContentCache
{
    /// <summary>
    /// How much encoded content to hold. Large enough for a typical case folder to stay
    /// resident across a run of checks, small enough not to crowd out a 32-bit-ish desktop
    /// process when someone points the app at an unusually large folder.
    /// </summary>
    public const long DefaultByteBudget = 512L * 1024 * 1024;

    private readonly object _gate = new();
    private readonly Dictionary<CacheKey, LinkedListNode<Entry>> _entries = new();

    /// <summary>Most recently used first, so eviction takes from the tail.</summary>
    private readonly LinkedList<Entry> _byRecency = new();

    private long _bytes;

    public InlineContentCache(long byteBudget = DefaultByteBudget)
    {
        ByteBudget = byteBudget > 0
            ? byteBudget
            : throw new ArgumentOutOfRangeException(nameof(byteBudget), "The budget must be positive.");
    }

    public long ByteBudget { get; }

    /// <summary>How much encoded content is currently held.</summary>
    public long ApproximateBytes
    {
        get { lock (_gate) { return _bytes; } }
    }

    public int Count
    {
        get { lock (_gate) { return _entries.Count; } }
    }

    /// <summary>
    /// The encoded form of <paramref name="filePath"/>, reading and encoding it on a miss.
    /// Throws whatever reading the file throws — a document that can't be read can't be sent.
    /// </summary>
    public InlineContent Get(string filePath, InlineEncoding encoding)
    {
        var info = new FileInfo(filePath);
        var key = new CacheKey(Path.GetFullPath(filePath), info.LastWriteTimeUtc, info.Length, encoding);

        if (TryTake(key, out var cached))
        {
            return cached;
        }

        var content = Read(filePath, encoding);

        // Two threads racing on the same file both encode it; the second store wins and the
        // first result is simply garbage. Cheaper than holding the lock across a file read.
        Store(key, content);
        return content;
    }

    /// <summary>Forgets everything held. Used when the case folder changes.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _byRecency.Clear();
            _bytes = 0;
        }
    }

    private static InlineContent Read(string filePath, InlineEncoding encoding) => encoding switch
    {
        InlineEncoding.Base64 => new InlineContent(
            Convert.ToBase64String(File.ReadAllBytes(filePath)), InlineEncoding.Base64),
        _ => new InlineContent(File.ReadAllText(filePath), InlineEncoding.Text),
    };

    private bool TryTake(CacheKey key, out InlineContent content)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var node))
            {
                content = null!;
                return false;
            }

            _byRecency.Remove(node);
            _byRecency.AddFirst(node);
            content = node.Value.Content;
            return true;
        }
    }

    private void Store(CacheKey key, InlineContent content)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                _bytes -= existing.Value.Content.ApproximateBytes;
                _byRecency.Remove(existing);
                _entries.Remove(key);
            }

            var node = _byRecency.AddFirst(new Entry(key, content));
            _entries[key] = node;
            _bytes += content.ApproximateBytes;

            // A single file larger than the whole budget would evict itself immediately, so
            // stop while it is still the only entry — it is about to be sent, and holding it
            // for this one request is the point.
            while (_bytes > ByteBudget && _entries.Count > 1)
            {
                var oldest = _byRecency.Last!;
                _byRecency.RemoveLast();
                _entries.Remove(oldest.Value.Key);
                _bytes -= oldest.Value.Content.ApproximateBytes;
            }
        }
    }

    /// <summary>Paths compare case-insensitively; Windows treats them that way.</summary>
    private readonly record struct CacheKey(string Path, DateTime WriteUtc, long Size, InlineEncoding Encoding)
    {
        public bool Equals(CacheKey other) =>
            string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase)
            && WriteUtc == other.WriteUtc
            && Size == other.Size
            && Encoding == other.Encoding;

        public override int GetHashCode() => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(Path), WriteUtc, Size, Encoding);
    }

    private sealed record Entry(CacheKey Key, InlineContent Content);
}
