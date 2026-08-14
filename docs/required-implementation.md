# Required implementation — a rewrite brief

What a second implementation of this pipeline must do differently, derived from five measured runs
and eight stages of remediation on the first one.

Everything asserted here was measured. Where a number appears it comes from a named run, and where
a design decision is argued from a defect the defect is named. The companion documents are
[expected-results-benchmark.md](test-results/expected-results-benchmark.md) (the answer key),
[scoring-rubric.md](test-results/scoring-rubric.md) (how a run is adjudicated),
[remediation-plan.md](test-results/remediation-plan.md) (Stages 0–8, what was built and what was
not), and the five run analyses under
[test-results/Runtime-Logs/latest](test-results/Runtime-Logs/latest).

---

## 0. The recommendation, stated plainly

**A full rewrite is not warranted. A re-architecture of two layers is.**

Five runs of evidence say the assessment reasoning, the retrieval engine, the check catalogue and
the per-requirement prompt structure all work. What does not work is the shape of the data between
them, and it fails in ways that no amount of further patching reaches — three separate stages of
machinery each shipped, each worked, and each moved zero findings.

| Layer | Verdict | Why |
| --- | --- | --- |
| Document conversion and chunking | **Keep** | 28 documents, 469 chunks, no measured loss attributable to it |
| Vector search and ranking | **Keep** | Byte-stable across four runs; retrieval stopped being the bottleneck at Run 3 |
| Check catalogue and per-requirement assessment | **Keep** | The assessor finds contradictions unaided when handed the right page |
| **The canonical model's shape** | **Rewrite** | One scalar per fact silently destroys the contradictions that *are* the findings |
| **Routing — which evidence reaches which requirement** | **Rewrite** | The largest single blocker, and only partly addressed |
| Outcome and rendering | **Repair** | A group can currently delete its own finding; Stage 8 fixed the symptom |
| **Measurement** | **Rewrite as infrastructure** | Recall is adjudicated by hand and is not reproducible to better than ±2 |

The rest of this document is the requirements for those four, and the learnings that must survive
into any implementation that replaces this one.

---

## 1. Read this first: what five runs settled

### 1.1 The plateau, and what it proves

| | Run 1 | Run 2 | Run 3 | Run 4 | Run 5 |
| --- | --- | --- | --- | --- | --- |
| **Caught (of 36 material findings)** | 16 (44%) | 24 | 24 | 24 | **24 (67%)** |
| Partial | 12 | 6 | 7 | 4 | **10** |
| **Missed** | 8 | 6 | 5 | 8 | **2** |
| Extraction sections succeeding | — | — | 4 of 12 | 12 of 12 | 12 of 12 |
| Checks reaching the Fact Find | 5/10 | 7/10 | 10/10 | 10/10 | 10/10 |
| Unverified quote rate | 36% | 32% | 14% | 11% | 18% → **13%** (Run 6, measured) |

**Recall moved once, in Run 2, and never again.** Runs 2, 3 and 4 caught an *identical* set of 24
findings. Between them the pipeline gained a working extraction (4/12 → 12/12 sections), full Fact
Find coverage (5/10 → 10/10 checks), a citation matcher that went from rejecting a third of quotes
to a ninth, a passage-pack floor, derived-figure arithmetic, and a cross-group contradiction pass.

**None of it reached a single additional finding.**

The honest reading, and the one a rewrite must start from: **the machinery was never the binding
constraint after Run 2.** Everything from Stage 3 onward improved a measure that was not recall,
and improving it did not help.

### 1.2 What was actually blocking

Run 5 broke the plateau — five findings in, five out, headline unchanged — and in doing so
exposed the real structure. Findings are lost at **five distinct places**, and the first
implementation only ever addressed two of them:

| # | Where a finding is lost | Evidence | Addressed? |
| --- | --- | --- | --- |
| 1 | **Retrieval** — the passage never enters the pack | Run 2: three checks reached category B in 0 of 19 groups | ✅ Stages 1, 5 |
| 2 | **Routing** — the right page reaches a group with no question it answers | Run 4: the −£288 deficit reached four packs and **nobody mentioned it** | ⚠️ partly, Stage 7 |
| 3 | **Extraction reconciliation** — the model resolves the contradiction that *is* the finding | Run 5: charge stated 0.52% on p5 and 0.18% on p11; extraction stores one value | ❌ **never** |
| 4 | **Outcome suppression** — a group finds it, then answers NoIssue | Run 5: G3.6 named the risk-rating contradiction and passed | ⚠️ symptom fixed, Stage 8 |
| 5 | **Cross-check composition** — the contradiction spans two checks, so neither sees it | F1.2, F5.2: halves in different checks, never joined | ⚠️ partly, Stage 4.2 |

**Losses 3 and 5 are architectural.** No prompt change, retrieval tweak or post-processing pass
reaches them, because the information is already gone by the time anything downstream runs. That
is the case for re-architecting rather than continuing to patch.

### 1.3 The measurement lesson, which outranks the rest

**Eight of the first twelve runs were cache replays reported as generated runs.** The pipeline was
being tuned against output that had not been produced by the code under test. This was invisible
until a wall-clock check was added: a genuine run generates at **322–647 tokens/second**; a replay
reports **20,000–49,000**.

**Two adjudications of the same 60 responses produced 39% and 44%.** The benchmark's definitions
left five points of room, so no comparison between runs was trustworthy below ±2 findings.

**Five runs, and not one moved a single variable.** Each moved two or three — code, plans and
canonical model together — so no recall change in the entire series is attributable by
measurement to the change that caused it.

> **The single most important requirement in this document.** A new implementation must make an
> unreliable measurement *impossible*, not merely detectable. Everything in §6 follows from the
> fact that this project spent five runs unable to tell whether its own changes worked.

---

## 2. The canonical model must record observations, not values

**This is the highest-value change in the document.** It closes five open findings, three unbuilt
remediation items, and an entire class of defect.

### 2.1 The defect

The report says Standard Life's existing charge is **0.52%** on page 5 and **0.18%** on page 11.
It says Zurich's is **0.18%** on page 5 and **0.93%** on page 11. The extraction stores one figure
per arrangement — page 5's rate paired with page 11's monetary amount.

Two benchmark findings die there:

- **F7.1** *is* the disagreement between those two tables. It cannot be reported from a model that
  holds one reconciled value.
- **F7.2** depends on the page-11 pairing surviving intact — £186.19 is 0.18% of £103,439
  (Zurich's fund), not of Standard Life's £3,002 — and it does not survive.

Three more (**F6.3, F9.3, F9.4**) regressed in Run 5 for the same reason: the run holds both
figures and compares them against a *provider document* rather than against the report's other
page, because the report's other page is no longer in the model.

**The extractor is already instructed to record contradictions rather than resolve them.** This is
a compliance failure against its own prompt — and one that cannot be fixed by strengthening the
instruction, because the schema has nowhere to put the second value.

### 2.2 The requirement

> **Every extracted fact is an observation, not a value. A concept holds a list of observations,
> never a scalar.**

```jsonc
// Wrong — the shape that loses findings
"existingAnnualChargePercentage": { "value": 0.52, "basis": "of fund value pa" }

// Required
"existingAnnualCharge": [
  { "value": 0.52, "basis": "of fund value pa", "statedAt": { "page": 5,  "section": "Existing Arrangements" },
    "verbatim": "Standard Life | 0.52% | £186.19" },
  { "value": 0.18, "basis": "of fund value pa", "statedAt": { "page": 11, "section": "Pension Switch Charges" },
    "verbatim": "Standard Life | 0.18% | £186.19" }
]
```

**Consequences that must be designed in, not bolted on:**

1. **Disagreement is computed, not judged.** A concept whose observations disagree beyond a stated
   tolerance is a *derived finding* produced in code, with both sides and both locations. F7.1
   stops depending on a model noticing anything.
2. **Nothing reconciles at extraction time.** The extractor's job is to record what the document
   says, everywhere it says it. Reconciliation is a later, visible, reviewable step.
3. **`verbatim` is mandatory on every observation.** This subsumes unbuilt item 4.3 — carrying the
   report's own words beside any normalised value — and it is what makes citation verification a
   lookup rather than a search.
4. **`statedAt` is mandatory.** A finding a reviewer cannot locate in the source document is not
   actionable, and half the benchmark findings are "page 5 disagrees with page 11".

### 2.3 No enums on extracted values

**Measured cost of getting this wrong: 8 of 12 extraction sections failing.** The schema's enum on
a charge line's `basis` rejected every value the model produced, and the whole section was
discarded. Removing the enums took extraction from 4 of 12 sections succeeding to 12 of 12 — the
single cleanest fix in the project.

> **Requirement.** Extracted values are free text. Normalisation to a controlled vocabulary is a
> separate, later, *lossy-by-declaration* step that keeps the original alongside. A schema that
> can reject a document's own wording will, and the loss is silent.

### 2.4 What this makes possible

| Closes | Currently |
| --- | --- |
| F7.1, F7.2, F6.3, F9.3, F9.4 | 5 partials in Run 5 |
| Remediation 7.6 | unbuilt, blocked |
| Remediation 4.3 (verbatim beside enum) | unbuilt, blocked |
| Remediation 4.4 extraction half (repeated-key detection) | unbuilt, blocked |
| `DerivedFigures` reading three different shapes for one fact | three attempts, three misses |

That last row deserves emphasis. `DerivedFigures` was written to find F7.2, then extended
(Stage 5.3), then unblocked by the schema fix (Stage 6.4), then extended again (Stage 7.2) — and
missed the figure **three times**, because the extraction wrote it in a different shape each time.
**A component that must guess the shape of its input will keep missing.**

---

## 3. Routing must be a declared contract, not an emergent property

### 3.1 The defect

Run 4 measured it exactly. The Fact Find's income section — carrying `Total Net Monthly Income
£1,430.00`, `Total Monthly Expenditure £1,718.00`, `Total Monthly Disposable Income £-288.00` —
**reached the evidence pack of four groups.** Not one mentioned it. `288` and `Disposable` appear
zero times in that run's output.

The four groups assessed personal details, tax status, employment and the emergency fund. **None
had a question the figure answers.** The group that did — income and expenditure — did not receive
it.

**Retrieval had succeeded and the finding was lost anyway.** A plan could say "this group needs the
Fact Find" and could not say "this group needs the Fact Find's *income* section", so a 42 KB form
was one indivisible target and the per-category floor spent its slot on whichever chunk embedded
best for the group's wording.

### 3.2 What Stage 7 proved, and what it did not

Section hints were added. **Three of six worked and three did nothing**, and the difference is the
whole lesson:

| Hint | Result |
| --- | --- |
| `Total Monthly Disposable Income`, `Current Monthly Cash Flow`, `Total Net Monthly Income` | **4 findings** missed → caught; the figure went from 0 mentions to **39** |
| `Residency`, `Contact Address` | **0 findings**; the words appear nowhere in the run's output |

Section targeting works where the section is a **labelled table** and fails where the fact is
**prose**. F1.6 and F5.4 — the residency findings — are the only two outright misses left in the
whole benchmark.

And for a full run nobody could tell *which* failure had occurred: "the hint matched and the group
ignored the section" is a reasoning problem; "the hint never matched anything" is a typo. Stage 8
added the instrumentation. **A new implementation must not need it, because the condition must be
impossible.**

### 3.3 The requirements

1. **Sections are first-class at index time.** Every chunk carries its heading path
   (`document → section → subsection`), extracted during conversion, not inferred later by
   substring search over the passage text. The current implementation matches hints against
   passage *text*, which is why a heading that is not repeated inside the body never matches.

2. **A group declares what it needs, and the declaration is validated against the corpus.**
   Not "category B" but "the fact at `/financialPosition/monthlyDisposableIncome`, stated in
   `Fact Find → Current Monthly Cash Flow`". **A declaration naming a section no indexed document
   contains must fail at build time**, not silently do nothing for a run.

3. **An unmet declaration is a reportable outcome.** If a group declared a fact it did not receive,
   that is a retrieval failure, and it must appear as one. Currently it is indistinguishable from
   the fact being absent from the case — which for most checks *is the finding*, so the two are
   being conflated in the direction that hides defects.

4. **Prose facts need a different mechanism, and it must be designed rather than discovered.**
   Residency status appears as a sentence, not a labelled row. Options: a fact-level index (assert
   `/client/residencyStatus` and index the passage that supports it), or entity-level retrieval.
   **Do not ship section targeting alone and assume it generalises — it measurably does not.**

---

## 4. A verdict must never be able to delete the working

### 4.1 The defect

CHK-003/G3.6 wrote, in its own `discrepancies` array:

> "P11 file notes state 'Risk rating of 6' … **contradicting all other sources** which state the
> final agreed rating is 5."

That is benchmark finding **F3.1**, correctly stated, both sides named. The group then returned
`NoIssue`. The renderer prints only groups that raised a concern, so the group vanished and the
string "Risk rating of 6" appears nowhere in the run's output.

Retrieval was **identical** to the previous run, where the same group found the same thing and
answered Potential Concern. Nothing was lost from the evidence, the routing, or the reasoning.

**The scale, once counted:**

| | Run 2 | Run 3 | Run 4 | Run 5 |
| --- | --- | --- | --- | --- |
| Groups passing while naming discrepancies | 12 | 15 | 13 | 9 of 60 |
| Discrepancies discarded | 44 | 51 | 36 | 27 |

**Every run in the series discarded between 27 and 51 named differences, and four analyses failed
to count them.** The risk-rating contradiction was found and dropped repeatedly across four runs
and scored as "missed" each time.

### 4.2 The requirement

> **Findings are emitted independently of the verdict.** A group produces (a) observations it
> compared, (b) differences it found, and (c) a judgement about them. The report renders (b)
> regardless of (c).

**And the corollary, which cost a design iteration to learn:** do *not* respond by forcing any
group naming a difference to Potential Concern. 27 discards in one run means most are immaterial —
a group is entitled to notice a £0.02 variance and wave it through. That rule would flood the
report and destroy the check-level signal. **Surface, do not promote.**

### 4.3 The general principle

This is the third instance of one pattern, and a new implementation should treat it as a law:

| Where | What was discarded |
| --- | --- |
| `comparisonPerformed` read as a veto | 20 groups, 71 discrepancies, including a fund rated 9 against an ATR of 5 |
| Citation verification forcing every unverified group to Potential Concern | all 9 No Issue verdicts in one run, from a broken matcher |
| Outcome suppressing a group's own discrepancies | 27–51 differences per run, including F3.1 |

> **A model-supplied field must never be able to delete work that was already done.** It may
> annotate, downgrade, or caveat. Deletion is not available to it.

---

## 5. Cross-check composition needs a fact graph, not string matching

The check catalogue splits contradictions in half by construction: F1.2's two halves live in
CHK-001 and CHK-005, F5.2 depends on F1.2 and F1.3, F9.2 restates F6.1 from the switch side. **A
contradiction spanning two checks is invisible to both**, because each group sees only its own
pack.

The first implementation's answer — `CrossGroupContradictions` — reads *finished prose findings*
and pairs figures that look like the same quantity. It has needed repair every run since it
shipped: splitting on decimal points, choosing restatements over real contradictions, reading
recurrence per-sentence when the flagship case has both markers in one sentence. In Run 5 it
emitted 10 pairs, of which none is a benchmark finding.

**It is solving the wrong problem.** Pairing figures extracted back out of prose is a
reconstruction of information the pipeline had in structured form and threw away.

> **Requirement.** Findings reference the observations they are about, by canonical path and
> observation id. Cross-check composition is then a query over a graph — *which concepts have
> findings from more than one check, and do those findings disagree* — not a regex over sentences.

---

## 6. Measurement must be infrastructure, not a document

This section is non-negotiable and should be built **first**, before any pipeline code.

### 6.1 What went wrong

- **8 of 12+ runs were replays presented as generated runs.** Fixed by a wall-clock guard, and
  only after the analysis of Run 1 was invalidated by it.
- **Recall was adjudicated by hand**, and two adjudications of identical output differed by five
  points. A rubric now exists; it closes the disagreements that *have* happened and will grow.
- **No run isolated a variable.** Five attempts, three or more variables each time.
- **Analysis parsed a stale build artefact** — `bin/…/check-plan` instead of the source — and
  understated the defect count from 3 to 17.

### 6.2 The requirements

1. **A run is a content-addressed artefact.** Inputs (corpus hash, plan hash, model id, settings,
   canonical model hash) and outputs are stored together. Two runs with the same input hash and
   different outputs is a reportable event; the same output from a different input hash is a
   replay and must be refused, not annotated.

2. **The benchmark is a test fixture, not prose.** The 36 material findings become machine-readable
   assertions — the concepts involved, the two sides, the tolerance. Recall is computed by the test
   suite on every run. **A human adjudicating 36 findings by reading is the reason ±2 is the noise
   floor**, and no amount of rubric discipline removes that.

3. **One variable per run, enforced.** The harness records the fingerprint diff between consecutive
   runs and refuses to report a recall delta when more than one component moved. If that is too
   strict, it reports the delta as unattributable — which is what every delta in this project's
   history actually is.

4. **Every count that matters is printed by the run itself.** The counts that mattered most were
   discovered by re-reading logs after the fact: 27–51 discarded discrepancies, hints matching
   nothing, the −£288 reaching four packs unmentioned. Each was invisible for at least one full
   run. **If an analysis needs a script over the logs, the run should have printed it.**

5. **A second case, built early.** Every check on ABC-99 lands on Potential Concern, so **this
   dataset cannot measure false positives at check level at all** — a pipeline returning Potential
   Concern unconditionally scores 10/10. Several changes here (relevance floors, derived findings,
   disagreement detection) alter false-positive behaviour, and none of them can be evaluated on
   this case. This has been deferred since Stage 0 and should not be deferred again.

### 6.3 Exit criteria for the new implementation

Carried from [remediation-plan.md](test-results/remediation-plan.md), with the ones this
implementation has met marked. **Missed-rate leads, because that is what the benchmark says to
gate on** — a missed concern leaves no trace; a spurious one is visible and can be discarded.

| Measure | Target | Best so far | Status |
| --- | --- | --- | --- |
| **Missed material findings** | **≤ 3 of 36** | **2 (Run 5)** | ✅ met |
| **Caught (recall)** | **≥ 27 of 36 (75%)** | 24 (67%), four runs | ❌ **the open one** |
| Runs mis-reported as generated | 0 | 0 since Stage 0 | ✅ |
| Extraction sections succeeding | all | 12 of 12 | ✅ |
| Checks reaching every declared category | all | 10/10 | ✅ |
| Unverified quote rate | < 12% | 13% (Run 6) | ❌ narrowly |
| Groups carrying a flagged quote | < 25% | 42% | ❌ |
| Findings raised then discarded silently | 0 | surfaced, not eliminated | ⚠️ |
| Section declarations matching nothing | 0 | instrumented | ⚠️ |
| False-positive rate on a clean case | measurable at all | **not measurable** | ❌ no dataset |

**Recall ≥ 75% is the one criterion five runs have not moved.** §2 and §3 are the argument for why
it needs an architectural change rather than a sixth stage.

---

## 7. What to carry over unchanged

A rewrite that discards these will re-learn them expensively.

### 7.1 Design decisions that are correct

- **One requirement per model call.** 60 groups rather than 10 checks. Small packs the model can
  attend to in full, failures isolated to the group that caused them, and an identical check
  header at the front of every prompt so the provider's prefix cache covers it.
- **The report is read once, at extraction.** Checks assess against the canonical model, not
  against the report. This is most of the cost control: £2.75 per full run
  (£0.62 extraction + £1.02 embeddings + £1.12 checks).
- **Assertion and evidence are different things, and the prompt says so.** The canonical model is
  *what the report claims*; retrieved passages are *what the file holds*. Conflating them is how a
  report gets to corroborate itself.
- **The plan owns the requirement text, not the model.** 25 of 60 responses once rewrote the
  requirement they were asked about. The plan's values are authoritative; divergence is counted.
- **Ordering in `Rank` is fully specified down to a tiebreak on the passage text**, because scores
  collide and an approximate index is under no obligation to return ties in a stable order. Without
  it the pack differs between sessions before the assessor is called.
- **Analysers are static functions over their arguments.** `CitationVerifier`, `DerivedFigures`,
  `ContentDensity`, `CheckPlanLint` have no state to fake and no second sensible implementation.

### 7.2 Guards that were learned the hard way

**Keep these exactly, with their reasons attached:**

- **Citation verification is elision-only.** A near-miss rule was designed on the measurement that
  39 of 105 failures had a ~90% contiguous run — then rejected, because the altered quotation the
  check exists for ("Risk rating of **6**" reported as "**5**") has a **96%** contiguous run. Any
  threshold loose enough to admit the near-misses admits the fabrication.
- **Punctuation folding stops at digits.** Fold the full stop ending a sentence; keep the one in
  `£1,430.00`. Measured: 34 quote failures → 19, with `£143000`, `018%` and `rating 50` still
  distinct from their true forms.
- **The citation downgrade applies to passes only.** Removing it entirely was tried and let the
  altered quotation clear its check again. A *pass* resting on an untraceable quote is still a
  concern; everything else keeps its verdict and reports confidence beside it.
- **Citation trust is not a recall measure.** It went 36% → 32% → 14% → 11% while recall went
  44% → 67% → 67% → 67%. Report it as what it is: whether a reviewer can follow the working.

### 7.3 The corpus and the answer key

The 28-document case file, the 10-check catalogue with its 60 requirement groups, the 36-finding
benchmark and the scoring rubric are the most valuable artefacts in the project and cost the most
to produce. **They are implementation-independent and must be treated as the fixed point a rewrite
is measured against.**

---

## 8. Build order

Sequenced so that each step is measurable when it lands, and so the things that make measurement
trustworthy come first. This is the one ordering principle the first implementation got right and
it should be kept.

| Phase | Deliverable | Exit test |
| --- | --- | --- |
| **0** | Run harness: content-addressed runs, replay refusal, fingerprint diffing, machine-readable benchmark, recall computed in CI | Re-score all five historical runs and reproduce their analyses to ±0 |
| **0b** | A second, clean case | False-positive rate is measurable at all |
| **1** | Observation-shaped canonical model + extractor, no enums, `verbatim` and `statedAt` mandatory | Both charge tables survive; disagreement between them is computed in code |
| **2** | Section-aware indexing; declarations validated against the corpus at build time | A declaration naming a missing section fails the build |
| **3** | Routing by declared fact; unmet declarations reported as retrieval failures | The −£288 reaches the group that asks about affordability, and only it |
| **4** | Assessment, ported | Findings emitted independently of verdict; no group can delete its own working |
| **5** | Derived findings over the observation graph — disagreement, arithmetic, cross-check composition | F7.1 and F7.2 computed, not judged |
| **6** | Prose-fact retrieval for the residency class | F1.6 and F5.4 reachable |

**Phases 0 and 0b before any pipeline code.** The first implementation spent five runs unable to
attribute its own results, and that is the cost of building them last.

---

## 9. Risks, and the ones already realised

| Risk | Realised? | Mitigation |
| --- | --- | --- |
| A component ships, appears correct, and does nothing | **Yes, repeatedly** — `DerivedFigures` missed its target figure three times across three stages | Every component lands with the run-level count that proves it fired |
| A schema constraint silently discards data | **Yes** — enums cost 8 of 12 extraction sections | No enums on extracted values; validation reports, never drops |
| A fix to a measure is mistaken for progress | **Yes** — four measures went green while recall sat still | Recall and missed-rate lead every report; other measures are labelled as what they measure |
| Reasoning is blamed for an input problem | **Yes, nearly** — the assessor found the Zurich ranking, the £12,000 reduction and the risk-6 file note unaided whenever it was handed them | Do not tune prompts before proving the input reached the group |
| The observation model makes packs too large | Not yet | Observations are referenced by id in prompts; only the relevant ones are inlined |
| Disagreement detection floods the report | **Plausible** — 27 discarded differences in one run were mostly immaterial | Tolerances and severity from the start; measure on the clean case before shipping |
| A rewrite loses the guards in §7.2 | Not yet | They are tested; port the tests first and let them fail |

---

## 10. What this document does not settle

- **Whether the assessor model should change.** No evidence either way: reasoning has not been the
  bottleneck at any point, so there is no measurement to argue from.
- **Whether the extraction should be one pass or twelve.** Twelve sections at 32k tokens works and
  costs £0.62. The observation model will enlarge the output; that needs measuring before the
  section split is redesigned.
- **How prose facts should be indexed.** §3.3 names the requirement and two candidate mechanisms
  and deliberately stops there — the honest answer is that the one thing measured (section hints)
  does not work for this class, and the replacement needs an experiment rather than a decision.
- **Whether 75% recall is achievable on this case at all.** The remaining twelve findings have
  never all been reachable at once. §2 and §3 argue five of them are architecturally blocked; that
  leaves seven whose blocker is not yet identified.
