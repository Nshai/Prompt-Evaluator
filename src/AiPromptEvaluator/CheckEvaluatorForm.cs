using System.Diagnostics;
using System.IO;
using System.Text;

using Microsoft.Extensions.AI;

namespace AiPromptEvaluator;

public partial class CheckEvaluatorForm : Form
{
    private readonly AppSettings _settings;
    private readonly IChatCompletionClient _chat;

    // Creation the container cannot do for us: a store the form disposes, a search scoped to a
    // case, a runner scoped to an extracted model, a log file named for the run.
    private readonly ICaseDocumentStoreFactory _stores;
    private readonly ICaseDocumentSearchServiceFactory _searches;
    private readonly ICheckPlanRunnerFactory _runners;
    private readonly IPromptLogWriterFactory _promptLogs;

    /// <summary>Navigation. The two screens open each other, so neither may construct the other.</summary>
    private readonly Func<MainForm> _mainForm;
    private readonly ICanonicalModelExtractor _extractor;
    private List<AssessmentCheck> _checks = new();
    private CancellationTokenSource? _cts;
    private bool _busy;

    /// <summary>The case whose chunks are in the vector store, or null when nothing is loaded.</summary>
    private string? _indexedCase;

    /// <summary>
    /// The canonical model for the current case, once it has been extracted or found in the
    /// store. Checks read the suitability report's assertions from here rather than parsing
    /// the report again, so a run without one cannot proceed.
    /// </summary>
    private CanonicalModelDocument? _model;

    private readonly ICanonicalModelStore _modelStore;

    /// <summary>
    /// Where a finished run is filed. Read back by the report button, which is the only reason
    /// the archive is written at all: a run nobody keeps cannot be reopened.
    /// </summary>
    private readonly ICheckRunStore _runStore;

    private CostBreakdown _lastBreakdown;

    /// <summary>
    /// Everything this screen drives arrives through the constructor, so the form knows what the
    /// pipeline does and nothing about how it is built. Swapping this screen for a web endpoint
    /// means resolving the same set from the same container.
    /// </summary>
    public CheckEvaluatorForm(
        AppSettings settings,
        IChatCompletionClient chat,
        ICanonicalModelStore modelStore,
        ICheckRunStore runStore,
        ICanonicalModelExtractor extractor,
        ICaseDocumentStoreFactory stores,
        ICaseDocumentSearchServiceFactory searches,
        ICheckPlanRunnerFactory runners,
        IPromptLogWriterFactory promptLogs,
        Func<MainForm> mainForm)
    {
        InitializeComponent();
        _settings = settings;
        _chat = chat;
        _modelStore = modelStore;
        _runStore = runStore;
        _extractor = extractor;
        _stores = stores;
        _searches = searches;
        _runners = runners;
        _promptLogs = promptLogs;
        _mainForm = mainForm;

        caseFolderTextBox.Text = _settings.DocumentFolder;
        bypassCacheCheckBox.Checked = _settings.BypassResponseCache;

        _lastBreakdown = CostBreakdown.Empty(_settings.SelectedModel);
        ShowBreakdown(_lastBreakdown);
        UpdateRunAvailability();

        Shown += (_, _) => BeginInvoke(() =>
        {
            mainSplit.Panel1MinSize = 200;
            mainSplit.Panel2MinSize = 400;
            mainSplit.SplitterDistance = (int)(mainSplit.Width * 0.25);

            rightSplit.Panel1MinSize = 180;
            rightSplit.Panel2MinSize = 160;
            rightSplit.SplitterDistance = (int)(rightSplit.Height * 0.55);

            detailDocSplit.Panel1MinSize = 200;
            detailDocSplit.Panel2MinSize = 160;
            detailDocSplit.SplitterDistance = (int)(detailDocSplit.Width * 0.62);

            // A case indexed in an earlier session is still in Qdrant, and its canonical
            // model is still in SQLite — find out before the user is told to redo either.
            _ = DetectExistingIndexAsync();
            _ = DetectExistingModelAsync();
        });

        // Restore last-used CSV path from settings if present
        if (!string.IsNullOrWhiteSpace(_settings.LastChecksCsvPath))
        {
            csvPathTextBox.Text = _settings.LastChecksCsvPath;
            TryLoadCsv(_settings.LastChecksCsvPath);
        }
    }

    // ──────────────────────────────────────────────
    // CSV loading
    // ──────────────────────────────────────────────

    private void BrowseCsvButton_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select Assessment Checks CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
        };

        if (!string.IsNullOrWhiteSpace(csvPathTextBox.Text) &&
            File.Exists(csvPathTextBox.Text))
        {
            dlg.InitialDirectory = Path.GetDirectoryName(csvPathTextBox.Text);
        }

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            csvPathTextBox.Text = dlg.FileName;
            TryLoadCsv(dlg.FileName);
        }
    }

    private void TryLoadCsv(string path)
    {
        try
        {
            _checks = AssessmentCheckLoader.Load(path);
            _settings.LastChecksCsvPath = path;
            PopulateChecksList();
            statusLabel.Text = $"{_checks.Count} checks loaded.";
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Failed to load CSV: {ex.Message}";
        }
    }

    private void PopulateChecksList()
    {
        checksListView.BeginUpdate();
        checksListView.Items.Clear();
        foreach (var check in _checks)
        {
            // Column 0 is the run status glyph, so the item's own text is that rather than the
            // check id. UseItemStyleForSubItems lets the glyph be coloured without tinting the
            // whole row, which would make a list of ten checks hard to read.
            var item = new ListViewItem(string.Empty) { UseItemStyleForSubItems = false };
            item.SubItems.Add(check.CheckId.Replace("\n", "").Trim());
            item.SubItems.Add(check.CheckName);
            item.SubItems.Add(check.CategoryCodes.Count > 0
                ? string.Join(", ", check.CategoryCodes)
                : "-");
            item.Tag = check;
            checksListView.Items.Add(item);
        }
        checksListView.EndUpdate();
    }

    // ──────────────────────────────────────────────
    // Per-check run status
    // ──────────────────────────────────────────────

    /// <summary>
    /// How a check's state shows in the list. Glyphs rather than an ImageList: they carry
    /// meaning at a glance, scale with the user's font, and need no assets.
    /// </summary>
    private static readonly Color RunningColour = Color.FromArgb(0, 102, 204);
    private static readonly Color PassColour = Color.FromArgb(0, 128, 0);
    private static readonly Color ConcernColour = Color.FromArgb(176, 96, 0);
    private static readonly Color ErrorColour = Color.FromArgb(180, 0, 0);

    private void SetCheckStatus(
        AssessmentCheck check, string glyph, Color colour, string tooltip, bool scrollIntoView = false)
    {
        foreach (ListViewItem item in checksListView.Items)
        {
            if (!ReferenceEquals(item.Tag, check))
            {
                continue;
            }

            item.SubItems[0].Text = glyph;
            item.SubItems[0].ForeColor = colour;
            item.ToolTipText = tooltip;

            // A long list scrolls past the visible rows, so the check being worked on is brought
            // into view — but only when it starts, never on the progress ticks within it, or the
            // list would jerk under the reader a dozen times per check.
            if (scrollIntoView)
            {
                item.EnsureVisible();
            }

            return;
        }
    }

    /// <summary>Clears every glyph, so a second run does not read as the first one's results.</summary>
    private void ResetCheckStatuses()
    {
        checksListView.BeginUpdate();

        foreach (ListViewItem item in checksListView.Items)
        {
            item.SubItems[0].Text = string.Empty;
            item.SubItems[0].ForeColor = checksListView.ForeColor;
            item.ToolTipText = string.Empty;
        }

        checksListView.EndUpdate();
    }

    /// <summary>Waiting its turn — distinct from "not part of this run", which stays blank.</summary>
    private void MarkQueued(IEnumerable<AssessmentCheck> checks)
    {
        foreach (var check in checks)
        {
            SetCheckStatus(check, "·", SystemColors.GrayText, "Queued");
        }
    }

    private void MarkRunning(AssessmentCheck check, string detail, bool scrollIntoView = false) =>
        SetCheckStatus(check, "▶", RunningColour, detail, scrollIntoView);

    /// <summary>
    /// The finished state. Indeterminate gets its own glyph rather than borrowing the pass one:
    /// a requirement nobody could assess is not a requirement that passed.
    /// </summary>
    private void MarkFinished(AssessmentCheck check, CheckFinding finding)
    {
        var (glyph, colour) = finding.ParsedOutcome switch
        {
            CheckOutcome.NoIssue => ("✓", PassColour),
            CheckOutcome.PotentialConcern => ("!", ConcernColour),
            CheckOutcome.NotApplicable => ("–", SystemColors.GrayText),
            CheckOutcome.Indeterminate => ("?", ConcernColour),
            _ => ("✕", ErrorColour),
        };

        SetCheckStatus(
            check, glyph, colour,
            $"{CheckFinding.Describe(finding.ParsedOutcome)} — {finding.Groups.Count} requirement(s), "
            + $"{finding.Elapsed.TotalSeconds:0.0}s");
    }

    private void MarkSkipped(AssessmentCheck check, string why) =>
        SetCheckStatus(check, "–", SystemColors.GrayText, why);

    // ──────────────────────────────────────────────
    // Case folder
    // ──────────────────────────────────────────────

    private void BrowseCaseFolderButton_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select case folder (contains A, B, C... sub-folders)",
            UseDescriptionForTitle = true,
        };

        if (Directory.Exists(caseFolderTextBox.Text))
        {
            dlg.SelectedPath = caseFolderTextBox.Text;
        }

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            caseFolderTextBox.Text = dlg.SelectedPath;
            // A different folder is a different case — whatever is indexed or extracted no
            // longer applies until the new case has been probed for both.
            _indexedCase = null;
            _model = null;
            UpdateRunAvailability();
            _ = DetectExistingIndexAsync();
            _ = DetectExistingModelAsync();
        }
    }

    /// <summary>
    /// Chunks every Markdown file under the case folder and writes it to the vector store,
    /// stamped with the case reference, tenant, document name and category. Runs then search
    /// those chunks instead of attaching whole documents.
    /// </summary>
    private async void LoadDocsButton_Click(object? sender, EventArgs e)
    {
        var caseFolder = caseFolderTextBox.Text.Trim();
        if (!Directory.Exists(caseFolder))
        {
            MessageBox.Show(this, "Please select a valid case folder first.", "No case folder",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.OpenAiApiKey))
        {
            MessageBox.Show(this, "Please configure an API key first.", "No API key",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var caseReference = _settings.ResolveCaseReference(caseFolder);

        SetBusy(true);
        statusLabel.Text = "Indexing documents...";

        responseTextBox.Clear();
        AppendResponseLine($"Indexing Markdown from {caseFolder}");
        AppendResponseLine($"Case reference: {caseReference}    Tenant: {_settings.TenantId}");
        AppendResponseLine(new string('-', 72));

        var elapsed = Stopwatch.StartNew();
        _cts = new CancellationTokenSource();

        // Outside the try: indexing a large case is the most expensive thing the app does,
        // and cancelling halfway through does not refund what has already been embedded.
        UsageTrackingEmbeddingGenerator? embeddings = null;

        using var store = _stores.Create();
        try
        {
            if (!await store.IsAvailableAsync(_cts.Token).ConfigureAwait(true))
            {
                statusLabel.Text = $"Qdrant is not responding at {store.Endpoint}.";
                AppendResponseLine(
                    $"Failed: no response from Qdrant at {store.Endpoint}. "
                    + "Start the container or correct the endpoint in Settings.");
                return;
            }

            // Wrapped so the chunker's own embedding calls are counted too: it embeds every
            // element of every document to find the cut points, which is the larger half of
            // what indexing actually costs.
            embeddings = new UsageTrackingEmbeddingGenerator(
                AiClientFactory.CreateEmbeddingGenerator(_settings));

            var indexer = new CaseDocumentIndexer(_settings, embeddings, store);

            AppendResponseLine($"Chunking: {indexer.ChunkingDescription}");
            AppendResponseLine($"Vector store: {store.Endpoint} collection \"{store.Collection}\"");
            AppendResponseLine(new string('-', 72));

            var progress = new Progress<CaseIndexProgress>(p =>
            {
                AppendResponseLine(DescribeIndexed(p));
                statusLabel.Text = $"Indexing {p.Done}/{p.Total}...";
            });

            var result = await indexer
                .IndexAsync(caseFolder, caseReference, progress, _cts.Token)
                .ConfigureAwait(true);

            elapsed.Stop();

            // Record the load the moment it succeeds. Everything below is reporting, and a
            // failure there must never throw away an index that is already written.
            _indexedCase = caseReference;

            var summary = SummariseLoad(result, elapsed.Elapsed);
            AppendResponseLine(new string('-', 72));
            AppendResponseLine(summary);

            // Indexing bills no chat tokens at all, so the whole cost of this run is the
            // embeddings — which is precisely what used to be reported as nothing.
            _lastBreakdown = CostBreakdown.Create(
                _settings.SelectedModel, TokenUsage.Empty, EmbeddingUsageOf(embeddings));
            ShowBreakdown(_lastBreakdown);
            AppendResponseLine(_lastBreakdown.FormatTotal());
            statusLabel.Text = summary;

            ReportSkippedAndFailed(result);

            // Refresh the document list so the indexed markers appear.
            if (checksListView.SelectedItems.Count > 0)
            {
                PopulateDocuments((AssessmentCheck)checksListView.SelectedItems[0].Tag!);
            }
        }
        catch (OperationCanceledException)
        {
            statusLabel.Text = "Indexing cancelled.";
            AppendResponseLine("Cancelled.");
            ReportPartialEmbeddingSpend(embeddings);
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Failed to index documents: {ex.Message}";
            AppendResponseLine($"Failed: {ex.Message}");
            ReportPartialEmbeddingSpend(embeddings);
        }
        finally
        {
            embeddings?.Dispose();
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    /// <summary>
    /// Shows what a stopped index had already spent. Cancelling does not refund the
    /// embeddings that were generated before the cancellation landed, and a run that
    /// reported nothing would leave the user thinking it had cost nothing.
    /// </summary>
    private void ReportPartialEmbeddingSpend(UsageTrackingEmbeddingGenerator? embeddings)
    {
        var usage = EmbeddingUsageOf(embeddings);
        if (!usage.IsPresent)
        {
            return;
        }

        _lastBreakdown = CostBreakdown.Create(_settings.SelectedModel, TokenUsage.Empty, usage);
        ShowBreakdown(_lastBreakdown);
        AppendResponseLine(_lastBreakdown.FormatTotal());
    }

    /// <summary>One line per indexed document: position, category, name, and how it was split.</summary>
    private static string DescribeIndexed(CaseIndexProgress p)
    {
        var category = string.IsNullOrEmpty(p.CategoryCode) ? "-" : p.CategoryCode;

        var outcome = p.Error is not null
            ? $"FAILED — {p.Error}"
            : $"{p.Chunks} chunk(s)";

        return $"[{p.Done,3}/{p.Total}] {category,-3} {p.DocumentName,-48} {outcome} ({p.Elapsed.TotalSeconds:0.0}s)";
    }

    private static string SummariseLoad(CaseIndexResult result, TimeSpan elapsed)
    {
        var summary = new StringBuilder(
            $"Ready: {result.Chunks:N0} chunks from {result.Documents} document(s) "
            + $"indexed for case {result.CaseReference} (tenant {result.TenantId}) in {elapsed.TotalSeconds:0.0}s");

        if (result.Failures.Count > 0)
        {
            summary.Append($", {result.Failures.Count} failed");
        }

        if (result.Skipped.Count > 0)
        {
            summary.Append($", {result.Skipped.Count} non-Markdown file(s) skipped");
        }

        return summary.Append('.').ToString();
    }

    private void ReportSkippedAndFailed(CaseIndexResult result)
    {
        if (result.Failures.Count > 0)
        {
            AppendResponseLine(string.Empty);
            AppendResponseLine($"{result.Failures.Count} document(s) could not be indexed:");
            foreach (var (document, error) in result.Failures)
            {
                AppendResponseLine($"  {document} — {error}");
            }
        }

        if (result.Skipped.Count > 0)
        {
            AppendResponseLine(string.Empty);
            AppendResponseLine(
                $"Skipped {result.Skipped.Count} file(s) that are not Markdown. Convert them to .md first "
                + "if their content needs to be searchable:");
            foreach (var name in result.Skipped.Take(25))
            {
                AppendResponseLine($"  {name}");
            }

            if (result.Skipped.Count > 25)
            {
                AppendResponseLine($"  ... and {result.Skipped.Count - 25} more");
            }
        }
    }

    private void AppendResponseLine(string text)
    {
        responseTextBox.AppendText(text + Environment.NewLine);
    }

    /// <summary>
    /// Asks the vector store whether this case is already indexed — from this session or a
    /// previous one — so "Run Check" is available without re-indexing after a restart.
    /// </summary>
    private async Task DetectExistingIndexAsync()
    {
        var caseFolder = caseFolderTextBox.Text.Trim();
        if (!Directory.Exists(caseFolder))
        {
            return;
        }

        var caseReference = _settings.ResolveCaseReference(caseFolder);

        try
        {
            using var store = _stores.Create();
            var count = await store.CountAsync(caseReference, _settings.TenantId).ConfigureAwait(true);

            if (count > 0)
            {
                _indexedCase = caseReference;
                statusLabel.Text = $"{count:N0} chunks already indexed for case {caseReference}.";
            }
        }
        catch
        {
            // Qdrant being unreachable is reported properly when the user loads or runs;
            // a failed probe just leaves the buttons in their "not loaded" state.
        }
        finally
        {
            UpdateRunAvailability();
        }
    }

    // ──────────────────────────────────────────────
    // Canonical model
    // ──────────────────────────────────────────────

    /// <summary>
    /// Parses the case's suitability report into the canonical model and stores it against
    /// the case reference and tenant.
    ///
    /// This is the only time the report is read by a model. Every check afterwards works
    /// from what is captured here, so a report of any template shape is normalised once and
    /// assessed many times.
    /// </summary>
    private async void ExtractModelButton_Click(object? sender, EventArgs e)
    {
        var caseFolder = caseFolderTextBox.Text.Trim();
        if (!Directory.Exists(caseFolder))
        {
            MessageBox.Show(this, "Please select a valid case folder first.", "No case folder",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.OpenAiApiKey))
        {
            MessageBox.Show(this, "Please configure an API key first.", "No API key",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var caseReference = _settings.ResolveCaseReference(caseFolder);

        // Re-extracting is how a bad parse is corrected, but it costs real tokens and
        // replaces a model the user may not realise is there.
        if (_model is not null &&
            MessageBox.Show(this,
                $"A canonical model for case {caseReference} was extracted on "
                + $"{_model.ExtractedAt:yyyy-MM-dd HH:mm}. Extract again and replace it?",
                "Model already extracted",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        SetBusy(true);
        statusLabel.Text = "Extracting the canonical model...";

        responseTextBox.Clear();
        AppendResponseLine($"Extracting the canonical model for case {caseReference} (tenant {_settings.TenantId})");
        AppendResponseLine($"Schema: {_settings.ResolveCanonicalSchemaPath()}");
        AppendResponseLine($"Store:  {_modelStore.DatabasePath}");

        var reportFiles = CanonicalModelExtractor.FindReportFiles(caseFolder);
        foreach (var file in reportFiles)
        {
            AppendResponseLine($"Report: {Path.GetFileName(file)}");
        }

        AppendResponseLine(new string('-', 72));

        var elapsed = Stopwatch.StartNew();
        _cts = new CancellationTokenSource();

        using var extractionLog = _promptLogs.Create(
            _settings.ResolvePromptLogFolder(), caseReference, DateTimeOffset.Now, filePrefix: "extract");

        // The extraction token cap is the setting most likely to explain a section that came
        // back short, so it is recorded before the first pass rather than reconstructed after.
        extractionLog.LogRunConfiguration(RunFingerprint.For(
            _settings, model: null, _settings.ResolveCheckPlanFolder(), planCount: 0,
            _settings.MaxPassagesPerGroup));

        AppendResponseLine($"Logging extraction to {extractionLog.FilePath}");

        if (_settings.BypassResponseCache)
        {
            // Worth saying before the passes start rather than after the bill arrives: this
            // is the setting that turns twelve cheap replayed sections into twelve generated
            // ones, and an extraction that suddenly costs full price with no explanation on
            // screen is how the last one came to be misread.
            AppendResponseLine(
                "Response cache bypassed — every section carries a run marker, so nothing can "
                + "be replayed from an earlier extraction. Expect the full input cost.");
        }

        try
        {
            var extractor = _extractor;

            var progress = new Progress<ExtractionProgress>(p =>
            {
                AppendResponseLine(
                    $"[{p.Done,2}/{p.Total}] {p.SectionName,-36} {p.Describe()} ({p.Elapsed.TotalSeconds:0.0}s)");
                statusLabel.Text = $"Extracting {p.Done}/{p.Total}...";
            });

            var result = await extractor
                .ExtractAsync(caseFolder, caseReference, progress, extractionLog, _cts.Token)
                .ConfigureAwait(true);

            await _modelStore.SaveAsync(result.Document, _cts.Token).ConfigureAwait(true);

            extractionLog.LogCanonicalModel(result.Document, result.Failures);

            elapsed.Stop();
            _model = result.Document;

            AppendResponseLine(new string('-', 72));

            var accessor = new CanonicalModelAccessor(result.Document.Json);
            AppendResponseLine(
                $"Stored {result.Document.JsonLength:N0} characters of canonical model for case {caseReference} "
                + $"in {elapsed.Elapsed.TotalSeconds:0.0}s.");
            AppendResponseLine($"Sections populated: {string.Join(", ", accessor.PopulatedSections)}");

            // Vocabulary drift used to be silent. objectiveType is documented Pension /
            // Investment / ..., extraction wrote "RetirementObjective", and nothing said so —
            // which is exactly how a rule that reads the field by value comes to match nothing.
            if (result.VocabularyCorrections.Count > 0)
            {
                var corrected = result.VocabularyCorrections.Where(c => c.WasMapped).ToList();
                var unmapped = result.VocabularyCorrections.Where(c => !c.WasMapped).ToList();

                AppendResponseLine(string.Empty);

                if (corrected.Count > 0)
                {
                    AppendResponseLine($"{corrected.Count} value(s) corrected to the documented vocabulary:");
                    foreach (var correction in corrected)
                    {
                        AppendResponseLine($"  {correction}");
                    }
                }

                if (unmapped.Count > 0)
                {
                    AppendResponseLine(
                        $"{unmapped.Count} value(s) are outside the documented vocabulary and were left as written:");
                    foreach (var correction in unmapped)
                    {
                        AppendResponseLine($"  {correction}");
                    }

                    AppendResponseLine(
                        "Nothing was discarded. A check matching one of these fields by value will "
                        + "not see them, so either the report uses a word the model does not have "
                        + "or the extraction invented one.");
                }
            }

            if (result.Failures.Count > 0)
            {
                AppendResponseLine(string.Empty);
                AppendResponseLine($"{result.Failures.Count} section(s) failed and are missing from the model:");
                foreach (var (section, error) in result.Failures)
                {
                    AppendResponseLine($"  {section} — {error}");
                }

                AppendResponseLine(
                    "Extract again to retry them; checks that rely on a missing section will report "
                    + "the data as absent.");
            }

            _lastBreakdown = result.Breakdown;
            ShowBreakdown(_lastBreakdown);
            statusLabel.Text =
                $"Canonical model stored for case {caseReference} ({result.Breakdown.Usage.TotalTokens:N0} tokens).";
        }
        catch (OperationCanceledException)
        {
            statusLabel.Text = "Extraction cancelled.";
            AppendResponseLine("Cancelled — nothing was stored.");
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Extraction failed: {ex.Message}";
            AppendResponseLine($"Failed: {ex.Message}");
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    /// <summary>Deletes the stored canonical model for the current case.</summary>
    private async void DeleteModelButton_Click(object? sender, EventArgs e)
    {
        var caseFolder = caseFolderTextBox.Text.Trim();
        var caseReference = _settings.ResolveCaseReference(
            Directory.Exists(caseFolder) ? caseFolder : string.Empty);

        if (string.IsNullOrWhiteSpace(caseReference))
        {
            statusLabel.Text = "No case is selected, so there is no model to delete.";
            return;
        }

        if (MessageBox.Show(this,
                $"Delete the stored canonical model for case {caseReference} (tenant {_settings.TenantId})?\n\n"
                + "The indexed document chunks are not affected. Extracting again re-reads the "
                + "suitability report and costs tokens.",
                "Delete canonical model",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
        {
            return;
        }

        SetBusy(true);

        try
        {
            var deleted = await _modelStore
                .DeleteAsync(caseReference, _settings.TenantId)
                .ConfigureAwait(true);

            _model = null;

            statusLabel.Text = deleted
                ? $"Deleted the canonical model for case {caseReference} (tenant {_settings.TenantId})."
                : $"No canonical model was stored for case {caseReference} (tenant {_settings.TenantId}).";
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Could not delete the model: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Looks for a model extracted in an earlier session, so the buttons and the run path
    /// reflect what is actually in the store rather than what this instance has done.
    /// </summary>
    private async Task DetectExistingModelAsync()
    {
        var caseFolder = caseFolderTextBox.Text.Trim();
        if (!Directory.Exists(caseFolder))
        {
            return;
        }

        var caseReference = _settings.ResolveCaseReference(caseFolder);

        try
        {
            _model = await _modelStore.LoadAsync(caseReference, _settings.TenantId).ConfigureAwait(true);

            if (_model is not null)
            {
                statusLabel.Text =
                    $"Canonical model for case {caseReference} extracted {_model.ExtractedAt:yyyy-MM-dd HH:mm} "
                    + $"from {string.Join(", ", _model.SourceDocuments)}.";
            }
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Could not read the canonical model store: {ex.Message}";
        }
        finally
        {
            UpdateRunAvailability();
        }
    }

    /// <summary>The indexed case reference, but only while it still matches the case folder in the text box.</summary>
    private string? CurrentCaseReference()
    {
        var caseFolder = caseFolderTextBox.Text.Trim();
        if (!Directory.Exists(caseFolder) || _indexedCase is null)
        {
            return null;
        }

        var caseReference = _settings.ResolveCaseReference(caseFolder);
        return string.Equals(caseReference, _indexedCase, StringComparison.OrdinalIgnoreCase)
            ? caseReference
            : null;
    }

    // ──────────────────────────────────────────────
    // Check selection
    // ──────────────────────────────────────────────

    private void ChecksListView_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (checksListView.SelectedItems.Count == 0)
        {
            return;
        }

        var check = (AssessmentCheck)checksListView.SelectedItems[0].Tag!;
        ShowCheckDetails(check);
        PopulateDocuments(check);
        responseTextBox.Clear();
        statusLabel.Text = string.Empty;
    }

    private void ShowCheckDetails(AssessmentCheck check)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Check ID:  {check.CheckId.Replace("\n", "").Trim()}");
        sb.AppendLine($"Name:      {check.CheckName}");
        sb.AppendLine($"Applies To: {check.AppliesTo}");
        if (!string.IsNullOrWhiteSpace(check.RegulatoryBasis))
        {
            sb.AppendLine($"Regulatory: {check.RegulatoryBasis}");
        }
        sb.AppendLine();
        sb.AppendLine("── Prompt ──────────────────────────────");
        sb.AppendLine(check.Prompt);
        sb.AppendLine();
        sb.AppendLine("── What to Look For ────────────────────");
        sb.AppendLine(check.WhatToLookFor);
        sb.AppendLine();
        sb.AppendLine("── Decision Logic ──────────────────────");
        sb.AppendLine(check.DecisionLogic);
        checkDetailsTextBox.Text = sb.ToString();
    }

    /// <summary>
    /// Lists the case-folder documents in this check's categories. It is a view of what the
    /// search can reach, not a selection: a run searches the whole indexed case, because the
    /// evidence for a check often sits in a document filed under another category.
    /// </summary>
    private void PopulateDocuments(AssessmentCheck check)
    {
        documentsCheckedListBox.Items.Clear();

        var caseFolder = caseFolderTextBox.Text.Trim();
        if (!Directory.Exists(caseFolder))
        {
            return;
        }

        var indexed = CurrentCaseReference() is not null;
        var files = AssessmentCheckLoader.GetFilesForCodes(caseFolder, check.CategoryCodes);

        foreach (var file in files.OrderBy(f => f))
        {
            // Display as "CODE / filename". A tick marks Markdown that is in the index; other
            // formats are flagged, since their content is not searchable until converted.
            var code = Path.GetFileName(Path.GetDirectoryName(file)) ?? string.Empty;
            var isMarkdown = CaseDocumentIndexer.IsIndexable(file);

            var marker = isMarkdown && indexed ? "✓ " : isMarkdown ? string.Empty : "✗ ";
            var suffix = isMarkdown ? string.Empty : "  (not Markdown — not indexed)";

            documentsCheckedListBox.Items.Add(
                new DocumentItem($"{marker}[{code}]  {Path.GetFileName(file)}{suffix}", file),
                isMarkdown && indexed);
        }
    }

    // ──────────────────────────────────────────────
    // Run
    // ──────────────────────────────────────────────

    private Task RunButton_ClickAsync() =>
        checksListView.SelectedItems.Count == 0
            ? ShowNoSelectionAsync()
            : RunChecksAsync([(AssessmentCheck)checksListView.SelectedItems[0].Tag!]);

    private async void RunButton_Click(object? sender, EventArgs e) => await RunButton_ClickAsync();

    /// <summary>Runs every loaded check, in CSV order, into one consolidated report.</summary>
    private async void RunAllButton_Click(object? sender, EventArgs e)
    {
        if (_checks.Count == 0)
        {
            MessageBox.Show(this, "Load an assessment checks CSV first.", "No checks loaded",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await RunChecksAsync(_checks);
    }

    private Task ShowNoSelectionAsync()
    {
        MessageBox.Show(this, "Please select a check to run.", "No check selected",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Assesses one or more checks from their query plans and writes a consolidated findings
    /// report.
    ///
    /// The suitability report is not read here. Its assertions come from the stored canonical
    /// model, and only the supporting evidence is retrieved — which is why a run costs the
    /// same whether it covers one check or ten, plus one decision call each.
    /// </summary>
    private async Task RunChecksAsync(IReadOnlyList<AssessmentCheck> checks)
    {
        if (string.IsNullOrWhiteSpace(_settings.OpenAiApiKey))
        {
            MessageBox.Show(this, "Please configure an API key first.", "No API key",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var caseReference = CurrentCaseReference();
        if (caseReference is null)
        {
            MessageBox.Show(this,
                "Index the case documents first — click \"Load Docs\" to chunk them into the vector store.",
                "Documents not indexed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_model is null)
        {
            MessageBox.Show(this,
                "Extract the canonical model first — click \"Extract Model\" to parse the suitability "
                + "report into the store.\n\nChecks read what the report asserts from that model rather "
                + "than parsing the report again.",
                "No canonical model", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var planFolder = _settings.ResolveCheckPlanFolder();
        var (plans, planFailures) = CheckQueryPlanLoader.Load(planFolder);

        if (plans.Count == 0)
        {
            MessageBox.Show(this,
                $"No query plans were found in \"{planFolder}\".\n\nSet the check plan folder in "
                + "Settings, or place the CHK-*.query-plan.json files beside the executable.",
                "No query plans", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        responseTextBox.Clear();
        ResetCheckStatuses();
        AppendResponseLine($"Assessing case {caseReference} (tenant {_settings.TenantId})");
        AppendResponseLine(
            $"Canonical model extracted {_model.ExtractedAt:yyyy-MM-dd HH:mm} from "
            + $"{string.Join(", ", _model.SourceDocuments)}");
        AppendResponseLine($"Query plans: {plans.Count} loaded from {planFolder}");
        AppendResponseLine(
            "Status column:  ·  queued    ▶  running    ✓  no issue    !  potential concern    "
            + "?  not assessable    –  N/A or skipped    ✕  error");

        foreach (var (file, error) in planFailures)
        {
            AppendResponseLine($"  Plan skipped — {file}: {error}");
        }

        AppendResponseLine(new string('-', 72));

        _cts = new CancellationTokenSource();
        var usage = TokenUsage.Empty;
        var runStartedAt = DateTimeOffset.Now;

        // Declared outside the try so a cancelled or failed run can still report the
        // embeddings it had already paid for by the time it stopped.
        UsageTrackingEmbeddingGenerator? embeddings = null;
        PromptLogWriter? promptLog = null;

        // Declared outside the try so a run that stops early can mark the checks that were
        // still in flight — with several running at once, there is no single "current" one.
        CheckRunBoard? board = null;

        // The archive is written from the same findings the report is built from, so a report
        // opened tomorrow says what the screen said today.
        var recorder = CheckRunRecorder.ForRun(runStartedAt, caseReference);
        var bypass = PromptCacheBypass.For(_settings.BypassResponseCache);

        using var store = _stores.Create();
        try
        {
            promptLog = _promptLogs.Create(
                _settings.ResolvePromptLogFolder(), caseReference, runStartedAt, filePrefix: "checks");

            // Written before the first prompt rather than with the report at the end, so a run
            // that is cancelled or fails still says what it was configured to do.
            promptLog.LogRunConfiguration(RunFingerprint.For(
                _settings, _model, planFolder, plans.Count, _settings.MaxPassagesPerGroup));

            AppendResponseLine($"Logging prompts to {promptLog.FilePath}");
            AppendResponseLine($"Archiving run {recorder.RunId} to {_runStore.DatabasePath}");

            if (bypass.IsEnabled)
            {
                AppendResponseLine(
                    "Response cache bypassed — every prompt carries a run marker, so nothing can "
                    + "be answered from an earlier run. Input tokens will be higher than usual.");
            }

            // Each planned search embeds its text, so a run over ten checks is a few hundred
            // small embedding calls alongside the chat spend.
            embeddings = new UsageTrackingEmbeddingGenerator(
                AiClientFactory.CreateEmbeddingGenerator(_settings));

            var searchTool = _searches.Create(caseReference, store, embeddings);

            // One budget for the whole run. Checks and their requirements both fan out, and
            // bounding each level separately would multiply into a request count neither
            // setting names.
            using var modelCalls = new ConcurrencyGate(_settings.MaxParallelRequests);
            using var searches = new ConcurrencyGate(_settings.MaxParallelRequests);
            using var runner = _runners.Create(
                _model, searchTool, promptLog, modelCalls, searches, recorder);

            var skipped = new List<string>();

            // Only the checks in this run are marked, so a single-check run leaves the other
            // rows blank rather than implying they were queued and cleared.
            MarkQueued(checks);

            board = new CheckRunBoard(checks);
            responseTextBox.Text = board.Render();

            // Every UI update from here is funnelled through IProgress, which posts to the
            // thread that created it. The work below runs on the pool, and a ListView touched
            // from there is a crash rather than a glitch.
            // Set once the run is over. Progress callbacks are posted to the UI message queue,
            // so one queued by the last check can still be waiting when the findings are
            // written — and would paint the progress board back over them.
            var settled = false;

            // Wall clock for the whole run. Read back against the output tokens to tell a
            // generated run from a gateway cache replay, which is otherwise indistinguishable
            // from one — see RunAuthenticity.
            var runClock = Stopwatch.StartNew();

            var ui = new Progress<Action>(update =>
            {
                if (!settled)
                {
                    update();
                }
            });

            void Post(Action update) => ((IProgress<Action>)ui).Report(update);

            void Refresh(AssessmentCheck check)
            {
                var row = board.Row(check);

                Post(() =>
                {
                    responseTextBox.Text = board.Render();
                    statusLabel.Text = board.Headline;

                    switch (row.State)
                    {
                        case CheckRunState.Running:
                            MarkRunning(check, row.Detail);
                            break;
                        case CheckRunState.Finished when row.Finding is not null:
                            MarkFinished(check, row.Finding);
                            break;
                        case CheckRunState.Skipped:
                            MarkSkipped(check, row.Detail);
                            break;
                    }
                });
            }

            var findingsByIndex = new CheckFinding?[checks.Count];

            // Checks run concurrently. They share nothing: each reads the same canonical model
            // and the same vector store, and writes only its own slot. What made them sequential
            // was never a dependency, only the shape of the loop.
            await ParallelWork.ForEachAsync(
                checks.Count,
                Math.Max(1, _settings.MaxParallelChecks),
                async (i, token) =>
                {
                    var check = checks[i];
                    var checkId = CheckQueryPlanLoader.NormaliseCheckId(check.CheckId);

                    // A check with no plan is reported rather than improvised: the whole point
                    // of the plan is that retrieval is decided in advance, not by the model.
                    if (!plans.TryGetValue(checkId, out var plan))
                    {
                        lock (skipped)
                        {
                            skipped.Add($"{checkId} — no query plan in {planFolder}");
                        }

                        promptLog.LogSkipped(checkId, $"No query plan in {planFolder}");
                        board.Skip(check, $"No query plan in {planFolder}");
                        Refresh(check);
                        return;
                    }

                    board.Start(check);
                    Refresh(check);

                    var stages = new Progress<string>(stage =>
                    {
                        board.Progress(check, stage);
                        Refresh(check);
                    });

                    var finding = await runner.RunAsync(check, plan, stages, token).ConfigureAwait(false);

                    findingsByIndex[i] = finding;
                    board.Finish(check, finding);
                    Refresh(check);
                },
                _cts.Token).ConfigureAwait(true);

            // Collected by position, so the report reads in the order the checks were listed
            // however the run happened to interleave them.
            settled = true;
            runClock.Stop();

            var findings = findingsByIndex.Where(f => f is not null).Select(f => f!).ToList();
            usage = findings.Aggregate(TokenUsage.Empty, (total, f) => AddUsage(total, f.Usage));

            var report = new FindingsReport(
                caseReference, _settings.TenantId, _settings.SelectedModel,
                DateTimeOffset.Now, findings, _model,
                RunFingerprint.For(
                    _settings, _model, planFolder, plans.Count, _settings.MaxPassagesPerGroup),
                runClock.Elapsed);

            // Written before the report is rendered, so a failure to save is reported beside the
            // findings rather than after the user has already read them and moved on.
            await SaveRunAsync(
                    recorder, caseReference, runStartedAt, report, bypass, planFolder, plans.Count, checks)
                .ConfigureAwait(true);

            responseTextBox.Text = report.Format();

            if (skipped.Count > 0)
            {
                AppendResponseLine(string.Empty);
                AppendResponseLine($"{skipped.Count} check(s) had no query plan and were not assessed:");
                foreach (var line in skipped)
                {
                    AppendResponseLine($"  {line}");
                }
            }

            _lastBreakdown = CostBreakdown.Create(
                _settings.SelectedModel, usage, EmbeddingUsageOf(embeddings));
            ShowBreakdown(_lastBreakdown);
            statusLabel.Text =
                $"Done. {report.Headline}. {_lastBreakdown.TotalTokens:N0} tokens billed.";
        }
        catch (OperationCanceledException)
        {
            // A cancelled run has still spent whatever it spent before stopping, so the
            // breakdown carries the searches that did run rather than resetting to zero.
            _lastBreakdown = CostBreakdown.Create(
                _settings.SelectedModel, usage, EmbeddingUsageOf(embeddings));
            ShowBreakdown(_lastBreakdown);
            statusLabel.Text = "Cancelled.";
            AppendResponseLine("Cancelled.");

            foreach (var check in board?.StopOutstanding("Cancelled before this check finished")
                                  ?? [])
            {
                MarkSkipped(check, "Cancelled before this check finished");
            }

            if (board is not null)
            {
                responseTextBox.Text = board.Render();
            }
        }
        catch (Exception ex)
        {
            responseTextBox.Text = $"Error: {ex.Message}";
            _lastBreakdown = CostBreakdown.Create(
                _settings.SelectedModel, usage, EmbeddingUsageOf(embeddings));
            ShowBreakdown(_lastBreakdown);
            statusLabel.Text = "The run failed.";
        }
        finally
        {
            embeddings?.Dispose();
            promptLog?.Dispose();
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    /// <summary>
    /// What a tracker actually recorded, or nothing when the run never got as far as
    /// creating one. Null means no embeddings were generated, which is different from
    /// embeddings that reported no usage.
    /// </summary>
    private EmbeddingUsage EmbeddingUsageOf(UsageTrackingEmbeddingGenerator? embeddings) =>
        embeddings is null || embeddings.Calls == 0
            ? EmbeddingUsage.None
            : new EmbeddingUsage(_settings.EmbeddingModel, embeddings.TotalTokens, embeddings.UsageReported);

    private static TokenUsage AddUsage(TokenUsage a, TokenUsage b) => new(
        a.InputTokens + b.InputTokens,
        a.OutputTokens + b.OutputTokens,
        a.CacheWriteTokens + b.CacheWriteTokens,
        a.CacheReadTokens + b.CacheReadTokens);

    /// <summary>
    /// Files the run, and says so on the report rather than silently.
    ///
    /// A failure to archive does not fail the run: the findings on screen are the run's output
    /// and are unaffected by whether a copy was kept. It is reported plainly, because the report
    /// button will not find this run afterwards and the reason should not have to be guessed.
    /// </summary>
    private async Task SaveRunAsync(
        CheckRunRecorder recorder,
        string caseReference,
        DateTimeOffset startedAt,
        FindingsReport report,
        PromptCacheBypass bypass,
        string planFolder,
        int planCount,
        IReadOnlyList<AssessmentCheck> checks)
    {
        try
        {
            var record = recorder.Build(
                caseReference,
                _settings.TenantId,
                _settings.SelectedModel,
                startedAt,
                DateTimeOffset.Now,
                RunFingerprint.For(_settings, _model, planFolder, planCount, _settings.MaxPassagesPerGroup),
                bypass,
                _model,
                report.Findings,
                checks);

            await _runStore.SaveAsync(record).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendResponseLine(string.Empty);
            AppendResponseLine(
                $"The run finished but could not be archived: {ex.Message}. The findings above "
                + "are unaffected; the compliance report will not be able to reopen this run.");
        }
    }

    /// <summary>
    /// Writes the extracted canonical model to a file the user chooses.
    ///
    /// The model is the assertion side of every check — the whole reason the suitability report
    /// is never sent to an assessor a second time — and until now the only way to read it was to
    /// open a SQLite file. A check that says the report asserts something is unanswerable without
    /// it.
    /// </summary>
    private async void SaveModelButton_Click(object? sender, EventArgs e)
    {
        if (_model is null)
        {
            MessageBox.Show(this,
                "No canonical model has been extracted for this case yet.",
                "Nothing to save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save the canonical model",
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"canonical-model_{Sanitise(_model.CaseReference)}_"
                       + $"{_model.ExtractedAt:yyyyMMdd-HHmmss}.json",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            // Re-indented on the way out. It is stored compactly because it is machine input;
            // it is saved to be read, and a 440,000-character single line is not readable.
            await File.WriteAllTextAsync(dialog.FileName, Indent(_model.Json)).ConfigureAwait(true);

            statusLabel.Text = $"Canonical model written to {dialog.FileName}";
            AppendResponseLine(
                $"Canonical model for {_model.CaseReference} written to {dialog.FileName} "
                + $"({_model.JsonLength:N0} characters, extracted {_model.ExtractedAt:yyyy-MM-dd HH:mm}).");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save the model",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// Builds the compliance report for the most recent archived run of this case and opens it.
    ///
    /// Read back from the archive rather than held from the run that produced it, deliberately:
    /// that is what makes the button work in a session that did not do the run, and it is the
    /// only way the report can be trusted to show what was stored rather than what is still in
    /// memory.
    /// </summary>
    private async void SaveReportButton_Click(object? sender, EventArgs e)
    {
        var caseReference = CurrentCaseReference()
                            ?? (Directory.Exists(caseFolderTextBox.Text.Trim())
                                ? _settings.ResolveCaseReference(caseFolderTextBox.Text.Trim())
                                : null);

        if (caseReference is null)
        {
            MessageBox.Show(this,
                "Select a case folder first — the report is built from that case's archived runs.",
                "No case", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var run = await _runStore
                .LoadLatestAsync(caseReference, _settings.TenantId)
                .ConfigureAwait(true);

            if (run is null)
            {
                MessageBox.Show(this,
                    $"No archived run was found for case {caseReference}.\n\nRun the checks first; "
                    + "every run is archived as it finishes, and the report is built from the "
                    + "archive rather than from the screen.",
                    "Nothing to report on", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Title = "Save the compliance report",
                Filter = "HTML (*.html)|*.html|All files (*.*)|*.*",
                FileName = $"compliance-report_{Sanitise(run.CaseReference)}_"
                           + $"{run.StartedAt:yyyyMMdd-HHmmss}.html",
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var html = ComplianceReportHtml.Render(run);
            await File.WriteAllTextAsync(dialog.FileName, html, Encoding.UTF8).ConfigureAwait(true);

            statusLabel.Text = $"Compliance report written to {dialog.FileName}";
            AppendResponseLine(
                $"Compliance report for run {run.RunId} written to {dialog.FileName} "
                + $"({run.Checks.Count} check(s), {run.AllGroups.Count()} requirement(s), "
                + $"{run.AllGroups.Sum(g => g.Passages.Count):N0} archived passage(s)).");

            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not build the report",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// The toggle writes straight through to settings, so it survives to the next screen and
    /// the next launch. It is a run option that happens to be persisted, not a dialog field.
    /// </summary>
    private void BypassCacheCheckBox_CheckedChanged(object? sender, EventArgs e) =>
        _settings.BypassResponseCache = bypassCacheCheckBox.Checked;

    /// <summary>Re-indents stored JSON for reading, or returns it unchanged if it will not parse.</summary>
    private static string Indent(string json)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return System.Text.Json.JsonSerializer.Serialize(
                document.RootElement,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (System.Text.Json.JsonException)
        {
            return json;
        }
    }

    /// <summary>A case reference safe to put in a file name.</summary>
    private static string Sanitise(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "case" : cleaned;
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// Clears this case's chunks from the vector store, so the next "Load Docs" indexes
    /// everything fresh. The rows are found by case reference and tenant, which means a case
    /// indexed in an earlier session can still be cleared.
    /// </summary>
    private async void UnloadDocsButton_Click(object? sender, EventArgs e)
    {
        var caseFolder = caseFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(_settings.CaseReference) && !Directory.Exists(caseFolder))
        {
            statusLabel.Text =
                "Select the case folder whose index should be cleared, or set a case reference in Settings.";
            return;
        }

        var caseReference = _settings.ResolveCaseReference(caseFolder);
        var tenantId = _settings.TenantId;

        if (MessageBox.Show(this,
                $"Delete the embeddings indexed for case {caseReference}, tenant {tenantId}?\n\n"
                + $"They will be removed from the \"{_settings.ResolveCollection()}\" collection. "
                + "Other cases and other tenants are untouched, and the next \"Load Docs\" indexes this "
                + "case again.",
                "Unload documents", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true);
        statusLabel.Text = "Deleting embeddings...";

        _cts = new CancellationTokenSource();

        using var store = _stores.Create();
        try
        {
            var before = await store.CountAsync(caseReference, tenantId, _cts.Token).ConfigureAwait(true);
            await store.DeleteCaseAsync(caseReference, tenantId, _cts.Token).ConfigureAwait(true);

            // Deletion is by payload filter, so confirm the filter actually emptied the case
            // rather than reporting success on the strength of the call not throwing.
            var after = await store.CountAsync(caseReference, tenantId, _cts.Token).ConfigureAwait(true);

            _indexedCase = null;
            UpdateRunAvailability();
            responseTextBox.Clear();
            ShowBreakdown(CostBreakdown.Empty(_settings.SelectedModel));

            statusLabel.Text = (before, after) switch
            {
                (0, _) => $"Nothing was indexed for case {caseReference} (tenant {tenantId}).",
                (_, 0) => $"Deleted {before:N0} embedding(s) for case {caseReference} (tenant {tenantId}).",
                _ => $"Deleted {before - after:N0} of {before:N0} embedding(s) for case {caseReference} "
                   + $"(tenant {tenantId}); {after:N0} remain.",
            };
        }
        catch (OperationCanceledException)
        {
            statusLabel.Text = "Unload cancelled.";
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Failed to clear the index: {ex.Message}";
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    private void CopyResponseButton_Click(object? sender, EventArgs e)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Model: {_lastBreakdown.ModelId}");
        sb.AppendLine(_lastBreakdown.FormatTotal());
        sb.AppendLine();
        sb.AppendLine(responseTextBox.Text);

        Clipboard.SetText(sb.ToString());
        statusLabel.Text = "Response and cost copied to clipboard.";
    }


    private void ShowBreakdown(CostBreakdown breakdown)
    {
        costListView.BeginUpdate();
        try
        {
            costListView.Items.Clear();
            foreach (var line in breakdown.Lines)
            {
                var item = new ListViewItem(line.Component);
                item.SubItems.Add(line.Tokens.ToString("N0"));
                item.SubItems.Add(line.RatePerMillion.ToString("0.####"));
                item.SubItems.Add(line.Cost.ToString("C4"));
                costListView.Items.Add(item);
            }
        }
        finally
        {
            costListView.EndUpdate();
        }

        totalCostLabel.Text = breakdown.FormatTotal();
        costNoteLabel.Text = DescribeRates(breakdown);
    }

    /// <summary>
    /// The line under the cost table. It has to name both models when both were used, and
    /// say plainly when a figure is an estimate or when embeddings were billed without the
    /// provider reporting a token count.
    /// </summary>
    private static string DescribeRates(CostBreakdown breakdown)
    {
        if (breakdown.RatesAreEstimated)
        {
            return $"A model in this run is not in the rate table; the cost shown is an estimate. "
                 + $"Chat: {breakdown.ModelId}."
                 + (breakdown.Embeddings.IsPresent ? $" Embeddings: {breakdown.Embeddings.ModelId}." : string.Empty);
        }

        if (!breakdown.Embeddings.IsPresent)
        {
            return $"Rates for {breakdown.ModelId}. This run made no embedding calls.";
        }

        if (!breakdown.Embeddings.UsageReported)
        {
            return $"Rates for {breakdown.ModelId} and {breakdown.Embeddings.ModelId}. The embedding "
                 + "endpoint returned no token count, so that cost is unknown rather than zero.";
        }

        return $"Chat priced at {breakdown.ModelId} rates, embeddings at "
             + $"{breakdown.Embeddings.ModelId} rates. Embeddings cover indexing, chunking and search.";
    }

    private void OpenPromptEvaluatorButton_Click(object? sender, EventArgs e)
    {
        var form = _mainForm();
        form.Location = Location;
        form.Size = Size;
        form.WindowState = WindowState;
        form.FormClosed += (_, _) => Show();
        Hide();
        form.Show();
    }

    private void OpenConfigButton_Click(object? sender, EventArgs e)
    {
        using var form = new ConfigurationForm(_settings);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            ShowBreakdown(CostBreakdown.Empty(_settings.SelectedModel));

            // The case reference, tenant or collection may have changed, which means a
            // different set of chunks — re-ask the store what is actually indexed now.
            _indexedCase = null;
            UpdateRunAvailability();
            _ = DetectExistingIndexAsync();
        }
    }

    private void SaveSettingsButton_Click(object? sender, EventArgs e)
    {
        try
        {
            SettingsStore.Save(_settings);
            statusLabel.Text = "Settings saved.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save settings",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        cancelRunButton.Enabled = busy;
        browseCaseFolderButton.Enabled = !busy;
        checksListView.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        UpdateRunAvailability();
    }

    /// <summary>
    /// Whether a run can proceed: a check needs both halves of its evidence, and they come
    /// from different stores. The indexed chunks supply what the case file evidences; the
    /// canonical model supplies what the suitability report asserts.
    /// </summary>
    private (bool CanRun, string Reason) RunReadiness()
    {
        if (CurrentCaseReference() is null)
        {
            return (false, "load docs first");
        }

        return _model is null ? (false, "extract model first") : (true, string.Empty);
    }

    /// <summary>
    /// A check can only run once the case documents are in the vector store — the model
    /// retrieves its evidence and never receives the documents directly.
    ///
    /// Indexing is a one-shot action per case: once it has succeeded there is nothing to gain
    /// from repeating it, so the button stays disabled until Browse picks another folder or
    /// the index is cleared.
    /// </summary>
    private void UpdateRunAvailability()
    {
        var indexed = CurrentCaseReference() is not null;
        var (canRun, reason) = RunReadiness();

        runButton.Enabled = !_busy && canRun;
        runButton.Text = canRun ? "Run Check" : $"Run Check ({reason})";

        runAllButton.Enabled = !_busy && canRun && _checks.Count > 0;
        runAllButton.Text = canRun ? "Run All Checks" : "Run All Checks";

        loadDocsButton.Enabled = !_busy && !indexed;
        loadDocsButton.Text = indexed ? "Docs Indexed" : "Load Docs";

        // Extraction reads the report, not the index, so it does not wait on Load Docs — the
        // two can be done in either order.
        extractModelButton.Enabled = !_busy && Directory.Exists(caseFolderTextBox.Text.Trim());
        extractModelButton.Text = _model is null ? "Extract Model" : "Re-extract Model";

        deleteModelButton.Enabled = !_busy && _model is not null;
        saveModelButton.Enabled = !_busy && _model is not null;

        // Unloading stays available as soon as the case is identifiable — by a configured
        // reference, or by the selected folder. The chunks may have been indexed in an
        // earlier session, which this instance knows nothing about.
        unloadDocsButton.Enabled = !_busy &&
            (!string.IsNullOrWhiteSpace(_settings.CaseReference) ||
             Directory.Exists(caseFolderTextBox.Text.Trim()));
    }

    private sealed class DocumentItem(string label, string fullPath)
    {
        public string FullPath { get; } = fullPath;
        public override string ToString() => label;
    }
}
