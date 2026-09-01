using AiPromptEvaluator;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The container has to be able to build what it promises.
///
/// A missing or mis-ordered registration is invisible until something asks for it, and the thing
/// that asks first is the application starting up — so the failure arrives as a crash on launch,
/// after a build that looked fine. Resolving every contract here moves that to the test run.
///
/// The settings point at nothing real. Nothing resolved below opens a connection in its
/// constructor, and that is worth keeping true: a service that dials out when it is *built*
/// rather than when it is *used* cannot be registered as a singleton and cannot be tested
/// without a live endpoint.
/// </summary>
public class CasePipelineRegistrationTests
{
    private static ServiceProvider Build() =>
        new ServiceCollection()
            .AddCasePipeline(new AppSettings())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

    [Theory]
    [InlineData(typeof(IChatCompletionClient))]
    [InlineData(typeof(ICanonicalModelStore))]
    [InlineData(typeof(ICanonicalModelExtractor))]
    [InlineData(typeof(IChatCompletionClientFactory))]
    [InlineData(typeof(ICaseDocumentStoreFactory))]
    [InlineData(typeof(IDoclingClientFactory))]
    [InlineData(typeof(ICaseDocumentSearchServiceFactory))]
    [InlineData(typeof(ICheckPlanRunnerFactory))]
    [InlineData(typeof(IPromptLogWriterFactory))]
    public void EveryContractResolves(Type contract)
    {
        using var services = Build();

        Assert.NotNull(services.GetRequiredService(contract));
    }

    /// <summary>
    /// The settings instance is shared, not copied. The configuration screen edits it in place
    /// and every service is expected to see the change without anything being rebuilt.
    /// </summary>
    [Fact]
    public void SettingsAreTheLiveInstance()
    {
        var settings = new AppSettings();

        using var services = new ServiceCollection().AddCasePipeline(settings).BuildServiceProvider();

        Assert.Same(settings, services.GetRequiredService<AppSettings>());
    }

    /// <summary>
    /// A factory given no settings uses the live ones; given some, it uses those. That second
    /// case is the configuration screen testing a connection the user has typed and not saved,
    /// and it is the only reason these factories take a parameter at all.
    /// </summary>
    [Fact]
    public void AFactoryHonoursSettingsPassedToIt()
    {
        var live = new AppSettings { SelectedModel = "live-model" };
        var probe = new AppSettings { SelectedModel = "probe-model" };

        using var services = new ServiceCollection().AddCasePipeline(live).BuildServiceProvider();
        var factory = services.GetRequiredService<IChatCompletionClientFactory>();

        Assert.NotSame(factory.Create(), factory.Create(probe));
    }

    /// <summary>
    /// Anything the caller disposes must be a fresh instance. A shared store would be closed by
    /// the first <c>using</c> that finished and be unusable to the next.
    /// </summary>
    [Fact]
    public void AnythingTheCallerDisposesIsANewInstance()
    {
        using var services = Build();

        var factory = services.GetRequiredService<ICaseDocumentStoreFactory>();

        using var first = factory.Create();
        using var second = factory.Create();

        Assert.NotSame(first, second);
    }
}
