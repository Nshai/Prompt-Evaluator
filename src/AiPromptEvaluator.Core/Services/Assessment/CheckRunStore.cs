using System.IO;
using System.Text.Json;

using Microsoft.Data.Sqlite;

namespace AiPromptEvaluator;

/// <summary>
/// The SQLite archive of finished runs.
///
/// Shredded into tables rather than kept as one JSON blob per run, because everything anyone
/// asks of this — "show me everything behind CHK-003", "every passage that mentioned the fact
/// find", "every data inconsistency in this case" — is a <c>WHERE</c> against a column, and none
/// of it can be answered by a blob without parsing every run in full first. The finding itself
/// is the exception and stays JSON inside its row: it is a document with a schema that already
/// exists, and shredding it would create a second definition of the same shape to keep in step.
/// Its two classifications are lifted out beside it precisely because they are the columns a
/// reader filters on.
///
/// A run is written once, at the end, in a single transaction. Streaming rows as the run
/// progressed was the alternative and was rejected: requirements are assessed concurrently, so
/// it would put a lock on the write path of every group, and a run that is cancelled halfway
/// should not leave a half-run in the archive claiming to be a report.
/// </summary>
public sealed class CheckRunStore : ICheckRunStore
{
    private static readonly JsonSerializerOptions FindingJson = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly string _databasePath;

    public CheckRunStore(AppSettings settings)
        : this(settings.ResolveCheckRunDbPath())
    {
    }

    public CheckRunStore(string databasePath) => _databasePath = databasePath;

    public string DatabasePath => _databasePath;

    /// <summary>
    /// Opens the database, creating the file and schema when they are not there. Called on
    /// every operation for the same reason <see cref="CanonicalModelStore"/> does it: the file
    /// lives in the user's local app data and can be deleted between runs.
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
            CREATE TABLE IF NOT EXISTS check_run (
                run_id               TEXT    NOT NULL PRIMARY KEY,
                tenant_id            INTEGER NOT NULL,
                case_reference       TEXT    NOT NULL COLLATE NOCASE,
                chat_model           TEXT    NOT NULL,
                started_at           TEXT    NOT NULL,
                completed_at         TEXT    NULL,
                fingerprint          TEXT    NOT NULL,
                cache_mode           TEXT    NOT NULL,
                canonical_model_json TEXT    NULL,
                canonical_model_id   TEXT    NOT NULL,
                schema_version       TEXT    NOT NULL,
                source_documents     TEXT    NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_check_run_case
                ON check_run (case_reference, tenant_id, started_at DESC);

            CREATE TABLE IF NOT EXISTS check_result (
                run_id                  TEXT    NOT NULL,
                check_id                TEXT    NOT NULL COLLATE NOCASE,
                check_name              TEXT    NOT NULL,
                outcome                 TEXT    NOT NULL,
                summary                 TEXT    NOT NULL,
                searches_run            INTEGER NOT NULL,
                passages_retrieved      INTEGER NOT NULL,
                canonical_resolved      INTEGER NOT NULL,
                canonical_missing       INTEGER NOT NULL,
                elapsed_ms              INTEGER NOT NULL,
                input_tokens            INTEGER NOT NULL,
                output_tokens           INTEGER NOT NULL,
                cache_read_tokens       INTEGER NOT NULL,
                cache_write_tokens      INTEGER NOT NULL,
                error                   TEXT    NULL,
                ordinal                 INTEGER NOT NULL,

                -- Copied off the check sheet as it read for this run. The sheet is edited
                -- between runs, so looking the wording up at render time would print this
                -- month's question beside last month's answer.
                narrative               TEXT    NOT NULL DEFAULT '',
                regulatory_basis        TEXT    NOT NULL DEFAULT '',
                applies_to              TEXT    NOT NULL DEFAULT '',
                PRIMARY KEY (run_id, check_id)
            );

            CREATE TABLE IF NOT EXISTS check_response (
                run_id        TEXT NOT NULL,
                check_id      TEXT NOT NULL COLLATE NOCASE,
                group_id      TEXT NOT NULL COLLATE NOCASE,
                requirement   TEXT NOT NULL,
                system_prompt TEXT NOT NULL,
                user_prompt   TEXT NOT NULL,
                raw_response  TEXT NOT NULL,
                finding_json  TEXT NULL,
                ordinal       INTEGER NOT NULL,
                PRIMARY KEY (run_id, check_id, group_id)
            );

            -- One row per requirement per category, rather than a delimited column on the
            -- response row. "Every data inconsistency in this case" is the question the closed
            -- vocabulary exists to answer, and it is a WHERE against a column here and a LIKE
            -- against a string list there — which would also match "Data inconsistency" inside
            -- some future longer name.
            CREATE TABLE IF NOT EXISTS check_issue_category (
                run_id   TEXT NOT NULL,
                check_id TEXT NOT NULL COLLATE NOCASE,
                group_id TEXT NOT NULL COLLATE NOCASE,
                category TEXT NOT NULL,
                PRIMARY KEY (run_id, check_id, group_id, category)
            );

            CREATE INDEX IF NOT EXISTS ix_check_issue_category
                ON check_issue_category (run_id, category);

            -- The closer-look flags are three booleans on the requirement rather than three
            -- rows, because they are a fixed set that every requirement has an answer for.
            CREATE TABLE IF NOT EXISTS check_closer_look (
                run_id           TEXT    NOT NULL,
                check_id         TEXT    NOT NULL COLLATE NOCASE,
                group_id         TEXT    NOT NULL COLLATE NOCASE,
                low_confidence   INTEGER NOT NULL,
                partial_coverage INTEGER NOT NULL,
                unreadable_source INTEGER NOT NULL,
                agent_score      INTEGER NOT NULL DEFAULT 0,
                agent_level      TEXT    NOT NULL DEFAULT '',
                evidence_score   INTEGER NOT NULL DEFAULT 0,
                evidence_level   TEXT    NOT NULL DEFAULT '',
                PRIMARY KEY (run_id, check_id, group_id)
            );

            -- Every search a requirement ran, hits or not. A query that returned nothing leaves
            -- no passage row, so without this a requirement whose every search came back empty
            -- is indistinguishable from one that deliberately searched for nothing — and those
            -- are opposite findings.
            CREATE TABLE IF NOT EXISTS check_query (
                run_id            TEXT    NOT NULL,
                check_id          TEXT    NOT NULL COLLATE NOCASE,
                group_id          TEXT    NOT NULL COLLATE NOCASE,
                ordinal           INTEGER NOT NULL,
                query_id          TEXT    NOT NULL,
                query_text        TEXT    NOT NULL,
                target_categories TEXT    NOT NULL,
                hits              INTEGER NOT NULL,
                PRIMARY KEY (run_id, check_id, group_id, ordinal)
            );

            CREATE TABLE IF NOT EXISTS check_evidence (
                run_id            TEXT    NOT NULL,
                check_id          TEXT    NOT NULL COLLATE NOCASE,
                group_id          TEXT    NOT NULL COLLATE NOCASE,
                ordinal           INTEGER NOT NULL,
                query_id          TEXT    NOT NULL,
                query_text        TEXT    NOT NULL,
                target_categories TEXT    NOT NULL,
                document_name     TEXT    NOT NULL,
                category_code     TEXT    NOT NULL,
                category_name     TEXT    NOT NULL,
                score             REAL    NOT NULL,
                passage_text      TEXT    NOT NULL,
                in_pack           INTEGER NOT NULL,
                PRIMARY KEY (run_id, check_id, group_id, ordinal)
            );

            CREATE INDEX IF NOT EXISTS ix_check_evidence_check
                ON check_evidence (run_id, check_id);

            CREATE TABLE IF NOT EXISTS check_fact (
                run_id    TEXT    NOT NULL,
                check_id  TEXT    NOT NULL COLLATE NOCASE,
                group_id  TEXT    NOT NULL COLLATE NOCASE,
                ordinal   INTEGER NOT NULL,
                path      TEXT    NOT NULL,
                value_json TEXT   NOT NULL,
                found     INTEGER NOT NULL,
                PRIMARY KEY (run_id, check_id, group_id, ordinal)
            );
            """;
        create.ExecuteNonQuery();

        return connection;
    }

    /// <inheritdoc />
    public async Task SaveAsync(CheckRunRecord run, CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Re-saving a run id replaces it wholesale. The only caller that does so is a run
        // finishing after its own cancellation write, and a merge of the two would leave the
        // archive holding groups from both.
        foreach (var table in new[]
                 {
                     "check_closer_look", "check_issue_category", "check_query", "check_fact",
                     "check_evidence", "check_response", "check_result", "check_run",
                 })
        {
            await using var wipe = connection.CreateCommand();
            wipe.Transaction = (SqliteTransaction)transaction;
            wipe.CommandText = $"DELETE FROM {table} WHERE run_id = $run;";
            wipe.Parameters.AddWithValue("$run", run.RunId);
            await wipe.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var header = connection.CreateCommand())
        {
            header.Transaction = (SqliteTransaction)transaction;
            header.CommandText =
                """
                INSERT INTO check_run
                    (run_id, tenant_id, case_reference, chat_model, started_at, completed_at,
                     fingerprint, cache_mode, canonical_model_json, canonical_model_id,
                     schema_version, source_documents)
                VALUES
                    ($run, $tenant, $case, $model, $started, $completed,
                     $fingerprint, $cache, $json, $modelId, $schema, $sources);
                """;
            header.Parameters.AddWithValue("$run", run.RunId);
            header.Parameters.AddWithValue("$tenant", run.TenantId);
            header.Parameters.AddWithValue("$case", run.CaseReference);
            header.Parameters.AddWithValue("$model", run.ChatModel);
            header.Parameters.AddWithValue("$started", run.StartedAt.ToString("O"));
            header.Parameters.AddWithValue("$completed", (object?)run.CompletedAt?.ToString("O") ?? DBNull.Value);
            header.Parameters.AddWithValue("$fingerprint", run.Fingerprint);
            header.Parameters.AddWithValue("$cache", run.CacheMode);
            header.Parameters.AddWithValue("$json", (object?)run.CanonicalModelJson ?? DBNull.Value);
            header.Parameters.AddWithValue("$modelId", run.CanonicalModelId);
            header.Parameters.AddWithValue("$schema", run.SchemaVersion);
            header.Parameters.AddWithValue("$sources", string.Join("\n", run.SourceDocuments));
            await header.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        for (var c = 0; c < run.Checks.Count; c++)
        {
            var check = run.Checks[c];

            await using (var result = connection.CreateCommand())
            {
                result.Transaction = (SqliteTransaction)transaction;
                result.CommandText =
                    """
                    INSERT INTO check_result
                        (run_id, check_id, check_name, outcome, summary, searches_run,
                         passages_retrieved, canonical_resolved, canonical_missing, elapsed_ms,
                         input_tokens, output_tokens, cache_read_tokens, cache_write_tokens,
                         error, ordinal, narrative, regulatory_basis, applies_to)
                    VALUES
                        ($run, $check, $name, $outcome, $summary, $searches,
                         $passages, $resolved, $missing, $elapsed,
                         $input, $output, $cacheRead, $cacheWrite, $error, $ordinal,
                         $narrative, $basis, $appliesTo);
                    """;
                result.Parameters.AddWithValue("$run", run.RunId);
                result.Parameters.AddWithValue("$check", check.CheckId);
                result.Parameters.AddWithValue("$name", check.CheckName);
                result.Parameters.AddWithValue("$outcome", check.Outcome);
                result.Parameters.AddWithValue("$summary", check.Summary);
                result.Parameters.AddWithValue("$narrative", check.Narrative);
                result.Parameters.AddWithValue("$basis", check.RegulatoryBasis);
                result.Parameters.AddWithValue("$appliesTo", check.AppliesTo);
                result.Parameters.AddWithValue("$searches", check.SearchesRun);
                result.Parameters.AddWithValue("$passages", check.PassagesRetrieved);
                result.Parameters.AddWithValue("$resolved", check.CanonicalPathsResolved);
                result.Parameters.AddWithValue("$missing", check.CanonicalPathsMissing);
                result.Parameters.AddWithValue("$elapsed", (long)check.Elapsed.TotalMilliseconds);
                result.Parameters.AddWithValue("$input", check.Usage.InputTokens);
                result.Parameters.AddWithValue("$output", check.Usage.OutputTokens);
                result.Parameters.AddWithValue("$cacheRead", check.Usage.CacheReadTokens);
                result.Parameters.AddWithValue("$cacheWrite", check.Usage.CacheWriteTokens);
                result.Parameters.AddWithValue("$error", (object?)check.Error ?? DBNull.Value);
                result.Parameters.AddWithValue("$ordinal", c);
                await result.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            for (var g = 0; g < check.Groups.Count; g++)
            {
                var group = check.Groups[g];

                await using (var response = connection.CreateCommand())
                {
                    response.Transaction = (SqliteTransaction)transaction;
                    response.CommandText =
                        """
                        INSERT INTO check_response
                            (run_id, check_id, group_id, requirement, system_prompt, user_prompt,
                             raw_response, finding_json, ordinal)
                        VALUES
                            ($run, $check, $group, $requirement, $system, $user,
                             $raw, $finding, $ordinal);
                        """;
                    response.Parameters.AddWithValue("$run", run.RunId);
                    response.Parameters.AddWithValue("$check", group.CheckId);
                    response.Parameters.AddWithValue("$group", group.GroupId);
                    response.Parameters.AddWithValue("$requirement", group.Requirement);
                    response.Parameters.AddWithValue("$system", group.SystemPrompt);
                    response.Parameters.AddWithValue("$user", group.UserPrompt);
                    response.Parameters.AddWithValue("$raw", group.RawResponse);
                    response.Parameters.AddWithValue(
                        "$finding",
                        group.Finding is null
                            ? DBNull.Value
                            : JsonSerializer.Serialize(group.Finding, FindingJson));
                    response.Parameters.AddWithValue("$ordinal", g);
                    await response.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                foreach (var category in group.IssueCategories)
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = (SqliteTransaction)transaction;
                    command.CommandText =
                        """
                        INSERT OR IGNORE INTO check_issue_category
                            (run_id, check_id, group_id, category)
                        VALUES ($run, $check, $group, $category);
                        """;
                    command.Parameters.AddWithValue("$run", run.RunId);
                    command.Parameters.AddWithValue("$check", group.CheckId);
                    command.Parameters.AddWithValue("$group", group.GroupId);
                    command.Parameters.AddWithValue("$category", category);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await using (var closerLook = connection.CreateCommand())
                {
                    // The two coverage scores are materialised here so they can be queried
                    // without reading every finding back. The report recomputes them from the
                    // same definition, so an archive written by an older build shows its
                    // recomputed score rather than a stale one.
                    var agent = group.AgentCoverage;
                    var evidence = group.EvidenceCoverage;

                    closerLook.Transaction = (SqliteTransaction)transaction;
                    closerLook.CommandText =
                        """
                        INSERT INTO check_closer_look
                            (run_id, check_id, group_id, low_confidence, partial_coverage,
                             unreadable_source, agent_score, agent_level, evidence_score,
                             evidence_level)
                        VALUES ($run, $check, $group, $low, $partial, $unreadable,
                                $agentScore, $agentLevel, $evidenceScore, $evidenceLevel);
                        """;
                    closerLook.Parameters.AddWithValue("$run", run.RunId);
                    closerLook.Parameters.AddWithValue("$check", group.CheckId);
                    closerLook.Parameters.AddWithValue("$group", group.GroupId);
                    closerLook.Parameters.AddWithValue("$low", group.CloserLook.LowConfidence ? 1 : 0);
                    closerLook.Parameters.AddWithValue("$partial", group.CloserLook.PartialCoverage ? 1 : 0);
                    closerLook.Parameters.AddWithValue("$unreadable", group.CloserLook.UnreadableSource ? 1 : 0);
                    closerLook.Parameters.AddWithValue("$agentScore", agent.Score);
                    closerLook.Parameters.AddWithValue("$agentLevel", agent.Level);
                    closerLook.Parameters.AddWithValue("$evidenceScore", evidence.Score);
                    closerLook.Parameters.AddWithValue("$evidenceLevel", evidence.Level);
                    await closerLook.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                for (var q = 0; q < group.Queries.Count; q++)
                {
                    var query = group.Queries[q];

                    await using var command = connection.CreateCommand();
                    command.Transaction = (SqliteTransaction)transaction;
                    command.CommandText =
                        """
                        INSERT INTO check_query
                            (run_id, check_id, group_id, ordinal, query_id, query_text,
                             target_categories, hits)
                        VALUES ($run, $check, $group, $ordinal, $queryId, $text, $targets, $hits);
                        """;
                    command.Parameters.AddWithValue("$run", run.RunId);
                    command.Parameters.AddWithValue("$check", group.CheckId);
                    command.Parameters.AddWithValue("$group", group.GroupId);
                    command.Parameters.AddWithValue("$ordinal", q);
                    command.Parameters.AddWithValue("$queryId", query.QueryId);
                    command.Parameters.AddWithValue("$text", query.Text);
                    command.Parameters.AddWithValue("$targets", query.TargetCategories);
                    command.Parameters.AddWithValue("$hits", query.Hits);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                for (var e = 0; e < group.Passages.Count; e++)
                {
                    var passage = group.Passages[e];

                    await using var evidence = connection.CreateCommand();
                    evidence.Transaction = (SqliteTransaction)transaction;
                    evidence.CommandText =
                        """
                        INSERT INTO check_evidence
                            (run_id, check_id, group_id, ordinal, query_id, query_text,
                             target_categories, document_name, category_code, category_name,
                             score, passage_text, in_pack)
                        VALUES
                            ($run, $check, $group, $ordinal, $queryId, $queryText,
                             $targets, $document, $code, $categoryName,
                             $score, $text, $inPack);
                        """;
                    evidence.Parameters.AddWithValue("$run", run.RunId);
                    evidence.Parameters.AddWithValue("$check", group.CheckId);
                    evidence.Parameters.AddWithValue("$group", group.GroupId);
                    evidence.Parameters.AddWithValue("$ordinal", e);
                    evidence.Parameters.AddWithValue("$queryId", passage.QueryId);
                    evidence.Parameters.AddWithValue("$queryText", passage.QueryText);
                    evidence.Parameters.AddWithValue("$targets", passage.TargetCategories);
                    evidence.Parameters.AddWithValue("$document", passage.DocumentName);
                    evidence.Parameters.AddWithValue("$code", passage.CategoryCode);
                    evidence.Parameters.AddWithValue("$categoryName", passage.CategoryName);
                    evidence.Parameters.AddWithValue("$score", passage.Score);
                    evidence.Parameters.AddWithValue("$text", passage.Text);
                    evidence.Parameters.AddWithValue("$inPack", passage.InPack ? 1 : 0);
                    await evidence.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                for (var f = 0; f < group.Facts.Count; f++)
                {
                    var fact = group.Facts[f];

                    await using var command = connection.CreateCommand();
                    command.Transaction = (SqliteTransaction)transaction;
                    command.CommandText =
                        """
                        INSERT INTO check_fact
                            (run_id, check_id, group_id, ordinal, path, value_json, found)
                        VALUES
                            ($run, $check, $group, $ordinal, $path, $json, $found);
                        """;
                    command.Parameters.AddWithValue("$run", run.RunId);
                    command.Parameters.AddWithValue("$check", group.CheckId);
                    command.Parameters.AddWithValue("$group", group.GroupId);
                    command.Parameters.AddWithValue("$ordinal", f);
                    command.Parameters.AddWithValue("$path", fact.Path);
                    command.Parameters.AddWithValue("$json", fact.Json);
                    command.Parameters.AddWithValue("$found", fact.Found ? 1 : 0);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CheckRunSummary>> ListAsync(
        string caseReference, int tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();

        // The headline is derived here rather than stored, so a change to how outcomes are
        // counted does not leave old rows describing themselves in the old arithmetic.
        command.CommandText =
            """
            SELECT r.run_id, r.case_reference, r.tenant_id, r.chat_model, r.started_at,
                   COUNT(c.check_id),
                   SUM(CASE WHEN c.outcome = 'PotentialConcern' THEN 1 ELSE 0 END),
                   SUM(CASE WHEN c.outcome = 'NoIssue'          THEN 1 ELSE 0 END),
                   SUM(CASE WHEN c.outcome = 'NotApplicable'    THEN 1 ELSE 0 END)
            FROM check_run r
            LEFT JOIN check_result c ON c.run_id = r.run_id
            WHERE r.case_reference = $case AND r.tenant_id = $tenant
            GROUP BY r.run_id
            ORDER BY r.started_at DESC;
            """;
        command.Parameters.AddWithValue("$case", caseReference);
        command.Parameters.AddWithValue("$tenant", tenantId);

        var runs = new List<CheckRunSummary>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var total = reader.GetInt32(5);
            var concerns = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
            var clear = reader.IsDBNull(7) ? 0 : reader.GetInt32(7);
            var notApplicable = reader.IsDBNull(8) ? 0 : reader.GetInt32(8);

            runs.Add(new CheckRunSummary(
                RunId: reader.GetString(0),
                CaseReference: reader.GetString(1),
                TenantId: reader.GetInt32(2),
                ChatModel: reader.GetString(3),
                StartedAt: DateTimeOffset.Parse(reader.GetString(4)),
                CheckCount: total,
                Headline: $"{concerns} potential concern(s), {clear} no issue, {notApplicable} N/A"));
        }

        return runs;
    }

    /// <inheritdoc />
    public async Task<CheckRunRecord?> LoadLatestAsync(
        string caseReference, int tenantId, CancellationToken cancellationToken = default)
    {
        var runs = await ListAsync(caseReference, tenantId, cancellationToken).ConfigureAwait(false);

        return runs.Count == 0
            ? null
            : await LoadAsync(runs[0].RunId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CheckRunRecord?> LoadAsync(string runId, CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();

        string caseReference, chatModel, fingerprint, cacheMode, canonicalModelId, schemaVersion, sources;
        string? canonicalJson;
        int tenantId;
        DateTimeOffset startedAt;
        DateTimeOffset? completedAt;

        await using (var header = connection.CreateCommand())
        {
            header.CommandText =
                """
                SELECT tenant_id, case_reference, chat_model, started_at, completed_at,
                       fingerprint, cache_mode, canonical_model_json, canonical_model_id,
                       schema_version, source_documents
                FROM check_run WHERE run_id = $run;
                """;
            header.Parameters.AddWithValue("$run", runId);

            await using var reader = await header.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            tenantId = reader.GetInt32(0);
            caseReference = reader.GetString(1);
            chatModel = reader.GetString(2);
            startedAt = DateTimeOffset.Parse(reader.GetString(3));
            completedAt = reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4));
            fingerprint = reader.GetString(5);
            cacheMode = reader.GetString(6);
            canonicalJson = reader.IsDBNull(7) ? null : reader.GetString(7);
            canonicalModelId = reader.GetString(8);
            schemaVersion = reader.GetString(9);
            sources = reader.GetString(10);
        }

        var evidence = await ReadEvidenceAsync(connection, runId, cancellationToken).ConfigureAwait(false);
        var facts = await ReadFactsAsync(connection, runId, cancellationToken).ConfigureAwait(false);
        var queries = await ReadQueriesAsync(connection, runId, cancellationToken).ConfigureAwait(false);
        var categories = await ReadCategoriesAsync(connection, runId, cancellationToken).ConfigureAwait(false);
        var closerLook = await ReadCloserLookAsync(connection, runId, cancellationToken).ConfigureAwait(false);
        var groups = await ReadGroupsAsync(
                connection, runId, evidence, facts, queries, categories, closerLook, cancellationToken)
            .ConfigureAwait(false);
        var checks = await ReadChecksAsync(connection, runId, groups, cancellationToken).ConfigureAwait(false);

        return new CheckRunRecord(
            RunId: runId,
            CaseReference: caseReference,
            TenantId: tenantId,
            ChatModel: chatModel,
            StartedAt: startedAt,
            CompletedAt: completedAt,
            Fingerprint: fingerprint,
            CacheMode: cacheMode,
            CanonicalModelJson: canonicalJson,
            CanonicalModelId: canonicalModelId,
            SchemaVersion: schemaVersion,
            SourceDocuments: sources.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Checks: checks);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string runId, CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var deleted = 0;

        foreach (var table in new[]
                 {
                     "check_closer_look", "check_issue_category", "check_query", "check_fact",
                     "check_evidence", "check_response", "check_result", "check_run",
                 })
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = $"DELETE FROM {table} WHERE run_id = $run;";
            command.Parameters.AddWithValue("$run", runId);

            var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (table == "check_run")
            {
                deleted = rows;
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return deleted > 0;
    }

    // ──────────────────────────────────────────────
    // Reading back. Each child table is read once for the whole run and grouped in memory,
    // rather than queried per check — a run has ten checks and a few thousand passages, and
    // ten round trips to save one pass over a list is the wrong trade.
    // ──────────────────────────────────────────────

    private static async Task<Dictionary<(string, string), List<RecordedPassage>>> ReadEvidenceAsync(
        SqliteConnection connection, string runId, CancellationToken cancellationToken)
    {
        var byGroup = new Dictionary<(string, string), List<RecordedPassage>>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT check_id, group_id, ordinal, query_id, query_text, target_categories,
                   document_name, category_code, category_name, score, passage_text, in_pack
            FROM check_evidence WHERE run_id = $run ORDER BY check_id, group_id, ordinal;
            """;
        command.Parameters.AddWithValue("$run", runId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = (reader.GetString(0), reader.GetString(1));
            if (!byGroup.TryGetValue(key, out var list))
            {
                byGroup[key] = list = [];
            }

            list.Add(new RecordedPassage(
                QueryId: reader.GetString(3),
                QueryText: reader.GetString(4),
                TargetCategories: reader.GetString(5),
                DocumentName: reader.GetString(6),
                CategoryCode: reader.GetString(7),
                CategoryName: reader.GetString(8),
                Score: reader.GetDouble(9),
                Text: reader.GetString(10),
                InPack: reader.GetInt32(11) != 0,
                Ordinal: reader.GetInt32(2)));
        }

        return byGroup;
    }

    private static async Task<Dictionary<(string, string), List<RecordedFact>>> ReadFactsAsync(
        SqliteConnection connection, string runId, CancellationToken cancellationToken)
    {
        var byGroup = new Dictionary<(string, string), List<RecordedFact>>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT check_id, group_id, path, value_json, found
            FROM check_fact WHERE run_id = $run ORDER BY check_id, group_id, ordinal;
            """;
        command.Parameters.AddWithValue("$run", runId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = (reader.GetString(0), reader.GetString(1));
            if (!byGroup.TryGetValue(key, out var list))
            {
                byGroup[key] = list = [];
            }

            list.Add(new RecordedFact(reader.GetString(2), reader.GetString(3), reader.GetInt32(4) != 0));
        }

        return byGroup;
    }

    private static async Task<Dictionary<string, List<RecordedGroup>>> ReadGroupsAsync(
        SqliteConnection connection,
        string runId,
        Dictionary<(string, string), List<RecordedPassage>> evidence,
        Dictionary<(string, string), List<RecordedFact>> facts,
        Dictionary<(string, string), List<RecordedQuery>> queries,
        Dictionary<(string, string), List<string>> categories,
        Dictionary<(string, string), CloserLook> closerLook,
        CancellationToken cancellationToken)
    {
        var byCheck = new Dictionary<string, List<RecordedGroup>>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT check_id, group_id, requirement, system_prompt, user_prompt, raw_response, finding_json
            FROM check_response WHERE run_id = $run ORDER BY check_id, ordinal;
            """;
        command.Parameters.AddWithValue("$run", runId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var checkId = reader.GetString(0);
            var groupId = reader.GetString(1);

            if (!byCheck.TryGetValue(checkId, out var list))
            {
                byCheck[checkId] = list = [];
            }

            GroupFinding? finding = null;
            if (!reader.IsDBNull(6))
            {
                // A finding that no longer deserialises is worth less than the raw response
                // beside it, and losing the whole report over one is the wrong trade.
                try
                {
                    finding = JsonSerializer.Deserialize<GroupFinding>(reader.GetString(6), FindingJson);
                }
                catch (JsonException)
                {
                    finding = null;
                }
            }

            list.Add(new RecordedGroup(
                CheckId: checkId,
                GroupId: groupId,
                Requirement: reader.GetString(2),
                Facts: facts.TryGetValue((checkId, groupId), out var f) ? f : [],
                Queries: queries.TryGetValue((checkId, groupId), out var q) ? q : [],
                Passages: evidence.TryGetValue((checkId, groupId), out var p) ? p : [],
                SystemPrompt: reader.GetString(3),
                UserPrompt: reader.GetString(4),
                RawResponse: reader.GetString(5),
                Finding: finding)
            {
                IssueCategories = categories.TryGetValue((checkId, groupId), out var c)
                    ? IssueCategory.Clean(c)
                    : [],
                CloserLook = closerLook.TryGetValue((checkId, groupId), out var look)
                    ? look
                    : CloserLook.None,
            });
        }

        return byCheck;
    }

    private static async Task<Dictionary<(string, string), List<RecordedQuery>>> ReadQueriesAsync(
        SqliteConnection connection, string runId, CancellationToken cancellationToken)
    {
        var byGroup = new Dictionary<(string, string), List<RecordedQuery>>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT check_id, group_id, query_id, query_text, target_categories, hits
            FROM check_query WHERE run_id = $run ORDER BY check_id, group_id, ordinal;
            """;
        command.Parameters.AddWithValue("$run", runId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = (reader.GetString(0), reader.GetString(1));
            if (!byGroup.TryGetValue(key, out var list))
            {
                byGroup[key] = list = [];
            }

            list.Add(new RecordedQuery(
                reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5)));
        }

        return byGroup;
    }

    private static async Task<Dictionary<(string, string), List<string>>> ReadCategoriesAsync(
        SqliteConnection connection, string runId, CancellationToken cancellationToken)
    {
        var byGroup = new Dictionary<(string, string), List<string>>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT check_id, group_id, category FROM check_issue_category WHERE run_id = $run;";
        command.Parameters.AddWithValue("$run", runId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = (reader.GetString(0), reader.GetString(1));
            if (!byGroup.TryGetValue(key, out var list))
            {
                byGroup[key] = list = [];
            }

            list.Add(reader.GetString(2));
        }

        return byGroup;
    }

    private static async Task<Dictionary<(string, string), CloserLook>> ReadCloserLookAsync(
        SqliteConnection connection, string runId, CancellationToken cancellationToken)
    {
        var byGroup = new Dictionary<(string, string), CloserLook>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT check_id, group_id, low_confidence, partial_coverage, unreadable_source
            FROM check_closer_look WHERE run_id = $run;
            """;
        command.Parameters.AddWithValue("$run", runId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            byGroup[(reader.GetString(0), reader.GetString(1))] = new CloserLook(
                LowConfidence: reader.GetInt32(2) != 0,
                PartialCoverage: reader.GetInt32(3) != 0,
                UnreadableSource: reader.GetInt32(4) != 0);
        }

        return byGroup;
    }

    private static async Task<List<RecordedCheck>> ReadChecksAsync(
        SqliteConnection connection,
        string runId,
        Dictionary<string, List<RecordedGroup>> groups,
        CancellationToken cancellationToken)
    {
        var checks = new List<RecordedCheck>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT check_id, check_name, outcome, summary, searches_run, passages_retrieved,
                   canonical_resolved, canonical_missing, elapsed_ms,
                   input_tokens, output_tokens, cache_read_tokens, cache_write_tokens, error,
                   narrative, regulatory_basis, applies_to
            FROM check_result WHERE run_id = $run ORDER BY ordinal;
            """;
        command.Parameters.AddWithValue("$run", runId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var checkId = reader.GetString(0);

            checks.Add(new RecordedCheck(
                CheckId: checkId,
                CheckName: reader.GetString(1),
                Outcome: reader.GetString(2),
                Summary: reader.GetString(3),
                SearchesRun: reader.GetInt32(4),
                PassagesRetrieved: reader.GetInt32(5),
                CanonicalPathsResolved: reader.GetInt32(6),
                CanonicalPathsMissing: reader.GetInt32(7),
                Elapsed: TimeSpan.FromMilliseconds(reader.GetInt64(8)),
                Usage: new TokenUsage(
                    InputTokens: reader.GetInt64(9),
                    OutputTokens: reader.GetInt64(10),
                    CacheWriteTokens: reader.GetInt64(12),
                    CacheReadTokens: reader.GetInt64(11)),
                Error: reader.IsDBNull(13) ? null : reader.GetString(13),
                Groups: groups.TryGetValue(checkId, out var g) ? g : [])
            {
                Narrative = reader.GetString(14),
                RegulatoryBasis = reader.GetString(15),
                AppliesTo = reader.GetString(16),
            });
        }

        return checks;
    }
}
