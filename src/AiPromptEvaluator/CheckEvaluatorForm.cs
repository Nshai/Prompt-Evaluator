using System.Diagnostics;
using System.IO;
using System.Text;

using Microsoft.Extensions.AI;

namespace AiPromptEvaluator;

public partial class CheckEvaluatorForm : Form
{
    private readonly AppSettings _settings;
    private readonly PromptEvaluator _evaluator;
    private readonly DoclingClient _docling;
    private List<AssessmentCheck> _checks = new();
    private CancellationTokenSource? _cts;
    private bool _busy;

    /// <summary>The case whose chunks are in the vector store, or null when nothing is loaded.</summary>
    private string? _indexedCase;

    private CostBreakdown _lastBreakdown;

    public CheckEvaluatorForm(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _evaluator = new PromptEvaluator(_settings);
        // Read the endpoint lazily so editing it in Settings takes effect without a restart.
        _docling = new DoclingClient(() => _settings.ResolveDoclingEndpoint());

        caseFolderTextBox.Text = _settings.DocumentFolder;

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

            // A case indexed in an earlier session is still in Qdrant — find out before the
            // user is told to load documents that are already there.
            _ = DetectExistingIndexAsync();
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
            var item = new ListViewItem(check.CheckId.Replace("\n", "").Trim());
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
            // A different folder is a different case — whatever is indexed no longer applies.
            _indexedCase = null;
            UpdateRunAvailability();
            _ = DetectExistingIndexAsync();
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

        using var store = new CaseDocumentStore(_settings);
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

            using var embeddings = AiClientFactory.CreateEmbeddingGenerator(_settings);
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

            _lastBreakdown = CostBreakdown.Empty(_settings.SelectedModel);
            ShowBreakdown(_lastBreakdown);
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
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Failed to index documents: {ex.Message}";
            AppendResponseLine($"Failed: {ex.Message}");
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
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
            using var store = new CaseDocumentStore(_settings);
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

    private async void RunButton_Click(object? sender, EventArgs e)
    {
        if (checksListView.SelectedItems.Count == 0)
        {
            MessageBox.Show(this, "Please select a check to run.", "No check selected",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

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

        var check = (AssessmentCheck)checksListView.SelectedItems[0].Tag!;

        SetBusy(true);
        responseTextBox.Text = "Working...";
        statusLabel.Text = "Searching the case file and assessing the check...";

        _cts = new CancellationTokenSource();

        using var store = new CaseDocumentStore(_settings);
        try
        {
            using var embeddings = AiClientFactory.CreateEmbeddingGenerator(_settings);
            var searchTool = new CaseDocumentSearchTool(_settings, embeddings, store, caseReference);

            var result = await _evaluator.RunWithToolsAsync(
                BuildSystemPrompt(caseReference),
                BuildCheckPromptText(check, caseReference),
                [searchTool.AsAIFunction()],
                _cts.Token).ConfigureAwait(true);

            responseTextBox.Text = ComposeRunReport(result.Response, searchTool.Calls);
            _lastBreakdown = result.Breakdown;
            ShowBreakdown(_lastBreakdown);
            statusLabel.Text =
                $"Done. {searchTool.Calls.Count} search(es), {result.Breakdown.Usage.TotalTokens:N0} tokens billed.";
        }
        catch (OperationCanceledException)
        {
            responseTextBox.Text = string.Empty;
            _lastBreakdown = CostBreakdown.Empty(_settings.SelectedModel);
            ShowBreakdown(_lastBreakdown);
            statusLabel.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            responseTextBox.Text = $"Error: {ex.Message}";
            _lastBreakdown = CostBreakdown.Empty(_settings.SelectedModel);
            ShowBreakdown(_lastBreakdown);
            statusLabel.Text = "The request failed.";
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
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

        using var store = new CaseDocumentStore(_settings);
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

    // ──────────────────────────────────────────────
    // Prompt builder
    // ──────────────────────────────────────────────

    /// <summary>
    /// The assessor's standing instructions. The case file is never attached — the model has
    /// to search for its evidence, so the prompt is explicit that an unsearched claim is not
    /// a finding.
    /// </summary>
    private string BuildSystemPrompt(string caseReference) =>
        $"""
        You are a financial services Quality Assurance assessor. You evaluate a client case file
        against one QA check at a time.

        You cannot see the case file directly. Use the search_case_documents tool to retrieve
        passages from it. The loaded case reference is "{caseReference}" and the tenant is
        {_settings.TenantId}; the tool defaults to both, so you normally pass only the search text.

        How to work:
        - Search before you conclude. Run several searches with different wordings — the
          terminology in the file rarely matches the wording of the check.
        - The same point may be evidenced in more than one document; the tool returns matches
          across documents, and corroboration matters.
        - Base findings only on returned passages. If searching turns up nothing relevant, say
          the evidence is absent rather than inferring it.
        - Cite the document name and category for each piece of evidence you rely on.
        """;

    private static string BuildCheckPromptText(AssessmentCheck check, string caseReference)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Assess case {caseReference} against this check.");
        sb.AppendLine();
        sb.AppendLine($"## Check: {check.CheckId.Replace("\n", "").Trim()} — {check.CheckName}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(check.RegulatoryBasis))
        {
            sb.AppendLine($"Regulatory Basis: {check.RegulatoryBasis}");
            sb.AppendLine();
        }
        sb.AppendLine("### Prompt");
        sb.AppendLine(check.Prompt);
        sb.AppendLine();
        sb.AppendLine("### What to Look For");
        sb.AppendLine(check.WhatToLookFor);
        sb.AppendLine();
        sb.AppendLine("### Decision Logic");
        sb.AppendLine(check.DecisionLogic);
        sb.AppendLine();
        sb.AppendLine(
            "State your finding as one of: **No Issue**, **Potential Concern**, or **N/A**, then give a "
            + "concise explanation quoting the specific evidence and naming the document it came from.");
        return sb.ToString();
    }

    /// <summary>
    /// The finding, plus the searches that produced it — the run is only as good as what it
    /// retrieved, so the searches belong in the record next to the conclusion.
    /// </summary>
    private static string ComposeRunReport(string response, IReadOnlyList<CaseSearchCall> calls)
    {
        var sb = new StringBuilder(response.TrimEnd());

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine(new string('-', 72));

        if (calls.Count == 0)
        {
            sb.AppendLine("No searches were run — the finding above is not grounded in the case file.");
            return sb.ToString();
        }

        sb.AppendLine($"Evidence gathered from {calls.Count} search(es):");
        foreach (var call in calls)
        {
            sb.AppendLine($"  \"{call.SearchText}\" → {call.Matches} passage(s)");
        }

        return sb.ToString();
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
        costNoteLabel.Text = breakdown.RatesAreEstimated
            ? $"'{breakdown.ModelId}' is not in the rate table; the cost shown is an estimate."
            : $"Rates for {breakdown.ModelId}. Embedding calls are billed separately and not shown here.";
    }

    private void OpenPromptEvaluatorButton_Click(object? sender, EventArgs e)
    {
        var form = new MainForm(_settings);
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
            SettingsStorage.Save(_settings);
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

        runButton.Enabled = !_busy && indexed;
        runButton.Text = indexed ? "Run Check" : "Run Check (load docs first)";

        loadDocsButton.Enabled = !_busy && !indexed;
        loadDocsButton.Text = indexed ? "Docs Indexed" : "Load Docs";

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
