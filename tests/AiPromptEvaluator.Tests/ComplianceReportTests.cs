using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The page a reviewer actually reads.
///
/// <b>Everything scored in this project has been scored from a rendered report</b>, so anything
/// the renderer drops is, for practical purposes, something the run never found. Two failures of
/// that kind are already on record: a passage retrieved and then evicted in ranking, which
/// appears in no prompt and so cannot be recovered from the prompt log either; and a requirement
/// that named a difference and passed, which read identically to one that found nothing.
///
/// So these tests are about what survives onto the page, not about markup. They assert on text a
/// reviewer would look for — a document name beside a quotation, a coverage band, a rule
/// reference — and they are deliberately loose about how it is wrapped.
/// </summary>
public class ComplianceReportTests
{
    // ── what the check was asking ─────────────────────────────────────────────

    /// <summary>
    /// The question and the rule are printed from the archive, not looked up. The check sheet is
    /// edited between runs, so rendering a month-old run against today's sheet would print this
    /// month's question above last month's answer — and the answer would look wrong rather than
    /// the question having moved.
    /// </summary>
    [Fact]
    public void TheCheckNarrativeAndItsRegulatoryBasisArePrinted()
    {
        var html = ComplianceReportHtml.Render(Run());

        Assert.Contains("What this check asks", html, StringComparison.Ordinal);
        Assert.Contains("reconcile with the evidence on file", html, StringComparison.Ordinal);
        Assert.Contains("Regulatory basis", html, StringComparison.Ordinal);
        Assert.Contains("COBS 9.2.1R", html, StringComparison.Ordinal);
        Assert.Contains("PRIN 2.1.1R", html, StringComparison.Ordinal);
    }

    /// <summary>One rule per chip, however the sheet's author separated them.</summary>
    [Fact]
    public void EachCitedRuleIsItsOwnChip()
    {
        var html = ComplianceReportHtml.Render(Run());

        Assert.Contains("<span class=\"ruleRef\">COBS 9.2.1R</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"ruleRef\">PRIN 2.1.1R</span>", html, StringComparison.Ordinal);
    }

    /// <summary>A run archived before the sheet was carried prints nothing, not an empty frame.</summary>
    [Fact]
    public void ARunArchivedWithoutTheSheetPrintsNoBriefAtAll()
    {
        var run = Run();
        var stripped = run with
        {
            Checks =
            [
                run.Checks[0] with
                {
                    Narrative = string.Empty,
                    RegulatoryBasis = string.Empty,
                    AppliesTo = string.Empty,
                },
            ],
        };

        var html = ComplianceReportHtml.Render(stripped);

        Assert.DoesNotContain("What this check asks", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"brief\"", html, StringComparison.Ordinal);
    }

    // ── the evidence behind the facts ─────────────────────────────────────────

    /// <summary>
    /// The question every reviewer asked of every finding: which file did that come from. It
    /// used to be two panes away, under the verdict, where a citation reads as support for a
    /// conclusion rather than as a checkable claim about a document.
    /// </summary>
    [Fact]
    public void EveryCitationIsPrintedWithTheDocumentItCameFrom()
    {
        var html = ComplianceReportHtml.Render(Run());

        Assert.Contains("Evidence cited for these facts", html, StringComparison.Ordinal);
        // The pound sign leaves the renderer as a numeric entity, so the assertion stops short
        // of it rather than encoding expectations about the escaper into every test.
        Assert.Contains("Basic Income | Employed | Monthly", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Fact Find.md</strong>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCanonicalPathsTheCitationsSupportAreNamedBesideThem()
    {
        var html = ComplianceReportHtml.Render(Run());

        Assert.Contains("/financialPosition/income", html, StringComparison.Ordinal);
    }

    /// <summary>A quotation the verifier could not find is marked where it is read, not only in a tally.</summary>
    [Fact]
    public void AQuotationThatTracesToNothingIsMarkedInTheEvidenceTrail()
    {
        var run = Run(unverified: ["Basic Income | Employed | Monthly | £ 1,200.00"]);

        var html = ComplianceReportHtml.Render(run);

        Assert.Contains("not traced to the pack", html, StringComparison.Ordinal);
    }

    // ── the two coverages ─────────────────────────────────────────────────────

    [Fact]
    public void BothCoveragesArePrintedForEveryRequirement()
    {
        var html = ComplianceReportHtml.Render(Run());

        Assert.Contains("Agent coverage", html, StringComparison.Ordinal);
        Assert.Contains("Evidence coverage", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The breakdown, not just the number. A requirement that reached nothing because the case
    /// file holds nothing has found something; one that reached nothing because ranking evicted
    /// it has found nothing and does not know — and only the list tells them apart.
    /// </summary>
    [Fact]
    public void TheEvidenceBreakdownSaysWhatWasEvictedRatherThanOnlyScoringIt()
    {
        var html = ComplianceReportHtml.Render(Run());

        Assert.Contains("evicted in ranking", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AFindingWithNothingWrongWithItsWorkingSaysSo()
    {
        var html = ComplianceReportHtml.Render(Run());

        Assert.Contains("traced back to the pack", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A search that came back empty leaves no passage, so counting the passages would report it
    /// as a search that never ran — and a requirement whose every search found nothing would read
    /// exactly like one that searched for nothing at all.
    /// </summary>
    [Fact]
    public void ASearchThatFoundNothingIsStillListedAmongTheSearches()
    {
        var html = ComplianceReportHtml.Render(Run());

        Assert.Contains("monthly outgoings", html, StringComparison.Ordinal);
        Assert.Contains("3 semantic search(es)", html, StringComparison.Ordinal);
    }

    // ── categorisation ────────────────────────────────────────────────────────

    [Fact]
    public void TheKindOfProblemAndTheCloserLookFlagsAreBothOnThePage()
    {
        var html = ComplianceReportHtml.Render(Run());

        Assert.Contains(IssueCategory.DataInconsistency, html, StringComparison.Ordinal);
        Assert.Contains(CloserLook.PartialCoverageLabel, html, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFilterRailOffersEveryCategoryTheRunActuallyRaised()
    {
        var html = ComplianceReportHtml.Render(Run());

        Assert.Contains("cat:" + IssueCategory.DataInconsistency, html, StringComparison.Ordinal);
    }

    // ── the page itself ───────────────────────────────────────────────────────

    /// <summary>
    /// The report is opened from a file, often on a machine with no route to the internet and
    /// always after the run it describes. <b>Anything the page has to fetch to say what the run
    /// found is a pane that renders empty exactly when someone needs it</b> — so no content, no
    /// script and no data may come from anywhere but the file itself.
    ///
    /// The one exception is the typeface, which is requested from Google Fonts and falls back to
    /// a system stack when it cannot be reached. A page in the wrong font is still readable; a
    /// page missing its findings is not.
    /// </summary>
    [Fact]
    public void ThePageFetchesNothingItNeedsInOrderToBeRead()
    {
        var html = ComplianceReportHtml.Render(Run());

        Assert.DoesNotContain("<script src", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fetch(", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("XMLHttpRequest", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);

        foreach (var host in Hosts(html))
        {
            Assert.Contains("fonts.g", host, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Every external host the rendered page names.</summary>
    private static IEnumerable<string> Hosts(string html) =>
        System.Text.RegularExpressions.Regex
            .Matches(html, @"https?://([^/""'\s]+)")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every string on this page came from a document or from a model reading one. A quotation
    /// containing markup has to read as that quotation, not run as markup.
    /// </summary>
    [Fact]
    public void TextTakenFromTheCaseIsEscapedRatherThanRendered()
    {
        var run = Run(quote: "<script>alert('x')</script>");

        var html = ComplianceReportHtml.Render(run);

        Assert.DoesNotContain("<script>alert", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// One class, one meaning.
    ///
    /// <b>The page carries its whole stylesheet inline, so a name reused for a second purpose
    /// silently restyles the first.</b> That is not hypothetical: <c>.rule</c> was the thin
    /// divider in the run header, and adding regulation chips under the same name turned every
    /// divider into an empty pill — visible only by reading the rendered HTML, because both
    /// elements still had the markup they were supposed to have.
    ///
    /// Selectors inside the print media query are excluded: overriding a screen rule for print
    /// is the whole point of that block.
    /// </summary>
    [Fact]
    public void NoStyleRuleQuietlyRedefinesAnotherOnesClass()
    {
        var html = ComplianceReportHtml.Render(Run());

        var screenOnly = html[..html.IndexOf("@media print", StringComparison.Ordinal)];

        var duplicates = System.Text.RegularExpressions.Regex
            .Matches(screenOnly, @"(?m)^\s*\.([A-Za-z][\w-]*)\{")
            .Select(m => m.Groups[1].Value)
            .GroupBy(c => c, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(
            duplicates.Count == 0,
            "These classes are styled twice, so one definition is silently overriding the other: "
            + string.Join(", ", duplicates));
    }

    [Fact]
    public void TheRunItDescribesIsIdentifiedOnThePage()
    {
        var html = ComplianceReportHtml.Render(Run());

        Assert.Contains("ABC-99", html, StringComparison.Ordinal);
        Assert.Contains("RUN-1", html, StringComparison.Ordinal);
        Assert.Contains("claude-haiku", html, StringComparison.Ordinal);
    }

    /// <summary>A replayed run is worth knowing about before its findings are read.</summary>
    [Fact]
    public void TheCacheModeTheRunUsedIsStated()
    {
        var html = ComplianceReportHtml.Render(Run());

        Assert.Contains("response cache bypassed", html, StringComparison.OrdinalIgnoreCase);
    }

    // ── fixtures ──────────────────────────────────────────────────────────────

    private static CheckRunRecord Run(
        IReadOnlyList<string>? unverified = null,
        string quote = "Basic Income | Employed | Monthly | £ 1,200.00")
    {
        var started = DateTimeOffset.Parse("2026-08-26T10:00:00+01:00");

        var finding = new GroupFinding
        {
            GroupId = "G1.4",
            Requirement = "Income and expenditure figures are consistent with the evidence",
            ReportSays = "Client earns £300 per week net.",
            FileSays = "The fact find records net basic monthly income of £1,200.",
            Discrepancies = ["£300/week does not reconcile with £1,200/month."],
            Analysis = "A material mismatch on an income figure.",
            Outcome = nameof(CheckOutcome.PotentialConcern),
            Severity = "High",
            IssueCategories = [IssueCategory.DataInconsistency],
            Citations = [new FindingCitation { PassageId = "P1", Source = "Fact Find.md", Quote = quote }],
            UnverifiedQuotes = unverified ?? [],
        };

        var group = new RecordedGroup(
            CheckId: "CHK-001",
            GroupId: "G1.4",
            Requirement: finding.Requirement!,
            Facts:
            [
                new RecordedFact("/financialPosition/income", "\"£300 per week\"", true),
            ],
            Queries:
            [
                new RecordedQuery("Q1.4.1", "income and expenditure", "B", 1),
                new RecordedQuery("Q1.4.2", "household budget", "G", 1),
                new RecordedQuery("Q1.4.3", "monthly outgoings", "B", 0),
            ],
            Passages:
            [
                new RecordedPassage(
                    "Q1.4.1", "income and expenditure", "B", "Fact Find.md", "B", "Fact Find",
                    0.81, quote, InPack: true, Ordinal: 0),
                new RecordedPassage(
                    "Q1.4.2", "household budget", "G", "Research.md", "G", "Research",
                    0.42, "General planning assumptions", InPack: false, Ordinal: 1),
            ],
            SystemPrompt: "You are a financial services Quality Assurance assessor.",
            UserPrompt: "[G1.4] Income and expenditure...",
            RawResponse: "{}",
            Finding: finding)
        {
            IssueCategories = [IssueCategory.DataInconsistency],
            CloserLook = new CloserLook(LowConfidence: false, PartialCoverage: true, UnreadableSource: false),
        };

        return new CheckRunRecord(
            RunId: "RUN-1",
            CaseReference: "ABC-99",
            TenantId: 99,
            ChatModel: "claude-haiku",
            StartedAt: started,
            CompletedAt: started.AddMinutes(5),
            Fingerprint: "Chat claude-haiku · seed pinned",
            CacheMode: "response cache bypassed (abc)",
            CanonicalModelJson: """{"modelVersion":"1.0"}""",
            CanonicalModelId: "8a0120d90ea2",
            SchemaVersion: "1.0",
            SourceDocuments: ["Suitability Report.md"],
            Checks:
            [
                new RecordedCheck(
                    CheckId: "CHK-001",
                    CheckName: "Suitability completeness against client information",
                    Outcome: nameof(CheckOutcome.PotentialConcern),
                    Summary: "One material discrepancy.",
                    SearchesRun: 3,
                    PassagesRetrieved: 2,
                    CanonicalPathsResolved: 1,
                    CanonicalPathsMissing: 0,
                    Elapsed: TimeSpan.FromSeconds(12),
                    Usage: new TokenUsage(1000, 200, 0, 50),
                    Error: null,
                    Groups: [group])
                {
                    Narrative = "Confirm the figures in the suitability report reconcile with "
                                + "the evidence on file.",
                    RegulatoryBasis = "COBS 9.2.1R\nPRIN 2.1.1R",
                    AppliesTo = "All advised sales.",
                },
            ]);
    }
}
