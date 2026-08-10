using System.IO;

namespace AiPromptEvaluator;

public partial class DocumentCategorizerForm : Form
{
    private readonly string _folderPath;
    private readonly Dictionary<string, string> _categories;

    public IReadOnlyDictionary<string, string> Categories => _categories;

    public DocumentCategorizerForm(string folderPath, Dictionary<string, string> existingCategories)
    {
        InitializeComponent();

        _folderPath = folderPath;
        _categories = new Dictionary<string, string>(existingCategories, StringComparer.OrdinalIgnoreCase);

        folderPathLabel.Text = folderPath;
        LoadFiles();
    }

    private void LoadFiles()
    {
        if (!Directory.Exists(_folderPath))
        {
            return;
        }

        var files = Directory.GetFiles(_folderPath, "*.*", SearchOption.AllDirectories)
            .Where(p => !p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                     && !p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p)
            .ToList();

        grid.Rows.Clear();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var relativePath = Path.GetRelativePath(_folderPath, file);
            _categories.TryGetValue(fileName, out var savedCategory);
            var category = savedCategory ?? DocumentCategory.Uncategorised;

            var rowIndex = grid.Rows.Add(relativePath, category);
            grid.Rows[rowIndex].Tag = fileName;
        }
    }

    private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != categoryColumn.Index)
        {
            return;
        }

        var row = grid.Rows[e.RowIndex];
        var fileName = row.Tag as string;
        var category = row.Cells[categoryColumn.Index].Value as string;

        if (fileName is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(category) || category == DocumentCategory.Uncategorised)
        {
            _categories.Remove(fileName);
        }
        else
        {
            _categories[fileName] = category;
        }
    }

    private void Grid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (grid.IsCurrentCellDirty)
        {
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void SetAllButton_Click(object? sender, EventArgs e)
    {
        var category = setAllComboBox.Text;
        if (string.IsNullOrEmpty(category))
        {
            return;
        }

        foreach (DataGridViewRow row in grid.Rows)
        {
            row.Cells[categoryColumn.Index].Value = category;
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }
}
