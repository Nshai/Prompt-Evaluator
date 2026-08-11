using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

namespace AiPromptEvaluator;

/// <summary>A single case-folder document and — once uploaded — its Files API file_id.</summary>
public sealed record CaseDocument(
    string FilePath,
    string FileName,
    string CategoryCode,
    string CategoryName,
    AnthropicFileKind Kind,
    string? FileId,
    ConversionTarget ConvertedTo = ConversionTarget.None,
    string? Error = null,
    string? UploadedPath = null,
    bool ReusedFromCache = false)
{
    /// <summary>
    /// The bytes that were actually sent to the Files API — the converted PDF or Markdown
    /// for converted documents, otherwise the source file. Token counting reads this,
    /// because it is what the model is billed for.
    /// </summary>
    public string BilledPath => UploadedPath ?? FilePath;

    /// <summary>
    /// A spreadsheet we could not turn into Markdown. Its bytes are meaningless as inline
    /// text, so it is left out of requests entirely rather than sent as noise.
    /// </summary>
    public bool IsExcludedSpreadsheet =>
        FileId is null && SpreadsheetToMarkdownConverter.IsConvertible(FilePath);

    /// <summary>True when this document can be attached to a check.</summary>
    public bool IsSendable => !IsExcludedSpreadsheet;
}

/// <summary>One completed file in a case-folder load, reported as it finishes.</summary>
public sealed record CaseLoadProgress(int Done, int Total, CaseDocument Document, TimeSpan Elapsed);

/// <summary>
/// Uploads every document under a case folder (including sub-folders) to the Anthropic
/// Files API once, and keeps the returned file_id keyed by path and grouped by the
/// document category taken from the first sub-folder under the case folder.
/// </summary>
public sealed class CaseDocumentLibrary
{
    /// <summary>
    /// How many files are uploaded at once. High enough to hide per-file latency on a case
    /// folder of a few hundred documents, low enough to stay clear of Files API rate limits.
    /// </summary>
    private const int MaxConcurrentUploads = 8;

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

    /// <summary>Documents that carry a file_id (PDF/text/image), reused or freshly uploaded.</summary>
    public int UploadedCount => _byPath.Values.Count(d => d.FileId is not null);

    /// <summary>Documents whose file_id came from the SQLite cache instead of a new upload.</summary>
    public int ReusedCount => _byPath.Values.Count(d => d.ReusedFromCache);

    /// <summary>Documents actually sent to the Files API during this load.</summary>
    public int FreshlyUploadedCount =>
        _byPath.Values.Count(d => d.FileId is not null && !d.ReusedFromCache);

    /// <summary>Documents the Files API can't take directly (.zip, ...), sent as inline text.</summary>
    public int UnsupportedCount => _byPath.Values.Count(d => d.FileId is null && d.IsSendable);

    /// <summary>Spreadsheets left out of requests because they could not be converted.</summary>
    public IReadOnlyList<CaseDocument> ExcludedSpreadsheets =>
        _byPath.Values.Where(d => d.IsExcludedSpreadsheet).ToList();

    /// <summary>How many documents were converted to each target before upload.</summary>
    public int ConvertedCount(ConversionTarget target) =>
        _byPath.Values.Count(d => d.ConvertedTo == target);

    /// <summary>Documents that failed to convert or upload, with the reason.</summary>
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

    /// <summary>The file_id → category map, for callers that want to inspect or log it.</summary>
    public IReadOnlyDictionary<string, string> FileIdToCategory() =>
        _byPath.Values
            .Where(d => d.FileId is not null)
            .ToDictionary(d => d.FileId!, d => d.CategoryName, StringComparer.Ordinal);

    public IReadOnlyList<IGrouping<string, CaseDocument>> ByCategory() =>
        _byPath.Values
            .GroupBy(d => d.CategoryName)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Walks <paramref name="caseFolder"/> recursively and uploads every supported file,
    /// recording the category for each. Uploads run concurrently; <paramref name="progress"/>
    /// is raised once per file as it completes, so order reflects completion, not the folder.
    ///
    /// A file that fails is recorded with its error rather than aborting the load — one bad
    /// document shouldn't cost the user every other upload in the batch.
    ///
    /// When <paramref name="store"/> is supplied, a file that has already been uploaded and
    /// hasn't changed since is taken straight from it: no conversion, no transfer, no new
    /// file_id. Use "Clear Uploads" to discard those records and force a fresh upload.
    /// </summary>
    public static async Task<CaseDocumentLibrary> LoadAsync(
        string caseFolder,
        AnthropicFileUploader uploader,
        IProgress<CaseLoadProgress>? progress = null,
        UploadedFileStore? store = null,
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
                MaxDegreeOfParallelism = MaxConcurrentUploads,
                CancellationToken = cancellationToken,
            },
            async (file, token) =>
            {
                var startedAt = Stopwatch.GetTimestamp();
                var document = await LoadOneAsync(caseFolder, file, uploader, store, token).ConfigureAwait(false);

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
        AnthropicFileUploader uploader,
        UploadedFileStore? store,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(file);
        var code = CategoryCodeFor(caseFolder, file);

        try
        {
            // An unchanged file still has a valid file_id on Anthropic's side, so skip the
            // whole conversion-and-upload step and reuse what we recorded last time.
            var cached = store?.GetIfCurrent(file);
            if (cached is not null)
            {
                return new CaseDocument(
                    FilePath: file,
                    FileName: fileName,
                    CategoryCode: code,
                    CategoryName: ResolveCategoryName(code),
                    Kind: cached.Kind,
                    FileId: cached.FileId,
                    ConvertedTo: cached.ConvertedTo,
                    UploadedPath: cached.UploadedPath,
                    ReusedFromCache: true);
            }

            var fileRef = await uploader.GetOrUploadAsync(file, cancellationToken).ConfigureAwait(false);

            if (store is not null && fileRef.FileId is not null)
            {
                RecordUpload(store, caseFolder, file, code, fileRef);
            }

            return new CaseDocument(
                FilePath: file,
                FileName: fileName,
                CategoryCode: code,
                CategoryName: ResolveCategoryName(code),
                Kind: fileRef.Kind,
                FileId: fileRef.FileId,
                ConvertedTo: fileRef.ConvertedTo,
                Error: fileRef.ConversionError,
                UploadedPath: fileRef.UploadedPath);
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
                FileId: null,
                Error: ex.Message.Trim());
        }
    }

    /// <summary>
    /// Records a fresh upload so the next load can skip it. A store failure must not fail the
    /// load — the file is uploaded either way, we just won't be able to reuse it next time.
    /// </summary>
    private static void RecordUpload(
        UploadedFileStore store, string caseFolder, string file, string code, AnthropicFileRef fileRef)
    {
        try
        {
            var info = new FileInfo(file);

            store.Save(new UploadedFile(
                FilePath: file,
                CaseFolder: caseFolder,
                CategoryCode: code,
                CategoryName: ResolveCategoryName(code),
                FileId: fileRef.FileId!,
                Kind: fileRef.Kind,
                ConvertedTo: fileRef.ConvertedTo,
                UploadedPath: fileRef.UploadedPath,
                SourceWriteUtc: info.LastWriteTimeUtc,
                SourceSize: info.Length,
                UploadedUtc: DateTime.UtcNow));
        }
        catch
        {
            // Caching is an optimisation; losing a row costs a re-upload, nothing more.
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
