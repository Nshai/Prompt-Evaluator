// Ported verbatim from IQFlow.Adapters.Ingest, apart from the namespace and this note.
//
// The implementation plan (§4) permits the conversion stage as the one exception to "build against
// v1": these are defect fixes for losses the pipeline analysis names and nothing downstream can
// recover. Kept byte-faithful on purpose — a "tidied" copy is a second implementation to keep in
// step with the original, and the fixes are the product of measurement rather than of taste.
using System.Text;

namespace IQWorkflow;

/// <summary>
/// Turns a Docling table (already resolved into a dense row/col grid by docling-serve) into
/// simple representations used for LLM prompting and for the deterministic fallback narrative.
/// </summary>
public static class TableGridBuilder
{
    public sealed record Grid(int NumRows, int NumCols, string?[][] Cells, IReadOnlyList<int> HeaderRowIndexes);

    public static Grid Build(DoclingTableItem table)
    {
        var data = table.Data ?? new DoclingTableData();
        var rows = data.NumRows > 0 ? data.NumRows : data.Grid.Count;
        var cols = data.NumCols > 0 ? data.NumCols : (data.Grid.Count > 0 ? data.Grid[0].Count : 0);

        var cells = new string?[rows][];
        for (var r = 0; r < rows; r++)
        {
            cells[r] = new string?[cols];
        }

        var headerRows = new SortedSet<int>();

        for (var r = 0; r < data.Grid.Count && r < rows; r++)
        {
            var row = data.Grid[r];
            for (var c = 0; c < row.Count && c < cols; c++)
            {
                var cell = row[c];
                cells[r][c] = cell.Text ?? string.Empty;
                if (cell.ColumnHeader)
                {
                    headerRows.Add(r);
                }
            }
        }

        // Docling always marks header rows explicitly; if none were flagged (rare), assume row 0.
        if (headerRows.Count == 0 && rows > 0)
        {
            headerRows.Add(0);
        }

        return new Grid(rows, cols, cells, headerRows.ToList());
    }

    /// <summary>Renders the grid back to a compact pipe-delimited table for LLM consumption.</summary>
    public static string ToDelimitedText(Grid grid)
    {
        var sb = new StringBuilder();
        for (var r = 0; r < grid.NumRows; r++)
        {
            sb.Append(string.Join(" | ", grid.Cells[r].Select(v => (v ?? string.Empty).Replace('\n', ' ').Trim())));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public static IReadOnlyList<string> GetHeaderLabels(Grid grid)
    {
        if (grid.HeaderRowIndexes.Count == 0 || grid.NumCols == 0)
        {
            return [];
        }

        var headerRow = grid.HeaderRowIndexes.Min();
        return Enumerable.Range(0, grid.NumCols)
            .Select(c => grid.Cells[headerRow][c] ?? $"Column {c + 1}")
            .ToList();
    }
}
