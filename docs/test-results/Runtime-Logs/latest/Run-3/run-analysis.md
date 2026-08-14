# Run 3 — after Stage 5

Scored against [expected-results-benchmark.md](../../../expected-results-benchmark.md) using
[scoring-rubric.md](../../../scoring-rubric.md), compared against
[the Run 2 analysis](../Run-2/run-analysis.md). The changes under test are Stage 5 of
[remediation-plan.md](../../../remediation-plan.md), commit `5e72f14`.

> **Three files are called `run-analysis.md`** — one in each run folder.

---

## 0. The run is genuine

| | |
| --- | --- |
| First → last group | 15:23:39 → 15:26:13 |
| Wall clock, 60 groups | **154 s** |
| Reported rate | **517 tok/s** for 88,570 output tokens |
| Responses vs Run 2 | different (`208d0ba3…` vs `3ff75458…`) |

No replay banner, and the rate sits squarely in the generated band. **This is the first run in the
project's history where nobody had to check by hand.**

### What moved since Run 2

| | Run 2 | Run 3 |
| --- | --- | --- |
| Search limit | top 8 | top 8 — **held** |
| Query plans | `8bf1dcc6186b` | `8bf1dcc6186b` — **held** |
| Extraction cap | 32,000 tok | 32,000 tok — **held** |
| Canonical model | `c9fcad97d0f0` | **`b92d449cd0f7`** (re-extracted) |
| Code | Stages 0–4 | **Stage 5** |

**Two variables, not four.** This is much closer to the controlled comparison §5.5 asked for: the
retrieval settings and the plans are identical, so the difference is Stage 5 plus whatever the
re-extraction changed. Not perfect — a fresh extraction is not a held variable — but attributable
in a way Run 2 was not.

---

## 1. Headline

| | Run 1 | Run 2 | **Run 3** | |
| --- | --- | --- | --- | --- |
| **Benchmark recall (caught)** | 16/36 (44%) | 24/36 (67%) | **24/36 (67%)** | — |
| Partial | 12 | 6 | 7 | |
| **Missed** | 8 | 6 | **5** | ▼ |
| Check outcomes matching expected | 9/10 | 10/10 | **10/10** | — |
| **Checks reaching the Fact Find** | 5/10 | 7/10 | **10/10** | ▲ |
| **Unverified quote rate** | 36% | 32% | **14%** | ▼▼ |
| **Groups carrying a flagged quote** | 70% | 70% | **45%** | ▼▼ |
| Citations produced | 274 | 302 | 350 | ▲ |
| …of which table reads | — | — | **104 (30%)** | new |
| Groups vetoed by `comparisonPerformed` | 20 | 17 | 16 | ▼ |
| Requirement text rewritten | 25 | 0 | 2 | |
| Wrong requirement id echoed | 22 | 23 | 19 | ▼ |

**Every mechanical measure Stage 5 targeted improved, several sharply. Recall did not move.**

That combination is the finding, and §3 is about why.

---

## 2. Recall against the 36 material findings

```
CAUGHT (24)   F1.8  F2.1  F2.2  F3.1  F3.3  F3.5  F4.1  F4.2  F4.3  F4.4
              F5.3  F6.1  F6.3  F7.1  F7.3  F7.5  F8.2  F9.1  F9.2  F9.3
              F9.4  F9.5  F10.1 F10.2

PARTIAL (7)   F1.1  F1.2  F1.3  F1.9  F3.4  F6.2  F8.1

MISSED (5)    F1.6  F5.1  F5.2  F5.4  F7.2
```

One movement: **F1.3 missed → partial.** The Fact Find's household income total is now retrieved
and cited — `Fact Find (Test 1).md [B] (P12) "Total Net Monthly Income £1,430.00"` — for the first
time in three runs. No group draws the contradiction from it, so it stops short of caught.

Nothing regressed. **Caught is unchanged at 24 and the noise floor is ±2**, so the honest reading
is that recall did not move.

---

## 3. What Stage 5 did

### 3.1 The per-category floor worked exactly as designed — and that was not enough

Groups whose pack contains a Fact Find passage:

| Check | Run 1 | Run 2 | **Run 3** |
| --- | --- | --- | --- |
| CHK-001 | 5/9 | 5/9 | **6/9** |
| CHK-002 | 1/6 | 1/6 | **4/6** |
| CHK-003 | 1/7 | 0/7 | **1/7** |
| CHK-004 | 2/5 | 3/5 | 3/5 |
| CHK-005 | 1/6 | 1/6 | **4/6** |
| CHK-006 | 1/4 | 2/4 | 2/4 |
| CHK-007 | 0/7 | 1/7 | 1/7 |
| **CHK-008** | **0/5** | **0/5** | **1/5** |
| **CHK-009** | **0/7** | **0/7** | **1/7** |
| CHK-010 | 4/4 | 4/4 | 4/4 |

**Every check now reaches the Fact Find**, including the two that reached it in zero groups across
both previous runs. The exit criterion is met and the mechanism is confirmed: 56 of 60 packs still
sit at the twelve-passage cap, so the floor is displacing passages exactly as intended rather than
the packs having simply grown.

**And the findings behind it did not follow.** F5.1 — the Fact Find's
`Total Monthly Disposable Income | £-288.00`, the starkest missed finding in the benchmark — is
still missed. Reading what the floor actually admitted explains why:

| Group | The Fact Find passage it received |
| --- | --- |
| CHK-005/G5.2 | `| | In force | In force | Status | | GMP Amount (p.a.) | |` |
| CHK-005/G5.3 | `| Lump Sum Cont. | | | | | Value | £116,555.45` |
| CHK-005/G5.4 | `| Year End | | Most Recent Annual Accounts | | Year 2 Annual Accounts…` |
| CHK-008/G8.1 | `| Statement 5 Answer | Yes` |
| CHK-009/G9.1 | `| Lump Sum Cont. | | | | | Value | £116,555.45` |

**Every one is form furniture.** Empty pension-feature grids, a blank accounts table, a
questionnaire answer cell. Not one is the income and expenditure section.

The defect has moved one level down and is now precisely stated: **the floor guarantees a passage
from the category, and the best-scoring passage in that category is not the one that matters.** The
Fact Find is a 42 KB form of mostly-empty tables; those tables embed near almost any query and
carry no content, so they win the within-category ranking against the one section that would settle
five findings. Note also that the same chunk — `Lump Sum Cont. … £116,555.45` — was admitted to
four different groups across three checks, which is the signature of one chunk dominating a
category rather than of a category being explored.

This is a **chunking and embedding problem, not a ranking one**, and no further work on `Rank` will
touch it.

> **A figure in that passage, adjudicated afterwards — and I had it wrong.** I read `£116,555.45`
> as a third pension total contradicting the £116,998.47 the arrangements sum to. Reading the
> passage in full, it sits beside `Valuation Date | 23/07/2026` in the Fact Find's Existing
> Pension Provision block: it is the **Aviva Platform plan's value at a future date**, after the
> consolidation, not the client's holdings today. A different quantity, so no contradiction — and
> the anomaly that *is* real, a plan valued nine months after the report date, is already
> benchmark finding **F1.12**. Nothing to add to the scoring set.

### 3.2 Table citations were adopted immediately and cut the failure rate by more than half

**104 of 350 citations (30%) came back as table reads**, unprompted beyond the schema field and one
paragraph of instruction. The effect:

| | Run 2 | Run 3 |
| --- | --- | --- |
| Unverified quotes | 96 of 302 (32%) | **49 of 350 (14%)** |
| Groups carrying a flagged quote | 42/60 (70%) | **27/60 (45%)** |
| Groups labelled `citations unverified` | 4 | **1** |

G1.4 shows the new form doing exactly what it was built for — every citation a row rather than a
reflowed sentence:

```
· intelliflo planning Baseline scenario.md [F] (P10)
    table: Employment | 16,640 | 13,200 | 29,840
    table: Total Regular Income | 28,613 | 13,200 | 41,813
    table: Household | 15,660
```

Each of those would previously have been prose, and each would have been rejected as a fabricated
quotation. **This is the single largest mechanical improvement of the three runs**, and it did not
require loosening the matcher: the altered-quotation defence is intact and cells are still checked
all-or-nothing against the passage named.

The residual 14% is close to the <12% target and no longer the dominant failure mode.

### 3.3 The cross-group pass is coherent now, and still not useful

The decimal-splitting bug is gone — no fragment begins mid-number — and the recurrence keys work,
so pairs are at least the same kind of quantity:

```
  Client income (monthly)
    [CHK-001/G1.4] …Report derived total monthly income as £4,486.67 but evidence shows…
    [CHK-003/G3.2] …State Pension £230/week, HGV earnings £300/week net…
```

But of five pairs reported, **none is a contradiction**. They are restatements of the same facts
from different checks. The one pair that would matter — G1.2's `£1,430.00` Fact Find total against
G1.4's `£4,486.67` report total, which is F1.3 — **is not among them**, because the "widest spread
between two groups" heuristic picked a different pair under the same key.

Selecting extremes was the wrong rule. What is wanted is the pair whose two sides disagree *about
the same claim*, not the two furthest apart.

---

## 4. Where the remaining five findings actually are

| | Finding | Why it is still missed |
| --- | --- | --- |
| **F5.1** | Fact Find disposable income −£288 | Category reached, wrong passage (§3.1) |
| **F5.2** | Affordability rests on unrecorded income | Entailed by F1.2/F1.3, both partial |
| **F5.4** | Cashflow "assets of your main residence" for a renter | Residency section never retrieved; zero mentions in 60 groups |
| **F1.6** | Tenant vs RNRB / main residence | Same passage as F5.4 |
| **F7.2** | £186.19 is 0.18% of Zurich's fund | See below |

**F7.2 should have been caught by `DerivedFigures` and was not.** Stage 5.3 extended it to read the
arrangements' own charge lines precisely for this, and `£186.19` appears nowhere in the run — which
means the canonical model does not carry that charge line under either path the component reads.
The fix was correct and the data is not there; this is now an **extraction** question, not an
assessment one.

**Three of the five are one document section.** F5.1, F5.4 and F1.6 all live in the Fact Find's
personal-circumstances and income pages, and F5.2 is entailed by two more from the same place. A
single retrieval improvement that reaches that section would move four findings.

---

## 5. Exit criteria

| Criterion | Target | Run 2 | **Run 3** | |
| --- | --- | --- | --- | --- |
| Runs mis-reported as generated | 0 | 0 | **0** | ✅ |
| L1 plan violations | 0, enforced | 0 | **0** | ✅ |
| Extraction report reaching assessors | 100% | 100% | **100%** | ✅ |
| Findings storing the model's requirement text | 0 | 0 | 2 of 60 | ⚠️ |
| **Groups reaching category B where declared** | ≥1 per check | 7/10 checks | **10/10 checks** | ✅ |
| High-severity concerns demoted by `comparisonPerformed` | 0 | 17 vetoed | 16 vetoed | ⚠️ |
| **Unverified quote rate** | <12% | 32% | **14%** | ⚠️ close |
| **Groups flagged** | <25% | 70% | **45%** | ❌ |
| **Benchmark recall** | ≥26/36 (72%) | 24/36 | **24/36 (67%)** | ⚠️ |
| **Missed** | ≤3 | 6 | **5** | ❌ |

**Five met, three close, two missed** — against four met in Run 2. The two hard misses are recall
and missed-count, and both now trace to the same cause.

---

## 6. What to do next

The order has changed. Retrieval *coverage* is solved; retrieval *precision within a document* is
the whole remaining problem.

| | Action | Why now |
| --- | --- | --- |
| 1 | **Chunk the Fact Find on its section headings, and drop chunks that are all-empty table cells.** | Four of five remaining misses are one document section. The floor admits the best-scoring chunk of a category and that chunk is an empty form grid — one chunk was admitted to four different groups. This is the whole gap. |
| 2 | Weight retrieval against content density, or index form labels separately from answers | Same defect, more general: a blank `| GMP Amount (p.a.) | |` row should not be able to outrank an income table for any query. |
| 3 | Fix the cross-group pair selection — most-disagreeing on the same claim, not widest spread | It is coherent now and still surfaces nothing. F1.3's own pair exists under the right key and was passed over. |
| 4 | Check why `costsAndCharges` carries no existing-arrangement charge lines | 5.3 was built for F7.2 and the data is absent. Extraction question. |
| 5 | Re-run with the plans, settings **and extraction** held | Run 3 held two of three. Holding the canonical model too would make the next recall number attributable. |
| 6 | Then revisit the relevance floor (4.5) | Still unbuilt, still after this; and now clearly secondary, since the problem is which passage wins, not which passages enter. |

---

## 7. Method and limits

- Scored per [scoring-rubric.md](../../../scoring-rubric.md), against the rendered output, in
  benchmark order, recording the deciding sentence for each verdict.
- **The rubric's first outing.** It settled F1.3 (both halves present, unjoined → partial under the
  first Partial bullet) and F1.9 (right area, different contradiction → partial under R4) without
  argument. It did not remove the judgement from F6.2 or F8.1.
- **Two variables moved**, not one: Stage 5 and a re-extraction. The category-coverage and
  citation numbers are attributable to Stage 5 by mechanism — the floor and the `cells` field are
  the only things that could produce them. Recall being flat is not attributable either way.
- ±2 findings remains the noise floor, so 24 → 24 means *no measured change*, not *no change*.
