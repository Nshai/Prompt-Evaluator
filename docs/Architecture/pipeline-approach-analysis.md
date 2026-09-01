# Pipeline approach analysis — where to tune for claim coverage

**Revision 4 — final.** 2026-08-29
**Scope:** every measured run of Test Case 1 (19 runs), Test Case 2 (4) and Test Case 3 (3), across
Haiku 4.5, Sonnet 4.6 and Nova 2 Lite.
**Question:** which pipeline stage should be tuned for the best claim coverage, and does new
technology — reranking, hybrid search, better embeddings, a better model — change the answer.

---

## Verdict

**The pipeline decides in advance, by hand, which facts are allowed to be compared with each other
— and it gets that decision wrong often enough to dominate every other source of loss.**

Three measurements carry the argument, each replicated on more than one case:

1. **~20% of the canonical model is read by no check.** 253 of 1,333 populated paths on Test Case 1;
   328 of 1,661 on Test Case 3. Extracted, validated, stored, paid for, never read.
2. **The report's claim and the fact that contradicts it routinely land in different prompts** — or
   in no prompt. Proven on the most severe finding of two separate cases.
3. **Retrieval loses ~1%, eviction loses nothing, ranking loses nothing, and the model makes no
   measurable difference on two of three cases.** The stages everyone tunes are not the ones losing
   findings.

**The fix is to stop authoring that decision and start computing it.** The cheapest version is a
composition change to the existing prompt — no new stage, no new vendor, testable in a day.

---

## 1. The pipeline as measured

Test Case 3, same case, same 585-chunk index, same 10 plans, **byte-identical retrieval packs.**

| Stage | What it does | **Haiku 4.5** | **Sonnet 4.6** | Ratio |
| --- | --- | --: | --: | :-: |
| **S0** Conversion | PDF/DOCX → Markdown | one-off | one-off | — |
| **S1** Extraction | 12 LLM passes → canonical model | **£0.6610**<br>307,349 tok · 303 s | **£2.1570**<br>318,740 tok · 629 s | 3.3× |
| **S2** Indexing | semantic chunking, Titan v2 1536-d → Qdrant | £1.2729 shared (585 chunks) | same index | — |
| **S2a** *The plan* | 10 plans, 88 groups, **196 canonical paths**; each group declares JSON paths (assertion) and queries + categories (evidence), **independently** | design-time | design-time | — |
| **S3** Retrieval | 236 queries, dual targeted/global, dedupe, near-duplicate collapse, pack ≤24 | **£0.0074** | **£0.0074** | 1.0× |
| **S4** Assessment | 88 group calls → structured findings | **£2.3407**<br>1,591,184 tok | **£7.2253**<br>1,711,220 tok | 3.1× |
| **S5** Aggregation | citation verification, cross-group contradictions, report | — | — | — |
| | **Run total (ex. index)** | **£3.00** | **£9.38** | **3.1×** |
| | **Caught, of 83 material findings** | **42%** | **54%** | +12 pp |

- **S4 is 78–84% of every run's tokens** on both models — the only cost lever that matters.
- **S3 is 0.2% of cost and produced identical output on both runs.** Retrieval is neither where the
  money nor the variance is.

Output character differs sharply at the same £/token:

| | Haiku | Sonnet |
| --- | --: | --: |
| Untraceable cited quotes | 99 | **25** |
| `NOT ASSESSED` blocks | 16 | **1** |
| *"no independent / not corroborated"* phrasing | 11 | **93** |
| Derived-figure references | 10 | **36** |
| Extraction internal inconsistencies found | 4 (1 a benchmark finding) | 5 (**3** benchmark findings) |

---

## 2. The recall funnel

| | Findings |
| --- | :-: |
| Material findings in the TC3 benchmark | **83** |
| Survive **S0** conversion | **79** |
| Key evidence reaches *some* assessor pack | **78** |
| Stated by Sonnet | **45** |
| Stated by Haiku | 35 |
| **Stated by at least one model** | **52** |

```
83 ──S0──▶ 79 ──S1/S2/S3──▶ 78 ──────S4──────▶ 45
     −4          −1                   −33
```

Sample of the in-pack misses, by occurrence in the shared extract: `30th March` ×44, `growth
assets` ×18, `incurred regardless` ×17, `diabet` ×16, `enhanced rates` ×14, `transaction costs`
×13, `3.8% or 3.9%` ×10, `xInternal transfer` ×9, `Nil Rate Band` ×9, `48.06` ×8.

---

## 3. What actually happens inside S4

Four hypotheses tested against the artefacts. Three eliminated.

### 3.1 Eliminated — attention dilution

| Finding | Group | Requirement | **Rank** | Haiku | Sonnet |
| --- | --- | --- | :-: | :-: | :-: |
| F6.5 | `G7.7` | Alternatives provide better cost or benefit | **1 of 24** | missed | missed |
| F8.3 | `G8.1` | Material disadvantages, risk factors, limitations | **1 of 24** | missed | missed |
| F6.5 | `G9.4` | Alternatives to the switch considered | **2 of 24** | missed | missed |
| F7.6 | `G7.1` | All material costs disclosed | 7 of 24 | **caught** | missed |
| F6.1 | `G9.10` | Material replacement information omitted | 12 of 24 | missed | missed |

**Two findings sat at rank 1 in a mandated group and were missed by both models.** A reranker
cannot improve on rank 1.

### 3.2 Eliminated — eviction by the 24-passage cap

53 of 88 groups sit at the cap; 621 of 2,476 retrieved passages (25%) are evicted. But in **every**
tested miss, a mandated group kept the passage:

| Finding | Evicted from | Survived in |
| --- | --- | --- |
| F6.1 | `G9.1`, `G6.5` | `G9.4`, `G9.10` |
| F6.5 | `G9.1`, `G9.3`, `G6.5`, `G8.8` | `G6.4`, `G9.4`, `G9.7`, `G7.10` |
| F1.6/F5.5 | `G1.5`, `G5.1` | `G1.4`, `G2.3`, `G5.8` |
| F7.6 | `G7.4` | `G7.1`, `G5.4` |
| F8.3 | `G8.4` | `G8.1` |

**Eviction was never the sole cause of any miss.** Preventing it would have produced zero additional
findings, on either model.

### 3.3 Eliminated — pack size as a recall lever

Test Case 1 Run 14 **raised** the pack from 24 to 27 — a mean of three extra passages, a maximum of
62 in one group, 13% more prompt. Recall fell from **75% to 64%**.

The measured relationship between pack size and recall in this pipeline is **negative**.

### 3.4 Confirmed — the facts that must meet never do

**Instance 1 — TC3 F6.1/F9.1, the most severe finding in the case.** The report claims *"you had no
ties or preference to leave the funds with Quilter"*; the certified Pension Sharing Annex, Section F,
records `[x] xInternal transfer`. The assertion is extracted four times, including to
`/replacementAnalysis[0]/benefitsOfSwitching[1]`.

| Group | `canonicalPaths` selected | Assertion? | Evidence? |
| --- | --- | :-: | :-: |
| `G9.2` Benefits and costs of switching | `benefitsOfSwitching[]`, `costsOfSwitching`, … | **YES** | **NO** |
| `G9.4` Alternatives to the switch | `alternativesConsidered[]`, … | NO | **YES** (rank 14) |
| `G9.10` Material information omitted | `featuresOrGuaranteesLost[]`, `exitPenalties`, … | NO | **YES** (rank 12) |

**No prompt in the run contained both halves.** No model could have made this finding — which is
what three runs and two models show.

**Instance 2 — TC3 F8.3, worse.** Sonnet's extractor did not merely capture the figures; it **wrote
the finding out in English:**

```
/research/comparisons[7]/finding =
  "Recommended funds underperformed against Nest over 5 years: 45.91% vs 48.06%"
```

The evidence sat at **rank 1 in `G8.1`**. The run reports nothing, because **`/research/comparisons[]`
is selected by no group in any of the ten plans.**

**Instance 3 — TC1 F1.6/F5.4, missed for eight consecutive runs.** The report applies a **Residence
Nil Rate Band**, which only exists for a homeowner, to a client the file records as a tenant. Both
halves are in the canonical model:

```
assertion     /taxTreatment/allowancesUsed[1]        "…a Main Residence Nil Rate Band (RNRB) of…"
contradiction /financialPosition/expenditures[0]     "…expenditure covering rent, utilities…"
corroboration `Tenant`, `main residence` — in 13 groups' packs, ZERO evictions, 8 runs running
```

**`/taxTreatment/allowancesUsed` is selected by no group in any plan.** Eight runs of guards were
added to a question the pipeline was never in a position to ask. The Run 18 analysis concluded the
passages *"are not reaching the groups that need them"* — measurement shows they **are**; the
assertion is what is missing.

**Instance 4 — the taxonomy trap.** TC3's *"incurred regardless"* sentence was filed by S1 under
`benefitsOfSwitching` — the extractor **classified an argument that the adviser's fee is
unavoidable as a benefit of switching.** `G9.2` received it pre-labelled that way and both models
accepted it.

### 3.5 The general measurement

Enumerating every populated leaf path in a canonical model and intersecting with the 196 paths the
plans select:

| Case | Populated paths | Read by some plan | **Unread** |
| --- | :-: | :-: | :-: |
| Test Case 1 | 1,333 | 1,080 (81%) | **253 (19%)** |
| Test Case 3 | 1,661 | 1,333 (80%) | **328 (20%)** |

The largest unread roots are the same on both cases: `recommendations[]`, **`taxTreatment`**,
`replacementAnalysis[]`, `clientAuthority`, `ongoingService`, `research`.

**One fifth of everything the extractor produces reaches no prompt.** That is ~£0.43 per case of
wasted extraction on Sonnet — trivial in money, and it contains at least two proven benchmark
findings.

---

## 4. The three-case picture

### 4.1 Test Case 1 — nineteen runs, and the plateau

36 material findings; the case the pipeline was developed against.

| Run | Configuration | C / P / M | Recall |
| :-: | --- | :-: | :-: |
| 1 | before any changes | 16 / 12 / 8 | 44% |
| 3–5 | | 24 / – / – | 67%, 67%, 67% |
| 12 | | 27 / 5 / 4 | **75%** |
| 13 | | 19 / 11 / 6 | 53% |
| 14 | **pack 24 → 27** | 23 / 9 / 4 | 64% |
| 15–16 | **Sonnet** | 25 / 7 / 4 · 25 / 6 / 5 | 69%, 69% |
| 17 | Haiku | 23 / 7 / 6 | 64% |
| 18 | Haiku | 27 / 5 / 4 | **75%** |

**Runs 1→3 added 23 points. The next fifteen runs added 8**, against a noise floor the series
itself measures at **±2 findings / ±5 points.** Three runs scored identically at 24/36; Runs 12 and
18 scored identically with six runs of churn between them.

**Nineteen runs of tuning on one case reach ~75% on that case and 42–54% on an unseen one.**

### 4.2 The model question, re-read against the floor

| Case | Haiku | Sonnet | Δ | Verdict |
| --- | :-: | :-: | :-: | --- |
| TC1 (R18 vs R16) | 27 | 25 | −2 | **within noise** |
| TC2 (R4 vs R3) | 41 | 42 | +1 | **within noise** |
| TC3 (R2 vs R3) | 35 | 45 | **+10** | outside noise |

**On two of three cases the model makes no measurable difference.** Nova 2 Lite was also tried
(TC1 Runs 9–10) and is not competitive. The £9.38 Sonnet configuration is measurably better on
**one case in three.**

### 4.3 The models fail in opposite directions

Testing each divergence against **both** canonical models separates S1 from S4:

| Finding | Token | Haiku's model | Sonnet's model | Caught by | Attribution |
| --- | --- | :-: | :-: | :-: | --- |
| F1.7 six-fund sum | `10,764.88` | **NO** | yes | Sonnet | **S1** — Haiku never itemised the holdings |
| F1.2 inheritance timing | `two years` | **NO** | yes | Sonnet | **S1** |
| F8.3 Nest performance | `48.06` | **NO** | yes | *neither* | **S1 + S2a** |
| F7.3 two Aviva charges | `0.19` / `0.21` | yes | yes | Sonnet | **S4** |
| F3.2/F5.2/F8.2 depletion | both halves | **yes** | **yes** | **Haiku** | **S4** |
| F7.6 cumulative charges | evidence-side, rank 7 both | — | — | **Haiku** | **S4** |

- **Sonnet's advantage is at S1** — richer extraction, computed once, feeding all 88 groups.
- **Haiku's advantage is at S4**, specifically at joining two statements already in front of it.
  Sonnet wrote the depletion contradiction as two adjacent bullets and never connected them.

### 4.4 The ceiling, measured twice

| Case | Haiku | Sonnet | **Union** | Caught by exactly one | **Caught by neither** |
| --- | :-: | :-: | :-: | :-: | :-: |
| TC1 (n = 34) | 79% | 71% | **85%** | 7 (21%) | **5 (15%)** |
| TC3 (n = 83) | 42% | 54% | **63%** | 24 (29%) | **21 (25%)** |

The union beats the best single model by ~10 points on both cases. **The 15–25% residue is the
architecture's ceiling** — and §3.4 explains most of it on both cases.

### 4.5 Cost and savings

**The lever.** 1,855 passage-sends at ~500–600 tokens is **0.93–1.11 M tokens, 54–65% of Sonnet's
1,711,220 chat tokens.** Halving the pack removes ~30% of the dominant cost. Measured blended rates:
Sonnet £4.22/M and Haiku £1.47/M at S4; Sonnet £6.77/M and Haiku £2.15/M at S1.

| # | Configuration | S1 | S4 | Doc-first | **£/run** | Findings | £/finding |
| :-: | --- | --: | --: | --: | --: | :-: | --: |
| **0** | **Sonnet / Sonnet — today** | 2.16 | 7.23 | — | **9.38** | **45** | £0.208 |
| 0b | Haiku / Haiku — today | 0.66 | 2.34 | — | **3.00** | **35** | £0.086 |
| 0c | Both models, unioned | 2.82 | 9.57 | — | **12.38** | **52** | £0.238 |
| **1** | Sonnet S1 / Haiku S4, pack 24→12 | 2.16 | 1.64 | — | **3.81** | 38–45 | £0.092 |
| **2** | + plan and composition fixes (runtime ≈ flat) | 2.16 | 1.72 | — | **3.89** | 45–55 | £0.078 |
| **3** | + document-first, on Haiku | 2.16 | 1.72 | 0.80 | **4.68** | 55–63 | £0.079 |

**£4.68 against £9.38 — half the cost for roughly a third more findings; £0.079 against £0.208 per
finding, 62% lower.** At 1,000 cases a year, ≈ **£4,700 saved and ≈ 600 more findings surfaced.**

Rows 0/0b/0c are measured end to end. **Rows 1–3's finding counts are estimates**, and row 1's is
load-bearing: whether Haiku's assessment on Sonnet's canonical model holds near 45 is untested,
costs **£4.50 and one afternoon**, and the savings case collapses to row 0b if it fails.

---

## 5. The technology questions, answered

### Would a reranker improve searching? Not for recall.

§3.1 and §3.2 refute the case: two decisive passages sat at **rank 1** and were missed **by both
models**, and eviction was never the sole cause of any miss. A reranker would have produced **zero**
additional findings.

It is still worth doing, for reasons that are not coverage:

1. **Cost.** S4 is 78–84% of run cost. If 12 well-chosen passages match 24 mediocre ones, the
   dominant cost roughly halves — and §3.3 says a smaller pack may score *better*.
2. **It retires a hand-rolled reranker.** `CheckPlanRunner.Rank()` is four stacked reservation
   systems — per targeted category, per declared section, top score, form-skeleton demotion — each
   added to fix a measured failure, each a metadata proxy for a trustworthy relevance score.
3. **It is a prerequisite** for retrieving 60–80 and keeping 24.

| Option | Notes |
| --- | --- |
| **Voyage `rerank-2.5-lite`** | Fastest to integrate; ~150–300 ms for 50 candidates; cost negligible |
| **Cohere `Rerank 3.5`** | Comparable; good tabular handling |
| **`BGE-reranker-v2-m3` / `Qwen3-Reranker` self-hosted** | **The production answer** — zero marginal cost, ~50 ms on a small GPU, client PII never leaves the tenant |

**Prototype with Voyage; land on self-hosted; justify on cost and code simplification. Sequence it
fifth.**

### Hybrid search? Yes — as a routing fix, not a recall fix.

Many findings turn on an exact token: `48.06`, `33.4%`, `£372,000.00`, `£9,839.36`, `17.68` vs
`13.21`. Dense-only retrieval on a general 1536-d embedding is weakest there. But §2 shows those
passages *are* found — what fails is that they reach the wrong group. `33.4` reached exactly one
pack, `G1.1`, and no CHK-007 or CHK-009 query ever retrieves it. **A sparse query for a literal
figure, issued from the group that cares, is a routing fix.** Qdrant supports sparse vectors and
native RRF/DBSF fusion in one query: a schema change, no new vendor, fully deterministic.

### A better embedding model? Marginal.

Titan v2 at 1536-d is dated, and `voyage-context-3` (contextualised chunk embeddings) would retire
the `TableContext()` workaround. But retrieval recall headroom is ~1 finding.

### A better chat model? Not a lever.

No measurable effect on two of three cases (§4.2), and Haiku beats Sonnet on Test Case 1.

---

## 6. The final approach

### 6.1 The principle

> **Every judgement moved into code has held. Every judgement moved into a prompt clause has been
> ignored by at least one model.**

| Moved into code | Result |
| --- | --- |
| Derived figures | Holds; produced Sonnet's sharpest catches |
| Near-duplicate collapse | Holds; deterministic |
| Citation verification | Holds; untraceable quotes 132 → 99 → 25 |
| Top-score slot reservation | Holds; measurable |

| Moved into a prompt clause | Result |
| --- | --- |
| **Comparison-basis clause** | **Violated by both models on identical packs**, both quoting both dates in the same paragraph |
| **Three guards + a hint on TC1 F1.6** | **None fired, across eight runs** |
| Absence-discipline clause | Partly effective; produced its own inverse failure in Haiku's `G4.1` |

**R4 extends the principle one level up.** *Which facts appear together in a prompt* is itself a
judgement — currently authored by hand in JSON, wrong for ~20% of the model, and **invariant to
model choice**. It should be computed.

### 6.2 Target architecture

```
S1  EXTRACTION            unchanged — but Sonnet, and cached
      │
      ├─▶ every populated path is guaranteed a reader (build-time assertion)
      │
S2a   PLAN                                 ┌─────────────────────────────┐
      │  group's own paths ────────────────▶│                             │
      │  ASSERTION DIGEST (all paths, 1 ln)▶│   S4  one prompt per group  │
S3    RETRIEVAL  pack ≤12, reranked ───────▶│                             │
      │                                     └─────────────────────────────┘
      │
S1b DOCUMENT CLAIMS  (B/C/D/F only, cached by hash)
      │
      └─▶ RECONCILE in code on canonicalPath ─▶ ADJUDICATE per candidate
```

Two changes carry it: **the assessor always sees the whole assertion side in compressed form**, and
**the evidence side is reconciled to it in code rather than by a plan author's guess.**

### 6.3 The moves, in order

**Move 1 — Guarantee every path a reader.** *(free, a test)* Assert at build time that every
populated canonical path is selected by at least one group. This alone makes TC1's F1.6 and TC3's
F8.3 reachable, and it is a failing test today on ~20% of the model.

**Move 2 — Add a collidability lint.** *(free, a test)* For every group, assert that its
`canonicalPaths` and its `evidenceCategories` / `targetCategories` describe two sides of one
comparison. `G9.10` declares evidence category `B` — where certified court orders live — while
selecting assertion paths about exit penalties. Nothing flags that.

**Move 3 — Hold prompt size constant and change its composition.** *(one config change)*
**Pack 24 → 12, and add an assertion digest**: every populated canonical path as one line —
path, value, quote pointer — roughly 300–400 lines, 8–10 k tokens. Net prompt size falls.

This is the load-bearing experiment and it directly tests §3.4. **It also carries the document's
largest single risk:** TC1 Run 14 is the only measured experiment on adding context here, and
adding 13% more prompt cost 11 points. The mitigation is the design — the digest is dense,
structured and short, it *replaces* rather than adds volume, and the pack shrinks by more than the
digest grows. If recall falls anyway, §3.4 is wrong and Moves 4–6 should not be built.

**Move 4 — Model split.** *(£4.50, one afternoon)* Haiku assessment against Sonnet's stored
canonical model. Potentially 54% coverage at half the cost.

**Move 5 — Routing repair and the code-level join.** For each finding whose evidence reached only
unmandated groups (TC3 F2.5/F9.6, F3.7, F8.6, F9.9, F1.15; TC1 F2.2), add the missing query,
category or path. Then match pack passages against **all** canonical assertions and inject strong
matches under *"assertions elsewhere in the report this evidence may bear on"* — the
`DerivedFigures` / `CrossGroupContradictions` pattern, all of which hold.

**Move 6 — Code-side comparisons.** Carry `asAtDate` on canonical values and passages and compute
date-comparability in code; extend `DerivedFigures` to sign errors (TC1 F7.3 is *"4.24% adds
instead of subtracts"* — arithmetic no model has ever caught); promote extraction self-consistency
to a first-class pass; cap routed-inconsistency fan-out (one item reached 26 of 88 groups, 92
times); add an S5 cross-group consistency check (`G7.3` says the 0.19% charge *is* confirmed while
`G7.5` says it is not).

**Move 7 — Hybrid retrieval and structure-aware chunking.** Sparse + dense RRF fusion in Qdrant;
never split a table; each table its own chunk with its caption. Fix the DOCX table loss at S0 — a
hard floor of 4 findings nothing downstream can recover.

**Move 8 — Reranking.** Retrieve 60–80, cross-encoder rerank to 12. Measured against cost and
`Rank()` complexity, not coverage.

**Move 9 — Document-first traversal**, for the residue Moves 1–8 cannot reach: findings where no
requirement asks the question at all.

### 6.4 How document-first claim extraction works

**Schema-constrained LLM inference. Semantic search has no role in it.**

Retrieval selects existing text by similarity; extraction transforms text into typed assertions. No
vector search can emit `{ path, value, asAtDate }`. The run proves it: `xInternal transfer` was
retrieved nine times and quoted verbatim in Sonnet's output. Retrieval succeeded completely; nothing
converted a ticked box into `transferRoutePreference = Internal`. And a search-driven version would
need a query authored per claim type — precisely the guessing that §3.4 identifies as the root cause.

| Step | Mechanism | Deterministic |
| --- | --- | :-: |
| Selection — which documents | code, by category | ✔ |
| Segmentation | code, structure-aware | ✔ |
| **Inference — emit typed claims** | **LLM, schema-constrained, one pass per document** | ✘ |
| Grounding — verify quotes | code, `CitationVerifier` | ✔ |

**The extractor emits the canonical JSON path directly**, chosen from a category-sliced schema —
`JsonSchemaSlicer` already does this for the report's twelve passes. Reconciliation is then path
equality plus value comparison, **with no hand-built ontology**, which is what would otherwise make
it as brittle as `expectSignals`.

**Scope and cost.** The unreachable findings cluster in categories **B, C, D, F** — **11 documents
of 37, 91 of 585 chunks, ~79 k tokens.** The bulky categories are the excluded ones (E, G, H are 25
documents and 446 chunks; the Aviva T&C alone is 75), so **cost is sublinear in document count.**

| Component | Scoped (11 docs) | All 37 |
| --- | --: | --: |
| Claim extraction, in + out | £0.76 | £4.05 |
| **Adjudication** — ~50 candidates × ~3 k tok | £1.75 | £2.50 |
| **Total, Sonnet** | **≈ £2.5** | ≈ £6.6 |
| **Total, Haiku** | **≈ £0.8** | ≈ £2.1 |

**It replaces reads rather than adding them.** The corpus is 509 k tokens; a run consumes 1.71 M —
**3.4 corpora** — because 397 distinct chunks occupy 1,855 pack slots, so **the average retrieved
chunk is sent to 4.7 different prompts.** Cache by document hash and the cost is paid once per case,
not once per run.

### 6.5 What to stop doing

- **Stop adding guards and clauses.** F1.6 absorbed three guards and a hint across eight runs; none
  fired. The comparison-basis clause is violated by both models while quoting both dates.
- **Stop tuning on Test Case 1.** It plateaued fifteen runs ago and the remaining movement is noise.
- **Stop treating model choice as the lever.** No measurable effect on two of three cases.
- **Stop growing packs.** The measured relationship is negative.

---

## 7. Correctness defects, independent of tuning

**Groups are assessed and silently dropped.** Six across three TC3 runs — `G1.2`, then `G6.6` and
`G8.3`, then `G3.6`, `G5.6`, `G6.7`. Each has a full pack and no line anywhere in the output.
`G8.3` is *Tax consequences of the recommendation*. Non-deterministic, model-independent. **Fix
first — it silently corrupts every measurement taken over it.**

**`No Issue` verdicts print the discrepancy that justifies a concern.** Haiku's `G7.2` returned No
Issue while printing *"Report states £9,838.36; Client Agreement states £9,839.36"* — benchmark
F7.9, stated exactly, under a heading saying there is nothing there.

**Groups of one check contradict each other**, with nothing at S5 noticing (§6.3, Move 6).

**Prompt adherence is weak and model-dependent** — 59 of 88 responses echoed the wrong requirement
id on TC1 Run 18; 64 of 88 on TC3.

**No noise floor for most configurations.** TC1 gives ±2 findings / ±5 points; nothing comparable
exists for TC2 or TC3, and no configuration has been run twice on either.

---

## 8. Sequence and gates

| Phase | Work | Cost | Gate |
| --- | --- | --- | --- |
| **0** | Fix the silent group drop; automate benchmark scoring; two runs per configuration; complete the 2×2 with TC2 on Sonnet | ~1 wk | Two runs agree within the floor |
| **1** | **Moves 1–4** — path-coverage test, collidability lint, pack 12 + assertion digest, model split | ~1 wk, £4.50 | **Does recall rise when the assertion slice widens at constant prompt size?** This decides everything after it |
| **2** | **Move 5** — routing repair and the code-level join | 2–3 wk | **TC3 F6.1 and F8.3, and TC1 F1.6, produce findings.** Every input is already present |
| **3** | **Move 6** — `asAtDate`, sign errors, self-consistency, fan-out cap, S5 cross-check | ~2 wk | +6 to 10 on TC3, no regression on TC1/TC2 |
| **4** | **Move 7** — hybrid retrieval, table-aware chunking, S0 table loss | ~2 wk | Identifier-bearing passages rank higher in the groups that want them |
| **5** | **Move 8** — reranking | ~1 wk | Cost falls; `Rank()` gets smaller |
| **6** | **Move 9** — document-first traversal | 4–6 wk | The UNMATCHED bucket surfaces findings no group owns |

### Deliberately not recommended

**Agentic retrieval.** It would help, and it destroys reproducibility — a first-class requirement in
a regulated QA tool where a finding must be defensible and repeatable. **The reproducible version is
worth doing instead:** the assessor already emits `missingInputs` and `NOT ASSESSED` naming what it
could not obtain (16 blocks on Haiku, 1 on Sonnet). Turn those into a **deterministic second
retrieval hop** — pass 1's declared gaps become pass 2's queries by a pure function.

**Running both models permanently.** The union is 63% (TC3) and 85% (TC1), and it is a legitimate
short-term option if coverage matters more than cost today. It is not an architecture: it buys ~10
points by paying twice for a defect that costs almost nothing to fix in the plan.

---

## 9. Summary

| Question | Answer |
| --- | --- |
| Which stage limits claim coverage? | **The plan (S2a), jointly with assessment (S4).** ~20% of the canonical model is read by no check; the claim and its contradiction routinely land in different prompts. Retrieval loses ~1% and is 0.2% of cost. |
| What is the ceiling of the current design? | **Measured twice.** TC3: 54% best single model, **63% union**, 25% unreachable. TC1 after 19 runs: 79%, **85% union**, 15% unreachable. |
| Does more tuning help? | **No.** TC1: runs 1→3 added 23 points; the next fifteen added 8, against a ±5-point floor. |
| Haiku or Sonnet? | **On two of three cases it does not matter.** Sonnet's advantage is at S1 (16% of cost, computed once); Haiku's is at S4 (84%), and Haiku *beat* Sonnet on TC1. **Test Sonnet-S1 / Haiku-S4 at £4.50 first.** |
| Would a reranker improve searching? | **Not for recall** — two decisive passages sat at rank 1 and were missed by both models. Worth doing for **cost** (S4 is 78–84% of spend) and to retire the hand-rolled reservation stack. Fifth. |
| Cheaper retrieval win? | **Hybrid dense+sparse fusion in Qdrant, as a routing fix.** |
| Highest-value change? | **Guarantee every canonical path a reader, and give every group the whole assertion side in compressed form at constant prompt size.** Cheapest work in the system, largest measured bucket, **model-independent**. |
| Strategic answer? | **Document-first traversal**, claims emitting canonical paths so reconciliation is path equality in code. |
| First action? | **Fix the silent group drop, automate scoring, then run Phase 1.** |

---

### Appendix A — evidence base

| Run | Model | All material | Caught |
| --- | --- | :-: | :-: |
| TC1 Run 1 | Haiku | 16 / 12 / 8 of 36 | 44% |
| TC1 Runs 3–5 | Haiku | 24 of 36 | 67% |
| TC1 Run 12 | Haiku | 27 / 5 / 4 of 36 | 75% |
| TC1 Run 14 | Haiku (pack 27) | 23 / 9 / 4 of 36 | 64% |
| TC1 Run 16 | Sonnet | 25 / 6 / 5 of 36 | 69% |
| TC1 Run 18 | Haiku | 27 / 5 / 4 of 36 | **75%** |
| **TC1 R16 ∪ R18** | both | **29 / 3 / 2 of 34** | **85%** |
| TC2 Run 2 | Haiku (pre-changes) | — | 36% |
| TC2 Run 3 | Sonnet | 42 / 12 / 21 of 75 | 56% |
| TC2 Run 4 | Haiku | 41 / 10 / 24 of 75 | 55% |
| TC3 Run 1 | Haiku | 26 / 27 / 30 of 83 | 31% |
| TC3 Run 2 | Haiku | 35 / 17 / 31 of 83 | 42% |
| TC3 Run 3 | Sonnet | 45 / 10 / 28 of 83 | 54% |
| **TC3 R2 ∪ R3** | both | **52 / 10 / 21 of 83** | **63%** |

[TC1 Run 18](../Test-Runs/Test-Case%201/Runtime-Logs/latest/Run-18/run-analysis.md) ·
[TC2 Run 3](../Test-Runs/Test-Case%202/Run-3/run-analysis.md) ·
[TC2 Run 4](../Test-Runs/Test-Case%202/Run-4/run-analysis.md) ·
[TC3 Run 1](../Test-Runs/Test-case%203/Run-1/run-analysis.md) ·
[TC3 Run 2](../Test-Runs/Test-case%203/Run-2/run-analysis.md) ·
[TC3 Run 3](../Test-Runs/Test-case%203/Run-3/run-analysis.md)

### Appendix B — how the measurements were taken, so they can be re-run

1. **Path coverage (§3.5).** Enumerate every populated leaf path in a canonical model, normalise
   `[n]` → `[]`, and intersect with every `"/…"` string in the ten query plans, treating a selected
   parent as covering its children.
2. **Assertion/evidence split (§3.4).** Walk the canonical model for the assertion text and record
   its paths; read the group's `canonicalPaths`, `evidenceCategories` and `targetCategories`; parse
   the retrieval extract mapping each passage to its owning group, its rank and its
   in-pack/`[evicted]` state; intersect.
3. **S1 vs S4 attribution (§4.3).** Run step 2 against **both** canonical models and cross-tabulate
   with the two runs' verdicts. Present in one model only → S1. Present in both → S4. Present in
   neither and evidence-side only → S4.
4. **Union and residue (§4.4).** Take the per-finding verdict tables of two runs on identical packs
   and compute the element-wise maximum.

### Appendix C — confidence

- **High** — §1's costs, §2's funnel, §3.1's ranks, §3.2's eviction traces, §3.3's Run 14 result,
  §3.4's path intersections, §3.5's coverage measurement, §4.1's trajectory and floor, §4.2, §4.3,
  §4.4. All direct measurements, re-runnable by Appendix B.
- **Medium-high** — attributing the 15–25% residue across the defect classes. Four instances are
  traced end to end; the rest is inference, and Move 3 is designed to test it cheaply.
- **Opinion, with a stated risk** — that widening the assertion slice raises recall. TC1 Run 14 is a
  measured counter-example for *adding* context; the design mitigates it by holding prompt size
  constant, and Phase 1 exists to settle it before anything is built.
- **Estimates, not results** — rows 1–3 of §4.5, and all effort figures in §8.
- **Unvalidated** — no configuration has been run twice on TC2 or TC3.
