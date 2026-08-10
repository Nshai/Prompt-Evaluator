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
}
