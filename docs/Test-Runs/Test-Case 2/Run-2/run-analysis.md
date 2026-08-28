# Run analysis — Test Case 2, Run 2 (and comparison with Run 1)

One run of case Test-Case-2 (Barnes) on 2026-08-28, scored against
[expected-results-benchmark.md](../expected-results-benchmark.md) and compared with
[Run 1](../Run-1/run-analysis.md).

**Run 2 swaps `claude-haiku-4-5` for `claude-sonnet-4-6` at both the extraction and the assessment
stage. Retrieval is byte-identical between the two runs** — the two retrieval extracts differ in
exactly one line, the header naming the model. Every scoring difference below is therefore
attributable to the model, at one stage or the other, and to nothing else.

| | Run 1 | Run 2 | |
| --- | --- | --- | --- |
| Model (extract + assess) | `intelliflo-claude-haiku-4-5` | `intelliflo-claude-sonnet-4-6` | |
| **Caught / Partial / Missed** | **23 / 8 / 16** | **28 / 2 / 17** | **+5 caught** |
| Recall (caught) | 49% | **60%** | +11 pts |
| Recall (caught + partial) | 66% | 64% | −2 pts |
| Recall excluding CHK-009 | 56% / 76% | **68%** / 73% | +12 pts |
| Check-level agreement | 9/10 | 9/10 | — |
| NOT ASSESSED groups | 9 | **2** | −7 |
| Untraceable citations | 85 | **37** | −48 |
| Wrong requirement id echoed | **49 of 78** | **0 of 78** | −49 |
| Severity High / Moderate / Low | 18 / 42 / 3 | 12 / 46 / 12 | recalibrated |
| Cost (extract + checks) | £2.59 | £8.59 | **3.3×** |
| Cost per finding caught | £0.113 | £0.307 | 2.7× |
| Wall clock (checks) | 230s | 409s | 1.8× |
| Output tokens | 133,763 | 145,931 | +9% |

**Headline: Sonnet converts partials into catches, stops declining to answer, and stops
misidentifying which requirement it is answering — at 3.3× the cost. It does not fix any of the
structural defects, and it loses three findings Haiku had.**

---

## Contents

1. [Provenance and what varied](#1-provenance-and-what-varied)
2. [CHK-009 fails identically — proof the defect is in the plan](#2-chk-009-fails-identically--proof-the-defect-is-in-the-plan)
3. [Finding-level scoring](#3-finding-level-scoring)
4. [What Sonnet gained](#4-what-sonnet-gained)
5. [What Sonnet lost](#5-what-sonnet-lost)
6. [What neither model reached](#6-what-neither-model-reached)
7. [False positives](#7-false-positives)
8. [Self-diagnostics compared](#8-self-diagnostics-compared)
9. [Findings the runs surfaced that the benchmark does not have](#9-findings-the-runs-surfaced-that-the-benchmark-does-not-have)
10. [What to do next](#10-what-to-do-next)
11. [The full benchmark](#11-the-full-benchmark) — all 75 material findings, not only the 47

---

## 1. Provenance and what varied

Genuine, not a replay: **409s for 145,931 output tokens = 357 tok/s** (replays reproduce
26,000–49,000 tok/s). Slower per token than Run 1's 580 tok/s, as expected for the larger model.

**Held constant, and verified rather than assumed:**

- **Plans** — both fingerprints read `plans 10@09cf0a31b103`.
- **Retrieval** — `207 searches, 3,252 passages` in both, and identical per check
  (30/480, 21/324, 28/446, 20/300, 27/432, 13/202, 27/432, 21/336, 20/300). Pack selection identical:
  `1,671 of 2,176 … 505 (23%) dropped`. Section hints failing: the same 14, in the same order.
- **Retrieval extract** — 39,436 lines each; `Compare-Object` returns **two differing lines**, which
  are the two halves of the one header line naming the model and run time. Same passages, same
  ranks, same evictions, same packs.
- **Vector store** — Run 2 reused Run 1's index (598 chunks); its embedding spend is £0.0065, the
  query embeddings only.

**Varied — two things, not one.** Sonnet did both the extraction *and* the assessment, so this is a
model swap across two stages. The canonical models differ (`888cecaca118` → `9d8bcf0bc622`,
325,372 → 369,353 characters) and the differences are material:

| Canonical model | Run 1 (Haiku) | Run 2 (Sonnet) | Consequence |
| --- | --- | --- | --- |
| `riskAssessment[].investmentTermYears` | absent | **5** | Run 2's G3.4 could reason about the term; Run 1's returned NOT ASSESSED |
| `checkTriggers.hasComplexProduct` | false | **true** (settled from the model) | Run 2 ran and reported G4.4 |
| `checkTriggers.hasVulnerabilityIndicators` | false | **true** (settled from the model) | Stronger CHK-010 |
| `checkTriggers.hasDecumulation` | true | false | No observed effect |
| `capacityForLoss.rating` | **High** (normalised from "Medium-High") | **Unspecified** | Both wrong, differently — see FP #4 |
| `existingArrangements[].adviceAction` | `RetainAndSwitchFunds` | `RetainAndSwitchFunds` | **identical** — see §2 |

Sonnet's extraction also introduced a mechanism Haiku's did not report: *"2 check trigger(s) were
settled from the model"*, correcting two triggers it had first written as false. That is a genuine
improvement and it changed which groups ran.

Sampling is unpinned in both runs, so neither is exactly reproducible and there is no measured noise
floor on this case. On Test Case 1 the noise floor was ±2 findings; the +5 here sits outside that,
but only just, and the caveat should travel with the number.

---

## 2. CHK-009 fails identically — proof the defect is in the plan

Both runs returned **N/A** for CHK-009, with the same sentence:

> Applicability not satisfied: adviceActions (found **RetainAndSwitchFunds**).

Both canonical models recorded `adviceAction: "RetainAndSwitchFunds"` and
`checkTriggers.hasReplacementOrSwitch: true`. Sonnet, a materially stronger extractor that fixed two
other triggers on its own initiative, produced the same value — because it is the *correct* value.

The defect is entirely in
[`CHK-009.query-plan.json`](../../../Implementation/check-plan/CHK-009.query-plan.json), whose second
applicability rule lists `["Switch","PartialTransfer","FullTransfer","Encash","Surrender"]` and
excludes `RetainAndSwitchFunds`. The canonical schema agrees with the plan and both disagree with
COBS: a 100% fund switch inside a retained wrapper is replacement business.

**This is now demonstrated rather than inferred.** Model quality cannot recover these six findings.
Adding one string to the plan can.

---

## 3. Finding-level scoring

Scored against the benchmark's 47 material findings (48 listed, less the withdrawn F3.3).

| ID | Run 1 | Run 2 | |
| --- | --- | --- | --- |
| F1.1 expenditure £2,493 / £2,206.67 / £3,710 | Caught | Caught | |
| F1.2 "stopped working" vs Fact Find *Employed* | Caught | Caught | |
| F1.3 "current earnings are sufficient" | Caught | Caught | Run 2 raises it in six groups |
| F1.4 Yvette £28,000 vs £16,800 | Caught | Caught | |
| F1.5 UFPLS not available | Missed | **Missed** | |
| F1.7 Fact Find denies a final salary scheme | Partial | **Missed** | ▼ regression |
| F2.1 retirement horizons | Caught | Caught | |
| F2.2 Yvette's retirement three ways | Caught | Caught | |
| F2.3 two unsupported objectives | Caught | Caught | Run 2 finds a third (IHT monitoring) |
| F2.4 Cash ISA "not dead set on doing this" | Caught | Caught | |
| F2.5 £2,200 target vs expenditure | Caught | Caught | Run 2 gives it a dedicated group (G2.8) |
| F3.1 CFL questionnaire — spouse "Strongly disagree" | Missed | **Missed** | |
| F3.2 no CFL rating; "No additional notes" | Caught | Caught | |
| F3.4 risk goal term vs no term stated | Partial | **Caught** | ▲ extraction supplied the 5 years |
| F4.1 no understanding of unit trusts / GIA | Caught | Caught | |
| F4.2 no understanding of pension planning | Caught | Caught | |
| F4.3 no comprehension check | Caught | Caught | |
| F5.1 deficit if he has stopped working | Partial | Partial | Neither does the subtraction |
| F5.2 "current situation" cashflow is post-advice | Partial | **Caught** | ▲ |
| F5.3 property EXCLUDED FROM SCENARIO | Missed | **Missed** | |
| F5.4 cash modelled at the invested rate | Missed | **Missed** | |
| F5.5 initial fee taken wholly from the GIA | Missed | **Caught** | ▲ |
| F5.6 two expenditure figures | Caught | Caught | |
| F6.1 ranking / lower projected value suppressed | Partial | **Caught** | ▲ and correctly, not as a citation clash |
| F6.2 "Lower Costs" vs +0.08% | Caught | Caught | |
| F6.3 ISA 4.72% vs Moneybox 4.5%, gross | Partial | **Missed** | ▼ regression |
| F6.4 £3,600 contribution basis | Caught | Caught | |
| F6.5 five options, none discussed | Caught | Caught | Run 2 names OPT-3/4/5 specifically |
| F6.6 4.72% arithmetic | Partial | **Missed** | ▼ regression |
| F7.1 1% adviser charge loaded on the existing plan | Missed | **Missed** | closer, still wrong |
| F7.2 1.62% / 1.08% vs appendix £0 | Caught | Caught | |
| F7.3 transaction costs excluded | Missed | **Caught** | ▲ via the KIID, better than the benchmark route |
| F7.4 critical yield justified by past performance | Partial | **Caught** | ▲ with the underperformance table |
| F7.5 3% initial on the retained pension and ISA transfer | Missed | **Missed** | |
| F7.6 cumulative charge effect not in the body | Caught | **Partial** | ▼ regression |
| F8.1 decrease confined to the appendix | Caught | Caught | |
| F8.2 "much steeper losses" vs CT −11.32% in 2022 | Missed | **Missed** | |
| F8.3 45.28% vs 30.12% give-up | Caught | Caught | Run 2 uses a different, stronger dataset |
| F8.4 GIA tax never worked for this client | Caught | Caught | |
| F9.1–F9.6 (CHK-009) | Missed ×6 | **Missed ×6** | check never ran |
| F10.1 no vulnerability assessment | Caught | Caught | Run 2 names FG21/1 explicitly |
| F10.2 "Doesn't trust anyone" | Missed | **Missed** | |

**Net: 6 upgrades, 4 regressions (three to Missed, one to Partial), 37 unchanged.**

---

## 4. What Sonnet gained

**It stopped refusing to answer.** NOT ASSESSED fell from 9 groups to 2. Run 1 declined on
G3.4, G3.9, G3.11, G4.3, G6.2, G6.3, G7.7 and others, usually on the grounds that "the comparison
needs a value that was not available". In several cases the value *was* available — most glaringly
G6.3, which claimed *"the suitability report itself is absent from the evidence pack"* while 42
chunks of it were indexed and cited by thirty other groups. Run 2 makes no such claim.

**It read the two switch-report tables as two tables.** Run 1's G6.4 and G7.7 could not find the
ranking; Run 2's G6.4 states the finding cleanly:

> The pension switch comparison shows the new solution projects £100 less than the existing pension
> at both mid and high growth rates … **yet the report does not explicitly acknowledge that the
> recommended solution produces a lower projected maturity value than the existing arrangement.**

and G8.7 completes it: *"The report does not disclose this £100 like-for-like decrease in projected
maturity value; it only discloses the monetary charge differential of £63."* That is F6.1/F8.1 stated
as a disclosure failure rather than, as Run 1 had it, a citation mismatch.

**It quantified the performance argument.** Run 2's G7.5 is the strongest single finding in either
run:

> the recommended fund … has delivered 5-year cumulative performance of 36.21% whereas the existing
> combined strategy has delivered 58.01% … the recommended fund has materially underperformed the
> existing holding on **every** time horizon shown (1YR: 11.92% vs 15.91%; 3YR: 33.59% vs 42.07%;
> 5YR: 36.21% vs 58.01%; 10YR: 70.66% vs 136.32%), **directly contradicting the report's claim that
> past performance of the recommended fund supports achievability of the outperformance
> requirement.**

That closes F7.4 and re-evidences F8.3 from the research report rather than the report's own FE
table.

**It caught the transaction-cost defect by a better route than the benchmark.** F7.3 in the
benchmark rests on the research reports' 0.51% total. Run 2 instead went to the KIID:

> The report asserts transaction costs are 'already accounted for in the fund's performance figures,
> which are presented net of all charges' — [P3] KIID … states the ongoing charges figure 'excludes
> the costs of buying or selling assets for the Fund' … the claim that they are 'accounted for' in
> disclosed charges is **potentially misleading**.

**Extraction gains, not assessor gains.** F3.4 upgraded because Sonnet's extraction captured
`investmentTermYears: 5` where Haiku's left it absent. The check then had something to reason
against. This is worth separating: some of the +5 is a better *record of the report*, not better
*reading of the evidence*.

---

## 5. What Sonnet lost

Three findings Haiku reached and Sonnet did not, plus one downgrade. All four are cases where Run 1
raised something thin and Run 2, being better calibrated, did not raise it at all.

- **F6.3 and F6.6 — the ISA comparison.** Run 1's G6.1 flagged *"'recommended ISA funds have provided
  an average performance of 4.72% over a 5-year period' with comparison to 'Moneybox interest rate
  of circa 4.5%' … no Moneybox comparison appears in the evidence."* Run 2 never mentions 4.72% or
  Moneybox anywhere in 2,414 lines. This is the benchmark's defect **D** — an ISA transfer out of a
  4.5% cash account into a fund the adviser's own illustration shows returning 3.4% after charges —
  and Run 2 is silent on it.
- **F1.7 — the Aon final salary contradiction.** Run 1 raised it in G1.2's "raised and not carried"
  block. Run 2 does not raise it at all.
- **F7.6 — cumulative charge effect.** Run 1's G7.3 concluded *"the report does not present an
  aggregated all-in cost comparison or a clear statement of cumulative impact over time."* Run 2
  discusses the illustration's 1.9% RIY but frames it as a comparability question rather than a
  disclosure failure.

The pattern is consistent: **Haiku over-raises and catches things by accident; Sonnet under-raises
and loses them.** Severity distribution shows the same effect from the other side — Run 1 marked 18
groups High, Run 2 twelve, and Run 2 moved 9 groups into Low.

---

## 6. What neither model reached

Nine findings survive both runs. They divide cleanly, and the division is the actionable result of
this comparison.

### Class A — the plan (6)

F9.1–F9.6. Zero retrieval, zero cost, silent N/A. **Not a model problem** (§2).

### Class B — evidence in the pack, unread by two different models (3)

Since retrieval is byte-identical, the pack membership established in the
[Run 1 analysis](../Run-1/run-analysis.md#5-why-the-misses-happened) holds exactly for Run 2:

| Finding | Where the evidence sat | Run 1 | Run 2 |
| --- | --- | --- | --- |
| **F3.1** CFL questionnaire | `G3.2` pack, **rank 6 of 24** | "no independent assessment of capacity for loss" | *"has no recorded responses for the capacity for loss section"* |
| **F1.5** UFPLS not available | `G1.7` pack, **rank 4**; also `G6.5` [2], `G4.6` [4] | "No contradiction … exists" | not raised |
| **F10.2** "Doesn't trust anyone" | retrieved 10 times | not raised | not raised |

F3.1 is the sharpest result in this comparison. Run 2's own **G3.10** states *"[P16] shows the raw
CFL questionnaire responses but no derived rating or label is visible in those passages"* — it
**knows the document is there** — while **G3.2**, in the same check, says the questionnaire *"has no
recorded responses for the capacity for loss section, meaning there is no evidenced structured
assessment underpinning the label."* Two groups of one check, four thousand lines apart in the same
log, contradicting each other about a passage ranked sixth in that very pack.

> **Corrected 2026-08-28.** An earlier draft of this row attributed *"the file holds no CFL
> questionnaire responses"* to G3.2. That sentence is not in the log. The wording above is quoted
> from lines 6637 and 8375 of `checks_Test-Case-2_20260828_171725.log`. The substance is unchanged
> and the pair is real — but it is a disagreement about the questionnaire's **responses** rather
> than about the questionnaire, which matters: it is why the cross-group check built for this
> defect keys claims on a document *and the part of it being claimed about*.

**Two models, one at 3.3× the cost, read past the same passage in the same position.** Whatever is
wrong here is not model capability.

The same applies to F5.3 (property `EXCLUDED FROM SCENARIO`, in-pack for G1.5/G2.4/G2.6, evicted for
G5.2), F5.4 (`4.5% expected return`, retrieved 49 times), F8.2 (`-11.32`, retrieved 8 times) and
F7.5.

### Class C — reasoning inverted, in both runs (1)

**F7.1.** Run 2 gets closer than Run 1 and still lands wrong. It now knows there are two tables —
G6.5 notes *"the critical yield comparison is described as being before the effect of any advice
charges, which the report does not make clear"* — but G7.11 has them **backwards**, describing the
£86,800/£86,700 pre-charge table as *"the post-adviser-fee comparison"*, and concludes only that the
two *"use different bases … which is not explained."*

Neither run asks the question that unlocks it: *why are the existing and new columns identical after
advice charges?* The answer — a 1% ongoing charge the client does not pay was applied to the existing
plan — is stated in the switch report (*"Subject to an existing advice charge: No"*), the policy
research sheet (*"Current Adviser charge: No"*) and the Aviva client report (£0.00 of adviser charges
over three years). All were retrieved.

---

## 7. False positives

Run 2 has fewer and milder false positives, but the largest one survives.

| # | Run 1 | Run 2 |
| --- | --- | --- |
| 1 | **Pre/post-advice-charge table confusion** presented as ten contradictions, severity High (G7.3, G7.8, G7.11) | **Persists in G7.3** as ten discrepancies at severity High — but G7.8 now applies the guard correctly (*"these projections exclude adviser charges and cover a different retirement age … so the guard applies"*) and drops to **Low**. Inconsistent within one check |
| 2 | *"No document mentions a 'Chartered savings account'"* — it appears 33 times in the run's own retrieval | **Gone** |
| 3 | *"the suitability report itself is absent from the evidence pack"* → NOT ASSESSED | **Gone** |
| 4 | Extraction normalised CFL `Medium-High → High`; reported as a report defect | **Recurs, inverted**: extraction wrote `Unspecified`, and G3.2 reports *"the canonical extraction failed to capture the label"* — correctly attributed this time, but still occupying a finding slot |
| 5 | *"the Fact Find estate planning section is blank … no evidence of a will"* (it is in Personal Details) | **Gone** |
| 6 | *"£1,558 at 0.62% implies £251,290"* — wrong rate | **Gone** |
| 7 | *"internally contradictory: absence of understanding and self-directed investment"* | **Softened** — G4.2 now says *"These two facts coexist in the file but the report conflates them ambiguously"*, which is fair |
| 8 | — | **New**: G5.1 reads the cashflow's £29,915 as *"approximately £2,493 per month essential items only"*. It is total expenditure **including tax and NI**; regular expenses are £26,480 (£2,206.67/month). See §9 — the observation is wrong but productive |

Five of Run 1's seven false positives disappear. The one that matters most — #1, at High severity,
prominently placed — does not.

---

## 8. Self-diagnostics compared

| Diagnostic | Run 1 | Run 2 | Reading |
| --- | --- | --- | --- |
| **Prompt adherence** | **49 of 78** echoed the wrong requirement id; 6 rewrote it | **0 of 78** wrong; 4 rewrote it | The single most dramatic difference. Haiku misidentified which of eleven requirements it was answering **two times in three**. This is the best available explanation for Class B, and it is now measured rather than hypothesised |
| **Citation trust** | 85 untraceable quotes | **37** | Both figures are inflated by table-row reformatting rather than fabrication (see Run 1 §7); the halving still indicates tighter quoting |
| **NOT ASSESSED** | 9 groups | **2** | |
| **Raised and not carried** | 6 groups, 23 differences | 3 groups, 11 differences | Run 1 discarded twice as much; two of its discards (F1.7, F3.4's 5-year term) were benchmark findings |
| **Section hints matching nothing** | 14 | **14, identical** | Plan-driven; model-independent. The four that matter (CFL questionnaire, investment horizon, Existing Arrangements, Effect of switching) fail in both |
| **Pack selection** | 505 of 2,176 dropped (23%) | **identical** | |

The prompt-adherence line is worth dwelling on. It is emitted by the runner, not the model, and it
went from 63% wrong to 0%. If requirement misidentification is what causes an assessor to read past
the passage it was given, then Class B should have shrunk — and it did not. F3.1, F1.5 and F10.2
survive a model that identifies its requirement correctly every time. **That rules out the most
attractive hypothesis and points instead at the pack itself**: 24 passages, most of them long, with
the decisive row buried in the middle of a wide Markdown table.

---

## 9. Findings the runs surfaced that the benchmark does not have

Run 2 produced five observations that are legitimate and absent from
[expected-results-benchmark.md](../expected-results-benchmark.md). They should be added, with the
usual caveat that a benchmark amended from run output is no longer fully independent.

1. **A cheaper risk-4 fund existed in the adviser's own research** (G6.4): *"the fund comparison in
   [P11] shows HSBC Global Strategy Conservative Portfolio AC at 0.10% AMC and CT Universal MAP
   Cautious Fund C Acc at 0.29% AMC, both at risk level 4, yet both are cheaper than the recommended
   Aviva fund at 0.41%; the report does not address why the lower-cost alternatives were rejected."*
   Material, and it strengthens F6.2.
2. **No recommendation states who will implement it** (G6.6, G6.8): `responsibleParty` is
   `NotStated` for all five, and `willAdviserImplement` is absent from the report entirely. The
   benchmark's F6.11 records this as **No issue** and is too generous — CHK-006's *What to Look For*
   asks explicitly for *"whether the adviser will arrange or implement the change"*.
3. **The CT KIID's minimum holding period contradicts the stated term** (G3.9, G8.2, G8.8): the fund
   *"may not be appropriate for investors who plan to withdraw their money within 5 years"* against a
   5-year investment term and a client planning to draw at 60.
4. **£60,000 → £57,120 → £52,326 is never reconciled** (nine groups). The benchmark treats
   £57,120 = £60,000 − £2,880 as internally consistent; the report never says so, and the cashflow
   uses a third figure.
5. **The £2,493 figure is traceable after all.** Run 2's G5.1 misreads it, but the misreading points
   somewhere useful: £29,915 ÷ 12 = £2,492.92, and £26,480 ÷ 12 = £2,206.67. The report's £2,493 is
   the cashflow's expenditure **including tax and NI**; £2,206.67 is regular expenses **excluding**
   it. F1.1's core stands — adding £800 discretionary and £5,000 of holidays to a figure that already
   contains them produces the fictitious £3,710 — but the benchmark should record where £2,493 comes
   from rather than presenting it as unexplained.

---

## 10. What to do next

The comparison changes the priority order set out in the Run 1 analysis.

**Confirmed by this run:**

| | Change | Recovers | Now proven by |
| --- | --- | --- | --- |
| **1** | Add `RetainAndSwitchFunds` to CHK-009's `adviceActions` | 6 findings | Two models, identical failure (§2) |
| **2** | Surface applicability-driven N/A as a warning | prevents recurrence | as above |
| **3** | Give the assessor the table caption with each passage | F7.1, kills FP #1 | Both runs invert it; Run 2 gets it backwards explicitly (§6 Class C) |

**Re-prioritised upward:**

| | Change | Why |
| --- | --- | --- |
| **4** | **De-duplicate near-identical chunks and never evict the group's top-scoring passage** | The eviction of the 0.737 passage in G3.2 is identical in both runs. Ranking is plan-driven and model-independent, so this is now the largest fixable lever on Class B |
| **5** | **Fix the converter's bold pseudo-headings** (`**Appendix**`) | The same 14 section hints fail in both runs. Four of them name the evidence behind F3.1, F3.4, F1.5 and F8.1 |
| **6** | **Cut pack width or foreground the decisive rows** | Two models read past `G3.2` passage [6]. With prompt adherence at 0/78 in Run 2, requirement confusion is excluded as the cause |

**Re-prioritised downward:**

| | Change | Why |
| --- | --- | --- |
| ~~9~~ | Investigate the 63% requirement-id mismatch | **Resolved** — Sonnet scores 0/78 and the Class B misses persist regardless. Not the cause |
| ~~10~~ | Re-run on a stronger assessor | **Done.** +5 findings for 3.3× the cost |

**On model choice.** Sonnet buys +11 points of caught-recall for 3.3× the spend, and buys something
harder to price: five fewer false positives, seven fewer refusals to answer, and better-calibrated
severity. On a compliance workflow where a reviewer reads every finding, the false-positive
reduction is probably worth more than the recall gain. But it also loses three findings, one of them
(F6.3) a headline defect of the case, and the £0.307-per-finding cost is 2.7× Haiku's.

**The honest summary is that neither model is the binding constraint.** Of the nine findings both
runs miss, six are a plan defect, one is a prompt defect, and the rest are evidence sitting unread
in a pack that both models received identically. Items 1–6 above are worth more than any further
model upgrade, and none of them requires one.

**Expected ceiling from items 1–3 alone, on Run 2's base:** 28 → ~35 caught of 47 (74%).

---

## 11. The full benchmark

Sections 3 to 6 score the benchmark's **scoring set** — *"the 47 material findings (Highest and
High)"*, which is what the benchmark nominates for recall. The per-check tables hold 28 further
material rows, 22 Moderate and 6 Low, that no analysis had adjudicated. Scored here, on the same
definitions.

| | Run 1 | Run 2 |
| --- | :-: | :-: |
| **Scoring set (47: Highest + High)** | | |
| Caught / partial / missed | 23 / 8 / 16 | 28 / 2 / 17 |
| Caught | **49%** | **60%** |
| Caught or partial | **66%** | **64%** |
| **Moderate and Low (28)** | | |
| Caught / partial / missed | 4 / 10 / 14 | 12 / 6 / 10 |
| Caught | **14%** | **43%** |
| **All material findings (75)** | | |
| Caught / partial / missed | **27 / 18 / 30** | **40 / 8 / 27** |
| Caught | **36%** | **53%** |
| Caught or partial | **60%** | **64%** |

**The wider set separates the two runs further than the scoring set does.** On the 47, Run 2 leads
by 11 points of caught-recall; on all 75 it leads by **17**. The extra 28 rows are where Haiku was
weakest — 4 caught against Sonnet's 12 — and they are mostly quiet evidential gaps rather than
arithmetic contradictions: an assertion no document supports, a figure attributed to the wrong
person, a scale used as though it were another scale.

That is consistent with what §4 and §5 found on the scoring set, and it sharpens it. Run 1's
apparent parity on "caught or partial" (66% against 64%) is an artefact of the narrower set: across
all 75 the two are level on that measure (60% against 64%) while Run 2 converts far more of it into
findings actually stated. **Run 1 gestures at roughly as much and states much less of it.**

### What moved

| Finding | Run 1 | Run 2 | Note |
| --- | --- | --- | --- |
| F1.8 plan value and impossible dates | Partial | **Caught** | Run 2 names 09/03/2026 "a data-entry anomaly" |
| F1.10 family details only in the report | Missed | **Caught** | Run 1 wrote *"Report [P12] corroborates"* — the report corroborating itself |
| F1.13 assets said to be jointly held | Missed | **Caught** | Run 2 spots the Cash ISA attributed to Yvette but recorded under Anthony |
| F2.6 £40,000 renovation arithmetic | Partial | **Caught** | The three-figure reconciliation |
| F3.6 Risk Rating 4 on two different scales | Partial | **Caught** | Run 2 names the 1–7 and 1–10 scales explicitly |
| F4.4 experience asserted as fact | Partial | **Caught** | Run 2 contrasts the questionnaire's conditional with the report's assertion |
| F8.6 derivatives and EM risks undisclosed | Missed | **Caught** | Run 1 does not mention derivatives at all |
| F10.3 "registered" Power of Attorney | Missed | **Caught** | Both print the same G6.9 pass; only Run 2 also says no PoA appears in the fact find |
| F10.6 accessible-format measures | Partial | **Caught** | Run 1 dismisses it as immaterial |
| F6.7 4.72% is a simple average | Partial | **Missed** | Run 1 at least challenged the figure's provenance |
| F7.7 £155,812 does not reconcile | Partial | **Missed** | Run 2 checks the initial fee, which is consistent, and stops |
| F10.4 all contact by telephone | **Caught** | Partial | Run 1 also spots *"Client will complete ATRQ in his own time"* |

### Missed by both, and worth noting

Ten rows survive both runs: **F1.11** (the fact find's own contact log disagrees on age), **F3.7**
(*"managed to remain within this risk tolerance"* — Run 1 concludes it **is** supported), **F3.8**
(realised volatility above the benchmark), **F5.7** (the pension is never drawn in any scenario),
**F6.8** (the Recommendation Summary contradicts the recommendation tables), **F7.8** (growth
assumptions differ across the file), **F7.9** (0.41% stated against 0.42% in the illustration),
**F9.7** (CHK-009 never ran), and both halves of the loss-illustration gap.

Neither run mentions 6.52, 0.42 or £51,694 anywhere. These are not reasoning failures — they are
figures that never reached an assessor, and they belong to the same class as the rest of §6's
Class B.

### Method

Scored by locating each finding's distinctive token in each run's `run-checks-output.txt` and
reading the surrounding finding. **`run-checks-output.txt` and not the checks log**: the log
contains the full prompts, so a search of it matches the evidence pack as readily as the findings
and inflates every count. Caught, partial and missed are the benchmark's own definitions.
