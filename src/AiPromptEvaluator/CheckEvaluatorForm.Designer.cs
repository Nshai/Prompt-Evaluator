namespace AiPromptEvaluator;

partial class CheckEvaluatorForm
{
    private System.ComponentModel.IContainer components = null;

    // Top bar
    private TableLayoutPanel rootLayout;
    private TableLayoutPanel topBar;
    private Label csvLabel;
    private TextBox csvPathTextBox;
    private Button browseCsvButton;
    private Label caseFolderLabel;
    private TextBox caseFolderTextBox;
    private Button browseCaseFolderButton;
    private Button loadDocsButton;

    // Main split: left (checks list) | right (details + docs + response)
    private SplitContainer mainSplit;

    // Left panel
    private GroupBox checksGroup;
    private ListView checksListView;
    private ColumnHeader statusColumn;
    private ColumnHeader checkIdColumn;
    private ColumnHeader checkNameColumn;
    private ColumnHeader codesColumn;

    // Right split: top (details + docs) | bottom (response)
    private SplitContainer rightSplit;

    // Top-right split: left (check details) | right (documents)
    private SplitContainer detailDocSplit;

    private GroupBox checkDetailsGroup;
    private TextBox checkDetailsTextBox;

    private GroupBox documentsGroup;
    private CheckedListBox documentsCheckedListBox;

    // Bottom-right: response
    private GroupBox responseGroup;
    private FlowLayoutPanel responseToolbar;
    private Button copyResponseButton;
    private Button saveReportButton;
    private Button saveModelButton;
    private CheckBox bypassCacheCheckBox;
    private TextBox responseTextBox;

    // Cost breakdown
    private GroupBox costGroup;
    private ListView costListView;
    private ColumnHeader costComponentColumn;
    private ColumnHeader costTokensColumn;
    private ColumnHeader costRateColumn;
    private ColumnHeader costCostColumn;
    private Label totalCostLabel;
    private Label costNoteLabel;

    // Action bar
    private TableLayoutPanel actionPanel;
    private Button runButton;
    private Button runAllButton;
    private Button cancelRunButton;
    private Button unloadDocsButton;
    private Button extractModelButton;
    private Button deleteModelButton;
    private Label statusLabel;
    private Button openPromptEvaluatorButton;
    private Button openConfigButton;
    private Button saveSettingsButton;

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
        topBar = new TableLayoutPanel();
        csvLabel = new Label();
        csvPathTextBox = new TextBox();
        browseCsvButton = new Button();
        caseFolderLabel = new Label();
        caseFolderTextBox = new TextBox();
        browseCaseFolderButton = new Button();
        loadDocsButton = new Button();
        mainSplit = new SplitContainer();
        checksGroup = new GroupBox();
        checksListView = new ListView();
        statusColumn = new ColumnHeader();
        checkIdColumn = new ColumnHeader();
        checkNameColumn = new ColumnHeader();
        codesColumn = new ColumnHeader();
        rightSplit = new SplitContainer();
        detailDocSplit = new SplitContainer();
        checkDetailsGroup = new GroupBox();
        checkDetailsTextBox = new TextBox();
        documentsGroup = new GroupBox();
        documentsCheckedListBox = new CheckedListBox();
        responseGroup = new GroupBox();
        responseToolbar = new FlowLayoutPanel();
        copyResponseButton = new Button();
        saveReportButton = new Button();
        saveModelButton = new Button();
        bypassCacheCheckBox = new CheckBox();
        responseTextBox = new TextBox();
        actionPanel = new TableLayoutPanel();
        costGroup = new GroupBox();
        costListView = new ListView();
        costComponentColumn = new ColumnHeader();
        costTokensColumn = new ColumnHeader();
        costRateColumn = new ColumnHeader();
        costCostColumn = new ColumnHeader();
        totalCostLabel = new Label();
        costNoteLabel = new Label();
        runButton = new Button();
        runAllButton = new Button();
        cancelRunButton = new Button();
        unloadDocsButton = new Button();
        extractModelButton = new Button();
        deleteModelButton = new Button();
        statusLabel = new Label();
        openPromptEvaluatorButton = new Button();
        openConfigButton = new Button();
        saveSettingsButton = new Button();

        rootLayout.SuspendLayout();
        topBar.SuspendLayout();
        mainSplit.Panel1.SuspendLayout();
        mainSplit.Panel2.SuspendLayout();
        mainSplit.SuspendLayout();
        checksGroup.SuspendLayout();
        rightSplit.Panel1.SuspendLayout();
        rightSplit.Panel2.SuspendLayout();
        rightSplit.SuspendLayout();
        detailDocSplit.Panel1.SuspendLayout();
        detailDocSplit.Panel2.SuspendLayout();
        detailDocSplit.SuspendLayout();
        checkDetailsGroup.SuspendLayout();
        documentsGroup.SuspendLayout();
        responseGroup.SuspendLayout();
        costGroup.SuspendLayout();
        actionPanel.SuspendLayout();
        SuspendLayout();

        // rootLayout
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Padding = new Padding(10);
        rootLayout.RowCount = 4;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.Controls.Add(topBar, 0, 0);
        rootLayout.Controls.Add(mainSplit, 0, 1);
        rootLayout.Controls.Add(costGroup, 0, 2);
        rootLayout.Controls.Add(actionPanel, 0, 3);
        rootLayout.Name = "rootLayout";

        // topBar — 2 rows: CSV row, folder row
        topBar.AutoSize = true;
        topBar.ColumnCount = 4;
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topBar.Dock = DockStyle.Fill;
        topBar.Margin = new Padding(0, 0, 0, 8);
        topBar.RowCount = 2;
        topBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        topBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        topBar.Controls.Add(csvLabel, 0, 0);
        topBar.Controls.Add(csvPathTextBox, 1, 0);
        topBar.Controls.Add(browseCsvButton, 2, 0);
        topBar.Controls.Add(caseFolderLabel, 0, 1);
        topBar.Controls.Add(caseFolderTextBox, 1, 1);
        topBar.Controls.Add(browseCaseFolderButton, 2, 1);
        topBar.Controls.Add(loadDocsButton, 3, 1);
        topBar.Name = "topBar";

        // csvLabel
        csvLabel.Anchor = AnchorStyles.Left;
        csvLabel.AutoSize = true;
        csvLabel.Margin = new Padding(0, 0, 8, 4);
        csvLabel.Name = "csvLabel";
        csvLabel.Text = "Checks CSV";

        // csvPathTextBox
        csvPathTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        csvPathTextBox.Margin = new Padding(0, 0, 8, 4);
        csvPathTextBox.Name = "csvPathTextBox";
        csvPathTextBox.ReadOnly = true;

        // browseCsvButton
        browseCsvButton.AutoSize = true;
        browseCsvButton.Margin = new Padding(0, 0, 0, 4);
        browseCsvButton.Name = "browseCsvButton";
        browseCsvButton.Padding = new Padding(8, 2, 8, 2);
        browseCsvButton.Text = "Browse...";
        browseCsvButton.UseVisualStyleBackColor = true;
        browseCsvButton.Click += BrowseCsvButton_Click;

        // caseFolderLabel
        caseFolderLabel.Anchor = AnchorStyles.Left;
        caseFolderLabel.AutoSize = true;
        caseFolderLabel.Margin = new Padding(0, 0, 8, 0);
        caseFolderLabel.Name = "caseFolderLabel";
        caseFolderLabel.Text = "Case folder";

        // caseFolderTextBox
        caseFolderTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        caseFolderTextBox.Margin = new Padding(0, 0, 8, 0);
        caseFolderTextBox.Name = "caseFolderTextBox";

        // browseCaseFolderButton
        browseCaseFolderButton.AutoSize = true;
        browseCaseFolderButton.Margin = new Padding(0, 0, 8, 0);
        browseCaseFolderButton.Name = "browseCaseFolderButton";
        browseCaseFolderButton.Padding = new Padding(8, 2, 8, 2);
        browseCaseFolderButton.Text = "Browse...";
        browseCaseFolderButton.UseVisualStyleBackColor = true;
        browseCaseFolderButton.Click += BrowseCaseFolderButton_Click;

        // loadDocsButton — uploads every file under the case folder to the Files API
        loadDocsButton.AutoSize = true;
        loadDocsButton.Margin = new Padding(0);
        loadDocsButton.Name = "loadDocsButton";
        loadDocsButton.Padding = new Padding(8, 2, 8, 2);
        loadDocsButton.Text = "Load Docs";
        loadDocsButton.UseVisualStyleBackColor = true;
        loadDocsButton.Click += LoadDocsButton_Click;

        // mainSplit — left: checks list, right: details/docs/response
        mainSplit.Dock = DockStyle.Fill;
        mainSplit.Margin = new Padding(0, 0, 0, 6);
        mainSplit.Name = "mainSplit";
        mainSplit.Orientation = Orientation.Vertical;
        mainSplit.Panel1.Controls.Add(checksGroup);
        mainSplit.Panel2.Controls.Add(rightSplit);
        mainSplit.SplitterWidth = 5;

        // checksGroup
        checksGroup.Controls.Add(checksListView);
        checksGroup.Dock = DockStyle.Fill;
        checksGroup.Name = "checksGroup";
        checksGroup.Padding = new Padding(6);
        checksGroup.TabStop = false;
        checksGroup.Text = "Checks";

        // checksListView
        checksListView.Columns.AddRange(new[] { statusColumn, checkIdColumn, checkNameColumn, codesColumn });
        checksListView.Dock = DockStyle.Fill;
        checksListView.FullRowSelect = true;
        checksListView.GridLines = true;
        checksListView.HideSelection = false;
        checksListView.MultiSelect = false;
        // The status glyph is terse by design; the tooltip carries the detail behind it.
        checksListView.ShowItemToolTips = true;
        checksListView.Name = "checksListView";
        checksListView.UseCompatibleStateImageBehavior = false;
        checksListView.View = View.Details;
        checksListView.SelectedIndexChanged += ChecksListView_SelectedIndexChanged;

        // The run status glyph. Narrow and unlabelled: it is read by shape and colour at a
        // glance, and a header would take more room than the column it names.
        statusColumn.Text = string.Empty;
        statusColumn.Width = 30;
        checkIdColumn.Text = "ID";
        checkIdColumn.Width = 70;
        checkNameColumn.Text = "Check Name";
        checkNameColumn.Width = 200;
        codesColumn.Text = "Cats";
        codesColumn.Width = 55;

        // rightSplit — top: detail+docs, bottom: response
        rightSplit.Dock = DockStyle.Fill;
        rightSplit.Name = "rightSplit";
        rightSplit.Orientation = Orientation.Horizontal;
        rightSplit.Panel1.Controls.Add(detailDocSplit);
        rightSplit.Panel2.Controls.Add(responseGroup);
        rightSplit.SplitterWidth = 5;

        // detailDocSplit — left: check details, right: documents
        detailDocSplit.Dock = DockStyle.Fill;
        detailDocSplit.Name = "detailDocSplit";
        detailDocSplit.Orientation = Orientation.Vertical;
        detailDocSplit.Panel1.Controls.Add(checkDetailsGroup);
        detailDocSplit.Panel2.Controls.Add(documentsGroup);
        detailDocSplit.SplitterWidth = 5;

        // checkDetailsGroup
        checkDetailsGroup.Controls.Add(checkDetailsTextBox);
        checkDetailsGroup.Dock = DockStyle.Fill;
        checkDetailsGroup.Name = "checkDetailsGroup";
        checkDetailsGroup.Padding = new Padding(6);
        checkDetailsGroup.TabStop = false;
        checkDetailsGroup.Text = "Check Details";

        // checkDetailsTextBox
        checkDetailsTextBox.Dock = DockStyle.Fill;
        checkDetailsTextBox.Multiline = true;
        checkDetailsTextBox.Name = "checkDetailsTextBox";
        checkDetailsTextBox.ReadOnly = true;
        checkDetailsTextBox.ScrollBars = ScrollBars.Vertical;
        checkDetailsTextBox.Font = new Font("Consolas", 8.5F);

        // documentsGroup
        documentsGroup.Controls.Add(documentsCheckedListBox);
        documentsGroup.Dock = DockStyle.Fill;
        documentsGroup.Name = "documentsGroup";
        documentsGroup.Padding = new Padding(6);
        documentsGroup.TabStop = false;
        documentsGroup.Text = "Documents (check to include)";

        // documentsCheckedListBox
        documentsCheckedListBox.CheckOnClick = true;
        documentsCheckedListBox.Dock = DockStyle.Fill;
        documentsCheckedListBox.FormattingEnabled = true;
        documentsCheckedListBox.Name = "documentsCheckedListBox";

        // responseGroup
        responseGroup.Controls.Add(responseTextBox);
        responseGroup.Controls.Add(responseToolbar);
        responseGroup.Dock = DockStyle.Fill;
        responseGroup.Name = "responseGroup";
        responseGroup.Padding = new Padding(6);
        responseGroup.TabStop = false;
        responseGroup.Text = "Assessment Response";

        // responseToolbar
        responseToolbar.AutoSize = true;
        responseToolbar.Controls.Add(copyResponseButton);
        responseToolbar.Controls.Add(saveReportButton);
        responseToolbar.Controls.Add(saveModelButton);
        responseToolbar.Dock = DockStyle.Top;
        responseToolbar.FlowDirection = FlowDirection.RightToLeft;
        responseToolbar.Name = "responseToolbar";

        // copyResponseButton
        copyResponseButton.AutoSize = true;
        copyResponseButton.Margin = new Padding(0, 0, 0, 4);
        copyResponseButton.Name = "copyResponseButton";
        copyResponseButton.Padding = new Padding(8, 2, 8, 2);
        copyResponseButton.Text = "Copy response + cost";
        copyResponseButton.UseVisualStyleBackColor = true;
        copyResponseButton.Click += CopyResponseButton_Click;

        // saveReportButton
        saveReportButton.AutoSize = true;
        saveReportButton.Margin = new Padding(0, 0, 8, 4);
        saveReportButton.Name = "saveReportButton";
        saveReportButton.Padding = new Padding(8, 2, 8, 2);
        saveReportButton.Text = "Compliance report...";
        saveReportButton.UseVisualStyleBackColor = true;
        saveReportButton.Click += SaveReportButton_Click;

        // saveModelButton
        saveModelButton.AutoSize = true;
        saveModelButton.Enabled = false;
        saveModelButton.Margin = new Padding(0, 0, 8, 4);
        saveModelButton.Name = "saveModelButton";
        saveModelButton.Padding = new Padding(8, 2, 8, 2);
        saveModelButton.Text = "Save model JSON...";
        saveModelButton.UseVisualStyleBackColor = true;
        saveModelButton.Click += SaveModelButton_Click;

        // responseTextBox
        responseTextBox.Dock = DockStyle.Fill;
        responseTextBox.Multiline = true;
        responseTextBox.Name = "responseTextBox";
        responseTextBox.ReadOnly = true;
        responseTextBox.ScrollBars = ScrollBars.Vertical;

        // costGroup
        costGroup.Controls.Add(costListView);
        costGroup.Controls.Add(costNoteLabel);
        costGroup.Controls.Add(totalCostLabel);
        costGroup.Dock = DockStyle.Fill;
        costGroup.Height = 170;
        costGroup.Margin = new Padding(0, 6, 0, 0);
        costGroup.Name = "costGroup";
        costGroup.Padding = new Padding(6);
        costGroup.TabStop = false;
        costGroup.Text = "Cost breakdown";

        // costListView
        costListView.Columns.AddRange(new[] { costComponentColumn, costTokensColumn, costRateColumn, costCostColumn });
        costListView.Dock = DockStyle.Fill;
        costListView.FullRowSelect = true;
        costListView.GridLines = true;
        costListView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        costListView.MultiSelect = false;
        costListView.Name = "costListView";
        costListView.UseCompatibleStateImageBehavior = false;
        costListView.View = View.Details;

        costComponentColumn.Text = "Component";
        costComponentColumn.Width = 130;
        costTokensColumn.Text = "Tokens";
        costTokensColumn.TextAlign = HorizontalAlignment.Right;
        costTokensColumn.Width = 75;
        costRateColumn.Text = "$/MTok";
        costRateColumn.TextAlign = HorizontalAlignment.Right;
        costRateColumn.Width = 75;
        costCostColumn.Text = "Cost";
        costCostColumn.TextAlign = HorizontalAlignment.Right;
        costCostColumn.Width = 90;

        // costNoteLabel
        costNoteLabel.AutoSize = false;
        costNoteLabel.Dock = DockStyle.Bottom;
        costNoteLabel.ForeColor = SystemColors.GrayText;
        costNoteLabel.Height = 32;
        costNoteLabel.Name = "costNoteLabel";
        costNoteLabel.Text = "Run a check to see the cost breakdown.";

        // totalCostLabel
        totalCostLabel.AutoSize = false;
        totalCostLabel.Dock = DockStyle.Bottom;
        totalCostLabel.Font = new Font(Font, FontStyle.Bold);
        totalCostLabel.Height = 24;
        totalCostLabel.Name = "totalCostLabel";
        totalCostLabel.Text = "Total: $0.0000 for 0 tokens";
        totalCostLabel.TextAlign = ContentAlignment.MiddleLeft;

        // actionPanel — left: Run + Run All + Cancel + model/index buttons + status;
        // right: Prompt Evaluator + Configuration + Save
        actionPanel.AutoSize = true;
        actionPanel.ColumnCount = 12;
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 0 Run
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 1 Run All
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 2 Cancel
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 3 Extract Model
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 4 Delete Model
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 5 Unload Docs
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 6 Bypass cache
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // 7 status (stretches)
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 7 Prompt Evaluator
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 8 Configuration
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 9 Save Settings
        actionPanel.Dock = DockStyle.Fill;
        actionPanel.Margin = new Padding(0);
        actionPanel.RowCount = 1;
        actionPanel.Controls.Add(runButton, 0, 0);
        actionPanel.Controls.Add(runAllButton, 1, 0);
        actionPanel.Controls.Add(cancelRunButton, 2, 0);
        actionPanel.Controls.Add(extractModelButton, 3, 0);
        actionPanel.Controls.Add(deleteModelButton, 4, 0);
        actionPanel.Controls.Add(unloadDocsButton, 5, 0);
        actionPanel.Controls.Add(bypassCacheCheckBox, 6, 0);
        actionPanel.Controls.Add(statusLabel, 7, 0);
        actionPanel.Controls.Add(openPromptEvaluatorButton, 8, 0);
        actionPanel.Controls.Add(openConfigButton, 9, 0);
        actionPanel.Controls.Add(saveSettingsButton, 10, 0);

        // bypassCacheCheckBox — applies to Extract Model and to Run, because both are answered
        // by the same gateway and either can be served from cache.
        bypassCacheCheckBox.Anchor = AnchorStyles.Left;
        bypassCacheCheckBox.AutoSize = true;
        bypassCacheCheckBox.Margin = new Padding(0, 0, 16, 0);
        bypassCacheCheckBox.Name = "bypassCacheCheckBox";
        bypassCacheCheckBox.Text = "Bypass cache";
        bypassCacheCheckBox.UseVisualStyleBackColor = true;
        bypassCacheCheckBox.CheckedChanged += BypassCacheCheckBox_CheckedChanged;
        actionPanel.Name = "actionPanel";

        // runButton
        runButton.AutoSize = true;
        runButton.Margin = new Padding(0, 0, 8, 0);
        runButton.Name = "runButton";
        runButton.Padding = new Padding(12, 4, 12, 4);
        runButton.Text = "Run Check";
        runButton.UseVisualStyleBackColor = true;
        runButton.Click += RunButton_Click;

        // runAllButton — every check that has a query plan, in one pass
        runAllButton.AutoSize = true;
        runAllButton.Margin = new Padding(0, 0, 8, 0);
        runAllButton.Name = "runAllButton";
        runAllButton.Padding = new Padding(12, 4, 12, 4);
        runAllButton.Text = "Run All Checks";
        runAllButton.UseVisualStyleBackColor = true;
        runAllButton.Click += RunAllButton_Click;

        // extractModelButton — parses the category I report into the canonical model store
        extractModelButton.AutoSize = true;
        extractModelButton.Margin = new Padding(0, 0, 8, 0);
        extractModelButton.Name = "extractModelButton";
        extractModelButton.Padding = new Padding(12, 4, 12, 4);
        extractModelButton.Text = "Extract Model";
        extractModelButton.UseVisualStyleBackColor = true;
        extractModelButton.Click += ExtractModelButton_Click;

        // deleteModelButton — drops the stored canonical model for this case
        deleteModelButton.AutoSize = true;
        deleteModelButton.Enabled = false;
        deleteModelButton.Margin = new Padding(0, 0, 12, 0);
        deleteModelButton.Name = "deleteModelButton";
        deleteModelButton.Padding = new Padding(12, 4, 12, 4);
        deleteModelButton.Text = "Delete Model";
        deleteModelButton.UseVisualStyleBackColor = true;
        deleteModelButton.Click += DeleteModelButton_Click;

        // cancelRunButton
        cancelRunButton.AutoSize = true;
        cancelRunButton.Enabled = false;
        cancelRunButton.Margin = new Padding(0, 0, 12, 0);
        cancelRunButton.Name = "cancelRunButton";
        cancelRunButton.Padding = new Padding(12, 4, 12, 4);
        cancelRunButton.Text = "Cancel";
        cancelRunButton.UseVisualStyleBackColor = true;
        cancelRunButton.Click += CancelButton_Click;

        // unloadDocsButton
        unloadDocsButton.AutoSize = true;
        unloadDocsButton.Enabled = false;
        unloadDocsButton.Margin = new Padding(0, 0, 12, 0);
        unloadDocsButton.Name = "unloadDocsButton";
        unloadDocsButton.Padding = new Padding(12, 4, 12, 4);
        unloadDocsButton.Text = "Unload Docs";
        unloadDocsButton.UseVisualStyleBackColor = true;
        unloadDocsButton.Click += UnloadDocsButton_Click;

        // statusLabel
        statusLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        statusLabel.AutoSize = false;
        statusLabel.ForeColor = SystemColors.GrayText;
        statusLabel.Name = "statusLabel";
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        // openPromptEvaluatorButton
        openPromptEvaluatorButton.AutoSize = true;
        openPromptEvaluatorButton.Margin = new Padding(8, 0, 8, 0);
        openPromptEvaluatorButton.Name = "openPromptEvaluatorButton";
        openPromptEvaluatorButton.Padding = new Padding(8, 2, 8, 2);
        openPromptEvaluatorButton.Text = "Prompt Evaluator...";
        openPromptEvaluatorButton.UseVisualStyleBackColor = true;
        openPromptEvaluatorButton.Click += OpenPromptEvaluatorButton_Click;

        // openConfigButton
        openConfigButton.AutoSize = true;
        openConfigButton.Margin = new Padding(0, 0, 8, 0);
        openConfigButton.Name = "openConfigButton";
        openConfigButton.Padding = new Padding(8, 2, 8, 2);
        openConfigButton.Text = "Configuration...";
        openConfigButton.UseVisualStyleBackColor = true;
        openConfigButton.Click += OpenConfigButton_Click;

        // saveSettingsButton
        saveSettingsButton.AutoSize = true;
        saveSettingsButton.Margin = new Padding(0);
        saveSettingsButton.Name = "saveSettingsButton";
        saveSettingsButton.Padding = new Padding(8, 2, 8, 2);
        saveSettingsButton.Text = "Save Settings";
        saveSettingsButton.UseVisualStyleBackColor = true;
        saveSettingsButton.Click += SaveSettingsButton_Click;

        // CheckEvaluatorForm
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1200, 780);
        Controls.Add(rootLayout);
        MinimumSize = new Size(900, 600);
        Name = "CheckEvaluatorForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Check Evaluator";

        actionPanel.ResumeLayout(false);
        actionPanel.PerformLayout();
        costGroup.ResumeLayout(false);
        costGroup.PerformLayout();
        responseGroup.ResumeLayout(false);
        documentsGroup.ResumeLayout(false);
        checkDetailsGroup.ResumeLayout(false);
        detailDocSplit.Panel1.ResumeLayout(false);
        detailDocSplit.Panel2.ResumeLayout(false);
        detailDocSplit.ResumeLayout(false);
        rightSplit.Panel1.ResumeLayout(false);
        rightSplit.Panel2.ResumeLayout(false);
        rightSplit.ResumeLayout(false);
        checksGroup.ResumeLayout(false);
        mainSplit.Panel1.ResumeLayout(false);
        mainSplit.Panel2.ResumeLayout(false);
        mainSplit.ResumeLayout(false);
        topBar.ResumeLayout(false);
        topBar.PerformLayout();
        rootLayout.ResumeLayout(false);
        rootLayout.PerformLayout();
        ResumeLayout(false);
    }
}
