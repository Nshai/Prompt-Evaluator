using System.IO;
using System.Text.Json;

namespace AiPromptEvaluator;

public static class SettingsStore
{
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AiPromptEvaluator",
        "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(AppDataPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(AppDataPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(AppDataPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppDataPath, json);
    }
}
