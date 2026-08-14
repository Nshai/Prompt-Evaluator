using Microsoft.Extensions.DependencyInjection;

namespace AiPromptEvaluator;

/// <summary>
/// The composition root, and the only place that knows both halves of the application.
///
/// It builds the container, adds the pipeline, adds the screens that consume it, and resolves the
/// first one. Everything downstream asks for what it needs. A different front end — a web host, a
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
    /// The container. Forms are transient because a closed form is disposed and a reopened one
    /// must be new; the pipeline decides its own lifetimes in
    /// <see cref="CasePipelineServiceCollectionExtensions.AddCasePipeline"/>.
    /// </summary>
    private static ServiceProvider Build(AppSettings settings) =>
        new ServiceCollection()
            .AddCasePipeline(settings)
            .AddTransient<CheckEvaluatorForm>()
            .AddTransient<MainForm>()

            // The two screens open each other. Typed factories rather than an injected
            // IServiceProvider, so navigation stays a declared dependency instead of a lookup
            // that only fails at run time.
            .AddSingleton<Func<CheckEvaluatorForm>>(sp => sp.GetRequiredService<CheckEvaluatorForm>)
            .AddSingleton<Func<MainForm>>(sp => sp.GetRequiredService<MainForm>)
            .BuildServiceProvider();
}
