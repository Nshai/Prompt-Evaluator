namespace AiPromptEvaluator;

/// <summary>
/// Creates a check runner for one extracted canonical model.
///
/// The model is the output of a preceding step, so it cannot be a registration; and the runner is
/// disposed by the caller, so it cannot be shared.
/// </summary>
public interface ICheckPlanRunnerFactory
{
    /// <summary>
    /// A runner for one extracted model. The gates are shared across every check of a run, so
    /// the total work in flight is bounded once rather than once per check.
    /// </summary>
    ICheckPlanRunner Create(
        CanonicalModelDocument model,
        ICaseDocumentSearchService search,
        PromptLogWriter? promptLog = null,
        ConcurrencyGate? modelCalls = null,
        ConcurrencyGate? searches = null);
}
