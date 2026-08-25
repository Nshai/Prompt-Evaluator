using System.Text.RegularExpressions;

namespace AiPromptEvaluator;

public sealed record AssessmentCheck(
    string CheckId,
    string CheckName,
    string AppliesTo,
    string RegulatoryBasis,
    string PrimaryDocumentCategories,
    string Prompt,
    string WhatToLookFor,
    string DecisionLogic)
{
    /// <summary>
    /// Extracts the single-letter category codes from the PrimaryDocumentCategories field.
    /// e.g. "Client Authority (A)\nKnow Your Client (B)" → ["A", "B"]
    /// </summary>
    public IReadOnlyList<string> CategoryCodes =>
        Regex.Matches(PrimaryDocumentCategories, @"\(([A-Z])\)")
             .Select(m => m.Groups[1].Value)
             .Distinct()
             .OrderBy(c => c)
             .ToList();

    /// <summary>
    /// Positional read, kept for callers that already know their column order.
    /// </summary>
    public static AssessmentCheck FromRow(List<string> row) =>
        FromRow(row, [0, 1, 2, 3, 4, 5, 6, 7]);

    /// <summary>
    /// Reads one row through a field-to-column map, as produced by
    /// <see cref="AssessmentCheckLoader.MapColumns"/>. A field mapped to -1 is a column the
    /// file does not carry and comes back empty — never the next column's value.
    /// </summary>
    public static AssessmentCheck FromRow(List<string> row, IReadOnlyList<int> columns)
    {
        string Get(int field)
        {
            if (field >= columns.Count)
            {
                return string.Empty;
            }

            var column = columns[field];
            return column >= 0 && column < row.Count ? row[column].Trim() : string.Empty;
        }

        return new AssessmentCheck(
            CheckId: Get(0),
            CheckName: Get(1),
            AppliesTo: Get(2),
            RegulatoryBasis: Get(3),
            PrimaryDocumentCategories: Get(4),
            Prompt: Get(5),
            WhatToLookFor: Get(6),
            DecisionLogic: Get(7));
    }
}
