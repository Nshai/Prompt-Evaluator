namespace AiPromptEvaluator;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private TableLayoutPanel rootLayout;
    private TableLayoutPanel topPanel;
    private Label modelLabel;
    private ComboBox modelComboBox;
    private Button openConfigButton;
    private Button openCheckEvaluatorButton;
    private Button saveSettingsButton;
    private GroupBox contextGroup;
    private TableLayoutPanel contextLayout;
    private TextBox documentFolderTextBox;
    private Button browseFolderButton;
    private Button categoriseButton;
    private SplitContainer mainSplit;
    private SplitContainer promptResponseSplit;
    private GroupBox promptGroup;
    private TextBox promptTextBox;
    private CheckBox clarificationCheckBox;
    private GroupBox responseGroup;
    private TextBox responseTextBox;
    private GroupBox costGroup;
    private ListView costListView;
    private ColumnHeader componentColumn;
    private ColumnHeader tokensColumn;
    private ColumnHeader rateColumn;
    private ColumnHeader costColumn;
    private Label totalCostLabel;
    private Label costNoteLabel;
    private TableLayoutPanel actionPanel;
    private Button runButton;
    private Button clearButton;
    private Label statusLabel;

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
        topPanel = new TableLayoutPanel();
        modelLabel = new Label();
        modelComboBox = new ComboBox();
        openConfigButton = new Button();
        openCheckEvaluatorButton = new Button();
        saveSettingsButton = new Button();
        contextGroup = new GroupBox();
        contextLayout = new TableLayoutPanel();
        documentFolderTextBox = new TextBox();
        browseFolderButton = new Button();
        categoriseButton = new Button();
        mainSplit = new SplitContainer();
        promptResponseSplit = new SplitContainer();
        promptGroup = new GroupBox();
        promptTextBox = new TextBox();
        clarificationCheckBox = new CheckBox();
        responseGroup = new GroupBox();
        responseTextBox = new TextBox();
        costGroup = new GroupBox();
        costListView = new ListView();
        componentColumn = new ColumnHeader();
        tokensColumn = new ColumnHeader();
        rateColumn = new ColumnHeader();
        costColumn = new ColumnHeader();
        totalCostLabel = new Label();
        costNoteLabel = new Label();
        actionPanel = new TableLayoutPanel();
        runButton = new Button();
        clearButton = new Button();
        statusLabel = new Label();

        rootLayout.SuspendLayout();
        topPanel.SuspendLayout();
        contextGroup.SuspendLayout();
        contextLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)mainSplit).BeginInit();
        mainSplit.Panel1.SuspendLayout();
        mainSplit.Panel2.SuspendLayout();
        mainSplit.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)promptResponseSplit).BeginInit();
        promptResponseSplit.Panel1.SuspendLayout();
        promptResponseSplit.Panel2.SuspendLayout();
        promptResponseSplit.SuspendLayout();
        promptGroup.SuspendLayout();
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
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.Controls.Add(topPanel, 0, 0);
        rootLayout.Controls.Add(contextGroup, 0, 1);
        rootLayout.Controls.Add(mainSplit, 0, 2);
        rootLayout.Controls.Add(actionPanel, 0, 3);
        rootLayout.Name = "rootLayout";

        // topPanel
        topPanel.AutoSize = true;
        topPanel.ColumnCount = 6;
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.Dock = DockStyle.Fill;
        topPanel.Margin = new Padding(0, 0, 0, 6);
        topPanel.RowCount = 1;
        topPanel.Controls.Add(modelLabel, 0, 0);
        topPanel.Controls.Add(modelComboBox, 1, 0);
        topPanel.Controls.Add(openCheckEvaluatorButton, 3, 0);
        topPanel.Controls.Add(openConfigButton, 4, 0);
        topPanel.Controls.Add(saveSettingsButton, 5, 0);
        topPanel.Name = "topPanel";

        // modelLabel
        modelLabel.AutoSize = true;
        modelLabel.Anchor = AnchorStyles.Left;
        modelLabel.Margin = new Padding(0, 0, 8, 0);
        modelLabel.Name = "modelLabel";
        modelLabel.Text = "Model";

        // modelComboBox
        modelComboBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        modelComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        modelComboBox.Name = "modelComboBox";
        modelComboBox.SelectedIndexChanged += ModelComboBox_Changed;
        modelComboBox.TextChanged += ModelComboBox_Changed;

        // openCheckEvaluatorButton
        openCheckEvaluatorButton.AutoSize = true;
        openCheckEvaluatorButton.Margin = new Padding(8, 0, 8, 0);
        openCheckEvaluatorButton.Name = "openCheckEvaluatorButton";
        openCheckEvaluatorButton.Padding = new Padding(8, 2, 8, 2);
        openCheckEvaluatorButton.Text = "Check Evaluator...";
        openCheckEvaluatorButton.UseVisualStyleBackColor = true;
        openCheckEvaluatorButton.Click += OpenCheckEvaluatorButton_Click;

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

        // contextGroup
        contextGroup.AutoSize = true;
        contextGroup.Controls.Add(contextLayout);
        contextGroup.Dock = DockStyle.Fill;
        contextGroup.Margin = new Padding(0, 0, 0, 6);
        contextGroup.Name = "contextGroup";
        contextGroup.Padding = new Padding(8);
        contextGroup.TabStop = false;
        contextGroup.Text = "Document context folder (optional)";

        // contextLayout
        contextLayout.AutoSize = true;
        contextLayout.ColumnCount = 3;
        contextLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        contextLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        contextLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        contextLayout.Dock = DockStyle.Fill;
        contextLayout.RowCount = 1;
        contextLayout.Controls.Add(documentFolderTextBox, 0, 0);
        contextLayout.Controls.Add(browseFolderButton, 1, 0);
        contextLayout.Controls.Add(categoriseButton, 2, 0);
        contextLayout.Name = "contextLayout";

        // documentFolderTextBox
        documentFolderTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        documentFolderTextBox.Margin = new Padding(0, 0, 8, 0);
        documentFolderTextBox.Name = "documentFolderTextBox";
        documentFolderTextBox.TextChanged += DocumentFolderTextBox_TextChanged;

        // browseFolderButton
        browseFolderButton.AutoSize = true;
        browseFolderButton.Margin = new Padding(0, 0, 6, 0);
        browseFolderButton.Name = "browseFolderButton";
        browseFolderButton.Padding = new Padding(8, 2, 8, 2);
        browseFolderButton.Text = "Browse...";
        browseFolderButton.UseVisualStyleBackColor = true;
        browseFolderButton.Click += BrowseFolderButton_Click;

        // categoriseButton
        categoriseButton.AutoSize = true;
        categoriseButton.Margin = new Padding(0);
        categoriseButton.Name = "categoriseButton";
        categoriseButton.Padding = new Padding(8, 2, 8, 2);
        categoriseButton.Text = "Categorise...";
        categoriseButton.UseVisualStyleBackColor = true;
        categoriseButton.Click += CategoriseButton_Click;

        // mainSplit
        mainSplit.Dock = DockStyle.Fill;
        mainSplit.Margin = new Padding(0, 0, 0, 6);
        mainSplit.Name = "mainSplit";
        mainSplit.Orientation = Orientation.Vertical;
        mainSplit.Panel1.Controls.Add(promptResponseSplit);
        mainSplit.Panel2.Controls.Add(costGroup);
        mainSplit.Panel2MinSize = 320;
        mainSplit.Size = new Size(1060, 460);
        mainSplit.SplitterDistance = 660;
        mainSplit.SplitterWidth = 6;

        // promptResponseSplit
        promptResponseSplit.Dock = DockStyle.Fill;
        promptResponseSplit.Name = "promptResponseSplit";
        promptResponseSplit.Orientation = Orientation.Horizontal;
        promptResponseSplit.Panel1.Controls.Add(promptGroup);
        promptResponseSplit.Panel2.Controls.Add(responseGroup);
        promptResponseSplit.Size = new Size(660, 460);
        promptResponseSplit.SplitterDistance = 190;
        promptResponseSplit.SplitterWidth = 6;

        // promptGroup
        promptGroup.Controls.Add(promptTextBox);
        promptGroup.Controls.Add(clarificationCheckBox);
        promptGroup.Dock = DockStyle.Fill;
        promptGroup.Name = "promptGroup";
        promptGroup.Padding = new Padding(8);
        promptGroup.TabStop = false;
        promptGroup.Text = "Prompt";

        // promptTextBox
        promptTextBox.AcceptsReturn = true;
        promptTextBox.Dock = DockStyle.Fill;
        promptTextBox.Multiline = true;
        promptTextBox.Name = "promptTextBox";
        promptTextBox.ScrollBars = ScrollBars.Vertical;

        // clarificationCheckBox
        clarificationCheckBox.AutoSize = true;
        clarificationCheckBox.Dock = DockStyle.Bottom;
        clarificationCheckBox.Margin = new Padding(0, 4, 0, 0);
        clarificationCheckBox.Name = "clarificationCheckBox";
        clarificationCheckBox.Text = "Ask for clarification when the prompt is ambiguous";
        clarificationCheckBox.UseVisualStyleBackColor = true;
        clarificationCheckBox.CheckedChanged += ClarificationCheckBox_CheckedChanged;

        // responseGroup
        responseGroup.Controls.Add(responseTextBox);
        responseGroup.Dock = DockStyle.Fill;
        responseGroup.Name = "responseGroup";
        responseGroup.Padding = new Padding(8);
        responseGroup.TabStop = false;
        responseGroup.Text = "Response";

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
        costGroup.Name = "costGroup";
        costGroup.Padding = new Padding(8);
        costGroup.TabStop = false;
        costGroup.Text = "Cost breakdown";

        // costListView
        costListView.Columns.AddRange(new[] { componentColumn, tokensColumn, rateColumn, costColumn });
        costListView.Dock = DockStyle.Fill;
        costListView.FullRowSelect = true;
        costListView.GridLines = true;
        costListView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        costListView.MultiSelect = false;
        costListView.Name = "costListView";
        costListView.UseCompatibleStateImageBehavior = false;
        costListView.View = View.Details;

        componentColumn.Text = "Component";
        componentColumn.Width = 130;
        tokensColumn.Text = "Tokens";
        tokensColumn.TextAlign = HorizontalAlignment.Right;
        tokensColumn.Width = 75;
        rateColumn.Text = "$/MTok";
        rateColumn.TextAlign = HorizontalAlignment.Right;
        rateColumn.Width = 75;
        costColumn.Text = "Cost";
        costColumn.TextAlign = HorizontalAlignment.Right;
        costColumn.Width = 90;

        // costNoteLabel
        costNoteLabel.AutoSize = false;
        costNoteLabel.Dock = DockStyle.Bottom;
        costNoteLabel.ForeColor = SystemColors.GrayText;
        costNoteLabel.Height = 32;
        costNoteLabel.Name = "costNoteLabel";
        costNoteLabel.Text = "Run a prompt to see the cost breakdown.";

        // totalCostLabel
        totalCostLabel.AutoSize = false;
        totalCostLabel.Dock = DockStyle.Bottom;
        totalCostLabel.Font = new Font(Font, FontStyle.Bold);
        totalCostLabel.Height = 24;
        totalCostLabel.Name = "totalCostLabel";
        totalCostLabel.Text = "Total: $0.0000 for 0 tokens";
        totalCostLabel.TextAlign = ContentAlignment.MiddleLeft;

        // actionPanel
        actionPanel.AutoSize = true;
        actionPanel.ColumnCount = 3;
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        actionPanel.Dock = DockStyle.Fill;
        actionPanel.Margin = new Padding(0);
        actionPanel.RowCount = 1;
        actionPanel.Controls.Add(runButton, 0, 0);
        actionPanel.Controls.Add(clearButton, 1, 0);
        actionPanel.Controls.Add(statusLabel, 2, 0);
        actionPanel.Name = "actionPanel";

        // runButton
        runButton.AutoSize = true;
        runButton.Margin = new Padding(0, 0, 8, 0);
        runButton.Name = "runButton";
        runButton.Padding = new Padding(12, 4, 12, 4);
        runButton.Text = "Run Prompt";
        runButton.UseVisualStyleBackColor = true;
        runButton.Click += RunButton_Click;

        // clearButton
        clearButton.AutoSize = true;
        clearButton.Margin = new Padding(0, 0, 12, 0);
        clearButton.Name = "clearButton";
        clearButton.Padding = new Padding(12, 4, 12, 4);
        clearButton.Text = "Clear";
        clearButton.UseVisualStyleBackColor = true;
        clearButton.Click += ClearButton_Click;

        // statusLabel
        statusLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        statusLabel.AutoSize = false;
        statusLabel.ForeColor = SystemColors.GrayText;
        statusLabel.Name = "statusLabel";
        statusLabel.Text = string.Empty;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        // MainForm
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1100, 700);
        Controls.Add(rootLayout);
        MinimumSize = new Size(900, 560);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "AI Prompt Evaluator";

        costGroup.ResumeLayout(false);
        responseGroup.ResumeLayout(false);
        promptGroup.ResumeLayout(false);
        promptGroup.PerformLayout();
        promptResponseSplit.Panel1.ResumeLayout(false);
        promptResponseSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)promptResponseSplit).EndInit();
        promptResponseSplit.ResumeLayout(false);
        mainSplit.Panel1.ResumeLayout(false);
        mainSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)mainSplit).EndInit();
        mainSplit.ResumeLayout(false);
        contextLayout.ResumeLayout(false);
        contextLayout.PerformLayout();
        contextGroup.ResumeLayout(false);
        contextGroup.PerformLayout();
        topPanel.ResumeLayout(false);
        topPanel.PerformLayout();
        actionPanel.ResumeLayout(false);
        actionPanel.PerformLayout();
        rootLayout.ResumeLayout(false);
        rootLayout.PerformLayout();
        ResumeLayout(false);
    }
}
