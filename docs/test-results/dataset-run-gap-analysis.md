# Gap analysis — dataset runs, case ABC-99

Measures the four runs in [../dataset/](../dataset/) against the answer key in
[expected-results-benchmark.md](expected-results-benchmark.md).

> **Headline.** Determinism is solved: three consecutive runs are byte-identical across all 60
> requirement groups. Category filtering works and category C is no longer starved. Recall against
> the benchmark is **53% of material findings caught outright, 19% partially, 28% missed** — and
> the misses are not scattered. Seven of the ten come from the Fact Find, which four checks never
> saw a single passage from. Two failure modes now dominate the output and both are mechanical:
> 17 groups decline to answer, and 38% of quotations fail verification, most of them on
> punctuation.

---

## The runs

| Run | Started | Searches | Hits | Passages | Contains |
| --- | --- | --- | --- | --- | --- |
| `001513` | 00:15:13 | 154 | 1,232 | 650 | GAP 1, GAP 6 indexing, extraction report |
| `095533` | 09:55:33 | 154 | 2,436 | 682 | …plus GAP 2 category filtering |
| `100051` | 10:00:51 | 154 | 2,436 | 682 | identical |
| `100428` | 10:04:28 | 154 | 2,436 | 682 | identical |

All measurements below are of `100428` unless stated.

---

## 1. Determinism — solved

Comparing the three post-fix runs group by group:

| | |
| --- | --- |
| Identical evidence pack | **60 / 60** |
| Identical response JSON | **60 / 60** |
| Groups whose outcome differs | **0 / 60** |

Byte-identical, not merely equivalent. The three log files differ only in the order sections were
written, which is the parallel executor interleaving its writes — the content of every section
matches. The question that started this work, *why does re-running produce different results*, is
answered: it no longer does.

Worth keeping: the ordering of log sections is the one remaining nondeterminism, and it is
cosmetic. It does mean two logs cannot be compared with `diff`, which is why the comparison above
is done by group key.

---

## 2. Category filtering — works, and shifts the problem

GAP 2's two-pass search is live: 2,436 hits from 154 searches, against 1,232 before. Coverage
against the number of groups whose plans target each category:

| | Category | Docs | Target | Baseline | Now | |
| --- | --- | --- | --- | --- | --- | --- |
| A | Client Authority | 2 | 5 | 15 | 9 | 180% |
| **B** | **Know Your Client** | **1** | **24** | **15** | **16** | **67%** |
| **C** | **Meetings & Communications** | **1** | **16** | **5** | **10** | **62%** |
| D | Risk/Capacity/Experience | 1 | 8 | 16 | 10 | 125% |
| E | Existing Plans | 12 | 12 | 25 | 14 | 117% |
| F | Needs Analysis | 2 | 21 | 19 | 19 | 90% |
| G | Research | 3 | 22 | 38 | 25 | 114% |
| H | Solution Design | 5 | 17 | 34 | 19 | 112% |
| I | Recommendations | 1 | 60 | 33 | 45 | 75% |

**Category C doubled**, from 5 groups to 10, and the effect is visible in the findings: G3.6 now
quotes the file note's *"Risk rating of 6"* correctly, which is the defect that started this whole
line of work.

The over-surfaced categories fell back toward their targets, which is the intended behaviour — the
targeted query displaces passages from categories that were only ever winning on document volume.

**But category B is now the starved one, and it is where the misses live.** Per check:

| Check | Groups reaching B | CSV names B primary? |
| --- | --- | --- |
| CHK-001 | 5 / 9 | yes |
| CHK-002 | 1 / 6 | yes |
| CHK-003 | 1 / 7 | yes |
| CHK-004 | 3 / 5 | yes |
| CHK-005 | 1 / 6 | yes |
| CHK-006 | 1 / 4 | yes |
| **CHK-007** | **0 / 7** | **yes** |
| **CHK-008** | **0 / 5** | **yes** |
| **CHK-009** | **0 / 7** | **yes** |
| CHK-010 | 4 / 4 | yes |

Four checks saw **no passage from the Fact Find at all**, and the catalogue names Know Your Client
as a primary category for every one of them. Category B is a single 42 KB document holding the
client's income, expenditure, disposable income, residency, tax rate, will and pension inventory —
the assertion side for most of CHK-001 and effectively all of CHK-005.

---

## 3. Recall against the benchmark

Of the **36 material findings**:

| | Count | |
| --- | --- | --- |
| **Caught** — contradiction stated with both sides named | **19** | 53% |
| **Partial** — area reached, conflict misstated or buried | **7** | 19% |
| **Missed** — no trace in any group | **10** | 28% |

Per check:

| Check | Material | Caught | Partial | Missed | Expected | Run outcome |
| --- | --- | --- | --- | --- | --- | --- |
| CHK-001 | 6 | 2 | 1 | 3 | Potential Concern | Potential Concern ✓ |
| CHK-002 | 2 | 2 | — | — | Potential Concern | Potential Concern ✓ |
| CHK-003 | 4 | 2 | 1 | 1 | Potential Concern | Potential Concern ✓ |
| CHK-004 | 4 | **4** | — | — | Potential Concern | Potential Concern ✓ |
| CHK-005 | 4 | 1 | — | 3 | Potential Concern | Potential Concern ✓ |
| CHK-006 | 3 | 2 | 1 | — | Potential Concern | **Indeterminate ✗** |
| CHK-007 | 4 | 2 | 1 | 1 | Potential Concern | Potential Concern ✓ |
| CHK-008 | 2 | — | 2 | — | Potential Concern | Potential Concern ✓ |
| CHK-009 | 5 | **5** | — | — | Potential Concern | Potential Concern ✓ |
| CHK-010 | 2 | — | 1 | 1 | Potential Concern | Potential Concern ✓ |

Check-level agreement is 9/10, but as the benchmark warns, that number is nearly meaningless when
every expected outcome is the same. The recall column is the real result.

**Where it does well.** CHK-009 and CHK-004 are complete. CHK-009 found the whole switch argument
— *"Consolidated projection shows switching DECREASES fund value"*, the Standard Life 0.08%/0.26%
contradiction, and the research ranking. CHK-004 caught every knowledge finding, quoting the
questionnaire against the report's assertion. CHK-006 found the single strongest fact in the case:
*"ranks the New Solution fifth by maturity value (£125,000) behind Zurich (£128,000), Existing
Solution (£127,000)…"*

**Where it fails.** CHK-005 missed three of four, CHK-001 three of six.

### The ten missed findings

| ID | Finding | Why |
| --- | --- | --- |
| F1.2 | State Pension recorded as £230 **monthly** in the Fact Find, used as £230 **weekly** throughout the report | Fact Find income table never retrieved |
| F1.3 | Kim's £1,100/month and the Sea Cadets income are not in the Fact Find | Fact Find income table never retrieved |
| F1.6 | Client rents; cashflow includes a main residence, IHT section discusses RNRB | Fact Find address page never retrieved |
| F1.8 | Pension totals £110,000 / £106,000 against an actual £116,998 | Requires summing a table — no group attempted it |
| F3.4 | People's Pension retained at fund risk level 9 against ATR 5 | Retained plans are outside every plan's search text |
| F5.1 | Fact Find states **Total Monthly Disposable Income –£288.00** | Fact Find cashflow page never retrieved |
| F5.2 | Affordability rests on income the Fact Find does not record | Depends on F1.2/F1.3 |
| F5.4 | Cashflow includes a property the client does not own | Depends on F1.6 |
| F7.2 | Standard Life charges computed on Zurich's fund value (£186.19 = 0.18% of £103,439) | Requires arithmetic across two tables |
| F10.1 | Recorded absence of investment knowledge as a vulnerability indicator | CHK-004 found the fact; CHK-010 never connected it |

**Seven of the ten are Fact Find facts.** F1.2, F1.3, F1.6, F5.1, F5.2, F5.4 all sit on three
pages of one document that CHK-005 reached once and CHK-001 five times in nine groups. This is not
a reasoning failure — the model was never shown the page.

The other three are a different failure: **F1.8 and F7.2 require arithmetic** (summing five fund
values; checking that £186.19 is 0.18% of the wrong number), and **F10.1 requires carrying a fact
between checks**. Nothing in the design does either.

### The seven partials

Worth separating from misses, because they show the reasoning working and the framing failing:

- **F8.1** — G8.4 finds the £12,000/9.4% figure but reports it as *"stated in the report but not
  independently corroborated"*. It is corroborated, in the switch report; and the finding is not
  that it lacks support but that it contradicts the body's "Lower Costs" claim.
- **F7.1** — G7.4 catches Zurich at 0.93% against 0.75% in the policy document, but not the
  report's own p5-versus-p11 contradiction, which needs no external evidence at all.
- **F10.2** — G10.4 says *"evidence contains no vulnerability assessment"*, which is true and is
  the finding, but frames it as an evidence gap rather than a concern about the advice.
- **F3.5, F6.3, F1.9, F8.2** — area reached, contradiction not stated.

---

## 4. Two mechanical failure modes now dominate the output

### 4a. Seventeen groups decline to answer

`comparisonPerformed: false` fired 17 times (28% of groups). This is the GAP 1 fix doing exactly
what it was built to do, and it is a real improvement over inventing an answer.

But **CHK-006 returned Indeterminate on all four groups**, so the check as a whole reports that it
could not be assessed — while G6.4's own text contains the strongest finding in the entire case.
The reasoning succeeded and the outcome discarded it.

That is the one wrong check-level outcome in the run, and it is not a retrieval problem. The four
groups declined because `/recommendations[]/rationale[]/text` and `isClientSpecific` are absent
from the canonical model, so the assertion side was empty. The rule is right in general — do not
guess at what the report says — but it fires even when the retrieved evidence is by itself
decisive.

**Fix:** a group with an empty assertion side but a finding grounded in retrieved evidence should
be able to report that finding. Indeterminate should mean *I could not compare*, not *one input
was missing*.

### 4b. Citation verification flags formatting as fabrication

| | Baseline `001513` | Current `100428` |
| --- | --- | --- |
| Citations | 280 | 291 |
| Failing verification | 95 (34%) | **112 (38%)** |
| Groups flagged | 41 / 60 | **43 / 60** |

It got worse, not better, as retrieval improved — more tabular evidence retrieved means more
quotes carrying table punctuation. Classifying the failures: **roughly 40% differ from the
evidence only in punctuation** — markdown table rows quoted with their `|` separators — and a
further fifth are elisions or joins with substantial contiguous runs present in the pack. A share
of the rest are quotes from the extraction report, which the prompt now instructs the assessor to
consult but which `EvidenceTextOf` does not include in the haystack.

At 43 of 60 groups flagged, the signal no longer discriminates. This is recorded in full as
[GAP 7](../gap-analysis.md#gap-7--citation-verification-flags-formatting-as-fabrication) and is
the smallest high-value fix outstanding.

---

## 5. Retrieval has no relevance floor

Unchanged and now measurable more precisely:

| | |
| --- | --- |
| Groups at the 12-passage cap | **56 / 57** |
| Lowest score admitted | **0.503** |
| 10th percentile | 0.576 |
| Median | 0.618 |

Every group that retrieves anything is full. The lowest-scoring passage in the run scored 0.503
and was presented as evidence indistinguishable from a 0.765 match. `"Nothing was retrieved"`
appears zero times across all four logs.

The two-pass search made this slightly worse in one respect: targeted hits are merged ahead of
unfiltered ones, so a weak passage from a target category displaces a stronger one from elsewhere.
That is the right trade — it is what fixed category C — but it means the cap is now doing more
work and a score floor matters more than it did.

---

## 6. What to do next, in order

| | Change | Why | Effort |
| --- | --- | --- | --- |
| 1 | **Guarantee the Fact Find reaches CHK-005, CHK-007, CHK-008, CHK-009** | Seven of ten missed findings are in a document four checks never saw. Largest single recall win available | small |
| 2 | **Fold table punctuation in `Normalise`; add the extraction report to `EvidenceTextOf`** | 38% of quotes fail on formatting; the check has stopped discriminating | small |
| 3 | **Let a group with an empty assertion side still report an evidence-grounded finding** | Turns CHK-006 from Indeterminate into the correct Potential Concern, and recovers the case's strongest finding | small |
| 4 | **Minimum score threshold** | 0.503 is being presented as evidence; the distribution now has enough data to calibrate from | small |
| 5 | **Populate `rationale[].text`, `isClientSpecific`, `dateOfBirth` and the other absent paths** | Directly causes 4a; 33 paths still resolve to nothing | medium |
| 6 | **Compute derived figures in code** — sums, percentages, ages | F1.8 and F7.2 are arithmetic the model will not do reliably | medium |

Items 1–4 are all small, and together they address 12 of the 17 findings currently missed or
partial.

**A note on measuring.** The check-level outcome is not a usable metric on this dataset — every
expected outcome is Potential Concern, so a pipeline that always said so would score 100%. Gate on
**missed material findings** instead, and build a second case with clean checks before trusting
any false-positive number.
