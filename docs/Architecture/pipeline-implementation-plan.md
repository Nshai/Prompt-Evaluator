# Pipeline implementation plan

**Companion to** [pipeline-approach-analysis.md](pipeline-approach-analysis.md) (Revision 4).
2026-08-30, revised 2026-08-31.

The analysis ends with nine moves and six phases. This is how they get built: what has landed,
what each remaining phase costs, and — the part that decides everything after it — **the gate that
tells us whether the diagnosis is right before we spend a month acting on it.**

---

## 0. The target for v1, and where it departs from §6.2

**§6.2's architecture is not v1's target, and cannot be.** It is recorded here because building
toward a diagram that this pipeline has decided not to reach would produce a permanent, invisible
shortfall — work would keep being measured against a bar nobody intends to clear.

§6.2 says two changes carry it:

> the assessor always sees the whole assertion side in compressed form, and **the evidence side is
> reconciled to it in code rather than by a plan author's guess.**

The first is the assertion digest, and it is built. The second is the bottom limb —
`S1b DOCUMENT CLAIMS → RECONCILE in code on canonicalPath` — and §6.4 specifies its mechanism
without ambiguity: *schema-constrained LLM inference, one pass per document.* **Reconciliation there
is path equality**, which requires both sides to be typed in the same vocabulary.

**v1 does not pay for a model pass over evidence documents.** That is a deliberate cost decision,
not an omission, and it removes the mechanism that types the evidence side. So the limb is out, and
with it the form of reconciliation the diagram is built around.

**Search is not a substitute for it, and the ceiling is measured rather than argued.** Enumerating
every populated canonical leaf and asking how many carry a literal an exact-match index could join
on:

| Case | Populated leaves | Carry any figure | Distinctive enough to join | Prose only |
| --- | :-: | :-: | :-: | :-: |
| Test Case 1 | 1,871 | 372 | 169 (9.0%) | — |
| Test Case 2 | 1,912 | 372 | 193 (10.1%) | — |
| Test Case 3 | 1,902 | 346 | 186 (9.8%) | **1,556 (81.8%)** |

A join needs something both sides share. About a tenth of the assertion side offers one; four fifths
offer none. Retrieval of any richness — hybrid, reranked, contextual — does not move that number,
because it is a property of the data and not of the search. Matching the prose four fifths by
similarity is prose-to-prose comparison, which is what `detect: None` exists to forbid on 18 of the
88 groups: *comparing prose as values manufactures findings*, and the largest false positive in
either measured run was ten "contradictions" that were one undisclosed basis tabulated ten times.

### v1's target, stated positively

```
S1  EXTRACTION            unchanged — cached, model a lever
      │
      ├─▶ every populated path has a reader (ratcheted, then asserted)
      │
S2a   PLAN  ── directives ──▶ code branches: reconciliation · adjudication
      │  group's own paths ────────────────▶┌─────────────────────────────┐
      │  ASSERTION DIGEST (all paths, 1 ln)▶│   S4  one prompt per group  │
S3    RETRIEVAL  pack ≤12, hybrid ─────────▶│   cached prefix, per-group  │
      │                                     └─────────────────────────────┘
      └─▶ CODE-LEVEL JOIN on shared literals ──▶ (reaches ~10% of assertions)
```

Two differences from §6.2, both deliberate:

1. **No S1b, and therefore no path-equality reconciliation.** The code-level join replaces it for
   the literal-bearing tenth. The prose remainder is handled where it is already affordable — by
   the S4 model that is reading the passages anyway, given the whole assertion side in compressed
   form. That is the digest's real job, and it is why Move 3 matters more here than in §6.2.
2. **The plan drives behaviour through typed directives**, which §6.2 does not model at all.
   `reconciliation` and `adjudication` are branched on by code, not interpolated into a prompt.

**What this costs in coverage is unmeasured and should not be guessed at.** §4.4 puts the current
architecture's ceiling at 63% (TC3) and 85% (TC1) for the union of two models. Whether the v1 target
reaches those, exceeds them, or falls short is what Phase 1 and a benchmark run decide. Nothing in
this document should be read as a claim that it matches §6.2's ceiling — it is a cheaper
architecture, and the honest expectation is that it trades some of the residue for an order of
magnitude less cost per document.

### What is therefore out of scope for v1

| §6.2 element | Status |
| --- | --- |
| `S1b DOCUMENT CLAIMS` | **Out.** Requires a model pass per evidence document. |
| `RECONCILE in code on canonicalPath` | **Out.** Requires S1b to type the evidence side. |
| `ADJUDICATE per candidate` | **Reshaped.** Adjudication is a plan directive over group findings, not a per-candidate call. |
| Reranking (Move 8) | **Dropped.** The analysis's own recommendation is to *"land on self-hosted"* — a cross-encoder on a GPU the tenant operates. That is an operational commitment, and the case for it was never recall: §3.1 shows two decisive passages at **rank 1** missed by both models, so reranking would have produced **zero** additional findings. Its only argument was cost, and prefix caching plus per-group pack budgets take that ground more cheaply and with nothing to run. |

### Document-first traversal, as a directive-driven design

Move 9 is the residue: findings where **no requirement asks the question at all**, which no amount
of retrieval, ranking or composition can reach. §6.4 specifies it as a model pass per document, and
that is the part v1 excludes — but the *shape* is worth recording now, because the check plan is
where it would be driven from and the directives to drive it already exist.

The traversal has four steps and only one of them needs a model:

| Step | Mechanism | Driven by | Deterministic |
| --- | --- | --- | :-: |
| Selection — which documents | code, by category | the group's `declares.evidenceCategories` | ✔ |
| Segmentation | code, structure-aware | Move 7's table-aware chunking | ✔ |
| **Inference — emit typed claims** | **model, schema-constrained, one pass per document** | a category-sliced schema (`JsonSchemaSlicer`) | ✘ |
| Grounding — verify quotes | code, `CitationVerifier` | `adjudication.requiresCitation` | ✔ |

**Three of the four are already built or queued**, and every one of them is plan-driven. What is
missing is the third, and it is missing on purpose: it is the per-document model pass.

**How the directives would drive it, unchanged.** This is the part worth recording, because it means
adopting document-first later is a scope decision rather than a redesign:

- `reconciliation.detect` already says what makes a candidate — `ValueDivergence`, `SetDifference`,
  `Absence`, `RangeBreach`, `None`. Against typed claims these become executable predicates rather
  than steers; `Absence` in particular becomes a lookup over the claim index instead of a model
  guessing at what is not there, which is the shape of 14 of Test Case 3's 83 findings.
- `reconciliation.modality` and `asAtToleranceDays` apply unchanged — they are properties of a
  typed value, and the claims would carry the same fields the canonical model already does.
- `emitOnePerPath` is exactly the fan-out control a claim-level join needs.
- `adjudication.mode: Deterministic` becomes reachable for the first time: with both sides typed,
  code can prove a divergence and only a sentence is wanted.

**The honest position on cost.** The scoped version is 11 documents of 37 and ~79k tokens, cached by
document hash so it is paid once per case rather than once per run. Whether that is affordable is a
decision about this pipeline's economics, not a technical question — and it is the only route to the
prose four fifths that §0 measures as unreachable by search.

---

## 1. The one-paragraph version

The analysis's verdict is that the pipeline decides in advance, by hand, in ten JSON files, which
of the report's claims may be compared with which piece of evidence — and gets that decision wrong
often enough to dominate retrieval, ranking, eviction and model choice combined. **Phase 0 and
Phase 1 are now implemented and green.** The measurement that carries the argument is no longer a
hand analysis of a saved extract; it is a test that runs on every build, and it reproduces the
analysis's number independently. What remains is to run the experiment the code now makes possible,
on real cases, and let the result decide whether Moves 4–9 are built at all.

---

## 2. What has landed

`dotnet test`: **1,064 passed, 0 failed.**

| Move | What was built | Where |
| --- | --- | --- |
| **Phase 0** | Silent group drop cannot recur | `Services/Assessment/CheckPlanRunner.cs`, `CheckFinding.cs` |
| **Move 1** | Path enumeration and the coverage ratchet | `Services/Extraction/CanonicalPaths.cs`, `Services/Assessment/PathCoverage.cs` |
| **Move 2** | Collidability lint, decidable half | `Services/Assessment/CheckPlanLint.cs` (rule L8) |
| **Move 3** | The assertion digest, behind a flag | `Services/Assessment/AssertionDigest.cs` |
| **Move 5** | The code-level join (the generic half) | `Services/Assessment/EvidenceJoin.cs` |
| **Directives** | `reconciliation` / `adjudication` ported; code branching still to come | `CheckQueryPlan.cs`, `docs/Implementation/check-plan/*` |
| **Cost** | Prefix ledger, cache accounting, per-group pack budgets | `Services/Ai/PromptPrefixLedger.cs`, `ChatCompletionClient.cs`, `CheckQueryPlan.cs` |
| **Providers** | Bedrock embeddings and Converse chat | `Services/Ai/BedrockEmbeddingGenerator.cs`, `Services/Ai/BedrockChatClient.cs` |

### 2.0 Providers — what unblocked the run

Neither of these is in the analysis; both were blocking any measured run at all.

**Embeddings never used their own endpoint.** `EmbeddingBaseUrl`, `EmbeddingApiKey` and their
resolvers all existed and nothing called them, so the generator was built from the *chat* endpoint
and key. Pointed at Bedrock, the OpenAI client failed in the least helpful way available: Bedrock
answers an unknown path with **HTTP 200** and a Coral `UnknownOperationException`, which the client
read as a valid response holding zero embeddings — *"Expected the number of embeddings (0) to match
the number of inputs (1)"*, a message naming neither the endpoint, the protocol nor the mistake.

**Chat could not reach Claude at all.** The OpenAI-compatible gateway lists 35 models including
every Claude, and serves none of them over `/v1/chat/completions`; `/v1/messages` and
`/v1/responses` are 404 and 400. Bedrock's `Converse` API is the only route, and it is also the only
one that can be *told* to cache. Verified live in eu-west-1:

```
1st call   cacheWrite=4902  cacheRead=0
2nd call   cacheRead=4902
3rd call   cacheRead=4902
```

Two things found only by running it, which no amount of review would have caught: Bedrock rejects a
`Content-Type` carrying `charset` (the obvious `StringContent` constructor always appends one), and
Claude on Bedrock refuses `temperature` and `top_p` together. Both are now regression-tested.

**Two facts about model ids** that cost a session between them: the endpoints name the same model
differently — `anthropic.claude-haiku-4-5` on the gateway,
`eu.anthropic.claude-haiku-4-5-20251001-v1:0` on the runtime — and **Titan v2 emits 1024/512/256
dimensions, never 1536.** The analysis's "Titan v2 1536-d" (§1, §5) is therefore not a configuration
that can exist; whichever the measured runs used, the document is wrong on one of the two.

### 2.1 Phase 0 — the silent group drop

Six requirements across three runs were assessed and then appeared nowhere in the output; one was
*"tax consequences of the recommendation"*. The analysis says fix it first, and it is right for a
reason worth restating: **everything downstream is computed over what survived it.** A check's
outcome is derived from its groups, the benchmark is scored against the output, and a requirement
that vanished scores as a requirement that found nothing.

Two changes, and one of them was a latent defect the first uncovered:

1. `RunAsync` allocates its findings array **nullable by element**, so an unwritten slot is a state
   the type admits, and reconciles every slot against the plan's groups before aggregating. A slot
   with no finding becomes `CheckPlanRunner.NotAssessed(group)` — a per-group `Error` carrying the
   requirement's identity and words that say the defect is in the run, not in the advice.
2. `CheckFinding.ParsedOutcome` promised that the deterministic checks *"can only ever move an
   outcome away from a pass"* and then broke that promise on this input: a finding with
   `ComparisonPerformed = false` and nothing compared was rewritten to `Indeterminate`
   **whatever it stated** — including `Error`. That turned "this requirement did not run" into "the
   assessor read it and could not decide". The rule is now guarded on `Error`, which is the app's
   own word and never the model's.

The property that matters: **a check can no longer report No Issue when one of its requirements
never ran.** (`SilentGroupDropTests`.)

> The *mechanism* of the original drop is still unproven — the parallel body writes by position and
> a throw fails the whole check, so no static path explains it. This is deliberately a **guard, not
> a diagnosis**: it converts a silent, non-deterministic, measurement-corrupting absence into a loud
> per-group error naming the requirement. If it ever fires, the archive will say which group and we
> will have the mechanism. Phase 0's remaining work — two runs per configuration, automated
> benchmark scoring — is unaffected and still to do.

### 2.2 Move 1 — guarantee every path a reader

`CanonicalPaths.Enumerate` walks a stored model and returns every populated leaf with three things:
the resolved path, the plan-shaped path (`[3]` → `[]`), and the section heading its object's
provenance block recorded. `PathCoverage.Measure` intersects that against every path the ten plans
select, **treating a selected parent as covering its children** — which is what the runner actually
does, so measuring on exact equality would have manufactured most of the gap.

Excluded from the walk, deliberately: `provenance`, `extractionReport`, `modelVersion`. Those are
the pipeline describing its own work rather than the report making a claim, and counting them would
inflate the denominator of every coverage figure with paths no check should ever select. The
heading survives as `CanonicalLeaf.Source`, which is the part that makes a value traceable.

**It reproduces the analysis.** Run against the three stored canonical models and the shipped plans:

| Case | Populated paths | Read by some plan | Unread | Largest unread roots |
| --- | :-: | :-: | :-: | --- |
| Test Case 1 (Run 18) | 574 | 451 | **21%** | `recommendations`, `replacementAnalysis`, `taxTreatment`, `clientAuthority` |
| Test Case 2 (Run 3) | 578 | 456 | **21%** | `recommendations`, `taxTreatment`, `clientAuthority`, `replacementAnalysis` |
| Test Case 3 (Run 3) | 549 | 434 | **21%** | `recommendations`, `taxTreatment`, `replacementAnalysis`, `clientAuthority` |

Independently derived, and **the same six roots the analysis names, on all three cases.** The
absolute counts differ from §3.5's (574 against 1,333) because this counts *distinct plan-shaped
paths* rather than one entry per array element — a case with forty holdings does not have forty
times the coverage problem of a case with one. The ratio, which is the finding, is identical.

**Shipped as a ratchet, not a gate.** "Every path has a reader" would be red on every build from
the moment it was written, and a permanently red test is one nobody reads. `PathCoverageTests`
asserts against a recorded baseline of **78%**, prints the full breakdown by root on every run, and
raising the baseline is how closing a root gets recorded. It also reports, in the other direction,
paths every plan selects that **no** stored model has ever populated — a plan citing a path the
schema does not have has silently had no assertion side for as long as it has existed.

### 2.3 Move 2 — the collidability lint

`CheckPlanLint` gains **L8**: `ValueMatch`, `RangeMatch` and `SetCoverage` each put a figure the
report states against something the file evidences, so a group declaring one of them must have both
sides — canonical paths, and at least one query targeting a category. Model-only groups are exempt,
because comparing the report against itself is a real and deliberate shape.

**L8 finds nothing on the shipped plans today.** That is worth stating plainly rather than burying:
the cheap half of Move 2 is already clean, and it is now a regression guard rather than a fix. The
expensive half — whether a group's paths and its evidence categories are two sides of *one*
question — **is not decidable from the plan alone.** `G9.10` selects assertion paths about exit
penalties while declaring category B, where certified court orders live, and nothing in either
declaration says they do not belong together. **That is what Move 3 routes around instead of
forbidding**, which is the analysis's own principle: compute the decision rather than authoring it.

### 2.4 Move 3 — composition at constant prompt size

`AssertionDigest.Render` prints the whole assertion side of the case as one line per populated path
— `` `path` = value ⟨section — document⟩ `` — minus the paths the group is already being shown in
full. It is placed with the assertion side and **above** the passages, so it cannot be read as more
evidence, and it tells the assessor plainly what it is for: *look here for a claim that contradicts
the requirement in front of you; do not assess these values against their own requirements.*

Four properties the tests hold it to, each bought by a measured failure:

- **The budget drops whole lines, never a partial one.** A half-written path looks like a value the
  model can cite, and would then fail citation verification having been manufactured by the renderer.
- **A truncated digest says so in the prompt.** Otherwise it is the assertion-side version of
  eviction: the assessor concludes the report is silent on a point the model records.
- **Off is byte-identical to before.** The experiment has to be attributable to one change.
- **The same model renders the same digest.** It sits in a cached prompt prefix, and reproducibility
  is a first-class requirement in a regulated QA tool.

Two settings, both fingerprinted, both on the configuration form: `AssertionDigest` (off by default
— it changes every prompt in a run, so a run opts in) and `AssertionDigestMaxChars` (40,000, about
10k tokens, about four hundred lines, which covered every populated path of all three cases).

---

## 3. The gate: run Phase 1 before building anything else

Everything in §4 rests on one unproven claim, and the analysis is honest that it is opinion with a
stated risk: **that widening the assertion slice at constant prompt size raises recall.** There is
one measured experiment on adding context to this prompt and it went the other way — Run 14 added
13% more prompt and lost eleven points.

The mitigation is arithmetic, and it must be confirmed from the prompt log rather than assumed:

```
pack 24, digest off   ≈ 24 passages × ~550 tok                    ≈ 13,200 tok
pack 12, digest on    ≈ 12 passages × ~550 tok  +  ~9,000 tok      ≈ 15,600 tok
```

That is **not** obviously smaller, and the honest reading of §3.3 is that it might be enough to hurt.
Two things follow, and both are cheap:

1. **Measure the real numbers first.** Run one check with the digest on and read the prompt log. If
   the digest lands above ~7k tokens, drop `AssertionDigestMaxChars` until pack-12-plus-digest is at
   or below pack-24-alone. **Constant prompt size is the experimental condition, not a slogan** —
   the whole point is to isolate composition from volume.
2. **Run the 2×2, not the diagonal.** Pack 24 / digest off (control), pack 12 / digest off, pack 24 /
   digest on, pack 12 / digest on. Without the two middle cells, a result cannot be attributed to
   composition rather than to the pack cut.

**Two runs per cell**, on **TC3** — TC1 plateaued fifteen runs ago and its remaining movement is
noise. TC1 gives a floor of ±2 findings / ±5 points; nothing comparable exists for TC3, so the
control cell run twice is also the first noise floor that case has ever had. Eight runs at Haiku's
£3.00 is **£24 and an afternoon.**

| Gate | Decision |
| --- | --- |
| Recall rises outside the floor at constant prompt size | §3.4 is confirmed. Build Moves 4–6. |
| Recall flat | Composition is not the lever. **Keep** Move 1's ratchet and Phase 0; do not build Moves 5–6. Reconsider Move 9 directly. |
| Recall falls | §3.4 is wrong, and the analysis says so itself: *"Moves 4–6 should not be built."* |

Move 4 — Haiku assessment against Sonnet's stored canonical model, £4.50 — is independent of this
gate and can run in parallel. It is load-bearing for the whole savings case: if Haiku-on-Sonnet's
model does not hold near 45 findings, §4.5 collapses to row 0b.

---

## 4. What is not built, and what each needs

Ordered as the analysis orders them. Estimates are the analysis's; the notes are what the code says
about them.

**Move 5 — routing repair and the code-level join** *(2–3 wk, gated on Phase 1)*
Two halves. The routing repair is plan edits for named findings — TC3 F2.5/F9.6, F3.7, F8.6, F9.9,
F1.15; TC1 F2.2 — and each should raise `PathCoverage`'s baseline as it lands. The code-level join
matches pack passages against **all** canonical assertions and injects strong matches under
*"assertions elsewhere in the report this evidence may bear on"*. `CanonicalPaths.Enumerate` already
supplies the left-hand side; the matcher is the new work, and it should follow `DerivedFigures` and
`CrossGroupContradictions`, both of which hold. **Note the overlap with Move 3:** if the digest
works, the join is a precision refinement of it rather than a separate mechanism, and should be
scoped down accordingly.

**Move 6 — code-side comparisons** *(~2 wk)*
`asAtDate` on canonical values and passages with date-comparability computed in code; `DerivedFigures`
extended to sign errors (TC1 F7.3 is *"4.24% adds instead of subtracts"* — arithmetic no model has
caught); extraction self-consistency promoted to a first-class pass; routed-inconsistency fan-out
capped (one item reached 26 of 88 groups, 92 times); an S5 cross-group consistency check. Every item
here is an instance of the principle: **every judgement moved into code has held; every judgement
moved into a prompt clause has been ignored by at least one model.**

**Move 7 — hybrid retrieval and structure-aware chunking** *(~2 wk)*
Sparse + dense RRF fusion in Qdrant, as a *routing* fix rather than a recall fix. Never split a
table; each table its own chunk with its caption, which retires the `TableContext()` workaround.
**Fix the DOCX table loss at S0 first** — it is a hard floor of 4 findings nothing downstream can
recover, and it is the only item in this document that is pure loss with no trade-off.

**Move 8 — reranking** *(~1 wk)*
Justified on cost and on retiring `CheckPlanRunner.Rank()`'s four stacked reservation systems, **not
on coverage**: two decisive passages sat at rank 1 and were missed by both models. Prototype on
Voyage `rerank-2.5-lite`; land on a self-hosted cross-encoder so client PII never leaves the tenant.

**Move 9 — document-first traversal** *(4–6 wk)*
Schema-constrained extraction emitting canonical paths directly, scoped to categories B/C/D/F —
11 documents of 37, ~79k tokens — cached by document hash. Reconciliation is then path equality in
code, with no hand-built ontology. `JsonSchemaSlicer` already does the schema-slicing half.

**Deliberately not recommended, and this plan agrees:** agentic retrieval (it destroys
reproducibility, which is a first-class requirement here — build the deterministic second hop from
`missingInputs` instead), and running both models permanently (it buys ~10 points by paying twice
for a defect that costs almost nothing to fix in the plan).

---

## 5. Sequence

| Phase | Work | Cost | Status |
| :-: | --- | --- | --- |
| **0** | Silent group drop | — | **Done** |
| **0** | Bedrock providers — embeddings and Converse chat | — | **Done** (unplanned; nothing could run without them) |
| **0** | Automated benchmark scoring; two runs per configuration; TC2 on Sonnet | ~1 wk | **To do** |
| **1** | Moves 1–3 in code | — | **Done** |
| **1** | Move 5's join; directive schema; cost levers | — | **Done** (built ahead of the gate, on request) |
| **1** | Config: dimensions → 1024, re-index, provider → Bedrock | ~hours | **To do — blocks any run** |
| **1** | The 2×2, twice, on TC3 · Move 4 in parallel | £24 + £4.50 | **The gate. Still unanswered.** |
| 2 | Directive branching (Move 6's substance, plan-driven) | ~1 wk | In progress |
| 3 | Move 7 — hybrid retrieval, table-aware chunking, S0 table loss | ~2 wk | Queued |
| — | Move 8 — reranking | — | **Dropped** — see §0 |
| — | Move 9 / S1b document-first | — | **Out of scope for v1**, design recorded in §0 |

**Note on ordering.** Moves 5 and the directive work were built before Phase 1's gate rather than
after it, at the request of the person running this. That is a real risk and it is recorded rather
than smoothed over: if the gate says composition does not raise recall, some of this was built on a
diagnosis that did not hold. None of it is wasted — the join, the directives and the cost levers
stand on their own reasoning — but the *ordering* the analysis argued for was not followed.

---

## 6. Known gaps in what was built

Stated rather than left to be discovered:

- **No end-to-end prompt test for the digest.** Its rendering is unit-tested and its call site is
  three lines, but no test constructs a `CheckPlanRunner` — that needs chat and search fakes which
  do not exist in the suite. Phase 1's first run should confirm from the prompt log that the digest
  appears once, in the right section, at the expected size. Building those fakes is worth doing and
  is the cheapest way to make prompt composition testable at all.
- **The group-drop mechanism is still unknown.** §2.1.
- **The coverage baseline is a ratchet at 78%, not a rule at 100%.** Raising it is Move 5's job, and
  the number should move every time a root is closed.
- **`AssertionDigestMaxChars` defaults to 40,000 on reasoning, not measurement** — the same status
  as `MaxPassagesPerGroup`'s 24, which the analysis is careful to flag. Treat the first run at this
  value as an experiment with its own measurement.
