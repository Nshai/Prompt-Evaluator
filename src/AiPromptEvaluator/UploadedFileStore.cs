using System.Data;
using System.IO;

using Microsoft.Data.Sqlite;

namespace AiPromptEvaluator;

/// <summary>One file that has been uploaded to the Files API, as recorded on disk.</summary>
public sealed record UploadedFile(
    string FilePath,
    string CaseFolder,
    string CategoryCode,
    string CategoryName,
    string FileId,
    AnthropicFileKind Kind,
    ConversionTarget ConvertedTo,
    string? UploadedPath,
    DateTime SourceWriteUtc,
    long SourceSize,
    DateTime UploadedUtc);

/// <summary>
/// Remembers which case-folder files have already been uploaded, and the file_id and
/// category each one maps to, in a SQLite database beside the app's settings.
///
/// The point is that uploading is the slow part of a load — conversion plus transfer runs
/// to tens of seconds for a real case folder. A file whose bytes haven't changed since it
/// was uploaded still has a valid file_id on Anthropic's side, so the whole step can be
/// skipped. Freshness is judged on last-write time and size together, so an edit in place
/// that preserves the timestamp is still caught.
/// </summary>
public sealed class UploadedFileStore
{
    private readonly string _connectionString;

    public UploadedFileStore(string? databasePath = null)
    {
        DatabasePath = databasePath ?? DefaultDatabasePath;

        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        Initialise();
    }

    public string DatabasePath { get; }

    public static string DefaultDatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AiPromptEvaluator",
        "uploads.db");

    private void Initialise()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();

        // NOCASE on the path columns because Windows paths are case-insensitive and the
        // same file can easily arrive with different casing.
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS uploaded_files (
                file_path        TEXT PRIMARY KEY COLLATE NOCASE,
                case_folder      TEXT NOT NULL COLLATE NOCASE,
                category_code    TEXT NOT NULL,
                category_name    TEXT NOT NULL,
                file_id          TEXT NOT NULL,
                kind             INTEGER NOT NULL,
                converted_to     INTEGER NOT NULL,
                uploaded_path    TEXT NULL,
                source_write_utc TEXT NOT NULL,
                source_size      INTEGER NOT NULL,
                uploaded_utc     TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_uploaded_files_case_folder
                ON uploaded_files (case_folder);
            """;

        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Serialised writers with a wait, rather than an immediate "database is locked"
        // failure — the parallel load has up to eight threads recording at once.
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    /// <summary>
    /// The recorded upload for <paramref name="filePath"/>, but only when the file on disk
    /// still matches what was uploaded. A changed file returns null so the caller re-uploads.
    /// </summary>
    public UploadedFile? GetIfCurrent(string filePath)
    {
        FileInfo info;
        try
        {
            info = new FileInfo(filePath);
            if (!info.Exists)
            {
                return null;
            }
        }
        catch
        {
            return null;
        }

        var record = Get(filePath);
        if (record is null)
        {
            return null;
        }

        // Sub-second precision survives the round-trip through the "o" format, but compare
        // on whole seconds anyway: some file systems and copy tools round the timestamp.
        var sameTime = Math.Abs((record.SourceWriteUtc - info.LastWriteTimeUtc).TotalSeconds) < 1;
        return sameTime && record.SourceSize == info.Length ? record : null;
    }

    public UploadedFile? Get(string filePath)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT * FROM uploaded_files WHERE file_path = $path;";
        command.Parameters.AddWithValue("$path", filePath);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public void Save(UploadedFile file)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO uploaded_files
                (file_path, case_folder, category_code, category_name, file_id, kind,
                 converted_to, uploaded_path, source_write_utc, source_size, uploaded_utc)
            VALUES
                ($path, $folder, $code, $name, $fileId, $kind,
                 $converted, $uploadedPath, $writeUtc, $size, $uploadedUtc)
            ON CONFLICT(file_path) DO UPDATE SET
                case_folder      = excluded.case_folder,
                category_code    = excluded.category_code,
                category_name    = excluded.category_name,
                file_id          = excluded.file_id,
                kind             = excluded.kind,
                converted_to     = excluded.converted_to,
                uploaded_path    = excluded.uploaded_path,
                source_write_utc = excluded.source_write_utc,
                source_size      = excluded.source_size,
                uploaded_utc     = excluded.uploaded_utc;
            """;

        command.Parameters.AddWithValue("$path", file.FilePath);
        command.Parameters.AddWithValue("$folder", file.CaseFolder);
        command.Parameters.AddWithValue("$code", file.CategoryCode);
        command.Parameters.AddWithValue("$name", file.CategoryName);
        command.Parameters.AddWithValue("$fileId", file.FileId);
        command.Parameters.AddWithValue("$kind", (int)file.Kind);
        command.Parameters.AddWithValue("$converted", (int)file.ConvertedTo);
        command.Parameters.AddWithValue("$uploadedPath", (object?)file.UploadedPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$writeUtc", file.SourceWriteUtc.ToString("o"));
        command.Parameters.AddWithValue("$size", file.SourceSize);
        command.Parameters.AddWithValue("$uploadedUtc", file.UploadedUtc.ToString("o"));

        command.ExecuteNonQuery();
    }

    /// <summary>Every upload recorded for a case folder, whether or not the file still matches.</summary>
    public IReadOnlyList<UploadedFile> GetForCaseFolder(string caseFolder)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT * FROM uploaded_files WHERE case_folder = $folder ORDER BY file_path;";
        command.Parameters.AddWithValue("$folder", caseFolder);

        var results = new List<UploadedFile>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(Read(reader));
        }

        return results;
    }

    public void Remove(string filePath)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();

        command.CommandText = "DELETE FROM uploaded_files WHERE file_path = $path;";
        command.Parameters.AddWithValue("$path", filePath);
        command.ExecuteNonQuery();
    }

    /// <summary>Forgets every upload for a case folder. Returns how many rows went.</summary>
    public int RemoveCaseFolder(string caseFolder)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();

        command.CommandText = "DELETE FROM uploaded_files WHERE case_folder = $folder;";
        command.Parameters.AddWithValue("$folder", caseFolder);
        return command.ExecuteNonQuery();
    }

    private static UploadedFile Read(IDataRecord row) => new(
        FilePath: row.GetString(row.GetOrdinal("file_path")),
        CaseFolder: row.GetString(row.GetOrdinal("case_folder")),
        CategoryCode: row.GetString(row.GetOrdinal("category_code")),
        CategoryName: row.GetString(row.GetOrdinal("category_name")),
        FileId: row.GetString(row.GetOrdinal("file_id")),
        Kind: (AnthropicFileKind)row.GetInt32(row.GetOrdinal("kind")),
        ConvertedTo: (ConversionTarget)row.GetInt32(row.GetOrdinal("converted_to")),
        UploadedPath: row.IsDBNull(row.GetOrdinal("uploaded_path"))
            ? null
            : row.GetString(row.GetOrdinal("uploaded_path")),
        SourceWriteUtc: DateTime.Parse(
            row.GetString(row.GetOrdinal("source_write_utc")), null,
            System.Globalization.DateTimeStyles.RoundtripKind),
        SourceSize: row.GetInt64(row.GetOrdinal("source_size")),
        UploadedUtc: DateTime.Parse(
            row.GetString(row.GetOrdinal("uploaded_utc")), null,
            System.Globalization.DateTimeStyles.RoundtripKind));
}
