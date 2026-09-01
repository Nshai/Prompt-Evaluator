using System.IO;

using AiPromptEvaluator;

using Microsoft.Extensions.DataIngestion;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// Move 7's chunking half: <b>never split a table.</b>
///
/// The semantic chunker cuts on similarity and a token budget and knows nothing about tables, so a
/// charges table, a fund allocation or a projection schedule is cut wherever the budget runs out.
/// What reaches the assessor is a header row with no figures, or figures with no header row —
/// neither of which answers the question that was asked of it. The findings that turn on reading
/// one row against its column heading are the ones the benchmark counts.
/// </summary>
public class TableAwareChunkerTests
{
    /// <summary>
    /// A chunker that cuts every element into its own chunk. Stands in for the semantic chunker
    /// so the test asserts what the wrapper protects rather than how similarity happens to land.
    /// </summary>
    private sealed class SplitEverything : IngestionChunker<string>
    {
        public async IAsyncEnumerable<IngestionChunk<string>> Emit(IngestionDocument document)
        {
            await Task.CompletedTask;

            foreach (var element in TableAwareChunker.Flatten(document))
            {
                var text = element.GetMarkdown();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return new IngestionChunk<string>(text, document, null);
                }
            }
        }

        public override IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
            IngestionDocument document, CancellationToken cancellationToken = default) =>
            Emit(document);
    }

    private static async Task<IReadOnlyList<IngestionChunk<string>>> Chunk(string markdown)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, markdown);

        try
        {
            var document = await CaseDocumentIndexer.ReadDocumentAsync(new MarkdownReader(), path);
            var chunker = new TableAwareChunker(new SplitEverything());
            var chunks = new List<IngestionChunk<string>>();

            await foreach (var chunk in chunker.ProcessAsync(document))
            {
                chunks.Add(chunk);
            }

            return chunks;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private const string ChargesTable = """
        # Aviva Pension

        Your plan carries the charges set out below.

        ## Ongoing charges

        | Fund | Annual charge | Transaction costs |
        | --- | --- | --- |
        | Aviva Multi-Asset Plus II | 0.19% | 0.04% |
        | Vanguard LifeStrategy 60 | 0.21% | 0.02% |

        These charges are deducted monthly.
        """;

    /// <summary>
    /// <b>The property everything else rests on.</b> One chunk holds the whole table — every row
    /// with its own header — so a figure can still be read against the column it sits under.
    /// </summary>
    [Fact]
    public async Task ATableArrivesWholeInOneChunk()
    {
        var chunks = await Chunk(ChargesTable);

        var table = Assert.Single(chunks, c => c.Content.Contains("0.19%", StringComparison.Ordinal));

        Assert.Contains("0.21%", table.Content);
        Assert.Contains("Annual charge", table.Content);
        Assert.Contains("Transaction costs", table.Content);
    }

    /// <summary>
    /// A table without its caption is a grid of numbers nobody can name. The nearest heading above
    /// it is carried as the chunk's context, which is where the pipeline already puts the words a
    /// passage is introduced by.
    /// </summary>
    [Fact]
    public async Task ATableCarriesTheHeadingAboveIt()
    {
        var chunks = await Chunk(ChargesTable);
        var table = Assert.Single(chunks, c => c.Content.Contains("0.19%", StringComparison.Ordinal));

        Assert.Equal("Ongoing charges", table.Context);
    }

    /// <summary>
    /// Prose is still handed to the chunker that was tuned for it — the wrapper takes tables out of
    /// its way and changes nothing else.
    /// </summary>
    [Fact]
    public async Task ProseIsStillChunkedByTheInnerChunker()
    {
        var chunks = await Chunk(ChargesTable);

        Assert.Contains(chunks, c => c.Content.Contains("charges set out below", StringComparison.Ordinal));
        Assert.Contains(chunks, c => c.Content.Contains("deducted monthly", StringComparison.Ordinal));

        // And the table is not repeated inside the prose stream.
        Assert.Single(chunks, c => c.Content.Contains("0.19%", StringComparison.Ordinal));
    }

    /// <summary>
    /// Several tables under different headings each keep their own, rather than all inheriting the
    /// first or the last.
    /// </summary>
    [Fact]
    public async Task EachTableKeepsItsOwnHeading()
    {
        var chunks = await Chunk("""
            ## Existing plan

            | Fund | Charge |
            | --- | --- |
            | Nest Higher Risk | 0.30% |

            ## Recommended plan

            | Fund | Charge |
            | --- | --- |
            | True Potential Balanced | 0.77% |
            """);

        Assert.Equal(
            "Existing plan",
            Assert.Single(chunks, c => c.Content.Contains("0.30%", StringComparison.Ordinal)).Context);

        Assert.Equal(
            "Recommended plan",
            Assert.Single(chunks, c => c.Content.Contains("0.77%", StringComparison.Ordinal)).Context);
    }

    /// <summary>
    /// A document with no tables passes through untouched, so switching the wrapper on cannot
    /// change how an all-prose document is indexed.
    /// </summary>
    [Fact]
    public async Task ADocumentWithNoTablesIsUnchanged()
    {
        var prose = "# Notes\n\nThe client is a tenant and pays rent of £900 per month.";

        var wrapped = await Chunk(prose);
        var direct = new List<IngestionChunk<string>>();

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, prose);

        try
        {
            var document = await CaseDocumentIndexer.ReadDocumentAsync(new MarkdownReader(), path);

            await foreach (var chunk in new SplitEverything().ProcessAsync(document))
            {
                direct.Add(chunk);
            }
        }
        finally
        {
            File.Delete(path);
        }

        Assert.Equal(direct.Select(c => c.Content), wrapped.Select(c => c.Content));
    }
}
