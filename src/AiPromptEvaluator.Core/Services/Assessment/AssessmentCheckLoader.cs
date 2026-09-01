using System.IO;

namespace AiPromptEvaluator;

public static class AssessmentCheckLoader
{
    /// <summary>
    /// Column headers this loader understands, in the order <see cref="AssessmentCheck"/>
    /// declares its fields. Each entry lists the spellings seen in the wild for one field.
    ///
    /// Matching is by name rather than position because the revised checks workbook drops
    /// Regulatory Basis and suffixes several headers with "(revised)". Read positionally,
    /// that file silently shifts every field from index 3 on: the categories land in
    /// RegulatoryBasis, the prompt lands in PrimaryDocumentCategories, and DecisionLogic
    /// comes back empty. Nothing about the run looks wrong — the checks simply assess the
    /// wrong text.
    /// </summary>
    private static readonly string[][] KnownHeaders =
    [
        ["check id", "checkid", "id"],
        ["check name", "checkname", "name"],
        ["applies to", "appliesto"],
        ["regulatory basis", "regulatorybasis"],
        ["primary document categories", "primary document categories (revised)", "document categories"],
        ["prompt", "prompt (revised)"],
        ["what to look for", "what to look for (revised)"],
        ["decision logic", "decision logic (revised)"],
    ];

    /// <summary>
    /// Reads the checks CSV, mapping columns by header name.
    ///
    /// A field whose header is absent comes back empty rather than borrowing its neighbour's
    /// value, and a row whose check id is not a CHK-nnn is skipped — the revised workbook
    /// carries a leading "Rules:" row that would otherwise parse as an eleventh check.
    /// </summary>
    public static List<AssessmentCheck> Load(string csvPath)
    {
        var rows = CsvParser.Parse(File.ReadAllText(csvPath));

        if (rows.Count == 0)
        {
            return [];
        }

        var index = MapColumns(rows[0]);

        return rows.Skip(1)
                   .Where(row => IsCheckRow(row, index))
                   .Select(row => AssessmentCheck.FromRow(row, index))
                   .ToList();
    }

    /// <summary>
    /// Field position to column position, or -1 where the file does not carry that column.
    /// Falls back to identity for any field whose header is missing, so a file with no
    /// recognisable header row still loads the way it always did.
    /// </summary>
    internal static int[] MapColumns(IReadOnlyList<string> header)
    {
        var normalised = header
            .Select(h => new string((h ?? string.Empty).Trim().ToLowerInvariant()
                .Where(c => !char.IsControl(c)).ToArray()))
            .ToList();

        var map = new int[KnownHeaders.Length];
        var recognised = 0;

        for (var field = 0; field < KnownHeaders.Length; field++)
        {
            map[field] = normalised.FindIndex(h => KnownHeaders[field].Contains(h));

            if (map[field] >= 0)
            {
                recognised++;
            }
        }

        // No header row worth the name. Positional is what this file must have meant.
        if (recognised < 2)
        {
            for (var field = 0; field < map.Length; field++)
            {
                map[field] = field;
            }
        }

        return map;
    }

    /// <summary>
    /// Whether a row is a check rather than a note. Rules, blank separators and continuation
    /// rows all fail this, and none of them should become an eleventh check.
    /// </summary>
    private static bool IsCheckRow(List<string> row, IReadOnlyList<int> columns)
    {
        // The id column, not column zero — the header map may have put it anywhere.
        var column = columns.Count > 0 ? columns[0] : 0;

        return column >= 0
            && column < row.Count
            && !string.IsNullOrWhiteSpace(row[column])
            && CheckIdPattern.IsMatch(row[column]);
    }

    private static readonly System.Text.RegularExpressions.Regex CheckIdPattern =
        new(@"^\s*CHK-\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// Returns all files under <paramref name="caseFolder"/> in the sub-folders
    /// whose names match any of <paramref name="categoryCodes"/>.
    /// e.g. codes ["A","B"] → caseFolder/A/* + caseFolder/B/*
    /// </summary>
    public static List<string> GetFilesForCodes(string caseFolder, IEnumerable<string> categoryCodes)
    {
        var result = new List<string>();
        foreach (var code in categoryCodes)
        {
            var dir = Path.Combine(caseFolder, code);
            if (!Directory.Exists(dir))
            {
                continue;
            }

            result.AddRange(
                Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                         .Where(f => !f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                                  && !f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)));
        }
        return result;
    }
}
