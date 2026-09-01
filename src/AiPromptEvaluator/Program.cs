using Microsoft.Extensions.DependencyInjection;

namespace AiPromptEvaluator;

/// <summary>
/// The composition root, and the only place that knows both halves of the application.
///
/// It builds the container, adds the pipeline, adds the screen that consumes it, and resolves it.
/// Everything downstream asks for what it needs. A different front end — a web host, a
/// CLI, a scheduled job — starts the same way: call <c>AddCasePipeline</c> and resolve.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var services = Build(SettingsStore.Load());

        Application.Run(services.GetRequiredService<CheckEvaluatorForm>());
    }

    /// <summary>
    /// The container. The form is transient because a closed form is disposed and a reopened one
    /// must be new; the pipeline decides its own lifetimes in
    /// <see cref="CasePipelineServiceCollectionExtensions.AddCasePipeline"/>.
    /// </summary>
    private static ServiceProvider Build(AppSettings settings) =>
        new ServiceCollection()
            .AddCasePipeline(settings)
            .AddTransient<CheckEvaluatorForm>()
            .BuildServiceProvider();
}
