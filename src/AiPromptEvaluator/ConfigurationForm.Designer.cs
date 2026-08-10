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
    private GroupBox modelsGroup;
    private TableLayoutPanel modelsLayout;
    private Label availableModelsLabel;
    private TextBox availableModelsTextBox;
    private Label selectedModelLabel;
    private ComboBox selectedModelComboBox;
    private Label maxTokensLabel;
    private NumericUpDown maxTokensUpDown;
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
        modelsGroup = new GroupBox();
        modelsLayout = new TableLayoutPanel();
        availableModelsLabel = new Label();
        availableModelsTextBox = new TextBox();
        selectedModelLabel = new Label();
        selectedModelComboBox = new ComboBox();
        maxTokensLabel = new Label();
        maxTokensUpDown = new NumericUpDown();
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
        contextGroup.SuspendLayout();
        contextLayout.SuspendLayout();
        buttonPanel.SuspendLayout();
        SuspendLayout();

        // rootLayout
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Padding = new Padding(10);
        rootLayout.RowCount = 5;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.Controls.Add(connectionGroup, 0, 0);
        rootLayout.Controls.Add(modelsGroup, 0, 1);
        rootLayout.Controls.Add(contextGroup, 0, 2);
        rootLayout.Controls.Add(clarificationCheckBox, 0, 3);
        rootLayout.Controls.Add(buttonPanel, 0, 4);
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
        connectionLayout.RowCount = 3;
        connectionLayout.Controls.Add(apiKeyLabel, 0, 0);
        connectionLayout.Controls.Add(apiKeyTextBox, 1, 0);
        connectionLayout.Controls.Add(showKeyCheckBox, 2, 0);
        connectionLayout.Controls.Add(baseUrlLabel, 0, 1);
        connectionLayout.Controls.Add(baseUrlTextBox, 1, 1);
        connectionLayout.Controls.Add(baseUrlHintLabel, 1, 2);
        connectionLayout.Name = "connectionLayout";

        // apiKeyLabel
        apiKeyLabel.Anchor = AnchorStyles.Left;
        apiKeyLabel.AutoSize = true;
        apiKeyLabel.Name = "apiKeyLabel";
        apiKeyLabel.Text = "Anthropic API key";

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
        baseUrlHintLabel.Text = "Leave empty for the default. Enter a base URL (e.g. https://litellm.example.com) or a full format string where {0}=version, {1}=endpoint.";

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
        modelsLayout.RowCount = 3;
        modelsLayout.Controls.Add(availableModelsLabel, 0, 0);
        modelsLayout.Controls.Add(availableModelsTextBox, 1, 0);
        modelsLayout.Controls.Add(selectedModelLabel, 0, 1);
        modelsLayout.Controls.Add(selectedModelComboBox, 1, 1);
        modelsLayout.Controls.Add(maxTokensLabel, 0, 2);
        modelsLayout.Controls.Add(maxTokensUpDown, 1, 2);
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
