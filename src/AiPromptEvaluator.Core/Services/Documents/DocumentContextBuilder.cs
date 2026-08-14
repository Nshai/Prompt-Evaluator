using System.IO;
using System.Text;

namespace AiPromptEvaluator;

public static class DocumentContextBuilder
{
    public static string BuildContext(string folderPath, IReadOnlyDictionary<string, string>? categories = null)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return string.Empty;
        }

        var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        && !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList();

        if (files.Count == 0)
        {
            return string.Empty;
        }

        // Group files by their assigned category; uncategorised files go last.
        var grouped = files
            .GroupBy(f => GetCategory(f, categories))
            .OrderBy(g => g.Key == DocumentCategory.Uncategorised ? 1 : 0)
            .ThenBy(g => g.Key);

        var builder = new StringBuilder();
        builder.AppendLine("The following documents have been provided as context. Each is labelled with its category.");
        builder.AppendLine();

        foreach (var group in grouped)
        {
            builder.AppendLine($"=== Category: {group.Key} ===");
            builder.AppendLine();

            foreach (var file in group)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    var preview = content.Length > 1800 ? content[..1800] : content;
                    builder.AppendLine($"--- File: {Path.GetFileName(file)} ---");
                    builder.AppendLine(preview.Replace("\r\n", "\n"));
                    builder.AppendLine();
                }
                catch
                {
                    builder.AppendLine($"--- File: {Path.GetFileName(file)} (unreadable) ---");
                    builder.AppendLine();
                }
            }
        }

        return builder.ToString();
    }

    private static string GetCategory(string filePath, IReadOnlyDictionary<string, string>? categories)
    {
        if (categories is null)
        {
            return DocumentCategory.Uncategorised;
        }

        var fileName = Path.GetFileName(filePath);
        return categories.TryGetValue(fileName, out var cat) && !string.IsNullOrEmpty(cat)
            ? cat
            : DocumentCategory.Uncategorised;
    }
}
