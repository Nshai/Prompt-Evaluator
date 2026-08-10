**Assessment Check Catalogue**

Shared Product Requirements Document — v3.0 (working)

Status: Draft for engineering & PM review  |  Created: 14 July 2026  |  Working copy established: 21 July 2026  |  v2.0: 26 July 2026 — major restructuring following customer (TFAS) feedback review  |  Updated: 29 July 2026 — editorial review and scope refinement

Source: IQ Quality and Compliance Assistant PRD v3.0 (9 July 2026), Section 6 and related check-configuration requirements, extracted and extended

*Scope note: this document defines the shared assessment check catalogue and check engine behaviour used by both IQ Quality Assistant and IQ Compliance Assistant. It does not define either app's presentation, workflow, or disposition logic — those remain in each app's own PRD.*

# Table of contents

1. Purpose & scope
2. Design principles
3. Functional requirements
4. Finding classification reference
5. Check catalogue 
6. Dead-stop criteria
7. Open questions
8. Dependencies & consumers
9. Glossary

# 1. Purpose & scope

The suitability report assessment engine is shared: one set of checks, one classification model, evaluated once per assessment, consumed by two different applications. 

- IQ Quality Assistant presents findings as adviser self-check guidance; 
- IQ Compliance Assistant (separate PRD) presents the same underlying findings to a checker for formal disposition. 

Defining the check catalogue once, here, avoids the two app teams maintaining diverging copies of the same catalogue, and gives both a single place to look when a check's definition, source mapping, or classification behaviour needs to change.

## 1.1 In scope

- The check catalogue: every check's identity, description, evaluation guidance, and classification scale.

- The source-content mapping per check — which parts of a case a given check actually needs to see.

- The three-level finding classification model shared by both apps.

- Not-Applicable logic based on goal/journey type.

- Firm-level guidance text on checks — firm-authored text, in the firm's own language, shown to users alongside a check's finding. This is not an AI prompt and does not alter the check's evaluation logic or criteria (REQ-CC-05).-  
- Rule/check versioning against regulatory change.

- Case-level dead-stop detection — recognising when a case cannot be assessed (REQ-CC-09).

- Cross-document consistency checking — comparing core facts and recommendation data across the full evidence set for a case, not just within one check's mapped source (REQ-CC-10).  - TO BE DISCUSSED

## 1.2 Out of scope — owned by the consuming app

- How findings are presented, toned, or worded for an adviser vs a checker (Quality Assistant REQ-QA-12; Compliance Assistant, separate PRD).

- Adviser comments, thumbs feedback, and re-run/carry-forward behaviour (Quality Assistant).

- Checker decision and case-level risk scoring (Compliance Assistant).

- Entry points, document auto-load per app, and UI/UX (each app's own PRD).

- Case-level workflow, locking, and queue mechanics (Pre-Sales Compliance Checking PRD).

- Interpreting or replicating dead-stop criteria within a consuming app — apps receive only the case-level result and the triggered reason; the rule set itself is engine-owned and never exposed (REQ-CC-09).

- Firm based test case upload and validation is not required

## 1.3 Not yet in scope — no content authored

Unlike §1.2, these aren't owned by another layer — no content exists anywhere yet, and none has been authored here without proper grounding.

- Mortgage advice checks (MCOB affordability, suitability, disclosure, evidence). The Mortgage specialist module is named as a placeholder in §5.4, but has no constituent checks. Authoring them needs dedicated compliance SME input and source material equivalent to what exists for the other advice regimes covered here — see OQ-09.

- Protection (ICOBS) checks are planned for inclusion in a later phase, pending dedicated compliance SME input and source material.

# 2. Design principles

- One engine, two consumers. A check is defined once; both apps call the same evaluation for the same document set and receive the same finding and classification.

- Guidance, not verdict, at the engine layer. The three classification levels (Section 4) are neutral observations. Each app decides how to act on them — the engine does not decide compliance outcomes. This holds even for a dead stop (Section 6): a dead stop means the engine could not complete an assessment, not that the advice is unsuitable — it is a can-we-assess gate, not a compliance verdict.

- Confidence through benchmarking, not per-firm re-validation. Firms need confidence that a check produces consistent, repeatable results against a given data set. Re-running that validation independently for every firm is not viable at scale — cost-prohibitive to run and to support. The engine should instead provide a benchmark firms can rely on (e.g. an intelliflo-run reference test suite against known data sets), rather than each firm proving out the same check logic themselves. Whether this becomes a commercial assurance/certification firms can point to, or is offered another way, is for product to decide — see OQ-06.

- Minimise source content per check. Every check declares what it needs, not what's available — see REQ-CC-02. This bounds cost and keeps evaluation focused. Cross-document consistency (REQ-CC-10) is a deliberate, scoped exception: detecting a conflict inherently needs visibility across sources, not one narrow slice.

- A signature is not evidence of understanding. A signed form, a ticked box, or standard boilerplate confirms only that something was presented — not that the client understood or engaged with it. Any check assessing comprehension, understanding, or informed agreement (most directly Top 10 #8, §5.2) must look for supporting interaction evidence — questions asked, explanations given, confirmed understanding — not treat the presence of a signed or acknowledged document as sufficient on its own.

# 3. Functional requirements

### REQ-CC-01 — Check definition schema

**User story**

*As a compliance administrator or engineer, I need every check to follow a consistent schema, so that checks are authorable, testable, and comparable across the catalogue.*

**Acceptance criteria**

**GIVEN** a check is added to or edited in the catalogue

**THEN** it must define: a unique rule ID, title, group and sub-group, a plain-language description, WHAT TO LOOK FOR guidance, an assessment scale describing No issue / Potential concern, applicable context goal/journey types (or "all"), and the relevant source content mapping (REQ-CC-02)

**AND** regulatory references (COBS, ICOBS, MCOB, PRIN 2A, FG21/1, etc.) are retained against the check for audit and adviser/checker context

**AND** WHAT TO LOOK FOR guidance has two layers: intelliflo's baseline guidance (regulatory-grounded, authored by the catalogue owner)

**AND **assessment-level firm prompts (from a predefined set) are available and applied per assessment, allowing firms to align suitability letter output to their processes (e.g., additional product-specific risks or process requirements not mandated by COBS or the FCA)

**Notes**

- RED FLAGS are not required as part of the check definition schema; the assessment scale (No issue / Potential concern) provides the primary classification mechanism.

- Whether a firm's prompt supplements (adds to) intelliflo's baseline WHAT TO LOOK FOR guidance, or replaces it outright, is an open product decision — see OQ-07.

- This firm prompt is a distinct mechanism from the firm-level guidance text in REQ-CC-05. REQ-CC-05's guidance text is explicitly display-only, shown to the user, and is not sent to the model as evaluation instruction. The firm prompt described here is different — it forms part of WHAT TO LOOK FOR, which is evaluation criteria the model acts on. The two should not be conflated; which mechanism a firm actually uses, and how they relate, needs to be made explicit before build (see OQ-07). **Updated (29 July 2026): Firm-level prompts per individual check are out of scope. Assessment-level prompts from a predefined set are in scope instead.**

- Context will guide which checks are appropriate for the assessment.

### REQ-CC-11 — Additional Content Checks - firm configurable - NEW

**User story**
*As the engine, there will be a set of additional (optional per firm) content checks as part of the assessment checker so that I can  check the content of the suitability letter.* 

**Acceptance criteria**
**GIVEN** these checks are configured for the firm from a library 
**THEN** we will run the additional content checks and assess the content of the suitability letter e.g.Does the SR include a section for each of the client’s goals and needs?
**AND** They will be very targeted and deterministic to keep them as simple as possible. 

**Notes**
- There will be a default library that could be extended over time (OQ-13)
- However, there could be an additional cost for this, or for batches of additional prompts. We can very quickly reverse engineer for customer SR’s the likely shapes and structures. We could also include a prompt that explicitly uses the firm’s own SAG (Suitable Advice Guidelines) to augment the AI’s knowledge and carry out additional checks.
- COULD - We also include a handful of more advisory prompts based on the limited regulation around the contents of the SR (TBC). The challenge is that the regulation does not mandate very much being in an SR; most things are optional. - TBC
We do however know the typical structure of an SR and the kinds of things it is likely to contain.
- Phase 2  - We could enhance these further with some limited parameterisation e.g. Does the SR contain a section called “X” that describes “Y”.
- Phase 3 - We allow firms to include their own prompts (signed off by intelliflo) for a premium.

### REQ-CC-12 — Contextual Information to assist engine  - NEW

**User story**
*As the engine, I need context provided prior to running the check cataglogue to aid AI in understanding the advice context to apply the appropriate checks.*

**Acceptance criteria**
**GIVEN** the user triggers the assessment checker
**WHEN** it is being used outside of the best practice advice journey
**THEN** the user will answer a small set of questions to provide advice context to the engine
**AND** These questions will include the Type of Journey, Type of Advice and potentially the type(s) of Goals being advised. The latter may be derivable from the SR itself.
**AND** where the assessment is triggered within advice this context will be automatcially set

**Notes**
- confirm the context questions (Oq-14)

### REQ-CC-02 — Source content mapping per check

**User story**

*As the engine, I need to know exactly which case content (document categories) each check requires, so that evaluation is accurate, fast, and cost-efficient rather than scanning the entire case for every check.*

**Acceptance criteria**

**GIVEN** a check is evaluated for a case

**THEN** only the source content mapped to that check (Section 5) is included in the evaluation context

**AND** a check may map to a single narrow source (e.g. one fact find field) or to a small, explicitly bounded set of sources where the check genuinely requires it (e.g. Product Replacement Justified, which needs both the existing policy documents and the suitability report's replacement analysis) — this is distinct from REQ-CC-10's cross-document consistency checking, which deliberately isn't bounded to a mapped set at all

**AND** the mapping is maintainable independently of the check's descriptive text, so a source location can be corrected without redefining the check

**AND** every source content reference is categorised using the document category taxonomy (§5.1) — a common vocabulary for what kind of evidence a check draws on, shared with the case-type document requirements the taxonomy was originally defined against

**AND** where the document category is unknown, or cannot be derived (using checker outside of advice) a confirmation by the user (e.g. Adviser) that they have uploaded one or more evidentiary documents to support the AI in assessing the SR. 

**AND** where evidence is missing for a category this will be flagged to the user to upload additional content before proceeding with the assessment checker. 

**Notes**

- This directly bounds the token-volume cost multiplier flagged earlier — evaluating the full catalogue against a full case each time is materially more expensive than each check pulling only what it needs.

- Some checks will need a case-data field reference (e.g. fact find ATRQ score) rather than a free-text document — the mapping must support both structured field references and document/section references. The §5.1 taxonomy categorises *what kind* of evidence is needed; it doesn't resolve *how* a specific reference is expressed — that's still OQ-04.

- This list will act as a soft gate to encourage the user to think about the evidence being provided, and if they do not have sufficient evidence to make them gather it before proceeding with the assessment. There should be a disclaimer indicating that failure to provide sufficient relevant evidence will result in a limited assessment. The AI will rely on the evidence for consistency checking of the SR.

- UX to determine how the document category to evidence mapping is completed - could upload all documents and apply category or upload per category

### REQ-CC-03 — Not-Applicable logic by goal/journey type

**User story**

*As an adviser or checker, I want irrelevant checks clearly marked rather than silently run or hidden, so that I can see what was excluded and why.*

**Acceptance criteria**

**GIVEN** a case has one or more goal types and a journey type

**WHEN** the catalogue is evaluated

**THEN** any check not applicable to the case's in-scope goal type(s) returns the classification "Not Applicable" rather than being omitted from the results

**AND** e.g. Protection Needs Addressed returns Not Applicable where no protection goal is present; specialist-trigger checks generally (§5.4) return Not Applicable for a case that never activates their trigger

**Notes**

- The check-to-goal-type mapping is a maintained table, implemented in two layers: each Top 10 check's "Applies to" column (§5.2) sets its top-level applicability (e.g. Rank 9, Replacement or switch justification, applies only where a replacement/switch is present); the specialist overlay trigger table (§5.4) governs whether a specialist module runs at all. A finer third layer sits within §5.3's consolidated sub-items, where individual sub-items carry their own conditional notes (e.g. ESG Preferences Consistency fires only where an ESG preference was stated) — these are currently prose notes, not structured data; formalising them as queryable applicability rules is implementation work for the authoring tool, not a gap in the mapping itself.

- **Descoped, not just unpopulated:** the Mortgage specialist module (§5.4) has no constituent checks and none are planned for this catalogue's current phase — the original 35-check catalogue never covered mortgage advice, and no source document reviewed to date provides mortgage check content. REQ-CC-03's worked example above was corrected accordingly (it previously cited a check that didn't exist). See §1.2 and OQ-09.

### REQ-CC-04 — Three-level finding classification

**User story**

*As either consuming app, I need a consistent, shared vocabulary for check outcomes, so that findings mean the same thing whether an adviser or a checker is looking at them.*

**Acceptance criteria**

**GIVEN** a check has been evaluated against its mapped source content

**THEN** the result is exactly one of: Not Applicable, No issue found, Potential concern

**AND** the classification is accompanied by the specific evidence found (or its absence) and a source reference

**AND** a "Potential Concern" classification is not itself a compliance failure — it is an observation; each consuming app defines what happens next with it

### REQ-CC-05 — Firm-level check guidance text

**User story**

*As a compliance administrator, I want to attach our own guidance text to a baseline check, in our own language, so that users see our firm's policy context alongside the regulatory baseline when a finding is shown to them.*

**Acceptance criteria**

**GIVEN** a firm wants to add their own guidance to a baseline check

**THEN** they can attach firm-authored guidance text to that check, scoped as product-specific, journey-type-specific, client-segment-specific, or universal

**AND** this text is display-only context for the user — it is not an AI prompt, it is not sent to the model as evaluation instruction, and it does not alter the check's baseline evaluation logic or criteria; the check is evaluated against the baseline criteria alone, for both consuming apps

**AND** the guidance text is surfaced to whichever app displays the finding, alongside it, to contextualise it for the user

**AND** guidance text is versioned, with history retained for audit

**AND** firms can review which checks are included in the intelliflo default set

**Notes**

- Firms cannot use this mechanism to change what a check looks for, how it classifies a finding, or its RED FLAGS — that would be a change to the check definition itself (REQ-CC-01, REQ-CC-07), not firm-level guidance text. Note REQ-CC-01 separately defines an evaluation-affecting firm prompt for WHAT TO LOOK FOR, which is a different mechanism from this one — see REQ-CC-01 and OQ-07.

### REQ-CC-06 — Benchmark reference test suite

**User story**

*As a compliance administrator or firm, I want access to intelliflo's validated benchmark test suite so that I can trust checks produce consistent, repeatable results without needing to independently validate every check.*

**Acceptance criteria**

**GIVEN** a check is deployed in the catalogue

**THEN** intelliflo maintains a benchmark reference test suite against known data sets (real anonymised service-case references and synthetic edge cases) that exercise the check across positive, negative, and boundary conditions

**AND** the test suite is run against each check version and results published to firms, documenting: pass rates, edge cases covered, known limitations, and result distribution (to surface consistency issues)

**AND** firms can access benchmark results for each check and ruleset version, together with the test cases and expected outcomes, to establish confidence in check behaviour without re-running validation themselves

**AND** the benchmark suite is versioned alongside the ruleset (REQ-CC-07) so that firms can trace check behaviour across regulatory updates

**AND** benchmark results are maintained and updated when regulatory changes are made or check definitions are refined, ensuring firms have current confidence data for the ruleset version they're running

**Notes**

- The benchmark model inverts the validation pattern: intelliflo runs once against a shared suite, firms rely on published results. This is cost-efficient at scale (run once, trust many times) vs the per-firm model where each firm re-runs the same validation independently.

- **Benchmark scope:** Positive tests (should trigger a concern), negative tests (should not trigger), and edge cases (boundary conditions). The suite grows over time as new edge cases and regulatory scenarios are discovered.

- **Commercial positioning open (OQ-06):** Whether benchmark results are published openly to all users, offered as a commercial assurance/certification firms can point to in audit, or made available through a tiered access model is a product decision — see OQ-06. This requirement specifies the benchmark suite exists and is published; how and to whom are separate.

- **Supporting evidence:** TFAS (24 July 2026) is already gathering real, anonymised service-case references — a cross-section of pensions and investment scenarios — specifically for tool validation against known outcomes. This pool forms the foundation for the intelliflo benchmark suite; synthetic edge cases and regulatory scenarios will supplement it.

- **Relationship to firm customization (REQ-CC-05):** Benchmark results cover intelliflo's baseline check definitions. Firms may attach guidance text (REQ-CC-05) or apply assessment-level prompts (REQ-CC-01) without affecting the check's baseline evaluation — the benchmark remains valid and applicable.

### REQ-CC-07 — Rule & regulatory versioning

**User story**

*As a compliance administrator, I need checks to be updatable as FCA regulation changes, without corrupting historical assessments already relied upon by either app.*

**Acceptance criteria**

**GIVEN** a regulatory change requires updating a check (e.g. COBS 9C replacing COBS 9/9A)

**THEN** the updated check is versioned with an effective date; the prior version is retained, not overwritten

**AND** historical assessments retain the check version active at the time they ran

**AND** firms can run on the old or new ruleset during a defined transition period; the active version is flagged on every assessment

### REQ-CC-08 — Consuming app contract

**User story**

*As either app, I need a stable, well-defined output from the engine, so that I can build my app's presentation and workflow logic against a predictable contract.*

**Acceptance criteria**

**GIVEN** a check has been evaluated

**THEN** the engine returns, per check: rule ID, classification (REQ-CC-04), the specific evidence text/data found, a source reference suitable for evidence-link navigation, applicable firm guidance text if any (REQ-CC-05), and the check/ruleset version used

**AND** each finding also includes: a concise rationale for the classification; missing or contradictory evidence, named specifically rather than implied

**AND** each finding includes a confidence indicator and an evidence-coverage indicator for the engine (not the check), together with the principal reason(s) for any uncertainty (e.g. weak evidence, conflicting evidence, document quality, model interpretation)

**AND** confidence is never presented, described, or capable of being read as the probability that the underlying advice is compliant — it describes the engine's certainty in its own finding, nothing more

**AND** the engine surfaces confidence and evidence-coverage; it does not itself decide what happens with a low-confidence, low-coverage, or conflicting-evidence finding — whether and how that routes to human review, and at what threshold, is a decision for each consuming app and its firm configuration, consistent with §2's "Guidance, not verdict" principle

**AND** where mapped source content exists but cannot be read — corrupted, password-protected, incomplete, or otherwise inaccessible — the engine distinguishes this from evidence that was searched for and genuinely not found, and flags it for escalation rather than folding it silently into an ordinary Concern classification

**AND** where a case is Dead Stop the engine returns the case-level result, the triggered criterion, and reasoning — the contract's shape for this case is defined by REQ-CC-09, not by the per-check fields above

**AND** where a finding arises from cross-document consistency checking (REQ-CC-10), the source reference cites every conflicting location, not the single source reference implied above for an ordinary check

**AND** this output is identical in structure whether the caller is Quality Assistant or Compliance Assistant — differences in tone, wording, or downstream action are applied by the consuming app, not the engine

**Notes**

- This closes a gap between REQ-CC-08 as previously written (five fields: rule ID, classification, evidence found, source reference, ruleset version) and the "Required output" spec used throughout the Top 10 AI Check Prompts document (outcome, rationale, evidence citations, missing/contradictory evidence, impact, remediation) — the two should have matched from the start.

- Confidence and evidence-coverage are genuinely new fields, not a formalisation of something already implicit. Producing a calibrated confidence score is itself model/engineering work with real cost and accuracy implications — the *field existing in the contract* is specified here; *how it's calibrated, and what accuracy targets apply to it*, is Assessment Engine non-functional territory, out of scope for this document — see OQ-11.

- REQ-CC-04 already requires a classification to be "accompanied by the specific evidence found (or its absence) and a source reference." This REQ-CC-08 entry is the authoritative full field list for the wire contract; REQ-CC-04's mention is the minimum, not a competing spec.

- The Top 10 AI Check Prompts document states, per check, "do not make the final regulatory or suitability decision on behalf of the authorised reviewer." That's already covered by REQ-CC-04 ("a Concern classification is not itself a compliance failure") and §2's "Guidance, not verdict" principle — not repeated here as a separate clause.

- **Not found vs inaccessible:** "genuinely not found" means the engine searched the mapped source and no such evidence exists or was recorded — an ordinary basis for a Potential Concern finding under REQ-CC-04. "Inaccessible" means evidence likely exists but the engine couldn't read it — a different problem calling for remediation (fix the file, re-supply it) rather than a suitability judgement. This distinction applies per document, at the level of an individual check's mapped source; it's separate from, and sits below, the case-level dead-stop criteria in §6.1. Dead-stop criterion #1 (suitability report unreadable/corrupted) and criteria #4–5 (core client information or critical evidence population unavailable) already cover the severe case where the *inaccessible* document is itself core or critical — this clause covers the general case of any other mapped document being inaccessible, which doesn't warrant halting the whole assessment.

### REQ-CC-09 — Case-level dead-stop detection

**User story**

*As either consuming app, I need the engine to recognise when a case cannot be safely, reliably, or lawfully assessed, so that I never receive misleading per-check findings — without needing to know or replicate the underlying criteria myself.*

**Acceptance criteria**

**GIVEN**a case is submitted for assessment

**THEN**the engine evaluates it against a maintained, engine-owned set of dead-stop criteria (Section 6) before running the check catalogue

**AND** where a criterion is met, the engine returns a case-level result of 'Unable to complete assessment'with related trigger reason

**AND** consuming apps receive only the case-level result, the triggered criterion's label. The criteria definitions, trigger logic, and required-outcome mappings (Section 6) are maintained and versioned within the engine (REQ-CC-07), not exposed as something a consuming app holds or interprets

**AND** a Dead Stop is not itself a determination that the advice is unsuitable — it means no assessment could be completed

### REQ-CC-10 — Cross-document consistency (Suitability Letter and document consistency) - TO BE DISCUSSED

**User story**

*As either consuming app, I need material facts and recommendation data checked for consistency between the suitability letter and the whole evidence set, not just within whatever a single check happens to look at, so that a conflict between documents and the suitability letter doesn't go unnoticed.*

**Acceptance criteria**

**GIVEN** a case's full evidence set — documents, recordings/transcripts, structured CRM data, and correspondence

**THEN** the engine compares core facts and recommendation data across all sources for the case, covering at minimum: client details, objectives, income and expenditure, assets/liabilities, contribution or withdrawal amounts, risk rating, product/provider/fund, charges, tax assumptions, and key risks 

**AND** where a material mismatch is found between the evidence set and the suitability letter, the output identifies both (or all) conflicting sources and explains the nature and materiality of the mismatch — a bare "inconsistent" classification without that detail does not satisfy this requirement

**AND** precedence and version rules are applied so formatting differences and superseded documents do not create unnecessary findings — the engine uses the most current evidence for each fact and identifies which version, if any, is superseded

**AND **cross-document consistency findings use the same 3-level classification as any other check (REQ-CC-04) — ordinary mismatches are Potential Concern findings; only where the correct position genuinely cannot be established does this escalate to dead-stop criterion #6, "Material facts irreconcilably inconsistent" (§6.1)

**Notes**

- This formalises what Evidence Trail Complete (previously held out as genuinely cross-document, §5.6) was informally trying to check. Evidence Trail Complete is now a Top 10 #6 sub-item (§5.3), evaluated via this requirement rather than a single-document source mapping — see §5.3.

- This is a deliberate, scoped exception to REQ-CC-02's "minimise source content per check" principle (§2). Detecting conflicts inherently requires visibility across sources; it cannot be bounded to one narrow slice the way an ordinary check can. Cost and implementation scope (e.g. whether this pulls full documents or only the specific fields listed above) is an open question — see OQ-10.

- This capability is not a "check" under REQ-CC-01's schema — it's an engine-level cross-cutting analysis layer, comparable in kind to REQ-CC-09's dead-stop detection, but producing ordinary per-field findings (REQ-CC-04) rather than a case-level gate.

- Does not itself decide which source is authoritative beyond applying precedence/version rules. Where the correct position cannot be established at all, that is dead-stop criterion #6's territory, not this requirement's job to resolve.

# 4. Finding classification reference

| **Classification** | **Meaning** |
| --- | --- |
| Not Applicable | Check is not relevant for this advice type or goal — no action expected from either app. |
| No issue found | Evidence present; the check's criteria are met. |
| Potential concern | Evidence is weak, dated, or context-dependent — warrants review, not automatically a problem. |



# 5. Check catalogue

The catalogue is structured as **10 core checks** for mainstream retail investment and pension advice, **specialist overlay modules** that activate only when a case-fact trigger is present. 

An assessment checks & prompts catalogue spreadsheet (currently v1) will contain all the checks and the related prompts, document categories & type, regulatory references.

This catalogue will be reviewed and iterated to finalising the prompting during testing/refinement. 

## 5.1 Document category taxonomy

The following is a list of the document categories used to classify doucments uploaded for evidence. The associated checks and prompts will refer to these categories. 

| **Code** | **Category** | **Examples** |
| --- | --- | --- |
|A	|Client Authority	|service/fee agreement;
|B	|Know Your Client	|Fact find; Client personal details; estate details; tax details; health details; dependants; employments and earned income; assets, liabilities and existing provisions (protection, investment, pension, annuity etc); eligibility; other incomes and expenditures; affordability; Goals (high level demands and needs); 
|C	|Meetings & Communications	|Meeting and presentation recordings/transcripts; file notes; phone calls; emails; follow-ups; evidence of questions, explanations and client responses
|D	|Risk, Capacity & Experience	|ATR questionnaire; capacity-for-loss assessment; risk reconciliation; investment knowledge/experience; vulnerability/support assessment;
|E	|Existing Plans & Portfolios	|Plan, portfolio and provider information; existing plan holdings & valuations; policy schedules; scheme booklets; guarantees/penalties; existing charges; asset allocation
|F	|Needs Analysis	|Needs Analysis; need level risk; need time horizon; need shortfalls; accumulation and decumulation modelling projections; asset allocation analysis; cashflow planning scenarios; stress tests; tax optimisation; 
|G	|Research	|Research notes; provider/platform/product/fund comparison; portfolio analysis; replacement & switching analysis and rationale; safeguarded benefits; RIY/Impact of charges; tax analysis; 
|H	|Solution Design	|Solution details; transaction details; quotes & illustrations; KIDs/KIIDs/KFDs; fund factsheets; costs & charges analysis; performance/volatility material; investment strategies; risk warnings; alternatives
|I	|Recommendations & Advice	|Suitability report; Goals/Needs (demands/needs); recommendations and rationale; alternatives; disadvantages; tax implications (where applicable); client-specific risks; review report



## 5.2 Specialist overlay modules

To be extended as a later phase including mortgage, protection and DB pensions as the catelogue extends.


## 5.3 Worked example — how a check is specified

Every check needs this level of structure so the AI classifies consistently. 
Example: Risk, Capacity for Loss and Investment Strategy Match

**PROMPTS** Where the Suitability Report includes portfolio /product investment strategy recommendations, are they consistent with the evidence provided?

Where the Suitability Report includes information relating to the client's attitude to risk, capacity for loss, tolerance for loss/need level risk and investment term it is consistent with the evidence provided?

Are the investment recommendations appropriate based on the risk assessment within the Suitability Report?

**WHAT TO LOOK FOR:** 
• ATR result and date
• Capacity for loss separately evidenced
• Risk need, time horizon and existing portfolio considered
• Mapped product/fund/portfolio risk is current and comparable
• Any mismatch or override is client-specific and evidenced

**DOCUMENT CATEGORIES** 
Risk, Capacity & Experience
Existing Plans & Portfolios
Research & Needs Analysis
Recommendations & Advice

**ASSESSMENT SCALE — No issue:** evidence complete, consistent and supports outcome. 

**ASSESSMENT SCALE — Potential concern:** material clarification or additional evidence required.

# 6. Dead-stop criteria

This section defines the engine-owned criteria REQ-CC-09 evaluates before running the check catalogue. Consuming apps never see this table directly — they receive only the case-level result and the triggered criterion's label (REQ-CC-09).

A dead stop applies only where the review cannot be completed safely, reliably, or lawfully — a fundamental evidence, legal, permission, authority, or scope failure. Ordinary suitability concerns continue through assessment and are reported as findings (Section 4), not gated here.

A single inaccessible document (corrupted, password-protected, incomplete) is not, by itself, a dead stop unless it happens to be core or critical evidence (criteria #1, #4, #5 below). The general case — any other mapped document being unreadable — is handled per-check as part of REQ-CC-08's output contract, distinct from evidence that's genuinely absent.

## 6.1 Dead-stop criteria


| **#** | **Criterion** | **Trigger** | **Reason** |
| --- | --- | --- | --- |
| 1 | Suitability report unavailable | The suitability report is missing, unreadable, corrupted, or so incomplete that the recommendation and rationale cannot be assessed. |Suitability letter cannot be found or unreadable  |
| 2 | No identifiable recommendation | The recommended product, provider, wrapper, investment solution, fund, transaction, or amount cannot be established with reasonable certainty. | No recommendation could not be established |
| 3 | Insufficient objective/goal information | Material information about the client's objectives preventing a suitability assessment. | No objectives/goals could not be established |
| 4 | Critical evidence missing | Evidence fundamental to the advice is unavailable — e.g. the fact find, risk/capacity assessment, existing-plan data for a replacement, charges/illustration, or retirement-income analysis for drawdown. | Key evidence has not been provided |


## 6.3 Case-level outcomes

| **Outcome** | **Engine treatment** | **Typical application** |
| --- | --- | --- |
| Unable to complete assessment | No assessment is triggered | One of the 3 criterion has failed and user cannot proceed.|
| None | Assessment can be run normally; the 3 per-check classifications apply. | Risk mismatch; poor objective quality; cost/value concern; inadequate rationale; disclosure or documentation weakness. |


# 7. Open questions/actions

| **ID** | **Topic** | **Detail** | **Outcome** |
| --- | --- | --- |---|
| OQ-04 | Structured field vs document references — representation format | §5.1's document category taxonomy now answers *what kind* of evidence a check needs (A–H). What remains open is *how* a specific reference is expressed — a structured case-data field pointer (e.g. fact find ATRQ score) vs a free-text document/section pointer — consistently across the mapping. The schema for that isn't defined yet. | To Be Discussed - is it better to use data (via advice) or all evidence is document based
| OQ-05 | Per-check vs batched evaluation | Whether each check is evaluated as an independent model call, or checks are batched by group to reduce call volume, is an implementation decision with real cost and consistency trade-offs — not yet decided. |Engineering to confirm
| OQ-10 | Cross-document consistency cost & scope | REQ-CC-10 is a deliberate exception to REQ-CC-02's "minimise source content per check" principle — it needs breadth across the evidence set by design. Whether it pulls full documents for every case or only the specific fields it compares (client details, objectives, income/expenditure, etc.), and how often it runs relative to per-check evaluation, is an implementation decision with real cost implications — not yet decided. Related to OQ-05's per-check-vs-batched question but distinct: this is about REQ-CC-10 specifically, not the catalogue generally. | TO BE DISCUSSED
| OQ-11 | Confidence calibration methodology home | REQ-CC-08 now requires a confidence and evidence-coverage indicator per finding. This document specifies that the field exists and how it must not be presented (never as probability of compliance) — it does not specify how confidence is calibrated, what accuracy targets apply, or how false-positive/false-negative rates are monitored. That's Assessment Engine non-functional territory, matching the Quality Assistant PRD's own OQ-17 ("home for Assessment Engine non-functional spec"), which the forthcoming Assessment Criteria PRD is being created to resolve. |
| OQ-12 | Review WHAT TO LOOK FOR | Review WHAT TO LOOK FOR in assessment check catalogue to align the 35 checks with the 10 core checks |
| OQ-13 | Confirm Assessment Content Checks | confirm default set of prompts to allow the engine to review the content of the suitability letter |
| OQ-14 | Confirm Assessment Context Questions | confirm context questions that will be provided to the engine |
| OQ-15 | Extend Checks to cover DB pension transfers | add additional checks to cover DB pension transfers as part of advice |

## 7.1 Resolved (kept for traceability)

| **ID** | **Topic** | **Resolution** | **Outcome** |
| --- | --- | --- |---|
| OQ-01 | 31 vs 35 checks | Superseded by the §5 restructuring (26 July 2026, TFAS customer feedback review): the catalogue is no longer a flat count of 31 or 35. It's 10 core checks (§5.2) + specialist overlay modules (§5.4) + consolidated sub-items (§5.3), with one standalone item (§5.5) and two retired items resolved and kept for traceability (§5.6). All 35 original items are accounted for; none dropped. |
| OQ-02 | Source content mapping accuracy | The mapping in Section 5 is a first-pass inference from each check's description, not yet reviewed by a compliance subject-matter expert. This review should also apply the §5.1 document category taxonomy (A–H) to each existing mapping — not done as part of adopting the taxonomy itself. (Evidence Trail Complete no longer needs a source-content mapping here — it's evaluated via REQ-CC-10's cross-document consistency checking instead; see Top 10 #6, §5.3.) | Resolved - See assessment check spreadsheet
| OQ-03 | Goal/journey-type mapping — implementation format | The mapping itself now exists across three layers: §5.2's "Applies to" column (Top 10 level), §5.4's specialist trigger table (module level), and §5.3's conditional notes (sub-item level). The remaining gap is narrower than originally framed: the sub-item-level layer is currently prose notes, not structured, queryable data — formalising it is implementation work for the authoring tool, overlapping with OQ-04's schema question. | resolved with OQ-14
| OQ-06 | Benchmark mechanism & commercial positioning | §2's design principle calls for a benchmark firms can rely on for check consistency, rather than each firm independently validating (cost-prohibitive at scale). How that benchmark is built and run (e.g. intelliflo-run reference test suite against known data sets), and whether there's a commercial assurance/certification angle to it, is undecided — for product to determine. REQ-CC-06 is flagged for rework pending this decision. **Evidence for the mechanism question, not a resolution:** the TFAS meeting (24 July 2026) shows intelliflo already gathering real, anonymised service-case references — a cross-section of pensions/investment scenarios, from a design partner — specifically for tool validation, rather than purely synthetic samples. This is consistent with, and lends real-world weight to, the "intelliflo-run reference test suite against known data sets" direction — it says nothing about the commercial positioning question, which remains fully open. | Resolved - see REQ-CC-06, no commercial angle here.
| OQ-07 | Firm prompt vs intelliflo WHAT TO LOOK FOR guidance | REQ-CC-01 allows a firm-authored prompt against WHAT TO LOOK FOR, alongside intelliflo's baseline guidance. Whether the firm's prompt supplements the baseline or replaces it outright is an open product decision. This also needs reconciling against REQ-CC-05's firm-level guidance text, which is explicitly display-only and not sent to the model — the WHAT TO LOOK FOR firm prompt is a different, evaluation-affecting mechanism, and the two should not be conflated. **Updated (29 July 2026): Firm-level prompts per individual check are out of scope. The mechanism moves to assessment-level prompts from a predefined set, applied per assessment.** | Resolved - Firm authored prompts are out of scope. Additional requirement for a configurable set of additional checks related to suitability letter content has been added REQ-CC-11
| OQ-08 | Dead-stop escalation execution ownership | REQ-CC-09 has the engine detect dead-stop conditions and signal Dead Stop/Review Paused with a reason. It does not define who holds the journey stage, routes the escalation, or records owner/date/release reason — that's a workflow execution question likely owned by Pre-Sales Compliance Checking or the New Advice Journey layer, not yet confirmed. | Resolved- no escalation process required, see section 6.
| OQ-09 | Mortgage check content — future authoring | Decided (26 July 2026): mortgage checks won't be invented without proper grounding — see §1.3. The Mortgage trigger (§5.4) stays as a named placeholder; REQ-CC-03's worked example was corrected to stop citing a check that doesn't exist. What remains open is narrower: whether/when this catalogue takes on mortgage advice at all, and if so, who supplies the compliance SME input and source material (a "Mortgage Trigger Rules" equivalent to what exists for pre-sales compliance doesn't currently exist either). Not a near-term blocker — no consuming app currently requires it. | Agreed this is a later addition for mortgage and protection.

# 8. Dependencies & consumers

- IQ Quality Assistant PRD (v3.1+) — consumes this catalogue for adviser self-check; owns presentation, tone, comments, and re-run behaviour.

- IQ Compliance Assistant PRD (not yet written) — will consume the same catalogue for formal checker disposition; owns case-level decision and workflow.

- New Advice Journey ruleset — owns document/data presence validation; this catalogue assumes minimum required files already exist.

- Rules authoring engine (shared with Pre-Sales Compliance Checking) — likely hosting infrastructure for check versioning and parameterisation; relationship to be confirmed.

- intelliflo office document storage — source of the documents and structured data referenced in Section 5.

- Pre-Sales Compliance Checking PRD / New Advice Journey — the engine signals Dead Stop or Review Paused (Section 6, REQ-CC-09) and the triggered reason; which layer actually holds the journey stage, routes the escalation, and records owner/date/release reason is a workflow execution question owned there, not by this engine — see OQ-08.

- Assessment Criteria PRD (not yet written, pending a session with a client and compliance partner) — expected to own confidence calibration methodology and accuracy targets for REQ-CC-08's confidence/evidence-coverage fields, matching the Quality Assistant PRD's OQ-17. This document requires the fields to exist and constrains how confidence must not be presented; it does not specify how confidence is produced — see OQ-11.

# 9. Glossary

| **Term** | **Meaning in this document** |
| --- | --- |
| COBS | Conduct of Business Sourcebook — focuses on designated investment business, retail investment advice, pensions, and long-term care insurance/life policies. |
| ICOBS | Insurance: Conduct of Business Sourcebook — focuses on general insurance and pure protection policies (such as home, car, or term life insurance). |
| MCOB | Mortgages and Home Finance: Conduct of Business Sourcebook — focuses on regulated mortgage contracts, home reversion plans, home purchase plans, and regulated bridging loans. |
| PRIN | Principles for Businesses — the FCA's rule book of overarching principles. |
| Dead Stop | A case-level engine result meaning no suitability outcome can be issued — a fundamental evidence, legal, permission, authority, or scope failure. Pre-empts per-check findings (Section 6, REQ-CC-09). |

