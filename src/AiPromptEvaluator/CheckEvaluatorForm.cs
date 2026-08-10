using System.IO;
using System.Text;

using Anthropic.Models.Beta.Messages;

namespace AiPromptEvaluator;

public partial class CheckEvaluatorForm : Form
{
    private readonly AppSettings _settings;
    private readonly PromptEvaluator _evaluator;
    private readonly AnthropicFileUploader _fileUploader;
    private List<AssessmentCheck> _checks = new();
    private CancellationTokenSource? _cts;
    private CostBreakdown _lastBreakdown;

    public CheckEvaluatorForm(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _evaluator = new PromptEvaluator(_settings);
        _fileUploader = new AnthropicFileUploader(_evaluator);
        caseFolderTextBox.Text = _settings.DocumentFolder;

        _lastBreakdown = CostBreakdown.Empty(_settings.SelectedModel);
        ShowBreakdown(_lastBreakdown);

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
        }
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

        var files = AssessmentCheckLoader.GetFilesForCodes(caseFolder, check.CategoryCodes);
        foreach (var file in files.OrderBy(f => f))
        {
            // Display as "CODE / filename", store full path
            var code = Path.GetFileName(Path.GetDirectoryName(file)) ?? string.Empty;
            var label = $"[{code}]  {Path.GetFileName(file)}";
            documentsCheckedListBox.Items.Add(new DocumentItem(label, file), true);
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

        var check = (AssessmentCheck)checksListView.SelectedItems[0].Tag!;

        // Collect only checked documents
        var selectedFiles = documentsCheckedListBox.CheckedItems
            .Cast<DocumentItem>()
            .Select(d => d.FullPath)
            .ToList();

        SetBusy(true);
        responseTextBox.Text = "Working...";
        statusLabel.Text = "Uploading documents...";

        _cts = new CancellationTokenSource();
        try
        {
            var checkPrompt = BuildCheckPromptText(check);
            var contentBlocks = await BuildDocumentBlocksAsync(selectedFiles, _cts.Token).ConfigureAwait(true);

            statusLabel.Text = "Calling the Anthropic API...";
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
    /// request: PDFs/images/plain-text are uploaded once via the Files API and referenced
    /// by file_id; formats the Files API can't take directly (docx, xlsx, zip, ...) fall
    /// back to inline text, same as before.
    /// </summary>
    private async Task<List<BetaContentBlockParam>> BuildDocumentBlocksAsync(List<string> filePaths, CancellationToken cancellationToken)
    {
        var blocks = new List<BetaContentBlockParam>();

        if (filePaths.Count == 0)
        {
            blocks.Add(new BetaTextBlockParam { Text = "No documents are available for this check." });
        }
        else
        {
            blocks.Add(new BetaTextBlockParam { Text = "### Supporting Documents" });

            // Group by category folder code
            var byCode = filePaths
                .GroupBy(f => Path.GetFileName(Path.GetDirectoryName(f)) ?? string.Empty)
                .OrderBy(g => g.Key);

            foreach (var group in byCode)
            {
                var catName = ResolveCategoryName(group.Key);
                blocks.Add(new BetaTextBlockParam { Text = $"#### [{group.Key}] {catName}" });

                foreach (var file in group)
                {
                    var fileName = Path.GetFileName(file);
                    var fileRef = await _fileUploader.GetOrUploadAsync(file, cancellationToken).ConfigureAwait(true);

                    switch (fileRef.Kind)
                    {
                        case AnthropicFileKind.Document:
                            blocks.Add(new BetaRequestDocumentBlock
                            {
                                Source = new BetaFileDocumentSource(fileRef.FileId!),
                                Title = $"[{group.Key}] {fileName}",
                                Context = catName,
                            });
                            break;
                        case AnthropicFileKind.Image:
                            blocks.Add(new BetaImageBlockParam
                            {
                                Source = new BetaFileImageSource(fileRef.FileId!),
                            });
                            break;
                        default:
                            blocks.Add(new BetaTextBlockParam { Text = BuildInlineTextBlock(file, fileName) });
                            break;
                    }
                }
            }
        }

        blocks.Add(new BetaTextBlockParam
        {
            Text = "Based on the documents above, assess this check and state your finding as one of: **No Issue**, **Potential Concern**, or **N/A**. Provide a concise explanation referencing specific evidence."
        });

        return blocks;
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

    private static string ResolveCategoryName(string code) => code switch
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
        _ => code,
    };

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
        runButton.Enabled = !busy;
        cancelRunButton.Enabled = busy;
        checksListView.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private sealed class DocumentItem(string label, string fullPath)
    {
        public string FullPath { get; } = fullPath;
        public override string ToString() => label;
    }
}
