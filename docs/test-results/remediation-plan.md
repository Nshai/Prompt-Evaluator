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
> ### Stage 8 — implemented 2026-08-14, after scoring Run 5
>
> **Build clean, 410 tests passing (was 384).** No behaviour change to any verdict.
>
> | Stage | Item | Status |
> | --- | --- | --- |
> | 8.1 | Surface findings a group discards | **done, steps 1 and 2** — counted at run level and printed under the check that passed. Step 3 (constraining the outcome) deliberately **not** built — see below |
> | 8.3 | Citation verifier normalisation | **done, measured** — Run 5's quote failures fall **34 → 19** replaying the same log through the shipped code |
> | 8.4 | A section hint that matches nothing must say so | **done** — `CheckPlanRunner.UnmatchedSections`, reported per run |
> | 8.2 | Build 7.6 — stop merging the two charge tables | **not done** — still the extraction-schema coordination blocking 4.3 and 4.4 |
> | 8.5 | Controlled re-run | **not done** — needs a run, not a code change |
> | 8.6 | Report all three counts | **done in this document** — missed-rate promoted above recall in [Exit criteria](#exit-criteria) |
>
> **8.1 was measured before it was designed, and the measurement changed the design.** The plan's
> first instinct was the obvious rule: a non-empty `discrepancies` array is incompatible with a
> pass. Counting first showed **9 to 15 groups per run** discarding **27 to 51 differences**, so
> that rule would have promoted dozens of £0.02 variances to Potential Concern, flooded the report
> and destroyed the check-level signal. What shipped changes no verdict — it refuses to let the
> working vanish with the group. Step 3 stays unbuilt until a run says which of the 27 matter.
>
> **8.3's premise was wrong and the fix is better for it.** The item was written as "normalise
> whitespace"; `Normalise` already collapsed whitespace. Measuring the 34 actual failures named
> three real causes instead: Wingdings bullets at `U+F0B7` that the PDF converter leaves in the
> evidence, markdown emphasis, and sentence punctuation — folded only where it is *not* holding
> two digits apart, so `£1,430.00` stays distinct from `£143000`. A fourth defect surfaced while
> building: `CollapseSeparatorRuns` emitted a double space, so a folded table row could not match
> itself.
>
> **And the fix broke elision before a test caught it.** Folding `.` away turned
> `"grown...the Fund"` into one span matching nothing. Dots adjacent to dots are now exempt. The
> guarantee the class exists for is pinned by test: evidence "a Risk rating of 6" against a
> finding quoting "a Risk rating of 5" still fails, and so do `0.18%` against `018%` and
> `rating 5.0` against `rating 50`.
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

> ## Stage 6 status — implemented 2026-08-14
>
> **6.1/6.2, 6.3, 6.4 and 6.6 are in the build. 6.5 is a run, not a change.**
> Build clean, **360 tests passing** (was 343).
>
> | Item | Status |
> | --- | --- |
> | 6.1 + 6.2 Content density | **done, merged** — an unfilled form ranks below anything carrying a value. Demoted, never dropped: an unfilled section is sometimes the finding. |
> | 6.3 Cross-group pair selection | **done** — relative disagreement, a 5% floor, and report-against-file preferred over two readings of the same evidence |
> | 6.4 Missing charge lines | **cause found and fixed** — the `basis` enum was stripping them; **all 69 canonical-schema enums are now free text** |
> | 6.4b Extraction reporting | **done** — a section that lost a value no longer reports as FAILED |
> | 6.6 Third pension total | **adjudicated — not a finding**, see below |
> | 6.5 One controlled run | **not a code change** — hold the extraction and re-run |
>
> Also fixed, found while reading Run 3's own output: `DerivedFigures` called an implied
> £115,195.45 "the total of all arrangements" (£116,998.47), 1.57% away and inside a 2% tolerance.
> A charge divided by its own rate lands on its base to within a rounding, so the tolerance is now
> 0.5%. **A false attribution is worse than none** — "matches no arrangement" tells a reader to
> look, naming the wrong plan tells them not to.
>
> ### 6.4 in full: the enums were discarding the evidence
>
> The extraction log named the cause on its own face, and it had been visible for three runs:
>
> ```
> [ 5/12] Existing arrangements  FAILED — Dropped 2 value(s) the schema's enums do not allow:
>                                basis = "of fund pa"; basis = "of fund pa (estimated)"
> [ 9/12] Costs and charges      FAILED — Dropped 8 value(s) … basis = "of fund value pa";
>                                "of amount invested"; "annual recurring charge"; …
> ```
>
> `Money.basis` allowed `Gross`, `Net`, `Unspecified`. Every charge line in this report carries a
> basis describing what the amount is *of*, so every one was stripped — which is why `£186.19`,
> the figure F7.2 turns on, has never once reached an assessor. **The enum was describing a
> vocabulary the documents do not use.**
>
> Per instruction, this was applied to the whole schema rather than the one field: all 69 enums
> are now string properties, each carrying its former values as guidance in the description
> (`"One of: Gross, Net, Unspecified."`). The model is still told the convention; nothing rejects
> it for departing from one. **The response schema for findings is deliberately untouched** — its
> `outcome` enum is what makes a verdict parseable, and it constrains a decision rather than
> describing a document.
>
> ### 6.6: I had it wrong
>
> I read `£116,555.45` in the Fact Find as a third pension total contradicting £116,998.47. In
> full, it sits beside `Valuation Date | 23/07/2026` — it is the Aviva Platform plan's value
> *after* consolidation, at a future date. A different quantity, so no contradiction. The real
> anomaly there, a plan valued nine months after the report, is already **F1.12**. Nothing is
> added to the scoring set.
>
> **Run 3 measured Stage 5: coverage and citations improved sharply, recall did not move.**
> Fact Find coverage 7/10 → **10/10 checks**, unverified quotes 32% → **14%**, flagged groups
> 70% → **45%**, missed 6 → **5**, caught unchanged at 24 of 36.
>
> The per-category floor put the right document in front of every check and the assessor still
> could not see the fact, because every Fact Find passage it admitted was an empty form table
> rather than the income section. **Retrieval coverage is solved; precision within a document is
> the entire remaining gap**, and four of the five remaining misses are one section of one
> document. See [Stage 6](#stage-6--what-run-3-says-to-do-next).
>
> Nothing in Stage 6 is built yet.

---

> ## Stage 7 status — implemented 2026-08-14
>
> **7.1, 7.2, 7.3 and 7.5 are in the build. 7.4 is a run, not a change.**
> Build clean, **366 tests passing** (was 360).
>
> | Item | Status |
> | --- | --- |
> | 7.1 Section-level routing | **done** — `evidenceSections` on a query group, matched in `Rank` above score; hints added to the 7 groups whose findings are blocked |
> | 7.2 Third charge shape | **done** — any object with `…Percentage` / `…Amount` siblings under one prefix is now divided, found structurally rather than by named path |
> | 7.3 £186.19 — 0.18% or 0.52%? | **settled: 0.18%.** The benchmark is right and the extraction is wrong — and the reason matters, see below |
> | 7.5 Citation metric framing | **done** — reported as "citation trust… measures how far the working can be checked, not how much was found" |
> | 7.4 Hold the extraction and re-run | **not a code change** |
>
> ### 7.3: the extraction is reconciling a contradiction it is told to record
>
> Opening the report settles it, and finds something worse than a wrong digit. Page 11 reads:
>
> ```
> Plan            Current Annual Recurring Charge      Impact of Recommended Alternative
> Aviva           0.50%   £18.72                       -0.06%   -£2.25
> Zurich          0.93%   £961.98                      -0.49%   -£506.85
> Standard Life   0.18%   £186.19                       0.26%    £268.94
> ```
>
> **0.18%, exactly as the benchmark reads it.** So £186.19 ÷ 0.18% = £103,439 — Zurich's fund, not
> Standard Life's £3,002. **F7.2 is confirmed as a real finding.**
>
> Where did 0.52% come from? Page 5's Existing Arrangements table:
>
> | | Zurich | Standard Life |
> | --- | --- | --- |
> | **Page 5** | 0.18% | **0.52%** |
> | **Page 11** | 0.93% | **0.18%** |
>
> **The two pages disagree — which is benchmark finding F7.1 — and the extraction merged them**,
> taking page 5's rate and attaching it to page 11's amount. It stored one value where the report
> holds two conflicting ones, which silently destroys F7.1's evidence and corrupts F7.2's pairing.
>
> That is a direct breach of the extractor's own standing instruction to *"record contradictions
> instead of resolving them"*, and it is an extraction-side defect: **7.2 now divides whatever pair
> it is given, so with 0.52% stored it will correctly report "matches nothing" rather than the
> finding.** The arithmetic is ready; the input is wrong. Added as [7.6](#76--stop-the-extraction-merging-two-tables-into-one-value-new-from-73).
>
> **Stage 6's schema fix worked outright: extraction went from 4 of 12 sections succeeding to
> 12 of 12, `charges` from 0 occurrences in the pack to 521, and £186.19 exists in the model for
> the first time.** Unverified quotes hit 11%, meeting the target.
>
> **Recall did not move. Third consecutive run at 24 of 36.**
>
> The measurement that explains it: the Fact Find's income section — the one carrying
> `Total Monthly Disposable Income £-288.00` — reached the evidence pack of four groups for the
> first time, and **not one of them mentioned it**. They were assessing personal details, tax
> status, employment and the emergency fund. The group that needed it did not receive it.
>
> **Retrieval is no longer the bottleneck; routing is.** Nothing in Stage 7 is built yet.

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
- [Stage 6 — What Run 3 says to do next](#stage-6--what-run-3-says-to-do-next) — **chunking**, N1 selection, E-side
- [Stage 7 — What Run 4 says to do next](#stage-7--what-run-4-says-to-do-next) — **section routing**, A4 third shape
- [Stage 8 — What Run 5 says to do next](#stage-8--what-run-5-says-to-do-next) — **S1 suppressed findings**, 7.6, verifier normalisation
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

**Defect.** [`GroupFinding.ParsedOutcome`](../../src/AiPromptEvaluator.Core/CheckFinding.cs)
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
it cannot promote a passage `CaseDocumentSearchService` was never asked for — which is why raising the
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
([CheckFinding.cs:215](../../src/AiPromptEvaluator.Core/CheckFinding.cs)) — so all 71 survive only
in the detail body and contribute nothing to the summary a reviewer actually reads.

**Test.** A group with `comparisonPerformed: false` and a populated `discrepancies` array keeps its
outcome; one with an empty array is Indeterminate. Assert against the real G3.7 and G7.7 responses.

**Expected gain.** F3.4 and F3.5 move from partial toward caught; CHK-010 stops being wholly
Indeterminate.

### 2.2 — Stop truncating the extraction report *(E4a)*

**Defect.** [`Truncate(extraction.Json, 4000)`](../../src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs)
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
[CitationVerifier.cs:69](../../src/AiPromptEvaluator.Core/Services/Assessment/CitationVerifier.cs), alongside the
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
`CaseDocumentSearchService.SearchAsync` runs every query twice — once restricted to its target
categories, once unfiltered — so Fact Find passages **do** enter the candidate set. Then
[`Rank`](../../src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs) sorts by a single binary key (in the
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

## Stage 6 — What Run 3 says to do next

Added 2026-08-14 after scoring Run 3, the first run measured under
[scoring-rubric.md](scoring-rubric.md) and the closest thing yet to a controlled comparison — the
plans, the search limit and the extraction cap were all held.

**Run 3 split cleanly: every mechanical measure Stage 5 targeted improved sharply, and recall did
not move.** Fact Find coverage went from 7 of 10 checks to **10 of 10**, the unverified quote rate
from 32% to **14%**, flagged groups from 70% to **45%** — and caught findings stayed at 24 of 36.
Missed fell 6 → 5.

That combination is the whole of Stage 6. The floor put the right *document* in front of every
check and the assessor still could not see the fact, because **the passage it got was the wrong
part of that document**. Retrieval coverage is solved. Retrieval precision *within* a document is
now the entire remaining gap.

### 6.1 — Chunk the Fact Find on its sections, and drop chunks that carry no content

**The measurement.** Every Fact Find passage the per-category floor admitted is form furniture:

| Group | The Fact Find passage it received |
| --- | --- |
| CHK-005/G5.2 | `| | In force | In force | Status | | GMP Amount (p.a.) | |` |
| CHK-005/G5.3 | `| Lump Sum Cont. | | | | | Value | £116,555.45` |
| CHK-005/G5.4 | `| Year End | | Most Recent Annual Accounts | | Year 2 Annual Accounts…` |
| CHK-008/G8.1 | `| Statement 5 Answer | Yes` |
| CHK-009/G9.1 | `| Lump Sum Cont. | | | | | Value | £116,555.45` |

Empty pension-feature grids, a blank accounts table, a questionnaire answer cell. **Not one is the
income and expenditure section**, which is where four of the five remaining missed findings live.
The same chunk was admitted to four different groups across three checks — the signature of one
chunk dominating a category rather than a category being explored.

**Why it happens.** The Fact Find is a 42 KB form of mostly-empty tables. An empty table row is
short, generic and structurally similar to a query, so it embeds close to almost anything while
carrying no assertion. Against that, the one section that would settle F5.1, F5.4 and F1.6 loses
the within-category ranking.

**This is a chunking and embedding problem, and no further work on `Rank` will touch it.** The
floor is doing its job: it guarantees a passage from the category, and the category's best-scoring
passage is worthless.

**Fix.** Chunk the Fact Find on its section headings rather than on size, so "Income", "Expenditure"
and "Residency" are retrievable units. Then drop, at indexing time, chunks whose cells are
overwhelmingly empty — a row of blank pipes is not evidence and should not be a candidate.

**Expected gain.** F5.1, F5.4, F1.6, and F5.2 by entailment. **Four of the five remaining misses,
from one change.**

### 6.2 — Rank on content, not only on similarity

The general form of 6.1, and worth doing even after it. A passage reading
`| GMP Amount (p.a.) | |` should not be able to outrank an income table for any query, and today it
can. Options, cheapest first: weight retrieval by content density (non-empty cells, digits, words
per character); index form *labels* separately from form *answers* so a label match does not
promote an unanswered field; or require an admitted passage to contain at least one of its query's
`expectSignals`, which the plans already declare and which nothing currently enforces at pack level.

### 6.3 — Fix how the cross-group pass picks its pair

Stage 5.2 fixed what the pass reads. **It did not fix what it chooses.** Run 3's output is now
coherent — no fragment begins mid-number, and the recurrence keys hold weekly against weekly — and
of five pairs reported, **none is a contradiction**. They are restatements of the same figures from
different checks.

Worse, the pair that would have mattered was available and passed over: G1.2 carries the Fact
Find's `£1,430.00` household total and G1.4 carries the report's `£4,486.67`, both under
*Client income (monthly)*, and that is F1.3. The "widest spread between two groups" heuristic chose
a different pair under the same key.

**Selecting extremes was the wrong rule.** What is wanted is the pair that most disagrees *about
the same claim* — prefer report-side against file-side, prefer pairs whose sentences share more
than the cue word, and report more than one pair per subject where they exist.

### 6.4 — Find out why no existing-arrangement charge lines reach the model

Stage 5.3 extended `DerivedFigures` to read `existingArrangements[].charges.lines` precisely so
that F7.2 — £186.19 quoted at 0.18%, which implies Zurich's £103,439 rather than Standard Life's
£3,002 — would be caught by division rather than by luck. **`£186.19` appears nowhere in Run 3.**

The fix is correct and the data is not there: the canonical model carries no charge line under
either path the component reads. **This is now an extraction question, not an assessment one**, and
it belongs with the extraction plan. Until it is answered, F7.2 is unreachable by any amount of
assessment-side work.

### 6.5 — Hold the extraction too, and re-run

Run 3 held the plans, the search limit and the extraction cap, and **re-extracted the canonical
model** — so Stage 5 and a fresh extraction moved together. The coverage and citation numbers are
attributable to Stage 5 by mechanism (the floor and the `cells` field are the only things that
could produce them); recall being flat is attributable to neither.

**Re-run against a pinned canonical model.** It is the last uncontrolled variable, and the next
recall number is worth little without it.

### 6.6 — Adjudicate a third pension total

The Fact Find passage the floor admitted states the client's pension total as **£116,555.45**. The
arrangements sum to **£116,998.47** and the report states **~£110,000**. That is a third figure for
the same quantity, in a document the benchmark was built from, and **it is not in the benchmark**.

Adjudicate it. If it is a real contradiction it belongs in the scoring set; if it is a different
quantity the benchmark should say so. Either way it should not sit unremarked in a run's evidence.

---

## Stage 7 — What Run 4 says to do next

Added 2026-08-14 after scoring Run 4. **Stage 6's schema fix worked outright and moved no
findings**, which makes this the third consecutive run to catch the same 24 of 36.

### The plateau is the finding

| | Run 2 | Run 3 | Run 4 |
| --- | --- | --- | --- |
| Extraction sections succeeding | — | 4 of 12 | **12 of 12** |
| Checks reaching the Fact Find | 7/10 | **10/10** | 10/10 |
| Unverified quote rate | 32% | 14% | **11%** |
| **Benchmark recall (caught)** | **24/36** | **24/36** | **24/36** |

Four canonical models, two retrieval configurations, six exit criteria met — and **the same 24
findings every time**. On a ±2 noise floor that is a plateau, not a coincidence, and it means the
twelve findings the pipeline does not reach are blocked by none of the machinery that has been
fixed.

Where they are actually blocked:

| Blocked by | Findings | |
| --- | --- | --- |
| **Section-level routing** — right document, wrong part, or right part to a group with no use for it | F1.1, F1.2, F1.3, F1.6, F5.1, F5.2, F5.4 | **7** |
| A third data shape `DerivedFigures` does not read | F7.2 | 1 |
| Cross-group composition, which depends on the above landing first | (F1.1, F1.3) | — |
| Judgement inside a group | F1.9, F3.4, F6.2, F8.1 | 4 |

**Seven of twelve are one document section reaching the wrong groups.** Stage 7 is mostly that.

### 7.1 — Let a query group target a document *section*, not just a category

**The measurement that settles it.** The Fact Find's income section — carrying
`Total Net Monthly Income £1,430.00`, `Total Monthly Expenditure £1,718.00` and
`Total Monthly Disposable Income £-288.00` — **reached the evidence pack of four groups** in Run 4,
for the first time in four runs: CHK-001's G1.1, G1.2, G1.3 and G1.9.

**Not one mentioned it.** `288` and `Disposable` appear zero times in the output.

Those four groups assess personal details, tax status, employment and the emergency fund. None has
a question the figure answers. The group that does — **G1.4, income and expenditure — did not
receive it**, and compared the report against the planning documents instead. Nor did any CHK-005
group, where F5.1 belongs.

**Retrieval is no longer the bottleneck. Routing is.** A plan can say "this group needs the Fact
Find" and cannot say "this group needs the Fact Find's *income section*", so a 42 KB form is a
single indivisible target and the per-category floor spends its one slot on whichever chunk
embeds best for that group's wording.

**Fix.** Carry the section heading with each chunk at indexing time, and let
`expectedCategories.evidence` take an optional section hint — `"B:Income"` beside `"B"`. The
per-category floor then holds its slot for the section rather than the document. This subsumes
Stage 6's chunking item, which was aimed at the same target and missed — see 7.5.

**Expected gain.** F1.1, F1.2, F1.3, F5.1, F5.2, F5.4, and F1.6 by the same route. **Seven
findings, and the only change on this list that can move recall.**

### 7.2 — Read the third charge shape in `DerivedFigures`

Stage 5.3 added `existingArrangements[].charges.lines[]` for F7.2. Stage 6.4 fixed the schema so
the data would survive. **Both worked, and the figure still escaped**, because the extraction
writes this comparison as flat sibling properties on a replacement-analysis entry:

```json
{ "arrangementId": "EA5",
  "existingAnnualChargePercentage": { "value": 0.52, "basis": "of fund value pa" },
  "existingAnnualChargeAmount":     { "amount": 186.19 } }
```

Neither path the component reads. **Third attempt, one path away:** pair any
`*Percentage` / `*Amount` siblings on the same object, not only entries in a `lines[]` array.

### 7.3 — Adjudicate £186.19: 0.18% or 0.52%?

**F7.2 cannot be scored honestly until this is settled, and it is not settleable from the logs.**

The benchmark reads the page-11 table as **0.18%** — which is what makes the finding, because
£186.19 at 0.18% implies £103,439, Zurich's fund, not Standard Life's £3,002. The extraction reads
the same table as **0.52%**, which implies £35,805 and matches nothing.

One of them is misreading a column. Open the report, decide, and correct whichever is wrong — the
benchmark or the extraction. Until then 7.2 could ship and produce a confidently wrong figure.

### 7.4 — Re-run holding the extraction

Run 4 *had* to re-extract, because Stage 6's schema change only takes effect on extraction. **That
excuse is now spent.** The next run can hold the plans, the settings and the canonical model, and
be the first fully controlled comparison in the project.

### 7.5 — Stop expecting the citation work to move recall, and say so

Unverified quotes have gone **36% → 32% → 14% → 11%**, the target is met, and across the same runs
recall has gone **44% → 67% → 67% → 67%**. The citation work is a **trust** measure — whether a
reviewer can rely on the working — and it has never been a recall measure. Report it as one, and
stop reading its progress as progress toward finding more.

**And a correction to Stage 6's premise.** 6.1 was argued on unfilled forms outranking content.
Tested against every Fact Find passage Run 4 admitted, **exactly one of eleven is a skeleton** and
`ContentDensity` catches it; the rest are genuinely filled tables about pension features, bank
statements and trading accounts. They are not empty — they are the wrong sections. **Content
density cannot separate "filled and relevant" from "filled and irrelevant", because both are
filled.** The rule is correct and the premise was too narrow, which is why 7.1 changes the unit of
targeting rather than the ranking again.

### 7.6 — Stop the extraction merging two tables into one value *(new, from 7.3)*

The report states Standard Life's existing charge as **0.52%** on page 5 and **0.18%** on page 11,
and Zurich's as **0.18%** on page 5 and **0.93%** on page 11. The extraction stores one figure per
arrangement, taking page 5's rate and pairing it with page 11's monetary amount.

**Two findings are lost to this.** F7.1 *is* the disagreement between those tables, and it cannot
be reported from a model that holds one reconciled value. F7.2 depends on the page-11 pairing being
preserved intact, and it is not.

The extractor is already instructed to record contradictions rather than resolve them, so this is
a compliance failure against its own prompt rather than a missing capability. **Fix by making the
charge a list rather than a scalar** — one entry per place the report states it, each carrying its
page — so a repeated key with different values survives to be compared. This is the same shape
[4.4](#44--repeated-key-detection-for-extractionreport-e4b) asks for and the same coordination
question as 4.3.

---

## Stage 8 — What Run 5 says to do next

Added 2026-08-14 after scoring [Run 5](Runtime-Logs/latest/Run-5/run-analysis.md). **Stage 7.1
worked.** It is the first change in five runs with a clean mechanical trace from commit to finding,
and it broke a three-run plateau — not in the headline, which is unchanged for the fourth time, but
in everything under it.

### The plateau broke and the number did not

| | Run 3 | Run 4 | **Run 5** | |
| --- | --- | --- | --- | --- |
| **Benchmark recall (caught)** | 24/36 | 24/36 | **24/36 (67%)** | — |
| Partial | 7 | 4 | **10** | |
| **Missed** | 5 | 8 | **2** | ▲▲ |
| `-£288` mentions in the output | 2 | **0** | **39** | ▲▲ |
| `1,430` (Total Net Monthly Income) | 2 | **0** | **33** | ▲▲ |

For three runs the caught set was *identical*. In Run 5 **five findings entered and five left**:

| In (→ Caught) | Out (Caught →) |
| --- | --- |
| F1.1, F1.3, F5.1, F5.2 — the Fact Find cluster | F3.1, F6.3, F7.1, F9.3, F9.4 |
| F6.2 — boilerplate rationale detected | |

Ten changed verdicts against a ±2 noise floor. **Missed fell from 8 to 2**, which is the measure
[the benchmark itself says to gate on](expected-results-benchmark.md) — *"A missed concern leaves
no trace in the output; a spurious one is visible and can be discarded by a reviewer."*

**7.1 is confirmed and 7.1 is not general.** The hints naming `Total Monthly Disposable Income`
and `Current Monthly Cash Flow` moved four findings. The hints naming `Residency` and
`Contact Address`, added in the same commit for F1.6, moved nothing — those words appear **nowhere
in the run's output**. Section targeting works where the section is a labelled table and fails
where the fact is prose. F1.6 and F5.4 are now the only outright misses, and they are the two the
hint was aimed at and failed.

### 8.1 — Surface findings a group discards *(new — S1)* — **DONE (steps 1–2); step 3 held**

**The pipeline found F3.1 and deleted it.** CHK-003/G3.6 wrote, in its own `discrepancies` array:

> "P11 file notes state 'Risk rating of 6' in the ATR wording section, **contradicting all other
> sources** which state the final agreed rating is 5."

Both sides named, conflict stated — the finding, correctly. The group then returned **`NoIssue`**,
and because the renderer prints only groups that raised a concern, the whole group vanished. The
string "Risk rating of 6" appears nowhere in `Run-Checks-output.txt`. In Run 4 the same group
reached the same finding and returned `PotentialConcern`, so it printed and scored Caught.

**Retrieval was identical in both runs.** The file note reached G3.6's pack either way. Nothing was
lost from the evidence, the routing or the reasoning: **the verdict field threw the finding away.**

This is a different failure from anything Stages 0–7 addressed, and it is invisible to every
measure the project has — recall is scored from the printed report, so a finding suppressed this
way reads as "never found".

**It is not one group, and it is not new.** Counting groups that returned `NoIssue` or
`NotApplicable` while their own `discrepancies` array was non-empty:

| | Run 2 | Run 3 | Run 4 | **Run 5** |
| --- | --- | --- | --- | --- |
| Groups passing while naming discrepancies | 12 | 15 | 13 | **9 of 60** |
| Discrepancies discarded | 44 | 51 | 36 | **27** |

**Every run in the series has been discarding between 27 and 51 named discrepancies, and no
analysis has counted them until now.** CHK-003/G3.1 appears in the Run 2 and Run 3 lists too, and
G3.6 in Run 5 — the risk-rating contradiction has been found and dropped repeatedly across four
runs, and scored as "missed" or credited to another group each time.

Most of these will be immaterial: a group is entitled to notice a £0.02 variance and correctly
decide it is not a concern. The rule below must therefore not force every one of them into the
report, or the output floods. But **the discard is currently silent**, which is what makes it
impossible to tell the £0.02 from the risk rating of 6.

**Fix, in the order it should be built** — the first step is free and tells you how to size the
rest:

1. **Count it, and print the count.** A one-line diagnostic beside prompt adherence and citation
   trust: *"9 group(s) recorded 27 discrepanc(ies) and did not carry them into a finding."* This is
   reporting, not behaviour, so it cannot regress anything and it converts an invisible failure
   into a tracked number. **Do this first, and re-read the four runs above against it.**
2. **Render the discarded discrepancies**, collapsed under the check that passed, marked
   raised-and-not-carried. A reviewer then sees what the pipeline saw and can disagree with it.
   This is the step that recovers F3.1.
3. **Only then consider constraining the outcome.** The obvious rule —
   *a non-empty `discrepancies` array is incompatible with a pass* — is the same shape as
   [2.1](#21--comparisonperformed-must-not-veto-a-concern-a3), which was built and scoped to
   `comparisonPerformed` rather than to the outcome. But 27 discards in Run 5 alone means most are
   immaterial by construction, and forcing all of them to Potential Concern would flood the report
   and destroy the check-level signal. **Do not ship this before step 1 has measured the
   distribution.** A severity threshold, or a rule keyed to whether the discrepancy names two
   conflicting figures, is likelier to be right than a blanket veto.

**Expected gain.** F3.1 recovers at step 2. The blind spot is the larger prize: no run has been
able to distinguish "not found" from "found and dropped", so recall has been measuring the two
together for five runs. **Step 1 is the cheapest item in this plan and it re-scopes everything
above it.**

> **Steps 1 and 2 built; step 3 deliberately not.** `GroupFinding.DiscardedDiscrepancies` names
> the differences a passing group listed; the run prints a count, and each one is rendered under
> the check that passed, marked `RAISED AND NOT CARRIED`. A wholly-cleared check is normally
> summarised rather than printed, so that path carries them too — otherwise the finding had two
> layers of folding to hide behind, which is how G3.6 was lost.
>
> **No verdict changes.** Counting first is what settled that: 27 discarded differences in Run 5
> alone means most are immaterial by construction, and the blanket veto step 3 describes would
> have promoted every £0.02 variance to Potential Concern. The rule stays unbuilt until a run
> shows which of them matter — which is now a question the report answers rather than one that
> needs the logs re-read.

### 8.2 — Build 7.6 — stop the extraction reconciling the two charge tables — **NOT DONE (blocked)**

**Unchanged in substance, promoted in priority: five of Run 5's ten partials sit on it.**

F7.1, F7.2, F6.3, F9.3 and F9.4 are all cases where the run **has both figures** and compares them
against a *provider document* instead of against the report's other page:

> "EA4 (Zurich) existing charge stated as 0.93% in report; Zurich provider documentation shows
> annual management charge of 0.75% (P1)" — CHK-007/G7.4

Right area, right figures, wrong contradiction. F7.1 *is* the report's page 5 disagreeing with its
page 11, and it cannot be reported from a model holding one reconciled value per arrangement.

**Run 5 makes this worse, not better.** Run 4 could at least state the two rates; the
re-extraction under `f83f31102d3f` moved the comparison further from the report's own tables. See
[7.6](#76--stop-the-extraction-merging-two-tables-into-one-value-new-from-73) for the fix — charge
as a list, one entry per place the report states it, each carrying its page. It is still blocked on
the same coordination question as 4.3 and 4.4, and that block is now costing five findings.

**Also here: the £268.94 is attached to the wrong plan.** CHK-009/G9.2 reports Standard Life's
+0.26% / £268.94 row as *Zurich's*, then records Standard Life as having no differential. F6.3 and
F9.3 both turn on that one sentence. Worth checking whether 7.2's sibling-pairing is keying on
position rather than `arrangementId`.

### 8.3 — Fix the citation verifier's false rejections — **DONE, measured 34 → 19**

**The 11% target broke: unverified quotes went 11% → 18%, groups flagged 42% → 52%.**

Inspection of the 61 rejections shows the largest single category is **table quotations differing
only in internal spacing** from the passage they came from. CHK-005/G5.2 flags

```
"Total Net Monthly Income  |  £1,430.00"     ← rejected as untraceable
table: Total Net Monthly Income  |  £1,430.00 ← cited two lines below
```

That is a matcher gap, not a model inventing quotes — and every one of those groups was downgraded
for it. [3.2](#32--fold-table-punctuation-in-normalise) already folds pipes and separator rows;
extend it to collapse runs of whitespace before comparing.

**Note what this costs beyond the metric.** 0.3 exists so a pass on an untraceable quote is
downgraded. A verifier that rejects correct citations is therefore *changing verdicts* on the
strength of a spacing difference.

> **Built, and the premise above is wrong.** `Normalise` already collapsed whitespace, so the
> item as written would have changed nothing. Replaying Run 5's 34 quote failures through the
> matcher named the actual causes:
>
> | Cause | Fix |
> | --- | --- |
> | `U+F0B7` — a Wingdings bullet the PDF converter leaves in the evidence, absent from every quotation of it | fold the Private Use Area to space |
> | Markdown emphasis (`**bold**`) in the evidence, not in the quote | fold `*` and `_` |
> | Sentence punctuation — 19 occurrences of a full stop, comma or colon present on one side only | fold, **but only where it is not between two digits** |
> | `CollapseSeparatorRuns` emitting a double space, so a folded table row could not match itself | collapse again after folding |
>
> **34 → 19 failures**, measured by replaying the same log through the shipped code. The digit
> rule is what keeps this safe: `£1,430.00` still differs from `£143000`, `0.18%` from `018%`,
> `rating 5.0` from `rating 50`, and the altered quotation the class exists for — "a Risk rating
> of 5" against evidence reading 6 — still fails. Tests pin all four.
>
> **One regression, caught by measuring rather than by review.** Folding `.` away also ate the
> dots of an ellipsis, breaking the elision path that lets a model quote a long sentence without
> its middle. Dots adjacent to dots are now exempt.

### 8.4 — Work out why the `Residency` hint did nothing *(F1.6, F5.4)* — **DONE (instrumented)**

The same commit, the same mechanism, two sections: `Total Monthly Disposable Income` moved four
findings and `Residency` moved zero. The words *Tenant*, *Residency* and *main residence* do not
appear in the output at all, so this is not a group receiving the section and ignoring it — the
section never arrived.

Two candidates, and they are distinguishable from the indexing output:

1. **The heading does not exist as a chunk boundary.** `Residency Status: Tenant – private` may sit
   inside a larger personal-details table whose heading is something else, in which case the hint
   matches nothing and silently does nothing.
2. **The hint matched and lost its slot.** The per-category floor reserves one slot; if two hints
   compete in the same group, one is evicted with no record.

**A hint that matches nothing should say so.** Whichever cause it is, the run gave no signal that
a plan asked for a section that does not exist — add it to the plan lint (L4: every
`evidenceSections` entry must match a heading in the indexed corpus) so this fails at build time
rather than being discovered by scoring a run.

> **Built as instrumentation, not as a lint, and the reason matters.** `CheckPlanLint` runs over
> the plans alone and has no corpus to check a heading against; whether `Residency` exists is a
> fact about the converted documents for *this case*, so a build-time rule would either need the
> corpus or would be checking nothing. `CheckPlanRunner.UnmatchedSections` therefore reports, per
> run, every hint that matched none of the passages the searches returned:
>
> ```
> Section hints matching nothing: 3 — G1.1: Residency; G1.1: Contact Address; G8.2: Residency.
> ```
>
> Measured over the **candidates**, not the ranked pack, which is what separates the two causes
> above: a hint reported here matched nothing at all (cause 1), and a hint that matched and then
> lost its slot (cause 2) stays silent because it needs a different answer. That distinction is
> the whole point of the item, and the next run answers it without another log-reading exercise.

### 8.5 — Do the controlled re-run (7.4), which is now overdue — **NOT DONE (needs a run)**

**Five runs, and not one has moved a single variable.** Run 5 moved three: Stage 7 code, the plan
hints, and a re-extracted canonical model — *plus* the front-end decoupling refactor, which claimed
to change no behaviour and cannot be shown to have kept that promise from this run.

The evidence that the refactor is not implicated is circumstantial but reasonable:

- **Retrieval is byte-stable across four runs** — 165 searches, 2,614 passages, in Runs 2, 3, 4 and
  5 alike. A change to service construction or lifetimes altering behaviour would be unlikely to
  leave those identical.
- **The embeddings line is unchanged at £0.0050 / 2,007 tokens** — the specific number that would
  have broken had the search-service factory been wired to the container's embedding generator
  rather than the caller's.
- **Every recall change traces to a plan hint or the extraction**, both data rather than code the
  refactor touched.

That is an argument, not a measurement. Hold the plans, the settings, the canonical model and the
code, and re-run. It is the cheapest item on this list and the only one that makes the next
comparison mean anything.

### 8.6 — Report all three counts, and stop leading with recall — **DONE**

Recall has now read **44% → 67% → 67% → 67% → 67%**. In Run 5 that flat line spans a run in which
ten of thirty-six verdicts moved and the missed count fell by three-quarters.

The rubric already requires all three counts and the noise floor beside them
([§5](scoring-rubric.md)). Apply the same rule to this plan's exit criteria: **promote missed-rate
to the headline measure** — it is what the benchmark says to gate on, it is the count that moved,
and it is the one a reviewer actually bears the cost of.

---

## Exit criteria

Measured across all five generated runs —
[Run 2](Runtime-Logs/latest/Run-2/run-analysis.md),
[Run 3](Runtime-Logs/latest/Run-3/run-analysis.md),
[Run 4](Runtime-Logs/latest/Run-4/run-analysis.md),
[Run 5](Runtime-Logs/latest/Run-5/run-analysis.md).

| After | Measure | Run 1 | Target | Run 2 | Run 3 | Run 4 | **Run 5** | |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Stage 0 | Runs mis-reported as generated | 8 of 12+ | 0 | 0 | 0 | 0 | **0** | ✅ |
| Stage 0 | Plan copies disagreeing | 4 of 9 stale | 1 source | 1 | 1 | 1 | **1** | ✅ |
| Stage 1 | L1 violations | 17 of 60 | 0, enforced | 0 | 0 | 0 | **0** | ✅ |
| Stage 1/5 | Checks reaching the Fact Find | 5/10 | every check | 7/10 | 10/10 | 10/10 | **10/10** | ✅ |
| Stage 2 | Extraction report reaching assessors | 45% | 100% | 100% | 100% | **100%** | ✅ |
| Stage 6 | **Extraction sections succeeding** | — | all | — | 4 of 12 | 12 of 12 | **12 of 12** | ✅ |
| **Stage 7** | **Missed** | 8 | ≤3 | 6 | 5 | 8 | **2** | ✅ |
| Stage 3/5 | **Unverified quote rate** | 36% | <12% | 32% | 14% | 11% ✅ | **18%** | ❌ |
| Stage 2 | Findings storing the model's requirement text | 25 of 60 | 0 | 0 | 2 | 3 | **1 of 60** | ⚠️ |
| Stage 2 | Groups vetoed by `comparisonPerformed` | 20 | 0 | 17 | 16 | 18 | **15** | ⚠️ |
| **Stage 8** | **Groups passing while naming discrepancies** | — | 0 | 12 | 15 | 13 | **9 of 60** | ❌ new |
| Stage 3/5 | Groups flagged | 70% | <25% | 70% | 45% | 42% | **52%** | ❌ |
| Stage 4 | **Benchmark recall (caught)** | 16/36 (44%) | ≥26/36 (72%) | 24/36 | 24/36 | 24/36 | **24/36 (67%)** | ❌ |

**Seven met, and the seventh is the first findings criterion ever to go green.**

**Missed fell 8 → 2**, the lowest of the series and inside target for the first time. That is the
measure [the benchmark says to gate on](expected-results-benchmark.md), and
[8.6](#86--report-all-three-counts-and-stop-leading-with-recall--done) promotes it to the headline
accordingly — it is listed above recall from this run on.

**Recall is 24 of 36 for the fourth consecutive run, and means something different this time.**
Runs 2–4 caught an identical set. Run 5 turned five of them over in each direction, so the flat
line spans ten changed verdicts. Reading 67% → 67% as "nothing happened" would be wrong in both
directions: Stage 7.1 worked, and five findings regressed at the same time.

**Two criteria went backwards**, both in Stage 3's citation work, and
[8.3](#83--fix-the-citation-verifiers-false-rejections--done-measured-34--19) shows the largest cause is the verifier
rejecting correct table quotations over whitespace — a matcher defect, not a trustworthiness
regression, but it downgrades real verdicts and so has to be fixed rather than explained away.

> **Read recall against the noise floor.** Two scorers hand-adjudicating the *same 60 responses*
> produced 39% and 44%, so **±2 findings is the noise floor**. The 23-point gain is well outside
> it. But F6.2 moved from caught to partial between two runs whose evidence for that group did not
> change at all, which is the noise floor doing exactly what it does.
> [scoring-rubric.md](scoring-rubric.md) writes down the rules that settle those cases, and Run 3
> was the first run scored under it. **24 → 24 therefore means no measured change, not no change** —
> and it is why Stage 6 is argued from the coverage and citation measurements rather than from the
> recall figure.

---

## What was not done, and why

Three items remain unbuilt. Each is blocked on something a code change here cannot supply.

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

**Runs 3 and 4 settle the priority, and the reason changed between them.** After Run 3 the argument
was that the highest-scoring passage of a category is an empty form, so a floor would keep the wrong
thing. Run 4 shows that was too narrow: of eleven Fact Find passages admitted, **exactly one is an
empty form** and the rest are filled tables about the wrong subject. A relevance floor cannot tell
"filled and relevant" from "filled and irrelevant" any more than content density could.
**Do [7.1](#71--let-a-query-group-target-a-document-section-not-just-a-category) first.** A floor
calibrated before targeting works at section level would be calibrated against the wrong
distribution either way.

**4.4, extraction half.** The assessment-side detection is built: `DerivedFigures` now reports
when one arrangement's charge is recorded at two different percentages, which is the route to
F7.1. Feeding repeated-key candidates into the `extractionReport` pass is the other half, sits
in the extraction pipeline, and is the same coordination question as 4.3.

**6.4 has since closed, and it is the one clean success of the last two stages.** The cause was the
schema's enums stripping every charge line's `basis`; removing them took extraction from 4 of 12
sections succeeding to 12 of 12, and `£186.19` now exists in the model. **It still did not produce
the finding**, because the extraction writes the figure in a third shape `DerivedFigures` does not
read — [7.2](#72--read-the-third-charge-shape-in-derivedfigures) — and because it pairs it with
0.52% where the benchmark reads 0.18%, which [7.3](#73--adjudicate-18619-018-or-052) has to settle
before the arithmetic can be trusted.

**7.1 landed and is the one item that moved findings.** Section hints reached four findings that
three prior stages had not — see [Stage 8](#stage-8--what-run-5-says-to-do-next). The half that
failed is instructive: the same commit's `Residency` hints moved nothing, so the mechanism is not
general and [8.4](#84--work-out-why-the-residency-hint-did-nothing-f16-f54--done-instrumented) has to find out why
before it is trusted on the next section.

**7.6 is now the largest unbuilt item and is costing five findings**, not two as estimated when it
was written. F7.1, F7.2, F6.3, F9.3 and F9.4 are all partials in Run 5 for the same reason: the
run holds both figures and compares them against a provider document rather than the report's
other page. It remains blocked on the same schema coordination as 4.3 and 4.4, and that block is
no longer cheap.

### What five runs have and have not settled

- **Run 2 moved four variables at once.** Its 23-point recall gain is real and attributable to
  none of them by measurement.
- **Runs 3 and 4 each held three of four** — plans, search limit, extraction cap — and re-extracted
  the canonical model. Run 4 had no choice: Stage 6's change *was* an extraction change. That
  excuse is now spent, and [7.4](#74--re-run-holding-the-extraction) is the first run that can hold
  everything.
- **Each stage has found a defect in the previous stage's output.** Run 2 found
  `CrossGroupContradictions` splitting on decimal points and `DerivedFigures` reading the wrong
  charge lines; Run 3 found the pair-selection heuristic choosing restatements over the one real
  contradiction available to it; Run 4 found `DerivedFigures` missing the figure a third time and
  showed Stage 6's own premise to be too narrow. **Shipping a component is not the same as it
  working, and only a run tells the difference.**
- **Three stages of machinery did not move recall, and the fourth moved the findings without
  moving the number.** 44% → 67% → 67% → 67% → 67%. Stages 5 and 6 improved coverage, extraction
  and citation trust and reached no additional finding. **Stage 7.1 reached four** — and five
  others regressed in the same run, so the headline is identical for the fourth time. The
  diagnosis behind Stage 7 was right: the assessor was being handed the right page and not asked
  the question it answers.
- **Recall has been measuring two different failures as one.** A finding the pipeline never found
  and a finding it found and discarded by its own outcome are indistinguishable in the printed
  report, and [8.1](#81--surface-findings-a-group-discards-new--s1--done-steps-12-step-3-held) shows the
  second has been happening 9–15 times per run since Run 2. **Every recall figure in this document
  is an underestimate by an unknown amount**, and step 1 of 8.1 is what bounds it.
- **Run 5 is the first run where both logs are genuine.** Run 3's extraction was a 2.0 s replay and
  Run 4's checks were a 24,554 tok/s replay; Run 5 measured 440 s and 563 tok/s. Stage 0's guard is
  the reason any of that is known.
- **Still no controlled run.** Run 5 moved three variables plus a refactor claiming to preserve
  behaviour. [8.5](#85--do-the-controlled-re-run-74-which-is-now-overdue--not-done-needs-a-run) is the cheapest item on
  the list and the only one that makes the next comparison mean anything.

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
