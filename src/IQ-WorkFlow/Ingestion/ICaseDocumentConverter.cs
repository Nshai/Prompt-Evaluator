namespace IQWorkflow;

/// <summary>
/// Converts one case document to Markdown.
///
/// <b>A bundle rather than a single result, because one file is not always one document.</b> An
/// email carries attachments, and in a case bundle the attachment is routinely the real evidence —
/// one <c>.msg</c> in Test Case 3 carries the certified Pension Sharing Order whose ticked "Internal
/// transfer" box is the case's central contradiction. Converting the covering note and discarding
/// what it delivered would be the most expensive kind of thorough.
///
/// The seam exists so email handling is a decorator over the ordinary converter rather than a branch
/// inside it: the email reader handles the two formats Docling cannot, hands each attachment back to
/// the real converter, and delegates everything else untouched.
/// </summary>
public interface ICaseDocumentConverter
{
    /// <summary>
    /// Converts one file into one or more Markdown documents.
    ///
    /// Never throws for a document that cannot be read — that is an outcome, carried on the
    /// <see cref="DocumentConversion.Error"/> of the result, because a case file of thirty-seven
    /// documents where one is encrypted should produce thirty-six conversions and a line saying why
    /// the thirty-seventh is missing.
    /// </summary>
    Task<IReadOnlyList<DocumentConversion>> ConvertAsync(
        string path,
        string relativeSource,
        string categoryCode,
        CancellationToken cancellationToken = default);
}
