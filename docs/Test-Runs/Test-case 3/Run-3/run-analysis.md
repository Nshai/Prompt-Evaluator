# Test Case 3, Run 3 — deep analysis

Sonnet 4.6, run 2026-08-29 00:40, on the same build as
[Run 2](../Run-2/run-analysis.md) — both rounds of the Test Case 2 improvement work. Scored
against [expected-results-benchmark.md](../expected-results-benchmark.md) across **all 83 material
findings**, with a finding-by-finding comparison to Run 2.

---

## 1. This is a clean model swap

| | Run 2 | **Run 3** |
| --- | --- | --- |
| Chat model | intelliflo-claude-haiku-4-5 | **intelliflo-claude-sonnet-4-6** |
| Plans | 10@`e267d2d36592` | same |
| Index | 585 chunks, unchanged | same |
| Adjusted settings | 9 named | same 9 |
| Settings digest | `84ca882c3ba6` | `3c40d101cc81` (differs only because the model name is in it) |
| Canonical model | `6802268cf6bc` | `6e147dd8ae14` (re-extracted by Sonnet) |
| Extraction cost | £0.6610 | **£2.1570** |
| Checks cost | £2.3407 | **£7.2253** |

**The two retrieval extracts are byte-identical apart from the header line.** 46,452 lines each;
`Compare-Object` returns exactly two differences, both the line naming the model. Identical search
counts per check (30/21/28/20/27/13/27/21/29/20), identical passage counts, identical packs.

So the pack every assessor read was the same in both runs. The only variables are the model doing
the extraction and the model doing the assessment — and those two cannot be separated here, because
one swap changed both. §6 shows the extraction difference is real and material.

Cost of the swap: **£9.38 against £3.00**, 3.1× on the checks alone.

---

## 2. The score

| | Run 1 (Haiku, round 1) | Run 2 (Haiku, round 2) | **Run 3 (Sonnet, round 2)** |
| --- | :-: | :-: | :-: |
| **Highest** (22) | 8 / 10 / 4 | 12 / 5 / 5 | **15 / 2 / 5** |
| **High** (36) | 12 / 12 / 12 | 14 / 9 / 13 | **20 / 4 / 12** |
| **Scoring set** (58) | 20 / 22 / 16 — 34% | 26 / 14 / 18 — 45% | **35 / 6 / 17 — 60%** |
| **Moderate** (21) | 5 / 4 / 12 | 9 / 1 / 11 | **8 / 4 / 9** |
| **Low** (4) | 1 / 1 / 2 | 0 / 2 / 2 | **2 / 0 / 2** |
| **Moderate + Low** (25) | 6 / 5 / 14 — 24% | 9 / 3 / 13 — 36% | **10 / 4 / 11 — 40%** |
| **All material** (83) | 26 / 27 / 30 | 35 / 17 / 31 | **45 / 10 / 28** |
| **Caught** | 31% | 42% | **54%** |
| **Missed** | 36% | 37% | **34%** |

Read as *caught / partial / missed*.

**54% on an unseen case, against 55% on the case the pipeline was tuned to.** The generalisation
gap that was 24 points after Run 1 and 13 points after Run 2 is now effectively closed. On the
scoring set alone — the 58 Highest and High findings — Sonnet reaches **60%**.

The Partial column collapses to 10. Run 3 is not hedging more; it is finishing more.

---

## 3. The finding this run overturns

The Test Case 2 experiment measured the same two variables and got the opposite answer:

| Experiment | Effect of the prompt/code changes | Effect of the model swap |
| --- | :-: | :-: |
| Test Case 2 (Runs 3 & 4) | **+14 caught** | +1 caught |
| **Test Case 3 (Runs 1 → 2 → 3)** | **+9 caught** | **+10 caught** |

On Test Case 2 the model was worth almost nothing once the prompts were fixed. On Test Case 3 it is
worth as much as two rounds of engineering. I would not have predicted that from the Test Case 2
result, and the conclusion I drew there — *"changes matter, model does not"* — does not survive
contact with a second case.

The plausible reading, stated as a reading and not a measurement: Test Case 2's findings are mostly
single-document comparisons that either model can make once told to make them, while Test Case 3's
turn on holding several documents and two report sections in view at once. **Whether a change helps
depends on which model is running it, and the Test Case 2 numbers were an upper bound for Haiku,
not a property of the pipeline.**

---

## 4. What moved against Run 2

Thirty-one of 83 findings changed verdict — **23 up, 8 down**. That ratio is the reverse of the
Run 1 → Run 2 comparison, which was 23 up and 19 down.

**Up (23):** F1.2, F1.7, F1.9, F2.2, F2.4, F2.7, F3.1, F3.5, F4.1, F4.4, F5.6, F6.3, F7.2, F7.3,
F7.5, F7.7, F7.9, F9.2, F9.4, F9.5, F9.7, F9.8, F10.3

**Down (8):** F1.6, F1.16, F3.2, F4.5, F5.2, F5.5, F7.6, F8.2

Per check:

| Check | n | Run 2 | Run 3 |
| --- | :-: | :-: | :-: |
| CHK-001 Suitability completeness | 16 | 4 / 7 / 5 | 6 / 3 / 7 |
| CHK-002 Objectives and horizon | 6 | 2 / 1 / 3 | **4 / 1 / 1** |
| CHK-003 Risk and capacity for loss | 8 | 3 / 1 / 4 | 4 / 0 / 4 |
| CHK-004 Knowledge and complexity | 5 | 2 / 0 / 3 | **2 / 2 / 1** |
| CHK-005 Financial resilience | 8 | **7 / 1 / 0** | 6 / 0 / 2 |
| CHK-006 Rationale and alternatives | 9 | 5 / 0 / 4 | 6 / 0 / 3 |
| CHK-007 Costs and charges | 9 | 3 / 4 / 2 | **6 / 1 / 2** |
| CHK-008 Disadvantages and risks | 7 | 5 / 0 / 2 | 4 / 0 / 3 |
| CHK-009 Replacement justification | 9 | 1 / 3 / 5 | **4 / 2 / 3** |
| CHK-010 Vulnerability | 6 | 3 / 0 / 3 | 3 / 1 / 2 |

**CHK-009 finally moves.** It sat at 1 / 3 / 5 across Run 1 and Run 2 — identical through two
rounds of engineering aimed squarely at it. The model swap moves it to 4 / 2 / 3.

### The single best result

**F6.3 / F9.2 — the switching analysis excludes 79% of the money.** Run 2 missed this; Run 3 states
it five separate times, and G7.4 states it with the arithmetic:

> *"EA1 is entirely absent from the switch charges comparison table. The like-for-like cost
> comparison therefore covers only two of the three arrangements being switched, omitting the
> largest single component (£260,000 / ~79% of the total fund value)."*

That is the benchmark's F9.2 including its percentage.

**F1.7 / F9.7 — six funds of seven.** Run 2 got as far as noticing the components did not sum;
Run 3 does the sum and states the gap:

> *"The True Potential holdings in the report sum to £2,787.29 + £10,764.88 + £2,378.80 + £7,254.00
> + £7,771.29 + £10,099.00 = £41,055.26, which is £5,617.54 less than the stated current value of
> £46,672.80 — either additional holdings are not listed or the component values do not sum."*

Both figures match the benchmark exactly.

**F6.4 / F9.3 — four analyses with opposite answers.** G7.8 lays out four RIY figures across four
scenarios and names the contradiction:

> *"[P2] shows the effect of switching as +20.85% (positive — a benefit to the client) under the
> 'no charges' scenario, directly contrasting with the −12.42% in the report; the different
> scenarios produce qualitatively opposite conclusions about whether switching benefits the client."*

**F7.3 — two different Aviva platform charges** (0.19% and 0.21%), caught in three groups. **F7.4 —
age 89 against analyses run to 87**, in three. **F1.2 — the inheritance is dated "next year" in one
part of the report and "two years' time" in another**, which Run 2 never reached as an internal
contradiction.

---

## 5. The regressions, and one of them is instructive

Eight findings went backwards. Five are ordinary: F1.6 and F5.5 (the mortgage rate reset from
1.24% to ~3.9%, which Run 2 caught and Run 3 does not mention — the strings `1.24%` and `3.8%`
appear nowhere), F7.6 (the £372,000 → £337,000 cumulative charge disclosure, quoted in Run 3's
evidence blocks and never made into a finding), F1.16 and F4.5.

**The other three share one cause, and it is worth stating precisely.** F3.2, F5.2 and F8.2 are all
the same underlying defect: the report says assets *"could be depleted at age 81"* under its own
stress test and, nearby, that funds *"would be expected to last throughout your lifetime"*, against
a quoted life expectancy of 87. Run 2 caught all three. Run 3 has both halves — **as two consecutive
bullets in the same group**:

> `- The stress-test result (funds exhausted at age 81) is asserted in the report but no cashflow`
> `  modelling output document is in the file to corroborate it.`
> `- The baseline scenario outcome ('assets sufficient throughout lifetime') is asserted but has no`
> `  independent modelling output in the file.`

It wrote the two contradictory statements one line apart and treated each as a corroboration
problem rather than noticing that they contradict each other. Nothing anywhere in the run joins
81 to 87.

This is a specific, diagnosable failure mode, and it is the shadow of Run 3's greatest strength.
The phrases *"no independent"*, *"not corroborated"*, *"appears only in the report"* and
*"uncorroborated"* occur **93 times in Run 3 against 11 in Run 2** — 8.5×. Sonnet applies the
corroboration test to almost everything, which is where most of its 23 gains come from, and the
same reflex converts an internal contradiction into two separate evidence gaps.

---

## 6. Where the gain actually comes from

Three mechanisms are visible in the output, and only the third is purely about the assessor.

**The extraction improved, and it improved on exactly the right things.** Haiku's extraction
recorded four internal inconsistencies; Sonnet's recorded five, and they are sharper:

| Run 2 extraction | Run 3 extraction |
| --- | --- |
| Quilter has no like-for-like comparison | **The 3% initial fee (£9,838.36) cannot be 3% of the stated £67,945.44 transfer value** |
| Nest's 1.8% contribution charge excluded | **£2,986/month in the narrative vs £2,237/month in the cashflow** |
| True Potential 1.48% vs 0.98% | True Potential 0.40% vs 0.98% |
| *A growth risk profile sits oddly with a modest income target* | The Nest table merges fund name and risk level |
| — | The £3,181.08 ongoing fee cannot be 1% of £67,945.44 |

Haiku's fourth entry is a soft judgement that is not on the benchmark at all. Three of Sonnet's are
benchmark findings. **Part of this run's advantage was decided before the assessor ran.**

**The derived-figures block is finally being used.** References to it: **10 in Run 2, 36 in Run 3.**
Haiku largely ignored a feature the improvement work added; Sonnet reasons from it, including the
sharpest arithmetic catch in the run —

> *"the derived figure check shows 1.23% at 0.09% implies a fund of only £1,556.91, which matches
> no arrangement"* (G7.6, on the required-outperformance figure)

**Citation discipline is transformed.**

| | Run 2 | Run 3 |
| --- | --: | --: |
| Untraceable cited quotes | 99 | **25** |
| Groups `citations partly verified` | 46 | **16** |
| Groups `citations unverified` | 2 | **0** |
| `NOT ASSESSED` blocks | 16 | **1** |
| Discrepancy bullets | 432 | 468 |
| Output size | 718 KB | 652 KB |

Sixteen `NOT ASSESSED` blocks became one. More findings, fewer words, a quarter of the untraceable
quotes.

---

## 7. What both models still miss

Fourteen findings are missed by Haiku and Sonnet alike. The pack was identical, so these are not
retrieval failures and not model failures in the ordinary sense — they are what the current group
prompt does not direct any model to look for.

| | Severity | Status |
| --- | :-: | --- |
| F6.1 / F9.1 court order records an internal-transfer preference | Highest ×2 | evidence in pack; see below |
| F6.5 *"charges would have been incurred regardless"* | High | quoted in evidence, never questioned |
| F2.5 / F9.6 *"Nest will become paid up"* contradicted | High ×2 | zero mentions |
| F8.3 switched out of the best-performing plan | High | `48.06` absent from both runs |
| F1.10 report predates the presentation meeting | High | zero mentions |
| F1.11 IHT narrative written for a married client | Mod | passage in pack, unremarked |
| F1.12 / F10.4 PoA drafted for *"your spouse"* | Mod ×2 | zero mentions |
| F1.13 / F10.2 type 2 diabetic; health absent from the report | Mod, High | evidence recital only |
| F8.6 ONS longevity data unused | Mod | zero mentions |
| F9.9 pension sharing charges apportioned 33.4% | Mod | `33.4` absent from both runs |

**F6.1 / F9.1 is now as clear as this evidence can get.** Section F of the certified Pension
Sharing Annex — *"[x] xInternal transfer"* against *"[ ] External transfer"* — is in the pack in
both runs, reaching `G9.4` and `G9.10` in Run 2. In Run 3 the model went one step further: it
**quoted the passage verbatim in G1.7's citation list**, question text and ticked box intact.

> `"F. In cases where the Transferee has a choice of an internal or external transfer, if the`
> ` Transferee has indicated a preference, indicate what this is.  |  xInternal transfer"`

Three runs, two models, the passage retrieved every time and now reproduced in the output — and the
most severe defect in the case still produces no finding. It is not retrieval, and after this run
it is not the model either.

---

## 8. False positives

The benchmark's 13 **No issue** rows are the test. Both runs fail on largely the same ones, and
Sonnet's aggressive corroboration reflex adds pressure rather than relieving it.

| Row | Run 2 | Run 3 |
| --- | --- | --- |
| F1.17 partner contributes £600 | FP — £600 vs £800 with both dates quoted | **FP, unchanged** |
| F2.6 objectives correctly reflected | FP — G2.1 vs G2.8 contradict each other | FP, but hedged (*"not explicitly recorded"*) |
| F3.9 agreed risk 7, no override | FP — Nest's fund rating read as a client rating | FP, but qualified (*"though one relates to an existing holding"*) |
| F3.10 True Potential risk 6 evidenced | FP | FP |
| F7.10 £3,181.08 reconciles | FP — G7.3 and G7.5 contradict each other | **Not an FP** — Sonnet does the arithmetic, gets £3,181, and reports the *undisclosed basis*, which is fair |
| F7.11 Nest 0.30% corroborated by Defaqto | accepted | **FP** — *"no provider document corroborates the 0.30%"*, while the Defaqto weighted AMC of 0.3% is in its own citations |
| F10.7 alternative formats offered | mild FP | **Not an FP** — Sonnet acknowledges the firm-level statement |

**The £600 / £800 partner contribution survives everything.** The Fact Find records £800 as at
21/07/2026; the report records £600 as at 24/03/2026. The group prompt's comparison-basis clause
tells the model to establish what each figure is *on* before calling two of them contradictory.
Both runs quote both dates in the same paragraph and report a discrepancy anyway. **Two models,
identical packs, same violation** — which removes the last excuse for treating this as a prompt
problem. It needs a mechanism.

**One new cost, from a committed feature.** The extraction's £2,986-vs-£2,237 expenditure
inconsistency is a real finding (benchmark F5.4), and routing put it in front of **26 of 88 groups**,
where it appears **92 times**. It is right, and it is repeated until it crowds out everything else
in checks it has no bearing on. Haiku's routing propagated a *wrong* inconsistency into four
groups; Sonnet's propagates a *right* one into twenty-six. Both are the same defect in the routing
rule.

---

## 9. Diagnostics

| | Run 2 | Run 3 |
| --- | --: | --: |
| Groups assessed | 88 | 88 |
| Groups appearing anywhere in the report | 86 | **85** |
| Groups silently dropped | `G6.6`, `G8.3` | **`G3.6`, `G5.6`, `G6.7`** |
| `No Issue` verdicts | 2 | 0 |
| Model paths absent across checks | 35 | **49** |
| Slowest check | 159.6s | 239.0s |

**The silent-drop defect reproduces, and it moves.** Run 1 dropped `G1.2`; Run 2 dropped `G6.6` and
`G8.3`; Run 3 drops `G3.6`, `G5.6` and `G6.7`. Six different groups across three runs, each with a
full pack in the retrieval extract and no line anywhere in the report. It is not model-specific and
it is not deterministic. Nothing in the output tells a reader the question was asked.

Sonnet's extraction leaves more paths unresolved (49 absent against 35), concentrated in CHK-010
where 5 of 14 paths resolve against Haiku's 13. The run reports those absences correctly rather
than inventing content for them, and CHK-010's score holds at 3 caught — but a thinner canonical
model is a real cost of the swap, not only a benefit.

---

## 10. Method and confidence

Scored by reading each of the 83 material benchmark rows against the run's output, in the
benchmark's own vocabulary: **caught** = states the substance naming both sides; **partial** =
touches the area but misidentifies or buries it; **missed** = otherwise. A finding is scored caught
wherever in the output it appears, not only under the check the benchmark files it under. The same
method and the same adjudicator as Run 2, in one sitting for both where the comparison mattered.

- **High confidence** — the 28 misses and the 45 catches at Highest and High. Each miss was tested
  against the retrieval extract to establish whether the evidence was in the pack.
- **High confidence** — §1's claim that the packs are identical. It is a whole-file diff returning
  two lines.
- **Moderate** — the Partial/Missed boundary at Moderate and Low. Some rest on the sentences around
  a token match.
- **Stated, not measured** — §3's explanation of *why* the model matters more here than on Test
  Case 2, and §6's split between the extraction and the assessor. One swap changed both; nothing
  here separates them.

Three findings are unreachable from the converted evidence, as the benchmark's own conversion note
says: F3.3, F4.1 and F10.3 depend on questionnaire tables the Markdown conversion dropped
(`Very little understanding` and `significant outstanding debts` appear nowhere in either pack).
Excluding them, Run 3 is **45 caught of 80 reachable — 56%**. Run 3 handles F4.1 better than Run 2
did without reaching it: Run 2 affirmatively cleared the knowledge claim, while Run 3 records that
the file is silent where the report is specific.

---

## 11. What to do next

1. **Re-run Test Case 2 on Sonnet with the current build.** The 2×2 is now half-built on each case
   and gives opposite answers. Until that cell exists, no statement about "the changes" or "the
   model" is safe on either case.
2. **Two runs per configuration, still not done.** Thirty-one verdicts moved here and there is no
   noise floor for either configuration. The 3-of-78 floor was measured on one case at an identical
   fingerprint, and does not transfer.
3. **The comparison-basis clause needs code, not words.** Two models, identical packs, same
   date-basis violation, both quoting both dates. Carry as-at dates on the two sides of a
   comparison and tell the assessor when they differ.
4. **Cap the routed inconsistency.** One extraction inconsistency reached 26 of 88 groups. Route by
   path overlap *and* limit the number of groups any single inconsistency can enter.
5. **Fix the silent group drop.** Six groups across three runs assessed and never printed. This is a
   correctness bug in the report and is independent of recall.
6. **F6.1 needs a different mechanism entirely.** The passage is retrieved, routed to the right
   groups, and now quoted in the output. Nothing about retrieval or model capability will fix it;
   the group prompt does not ask any question that this passage answers.

**The conclusion: Sonnet on the current build reaches 54% on an unseen case against 55% on the
tuned one, at 3.1× the cost — and the Test Case 2 finding that the model barely matters is wrong.
On this case the model is worth as much as both rounds of engineering combined.**
