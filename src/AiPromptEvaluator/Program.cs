namespace AiPromptEvaluator;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var settings = SettingsStorage.Load();
        Application.Run(new CheckEvaluatorForm(settings));
    }
}
