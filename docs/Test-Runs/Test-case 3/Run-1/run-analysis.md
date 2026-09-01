# Run analysis — Test Case 3, Run 1

One run of case Test-Case-3 (Anness) on 2026-08-28 at 23:20, scored against
[expected-results-benchmark.md](../expected-results-benchmark.md) across **all 83 material
findings**, not only the set the benchmark nominates for recall.

**This is the first time any case other than Test Case 2 has been scored.** Everything the pipeline
knows about how to find these defects was learned from one client file; this is the first evidence
about whether that generalises.

---

## First, a correction to the framing

The run was described as *"haiku before changes made recently"*. Half of that is right and the half
that is wrong changes what the numbers mean.

```
plans 10@e267d2d36592 · model 76dd761defbd
Settings 84ca882c3ba6 · scope Core and Supplementary queries ·
          trigger probes bypassed — every check assessed
Embeddings … 24/group (reserving 1/section, 1/category, 1/top score; near-duplicate 0.90)
```

**Those are byte-for-byte the plans hash and settings digest of [Test Case 2 Run 4](../../Test-Case%202/Run-4/run-analysis.md).**
This run carries the whole first round of improvements — the top-score reservation, the
near-duplicate collapse, the table-context window, the absence-discipline clause, the cross-group
document check, the CHK-009 plan fix — and runs every check with the trigger probe bypassed.

What it does not carry is the **second** round, committed afterwards: the derived switch comparison,
annualised returns, per-group routing of the extraction's contradictions, and the absence clause's
second half.

So this is not a before/after comparison. It is something more useful:

> **Same pipeline, same model, same configuration, different case.**

Test Case 2 Run 4 is the control. The question this run answers is whether the work generalises.

---

## The answer

| | TC2 Run 4<br>the tuned case | **TC3 Run 1**<br>**the unseen case** |
| --- | :-: | :-: |
| Model | Haiku 4.5 | Haiku 4.5 |
| Plans / settings | `e267d2d36592` / `84ca882c3ba6` | **identical** |
| Checks assessed | 10 of 10 | 10 of 10 |
| Groups | 88 | 88 |
| **Scoring set** | 29 / 6 / 12 of 47 | **20 / 22 / 16 of 58** |
| Caught | **62%** | **34%** |
| **Moderate + Low** | 12 / 4 / 12 of 28 | **6 / 5 / 14 of 25** |
| Caught | 43% | **24%** |
| **All material findings** | 41 / 10 / 24 of 75 | **26 / 27 / 30 of 83** |
| **Caught** | **55%** | **31%** |
| **Missed** | 32% | **36%** |
| Citation trust (untraceable) | 141 | 132 |
| Wrong requirement id | 61 of 88 | **64 of 88** |
| Dead section hints | 14 | **13 — a different 13** |
| Cost (checks) | £2.30 | £2.46 |

**Recall on an unseen case is 31%, against 55% on the case the pipeline was tuned against — and
below the 36% that the same model scored on Test Case 2 before any of this work.**

That is the headline and it should not be softened. The rest of this document is about which parts
of the gap are real, which are artefacts of a harder case, and what it changes.

---

## Contents

1. [How much of the gap is the case being harder](#1-how-much-of-the-gap-is-the-case-being-harder)
2. [Scoring: the 58](#2-scoring-the-58)
3. [Scoring: the 25 Moderate and Low](#3-scoring-the-25-moderate-and-low)
4. [The most severe defect in the case scores zero mentions](#4-the-most-severe-defect-in-the-case-scores-zero-mentions)
5. [What travelled, and what did not](#5-what-travelled-and-what-did-not)
6. [Diagnostics, and one thing that is not a defect](#6-diagnostics-and-one-thing-that-is-not-a-defect)
7. [What this changes](#7-what-this-changes)

---

## 1. How much of the gap is the case being harder

Three honest deductions before the 25-point gap is treated as a verdict.

**The benchmark is bigger and more severe.** 83 material findings against 75, and 22 rated Highest
against Test Case 2's 20 — in a benchmark with more checks reaching Highest. A benchmark with more
severe findings is not automatically harder to score against, but it is not the same instrument.

**Six of Test Case 2's fourteen-finding gain were CHK-009 becoming reachable**, and that is a
one-off: the check now runs on both cases, so it is in the baseline here rather than a gain. Strip
it from the comparison and Test Case 2 Run 4's advantage narrows from +14 to +8 findings.

**Several of Test Case 2's plan-level fixes were literally written from Test Case 2.** The section
hints that moved four findings there name phrases from that client's documents. Here, thirteen
hints match nothing and **they are a different thirteen** — `G2.2: ranked by Maturity Value`,
`G4.1: No understanding / knowledge`, `G6.4: ran…`. A hint tuned to one file is inert on the next,
which is exactly what the lint rule L6 was written to prevent and exactly what this measures.

**What survives those deductions is still a large gap**, and it is concentrated in a way the next
section makes visible: the findings this run misses are not the ones Test Case 2 was missing.

### The benchmark has an arithmetic error

Its scoring block reads *"The **44 material findings** (Highest and High) are the benchmark for
recall"* and then **lists 58**. Highest + High = 22 + 36 = **58**. The 44 is stale.

Scored here against the 58 the benchmark actually lists, which is also what its own severity
distribution supports. **A percentage quoted against 44 would be 32% too generous**, and this is
the second benchmark whose stated denominator needed checking before use.

---

## 2. Scoring: the 58

| ID | Sev | Score | Note |
| --- | :-: | :-: | --- |
| F1.1 inheritance £350,000 vs £150,000 | Highest | **Caught** | *"Report states inheritance £350,000; Fact Find states £150,000"* |
| F1.2 two timings for the inheritance | Highest | Partial | timing noted, not as a contradiction |
| F1.3 employed → self-employed | High | **Caught** | and better: the run adds that she has stopped entirely |
| F1.4 £327,945 of pensions absent from the asset table | High | Partial | flags the table excluding the inheritance and liabilities, not the pensions |
| F1.5 mortgage £750 vs £476 | High | Partial | |
| F1.6 mortgage rate reset 1.24% → 3.8% | High | **Missed** | zero mentions |
| F1.7 six funds of seven, £41,055 vs £46,673 | High | **Caught** | |
| F1.8 Quilter "c. £260,000" vs 33.4% of CEV | High | Partial | value queried, the percentage basis not reached |
| F1.9 the only fact find postdates the report | High | Partial | *"no contemporaneous fact find document"* |
| F1.10 report predates the presentation meeting | High | **Missed** | |
| F2.1 six inconsistent time horizons | Highest | Partial | two of the six |
| F2.2 analysis models a plan the client rejected | Highest | **Missed** | |
| F2.3 retirement income target does not reconcile | High | **Caught** | |
| F2.4 drawdown discussion not carried through | High | Partial | |
| F2.5 *"Nest will become paid up"* contradicted | High | **Caught** | |
| F3.1 capacity for loss rests on the inheritance | Highest | Partial | |
| F3.2 depletion at 81 against life expectancy 87 | Highest | **Caught** | *"concerning in light of the client's stated life expectancy of age 87 … yet the report concludes this scenario is acceptable"* |
| F3.3 two adverse CFL answers unreflected | High | **Missed** | zero mentions |
| F3.4 *"No additional notes were recorded"* throughout | High | **Caught** | |
| F3.5 two conflicting Defaqto analyses of the same plan | High | Partial | |
| F4.1 *"solid understanding"* vs the questionnaire | Highest | Partial | absence of support, not the contradiction |
| F4.2 annuity dropped on preference alone | High | Partial | |
| F4.3 comprehension never tested | High | Partial | |
| F4.4 pension-credit rules never addressed | High | **Missed** | zero mentions |
| F5.1 cashflow conclusion rests on the inheritance | Highest | **Caught** | |
| F5.2 depletion disclosed, no conclusion drawn | Highest | **Caught** | |
| F5.3 no cashflow model output in the file | High | **Missed** | |
| F5.4 three expenditure figures do not reconcile | High | **Caught** | |
| F5.5 mortgage reset absent from the cashflow | High | **Missed** | zero mentions |
| F5.6 £9,838 initial fee taken from the fund | High | **Caught** | |
| F5.7 6.00% growth against a lower switching rate | High | Partial | |
| F6.1 court order records the opposite preference | Highest | **Missed** | **zero mentions — see §4** |
| F6.2 no Quilter evidence of any kind | Highest | Partial | reaches "no fund value or basis stated" |
| F6.3 switching excludes the £260,000 credit | Highest | **Missed** | |
| F6.4 four analyses, the two friendliest reported | Highest | Partial | has the −12.42%, not the four runs |
| F6.5 *"incurred regardless"* | High | **Missed** | zero mentions |
| F6.6 five options, none discussed | High | **Caught** | |
| F6.7 annuity dropped | High | Partial | |
| F7.1 −12.42% and +0.43% confined to the appendix | Highest | Partial | |
| F7.2 every comparison omits the 1% ongoing charge | Highest | **Caught** | *"the report does not quantify or address the ongoing adviser charge of 1.00% per annum"* |
| F7.3 two different Aviva platform charges | High | **Caught** | |
| F7.4 critical yield to 89 against analyses to 87 | High | **Missed** | |
| F7.5 three platform/advice charges unevidenced | High | **Caught** | |
| F7.6 cumulative effect of charges never disclosed | High | **Missed** | |
| F8.1 the −12.42% never appears in the body | Highest | **Caught** | |
| F8.2 depletion at 81 beside "funds would be expected" | Highest | **Caught** | |
| F8.3 switched out of the best-performing plan | High | **Caught** | |
| F8.4 tax consequences never worked | High | Partial | |
| F9.1 court order preference (dup. F6.1) | Highest | **Missed** | |
| F9.2 no comparison for 79% of the money | Highest | Partial | |
| F9.3 four analyses (dup. F6.4) | Highest | Partial | |
| F9.4 charge comparison excludes the 1% (dup. F7.2) | Highest | Partial | |
| F9.5 projections paid up to 87 while drawing from 60 | High | **Missed** | |
| F9.6 Nest paid-up assertion (dup. F2.5) | High | **Caught** | |
| F9.7 True Potential holdings (dup. F1.7) | High | **Missed** | caught under F1.7, not raised in CHK-009 |
| F10.1 no vulnerability assessment of any kind | Highest | **Caught** | |
| F10.2 type 2 diabetic, annuity dropped | High | Partial | health noted, not linked to the annuity |
| F10.3 *"Very little understanding"* vs the assertion | High | **Missed** | zero mentions |

**19 caught, 17 partial, 22 missed.**

---

## 3. Scoring: the 25 Moderate and Low

| ID | Sev | Score | Note |
| --- | :-: | :-: | --- |
| F1.11 IHT narrative written for a married client | Mod | **Missed** | the boilerplate is quoted, not questioned |
| F1.12 PoA section refers to *"your spouse"* | Mod | **Missed** | quoted verbatim without comment |
| F1.13 type 2 diabetic, health described as good | Mod | **Caught** | *"Report states health as 'inferred' good health; Fact Find … record active management of Type 2 diabetes"* |
| F1.14 £230.21 is the prior year's State Pension rate | Mod | **Missed** | figure repeated, vintage not checked |
| F1.15 divorce dates disagree | Low | **Missed** | |
| F1.16 four adviser names across one file | Low | **Missed** | two names appear in quoted evidence, unremarked |
| F2.7 no priority order for the objectives | Low | Partial | |
| F3.6 projections run on £300,000 over 10 years | Mod | **Missed** | |
| F3.7 no asset allocation stated | Mod | Partial | |
| F3.8 *"individually and combined"* claim | Mod | **Missed** | |
| F4.5 questionnaire answers that qualify the experience | Mod | **Missed** | |
| F5.8 emergency fund never assessed | Mod | **Caught** | the £7,000 against £24,000 required |
| F6.8 no fund selection rationale beyond CIP text | Mod | **Caught** | |
| F6.9 stakeholder comparison is boilerplate | Mod | Partial | |
| F7.7 3% fee on an amount not yet known | Mod | Partial | |
| F7.8 transaction costs excluded everywhere | Mod | **Caught** | |
| F7.9 £9,839.36 against £9,838.36 | Low | **Caught** | both figures stated |
| F8.5 no loss scenario for the recommended portfolio | Mod | Partial | |
| F8.6 the ONS longevity data is not used | Mod | **Missed** | zero mentions |
| F8.7 *"no safeguarded benefits"* unevidenced | Mod | **Caught** | *"provides no evidence from Nest documentation … does not state they are absent"* |
| F9.8 *"more flexible retirement options"* | Mod | **Missed** | |
| F9.9 pension sharing charges apportioned 33.4% | Mod | **Missed** | |
| F10.4 PoA for a *"spouse"* of a divorced client | Mod | **Missed** | |
| F10.5 no third party present, report predates the meeting | Mod | **Missed** | |
| F10.6 no trusted contact, no comprehension check | Mod | **Missed** | zero mentions |

**6 caught, 5 partial, 14 missed.**

---

## 4. The most severe defect in the case scores zero mentions

**F6.1 / F9.1.** A certified Pension Sharing Annex — a court order — records at section F that the
client's stated preference is an **internal transfer**. The report recommends an external transfer
and asserts the opposite preference.

The string `internal transfer` appears **nowhere in this run's output**. Not in a finding, not in a
discrepancy, not in a quoted passage.

This is the case's equivalent of Test Case 2's rigged charge comparison, and it fails in the same
shape: a certified document in the file states the opposite of what the report says, and no check
puts the two side by side. The difference is that Test Case 2's version was at least *retrieved* —
four runs quoted the decisive sentence and misread it. Here the sentence does not reach an assessor
at all.

Nine other findings score zero mentions: **F1.6** (a known mortgage rate reset), **F3.3** (two
adverse capacity-for-loss answers), **F4.4** (pension-credit rules), **F5.5** (the same rate reset
in the cashflow), **F6.5** (*"incurred regardless"*), **F8.6**, **F10.3** (*"Very little
understanding"* against the report's *"solid understanding"*), **F10.6**, and **F9.7** in its own
check.

**F10.3 is worth singling out.** `G4.1`'s section hint is literally
`No understanding / knowledge` — the phrase from the questionnaire — and it is on the dead-hint
list. The plan asks for exactly this evidence, by name, and the retrieval does not deliver it. That
is the Test Case 2 Class A failure, reproduced on a case whose hints were written independently.

---

## 5. What travelled, and what did not

Splitting the 58 by what kind of work each finding needs makes the gap legible.

**What travelled well — a stated figure against another stated figure.** F1.1, F1.7, F2.3, F5.4,
F5.6, F7.3, F7.5, F7.9, F8.3 are all caught, and several are caught crisply. Two numbers in the
pack that disagree is the shape this pipeline now handles reliably, on either case.

**What travelled partly — an assertion with no support.** F1.9, F4.1, F4.3, F6.2, F10.2 all land at
Partial with the same wording: the run establishes that nothing corroborates the claim and stops
short of the document that contradicts it. **This is the absence-discipline side effect identified
in [Test Case 2 Run 3 §7](../../Test-Case%202/Run-3/run-analysis.md#7-the-absence-clause-has-a-side-effect),
reproduced on an unseen case** — and it is the strongest independent evidence yet that the fix for
it (the clause's second half, committed after this run) is correctly aimed.

**What did not travel — a document in the file saying the opposite.** F6.1/F9.1, F3.3, F10.3, F2.5's
harder half, F6.5. These need a specific passage to reach a specific group, and on this case it does
not. Every one of them is a retrieval outcome, not a reasoning one.

**What did not travel — one arithmetic step across two tables.** F6.3 (the switching analysis covers
£67,945 of £327,945), F7.4 (89 against 87), F7.6 (£372,000 to £337,000), F9.5. The run has the
figures and does not relate them.

**That last group is what the second round of changes was built for.** The derived switch
comparison and annualised returns, committed after this run, compute exactly this class from the
canonical model. Whether they fire on this case is the first thing the next run should establish —
the model records `replacementAnalysis` and `costsAndCharges` for all three arrangements, so the
inputs are present.

---

## 6. Diagnostics, and one thing that is not a defect

| | Value | Reading |
| --- | :-: | --- |
| Retrieval | 236 searches, 3,752 passages | comparable to Test Case 2 |
| Pack selection | 1,852 of 2,472 (25% dropped) | comparable |
| Dead section hints | **13** | a *different* 13 — see §1 |
| Citation trust | **132 untraceable** | Haiku's signature; 30 on Sonnet in Test Case 2 |
| Wrong requirement id | **64 of 88 (73%)** | Haiku's signature, slightly worse than Test Case 2's 69% |
| Raised and not carried | 1 group, 4 differences | low |
| Outcomes | 83 concern, 4 no issue, **2 shortfall, 1 N/A** | |

**The one N/A is `CHK-003/G3.11`, with trigger probes bypassed** — so it is the assessor returning
Not Applicable for a requirement, not a check being skipped. Worth a look but not the silent-skip
class.

**The extraction reported two real defects on itself**, and both are the kind the run should be
read beside:

> `/costsAndCharges/existing` written twice in the same object, **values differ**, the first kept
> Cross-references — `linkedRecommendationIds → REC-004` resolves to nothing; **one section failed
> and is missing from the model**

A charge section where two readings disagreed and one was silently dropped is directly upstream of
the CHK-007 findings scored above.

### Not a defect: the category codes

Citations show the same document as `[C]` and `[C_MeetingNotes]`, and long-form codes
(`[B_FactFind]`, `[I_RecommendationsAndAdvice]`) appear beside single letters. **I checked whether
this breaks category targeting and it does not**: the retrieval extract contains 259 `[C]` and zero
`[C_MeetingNotes]`, so every *passage* carries a single letter and `Rank` matches correctly. The
long-form names come from the canonical model's own `documentCategory` provenance and reach the
output only through citations of model fragments.

It is a display inconsistency in the findings report — a reviewer has to work out that two codes
name one document — and worth normalising. It is not a retrieval fault, and I nearly reported it
as one.

---

## 7. What this changes

**The generalisation result is the finding, and it outranks everything else in this document.**

A pipeline that scores 55% on the case it was developed against and 31% on the next one has been
measured on one case and tuned to it. Nothing here says the work was wrong — Test Case 2's gains
were real and independently verified across two models — but it does say the **improvement figures
quoted from Test Case 2 are an upper bound, not an expectation.**

Three consequences.

**1. Every future change is measured on two cases, not one.** A change that moves Test Case 2 and
not Test Case 3 is a change to Test Case 2. That is now cheap to check: Haiku scores this case for
£2.46, and the benchmark parses mechanically.

**2. The dead section hints are the largest single lever, and they are per-case.** Thirteen here,
fourteen on Test Case 2, and no overlap worth mentioning. `G4.1`'s hint names the exact phrase
behind a missed Highest-severity finding. **The index has still never been rebuilt** since the
converter's pseudo-heading fix — three runs now — so the standing recommendation stands and gains
a second case's worth of evidence.

**3. The second round of changes is aimed at the right class, on this evidence.** The absence-clause
side effect reproduces here exactly as predicted, and the largest untravelled group is the
two-tables arithmetic that the derived comparisons compute. Neither is confirmed until this case is
re-run with them.

### What to do next, in order

| | Action | Why |
| :-: | --- | --- |
| **1** | **Re-index, then re-run this case** | Thirteen dead hints, one naming a Highest-severity finding's exact evidence. Untested for three runs |
| **2** | **Re-run this case on the current build** | The second round of changes has never met an unseen case; the absence clause and the derived comparisons both have predictions to test here |
| **3** | Fix the benchmark's stated count | It says 44 and lists 58. The second benchmark needing this |
| **4** | Investigate why the court-order preference reaches no assessor | Zero mentions of the most severe defect in the case. Start with `RetrievalDryRun`, not with a model |
| **5** | Normalise category codes in citations | Display only, but a reviewer should not have to deduce that `[C]` and `[C_MeetingNotes]` are one thing |
| **6** | Score a second run of this configuration | Everything above rests on one run at an unpinned seed |

---

## Method

Scored from `run-checks-output.txt`, not the checks log — the log embeds the full prompts and so
matches the evidence pack as readily as the findings, inflating every count.

Caught, partial and missed are the benchmark's own definitions: *caught* when the run states the
substance of the contradiction, naming both sides and what conflicts; *partial* when it touches the
area but misidentifies the conflict, states one side without the contradiction, or buries it;
*missed* otherwise.

**Confidence varies across the table and the reader should know where.** Every zero-mention finding
was verified by search across the whole output, and every headline defect and every scored *Caught*
was read in context. The *Partial* assignments in the middle of the range are the least certain:
they rest on the surrounding sentences of a token match rather than a full reading of all 88 group
findings, and a second reader could move two or three of them a step in either direction. That
uncertainty is smaller than the 25-point gap this run is about, and larger than any single row.

The benchmark's own caveat applies and matters more here than on Test Case 2: **these are not 58
independent trials.** F6.1/F9.1, F6.2/F9.2, F6.3/F6.4/F7.1/F8.1/F9.3, F7.2/F9.4, F3.1/F5.1,
F3.2/F5.2/F8.2 and F4.1/F10.3 are the same defects seen from different checks. A single missed
defect can cost up to five scored rows, which is why the court-order preference and the switching
analyses between them account for a quarter of everything missed.

---

> **Correction, 2026-08-29.** The summary table above originally read *19 / 17 / 22 of 58* on the
> scoring set and *25 / 22 / 36 of 83* overall, with a 30% caught and 43% missed rate. Those totals
> do not follow from this report's own finding-by-finding table, which counts **20 / 22 / 16** and
> **26 / 27 / 30** — a 31% caught and 36% missed rate. The Moderate + Low row was right; the
> Highest and High rows were miscounted, overstating the missed column and understating the
> partials. The individual verdicts below were unaffected and are unchanged. Corrected while
> scoring [Run 2](../Run-2/run-analysis.md), which depends on these numbers as its control.
