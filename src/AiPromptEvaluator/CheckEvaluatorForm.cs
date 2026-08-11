using System.Diagnostics;
using System.IO;
using System.Text;

using Anthropic.Models.Beta.Messages;

namespace AiPromptEvaluator;

public partial class CheckEvaluatorForm : Form
{
    /// <summary>
    /// How much document content one token-counting request may carry. The counting endpoint
    /// rejects file_id sources, so documents go inline and a whole case folder easily exceeds
    /// the request size limit — counting is therefore split into batches under this budget.
    /// </summary>
    private const long CountingBatchByteBudget = 6L * 1024 * 1024;

    /// <summary>
    /// The default per-request context window. Documents totalling more than this can't all
    /// be attached to one check, so the load report flags it rather than letting the run fail
    /// with "prompt is too long".
    /// </summary>
    private const long ContextWindowTokens = 200_000;

    private readonly AppSettings _settings;
    private readonly PromptEvaluator _evaluator;
    private readonly DoclingClient _docling;
    private readonly AnthropicFileUploader _fileUploader;
    private readonly UploadedFileStore? _uploadStore;
    private List<AssessmentCheck> _checks = new();
    private CaseDocumentLibrary? _library;
    private CancellationTokenSource? _cts;
    private bool _busy;
    private CostBreakdown _lastBreakdown;

    public CheckEvaluatorForm(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _evaluator = new PromptEvaluator(_settings);
        // Read the endpoint lazily so editing it in Settings takes effect without a restart.
        _docling = new DoclingClient(() => _settings.ResolveDoclingEndpoint());
        _fileUploader = new AnthropicFileUploader(
            _evaluator, new SpreadsheetToMarkdownConverter(_docling));

        // The upload cache is an optimisation: if the database can't be opened the app still
        // works, it just re-uploads every time.
        try
        {
            _uploadStore = new UploadedFileStore();
        }
        catch (Exception ex)
        {
            _uploadStore = null;
            statusLabel.Text = $"Upload cache unavailable ({ex.Message}); files will be re-uploaded each time.";
        }

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
            // A different folder invalidates any previously uploaded file_id map.
            _library = null;
            UpdateRunAvailability();
        }
    }

    /// <summary>
    /// Uploads every file under the case folder (and its sub-folders) to the Files API,
    /// recording each returned file_id against its document category. Runs are then free
    /// to reference the documents by file_id instead of re-uploading them.
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

        if (string.IsNullOrWhiteSpace(_settings.AnthropicApiKey))
        {
            MessageBox.Show(this, "Please configure an Anthropic API key first.", "No API key",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!await ConfirmDoclingForSpreadsheetsAsync(caseFolder).ConfigureAwait(true))
        {
            return;
        }

        SetBusy(true);
        statusLabel.Text = "Loading documents...";

        responseTextBox.Clear();
        AppendResponseLine($"Uploading documents from {caseFolder}");
        AppendResponseLine(new string('-', 72));

        var elapsed = Stopwatch.StartNew();
        _cts = new CancellationTokenSource();
        try
        {
            // Uploads complete out of order, so each line carries its own running count.
            var progress = new Progress<CaseLoadProgress>(p =>
            {
                AppendResponseLine(DescribeUpload(p));
                statusLabel.Text = $"Uploading {p.Done}/{p.Total}...";
            });

            var library = await CaseDocumentLibrary
                .LoadAsync(caseFolder, _fileUploader, progress, _uploadStore, _cts.Token)
                .ConfigureAwait(true);

            elapsed.Stop();

            // Keep the library the moment it exists. Everything below is reporting, and a
            // failure there must never throw away documents that are already uploaded.
            _library = library;

            var summary = SummariseLoad(library, elapsed.Elapsed);
            AppendResponseLine(new string('-', 72));
            AppendResponseLine(summary);

            _lastBreakdown = CostBreakdown.Empty(_settings.SelectedModel);
            ShowBreakdown(_lastBreakdown);
            statusLabel.Text = summary;

            var excluded = library.ExcludedSpreadsheets;
            if (excluded.Count > 0)
            {
                AppendResponseLine(string.Empty);
                AppendResponseLine($"Excluded {excluded.Count} spreadsheet(s) that could not be converted:");
                foreach (var doc in excluded)
                {
                    AppendResponseLine($"  {doc.FileName} — {doc.Error ?? "unsupported format"}");
                }
            }

            // Refresh the document list so the loaded markers appear.
            if (checksListView.SelectedItems.Count > 0)
            {
                PopulateDocuments((AssessmentCheck)checksListView.SelectedItems[0].Tag!);
            }
        }
        catch (OperationCanceledException)
        {
            statusLabel.Text = "Document load cancelled.";
            AppendResponseLine("Cancelled.");
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Failed to load documents: {ex.Message}";
            AppendResponseLine($"Failed: {ex.Message}");
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    /// <summary>One line per completed upload: position, category, name, and what happened to it.</summary>
    private static string DescribeUpload(CaseLoadProgress p)
    {
        var doc = p.Document;
        var category = string.IsNullOrEmpty(doc.CategoryCode) ? "-" : doc.CategoryCode;

        var outcome = doc switch
        {
            { Error: not null } when doc.IsExcludedSpreadsheet => $"EXCLUDED — {doc.Error}",
            { Error: not null } => $"FAILED — {doc.Error}",
            { IsExcludedSpreadsheet: true } => "EXCLUDED — unsupported spreadsheet",
            { ReusedFromCache: true } => "reused (already uploaded)",
            { FileId: not null, ConvertedTo: ConversionTarget.Pdf } => "uploaded (converted to PDF)",
            { FileId: not null, ConvertedTo: ConversionTarget.Markdown } => "uploaded (converted to Markdown)",
            { FileId: not null } => "uploaded",
            _ => "inline text (format not supported by the Files API)",
        };

        return $"[{p.Done,3}/{p.Total}] {category,-3} {doc.FileName,-48} {outcome} ({p.Elapsed.TotalSeconds:0.0}s)";
    }

    private static string SummariseLoad(CaseDocumentLibrary library, TimeSpan elapsed)
    {
        var summary = new StringBuilder(
            $"Ready: {library.UploadedCount} documents across {library.ByCategory().Count} categories in {elapsed.TotalSeconds:0.0}s");

        if (library.ReusedCount > 0)
        {
            summary.Append($", {library.ReusedCount} reused from cache, {library.FreshlyUploadedCount} uploaded");
        }

        var toPdf = library.ConvertedCount(ConversionTarget.Pdf);
        if (toPdf > 0)
        {
            summary.Append($", {toPdf} converted from Word to PDF");
        }

        var toMarkdown = library.ConvertedCount(ConversionTarget.Markdown);
        if (toMarkdown > 0)
        {
            summary.Append($", {toMarkdown} converted from spreadsheet to Markdown");
        }

        if (library.UnsupportedCount > 0)
        {
            summary.Append($", {library.UnsupportedCount} sent as inline text");
        }

        var excluded = library.ExcludedSpreadsheets.Count;
        if (excluded > 0)
        {
            summary.Append($", {excluded} spreadsheet(s) excluded");
        }

        return summary.Append('.').ToString();
    }

    private void AppendResponseLine(string text)
    {
        responseTextBox.AppendText(text + Environment.NewLine);
    }

    /// <summary>
    /// Spreadsheets need the Docling sidecar. Probing it before the load starts turns what
    /// would otherwise be a per-file failure partway through into one up-front decision.
    /// Returns false when the user chooses not to continue.
    /// </summary>
    private async Task<bool> ConfirmDoclingForSpreadsheetsAsync(string caseFolder)
    {
        bool hasSpreadsheets;
        try
        {
            hasSpreadsheets = Directory
                .EnumerateFiles(caseFolder, "*.*", SearchOption.AllDirectories)
                .Any(SpreadsheetToMarkdownConverter.IsConvertible);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not read the case folder: {ex.Message}", "Case folder",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (!hasSpreadsheets)
        {
            return true;
        }

        statusLabel.Text = $"Checking Docling at {_docling.Endpoint}...";
        if (await _docling.IsAvailableAsync().ConfigureAwait(true))
        {
            return true;
        }

        var answer = MessageBox.Show(this,
            $"The case folder contains spreadsheets, but Docling is not responding at {_docling.Endpoint}.\n\n"
            + "Start the Docling container or correct the endpoint in Settings.\n\n"
            + "Load anyway? Spreadsheets will be skipped.",
            "Docling unavailable", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
        {
            statusLabel.Text = "Document load cancelled.";
        }

        return answer == DialogResult.Yes;
    }

    /// <summary>The loaded library, but only while it still matches the case folder in the text box.</summary>
    private CaseDocumentLibrary? CurrentLibrary()
    {
        var caseFolder = caseFolderTextBox.Text.Trim();
        return _library is not null &&
               CaseDocumentLibrary.AreSameFolder(_library.CaseFolder, caseFolder)
            ? _library
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

    private void PopulateDocuments(AssessmentCheck check)
    {
        documentsCheckedListBox.Items.Clear();

        var caseFolder = caseFolderTextBox.Text.Trim();
        if (!Directory.Exists(caseFolder))
        {
            return;
        }

        var library = CurrentLibrary();
        var files = AssessmentCheckLoader.GetFilesForCodes(caseFolder, check.CategoryCodes);
        foreach (var file in files.OrderBy(f => f))
        {
            // Display as "CODE / filename", store full path. A tick marks documents already
            // uploaded by "Load Docs"; an excluded spreadsheet is flagged and left unticked,
            // since a run would drop it anyway.
            var code = Path.GetFileName(Path.GetDirectoryName(file)) ?? string.Empty;
            var known = library is not null && library.TryGet(file, out var doc) ? doc : null;

            var marker = known switch
            {
                { IsExcludedSpreadsheet: true } => "✗ ",
                { FileId: not null } => "✓ ",
                _ => string.Empty,
            };

            var suffix = known?.IsExcludedSpreadsheet == true ? "  (excluded — could not convert)" : string.Empty;
            var label = $"{marker}[{code}]  {Path.GetFileName(file)}{suffix}";

            documentsCheckedListBox.Items.Add(
                new DocumentItem(label, file), known?.IsExcludedSpreadsheet != true);
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

        if (string.IsNullOrWhiteSpace(_settings.AnthropicApiKey))
        {
            MessageBox.Show(this, "Please configure an Anthropic API key first.", "No API key",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var library = CurrentLibrary();
        if (library is null)
        {
            MessageBox.Show(this,
                "Load the case documents first — click \"Load Docs\" to upload them and capture their file IDs.",
                "Documents not loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var check = (AssessmentCheck)checksListView.SelectedItems[0].Tag!;

        // Collect only checked documents
        var selectedFiles = documentsCheckedListBox.CheckedItems
            .Cast<DocumentItem>()
            .Select(d => d.FullPath)
            .ToList();

        SetBusy(true);
        responseTextBox.Text = "Working...";
        statusLabel.Text = "Calling the Anthropic API...";

        _cts = new CancellationTokenSource();
        try
        {
            var checkPrompt = BuildCheckPromptText(check);
            var contentBlocks = BuildDocumentBlocks(selectedFiles, library);

            var result = await _evaluator.RunWithFilesAsync(checkPrompt, contentBlocks, _cts.Token).ConfigureAwait(true);

            responseTextBox.Text = result.Response;
            _lastBreakdown = result.Breakdown;
            ShowBreakdown(_lastBreakdown);
            statusLabel.Text = $"Done. {result.Breakdown.Usage.TotalTokens:N0} tokens billed.";
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
    /// Deletes this case folder's uploads from Anthropic and forgets them in SQLite, so the
    /// next "Load Docs" uploads everything fresh.
    ///
    /// The file_ids come from the loaded library *and* the database, because after a restart
    /// there is no library but the rows — and the files on Anthropic's side — are still there.
    /// </summary>
    private async void UnloadDocsButton_Click(object? sender, EventArgs e)
    {
        var caseFolder = caseFolderTextBox.Text.Trim();

        var uploadedIds = (_library?.Documents ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d.FileId))
            .Select(d => d.FileId!)
            .Concat(CachedFileIdsFor(caseFolder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uploadedIds.Count == 0)
        {
            _library = null;
            UpdateRunAvailability();
            statusLabel.Text = "No uploaded documents to unload.";
            return;
        }

        if (MessageBox.Show(this,
                $"Delete {uploadedIds.Count} uploaded file(s) from Anthropic and clear them from the upload cache?\n\n"
                + "The next \"Load Docs\" will upload them again.",
                "Unload documents", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true);
        statusLabel.Text = "Deleting uploaded documents...";

        _cts = new CancellationTokenSource();
        var deleted = 0;
        var alreadyGone = 0;

        try
        {
            foreach (var fileId in uploadedIds)
            {
                try
                {
                    await _fileUploader.DeleteUploadedFileAsync(fileId, _cts.Token).ConfigureAwait(true);
                    deleted++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Already deleted server-side, or never existed. Either way the local
                    // record is wrong and must go, or we would reuse a dead file_id forever.
                    alreadyGone++;
                }
            }

            var forgotten = _uploadStore?.RemoveCaseFolder(caseFolder) ?? 0;

            _library = null;
            UpdateRunAvailability();
            responseTextBox.Clear();
            ShowBreakdown(CostBreakdown.Empty(_settings.SelectedModel));

            statusLabel.Text = alreadyGone > 0
                ? $"Deleted {deleted} file(s), {alreadyGone} were already gone; cleared {forgotten} cache entries."
                : $"Deleted {deleted} file(s) and cleared {forgotten} cache entries.";
        }
        catch (OperationCanceledException)
        {
            statusLabel.Text = $"Unload cancelled after deleting {deleted} file(s).";
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Failed to unload documents: {ex.Message}";
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    /// <summary>file_ids recorded for a case folder, so a restart can still clean them up.</summary>
    private IReadOnlyList<string> CachedFileIdsFor(string caseFolder)
    {
        if (_uploadStore is null || string.IsNullOrWhiteSpace(caseFolder))
        {
            return [];
        }

        try
        {
            return _uploadStore.GetForCaseFolder(caseFolder).Select(f => f.FileId).ToList();
        }
        catch
        {
            return [];
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

    private static string BuildCheckPromptText(AssessmentCheck check)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a financial services Quality Assurance assessor. Your task is to evaluate a client case file against a specific QA check.");
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
        return sb.ToString();
    }

    /// <summary>
    /// Builds the ordered content blocks for the "Supporting Documents" portion of the
    /// request, entirely from the documents already loaded by "Load Docs" — PDFs, text
    /// and images are referenced by their captured file_id, and formats the Files API
    /// can't take directly (zip, ...) fall back to inline text. Spreadsheets that could not
    /// be converted are left out entirely — their bytes carry no meaning as text. Nothing is
    /// uploaded here: a run never touches the Files API.
    /// </summary>
    private static List<BetaContentBlockParam> BuildDocumentBlocks(List<string> filePaths, CaseDocumentLibrary library)
    {
        var blocks = new List<BetaContentBlockParam>();

        // Only documents present in the loaded library, and sendable, can be referenced.
        var documents = filePaths
            .Select(f => library.TryGet(f, out var doc) ? doc : null)
            .Where(d => d is not null && d.IsSendable)
            .Select(d => d!)
            .ToList();

        if (documents.Count == 0)
        {
            blocks.Add(new BetaTextBlockParam { Text = "No documents are available for this check." });
        }
        else
        {
            blocks.Add(new BetaTextBlockParam { Text = "### Supporting Documents" });

            var byCategory = documents
                .GroupBy(d => d.CategoryCode)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var group in byCategory)
            {
                blocks.Add(new BetaTextBlockParam
                {
                    Text = $"#### [{group.Key}] {ResolveCategoryName(group.Key)}"
                });

                foreach (var doc in group)
                {
                    blocks.Add(BlockFor(doc));
                }
            }
        }

        blocks.Add(new BetaTextBlockParam
        {
            Text = "Based on the documents above, assess this check and state your finding as one of: **No Issue**, **Potential Concern**, or **N/A**. Provide a concise explanation referencing specific evidence."
        });

        return blocks;
    }

    /// <summary>
    /// One content block per document: a file_id reference for PDFs/text/images, or an
    /// inline text preview for formats the Files API can't take directly.
    /// </summary>
    private static BetaContentBlockParam BlockFor(CaseDocument doc) => doc.Kind switch
    {
        AnthropicFileKind.Document => new BetaRequestDocumentBlock
        {
            Source = new BetaFileDocumentSource(doc.FileId!),
            Title = $"[{doc.CategoryCode}] {doc.FileName}",
            Context = doc.CategoryName,
        },
        AnthropicFileKind.Image => new BetaImageBlockParam
        {
            Source = new BetaFileImageSource(doc.FileId!),
        },
        // A Word document we failed to convert would be binary noise as inline text.
        _ when doc.Error is not null => new BetaTextBlockParam
        {
            Text = $"--- File: {doc.FileName} (could not be converted: {doc.Error}) ---",
        },
        _ => new BetaTextBlockParam { Text = BuildInlineTextBlock(doc.FilePath, doc.FileName) },
    };

    /// <summary>
    /// The counting equivalent of <see cref="BlockFor"/>. The token-counting endpoint rejects
    /// file_id sources outright ("File sources are not supported in the token counting
    /// endpoint"), so the same bytes are sent inline instead — base64 for PDFs, plain text
    /// otherwise. Token counting is free, so re-sending the content here costs nothing.
    ///
    /// Returns null for anything we can't price locally, so it is left out of the estimate
    /// rather than counted as zero.
    /// </summary>
    private static BetaContentBlockParam? CountingBlockFor(CaseDocument doc)
    {
        var path = doc.BilledPath;

        try
        {
            if (doc.Kind == AnthropicFileKind.Document &&
                Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return new BetaRequestDocumentBlock
                {
                    Source = new BetaBase64PdfSource(Convert.ToBase64String(File.ReadAllBytes(path))),
                    Title = $"[{doc.CategoryCode}] {doc.FileName}",
                    Context = doc.CategoryName,
                };
            }

            if (doc.Kind == AnthropicFileKind.Document)
            {
                return new BetaRequestDocumentBlock
                {
                    Source = new BetaPlainTextSource(File.ReadAllText(path)),
                    Title = $"[{doc.CategoryCode}] {doc.FileName}",
                    Context = doc.CategoryName,
                };
            }

            // Images bill by dimensions rather than bytes; leave them out of the estimate.
            if (doc.Kind == AnthropicFileKind.Image)
            {
                return null;
            }

            return new BetaTextBlockParam { Text = BuildInlineTextBlock(doc.FilePath, doc.FileName) };
        }
        catch
        {
            // An unreadable file just doesn't contribute to the estimate.
            return null;
        }
    }

    private static string BuildInlineTextBlock(string file, string fileName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- File: {fileName} ---");
        try
        {
            var content = File.ReadAllText(file);
            var preview = content.Length > 2000 ? content[..2000] : content;
            sb.AppendLine(preview.Replace("\r\n", "\n"));
        }
        catch
        {
            sb.AppendLine("(unreadable)");
        }
        return sb.ToString();
    }

    private static string ResolveCategoryName(string code) => CaseDocumentLibrary.ResolveCategoryName(code);

    /// <summary>
    /// Prices the uploaded documents: counts the input tokens the file blocks would bill if
    /// every sendable document were attached, and shows that in the cost panel. Uploading and
    /// token counting are both free — this is what a run attaching all documents would cost.
    /// Returns a one-line summary for the response pane.
    /// </summary>
    private async Task<string> ShowDocumentTokenCostAsync(
        CaseDocumentLibrary library, CancellationToken cancellationToken)
    {
        // Count per category, because that is the unit a check actually attaches — a total
        // alone can't tell you whether any given check will fit in the context window.
        var categories = library.SendableDocuments
            .GroupBy(d => d.CategoryCode)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var report = new StringBuilder();
        long inputTokens = 0;
        var counted = 0;
        var skipped = 0;

        var perCategory = new List<(string Code, long Tokens, int Documents)>();

        foreach (var category in categories)
        {
            var (tokens, ok, failed) = await CountCategoryAsync(category, cancellationToken).ConfigureAwait(true);

            inputTokens += tokens;
            counted += ok;
            skipped += failed;
            perCategory.Add((category.Key, tokens, ok));
        }

        _lastBreakdown = CostBreakdown.Create(
            _settings.SelectedModel,
            new TokenUsage(InputTokens: inputTokens, OutputTokens: 0, CacheWriteTokens: 0, CacheReadTokens: 0));

        ShowBreakdown(_lastBreakdown);
        costNoteLabel.Text =
            $"Estimated input cost if all {counted} documents are attached to one check. "
            + "Uploading and token counting are free; you are billed when the documents are sent with a run.";

        report.Append($"Total upload: {inputTokens:N0} input tokens across {counted} documents "
                    + $"— {_lastBreakdown.TotalCost:C4} if all were attached to one check ({_settings.SelectedModel}).");

        if (skipped > 0)
        {
            report.Append($" ({skipped} could not be priced and are not included.)");
        }

        if (inputTokens > ContextWindowTokens)
        {
            report.AppendLine();
            report.Append($"Note: that is over the {ContextWindowTokens:N0}-token context window, "
                        + "so no single check can attach every document. Checks attach by category:");
        }
        else
        {
            report.AppendLine();
            report.Append("Per category:");
        }

        foreach (var (code, tokens, documents) in perCategory)
        {
            var name = CaseDocumentLibrary.ResolveCategoryName(code);
            var overflow = tokens > ContextWindowTokens ? "  ** exceeds the context window **" : string.Empty;

            report.AppendLine();
            var label = string.IsNullOrEmpty(code) ? "-" : code;
            report.Append($"  [{label}] {name,-30} {tokens,9:N0} tokens  {documents,3} docs{overflow}");
        }

        return report.ToString();
    }

    /// <summary>Counts one category's documents, splitting into requests that fit the size limit.</summary>
    private async Task<(long Tokens, int Counted, int Skipped)> CountCategoryAsync(
        IEnumerable<CaseDocument> documents, CancellationToken cancellationToken)
    {
        var priced = documents
            .Select(doc => (Block: CountingBlockFor(doc), Bytes: EstimatedRequestBytes(doc)))
            .Where(x => x.Block is not null)
            .Select(x => (Block: x.Block!, x.Bytes))
            .ToList();

        long tokens = 0;
        var counted = 0;
        var skipped = 0;

        foreach (var batch in BatchByBytes(priced, CountingBatchByteBudget))
        {
            try
            {
                tokens += await _evaluator
                    .CountTokensAsync(batch.Select(x => x.Block).ToList(), cancellationToken)
                    .ConfigureAwait(true);

                counted += batch.Count;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // A single oversized document can still be refused. Leave it out of the
                // estimate rather than losing the figure for everything else.
                skipped += batch.Count;
            }
        }

        return (tokens, counted, skipped);
    }

    /// <summary>
    /// Splits documents into groups that each stay under the request size limit. Token counts
    /// are summed across the groups, which is a few tokens high per extra request because each
    /// carries its own message envelope — immaterial against a case folder's total.
    /// </summary>
    private static List<List<(BetaContentBlockParam Block, long Bytes)>> BatchByBytes(
        List<(BetaContentBlockParam Block, long Bytes)> items, long budget)
    {
        var batches = new List<List<(BetaContentBlockParam, long)>>();
        var current = new List<(BetaContentBlockParam, long)>();
        long running = 0;

        foreach (var item in items)
        {
            // A single document over budget still goes on its own, and either counts or doesn't.
            if (current.Count > 0 && running + item.Bytes > budget)
            {
                batches.Add(current);
                current = [];
                running = 0;
            }

            current.Add(item);
            running += item.Bytes;
        }

        if (current.Count > 0)
        {
            batches.Add(current);
        }

        return batches;
    }

    /// <summary>How much request body a document will occupy, base64 inflation included.</summary>
    private static long EstimatedRequestBytes(CaseDocument doc)
    {
        try
        {
            var length = new FileInfo(doc.BilledPath).Length;
            return Path.GetExtension(doc.BilledPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                ? length * 4 / 3
                : length;
        }
        catch
        {
            return 0;
        }
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
            ? $"'{breakdown.ModelId}' is not in the rate table; showing Opus-tier estimates."
            : $"Rates for {breakdown.ModelId}. Cache writes bill at 1.25x input, reads at 0.1x.";
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
    /// A check can only run once the case documents have been loaded — runs reference
    /// documents by file_id and never upload during the run itself.
    ///
    /// Loading is a one-shot action per folder: once it has succeeded there is nothing to
    /// gain from repeating it, so the button stays disabled until Browse picks a folder,
    /// which clears the library.
    /// </summary>
    private void UpdateRunAvailability()
    {
        var loaded = CurrentLibrary() is not null;

        runButton.Enabled = !_busy && loaded;
        runButton.Text = loaded ? "Run Check" : "Run Check (load docs first)";

        loadDocsButton.Enabled = !_busy && !loaded;
        loadDocsButton.Text = loaded ? "Docs Loaded" : "Load Docs";

        // Unload stays available whenever anything is uploaded for this folder — including
        // uploads from an earlier session, which outlive the in-memory library.
        unloadDocsButton.Enabled = !_busy &&
            (loaded || CachedFileIdsFor(caseFolderTextBox.Text.Trim()).Count > 0);
    }

    private sealed class DocumentItem(string label, string fullPath)
    {
        public string FullPath { get; } = fullPath;
        public override string ToString() => label;
    }
}
