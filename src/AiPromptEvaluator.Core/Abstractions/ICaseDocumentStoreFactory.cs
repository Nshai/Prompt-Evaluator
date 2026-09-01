namespace AiPromptEvaluator;

/// <summary>
/// Creates a vector store, optionally against settings that have not been saved.
///
/// A store holds a connection and is disposed by whoever opened it, so it is created per
/// operation rather than shared: a singleton would be closed by the first <c>using</c> that
/// finished and be unusable to the next.
/// </summary>
public interface ICaseDocumentStoreFactory
{
    /// <summary>The caller disposes what it gets.</summary>
    ICaseDocumentStore Create(AppSettings? settings = null);
}
