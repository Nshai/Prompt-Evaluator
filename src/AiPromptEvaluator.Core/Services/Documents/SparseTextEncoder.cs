using System.Globalization;
using System.Text.RegularExpressions;

namespace AiPromptEvaluator;

/// <summary>One sparse vector: term ids and the weight each carries.</summary>
public sealed record SparseText(IReadOnlyList<uint> Indices, IReadOnlyList<float> Weights)
{
    public static readonly SparseText Empty = new([], []);

    public int Count => Indices.Count;
}

/// <summary>
/// Turns text into a sparse lexical vector, so retrieval can match a literal as well as a meaning.
///
/// <b>What this is for.</b> Many findings turn on an exact token — <c>48.06</c>, <c>33.4%</c>,
/// <c>£9,839.36</c>, <c>17.68</c> against <c>13.21</c>, <c>xInternal transfer</c> — and a dense
/// embedding is weakest precisely there: a general-purpose 1536-d model puts two different
/// four-figure sums in almost the same place. The measured symptom is not that those passages are
/// missing from the index. It is that they reach the wrong group: <c>33.4</c> reached exactly one
/// pack, <c>G1.1</c>, and no query in the two checks that needed it retrieves it. <b>Sparse
/// retrieval here is a routing fix, not a recall fix.</b>
///
/// <b>What this is not.</b> It is not BM25, and calling it that would be a lie with consequences.
/// BM25 needs corpus statistics — an inverse document frequency per term — and those are not
/// available at query time without persisting a term table beside the collection and keeping it in
/// step with every re-index. What is here instead is term-frequency saturation and length
/// normalisation (BM25's other two halves) with a <b>shape-based weight</b> standing in for IDF: a
/// token carrying digits is rarer and more discriminating than an English word, in this corpus,
/// reliably. That is an approximation, it is stated rather than hidden, and it is aimed squarely at
/// the tokens the analysis says the findings turn on.
///
/// If sparse retrieval proves its worth, real IDF is the obvious next increment — computed over the
/// case at index time and stored with the collection.
/// </summary>
public static class SparseTextEncoder
{
    /// <summary>BM25's term-frequency saturation. Above this, repetition stops adding weight.</summary>
    private const double Saturation = 1.2;

    /// <summary>BM25's length normalisation. 0 ignores length, 1 divides it out entirely.</summary>
    private const double LengthNormalisation = 0.75;

    /// <summary>
    /// The average passage length weights are normalised against.
    ///
    /// Fixed rather than measured, because the chunker already targets a token budget — passages in
    /// this index are the same size by construction, so a corpus pass would compute a number the
    /// configuration already implies.
    /// </summary>
    private const double AverageLength = 400d;

    /// <summary>
    /// A token is a run of letters, or a number that keeps the shape it was written in.
    ///
    /// <b>The number pattern is the whole point.</b> Splitting on punctuation would cut
    /// <c>9,839.36</c> into <c>9</c>, <c>839</c> and <c>36</c> — three tokens that match half the
    /// corpus and identify nothing, while the figure that identifies the passage disappears. The
    /// currency symbol and percent sign are dropped so a query saying <c>£9,839.36</c> matches a
    /// document saying <c>9,839.36</c>; the digits are what carry the meaning.
    /// </summary>
    private static readonly Regex Tokens = new(
        @"\d[\d,]*(?:\.\d+)?|[A-Za-z]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Words too common to route anything. Deliberately short: this is a stop list, not a
    /// vocabulary, and every domain word it might remove — "charge", "pension", "transfer" — is one
    /// a plan legitimately searches on.
    /// </summary>
    private static readonly IReadOnlySet<string> Ignored = new HashSet<string>(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "been", "but", "by", "for", "from", "had", "has",
        "have", "he", "her", "his", "i", "if", "in", "is", "it", "its", "not", "of", "on", "or",
        "she", "that", "the", "their", "there", "they", "this", "to", "was", "were", "will", "with",
        "you", "your",
    };

    /// <summary>
    /// The sparse vector for a piece of text, or <see cref="SparseText.Empty"/> where it carries
    /// nothing worth indexing.
    /// </summary>
    public static SparseText Encode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return SparseText.Empty;
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var length = 0;

        foreach (Match match in Tokens.Matches(text))
        {
            var token = Normalise(match.Value);

            if (token.Length == 0 || Ignored.Contains(token))
            {
                continue;
            }

            length++;
            counts[token] = counts.TryGetValue(token, out var seen) ? seen + 1 : 1;
        }

        if (counts.Count == 0)
        {
            return SparseText.Empty;
        }

        var normaliser = Saturation
            * (1 - LengthNormalisation + (LengthNormalisation * length / AverageLength));

        // Ordered by term id so the same text always produces the same vector in the same order —
        // a passage's stored vector has to be reproducible across re-indexes, and a finding has to
        // be repeatable.
        var terms = counts
            .Select(pair => (Id: TermId(pair.Key), Weight: Weigh(pair.Key, pair.Value, normaliser)))
            .Where(t => t.Weight > 0f)
            .GroupBy(t => t.Id)
            .OrderBy(g => g.Key)
            .ToList();

        return new SparseText(
            [.. terms.Select(g => g.Key)],

            // A hash collision would otherwise silently drop one of two real terms; taking the
            // larger keeps the more discriminating of them rather than whichever came first.
            [.. terms.Select(g => g.Max(t => t.Weight))]);
    }

    /// <summary>
    /// One term's weight: saturating term frequency, times what the token's shape says about how
    /// much it can discriminate.
    /// </summary>
    private static float Weigh(string token, int frequency, double normaliser)
    {
        var tf = frequency * (Saturation + 1) / (frequency + normaliser);

        return (float)(tf * Discrimination(token));
    }

    /// <summary>
    /// The stand-in for inverse document frequency.
    ///
    /// <b>Ordered by how reliably a token identifies one passage in this corpus.</b> A decimal
    /// figure is the strongest signal there is — <c>48.06</c> appears in the few passages that are
    /// about it and nowhere else — while an ordinary English word appears everywhere. A bare
    /// integer sits between: <c>60000</c> is the annual allowance and identifies a passage, while
    /// <c>3</c> is a table row number.
    /// </summary>
    internal static double Discrimination(string token)
    {
        var digits = token.Any(char.IsAsciiDigit);

        if (!digits)
        {
            // A long word is rarer than a short one, and this is the only signal available without
            // corpus statistics. Bounded so it can never outweigh a figure.
            return token.Length >= 8 ? 1.4d : 1d;
        }

        if (token.Contains('.', StringComparison.Ordinal))
        {
            return 4d;
        }

        // Short digit runs are row numbers, ages and counts; long ones are amounts and identifiers.
        return token.Length >= 4 ? 2.5d : 1.2d;
    }

    /// <summary>
    /// Lower-cased, with thousands separators removed so <c>9,839.36</c> and <c>9839.36</c> are one
    /// term. Reports write a figure both ways in the same document.
    /// </summary>
    private static string Normalise(string token) =>
        token.Contains(',', StringComparison.Ordinal)
            ? token.Replace(",", string.Empty, StringComparison.Ordinal).ToLowerInvariant()
            : token.ToLowerInvariant();

    /// <summary>
    /// A stable id for a term.
    ///
    /// <b>Deliberately not <see cref="string.GetHashCode()"/>.</b> That is seeded per process, so
    /// the same passage would index under different term ids on every launch and a query would stop
    /// matching documents indexed by an earlier run — the hardest kind of failure to notice,
    /// because everything visible about both runs is identical. FNV-1a is a fixed function of the
    /// bytes and always will be.
    /// </summary>
    internal static uint TermId(string term)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;

        var hash = offset;

        foreach (var c in term)
        {
            hash = (hash ^ c) * prime;
        }

        return hash;
    }

    /// <summary>The terms a text produces, for tests and for explaining a match to a reader.</summary>
    internal static IReadOnlyList<string> Terms(string? text) =>
        text is null
            ? []
            : Tokens.Matches(text)
                .Select(m => Normalise(m.Value))
                .Where(t => t.Length > 0 && !Ignored.Contains(t))
                .Distinct(StringComparer.Ordinal)
                .ToList();

    /// <summary>How the encoder describes itself in a run report.</summary>
    public static string Description =>
        $"lexical sparse (TF saturation k1={Saturation.ToString(CultureInfo.InvariantCulture)}, "
        + $"b={LengthNormalisation.ToString(CultureInfo.InvariantCulture)}, shape-weighted terms)";
}
