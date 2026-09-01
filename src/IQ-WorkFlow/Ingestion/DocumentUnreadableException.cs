namespace IQWorkflow;

/// <summary>
/// A document the converter could open and could not read — an encrypted PDF, a corrupt file, a
/// password-protected workbook.
///
/// <b>Ported from <c>IQFlow.Core.Ingest</c> because the distinction it carries is load-bearing.</b>
/// The client that throws it says so in its own words: a document that failed to convert and a
/// document that converted to nothing "must not be triaged alike". One is an operator problem with
/// a fix — supply the password, repair the file — and the other is a document that genuinely holds
/// no text.
///
/// <b>Why that matters downstream and not only here.</b> A coverage gap over a document nobody could
/// open reads, to every later stage, as "the case file does not evidence this" — which is a claim
/// about the advice. It is actually a claim about the run. Collapsing this into a generic failure
/// would lose exactly the fact that separates the two.
/// </summary>
public sealed class DocumentUnreadableException(string fileName, string reason)
    : InvalidOperationException($"'{fileName}' could not be read: {reason}")
{
    /// <summary>Which document.</summary>
    public string FileName { get; } = fileName;

    /// <summary>Why, in terms an operator can act on.</summary>
    public string Reason { get; } = reason;
}
