namespace AiPromptEvaluator;

public partial class ConfigurationForm : Form
{
    private readonly AppSettings _settings;

    public ConfigurationForm(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        apiKeyTextBox.Text = _settings.OpenAiApiKey;
        baseUrlTextBox.Text = _settings.OpenAiBaseUrl;
        doclingTextBox.Text = _settings.DoclingEndpoint;
        availableModelsTextBox.Text = _settings.AvailableModels;
        documentFolderTextBox.Text = _settings.DocumentFolder;
        maxTokensUpDown.Value = Clamp(maxTokensUpDown, _settings.MaxTokens);

        embeddingModelTextBox.Text = _settings.EmbeddingModel;
        embeddingDimensionsUpDown.Value = Clamp(embeddingDimensionsUpDown, _settings.EmbeddingDimensions);
        qdrantTextBox.Text = _settings.QdrantEndpoint;
        collectionTextBox.Text = _settings.QdrantCollection;
        caseReferenceTextBox.Text = _settings.CaseReference;
        tenantUpDown.Value = Clamp(tenantUpDown, _settings.TenantId);
        chunkTokensUpDown.Value = Clamp(chunkTokensUpDown, _settings.MaxTokensPerChunk);
        chunkOverlapUpDown.Value = Clamp(chunkOverlapUpDown, _settings.ChunkOverlapTokens);
        searchResultsUpDown.Value = Clamp(searchResultsUpDown, _settings.MaxSearchResults);
        embeddingCharsUpDown.Value = Clamp(embeddingCharsUpDown, _settings.MaxEmbeddingInputCharacters);

        embeddingBaseUrlTextBox.Text = _settings.EmbeddingBaseUrl;
        embeddingApiKeyTextBox.Text = _settings.EmbeddingApiKey;
        promptLogTextBox.Text = _settings.PromptLogFolder;

        passagesPerGroupUpDown.Value = Clamp(passagesPerGroupUpDown, _settings.MaxPassagesPerGroup);
        reserveCategoryUpDown.Value = Clamp(reserveCategoryUpDown, _settings.ReservedSlotsPerTargetedCategory);
        reserveSectionUpDown.Value = Clamp(reserveSectionUpDown, _settings.ReservedSlotsPerDeclaredSection);
        reserveTopScoreUpDown.Value = Clamp(reserveTopScoreUpDown, _settings.ReservedSlotsForTopScore);
        nearDuplicateUpDown.Value = Math.Clamp(
            (decimal)_settings.NearDuplicateOverlap,
            nearDuplicateUpDown.Minimum,
            nearDuplicateUpDown.Maximum);
        decisionTokensUpDown.Value = Clamp(decisionTokensUpDown, _settings.DecisionMaxTokens);
        extractionReportUpDown.Value = Clamp(extractionReportUpDown, _settings.ExtractionReportMaxChars);
        parallelRequestsUpDown.Value = Clamp(parallelRequestsUpDown, _settings.MaxParallelRequests);
        parallelChecksUpDown.Value = Clamp(parallelChecksUpDown, _settings.MaxParallelChecks);
        coreQueriesOnlyCheckBox.Checked = _settings.CoreQueriesOnly;

        canonicalSchemaTextBox.Text = _settings.CanonicalSchemaPath;
        checkPlanTextBox.Text = _settings.CheckPlanFolder;
        canonicalDbTextBox.Text = _settings.CanonicalModelDbPath;
        runDbTextBox.Text = _settings.CheckRunDbPath;
        extractionTokensUpDown.Value = Clamp(extractionTokensUpDown, _settings.ExtractionMaxTokens);

        structuredFindingsCheckBox.Checked = _settings.StructuredFindings;

        pinTemperatureCheckBox.Checked = _settings.PinTemperature;
        temperatureUpDown.Value = Clamp(temperatureUpDown, _settings.Temperature);
        pinTopPCheckBox.Checked = _settings.PinTopP;
        topPUpDown.Value = Clamp(topPUpDown, _settings.TopP);
        pinSeedCheckBox.Checked = _settings.PinSeed;
        seedUpDown.Value = Math.Clamp(_settings.SamplingSeed, (long)seedUpDown.Minimum, (long)seedUpDown.Maximum);

        UpdateSamplingEnablement();

        RefreshModelChoices();
        selectedModelComboBox.Text = _settings.SelectedModel;
    }

    /// <summary>Keeps a stored value inside the control's range, so a hand-edited settings file can't throw.</summary>
    private static decimal Clamp(NumericUpDown control, int value) =>
        Math.Clamp(value, control.Minimum, control.Maximum);

    /// <summary>As above, for the decimal-valued sampling controls (temperature, top-p).</summary>
    private static decimal Clamp(NumericUpDown control, float value) =>
        Math.Clamp((decimal)value, control.Minimum, control.Maximum);

    /// <summary>
    /// Greys out each pinned value alongside its checkbox — a seed the user cannot edit when
    /// "pin seed" is off is a clearer signal than a value that quietly stops applying.
    /// </summary>
    private void UpdateSamplingEnablement()
    {
        temperatureUpDown.Enabled = pinTemperatureCheckBox.Checked;
        topPUpDown.Enabled = pinTopPCheckBox.Checked;
        seedUpDown.Enabled = pinSeedCheckBox.Checked;
    }

    private void PinTemperatureCheckBox_CheckedChanged(object? sender, EventArgs e) => UpdateSamplingEnablement();

    private void PinTopPCheckBox_CheckedChanged(object? sender, EventArgs e) => UpdateSamplingEnablement();

    private void PinSeedCheckBox_CheckedChanged(object? sender, EventArgs e) => UpdateSamplingEnablement();

    private void CanonicalSchemaBrowseButton_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select the canonical model JSON Schema",
            Filter = "JSON schema (*.json)|*.json|All files (*.*)|*.*",
        };

        // Start where the current setting points, or where the deployed copy lives — the
        // user is nearly always looking for a sibling of one or the other.
        var current = _settings.ResolveCanonicalSchemaPath();
        if (File.Exists(current))
        {
            dlg.InitialDirectory = Path.GetDirectoryName(current);
            dlg.FileName = Path.GetFileName(current);
        }

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            canonicalSchemaTextBox.Text = dlg.FileName;
        }
    }

    private void CheckPlanBrowseButton_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select the folder holding the CHK-*.query-plan.json files",
            UseDescriptionForTitle = true,
        };

        var current = _settings.ResolveCheckPlanFolder();
        if (Directory.Exists(current))
        {
            dlg.SelectedPath = current;
        }

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            checkPlanTextBox.Text = dlg.SelectedPath;
        }
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

    /// <summary>
    /// Probes the endpoint as typed, without saving — so a wrong address is caught here
    /// rather than partway through a document load.
    /// </summary>
    private async void DoclingTestButton_Click(object? sender, EventArgs e)
    {
        var endpoint = doclingTextBox.Text.Trim();
        if (string.IsNullOrEmpty(endpoint))
        {
            endpoint = AppSettings.DefaultDoclingEndpoint;
        }

        doclingTestButton.Enabled = false;
        doclingTestButton.Text = "Testing...";
        try
        {
            var client = new DoclingClient(() => endpoint);
            var available = await client.IsAvailableAsync().ConfigureAwait(true);

            if (available)
            {
                MessageBox.Show(this, $"Docling is responding at {client.Endpoint}.", "Docling",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this,
                    $"No response from {client.Endpoint}.\n\n"
                    + "Check that the docling-serve container is running and the port matches.",
                    "Docling", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            doclingTestButton.Text = "Test";
            doclingTestButton.Enabled = true;
        }
    }

    /// <summary>
    /// Sends one minimal chat request as typed, without saving — a rejected key, a wrong base
    /// URL, or a model/gateway that rejects a sampling parameter is then caught here with its
    /// exact error text, rather than surfacing as an identical "Error" on all ten checks with
    /// nothing to go on.
    /// </summary>
    private async void ChatTestButton_Click(object? sender, EventArgs e)
    {
        var model = selectedModelComboBox.Text.Trim();
        if (string.IsNullOrEmpty(model))
        {
            MessageBox.Show(this, "Please select or enter a model first.", "Chat model",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var probe = new AppSettings
        {
            OpenAiApiKey = apiKeyTextBox.Text.Trim(),
            OpenAiBaseUrl = baseUrlTextBox.Text.Trim(),
            SelectedModel = model,
            MaxTokens = 16,
            PinTemperature = pinTemperatureCheckBox.Checked,
            Temperature = (float)temperatureUpDown.Value,
            PinTopP = pinTopPCheckBox.Checked,
            TopP = (float)topPUpDown.Value,
            PinSeed = pinSeedCheckBox.Checked,
            SamplingSeed = (long)seedUpDown.Value,
        };

        chatTestButton.Enabled = false;
        chatTestButton.Text = "Testing...";
        try
        {
            var evaluator = new ChatCompletionClient(probe);
            var result = await evaluator
                .RunRawAsync("Reply with the single word: ready", CancellationToken.None)
                .ConfigureAwait(true);

            MessageBox.Show(this,
                $"{model} responded at {probe.ResolveBaseUrl()}.\n\nReply: {result.Response.Trim()}",
                "Chat model", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Chat model", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            chatTestButton.Text = "Test";
            chatTestButton.Enabled = true;
        }
    }

    /// <summary>
    /// Probes Qdrant as typed, without saving — a wrong port (6333 instead of 6334 is the
    /// usual one) is then caught here rather than partway through indexing a case.
    /// </summary>
    private async void QdrantTestButton_Click(object? sender, EventArgs e)
    {
        var probe = new AppSettings
        {
            QdrantEndpoint = qdrantTextBox.Text.Trim(),
            QdrantApiKey = _settings.QdrantApiKey,
            QdrantCollection = collectionTextBox.Text.Trim(),
        };

        qdrantTestButton.Enabled = false;
        qdrantTestButton.Text = "Testing...";
        try
        {
            using var store = new CaseDocumentStore(probe);
            var available = await store.IsAvailableAsync().ConfigureAwait(true);

            if (available)
            {
                MessageBox.Show(this, $"Qdrant is responding at {store.Endpoint}.", "Qdrant",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this,
                    $"No response from {store.Endpoint}.\n\n"
                    + "Check that the Qdrant container is running and that the port is the gRPC one (6334).",
                    "Qdrant", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Qdrant", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            qdrantTestButton.Text = "Test";
            qdrantTestButton.Enabled = true;
        }
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

    private void PromptLogBrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a folder to write prompt and response logs to",
            UseDescriptionForTitle = true
        };

        if (Directory.Exists(promptLogTextBox.Text))
        {
            dialog.SelectedPath = promptLogTextBox.Text;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            promptLogTextBox.Text = dialog.SelectedPath;
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

        var embeddingModel = embeddingModelTextBox.Text.Trim();
        if (string.IsNullOrEmpty(embeddingModel))
        {
            MessageBox.Show(this, "Please enter an embedding model — searching the case file needs one.",
                "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.OpenAiApiKey = apiKeyTextBox.Text.Trim();
        _settings.OpenAiBaseUrl = baseUrlTextBox.Text.Trim();
        _settings.DoclingEndpoint = doclingTextBox.Text.Trim();
        _settings.AvailableModels = availableModelsTextBox.Text.Trim();
        _settings.SelectedModel = selectedModel;
        _settings.DocumentFolder = documentFolderTextBox.Text.Trim();
        _settings.MaxTokens = (int)maxTokensUpDown.Value;

        _settings.EmbeddingModel = embeddingModel;
        _settings.EmbeddingDimensions = (int)embeddingDimensionsUpDown.Value;
        _settings.QdrantEndpoint = qdrantTextBox.Text.Trim();
        _settings.QdrantCollection = collectionTextBox.Text.Trim();
        _settings.CaseReference = caseReferenceTextBox.Text.Trim();
        _settings.TenantId = (int)tenantUpDown.Value;
        _settings.MaxTokensPerChunk = (int)chunkTokensUpDown.Value;
        _settings.ChunkOverlapTokens = (int)chunkOverlapUpDown.Value;
        _settings.MaxSearchResults = (int)searchResultsUpDown.Value;
        _settings.MaxEmbeddingInputCharacters = (int)embeddingCharsUpDown.Value;

        _settings.EmbeddingBaseUrl = embeddingBaseUrlTextBox.Text.Trim();
        _settings.EmbeddingApiKey = embeddingApiKeyTextBox.Text.Trim();
        _settings.PromptLogFolder = promptLogTextBox.Text.Trim();

        _settings.MaxPassagesPerGroup = (int)passagesPerGroupUpDown.Value;
        _settings.ReservedSlotsPerTargetedCategory = (int)reserveCategoryUpDown.Value;
        _settings.ReservedSlotsPerDeclaredSection = (int)reserveSectionUpDown.Value;
        _settings.ReservedSlotsForTopScore = (int)reserveTopScoreUpDown.Value;
        _settings.NearDuplicateOverlap = (double)nearDuplicateUpDown.Value;
        _settings.DecisionMaxTokens = (int)decisionTokensUpDown.Value;
        _settings.ExtractionReportMaxChars = (int)extractionReportUpDown.Value;
        _settings.MaxParallelRequests = (int)parallelRequestsUpDown.Value;
        _settings.MaxParallelChecks = (int)parallelChecksUpDown.Value;
        _settings.CoreQueriesOnly = coreQueriesOnlyCheckBox.Checked;

        _settings.CanonicalSchemaPath = canonicalSchemaTextBox.Text.Trim();
        _settings.CheckPlanFolder = checkPlanTextBox.Text.Trim();
        _settings.CanonicalModelDbPath = canonicalDbTextBox.Text.Trim();
        _settings.CheckRunDbPath = runDbTextBox.Text.Trim();
        _settings.ExtractionMaxTokens = (int)extractionTokensUpDown.Value;

        _settings.StructuredFindings = structuredFindingsCheckBox.Checked;

        _settings.PinTemperature = pinTemperatureCheckBox.Checked;
        _settings.Temperature = (float)temperatureUpDown.Value;
        _settings.PinTopP = pinTopPCheckBox.Checked;
        _settings.TopP = (float)topPUpDown.Value;
        _settings.PinSeed = pinSeedCheckBox.Checked;
        _settings.SamplingSeed = (long)seedUpDown.Value;

        try
        {
            SettingsStore.Save(_settings);
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
