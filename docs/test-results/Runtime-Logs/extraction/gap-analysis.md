# Extraction gap analysis — case ABC-99, run of 2026-08-13 12:56

Compares the canonical model produced at runtime
([ABC-Cononical-Model.txt](ABC-Cononical-Model.txt), extracted by `intelliflo-claude-haiku-4-5`
over the prompts in [extract_ABC-99_20260813_125630.log](extract_ABC-99_20260813_125630.log))
against the reference model in
[../../artifacts/examples/suitability-report-test-1.extract.json](../../../artifacts/examples/suitability-report-test-1.extract.json).

Both describe the same 21-page suitability report. The improvement plan is in
[improvement-plan.md](improvement-plan.md).

> **Summary.** The reported failure — *"The model did not return a JSON object for this section"* —
> is a misdiagnosis. The model returned well-formed JSON and was **cut off at the output token
> limit**, 87% of the way through, with four of five recommendations already complete. That is the
> visible failure. The more serious one is silent: **the runtime model's cross-reference
> identifiers do not join**, because each section is extracted in an independent call and invents
> its own key scheme. `linkedObjectiveIds` points at labels like `"LowerCosts"` that exist nowhere
> in `objectives`. CHK-006 cannot be evaluated against a model whose recommendations do not link
> to its objectives — and CHK-006 is the check that returned Indeterminate on all four groups in
> the assessment runs.

---

## Side by side

| | Runtime (Haiku 4.5) | Reference |
| --- | --- | --- |
| Total JSON | **256,908 bytes** | 91,983 bytes |
| Top-level sections present | 20 of 21 | 21 of 21 |
| Provenance blocks | 509 | 285 |
| Quotes carried | **476** (52,500 bytes) | 27 (2,153 bytes) |
| Repeated quote instances | **141** | 0 |
| Enum violations | **47** | 7 |
| `extractionReport.expectedButAbsent` | 4 | **12** |
| `extractionReport.internalInconsistencies` | 3 | **12** |
| Recommendations extracted | **0** (section failed) | 6 |
| Identifier scheme | inconsistent, 3 competing forms | `C1`, `EA1`, `OBJ1`, `REC1`, `NP1` |

The runtime model is **2.8× larger and carries less usable structure**. Size here is not richness.

---

## GAP E1 — The Recommendations section was truncated, not malformed

**Severity: critical.** This is the reported failure, and the reported cause is wrong.

All twelve sections returned a ```` ```json ```` fenced object. Eleven closed their fence.
Recommendations did not:

| Section | Response bytes | Fence closed | Parses |
| --- | --- | --- | --- |
| Existing arrangements | 48,380 | yes | ✓ |
| Costs and charges | 47,751 | yes | ✓ |
| Replacement analysis | **56,410** | yes | ✓ |
| **Recommendations** | **61,422** | **no** | **✗ unterminated string** |

`ExtractionMaxTokens` is **16,000** ([AppSettings.cs:198](../../../../src/AiPromptEvaluator/AppSettings.cs#L198)).
61,422 characters of JSON is roughly 15.4k tokens. The section ran into the ceiling and stopped
mid-string, inside a `documentCategory` value.

**Four of the five recommendations were already complete.** The last balanced array element closes
at byte 53,359 — 87% of the way through — and `REC-001` through `REC-004` are whole and parseable.
The current code discards all of it and records the section as failed, so a run that got 80% of
the way loses 100% of the answer.

`ParseObject` tolerates fences and surrounding prose but has no notion of truncation: it returns
`null`, and `ExtractSectionAsync` turns that into *"The model did not return a JSON object"*
([CanonicalModelExtractor.cs:173](../../../../src/AiPromptEvaluator/CanonicalModelExtractor.cs#L173)).
The message sent whoever read it looking for a prompt-compliance problem, which is the one thing
it was not.

**Replacement analysis at 56,410 bytes is the next failure waiting to happen** — it cleared the
limit by about 1,200 tokens on this document, and this is a comparatively simple case with five
arrangements and one client.

---

## GAP E2 — Cross-reference identifiers do not join

**Severity: critical.** Larger than E1, and it produces no error at all.

The canonical model is relational: recommendations point at objectives and at existing
arrangements, and the checks navigate those links. In the runtime model the links are broken.

| Field | Runtime | Reference |
| --- | --- | --- |
| `clientId` | `"John Sullivan"`, `"client-1"`, `"client-2"` — three forms in one model | `"C1"`, `"C2"` |
| `arrangementId` | `"Aviva - SP50163762"` **and** `"Aviva-SP50163762"` — two spellings of one arrangement | `"EA1"`…`"EA5"` |
| `objectiveId` | `"OBJ-001"`…`"OBJ-003"` | `"OBJ1"`…`"OBJ3"` |
| `linkedObjectiveIds` | `["LowerCosts"]`, `["ConsolidationAndClarity"]`, `["EnsureAppropriateStructure"]` | `["OBJ1","OBJ2","OBJ3"]` |
| `sourceArrangementIds` | **absent from the merged model** | `["EA1"]`, `["EA3","EA4","EA5"]` |

`linkedObjectiveIds` is the clearest case. The objectives section assigned `OBJ-001`, `OBJ-002`,
`OBJ-003`. The recommendations section, running as a separate call that never saw them, invented
descriptive labels instead. **Not one of the seventeen `linkedObjectiveIds` values resolves.**

The schema is the enabler. It says only:

> `clientId` — Local id used for cross-references within this instance.

That is a contract between passes, but there is only one pass per section and nothing carries the
keys between them. Each call re-reads the report and re-invents the naming. The system prompt
([CanonicalModelExtractor.cs:185](../../../../src/AiPromptEvaluator/CanonicalModelExtractor.cs#L185))
says nothing about identifiers at all.

**This is why CHK-006 cannot pass.** The check asks whether each recommendation has
client-specific rationale *linked to a stated objective*. `linkedObjectiveIds` was among the paths
that resolved to nothing in the assessment runs, and all four CHK-006 groups returned
Indeterminate. The retrieval work will not fix it — the join is broken in the model itself.

Note that the reference model's `OBJ1` and the runtime's `OBJ-001` are equally valid in isolation.
The defect is not the format; it is that **nothing makes the format the same twice**.

---

## GAP E3 — Quote discipline is costing the output budget

**Severity: high.** This is what pushed Recommendations over the limit.

| | Runtime | Reference |
| --- | --- | --- |
| Provenance blocks | 509 | 285 |
| …carrying a quote | **476 (93%)** | 27 (9%) |
| Average quote length | 110 chars | 79 chars |
| Total quote bytes | **52,500** | 2,153 |
| Repeated quote instances | **141** | 0 |

Quotes are **20% of the entire runtime model**, and 141 of them are duplicates — the same sentence
copied into provenance after provenance.

The runtime model is following the instruction; the reference is not:

> Every Stated or Inferred provenance needs a verbatim quote from the document and the page number

The reference carries quotes on 9% of blocks and is the more usable artefact. That is worth sitting
with: **the prompt asks for something whose full cost was never priced.** A quote on every
assertion is defensible in principle, but at 476 quotes it consumes the budget that the
recommendations needed, and duplicate quotes add nothing a page number does not already give.

---

## GAP E4 — The extraction under-reports its own gaps

**Severity: high**, because the assessment side now depends on this field.

| | Runtime | Reference |
| --- | --- | --- |
| `expectedButAbsent` | 4 | **12** |
| `ambiguities` | 4 | 4 |
| `internalInconsistencies` | 3 | **12** |
| `overallConfidence` | 0.82 | 0.87 |

The assessor prompt now presents this section under *"Use this to tell report silence from
extraction failure"*. On the runtime model it names four absent paths where the reference names
twelve, and three internal inconsistencies where the reference finds twelve — in a report that
demonstrably contains many more than three.

The section runs **last**, after eleven other passes, and is given the same document but none of
what those passes found. It is being asked to report on work it cannot see. The reference model
was produced with the whole document in view, and it shows.

This directly weakens the fix made for GAP 4 in the assessment pipeline: the assessor is told to
consult a self-report that is under-populated.

---

## GAP E5 — Code-owned fields are being answered by the model

**Severity: moderate**, and trivially fixable.

```json
"extractedAt": "2025-01-01T00:00:00Z",
"extractorModel": "claude-opus"
```

Neither is true. The run was on **2026-08-13** using **`intelliflo-claude-haiku-4-5`**. These are
facts the process knows and the model cannot; asked for them anyway, it produced a plausible
placeholder — exactly the failure mode the system prompt's *"Never invent a value"* rule exists to
prevent, on the two fields where the rule was never enforceable.

A stored model that misreports which model produced it also defeats the point of the run
fingerprint.

---

## GAP E6 — `basis` is being used as a free-text field

**Severity: moderate.**

47 enum violations in the runtime model against 7 in the reference. 42 of the 47 are on `basis`:

```
basis = "of fund value pa"                                            x25
basis = "of assumed growth rate"                                       x6
basis = "of projected fund value"                                      x3
basis = "annual growth required at mid-growth rate 2.94% up to age 75" x1
```

The last one is a sentence in an enum slot. The system prompt's rule — *"Enumerated fields must use
a value from the schema's enum, or be omitted"* — is stated and not enforced, because nothing
validates the response against the schema before merging it. The reference makes the same class of
mistake six times fewer, so this is partly a model-capability difference and partly an absent
check.

---

## What the runtime model does better

Worth recording, because the fix should not lose it:

- **`replacementAnalysis` has 4 entries against the reference's 3.** The runtime includes the
  Scottish Widows encashment as a replacement, which is the better reading — an encashment is a
  disposal of an existing arrangement and CHK-009 should see it.
- **Coverage of `vulnerability`, `taxTreatment` and `ongoingService` is substantially fuller.**
  Some of that is the verbosity of E3, but not all of it: the reference's `vulnerability` block is
  598 bytes and thin.
- **It ran to completion on eleven of twelve sections** on a model far cheaper than the reference's.

---

## One finding that revises earlier analysis

`/parties/clients[]/dateOfBirth` is absent from **both** models. It was listed in
[../../gap-analysis.md](../../../gap-analysis.md) GAP 4 as an extraction failure that made CHK-001's
age comparison impossible.

It is not an extraction failure. **The suitability report never states a date of birth** — it says
*"You are currently aged 69"* and nothing more. Both extractions are correct to omit it.

The date of birth is in the Fact Find, which is category B — the category that
[../../test-results/dataset-run-gap-analysis.md](../../dataset-run-gap-analysis.md)
found four checks never retrieve a single passage from. So the age finding is a **retrieval** gap,
not an extraction gap, and the fix belongs on the search side. GAP 4's list of 33 paths should be
re-triaged on this basis: some proportion of it is the report legitimately not saying something.

---

## Priority

| | Gap | Cost of leaving it |
| --- | --- | --- |
| 1 | **E2** identifiers do not join | CHK-006 cannot be evaluated; silent |
| 2 | **E1** truncation discards 87% complete work | One section lost per run, misdiagnosed |
| 3 | **E3** quote volume | Causes E1; 20% of model is quotes, 30% of those duplicates |
| 4 | **E4** thin self-report | Weakens the assessor's silence-vs-failure distinction |
| 5 | **E6** enum violations | Unvalidated values reach the checks |
| 6 | **E5** fabricated provenance fields | Model misattributes its own origin |

E2 and E1 are independent: fixing the token limit will not make the identifiers join, and fixing
the identifiers will not stop the truncation.
