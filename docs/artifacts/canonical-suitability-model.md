# Canonical Suitability Model (CSM) v1.0

**Purpose** — a single, template-agnostic data model that an LLM can populate by parsing a suitability report/letter of any format, and that the QA engine can then evaluate the ten assessment checks (CHK-001 … CHK-010) against.

**Inputs analysed**

- `quality-checks/Assessment Checks & Prompts QA CA v1.0 (Checks).csv` — 10 checks, each with prompt, "what to look for" and decision logic
- `quality-checks/Assessment Checks & Prompts QA CA v1.0(Document Categories v1.csv` — evidence taxonomy A–I
- `example-documents/I/Suitability Report Test 1.md` — worked example (pension consolidation, 5 ceding plans)
- `consolidated-swagger.json` — Intelliflo Office API (1,869 schemas), used as the naming and enumeration authority

---

## 1. The architectural decision that shapes everything else

Read the checks carefully and a pattern falls out. Nine of the ten are phrased identically:

> *"Where the Suitability Report includes X, is it **consistent with the evidence provided**?"*

The checks are not asking "is the advice good?" — they are asking **"does what the report says match what the file says, and is what it says appropriate?"** Every check decomposes into two limbs, and the CSV labels them explicitly:

| Limb | Question | Nature |
|---|---|---|
| **Consistency** | Do the report's assertions match the evidence in categories A–H? | A *diff* between two datasets |
| **Appropriateness** | Given those facts, is the recommendation defensible? | A *rule/judgement* over one dataset |

That drives the central design choice:

> **One canonical model, populated twice.** The suitability report (category I) is parsed into a CSM instance. The supporting evidence (categories A–H) is parsed into a second CSM instance. The consistency limb of every check becomes a **field-level comparison of two CSM instances**; the appropriateness limb becomes **rules evaluated over the merged instance**.

This is why the model is not a "suitability report schema". A suitability-report-shaped schema (sections, headings, narrative blocks) would be useless for the consistency limb, because the evidence documents have no sections or headings in common with it. The model is instead shaped around **the advice case itself** — the client, their money, their goals, their risk profile, their existing plans, the recommendation — which is the one structure both sides share.

Three consequences follow:

1. **Nothing in the model is named after a report section.** There is no `attitudeToRiskSection`; there is `riskAssessment.perClient[].attitudeToRisk`. Template variation is absorbed at extraction time, not modelled.
2. **Every entity carries provenance.** A QA finding must cite a page and a quote or it cannot be actioned by a reviewer. See §3.
3. **Absence is a first-class value.** The decision logic has three outcomes — *No Issue*, *Potential Concern*, *N/A: trigger absent*. "The report never mentions lost guarantees" and "the extractor missed it" are different findings, so the model records both (`assertionStatus: Absent` and `extractionReport.expectedButAbsent[]`).

---

## 2. Model structure

Twelve top-level aggregates, each traceable to the document categories and checks that consume it.

```
CanonicalSuitabilityModel
├── source                     · what was parsed (documentId, category, type, page count)
├── case                       · adviceType, adviceBasis, adviser, firm, reportDate   → IO ServiceCase
├── parties
│   ├── clients[]              · person, tax status, health, employment, state pension → IO Client/PersonValue
│   ├── dependants[]                                                                   → IO Dependant
│   └── professionalContacts[]
├── clientAuthority            · [Cat A] agreement, services, fee basis, consents     → IO ClientAgreement
├── financialPosition          · [Cat B] incomes, expenditures, assets, liabilities,
│                                 totals, emergencyFund, liquidityAndAccessNeeds,
│                                 affordabilityAssessment, foreseeableChanges         → IO Income/Expenditure/Asset/Liability
├── objectives[]               · [Cat B/F] goals with priority, term, target, horizon → IO BaseObjective + subtypes
├── riskAssessment             · [Cat D] ATR (assessed vs agreed), CFL, tolerance,
│                                 investment term, reconciliation                     → IO AtrChosenRiskProfile
├── knowledgeAndExperience     · [Cat D] stated level, product experience,
│                                 comprehension evidence, product complexity
├── vulnerability              · [Cat B/C] FG21/1 drivers, support needs, adjustments,
│                                 third-party authority, foreseeable harms            → IO ClientVulnerabilityDocument
├── existingArrangements[]     · [Cat E] plans with value, transfer value, charges,
│                                 holdings, risk rating, safeguarded benefits,
│                                 penalties, retirement options, adviceAction         → IO BasePlan/PlanValuation/PlanCharge
├── needsAnalysis              · [Cat F] cashflow scenarios, stress tests, shortfalls,
│                                 assumptions
├── research                   · [Cat G] optionsConsidered[], comparisons[]
├── recommendations[]          · [Cat I] rationale, benefits, disadvantages, risks,
│                                 tax, alternatives, linked objectives                → IO Recommendation2Document
├── solution                   · [Cat H] recommendedPlans[], investmentStrategy,
│                                 providerSelection, documentsProvided                → IO BasePlan/PortfolioModel
├── costsAndCharges            · layered ChargeSets, adviserFees, comparison, RIY,
│                                 valueAssessment, disclosureCompleteness             → IO PlanFee/PlanCharge/FundCharges
├── replacementAnalysis[]      · one per ceding plan: comparison, benefits, costs,
│                                 featuresLost, alternatives, netBenefit
├── riskWarningsAndDisadvantages · risks, disadvantages, loss scenarios, prominence
├── taxTreatment               · consequences, allowances, IHT
├── ongoingService             · service level, cost, frequency, opt-in
├── checkTriggers              · derived booleans that scope which checks apply
└── extractionReport           · expectedButAbsent[], ambiguities[], internalInconsistencies[]
```

### Design rules applied

**Facts and narrative are separated.** A suitability report is roughly 20% structured data and 80% prose, and the checks test both. Structured facts get typed fields (`Money`, `Percentage`, enums). Prose that must be *present, specific and consistent* is captured as `Statement` objects — `{ text, topic, isClientSpecific, linkedObjectiveIds, provenance }`. The `topic` field normalises free text into a comparable label ("LowerCosts", "LossOfGuarantee"), which is what makes "did the report explain X?" answerable. `isClientSpecific` is the direct machine test for the CHK-006 requirement for *"personalised language linked to individual circumstances"* — boilerplate paragraphs score `false`.

**Assessed vs agreed is modelled everywhere it can diverge.** `AttitudeToRisk` carries both `assessedCode` (questionnaire output) and `agreedCode` (what was adopted), plus a `RiskReconciliation` block. The example report increases the client from 4 to 5 against the questionnaire result; without a dedicated override structure that CHK-003 concern is invisible to a field comparison, because a single `riskLevel: 5` field would match the evidence bundle's "5" perfectly while hiding the whole issue.

**Presence flags where the check tests disclosure completeness.** `costsAndCharges.disclosureCompleteness` is a set of booleans, one per cost layer, because CHK-007's second limb asks whether *"all material initial, ongoing, product, platform, investment transaction costs and advice fees"* are included. Deriving that from a bag of charge lines is fragile; asserting it per layer is not.

**Money is never a bare number.** `Money` = `{ currency, amount, basis (Gross|Net), asAt, provenance }`. Half of CHK-001 is *"data mismatches and unsupported figures"*, and gross/net and as-at-date confusion is the single most common source of false mismatches between a report and a fact-find.

**Risk scales are not assumed comparable.** `ExistingArrangement.riskRating` carries `scaleName` and `isComparableToClientScale`. The example report shows existing plans rated 9, 4, 2, 7 and "4 (Standard Life rated)" against a client profile of 5 on a different scale. A naïve comparison would generate nonsense findings.

**Every ceding plan gets its own `ReplacementAnalysis`.** CHK-009 is per-switch, not per-case. Each named field (comparison, benefits, costs, `featuresOrGuaranteesLost`, alternatives, conclusion) mirrors one bullet in the check's "what to look for", so a missing element is detected by a null rather than by an LLM judging prose completeness.

**`checkTriggers` makes "N/A" computable.** Every check's decision logic ends in *"N/A: trigger absent"*. Those triggers are structural (is there a switch? are there contributions or withdrawals? is there a costs section?), so they are derived once at extraction time rather than re-judged by ten separate check prompts.

---

## 3. Provenance, confidence and absence

Every entity carries a `Provenance` block:

```json
{
  "assertionStatus": "Stated | Inferred | Derived | Absent",
  "documentId": "I/Suitability Report Test 1.pdf",
  "documentCategory": "I_RecommendationsAndAdvice",
  "pageNumbers": [4],
  "sectionHeading": "Attitude to Risk & Capacity for loss",
  "quote": "We agreed that you are a risk rating 5 rather than a 4 as per the questionnaire.",
  "confidence": 0.95,
  "extractionNote": "Questionnaire result inferred as 4 from the override sentence; no questionnaire score stated directly."
}
```

`assertionStatus` is the load-bearing field:

- **Stated** — explicit in the text. Full weight in a consistency comparison.
- **Inferred** — the LLM read it out of narrative prose (e.g. household expenditure of £1,700/month inferred from a paragraph, not a table). A mismatch against evidence should be reported at lower severity, because the inference itself may be the error.
- **Derived** — calculated by the extractor from other extracted values. Never treat as an independent assertion; a mismatch here is arithmetic, not advice failure.
- **Absent** — the model expected the field for this case type and it is not in the document. This is what turns most checks from "read and judge" into "test a null".

`quote` is mandatory for Stated and Inferred, and it is what a compliance reviewer sees next to a finding. A finding without a citation is not actionable, so the model refuses to represent one.

### A missing section is not an absent value

`Absent` means the extraction read the report and the report did not say it. A section whose
extraction pass **failed** means nobody read it. Those are opposite conclusions, and the model
carries no field that distinguishes them — a failed section simply has no key, and a check
reading a canonical path under it sees exactly what it sees for a value the report never
contained.

That is not hypothetical. In an observed run of case ABC-99, two passes failed on a single
stray closing brace apiece, taking `financialPosition` and `recommendations` with them — the
payload of eight of the ten checks, since CHK-006 reads 22 canonical paths under
`/recommendations` and CHK-001 reads 21 under `/financialPosition`. Every one of those checks
still ran, because their triggers live in `checkTriggers`, which succeeded, and would have
reported the data as absent from the file: a finding about the adviser, caused by a bracket.

Two things now stand between that failure and a false finding:

- **A malformed reply is retried once, with a different prompt.** A reply that is *complete but
  mis-nested* is a different animal from one cut off at the token cap: nothing was lost, one
  bracket is in the wrong place, and the same prompt run again normally lands — provided it is
  not the same prompt. An observed retry re-sent the identical question and the gateway served
  the identical failed reply from cache: 41,580 characters, the same stray brace at the same
  offset, in the same second, billed twice and lost anyway. The retry now carries a second-attempt
  note naming the two faults seen in practice, varied by the digest of the reply that failed —
  which differs from attempt one, as the cache key needs, while staying reproducible for a run
  that pins its sampling. A retry that still comes back byte-identical is reported as a cached
  failure rather than as a second independent one. Repairing the brackets was the
  alternative and was rejected — where an unmatched closer belongs is a guess, and a wrongly
  re-nested fragment is worse than a second call, because it would be merged and believed.
  Both attempts are counted in the cost.
- **A failed section is named to the self-report pass.** That pass is shown a summary of what
  the previous passes produced, and it was built by walking the keys present in the model —
  so a failed pass, having written no key, was neither populated nor empty. It was invisible.
  The run above told it *"Sections that came back empty: none"* while two sections were
  missing outright, and the resulting `extractionReport` mentioned neither at 0.78 confidence.
  Failed sections now appear on their own line, and the pass is told to record them as
  `expectedButAbsent` with reason `PresentButUnparseable` and to lower its confidence.

- **A repeated property name keeps the section instead of losing it.** The third failure mode,
  and the one that hid best: a duplicate key *parses*. `JsonNode.Parse` accepts it and builds its
  dictionary lazily, so the reply came back non-null, the reader called it well formed, nothing
  retried, and it detonated at whatever line first asked the object for its count. Three
  consecutive runs lost `existingArrangements` to it — five pension plans, their values, charges,
  risk ratings and advice actions, and with them the identifier table every later pass needed, so
  eight or nine cross-references dangled too. The reply was read twice and the model repeated a
  run of *two* properties verbatim, with identical values on both sides; the exception could only
  ever name the first, because it threw on it. The reader now keeps the **first** occurrence of
  each name — the model writes in document order, so a repeat is a stutter rather than a
  correction — and reports every one, saying whether the two values agreed.

What still does not exist is a way for a **check** to tell the two apart. The extraction report
records it; the canonical model does not. See [Known limitations](#8-known-limitations).

### Where provenance is *not* attached

Provenance sits at entity and statement level, not on every scalar — with the exception of `Money` and `Percentage`, which carry their own because financial figures are the most-cited values in the checks. Putting it on every scalar would triple token cost at extraction for no analytical gain.

---

## 4. Alignment with Intelliflo Office

The model reuses Office resource names and enumerations wherever an equivalent resource exists, so an extracted case can be reconciled against, or written back into, Office without a translation layer. Full field mapping in [io-resource-mapping.md](io-resource-mapping.md). Highlights:

| Canonical | Intelliflo Office schema | Notes |
|---|---|---|
| `case` | `ServiceCase` | reference, category, adviser, client, objectives, plans |
| `parties.clients[]` | `Client` + `PersonValue` + `TerritorialProfileValue` + `HealthProfileValue` | `clientCategory` enum taken verbatim |
| `parties.dependants[]` | `Dependant` | `relationshipType` enum verbatim |
| `clients[].employments[]` | `Employment` / `BaseEmployment` | `employmentStatus` enum verbatim |
| `financialPosition.incomes[]` | `BaseIncome` / `Income` | `frequency` enum verbatim |
| `financialPosition.expenditures[]` | `Expenditure` | `isEssential`, `isConsolidated`, `monthlyEquivalent` kept |
| `financialPosition.assets[]` | `Asset` | `assetType` enum verbatim |
| `financialPosition.emergencyFund` | `EmergencyFund` | committed/required/shortfall |
| `objectives[]` | `BaseObjective` + `RetirementObjective`/`InvestmentObjective`/… | `objectiveType` = IO `discriminator`; `timeHorizon` enum verbatim |
| `riskAssessment…attitudeToRisk` | `AtrChosenRiskProfile` / `AtrGeneratedRiskProfile` | chosen vs generated is exactly the agreed-vs-assessed split |
| `vulnerability.perClient[]` | `ClientVulnerabilityDocument` | categories, notes, assessedOn |
| `existingArrangements[]` | `BasePlan` (+ `PensionContributionDrawdownPlan`) | policyNumber, productProvider, latestValuation |
| `…currentValue` / `transferValue` | `PlanValuation.value` / `.surrenderTransferValue` | |
| `…holdings[]` | `GetPlanHolding` + `Fund` + `FundCharges` | ISIN/SEDOL/citicode, OCF, transaction costs |
| `recommendations[].proposalType` | `BaseRecommendation2Proposal.discriminator` | enum verbatim — a canonical recommendation maps 1:1 to an Office proposal |
| `recommendations[]` | `Recommendation2Document` | requirements → `linkedObjectiveIds`, proposals → recommendation set |
| `solution.investmentStrategy` | `PortfolioModel` | `managementStyle` (Active/Passive), benchmark, allocations |
| `costsAndCharges.…lines[].chargeType` | `PlanCharge.type` | OneOffCharge/OngoingCharge/TransactionCost/IncidentalCost verbatim |
| `costsAndCharges.adviserFees[]` | `PlanFee` + `FeeType` + `FeePaymentType` + `FeeDetail` | `feeCategory` = InitialFee/OngoingFee/AdHocFee verbatim |

**Where the model deliberately departs from Office.** Office models *the current state of a client record*; the canonical model must represent *what one document claims at one point in time*, including claims that are wrong. So the model adds: provenance on everything, `assertionStatus`, the `Statement` type (Office has no representation of narrative rationale), `RiskReconciliation`, `ReplacementAnalysis`, `disclosureCompleteness`, `checkTriggers` and `extractionReport`. None of those belong in a CRM; all of them are required to run the checks.

---

## 5. Extraction guidance for the LLM

The model is designed to be filled by a multi-pass extraction rather than one prompt.

1. **Classify** — document type, case type, parties, dates. Populate `source`, `case`, `parties`.
2. **Harvest structured facts** — tables first (existing arrangements, charges, funds, cashflow figures). Tables carry the highest-confidence data and the report's own comparison tables map almost directly onto `ExistingArrangement` and `ChargeSet`.
3. **Harvest narrative** — rationale, disadvantages, risks, alternatives into `Statement[]`, each with `topic` and `isClientSpecific`.
4. **Link** — resolve `linkedObjectiveIds`, `sourceArrangementIds`, `targetPlanId`. Unresolvable links go to `extractionReport.ambiguities`.
5. **Derive** — `checkTriggers`, totals, `disclosureCompleteness`, and internal contradiction detection.

### Rules the extractor must follow

- **Never invent.** If a value is not in the document, emit `assertionStatus: "Absent"` and add an `expectedButAbsent` entry. A hallucinated figure produces a false "consistent" result, which is worse than a gap.
- **Prefer the narrative's own units.** Capture "£300 per week net" as `{ amount: 300, basis: "Net", frequency: "Weekly" }` and let `monthlyEquivalentNet` be `Derived`. Normalising at extraction time destroys the audit trail.
- **Record contradictions rather than resolving them.** The example report states pension totals of ~£110,000, ~£106,000 and a recommended transfer of £110,185 in three places. Those go in `extractionReport.internalInconsistencies` — they are direct CHK-001 findings and must not be silently reconciled.
- **One recommendation per discrete action.** "Switch Aviva, switch Zurich, switch Standard Life, retain People's Pension, encash Scottish Widows" is five `recommendations[]` entries, not one. Each carries its own rationale and alternatives because CHK-006 and CHK-009 assess them individually.
- **Boilerplate is data.** Generic paragraphs are extracted with `isClientSpecific: false` rather than skipped — their presence-without-personalisation is itself the CHK-006 finding.

---

## 6. Check coverage

Every check maps to a defined set of canonical paths. The full matrix — 85 rows, one per query-plan group — is [check-coverage-matrix.csv](check-coverage-matrix.csv), and it is **generated from the query plans** rather than maintained alongside them, so a group added to a plan cannot go unrecorded. Summary:

| Check | Primary canonical paths | Trigger |
|---|---|---|
| CHK-001 Suitability completeness vs client information | `parties.*`, `financialPosition.*`, `existingArrangements[]`, `extractionReport.internalInconsistencies` | always |
| CHK-002 Objectives, needs, time horizon | `objectives[]`, `financialPosition.liquidityAndAccessNeeds`, `.foreseeableChanges`, `recommendations[].linkedObjectiveIds` | always |
| CHK-003 Risk, CFL, investment strategy match | `riskAssessment.*`, `solution.investmentStrategy`, `existingArrangements[].riskRating` | investment/pension |
| CHK-004 Knowledge, experience, complexity | `knowledgeAndExperience.*`, `solution.recommendedPlans[]` | always |
| CHK-005 Resilience, affordability, liquidity | `financialPosition.affordabilityAssessment`, `.emergencyFund`, `.liquidityAndAccessNeeds`, `needsAnalysis.scenarios[]`, `costsAndCharges` | contributions/withdrawals/charges present |
| CHK-006 Rationale and alternatives | `recommendations[].rationale`, `.alternativesConsidered`, `.adviceStatus`, `.intendedOutcome`, `.implementation`, `.optionsPresented[]`, `research.optionsConsidered[]`, `solution.providerSelection.rationale`, `existingArrangements[].adviceAction` | always |
| CHK-007 Costs, charges, value | `costsAndCharges.*` (all branches) | always; second limb on `hasCostsAndChargesSection` |
| CHK-008 Disadvantages, risks, understanding | `riskWarningsAndDisadvantages.*`, `recommendations[].disadvantages`, `.risks`, `taxTreatment` | always |
| CHK-009 Replacement/switch justification | `replacementAnalysis[]`, `existingArrangements[].safeguardedBenefits`, `.exitPenalties`, `costsAndCharges.reductionInYield` | `checkTriggers.hasReplacementOrSwitch` |
| CHK-010 Vulnerability and foreseeable harm | `vulnerability.*` (overlay on all others) | always; enhanced on `hasVulnerabilityIndicators` |

### v1.0 extension for the revised checks

The revised CHK-006 asks four things the model could not express, so `Recommendation` gained fields for them. All are optional and nullable, and the worked example still validates unchanged.

| Field | Why the existing model could not answer it |
|---|---|
| `adviceStatus` | *Whether the adviser is recommending the change.* `action` says what the change is — Transfer, Switch, Retain — never whether it is advised. "You may wish to consider" is not a recommendation, and nothing in the model could record that distinction. `NotStated` is the value that produces the finding. |
| `intendedOutcome` | *The intended outcome of the change.* `rationale[]` argues why the recommendation is suitable; the intended outcome is what it is expected to produce. A report can carry one without the other, so they cannot share a field. |
| `implementation` | *Whether the adviser will arrange or implement the change.* `solution.implementation` records this for the case as a whole; CHK-006 asks it of each change, and a case mixing adviser-implemented and client-implemented actions collapses into one answer otherwise. |
| `optionsPresented[]` / `presentsUnresolvedOptions` | *Recommendations presenting multiple possible outcomes without saying which is recommended.* Options left open to the client had no representation at all — they read as an ordinary recommendation, which is exactly the failure the check is looking for. |

`RecommendationImplementation` and `PresentedOption` are the two new `$defs`. Both carry `provenance`, like every other evidenced structure in the model.

---

### Closed vocabularies, and where they are enforced

58 properties document a closed vocabulary in their `description` — `"One of: Pension, Investment, …"` — rather than as a JSON Schema `enum`. That is deliberate, and it was bought with a measured finding: an enum is enforced by `CanonicalModelValidator.StripEnumViolations`, which **deletes** values it does not recognise, and the values it deleted were the ones the documents actually use. Three runs in a row could not reach a charge finding because the `basis` strings never survived extraction.

Guidance drifts, though, and nothing was watching it. The extractor's rule said *"Enumerated fields must use a value from the schema's enum"* — pointing at a construct the schema does not contain. `objectiveType` is documented `Pension`; extraction wrote `RetirementObjective`; the worked example still carries it; nothing said so.

Two changes, neither of which deletes anything:

- **The rule now names the convention the schema uses.** A description reading `"One of: A, B, C."` is a closed vocabulary, to be used exactly as spelled, with `Other` or omission where nothing fits — and coining a variant is called out as what it is.
- **`CanonicalVocabulary` corrects the near-misses in code**, reading the vocabularies out of the schema itself so there is never a second copy to fall out of step. It fixes capitalisation, spacing and separators, and a documented value wearing a generic word (`PensionObjective` → `Pension`). It stops there.

What it will not do is guess. `RetirementObjective` is not a misspelling of any documented value, and deciding it means `Pension` is a judgement about what the author meant — the same "quietly pick one" the extractor is explicitly told not to do. It is **left exactly as written and reported**, in the run output and on `ExtractionResult.VocabularyCorrections`.

That reporting is the actual repair. A value outside the vocabulary is not necessarily wrong — it may be the report using a word the model lacks, which is a gap in the model — but it is always something a rule matching that field by value will miss, and now it is visible either way.

---

## 7. Worked example

[examples/suitability-report-test-1.extract.json](examples/suitability-report-test-1.extract.json) is a populated instance from `example-documents/I/Suitability Report Test 1.md` — a five-plan pension consolidation. It exercises the harder parts of the model: the 4→5 risk override, five arrangements with three different advice actions, per-plan replacement analysis, layered charges with a critical-yield table, and three internally inconsistent portfolio totals.

---

## 8. Known limitations

- **A check cannot tell an absent value from an unread section — except where it decides
  whether the check runs at all.** Where an extraction pass fails, its properties are simply
  missing, and a canonical path under them resolves to nothing: the same answer a check gets for
  a value the report genuinely does not contain.

  The case that mattered most is now closed. An **applicability rule** reading such a path used
  to fail, and with `onAbsent: "Skip"` that settled the whole check — CHK-009 reported N/A and
  dropped five material findings, three of them the most severe in the case, while reporting no
  problem at all. `CanonicalModelAccessor.WasNeverRead` reads
  `/extractionReport/expectedButAbsent` for entries reasoned `PresentButUnparseable`, and a rule
  whose paths all lie under one is **undetermined rather than failed**: the check runs, and the
  run says why it could not be sure. A rule that fails on a section the extraction *did* read
  still narrows the run, which is what the rules are for.

  Inside a group, the ambiguity remains. A `side: "Assertion"` query against a failed section
  still reports the data as absent from the file, which reads as a finding about the adviser.
  Carrying a per-section extraction status in the model, rather than only in the report, would
  close that too.
- **`isClientSpecific` is a judgement, not a fact.** It will be the least reliable field in the model. Treat it as a ranking signal for reviewer attention, not as a pass/fail gate.
- **Provider risk-scale normalisation is out of scope.** The model records scales faithfully and flags comparability; mapping between Dynamic Planner, FE, Defaqto and provider-internal scales needs a reference dataset the model does not carry.
- **Charts are not parsed.** Cashflow and performance charts in the example arrive as OCR noise. Numeric performance data is only reliably captured where the report also tabulates it; `PerformanceLine.provenance.confidence` should reflect that.
- **The appropriateness limb needs rules the model does not contain.** The model supplies the operands (risk level vs strategy risk, cost vs benefit, term vs horizon); the thresholds are a separate rules artefact and deliberately not embedded in the schema, since they will change independently of the data shape.
