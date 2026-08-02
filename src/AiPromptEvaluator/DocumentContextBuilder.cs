using System.IO;
using System.Text;

namespace AiPromptEvaluator;

public static class DocumentContextBuilder
{
    public static string BuildContext(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return string.Empty;
        }

        var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList();

        if (files.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("The following local documents were found and may be used as context:");

        foreach (var file in files)
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
            }
        }

        return builder.ToString();
    }
}
