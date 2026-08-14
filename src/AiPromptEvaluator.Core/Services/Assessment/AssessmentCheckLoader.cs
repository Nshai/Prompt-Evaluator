using System.IO;

namespace AiPromptEvaluator;

public static class AssessmentCheckLoader
{
    public static List<AssessmentCheck> Load(string csvPath)
    {
        var text = File.ReadAllText(csvPath);
        var rows = CsvParser.Parse(text);

        // Skip header row
        return rows.Skip(1)
                   .Where(r => r.Count >= 1 && !string.IsNullOrWhiteSpace(r[0]))
                   .Select(AssessmentCheck.FromRow)
                   .ToList();
    }

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
