using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

namespace AiPromptEvaluator;

/// <summary>A single case-folder document, in the form it will be sent in.</summary>
public sealed record CaseDocument(
    string FilePath,
    string FileName,
    string CategoryCode,
    string CategoryName,
    AnthropicFileKind Kind,
    string? PreparedPath,
    ConversionTarget ConvertedTo = ConversionTarget.None,
    string? Error = null,
    bool ReusedFromCache = false)
{
    /// <summary>
    /// The bytes that actually travel with a request — the converted PDF or Markdown for
    /// converted documents, otherwise the source file. Both the content block and the token
    /// count read this, because it is what the model is billed for.
    /// </summary>
    public string BilledPath => PreparedPath ?? FilePath;

    /// <summary>
    /// A spreadsheet we could not turn into Markdown. Its bytes are meaningless as inline
    /// text, so it is left out of requests entirely rather than sent as noise.
    /// </summary>
    public bool IsExcludedSpreadsheet =>
        PreparedPath is null && SpreadsheetToMarkdownConverter.IsConvertible(FilePath);

    /// <summary>True when this document can be attached to a check.</summary>
    public bool IsSendable => !IsExcludedSpreadsheet;
}

/// <summary>One completed file in a case-folder load, reported as it finishes.</summary>
public sealed record CaseLoadProgress(int Done, int Total, CaseDocument Document, TimeSpan Elapsed);

/// <summary>
/// Converts every document under a case folder (including sub-folders) into sendable form
/// once, keyed by path and grouped by the document category taken from the first sub-folder
/// under the case folder. Runs then attach those documents inline.
/// </summary>
public sealed class CaseDocumentLibrary
{
    /// <summary>
    /// How many files are converted at once. High enough to hide per-file latency on a case
    /// folder of a few hundred documents, low enough not to swamp Word or the Docling sidecar.
    /// </summary>
    private const int MaxConcurrentConversions = 8;

    private readonly ConcurrentDictionary<string, CaseDocument> _byPath = new(StringComparer.OrdinalIgnoreCase);

    private CaseDocumentLibrary(string caseFolder)
    {
        CaseFolder = caseFolder;
    }

    /// <summary>The folder this library was built from — compare before reusing it.</summary>
    public string CaseFolder { get; }

    public IReadOnlyCollection<CaseDocument> Documents => _byPath.Values.ToList();

    /// <summary>Documents that can be attached to a check.</summary>
    public IReadOnlyList<CaseDocument> SendableDocuments =>
        _byPath.Values.Where(d => d.IsSendable).ToList();

    /// <summary>Documents in sendable form, whether converted or used as they stand.</summary>
    public int PreparedCount => _byPath.Values.Count(d => d.PreparedPath is not null);

    /// <summary>Documents whose conversion came from the SQLite cache instead of being redone.</summary>
    public int ReusedCount => _byPath.Values.Count(d => d.ReusedFromCache);

    /// <summary>Documents actually converted during this load.</summary>
    public int FreshlyPreparedCount =>
        _byPath.Values.Count(d => d.PreparedPath is not null && !d.ReusedFromCache);

    /// <summary>Documents no block type takes directly (.zip, ...), sent as a text preview.</summary>
    public int UnsupportedCount =>
        _byPath.Values.Count(d => d.Kind == AnthropicFileKind.Unsupported && d.IsSendable);

    /// <summary>Spreadsheets left out of requests because they could not be converted.</summary>
    public IReadOnlyList<CaseDocument> ExcludedSpreadsheets =>
        _byPath.Values.Where(d => d.IsExcludedSpreadsheet).ToList();

    /// <summary>How many documents were rendered to each target.</summary>
    public int ConvertedCount(ConversionTarget target) =>
        _byPath.Values.Count(d => d.ConvertedTo == target);

    /// <summary>Documents that failed to convert, with the reason.</summary>
    public IReadOnlyList<CaseDocument> Failures =>
        _byPath.Values.Where(d => d.Error is not null).ToList();

    public bool TryGet(string filePath, out CaseDocument document) =>
        _byPath.TryGetValue(filePath, out document!);

    /// <summary>Returns true when two paths point at the same folder after full-path normalization.</summary>
    public static bool AreSameFolder(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            var leftPath = NormalizeFolderPath(left);
            var rightPath = NormalizeFolderPath(right);
            return string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string NormalizeFolderPath(string path)
    {
        var fullPath = Path.GetFullPath(path.Trim());
        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(root) && string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public IReadOnlyList<IGrouping<string, CaseDocument>> ByCategory() =>
        _byPath.Values
            .GroupBy(d => d.CategoryName)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Walks <paramref name="caseFolder"/> recursively and gets every file into sendable form,
    /// recording the category for each. Conversions run concurrently; <paramref name="progress"/>
    /// is raised once per file as it completes, so order reflects completion, not the folder.
    ///
    /// A file that fails is recorded with its error rather than aborting the load — one bad
    /// document shouldn't cost the user every other conversion in the batch.
    ///
    /// When <paramref name="store"/> is supplied, a file that was converted before and hasn't
    /// changed since is taken straight from it, so Word and Docling are not driven again. Use
    /// "Clear Cache" to discard those records and force a fresh conversion.
    /// </summary>
    public static async Task<CaseDocumentLibrary> LoadAsync(
        string caseFolder,
        CaseFilePreparer preparer,
        IProgress<CaseLoadProgress>? progress = null,
        PreparedFileStore? store = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(caseFolder))
        {
            throw new DirectoryNotFoundException($"Case folder not found: {caseFolder}");
        }

        var library = new CaseDocumentLibrary(caseFolder);

        var files = Directory.GetFiles(caseFolder, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                     && !f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var done = 0;

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrentConversions,
                CancellationToken = cancellationToken,
            },
            async (file, token) =>
            {
                var startedAt = Stopwatch.GetTimestamp();
                var document = await LoadOneAsync(caseFolder, file, preparer, store, token).ConfigureAwait(false);

                library._byPath[file] = document;

                var completed = Interlocked.Increment(ref done);
                progress?.Report(new CaseLoadProgress(
                    completed, files.Count, document, Stopwatch.GetElapsedTime(startedAt)));
            }).ConfigureAwait(false);

        return library;
    }

    private static async Task<CaseDocument> LoadOneAsync(
        string caseFolder,
        string file,
        CaseFilePreparer preparer,
        PreparedFileStore? store,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(file);
        var code = CategoryCodeFor(caseFolder, file);

        try
        {
            // An unchanged file converts to the same output, and the store only hands back a
            // record whose output is still on disk — so skip the conversion entirely.
            var cached = store?.GetIfCurrent(file);
            if (cached is not null)
            {
                return new CaseDocument(
                    FilePath: file,
                    FileName: fileName,
                    CategoryCode: code,
                    CategoryName: ResolveCategoryName(code),
                    Kind: cached.Kind,
                    PreparedPath: cached.PreparedPath,
                    ConvertedTo: cached.ConvertedTo,
                    ReusedFromCache: true);
            }

            var fileRef = await preparer.PrepareAsync(file, cancellationToken).ConfigureAwait(false);

            if (store is not null && fileRef.PreparedPath is not null)
            {
                RecordPreparation(store, caseFolder, file, code, fileRef);
            }

            return new CaseDocument(
                FilePath: file,
                FileName: fileName,
                CategoryCode: code,
                CategoryName: ResolveCategoryName(code),
                Kind: fileRef.Kind,
                PreparedPath: fileRef.PreparedPath,
                ConvertedTo: fileRef.ConvertedTo,
                Error: fileRef.ConversionError);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new CaseDocument(
                FilePath: file,
                FileName: fileName,
                CategoryCode: code,
                CategoryName: ResolveCategoryName(code),
                Kind: AnthropicFileKind.Unsupported,
                PreparedPath: null,
                Error: ex.Message.Trim());
        }
    }

    /// <summary>
    /// Records a fresh conversion so the next load can skip it. A store failure must not fail
    /// the load — the file is converted either way, we just re-convert it next time.
    /// </summary>
    private static void RecordPreparation(
        PreparedFileStore store, string caseFolder, string file, string code, PreparedFileRef fileRef)
    {
        try
        {
            var info = new FileInfo(file);

            store.Save(new PreparedFile(
                FilePath: file,
                CaseFolder: caseFolder,
                CategoryCode: code,
                CategoryName: ResolveCategoryName(code),
                Kind: fileRef.Kind,
                ConvertedTo: fileRef.ConvertedTo,
                PreparedPath: fileRef.PreparedPath,
                SourceWriteUtc: info.LastWriteTimeUtc,
                SourceSize: info.Length,
                PreparedUtc: DateTime.UtcNow));
        }
        catch
        {
            // Caching is an optimisation; losing a row costs a re-conversion, nothing more.
        }
    }

    /// <summary>The first path segment below the case folder, e.g. "A" for caseFolder/A/x.pdf.</summary>
    private static string CategoryCodeFor(string caseFolder, string filePath)
    {
        var relative = Path.GetRelativePath(caseFolder, filePath);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Length > 1 ? segments[0] : string.Empty;
    }

    public static string ResolveCategoryName(string code) => code switch
    {
        "A" => "Client Authority",
        "B" => "Know Your Client",
        "C" => "Meetings & Communications",
        "D" => "Risk, Capacity & Experience",
        "E" => "Existing Plans & Portfolios",
        "F" => "Needs Analysis",
        "G" => "Research",
        "H" => "Solution Design",
        "I" => "Recommendations & Advice",
        "" => DocumentCategory.Uncategorised,
        _ => code,
    };
}
