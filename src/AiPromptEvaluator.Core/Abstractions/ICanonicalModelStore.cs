namespace AiPromptEvaluator;

/// <summary>Where an extracted canonical model is kept between runs.</summary>
public interface ICanonicalModelStore
{
    string DatabasePath { get; }

    Task SaveAsync(CanonicalModelDocument document, CancellationToken cancellationToken = default);

    Task<CanonicalModelDocument?> LoadAsync(
        string caseReference,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string caseReference, int tenantId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string caseReference, int tenantId, CancellationToken cancellationToken = default);
}
