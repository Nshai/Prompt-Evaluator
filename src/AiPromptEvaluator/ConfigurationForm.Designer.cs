namespace AiPromptEvaluator;

partial class ConfigurationForm
{
    private System.ComponentModel.IContainer components = null;

    private TableLayoutPanel rootLayout;
    private GroupBox connectionGroup;
    private TableLayoutPanel connectionLayout;
    private Label apiKeyLabel;
    private TextBox apiKeyTextBox;
    private CheckBox showKeyCheckBox;
    private Label baseUrlLabel;
    private TextBox baseUrlTextBox;
    private Label baseUrlHintLabel;
    private Label doclingLabel;
    private TextBox doclingTextBox;
    private Button doclingTestButton;
    private Label doclingHintLabel;
    private GroupBox modelsGroup;
    private TableLayoutPanel modelsLayout;
    private Label availableModelsLabel;
    private TextBox availableModelsTextBox;
    private Label selectedModelLabel;
    private ComboBox selectedModelComboBox;
    private Label maxTokensLabel;
    private NumericUpDown maxTokensUpDown;
    private Label embeddingModelLabel;
    private TextBox embeddingModelTextBox;
    private Label embeddingDimensionsLabel;
    private NumericUpDown embeddingDimensionsUpDown;
    private GroupBox vectorGroup;
    private TableLayoutPanel vectorLayout;
    private Label qdrantLabel;
    private TextBox qdrantTextBox;
    private Button qdrantTestButton;
    private Label qdrantHintLabel;
    private Label collectionLabel;
    private TextBox collectionTextBox;
    private Label caseReferenceLabel;
    private TextBox caseReferenceTextBox;
    private Label caseReferenceHintLabel;
    private Label tenantLabel;
    private NumericUpDown tenantUpDown;
    private Label chunkTokensLabel;
    private NumericUpDown chunkTokensUpDown;
    private Label chunkOverlapLabel;
    private NumericUpDown chunkOverlapUpDown;
    private Label searchResultsLabel;
    private NumericUpDown searchResultsUpDown;
    private GroupBox contextGroup;
    private TableLayoutPanel contextLayout;
    private Label documentFolderLabel;
    private TextBox documentFolderTextBox;
    private Button browseButton;
    private CheckBox clarificationCheckBox;
    private FlowLayoutPanel buttonPanel;
    private Button saveButton;
    private Button cancelButton;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        rootLayout = new TableLayoutPanel();
        connectionGroup = new GroupBox();
        connectionLayout = new TableLayoutPanel();
        apiKeyLabel = new Label();
        apiKeyTextBox = new TextBox();
        showKeyCheckBox = new CheckBox();
        baseUrlLabel = new Label();
        baseUrlTextBox = new TextBox();
        baseUrlHintLabel = new Label();
        doclingLabel = new Label();
        doclingTextBox = new TextBox();
        doclingTestButton = new Button();
        doclingHintLabel = new Label();
        modelsGroup = new GroupBox();
        modelsLayout = new TableLayoutPanel();
        availableModelsLabel = new Label();
        availableModelsTextBox = new TextBox();
        selectedModelLabel = new Label();
        selectedModelComboBox = new ComboBox();
        maxTokensLabel = new Label();
        maxTokensUpDown = new NumericUpDown();
        embeddingModelLabel = new Label();
        embeddingModelTextBox = new TextBox();
        embeddingDimensionsLabel = new Label();
        embeddingDimensionsUpDown = new NumericUpDown();
        vectorGroup = new GroupBox();
        vectorLayout = new TableLayoutPanel();
        qdrantLabel = new Label();
        qdrantTextBox = new TextBox();
        qdrantTestButton = new Button();
        qdrantHintLabel = new Label();
        collectionLabel = new Label();
        collectionTextBox = new TextBox();
        caseReferenceLabel = new Label();
        caseReferenceTextBox = new TextBox();
        caseReferenceHintLabel = new Label();
        tenantLabel = new Label();
        tenantUpDown = new NumericUpDown();
        chunkTokensLabel = new Label();
        chunkTokensUpDown = new NumericUpDown();
        chunkOverlapLabel = new Label();
        chunkOverlapUpDown = new NumericUpDown();
        searchResultsLabel = new Label();
        searchResultsUpDown = new NumericUpDown();
        contextGroup = new GroupBox();
        contextLayout = new TableLayoutPanel();
        documentFolderLabel = new Label();
        documentFolderTextBox = new TextBox();
        browseButton = new Button();
        clarificationCheckBox = new CheckBox();
        buttonPanel = new FlowLayoutPanel();
        saveButton = new Button();
        cancelButton = new Button();

        rootLayout.SuspendLayout();
        connectionGroup.SuspendLayout();
        connectionLayout.SuspendLayout();
        modelsGroup.SuspendLayout();
        modelsLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)maxTokensUpDown).BeginInit();
        ((System.ComponentModel.ISupportInitialize)embeddingDimensionsUpDown).BeginInit();
        vectorGroup.SuspendLayout();
        vectorLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)tenantUpDown).BeginInit();
        ((System.ComponentModel.ISupportInitialize)chunkTokensUpDown).BeginInit();
        ((System.ComponentModel.ISupportInitialize)chunkOverlapUpDown).BeginInit();
        ((System.ComponentModel.ISupportInitialize)searchResultsUpDown).BeginInit();
        contextGroup.SuspendLayout();
        contextLayout.SuspendLayout();
        buttonPanel.SuspendLayout();
        SuspendLayout();

        // rootLayout
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Padding = new Padding(10);
        rootLayout.AutoScroll = true;
        rootLayout.RowCount = 6;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.Controls.Add(connectionGroup, 0, 0);
        rootLayout.Controls.Add(modelsGroup, 0, 1);
        rootLayout.Controls.Add(vectorGroup, 0, 2);
        rootLayout.Controls.Add(contextGroup, 0, 3);
        rootLayout.Controls.Add(clarificationCheckBox, 0, 4);
        rootLayout.Controls.Add(buttonPanel, 0, 5);
        rootLayout.Name = "rootLayout";

        // connectionGroup
        connectionGroup.AutoSize = true;
        connectionGroup.Controls.Add(connectionLayout);
        connectionGroup.Dock = DockStyle.Fill;
        connectionGroup.Margin = new Padding(0, 0, 0, 8);
        connectionGroup.Name = "connectionGroup";
        connectionGroup.Padding = new Padding(8);
        connectionGroup.TabStop = false;
        connectionGroup.Text = "Connection";

        // connectionLayout
        connectionLayout.AutoSize = true;
        connectionLayout.ColumnCount = 3;
        connectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        connectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        connectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        connectionLayout.Dock = DockStyle.Fill;
        connectionLayout.RowCount = 5;
        connectionLayout.Controls.Add(apiKeyLabel, 0, 0);
        connectionLayout.Controls.Add(apiKeyTextBox, 1, 0);
        connectionLayout.Controls.Add(showKeyCheckBox, 2, 0);
        connectionLayout.Controls.Add(baseUrlLabel, 0, 1);
        connectionLayout.Controls.Add(baseUrlTextBox, 1, 1);
        connectionLayout.Controls.Add(baseUrlHintLabel, 1, 2);
        connectionLayout.Controls.Add(doclingLabel, 0, 3);
        connectionLayout.Controls.Add(doclingTextBox, 1, 3);
        connectionLayout.Controls.Add(doclingTestButton, 2, 3);
        connectionLayout.Controls.Add(doclingHintLabel, 1, 4);
        connectionLayout.Name = "connectionLayout";

        // apiKeyLabel
        apiKeyLabel.Anchor = AnchorStyles.Left;
        apiKeyLabel.AutoSize = true;
        apiKeyLabel.Name = "apiKeyLabel";
        apiKeyLabel.Text = "API key";

        // apiKeyTextBox
        apiKeyTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        apiKeyTextBox.Margin = new Padding(0, 3, 8, 3);
        apiKeyTextBox.Name = "apiKeyTextBox";
        apiKeyTextBox.UseSystemPasswordChar = true;

        // showKeyCheckBox
        showKeyCheckBox.Anchor = AnchorStyles.Left;
        showKeyCheckBox.AutoSize = true;
        showKeyCheckBox.Name = "showKeyCheckBox";
        showKeyCheckBox.Text = "Show";
        showKeyCheckBox.UseVisualStyleBackColor = true;
        showKeyCheckBox.CheckedChanged += ShowKeyCheckBox_CheckedChanged;

        // baseUrlLabel
        baseUrlLabel.Anchor = AnchorStyles.Left;
        baseUrlLabel.AutoSize = true;
        baseUrlLabel.Name = "baseUrlLabel";
        baseUrlLabel.Text = "API base URL";

        // baseUrlTextBox
        baseUrlTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        baseUrlTextBox.Margin = new Padding(0, 3, 8, 0);
        baseUrlTextBox.Name = "baseUrlTextBox";

        // baseUrlHintLabel
        baseUrlHintLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        baseUrlHintLabel.AutoSize = true;
        baseUrlHintLabel.ForeColor = SystemColors.GrayText;
        baseUrlHintLabel.Margin = new Padding(0, 0, 8, 3);
        baseUrlHintLabel.Name = "baseUrlHintLabel";
        baseUrlHintLabel.Text = $"OpenAI-compatible endpoint — the official API, a gateway (e.g. https://litellm.example.com/v1) or a self-hosted server. Leave empty for {AppSettings.DefaultBaseUrl}.";

        // doclingLabel
        doclingLabel.Anchor = AnchorStyles.Left;
        doclingLabel.AutoSize = true;
        doclingLabel.Name = "doclingLabel";
        doclingLabel.Text = "Docling endpoint";

        // doclingTextBox
        doclingTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        doclingTextBox.Margin = new Padding(0, 3, 8, 0);
        doclingTextBox.Name = "doclingTextBox";
        doclingTextBox.PlaceholderText = AppSettings.DefaultDoclingEndpoint;

        // doclingTestButton
        doclingTestButton.Anchor = AnchorStyles.Left;
        doclingTestButton.AutoSize = true;
        doclingTestButton.Name = "doclingTestButton";
        doclingTestButton.Text = "Test";
        doclingTestButton.UseVisualStyleBackColor = true;
        doclingTestButton.Click += DoclingTestButton_Click;

        // doclingHintLabel
        doclingHintLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        doclingHintLabel.AutoSize = true;
        doclingHintLabel.ForeColor = SystemColors.GrayText;
        doclingHintLabel.Margin = new Padding(0, 0, 8, 3);
        doclingHintLabel.Name = "doclingHintLabel";
        doclingHintLabel.Text = $"Docker host running docling-serve, used to convert spreadsheets to Markdown. Leave empty for {AppSettings.DefaultDoclingEndpoint}.";

        // modelsGroup
        modelsGroup.AutoSize = true;
        modelsGroup.Controls.Add(modelsLayout);
        modelsGroup.Dock = DockStyle.Fill;
        modelsGroup.Margin = new Padding(0, 0, 0, 8);
        modelsGroup.Name = "modelsGroup";
        modelsGroup.Padding = new Padding(8);
        modelsGroup.TabStop = false;
        modelsGroup.Text = "Models";

        // modelsLayout
        modelsLayout.AutoSize = true;
        modelsLayout.ColumnCount = 2;
        modelsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        modelsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        modelsLayout.Dock = DockStyle.Fill;
        modelsLayout.RowCount = 5;
        modelsLayout.Controls.Add(availableModelsLabel, 0, 0);
        modelsLayout.Controls.Add(availableModelsTextBox, 1, 0);
        modelsLayout.Controls.Add(selectedModelLabel, 0, 1);
        modelsLayout.Controls.Add(selectedModelComboBox, 1, 1);
        modelsLayout.Controls.Add(maxTokensLabel, 0, 2);
        modelsLayout.Controls.Add(maxTokensUpDown, 1, 2);
        modelsLayout.Controls.Add(embeddingModelLabel, 0, 3);
        modelsLayout.Controls.Add(embeddingModelTextBox, 1, 3);
        modelsLayout.Controls.Add(embeddingDimensionsLabel, 0, 4);
        modelsLayout.Controls.Add(embeddingDimensionsUpDown, 1, 4);
        modelsLayout.Name = "modelsLayout";

        // availableModelsLabel
        availableModelsLabel.Anchor = AnchorStyles.Left;
        availableModelsLabel.AutoSize = true;
        availableModelsLabel.Name = "availableModelsLabel";
        availableModelsLabel.Text = "Available models (CSV)";

        // availableModelsTextBox
        availableModelsTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        availableModelsTextBox.Margin = new Padding(0, 3, 0, 3);
        availableModelsTextBox.Name = "availableModelsTextBox";
        availableModelsTextBox.TextChanged += AvailableModelsTextBox_TextChanged;

        // selectedModelLabel
        selectedModelLabel.Anchor = AnchorStyles.Left;
        selectedModelLabel.AutoSize = true;
        selectedModelLabel.Name = "selectedModelLabel";
        selectedModelLabel.Text = "Selected model";

        // selectedModelComboBox
        selectedModelComboBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        selectedModelComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        selectedModelComboBox.Margin = new Padding(0, 3, 0, 3);
        selectedModelComboBox.Name = "selectedModelComboBox";

        // maxTokensLabel
        maxTokensLabel.Anchor = AnchorStyles.Left;
        maxTokensLabel.AutoSize = true;
        maxTokensLabel.Name = "maxTokensLabel";
        maxTokensLabel.Text = "Max output tokens";

        // maxTokensUpDown
        maxTokensUpDown.Anchor = AnchorStyles.Left;
        maxTokensUpDown.Increment = 256;
        maxTokensUpDown.Margin = new Padding(0, 3, 0, 3);
        maxTokensUpDown.Maximum = 64000;
        maxTokensUpDown.Minimum = 256;
        maxTokensUpDown.Name = "maxTokensUpDown";
        maxTokensUpDown.Width = 120;

        // embeddingModelLabel
        embeddingModelLabel.Anchor = AnchorStyles.Left;
        embeddingModelLabel.AutoSize = true;
        embeddingModelLabel.Name = "embeddingModelLabel";
        embeddingModelLabel.Text = "Embedding model";

        // embeddingModelTextBox
        embeddingModelTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        embeddingModelTextBox.Margin = new Padding(0, 3, 0, 3);
        embeddingModelTextBox.Name = "embeddingModelTextBox";
        embeddingModelTextBox.PlaceholderText = "text-embedding-3-small";

        // embeddingDimensionsLabel
        embeddingDimensionsLabel.Anchor = AnchorStyles.Left;
        embeddingDimensionsLabel.AutoSize = true;
        embeddingDimensionsLabel.Name = "embeddingDimensionsLabel";
        embeddingDimensionsLabel.Text = "Embedding dimensions";

        // embeddingDimensionsUpDown — defines the collection, so changing it means reloading docs
        embeddingDimensionsUpDown.Anchor = AnchorStyles.Left;
        embeddingDimensionsUpDown.Increment = 128;
        embeddingDimensionsUpDown.Margin = new Padding(0, 3, 0, 3);
        embeddingDimensionsUpDown.Maximum = 8192;
        embeddingDimensionsUpDown.Minimum = 64;
        embeddingDimensionsUpDown.Name = "embeddingDimensionsUpDown";
        embeddingDimensionsUpDown.Width = 120;

        // vectorGroup
        vectorGroup.AutoSize = true;
        vectorGroup.Controls.Add(vectorLayout);
        vectorGroup.Dock = DockStyle.Fill;
        vectorGroup.Margin = new Padding(0, 0, 0, 8);
        vectorGroup.Name = "vectorGroup";
        vectorGroup.Padding = new Padding(8);
        vectorGroup.TabStop = false;
        vectorGroup.Text = "Vector store (Qdrant)";

        // vectorLayout
        vectorLayout.AutoSize = true;
        vectorLayout.ColumnCount = 3;
        vectorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        vectorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        vectorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        vectorLayout.Dock = DockStyle.Fill;
        vectorLayout.RowCount = 9;
        vectorLayout.Controls.Add(qdrantLabel, 0, 0);
        vectorLayout.Controls.Add(qdrantTextBox, 1, 0);
        vectorLayout.Controls.Add(qdrantTestButton, 2, 0);
        vectorLayout.Controls.Add(qdrantHintLabel, 1, 1);
        vectorLayout.Controls.Add(collectionLabel, 0, 2);
        vectorLayout.Controls.Add(collectionTextBox, 1, 2);
        vectorLayout.Controls.Add(caseReferenceLabel, 0, 3);
        vectorLayout.Controls.Add(caseReferenceTextBox, 1, 3);
        vectorLayout.Controls.Add(caseReferenceHintLabel, 1, 4);
        vectorLayout.Controls.Add(tenantLabel, 0, 5);
        vectorLayout.Controls.Add(tenantUpDown, 1, 5);
        vectorLayout.Controls.Add(chunkTokensLabel, 0, 6);
        vectorLayout.Controls.Add(chunkTokensUpDown, 1, 6);
        vectorLayout.Controls.Add(chunkOverlapLabel, 0, 7);
        vectorLayout.Controls.Add(chunkOverlapUpDown, 1, 7);
        vectorLayout.Controls.Add(searchResultsLabel, 0, 8);
        vectorLayout.Controls.Add(searchResultsUpDown, 1, 8);
        vectorLayout.Name = "vectorLayout";

        // qdrantLabel
        qdrantLabel.Anchor = AnchorStyles.Left;
        qdrantLabel.AutoSize = true;
        qdrantLabel.Name = "qdrantLabel";
        qdrantLabel.Text = "Qdrant endpoint";

        // qdrantTextBox
        qdrantTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        qdrantTextBox.Margin = new Padding(0, 3, 8, 0);
        qdrantTextBox.Name = "qdrantTextBox";
        qdrantTextBox.PlaceholderText = AppSettings.DefaultQdrantEndpoint;

        // qdrantTestButton
        qdrantTestButton.Anchor = AnchorStyles.Left;
        qdrantTestButton.AutoSize = true;
        qdrantTestButton.Name = "qdrantTestButton";
        qdrantTestButton.Text = "Test";
        qdrantTestButton.UseVisualStyleBackColor = true;
        qdrantTestButton.Click += QdrantTestButton_Click;

        // qdrantHintLabel
        qdrantHintLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        qdrantHintLabel.AutoSize = true;
        qdrantHintLabel.ForeColor = SystemColors.GrayText;
        qdrantHintLabel.Margin = new Padding(0, 0, 8, 3);
        qdrantHintLabel.Name = "qdrantHintLabel";
        qdrantHintLabel.Text = $"Docker host running Qdrant, holding the indexed case chunks. This is the gRPC port, not the 6333 dashboard. Leave empty for {AppSettings.DefaultQdrantEndpoint}.";

        // collectionLabel
        collectionLabel.Anchor = AnchorStyles.Left;
        collectionLabel.AutoSize = true;
        collectionLabel.Name = "collectionLabel";
        collectionLabel.Text = "Collection";

        // collectionTextBox
        collectionTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        collectionTextBox.Margin = new Padding(0, 3, 8, 3);
        collectionTextBox.Name = "collectionTextBox";
        collectionTextBox.PlaceholderText = AppSettings.DefaultQdrantCollection;

        // caseReferenceLabel
        caseReferenceLabel.Anchor = AnchorStyles.Left;
        caseReferenceLabel.AutoSize = true;
        caseReferenceLabel.Name = "caseReferenceLabel";
        caseReferenceLabel.Text = "Case reference";

        // caseReferenceTextBox
        caseReferenceTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        caseReferenceTextBox.Margin = new Padding(0, 3, 8, 0);
        caseReferenceTextBox.Name = "caseReferenceTextBox";
        caseReferenceTextBox.PlaceholderText = "(the case folder's name)";

        // caseReferenceHintLabel
        caseReferenceHintLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        caseReferenceHintLabel.AutoSize = true;
        caseReferenceHintLabel.ForeColor = SystemColors.GrayText;
        caseReferenceHintLabel.Margin = new Padding(0, 0, 8, 3);
        caseReferenceHintLabel.Name = "caseReferenceHintLabel";
        caseReferenceHintLabel.Text = "Stamped on every indexed chunk and used to scope every search. Leave empty to use the case folder's name.";

        // tenantLabel
        tenantLabel.Anchor = AnchorStyles.Left;
        tenantLabel.AutoSize = true;
        tenantLabel.Name = "tenantLabel";
        tenantLabel.Text = "Tenant id";

        // tenantUpDown
        tenantUpDown.Anchor = AnchorStyles.Left;
        tenantUpDown.Margin = new Padding(0, 3, 0, 3);
        tenantUpDown.Maximum = 1000000;
        tenantUpDown.Minimum = 0;
        tenantUpDown.Name = "tenantUpDown";
        tenantUpDown.Width = 120;

        // chunkTokensLabel
        chunkTokensLabel.Anchor = AnchorStyles.Left;
        chunkTokensLabel.AutoSize = true;
        chunkTokensLabel.Name = "chunkTokensLabel";
        chunkTokensLabel.Text = "Max tokens per chunk";

        // chunkTokensUpDown
        chunkTokensUpDown.Anchor = AnchorStyles.Left;
        chunkTokensUpDown.Increment = 50;
        chunkTokensUpDown.Margin = new Padding(0, 3, 0, 3);
        chunkTokensUpDown.Maximum = 4000;
        chunkTokensUpDown.Minimum = 64;
        chunkTokensUpDown.Name = "chunkTokensUpDown";
        chunkTokensUpDown.Width = 120;

        // chunkOverlapLabel
        chunkOverlapLabel.Anchor = AnchorStyles.Left;
        chunkOverlapLabel.AutoSize = true;
        chunkOverlapLabel.Name = "chunkOverlapLabel";
        chunkOverlapLabel.Text = "Chunk overlap tokens";

        // chunkOverlapUpDown
        chunkOverlapUpDown.Anchor = AnchorStyles.Left;
        chunkOverlapUpDown.Increment = 25;
        chunkOverlapUpDown.Margin = new Padding(0, 3, 0, 3);
        chunkOverlapUpDown.Maximum = 2000;
        chunkOverlapUpDown.Minimum = 0;
        chunkOverlapUpDown.Name = "chunkOverlapUpDown";
        chunkOverlapUpDown.Width = 120;

        // searchResultsLabel
        searchResultsLabel.Anchor = AnchorStyles.Left;
        searchResultsLabel.AutoSize = true;
        searchResultsLabel.Name = "searchResultsLabel";
        searchResultsLabel.Text = "Results per search";

        // searchResultsUpDown
        searchResultsUpDown.Anchor = AnchorStyles.Left;
        searchResultsUpDown.Margin = new Padding(0, 3, 0, 3);
        searchResultsUpDown.Maximum = 50;
        searchResultsUpDown.Minimum = 1;
        searchResultsUpDown.Name = "searchResultsUpDown";
        searchResultsUpDown.Width = 120;

        // contextGroup
        contextGroup.AutoSize = true;
        contextGroup.Controls.Add(contextLayout);
        contextGroup.Dock = DockStyle.Fill;
        contextGroup.Margin = new Padding(0, 0, 0, 8);
        contextGroup.Name = "contextGroup";
        contextGroup.Padding = new Padding(8);
        contextGroup.TabStop = false;
        contextGroup.Text = "Context";

        // contextLayout
        contextLayout.AutoSize = true;
        contextLayout.ColumnCount = 3;
        contextLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        contextLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        contextLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        contextLayout.Dock = DockStyle.Fill;
        contextLayout.RowCount = 1;
        contextLayout.Controls.Add(documentFolderLabel, 0, 0);
        contextLayout.Controls.Add(documentFolderTextBox, 1, 0);
        contextLayout.Controls.Add(browseButton, 2, 0);
        contextLayout.Name = "contextLayout";

        // documentFolderLabel
        documentFolderLabel.Anchor = AnchorStyles.Left;
        documentFolderLabel.AutoSize = true;
        documentFolderLabel.Name = "documentFolderLabel";
        documentFolderLabel.Text = "Document folder";

        // documentFolderTextBox
        documentFolderTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        documentFolderTextBox.Margin = new Padding(0, 3, 8, 3);
        documentFolderTextBox.Name = "documentFolderTextBox";

        // browseButton
        browseButton.Anchor = AnchorStyles.Left;
        browseButton.AutoSize = true;
        browseButton.Name = "browseButton";
        browseButton.Padding = new Padding(8, 2, 8, 2);
        browseButton.Text = "Browse...";
        browseButton.UseVisualStyleBackColor = true;
        browseButton.Click += BrowseButton_Click;

        // clarificationCheckBox
        clarificationCheckBox.AutoSize = true;
        clarificationCheckBox.Margin = new Padding(0, 0, 0, 8);
        clarificationCheckBox.Name = "clarificationCheckBox";
        clarificationCheckBox.Text = "Ask for clarification when the prompt is ambiguous";
        clarificationCheckBox.UseVisualStyleBackColor = true;

        // buttonPanel
        buttonPanel.AutoSize = true;
        buttonPanel.Dock = DockStyle.Fill;
        buttonPanel.FlowDirection = FlowDirection.RightToLeft;
        buttonPanel.Margin = new Padding(0);
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Name = "buttonPanel";

        // saveButton
        saveButton.AutoSize = true;
        saveButton.Margin = new Padding(8, 0, 0, 0);
        saveButton.Name = "saveButton";
        saveButton.Padding = new Padding(12, 4, 12, 4);
        saveButton.Text = "Save";
        saveButton.UseVisualStyleBackColor = true;
        saveButton.Click += SaveButton_Click;

        // cancelButton
        cancelButton.AutoSize = true;
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Margin = new Padding(0);
        cancelButton.Name = "cancelButton";
        cancelButton.Padding = new Padding(12, 4, 12, 4);
        cancelButton.Text = "Cancel";
        cancelButton.UseVisualStyleBackColor = true;

        // ConfigurationForm
        AcceptButton = saveButton;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = cancelButton;
        ClientSize = new Size(720, 460);
        Controls.Add(rootLayout);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(640, 440);
        Name = "ConfigurationForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Configuration";

        buttonPanel.ResumeLayout(false);
        buttonPanel.PerformLayout();
        contextLayout.ResumeLayout(false);
        contextLayout.PerformLayout();
        contextGroup.ResumeLayout(false);
        contextGroup.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)searchResultsUpDown).EndInit();
        ((System.ComponentModel.ISupportInitialize)chunkOverlapUpDown).EndInit();
        ((System.ComponentModel.ISupportInitialize)chunkTokensUpDown).EndInit();
        ((System.ComponentModel.ISupportInitialize)tenantUpDown).EndInit();
        vectorLayout.ResumeLayout(false);
        vectorLayout.PerformLayout();
        vectorGroup.ResumeLayout(false);
        vectorGroup.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)embeddingDimensionsUpDown).EndInit();
        ((System.ComponentModel.ISupportInitialize)maxTokensUpDown).EndInit();
        modelsLayout.ResumeLayout(false);
        modelsLayout.PerformLayout();
        modelsGroup.ResumeLayout(false);
        modelsGroup.PerformLayout();
        connectionLayout.ResumeLayout(false);
        connectionLayout.PerformLayout();
        connectionGroup.ResumeLayout(false);
        connectionGroup.PerformLayout();
        rootLayout.ResumeLayout(false);
        rootLayout.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
