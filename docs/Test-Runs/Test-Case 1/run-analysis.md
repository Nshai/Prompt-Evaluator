# Run analysis — Runs 7, 8 and 9

Three runs of case ABC-99 on 2026-08-26, scored against
[expected-results-benchmark.md](expected-results-benchmark.md) under
[scoring-rubric.md](scoring-rubric.md).

**These three runs vary exactly one thing: the assessor model.** Same plans, same canonical
model, same settings, same embeddings — and, as it turns out, byte-identical retrieval. That has
not been true of any previous comparison in this repository, and it makes these the first numbers
here that attribute cleanly to anything.

| | Run 7 | Run 8 | Run 9 |
| --- | --- | --- | --- |
| Assessor model | `claude-haiku-4-5` | `claude-sonnet-4-6` | `nova-2-lite-v1:0` |
| **Caught / Partial / Missed** | **20 / 10 / 6** | **28 / 5 / 3** | **7 / 15 / 14** |
| Recall | 56% | **78%** | 19% |
| Cost | £2.09 | £6.44 | £3.90 |
| **Cost per finding caught** | **£0.105** | £0.230 | £0.557 |
| Wall clock | 281s | 620s | 149s |
| Output tokens | 149,582 | 160,947 | 84,667 |

Noise floor is **±2 findings / ±5 points**, measured. Run 8 beats Run 7 by 8 findings and Run 7
beats Run 9 by 13 — both well outside it. The previous best recorded on this benchmark was **20 /
36 (56%)**, so **Run 8 is a new high by 8 findings** and Run 7 merely matches the old best under a
completely different configuration.

---

## 1. Provenance — what these runs actually are

The rubric's first procedural step is to establish a run is genuine before scoring it. All three
are.

| | Wall clock | Output tokens | Rate | Verdict |
| --- | --- | --- | --- | --- |
| Run 7 | 281s | 149,582 | 532 tok/s | genuine |
| Run 8 | 620s | 160,947 | 260 tok/s | genuine |
| Run 9 | 149s | 84,667 | 567 tok/s | genuine |

Gateway replays reproduce 26,000–49,000 tok/s. Nothing here is close.

**Everything except the model is held constant, and this was verified rather than assumed:**

- **Plans** — all three fingerprints read `plans 10@fb7a2d0ab61d`. Recomputing
  `DigestOfFolder` over the current `docs/artifacts/check-plan/` gives `fb7a2d0ab61d` exactly, so
  these runs used the plans as they now stand, including the nine queries and ten section hints
  added that morning.
- **Canonical model** — all three read `model 8a0120d90ea2`, extracted 2026-08-26 10:03 from one
  extraction pass. Runs 7 and 8 carry the same extract log; Run 9 reuses the stored model.
- **Settings** — `top 8/search, 24/group (reserving 1/section, 1/category)`, extraction cap
  32,000. This is the **first run on record at `maxPassagesPerGroup: 24`**; every previous run was
  at the hard-coded 12.
- **Retrieval** — identical to the passage: **223 searches, 3,520 hits, 224 canonical paths
  resolved, 31 absent** in all three. Retrieval is plan-driven and model-independent, and the
  numbers prove it.

**One caveat that applies to all three.** The fingerprints read *"temperature default, top-p
default, seed not pinned"*, against [optimal-configuration.md](optimal-configuration.md)'s
recommendation to pin all three. **Determinism is untested here**, so each figure is one sample
from an unmeasured distribution. The gaps are large enough to survive that; the exact numbers are
not.

**The extraction was clean.** *"Sections whose extraction pass failed: none."* No section was
lost, `existingArrangements` merged, and the five plans' risk ratings (9, 4, 2, 7, 4) appear in
all three outputs. Whether the duplicate-key defect was *survived* or simply did not recur cannot
be told from one run — the extractor logged no duplicate-key shortfall, so the model did not
stutter this time. The reader fix and the prompt rule landed together and cannot be separated on
this evidence.

---

## 2. Scores

Verdicts under the rubric's definitions. **C** = Caught, **P** = Partial, **M** = Missed.

| Finding | Substance | R7 | R8 | R9 |
| --- | --- | :-: | :-: | :-: |
| F1.1 | £300/wk (≈£1,300/mo) vs fact find £1,200 | C | C | C |
| F1.2 | State Pension £230 **weekly** vs £230 **monthly** | C | C | **M** ⚠ |
| F1.3 | Kim's £1,100 and £20/wk not in the fact find | C | C | C |
| F1.6 | Cashflow models a residence for a tenant | M | M | M |
| F1.8 | £110,000 / £106,000 vs £116,998.47 | C | C | C |
| F1.9 | Three plans absent from the fact find schedule | M | M | M |
| F2.1 | Five unreconciled retirement horizons | C | C | P |
| F2.2 | CFL "<3 years" / "Short Term" vs a 10-year case | M | C | M |
| F3.1 | File note says risk 6, report says 5 | C | C | P |
| F3.3 | Investment Period 2 years vs 10-year justification | C | C | P |
| F3.4 | People's Pension retained at risk 9 against agreed 5 | P | P | M |
| F3.5 | Every ATR answer cautious; natural level 4 | C | C | P |
| F4.1 | "No understanding / knowledge" vs "comfortable" | C | C | C |
| F4.2 | ATR: "I have no understanding of investments" | P | P | P |
| F4.3 | Balanced description's precondition contradicted | C | C | M |
| F4.4 | SIPP + drawdown, no comprehension tested | P | C | P |
| F5.1 | Disposable −£288 vs "comfortably meets" | C | C | C |
| F5.2 | Affordability rests on unrecorded income | C | C | P |
| F5.3 | £6,000 emergency fund scheduled to be spent | P | P | P |
| F5.4 | Cashflow includes a residence the client rents | M | M | M |
| F6.1 | Research ranks doing nothing above the advice | C | C | M |
| F6.2 | Same rationale bullets across three switches | C | C | C |
| F6.3 | "Lower Costs" on a switch that raises charges | P | P | M |
| F7.1 | The report's two charge tables contradict | P | C | P |
| F7.2 | Standard Life row computed on Zurich's fund | M | C | M |
| F7.3 | "4.24% more expensive" adds instead of subtracts | C | C | C |
| F7.5 | 4.8% critical yield justified by past performance | C | C | P |
| F8.1 | £12,000 / 9.4% reduction only in the appendix | P | C | P |
| F8.2 | Ranking above the recommendation never disclosed | M | C | M |
| F9.1 | Switch reduces maturity at every growth rate | P | C | P |
| F9.2 | Ranking places the existing arrangement higher | C | C | M |
| F9.3 | Standard Life switch raises charges, sold on cost | P | P | M |
| F9.4 | Retaining Zurich projects £128,000 vs £125,000 | C | C | M |
| F9.5 | Existing charge figures internally inconsistent | P | C | P |
| F10.1 | No understanding = FG21/1 capability driver | C | C | P |
| F10.2 | 69, moving all provision, no screening recorded | C | C | P |
| | **Caught** | **20** | **28** | **7** |
| | **Partial** | **10** | **5** | **15** |
| | **Missed** | **6** | **3** | **14** |

**Scored under R8 (raised and not carried):** F3.1 in Run 8, which names the conflict and then
discounts it as *"likely a drafting error superseded by the final report"*. On the strict reading
that a report disclaiming a finding has not delivered it, **Run 8 scores 27/36 rather than 28**.
No finding in Run 7 or Run 9 turns on R8.

**Scored under R1 (caught under any check):** F5.1 and F1.8 land in CHK-001 rather than CHK-005 in
Run 9; F6.1, F9.2 and F9.4 all land in CHK-009 rather than CHK-006 in Runs 7 and 8.

---

## 3. What separates the models

### Run 8 wins on arithmetic it performs itself

The eight findings Run 8 catches that Run 7 does not are **F2.2, F4.4, F7.1, F7.2, F8.1, F8.2,
F9.1, F9.5** — and six of the eight turn on doing a calculation rather than noticing a phrase.

The clearest is **F7.2**, which no other run reached:

> EA5 monetary charge: 0.18% of Standard Life fund value £3,002 = £5.40, not £186.19. £186.19 at
> 0.18% implies a fund of ~£103,439 (the Zurich fund value), confirming row transposition.

That is the benchmark's finding exactly, derived, not spotted. G7.4's guard asks for precisely
this — *"Recompute: a monetary charge that is not the stated percentage of the stated plan value
indicates transposed or erroneous rows"* — and Run 8 is the only model that acted on it. The guard
was in the plan for all three runs.

**F7.1** is the same story:

> EA5 (Standard Life): report page 5 states fund charge 0.52%; the switch comparison table (page
> 11) uses 0.18% as the existing charge for EA5. These are contradictory within the report itself.
> 0.18% belongs to EA4 (Zurich), not EA5.

Run 9 has both figures and attributes the difference to a provider document rather than to the
report's own two tables — the right area, the wrong contradiction, which the rubric scores Partial
under R4. Run 7 never joins them.

### Run 7 is the value play

At **£0.105 per finding caught**, Run 7 is more than twice as efficient as Run 8 and five times
Run 9. It matches the best recall ever recorded on this benchmark for a third of Sonnet's cost.
If the question is "what catches the most per pound", Haiku wins on this evidence and is not
close.

If the question is "what catches the most", Sonnet wins by 8 findings, and three of those (F7.1,
F7.2, F8.2) are Highest-severity contradictions a reviewer would escalate first.

### Run 9 should not be used

Nova 2 Lite is **the worst recall and the second-highest cost**: £3.90 to catch 7 findings against
Haiku's £2.09 for 20. It writes 47% fewer output tokens than Sonnet and reaches Partial far more
often than either — 15 of 36, the signature of a model that retrieves the right area and does not
close the argument.

It also produced **the only false negative in the three runs**, on F1.2:

> The report asserts C1 State Pension as £1,000/month net, but evidence shows £230/week in [P10]
> which annualises to £12,040/year or £1,003.33/month — **close but not exact**

The fact find records £230 **monthly**. Run 9 read it as weekly, did the arithmetic, and concluded
the report was approximately right — inverting a Highest-severity finding into a reassurance. The
rubric counts this as Missed and requires it be flagged separately, because a wrong statement is
worse than silence: a reviewer reading that sentence has been actively told there is no problem.

Runs 7 and 8 both got it right, Run 8 with the corroborating cross-check:

> Cashflow section [P9] states state pension income of £11,973 per annum … but fact find records
> £230/month (£2,760/year) — these cannot both be correct.

---

## 4. What the plan changes did, and did not, do

Nine queries and ten section hints were added to the plans that morning. This is the first run
that measures them.

### The ranking query worked, and it was the largest single gain

**F6.1, F9.2 and F9.4 were previously unreachable** — no query in 85 groups contained "rank",
"maturity value", or any of the ranked figures. All three are now Caught in both Run 7 and Run 8:

> The pension switch report [P3] shows rankings by maturity value at mid-growth rate: Zurich
> pension ranked 1st at £128,000 (0.2% RIY), Existing Solution 2nd at £127,000 (0.3% RIY), New
> Solution 5th at £125,000 (0.6% RIY). *(Run 7, CHK-009 G9.8)*

Run 8 adds **F8.2**, the disclosure failure that follows from it:

> The report's rationale emphasises cost savings … but does not acknowledge that the projected
> maturity value is lower under the new solution in all modelled scenarios. *(Run 8, CHK-006 G6.4)*

**Four findings, three of them Highest, from one query text repeated in four groups.**

### Six of the ten new section hints never fired

All three runs report, identically:

> Section hints matching nothing: 6 — G1.5: Residency Status; G2.2: ranked by Maturity Value;
> G5.5: LIQUIDATED; G6.4: ranked by Maturity Value; G8.8: ranked by Maturity Value; G9.7: ranked
> by Maturity Value.

**This is a defect in my verification, and it is worth stating plainly.**
`SectionHintReachabilityTests` checks that a hint's string exists in a document belonging to a
category the group searches. That is necessary and it is not sufficient: the hint is matched
against **passages the search returned**, not against the document. If no query retrieves the
chunk containing the string, the hint has nothing to promote and does nothing — silently, which is
the exact failure mode the test was written to prevent.

The four that did fire — `incorporates the assets of your main residence` (G1.11, G5.2),
`No understanding / knowledge` (G4.1), `Existing Arrangements` (G7.4), `Effect of switching`
(G8.1, G8.5) — cover the findings where the retrieval half plainly worked. F7.1 and F7.2 land in
G7.4 in Run 8; F4.1 lands in G4.1 in all three.

The ranking findings were caught **despite** their hints failing, because the query text pulled
the switch report into the pack on its own and the passage cap did not bite. The hint was
insurance that was not needed. That is luck, not design, and at 12 passages per group rather than
24 it might well have gone the other way.

### Three findings were retrieved and then reasoned away

**F1.6 and F5.4** are the sharpest disappointment. The report's assertion reached the assessor —
the `incorporates the assets of your main residence` hint matched — but **the word "tenant" does
not appear anywhere in any of the three outputs** except in the case's own tenant id. The fact
find's `Residency Status: Tenant – private` never arrived, because *that* hint (G1.5) was one of
the six that matched nothing. Half a contradiction is not a finding, and all three models
correctly declined to make one.

**F3.4** is worse, because everything needed did arrive. The People's Pension's risk rating of 9
is quoted in all three runs. Then G3.7's guard sent it away:

> Existing arrangements show varying risk ratings on provider scales: Risk Level 9, 4, 2, 7, and 4
> … which are on 'Provider scale' and **marked as not comparable to the client scale**.
> *(Run 7, CHK-003 G3.7)*

The guard — *"Compare on one scale only; where scales differ and no mapping is given, report the
incomparability rather than a breach"* — is correct in general and wrong here, because the report
itself claims the retained fund *"aligns with your risk appetite"*. This suppression was predicted
when the guard was reviewed and is now observed: **a guard written to prevent a false positive is
costing a Highest-severity true one.** It is the clearest actionable defect this analysis found.

---

## 5. The three findings no model reached

| | Why |
| --- | --- |
| **F1.6 / F5.4** | The fact find's tenancy never reaches the output. One dead section hint, and no query text names residency status. |
| **F1.9** | Nothing anywhere relates the report's five plans to the fact find's pension schedule. G1.7 is a `ValueMatch` group whose four guards are all about value differences; membership — a plan present in one document and absent from the other — is not something it is asked to test. |

These are **pipeline defects, not model defects**: three different models with three very
different capability profiles all failed identically. F1.9 was flagged as a known gap when the
plans were last reviewed and remains one.

---

## 6. Reading the outcome column

All three runs returned **10 Potential Concern, 0 No Issue, 0 N/A**, matching the benchmark's
expected outcomes 10/10. As the rubric says, record it and never lead with it: a pipeline
returning Potential Concern unconditionally scores the same.

One thing the outcome column does show is worth noting. **CHK-009 ran in all three runs**, and it
is where F6.1, F9.2 and F9.4 were caught. Before the applicability fix, an extraction failure on
`existingArrangements` would have skipped that check entirely and reported N/A. The extraction did
not fail here, so the fix was not exercised — but the check that would have been silently lost is
the one carrying three of Run 8's Highest catches.

Group-level verdicts, which the outcome column hides:

| | Run 7 | Run 8 | Run 9 |
| --- | --- | --- | --- |
| Group blocks rendered | 81 | 82 | 81 |
| Potential Concern | 74 | 76 | 73 |
| **Indeterminate** | 2 | **0** | 1 |
| Quotes rejected as unverifiable | 116 | 59 | 37 |

Run 8 is the first run on record with **no Indeterminate groups at all** — gap A3 does not fire
once. Its unverified-quote count is also half Run 7's, which bears on gap A2 (citation
verification flagging formatting as fabrication): a model that quotes more carefully trips it
less. Run 9's low count reflects writing less, not quoting better.

---

## 7. What to do next

1. **Fix the G3.7 guard.** Add: where the report claims alignment for a specific retained
   arrangement, the incomparability of scales does not excuse the claim — report the conflict.
   One finding (F3.4, Highest), and the evidence is already reaching the assessor.
2. **Strengthen the section-hint test to prove retrievability, not existence.** The current test
   passes on six hints that do nothing. Either assert against a recorded run's retrieved passages,
   or drop hints whose string no query plausibly retrieves and rely on query text alone.
3. **Give F1.9 a membership test.** G1.7 compares values; it needs to be asked whether every plan
   in the report appears in the fact find's schedule.
4. **Reach the fact find's residency.** F1.6 and F5.4 need the evidence half, which means query
   text naming residency status, not only a section hint.
5. **Pin sampling and run each model twice.** Every number here is one sample with sampling
   unpinned. The Sonnet–Haiku gap is 8 findings and will survive; nothing smaller should be
   trusted until determinism is measured.
6. **Decide the cost question deliberately.** Haiku at £0.105/finding and Sonnet at £0.230 are
   both defensible. Sonnet's extra 8 findings include three Highest-severity contradictions and it
   produced no Indeterminate groups; Haiku costs a third as much and matches the previous best.
   A tiered arrangement — Haiku by default, Sonnet on checks whose findings turn on arithmetic
   (CHK-007, CHK-009) — is worth measuring and is not yet supported by any evidence here.

**`maxPassagesPerGroup: 24` remains unmeasured.** All three runs used it and none used 12, so its
effect is confounded with everything else that changed. The comparison worth running next is one
model at 12 against the same model at 24, changing nothing else.

---

## 8. Limits of this analysis

- **One adjudicator, one pass.** The rubric exists because two adjudications of the same output
  once differed by 5 points. Treat ±2 findings as the floor.
- **Sampling was not pinned**, so no run here is reproducible and the variance is unknown.
- **Four things changed at once** between these runs and the previous best: the plans, the
  extraction fixes, the passage cap, and the assessor model. The *model* comparison within these
  three is clean; the comparison to Run 6 and earlier is not, and no recall delta against them
  should be attributed to any single change.
- **False positives are not measured.** Every check is expected to land on Potential Concern, so
  this dataset cannot measure them at check level at all. Run 8 produced the most findings overall
  and some are wrong — its F7.3 bullet confuses the 4.24% cost figure with a critical yield — and
  nothing here counts that.
