# Run analysis — Test Case 2, Run 4 (Haiku, with the changes)

One run of case Test-Case-2 (Barnes) on 2026-08-28 at 22:54, scored against
[expected-results-benchmark.md](../expected-results-benchmark.md) across **all 75 material
findings**, not only the 47 the benchmark nominates for recall.

**Run 4 is Run 3's configuration with the assessor swapped back to `intelliflo-claude-haiku-4-5`.**
That makes it the most informative run in the set, because it closes both arms of the experiment:

|  | Before the changes | After the changes |
| --- | --- | --- |
| **Haiku 4.5** | Run 1 | **Run 4** |
| **Sonnet 4.6** | Run 2 | Run 3 |

Retrieval between Run 3 and Run 4 is **byte-identical** — 42,390 lines, two differing, both halves
of the header naming the model. Same passages, same ranks, same packs. Every difference between
Runs 3 and 4 is the model; every difference between Runs 1 and 4 is the changes.

| | Run 1<br>Haiku, before | Run 2<br>Sonnet, before | Run 3<br>Sonnet, after | **Run 4**<br>**Haiku, after** |
| --- | :-: | :-: | :-: | :-: |
| Checks assessed | 9 of 10 | 9 of 10 | 10 of 10 | **10 of 10** |
| Groups | 78 | 78 | 88 | **88** |
| **Scoring set (47)** — caught / partial / missed | 23 / 8 / 16 | 28 / 2 / 17 | 29 / 6 / 12 | **29 / 6 / 12** |
| Caught | 49% | 60% | 62% | **62%** |
| **Moderate + Low (28)** | 4 / 10 / 14 | 12 / 6 / 10 | 13 / 6 / 9 | **12 / 4 / 12** |
| Caught | 14% | 43% | 46% | **43%** |
| **All 75** — caught / partial / missed | 27 / 18 / 30 | 40 / 8 / 27 | 42 / 12 / 21 | **41 / 10 / 24** |
| **Caught** | 36% | 53% | 56% | **55%** |
| **Missed** | 30 | 27 | 21 | **24** |
| Citation trust (untraceable quotes) | 85 | 37 | 30 | **141** |
| Wrong requirement id | 49 of 78 (63%) | 0 of 78 | 0 of 88 | **61 of 88 (69%)** |
| Raised and not carried | 6 groups / 23 | 3 / 11 | 1 / 3 | **2 / 4** |
| Cost (extract + checks) | £2.59 | £8.59 | £9.58 | **£2.97** |
| Cost per finding caught | £0.096 | £0.215 | £0.228 | **£0.072** |

**Headline: the changes moved Haiku from 27 caught to 41 — level with Sonnet — at a third of the
cost. On the 47-finding scoring set the two models now score identically, 29/6/12 each. What Haiku
does not buy is a finding a reviewer can check: 141 untraceable quotations against Sonnet's 30.**

---

## Contents

1. [The experiment closes](#1-the-experiment-closes)
2. [Scoring: the 47](#2-scoring-the-47)
3. [Scoring: the 28 Moderate and Low](#3-scoring-the-28-moderate-and-low)
4. [Where the two models disagree](#4-where-the-two-models-disagree)
5. [What Haiku costs: the working cannot be checked](#5-what-haiku-costs-the-working-cannot-be-checked)
6. [The cross-group document check is noisier on Haiku — and earned its place anyway](#6-the-cross-group-document-check-is-noisier-on-haiku--and-earned-its-place-anyway)
7. [Missed by all four runs](#7-missed-by-all-four-runs)
8. [What to do next](#8-what-to-do-next)

---

## 1. The experiment closes

### On a fixed model, the changes are worth 14 findings

Run 1 and Run 4 are the same assessor at the same case. Between them sit the plan fix, the pack
reservations, the near-duplicate collapse, the table-context window, the prompt clauses, the
cross-group document check — and `IgnoreTriggerProbe`.

| Haiku | Caught | Partial | Missed |
| --- | :-: | :-: | :-: |
| Run 1 — before | 27 | 18 | 30 |
| **Run 4 — after** | **41** | **10** | **24** |
| | **+14** | −8 | **−6** |

Both directions are outside the measured noise floor of three outcomes.

**Six of the fourteen are CHK-009 becoming reachable**, and that remains confounded exactly as it
was in Run 3: `trigger probes bypassed` was on, so the probe never ran, and Run 4's extraction —
like Run 3's — is not the one that produced the original failure. The other eight are not
confounded and are distributed across CHK-001, CHK-003, CHK-005, CHK-006 and CHK-007.

### On a fixed configuration, the models are level

Runs 3 and 4 share a pack, byte for byte.

| Scoring set (47) | Caught | Partial | Missed |
| --- | :-: | :-: | :-: |
| Run 3 — Sonnet | 29 | 6 | 12 |
| Run 4 — Haiku | **29** | **6** | **12** |

Identical. Not the same twelve missed — see §4 — but the same count, from the same evidence, at
£2.97 against £9.58.

Across all 75 Sonnet keeps a small edge (42 against 41 caught, 21 against 24 missed) which is
**inside the noise floor** and should not be read as an effect on this evidence alone.

### What still had no opportunity to act

Embeddings cost £0.0074 — query embeddings only. **The index was not rebuilt**, so the converter's
pseudo-heading promotion is untested for a second run, and the same 14 section hints match nothing:

> G1.1: Residency Status; G1.11: cashflow modeller; **G3.4: Capacity For Loss questionnaire
> responses**; G3.4: my investment horizon; G3.6: ATR Wording; …

Four of those name the evidence behind four findings that all four runs have now missed.

---

## 2. Scoring: the 47

| ID | R1 Haiku | R2 Sonnet | R3 Sonnet+ | **R4 Haiku+** | |
| --- | --- | --- | --- | --- | --- |
| F1.1 expenditure £2,493 / £2,206.67 / £3,710 | Caught | Caught | Caught | **Caught** | states the £1,504 / 68% gap |
| F1.2 "stopped working" vs *Employed* | Caught | Caught | Caught | **Caught** | |
| F1.3 "current earnings are sufficient" | Caught | Caught | **Missed** | **Caught** | ▲ recovers what R3 lost |
| F1.4 Yvette £28,000 vs £16,800 | Caught | Caught | Caught | **Caught** | |
| F1.5 UFPLS not available | Missed | Missed | Partial | **Partial** | best-worded yet — §4 |
| F1.7 Fact Find denies a final salary scheme | Partial | Missed | Partial | **Partial** | |
| F2.1 retirement horizons | Caught | Caught | Caught | **Caught** | |
| F2.2 Yvette's retirement three ways | Caught | Caught | Caught | **Caught** | |
| F2.3 two unsupported objectives | Caught | Caught | Caught | **Caught** | |
| F2.4 Cash ISA "not dead set on doing this" | Caught | Caught | Caught | **Caught** | |
| F2.5 £2,200 target vs expenditure | Caught | Caught | Caught | **Caught** | |
| F3.1 CFL questionnaire — spouse "Strongly disagree" | Missed | Missed | Missed | **Missed** | hint still dead |
| F3.2 no CFL rating; "No additional notes" | Caught | Caught | Caught | **Caught** | |
| F3.4 risk goal term | Partial | Caught | Caught | **Caught** | |
| F4.1 no understanding of unit trusts / GIA | Caught | Caught | Caught | **Caught** | |
| F4.2 no understanding of pension planning | Caught | Caught | Caught | **Caught** | |
| F4.3 no comprehension check | Caught | Caught | Caught | **Caught** | |
| F5.1 deficit if he has stopped working | Partial | Partial | Partial | **Partial** | |
| F5.2 "current situation" cashflow is post-advice | Partial | Caught | Caught | **Caught** | |
| F5.3 property EXCLUDED FROM SCENARIO | Missed | Missed | Missed | **Missed** | |
| F5.4 cash modelled at the invested rate | Missed | Missed | Missed | **Missed** | |
| F5.5 initial fee taken wholly from the GIA | Missed | Caught | Caught | **Caught** | |
| F5.6 emergency fund | Caught | Caught | Caught | **Caught** | |
| F6.1 lower projected maturity not disclosed | Partial | Caught | Caught | **Caught** | £86,800 → £86,700 stated |
| F6.2 "Lower Costs" costs more | Caught | Caught | Caught | **Caught** | |
| F6.3 ISA returns less than the cash it replaces | Partial | Missed | Missed | **Missed** | |
| F6.4 alternatives considered | Caught | Caught | Caught | **Partial** | ▼ |
| F6.5 Bed & ISA | Caught | Caught | Caught | **Caught** | |
| F6.6 "not applicable to this plan" | Partial | Missed | Missed | **Missed** | |
| F7.1 1% charge applied to the existing plan | Missed | Missed | Missed | **Missed** | §7 |
| F7.2 aggregated charge disclosure | Caught | Caught | Caught | **Caught** | |
| F7.3 pre/post-charge tables | Missed | Caught | Caught | **Caught** | |
| F7.4 0.62% vs 0.54% | Partial | Caught | Caught | **Caught** | |
| F7.5 transaction costs excluded | Missed | Missed | Missed | **Missed** | |
| F7.6 10–15 working day settlement | Caught | Partial | Partial | **Partial** | |
| F8.1 suppressed ranking | Caught | Caught | Caught | **Caught** | |
| F8.2 −11.32 | Missed | Missed | Missed | **Missed** | |
| F8.3 risk warnings | Caught | Caught | Caught | **Caught** | |
| F8.4 capital at risk | Caught | Caught | Caught | **Caught** | |
| **F9.1** switch reduces maturity; ranking suppressed | Missed | Missed | Partial | **Caught** | ▲ best of the four |
| **F9.2** 1% charge invalidates the comparison | Missed | Missed | Missed | **Missed** | |
| **F9.3** "Lower Costs" | Missed | Missed | Caught | **Caught** | |
| **F9.4** ISA out of 4.5% cash | Missed | Missed | Missed | **Missed** | |
| **F9.5** wrong existing product modelled | Missed | Missed | Caught | **Missed** | ▼ Sonnet-only |
| **F9.6** existing arrangement described inaccurately | Missed | Missed | Partial | **Partial** | |
| F10.1 vulnerability not assessed | Caught | Caught | Caught | **Caught** | |
| F10.2 "Doesn't trust anyone" | Missed | Missed | Missed | **Missed** | |

**29 caught, 6 partial, 12 missed** — the same counts as Run 3, from the same pack, at a third of
the cost.

F9.1 is Run 4's best single result and no other run reached it:

> A ranked comparison of four pension funds (Aviva Multi-asset Plus II returning 36.21% over 5
> years vs alternatives at 31.28%, 25.90%, 21.49%) appears in the research [P10] but **is not
> referenced or discussed** in the report.

together with the projection pair — *"existing pension projects £86,800 while the new solution
projects £86,700 — a decrease of £100"*. Both halves of F9.1, stated.

---

## 3. Scoring: the 28 Moderate and Low

| ID | Sev | R1 | R2 | R3 | **R4** |
| --- | :-: | --- | --- | --- | --- |
| F1.6 6,900 vs 3,000 funds | Mod | Partial | Partial | Partial | **Partial** |
| F1.8 plan value and impossible dates | Mod | Partial | Caught | Caught | **Caught** |
| F1.9 State Pension unevidenced, no BR19 | Mod | Caught | Caught | Caught | **Caught** |
| F1.10 family details only in the report | Mod | Missed | Caught | Missed | **Missed** |
| F1.11 fact find disagrees on age | Low | Missed | Missed | Missed | **Partial** ▲ |
| F1.12 will and registered PoA unevidenced | Mod | Caught | Caught | Partial | **Missed** ▼ |
| F1.13 assets said to be jointly held | Low | Missed | Caught | Caught | **Caught** |
| F2.6 £40,000 renovation arithmetic | Mod | Partial | Caught | Caught | **Caught** |
| F3.5 Vanguard funds rated 5 and 7 by nothing | Mod | Partial | Partial | Caught | **Missed** ▼ |
| F3.6 Risk Rating 4 on two different scales | Mod | Partial | Caught | Caught | **Caught** |
| F3.7 "managed to remain within this tolerance" | Mod | Missed | Missed | Missed | **Missed** |
| F3.8 volatility above the benchmark | Low | Missed | Missed | Missed | **Missed** |
| F4.4 experience asserted as fact | Mod | Partial | Caught | Caught | **Caught** |
| F4.5 generic technical sections | Mod | Caught | Caught | Caught | **Caught** |
| F5.7 pension never drawn in any scenario | Mod | Missed | Missed | Missed | **Missed** |
| F6.7 4.72% is a simple average | Mod | Partial | Missed | Missed | **Missed** |
| F6.8 Recommendation Summary contradicts tables | Mod | Missed | Missed | Missed | **Missed** |
| F6.9 GIA research run on £60,000 | Low | Missed | Partial | Missed | **Missed** |
| F7.7 £155,812 does not reconcile | Mod | Partial | Missed | Partial | **Caught** ▲▲ |
| F7.8 growth assumptions differ | Mod | Missed | Missed | Partial | **Missed** |
| F7.9 0.41% vs 0.42% | Low | Missed | Missed | Missed | **Missed** |
| F8.5 no loss scenario illustrated | Mod | Partial | Partial | Caught | **Partial** |
| F8.6 derivatives and EM risks undisclosed | Mod | Missed | Caught | Caught | **Caught** |
| **F9.7** third-ranked solution | Mod | Missed | Missed | Caught | **Caught** |
| F10.3 "registered" Power of Attorney | Mod | Missed | Caught | Caught | **Caught** |
| F10.4 telephone contact, ATRQ alone | Mod | Caught | Partial | Partial | **Partial** |
| F10.5 Yvette relied on but never met | Mod | Missed | Partial | Partial | **Missed** |
| F10.6 accessible-format measures | Low | Partial | Caught | Caught | **Caught** |

**12 caught, 4 partial, 12 missed.**

F7.7 is the standout, and the only run of four to state it:

> the actual initial total to be invested is £159,792 … At £159,792, a 1% ongoing fee would be
> **£1,598, not £1,558**. The £1,558 figure appears to exclude the ISA and/or misstate the base.

---

## 4. Where the two models disagree

Same pack, same twelve-missed count, **different twelve**. Six rows separate them across all 75.

| Finding | Sonnet (R3) | Haiku (R4) | What the difference is |
| --- | :-: | :-: | --- |
| **F1.3** earnings sufficient for the contribution | Missed | **Caught** | Haiku states it flatly: *"Report states 'your current earnings are sufficient…' but Fact Find shows £0 employment income in cashflow first year, contradicting assertion"* |
| **F9.1** ranking suppressed | Partial | **Caught** | Haiku found the four-fund ranking in the research; Sonnet had the projection pair but not the ranking |
| **F7.7** ongoing fee base | Partial | **Caught** | Haiku does the arithmetic |
| **F1.11** fact find age | Missed | **Partial** | Haiku is the only run to surface *Age 56* against the report's 55 |
| **F9.5** wrong existing product modelled | **Caught** | Missed | Sonnet noticed the switch report modelled Multi-Asset Plus while every other document says Vanguard LifeStrategy |
| **F3.5** Vanguard funds rated 5 and 7 | **Caught** | Missed | Sonnet reconciled the alternatives section against the main body |

**The pattern is legible.** Haiku wins where the finding is one arithmetic step or one flat
contradiction between two stated figures. Sonnet wins where the finding requires holding two
documents' descriptions of the same object side by side and noticing they are different objects.

Neither is a capability ceiling on the other's misses: F1.3 is not hard, and Sonnet had the same
passage.

### One place Haiku words it better

F1.5 / F9.6, where Run 3's absence-discipline side effect was worst. Sonnet wrote *"No provider
document confirms UFPLS availability."* Haiku writes:

> Report states existing arrangement offers 'Full flexi-access drawdown and UFPLS available' **but
> product information [P13] states** 'No annuity options' and 'We do offer full and partial
> encashment from age 55. We offer flexi-access drawdown' **without specifying UFPLS capability**

That names the contradicting document and quotes it. It still stops short of the benchmark's claim
— the form states UFPLS is *not* available, not merely that it is unmentioned — so it remains
Partial, but it is the closest any run has come, and it supports the fix proposed in
[Run 3 §7](../Run-3/run-analysis.md#7-the-absence-clause-has-a-side-effect).

---

## 5. What Haiku costs: the working cannot be checked

| | Run 3 Sonnet | Run 4 Haiku |
| --- | :-: | :-: |
| Untraceable quotations | 30 | **141** |
| Wrong requirement id echoed | 0 of 88 | **61 of 88** |
| Requirement text rewritten | 12 | 9 |

**Citation trust is 4.7× worse.** Both runs were given identical passages. A finding whose
quotations cannot be traced back to the pack is a finding a reviewer must re-derive from scratch,
and 141 of them is most of the run.

**And the requirement-misidentification signature is back**: 61 of 88 responses echoed the wrong
requirement id, against Run 1's 49 of 78. This is emitted by the runner, not the model, so it is
measurement rather than opinion. It did not stop Haiku scoring 29 on the 47 — which is the same
finding the Run 2 analysis reached, that requirement confusion does not predict recall — but it
means the run's own labelling of *which* requirement each finding answers cannot be trusted.

There is also at least one apparent fabrication. Run 4's `G3.1` states:

> Capacity for loss questionnaire [P16] **shows responses supporting the medium-high capacity
> conclusion** documented in [P9]

No document in this case states a capacity-for-loss rating — that is benchmark finding F3.2, which
Run 4 itself catches in `G3.10`. The two are one check apart and directly contradictory.

**That contradiction was caught automatically**, which is the next section.

---

## 6. The cross-group document check is noisier on Haiku — and earned its place anyway

Item 4.1 produced nine pairs on Run 4, as it did on Run 3. The precision is visibly lower.

**The one that justifies the feature**, catching the fabrication above:

> **capacity for loss questionnaire**
> found: [CHK-003/G3.1] Capacity for loss questionnaire [P16] shows responses supporting the
> medium-high capacity conclusion …
> missing: [CHK-010/G10.4] Report asserts 'Medium-High' capacity for loss but provides **no
> evidence** of how the capacity for loss questionnaire was completed or what notes were recorded

Two groups of the same run, one asserting the questionnaire supports the rating and one saying
nothing evidences it. No single group could see this, and on this case the second is right.

**A false pair, and it is my bug:**

> **fact find**
> found: [CHK-001/G1.1] Fact Find [P4] records Anthony Barnes with Date of Birth 06/01/1970 …
> missing: [CHK-001/G1.10] **no corroborating figure found in fact find** or supporting documents

"no corroborating **figure** found in fact find" denies a *figure*, not the fact find. The
proximity rule should have caught that — it looks for a document part between the absence cue and
the document name — but `figure` is not in `DocumentParts`, so `ObjectAfter` returned null and the
denial was attributed to the document itself.

**The fix is to widen the part vocabulary** (`figure`, `value`, `date`, `reference`, `detail`,
`assessment`) and, more robustly, to treat *any* intervening noun phrase as a part rather than
requiring it to be on a list. The current list is a whitelist where a blacklist would be safer:
when in doubt, key the claim narrowly and report nothing, rather than key it broadly and report a
pair.

Three of Run 4's nine pairs are of this shape. On Run 3's tighter prose only one was.

---

## 7. Missed by all four runs

Fourteen findings have now survived two models, two configurations and four runs.

**Class A — the evidence never reached an assessor (7).** F3.1, F5.3, F5.4, F8.2, F10.2, F3.8,
F5.7. The strings `Strongly disagree`, `EXCLUDED FROM SCENARIO`, `-11.32`, `6.52`, `£51,694` and
`Doesn't trust anyone` appear in **no run's output at all**, on either model. Four of the fourteen
dead section hints name exactly this evidence. **This is what re-indexing is for**, and it is the
single largest block remaining.

**Class B — the report's own internal contradictions (3).** F6.8 (the Recommendation Summary
contradicts the recommendation tables), F7.9 (0.41% stated, 0.42% in the illustration), F3.7 (an
unevidenced claim that funds are *"managed to remain within this risk tolerance"*). All three are
report-versus-report, which is the canonical model's territory rather than retrieval's.

**Class C — the comparison nobody performs (2).** F7.1 / F9.2 — the post-advice-charge table
applies a 1% charge the client does not pay to the existing plan. Four runs. Run 3 quoted *"Subject
to an existing advice charge: No"* and used it to reassure. Run 4 gets nearest on the cost side —
*"more than offset by fund charges and the addition of a 1% per annum advice fee. The report does
not clearly explain why, given this net increase, the switch is justified on cost grounds"* — but
never asks why the two columns of the like-for-like table agree.

Item 3.3 is printed in that group's own *How to compare* block in both runs. **Four runs, two
models, one prompt clause, zero catches. This has to be derived in code.**

**Class D — one arithmetic step nobody takes (2).** F6.3 / F9.4, the ISA moved out of a 4.5% cash
account into a fund returning 3.4% after charges. Every run restates the report's own gross
comparison (4.72% vs 4.5%) and none applies the charges. Run 1 came closest and only by
challenging where 4.72% came from.

---

## 8. What to do next

The order has changed. Run 4 settles the model question and sharpens what is left.

| | Change | Why now |
| :-: | --- | --- |
| **1** | **Re-index, then re-run** | Seven of the fourteen permanent misses are evidence no assessor has ever seen, and four dead hints name it. Untested across two runs. Nothing else on this list is worth more |
| **2** | **Re-run with `IgnoreTriggerProbe` off** | CHK-009 is worth six findings and three routes could have enabled it. One line of configuration settles which |
| **3** | **Derive the like-for-like comparison in code** | Four runs, two models, one prompt clause, zero catches (§7 Class C). `DerivedFigures` already hands the assessor settled arithmetic; this belongs there |
| **4** | **Extend the absence clause** | Three findings sit at Partial across both models for one missing sentence. Haiku's wording (§4) shows the assessor is one step away |
| **5** | Widen `DocumentParts`, or invert it to a blacklist | Three of nine pairs on Haiku are false for one missing noun (§6) |
| **6** | Score a second run at each configuration | F1.3, F3.5, F6.4 and F9.5 all moved between two runs sharing a pack. The floor is three outcomes and several conclusions here sit near it |

### On which model to run

**This is now a real choice rather than an obvious one.**

| | Sonnet | Haiku |
| --- | --- | --- |
| Recall (all 75) | 42 caught | 41 caught |
| Recall (the 47) | 29 caught | 29 caught |
| Cost | £9.58 | **£2.97** |
| Untraceable quotations | **30** | 141 |
| Wrong requirement id | **0 of 88** | 61 of 88 |
| Apparent fabrications observed | none | at least one (§5) |

On recall they are level. **On auditability they are not close.** For a compliance workflow where a
reviewer reads every finding and must be able to follow it back to a document, 141 untraceable
quotations is the whole of the difference, and it is worth more than £6.61.

The useful conclusion is not "use Sonnet". It is that **the changes, not the model, are what moved
recall** — +14 findings on a fixed model against +1 for tripling the spend — and the remaining
fourteen misses are, on this evidence, immune to both.
