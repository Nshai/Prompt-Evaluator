# Run 5 — after Stage 7 and the front-end decoupling

Scored against [expected-results-benchmark.md](../../../expected-results-benchmark.md) using
[scoring-rubric.md](../../../scoring-rubric.md), compared against
[the Run 4 analysis](../Run-4/run-analysis.md). The changes under test are Stage 7 of
[remediation-plan.md](../../../remediation-plan.md) (commit `dcaafe4`) **and** the decoupling
refactor (commit `c6879f3`), which was intended to change no behaviour at all.

> **Five files are called `run-analysis.md`** — one in each run folder.

---

## 0. Which log is the run

**Both logs are genuine.** This is the first run in the series where that is true of the
extraction and the checks together.

| | Wall clock | Output tokens | Rate | Verdict |
| --- | --- | --- | --- | --- |
| `extract_ABC-99_20260814_212954.log` | **440.1 s** | 321,940 chars stored | — | genuine |
| `checks_ABC-99_20260814_213751.log` | **153 s** | 86,317 | **563 tok/s** | genuine |

563 tok/s sits inside the observed generated band (322–647) and is a factor of 40 below the
replay threshold of 2,000. For contrast, Run 4's `Run-Checks-output.txt` was a **24,554 tok/s**
replay and Run 3's extraction completed in **2.0 s**. There is only one checks log in this folder
and no replay banner anywhere in it.

### What changed since Run 4

**Three variables moved, not one.** This matters for attribution and is stated up front.

| | Run 4 | Run 5 |
| --- | --- | --- |
| Code | Stage 6 (`a13f1f1`) | **Stage 7 (`dcaafe4`) + refactor (`c6879f3`)** |
| Query plans | `8bf1dcc6186b` | **`57a050289d77`** — 7 groups gained `evidenceSections` |
| Canonical model | `5027a895fcff` | **`f83f31102d3f`** (re-extracted) |
| Search limit / extraction cap | top 8 / 12 per group / 32k | **all held** |

The plan change is narrow and targeted, and it is the reason to read this run carefully. Seven
groups in CHK-001, CHK-005 and CHK-008 gained section hints naming
`Current Monthly Cash Flow`, `Total Net Monthly Income`, `Total Monthly Disposable Income`,
`Existing Pension Provision`, `Residency` and `Contact Address` — aimed squarely at the Fact Find
cluster that had been missed for three consecutive runs.

---

## 1. Headline

| | Run 2 | Run 3 | Run 4 | **Run 5** | |
| --- | --- | --- | --- | --- | --- |
| Extraction sections succeeding | — | 4 of 12 | 12 of 12 | **12 of 12** | — |
| Canonical paths resolved / absent | 115 / 30 | 115 / 30 | 121 / 24 | **122 / 23** | ▲ |
| **Unverified quote rate** | 32% | 14% | 11% | **18%** | ▼ **worse** |
| Groups carrying a flagged quote | 70% | 45% | 42% | **52%** | ▼ worse |
| Citations / unverified | 302 / 96 | 350 / 49 | 334 / 38 | **343 / 61** | |
| Wrong requirement id echoed | 23/60 | 19/60 | 24/60 | **29/60** | ▼ worse |
| Check outcomes matching expected | 10/10 | 10/10 | 10/10 | **10/10** | — |
| Checks reaching the Fact Find | 7/10 | 10/10 | 10/10 | **10/10** | — |
| **Benchmark recall (caught)** | 24/36 | 24/36 | 24/36 | **24/36 (67%)** | — |
| Partial | 6 | 7 | 4 | **10** | |
| **Missed** | 6 | 5 | 8 | **2** | ▲▲ **best yet** |
| Cost (checks) | — | — | £1.0719 | £1.1216 | |

**Recall is 24/36 for the fourth consecutive run. The missed count fell from 8 to 2 — the lowest
of the series — and the set of findings behind the unchanged 24 turned over almost completely.**

---

## 2. Recall against the 36 material findings

```
CAUGHT (24)   F1.1  F1.3  F1.8  F2.1  F2.2  F3.3  F3.5  F4.1  F4.2  F4.3
              F4.4  F5.1  F5.2  F5.3  F6.1  F6.2  F7.3  F7.5  F8.2  F9.1
              F9.2  F9.5  F10.1 F10.2

PARTIAL (10)  F1.2  F1.9  F3.1  F3.4  F6.3  F7.1  F7.2  F8.1  F9.3  F9.4

MISSED (2)    F1.6  F5.4
```

### The number did not move and almost everything under it did

For three runs the caught set was **identical** — the same 24 findings, run after run. That
stopped here. Five findings entered and five left:

| In (Missed/Partial → Caught) | Out (Caught → Partial) |
| --- | --- |
| **F1.1** report £300/wk vs Fact Find £1,200/mo | **F3.1** three risk ratings unreconciled |
| **F1.3** Kim's £1,100 and Sea Cadets £20/wk unrecorded | **F6.3** "Lower Costs" leads a switch that raises charges |
| **F5.1** −£288 deficit vs "comfortably meets" | **F7.1** the report's two charge tables contradict |
| **F5.2** affordability rests on unrecorded income | **F9.3** same as F6.3, switch side |
| **F6.2** switch rationale is boilerplate across three plans | **F9.4** retaining Zurich beats the recommendation |

Ten changed verdicts against a **±2 noise floor** is far outside noise. The headline of "67%,
unchanged" is the least informative true statement available about this run.

---

## 3. The section hint worked, and the evidence is mechanical

This is the clearest causal result in five runs.

| | Run 4 | Run 5 |
| --- | --- | --- |
| Mentions of `-£288` in the output | **0** | **39** |
| Mentions of `1,430` (Total Net Monthly Income) | **0** | **33** |
| Groups stating the deficit as a discrepancy | 0 | **4** (G1.4, G5.1, G5.2, G5.4) |

Run 4's diagnosis was that the −£288 *reached four groups' passage packs and nobody mentioned it*
— routing, not retrieval. Stage 7 named the Fact Find sections the plan needed, and CHK-005/G5.4
now states the finding in the benchmark's own terms:

> "The canonical model reports a net monthly surplus of £2,786.67, yet the fact find … shows a net
> monthly deficit of £-288.00 [P1]. This is not a minor variance but a reversal of sign totalling
> approximately £3,074.67."

That is F5.1 caught outright, and F5.2, F1.1 and F1.3 fall out of the same group and G1.4. The
four findings that landed map exactly onto the sections named in the plan diff. This is
**mechanism, not correlation** — the hint named `Total Monthly Disposable Income`, and the figure
under that heading went from absent to stated four times.

**Two hints in the same commit did nothing.** `Residency` and `Contact Address` were added to
CHK-001/G1.1 and CHK-008 to reach F1.6 (client is a private tenant; the cashflow "incorporates the
assets of your main residence"). The words *Tenant*, *Residency* and *main residence* appear
**nowhere in the run's output**, and no group reached the area at all. F1.6 and F5.4 are the only
two outright misses left, and they are the two the section hint was aimed at and failed to move.
Whatever makes a hint work, naming the section is not sufficient on its own.

---

## 4. The regressions, and one that is not what it looks like

### 4.1 F3.1 — the pipeline found it and threw it away

This is the most important defect the run exposes, and it would be invisible from the output.

CHK-003/G3.6 wrote, in its own `discrepancies` array:

> "P11 file notes state 'Risk rating of 6' in the ATR wording section, **contradicting all other
> sources** which state the final agreed rating is 5."

That is F3.1, stated correctly, both sides named. The group then returned **`NoIssue`** — and
because the renderer prints only groups that raised a concern, the entire group was suppressed.
The string "Risk rating of 6" appears nowhere in `Run-Checks-output.txt`.

In Run 4 the same group reached the same finding and returned `PotentialConcern`, so it printed
and scored Caught. Nothing about the retrieval changed: the file note reached G3.6's pack in both
runs. **The finding was not lost; the verdict was.** Under [rubric R7](../../../scoring-rubric.md)
— score what is printed — this is Partial, not Caught, and it is a defect in outcome selection
rather than in retrieval or routing.

Worth noting what this implies for the whole series: a group can hold a correct material finding
and be deleted from the report by its own outcome field. Nothing currently surfaces that.

### 4.2 F6.3 / F9.3 — the right figure attached to the wrong plan

The benchmark finding is that **"Lower Costs" leads the Standard Life switch rationale, which the
report's own table shows increases charges by 0.26% / £268.94.**

Run 5 has the figure. CHK-009/G9.2's `reportSays` reads:

> "for **Zurich**, annual charge 0.26% HIGHER, costing £268.94 pa additional … for Standard Life,
> no specific charge differential stated"

The £268.94 is the Standard Life row, attributed to Zurich, and Standard Life is then recorded as
having no differential at all. Under the rubric's *conflict is misidentified* bullet this is
Partial. Both F6.3 and F9.3 turn on the same sentence and both move together.

### 4.3 F7.1 — contradicted against the wrong document

The benchmark's route is the report contradicting **itself**: Existing Arrangements (p5) gives
Zurich 0.18% / Standard Life 0.52%; Pension Switch Charges (p11) gives Zurich 0.93% /
Standard Life 0.18%. Run 5 has both figures and tests them against *provider documentation*
instead:

> "EA4 (Zurich) existing charge stated as 0.93% in report; Zurich provider documentation shows
> annual management charge of 0.75% (P1)"

Right area, right figures, wrong contradiction — Partial under
[R4](../../../scoring-rubric.md). The same shift explains **F9.4**: the run reaches "retaining
beats switching" through the £127,000 / £120,000 portfolio totals, but £128,000 — the
retain-Zurich-alone figure the benchmark names — appears nowhere in the run.

**A pattern connects all four.** In every case the run has the numbers and compares them against
an external document rather than against the report's other page. Run 4's Stage 7.3 note found
the extraction *merging* page 5 with page 11 for the Standard Life charge — the same two tables.
The re-extraction under `f83f31102d3f` appears to have made the report-internal comparison harder
to reach, not easier. Item 7.6 in the remediation plan is the relevant one and it remains unbuilt.

### 4.4 Citation trust went backwards

| | Run 3 | Run 4 | **Run 5** |
| --- | --- | --- | --- |
| Citations | 350 | 334 | **343** |
| Unverified | 49 | 38 | **61** |
| **Rate** | 14% | **11%** | **18%** |
| Groups carrying a flag | 45% | 42% | **52%** |

The 11% figure was recorded as *target met* in Run 4. It is not met now. Inspection of the 61
shows the largest single category is **table quotations that differ only in whitespace** from the
passage they came from — G5.2 flags `"Total Net Monthly Income  |  £1,430.00"` as untraceable
while citing `table: Total Net Monthly Income  |  £1,430.00` two lines below. That is a
normalisation gap in the verifier, not a model that invented a quote, and it inflates the rate
against a run whose citations are otherwise more grounded than Run 4's.

It is still a regression on the printed metric, and every one of those groups was downgraded.

### 4.5 Prompt adherence drifted further

29 of 60 responses echoed the wrong requirement id, against 24 in Run 4 and 19 in Run 3. The plan
values are used regardless, so nothing downstream is wrong, but the trend across four runs is the
wrong way and this remains the open defect recorded in the Run 1 analysis.

---

## 5. Did the refactor change behaviour?

The decoupling commit claimed to preserve behaviour. It cannot be cleanly proven from this run
because the plans and the canonical model moved in the same window, but the available evidence
says **the refactor is not implicated**:

- **Retrieval is byte-stable across four runs**: 165 searches, 2,614 passages, in Runs 2, 3, 4
  and 5 alike. A change to service construction, lifetimes or DI wiring that altered behaviour
  would be very unlikely to leave those identical.
- **Cost and token counts moved by 5%** (£1.0719 → £1.1216), consistent with a re-extracted model
  and longer packs, and the embedding line is unchanged at £0.0050 / 2,007 tokens — which is the
  specific number that would have broken had the search-service factory been wired to the
  container's embedding generator rather than the caller's.
- **Every recall change traces to a plan hint or the extraction**, both of which are data, not
  code paths the refactor touched.

The stronger statement — that the refactor changed nothing — needs the controlled re-run that
remediation-plan item 7.4 describes and which still has not been done. Four runs have now each
moved two or three variables at once.

---

## 6. Exit criteria

| Criterion | Run 2 | Run 3 | Run 4 | **Run 5** |
| --- | --- | --- | --- | --- |
| Extraction sections succeed | — | 4/12 | 12/12 | **12/12 ✅** |
| Checks reach the Fact Find | 7/10 | 10/10 | 10/10 | **10/10 ✅** |
| Unverified quote rate ≤ 12% | 32% | 14% | 11% ✅ | **18% ❌** |
| Check outcomes match expected | 10/10 | 10/10 | 10/10 | **10/10 ✅** |
| Recall ≥ 75% (27/36) | 67% | 67% | 67% | **67% ❌** |
| **Missed ≤ 4** | 6 | 5 | 8 | **2 ✅** |
| Run is not a replay | ❌ | ext ❌ | checks ❌ | **✅ both** |

**The benchmark's own guidance is that the gate belongs on missed-rate, not on agreement** — *"A
missed concern leaves no trace in the output; a spurious one is visible and can be discarded by a
reviewer."* On that measure this is comfortably the best run of the series: 22% → 6%.

---

## 7. What this run establishes

1. **Routing was the right diagnosis, and section hints are a working lever.** Run 4 said the
   evidence was arriving and not being used. Naming the section moved four findings from missed
   to caught and drove the Fact Find deficit from 0 to 39 mentions. This is the first change in
   five runs with a clean mechanical trace from commit to finding.

2. **The lever is not general.** The same commit's `Residency` hints moved nothing, and those two
   findings are now the only outright misses. Section naming works where the section is a
   labelled table and fails where the fact is prose.

3. **A correct finding can be deleted by its own outcome.** G3.6 stated F3.1 and returned
   `NoIssue`. Recall measured from the printed report cannot see this, and neither can a reviewer.
   This is a new failure mode, distinct from the retrieval and routing failures of Runs 2–4.

4. **The report-internal comparisons got worse as the file-versus-report comparisons got better.**
   Four of the five findings lost this run are cases where the run had both figures and checked
   them against a provider document instead of against the report's other page. That is the same
   defect as unbuilt item 7.6.

5. **Recall has now been 67% four times while the underlying set changed completely.** A single
   number on a chart would show a flat line through a run in which ten of thirty-six verdicts
   moved. Report all three counts, as the rubric requires.

---

## 8. What to do next

Ordered by expected findings per unit of work.

1. **Surface findings that groups discard** *(recovers F3.1, and unknown others)*. A group that
   returns `NoIssue` while its own `discrepancies` array is non-empty and names a material
   contradiction should either be printed or be flagged. At minimum, print a count. This is the
   only item here that recovers a finding the pipeline has *already found*.
2. **Build item 7.6 — stop the extraction reconciling the two charge tables** *(F7.1, F7.2, F6.3,
   F9.3, F9.4)*. Five of the ten partials sit on report-internal contradictions the extraction
   smooths over. This is the largest single cluster remaining.
3. **Normalise whitespace in the citation verifier** *(restores the 11% metric)*. Table quotations
   differing only in internal spacing are being rejected, downgrading groups whose citations are
   correct.
4. **Do the controlled re-run (item 7.4)** — one variable, plans and model pinned. Four runs have
   moved two or three at once and the refactor's behaviour-preservation is still unproven.
5. **Work out why the `Residency` hint did nothing** *(F1.6, F5.4)*. These are the last two
   outright misses and the hint mechanism that reached the cash flow table did not reach them.

---

## 9. Method and limits

- Scored from `Run-Checks-output.txt` — the rendered output, per rubric §1 — with the log consulted
  only to establish what a group found before its outcome suppressed it (§4.1).
- All 36 findings were taken in benchmark order; each verdict rests on a quoted sentence.
- **Verdicts I hesitated over**, per rubric §4.5, and which the next revision should settle:
  **F7.2** (£186.19 now appears once, in a summary field, with the *charge* questioned but not the
  wrong-fund arithmetic — scored Partial, defensible as Missed) and **F3.1** (found and suppressed
  — Partial under R7, but R5 says outcome is irrelevant to the verdict; the two rules conflict when
  the group is not printed at all, and R7 was preferred because the reader never sees it).
  On the stricter reading of both, the run is **24 / 8 / 4** — missed still halved from Run 4.
- **Three variables moved together** (code, plans, canonical model). Attribution in §3 rests on the
  plan diff naming the exact sections whose figures appeared; attribution of the §4 regressions is
  weaker and is stated as a pattern, not a cause.
- The run was produced from a different working copy (`C:\work\Prompt-Evaluator-Semantic`,
  documents from `C:\work\Prompt-Evaluator-Forms`) than this repository. The plan and model
  fingerprints in the header are the only evidence tying it to `dcaafe4`, and they match.
- One case, one adjudicator, and no false-positive measurement is possible on this dataset.
