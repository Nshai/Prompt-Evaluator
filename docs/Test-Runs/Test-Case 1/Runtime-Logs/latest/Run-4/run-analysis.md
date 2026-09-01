# Run 4 — after Stage 6

Scored against [expected-results-benchmark.md](../../../expected-results-benchmark.md) using
[scoring-rubric.md](../../../scoring-rubric.md), compared against
[the Run 3 analysis](../Run-3/run-analysis.md). The changes under test are Stage 6 of
[remediation-plan.md](../../../remediation-plan.md), commit `a13f1f1`.

> **Four files are called `run-analysis.md`** — one in each run folder.

---

## 0. Which log is the run

**`checks_ABC-99_20260814_190626.log` is the run** (19:06:40 → 19:09:09, **149 s**).
`checks_ABC-99_20260814_191304.log` is a **2-second replay** of it, byte-identical across all 60
responses, and `Run-Checks-output.txt` is the replay's rendering — correctly stamped:

> `*** REPLAY — NOT A GENERATED RUN. 83,352 output tokens in 3.4s is 24,554 tok/s… ***`

Tenth replay on record, second to announce itself. Findings below are the genuine run's.

### What moved since Run 3

| | Run 3 | Run 4 |
| --- | --- | --- |
| Search limit / plans / extraction cap | top 8 / `8bf1dcc6186b` / 32k | **all held** |
| Canonical model | `b92d449cd0f7` | **`5027a895fcff`** (re-extracted) |
| Code | Stage 5 | **Stage 6** |

Same two-variable shape as Run 3 — and the re-extraction is not incidental this time. **Stage 6's
schema change only takes effect on a re-extraction**, so it had to move.

---

## 1. Headline

| | Run 2 | Run 3 | **Run 4** | |
| --- | --- | --- | --- | --- |
| **Extraction sections succeeding** | — | 4 of 12 | **12 of 12** | ▲▲ |
| Canonical paths resolved / absent | — | 115 / 30 | **121 / 24** | ▲ |
| **Unverified quote rate** | 32% | 14% | **11%** | ✅ target met |
| Groups carrying a flagged quote | 70% | 45% | **42%** | ▼ |
| Citations / of which table reads | 302 / — | 350 / 104 | 334 / **109 (33%)** | |
| Check outcomes matching expected | 10/10 | 10/10 | **10/10** | — |
| Checks reaching the Fact Find | 7/10 | 10/10 | **10/10** | — |
| **Benchmark recall (caught)** | 24/36 | 24/36 | **24/36 (67%)** | — |
| Partial | 6 | 7 | **4** | |
| **Missed** | 6 | 5 | **8** | ▼ worse |

**The extraction defect is fixed outright. Recall is unchanged for the third run running, and the
missed count went backwards.**

---

## 2. Stage 6's headline result: the schema was throwing the evidence away

This is the cleanest fix in four runs and it is worth stating plainly.

| | Run 3 | Run 4 |
| --- | --- | --- |
| Sections reporting FAILED | **8 of 12** | **0 of 12** |
| Occurrences of `charges` in the pack | **0** | **521** |
| `£186.19` present anywhere | **no** | **yes** |
| Stored model | ~282k tokens' worth | **318,294 chars, all 22 sections populated** |

Run 3's extraction log had said so on its own face, for three runs:

```
[ 5/12] Existing arrangements  FAILED — Dropped 2 value(s) the schema's enums do not allow:
                               basis = "of fund pa"
[ 9/12] Costs and charges      FAILED — Dropped 8 value(s) … basis = "of fund value pa"; …
```

With the enums gone, the same passes now read:

```
[ 5/12] Existing arrangements   18,847 chars (40.6s)
[ 9/12] Costs and charges       23,812 chars (49.5s)
```

And the value the benchmark's F7.2 turns on now exists in the model, carrying precisely the basis
that used to get it deleted:

```json
{ "arrangementId": "EA5",
  "existingAnnualChargePercentage": { "value": 0.52, "basis": "of fund value pa" },
  "existingAnnualChargeAmount":     { "amount": 186.19, "asAt": "2025-10-14" } }
```

One residual, and it is the known one: the `REC5` dangling reference — a recommendation id in an
objective slot — which gap-analysis recorded as E2's leftover.

**This unblocked nothing yet.** §4.1 explains why.

---

## 3. Recall against the 36 material findings

```
CAUGHT (24)   F1.8  F2.1  F2.2  F3.1  F3.3  F3.5  F4.1  F4.2  F4.3  F4.4
              F5.3  F6.1  F6.3  F7.1  F7.3  F7.5  F8.2  F9.1  F9.2  F9.3
              F9.4  F9.5  F10.1 F10.2

PARTIAL (4)   F1.9  F3.4  F6.2  F8.1

MISSED (8)    F1.1  F1.2  F1.3  F1.6  F5.1  F5.2  F5.4  F7.2
```

**Caught is identical to Runs 2 and 3 — the same 24 findings, three runs running.** What moved is
the Fact Find group: **F1.1, F1.2 and F1.3 went partial → missed.**

In Run 3 those scraped a partial because the Fact Find's income table reached the *output* — G1.2
cited `Total Net Monthly Income £1,430.00`. In Run 4 the string `Net Monthly Income` **appears
nowhere in the output at all**, so under [rubric R7](../../../scoring-rubric.md) — score what is
printed — they are missed.

That is a three-finding move on a ±2 noise floor, so it is real. It is also not a regression in
capability, which §4.2 is about.

---

## 4. Why the fixes did not convert

### 4.1 The charge data now exists, in a third shape nothing reads

`DerivedFigures.ChargeLines` reads two paths: `existingArrangements[].charges.lines[]` and
`costsAndCharges.{existing,recommended}[].lines[]`. Stage 5.3 added the first of those *specifically*
so F7.2 would be caught by division.

The extraction stores this comparison as neither. It writes flat sibling properties on a
replacement-analysis entry — `existingAnnualChargePercentage` and `existingAnnualChargeAmount` —
so the component built for exactly this figure still cannot see it. `Charge arithmetic` produced
the same two lines it did in Run 3, both from the recommended plan:

```
- Aviva Platform platform charge: £231.39 at 0.21% implies £110,185.71 …
- Investment Management charge:   £253.43 at 0.22% implies £115,195.45 …
```

**Two chases, two misses, and the third is a one-line addition** — read the paired
`existing*Percentage` / `existing*Amount` properties wherever they appear, rather than only
`lines[]`.

> **And a discrepancy worth adjudicating.** The model pairs `£186.19` with **0.52%**. The benchmark
> reads the same table as **0.18%**, which is what makes the finding — £186.19 at 0.18% implies
> £103,439, Zurich's fund, not Standard Life's £3,002. At 0.52% it implies £35,805, which matches
> nothing. Either the extraction is reading the wrong column or the benchmark is. **F7.2 cannot be
> scored honestly until that is settled**, and it needs the report, not the logs.

### 4.2 The −£288 reached the assessor for the first time, and nobody used it

This is the most important measurement in the run.

The Fact Find's income section — the one carrying
`Total Net Monthly Income £1,430.00`, `Total Monthly Expenditure £1,718.00` and
`Total Monthly Disposable Income £-288.00` — **is in the evidence pack of four groups**: CHK-001's
G1.1, G1.2, G1.3 and G1.9. First time in four runs.

**Not one of them mentions it.** `288` and `Disposable` appear zero times in the output.

Look at which groups got it: personal details, tax status, employment, emergency fund. None is
asking about affordability. The group that is — **G1.4, income and expenditure — did not get it**,
and compared the report against the planning documents instead, exactly as in Run 3. Neither did
any CHK-005 group, where F5.1 belongs.

**Retrieval is no longer the bottleneck for F5.1. Routing is.** The right passage is being fetched
and delivered to groups with no question to ask of it, and the plans' `expectedCategories` cannot
express "this group needs *this section*", only "this group needs the Fact Find".

### 4.3 My Stage 6 diagnosis was half wrong

Stage 6 was argued on the claim that unfilled form tables outrank content within a category.
Testing `ContentDensity` against every Fact Find passage Run 4 actually admitted:

| Passage | Filled cells | Carrying a value | Verdict |
| --- | --- | --- | --- |
| `\| \| In force \| In force \| Status \| \| GMP Amount…` | 17 | 1 (6%) | **skeleton** |
| `\| Lump Sum Cont. \| … \| £116,555.45` | 17 | 4 (24%) | filled |
| `\| Statement 5 Answer \| Yes …` | 24 | 5 (21%) | filled |
| `\| Entertainment \| … £100.00 \| Monthly …` *(the income table)* | 32 | 10 (31%) | filled |
| `\| Year End \| … Annual Accounts …` | 33 | 18 (55%) | filled |

**Exactly one is a skeleton, and the rule catches it.** The others are genuinely filled tables —
they are simply the *wrong sections*: pension features, a bank statement answer, trading accounts.

So the fix works and the premise was too narrow. The Fact Find is not mostly empty forms; it is a
long document of many filled sections, and only one of them is about income. Ranking by content
density cannot separate "filled and relevant" from "filled and irrelevant", because both are
filled. **Category coverage was the wrong unit of measurement all along** — the unit that matters
is the section.

### 4.4 The cross-group pass still surfaces nothing

Stage 6.3 made the selection cross-side, relative and thresholded, and the section still reports
nothing useful. The pair that would matter cannot form this run: the £1,430 is in the *pack* but
never in a finding's `reportSays` or `fileSays`, and this pass reads findings, not packs.

**It cannot compose what no group wrote down.** That is a real limit of the design and worth
stating: it is a second-order fix that depends on the first-order one landing.

---

## 5. Exit criteria

| Criterion | Target | Run 3 | **Run 4** | |
| --- | --- | --- | --- | --- |
| Runs mis-reported as generated | 0 | 0 | **0** | ✅ |
| L1 plan violations | 0, enforced | 0 | **0** | ✅ |
| **Extraction sections succeeding** | all | 4 of 12 | **12 of 12** | ✅ |
| Extraction report reaching assessors | 100% | 100% | **100%** | ✅ |
| Checks reaching the Fact Find | every check | 10/10 | **10/10** | ✅ |
| **Unverified quote rate** | <12% | 14% | **11%** | ✅ |
| Groups flagged | <25% | 45% | **42%** | ❌ |
| Findings storing the model's requirement text | 0 | 2 | 3 of 60 | ⚠️ |
| **Benchmark recall** | ≥26/36 | 24/36 | **24/36** | ❌ |
| **Missed** | ≤3 | 5 | **8** | ❌ |

**Six met — the most yet — and the two that matter most are unmoved.** Every criterion about the
*machinery* is now green. Every criterion about *findings* is not.

---

## 6. What this run actually establishes

Three runs of remediation have now produced: complete category coverage, complete extraction,
an 11% citation failure rate, correct check outcomes — and **the same 24 findings every time**.

That is not a coincidence, and it is the thing to act on. The 12 findings the pipeline does not
reach are not blocked by any of the machinery that has been fixed:

| Blocked by | Findings |
| --- | --- |
| **Section-level routing** — right document, wrong part, or right part to the wrong group | F1.1, F1.2, F1.3, F5.1, F5.2, F5.4, F1.6 |
| **A third data shape** `DerivedFigures` does not read | F7.2 |
| **Cross-group composition**, which depends on the above landing first | F1.1, F1.3 |
| Judgement calls inside a group | F1.9, F3.4, F6.2, F8.1 |

**Seven of the twelve are one document section reaching the wrong groups.**

---

## 7. What to do next

| | Action | Why now |
| --- | --- | --- |
| 1 | **Let a query group target a document *section*, not just a category.** Index the section heading with each chunk and let `expectedCategories` carry an optional section hint. | The −£288 was fetched and handed to four groups that had no use for it, while the group that needed it got the planning docs. This is the whole of §4.2 and most of §6. |
| 2 | **Read paired `existing*Percentage` / `existing*Amount` properties in `DerivedFigures`**, not only `lines[]` | Third attempt at F7.2; the data finally exists and is one path away. |
| 3 | **Adjudicate £186.19: 0.18% or 0.52%?** | F7.2 is unscoreable until settled, and it decides whether the extraction or the benchmark is misreading the table. |
| 4 | Re-run holding the extraction | Run 4 had to re-extract for the schema fix. That excuse is now spent; the next run can hold everything. |
| 5 | Stop expecting the citation work to move recall | It has gone 36% → 11% and moved no findings. It is a trust measure, not a recall one, and should be reported as one. |

---

## 8. Method and limits

- Scored per the rubric, against the rendered output, recording the deciding sentence for each
  verdict. R7 (score what is printed) decided F1.1, F1.2 and F1.3 — all three are in packs and
  none in the output.
- **Two variables moved**, Stage 6 and a re-extraction, and here they are inseparable by design:
  the schema change *is* an extraction change. The extraction numbers in §2 are attributable to
  it; nothing else is.
- Caught has been 24 for three consecutive runs across four different canonical models and two
  retrieval configurations. **On a ±2 noise floor that is a plateau, not a coincidence** — and
  §6 is the argument for where it sits.
