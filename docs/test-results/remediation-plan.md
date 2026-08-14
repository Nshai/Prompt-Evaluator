# Remediation plan — assessment pipeline

Acts on the defects in the
[Run 1 analysis](Runtime-Logs/latest/Run-1%20%28Before%20changes%20applied%29/run-analysis.md) and
the open gaps in [gap-analysis.md](gap-analysis.md), and is re-scored against the
[Run 2 analysis](Runtime-Logs/latest/Run-2/run-analysis.md). Companion to
[Runtime-Logs/extraction/improvement-plan.md](Runtime-Logs/extraction/improvement-plan.md), which
closed the extraction side (E1–E6); everything here is retrieval, assessment and reporting.

Ordered so each stage ships and is measured on its own, and so that **the stages that make
measurement trustworthy come before the stages that need measuring**.

---

> ## Build status — implemented and measured 2026-08-14
>
> **Measured on Run 2: benchmark recall 44% → 67% (16 → 24 of 36), all ten checks now
> matching their expected outcome, five exit criteria met and four missed.** The two
> causes behind the misses are now understood precisely and are
> [Stage 5](#stage-5--what-run-2-says-to-do-next). Full scoring in the
> [Run 2 analysis](Runtime-Logs/latest/Run-2/run-analysis.md).
>
> **Stages 0 to 4 are in the build except items 4.3 and 4.5, and the extraction half of 4.4,
> which are deliberately not — see [What was not done](#what-was-not-done-and-why).**
> Build clean, **326 tests passing** (was 263).
>
> | Stage | Item | Status |
> | --- | --- | --- |
> | 0.1 | Replay guard | **done** — `RunAuthenticity`, banner + rate on every report |
> | 0.2 | Plan integrity | **done, premise corrected** — the "nine disagreeing copies" were stale build output, not duplication; the real guard is a pinned plan-set shape |
> | 0.3 | Citation confidence | **done, scope reduced** — a *pass* on an untraceable quote is still downgraded; see below |
> | 1.1 | Plan lint L1/L2 | **done** — `CheckPlanLint`, enforced against the shipped plans |
> | 1.2 | Repair the groups | **done** — 15 L1 violations fixed, 38 edits |
> | 1.3 | Reconcile check scope | **done** — B added to CHK-007/008/009, D added to CHK-010 |
> | 1.4 | Overlay trigger | **done** — `ContinuesWithReducedScope` reaches prompt and summary |
> | 2.1 | `comparisonPerformed` veto | **done** — a group that named discrepancies keeps its verdict |
> | 2.2 | Extraction report truncation | **done** — 4,000 → 24,000 chars |
> | 2.3 | Requirement authority | **done** — plan wins; divergence counted instead |
> | 3.1 | Unescaped fragments | **done** — `UnsafeRelaxedJsonEscaping` |
> | 3.2 | Table punctuation | **done** — pipes and separator rows folded |
> | 3.3 | Elisions | **done; near-miss rule rejected as unsafe** — see below |
> | 4.1 | Derived figures | **done** — `DerivedFigures` |
> | 4.2 | Cross-group contradictions | **done** — `CrossGroupContradictions`, report addendum |
> | 4.4 | Repeated-key detection | **half done** — charge disagreement detected in `DerivedFigures`; the extraction-side feed is not built |
> | 4.3 | Verbatim claim beside enum | **not done** — extraction-schema change |
> | 4.5 | Relevance floor | **not done** — needs calibration against a generated run |
>
> ### Three things the plan got wrong, found by building it
>
> **The near-miss citation rule would have re-opened the hole it was meant to widen.** §3.3
> proposed accepting a quote when a contiguous run covering ~90% of it appears in the evidence,
> on the measurement that 39 of 105 failures looked like that. But the altered quotation
> `CitationVerifier` exists to catch — evidence "a Risk rating of **6**", finding "a Risk rating
> of **5**" — shares a **96% contiguous run** with its source. Any threshold loose enough to
> admit the near-misses admits that. Verified by measurement, then dropped; only explicit
> elision is accepted, and there is a test pinning the altered quotation as a failure.
>
> **Removing the citation downgrade regressed the same defence.** §0.3 said to keep the model's
> outcome and report confidence separately. Implemented literally, the altered quotation cleared
> its check again. The rule now moves only *passes* — a No Issue resting on an untraceable quote
> is still a Potential Concern — while everything else keeps its verdict and reports confidence
> beside it. The "0 no issue" problem the item was written for is fixed by the matcher, not by
> removing the safety.
>
> **The "nine disagreeing plan copies" were one source and eight build outputs.**
> `docs/artifacts/check-plan` was already the single source, wired through the `.csproj`, and
> `bin/` is gitignored; four copies were merely stale and a build refreshed them. The residual
> risk was real but different — nothing asserted the loaded plan set matched the catalogue — so
> that is what was built. **My own §4.1 analysis had parsed the stale copy and understated the
> defect: 17 groups, not 3.**
>
> ### And one the run itself got wrong
>
> Writing the test for the arrangement totals, the five plans sum to **£116,998.47**. The
> extraction reported **£116,997.47** and the assessment repeated it. The finding was right and
> the arithmetic behind it was out by a pound — which is the argument for 4.1 in one line.

---

## The shape of the fix

Nineteen defects, but they are not nineteen independent problems. They fall into four groups, and
the order matters more than the individual items:

1. **Three of them make every other measurement unreliable** — cache replays that look like runs,
   plan files that disagree with each other, and a citation flag that silently rewrites verdicts.
   Until these are closed, no recall number can be trusted, including the ones in this plan's own
   exit criteria.
2. **The largest single recall lever is a data fix, not an algorithm.** Six of eight missed
   findings sit behind a document category no query asks for. This is a plan edit plus two lint
   rules — no model call, no retrieval tuning.
3. **Three defects destroy work the pipeline already did.** A `comparisonPerformed` veto discards
   11 High-severity concerns; a 4,000-character truncation drops 55% of the extraction report; a
   `Requirement` field lets the model redefine what it was asked. All are small, and all return
   findings the run already reached.
4. **Only the last stage needs new capability** — arithmetic in code, cross-group composition, and
   a relevance floor with somewhere to land.

**Deliberately not doing prompt-tuning first.** Two of the four largest defects cannot be fixed by
prompting at all, and the run's own reasoning quality is not the bottleneck: the assessor found the
Zurich ranking, the £12,000 reduction, the risk-6 file note and the balanced-investor precondition
unaided. It is being let down by what it is shown and by what happens to its answers afterwards.

---

## Contents

- [Stage 0 — Make measurement trustworthy](#stage-0--make-measurement-trustworthy) — R0, P0, A2c
- [Stage 1 — Fix the query plans](#stage-1--fix-the-query-plans) — R2, L1–L3
- [Stage 2 — Stop discarding work already done](#stage-2--stop-discarding-work-already-done) — A3, E4a, N3
- [Stage 3 — Make citation verification discriminate](#stage-3--make-citation-verification-discriminate) — A2
- [Stage 4 — New capability](#stage-4--new-capability) — A4, N1, N2, E4b, R1
- [Stage 5 — What Run 2 says to do next](#stage-5--what-run-2-says-to-do-next) — **R3**, N1 repair, A4 repair, A2
- [Exit criteria](#exit-criteria)
- [Scoring rubric](scoring-rubric.md) — how a run is adjudicated, and why it needed writing down
- [What this plan does not do](#what-this-plan-does-not-do)

---

## Stage 0 — Make measurement trustworthy

Nothing else in this plan can be verified until these three land. Total effort is small; the value
is that every later stage becomes measurable.

### 0.1 — Refuse to present a cache replay as a run *(new — R0)*

**Defect.** The run stamped `2026-08-14 11:40:31` produced 660,284 tokens in **four seconds** and
is byte-identical to `160653` from the previous day. It carried a fresh date and a `£0.9775` cost
line. Eight of twelve-plus logs on record are replays, and two prior analyses drew conclusions from
them.

**Fix.** In `CheckPlanRunner`, compare elapsed wall clock against output tokens. Below a floor —
1,000 tok/s is far above any real generation rate and far below the observed 165,000 — mark the run
`Replayed`. Then:

- `FindingsReport.Format()` stamps `*** REPLAY — NOT A GENERATED RUN ***` in the header block,
  beside the fingerprint.
- Suppress the cost line, or label it `cached`.
- `PromptLogWriter` records the per-group elapsed times it already has, so the check is auditable
  after the fact.

**Test.** A run whose recorded elapsed time is implausible for its token count is flagged; a normal
run is not. Assert on the two real logs: `160653` (122 s) passes, `20260814_114031` (4 s) is
flagged.

**Effort.** Small. **Blocks:** every exit criterion in this document.

### 0.2 — One authoritative copy of the query plans *(new — P0)*

**Defect.** Nine copies of `check-plan/` exist in the tree and they disagree.
`docs/artifacts/check-plan/CHK-001` has 9 query groups; `src/.../bin/Debug/.../check-plan/CHK-001`
has 8 — G1.9 is missing. Debug outputs for the app, the installer and the test project are all
stale; Release copies are current. **A run from the Debug build silently assesses eight of nine
requirements and reports success.**

**Fix.** Nominate `docs/artifacts/check-plan/` as the single source, reference it from the
`.csproj` as a `Content`/`CopyToOutputDirectory` item, and delete the checked-in duplicates. Add a
startup assertion: the loaded plan set's hash is logged in the run fingerprint (it already is —
`plans 10@21fa2f6e50b5`), and `AssessmentCheckLoader` fails loudly if a plan file is absent or a
check has zero query groups.

**Test.** Plan count and per-check group counts are asserted against the catalogue CSV, so a
dropped group fails the build rather than a run.

**Effort.** Small. **Note:** this defect corrupted my own first analysis of Stage 1 — worth fixing
before anyone else parses a plan file.

### 0.3 — Stop citation verification rewriting the verdict *(A2c)*

**Defect.** [`GroupFinding.ParsedOutcome`](../../src/AiPromptEvaluator/CheckFinding.cs#L100-L103)
converts any finding with an unverified quote to `PotentialConcern`. At a 36% quote-failure rate
this converted **all nine** `NoIssue` groups, which is why the run summary reads *"0 no issue"*.
A reader of `output-latest.txt` cannot distinguish an assessed concern from a formatting artefact.

**Fix.** Keep the model's outcome. Add `CitationConfidence { Verified, PartiallyVerified,
Unverified }` derived from `UnverifiedQuotes`, render it beside the outcome, and sort unverified
findings first in the report as the footer already promises. The deterministic downgrade was the
right instinct — A1's altered quotation is exactly what it was built for — but it belongs on a
confidence axis, not on the verdict.

**Sequencing.** Ship this **with Stage 3**, not before: at today's 36% failure rate, removing the
downgrade without fixing the false positives would let genuinely unsupported findings read as
clean. Listed here because it is the same defect.

**Test.** A finding with an unverified quote keeps its outcome and reports `Unverified`; the
headline counts stop depending on citation formatting.

**Effort.** Small.

---

## Stage 1 — Fix the query plans

**The single largest recall lever in the plan.** Six of the eight missed benchmark findings sit
behind a document category no query asks for. This is not tunable: `Rank()` re-orders candidates,
it cannot promote a passage `CaseDocumentSearchTool` was never asked for — which is why raising the
search limit 8 → 16 bought two passages.

### 1.1 — Two lint rules over the plans *(L1, L2)*

Both are pure functions over the plan JSON, both run at load and in CI, and between them they would
have caught 17 of the 20 category defects **before any model call**.

| | Invariant | Violations today |
| --- | --- | --- |
| **L1** | Every group's `expectedCategories.evidence` ⊆ union of its queries' `targetCategories` | **17 of 60 groups** |
| **L2** | Every check's `primaryCategories` ⊆ union of its groups' declared evidence categories | **3 checks** — B in CHK-007, CHK-008, CHK-009 |

L1's violations, from `docs/artifacts/check-plan`:

```
G1.1 declares C, queries none      G6.1 declares B, queries none
G2.2 declares C, queries none      G6.2 declares H, queries none
G3.1 declares B, queries none      G7.1 declares E, queries none
G3.4 declares F, queries none      G7.2 declares H, queries none
G3.7 declares D, queries none      G7.6 declares G H, queries neither
G4.4 declares D H, queries neither G8.3 declares H, queries none
G5.2 declares B, queries none      G9.5 declares G, queries none
G5.4 declares B, queries none      G9.6 declares E, queries none
G5.5 declares B, queries none
```

**Fail the load, not a warning.** A plan that declares evidence it never asks for is
mis-configured, and the failure mode is silent under-assessment.

### 1.2 — Repair the 17 groups *(L1 content)*

Add the missing `targetCategories` to each group's queries. **G5.2 is the priority**: its
requirement is *"Financial situation plus cashflow supports the recommendation"* — the group that
must find the Fact Find's **−£288 disposable income** — and it declares B while querying only F and
I. F5.1, F5.2 and F5.4 are all lost there.

Adding a category is not sufficient on its own: the query *text* has to retrieve the right passage
from it. Each repaired group needs at least one query phrased at the Fact Find's own vocabulary
(`total monthly disposable income`, `total net monthly income`, `residency status`,
`highest rate of income tax paid`), not the report's.

### 1.3 — Reconcile check scope with the catalogue *(L2, L3)*

- **L2 — B declared but unused in CHK-007, CHK-008, CHK-009.** The catalogue names Know Your Client
  as a primary category for all three. Either the groups need B queries added, or
  `primaryCategories` is wrong and should be corrected. Decide per check against the CSV; do not
  silently drop the declaration.
- **L3 — CHK-010 and category D.** D appears in neither CHK-010's `primaryCategories` nor any of
  its groups, so **no lint catches this**: the plan is internally consistent and factually wrong.
  The Investor Experience questionnaire — the FG21/1 low-capability driver behind F10.1 and F10.2 —
  lives in category D. Add D to CHK-010's `primaryCategories` and to G10.1/G10.4's queries.

**Then re-audit all ten plans against the checks CSV for further L3 cases.** L3 is the class the
tooling cannot find, and CHK-010 is unlikely to be the only instance.

### 1.4 — Do not treat the trigger as a veto *(A3, CHK-010 half)*

CHK-010 returned Indeterminate on all four groups on `hasVulnerabilityIndicators = false`, then
argued the case affirmatively: *"none of which inherently signal vulnerability requiring
adjustment… The case does not present clear FG21/1 drivers."* The contradicting evidence is quoted
four times in CHK-004 of the same run.

An overlay check that applies to every case cannot be gated on an extraction flag derived from the
report — the report's silence is the thing being assessed. Fix with 1.3 so the evidence reaches
CHK-010, and make the trigger a note in the prompt rather than an outcome.

**Expected gain, Stage 1.** F1.2, F1.3, F1.6, F5.1, F5.2, F5.4, F10.1, F10.2 become *reachable*.
Reachable is not caught — the assessor still has to make the comparison — but they are unreachable
today at any retrieval setting.

---

## Stage 2 — Stop discarding work already done

Three defects where the pipeline reached a finding and then threw it away. All small; all recover
findings from output that already exists.

### 2.1 — `comparisonPerformed` must not veto a concern *(A3)*

**Defect.** `comparisonPerformed` is a field the *model* fills in, and `ParsedOutcome` lets it
override the model's own verdict:

```csharp
!ComparisonPerformed ? CheckOutcome.Indeterminate : …
```

Measured over 60 groups: **20 set it false; 13 of those also wrote `PotentialConcern`; 11 also
wrote `severity: High`.** Seventy-one discrepancies were written and then dropped from the summary.
G3.7 lists the existing fund ratings `(9, 4, 2, 7, 4)`, writes High — and is filed as unassessable.
That is benchmark finding F3.4. G7.7 does the same with *"9.4% to 10.0% decrease in maturity
value"*.

**Fix.** Distinguish *I could not compare* from *I compared, and separately could not close one
input*. A group that names a discrepancy has, by definition, compared something:

```csharp
!ComparisonPerformed && Discrepancies.Count == 0 => Indeterminate
!ComparisonPerformed && Discrepancies.Count >  0 => ParseOutcome(Outcome)   // carry MissingInputs as a caveat
```

Also fix `Summarise`, which prints discrepancies **only** from `PotentialConcern` groups
([CheckFinding.cs:215](../../src/AiPromptEvaluator/CheckFinding.cs#L215)) — so all 71 survive only
in the detail body and contribute nothing to the summary a reviewer actually reads.

**Test.** A group with `comparisonPerformed: false` and a populated `discrepancies` array keeps its
outcome; one with an empty array is Indeterminate. Assert against the real G3.7 and G7.7 responses.

**Expected gain.** F3.4 and F3.5 move from partial toward caught; CHK-010 stops being wholly
Indeterminate.

### 2.2 — Stop truncating the extraction report *(E4a)*

**Defect.** [`Truncate(extraction.Json, 4000)`](../../src/AiPromptEvaluator/CheckPlanRunner.cs#L521)
cuts the check-level extraction block mid-key. Measured in this run's prompts: **4,986 characters —
55% — never reach any assessor**, and the cut lands inside the fourth `internalInconsistencies`
entry, so the assessor sees half a sentence. Everything after, including `ambiguities`, is
invisible. G1.8 survives only because it *also* receives the array as an untruncated canonical
fragment.

**Fix.** Raise the cap to fit the whole report (the observed model is ~9 KB), or truncate
structurally — keep whole array elements and drop the tail with an explicit count — never mid-token.
Emit a warning when truncation occurs at all.

**Effort.** Trivial. **Expected gain.** Every group sees the inconsistencies and ambiguities
extraction already found.

### 2.3 — The requirement is the plan's to state *(N3)*

**Defect.** `ParseGroup` makes the plan authoritative for `GroupId` and then does not do the same
for `Requirement`:

```csharp
GroupId = group.GroupId,                                  // plan wins, always
Requirement = string.IsNullOrWhiteSpace(finding.Requirement)
    ? group.Requirement
    : finding.Requirement,                                // model wins whenever it answers
```

**25 of 60 findings print and store the model's wording, not the catalogue's.** Most are harmless
expansions of a short label. But the mechanism permits scope change, and G1.7 took it — plan:
*"Existing products **(pensions and investments)**"*; output: *"Existing products **(pensions,
investments and protection plans)**"*. The assessor widened its own requirement, and the widened
text is what the reviewer reads and what the audit trail records. **A requirement the assessor can
redefine is not auditable.**

**Fix.** Delete the conditional; the plan wins unconditionally, exactly as for `GroupId`. Also
removes the doubled `[G3.1] [G3.1]` headings, which come from the model prefixing the identifier
into the requirement while the renderer adds its own.

**Also record the divergence.** 22 of 60 responses echoed a **check** id where a **group** id was
asked for. Harmless — `ParseGroup` overwrites it — but it means over a third of responses lost
track of which requirement they were answering. Count both divergences per run as a prompt-adherence
metric. If it climbs, something upstream has changed.

**Test.** A response echoing a wrong `groupId` or a rewritten `requirement` yields a finding
carrying the plan's values, and the divergence is counted.

**Effort.** Trivial.

---

## Stage 3 — Make citation verification discriminate

At **36% of quotes and 70% of groups flagged**, the signal is noise, and under 0.3 it rewrites
verdicts. Re-testing every citation against its own group's prompt and progressively relaxing the
matcher gives the real breakdown:

| | Count | Share | Fix |
| --- | --- | --- | --- |
| Absent from their own prompt | 105 | 100% | |
| JSON `\uXXXX` escapes | 7 | 7% | 3.1 |
| Table `\|` separators | 12 | 11% | 3.2 |
| Explicit ellipsis, two spans stitched | 7 | 7% | 3.3 |
| ≥60% contiguous run present (elision) | 39 | 37% | 3.3 |
| **No substantial contiguous run** | **40** | **38%** | **3.4 — not a matcher problem** |

**Punctuation is 11%, not the ~40% gap-analysis estimated by inspection.** Correct that record.

### 3.1 — Serialise canonical fragments unescaped

The fragment JSON reaches the prompt with `£` and `+` as `\`-escaped sequences; the model quotes
them rendered, so `Normalise` compares two different strings. Serialise fragments with
`JavaScriptEncoder.UnsafeRelaxedJsonEscaping`. **This is why G1.8 — the group that catches F1.8, the
run's best assertion-side finding — is flagged as fabricated for citing the place it came from.**

### 3.2 — Fold table punctuation in `Normalise`

Add `|` and separator runs (`---`) to the fold in
[CitationVerifier.cs:69](../../src/AiPromptEvaluator/CitationVerifier.cs#L69), alongside the
existing quote and dash folding.

### 3.3 — Accept elisions explicitly

Where a quote contains `...` or `…`, split on it and require each span to appear **in order** in
the haystack. For the rest, accept when a contiguous run covering ≥90% of the quote is present.
Keep `MinimumQuoteLength`. This is bounded relaxation — it cannot admit A1's altered quotation,
because a changed digit breaks the contiguous run.

### 3.4 — The residual 38% is a behaviour problem, not a matcher problem

Forty quotes have no substantial contiguous run in the text they cite. They are the model
reformatting a table into prose and presenting the result inside quotation marks:

```
[G2.4]  "Retire (Kim) age 66 in 2026, Retire (John) age 71 in 2027"
[G2.3]  "Year 2025 Age 69/65: Liquid Assets £6,000; Year 2057 Age 101/97: Liquid Assets £1,865"
```

Neither sentence exists in any document. Both are accurate *reads* and fabricated *quotations*.
**The verifier is right to reject them, and loosening the matcher far enough to admit them would
re-open the exact hole it was built to close.**

Fix on the schema side instead: allow a citation to be either a verbatim span **or** a structured
table reference (`passageId` + row/column identifier + the values read), and say so in the prompt.
Verify the structured form by checking the cells, not the prose.

**Expected result of Stage 3.** Failure rate from 36% to roughly 12%, flagged groups from 70% to
under 25%, and 0.3 can then ship safely.

---

## Stage 4 — New capability

Everything above is repair. These four are things the pipeline has never done.

### 4.1 — Compute derived figures in code *(A4)*

**Defect.** Sums, percentages-of and implied values are asked of the model. Three consequences:

- **F7.2 missed by one division.** G7.4 back-solves *"this would require a fund value of
  approximately £103,430"* for Zurich, then lists *"Current fund value for … Standard Life to verify
  the £186.19 figure"* as a **missing input**. £186.19 ÷ 0.0018 = £103,439 — the same number, four
  sentences up.
- **F1.8 caught by luck.** The £116,997.47 total is right only because *extraction* summed it.
  Nothing in the assessment path adds five fund values.
- **Delegated arithmetic manufactures.** Given the report's unit typo *"annual expenditure
  approximately £1,700"*, G5.2 concluded *"should be £141.67 monthly"* and propagated it.

**Fix.** A `DerivedFigures` component over the canonical model, run before assessment, emitting
computed values into the prompt as facts: arrangement totals, percentage-of-value checks, implied
fund values from charge/rate pairs, and frequency normalisation (weekly/monthly/annual restated
consistently, with the source frequency retained). All decidable without retrieval, all
unit-testable against this case.

**Expected gain.** F7.2, F7.1; F1.8 becomes robust; the F1.11 class of unit contradictions becomes
detectable rather than a trap.

### 4.2 — Cross-group contradiction pass *(N1)*

**Defect.** Four findings fail only because the assertion and its contradiction land in different
groups. F1.1 is the clearest: the Fact Find's **£1,200** is in CHK-006 G6.3's `fileSays`; the
report's **£300/week** is in CHK-001 G1.3 and G1.4. Both in this run, four thousand log lines apart,
and no component ever holds both.

**Fix.** After all 60 findings land, index them by canonical path and by the figures they mention,
and flag pairs where one group's `reportSays` and another's `fileSays` carry conflicting values for
the same quantity. Report as a run-level addendum, not inside a check.

**Cheap, because it needs no retrieval and no new prompts** — everything it consumes is already on
`CheckFinding.Groups`. Start rule-based on numbers and dates; a model pass over candidate pairs is
a later refinement.

**Expected gain.** F1.1, F6.3, F9.3, F8.1.

### 4.3 — Keep the report's verbatim claim beside the enum *(N2)*

**Defect.** The report says the client is *"comfortable with how investments work and [has] a
reasonable understanding of potential risks & rewards"*. The canonical model records
`knowledgeLevel: "Limited"`. G4.1 then compares *Limited* against *"No understanding / knowledge"*
and concludes *"There is no contradiction"*. **The finding was normalised away before any assessor
saw it.**

**Fix.** For fields a check tests as a *claim* rather than a *category* — knowledge level, risk
label, time-horizon label, capacity for loss — carry the report's exact sentence alongside the enum
in the schema, and show both. This is an extraction-schema change; coordinate with the extraction
plan's owners.

**Expected gain.** F4.1. **Watch for:** the same pattern in risk and horizon fields, which this
case does not isolate.

### 4.4 — Repeated-key detection for `extractionReport` *(E4b)*

**Defect.** G1.8's own `fileSays` states the architecture: *"this requirement assesses the report's
internal consistency against itself… The canonical model identifies internal inconsistencies
reported by the extraction process itself."* So CHK-001's internal-consistency requirement returns
exactly what extraction found — four — where the benchmark counts at least eight. **The page-5
versus page-11 Zurich charge conflict (F7.1) is not among them, and no second mechanism could find
it.**

**Fix.** The unbuilt half of E4: walk the canonical model for paths carrying the same semantic key
with different values, and hand the candidates to the `extractionReport` pass. No model call to
detect; a model call only to describe.

**Expected gain.** F7.1, F9.5.

### 4.5 — Relevance floor, with somewhere to land *(R1)*

**Defect.** Lowest passage admitted scores **0.503**; median 0.616; *"Nothing was retrieved"* has
**never** appeared in any log. Two false positives trace directly to it — G9.3 reading Zurich's
*"life cover … no longer available on new pension plans"* boilerplate as this client losing life
cover (the benchmark records F8.5 as **no issue**), and G1.7 concluding *"no evidence whatsoever is
provided"* about documents the same run quotes elsewhere.

**Fix, in order:**

1. **Write the `ifEvidenceAbsent` rules first.** gap-analysis says they are unreachable; in fact
   **0 of 60 groups define one.** They were never written. A floor without them just empties packs.
2. Then add a minimum score. The band is narrow (0.503–0.765), so calibrate rather than guess —
   sweep on this case and check that no currently-caught finding loses its evidence.

**Sequenced last deliberately.** A floor is the only change here that can *reduce* recall, and it
should be measured against a pipeline whose other defects are closed.

---

> ## Stage 5 status — implemented 2026-08-14
>
> **5.1, 5.2, 5.3, 5.4 and 5.6 are in the build. 5.5 is a run, not a change.**
> Build clean, **343 tests passing** (was 326).
>
> | Item | Status |
> | --- | --- |
> | 5.1 Per-category floor in `Rank` | **done** — one slot per declared evidence category, then score |
> | 5.2 `CrossGroupContradictions` | **done** — no splitting inside decimals; recurrence read per figure |
> | 5.3 `DerivedFigures` inputs | **done** — arrangement charge lines read; both totals compared |
> | 5.4 Structured table citations | **done** — `cells` on the citation, verified against the named passage |
> | 5.6 Scoring rubric | **done** — [scoring-rubric.md](scoring-rubric.md) |
> | 5.5 One controlled run | **not a code change** — re-run at top 16, everything else held |
>
> ### One thing Stage 5 got wrong while building it
>
> §5.2 said to require the two figures to be "the same kind of quantity", and the first
> implementation read recurrence from the **sentence**. That broke the case the class exists for:
> *"the client receives £300 per week as an HGV driver, a monthly income of £1,300"* carries both
> markers, so whichever matched first won and the other figure was mislabelled — and the monthly
> half is exactly what pairs against the fact find's £1,200. A test caught it. Recurrence is now
> read from the words around **each figure**, not the sentence.
>
> ### And one thing 5.4 confirmed rather than assumed
>
> The near-miss matcher stays rejected. Cells are checked **all-or-nothing against the passage the
> citation names** — a row read is a claim about the whole row, and admitting a partial match
> would let a wrong figure ride along with three right ones, which is the altered-quotation
> failure wearing a different hat.

---

## Stage 5 — What Run 2 says to do next

Added 2026-08-14 after scoring the first genuinely generated run since Stages 0–4 landed. Ordered
by measured value, not by tidiness: **5.1 alone accounts for five of the six remaining misses.**

### 5.1 — A per-category floor in `Rank` *(R3 — the one that matters)*

**Stage 1 worked and the pack cap undid it.** The plans now ask for the Fact Find, and
`CaseDocumentSearchTool.SearchAsync` runs every query twice — once restricted to its target
categories, once unfiltered — so Fact Find passages **do** enter the candidate set. Then
[`Rank`](../../src/AiPromptEvaluator/CheckPlanRunner.cs#L388) sorts by a single binary key (in the
group's targeted set, or not), then by score, and takes twelve:

```csharp
.OrderByDescending(p => targeted.Count == 0 || targeted.Contains(p.CategoryCode) ? 1 : 0)
.ThenByDescending(p => p.Score)
.Take(MaxPassagesPerGroup)
```

Once B joins E/G/H/I in `targeted`, that first key is **uniform across every candidate**, the
ordering collapses to pure score, and Fact Find prose loses to research and report prose. The
passages are retrieved and then evicted. Every affected pack sits exactly at the cap — CHK-009's
seven groups hold 12, 12, 12, 12, 12, 8, 12.

**This is R3, "the passage cap binds before the search limit does" — the lever gap-analysis called
untested and this plan deferred.** §1.1 said adding a category was not sufficient on its own and
gave the wrong reason: I blamed query phrasing. The queries are fine.

**Fix.** Reserve slots per declared evidence category before filling the remainder by score — one
or two each, so a category a group declares cannot be crowded out entirely by a better-scoring
neighbour. Then re-measure; if the floor helps, `MaxPassagesPerGroup` is worth varying too, which
remains untested.

**Expected gain.** F1.3, F1.6, F5.1, F5.2, F5.4 — every one a Fact Find fact, and F5.1 the
starkest: CHK-005 still never sees `Total Monthly Disposable Income | £-288.00`, and G5.1 still
concludes affordability is supported.

### 5.2 — Fix `CrossGroupContradictions`, which is currently emitting noise

It fires, and its output is unusable:

```
  Client income
    [CHK-001/G1.3] (2) Employed as Volunteer with Sea Cadets with income £20 per week (page 3)
    [CHK-005/G5.1] 55 is deducted from the fund transfer value (£110,185), not from monthly income
```

`55` is the tail of `£3,305.55`. **The sentence splitter splits on `.` and therefore splits every
decimal.** Of the six pairs reported, none is a genuine contradiction — all six pair unrelated
quantities that share a cue word.

**Fix.** Do not split on a period between digits. Then require the two figures to be the same
*kind* of quantity, not merely to match a cue: a monthly income and a one-off fee are not in
disagreement just because both are money and both sentences say "income". Until both land, this
section is worse than absent — it is printed to reviewers and teaches them to skip the addendum.

### 5.3 — Extend `DerivedFigures` to the inputs it missed

**F7.2 regressed from partial to missed**, and the component built to own it never saw the right
data. Its two charge outputs implied £110,185.71 and £115,195.45 and reported both as *"matches no
arrangement's current value"* — true, and useless.

Two defects:

- **It reads only `costsAndCharges.*.lines` for implied bases.** The Standard-Life-computed-on-
  Zurich case lives in the existing arrangements' own charge records, which it consults only for
  the repeated-value check. Read both.
- **It compares implied bases against arrangement values alone.** £110,185.71 *is* the transfer
  total — the amount being invested. Comparing against that, and against the arrangement sum,
  turns a useless line into a check.

### 5.4 — Structured table citations *(the deferred half of §3.4)*

Re-measured on Run 2: the escape fix cleared **7 of 7**, pipe-folding now recovers 24, and the
residual is **105 of 129 failures — 81%**. That residual is the bucket §3.4 identified and
deliberately left: the model reformatting a table into prose and quoting the result.

It now blocks three exit criteria on its own, and it is why the report still prints `0 no issue`
— eleven passes were downgraded. **This is the largest remaining single cause of anything.** The
fix is unchanged: allow a citation to be either a verbatim span **or** a structured table reference
(passage id + row/column + values read), and verify the structured form by checking cells.

### 5.5 — One controlled run

Run 2 moved **four things at once** — search limit 16→8, extraction cap 16k→32k, a re-extracted
canonical model, and all of Stages 0–4. The recall gain is real and far outside the noise floor,
but nothing in it is attributable by measurement; §3 of the Run 2 analysis attributes by mechanism
instead, which is weaker.

**Re-run at top 16 with everything else held.** One variable, one comparison — and the R3 question
in 5.1 gets a direct answer.

### 5.6 — Write the scoring rubric before the next recall claim

Still owed, and now overdue: F6.2 moved caught → partial between two runs whose evidence for that
group did not change. Until two readers can reproduce a score, every recall number carries ±2.

---

## Exit criteria

Measured on Run 2, the first genuinely generated run since the plan landed —
[Run-2/run-analysis.md](Runtime-Logs/latest/Run-2/run-analysis.md).

| After | Measure | Run 1 | Target | **Run 2** | |
| --- | --- | --- | --- | --- | --- |
| Stage 0 | Runs mis-reported as generated | 8 of 12+ | **0** | **0** — the replay self-announced | ✅ |
| Stage 0 | Plan copies disagreeing | 4 of 9 stale | 1 source | 1 source, shape pinned | ✅ |
| Stage 1 | L1 violations | 17 of 60 | 0, enforced | **0**, enforced at load | ✅ |
| Stage 1 | Groups reaching category B: CHK-005 / 007 / 008 / 009 | 1/6, 0/7, 0/5, 0/7 | ≥1 per group declaring it | 1/6, **1/7**, 0/5, 0/7 | ❌ |
| Stage 2 | High-severity concerns demoted by `comparisonPerformed` | 11 | 0 | 17 groups still vetoed | ⚠️ |
| Stage 2 | Extraction report reaching assessors | 45% | 100% | **100%** | ✅ |
| Stage 2 | Findings storing the model's requirement text | 25 of 60 | 0 | **0** | ✅ |
| Stage 3 | Unverified quote rate | 36% | <12% | **32%** | ❌ |
| Stage 3 | Groups flagged | 70% | <25% | **70%** | ❌ |
| Stage 4 | Benchmark recall (caught) | 16/36 (44%) | ≥26/36 (72%) | **24/36 (67%)** | ⚠️ |
| Stage 4 | Missed | 8 | ≤3 | **6** | ❌ |

**Five met, two close, four missed.** Recall moved 44% → 67% and every check now lands on its
expected outcome. The four misses share two causes, and both are now understood precisely rather
than suspected — which is what Stage 5 acts on.

> **Read recall against the noise floor.** Two scorers hand-adjudicating the *same 60 responses*
> produced 39% and 44%, so **±2 findings is the noise floor**. The 23-point gain is well outside
> it. But F6.2 moved from caught to partial between two runs whose evidence for that group did not
> change at all, which is the noise floor doing exactly what it does.
> [scoring-rubric.md](scoring-rubric.md) now writes down the rules that settle those cases; the
> next adjudication is the first that can be reproduced.

---

## What was not done, and why

Three items remain unbuilt. Each is blocked on something a code change cannot supply.

**4.3 — carry the report's verbatim claim beside the normalised enum.** This is a change to the
extraction schema (`knowledgeLevel`, the risk and horizon labels), not to the assessment path,
and it changes what every stored canonical model contains. It belongs with the extraction plan
and needs its owners: shipping it here would leave the two plans disagreeing about the schema.
**F4.1 remains neutralised until it lands.**

**4.5 — relevance floor.** Still not done, and one of the two reasons has now gone. The
`ifEvidenceAbsent` rules it needs to trigger into do not exist — **0 of 60 query groups define
one** — so a floor today would only empty packs; that half stands. The other half was that
calibration needed a generated run, and **Run 2 supplies one**: the admitted band measured
0.507–0.765, median 0.620, and *"Nothing was retrieved"* has still never appeared in any log.

**But Run 2 also changed the priority.** A floor removes low-scoring passages; §5.1 shows the
binding problem is the opposite one — relevant Fact Find passages are being retrieved and then
**evicted by the twelve-passage cap**. Tightening what enters while the cap still decides what
survives would trade one silent loss for another. **Do 5.1 first, re-measure, then calibrate this
against the result.**

**4.4, extraction half.** The assessment-side detection is built: `DerivedFigures` now reports
when one arrangement's charge is recorded at two different percentages, which is the route to
F7.1. Feeding repeated-key candidates into the `extractionReport` pass is the other half, sits
in the extraction pipeline, and is the same coordination question as 4.3.

**Run 2 has now executed** — see the [exit criteria](#exit-criteria) for what it measured and
[Stage 5](#stage-5--what-run-2-says-to-do-next) for what it says to do next. Two things it did not
settle:

- **It moved four variables at once** (search limit, extraction cap, canonical model, and all of
  Stages 0–4), so the 23-point recall gain is real but not attributable by measurement. §5.5 asks
  for one controlled run.
- **It found two defects in this plan's own output** — `CrossGroupContradictions` splitting on
  decimal points, and `DerivedFigures` reading the wrong charge lines. Both are §5.2 and §5.3.
  Shipping a component is not the same as it working.

---

## What this plan does not do

- **No prompt rewriting of the assessor's reasoning.** It found the Zurich ranking, the £12,000
  reduction, the risk-6 file note and the balanced-investor precondition unaided. Reasoning is not
  the bottleneck; inputs and post-processing are. Revisit after Stage 4 measures.
- **No retrieval parameter tuning.** Search limit, `MaxPassagesPerGroup` and the candidate set are
  not the binding constraint while 17 groups do not query the categories they declare. Re-open R3
  after Stage 1.
- **No second case.** Every check on ABC-99 lands on Potential Concern, so **this dataset cannot
  measure false positives at check level at all** — and Stages 3 and 4.5 both change false-positive
  behaviour. A clean case is needed to see whether they help or hurt, and building one should start
  in parallel rather than after.
- **Not carried over from gap-analysis:** the `basis` enum review (E6) and the *"Failed sections
  (6)"* mis-report. Both are extraction-side and belong with that plan; neither affects assessment
  recall.
