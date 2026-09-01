# Findings improvement plan — generic across cases

Written from the scored runs of Test Case 2 ([Run 1](Test-Case%202/Run-1/run-analysis.md),
[Run 2](Test-Case%202/Run-2/run-analysis.md)) and the benchmarks for
[Test Case 2](Test-Case%202/expected-results-benchmark.md),
[Test Case 3](Test-case%203/expected-results-benchmark.md) and
[Test Case 4](Test-Case%204/expected-results-benchmark.md).

It supersedes §10 of the Run 2 analysis, which listed the same levers without the constraint that
governs this document.

---

## The constraint

**Every change here must be a rule about a *kind* of evidence or a *shape* of comparison, never
about a fact of a case.** The runs were scored against one client file; the fixes must work on a
file nobody has read yet.

The test each item has to pass is stated for it explicitly, and it is always the same test:

> Would this change do anything at all on a case it was not written for?

A remedy that fails that test is overfitting wearing the clothes of a fix. Two candidates from the
Run 2 analysis fail it and are excluded — see [What is deliberately not here](#what-is-deliberately-not-here).

---

## New evidence: the measurement floor

The second Run 2 checks log added to the repository is not a variant. It is a **repeat of the same
run at the same configuration** — same assessor, same plans hash `09cf0a31b103`, same model hash
`9d8bcf0bc622`, same `24/group (reserving 1/section, 1/category)`, same 78 groups across the same
nine checks, both with CHK-009 skipped before any group ran.

Diffed group by group:

| | `17:17:25` | `18:31:10` | Delta |
| --- | --- | --- | --- |
| Groups assessed | 78 | 78 | — |
| **Outcome flips** | — | — | **3 of 78 (3.8%)** |
| **Severity flips** | — | — | **12 of 69 (17.4%)** |
| Potential Concern | 69 | 70 | +1 |
| High / Moderate / Low | 10 / 49 / 10 | 12 / 46 / 12 | polarising |

The three outcome flips: `CHK-007/G7.2` NoIssue → PotentialConcern, `CHK-010/G10.3` NoIssue →
PotentialConcern, `CHK-008/G8.3` PotentialConcern → NoIssue. Twelve groups changed severity,
including `G7.3` Moderate → High and `G7.8` Moderate → Low — the two halves of the largest known
false positive moving in opposite directions between two runs of the same prompt.

The run configuration line says why: **`seed not pinned`.**

This has a hard consequence for every other item in this plan. **A single-run delta of three
findings is inside the noise.** The Run 1 → Run 2 comparison survives it — 23 → 28 caught with a
byte-identical retrieval extract is a real effect — but nothing smaller does, and no item below can
be accepted or rejected on one run each side. That is why Tier 0 goes first and is not optional.

---

## The tiers

Status as implemented on 2026-08-28, against `a28230d`. The suite goes from 827 tests to **894**,
all passing; three items landed differently from the plan and are marked, with the reasons in the
[postscript](#postscript-where-the-plan-was-wrong).

| Tier | # | Item | Kind | Class | Status |
| :-: | :-: | --- | --- | --- | --- |
| **0** | 0.1 | Pin the seed; make the repeat run the unit of measurement | config + harness | all measurement | **changed** — the seed is already on and this route refuses it; the run now says so |
| | 0.2 | Complete the run fingerprint | code | reproducibility | **done** |
| | 0.3 | Score a run against a benchmark mechanically | harness | all measurement | **done** |
| **1** | 1.1 | An applicability rule may narrow a check, never overrule an affirmative trigger | code | **A** — silent N/A | **done** |
| | 1.2 | Lint applicability values against the schema's closed vocabulary | code | **A** | **done** — found a second instance |
| | 1.3 | Report skipped checks separately from cleared checks, everywhere | code | **A** | **done** |
| **2** | 2.1 | The group's top-scoring passage can never be evicted | code | **B** — unread evidence | **done** |
| | 2.2 | Collapse near-duplicate chunks, not just identical ones | code | **B** | **done** |
| | 2.3 | A table's caption and header row travel with every window of it | code | **C** + largest FP | **done** |
| | 2.4 | Validate section hints against the index, at load | code + lint | **B** | **already existed** — see postscript |
| | 2.5 | Promote pseudo-headings and table captions in conversion | converter | **B** | **done** |
| **3** | 3.1 | Absence discipline | prompt | **B** | **done** — standing rules |
| | 3.2 | Establish the basis before reporting a difference | prompt | largest FP | **done** — group prompt |
| | 3.3 | An identical result on both sides is a question, not a pass | prompt | **C** | **done** — group prompt |
| | 3.4 | Never withhold a finding for uncertainty about its severity | prompt | model-swap regressions | **done** — merged into an existing rule |
| | 3.5 | An extraction gap is a missing input, not a report defect | prompt | FP | **done** — group prompt |
| **4** | 4.1 | Cross-group presence/absence contradictions | code | **B** diagnostic | **done** |
| | 4.2 | Benchmark governance: candidates before promotion | process | benchmark integrity | **done** |

Classes A / B / C are as defined in [Run 2 §6](Test-Case%202/Run-2/run-analysis.md#6-what-neither-model-reached):
A is the plan skipping a check, B is evidence in the pack that no model read, C is reasoning that
lands inverted.

---

## Tier 0 — measurement

Nothing below Tier 0 can be evaluated without it. Two identical runs disagree on 3 outcomes and 12
severities; every item in Tiers 1–3 claims an effect of comparable size or smaller.

### 0.1 Pin the seed, and make the repeat run the unit

Two changes, one config and one convention.

**Config.** The run configuration line already prints `seed not pinned`, so the field exists and is
being reported as absent. Pin it, and record the value in the header beside the model name. A pinned
seed does not make the assessor deterministic — the provider does not guarantee it — but it removes
the one source of variance that is free to remove.

**Convention.** A change is accepted on **two runs each side, scored separately**, and reported as a
range rather than a point. An item that moves the count by fewer findings than the observed
run-to-run spread has not been shown to do anything. The spread is currently 3 outcomes; re-measure
it after the seed is pinned rather than assuming it shrinks.

*Generic by construction:* it is a measurement protocol; it encodes no case content at all.

### 0.2 Complete the run fingerprint

[RunFingerprint.cs](src/AiPromptEvaluator.Core/RunFingerprint.cs) records the plans hash and the
model hash. It does not record `CoreQueriesOnly`, `MaxPassagesPerGroup`,
`ReservedSlotsPerTargetedCategory`, `ReservedSlotsPerDeclaredSection`, or the assessor and extractor
model identifiers separately.

Each of those changes the findings. `CoreQueriesOnly` alone decides whether a group's supplementary
queries run at all
([CheckPlanRunner.cs:341](src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs#L341),
[:574](src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs#L574)) — a run with it on
and a run with it off can produce different packs and are currently indistinguishable in the
archive.

Add every setting that reaches retrieval or the prompt to the fingerprint, and print it in the
`[RUN CONFIGURATION]` block. The block already prints `top 8/search, 24/group (reserving
1/section, 1/category)` in prose; the fingerprint should carry the same values in a form that can be
compared between runs.

*Generic by construction:* a fingerprint of the configuration, independent of the case being run.

### 0.3 Score a run against a benchmark mechanically

Four benchmarks now exist in the same format — a table of finding ids (`F<check>.<n>`), a severity,
and a statement of the defect. Scoring is currently a manual read of a 65,000-line log against a
42KB markdown file, which is why it has been done for one case out of four.

Build the smallest thing that works: a benchmark front-matter block mapping each finding id to the
check (and, where known, the group) that should raise it, and a scorer that reports **caught /
partial / missed** by matching the run's findings to those ids, with the matched text printed for
review. The judgement of whether a finding matches stays human; the bookkeeping does not.

*Generic by construction:* the mapping lives in each case's benchmark, not in the scorer. A new case
gets a benchmark and is scorable the same day.

---

## Tier 1 — applicability and silent skips

Six of the nine findings both Test Case 2 runs missed are one defect: a check that never ran, at
zero cost, reported as clear.

### 1.1 An applicability rule may narrow a check, never overrule an affirmative trigger

**The defect class.** `ProbeTriggerAsync` ANDs the canonical model's own trigger boolean with the
plan's applicability rules
([CheckPlanRunner.cs:346-357](src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs#L346)).
The comment defends the AND — *"a rule can only ever narrow, never rescue a check whose trigger
field says no"* — and that direction is right. The other direction is not defended and is what
fires: the trigger field says **yes**, one applicability rule enumerates a closed vocabulary and
omits a legitimate member of it, and the check is skipped.

Observed in both Test Case 2 runs, both extractors, and both are correct: `adviceAction` was
`RetainAndSwitchFunds`, a documented value of the schema's own list
([canonical-suitability-model.schema.json:2120](docs/Implementation/canonical-suitability-model.schema.json#L2120)),
absent from CHK-009's `adviceActions`
([CHK-009.query-plan.json:24-30](docs/Implementation/check-plan/CHK-009.query-plan.json#L24)) while
`hasReplacementOrSwitch` was `true`.

**The change.** Where the trigger field is affirmatively `true` and an applicability rule fails,
treat the rule the way `EvaluateApplicability` already treats an *undetermined* rule
([:427-460](src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs#L427)): **the check
runs, and the detail says which rule disagreed and what it found.** The precedent is in the file and
the reasoning is already written there —

> *"The check runs instead of being skipped, which is the safe direction: a check that need not have
> run is visible in the output and costs one call, and one that silently did not run costs a finding
> nobody can see missing."*

That is the trigger-contradiction path. This item applies the same rule one line further down.

Adding `RetainAndSwitchFunds` to the list is the narrow fix and should also be done — it is a plain
omission — but on its own it fixes one value of one enum in one plan, and the next omitted value
fails silently again.

*Generic by construction:* it is a rule about the relationship between two signals, not about
pensions, switches, or any value. A check whose trigger says it applies is never skipped without a
visible reason, in any case, for any check.

### 1.2 Lint applicability values against the schema's closed vocabulary

The extractor prompt already treats a description reading *"One of: A, B, C."* as a closed
vocabulary. A plan's applicability rule enumerating a subset of that vocabulary is asserting that the
omitted members do not trigger the check — which may be right, but is currently invisible.

Add a rule to [CheckPlanLint.cs](src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanLint.cs):
for every applicability rule whose `canonicalPaths` resolve to a schema property with a closed
vocabulary, report the values the rule **excludes**. Not an error — an exclusion can be deliberate —
but printed at load, so an omission is a decision somebody made rather than one nobody noticed.

*Generic by construction:* it reads the schema and the plan and compares them. It knows nothing about
any case.

### 1.3 Report skipped checks separately from cleared checks

A check that returned N/A on applicability currently appears alongside checks that ran and found
nothing. The file already records what that cost once, for a different trigger — CHK-005 skipped
twice while a £110,185 transfer sat in the case, *"and both runs printed it under CHECKS CLEARED —
the check that did not run looked exactly like one that passed."*

Give N/A its own section in the console output, the HTML report and the checks log, with the
applicability detail printed under it. When 1.1 lands, add the warning line for a check that ran
*despite* a failed rule, so the reviewer sees both directions.

*Generic by construction:* an output-formatting rule.

---

## Tier 2 — pack construction

This is the largest fixable lever and the one the model comparison isolated. Retrieval between Run 1
and Run 2 was **byte-identical, 39,436 lines, two differing** — so every pack defect is
model-independent, survives a 3.3× spend increase, and will survive the next one.

### 2.1 The group's top-scoring passage can never be evicted

**The defect class.** `Rank` reserves slots for declared sections and declared categories and then
fills the remainder by score
([CheckPlanRunner.cs:673-800](src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs#L673)).
The reservations are sound and the file explains why each exists. But nothing reserves a slot for the
**best-scoring passage the group's own searches returned**, and when the reservations plus a
near-duplicate cluster fill the cap, it can be evicted.

Observed identically in both runs: in one group the top passage at score `0.737` was dropped while
eight near-identical chunks at `0.627`, all from one query, were kept.

**The change.** Take the top *n* by score first — `n` = 1 or 2, configurable, default 1 — before the
section and category floors run. The cap is 24; a group can afford one guaranteed slot for its own
best answer.

*Generic by construction:* score is computed per query per case. The rule names no document, no
category, and no section.

### 2.2 Collapse near-duplicate chunks, not just identical ones

`DeduplicationKey` is the document name and the passage text verbatim
([:640](src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs#L640)) — deliberately, and
the reasoning for using the text rather than its hash is good. But it only catches passages that are
*character-identical*. Overlapping chunk windows over the same table produce eight passages that are
95% the same text and eight distinct keys, and they consume a third of the cap.

Add a second pass after exact dedup: where two passages from the same document overlap above a
threshold, keep the higher-scoring one and record the drop. Shingle overlap is enough; this does not
need to be clever, it needs to be stable and logged.

*Generic by construction:* it operates on chunk text similarity, which is a property of the indexing
window, not of any case.

### 2.3 A table's caption and header row travel with every window of it

**The defect class, and it is the one worth the most.** Where the same quantity is tabulated twice on
two different bases — before and after a charge, gross and net, two retirement ages — the caption
above the table is the only thing that distinguishes them, and it is the first thing a window drops.

`Window` already centres on a declared section rather than truncating from the start
([:1303](src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs#L1303)), which was the
right fix for a different problem and is documented as such. It does not carry the caption or the
column headers with the centred window, so a passage delivered mid-table arrives as a grid of
numbers with no statement of what they are on.

Both Test Case 2 runs failed on exactly this. Run 2 got closer and landed inverted, describing a
pre-charge table as *"the post-adviser-fee comparison"* — and the same confusion is the largest
surviving false positive, presented as ten High-severity contradictions in one group while another
group of the same check applied the guard and dropped to Low.

**The change.** When rendering a windowed passage, detect the enclosing table and prepend its caption
line and header row above the window, marked as context rather than as the passage body. The
assessor is already told to cite table rows via `cells` rather than prose, so it is already being
asked to read tables structurally; give it the structure.

*Generic by construction:* every case file in this domain contains illustration tables, comparison
tables and charge tables. The rule is about table rendering, not about any table.

### 2.4 Validate section hints against the index, at load

`SectionReach` already separates a hint that **matched nothing** from one that **was evicted**, and
the file records that conflating them made two analyses believe a retrieval defect was fixed when it
was not. That diagnostic is after the fact: it reports per run, after the model spend.

The same 14 hints matched nothing in both Test Case 2 runs — identical, because hints are plan data.

**The change.** At plan load, or as a lint pass over an indexed case, resolve every declared section
hint against the index and fail loudly for the ones that match no chunk anywhere. A hint that matches
nothing is a typo or a wording that does not survive conversion, and both are fixable before a run
rather than after it.

*Generic by construction:* it checks a plan against whatever corpus is loaded. It runs for any case.

### 2.5 Promote pseudo-headings and table captions in conversion

The reason several hints match nothing is upstream of the plan. The converter emits a document's
appendix heading as bold body text — `**Appendix**` — rather than as a heading, and emits table
captions as loose paragraphs. A hint naming that section is correct about the document and wrong
about the artefact.

Fix it in conversion: a short bold-only line immediately followed by a table or a heading-shaped
block becomes a heading of the appropriate level, and a caption line adjacent to a table is attached
to it. Then re-convert every case and re-run 2.4 to see which hints now resolve.

*Generic by construction:* a converter rule keyed on document structure. It improves every document
processed afterwards, including ones not yet supplied.

---

## Tier 3 — assessor prompt

Five clauses for [Prompts.cs](src/AiPromptEvaluator.Core/Constants/Prompts.cs). The house style
there is that each rule earned its place from an observed failure and is phrased as a general rule;
these follow it. **None names a document type, a product, a figure or a check.**

### 3.1 Absence discipline

> Before you state that the file holds no document, record or assessment of some kind, look through
> the passages for one. Where a passage of that kind is present, you may not report it as absent:
> say what it contains, and if what it contains does not answer the requirement, say that instead —
> "the questionnaire is on file and does not record a rating" is a different finding from "there is
> no questionnaire", and only one of them is available to you when the document is in front of you.

**Why it is needed.** The sharpest result in the whole comparison: one group reported *"the file
holds no CFL questionnaire responses"* about a passage ranked **sixth of twenty-four in its own
pack**, while another group of the same check said *"the questionnaire responses in [P17] are on
file."* Two models, one at 3.3× the cost, in identical packs.

Prompt adherence was **49 of 78 wrong in Run 1 and 0 of 78 in Run 2**, and the miss persisted — so
requirement confusion is excluded as the cause and this clause is aimed at what is left.

*Generic by construction:* "a document of the kind you are about to call absent" is a shape, not a
document.

### 3.2 Establish the basis before reporting a difference

> Where the same quantity appears twice with different values, establish what each figure is *on*
> before you call them contradictory. Two figures computed on different bases — one including a
> charge, a tax or a fee the other excludes, one at a different date, age or term — are not two
> answers to one question. Name both bases in "analysis". Where the report presents them without
> saying they differ, the finding is that the basis is undisclosed; it is not one contradiction per
> row of the table.

**Why it is needed.** The largest false positive in both runs: ten High-severity "contradictions"
that are one undisclosed basis, tabulated ten times. The same run applied the guard correctly one
group later and dropped to Low — so the guard works and the failure is that ten rows of one table
read as ten independent findings.

This clause and item 2.3 are the two halves of the same fix: 2.3 gives the assessor the caption,
3.2 tells it what to do with it.

*Generic by construction:* it is a rule about bases and tabulation.

### 3.3 An identical result on both sides is a question, not a pass

> Where a comparison between an existing option and a recommended one shows no difference — or a
> difference too small to matter — ask what assumption produced that. A charge, a rate or a term
> applied to one side and not the other will make two unlike things look alike. Say which
> assumptions were applied to each side, and where the pack does not let you establish that, say so
> in "missingInputs".

**Why it is needed.** Neither run asked why two columns of a comparison were identical, and the
answer — an ongoing charge applied to a side that does not pay it — was stated in three separate
retrieved documents. This is the whole of Class C.

*Generic by construction:* "two sides of a comparison agreeing when they should not" is a shape that
recurs in every switch, replacement and cost comparison in the domain.

### 3.4 Never withhold a finding for uncertainty about its severity

> Raise what the evidence supports, at the severity it supports. Uncertainty about how serious
> something is belongs in "severity" and "analysis", not in the decision whether to report it. A
> difference you are unsure how to weigh is a Low finding, not a silence.

**Why it is needed.** The model swap **lost three findings the cheaper model caught**, one of them a
headline defect of the case, by being better calibrated and raising less. Better calibration should
buy fewer false positives, not fewer findings; this clause is what separates the two.

*Generic by construction:* a rule about the relationship between reporting and severity.

### 3.5 An extraction gap is a missing input, not a report defect

> Where a value is absent from the canonical model, that is either the report being silent or the
> extraction having failed, and the two are different findings. Do not report an extraction failure
> as something the report did wrong. Name it in "missingInputs" and set "comparisonPerformed" to
> false.

The prompt already draws this distinction in `BuildGroupPrompt`'s absent-fragment note
([:1069-1074](src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs#L1069)); it is not in
the standing rules, and one group spent a finding slot on *"the canonical extraction failed to
capture the label"* — correctly attributed, and still occupying a slot a reviewer has to read.

*Generic by construction:* it is about the two channels of the pipeline.

---

## Tier 4 — cross-group and process

### 4.1 Cross-group presence/absence contradictions

[CrossGroupContradictions.cs](src/AiPromptEvaluator.Core/Services/Assessment/CrossGroupContradictions.cs)
already finds the same quantity described differently by two groups, costs no retrieval and no model
call, and exists precisely because *"a contradiction whose two halves fall in different groups is
structurally unreachable however good retrieval becomes."*

Extend it to a second shape: **one group asserting a document or record is on file while another
asserts it is absent.** That is the machine-detectable signature of the Class B failure, it was
present in both Test Case 2 runs, and unlike item 3.1 it does not depend on the assessor taking
instruction. Report it as a run diagnostic beside the existing ones.

*Generic by construction:* it reads finished findings for a contradiction shape.

### 4.2 Benchmark governance: candidates before promotion

Run 2 surfaced five observations that are legitimate and absent from the Test Case 2 benchmark, and
one benchmark entry it showed to be too generous. Folding run output straight into a benchmark makes
the benchmark no longer independent of the system it scores.

The protocol: run-surfaced observations go into a `candidate-findings.md` beside the benchmark;
promotion into the benchmark requires verification **against the source documents**, recorded with
the document and page. The Test Case 2 candidates and the one correction are the first entries.

*Generic by construction:* a process rule for every case.

---

## Sequencing

```
0.1 seed + repeat-run protocol ─┐
0.2 fingerprint                 ├─► baseline (2 runs, scored)
0.3 scoring harness            ─┘
        │
        ├─► Tier 1 (1.1, 1.2, 1.3)   no model spend, effect is binary — a check runs or it does not
        │
        ├─► Tier 2 (2.5 → 2.4 → 2.1, 2.2, 2.3)   converter first: 2.4 is only meaningful after it
        │
        ├─► Tier 3 (3.1–3.5)   after Tier 2, or the prompt is being tuned against a pack that is about to change
        │
        └─► Tier 4 (4.1, 4.2)
```

**Two runs each side at every gate.** Tier 3 in particular must not be measured against a single run:
three of its five clauses target effects smaller than the 3-outcome noise floor measured above.

Tier 2 before Tier 3 is the ordering that matters most. Items 2.3 and 3.2 are one fix in two places,
and tuning the clause against packs that do not yet carry table captions would tune it against the
wrong input.

---

## What is deliberately not here

**Re-running on a stronger assessor.** Done, measured, and it is not the binding constraint. It
bought +11 points of caught-recall and five fewer false positives for 3.3× the spend, and lost three
findings including a headline defect. Of the nine findings both models missed, six were a check that
never ran, one was a rendering defect, and the rest were passages sitting unread in identical packs.
Keep the stronger assessor for the false-positive reduction; do not expect the next upgrade to move
recall.

**Case-specific plan data.** Adding a section hint that names a phrase from one client's fact find,
or a query written around one report's wording, moves that case and does nothing anywhere else. Two
such items appeared in the Run 2 next-steps list and are excluded. Where a hint is genuinely
generic — a section heading the document *format* always carries — it belongs in the plan and should
go in; the test is item 2.4, which will now tell you at load whether it resolves.

**Widening the pack.** Raising `MaxPassagesPerGroup` above 24 is the obvious response to "the
evidence was in the pack and went unread", and it is the wrong one: the evidence was **already in**
the pack, at rank 6 of 24, and two models read past it. More passages is more to read past. Items
2.1, 2.2 and 2.3 make the pack narrower and better ordered instead. If a width change is tried, it
should be *downward*, and measured under 0.1.

---

## Postscript: where the plan was wrong

Three items did not survive contact with the code, and one claim in the evidence behind them was a
misquote. Recorded here rather than edited away, because a plan whose errors are removed reads as
though it had none.

### 0.1 — the seed is already pinned, and this route refuses it

The plan said to pin the seed. `AppSettings.PinSeed` **already defaults to true**; the run header
read `seed not pinned` because the gateway rejects the parameter, which the settings documentation
had said all along. There was nothing to turn on.

So the item became what it should always have been: the fingerprint now prints, beside the sampling
line, what unpinned sampling costs — *"3 of 78 outcomes and 12 of 69 severities moved between two
runs with identical fingerprints… score two runs each side"* — where a reviewer diffing two runs
will see it. The measurement protocol stands unchanged; only the belief that a config flag could
avoid it has gone.

### 2.4 — already built, and better than described

`RetrievalDryRun` executes every plan's retrieval with no model call, and `SectionReach` already
separates a hint that matched nothing from one that matched and was evicted — a distinction the
plan did not know existed and which its own class documentation records two analyses getting
wrong. `SectionHintReachabilityTests` covers the corpus-level half.

Nothing was added. **One thing was fixed**: item 2.2 put a near-duplicate collapse into the real
pack-building path and not into the dry run's, which would have made the dry run a confident report
on a pack no assessor would ever see. Both now take the same steps in the same order, with a
comment at the join saying why they must.

### 3.2, 3.3, 3.5 — in the group prompt, not the standing rules

The standing assessor prompt has a length budget of 5,500 characters and was at 5,401. The test
guarding it says, in terms, that a raise to 6,000 should be paid for by cutting a rule rather than
by widening again.

Two of the five clauses govern how to read a comparison whose sides sit on different bases, and
they cannot bite on a group that has no comparison — so they are printed in that group's *How to
compare* block, next to its guards, and cost the standing prompt nothing. The extraction-gap rule
joined the absent-fragment note where that distinction is already drawn. Only absence discipline
went into the standing rules; the severity clause merged into the existing "do not soften" rule,
which is the same instruction about the same reflex.

That left one raise to pay for, and it was paid: the clause telling the assessor that a plan's
categorisation steer is *"a steer and not a menu"* was already printed verbatim beneath the steer
itself, which is the better placement. Two copies of one rule; the weaker one went.

### The evidence: one quote in the Run 2 analysis was wrong

That analysis attributed *"the file holds no CFL questionnaire responses"* to `G3.2`. **The sentence
is not in the log.** Checking it while building item 4.1 turned up the real pair, at lines 6637 and
8375 of `checks_Test-Case-2_20260828_171725.log`: `G3.10` says *"[P16] shows the raw CFL
questionnaire responses"*, `G3.2` says the questionnaire *"has no recorded responses for the
capacity for loss section"*.

The contradiction is real and the conclusion drawn from it stands. But it is a disagreement about
the questionnaire's **responses**, not about the questionnaire — and that is not a quibble: a
cross-group check keyed on the document alone does not see it. Item 4.1 keys claims on a document
*and the part of it being claimed about*, and the run's own sentences are the test that proves it.
The analysis is corrected in place with a note.

---

## What is now measurable that was not

Item 0.3 parses what the benchmarks already contain — no new authoring — and reading the four of
them produced a question nobody had asked:

| Benchmark | Rows parsed as findings |
| --- | :-: |
| Test Case 1 | 59 |
| Test Case 2 | **75** |
| Test Case 3 | 83 |
| Test Case 4 | 90 |

**The Test Case 2 run analyses scored against 47.** The per-check tables hold 75 material rows —
27 High, 20 Highest, 22 Moderate, 6 Low — plus 15 recorded as *No issue* and one withdrawn; the
scoring block near the end lists 48. Neither is wrong on its face, and a scoring block may be a
deliberate selection. But nothing in the document says which, so two readers reach two
denominators for one run, and every recall percentage quoted anywhere in these analyses depends on
which they picked.

**Settle that before the next run is scored.** One sentence in each benchmark, saying which set a
percentage is a percentage of. It is recorded, with the other benchmark-integrity items, in
[Test Case 2's candidate findings](Test-Case%202/candidate-findings.md).
