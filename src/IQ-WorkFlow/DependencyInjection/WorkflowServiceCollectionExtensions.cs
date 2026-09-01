using AiPromptEvaluator;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace IQWorkflow;

/// <summary>
/// The composition root.
///
/// <b>Registration order is execution order.</b> The runner takes the stages as an ordered sequence
/// and does not name any of them, so the pipeline's shape is readable in one place — here — rather
/// than reconstructed from priorities that each stage asserts about itself. Adding a stage is a line
/// in this method; it is not an edit to the runner, which is what makes the pipeline open for
/// extension and closed for modification in the way that actually matters.
/// </summary>
public static class WorkflowServiceCollectionExtensions
{
    /// <summary>
    /// Adds the workflow over an already-registered case pipeline.
    ///
    /// <paramref name="settings"/> is the live instance <c>AddCasePipeline</c> registers, so a
    /// configuration change is seen by every stage rather than by whichever ones were built after
    /// it.
    /// </summary>
    public static IServiceCollection AddWorkflow(this IServiceCollection services, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        services.AddCasePipeline(settings);

        // Deferred for the same reason Core defers its own: building an embedding generator needs a
        // configured key, and the workflow is composed before anything has validated one. An eager
        // singleton would turn a missing key into a failure to start rather than a failure at the
        // first call that names what is missing.
        services.AddSingleton<Func<AppSettings, IEmbeddingGenerator<string, Embedding<float>>>>(
            _ => AiClientFactory.CreateEmbeddingGenerator);

        services.AddSingleton<
            Func<AppSettings, IEmbeddingGenerator<string, Embedding<float>>, ICaseDocumentStore, ICaseDocumentIndexer>>(
            _ => (s, embeddings, store) => new CaseDocumentIndexer(s, embeddings, store));

        // How a converter is built for a run. The Docling endpoint comes from settings, so the
        // converter cannot be a singleton built before anything has validated one.
        services.AddSingleton<Func<AppSettings, ICaseDocumentConverter>>(
            _ => (Func<AppSettings, ICaseDocumentConverter>)BuildConverter);

        // The pipeline. Order here is only readability — the graph is built from each stage's own
        // declared dependencies, so conversion leads because extraction and indexing both name it.
        services.AddSingleton<IWorkflowStage, ConvertDocumentsStage>();
        services.AddSingleton<IWorkflowStage, ExtractModelStage>();
        services.AddSingleton<IWorkflowStage, IndexCaseStage>();
        services.AddSingleton<IWorkflowStage, AssessChecksStage>();
        services.AddSingleton<IWorkflowStage, ReportStage>();

        // Last, and after the report: scoring reads the findings the report has already been
        // written from, and a benchmark that is missing must not stop a run producing its report.
        services.AddSingleton<IWorkflowStage, ScoreBenchmarkStage>();

        // The Agent Framework's engine drives the graph. WorkflowRunner is kept as the plain
        // sequential alternative — it needs no engine, which makes it the cheaper thing to reach
        // for when diagnosing whether a problem is in a stage or in the orchestration.
        services.AddSingleton<AgentWorkflowRunner>();
        services.AddSingleton<WorkflowRunner>();

        return services;
    }

    /// <summary>
    /// The conversion chain for one run: Docling underneath, email over the top.
    ///
    /// <b>A decorator rather than a branch inside the converter.</b> The email reader handles the
    /// two formats Docling cannot, hands each attachment back to Docling, and delegates everything
    /// else untouched — so neither half knows anything about the other's problem.
    ///
    /// <b>The two paid passes are built only when configuration asks for them</b>, and a chat client
    /// is constructed only in that case. That ordering is deliberate: it means a run with narration
    /// off never needs a chat credential to convert a case, so the default configuration converts on
    /// a machine that has one endpoint configured and nothing else.
    /// </summary>
    private static ICaseDocumentConverter BuildConverter(AppSettings settings)
    {
        var pictures = new PictureNarrationOptions
        {
            Enabled = settings.PictureNarration,
            MinimumImageBytes = settings.MinimumImageBytes,
            MaxImagesPerDocument = settings.MaxImagesPerDocument,
        };

        var tables = new TableNarrationOptions { Enabled = settings.TableNarration };

        IPictureDescriber? describer = null;
        ITableNarrator? narrator = null;

        if (pictures.Enabled || tables.Enabled)
        {
            var model = string.IsNullOrWhiteSpace(settings.NarrationModel)
                ? settings.SelectedModel
                : settings.NarrationModel;

            // One client for both, because they are the same endpoint and the same credential, and
            // two would double the connection pool for no gain.
            var client = AiClientFactory.CreateChatClient(settings);

            describer = pictures.Enabled ? new LlmPictureDescriber(client, model) : null;
            narrator = tables.Enabled ? new LlmTableNarrator(client, model) : null;
        }

        var docling = new DoclingMarkdownConverter(
            new DoclingClient(new DoclingOptions
            {
                BaseUrl = settings.ResolveDoclingEndpoint(),

                // Asked for only when something will read them: base64 images make the response an
                // order of magnitude larger, and nothing else in this pipeline looks at them.
                IncludeImages = pictures.Enabled,
            }),
            pictures,
            describer,
            tables,
            narrator);

        return new EmailDocumentConverter(docling);
    }
}
