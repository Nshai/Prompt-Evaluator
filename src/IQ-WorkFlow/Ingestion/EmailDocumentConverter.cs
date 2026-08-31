// Ported from IQFlow.Adapters.Ingest. The reading half — MIME and Outlook parsing, the header
// block, the inline-part rules, the attachment filters — is byte-faithful apart from the namespace.
// The writing half is not: this emits Markdown into the case folder, which is v1's output contract,
// where the reference implementation returns segments.
//
// The implementation plan (§4) permits the conversion stage as the one exception to "build against
// v1", and email is one of the three fixes it names.
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

using MimeKit;

namespace IQWorkflow;

/// <summary>One attachment, in memory.</summary>
/// <param name="FileName">Sanitised.</param>
/// <param name="Content">The bytes.</param>
public sealed record EmailAttachment(string FileName, byte[] Content);

/// <summary>An email, reduced to what the pipeline needs.</summary>
/// <param name="Header">Labelled headers, citable as evidence.</param>
/// <param name="Subject">For captioning the body.</param>
/// <param name="Body">Plain text.</param>
/// <param name="Attachments">Everything it carried.</param>
/// <param name="Date">
/// When it was sent. Carried separately from the header text because Test Case 3 turns on a date.
/// </param>
public sealed record EmailContent(
    string Header,
    string Subject,
    string Body,
    IReadOnlyList<EmailAttachment> Attachments,
    string? Date = null);

/// <summary>
/// Converts case correspondence, and everything attached to it.
///
/// <b>Email is evidence twice over, and the second way is the one that gets missed.</b> The body
/// carries what was said; the headers carry <em>when</em> — and Test Case 3 turns on exactly that:
/// the recommendation meeting was scheduled for 30 March, six days after the suitability report is
/// dated. A converter that keeps the prose and drops the <c>Date</c> header loses the finding.
///
/// <b>Attachments are converted too, and in a case bundle they are often the real evidence.</b> One
/// <c>.msg</c> in Test Case 3 is 2.1 MB and carries the certified Pension Sharing Order — the
/// document whose ticked "Internal transfer" box is the case's central contradiction. Reading the
/// covering note and discarding what it attached would be the most expensive kind of thorough.
///
/// A decorator over the real converter rather than a replacement: it handles the two formats
/// Docling cannot, hands attachments back to Docling, and delegates everything else untouched.
/// </summary>
public sealed class EmailDocumentConverter : ICaseDocumentConverter
{
    /// <summary>
    /// Registers the legacy Windows code pages before any <c>.msg</c> is opened.
    ///
    /// <b>.NET Core ships only Unicode and ASCII.</b> Outlook stores its RTF body with a
    /// <c>Windows-1252</c> declaration, and <c>MsgReader</c> asks the framework for that encoding by
    /// name — which throws <c>'Windows-1252' is not a supported encoding name</c> inside a static
    /// constructor, so the type stays permanently unusable for the life of the process. A whole case
    /// conversion was lost to it. Registering the provider once, here, is the documented fix.
    /// </summary>
    static EmailDocumentConverter() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly ICaseDocumentConverter _inner;

    public EmailDocumentConverter(ICaseDocumentConverter inner) =>
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>Extensions this converter claims.</summary>
    public static IReadOnlySet<string> EmailExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".eml", ".msg" };

    /// <summary>True when this converter, rather than the sidecar, reads the file.</summary>
    public static bool IsEmail(string path) =>
        EmailExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// Attachments not worth converting.
    ///
    /// Signature images and tracking pixels arrive on every message in a thread. Converting them
    /// costs a round trip each and yields a logo.
    /// </summary>
    private static readonly HashSet<string> IgnoredAttachmentExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".p7s", ".p7m", ".ics", ".vcf", ".gif", ".ico" };

    /// <summary>Smallest attachment worth a conversion round trip.</summary>
    internal const long MinimumAttachmentBytes = 4096;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentConversion>> ConvertAsync(
        string path,
        string relativeSource,
        string categoryCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var full = Path.GetFullPath(path);

        if (!IsEmail(full))
        {
            return await _inner.ConvertAsync(full, relativeSource, categoryCode, cancellationToken)
                .ConfigureAwait(false);
        }

        EmailContent message;

        try
        {
            message = Path.GetExtension(full).Equals(".msg", StringComparison.OrdinalIgnoreCase)
                ? ReadOutlookMessage(full)
                : ReadMimeMessage(full);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Same reasoning as an unreadable PDF: an operational fact about one document, recorded
            // so a coverage gap over it does not read downstream as the case file being silent.
            return [new DocumentConversion(
                relativeSource, categoryCode, null, 0,
                $"The message could not be read: {ex.Message}")];
        }

        var stem = Path.GetFileNameWithoutExtension(full);

        var bundle = new List<DocumentConversion>
        {
            new(relativeSource, categoryCode, RenderMarkdown(message), OutputName: stem),
        };

        foreach (var attachment in message.Attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var converted = await ConvertAttachmentAsync(
                attachment, relativeSource, categoryCode, stem, message, cancellationToken)
                .ConfigureAwait(false);

            if (converted is not null)
            {
                bundle.Add(converted);
            }
        }

        return bundle;
    }

    /// <summary>
    /// The email as Markdown: headers first, then the body.
    ///
    /// <b>Headers as their own labelled block, above a heading of their own.</b> A claim about when
    /// something happened has to be able to quote them — "Date: 2026-03-30" is citable in a way an
    /// inference from prose is not — and the chunker gives a passage the nearest heading above it,
    /// so headers and body stay attributable to this message rather than to whatever preceded it.
    /// </summary>
    internal static string RenderMarkdown(EmailContent message)
    {
        var markdown = new StringBuilder();

        markdown.Append("# ").Append(Blank(message.Subject)).Append("\n\n");
        markdown.Append("## Email header\n\n");

        // A fenced block, so a header line is never mistaken for Markdown of its own — an address
        // in angle brackets is otherwise read as an HTML tag and disappears from the rendering.
        markdown.Append("```\n").Append(message.Header).Append("\n```\n");

        if (!string.IsNullOrWhiteSpace(message.Body))
        {
            markdown.Append("\n## Message\n\n").Append(message.Body.Trim()).Append('\n');
        }

        return markdown.ToString();
    }

    /// <summary>
    /// Converts one attachment through the real converter.
    ///
    /// Failures are recorded and skipped rather than thrown. An unreadable attachment on one message
    /// should not lose the message, still less the case — the same reasoning that keeps a
    /// password-protected document from failing a batch.
    /// </summary>
    private async Task<DocumentConversion?> ConvertAttachmentAsync(
        EmailAttachment attachment,
        string relativeSource,
        string categoryCode,
        string stem,
        EmailContent parent,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(attachment.FileName);

        if (IgnoredAttachmentExtensions.Contains(extension)
            || attachment.Content.Length < MinimumAttachmentBytes
            || !DoclingMarkdownConverter.IsConvertible(attachment.FileName)
            || EmailExtensions.Contains(extension))
        {
            return null;
        }

        // Written to a temp file because the converter's contract is a path — it hands the bytes to
        // a sidecar over HTTP, and a stream overload for this one caller would widen the interface
        // for no gain elsewhere.
        var temp = Path.Combine(
            Path.GetTempPath(), "iqworkflow-attachments", Guid.NewGuid().ToString("N"),
            SafeFileName(attachment.FileName));

        Directory.CreateDirectory(Path.GetDirectoryName(temp)!);

        var source = $"{relativeSource} › {attachment.FileName}";

        try
        {
            await File.WriteAllBytesAsync(temp, attachment.Content, cancellationToken).ConfigureAwait(false);

            var converted = await _inner
                .ConvertAsync(temp, source, categoryCode, cancellationToken)
                .ConfigureAwait(false);

            var first = converted.Count > 0 ? converted[0] : null;

            if (first is null || !first.Succeeded)
            {
                return first is null
                    ? null
                    : first with { Source = source, OutputName = OutputNameFor(stem, attachment.FileName) };
            }

            // The provenance line is the whole reason attachments are worth converting at all: a
            // certified order is evidence, and *when it arrived and from whom* is half of what makes
            // it evidence. Prepended so it survives chunking with the first passage.
            return first with
            {
                Source = source,
                OutputName = OutputNameFor(stem, attachment.FileName),
                Markdown = Provenance(attachment.FileName, parent) + first.Markdown,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DocumentConversion(
                source, categoryCode, null, 0,
                $"The attachment could not be converted: {ex.Message}",
                OutputNameFor(stem, attachment.FileName));
        }
        finally
        {
            // Case data does not linger in the temp directory.
            var directory = Path.GetDirectoryName(temp)!;

            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // A leftover temp folder is not worth losing a converted attachment over.
            }
        }
    }

    private static string Provenance(string fileName, EmailContent parent) =>
        $"> Attached to \"{Blank(parent.Subject)}\", sent {Blank(parent.Date)}. "
        + $"Attachment file name: {fileName}.\n\n";

    /// <summary>
    /// What the attachment's Markdown is called.
    ///
    /// Prefixed with the message's own name so two messages carrying <c>statement.pdf</c> do not
    /// write over each other, and so a reader can see at a glance which message delivered it.
    /// </summary>
    internal static string OutputNameFor(string stem, string attachmentFileName) =>
        SafeFileName($"{stem}-{Path.GetFileNameWithoutExtension(attachmentFileName)}");

    /// <summary>Reads an RFC 822 message.</summary>
    internal static EmailContent ReadMimeMessage(string path)
    {
        var message = MimeMessage.Load(path);

        var attachments = new List<EmailAttachment>();
        var index = 0;

        // Every non-text part, not only those marked "attachment".
        //
        // A screenshot pasted into a message body is Content-Disposition: inline, and MimeKit's
        // Attachments collection excludes it by definition. One 12 MB email in Test Case 3 is
        // exactly that — a single inline JPEG of a State Pension forecast, which is the whole
        // evidential content of the message. Reading only the declared attachments converted it
        // "successfully" into 248 characters of boilerplate.
        foreach (var part in message.BodyParts.OfType<MimePart>())
        {
            index++;

            // A part can declare itself an attachment and carry nothing: a stub left by a mail
            // client that stripped the payload, which is worth skipping rather than crashing on.
            if (part.Content is null || part is TextPart)
            {
                continue;
            }

            using var buffer = new MemoryStream();
            part.Content.DecodeTo(buffer);

            attachments.Add(new EmailAttachment(NameFor(part, index), buffer.ToArray()));
        }

        var date = message.Date.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

        return new EmailContent(
            BuildHeader(
                message.Subject,
                message.From.ToString(),
                message.To.ToString(),
                message.Cc?.ToString(),
                date,
                attachments.Select(a => a.FileName)),
            message.Subject ?? "(no subject)",
            message.TextBody ?? StripHtml(message.HtmlBody),
            attachments,
            date);
    }

    /// <summary>
    /// Reads an Outlook message.
    ///
    /// Docling rejects <c>.msg</c> outright, and a compound-file format is not something to parse by
    /// hand. This is the one place a second document library earns its place.
    /// </summary>
    internal static EmailContent ReadOutlookMessage(string path)
    {
        using var message = new MsgReader.Outlook.Storage.Message(path);

        var attachments = new List<EmailAttachment>();

        foreach (var attachment in message.Attachments.OfType<MsgReader.Outlook.Storage.Attachment>())
        {
            if (attachment.Data is { Length: > 0 } data && !string.IsNullOrWhiteSpace(attachment.FileName))
            {
                attachments.Add(new EmailAttachment(SafeFileName(attachment.FileName), data));
            }
        }

        var recipients = string.Join(
            ", ",
            message.Recipients
                .Select(r => r.DisplayName ?? r.Email ?? string.Empty)
                .Where(r => !string.IsNullOrWhiteSpace(r)));

        var date = message.SentOn?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        return new EmailContent(
            BuildHeader(
                message.Subject,
                message.GetEmailSender(false, false),
                recipients,
                null,
                date,
                attachments.Select(a => a.FileName)),
            message.Subject ?? "(no subject)",
            message.BodyText ?? StripHtml(message.BodyHtml),
            attachments,
            date);
    }

    /// <summary>
    /// Renders the headers a reader — or an extractor — would want to quote.
    ///
    /// Labelled rather than raw, so a quote from it reads as evidence: "Date: 2026-03-30" is citable
    /// in a way a raw <c>Received:</c> chain is not.
    /// </summary>
    internal static string BuildHeader(
        string? subject,
        string? from,
        string? to,
        string? cc,
        string? date,
        IEnumerable<string> attachmentNames)
    {
        var header = new StringBuilder();

        header.Append("Subject: ").AppendLine(Blank(subject));
        header.Append("From: ").AppendLine(Blank(from));
        header.Append("To: ").AppendLine(Blank(to));

        if (!string.IsNullOrWhiteSpace(cc))
        {
            header.Append("Cc: ").AppendLine(cc);
        }

        header.Append("Date: ").AppendLine(Blank(date));

        var names = attachmentNames.ToArray();

        if (names.Length > 0)
        {
            header.Append("Attachments: ").AppendLine(string.Join("; ", names));
        }

        return header.ToString().TrimEnd();
    }

    private static string Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(not stated)" : value.Trim();

    /// <summary>Reduces an HTML body to its text, so a body-less plain part is not a blank email.</summary>
    internal static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = Regex.Replace(html, "<[^>]+>", " ", RegexOptions.None, TimeSpan.FromSeconds(2));

        text = WebUtility.HtmlDecode(text);

        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// The name a carried part should be saved under.
    ///
    /// An inline image usually has no file name — it is referenced by Content-Id from the HTML body
    /// — so one is derived from its media subtype. Without an extension the converter would reject
    /// it as unconvertible, which is how the content would be lost a second time.
    /// </summary>
    internal static string NameFor(MimePart part, int index)
    {
        var declared = part.FileName ?? part.ContentType.Name;

        if (!string.IsNullOrWhiteSpace(declared))
        {
            return SafeFileName(declared);
        }

        var extension = part.ContentType.MediaSubtype?.ToLowerInvariant() switch
        {
            "jpeg" or "jpg" => ".jpg",
            "png" => ".png",
            "tiff" => ".tiff",
            "bmp" => ".bmp",
            "webp" => ".webp",
            "gif" => ".gif",
            "pdf" => ".pdf",
            _ => null,
        };

        return extension is null ? $"part-{index}" : $"inline-{index}{extension}";
    }

    /// <summary>
    /// Strips anything from an attachment name that could escape the directory it is written to.
    ///
    /// The name comes from the message, which came from outside. A file called
    /// <c>..\..\startup\payload.exe</c> is a well-worn trick and costs one line to refuse.
    /// </summary>
    internal static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "attachment" : name;
    }
}
