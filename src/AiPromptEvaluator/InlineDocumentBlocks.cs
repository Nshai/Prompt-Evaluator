using System.IO;
using System.Text;

using Anthropic.Models.Messages;

namespace AiPromptEvaluator;

/// <summary>
/// Turns case documents into inline content blocks — base64 for PDFs and images, plain text
/// otherwise — rather than Files API <c>file_id</c> references.
///
/// Inline blocks are what the Messages API itself defines, so they work unchanged through a
/// proxy (LiteLLM) and on Bedrock, neither of which exposes Anthropic's Files API. It also
/// removes the upload round-trip and the server-side lifetime of an uploaded file: what the
/// model sees is exactly the bytes on disk at the moment of the request.
///
/// The cost is that the same bytes travel with every request, which is what
/// <see cref="InlineContentCache"/> (encode once) and prompt caching (bill once) address.
/// </summary>
public static class InlineDocumentBlocks
{
    /// <summary>
    /// Builds the "Supporting Documents" blocks for a run, in category order, from documents
    /// already prepared by a case-folder load. Spreadsheets that could not be converted are
    /// left out entirely — their bytes carry no meaning as text.
    ///
    /// A document that can't be read is reported in place rather than failing the run: one
    /// unreadable file shouldn't cost the check every other document it needs.
    /// </summary>
    /// <param name="markForPromptCaching">
    /// True to mark the last document block as a cache breakpoint. Off for token counting,
    /// where there is nothing to cache and the marker only muddies the estimate.
    /// </param>
    public static List<ContentBlockParam> Build(
        IEnumerable<CaseDocument> documents,
        InlineContentCache cache,
        bool markForPromptCaching = true)
    {
        var sendable = documents.Where(d => d.IsSendable).ToList();
        var blocks = new List<ContentBlockParam>();

        if (sendable.Count == 0)
        {
            blocks.Add(new TextBlockParam { Text = "No documents are available for this check." });
            return blocks;
        }

        blocks.Add(new TextBlockParam { Text = "### Supporting Documents" });

        var byCategory = sendable
            .GroupBy(d => d.CategoryCode)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byCategory)
        {
            blocks.Add(new TextBlockParam
            {
                Text = $"#### [{group.Key}] {CaseDocumentLibrary.ResolveCategoryName(group.Key)}",
            });

            foreach (var doc in group.OrderBy(d => d.FileName, StringComparer.OrdinalIgnoreCase))
            {
                blocks.Add(BlockFor(doc, cache));
            }
        }

        if (markForPromptCaching)
        {
            MarkLastForCaching(blocks);
        }

        return blocks;
    }

    /// <summary>
    /// One content block per document: a base64 PDF or image, the converted Markdown or text
    /// as a plain-text document, or a note in place of a file that could not be prepared.
    /// </summary>
    public static ContentBlockParam BlockFor(CaseDocument doc, InlineContentCache cache)
    {
        // A Word document we failed to convert would be binary noise as inline text.
        if (doc.Error is not null && doc.PreparedPath is null)
        {
            return new TextBlockParam
            {
                Text = $"--- File: {doc.FileName} (could not be converted: {doc.Error}) ---",
            };
        }

        var path = doc.BilledPath;
        var title = $"[{doc.CategoryCode}] {doc.FileName}";

        try
        {
            if (doc.Kind == AnthropicFileKind.Image)
            {
                return new ImageBlockParam
                {
                    Source = new Base64ImageSource
                    {
                        Data = cache.Get(path, InlineEncoding.Base64).Data,
                        MediaType = ImageMediaTypeFor(path),
                    },
                };
            }

            if (IsPdf(path))
            {
                return new DocumentBlockParam
                {
                    Source = new Base64PdfSource { Data = cache.Get(path, InlineEncoding.Base64).Data },
                    Title = title,
                    Context = doc.CategoryName,
                };
            }

            if (doc.Kind == AnthropicFileKind.Document)
            {
                return new DocumentBlockParam
                {
                    Source = new PlainTextSource { Data = cache.Get(path, InlineEncoding.Text).Data },
                    Title = title,
                    Context = doc.CategoryName,
                };
            }

            // Formats the document block doesn't accept (.zip, ...) still carry something
            // useful often enough to be worth a bounded text preview.
            return new TextBlockParam { Text = BuildTextPreview(doc, cache) };
        }
        catch (Exception ex)
        {
            return new TextBlockParam
            {
                Text = $"--- File: {doc.FileName} (could not be read: {ex.Message.Trim()}) ---",
            };
        }
    }

    /// <summary>
    /// Marks the end of the document prefix for prompt caching, so a second check attaching
    /// the same documents reads them from cache at a tenth of the input price instead of
    /// paying full freight again.
    ///
    /// This is why the per-check text goes *after* the documents in a request: caching is a
    /// prefix match, so a varying block ahead of the documents would invalidate all of them.
    /// </summary>
    public static void MarkLastForCaching(List<ContentBlockParam> blocks)
    {
        if (blocks.Count == 0)
        {
            return;
        }

        var last = blocks[^1];
        var ephemeral = new CacheControlEphemeral();

        blocks[^1] = last.Value switch
        {
            DocumentBlockParam document => document with { CacheControl = ephemeral },
            ImageBlockParam image => image with { CacheControl = ephemeral },
            TextBlockParam text => text with { CacheControl = ephemeral },
            // Any other block type carries no cache_control we can set; leaving the prefix
            // unmarked costs a cache miss, not a failed request.
            _ => last,
        };
    }

    private static bool IsPdf(string path) =>
        Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private static MediaType ImageMediaTypeFor(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => MediaType.ImagePng,
            ".gif" => MediaType.ImageGif,
            ".webp" => MediaType.ImageWebP,
            _ => MediaType.ImageJpeg,
        };

    private const int TextPreviewCharacters = 2000;

    private static string BuildTextPreview(CaseDocument doc, InlineContentCache cache)
    {
        var content = cache.Get(doc.BilledPath, InlineEncoding.Text).Data;
        var preview = content.Length > TextPreviewCharacters ? content[..TextPreviewCharacters] : content;

        return new StringBuilder()
            .AppendLine($"--- File: {doc.FileName} ---")
            .AppendLine(preview.Replace("\r\n", "\n"))
            .ToString();
    }
}
