using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AiPromptEvaluator;

/// <summary>
/// Registers the pipeline, so a front end asks for what it needs rather than knowing how to
/// build it.
///
/// This is the other half of splitting the assembly. Moving the code out of the WinForms project
/// stopped the pipeline depending on a user interface; this stops a user interface depending on
/// the pipeline's construction order — which forms, until now, knew in detail: which store to
/// open before which indexer, which client to hand the extractor, which gates to share between
/// runners.
///
/// **Lifetimes are chosen from what the classes actually do, not by convention.** The clients and
/// stores that hold a connection or a file handle are created per operation through a factory and
/// disposed by whoever asked for them, because that is how they are already used and changing it
/// would change when a connection closes. What is registered as a singleton is only what is safe
/// to share: settings, the model clients, and the stateless services built on them.
/// </summary>
public static class CasePipelineServiceCollectionExtensions
{
    /// <summary>
    /// Adds everything needed to index a case, extract its canonical model and assess it.
    ///
    /// <paramref name="settings"/> is registered as the live instance rather than copied: the
    /// configuration screen edits it in place and every service is expected to see the change.
    /// </summary>
    public static IServiceCollection AddCasePipeline(this IServiceCollection services, AppSettings settings)
    {
        services.AddSingleton(settings);

        // The embedding generator is registered as a *deferred* factory, not an instance.
        //
        // Building one requires a configured API key, and the application starts before the user
        // has configured anything — the first thing they do is open the settings screen. An eager
        // singleton turns that into a crash on launch, where the behaviour has always been to
        // fail at the point of the first call with a message naming what is missing. A test
        // caught this; it would otherwise have shipped as "the app no longer starts".
        services.AddSingleton<Func<IEmbeddingGenerator<string, Embedding<float>>>>(sp =>
            () => AiClientFactory.CreateEmbeddingGenerator(sp.GetRequiredService<AppSettings>()));

        // Stateless over their dependencies, so one instance serves the whole process.
        services.AddSingleton<IChatCompletionClient, ChatCompletionClient>();
        services.AddSingleton<ICanonicalModelStore, CanonicalModelStore>();
        services.AddSingleton<ICanonicalModelExtractor, CanonicalModelExtractor>();


        // Everything whose lifetime or arguments belong to the caller.
        services.AddSingleton<IChatCompletionClientFactory, ChatCompletionClientFactory>();
        services.AddSingleton<ICaseDocumentStoreFactory, CaseDocumentStoreFactory>();
        services.AddSingleton<IDoclingClientFactory, DoclingClientFactory>();
        services.AddSingleton<ICaseDocumentSearchServiceFactory, CaseDocumentSearchServiceFactory>();
        services.AddSingleton<ICheckPlanRunnerFactory, CheckPlanRunnerFactory>();
        services.AddSingleton<IPromptLogWriterFactory, PromptLogWriterFactory>();

        return services;
    }
}

// ──────────────────────────────────────────────
// The factories. Each is a few lines, and they live together because they exist for one reason:
// the container cannot supply a case reference, an extracted model, or the unsaved settings a
// configuration screen is testing.
// ──────────────────────────────────────────────

internal sealed class ChatCompletionClientFactory(AppSettings live) : IChatCompletionClientFactory
{
    public IChatCompletionClient Create(AppSettings? settings = null) => new ChatCompletionClient(settings ?? live);
}

internal sealed class CaseDocumentStoreFactory(AppSettings live) : ICaseDocumentStoreFactory
{
    public ICaseDocumentStore Create(AppSettings? settings = null) => new CaseDocumentStore(settings ?? live);
}

internal sealed class DoclingClientFactory(AppSettings live) : IDoclingClientFactory
{
    public IDoclingClient Create(Func<string>? endpointProvider = null) =>
        new DoclingClient(endpointProvider ?? live.ResolveDoclingEndpoint);
}

internal sealed class CaseDocumentSearchServiceFactory(
    AppSettings settings,
    Func<IEmbeddingGenerator<string, Embedding<float>>> defaultEmbeddings) : ICaseDocumentSearchServiceFactory
{
    public ICaseDocumentSearchService Create(
        string caseReference,
        ICaseDocumentStore store,
        IEmbeddingGenerator<string, Embedding<float>>? embeddings = null) =>
        new CaseDocumentSearchService(settings, embeddings ?? defaultEmbeddings(), store, caseReference);
}

internal sealed class CheckPlanRunnerFactory(
    AppSettings settings,
    IChatCompletionClient chat) : ICheckPlanRunnerFactory
{
    public ICheckPlanRunner Create(
        CanonicalModelDocument model,
        ICaseDocumentSearchService search,
        PromptLogWriter? promptLog = null,
        ConcurrencyGate? modelCalls = null,
        ConcurrencyGate? searches = null) =>
        new CheckPlanRunner(settings, chat, search, model, promptLog, modelCalls, searches);
}

internal sealed class PromptLogWriterFactory : IPromptLogWriterFactory
{
    public PromptLogWriter Create(
        string logFolder,
        string caseReference,
        DateTimeOffset startedAt,
        string? filePrefix = null) =>
        new(logFolder, caseReference, startedAt, filePrefix);
}
