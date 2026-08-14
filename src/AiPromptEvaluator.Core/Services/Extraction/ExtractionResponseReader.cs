using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiPromptEvaluator;

/// <summary>
/// Reads an extraction pass's reply, including the replies that did not finish.
///
/// A section that overruns the output cap stops mid-token: the JSON is valid up to the cut and
/// unparseable after it. The pipeline used to report that as "the model did not return a JSON
/// object", which sent whoever read it looking for a prompt-compliance problem — the one thing it
/// was not. In an observed run the recommendations pass was cut off 87% of the way through with
/// four of five recommendations already complete, and all four were discarded.
///
/// So this distinguishes the two failures, and recovers what a truncated reply did finish.
/// </summary>
public static class ExtractionResponseReader
{
    /// <summary>
    /// The reply as a JSON object, tolerating the two things a chat model does even when told
    /// not to: wrapping the object in a markdown fence, and adding a sentence around it.
    /// </summary>
    public static JsonObject? ParseObject(string response)
    {
        var text = Unfence(response);

        if (!text.StartsWith('{'))
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return null;
            }

            text = text[start..(end + 1)];
        }

        try
        {
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether the reply looks cut off rather than malformed.
    ///
    /// Two signals, either of which is enough: a fence that opens and never closes, and a
    /// bracket depth that never returns to zero. Both were true of the observed failure and
    /// neither was being looked at.
    /// </summary>
    public static bool LooksTruncated(string response)
    {
        var trimmed = response.TrimEnd();

        if (trimmed.StartsWith("```", StringComparison.Ordinal) &&
            !trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            return true;
        }

        var text = Unfence(response);
        if (!text.StartsWith('{'))
        {
            return false;
        }

        return Scan(text).Depth > 0;
    }

    /// <summary>
    /// The longest valid prefix of a truncated reply, closed off — or null when nothing whole
    /// survived.
    ///
    /// Extraction sections are an object holding one or more arrays, so the useful unit to
    /// recover is a completed array element: four whole recommendations are worth four
    /// recommendations, where the fifth being half-written costs nothing but itself.
    ///
    /// The caller is expected to record the shortfall rather than merge this as though it were
    /// complete. A partial section presented as whole would turn a loud failure into a quiet
    /// one, which is the trade this pipeline exists to avoid.
    /// </summary>
    public static JsonObject? Salvage(string response)
    {
        var text = Unfence(response);
        if (!text.StartsWith('{'))
        {
            return null;
        }

        var scan = Scan(text);
        if (scan.LastElementEnd is not { } cut)
        {
            return null;
        }

        // Close whatever was still open at the cut, innermost first.
        var closers = string.Concat(
            Enumerable.Reverse(scan.OpenAtLastElementEnd).Select(c => c == '{' ? '}' : ']'));

        try
        {
            return JsonNode.Parse(text[..(cut + 1)] + closers) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>How many whole elements of <paramref name="property"/> survived a truncation.</summary>
    public static int SalvagedCount(JsonObject? salvaged, string property) =>
        salvaged?[property] is JsonArray array ? array.Count : 0;

    private static string Unfence(string response)
    {
        var text = response.Trim();

        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstNewline = text.IndexOf('\n');
        if (firstNewline >= 0)
        {
            text = text[(firstNewline + 1)..];
        }

        var fenceEnd = text.LastIndexOf("```", StringComparison.Ordinal);
        if (fenceEnd >= 0)
        {
            text = text[..fenceEnd];
        }

        return text.Trim();
    }

    /// <summary>
    /// Walks the text once, tracking bracket nesting outside of string literals, and records
    /// where the last array element at depth two closed — that being the last point the reply
    /// can be cut without losing a whole entity.
    /// </summary>
    private static ScanResult Scan(string text)
    {
        var stack = new List<char>();
        var openAtLastElementEnd = new List<char>();
        int? lastElementEnd = null;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            switch (c)
            {
                case '{' or '[':
                    stack.Add(c);
                    break;

                case '}' or ']':
                    if (stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }

                    // Depth 2 means: inside the root object, inside one of its arrays. What
                    // just closed is therefore a whole element of a section's array.
                    if (stack.Count == 2)
                    {
                        lastElementEnd = i;
                        openAtLastElementEnd = [.. stack];
                    }

                    break;
            }
        }

        return new ScanResult(stack.Count, lastElementEnd, openAtLastElementEnd);
    }

    /// <param name="Depth">Brackets still open at the end — above zero means truncated.</param>
    /// <param name="LastElementEnd">Index of the last whole array element's closing bracket.</param>
    /// <param name="OpenAtLastElementEnd">Brackets open at that point, outermost first.</param>
    private sealed record ScanResult(int Depth, int? LastElementEnd, List<char> OpenAtLastElementEnd);
}
