namespace AiPromptEvaluator;

public partial class ConfigurationForm : Form
{
    private readonly AppSettings _settings;

    public ConfigurationForm(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        apiKeyTextBox.Text = _settings.AnthropicApiKey;
        baseUrlTextBox.Text = _settings.AnthropicBaseUrl;
        availableModelsTextBox.Text = _settings.AvailableModels;
        documentFolderTextBox.Text = _settings.DocumentFolder;
        clarificationCheckBox.Checked = _settings.AskClarification;
        maxTokensUpDown.Value = Math.Clamp(
            _settings.MaxTokens, (int)maxTokensUpDown.Minimum, (int)maxTokensUpDown.Maximum);

        RefreshModelChoices();
        selectedModelComboBox.Text = _settings.SelectedModel;
    }

    private void ShowKeyCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        apiKeyTextBox.UseSystemPasswordChar = !showKeyCheckBox.Checked;
    }

    private void AvailableModelsTextBox_TextChanged(object? sender, EventArgs e)
    {
        RefreshModelChoices();
    }

    /// <summary>Keeps the dropdown in sync with the CSV list without losing the typed selection.</summary>
    private void RefreshModelChoices()
    {
        var current = selectedModelComboBox.Text;

        selectedModelComboBox.Items.Clear();
        foreach (var model in availableModelsTextBox.Text
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            selectedModelComboBox.Items.Add(model);
        }

        selectedModelComboBox.Text = current;
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
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

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        var selectedModel = selectedModelComboBox.Text.Trim();
        if (string.IsNullOrEmpty(selectedModel))
        {
            MessageBox.Show(this, "Please select or enter a model.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.AnthropicApiKey = apiKeyTextBox.Text.Trim();
        _settings.AnthropicBaseUrl = baseUrlTextBox.Text.Trim();
        _settings.AvailableModels = availableModelsTextBox.Text.Trim();
        _settings.SelectedModel = selectedModel;
        _settings.DocumentFolder = documentFolderTextBox.Text.Trim();
        _settings.AskClarification = clarificationCheckBox.Checked;
        _settings.MaxTokens = (int)maxTokensUpDown.Value;

        try
        {
            SettingsStorage.Save(_settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save settings",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
