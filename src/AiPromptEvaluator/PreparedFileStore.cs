using System.Data;
using System.IO;

using Microsoft.Data.Sqlite;

namespace AiPromptEvaluator;

/// <summary>One case-folder file that has been converted into sendable form, as recorded on disk.</summary>
public sealed record PreparedFile(
    string FilePath,
    string CaseFolder,
    string CategoryCode,
    string CategoryName,
    AnthropicFileKind Kind,
    ConversionTarget ConvertedTo,
    string? PreparedPath,
    DateTime SourceWriteUtc,
    long SourceSize,
    DateTime PreparedUtc);

/// <summary>
/// Remembers which case-folder files have already been converted, and where the converted
/// output landed, in a SQLite database beside the app's settings.
///
/// Since documents are sent inline there is nothing to upload, but conversion is still the
/// slow part of a load — driving Word for a .docx or Docling for a spreadsheet runs to
/// seconds per file. A file whose bytes haven't changed converts to the same output, so the
/// whole step can be skipped as long as that output is still on disk. Freshness is judged on
/// last-write time and size together, so an edit in place that preserved the timestamp is
/// still caught.
/// </summary>
public sealed class PreparedFileStore
{
    private readonly string _connectionString;

    public PreparedFileStore(string? databasePath = null)
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
        //
        // A pre-inline database also carries an `uploaded_files` table of Files API file_ids.
        // It is left alone deliberately: nothing reads it now, but dropping it would discard
        // the only local record of files that still exist on Anthropic's side.
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS prepared_files (
                file_path        TEXT PRIMARY KEY COLLATE NOCASE,
                case_folder      TEXT NOT NULL COLLATE NOCASE,
                category_code    TEXT NOT NULL,
                category_name    TEXT NOT NULL,
                kind             INTEGER NOT NULL,
                converted_to     INTEGER NOT NULL,
                prepared_path    TEXT NULL,
                source_write_utc TEXT NOT NULL,
                source_size      INTEGER NOT NULL,
                prepared_utc     TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_prepared_files_case_folder
                ON prepared_files (case_folder);
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
    /// The recorded conversion for <paramref name="filePath"/>, but only when the file on disk
    /// still matches what was converted *and* the converted output is still there. Anything
    /// else returns null so the caller re-converts.
    /// </summary>
    public PreparedFile? GetIfCurrent(string filePath)
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

        // Converted output lives in the temp folder, which Windows clears out from under us.
        if (record.ConvertedTo != ConversionTarget.None &&
            (record.PreparedPath is null || !File.Exists(record.PreparedPath)))
        {
            return null;
        }

        // Sub-second precision survives the round-trip through the "o" format, but compare
        // on whole seconds anyway: some file systems and copy tools round the timestamp.
        var sameTime = Math.Abs((record.SourceWriteUtc - info.LastWriteTimeUtc).TotalSeconds) < 1;
        return sameTime && record.SourceSize == info.Length ? record : null;
    }

    public PreparedFile? Get(string filePath)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT * FROM prepared_files WHERE file_path = $path;";
        command.Parameters.AddWithValue("$path", filePath);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public void Save(PreparedFile file)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO prepared_files
                (file_path, case_folder, category_code, category_name, kind,
                 converted_to, prepared_path, source_write_utc, source_size, prepared_utc)
            VALUES
                ($path, $folder, $code, $name, $kind,
                 $converted, $preparedPath, $writeUtc, $size, $preparedUtc)
            ON CONFLICT(file_path) DO UPDATE SET
                case_folder      = excluded.case_folder,
                category_code    = excluded.category_code,
                category_name    = excluded.category_name,
                kind             = excluded.kind,
                converted_to     = excluded.converted_to,
                prepared_path    = excluded.prepared_path,
                source_write_utc = excluded.source_write_utc,
                source_size      = excluded.source_size,
                prepared_utc     = excluded.prepared_utc;
            """;

        command.Parameters.AddWithValue("$path", file.FilePath);
        command.Parameters.AddWithValue("$folder", file.CaseFolder);
        command.Parameters.AddWithValue("$code", file.CategoryCode);
        command.Parameters.AddWithValue("$name", file.CategoryName);
        command.Parameters.AddWithValue("$kind", (int)file.Kind);
        command.Parameters.AddWithValue("$converted", (int)file.ConvertedTo);
        command.Parameters.AddWithValue("$preparedPath", (object?)file.PreparedPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$writeUtc", file.SourceWriteUtc.ToString("o"));
        command.Parameters.AddWithValue("$size", file.SourceSize);
        command.Parameters.AddWithValue("$preparedUtc", file.PreparedUtc.ToString("o"));

        command.ExecuteNonQuery();
    }

    /// <summary>Every conversion recorded for a case folder, whether or not the file still matches.</summary>
    public IReadOnlyList<PreparedFile> GetForCaseFolder(string caseFolder)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT * FROM prepared_files WHERE case_folder = $folder ORDER BY file_path;";
        command.Parameters.AddWithValue("$folder", caseFolder);

        var results = new List<PreparedFile>();
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

        command.CommandText = "DELETE FROM prepared_files WHERE file_path = $path;";
        command.Parameters.AddWithValue("$path", filePath);
        command.ExecuteNonQuery();
    }

    /// <summary>Forgets every conversion for a case folder. Returns how many rows went.</summary>
    public int RemoveCaseFolder(string caseFolder)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();

        command.CommandText = "DELETE FROM prepared_files WHERE case_folder = $folder;";
        command.Parameters.AddWithValue("$folder", caseFolder);
        return command.ExecuteNonQuery();
    }

    private static PreparedFile Read(IDataRecord row) => new(
        FilePath: row.GetString(row.GetOrdinal("file_path")),
        CaseFolder: row.GetString(row.GetOrdinal("case_folder")),
        CategoryCode: row.GetString(row.GetOrdinal("category_code")),
        CategoryName: row.GetString(row.GetOrdinal("category_name")),
        Kind: (AnthropicFileKind)row.GetInt32(row.GetOrdinal("kind")),
        ConvertedTo: (ConversionTarget)row.GetInt32(row.GetOrdinal("converted_to")),
        PreparedPath: row.IsDBNull(row.GetOrdinal("prepared_path"))
            ? null
            : row.GetString(row.GetOrdinal("prepared_path")),
        SourceWriteUtc: DateTime.Parse(
            row.GetString(row.GetOrdinal("source_write_utc")), null,
            System.Globalization.DateTimeStyles.RoundtripKind),
        SourceSize: row.GetInt64(row.GetOrdinal("source_size")),
        PreparedUtc: DateTime.Parse(
            row.GetString(row.GetOrdinal("prepared_utc")), null,
            System.Globalization.DateTimeStyles.RoundtripKind));
}
