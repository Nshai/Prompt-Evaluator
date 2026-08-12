namespace AiPromptEvaluator;

partial class ConfigurationForm
{
    private System.ComponentModel.IContainer components = null;

    private Panel scrollPanel;
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
    private Button chatTestButton;
    private Label embeddingModelLabel;
    private TextBox embeddingModelTextBox;
    private FlowLayoutPanel modelsNumericRow;
    private Label maxTokensLabel;
    private NumericUpDown maxTokensUpDown;
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
    private FlowLayoutPanel vectorNumericRow;
    private Label tenantLabel;
    private NumericUpDown tenantUpDown;
    private Label chunkTokensLabel;
    private NumericUpDown chunkTokensUpDown;
    private Label chunkOverlapLabel;
    private NumericUpDown chunkOverlapUpDown;
    private Label searchResultsLabel;
    private NumericUpDown searchResultsUpDown;
    private GroupBox canonicalGroup;
    private TableLayoutPanel canonicalLayout;
    private Label canonicalSchemaLabel;
    private TextBox canonicalSchemaTextBox;
    private Button canonicalSchemaBrowseButton;
    private Label checkPlanLabel;
    private TextBox checkPlanTextBox;
    private Button checkPlanBrowseButton;
    private Label canonicalDbLabel;
    private TextBox canonicalDbTextBox;
    private Label canonicalHintLabel;
    private Label extractionTokensLabel;
    private NumericUpDown extractionTokensUpDown;
    private GroupBox reproducibilityGroup;
    private TableLayoutPanel reproducibilityLayout;
    private CheckBox structuredFindingsCheckBox;
    private FlowLayoutPanel samplingRow;
    private CheckBox pinTemperatureCheckBox;
    private NumericUpDown temperatureUpDown;
    private CheckBox pinTopPCheckBox;
    private NumericUpDown topPUpDown;
    private CheckBox pinSeedCheckBox;
    private NumericUpDown seedUpDown;
    private Label reproducibilityHintLabel;
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
        scrollPanel = new Panel();
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
        chatTestButton = new Button();
        embeddingModelLabel = new Label();
        embeddingModelTextBox = new TextBox();
        modelsNumericRow = new FlowLayoutPanel();
        maxTokensLabel = new Label();
        maxTokensUpDown = new NumericUpDown();
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
        vectorNumericRow = new FlowLayoutPanel();
        tenantLabel = new Label();
        tenantUpDown = new NumericUpDown();
        chunkTokensLabel = new Label();
        chunkTokensUpDown = new NumericUpDown();
        chunkOverlapLabel = new Label();
        chunkOverlapUpDown = new NumericUpDown();
        searchResultsLabel = new Label();
        searchResultsUpDown = new NumericUpDown();
        canonicalGroup = new GroupBox();
        canonicalLayout = new TableLayoutPanel();
        canonicalSchemaLabel = new Label();
        canonicalSchemaTextBox = new TextBox();
        canonicalSchemaBrowseButton = new Button();
        checkPlanLabel = new Label();
        checkPlanTextBox = new TextBox();
        checkPlanBrowseButton = new Button();
        canonicalDbLabel = new Label();
        canonicalDbTextBox = new TextBox();
        canonicalHintLabel = new Label();
        extractionTokensLabel = new Label();
        extractionTokensUpDown = new NumericUpDown();
        reproducibilityGroup = new GroupBox();
        reproducibilityLayout = new TableLayoutPanel();
        structuredFindingsCheckBox = new CheckBox();
        samplingRow = new FlowLayoutPanel();
        pinTemperatureCheckBox = new CheckBox();
        temperatureUpDown = new NumericUpDown();
        pinTopPCheckBox = new CheckBox();
        topPUpDown = new NumericUpDown();
        pinSeedCheckBox = new CheckBox();
        seedUpDown = new NumericUpDown();
        reproducibilityHintLabel = new Label();
        contextGroup = new GroupBox();
        contextLayout = new TableLayoutPanel();
        documentFolderLabel = new Label();
        documentFolderTextBox = new TextBox();
        browseButton = new Button();
        clarificationCheckBox = new CheckBox();
        buttonPanel = new FlowLayoutPanel();
        saveButton = new Button();
        cancelButton = new Button();

        scrollPanel.SuspendLayout();
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
        canonicalGroup.SuspendLayout();
        canonicalLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)extractionTokensUpDown).BeginInit();
        reproducibilityGroup.SuspendLayout();
        reproducibilityLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)temperatureUpDown).BeginInit();
        ((System.ComponentModel.ISupportInitialize)topPUpDown).BeginInit();
        ((System.ComponentModel.ISupportInitialize)seedUpDown).BeginInit();
        contextGroup.SuspendLayout();
        contextLayout.SuspendLayout();
        buttonPanel.SuspendLayout();
        SuspendLayout();

        // scrollPanel — everything but the Save/Cancel buttons scrolls here, so the form stays
        // usable when it is resized smaller than its content, and the buttons stay reachable.
        scrollPanel.AutoScroll = true;
        scrollPanel.Controls.Add(rootLayout);
        scrollPanel.Dock = DockStyle.Fill;
        scrollPanel.Name = "scrollPanel";

        // rootLayout
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.AutoSize = true;
        rootLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        rootLayout.Dock = DockStyle.Top;
        rootLayout.Padding = new Padding(10);
        rootLayout.RowCount = 7;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.Controls.Add(connectionGroup, 0, 0);
        rootLayout.Controls.Add(modelsGroup, 0, 1);
        rootLayout.Controls.Add(vectorGroup, 0, 2);
        rootLayout.Controls.Add(canonicalGroup, 0, 3);
        rootLayout.Controls.Add(reproducibilityGroup, 0, 4);
        rootLayout.Controls.Add(contextGroup, 0, 5);
        rootLayout.Controls.Add(clarificationCheckBox, 0, 6);
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
        baseUrlHintLabel.MaximumSize = new Size(500, 0);
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
        doclingHintLabel.MaximumSize = new Size(500, 0);
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
        modelsLayout.ColumnCount = 3;
        modelsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        modelsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        modelsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        modelsLayout.Dock = DockStyle.Fill;
        modelsLayout.RowCount = 4;
        modelsLayout.Controls.Add(availableModelsLabel, 0, 0);
        modelsLayout.Controls.Add(availableModelsTextBox, 1, 0);
        modelsLayout.SetColumnSpan(availableModelsTextBox, 2);
        modelsLayout.Controls.Add(selectedModelLabel, 0, 1);
        modelsLayout.Controls.Add(selectedModelComboBox, 1, 1);
        modelsLayout.Controls.Add(chatTestButton, 2, 1);
        modelsLayout.Controls.Add(embeddingModelLabel, 0, 2);
        modelsLayout.Controls.Add(embeddingModelTextBox, 1, 2);
        modelsLayout.SetColumnSpan(embeddingModelTextBox, 2);
        modelsLayout.Controls.Add(modelsNumericRow, 0, 3);
        modelsLayout.SetColumnSpan(modelsNumericRow, 3);
        modelsLayout.Name = "modelsLayout";

        // modelsNumericRow — max output tokens and embedding dimensions are both short numeric
        // fields, so they sit side by side instead of each claiming a full-width row. Anchor
        // (not Dock) takes the width from the parent cell while AutoSize computes the panel's
        // own height from however many rows WrapContents produces — the combination a wrapping
        // FlowLayoutPanel needs; Dock on an AutoSize row was clipping content against the
        // group border because the two AutoSize computations fought each other.
        modelsNumericRow.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        modelsNumericRow.AutoSize = true;
        modelsNumericRow.Controls.Add(maxTokensLabel);
        modelsNumericRow.Controls.Add(maxTokensUpDown);
        modelsNumericRow.Controls.Add(embeddingDimensionsLabel);
        modelsNumericRow.Controls.Add(embeddingDimensionsUpDown);
        modelsNumericRow.Margin = new Padding(0, 3, 0, 0);
        modelsNumericRow.Name = "modelsNumericRow";
        modelsNumericRow.WrapContents = true;

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
        selectedModelComboBox.Margin = new Padding(0, 3, 8, 3);
        selectedModelComboBox.Name = "selectedModelComboBox";

        // chatTestButton — sends one minimal chat request as typed, without saving, so a
        // gateway or model rejection surfaces here rather than after running all ten checks.
        chatTestButton.Anchor = AnchorStyles.Left;
        chatTestButton.AutoSize = true;
        chatTestButton.Name = "chatTestButton";
        chatTestButton.Text = "Test";
        chatTestButton.UseVisualStyleBackColor = true;
        chatTestButton.Click += ChatTestButton_Click;

        // maxTokensLabel
        maxTokensLabel.AutoSize = true;
        maxTokensLabel.Margin = new Padding(0, 6, 6, 0);
        maxTokensLabel.Name = "maxTokensLabel";
        maxTokensLabel.Text = "Max output tokens";

        // maxTokensUpDown
        maxTokensUpDown.Increment = 256;
        maxTokensUpDown.Margin = new Padding(0, 3, 16, 3);
        maxTokensUpDown.Maximum = 64000;
        maxTokensUpDown.Minimum = 256;
        maxTokensUpDown.Name = "maxTokensUpDown";
        maxTokensUpDown.Width = 100;

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
        embeddingDimensionsLabel.AutoSize = true;
        embeddingDimensionsLabel.Margin = new Padding(0, 6, 6, 0);
        embeddingDimensionsLabel.Name = "embeddingDimensionsLabel";
        embeddingDimensionsLabel.Text = "Embedding dimensions";

        // embeddingDimensionsUpDown — defines the collection, so changing it means reloading docs
        embeddingDimensionsUpDown.Increment = 128;
        embeddingDimensionsUpDown.Margin = new Padding(0, 3, 0, 3);
        embeddingDimensionsUpDown.Maximum = 8192;
        embeddingDimensionsUpDown.Minimum = 64;
        embeddingDimensionsUpDown.Name = "embeddingDimensionsUpDown";
        embeddingDimensionsUpDown.Width = 100;

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
        vectorLayout.RowCount = 6;
        vectorLayout.Controls.Add(qdrantLabel, 0, 0);
        vectorLayout.Controls.Add(qdrantTextBox, 1, 0);
        vectorLayout.Controls.Add(qdrantTestButton, 2, 0);
        vectorLayout.Controls.Add(qdrantHintLabel, 1, 1);
        vectorLayout.Controls.Add(collectionLabel, 0, 2);
        vectorLayout.Controls.Add(collectionTextBox, 1, 2);
        vectorLayout.Controls.Add(caseReferenceLabel, 0, 3);
        vectorLayout.Controls.Add(caseReferenceTextBox, 1, 3);
        vectorLayout.Controls.Add(caseReferenceHintLabel, 1, 4);
        vectorLayout.Controls.Add(vectorNumericRow, 0, 5);
        vectorLayout.SetColumnSpan(vectorNumericRow, 3);
        vectorLayout.Name = "vectorLayout";

        // vectorNumericRow — tenant id, chunk size, overlap and result count are all short
        // numeric fields; grouping them side by side (wrapping as the form narrows) avoids a
        // full-width row each and cuts the group's height by four rows. Anchor, not Dock — see
        // modelsNumericRow.
        vectorNumericRow.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        vectorNumericRow.AutoSize = true;
        vectorNumericRow.Controls.Add(tenantLabel);
        vectorNumericRow.Controls.Add(tenantUpDown);
        vectorNumericRow.Controls.Add(chunkTokensLabel);
        vectorNumericRow.Controls.Add(chunkTokensUpDown);
        vectorNumericRow.Controls.Add(chunkOverlapLabel);
        vectorNumericRow.Controls.Add(chunkOverlapUpDown);
        vectorNumericRow.Controls.Add(searchResultsLabel);
        vectorNumericRow.Controls.Add(searchResultsUpDown);
        vectorNumericRow.Margin = new Padding(0, 3, 0, 0);
        vectorNumericRow.Name = "vectorNumericRow";
        vectorNumericRow.WrapContents = true;

        // canonicalGroup — where the schema, the query plans and the extracted models live
        canonicalGroup.AutoSize = true;
        canonicalGroup.Controls.Add(canonicalLayout);
        canonicalGroup.Dock = DockStyle.Fill;
        canonicalGroup.Margin = new Padding(0, 0, 0, 8);
        canonicalGroup.Name = "canonicalGroup";
        canonicalGroup.Padding = new Padding(8);
        canonicalGroup.TabStop = false;
        canonicalGroup.Text = "Canonical model";

        // canonicalLayout
        canonicalLayout.AutoSize = true;
        canonicalLayout.ColumnCount = 3;
        canonicalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        canonicalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        canonicalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        canonicalLayout.Dock = DockStyle.Fill;
        canonicalLayout.RowCount = 5;
        canonicalLayout.Controls.Add(canonicalSchemaLabel, 0, 0);
        canonicalLayout.Controls.Add(canonicalSchemaTextBox, 1, 0);
        canonicalLayout.Controls.Add(canonicalSchemaBrowseButton, 2, 0);
        canonicalLayout.Controls.Add(checkPlanLabel, 0, 1);
        canonicalLayout.Controls.Add(checkPlanTextBox, 1, 1);
        canonicalLayout.Controls.Add(checkPlanBrowseButton, 2, 1);
        canonicalLayout.Controls.Add(canonicalHintLabel, 1, 2);
        canonicalLayout.Controls.Add(canonicalDbLabel, 0, 3);
        canonicalLayout.Controls.Add(canonicalDbTextBox, 1, 3);
        canonicalLayout.Controls.Add(extractionTokensLabel, 0, 4);
        canonicalLayout.Controls.Add(extractionTokensUpDown, 1, 4);
        canonicalLayout.Name = "canonicalLayout";

        // canonicalSchemaLabel
        canonicalSchemaLabel.Anchor = AnchorStyles.Left;
        canonicalSchemaLabel.AutoSize = true;
        canonicalSchemaLabel.Name = "canonicalSchemaLabel";
        canonicalSchemaLabel.Text = "Model schema";

        // canonicalSchemaTextBox
        canonicalSchemaTextBox.Dock = DockStyle.Fill;
        canonicalSchemaTextBox.Margin = new Padding(0, 3, 8, 3);
        canonicalSchemaTextBox.Name = "canonicalSchemaTextBox";
        canonicalSchemaTextBox.PlaceholderText = AppSettings.DefaultCanonicalSchemaFileName + " (beside the app)";

        // canonicalSchemaBrowseButton
        canonicalSchemaBrowseButton.AutoSize = true;
        canonicalSchemaBrowseButton.Margin = new Padding(0, 3, 0, 3);
        canonicalSchemaBrowseButton.Name = "canonicalSchemaBrowseButton";
        canonicalSchemaBrowseButton.Padding = new Padding(8, 2, 8, 2);
        canonicalSchemaBrowseButton.Text = "Browse...";
        canonicalSchemaBrowseButton.UseVisualStyleBackColor = true;
        canonicalSchemaBrowseButton.Click += CanonicalSchemaBrowseButton_Click;

        // checkPlanLabel
        checkPlanLabel.Anchor = AnchorStyles.Left;
        checkPlanLabel.AutoSize = true;
        checkPlanLabel.Name = "checkPlanLabel";
        checkPlanLabel.Text = "Check plan folder";

        // checkPlanTextBox
        checkPlanTextBox.Dock = DockStyle.Fill;
        checkPlanTextBox.Margin = new Padding(0, 3, 8, 3);
        checkPlanTextBox.Name = "checkPlanTextBox";
        checkPlanTextBox.PlaceholderText = AppSettings.DefaultCheckPlanFolderName + " (beside the app)";

        // checkPlanBrowseButton
        checkPlanBrowseButton.AutoSize = true;
        checkPlanBrowseButton.Margin = new Padding(0, 3, 0, 3);
        checkPlanBrowseButton.Name = "checkPlanBrowseButton";
        checkPlanBrowseButton.Padding = new Padding(8, 2, 8, 2);
        checkPlanBrowseButton.Text = "Browse...";
        checkPlanBrowseButton.UseVisualStyleBackColor = true;
        checkPlanBrowseButton.Click += CheckPlanBrowseButton_Click;

        // canonicalHintLabel
        canonicalHintLabel.AutoSize = true;
        canonicalHintLabel.ForeColor = SystemColors.GrayText;
        canonicalHintLabel.Margin = new Padding(0, 0, 0, 8);
        canonicalHintLabel.Name = "canonicalHintLabel";
        canonicalHintLabel.Text =
            "Leave blank to use the copies deployed with the app. A check reads what the report asserts\r\n"
            + "from the extracted model and searches only the evidence documents.";

        // canonicalDbLabel
        canonicalDbLabel.Anchor = AnchorStyles.Left;
        canonicalDbLabel.AutoSize = true;
        canonicalDbLabel.Name = "canonicalDbLabel";
        canonicalDbLabel.Text = "Model database";

        // canonicalDbTextBox
        canonicalDbTextBox.Dock = DockStyle.Fill;
        canonicalDbTextBox.Margin = new Padding(0, 3, 8, 3);
        canonicalDbTextBox.Name = "canonicalDbTextBox";
        canonicalDbTextBox.PlaceholderText = "canonical-models.db (in the app's local data folder)";

        // extractionTokensLabel
        extractionTokensLabel.Anchor = AnchorStyles.Left;
        extractionTokensLabel.AutoSize = true;
        extractionTokensLabel.Name = "extractionTokensLabel";
        extractionTokensLabel.Text = "Extraction max tokens";

        // extractionTokensUpDown
        extractionTokensUpDown.Anchor = AnchorStyles.Left;
        extractionTokensUpDown.Increment = 1000;
        extractionTokensUpDown.Margin = new Padding(0, 3, 0, 3);
        extractionTokensUpDown.Maximum = 128000;
        extractionTokensUpDown.Minimum = 1000;
        extractionTokensUpDown.Name = "extractionTokensUpDown";
        extractionTokensUpDown.Width = 120;

        // reproducibilityGroup — what keeps two runs of the same check agreeing with each other
        reproducibilityGroup.AutoSize = true;
        reproducibilityGroup.Controls.Add(reproducibilityLayout);
        reproducibilityGroup.Dock = DockStyle.Fill;
        reproducibilityGroup.Margin = new Padding(0, 0, 0, 8);
        reproducibilityGroup.Name = "reproducibilityGroup";
        reproducibilityGroup.Padding = new Padding(8);
        reproducibilityGroup.TabStop = false;
        reproducibilityGroup.Text = "Assessment reproducibility";

        // reproducibilityLayout
        reproducibilityLayout.AutoSize = true;
        reproducibilityLayout.ColumnCount = 1;
        reproducibilityLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        reproducibilityLayout.Dock = DockStyle.Fill;
        reproducibilityLayout.RowCount = 3;
        reproducibilityLayout.Controls.Add(structuredFindingsCheckBox, 0, 0);
        reproducibilityLayout.Controls.Add(samplingRow, 0, 1);
        reproducibilityLayout.Controls.Add(reproducibilityHintLabel, 0, 2);
        reproducibilityLayout.Name = "reproducibilityLayout";

        // structuredFindingsCheckBox
        structuredFindingsCheckBox.AutoSize = true;
        structuredFindingsCheckBox.Margin = new Padding(0, 3, 0, 3);
        structuredFindingsCheckBox.Name = "structuredFindingsCheckBox";
        structuredFindingsCheckBox.Text = "Constrain findings to the response schema";
        structuredFindingsCheckBox.UseVisualStyleBackColor = true;

        // samplingRow — temperature, top-p and seed are each independently pinnable, since a
        // gateway or model can reject one of the three without objecting to the others. Anchor,
        // not Dock — see modelsNumericRow.
        samplingRow.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        samplingRow.AutoSize = true;
        samplingRow.Controls.Add(pinTemperatureCheckBox);
        samplingRow.Controls.Add(temperatureUpDown);
        samplingRow.Controls.Add(pinTopPCheckBox);
        samplingRow.Controls.Add(topPUpDown);
        samplingRow.Controls.Add(pinSeedCheckBox);
        samplingRow.Controls.Add(seedUpDown);
        samplingRow.Margin = new Padding(0, 3, 0, 3);
        samplingRow.Name = "samplingRow";
        samplingRow.WrapContents = true;

        // pinTemperatureCheckBox
        pinTemperatureCheckBox.AutoSize = true;
        pinTemperatureCheckBox.Margin = new Padding(0, 6, 4, 0);
        pinTemperatureCheckBox.Name = "pinTemperatureCheckBox";
        pinTemperatureCheckBox.Text = "Temperature";
        pinTemperatureCheckBox.UseVisualStyleBackColor = true;
        pinTemperatureCheckBox.CheckedChanged += PinTemperatureCheckBox_CheckedChanged;

        // temperatureUpDown
        temperatureUpDown.DecimalPlaces = 1;
        temperatureUpDown.Increment = 0.1m;
        temperatureUpDown.Margin = new Padding(0, 3, 16, 3);
        temperatureUpDown.Maximum = 2m;
        temperatureUpDown.Minimum = 0m;
        temperatureUpDown.Name = "temperatureUpDown";
        temperatureUpDown.Width = 70;

        // pinTopPCheckBox
        pinTopPCheckBox.AutoSize = true;
        pinTopPCheckBox.Margin = new Padding(0, 6, 4, 0);
        pinTopPCheckBox.Name = "pinTopPCheckBox";
        pinTopPCheckBox.Text = "Top-p";
        pinTopPCheckBox.UseVisualStyleBackColor = true;
        pinTopPCheckBox.CheckedChanged += PinTopPCheckBox_CheckedChanged;

        // topPUpDown
        topPUpDown.DecimalPlaces = 2;
        topPUpDown.Increment = 0.05m;
        topPUpDown.Margin = new Padding(0, 3, 16, 3);
        topPUpDown.Maximum = 1m;
        topPUpDown.Minimum = 0m;
        topPUpDown.Name = "topPUpDown";
        topPUpDown.Width = 70;

        // pinSeedCheckBox
        pinSeedCheckBox.AutoSize = true;
        pinSeedCheckBox.Margin = new Padding(0, 6, 4, 0);
        pinSeedCheckBox.Name = "pinSeedCheckBox";
        pinSeedCheckBox.Text = "Seed";
        pinSeedCheckBox.UseVisualStyleBackColor = true;
        pinSeedCheckBox.CheckedChanged += PinSeedCheckBox_CheckedChanged;

        // seedUpDown
        seedUpDown.Margin = new Padding(0, 3, 0, 3);
        seedUpDown.Maximum = int.MaxValue;
        seedUpDown.Minimum = 0;
        seedUpDown.Name = "seedUpDown";
        seedUpDown.Width = 100;

        // reproducibilityHintLabel
        reproducibilityHintLabel.AutoSize = true;
        reproducibilityHintLabel.ForeColor = SystemColors.GrayText;
        reproducibilityHintLabel.Margin = new Padding(0, 6, 0, 0);
        reproducibilityHintLabel.Name = "reproducibilityHintLabel";
        reproducibilityHintLabel.Text =
            "Two runs of the same check over the same case should reach the same finding. Uncheck a\r\n"
            + "parameter only for an endpoint that rejects it. Changing the seed samples a second opinion;\r\n"
            + "the findings report records what was pinned, so a difference between runs can be traced to\r\n"
            + "what actually changed.";

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
        qdrantHintLabel.MaximumSize = new Size(500, 0);
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
        tenantLabel.AutoSize = true;
        tenantLabel.Margin = new Padding(0, 6, 6, 0);
        tenantLabel.Name = "tenantLabel";
        tenantLabel.Text = "Tenant id";

        // tenantUpDown
        tenantUpDown.Margin = new Padding(0, 3, 16, 3);
        tenantUpDown.Maximum = 1000000;
        tenantUpDown.Minimum = 0;
        tenantUpDown.Name = "tenantUpDown";
        tenantUpDown.Width = 90;

        // chunkTokensLabel
        chunkTokensLabel.AutoSize = true;
        chunkTokensLabel.Margin = new Padding(0, 6, 6, 0);
        chunkTokensLabel.Name = "chunkTokensLabel";
        chunkTokensLabel.Text = "Max tokens/chunk";

        // chunkTokensUpDown
        chunkTokensUpDown.Increment = 50;
        chunkTokensUpDown.Margin = new Padding(0, 3, 16, 3);
        chunkTokensUpDown.Maximum = 4000;
        chunkTokensUpDown.Minimum = 64;
        chunkTokensUpDown.Name = "chunkTokensUpDown";
        chunkTokensUpDown.Width = 90;

        // chunkOverlapLabel
        chunkOverlapLabel.AutoSize = true;
        chunkOverlapLabel.Margin = new Padding(0, 6, 6, 0);
        chunkOverlapLabel.Name = "chunkOverlapLabel";
        chunkOverlapLabel.Text = "Chunk overlap";

        // chunkOverlapUpDown
        chunkOverlapUpDown.Increment = 25;
        chunkOverlapUpDown.Margin = new Padding(0, 3, 16, 3);
        chunkOverlapUpDown.Maximum = 2000;
        chunkOverlapUpDown.Minimum = 0;
        chunkOverlapUpDown.Name = "chunkOverlapUpDown";
        chunkOverlapUpDown.Width = 90;

        // searchResultsLabel
        searchResultsLabel.AutoSize = true;
        searchResultsLabel.Margin = new Padding(0, 6, 6, 0);
        searchResultsLabel.Name = "searchResultsLabel";
        searchResultsLabel.Text = "Results/search";

        // searchResultsUpDown
        searchResultsUpDown.Margin = new Padding(0, 3, 0, 3);
        searchResultsUpDown.Maximum = 50;
        searchResultsUpDown.Minimum = 1;
        searchResultsUpDown.Name = "searchResultsUpDown";
        searchResultsUpDown.Width = 90;

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

        // buttonPanel — docked to the form itself, not the scrolling area, so Save/Cancel are
        // always reachable regardless of scroll position.
        buttonPanel.AutoSize = true;
        buttonPanel.Dock = DockStyle.Bottom;
        buttonPanel.FlowDirection = FlowDirection.RightToLeft;
        buttonPanel.Padding = new Padding(10);
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
        Controls.Add(scrollPanel);
        Controls.Add(buttonPanel);
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
        ((System.ComponentModel.ISupportInitialize)extractionTokensUpDown).EndInit();
        canonicalLayout.ResumeLayout(false);
        canonicalLayout.PerformLayout();
        canonicalGroup.ResumeLayout(false);
        canonicalGroup.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)seedUpDown).EndInit();
        ((System.ComponentModel.ISupportInitialize)topPUpDown).EndInit();
        ((System.ComponentModel.ISupportInitialize)temperatureUpDown).EndInit();
        samplingRow.ResumeLayout(false);
        samplingRow.PerformLayout();
        reproducibilityLayout.ResumeLayout(false);
        reproducibilityLayout.PerformLayout();
        reproducibilityGroup.ResumeLayout(false);
        reproducibilityGroup.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)searchResultsUpDown).EndInit();
        ((System.ComponentModel.ISupportInitialize)chunkOverlapUpDown).EndInit();
        ((System.ComponentModel.ISupportInitialize)chunkTokensUpDown).EndInit();
        ((System.ComponentModel.ISupportInitialize)tenantUpDown).EndInit();
        vectorNumericRow.ResumeLayout(false);
        vectorNumericRow.PerformLayout();
        vectorLayout.ResumeLayout(false);
        vectorLayout.PerformLayout();
        vectorGroup.ResumeLayout(false);
        vectorGroup.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)embeddingDimensionsUpDown).EndInit();
        ((System.ComponentModel.ISupportInitialize)maxTokensUpDown).EndInit();
        modelsNumericRow.ResumeLayout(false);
        modelsNumericRow.PerformLayout();
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
        scrollPanel.ResumeLayout(false);
        scrollPanel.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
