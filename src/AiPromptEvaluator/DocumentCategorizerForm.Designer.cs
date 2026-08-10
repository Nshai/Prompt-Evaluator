namespace AiPromptEvaluator;

partial class DocumentCategorizerForm
{
    private System.ComponentModel.IContainer components = null;

    private TableLayoutPanel rootLayout;
    private Label folderPathLabel;
    private DataGridView grid;
    private DataGridViewTextBoxColumn fileColumn;
    private DataGridViewComboBoxColumn categoryColumn;
    private TableLayoutPanel setAllPanel;
    private Label setAllLabel;
    private ComboBox setAllComboBox;
    private Button setAllButton;
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
        folderPathLabel = new Label();
        grid = new DataGridView();
        fileColumn = new DataGridViewTextBoxColumn();
        categoryColumn = new DataGridViewComboBoxColumn();
        setAllPanel = new TableLayoutPanel();
        setAllLabel = new Label();
        setAllComboBox = new ComboBox();
        setAllButton = new Button();
        buttonPanel = new FlowLayoutPanel();
        saveButton = new Button();
        cancelButton = new Button();

        rootLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)grid).BeginInit();
        setAllPanel.SuspendLayout();
        buttonPanel.SuspendLayout();
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
        rootLayout.Controls.Add(folderPathLabel, 0, 0);
        rootLayout.Controls.Add(grid, 0, 1);
        rootLayout.Controls.Add(setAllPanel, 0, 2);
        rootLayout.Controls.Add(buttonPanel, 0, 3);
        rootLayout.Name = "rootLayout";

        // folderPathLabel
        folderPathLabel.AutoSize = true;
        folderPathLabel.Dock = DockStyle.Fill;
        folderPathLabel.ForeColor = SystemColors.GrayText;
        folderPathLabel.Margin = new Padding(0, 0, 0, 6);
        folderPathLabel.Name = "folderPathLabel";

        // grid
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.Columns.AddRange(fileColumn, categoryColumn);
        grid.Dock = DockStyle.Fill;
        grid.Margin = new Padding(0, 0, 0, 6);
        grid.Name = "grid";
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.CellValueChanged += Grid_CellValueChanged;
        grid.CurrentCellDirtyStateChanged += Grid_CurrentCellDirtyStateChanged;

        // fileColumn
        fileColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        fileColumn.HeaderText = "File";
        fileColumn.Name = "fileColumn";
        fileColumn.ReadOnly = true;
        fileColumn.SortMode = DataGridViewColumnSortMode.Automatic;

        // categoryColumn
        categoryColumn.FillWeight = 40F;
        categoryColumn.HeaderText = "Category";
        categoryColumn.Name = "categoryColumn";
        categoryColumn.SortMode = DataGridViewColumnSortMode.Automatic;
        categoryColumn.Width = 220;
        foreach (var cat in DocumentCategory.All)
        {
            categoryColumn.Items.Add(cat);
        }

        // setAllPanel
        setAllPanel.AutoSize = true;
        setAllPanel.ColumnCount = 3;
        setAllPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        setAllPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240F));
        setAllPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        setAllPanel.Dock = DockStyle.Fill;
        setAllPanel.Margin = new Padding(0, 0, 0, 6);
        setAllPanel.RowCount = 1;
        setAllPanel.Controls.Add(setAllLabel, 0, 0);
        setAllPanel.Controls.Add(setAllComboBox, 1, 0);
        setAllPanel.Controls.Add(setAllButton, 2, 0);
        setAllPanel.Name = "setAllPanel";

        // setAllLabel
        setAllLabel.Anchor = AnchorStyles.Left;
        setAllLabel.AutoSize = true;
        setAllLabel.Margin = new Padding(0, 0, 8, 0);
        setAllLabel.Name = "setAllLabel";
        setAllLabel.Text = "Set all to:";

        // setAllComboBox
        setAllComboBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        setAllComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        setAllComboBox.Margin = new Padding(0, 0, 8, 0);
        setAllComboBox.Name = "setAllComboBox";
        foreach (var cat in DocumentCategory.All)
        {
            setAllComboBox.Items.Add(cat);
        }
        setAllComboBox.SelectedIndex = 0;

        // setAllButton
        setAllButton.AutoSize = true;
        setAllButton.Name = "setAllButton";
        setAllButton.Padding = new Padding(8, 2, 8, 2);
        setAllButton.Text = "Apply to All";
        setAllButton.UseVisualStyleBackColor = true;
        setAllButton.Click += SetAllButton_Click;

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

        // DocumentCategorizerForm
        AcceptButton = saveButton;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = cancelButton;
        ClientSize = new Size(760, 540);
        Controls.Add(rootLayout);
        MinimumSize = new Size(600, 400);
        Name = "DocumentCategorizerForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Categorise Documents";

        buttonPanel.ResumeLayout(false);
        buttonPanel.PerformLayout();
        setAllPanel.ResumeLayout(false);
        setAllPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)grid).EndInit();
        rootLayout.ResumeLayout(false);
        rootLayout.PerformLayout();
        ResumeLayout(false);
    }
}
