namespace AiPromptEvaluator;

public partial class MainForm : Form
{
    private readonly AppSettings _settings;
    private bool _suppressSettingsSync;

    public MainForm() : this(SettingsStorage.Load()) { }

    public MainForm(AppSettings settings)
    {
        InitializeComponent();

        _settings = settings;
        LoadSettingsIntoUi();
        ShowBreakdown(CostBreakdown.Empty(_settings.SelectedModel));
    }

    private void LoadSettingsIntoUi()
    {
        _suppressSettingsSync = true;
        try
        {
            documentFolderTextBox.Text = _settings.DocumentFolder;
            clarificationCheckBox.Checked = _settings.AskClarification;
            RefreshModelList();
        }
        finally
        {
            _suppressSettingsSync = false;
        }
    }

    /// <summary>Repopulates the model dropdown from settings, preserving the selection.</summary>
    private void RefreshModelList()
    {
        var previous = _suppressSettingsSync;
        _suppressSettingsSync = true;
        try
        {
            modelComboBox.Items.Clear();
            foreach (var model in _settings.ParseModels())
            {
                modelComboBox.Items.Add(model);
            }

            modelComboBox.Text = _settings.SelectedModel;
        }
        finally
        {
            _suppressSettingsSync = previous;
        }
    }

    private void ModelComboBox_Changed(object? sender, EventArgs e)
    {
        if (_suppressSettingsSync)
        {
            return;
        }

        _settings.SelectedModel = modelComboBox.Text.Trim();

        // Re-price the existing token counts against the newly selected model.
        ShowBreakdown(CostBreakdown.Create(_settings.SelectedModel, _lastUsage));
    }

    private TokenUsage _lastUsage = TokenUsage.Empty;

    private void DocumentFolderTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (!_suppressSettingsSync)
        {
            _settings.DocumentFolder = documentFolderTextBox.Text;
        }
    }

    private void ClarificationCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (!_suppressSettingsSync)
        {
            _settings.AskClarification = clarificationCheckBox.Checked;
        }
    }

    private void BrowseFolderButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a folder containing documents to use as prompt context",
            UseDescriptionForTitle = true
        };

        if (Directory.Exists(documentFolderTextBox.Text))
        {
            dialog.SelectedPath = documentFolderTextBox.Text;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            documentFolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void CategoriseButton_Click(object? sender, EventArgs e)
    {
        var folder = documentFolderTextBox.Text.Trim();
        if (!Directory.Exists(folder))
        {
            MessageBox.Show(this, "Please select a valid document folder first.", "No folder",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new DocumentCategorizerForm(folder, _settings.DocumentCategories);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _settings.DocumentCategories = new Dictionary<string, string>(form.Categories);
        }
    }

    private void OpenCheckEvaluatorButton_Click(object? sender, EventArgs e)
    {
        var form = new CheckEvaluatorForm(_settings);
        form.Location = Location;
        form.Size = Size;
        form.WindowState = WindowState;
        form.FormClosed += (_, _) => Show();
        Hide();
        form.Show();
    }

    private void OpenConfigButton_Click(object? sender, EventArgs e)
    {
        using var configForm = new ConfigurationForm(_settings);
        if (configForm.ShowDialog(this) == DialogResult.OK)
        {
            LoadSettingsIntoUi();
            ShowBreakdown(CostBreakdown.Create(_settings.SelectedModel, _lastUsage));
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

    private void ClearButton_Click(object? sender, EventArgs e)
    {
        promptTextBox.Clear();
        responseTextBox.Clear();
        statusLabel.Text = string.Empty;
        _lastUsage = TokenUsage.Empty;
        ShowBreakdown(CostBreakdown.Empty(_settings.SelectedModel));
    }

    private async void RunButton_Click(object? sender, EventArgs e)
    {
        SetBusy(true);
        responseTextBox.Text = "Working...";
        statusLabel.Text = "Calling the Anthropic API...";

        try
        {
            var evaluator = new PromptEvaluator(_settings);
            var result = await evaluator.RunAsync(promptTextBox.Text).ConfigureAwait(true);

            responseTextBox.Text = result.Response;
            _lastUsage = result.Breakdown.Usage;
            ShowBreakdown(result.Breakdown);
            statusLabel.Text = $"Done. {result.Breakdown.Usage.TotalTokens:N0} tokens billed.";
        }
        catch (InvalidOperationException ex)
        {
            // Validation problems (no prompt, no API key) — not an API failure.
            responseTextBox.Text = string.Empty;
            statusLabel.Text = string.Empty;
            MessageBox.Show(this, ex.Message, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            responseTextBox.Text = $"Error: {ex.Message}";
            statusLabel.Text = "The request failed.";
            _lastUsage = TokenUsage.Empty;
            ShowBreakdown(CostBreakdown.Empty(_settings.SelectedModel));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        runButton.Enabled = !busy;
        clearButton.Enabled = !busy;
        openConfigButton.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    /// <summary>Renders the per-component cost breakdown on the main screen.</summary>
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
}
