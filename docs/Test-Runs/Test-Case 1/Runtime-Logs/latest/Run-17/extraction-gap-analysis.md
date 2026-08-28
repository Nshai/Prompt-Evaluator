# Extraction gap analysis — Run 17's canonical model

An examination of [canonical-model_ABC-99_20260827-095807.json](canonical-model_ABC-99_20260827-095807.json)
(350,536 characters, 22 sections, extracted 2026-08-27 09:58 by `intelliflo-claude-haiku-4-5`), the
first live extraction in the series. Read against
[the schema](../../../artifacts/canonical-suitability-model.schema.json), the ten query plans,
[the 60 re-extracted material facts](../../material-facts-gap-analysis.md) and
[Run 17's scored output](run-analysis.md).

---

## The finding

**The canonical model is not a model of the case. It is a model of the suitability report.**

Every one of the 274 provenance blocks that names a source document names the same one:

```
documentId               count
Suitability Report Test 1.pdf      245
I/Suitability Report Test 1.pdf     29
(none)                             335
```

```
documentCategory         count
I_RecommendationsAndAdvice         354
G_Research                          16
(none)                             239
```

**Zero blocks cite the Fact Find. Zero cite the file note. Zero cite the risk profile report.** The
`source` object confirms this is by design rather than by accident — it is a single object, not an
array:

```json
"source": { "documentId": "Suitability Report Test 1.pdf", "documentType": "SuitabilityReport", "pageCount": 21 }
```

Pages 1 to 21 are all cited. The extraction read the report completely and read nothing else.

**This is the structural gap, and it reframes the previous analysis.**
[material-facts-gap-analysis.md §3](../../material-facts-gap-analysis.md) concluded that the missing
thing was a `questionnaireResponses[]` array. That conclusion was right about the symptom and
incomplete about the cause. The questionnaire answers are not missing because the schema lacks a
field for them — they are missing because **the questionnaire is in a document the extractor never
opens.** Adding the array to the schema would not populate it.

Every check in this pipeline is a comparison of *what the report says* against *what the file
records*. The canonical model supplies only the first half. The second half exists solely as retrieved
passages, which is why:

- **F3.1 was lost in Run 17** — the file note's `Risk rating of 6` reached eight group prompts and no
  CHK-003 group. A canonical path cannot be evicted from a pack; a passage can.
- **F2.2 has never been Caught in seventeen runs** — its evidence is the CFL questionnaire's *"Less
  than 3 years"*, in the risk profile report.
- **F1.6 and F5.4 have never been Caught** — `Residency Status: Tenant – private` is a Fact Find row.
- **F1.9 is inverted in every run** — it requires knowing what the Fact Find's pension table omits,
  and the model has no representation of that table at all.

**Six of the 36 material findings depend on a document the extraction does not read.** That is not a
schema gap and no new field fixes it.

---

## 1. What is genuinely absent from the model

Three items, in descending order of consequence.

### 1a. Client date of birth — and Run 17's misattribution explained

```json
{"id":"C1","role":"Primary","first":"John","last":"Sullivan","age":69,"gender":"Male"}
{"id":"C2","role":"Joint","first":"Kim","age":65,"gender":"Female"}
```

**Neither client has `dateOfBirth`. Both have `age`.** And `age: 69` is sourced from the report:

> `"quote": "You are currently aged 69 and married to Kim, aged 65. You are both in good health"`

This confirms and sharpens [run-analysis §4](run-analysis.md#4-the-live-extraction-is-better-in-the-aggregate-and-worse-in-one-specific-place).
The mechanism is now legible, and it is not a dropped field:

**The Fact Find records `Date of Birth 07/06/1956` and `Age 70`. The report says 69. The extraction
took the report's 69 and never had the Fact Find's DOB to disagree with it** — because it never read
the Fact Find. G1.1's complaint (*"no date of birth extracted for client in canonical model … no DOB
in evidence pack to verify"*) is therefore accurate about the model and describes a designed
limitation rather than a bug.

G1.3's misattribution — assigning Kim's `07/05/1960` to John — follows from the same cause. The
assessor had a `Client` object with an age and no DOB, and a retrieved passage containing a DOB, and
joined them. **My earlier hypothesis that F1.5's blank name fields caused this is wrong**: the model
has `firstName: "John"`, `lastName: "Sullivan"` populated, because the *report* names him. The blank
Fact Find name fields never reached the model.

`dateOfBirth` is in the schema (two definitions, lines 470 and 823). It is unpopulated because its
only source is unread.

### 1b. `hasComplexProduct: false` contradicts its own extraction pass

```json
"checkTriggers": { "hasComplexProduct": false }

"knowledgeAndExperience": {
  "recommendedProductComplexity": [{
    "productName": "Aviva Platform Personal Pension with Flexi Access Drawdown",
    "complexity": "Complex",
    "complexityDrivers": [ "Multiple investment funds (6,900+ available)", "Flexible drawdown mechanisms",
      "Tax planning considerations…", "Inheritance Tax planning changes from 6 April 2027",
      "Death benefit options with different taxation regimes…", "Lifetime allowance and annual allowance…" ] }]
}
```

**The model asserts the product is Complex with six named drivers, and the trigger says there is no
complex product.** Both are in the same section, `knowledgeAndExperience` and `checkTriggers` being
written by passes 4 and 12.

**It cost nothing in Run 17, by luck.** No query plan gates on `hasComplexProduct` — `grep` across all
ten returns zero — so nothing was switched off. That is the same latent condition that produced the
CHK-005 outage in Runs 15 and 16, sitting in a different field and currently harmless only because no
plan reads it.

### 1c. `hasVulnerabilityIndicators: false` and CHK-010's near miss

CHK-010's `triggerProbe.triggerField` **is** `checkTriggers.hasVulnerabilityIndicators`
(`CHK-010.query-plan.json:13`), and the model set it to `false`. The check ran anyway:

> No trigger was recorded; this check applies to every case and was assessed anyway.

**CHK-010 survived on its unconditional-overlay status, not because the trigger was right.** The model
records `hasVulnerability: "No"` with three characteristics, all quoted from the report, while the
file records four *"No understanding / knowledge"* answers and a −£288 monthly deficit — the FG21/1
capability and resilience indicators that F10.1 and F10.2 are about. Had CHK-010 been gated the way
CHK-005 is, **the two Highest-severity vulnerability findings would have been unreachable.**

Note what this means for the CHK-005 fix: the trigger that broke was fixed by a live extraction
(`hasCapitalContributionsOrWithdrawals: true`, confirmed in this artefact). The *class* of defect —
a trigger derived from the report alone, disabling a check about the file — is untouched.

---

## 2. What is not a gap, and it is most of the model

Every path flagged as a possible gap in the earlier analysis is populated:

| Path | State |
| --- | --- |
| `/needsAnalysis/lifeExpectancyBasis` | array(2) — populated |
| `/needsAnalysis/scenarios` | array(5), with `stressType` |
| `/needsAnalysis/assumptions` | array(10) |
| `/solution/providerSelection` | `providerName`, `rationale`, `financialStrengthRating` |
| `/solution/investmentStrategy` | 9 keys incl. `rationale`, `isCentralisedInvestmentProposition`, `managementStyle` |
| `/ongoingService` | 9 keys incl. `reviewFrequency`, `servicesIncluded` |
| `/costsAndCharges/valueAssessment` | `benefitsStated`, `costJustification`, `requiredOutperformance*` |
| `/replacementAnalysis` | array(4) |
| `/riskWarningsAndDisadvantages/lossScenariosIllustrated` | array(1) |
| `/vulnerability` | `perClient`, `foreseeableHarms` |

All 22 top-level sections are present and none is null. 384 of 608 provenance blocks carry a verbatim
`quote`. Modality is used properly and with discrimination — `Actual` 154, `Recommended` 37, `Assumed`
35, `Intended` 23, `Reported` 18, `Projected` 13, `Derived` 3, `Conditional` 1 — which is the field
doing real work rather than defaulting.

**The 26 `recommendation.reason` and `fund.reason` facts (43% of the spreadsheet) are all present**, as
`Statement` objects with topics and quotes. Nothing needs adding for them, confirming
[material-facts-gap-analysis §5](../../material-facts-gap-analysis.md).

### The extractor found six real contradictions on its own

`extractionReport.internalInconsistencies` has six entries, `overallConfidence` 0.82. Two are
benchmark findings:

> Page 7 states for Aviva … 'The plan itself is 0.06% more expensive than the recommended solution'
> but page 11 comparison table shows Aviva existing plan charge as 0.50% with recommended charge as
> 0.44% — **F7.1 / F6.3 territory**

> Page 5 existing arrangements table shows total pension values as … = £116,998.47. Page 3 text states
> 'combined val…' — **F1.8**

Plus the £230 weekly/annual mismatch (**F1.2's report-internal half**) and the knowledge/risk tension
(**F4.1**). `expectedButAbsent` correctly names `featuresOrGuaranteesLost` and
`riskAssessment/vulnerabilityFlags`.

**This is the strongest thing in the artefact.** The extractor is capable of stating a contradiction
in its own words when both sides are in front of it. Every one of these six is *report-versus-report*,
which is precisely the class it can see. The findings it cannot reach are the report-versus-file ones —
not because it reasons less well about them, but because it is only ever shown one side.

---

## 3. Revisions to the previous analysis

Stated plainly because both documents are now in the repository and the second corrects the first.

1. **The primary gap is source coverage, not schema surface.**
   [material-facts-gap-analysis §6 item 1](../../material-facts-gap-analysis.md) proposed
   `questionnaireResponses[]` as *"the only item that moves the benchmark."* The field is still the
   right shape, but **it cannot be populated from the suitability report**, because the questionnaires
   are in the risk profile report. Adding it without extending source coverage produces an empty
   array and a false sense of closure.
2. **F1.5 is not the cause of the John/Kim confusion.** The model has both names populated from the
   report. §1a.
3. **`lifeExpectancyBasis` is populated**, so the *"extracted and read by nothing"* item is real only
   on the reading side — the fact is published and no requirement reads it. That still matches the
   fact library's own stated defect, and item 2 of the previous plan stands unchanged.
4. **`hasComplexProduct` and `hasVulnerabilityIndicators` are new findings** not present in either
   earlier document.

---

## 4. What to do

Two items, and the first is a change of scope rather than a change of field.

### 1. Decide whether the canonical model is report-only, then make the decision explicit

This is a design question and should be answered before more fields are added. Either:

**(a) The model stays report-only.** Then it is a *report* model, `source` is correctly singular, and
the pipeline's design is: canonical model = the assertions, retrieval = the evidence. That is
defensible and coherent. But it means **six benchmark findings are permanently retrieval-dependent**,
and the correct investment is in retrieval reliability — section hints, reserved slots, routing — not
in extraction. It also means the *"no date of birth extracted"* complaint is expected behaviour and
should be worded as such rather than as a defect.

**(b) The model covers the file as well.** Then `source` becomes an array, provenance already carries
`documentId` and `documentCategory` to keep sides distinct, and the schema needs `questionnaireResponses[]`
plus a Fact Find representation. This is the larger change and the one that would let a check compare
two canonical values instead of a value against a passage.

**Recommendation: (a), stated explicitly, with one exception.** The evidence favours (a) — the
architecture works, 43% of the facts are covered, and the extractor demonstrably reasons well within
one document. The exception is the **questionnaire instruments**, which are structured tables whose
answers four benchmark findings turn on and which retrieval has now demonstrably failed to deliver
(F3.1 lost, F2.2 never caught). A narrow second extraction pass over the risk profile report,
populating `questionnaireResponses[]` only, buys the largest share of the benefit for the smallest
change to the design.

Until this is decided, **do not add fields** — a field with no reachable source is worse than no
field, by the fact library's own reasoning.

### 2. Make a trigger that contradicts its own model fail loudly

Three triggers in one artefact disagree with the data beside them: `hasComplexProduct: false` against
`complexity: "Complex"`, and `hasVulnerabilityIndicators: false` against a file full of FG21/1
indicators. One was harmless because no plan reads it; one was harmless because CHK-010 is
unconditional. **CHK-005 was not harmless, twice.**

Add a post-extraction consistency check: where a trigger is `false` and the model contains data
implying it should be `true`, print it and refuse to gate a check on it. The pairs are known and few —
`hasComplexProduct` against `recommendedProductComplexity[].complexity`,
`hasCapitalContributionsOrWithdrawals` against `existingArrangements[].contributions`/`withdrawals`,
`hasVulnerabilityIndicators` against `vulnerability.perClient[]`.

This is the *"make a disabled check loud"* recommendation from Runs 15, 16 and 17, narrowed to a
mechanical test rather than a reporting preference.

---

## 5. Limits

- **One extraction, one case.** Every count here is from this artefact. The report-only source
  pattern is confirmed by the `source` schema being a single object, so that generalises; the specific
  omissions may not.
- **`documentId: "…pdf"` where the indexed corpus is `.md`.** The model cites the PDF and the
  retrieval reads converted Markdown. Consistent within the model, but it means a citation cannot be
  followed to the file the pipeline actually read — the same mismatch noted in
  [implementation-list item 5](implementation-list.md).
- **The 16 `G_Research` provenance blocks are unexplained.** A research document is cited 16 times
  while `source` names only the suitability report. Either the report reproduces research content and
  the extractor attributed it correctly, or a second document was in scope. Not determinable from this
  artefact.
- **335 of 609 provenance blocks name no document.** They carry `assertionStatus` and often a quote.
  Whether the extractor omitted the id or the schema permits a bare assertion was not established
  here, and it affects the 274 figure: the true report-only share is 100% of *attributed* blocks, not
  100% of all blocks.
- **No claim that fixing source coverage fixes recall.** Run 17's losses were routing (F3.1),
  wrong-question (F3.4) and wrong-route (F4.2, F7.3, F9.5) failures. Source coverage addresses F2.2,
  F1.6, F5.4 and F1.9 — findings that have never been Caught in seventeen runs — and would make F3.1
  and F3.5 robust. It does not address the others.
