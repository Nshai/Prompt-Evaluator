# Run 2 — after the remediation plan

Scored against [expected-results-benchmark.md](../../../expected-results-benchmark.md), compared
against [Run 1](../Run-1%20%28Before%20changes%20applied%29/) as analysed in
[the Run 1 analysis](../Run-1%20%28Before%20changes%20applied%29/run-analysis.md). The changes under test are
[remediation-plan.md](../../../remediation-plan.md), commits `e775a4c` and `77a2e51`.

> **Two files are called `run-analysis.md`** — one in each run folder. This one covers Run 2;
> the one in `Run-1 (Before changes applied)/` covers Run 1.

---

## 0. Which log is the run

**`checks_ABC-99_20260814_141041.log` is the run. `checks_ABC-99_20260814_141529.log` is a replay
of it**, and `Run-all-Checks-output.txt` is the replay's rendered output.

| | `141041` | `141529` |
| --- | --- | --- |
| First → last group | 14:10:52 → 14:13:18 | 14:15:30 → **14:15:32** |
| Wall clock, 60 groups | **146 s** | **2 s** |
| MD5 of the 60 responses | `3ff75458…` | `3ff75458…` — identical |

**The replay guard shipped in 0.1 caught this in production, unprompted.** The captured output
opens with:

> `*** REPLAY — NOT A GENERATED RUN. 86,252 output tokens in 2.9s is 29,293 tok/s, which no model
> produces… ***`

and the summary line reads `SUMMARY: REPLAY (cached) — 10 potential concern(s)`. This is the first
time in the project's history that a replay announced itself rather than being found weeks later by
hand. **It is also the ninth replay on record**, so the failure mode is not going away and the
guard is now load-bearing.

Because the two are byte-identical, the findings analysed below are the genuine run's. Everything
except the timings and the cost line can be read normally.

### The configuration changed in three ways at once

| | Run 1 | Run 2 |
| --- | --- | --- |
| Search limit | top **16** | top **8** |
| Extraction cap | 16,000 tok | **32,000** tok |
| Canonical model | `88219124f468` | **`c9fcad97d0f0`** (re-extracted) |
| Query plans | `21fa2f6e50b5` | **`8bf1dcc6186b`** (repaired) |
| Code | pre-remediation | **post-remediation** |

> **Read the recall numbers with this in front of you.** Retrieval breadth, the extraction, the
> plans and the assessment code all moved between these two runs. The recall improvement below is
> real but it is **not attributable to the remediation alone**, and this run does not isolate any
> single change. Where a specific fix can be traced to a specific finding, §3 says so and says how
> it was traced.

---

## 1. Headline

| | Run 1 | Run 2 | |
| --- | --- | --- | --- |
| **Benchmark recall (caught)** | 16 / 36 (44%) | **24 / 36 (67%)** | ▲ 8 |
| Partial | 12 | 6 | ▼ 6 |
| **Missed** | 8 | **6** | ▼ 2 |
| Check outcomes matching expected | 9 / 10 | **10 / 10** | ▲ |
| Groups the model marked a concern | 41 | 45 | ▲ |
| Groups vetoed by `comparisonPerformed` | 20 | 17 | ▼ |
| Unverified quote rate | 100/274 (36%) | 96/302 (**32%**) | ▼ 4pt |
| Groups carrying a flagged quote | 42/60 (70%) | 42/60 (70%) | — |
| Requirement text rewritten by the model | 25 / 60 | **0 / 60** | ▼ |
| Wrong requirement id echoed | 22 / 60 | 23 / 60 | — |

**Recall is up 23 points and every check now lands on the expected outcome.** Two exit criteria are
met, two are missed, and one is met for the wrong reason. §5 has the scorecard.

---

## 2. Recall against the 36 material findings

```
CAUGHT (24)   F1.8  F2.1  F2.2  F3.1  F3.3  F3.5  F4.1  F4.2  F4.3  F4.4
              F5.3  F6.1  F6.3  F7.1  F7.3  F7.5  F8.2  F9.1  F9.2  F9.3
              F9.4  F9.5  F10.1 F10.2

PARTIAL (6)   F1.1  F1.2  F1.9  F3.4  F6.2  F8.1

MISSED (6)    F1.3  F1.6  F5.1  F5.2  F5.4  F7.2
```

### Movement

| | Finding | Run 1 → Run 2 | What changed |
| --- | --- | --- | --- |
| ▲▲ | **F10.1** no-knowledge as an FG21/1 driver | missed → **caught** | CHK-010 reaches category D |
| ▲▲ | **F10.2** 69-year-old, no screening | missed → **caught** | same |
| ▲ | **F3.5** ATR upgrade rests on contradicted premises | partial → **caught** | G3.7 no longer vetoed |
| ▲ | **F4.1** "reasonable understanding" vs "no understanding" | partial → **caught** | knowledge question reaches CHK-004 |
| ▲ | **F6.3 / F9.3** Standard Life sold on lower cost while costing more | partial → **caught** | extraction report reaches CHK-008 whole |
| ▲ | **F7.1** page-5 vs page-11 charge tables | partial → **caught** | same |
| ▲ | **F9.4** retaining Zurich projects £128,000 | partial → **caught** | research ranking retrieved |
| ▲ | **F9.5** cost comparison unreliable | partial → **caught** | both routes now present |
| ▲ | **F1.2** state pension weekly vs monthly | missed → **partial** | Fact Find income table retrieved |
| ▼ | **F6.2** near-verbatim rationale bullets | **caught → partial** | see §4.3 |
| ▼ | **F7.2** charge computed on the wrong fund | **partial → missed** | see §4.2 |

Nine findings improved, two regressed. **The two regressions are the more instructive half.**

---

## 3. What the fixes actually did — traced

### 3.1 CHK-010 was transformed, and the trace is clean

Run 1: Indeterminate on all four groups, with the assessor arguing in its own words that
*"none of which inherently signal vulnerability"* while CHK-004 quoted the contradicting evidence
four times.

Run 2: **Potential Concern, G10.4 at High severity**, opening

> *"No trigger was recorded; this check applies to every case and was assessed anyway."*

— the wording added in 1.4 — and citing `Risk profile report.md [D]` directly:

> *"the file evidences a material mismatch between client's documented investment knowledge
> (limited/none) and the Risk 5 recommendation which requires 'some experience'"*
> *"Client is age 69 (approaching retirement) with no direct investment experience… The report does
> not address whether age combined with limited knowledge and near-retirement status creates
> foreseeable harm"*

Both benchmark findings, stated plainly. **This one is attributable**: category D appears in
CHK-010's packs for the first time (G10.1 `A B D`, G10.4 `B C D F I`), no CHK-010 query targeted D
before 1.3 added it, and the overlay wording is verbatim from 1.4.

### 3.2 The `comparisonPerformed` veto released real findings

Groups vetoed fell 20 → 17, and the ones released are the ones that mattered. **G3.7 was
Indeterminate in Run 1 and is Potential Concern / High in Run 2**, carrying F3.5:

> *"Investment period is 2 years [P8], but Risk 5 (Balanced) portfolio with potential for 'larger
> drops in value' [P7] is being recommended… no documented justification or risk reassessment is
> cited for why the questionnaire score was overridden upward."*

### 3.3 The untruncated extraction report reached the checks that needed it

CHK-008's summary now reads:

> *"internal inconsistencies flag conflicting charge data across **pages 5, 6, and 11** of the
> report itself"* … *"Standard Life is shown as 0.26% MORE expensive (not less), contradicting the
> report's assertion of only 0.08% disadvantage."*

That is F7.1 and F6.3/F9.3 in two sentences, in a check that in Run 1 saw a version of the
extraction report cut off 45% of the way through. Both the cap raise (2.2) and the richer
extraction (32,000-token cap, outside this plan) contribute; they cannot be separated here.

### 3.4 DerivedFigures is in every prompt and is correct

All 60 prompts carry the block, and it computes the total the pipeline previously got wrong:

```
- The 5 arrangements with a current value sum to £116,998.47 (People's Pension £6,601.55 +
  Scottish Widows £211.32 + Aviva £3,744.36 + Zurich £103,439.24 + Standard Life £3,002.00).
- OngoingCharge: £231.39 at 0.21% implies a fund value of £110,185.71, which matches no
  arrangement's current value.
```

F1.8 is caught and no longer depends on extraction happening to have summed it in prose.

### 3.5 The requirement is now the catalogue's

`0 of 60` findings carry rewritten requirement text, against 25 in Run 1. The scope-widening
observed at G1.7 cannot recur. The identifier divergence is unchanged at 23 of 60 — expected, since
that was never the defect, only the signal.

---

## 4. What did not work, including in my own changes

### 4.1 Category B is still missing where it matters most — and now I know why

Groups whose pack contains a Fact Find passage:

| Check | Run 1 | Run 2 | |
| --- | --- | --- | --- |
| CHK-001 | 5/9 | 5/9 | — |
| CHK-004 | 2/5 | **3/5** | ▲ |
| CHK-006 | 1/4 | **2/4** | ▲ |
| CHK-007 | **0/7** | **1/7** | ▲ |
| CHK-003 | 1/7 | **0/7** | ▼ |
| CHK-005 | 1/6 | 1/6 | — |
| **CHK-008** | **0/5** | **0/5** | — |
| **CHK-009** | **0/7** | **0/7** | — |

**The plan repairs did their job and the pack cap undid it.** `CaseDocumentSearchTool.SearchAsync`
runs each query twice — once restricted to its target categories, once unfiltered — so the Fact
Find passages *do* enter the candidate set. Then `Rank` orders by a single binary key (in the
group's targeted set, or not) and then by score, and takes twelve. Once B joins E/G/H/I in the
targeted set, that first key is uniform for every candidate and the ordering collapses to **pure
score** — and Fact Find prose scores below research and report prose for these queries. The B
passages are retrieved and then evicted.

Every affected pack is exactly at the cap: CHK-009's seven groups hold 12, 12, 12, 12, 12, 8, 12
passages.

**This is R3, "the passage cap binds before the search limit does" — the lever gap-analysis called
untested and my plan explicitly deferred.** §1.1 of the plan said adding a category was not
sufficient on its own and gave the wrong reason: I said the query text had to match the Fact Find's
vocabulary. The queries are fine. The ranking is what drops them.

**The fix is a per-category floor in `Rank`** — reserve one or two of the twelve slots for each
declared evidence category before filling the rest by score. That is gap-analysis item 5, which I
folded into "fix the plans" and should have kept separate.

This single defect accounts for **five of the six remaining misses**: F1.3, F1.6, F5.1, F5.2 and
F5.4 are all Fact Find facts. **F5.1 in particular is untouched** — CHK-005 still never sees the
Fact Find's `Total Monthly Disposable Income | £-288.00`, and G5.1 still concludes affordability is
supported.

### 4.2 F7.2 regressed, and DerivedFigures is why it did not catch it

`£186.19` appears **nowhere** in Run 2. In Run 1, G7.4 at least back-solved the implied fund value
for Zurich and listed Standard Life's as a missing input — partial credit. In Run 2 the group takes
a different route and never reaches the figure at all.

DerivedFigures should have covered this and did not, because **the charge lines it found were the
recommended plan's, not the existing arrangements'**. Its two outputs implied £110,185.71 and
£115,195.45 — the transfer total and something near it — and it reported both as *"matches no
arrangement's current value"*, which is true and useless. Two defects in my own code:

- **It should compare against the transfer total as well as arrangement values.** £110,185.71 *is*
  the amount being invested; saying it matches nothing is actively misleading.
- **It never saw the existing arrangements' charge lines**, so the Standard-Life-computed-on-Zurich
  case had no chance to fire. `costsAndCharges.existing[].lines` was empty in this model, and
  `existingArrangements[].charges` was not read for implied bases — only for the repeated-value
  check.

### 4.3 F6.2 regressed for an unrelated reason

Run 1 named all three verbatim-repeated rationale sentences. Run 2 gets as far as *"The lifestyling
statement appears twice identically in the canonical array, which under the guard rule suggests
boilerplate"* and then declines, saying it cannot verify client-specificity because the report's
own rationale text is not in the retrieved passages. This is model variance on a group whose
evidence side did not change, and it is a reminder that **±2 findings remains the noise floor**.

### 4.4 The cross-group pass is firing and its output is poor

The section renders, and its content is mostly unusable:

```
  Client income
    [CHK-001/G1.3] (2) Employed as Volunteer with Sea Cadets with income £20 per week (page 3)
    [CHK-005/G5.1] 55 is deducted from the fund transfer value (£110,185), not from monthly income
```

`55` is the tail of `£3,305.55`. **My sentence splitter splits on `.` and therefore splits every
decimal**, so half the fragments start mid-number. Of the six pairs reported, none is a genuine
contradiction; all six pair unrelated quantities that happen to share a cue word.

Two fixes: do not split on a period between digits, and require the two figures to be the same
*kind* of quantity rather than merely matching a cue. Until then the section is noise, and noise in
an addendum is worse than an absent addendum.

### 4.5 Citation verification improved less than hoped, exactly as predicted

36% → 32% unverified; flagged groups unchanged at 70%. Re-measuring the failures mechanically:

| | Run 1 | Run 2 |
| --- | --- | --- |
| Absent from their own prompt | 105 | 129 |
| …recoverable by decoding escapes | 7 | **0** |
| …recoverable by folding table pipes | 12 | 24 |
| …genuinely absent | 86 | **105** |

**The escape fix (3.1) worked completely** — zero remaining. Pipe-folding is doing more work than
before. But the residual is the bucket §3.4 of the plan identified and deliberately did not fix:
the model reformatting a table into prose and quoting the result. That is now **81% of all
failures**, and it is why the rate barely moved and why the report still says `0 no issue` —
**eleven passes were downgraded** by the pass-only rule.

The pass-only downgrade is behaving correctly; the input to it is still too noisy for the counts to
be read.

---

## 5. Exit criteria

| Criterion | Target | Run 2 | |
| --- | --- | --- | --- |
| Runs mis-reported as generated | 0 | **0** — the replay self-announced | ✅ |
| L1 plan violations | 0, enforced | **0**, enforced at load | ✅ |
| High-severity concerns demoted by `comparisonPerformed` | 0 | 17 groups still vetoed | ⚠️ improved, not met |
| Extraction report reaching assessors | 100% | **100%** | ✅ |
| Findings storing the model's requirement text | 0 | **0** | ✅ |
| Groups reaching category B where declared | ≥1 per group | **0/5 and 0/7** in CHK-008/009 | ❌ |
| Unverified quote rate | <12% | **32%** | ❌ |
| Groups flagged | <25% | **70%** | ❌ |
| **Benchmark recall** | ≥26/36 (72%) | **24/36 (67%)** | ⚠️ close |
| **Missed** | ≤3 | **6** | ❌ |

Four met, one close, four missed. **The four misses share two causes**: the pack cap evicting
category B, and the table-quotation behaviour left unfixed by design.

---

## 6. What to do next, in order

| | Action | Why now |
| --- | --- | --- |
| 1 | **Per-category floor in `Rank`** — reserve slots per declared evidence category before filling by score | Five of the six remaining misses are Fact Find facts that were *retrieved and then evicted*. Highest-value change available, and small. |
| 2 | Fix `CrossGroupContradictions`: don't split on decimal points; require same-kind quantities | The section is currently noise, and it is printed to reviewers. |
| 3 | Extend `DerivedFigures` to existing arrangements' charge lines and to the transfer total | F7.2 regressed to missed; the component that should own it never saw the right inputs. |
| 4 | Structured table citations (plan §3.4) | 81% of citation failures; blocks three exit criteria on its own. |
| 5 | Re-run at **top 16** with everything else held | Run 2 changed retrieval breadth, extraction, plans and code together. One controlled variable would make the next comparison mean something. |
| 6 | Write the scoring rubric before the next recall claim | 44% → 67% is far outside the noise floor, but F6.2 moved on model variance alone. |

---

## 7. Method and limits

- Recall was adjudicated by reading the rendered output in full against the benchmark's 36 material
  findings, the same way and by the same reader as Run 1 — so the two numbers are comparable to
  each other even though neither is reproducible by a second reader to better than ±2.
- **Three variables moved between the runs.** Attribution in §3 rests on mechanism (a category
  appearing in a pack for the first time, wording traceable to a specific commit), not on the
  recall delta.
- Category coverage and citation figures were measured by parsing the genuine log
  (`141041`), not the replay's rendered output.
- The `Run-1 (Before changes applied)` folder was renamed after
  [run-analysis.md](../Run-1%20%28Before%20changes%20applied%29/run-analysis.md) was written, so that document's links to its own logs no
  longer resolve.
