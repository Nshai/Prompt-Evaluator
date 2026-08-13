# Gap analysis — case ABC-99

One document, consolidating what was previously spread across three. Covers the whole pipeline —
extraction of the canonical model, retrieval over the case file, and assessment against the ten
checks — measured on every run in [Runtime-Logs/](Runtime-Logs/).

Supersedes `docs/gap-analysis.md`, `docs/test-results/dataset-run-gap-analysis.md` and
`Runtime-Logs/extraction/gap-analysis.md`. The answer key it is scored against is
[expected-results-benchmark.md](expected-results-benchmark.md); the settings to run with are in
[optimal-configuration.md](optimal-configuration.md).

---

## Contents

1. [What the runs actually are](#1-what-the-runs-actually-are) — over half are cache replays
2. [Where the pipeline stands](#2-where-the-pipeline-stands) — the gap register
3. [Extraction](#3-extraction) — E1–E6
4. [Retrieval](#4-retrieval) — R1–R3
5. [Assessment](#5-assessment) — A1–A4
6. [Recall against the benchmark](#6-recall-against-the-benchmark)
7. [What to do next](#7-what-to-do-next)
8. [Corrections this document makes](#8-corrections-this-document-makes)

---

## 1. What the runs actually are

**Twelve run logs exist. Five were generated. Seven are gateway cache replays.** This has to come
first, because two earlier analyses drew conclusions from replays without knowing it.

A replay is identifiable from the log alone: it reproduces tens of thousands of tokens in two or
three seconds. The application has no response cache — the code was checked — and prefix caching
reduces the cost of *reading* input, not of *writing* output.

| Run | Kind | Wall clock | Implied rate | Searches | Hits | Passages | Genuine? |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `001513` | checks | 250s | 322 tok/s | 154 | 1,232 | 650 | **yes** |
| `095533` | checks | 126s | 635 tok/s | 154 | 2,436 | 682 | **yes** |
| `100051` | checks | 3s | 26,650 tok/s | 154 | 2,436 | 682 | replay |
| `100428` | checks | 2s | 39,975 tok/s | 154 | 2,436 | 682 | replay |
| `extract 125630` | extract | 2s | 49,058 tok/s | — | — | — | replay |
| `extract 142545` | extract | 412s | 170 tok/s | — | — | — | **yes** |
| `143309` | checks | 121s | 586 tok/s | 154 | 308 | 209 | **yes** |
| `150027` / `150038` / `150042` | checks | 2s each | 35,482 tok/s | 154 | 308 | 209 | replay ×3 |
| `extract 160644` | extract | 2s | 34,950 tok/s | — | — | — | replay |
| `160653` | checks | 129s | 647 tok/s | 154 | 4,791 | 684 | **yes** |

Two consequences, both material:

**A replay reproduces failures as faithfully as successes.** The extraction whose truncation
started this whole line of work — `125630` — is itself a replay of an earlier run. The truncated
response is real; it was simply served from cache when re-requested. The diagnosis stands, but the
run does not date the defect.

**No two genuine runs share a configuration.** The four genuine check runs are one per
configuration: pre-fix at top 8, post-fix at top 8, top 1, top 16. **Determinism is therefore
untested**, and every apparently-identical pair on record is one generation and one replay.

> **Operational rule.** Read a run's wall-clock duration before treating it as evidence. Two
> minutes is a run; two seconds is a recording of one.

---

## 2. Where the pipeline stands

| | Gap | Severity | Status |
| --- | --- | --- | --- |
| **E1** | Extraction section truncated at the output cap | critical | **fixed & verified** |
| **E2** | Cross-reference identifiers do not join | critical | **fixed & verified**, one residual |
| **E3** | Quote volume consuming the output budget | high | **fixed** — 476 quotes → 249 |
| **E4** | Extraction under-reports its own gaps | high | partly fixed |
| **E5** | Model answering fields the process owns | moderate | **fixed & verified** |
| **E6** | Free text in enum fields | moderate | **caught**, not prevented |
| **R1** | No relevance floor | high | **open** — unchanged in all four runs |
| **R2** | Category B unreachable for three checks | high | **open** — invariant across 16× of search limit |
| **R3** | Passage cap binds before the search limit does | high | **open** — newly measured |
| **A1** | Assessor manufactured reconciliations | critical | **fixed & verified** |
| **A2** | Citation verification flags formatting as fabrication | high | **open** — 38% of quotes |
| **A3** | Indeterminate fires when one input is absent | high | **open** |
| **A4** | Derived figures delegated rather than computed | medium | **open** |

Six of thirteen are closed. **Every open gap is now on the retrieval or assessment side; extraction
is essentially done.**

---

## 3. Extraction

Measured `before` = the model in
[extraction/ABC-Cononical-Model.txt](Runtime-Logs/extraction/ABC-Cononical-Model.txt);
`after` = the genuine run
[extract_142545](Runtime-Logs/extraction/result/extract_ABC-99_20260813_142545.log).

| | before | after |
| --- | --- | --- |
| Sections failed | 1 of 12 | **0** |
| Recommendations extracted | **0** | **5** |
| Total JSON | 256,908 bytes | 208,507 |
| Quotes carried | 476 | **249** |
| Duplicate quotes | 141 | **47** |
| Dangling objective references | **18** | **1** |
| Enum violations reaching the model | 47 | **1** |
| Absent canonical paths | 33 | **25** |
| `internalInconsistencies` reported | 3 | 5 |

### E1 — Truncation, not malformed output *(fixed)*

The reported error was *"the model did not return a JSON object for this section"*. It did: 61,422
characters of well-formed JSON, cut off mid-string at the 16,000-token cap, **with four of five
recommendations already complete**. All four were discarded.

All twelve sections emitted a ` ```json ` fence; eleven closed it. Recommendations did not — the
one signal nothing was looking at.

Fixed three ways: the cap raised to 32,000; truncation distinguished from malformed output and
reported honestly; and a cut-off reply salvaged to its last complete array element. The prompt
change in E3 mattered more than the cap — recommendations fell to 32,202 characters, which fits
inside the *old* limit.

### E2 — Identifiers that do not join *(fixed; one residual)*

The larger defect, and silent. Each section is a separate call, so a pass that has not seen the
objectives pass cannot know what it called them. The objectives pass assigned `OBJ-001`–`OBJ-003`;
the recommendations pass wrote `linkedObjectiveIds` of `"LowerCosts"` and
`"ConsolidationAndClarity"`. **Eighteen references, none resolving, no error raised.** CHK-006 asks
precisely whether each recommendation ties to a stated objective, and returned Indeterminate on all
four groups while retrieval took the blame.

Ids are now assigned by the code (`C1`, `OBJ1`, `EA1`, `NP1`, `REC1`) as each defining pass lands,
and the resulting table is rendered into every later prompt. Of 125 references in the new model,
**124 resolve**.

**The residual is a type error, and it exposed a defect in the fix.** One `linkedObjectiveIds`
array holds `"REC5"` — a recommendation id in an objective slot. The dangling check passed it
because it validated against every id in the model rather than against ids *of the right kind*.
`REC5` is a real id; it is just not an objective. Now corrected, with a test.

### E3 — Quote volume *(fixed)*

Quotes were 52,500 bytes — 20% of the model — and 141 were duplicates. The prompt demanded a
verbatim quote on every assertion without anyone pricing it, and that is what consumed the budget
the recommendations needed. Narrowed to contestable values, once per passage, shortest span that
carries the assertion: **476 quotes → 249, duplicates 141 → 47.**

### E4 — Thin self-report *(partly fixed)*

`extractionReport` runs last and was given the document but nothing about the eleven passes before
it. It now receives a summary of which sections came back populated or empty, and
`internalInconsistencies` rose from 3 to 5.

**Still short.** The report contains at least eight contradictions visible without leaving
category I. The unimplemented half is repeated-key-different-value detection — computing candidate
contradictions and handing them to the pass — which is what would catch the page-5-versus-page-11
charge tables.

### E5 — Fields the process owns *(fixed)*

The model reported `extractedAt: "2025-01-01T00:00:00Z"` and `extractorModel: "claude-opus"` for a
run on 2026-08-13 using Haiku 4.5 — a plausible guess in the two places *"never invent a value"*
could never have been enforced, because the model cannot know either answer. Both are stamped by
the process and removed from the schema slice.

### E6 — Enum drift *(caught, not prevented)*

47 values reached the stored model in enum slots the schema disallows, 42 on `basis`, including
the sentence *"annual growth required at mid-growth rate 2.94% up to age 75"*. Fragments are now
validated before merging; 14 values were dropped in the new run and 1 slipped through.

**11 of the 14 drops are `basis`.** On that evidence the schema's enum is more likely too narrow
for the charge bases this report uses than the model is wrong. Worth reviewing the schema before
adding more validation.

**A reporting defect of my own:** stripped enum values are added to the same list as real failures,
so the run summary reads *"Failed sections (6)"* for six sections that all succeeded. Misleading in
exactly the way the old *"did not return a JSON object"* message was.

---

## 4. Retrieval

All four genuine runs, same case, same plans:

| | `001513` | `095533` | `143309` | `160653` |
| --- | --- | --- | --- | --- |
| Search limit | 8 | 8 | **1** | **16** |
| Category filtering | no | yes | yes | yes |
| Hits | 1,232 | 2,436 | 308 | **4,791** |
| Passages after cap | 650 | 682 | 209 | **684** |
| Groups at the 12-cap | 47 | 56 | 0 | **57** |
| Lowest score admitted | 0.559 | 0.503 | 0.522 | 0.503 |

### R1 — No relevance floor *(open)*

Every search returns exactly its limit regardless of whether anything is relevant. The phrase
*"Nothing was retrieved"* has **never once appeared in any log**, so `CheckPlanRunner`'s zero-hit
branch and every plan's `ifEvidenceAbsent` rule are unreachable. The lowest-scoring passage
admitted scored **0.503**.

One consequence, observed: CHK-009 G9.3 raised a Potential Concern that life cover, waiver
benefits and guaranteed annuity rates are being lost, citing the Zurich policy. That text is an
**unfilled transfer questionnaire** — every checkbox blank — and the same document states at lines
231 and 239 that *"This plan does not have life cover."* Template text was read as evidence, and
the benchmark records the point correctly as no issue.

### R2 — Category B is unreachable for three checks *(open)*

Groups reaching the Fact Find, per check, across every configuration tested:

| Check | top 8 (pre) | top 8 | top 1 | top 16 | of |
| --- | --- | --- | --- | --- | --- |
| CHK-001 | 5 | 5 | 6 | 5 | 9 |
| CHK-005 | 1 | 1 | 1 | 1 | 6 |
| **CHK-007** | **0** | **0** | **0** | **0** | 7 |
| **CHK-008** | **0** | **0** | **0** | **0** | 5 |
| **CHK-009** | **0** | **0** | **0** | **0** | 7 |

**Zero, invariant across a sixteen-fold range of search limit.** The catalogue names Know Your
Client as a primary category for all three. Category B is one 42 KB document holding income,
expenditure, disposable income, residency, tax rate, will and pension inventory — the assertion
side for most of CHK-001 and effectively all of CHK-005.

This is not a tuning problem. The plans for those checks do not reach it and more candidates do
not help.

### R3 — The passage cap binds before the search limit *(open, newly measured)*

Doubling the search limit from 8 to 16 was tested:

| | top 8 | top 16 |
| --- | --- | --- |
| Hits | 2,436 | **4,791** |
| Passages | 682 | **684** |
| Hits discarded | 72% | **86%** |
| Category-group touches | 157 | **150** |

**Twice the retrieval bought two passages.** `MaxPassagesPerGroup` is 12 and every group was
already at it. Worse, **category diversity fell** — a bigger candidate set means the top twelve
come from whichever documents score best, so each group sees *fewer* distinct categories. Every
category dropped except Needs Analysis; B went 16 → 15.

Once groups sit at the cap, the search limit stops being the binding constraint. **The cap is the
untested lever**, not the candidate set.

---

## 5. Assessment

| | `001513` | `095533` | `143309` | `160653` |
| --- | --- | --- | --- | --- |
| Potential Concern | 40 | 38 | 25 | 36 |
| No Issue | 4 | 5 | 3 | 4 |
| **Indeterminate** | 16 | 17 | **32** | 20 |
| Citations | 280 | 291 | 212 | 274 |
| **Unverified quotes** | 95 (34%) | 112 (**38%**) | 65 (31%) | 105 (38%) |
| Groups flagged | 41 | 43 | 36 | 43 |

### A1 — Manufactured reconciliations *(fixed & verified)*

Two instances, both converting a Potential Concern into a No Issue.

**A quotation was altered.** The file note reads *"You are happy to proceed with a Risk rating of
**6**"*; the assessor reported it, inside quotation marks, as **5** — making the evidence agree
with the recommendation and clearing the check. Everything upstream had worked.

**Impossible arithmetic was invented.** G1.1 reasoned that the client was *"age 70 as at fact find
date 22/09/2025, reconciling to 69 at report date 14/10/2025"*. Nobody is 70 in September and 69
in October; the tolerance was applied backwards; the fact find date was wrong.

Both are fixed by reasoning-before-verdict ordering, citation verification and an `Indeterminate`
outcome. G3.6 now states the contradiction plainly in every subsequent run.

> **A correction to earlier analysis.** `dateOfBirth` was blamed on extraction. It is not an
> extraction failure — **the suitability report never states a date of birth**, only *"currently
> aged 69"*. Both extractions correctly omit it. The date of birth is in the Fact Find, which is
> category B — so this is [R2](#r2--category-b-is-unreachable-for-three-checks-open), a retrieval
> gap, and the 33-path list needs re-triaging on that basis: some proportion is the report
> legitimately not saying something.

### A2 — Citation verification flags formatting as fabrication *(open)*

**38% of quotes fail, 43 of 60 groups flagged.** It got worse as retrieval improved, because more
tabular evidence means more quotes carrying table punctuation.

Classifying the failures: roughly 40% differ from the evidence **only in punctuation** — markdown
table rows quoted with their `|` separators — and a fifth are elisions with substantial contiguous
runs present. A share of the rest quote the extraction report, which the prompt now instructs the
assessor to consult but which `EvidenceTextOf` does not include in the haystack.

At two-thirds of groups flagged the signal no longer discriminates. G1.1 shows the damage: it is
forced to Potential Concern, correctly, **but on a reflowed date-of-birth table cell**, while the
real age defect goes unremarked in the same finding.

Two small fixes: fold table punctuation in `Normalise`, and add the extraction report to
`EvidenceTextOf`.

### A3 — Indeterminate fires when one input is absent *(open)*

Declining to answer is the behaviour that was missing, and it works — 16 to 32 groups per run now
say so rather than guessing.

But **CHK-006 returned Indeterminate on all four groups** in `095533` while G6.4's own text
contained the strongest finding in the case: *"ranks the New Solution fifth by maturity value
(£125,000) behind Zurich (£128,000), Existing Solution (£127,000)"*. The reasoning succeeded and
the outcome discarded it, because the assertion side was empty.

Indeterminate should mean *I could not compare*, not *one input was missing*. A group with an
empty assertion side but a finding grounded in retrieved evidence should be able to report it.

### A4 — Derived figures are delegated *(open)*

Sums and percentages a check turns on are asked of the model rather than computed. Two benchmark
findings are missed in every run for this reason: the pension totals (£110,000 and £106,000 stated
against an actual £116,998) and the Standard Life charge computed on Zurich's fund value
(£186.19 = 0.18% of £103,439, not of £3,002).

---

## 6. Recall against the benchmark

Check-level outcome is not a usable metric on this case — every expected outcome is Potential
Concern, so a pipeline that always said so would score full marks. Recorded only for completeness:

| Run | Check outcomes matching expected |
| --- | --- |
| `001513` | 10 / 10 |
| `095533` | 9 / 10 (CHK-006 Indeterminate) |
| `143309` | 8 / 10 (CHK-005, CHK-010 Indeterminate) |
| `160653` | 9 / 10 (CHK-010 Indeterminate) |

**The discriminating measure is the 36 material findings.** Scored by hand against the benchmark:

| Run | Config | Caught | Partial | Missed | Recall |
| --- | --- | --- | --- | --- | --- |
| `095533` | top 8, old model | **20** | 7 | 9 | **56%** |
| `143309` | top 1, new model | 8 | 13 | 15 | 22% |
| `160653` | top 16, new model | 14 | 12 | 10 | 39% |

> **These three are not cleanly comparable.** `095533` used the pre-fix canonical model; the other
> two use the fixed one. Configuration and assertion side changed together, so the recall column
> confounds retrieval with extraction. **No run yet combines the fixed extraction with a healthy
> search limit** — that measurement is the one still missing.

What survives the confound, because it does not depend on the model: the retrieval numbers in
[§4](#4-retrieval), and the fact that **seven of the ten findings missed at top 8 are Fact Find
facts** — the category three checks never reach.

Two findings are caught *better* with the fixed extraction, both assertion-side:

- **F1.8** — *"£116,997.47 versus ~£110,000 versus £110,185 — a variance of between £6,812 and
  £6,997"*. Previously missed entirely.
- **F7.3** — *"a plan cannot simultaneously be both more expensive and less expensive than the
  current plans"*.

---

## 7. What to do next

| | Change | Gap | Effort | Why now |
| --- | --- | --- | --- | --- |
| 1 | Fold table punctuation in `Normalise`; add extraction report to `EvidenceTextOf` | A2 | small | 38% false-positive rate makes the check useless |
| 2 | One genuine run: fixed extraction at top 8 | — | none | The missing measurement blocking everything else |
| 3 | Let an empty assertion side still report an evidence-grounded finding | A3 | small | Recovers the case's strongest finding |
| 4 | Minimum relevance score before a passage enters the pack | R1 | small | 0.503 is being presented as evidence |
| 5 | Per-category floor in the pack, or plan queries that reach category B | R2 | medium | Three checks, zero Fact Find, invariant |
| 6 | Vary `MaxPassagesPerGroup` above 12 | R3 | small | The untested lever; the search limit is not it |
| 7 | Compute sums, percentages and ages in code | A4 | medium | Two findings missed in every run |
| 8 | Review the schema's `basis` enum | E6 | small | 11 of 14 drops are one field |
| 9 | Separate stripped-value warnings from real failures | E6 | trivial | "Failed sections (6)" for six that succeeded |
| 10 | Repeated-value detection for `extractionReport` | E4 | medium | The half that would catch the charge tables |

**Item 2 costs nothing but a run**, and until it exists the recall table above cannot be read.

---

## 8. Corrections this document makes

Recorded rather than quietly overwritten, because each was stated confidently and was wrong.

**Determinism was reported as solved.** It rested on three byte-identical runs, two of which were
cache replays. What is established is prompt-level reproducibility — identical inputs produce
identical evidence packs and prompts, 60/60. Whether the *model* answers the same question the
same way twice is untested. See [§1](#1-what-the-runs-actually-are).

**GAP 2 was reported as fixed and measured.** The run said to verify it began at 00:15:13; the fix
was committed at 01:33:40. That run returned 154 × 8 hits with no exceptions — the signature of
the second query never firing.

**`extractionReport` was reported as not consulted.** It is present in all 60 prompts, with the
exact framing the gap asked for.

**`dateOfBirth` was reported as an extraction failure.** The report never states a date of birth.
Both extractions are correct to omit it; the gap is retrieval.

**Dangling references were reported as validated.** The check compared against every id in the
model rather than ids of the right kind, so `linkedObjectiveIds: ["REC5"]` passed. Now fixed —
found by re-measuring for this document rather than by any test.
