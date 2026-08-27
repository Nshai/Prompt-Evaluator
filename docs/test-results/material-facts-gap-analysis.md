# Material facts gap analysis — the 60 re-extracted facts against the implementation

An analysis of [ReExtracted_Material_Facts_Gap_Analysis.xlsx](../ReExtracted_Material_Facts_Gap_Analysis.xlsx)
against the canonical model schema, the extraction passes, the ten query plans and the evidence of
[Run 17](Runtime-Logs/latest/Run-17/run-analysis.md).

---

## The finding

**The spreadsheet's 60 facts are almost all already extractable, and the one class that is not is the
class three benchmark findings depend on.**

The headline count is reassuring and misleading. Of the 21 `factTypeId` groups, 20 have a canonical
home and are read by at least one query-plan group; only `/needsAnalysis/lifeExpectancyBasis` is
extracted and read by nothing. On a naive reading the coverage gap is 1 in 21.

That reading is wrong, and the reason it is wrong is the point of this document. **The schema holds
the *conclusions* of the risk and knowledge assessments and has no field anywhere for the
*questionnaire answers those conclusions were drawn from*.** `grep` for
`questionnaireResponses|questionnaireAnswers|answers|responses` over the 128 KB schema returns
**zero**.

So the pipeline records:

| What the model holds | What the file says | Where the answers live |
| --- | --- | --- |
| `attitudeToRisk.assessedCode: 4` | *"Cautious"*, *"More cautious"*, *"Low risk investments"*, anxiety at a 20% fall | **retrieval only** |
| `knowledgeAndExperience.statedLevel: Moderate` | **four** × *"No understanding / knowledge"* | **retrieval only** |
| `capacityForLoss.rating: Medium` | *"My spouse… is likely to be able and willing to support me: Disagree"* | **retrieval only** |

**F3.1, F3.5, F4.1 and F4.2 — four of the 36 material benchmark findings, three of them Highest
severity — are reachable only if a passage carrying a questionnaire row happens to survive into the
right group's pack.** That is not a hypothetical fragility. It is exactly how F3.1 was lost in Run 17:
the file-note passage reached eight group prompts and no CHK-003 group, and the finding the pipeline
had held for five consecutive runs disappeared. A canonical path cannot be evicted from a pack; a
passage can.

**The gap is therefore not "60 facts are missing".** It is that the facts most load-bearing for the
benchmark are carried by the weakest transport the pipeline has.

---

## 1. What the spreadsheet actually is

60 rows, one sheet (*Comprehensive Missing Facts*), columns
`factTypeId · statement · sourceQuote · value · unit · currency · modality · subjects · page ·
extractionConfidence`.

Every row is a **narrative** fact — a reason, a rationale, a client preference, an awareness
confirmation — as distinct from the numeric data the existing 44-fact library is built around. The
distribution says so plainly:

| factTypeId | n | | factTypeId | n |
| --- | :-: | --- | --- | :-: |
| `recommendation.reason` | **16** | | `retirement.preference` | 2 |
| `fund.reason` | **10** | | `recommendation.ancillary` | 2 |
| `knowledge.experience` | 4 | | `life.expectancy` | 2 |
| `risk.attitude-detail` | 3 | | `risk.awareness` | 2 |
| `provider.selection` | 3 | | `risk.personality` | 2 |
| `cashflow.outcome` | 3 | | `stress.test` | 2 |
| 9 singletons | 9 | | | |

**26 of 60 — 43% — are `recommendation.reason` or `fund.reason`.** Those are the two things the
schema is best at: `Statement` (`text` + `topic` + `isClientSpecific` + `provenance.quote`) appears in
roughly 30 places, and `/recommendations[]/rationale[]`, `/solution/investmentStrategy/rationale[]`
and `/existingArrangements[]/adviceActionRationale[]` all exist and are all read.

The spreadsheet's own taxonomy is **not** the fact library's taxonomy — `factTypeId` values like
`risk.attitude-detail` and `fund.reason` do not appear among the 44 ids in
[fact-library.map.json](../artifacts/fact-library.map.json). Any reconciliation has to be by meaning,
not by key, which is why this was done by hand rather than by diff.

---

## 2. Coverage, honestly counted

Three questions per fact group, in the order that matters: can the schema hold it, does an extraction
pass populate it, does a query-plan group read it.

| factTypeId | Schema path | Extracted? | Read by a plan? | Verdict |
| --- | --- | :-: | :-: | --- |
| `risk.attitude-detail` | `/riskAssessment/perClient[]/reconciliation/{hasOverride,direction,fromCode,toCode,justification[],clientAcknowledgement[]}` | yes | yes | **Covered** |
| `capacity-for-loss` | `…/capacityForLoss/assessmentBasis[]` | yes | yes | **Covered** |
| `knowledge.experience` | `…/productExperience[]`, `/statedLevel`, `/understandingEvidence[]` | yes | yes | **Covered** (but see §3) |
| `objective.retirement` | `/objectives[]/{objectiveType,summary,details}` | yes | yes | **Covered** |
| `objective.structure` | `/objectives[]/{summary,details}` | yes | yes | **Covered** |
| `recommendation.reason` (16) | `/recommendations[]/rationale[]`, `/benefits[]`; `/existingArrangements[]/adviceActionRationale[]`; `/replacementAnalysis[]/benefitsOfSwitching[]` | yes | yes | **Covered** |
| `recommendation.action` | `/recommendations[]/action`, `/existingArrangements[]/adviceAction` | yes | yes | **Covered** |
| `fund.reason` (10) | `/solution/investmentStrategy/rationale[]`, `/isCentralisedInvestmentProposition`, `/managementStyle`; `/costsAndCharges/valueAssessment/costJustification[]` | yes | yes | **Covered** |
| `provider.selection` | `/solution/providerSelection/rationale[]` | yes | yes | **Covered** |
| `cashflow.outcome` | `/needsAnalysis/scenarios[]/{outcome,description}` | yes | yes | **Covered** |
| `stress.test` | `/needsAnalysis/scenarios[]/{scenarioType,stressType,outcome}` | yes | yes | **Covered**, split — §4 |
| `benefit.death` | `/solution/recommendedPlans[]/deathBenefits[]` | yes | yes | **Covered** |
| `recommendation.ancillary` | `/solution/implementation/nextSteps[]`, `/recommendations[]/action = NonProductAction` | yes | yes | **Covered** |
| `review.commitment` | `/ongoingService/{reviewFrequency,servicesIncluded[]}` | yes | yes | **Covered**, unscoped — §4 |
| `risk.objective` | `…/capacityForLoss/assessmentBasis[]` (nearest) | yes | yes | **Partial** — no reliance field |
| `risk.time-horizon` | `…/investmentTermYears`, `/objectives[]/termYears` | yes | yes | **Partial** — number, no linkage narrative |
| `risk.strategy` | `/solution/investmentStrategy/rationale[]` | yes | yes | **Partial** — filed under the strategy, not the profile |
| `risk.awareness` | `…/understandingEvidence[]` | yes | yes | **Partial** — shares a field with comprehension evidence |
| `retirement.preference` | `/existingArrangements[]/retirementOptionsAvailable[]`, `/recommendedPlans[]/wrapper` | yes | yes | **Partial** — records *capability*, not *preference* |
| `life.expectancy` | `/needsAnalysis/lifeExpectancyBasis[]` | yes | **no** | **Gap** — §4 |
| `risk.personality` | — | — | — | **Absent** — §3 |

**14 Covered · 5 Partial · 1 unread · 1 Absent.**

**A caution on how this table was produced.** A first pass using exact-string matching on plan files
reported 13 canonical nodes as read by nothing. That was an artefact of the matching: the plans
declare ancestors, and `FactLibraryCoverageTests.Related` (`tests/…/FactLibraryCoverageTests.cs:70`)
treats a group naming `/recommendations[]` as reading everything beneath it. Re-running the
reconciliation with the same ancestor rule the test uses gives **12 of 13 read**. The one that
survives is `lifeExpectancyBasis`. Any future audit of this kind must use the ancestor rule or it will
manufacture gaps.

---

## 3. The gap that matters: no questionnaire answers in the model

**This is the whole recommendation of this document. The other items are tidying.**

`risk.personality` is the only Absent verdict, and on its own it looks trivial — two facts, *"not
particularly cautious"* and *"no strong positive or negative associations with risk"*, which are
Balanced-investor **description** text rather than client data. Fixing that alone would be worth
almost nothing.

The real finding is what the Absent verdict is a symptom of. `AttitudeToRisk`
(schema line 1531) carries `assessedCode`, `agreedCode`, `scaleName`, `scaleMin`, `scaleMax`,
`label`, `description`, `assessedOn`, `methodology`, `provenance` — **ten scalar fields and no array.**
`KnowledgeAndExperience.perClient[]` carries `statedLevel` (an enum: `None|Limited|Moderate|Good|
Extensive|Unspecified`), `productExperience[]` (`productType` + `hasHeld` + `yearsExperience`) and
`understandingEvidence[]`.

**Nowhere in 128 KB of schema is there a field for a questionnaire answer.**

The consequence is visible in Run 17's own output. The extraction recorded:

> Report states client knowledge level is **'Moderate'**

against a file that answers *"No understanding / knowledge"* to **four** separate questions. The
questionnaire rows reached the assessor as retrieved passage `[P16]` and `[P9]`/`[P10]`, and CHK-004
caught the contradiction — this time. F3.5 was caught the same way, from `[P16]`. **F3.1 was not**: the
file note's ATR paragraph reached eight groups and no CHK-003 group, and a finding held for five runs
was lost.

**That is the asymmetry.** A canonical path is delivered to every group whose plan names it,
deterministically, and cannot be evicted. A passage competes for one of 24 slots on embedding rank.
Four benchmark findings — F3.1, F3.5, F4.1, F4.2, three of them Highest — currently ride on the
second mechanism when the first exists and is unused for this data.

**The fix is a questionnaire-response array, populated verbatim, read by CHK-003 and CHK-004.** Not a
personality field. §6 item 1.

Two secondary points reinforce it:

- **`statedLevel: Moderate` is a lossy summary of a contradiction.** The extraction was asked for a
  level and produced one; the four `No understanding / knowledge` answers were the evidence *against*
  that level and had nowhere to go. An extraction that must choose a code will always erase the
  disagreement that a QA check exists to find.
- **The same shape applies to capacity for loss.** `capacityForLoss.rating: Medium` is recorded, and
  Run 17's G3.7 noted that *"no passage in the file uses the term 'Medium'"*. The CFL questionnaire's
  *"My spouse… is likely to be able and willing to support me financially: **Disagree**"* (benchmark
  F3.7) and its *"Less than 3 years"* horizon (**F2.2**, Highest, Partial in every run) are
  questionnaire answers with no canonical home. F2.2 has never been Caught, and the reason is now
  legible: **the run reaches the 2-year conflict from the risk profile report every time, because
  that is the only route it has.** The rubric scores that Partial under R4 — right conclusion, wrong
  evidence.

So a questionnaire-response array is not one fix for one finding. It is the missing transport for
**F2.2, F3.1, F3.5, F3.7, F4.1 and F4.2**, four of which are in the scored set and two of which have
never been Caught in seventeen runs.

---

## 4. The smaller gaps, worth doing and worth not overselling

**`lifeExpectancyBasis` is extracted and read by nothing.** The one genuine
publish-without-a-reader in the set — the exact defect
[fact-library.map.json](../artifacts/fact-library.map.json)'s own `$comment` says is *"worse than not
extracting it at all, because the report shows it as an established fact."* It is also text-only
(`Statement[]`), with no `clientId` and no `assumedAge`, so the spreadsheet's *"male to 86 / female to
88"* can only be prose and no check can reconcile it against `/parties/clients[]/dateOfBirth`. Run 17
mentions age 86 twice and age 88 once, all from retrieval.

This is **not** a benchmark finding. It is on the list because it is a rendered fact nobody checks,
and because the fix is small.

**Stress-test inputs are split across three nodes with no key joining them.** The scenario shape is
`/needsAnalysis/scenarios[]/{scenarioType,stressType}`, the 6% inflation rate is
`/needsAnalysis/assumptions[]`, and the −30% is
`/riskWarningsAndDisadvantages/lossScenariosIllustrated[]/lossPercentage`. Nothing carries a
`scenarioId`, so *"high-inflation stress test at 6% passed"* cannot be reassembled deterministically —
it can only be inferred by an assessor reading three arrays. Benchmark **F8.3** (Moderate, unscored)
is adjacent: *"the −30% stress test is applied to the cashflow, not to the investment."* A join key
would let a check state that mechanically.

**Client preference is not modelled.** `retirementOptionsAvailable[]` is provider capability and
`recommendedPlans[]/wrapper` is the product chosen; neither records that the client *prefers*
Flexi-Access Drawdown or *wants control over benefit timing*. Both spreadsheet facts are real client
assertions and both currently land, if at all, in `/objectives[]/summary`. Low priority — no benchmark
finding turns on it — but it is a genuine modelling gap and cheap to close.

**`review.commitment` has no scope.** `/ongoingService/reviewFrequency` is a `Frequency` enum for the
service as a whole; *"your risk profile will be continually reviewed"* is a commitment about a
specific subject. `servicesIncluded[]` can hold the words and loses the subject.

**Three Partial verdicts need no schema change at all** — `risk.objective`, `risk.time-horizon`,
`risk.strategy`. Each has a `Statement[]` home that will hold the text; the imprecision is that the
statement lands under a neighbouring concept. Recording them is better than not, and a new field for
each would add schema surface for no measurable gain. **Left alone deliberately.**

---

## 5. What is *not* a gap, and why that matters

Stating this explicitly because the spreadsheet's framing — *"Comprehensive Missing Facts"* — invites
a plan to add 60 fields, and that would be the wrong plan.

- **26 of the 60 facts (43%) are `recommendation.reason` and `fund.reason`.** Every one has a
  `Statement[]` home, every home is populated, every home is read. Run 17 quotes CIP governance,
  quarterly investment committee, external research team, competitive charges, customer service and
  financial strength — all of it reached the output. **This is the largest block in the spreadsheet
  and it needs nothing.**
- **The `Statement` type is why.** `text` + `topic` + `isClientSpecific` + `provenance.quote` is
  general enough to carry a reason without a bespoke field per reason. A schema of enums and numbers
  would have failed all 26; this one does not.
- **`beneficiary` is in the schema and in four plan files.** An earlier pass reported it absent; that
  was a `grep` error, corrected here.
- **`provider.selection`, `cashflow.outcome`, `benefit.death`, `recommendation.ancillary` are all
  fully covered** and all appear in Run 17's output.

**The corollary is uncomfortable and should be said.** If 20 of 21 groups are extractable and read,
and the benchmark still shows 6 Missed and 7 Partial in Run 17, then **most of the remaining recall
gap is not a facts-coverage problem.** Run 17's own losses were a routing failure (F3.1), a
wrong-question failure (F3.4), and four right-conclusion-wrong-route failures (F4.2, F7.3, F9.5,
F8.1). Only F2.2 and the questionnaire-answer group in §3 are genuinely a modelling gap. **Adding
fields will not fix the others, and a plan that implies it would is a plan that spends effort in the
wrong place.**

---

## 6. The plan

Four items. Item 1 is the whole of the value; items 2–4 are cheap and worth doing alongside it.
Sequenced so the measurement stays interpretable.

### 1. A questionnaire-response array — the only item that moves the benchmark

**Schema.** Add to `riskAssessment.perClient[]` and to `knowledgeAndExperience.perClient[]`:

```
questionnaireResponses[]:
  instrument     — AttitudeToRisk | CapacityForLoss | InvestorExperience   (required)
  question       — verbatim question text                                  (required)
  answer         — verbatim answer text, NOT normalised to a code          (required)
  answeredOn     — date
  provenance     — existing Provenance type (document, page, quote)
```

**`answer` must be verbatim and must not be mapped to an enum.** The entire value of this field is
that it preserves what the code erases. A vocabulary would recreate the `statedLevel: Moderate`
problem one level down.

**Extraction.** Pass 4 (*Risk, knowledge and vulnerability*) already owns both parents — no new pass,
no change to the 12-pass split. The prompt in `Constants/Prompts.cs` must be told to transcribe every
questionnaire row it finds, and explicitly **not** to summarise, deduplicate or reconcile them against
`statedLevel` / `assessedCode`. Add to `docs/artifacts/fact-library.map.json` as
`risk.questionnaire-responses` and `knowledge.questionnaire-responses`, which makes
`FactLibraryCoverageTests` enforce that a reader exists.

**Query plans.** Add `canonicalPaths` to:

| Group | Path | Finding |
| --- | --- | --- |
| `G3.6` risk override justification | `/riskAssessment/perClient[]/questionnaireResponses` | **F3.1**, F3.5 |
| `G3.8` conflicting risk scores | same | F3.5 |
| `G3.2` capacity for loss | same, `instrument = CapacityForLoss` | F3.7, **F2.2** |
| `G3.4` time horizon | same | **F2.2**, F3.3 |
| `G4.1` stated knowledge level | `/knowledgeAndExperience/perClient[]/questionnaireResponses` | **F4.1**, F4.2 |
| `G4.4` product too complex | same | F4.1 |

**This complements the section hint in
[Run 17 implementation-list item 1](Runtime-Logs/latest/Run-17/implementation-list.md), it does not
replace it.** The hint gets the file-note *narrative* into G3.6's pack; the canonical path gets the
questionnaire *answers* there deterministically. F3.1's contradiction needs the narrative (the
sentence containing "6" is prose in a file note, not a questionnaire row), so the hint remains the
primary fix for F3.1 and this is the belt to its braces. **Do both, in one commit, and attribute
carefully** — see §7.

**Done when.** `/riskAssessment/perClient[]/questionnaireResponses` holds the four cautious ATR
answers and the CFL *"Less than 3 years"* and *"Disagree"* rows verbatim;
`/knowledgeAndExperience/perClient[]/questionnaireResponses` holds the four *"No understanding /
knowledge"* rows; and a test pins that the extraction does not collapse them to a level. **Target:
F2.2 Partial → Caught** (it has never been Caught), with F3.5, F4.1 and F4.2 made robust rather than
retrieval-dependent.

### 2. Give `lifeExpectancyBasis` a reader and a structure

Add `clientId` and `assumedAge` (integer) beside the existing `Statement[]`, and
`canonicalPaths: ["/needsAnalysis/lifeExpectancyBasis"]` to the CHK-002 group that assesses time
horizon and the CHK-005 group that assesses cashflow duration. Closes the one genuine
publish-without-a-reader, and makes the assumed age comparable against
`/parties/clients[]/dateOfBirth` arithmetically rather than in prose.

Do **not** claim a benchmark finding for this. It recovers none.

### 3. A join key across stress-test inputs

Add `scenarioId` to `/needsAnalysis/scenarios[]`, and an optional `scenarioId` reference on
`/needsAnalysis/assumptions[]` and
`/riskWarningsAndDisadvantages/lossScenariosIllustrated[]`. Lets one group state *"the −30% is applied
to the cashflow, not to the recommended funds"* (**F8.3**, Moderate) from the model instead of
inferring it across three arrays.

### 4. Client preference, and a scope for review commitments

`/objectives[]` gains `statedPreferences[]` (`Statement[]`) for *"prefers Flexi-Access Drawdown"* and
*"wants control over benefit timing"*; `/ongoingService` gains `reviewCommitments[]` with a `scope`
field. Both are modelling correctness rather than recall. Lowest priority; skip if time is short.

### Explicitly not doing

- **A `risk.personality` field.** The two facts are Balanced-investor *description* text, not client
  data, and `attitudeToRisk.description` already holds it. Adding a `traits[]` array would model the
  profiler's prose, which no check needs.
- **New fields for `risk.objective`, `risk.time-horizon`, `risk.strategy`.** §4 — the `Statement[]`
  homes are adequate and the imprecision costs nothing measurable.
- **Anything for the 26 `recommendation.reason` / `fund.reason` facts.** §5 — fully covered, fully
  read, demonstrably reaching the output.

---

## 7. Sequencing, and how not to lose the measurement

**Every item here changes the query plans, and the plans digest is what defeated the extraction
cache in Run 17.** Each change will therefore also force a fresh canonical model, which is the
confound Run 17's analysis could not remove. Consequences:

1. **Run the `8 / 24` Haiku baseline first** — item 8 on
   [Run 17's list](Runtime-Logs/latest/Run-17/implementation-list.md), still the cheapest thing on
   either page and the only way the rest becomes interpretable.
2. **Land item 1 alone**, not with the section hints. Both target F3.1 and both target CHK-003; landed
   together, a recovery cannot be attributed and the project acquires another uncontrolled
   comparison. Item 1's distinctive prediction is **F2.2**, which the section hint cannot affect —
   watch that one to tell them apart.
3. **Then items 2–4 in one commit.** None is expected to move the scored set, so they can share a run.

**Expected effect, stated in advance so it can be wrong.** Item 1 should move **F2.2 to Caught** and
make F3.5/F4.1/F4.2 robust without changing their verdicts. It should **not** by itself recover F3.1 —
the section hint is the fix for that. If recall moves by more than two findings, the noise floor
(±2, measured) means the excess needs explaining before it is believed.

---

## 8. Limits

- **One case.** All 60 facts come from case ABC-99's suitability report. A field justified by one
  document may be idiosyncratic to it, and `questionnaireResponses` is the only item here whose need
  is corroborated independently — by four benchmark findings and by seventeen runs of F2.2 never
  being Caught.
- **The spreadsheet's provenance is not established.** `extractionConfidence` runs 0.92–0.99 with no
  stated method, and the `page` column cites the suitability report only. Whether these 60 were
  produced by a different extractor, a different prompt or by hand is not recorded in the file and
  was not determinable from it. **Nothing in this plan depends on that**, because each fact was
  re-checked against the schema directly.
- **"Extracted?" was verified against Run 17's extraction log, one run.** A path appearing there is
  evidence the extraction *can* populate it, not that it always will — and Run 17 itself showed a
  field silently going missing (the client's date of birth).
- **The §2 table's read-column depends on the ancestor rule.** With exact matching it would read 12
  gaps instead of 1. The ancestor rule is the one the build test uses, so it is the right rule, but
  the two answers differ enough that any citation of this table should say which was used.
- **No claim is made that closing these gaps addresses the recall shortfall.** §5 is explicit that
  most of Run 17's losses were routing and verification failures, not missing facts. This document
  fixes what it can see; it does not claim that is most of what is wrong.
