# Run analysis — Test Case 2, Run 1

One run of case Test-Case-2 (Barnes) on 2026-08-28, scored against
[expected-results-benchmark.md](../expected-results-benchmark.md).

**Headline: 23 caught, 8 partial, 16 missed of 47 scored findings — 49% recall, 66% with
partials.** Six of the sixteen misses are one check that never ran. Of the ten that remain, **every
one had its evidence retrieved**, and in the three cases traced to the passage, the evidence was
*in the assessor's pack, in the right group*. Retrieval is not the bottleneck on this case. The
assessor is.

| | Run 1 |
| --- | --- |
| Assessor / extractor model | `intelliflo-claude-haiku-4-5` |
| **Caught / Partial / Missed** | **23 / 8 / 16** |
| Recall (caught) | 49% |
| Recall (caught + partial) | 66% |
| Recall excluding CHK-009 | 56% / 76% |
| Check-level agreement | 9/10 (CHK-009 wrong) |
| Cost | £3.89 (extract £0.72 · embed £1.30 · checks £1.88) |
| Wall clock (checks) | 230s for 133,763 output tokens — 580 tok/s |
| Retrieval | 207 searches, 3,252 passages, 225 canonical paths resolved, 35 absent |

---

## Contents

1. [Provenance](#1-provenance)
2. [Check-level outcome](#2-check-level-outcome)
3. [CHK-009 — one line of plan JSON cost six findings](#3-chk-009--one-line-of-plan-json-cost-six-findings)
4. [Finding-level scoring](#4-finding-level-scoring)
5. [Why the misses happened](#5-why-the-misses-happened)
6. [False positives](#6-false-positives)
7. [The self-diagnostics, read against the score](#7-the-self-diagnostics-read-against-the-score)
8. [Corrections to the benchmark](#8-corrections-to-the-benchmark)
9. [What to do next](#9-what-to-do-next)

---

## 1. Provenance

The run is genuine, not a gateway replay: **230s for 133,763 output tokens = 580 tok/s**. Replays on
this stack reproduce 26,000–49,000 tok/s.

- **Fingerprint** — `plans 10@09cf0a31b103 · model 888cecaca118 (schema v1.0)`, embeddings
  `amazon.titan-embed-text-v2:0`, `top 8/search, 24/group (reserving 1/section, 1/category)`,
  extraction cap 32,000, sampling unpinned (`temperature default, top-p default, seed not pinned`).
- **Extraction** — one pass, 2026-08-28 15:53, 12 sections, 325,372 characters, response cache
  bypassed. Section 4 (*Risk, knowledge and vulnerability*) returned invalid JSON on the first
  attempt and parsed on retry; both attempts were paid for.
- **Indexing** — 598 chunks from 38 documents, semantic chunking at 600 tokens / 100 overlap.
- **Sampling is not pinned**, so this run is not exactly reproducible. Everything below is a
  single observation, not a measurement with a known noise floor.

One provenance caveat worth recording: the logs reference
`C:\work\Prompt-Evaluator-Semantic\...` and user `nshaikh`, i.e. the run was produced on a
different checkout from the one this analysis was written against. The plan digest is the only
link, and it has not been recomputed against `docs/Implementation/check-plan/`.

---

## 2. Check-level outcome

| Check | Expected | Run 1 | |
| --- | --- | --- | --- |
| CHK-001 | Potential Concern | Potential Concern | ✓ |
| CHK-002 | Potential Concern | Potential Concern | ✓ |
| CHK-003 | Potential Concern | Potential Concern | ✓ |
| CHK-004 | Potential Concern | Potential Concern | ✓ |
| CHK-005 | Potential Concern | Potential Concern | ✓ |
| CHK-006 | Potential Concern | Potential Concern | ✓ |
| CHK-007 | Potential Concern | Potential Concern | ✓ |
| CHK-008 | Potential Concern | Potential Concern | ✓ |
| **CHK-009** | **Potential Concern** | **N/A** | **✕** |
| CHK-010 | Potential Concern | Potential Concern | ✓ |

As the benchmark warns, check-level agreement is nearly worthless here — nine of ten land on
Potential Concern by construction. The one place it carries information is CHK-009, and the run got
it wrong in the most expensive direction: **N/A leaves no trace in the output**. A reviewer reading
this report would see "Checks cleared" and move on.

---

## 3. CHK-009 — one line of plan JSON cost six findings

The run's own explanation is precise enough to fix from:

> Trigger absent: Nothing is being replaced, switched, transferred, encashed or surrendered..
> Canonical model checkTriggers.hasReplacementOrSwitch = **true**. Applicability not satisfied:
> **adviceActions (found RetainAndSwitchFunds)**. 0 corroborating passage(s) from 0 probe search(es).

The chain:

1. The extraction was **correct**. The canonical model records the single arrangement as
   `adviceAction: "RetainAndSwitchFunds"` — a documented schema value, and the right one for a
   100% fund switch inside a retained wrapper.
2. `checkTriggers.hasReplacementOrSwitch` was also **correct** (`true`).
3. [`CHK-009.query-plan.json`](../../../Implementation/check-plan/CHK-009.query-plan.json) has two
   applicability rules, ANDed. The second lists
   `["Switch", "PartialTransfer", "FullTransfer", "Encash", "Surrender"]`. `RetainAndSwitchFunds` is
   not among them, so rule 2 vetoed rule 1.
4. `onAbsent: "Skip"` → the check ended with **zero retrieval, zero cost, and a clean N/A**.

The canonical schema is complicit: its own description of `adviceAction` reads *"Triggers CHK-009
when Switch/Transfer/Encash/Surrender"* while listing `RetainAndSwitchFunds` as a valid value. The
schema and the plan agree with each other and both disagree with COBS — a fund switch of the whole
fund inside a retained wrapper *is* replacement business.

**Fix:** add `"RetainAndSwitchFunds"` (and, on the same reasoning, `"Amend"` deserves a look) to
CHK-009's `adviceActions` list, and correct the schema description. That single edit puts six
scored findings (F9.1–F9.6) back in reach and takes the ceiling on this case from 47 to the full
set.

This is the second time this exact failure mode has been recorded. [`CheckPlanRunner.cs:395-402`](../../../../src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs#L395-L402)
already documents it: *"CHK-009's second rule reads `/existingArrangements[]/adviceAction`, three
runs lost that section to a duplicate property name, and the check would have settled as not
applicable — dropping five material findings."* Last time the cause was a lost section; this time
it is a vocabulary gap. The lesson generalises: **an N/A produced by an applicability rule on an
enumerated value should be reported loudly, not filed under "checks cleared."**

---

## 4. Finding-level scoring

Scored against the benchmark's material set. **Caught** = the run states the substance of the
contradiction, naming both sides. **Partial** = touches the area but misstates the conflict, states
one side only, or buries it. **Missed** = neither.

### Caught — 23

| ID | Where the run got it | Note |
| --- | --- | --- |
| F1.1 | G1.1, G1.4, G1.8, G1.10, G1.11, G5.2, G5.4, G5.8, G8.1 | Found nine times; G1.4 even derives the £3,710.17 total independently |
| F1.2 | G1.3 (High) | "stopped working altogether" vs Fact Find `Employed / £890` |
| F1.3 | G8.3 | Names the same-paragraph contradiction on earnings sufficiency |
| F1.4 | G1.3, G5.7 | Quantified: £28,000 vs £16,800, "£11,200 (40%)" |
| F2.1 | G2.2 (High) | Age 60 vs 67, plus Fact Find "Medium Term" |
| F2.2 | G2.4 (High) | Yvette: report 2030 vs cashflow 2033 |
| F2.3 | G2.1, G2.7 | Both unsupported objectives, argued in detail |
| F2.4 | G2.1 | Quotes "not dead set on doing this" verbatim |
| F2.5 | G2.7 | £2,200 target against £2,206.67 expenditure |
| F3.2 | G3.2, and again in G3.3/G3.10 raised-not-carried | "No additional notes were recorded"; rating not evidenced outside the report |
| F4.1 | G4.5 | Explicitly links "no understanding of unit trusts" to the recommended holdings |
| F4.2 | G4.2, G10.1, G10.4 | The omitted pension-planning answer |
| F4.3 | G4.5, G4.7 | Including the ATRQ-completed-alone point |
| F5.6 | as F1.1 | |
| F6.2 | G1.8, G6.5, G7.10 | *"the report claims a cost benefit ('Lower Costs') that is contradicted by the arithmetic"* |
| F6.4 | G2.1, G2.7, G6.1, G8.3 | £3,600 basis unresolved on the adviser's own file |
| F6.5 | G6.7 | Sharp: the recommendation *"appears to be a hybrid or sixth option not among the five presented"* |
| F7.2 | G7.6 | The best single finding in the run — see below |
| F7.6 | G7.3 | Retrieves £91,500→£84,800 and concludes no aggregated all-in cost comparison is given |
| F8.1 | G8.7, G8.8 | *"the suitability report does not disclose these small projected shortfalls"* |
| F8.3 | G7.10, G8.6 | 45.28% vs 30.12% give-up, unquantified by the report |
| F8.4 | G6.1, G8.1, G8.3 | No CGT planning, no marginal rate established |
| F10.1 | G10.4 | *"No formal vulnerability assessment ... despite clear indicators of low financial capability"* |

**G7.6 deserves singling out.** It reasoned to within one step of the case's deepest defect:

> The required outperformance of 1.08% annually exceeds the stated charge difference of 0.08% by
> 1.00 percentage point. No explanation in the report accounts for this gap or justifies why
> outperformance must be 13.5 times the charge increase to break even.

The missing 1.00% *is* the new ongoing adviser charge. One more inference and the run would have
had F7.1.

### Partial — 8

| ID | What was reached | What was not |
| --- | --- | --- |
| F1.7 | G1.2 raised the Aon final-salary contradiction | …then passed. Appears only under "raised and not carried" |
| F3.4 | G3.4 correctly found no term is stated anywhere | The 5-year risk-profile goal was found by G3.10 — which passed |
| F5.1 | G5.2 has "£1,700 vs £2,590"; G5.4 has "report silent on whether the £890 will continue" | Never does the subtraction: £1,700 − £2,206.67 = −£507/month |
| F5.2 | G1.5 notices the "current" cashflow *"includes an Aviva GIA of £52,326 not separately listed"* | Never concludes that no pre-advice baseline exists |
| F6.1 | The £100 / 0.1% decrease is found in G7.3, G7.8, G7.11, G8.7, G8.8 | The ranking itself: G7.7 says the table *"does not appear in the retrieved evidence"* — while CHK-004 retrieved it (`Existing Solution 0.6% £86,800`) |
| F6.3 | G6.1 flags 4.72% and the Moneybox comparison as unevidenced | Not that the comparison is gross of 1.5% of charges, against an illustration showing 5.0%→3.4% |
| F6.6 | G6.1, as above | The simple-vs-compound arithmetic, and the 31.28% in the research report |
| F7.4 | G8.7: 2.94% *"achievable"* on past performance, *"leaving minimal safety margin"* | Not that the critical yield is real and the performance nominal |

### Missed — 16

**CHK-009 (6):** F9.1, F9.2, F9.3, F9.4, F9.5, F9.6 — the check never ran.

**Everything else (10):** F1.5, F3.1, F5.3, F5.4, F5.5, F7.1, F7.3, F8.2, F10.2 — and F5.5.

---

## 5. Why the misses happened

I traced each miss back through
`retrieval-extract_Test-Case-2_20260828-161740.txt` to establish whether the evidence ever reached
the pipeline. **It did, in every case.**

| Missed finding | Decisive evidence | Times it appears in the retrieval extract |
| --- | --- | --- |
| F3.1 CFL questionnaire | "able and willing to support me financially" | **11** |
| F1.5 UFPLS not available | "Uncrystallised fund pension lump sum is not applicable to this plan" | **20** |
| F5.3 property excluded | "EXCLUDED FROM SCENARIO" | **8** |
| F5.4 cash at the invested rate | "4.5% expected return" | **49** |
| F7.3 transaction costs | "Transaction Costs" / "0.51%" | **66 / 30** |
| F8.2 "much steeper losses" | "-11.32" (CT fund, calendar 2022) | **8** |
| F10.2 "Doesn't trust anyone" | verbatim | **10** |

For three of these I went further and located the passage inside the group's pack.

### F3.1 — in the pack, at rank 6, in the right group

`CHK-003 G3.2 — Capacity for loss assessment` retrieved 40 passages and packed 24. Passage **[6]**
is a Risk profile report chunk containing the Investor Experience table *and* the opening of the
Capacity For Loss questionnaire, including `I am investing in order to meet goals at what point in
the future (my investment horizon). | 3-9 years`. The spouse-support row is in the same table.

G3.2 nonetheless concluded:

> The risk profile questionnaire [P2] and additional notes [P1] provide no independent assessment of
> capacity for loss—no notes were recorded.

The assessor had the questionnaire in front of it, quoted from the same document, and reported it
as absent. **This is not retrieval. This is the assessor.**

The same passage also carries the "3-9 years" horizon that `G3.4` declared unavailable and returned
`NOT ASSESSED` for. Two groups, one pack away from each other, neither able to see the other's
evidence.

### F1.5 — in the pack, at rank 4, in the group whose job it was

`CHK-001 G1.7 — Existing products … are consistent with the evidence` packed the Aviva plan
information form at **[4]**. That document says *"Uncrystallised fund pension lump sum is **not
applicable** to this plan."* The report's Existing Arrangements table says *"Full flexi-access
drawdown **and UFPLS** available."*

G1.7 concluded:

> All material facts about the existing arrangement—provider, policy number, plan type, current
> holdings, current value as at the report date, transfer value, and contribution status—are
> corroborated … **No contradiction … exists.**

The same document was in-pack at [2] for G6.5 and [4] for G4.6. Three groups, three misses.

### A ranking anomaly worth fixing on its own

In `G3.2`, the **highest-scoring passage in the group (0.737)** was **evicted**, while eight
near-duplicate cashflow chunks at 0.627 — from a single query, `Q3.2.4` — were retained, along with
in-pack passages scoring 0.587 and 0.560. The evicted passage carried the *Capacity for loss for the
goal* section and the head of the ATR questionnaire.

Per-query slot allocation is crowding out the best evidence with redundant near-duplicates from one
query. The five cashflow scenarios are 90% identical by construction, and the ranker is treating
them as five independent sources.

### Summary of causes

| Cause | Findings | Fix belongs to |
| --- | --- | --- |
| Applicability rule vetoed a valid vocabulary value | F9.1–F9.6 (6) | **Plan JSON** — one line |
| Evidence in the pack, not used by the assessor | F3.1, F1.5, F5.3, F5.4, F7.3, F8.2, F10.2 (7) | **Assessor prompt / model** |
| Reasoning inverted on retrieved evidence | F7.1 (1) | **Assessor prompt** — see below |
| Found by one group, needed by another | F1.7, F3.4 (partial) | **Runner** — carry raised-not-carried forward |
| Near-duplicate crowding in ranking | contributory to F3.1, F5.3 | **Ranker** |

### F7.1 — the one the run reasoned backwards

The switch report contains **two** comparison tables: one before advice charges (£86,800 vs
£86,700, RIY 0.6/0.7, effect £100 / 0.1%) and one after (£74,000 / £83,300 / £93,400 both sides,
RIY 1.6/1.7/1.8, effect £0 / 0.0%). The suitability report's appendix reproduces the **second**,
accurately.

`G7.3`, `G7.8` and `G7.11` all compared the report's after-charges table against the source's
before-charges table and reported the difference as a contradiction — *"Report asserts Mid growth
RIY for Existing as 1.7%; Pension Switch Report shows 0.6%, a material difference of 1.1 percentage
points."* That is a false positive, and `G7.8` carries it at **severity High with citations
unverified**.

Worse, `G7.11` then read the identical after-charge columns as *reassurance*:

> The RIY figures for the new solution … are marginally lower than or equal to the existing
> pension's RIY at High growth …, **contradicting the assertion of materially higher charges.**

The correct reading is the opposite: the two columns are identical only because a 1% ongoing adviser
charge the client does not currently pay has been applied to the existing plan as well as the new
one. The run had the raw material — it retrieved both tables — and drew the inverse conclusion. This
is the single most consequential reasoning error in the run, and the only miss that is **actively
misleading** rather than merely absent.

---

## 6. False positives

Seven, of which three would waste a reviewer's time and one is self-inflicted by the pipeline.

| # | Where | Claim | Reality |
| --- | --- | --- | --- |
| 1 | G7.3 / G7.8 / G7.11 | Report's RIY figures "contradicted by the switch report" | Two different tables, pre- and post-advice-charge. **High severity, prominent** |
| 2 | G2.1 | *"No document mentions a 'Chartered savings account'"* | It appears **33 times** in this run's own retrieval extract, including the Fact Find asset table |
| 3 | G6.3 | *"the suitability report document itself is absent from the evidence pack"* → NOT ASSESSED | The report is indexed as 42 chunks in category [I] and cited by ~30 other groups |
| 4 | G3.2 headline | *"report asserts capacity for loss is 'High' but the report states 'Medium-High'"* | The canonical model normalised `Medium-High → High`. A pipeline artefact reported as a report defect (G3.10 identifies it correctly, in a group that passed) |
| 5 | G6.9 | *"the Fact Find estate planning section is blank and contains no evidence of a will"* | Will and POA are recorded under **Personal Details**, not Estate Planning |
| 6 | G5.1 / G5.4 | *"£1,558 at 0.62% implies £251,290"* | £1,558 is the 1% adviser charge, not 0.62%. Arithmetic on the wrong rate |
| 7 | G4.2 | *"internally contradictory: absence of understanding and self-directed investment in the same product class"* | The client's own questionnaire says both. The report is reproducing it faithfully |

#1 and #4 share a shape: **the pipeline's own transformations being reported as findings against the
adviser.** Both are avoidable — #1 by giving the assessor the table headers, #4 by never
normalising a rating without recording the raw value alongside.

---

## 7. The self-diagnostics, read against the score

The run's tail block is the most useful part of the output, and it predicts the misses well enough
to be used as a gate.

**`Section hints matching nothing: 14`** — four of the fourteen name the exact evidence behind a
miss:

| Failed hint | Finding it would have reached |
| --- | --- |
| `G3.4: Capacity For Loss questionnaire responses` | F3.1 |
| `G3.4: my investment horizon` | F3.4 |
| `G7.4: Existing Arrangements` | F1.5 / F1.6 |
| `G8.1: Effect of switching` | F8.1 / F6.1 |

The cause for the first two is a **conversion defect**. In
`Risk profile report 11 08 2025 08 58.md` the *discussion notes* heading exists as
`#### Capacity for loss questionnaire` (containing only "No additional notes were recorded"), but the
**Appendix** and the three questionnaire tables 30 lines below it carry **no markdown heading at
all** — Docling emitted `**Appendix**` as bold text. The section hint pointed at prose, not a
section. The same defect destroyed the equivalent tables in Test Case 3 and Test Case 4.

**`Citation trust: 85 quote(s) could not be traced`** — sampling these, most are **reformatted real
content**, not fabrications: `"Anthony Barnes | Main Residence | House Mortgage-free | £400,000.00"`
against a source row of `| Anthony Barnes | Main Residence | | House Mortgage- free | | | GBP | | |
£ 400,000.00 |`. Column separators collapsed, a hyphenation join, a space removed after `£`. The
verifier is measuring verbatim fidelity on table rows the model has tidied. It is not currently a
hallucination signal, and treating it as one will train the wrong reflex. Worth normalising
whitespace and pipe runs before comparison.

**`Pack selection: 505 of 2,176 (23%) dropped`** — the G3.2 case shows this is not benign. The
number alone is uninformative; a *"highest-scoring passage in the group was evicted"* counter would
be.

**`Raised and not carried: 6 group(s) named 23 difference(s) and then passed`** — this caught two
real findings that the check-level output discarded (F1.7 in G1.2, F3.4's 5-year horizon in G3.10).
The block is doing exactly what its own comment claims. It should feed back into the finding list,
not sit below it.

**`Prompt adherence: of 78 responses, 49 echoed the wrong requirement id and 6 rewrote the
requirement text`** — **63% wrong**. The plan's values were substituted, so no output was corrupted,
but a model misidentifying which of eleven requirements it is answering two times in three is a
strong signal that the requirement is not landing prominently enough in the prompt. This is the
cheapest available lead on the "evidence in the pack, not used" class.

---

## 8. Corrections to the benchmark

Scoring this run surfaced two errors in
[expected-results-benchmark.md](../expected-results-benchmark.md). Both are mine, and both should be
fixed before the file is used again.

**F3.3 should be withdrawn.** I wrote that the capacity-for-loss rationale — *"you will only have a
shortfall for a small period of time, when Yvette retires, until her State Pension begins"* — is
*"contradicted by the adviser's own modelling, which shows no shortfall in any year."* The run
produced the actual rows and they support the adviser, not me:

```
2030 | 60/64 | 239,271 | 22,000 | 29,101 | (7,101)
2031 | 61/65 | 235,314 | 22,000 | 29,052 | (7,052)
2032 | 62/66 | 231,288 | 22,000 | 29,002 | (7,002)
2033 | 63/67 | 227,191 | 34,451 | 29,165 |       0
```

Yvette's salary ends in 2030 and her State Pension starts in 2033 — a three-year income deficit of
about £7,000 a year, met from £150,000 of liquid assets, exactly as the report describes. I
conflated the *asset* shortfall (none, correctly) with the *income* deficit (real). The residual
point — that the report never quantifies the period or the draw — is Low, not High.

**The stated count is wrong.** The benchmark says "38 material findings" in three places; the
scoring block lists **48**. All scores in this document use 48, less the withdrawn F3.3, giving a
denominator of **47**.

---

## 9. What to do next

Ordered by findings recovered per unit of work.

| | Change | Recovers | Effort |
| --- | --- | --- | --- |
| **1** | Add `RetainAndSwitchFunds` to CHK-009's `adviceActions`; correct the schema description | 6 findings, one whole check | One line |
| **2** | Surface applicability-driven N/A as a warning, not under "Checks cleared" | Prevents silent recurrence | Small |
| **3** | Give the assessor the *table caption* alongside each passage, so pre/post-advice-charge tables are distinguishable | F7.1, and removes FP #1 | Small |
| **4** | Feed "raised and not carried" back into the finding list for the check that raised it | F1.7, F3.4 | Small |
| **5** | Fix the converter so bold pseudo-headings (`**Appendix**`) become headings | F3.1, F3.4, and the same class in TC3/TC4 | Medium |
| **6** | De-duplicate near-identical chunks before ranking; never evict the top-scoring passage in a group | F3.1, F5.3 | Medium |
| **7** | Never normalise a canonical value without retaining the raw one beside it | FP #4 | Small |
| **8** | Normalise whitespace and pipe runs before citation verification | Makes the 85-quote signal usable | Small |
| **9** | Investigate the 63% requirement-id mismatch | The largest miss class (7 findings) | Medium |
| **10** | Re-run with sampling pinned, and with a stronger assessor for comparison | Establishes a noise floor; TC1 showed +8 findings from Haiku → Sonnet | Cheap |

**Item 10 deserves emphasis.** On Test Case 1, the same model change — `claude-haiku-4-5` to
`claude-sonnet-4-6`, everything else held constant — moved recall from 56% to 78%. This run used
Haiku, and its dominant failure mode (evidence in the pack, not used) is precisely the one that
comparison showed to be model-sensitive. Before attributing any of these seven misses to retrieval or
to the plans, run the case again on a stronger assessor.

**Expected ceiling from items 1–4 alone:** 23 → ~30 caught of 47 (64%), without touching the
converter, the ranker or the model.
