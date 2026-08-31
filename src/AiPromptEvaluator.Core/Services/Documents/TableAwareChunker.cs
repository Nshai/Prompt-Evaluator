using System.Runtime.CompilerServices;

using Microsoft.Extensions.DataIngestion;

namespace AiPromptEvaluator;

/// <summary>
/// Chunks a document without ever cutting a table in half.
///
/// <b>The failure this fixes.</b> The semantic chunker splits on similarity and a token budget, and
/// it knows nothing about tables — so a charges table, a fund allocation or a projection schedule is
/// cut wherever the budget runs out. What reaches the assessor is a header row with no figures, or
/// figures with no header row, and neither can answer the question that was asked of it. Findings
/// that turn on reading one row against its column heading — <c>0.19</c> against <c>0.21</c>,
/// <c>45.91%</c> against <c>48.06%</c>, a ticked box in a certified form — are the ones most exposed
/// to it, and they are exactly the findings the benchmark counts.
///
/// <b>What this does instead.</b> A table becomes one chunk, whole, carrying the heading that
/// introduces it. Everything between tables is handed to the semantic chunker unchanged, so prose
/// keeps the cut points that were tuned for it and only tables are treated specially.
///
/// <b>A table over the token budget is still emitted whole.</b> That is a deliberate choice against
/// the budget: half a table is not half an answer, it is a misleading one, and a long table that
/// costs a few hundred extra tokens is cheaper than the finding it would otherwise lose. The budget
/// governs how prose is divided, not whether a table may exist.
/// </summary>
public sealed class TableAwareChunker : IngestionChunker<string>
{
    private readonly IngestionChunker<string> _prose;

    /// <param name="prose">
    /// The chunker everything that is not a table is handed to — in the pipeline, the semantic
    /// similarity chunker driven by the same embedding generator the search uses.
    /// </param>
    public TableAwareChunker(IngestionChunker<string> prose) => _prose = prose;

    /// <summary>
    /// The chunker everything that is not a table is handed to.
    ///
    /// Exposed so the strategy stays testable through the wrapper: a regression from semantic
    /// cutting to a fixed-width splitter would index and search just as happily and show up only
    /// as worse retrieval, which is precisely the failure the test guarding it exists to catch.
    /// </summary>
    public IngestionChunker<string> Prose => _prose;

    public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
        IngestionDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Walked in document order so a table keeps the heading that introduces it: a caption
        // sits before its table, and a table without one is a grid of numbers nobody can name.
        foreach (var (section, tables) in Partition(document))
        {
            if (section is not null)
            {
                await foreach (var chunk in _prose
                    .ProcessAsync(section, cancellationToken)
                    .ConfigureAwait(false))
                {
                    yield return chunk;
                }
            }

            foreach (var (table, caption) in tables)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var markdown = table.GetMarkdown();

                if (string.IsNullOrWhiteSpace(markdown))
                {
                    continue;
                }

                yield return new IngestionChunk<string>(markdown, document, caption);
            }
        }
    }

    /// <summary>
    /// Splits a document into runs of prose and the tables that follow them.
    ///
    /// The prose run is rebuilt as its own document so the semantic chunker sees exactly what it
    /// would have seen, minus the tables — its cut points are unchanged for the text it still owns.
    /// </summary>
    private static IEnumerable<(IngestionDocument? Prose, List<(IngestionDocumentTable Table, string? Caption)> Tables)>
        Partition(IngestionDocument document)
    {
        var prose = new List<IngestionDocumentElement>();
        var tables = new List<(IngestionDocumentTable, string?)>();
        var heading = (string?)null;

        foreach (var element in Flatten(document))
        {
            switch (element)
            {
                case IngestionDocumentTable table:
                    tables.Add((table, heading));
                    break;

                case IngestionDocumentHeader header:
                    // The nearest heading above a table is its caption. Kept as the chunk's
                    // context so a table arrives named rather than as a bare grid.
                    heading = Trim(header.Text) ?? heading;
                    prose.Add(element);
                    break;

                default:
                    prose.Add(element);
                    break;
            }
        }

        yield return (prose.Count > 0 ? Rebuild(document, prose) : null, tables);
    }

    /// <summary>Every element of a document, sections walked through rather than around.</summary>
    internal static IEnumerable<IngestionDocumentElement> Flatten(IngestionDocument document) =>
        document.Sections.SelectMany(Flatten);

    private static IEnumerable<IngestionDocumentElement> Flatten(IngestionDocumentElement element)
    {
        if (element is not IngestionDocumentSection section)
        {
            yield return element;
            yield break;
        }

        foreach (var child in section.Elements.SelectMany(Flatten))
        {
            yield return child;
        }
    }

    private static IngestionDocument Rebuild(
        IngestionDocument original, IReadOnlyList<IngestionDocumentElement> elements)
    {
        var rebuilt = new IngestionDocument(original.Identifier);
        var section = new IngestionDocumentSection();

        foreach (var element in elements)
        {
            section.Elements.Add(element);
        }

        rebuilt.Sections.Add(section);

        return rebuilt;
    }

    private static string? Trim(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
