namespace AiPromptEvaluator;

public static class DocumentCategory
{
    public const string Uncategorised = "(Uncategorised)";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Uncategorised,
        "Suitability Report",
        "Client Authority",
        "Know Your Client",
        "Meetings & Communications",
        "Risk, Capacity & Experience",
        "Existing Plans & Portfolios",
        "Needs Analysis",
        "Research",
        "Solution Design",
        "Recommendations & Advice",
    };

    /// <summary>
    /// The label for a case-folder sub-folder code, e.g. "B" → "Know Your Client".
    /// An unrecognised code is used as-is rather than hidden.
    /// </summary>
    public static string NameForCode(string code) => code switch
    {
        "A" => "Client Authority",
        "B" => "Know Your Client",
        "C" => "Meetings & Communications",
        "D" => "Risk, Capacity & Experience",
        "E" => "Existing Plans & Portfolios",
        "F" => "Needs Analysis",
        "G" => "Research",
        "H" => "Solution Design",
        "I" => "Recommendations & Advice",
        "" => Uncategorised,
        _ => code,
    };
}
