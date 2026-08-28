using System.IO;

using AiPromptEvaluator;

using Xunit;

namespace AiPromptEvaluator.Tests;

/// <summary>
/// The plans have to ask for the evidence they say they need.
///
/// Six of the eight benchmark findings missed by the run scored in
/// docs/test-results/Runtime-Logs/latest sat behind a document category no query asked for.
/// The plans declared those categories in <c>expectedCategories.evidence</c> and then did not
/// search them, and nothing read the field — so the defect was invisible in the data for as
/// long as the plans existed.
/// </summary>
public class CheckPlanLintTests
{
    // ──────────────────────────────────────────────
    // The rules, on synthetic plans
    // ──────────────────────────────────────────────

    [Fact]
    public void AGroupThatQueriesWhatItDeclaresIsClean()
    {
        var plan = Plan(
            primary: ["B", "F"],
            Group("G1.1", declares: ["B", "F"], queries: ["B", "F", "I"]));

        Assert.Empty(CheckPlanLint.Inspect(plan));
    }

    /// <summary>
    /// G5.2 as it shipped. Its requirement is "Financial situation plus cashflow supports the
    /// recommendation" — the group that has to find the Fact Find's £-288 disposable income —
    /// and it declared category B while querying only F and I.
    /// </summary>
    [Fact]
    public void AGroupThatDeclaresACategoryItNeverQueriesIsCaught()
    {
        var plan = Plan(
            primary: ["B", "F"],
            Group("G5.2", declares: ["B", "F"], queries: ["F", "I"]));

        var violation = Assert.Single(CheckPlanLint.Inspect(plan));

        Assert.Equal("L1", violation.Rule);
        Assert.Equal("G5.2", violation.GroupId);
        Assert.Contains("declares evidence category B", violation.Detail);
    }

    /// <summary>
    /// CHK-007, CHK-008 and CHK-009 all named B as primary while no group declared it, so the
    /// group-level rule sees nothing wrong. The check's own scope and its groups' scope have
    /// to be reconciled separately.
    /// </summary>
    [Fact]
    public void APrimaryCategoryNoGroupDeclaresIsCaught()
    {
        var plan = Plan(
            primary: ["B", "G"],
            Group("G7.1", declares: ["G"], queries: ["G", "I"]));

        var violation = Assert.Single(CheckPlanLint.Inspect(plan));

        Assert.Equal("L2", violation.Rule);
        Assert.Contains("names B as a primary category", violation.Detail);
    }

    /// <summary>
    /// Internal-consistency groups compare the report against itself and have no evidence side.
    /// Demanding a document search from them would be demanding they retrieve something they
    /// have correctly decided does not exist.
    /// </summary>
    [Fact]
    public void AModelOnlyGroupIsNotAskedToRetrieveAnything()
    {
        var plan = Plan(
            primary: [],
            new PlanQueryGroup
            {
                GroupId = "G1.8",
                Declares = new PlanDeclares { EvidenceCategories = ["I"] },
                Retrieval = new PlanRetrieval
                {
                    Queries =
                    [
                        new PlannedQuery { Id = "Q1", Side = "Assertion", TargetCategories = ["I"] },
                    ],
                },
            });

        Assert.Empty(CheckPlanLint.Inspect(plan));
    }

    /// <summary>
    /// The report cannot corroborate itself, and a group whose evidence side is category I and
    /// nothing else has no evidence side — it reaches a verdict about the report from the report.
    ///
    /// Run 17's G1.11 is the observed case: "the inflation assumptions … are corroborated by [P11]
    /// and [P12]", where both are the suitability report. The group's conclusion reads exactly like
    /// one drawn from a supporting document, and nothing in the output distinguishes them.
    /// </summary>
    [Fact]
    public void AGroupWhoseOnlyEvidenceIsTheReportIsCaught()
    {
        var plan = Plan(
            primary: ["I"],
            Group("G1.11", declares: ["I"], queries: ["I"]));

        var violation = Assert.Single(CheckPlanLint.Inspect(plan));

        Assert.Equal("L5", violation.Rule);
        Assert.Contains("sole support for a claim the report makes", violation.Detail);
    }

    /// <summary>
    /// The exemption, and why it is keyed on the requirement text. Two shipped groups read only
    /// their own document on purpose — CHK-001's data mismatches "within the report itself" and
    /// CHK-008's prominence — and for those the report genuinely is the whole comparison. Keying
    /// the exemption on the wording rather than a flag means a group has to declare the intent
    /// where a reader of the plan meets it, and cannot keep it by leaving a boolean set after the
    /// requirement has changed underneath it.
    /// </summary>
    [Theory]
    [InlineData("Data mismatches and unsupported figures within the report itself")]
    [InlineData("Disadvantages and risks are fairly and prominently presented")]
    public void AGroupThatSaysItIsCheckingTheReportAgainstItselfIsAllowedTo(string requirement)
    {
        var group = Group("G1.8", declares: ["I"], queries: ["I"]);

        var plan = Plan(primary: ["I"], group with { Requirement = requirement });

        Assert.Empty(CheckPlanLint.Inspect(plan));
    }

    /// <summary>
    /// [I] alongside a supporting category is not the failure. Several requirements need a table
    /// from the report to set against a claim made elsewhere in it, and that is a comparison with
    /// two sides.
    /// </summary>
    [Fact]
    public void TheReportAmongOtherEvidenceCategoriesIsFine()
    {
        var plan = Plan(
            primary: ["B", "I"],
            Group("G7.4", declares: ["B", "I"], queries: ["B", "I"]));

        Assert.Empty(CheckPlanLint.Inspect(plan));
    }

    /// <summary>
    /// Nothing in a plan may belong to one case only.
    ///
    /// <b>The plans were written against one file and drifted toward it.</b> A provider in an
    /// expectSignals list, a risk-profiler vendor, one client's sentence as a section hint — each
    /// works on that case and is mistuned to the next, because the pipeline validates whichever
    /// suitability report it is given against whichever evidence came with it.
    ///
    /// In expectSignals it is worse than mistuned. A signal that does not appear makes the runner
    /// tell the assessor the data point is "absent from the case file rather than merely
    /// unretrieved", so a value carried over from another case manufactures a false absence.
    /// </summary>
    [Theory]
    [InlineData("Standard Life")]
    [InlineData("Defaqto")]
    [InlineData("Sullivan")]
    public void APlanNamingSomethingFromOneCaseIsCaught(string fromOneCase)
    {
        var group = Group("G7.4", declares: ["E"], queries: ["E"]);

        var plan = Plan(
            primary: ["E"],
            group with
            {
                Retrieval = group.Retrieval! with { EvidenceSections = [fromOneCase] },
            });

        var violation = Assert.Single(CheckPlanLint.Inspect(plan));

        Assert.Equal("L6", violation.Rule);
        Assert.Contains("belongs to one case rather than to the domain", violation.Detail);
    }

    /// <summary>
    /// An expectSignals value, which is the field where this does real damage.
    /// </summary>
    [Fact]
    public void AnExpectSignalNamingOneCasesProviderIsCaught()
    {
        var plan = Plan(
            primary: ["E"],
            new PlanQueryGroup
            {
                GroupId = "G9.1",
                Requirement = "Comparison of existing versus recommended",
                Declares = new PlanDeclares { EvidenceCategories = ["E"] },
                Retrieval = new PlanRetrieval
                {
                    Queries =
                    [
                        new PlannedQuery
                        {
                            Id = "Q9.1.1",
                            Side = "Evidence",
                            TargetCategories = ["E"],
                            ExpectSignals = ["annual charge", "Zurich"],
                        },
                    ],
                },
            });

        var violation = Assert.Single(CheckPlanLint.Inspect(plan));

        Assert.Equal("L6", violation.Rule);
        Assert.Contains("Zurich", violation.Detail);
    }

    /// <summary>
    /// What must stay allowed, or the rule costs more than it saves. Statutory amounts are UK
    /// constants; "main residence" and "a fact find" are domain vocabulary; a research tool's column
    /// heading and a profiler's fixed option label appear in every case that uses that tool. All five
    /// are load-bearing for benchmark findings.
    /// </summary>
    [Theory]
    [InlineData("£60,000")]
    [InlineData("main residence")]
    [InlineData("ranked by Maturity Value")]
    [InlineData("No understanding / knowledge")]
    [InlineData("Total Monthly Disposable Income")]
    public void DomainVocabularyAndToolWordingAreNotCaught(string generic)
    {
        var group = Group("G1.1", declares: ["B"], queries: ["B"]);

        var plan = Plan(
            primary: ["B"],
            group with
            {
                Retrieval = group.Retrieval! with { EvidenceSections = [generic] },
            });

        Assert.Empty(CheckPlanLint.Inspect(plan));
    }

    [Fact]
    public void APlanWithNoQueryGroupsIsCaught()
    {
        var violation = Assert.Single(CheckPlanLint.Inspect(Plan(primary: ["B"])));

        Assert.Equal("L0", violation.Rule);
        Assert.Contains("assessed nothing", violation.Detail);
    }

    // ──────────────────────────────────────────────
    // The rules, on the plans that actually ship
    // ──────────────────────────────────────────────

    /// <summary>
    /// The regression that matters. Every shipped plan must satisfy L1 and L2 — this is what
    /// stops a future plan edit silently reintroducing a category the check declares and never
    /// reads.
    /// </summary>
    [Fact]
    public void EveryShippedPlanSatisfiesTheLint()
    {
        var (plans, failures) = CheckQueryPlanLoader.Load(PlanFolder);

        Assert.Empty(failures);
        Assert.Equal(10, plans.Count);

        var violations = CheckPlanLint.Inspect(plans.Values);

        Assert.True(
            violations.Count == 0,
            "The shipped query plans declare evidence they never search:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations.Select(v => "  " + v)));
    }

    /// <summary>
    /// Every shipped requirement steers the assessor towards the kinds of problem it tends to
    /// raise, and every value is one of the nine.
    ///
    /// <b>A misspelt category is worse than none.</b> The whole value of a closed vocabulary is
    /// that a reviewer filtering to "every disclosure shortfall in this case" gets all of them;
    /// one plan spelling it "Disclosure shortfalls" produces findings that quietly answer no
    /// filter at all. Lint rule L4 refuses the value rather than passing it through, and this is
    /// the regression that keeps the shipped set clean.
    /// </summary>
    [Fact]
    public void EveryShippedRequirementCarriesASteerFromTheVocabulary()
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);

        var unsteered = plans.Values
            .SelectMany(p => p.QueryGroups.Select(g => (p.CheckId, g)))
            .Where(x => x.g.SteeredIssueCategories.Count == 0)
            .Select(x => x.CheckId.Trim() + " " + x.g.GroupId)
            .ToList();

        Assert.True(
            unsteered.Count == 0,
            "These requirements name no issue category, so their findings reach the report "
            + "uncategorised:" + Environment.NewLine + string.Join(Environment.NewLine, unsteered));
    }

    /// <summary>
    /// The steer is a steer and not a menu, so it may not close the vocabulary down to one
    /// answer either. A requirement told to expect exactly one kind of problem is a requirement
    /// whose finding was decided in the plan.
    /// </summary>
    [Fact]
    public void NoShippedRequirementIsSteeredToASingleAnswer()
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);

        foreach (var group in plans.Values.SelectMany(p => p.QueryGroups))
        {
            Assert.InRange(group.SteeredIssueCategories.Count, 2, IssueCategory.All.Count);
        }
    }

    /// <summary>
    /// The plan set's shape, pinned. A dropped query group is otherwise silent: the check runs,
    /// reports success, and assesses one requirement fewer than the catalogue says it has.
    /// The stale build output that prompted this had CHK-001 at eight groups against nine.
    /// Re-pinned when the plans were rebuilt against "Revised checks.csv": 60 groups to 85, and
    /// again at 87 when the fact library was reconciled against the plans and two published facts
    /// turned out to have no requirement reading them at all.
    ///
    /// 88 adds CHK-003's G3.11, for the funds inside a plan the advice retains. F3.4 — the People's
    /// Pension kept at risk 9 against an agreed 5 — was missed by six consecutive runs, and the
    /// last of them showed why no guard could reach it: nothing asked. The word "retained" appeared
    /// once in the whole of CHK-003, inside a guard on G3.7, whose requirement is the recommended
    /// strategy rather than the money left where it was.
    /// </summary>
    [Fact]
    public void TheShippedPlanSetHasTheGroupsTheCatalogueExpects()
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);

        var counts = plans.Values.ToDictionary(
            p => p.CheckId.Trim(),
            p => p.QueryGroups.Count,
            StringComparer.OrdinalIgnoreCase);

        Assert.Equal(11, counts["CHK-001"]);
        Assert.Equal(8, counts["CHK-002"]);
        Assert.Equal(11, counts["CHK-003"]);
        Assert.Equal(7, counts["CHK-004"]);
        Assert.Equal(8, counts["CHK-005"]);
        Assert.Equal(9, counts["CHK-006"]);
        Assert.Equal(11, counts["CHK-007"]);
        Assert.Equal(8, counts["CHK-008"]);
        Assert.Equal(10, counts["CHK-009"]);
        Assert.Equal(5, counts["CHK-010"]);

        Assert.Equal(88, counts.Values.Sum());
    }

    /// <summary>
    /// L3 is the class no rule can catch, and CHK-010 was the observed case: the vulnerability
    /// overlay never reached category D, where the client's recorded "No understanding /
    /// knowledge" answers live. Pinned by name because the fix is a judgement about where
    /// evidence lives, and a later edit could undo it without breaking anything else.
    /// </summary>
    [Fact]
    public void TheVulnerabilityOverlayReachesTheRiskProfileReport()
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);
        var overlay = plans["CHK-010"];

        Assert.Contains("D", overlay.PrimaryCategories);
        Assert.Contains(overlay.QueryGroups, g => g.QueriedCategories.Contains("D"));
    }

    /// <summary>
    /// The three checks that reached the Fact Find in zero groups out of nineteen.
    /// </summary>
    [Theory]
    [InlineData("CHK-005")]
    [InlineData("CHK-007")]
    [InlineData("CHK-008")]
    [InlineData("CHK-009")]
    public void TheChecksThatNeedTheFactFindNowQueryIt(string checkId)
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);

        Assert.Contains(plans[checkId].QueryGroups, g => g.QueriedCategories.Contains("B"));
    }

    // ── L4: the vocabularies the schema no longer enforces ────────────────────

    /// <summary>
    /// The exact bug this rule exists for. "Supporting" is not "Supplementary", so IsCore
    /// returns true and the query runs even under CoreQueriesOnly — a value that is wrong and
    /// changes behaviour without failing anything. It was found in CHK-007 and CHK-008.
    /// </summary>
    [Fact]
    public void APriorityOutsideTheVocabularyIsCaught()
    {
        var group = Group("G1", ["B"], ["B"]);
        group.Retrieval.Queries[0] = group.Retrieval.Queries[0] with { Priority = "Supporting" };

        var violations = CheckPlanLint.Inspect(Plan(["B"], group));

        var l4 = Assert.Single(violations, v => v.Rule == "L4");
        Assert.Contains("priority", l4.Detail);
        Assert.Contains("Supporting", l4.Detail);
    }

    [Fact]
    public void ASideOutsideTheVocabularyIsCaught()
    {
        var group = Group("G1", ["B"], ["B"]);
        group.Retrieval.Queries[0] = group.Retrieval.Queries[0] with { Side = "Assertions" };

        Assert.Contains(
            CheckPlanLint.Inspect(Plan(["B"], group)),
            v => v.Rule == "L4" && v.Detail.Contains("side"));
    }

    /// <summary>
    /// A category code outside A–I reaches the Qdrant filter and matches nothing, so the group
    /// retrieves less than it declared and no error is raised anywhere.
    /// </summary>
    [Fact]
    public void ACategoryCodeOutsideTheTaxonomyIsCaught()
    {
        var violations = CheckPlanLint.Inspect(Plan(["B"], Group("G1", ["B"], ["Z"])));

        Assert.Contains(
            violations,
            v => v.Rule == "L4" && v.Detail.Contains("targetCategories") && v.Detail.Contains("Z"));
    }

    [Fact]
    public void AValidPlanRaisesNoVocabularyViolation()
    {
        var violations = CheckPlanLint.Inspect(Plan(["B"], Group("G1", ["B"], ["B"])));

        Assert.DoesNotContain(violations, v => v.Rule == "L4");
    }

    /// <summary>
    /// An absent optional field takes its default and is not a violation; only a value that is
    /// present and wrong is.
    /// </summary>
    [Fact]
    public void AnAbsentOptionalFieldIsNotAViolation()
    {
        var group = Group("G1", ["B"], ["B"]);
        group = group with { Verification = new PlanVerification { Limb = string.Empty } };

        Assert.DoesNotContain(CheckPlanLint.Inspect(Plan(["B"], group)), v => v.Rule == "L4");
    }

    /// <summary>
    /// The shipped plans must be clean, since the schema no longer refuses these values.
    /// </summary>
    [Fact]
    public void TheShippedPlansUseOnlyDocumentedVocabulary()
    {
        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);

        var violations = CheckPlanLint.Inspect(plans.Values).Where(v => v.Rule == "L4").ToList();

        Assert.True(
            violations.Count == 0,
            "Vocabulary violations: " + string.Join("; ", violations));
    }

    /// <summary>
    /// L7, on the shipped plans: every value an applicability rule excludes from its schema
    /// vocabulary is listed here, or the build fails.
    ///
    /// <b>This is an approval test, not a correctness test.</b> Excluding a value is usually
    /// right — an advice action of "Retain" genuinely does not trigger a switch check. What was
    /// wrong was that nobody could see the exclusions, so one made by accident looked exactly
    /// like one made on purpose, and the check it silenced reported as clear having searched
    /// nothing.
    ///
    /// Measured: a rule accepted five of the ten values documented for <c>adviceAction</c>. The
    /// sixth, <c>RetainAndSwitchFunds</c>, is what two separate extractions of the same case
    /// recorded — correctly — and six material findings were lost to its omission across two
    /// runs on two different models. Adding it to the plan fixes that case. This test is what
    /// makes the next omission fail here rather than in a run analysis.
    ///
    /// To change the shipped set: change the plan, run this, and paste the reported line in.
    /// Re-approving is one edit; noticing without this test took two runs and a benchmark.
    /// </summary>
    [Fact]
    public void EveryApplicabilityExclusionInTheShippedPlansHasBeenApproved()
    {
        var approved = new[]
        {
            // CHK-002's applicability narrows an objectives check to investment and pension
            // goals. A case whose only recorded objective is protection, a mortgage or estate
            // planning still has objectives to align, so this exclusion is doubtful — but it no
            // longer decides anything on its own: the rule is ANDed with isAdvisedCase, and
            // CheckPlanRunner now runs a check whose trigger field affirmatively says it applies
            // even when a rule disagrees. Recorded here rather than widened, because which
            // objectives CHK-002 covers is a question about the check catalogue, not about lint.
            "CHK-002 goalTypes/objectiveType excludes Protection, Mortgage, EstatePlanning, "
            + "Budget, EquityRelease, Other",

            // The same rule accepts two values the schema does not document. They are covering
            // for an extraction that was observed writing "RetirementObjective" where the
            // vocabulary says "Pension"; CanonicalVocabulary now corrects that class
            // mechanically, so these are a belt beside a brace rather than dead weight.
            "CHK-002 goalTypes/objectiveType accepts undocumented Retirement, RetirementObjective",

            // CHK-009 assesses a replacement or switch. A plan retained unchanged, stopped,
            // amended in place, or carrying no advice at all is not one.
            "CHK-009 adviceActions/adviceAction excludes Retain, Stop, Amend, NoAdviceGiven",
        };

        var (plans, _) = CheckQueryPlanLoader.Load(PlanFolder);

        var vocabularies = CanonicalVocabulary.Parse(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "canonical-suitability-model.schema.json")));

        var reported = CheckPlanLint.Inspect(plans.Values, vocabularies)
            .Where(v => v.Rule == "L7")
            .Select(Summarise)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        var unapproved = reported.Except(approved, StringComparer.Ordinal).ToList();

        Assert.True(
            unapproved.Count == 0,
            "An applicability rule excludes values nobody has approved. Confirm each exclusion "
            + "is deliberate — a value omitted by accident settles its check before any search "
            + "runs — then add the line to `approved`:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, unapproved.Select(u => "  \"" + u + "\",")));

        // The other direction: an approval left behind after the plan stopped excluding
        // anything is a comment claiming a decision that is no longer being made.
        Assert.Empty(approved.Except(reported, StringComparer.Ordinal));
    }

    /// <summary>
    /// One L7 violation reduced to the assertion being approved, without the advice that
    /// follows it. Every L7 message opens with that clause and separates it with an em dash.
    /// </summary>
    private static string Summarise(CheckPlanLint.Violation violation)
    {
        var end = violation.Detail.IndexOf(" — ", StringComparison.Ordinal);

        return violation.CheckId
            + " "
            + (end < 0 ? violation.Detail : violation.Detail[..end]);
    }

    // ──────────────────────────────────────────────

    private static string PlanFolder =>
        Path.Combine(AppContext.BaseDirectory, "check-plan");

    private static CheckQueryPlan Plan(string[] primary, params PlanQueryGroup[] groups) =>
        new()
        {
            CheckId = "CHK-999",
            CheckName = "Test",
            PrimaryCategories = [.. primary],
            QueryGroups = [.. groups],
        };

    private static PlanQueryGroup Group(string id, string[] declares, string[] queries) =>
        new()
        {
            GroupId = id,
            Declares = new PlanDeclares { EvidenceCategories = [.. declares] },
            Retrieval = new PlanRetrieval
            {
                Queries =
                [
                    new PlannedQuery { Id = "Q1", Side = "Evidence", TargetCategories = [.. queries] },
                ],
            },
        };
}
