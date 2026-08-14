namespace AiPromptEvaluator;

/// <summary>Assesses one check against a case, from its query plan.</summary>
public interface ICheckPlanRunner : IDisposable
{
    Task<CheckFinding> RunAsync(
        AssessmentCheck check,
        CheckQueryPlan plan,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
