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

    public static AssessmentCheck FromRow(List<string> row)
    {
        string Get(int i) => i < row.Count ? row[i].Trim() : string.Empty;
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
