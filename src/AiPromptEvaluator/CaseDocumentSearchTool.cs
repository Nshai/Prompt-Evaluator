using System.ComponentModel;

using Microsoft.Extensions.AI;

namespace AiPromptEvaluator;

/// <summary>One passage handed back to the model, with everything it needs to cite it.</summary>
public sealed record CaseDocumentSearchMatch(
    string SearchedText,
    string CaseReference,
    int TenantId,
    string DocumentName,
    string CategoryCode,
    string CategoryName,
    double Score);

/// <summary>A search the model ran, kept so the run can be shown and audited afterwards.</summary>
public sealed record CaseSearchCall(string SearchText, int Matches);

/// <summary>
/// The semantic-search function the model calls while assessing a check: it embeds the
/// model's search text and returns the closest passages from one case, filtered by tenant.
///
/// Matches from several documents come back together — the same point can be evidenced in
/// a fact-find and a suitability report, and the check needs to see both.
/// </summary>
public sealed class CaseDocumentSearchTool
{
    private readonly AppSettings _settings;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddings;
    private readonly CaseDocumentStore _store;
    private readonly string _caseReference;
    private readonly List<CaseSearchCall> _calls = new();

    public CaseDocumentSearchTool(
        AppSettings settings,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        CaseDocumentStore store,
        string caseReference)
    {
        _settings = settings;
        _embeddings = embeddings;
        _store = store;
        _caseReference = caseReference;
    }

    /// <summary>Every search the model ran, in order, for the run report.</summary>
    public IReadOnlyList<CaseSearchCall> Calls => _calls;

    /// <summary>The tool definition to hand to the chat client.</summary>
    public AIFunction AsAIFunction() => AIFunctionFactory.Create(
        SearchAsync,
        name: "search_case_documents",
        description:
            "Search the indexed case file for passages relevant to a question or phrase. "
            + "Returns the matching text from the document along with the case reference, tenant id, "
            + "document name and document category, so every finding can be cited. Matches from "
            + "several documents are returned when the case evidences the point in more than one place. "
            + "Call this repeatedly with different wordings to gather evidence before deciding a check.");

    [Description("Searches the case documents for text relevant to the search phrase.")]
    private async Task<IReadOnlyList<CaseDocumentSearchMatch>> SearchAsync(
        [Description("What to look for, in natural language, e.g. 'client attitude to risk questionnaire score'.")]
        string searchText,
        [Description("The case reference to search within. Omit to use the case that is loaded.")]
        string? caseReference = null,
        [Description("The tenant that owns the case. Omit to use the configured tenant.")]
        int? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [];
        }

        // The model may echo back a case or tenant it read in the prompt; anything else is a
        // hallucinated identifier, so fall back to what was actually loaded rather than
        // searching a case this run has nothing to do with.
        var scopedCase = string.IsNullOrWhiteSpace(caseReference) ? _caseReference : caseReference.Trim();
        var scopedTenant = tenantId ?? _settings.TenantId;

        var vector = await _embeddings
            .GenerateVectorAsync(searchText, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var hits = await _store
            .SearchAsync(scopedCase, scopedTenant, vector, _settings.MaxSearchResults, cancellationToken)
            .ConfigureAwait(false);

        lock (_calls)
        {
            _calls.Add(new CaseSearchCall(searchText.Trim(), hits.Count));
        }

        return hits
            .Select(hit => new CaseDocumentSearchMatch(
                SearchedText: hit.Chunk.Text,
                CaseReference: hit.Chunk.CaseReference,
                TenantId: hit.Chunk.TenantId,
                DocumentName: hit.Chunk.DocumentName,
                CategoryCode: hit.Chunk.CategoryCode,
                CategoryName: hit.Chunk.CategoryName,
                Score: Math.Round(hit.Score, 4)))
            .ToList();
    }
}
