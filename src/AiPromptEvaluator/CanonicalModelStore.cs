using System.IO;

using Microsoft.Data.Sqlite;

namespace AiPromptEvaluator;

/// <summary>
/// The SQLite store holding one extracted canonical model per case, keyed by tenant and
/// case reference — the same key the vector store uses, so the model and the chunks for a
/// case are always addressed the same way.
///
/// SQLite rather than Qdrant because this is a single document fetched by exact key, never
/// searched by similarity. Keeping it out of the vector store also means clearing the index
/// and deleting the model are separate, deliberate actions: re-indexing documents should
/// not silently discard an extraction that cost real tokens to produce.
/// </summary>
public sealed class CanonicalModelStore
{
    private readonly string _databasePath;

    public CanonicalModelStore(AppSettings settings)
        : this(settings.ResolveCanonicalModelDbPath())
    {
    }

    public CanonicalModelStore(string databasePath)
    {
        _databasePath = databasePath;
    }

    /// <summary>Where the database file lives, for status messages.</summary>
    public string DatabasePath => _databasePath;

    /// <summary>
    /// Creates the database and table if they are not there yet. Called before every
    /// operation: the file lives in the user's local app data and can be deleted between
    /// runs, so assuming it exists after the first call would be wrong.
    /// </summary>
    private SqliteConnection OpenConnection()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());

        connection.Open();

        using var create = connection.CreateCommand();
        create.CommandText =
            """
            CREATE TABLE IF NOT EXISTS canonical_model (
                tenant_id        INTEGER NOT NULL,
                case_reference   TEXT    NOT NULL COLLATE NOCASE,
                model_json       TEXT    NOT NULL,
                model_id         TEXT    NOT NULL,
                schema_version   TEXT    NOT NULL,
                source_documents TEXT    NOT NULL,
                extracted_at     TEXT    NOT NULL,
                input_tokens     INTEGER NOT NULL DEFAULT 0,
                output_tokens    INTEGER NOT NULL DEFAULT 0,
                cache_read_tokens  INTEGER NOT NULL DEFAULT 0,
                cache_write_tokens INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (tenant_id, case_reference)
            );
            """;
        create.ExecuteNonQuery();

        return connection;
    }

    /// <summary>
    /// Writes the model, replacing any previous extraction for the same case. Re-extracting
    /// is the supported way to correct a bad parse, so an upsert is the right behaviour —
    /// two models for one case would leave a check run choosing between them.
    /// </summary>
    public async Task SaveAsync(CanonicalModelDocument document, CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO canonical_model
                (tenant_id, case_reference, model_json, model_id, schema_version,
                 source_documents, extracted_at,
                 input_tokens, output_tokens, cache_read_tokens, cache_write_tokens)
            VALUES
                ($tenant, $case, $json, $model, $schema,
                 $sources, $extracted,
                 $input, $output, $cacheRead, $cacheWrite)
            ON CONFLICT (tenant_id, case_reference) DO UPDATE SET
                model_json         = excluded.model_json,
                model_id           = excluded.model_id,
                schema_version     = excluded.schema_version,
                source_documents   = excluded.source_documents,
                extracted_at       = excluded.extracted_at,
                input_tokens       = excluded.input_tokens,
                output_tokens      = excluded.output_tokens,
                cache_read_tokens  = excluded.cache_read_tokens,
                cache_write_tokens = excluded.cache_write_tokens;
            """;

        command.Parameters.AddWithValue("$tenant", document.TenantId);
        command.Parameters.AddWithValue("$case", document.CaseReference);
        command.Parameters.AddWithValue("$json", document.Json);
        command.Parameters.AddWithValue("$model", document.ModelId);
        command.Parameters.AddWithValue("$schema", document.SchemaVersion);
        command.Parameters.AddWithValue("$sources", string.Join("\n", document.SourceDocuments));
        command.Parameters.AddWithValue("$extracted", document.ExtractedAt.ToString("O"));
        command.Parameters.AddWithValue("$input", document.Usage.InputTokens);
        command.Parameters.AddWithValue("$output", document.Usage.OutputTokens);
        command.Parameters.AddWithValue("$cacheRead", document.Usage.CacheReadTokens);
        command.Parameters.AddWithValue("$cacheWrite", document.Usage.CacheWriteTokens);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The stored model for this case, or null when nothing has been extracted yet.</summary>
    public async Task<CanonicalModelDocument?> LoadAsync(
        string caseReference, int tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT model_json, model_id, schema_version, source_documents, extracted_at,
                   input_tokens, output_tokens, cache_read_tokens, cache_write_tokens
            FROM canonical_model
            WHERE tenant_id = $tenant AND case_reference = $case;
            """;
        command.Parameters.AddWithValue("$tenant", tenantId);
        command.Parameters.AddWithValue("$case", caseReference);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new CanonicalModelDocument(
            CaseReference: caseReference,
            TenantId: tenantId,
            Json: reader.GetString(0),
            ModelId: reader.GetString(1),
            SchemaVersion: reader.GetString(2),
            SourceDocuments: reader.GetString(3)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            ExtractedAt: DateTimeOffset.Parse(reader.GetString(4)),
            Usage: new TokenUsage(
                InputTokens: reader.GetInt64(5),
                OutputTokens: reader.GetInt64(6),
                CacheWriteTokens: reader.GetInt64(8),
                CacheReadTokens: reader.GetInt64(7)));
    }

    /// <summary>True when a model exists for this case, without paying to read it back.</summary>
    public async Task<bool> ExistsAsync(
        string caseReference, int tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();

        command.CommandText =
            "SELECT COUNT(1) FROM canonical_model WHERE tenant_id = $tenant AND case_reference = $case;";
        command.Parameters.AddWithValue("$tenant", tenantId);
        command.Parameters.AddWithValue("$case", caseReference);

        var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(count) > 0;
    }

    /// <summary>Removes the model for one case. Returns true when a row was actually deleted.</summary>
    public async Task<bool> DeleteAsync(
        string caseReference, int tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();

        command.CommandText =
            "DELETE FROM canonical_model WHERE tenant_id = $tenant AND case_reference = $case;";
        command.Parameters.AddWithValue("$tenant", tenantId);
        command.Parameters.AddWithValue("$case", caseReference);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }
}
