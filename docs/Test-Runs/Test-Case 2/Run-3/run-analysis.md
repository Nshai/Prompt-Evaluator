# Run analysis — Test Case 2, Run 3

One run of case Test-Case-2 (Barnes) on 2026-08-28 at 22:39, scored against
[expected-results-benchmark.md](../expected-results-benchmark.md) and compared with
[Run 1](../Run-1/run-analysis.md) and [Run 2](../Run-2/run-analysis.md).

**Run 3 is the first run carrying the changes from
[findings-improvement-plan.md](../../findings-improvement-plan.md).** It uses the same assessor as
Run 2 — `intelliflo-claude-sonnet-4-6` — so the model is held constant and the differences are
attributable to the changes, to the extraction, and to unpinned sampling.

**Scored across all 75 material findings, not only the 47 the benchmark nominates for recall.**

| | Run 1 | Run 2 | **Run 3** |
| --- | :-: | :-: | :-: |
| Model | Haiku 4.5 | Sonnet 4.6 | Sonnet 4.6 |
| Checks assessed | 9 of 10 | 9 of 10 | **10 of 10** |
| Groups assessed | 78 | 78 | **88** |
| **Scoring set (47 — Highest + High)** | | | |
| Caught / partial / missed | 23 / 8 / 16 | 28 / 2 / 17 | **29 / 6 / 12** |
| Caught | 49% | 60% | **62%** |
| **Moderate + Low (28)** | | | |
| Caught / partial / missed | 4 / 10 / 14 | 12 / 6 / 10 | **13 / 6 / 9** |
| Caught | 14% | 43% | **46%** |
| **All 75** | | | |
| Caught / partial / missed | 27 / 18 / 30 | 40 / 8 / 27 | **42 / 12 / 21** |
| **Caught** | 36% | 53% | **56%** |
| **Missed** | 30 | 27 | **21** |
| Citation trust (untraceable quotes) | 85 | 37 | **30** |
| Raised and not carried | 6 groups / 23 | 3 / 11 | **1 / 3** |
| Wrong requirement id | 49 of 78 | 0 of 78 | 0 of 88 |
| Cost (extract + checks) | £2.59 | £8.59 | **£9.58** |

**Headline: missed findings fall from 27 to 21, the first fall since scoring began, and CHK-009
runs for the first time. But the run cannot prove why CHK-009 ran, one change had no opportunity to
act at all, and the assessor's new caution about absence has introduced a failure mode of its own.**

---

## Contents

1. [What actually varied, and what this run cannot settle](#1-what-actually-varied-and-what-this-run-cannot-settle)
2. [CHK-009 ran — what it found](#2-chk-009-ran--what-it-found)
3. [Scoring: the 47](#3-scoring-the-47)
4. [Scoring: the 28 Moderate and Low](#4-scoring-the-28-moderate-and-low)
5. [The new machinery, observed](#5-the-new-machinery-observed)
6. [Two defects in the new code, visible in the output](#6-two-defects-in-the-new-code-visible-in-the-output)
7. [The absence clause has a side effect](#7-the-absence-clause-has-a-side-effect)
8. [Regressions](#8-regressions)
9. [F7.1 is still missed, and now more diagnostically](#9-f71-is-still-missed-and-now-more-diagnostically)
10. [What to do next](#10-what-to-do-next)

---

## 1. What actually varied, and what this run cannot settle

The configuration line records the changes reaching the run:

```
Embeddings … top 8/search, 24/group (reserving 1/section, 1/category, 1/top score;
             near-duplicate 0.90) … plans 10@e267d2d36592 · model f7d4163aaf8e
Settings 3c40d101cc81 · scope Core and Supplementary queries ·
             trigger probes bypassed — every check assessed
```

Three things must be said before any number below is read.

### The run is not a controlled test of the plan fix

`trigger probes bypassed` — `IgnoreTriggerProbe` was **on**. CHK-009 therefore ran because the
probe was skipped entirely, and would have run whatever the plan said.

Worse for attribution, the extraction also changed. Run 3's canonical model records **four**
arrangements carrying an advice action where Runs 1 and 2 recorded one:

| | Run 1 | Run 2 | Run 3 |
| --- | --- | --- | --- |
| `adviceAction` values | `RetainAndSwitchFunds` | `RetainAndSwitchFunds` | `RetainAndSwitchFunds`, **`FullTransfer`**, `Retain`, `NoAdviceGiven` |
| Model size | 325,372 | 369,353 | **390,597** |

`FullTransfer` was in CHK-009's applicability list *before* the fix. So there are **three
independent routes** by which CHK-009 could have run, and this run distinguishes none of them:

1. the trigger probe was bypassed;
2. the plan now accepts `RetainAndSwitchFunds`;
3. the extraction now emits `FullTransfer`, which the plan always accepted.

**The experiment that separates them is one line of configuration:** turn `IgnoreTriggerProbe` off
and re-run. If CHK-009 still runs, route 1 is excluded and the trigger detail will name which of
the other two carried it.

### One change had no opportunity to act

Embeddings cost **£0.0074 for 2,959 tokens** — query embeddings only. **The vector store was not
rebuilt**, so the converter's pseudo-heading promotion (item 2.5) never touched an index.

The proof is in the diagnostics: *"Section hints matching nothing: 14 — G1.1: Residency Status;
G1.11: cashflow modeller; G3.4: Capacity For Loss questionnaire responses …"* — **the same 14
hints, in the same order, as Run 2.** Four of them name the evidence behind F3.1, F3.4, F1.5 and
F8.1.

Item 2.5 is untested. Re-indexing is a prerequisite for the next run, not an optimisation.

### Sampling is still unpinned

The run prints its own caveat, which is item 0.1 working:

> Sampling is not pinned on this route, so two runs of this configuration will differ. Measured on
> one case: 3 of 78 outcomes and 12 of 69 severities moved between two runs with identical
> fingerprints.

**A six-finding move in missed-rate is outside that floor. A one- or two-finding move is not.** The
47-set change of +1 caught is inside the noise; the all-75 change of −6 missed is not.

### What did change in retrieval

| | Run 2 | Run 3 |
| --- | :-: | :-: |
| Searches | 207 | **236** |
| Passages retrieved | 3,252 | **3,710** |
| Reached an assessor | 1,671 of 2,176 (77%) | **1,868 of 2,451 (76%)** |
| Dropped by ranking, cap or collapse | 505 (23%) | **583 (24%)** |

The increase is CHK-009's own retrieval (29 searches, 458 passages). On the nine shared checks the
per-check search and passage counts are unchanged, which is expected: the top-score reservation and
the near-duplicate collapse both act *after* retrieval, on candidates.

---

## 2. CHK-009 ran — what it found

Ten groups, all ten raising a concern. This is the first time in three runs that any of F9.1–F9.7
has been reachable.

| | Benchmark | Run 1 | Run 2 | **Run 3** |
| --- | --- | :-: | :-: | :-: |
| **F9.1** Switch reduces maturity value; doing nothing ranked higher | Highest | Missed | Missed | **Partial** |
| **F9.2** Post-charge comparison invalid — 1% charge applied to the existing plan | Highest | Missed | Missed | **Missed** |
| **F9.3** "Lower Costs" leads a rationale where charges rise | Highest | Missed | Missed | **Caught** |
| **F9.4** ISA moved out of 4.5% cash into a fund returning 3.4% net | Highest | Missed | Missed | **Missed** |
| **F9.5** Switching analysis models the wrong existing product | High | Missed | Missed | **Caught** |
| **F9.6** Existing arrangement described inaccurately (UFPLS, fund range) | High | Missed | Missed | **Partial** |
| **F9.7** No reason given for preferring over the third-ranked solution | Moderate | Missed | Missed | **Caught** |

**Three caught outright, two partial, two missed.** Three of the six Highest-severity CHK-009 rows
remain unreached even with the check running.

The catches are good. F9.3:

> The report states 'Lower Costs – Moving to our agency terms will reduce your platform charge from
> 0.32% to 0.21%' as a rationale for the pension switch. While the platform charge element does
> fall … the total annual recurring charge increases from 0.54% to 0.62% (an increase of 0.08%) …
> The 'lower costs' rationale is therefore **misleading at the total charge level**, even if the
> platform component alone is lower.

F9.5 is better than the benchmark's own statement of it:

> the pension switch report [P1] models the existing pension as 100% invested in the Aviva Insured
> Funds Multi-asset Plus Fund II, yet the personal pension plan review [P14] and the suitability
> report consistently describe the existing holdings as **Vanguard LifeStrategy 60% and 80%**. This
> discrepancy means the like-for-like projections in [P2] and [P6] may not have used the correct
> existing fund, **undermining the reliability of the switching comparison**.

And CHK-009 found something the benchmark does not have: a £22.69 arithmetic inconsistency between
the switch charges table (implying £490) and the annual recurring charges table (£512.69), with the
consequence drawn — *"the '£63 increase' figure used to characterise the cost of switching is
understated … weakening the like-for-like comparison."* That is a **candidate finding**; it belongs
in [candidate-findings.md](../candidate-findings.md) pending verification against the source.

**F9.2 and F9.4 are the two that matter most and both are missed.** See §9.

---

## 3. Scoring: the 47

| ID | Run 1 | Run 2 | **Run 3** | |
| --- | --- | --- | --- | --- |
| F1.1 expenditure £2,493 / £2,206.67 / £3,710 | Caught | Caught | **Caught** | |
| F1.2 "stopped working" vs Fact Find *Employed* | Caught | Caught | **Caught** | Run 3 also spots *Factory worker* vs *Driver* |
| F1.3 "current earnings are sufficient" | Caught | Caught | **Missed** | ▼ **regression** |
| F1.4 Yvette £28,000 vs £16,800 | Caught | Caught | **Caught** | |
| F1.5 UFPLS not available | Missed | Missed | **Partial** | ▲ but see §7 |
| F1.7 Fact Find denies a final salary scheme | Partial | Missed | **Partial** | ▲ |
| F2.1 retirement horizons | Caught | Caught | **Caught** | |
| F2.2 Yvette's retirement three ways | Caught | Caught | **Caught** | |
| F2.3 two unsupported objectives | Caught | Caught | **Caught** | |
| F2.4 Cash ISA "not dead set on doing this" | Caught | Caught | **Caught** | |
| F2.5 £2,200 target vs expenditure | Caught | Caught | **Caught** | |
| F3.1 CFL questionnaire — spouse "Strongly disagree" | Missed | Missed | **Missed** | hint still dead |
| F3.2 no CFL rating; "No additional notes" | Caught | Caught | **Caught** | |
| F3.4 risk goal term vs no term stated | Partial | Caught | **Caught** | |
| F4.1 no understanding of unit trusts / GIA | Caught | Caught | **Caught** | |
| F4.2 no understanding of pension planning | Caught | Caught | **Caught** | |
| F4.3 no comprehension check | Caught | Caught | **Caught** | |
| F5.1 deficit if he has stopped working | Partial | Partial | **Partial** | |
| F5.2 "current situation" cashflow is post-advice | Partial | Caught | **Caught** | |
| F5.3 property EXCLUDED FROM SCENARIO | Missed | Missed | **Missed** | |
| F5.4 cash modelled at the invested rate | Missed | Missed | **Missed** | |
| F5.5 initial fee taken wholly from the GIA | Missed | Caught | **Caught** | £57,120 − £52,326 = £4,794 |
| F5.6 emergency fund | Caught | Caught | **Caught** | |
| F6.1 lower projected maturity not disclosed | Partial | Caught | **Caught** | |
| F6.2 "Lower Costs" costs more | Caught | Caught | **Caught** | |
| F6.3 ISA returns less than the cash it replaces | Partial | Missed | **Missed** | |
| F6.4 alternatives | Caught | Caught | **Caught** | |
| F6.5 Bed & ISA | Caught | Caught | **Caught** | |
| F6.6 "not applicable to this plan" | Partial | Missed | **Missed** | |
| F7.1 1% charge applied to the existing plan | Missed | Missed | **Missed** | see §9 |
| F7.2 aggregated charge disclosure | Caught | Caught | **Caught** | |
| F7.3 pre/post-charge tables | Missed | Caught | **Caught** | |
| F7.4 0.62% vs 0.54% | Partial | Caught | **Caught** | |
| F7.5 transaction costs excluded | Missed | Missed | **Missed** | Run 3 clears it explicitly |
| F7.6 10–15 working day settlement | Caught | Partial | **Partial** | |
| F8.1 suppressed ranking | Caught | Caught | **Caught** | |
| F8.2 −11.32 | Missed | Missed | **Missed** | |
| F8.3 risk warnings | Caught | Caught | **Caught** | |
| F8.4 capital at risk | Caught | Caught | **Caught** | |
| **F9.1** | Missed | Missed | **Partial** | ▲ |
| **F9.2** | Missed | Missed | **Missed** | |
| **F9.3** | Missed | Missed | **Caught** | ▲▲ |
| **F9.4** | Missed | Missed | **Missed** | |
| **F9.5** | Missed | Missed | **Caught** | ▲▲ |
| **F9.6** | Missed | Missed | **Partial** | ▲ |
| F10.1 vulnerability not assessed | Caught | Caught | **Caught** | |
| F10.2 "Doesn't trust anyone" | Missed | Missed | **Missed** | |

**29 caught, 6 partial, 12 missed.** The +1 caught over Run 2 is inside the noise floor; the
**−5 missed is not**, and all five come from CHK-009.

---

## 4. Scoring: the 28 Moderate and Low

| ID | Sev | Run 1 | Run 2 | **Run 3** |
| --- | :-: | --- | --- | --- |
| F1.6 6,900 vs 3,000 funds | Mod | Partial | Partial | **Partial** |
| F1.8 plan value and impossible dates | Mod | Partial | Caught | **Caught** |
| F1.9 State Pension unevidenced, no BR19 | Mod | Caught | Caught | **Caught** |
| F1.10 family details only in the report | Mod | Missed | Caught | **Missed** ▼ |
| F1.11 fact find contact log disagrees on age | Low | Missed | Missed | **Missed** |
| F1.12 will and registered PoA unevidenced | Mod | Caught | Caught | **Partial** ▼ |
| F1.13 assets said to be jointly held | Low | Missed | Caught | **Caught** |
| F2.6 £40,000 renovation arithmetic | Mod | Partial | Caught | **Caught** |
| F3.5 Vanguard funds rated 5 and 7 by nothing | Mod | Partial | Partial | **Caught** ▲ |
| F3.6 Risk Rating 4 on two different scales | Mod | Partial | Caught | **Caught** |
| F3.7 "managed to remain within this tolerance" | Mod | Missed | Missed | **Missed** |
| F3.8 volatility above the benchmark | Low | Missed | Missed | **Missed** |
| F4.4 experience asserted as fact | Mod | Partial | Caught | **Caught** |
| F4.5 generic technical sections | Mod | Caught | Caught | **Caught** |
| F5.7 pension never drawn in any scenario | Mod | Missed | Missed | **Missed** |
| F6.7 4.72% is a simple average | Mod | Partial | Missed | **Missed** |
| F6.8 Recommendation Summary contradicts tables | Mod | Missed | Missed | **Missed** |
| F6.9 GIA research run on £60,000 | Low | Missed | Partial | **Missed** ▼ |
| F7.7 £155,812 does not reconcile | Mod | Partial | Missed | **Partial** ▲ |
| F7.8 growth assumptions differ | Mod | Missed | Missed | **Partial** ▲ |
| F7.9 0.41% vs 0.42% | Low | Missed | Missed | **Missed** |
| F8.5 no loss scenario illustrated | Mod | Partial | Partial | **Caught** ▲ |
| F8.6 derivatives and EM risks undisclosed | Mod | Missed | Caught | **Caught** |
| **F9.7** third-ranked solution | Mod | Missed | Missed | **Caught** ▲▲ |
| F10.3 "registered" Power of Attorney | Mod | Missed | Caught | **Caught** |
| F10.4 all contact by telephone, ATRQ alone | Mod | Caught | Partial | **Partial** |
| F10.5 Yvette relied on but never met | Mod | Missed | Partial | **Partial** |
| F10.6 accessible-format measures | Low | Partial | Caught | **Caught** |

**13 caught, 6 partial, 9 missed.** Four gains (F3.5, F7.8, F8.5, F9.7), three losses (F1.10,
F1.12, F6.9) — a net movement well inside the noise floor except for F9.7, which is CHK-009 again.

**Nine rows have now been missed by all three runs**, on two different models: F1.11, F3.7, F3.8,
F5.7, F6.8, F7.9, and — from the scoring set — F3.1, F5.3, F5.4, F8.2, F10.2. Neither `6.52`, nor
`0.42`, nor `£51,694`, nor `−11.32`, nor `EXCLUDED FROM SCENARIO` appears anywhere in any run's
output. **These are not reasoning failures. They are figures no assessor was shown**, and they are
the residue that item 2.5 was written for and could not touch here.

---

## 5. The new machinery, observed

### The basis clause fires, and fires in both directions

Item 3.2 is printed in each group's *How to compare* block. It is being used, and — the part that
matters — used to **refuse** the guard as well as to apply it:

> The guard about different bases **does not explain this gap** — both figures purport to be
> Yvette's annual employment income.

> This is explained by the different bases and is not a finding.

A guard that only ever excuses is a licence. Both uses in one run is the behaviour the clause was
written for.

It also produced analysis no previous run reached — the charge-basis reconciliation:

> £512.69 at 0.62% implies a base of approximately £82,692 — which is £3,618 higher, consistent
> with the £3,600 new contribution being added. The report does not explain this basis difference.
> However, the 'Like for Like' comparison using £63 outperformance requirement is therefore
> applying the new plan's higher base …

### The cross-group document check fires on real data

Item 4.1 produced a **DOCUMENTS ONE CHECK FOUND AND ANOTHER CALLED MISSING** section with nine
pairs. The best of them is exactly the shape it was built for:

> **fund factsheet**
> found: [CHK-003/G3.8] The Aviva fund factsheet [P18] uses a 1–7 volatility scale distinct from the
> 1–10 client profile scale …
> missing: [CHK-003/G3.11] The file contains no Defaqto or provider fund factsheet for Vanguard
> LifeStrategy 60% Equity or 80% Equity …

and

> **key features document**
> found: [CHK-004/G4.3] (b) Key Features documents for the Aviva Pension, ISA and GIA
> missing: [CHK-007/G7.5] No provider illustration or key features document for the new pension plan
> is in the pack

Both are genuine and neither was reachable by any single group. Two of the nine are noise (see §6).

### Table citations reach the report

Citations now carry table rows as cells rather than as rewritten prose, throughout:

```
· Fact Find (1).md [B] (P24)
  table: Policy No  |  AV2075926-001  |  £  |  88,590.88
· Notes - Barnes.md [C] (P19)
  "Retired, gets DB income, but works part-time. When will he stop? Defo 60?"
```

Citation trust improved 37 → 30 untraceable quotes, and *raised and not carried* fell from 3 groups
/ 11 differences to **1 group / 3**.

### What did not appear

There is no **CHECKS NOT RUN** section, correctly — with trigger probes bypassed, nothing was N/A.
Item 1.3 is therefore also untested by this run.

---

## 6. Two defects in the new code, visible in the output

Both are in `CrossGroupContradictions.Documents`, which I wrote for item 4.1.

### `Singular()` mangles irregular plurals

```
fact find (entrie)
```

`Singular()` strips a trailing `s`, so `entries` becomes `entrie`. Harmless to the matching — both
sides normalise the same way — but it prints, and a reviewer reading `fact find (entrie)` will
trust the next line less. The fix is to strip `ies → y` before the bare `s`.

### One sentence appears on both sides of two pairs

```
capacity for loss questionnaire
  found:   [CHK-003/G3.2] [P1] shows a Capacity for Loss questionnaire section exists …
  missing: [CHK-003/G3.9] No standalone capacity for loss questionnaire … is present …

capacity for loss questionnaire (note)
  found:   [CHK-010/G10.4] The capacity for loss questionnaire [P1] records no additional notes …
  missing: [CHK-003/G3.2] [P1] shows a Capacity for Loss questionnaire section exists …
```

The G3.2 sentence is the **found** side of the first pair and the **missing** side of the second.
Keying on document-plus-part was meant to prevent exactly this, and it does within one key; it does
not prevent one sentence taking opposite roles under two different keys. A sentence that has been
recorded as present for a document should not be admitted as a denial of that document's parts.

Neither defect is dangerous — this pass reports, it does not decide — but both cost credibility in
a section a reviewer is being asked to act on.

---

## 7. The absence clause has a side effect

Item 3.1 tells the assessor not to call a document absent when a passage of that kind is in the
pack. It is working, and it has made a specific class of finding **worse**.

Three benchmark findings turn on a supporting document *actively contradicting* the report:

| | Report asserts | Evidence states |
| --- | --- | --- |
| F1.5 | *"Full flexi-access drawdown and UFPLS available"* | the Aviva plan information form: **UFPLS not available** |
| F1.6 | *"Over **6,900** funds available"* | the same form: **over 3000 funds** |
| F9.6 | both of the above, as the basis of the switch comparison | as above |

Run 3 states all three like this:

> No provider document in the pack confirms this figure for either side.
> No provider document confirms UFPLS availability on the existing plan.

That is a careful, well-formed **absence** claim — and the truth is a **contradiction**. The
difference is the whole finding: "nothing corroborates this" is a documentation gap; "the provider's
own form says the opposite" is a misstatement in a suitability report. Run 3 scores Partial on all
three where the stronger claim was available.

**This is the clause doing half its job.** It stopped the assessor asserting an absence it could not
support — Run 2's failure mode — and did not push it on to check whether the document present
*disagrees*. The clause says what not to claim; it does not say what to do next.

The remedy is one sentence, and it belongs in the same rule: *where a document of that kind is in
the pack, say what it states about the point — if it states the opposite, that is the finding, and
it outranks any statement about what is missing.*

---

## 8. Regressions

Three, all on the same model, so all attributable to the changes or to sampling.

| | Was | Now | Reading |
| --- | --- | --- | --- |
| **F1.3** current earnings sufficient for the contribution | Caught in Runs 1 **and** 2 | **Missed** | The only finding caught by both previous runs and lost. Run 2 stated it plainly — *"the report simultaneously states the client has stopped working and that earnings are sufficient"*. Run 3's CHK-001 covers tax status instead and never reaches it |
| **F1.10** family details only in the report | Caught in Run 2 | **Missed** | Zero mentions of *grandchildren* or *financially independent* anywhere in Run 3 |
| **F1.12** will and PoA unevidenced | Caught in Runs 1 and 2 | **Partial** | Run 3 records the assertion and the extraction note without concluding the file does not support it |

All three sit in CHK-001, whose group count is unchanged at 11. CHK-001's attention was spent
elsewhere — its discrepancy lists are visibly longer and include new material (Factory worker vs
Driver, the £3,600 basis difference). **This looks like displacement inside a fixed budget rather
than a capability loss**, which is a hypothesis the repeat-run protocol can test and this single run
cannot.

---

## 9. F7.1 is still missed, and now more diagnostically

F7.1 / F9.2 is the case's defining defect: the post-advice-charge comparison applies a 1% ongoing
adviser charge to the existing plan, which the client does not pay, making a worse outcome look
level.

Run 3 **retrieved and quoted the decisive evidence** — and used it to support the switch:

> [P6] (category G, pension switch report) confirms: 'Transfer penalty: £0.00' and **'Subject to an
> existing advice charge: No'**. [P1] … shows the effect of switching as '£0 or 0.0% decrease'
> across all growth scenarios … The provider documentation in [P12] therefore evidences that no
> guaranteed annuity rates, no guaranteed minimum pension, no terminal bonuses and no MVA/early
> transfer charges apply to the ceding plan.

*"Subject to an existing advice charge: No"* is the sentence that proves the comparison is rigged.
Run 3 reads it as one more reassurance that the ceding plan is clean.

Item 3.3 — *"where a comparison shows no difference, ask what assumption produced that"* — is
printed in this very group's *How to compare* block, and the group had `£0 or 0.0% decrease` in
front of it. **The clause was present, the evidence was present, and the question was not asked.**

That is the sharpest result in this run. It is not retrieval, it is not the plan, and it is not
requirement confusion (0 of 88). Three runs, two models, one conclusion: **this defect needs the
comparison performed in code, not asked for in a prompt.** The numbers are all in the canonical
model — an existing charge, a new charge, and a projection pair that agree when the charges do not.
`DerivedFigures` already computes exactly this kind of thing and hands it to the assessor as settled.

---

## 10. What to do next

**Ordered by what the evidence supports, not by cost.**

| | Change | Why now |
| :-: | --- | --- |
| **1** | **Re-index before the next run** | Item 2.5 is untested and four dead section hints name the evidence behind four missed findings. Nothing else on this list can be measured cleanly until the index reflects the converter |
| **2** | **Re-run with `IgnoreTriggerProbe` off** | Three routes could have run CHK-009 and this run separates none of them. One line of configuration settles it |
| **3** | **Extend the absence clause** (§7) | Three findings sit at Partial for one missing sentence. Cheapest recall on the list |
| **4** | **Derive the like-for-like comparison in code** (§9) | Two models, three runs, one prompt clause, and the decisive sentence quoted in support of the thing it disproves. `DerivedFigures` is the right home |
| **5** | Fix `Singular()` and the both-sides admission (§6) | Small, and they cost credibility in a section reviewers are asked to act on |
| **6** | Score a second Run-3-configuration run | F1.3, F1.10 and F1.12 moved; the floor is 3 outcomes. Displacement or capability loss is currently undecidable |

**Not on the list: a stronger model.** Run 2 and Run 3 share one. Of the 21 findings Run 3 misses,
five are figures no assessor was shown, two are a comparison the prompt cannot make it perform, and
three are the absence-clause side effect. None of them is a reasoning ceiling.

### Expected position after items 1–4

Items 3 and 4 alone convert F1.5, F1.6, F9.6, F7.1 and F9.2 — three partials and two misses. Item 1
puts F3.1, F3.4 and F8.1's evidence in front of an assessor for the first time. **On Run 3's base
that is 42 → roughly 48 of 75 caught, and 21 → roughly 15 missed**, without a model change and
without another £9.58 of speculative spend.

---

## Method

Scored from `run-checks-output.txt` for each run — **not** the checks log, which embeds the full
prompts and so matches the evidence pack as readily as the findings, inflating every count.

Caught, partial and missed are the benchmark's own definitions: *caught* when a run states the
substance of the contradiction, naming both sides and what conflicts; *partial* when it touches the
area but misidentifies the conflict, states it without the contradiction, or buries it; *missed*
otherwise.

Findings the previous runs caught and whose distinctive tokens remain present in Run 3 with
unchanged framing were carried forward rather than re-read in full; every changed score, every
CHK-009 row and every row missed by two or more runs was read in context.

The benchmark's own caveat applies: the 47 are **not** 47 independent trials — F6.1/F8.1/F9.1,
F6.2/F9.3, F6.3/F9.4 and F7.1/F9.2 are the same defects seen from different checks. CHK-009's
arrival therefore inflates recall slightly by giving four already-scored defects a second place to
be counted. **The missed-count is the more honest measure**, which is why the benchmark says to gate
on it.
