# Check query plans

A pre-computed semantic-search programme for each of the ten QA checks, executed by [CheckPlanRunner](../../../src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs) against [CaseDocumentSearchService](../../../src/AiPromptEvaluator.Core/Services/Documents/CaseDocumentSearchService.cs). [check-coverage-matrix.csv](../check-coverage-matrix.csv) is generated from these plans — one row per query group — so the two cannot drift apart.

These files are deployed beside the executable on build, so editing one changes what the next check run retrieves.

| File | Groups | Queries |
|---|---:|---:|
| [CHK-001](CHK-001.query-plan.json) Suitability completeness against client information | 11 | 36 |
| [CHK-002](CHK-002.query-plan.json) Objectives, needs and time-horizon alignment | 7 | 22 |
| [CHK-003](CHK-003.query-plan.json) Risk, capacity for loss and investment strategy match | 10 | 34 |
| [CHK-004](CHK-004.query-plan.json) Knowledge, experience and product complexity | 7 | 21 |
| [CHK-005](CHK-005.query-plan.json) Financial resilience, affordability and liquidity | 8 | 28 |
| [CHK-006](CHK-006.query-plan.json) Recommendation rationale and suitable alternatives | 8 | 30 |
| [CHK-007](CHK-007.query-plan.json) Costs, charges and value of the recommendation | 11 | 39 |
| [CHK-008](CHK-008.query-plan.json) Disadvantages, risks and customer understanding | 8 | 30 |
| [CHK-009](CHK-009.query-plan.json) Replacement or switch justification | 10 | 37 |
| [CHK-010](CHK-010.query-plan.json) Vulnerability, support needs and foreseeable harm overlay | 5 | 20 |
| **Total** | **85** | **297** |

Schema: [query-plan.schema.json](query-plan.schema.json). All ten validate against it, and every field's description names its role — `retrieval`, `verification`, `build-time` or `load`.

**Fixed vocabularies are real JSON Schema enums** — `side`, `priority`, `onAbsent`, `limb`, `comparison.method` and the A–I category codes. Unlike the canonical model, where an enum is enforced by `StripEnumViolations` and *deletes* the offending value, nothing here consumes a plan that failed to validate, so an enum costs nothing and refuses the value outright.

[CheckPlanLint](../../../src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanLint.cs) rule **L4** checks the same vocabularies, deliberately duplicating them, because **nothing in the build validates a plan against this schema** — that only happens when someone runs a validator by hand. L4 runs over all ten shipped plans in the test suite and fails the build, so it is the check that actually catches a bad value on the way in.

Both exist because every one of these fails silently rather than loudly. `"priority": "Supporting"` is not `Supplementary`, so `IsCore` returns true and the query runs even under `CoreQueriesOnly` — wrong, and it changes behaviour without breaking anything. A category code outside A–I reaches the Qdrant filter and matches nothing, so the group retrieves less than it declared. Both were real: `"Supporting"` was found in CHK-007 and CHK-008.

The plans track [Revised checks.csv](../../QA-Checks/Revised%20checks.csv). The app-loadable form of that file is [Revised checks (loadable).csv](../../QA-Checks/Revised%20checks%20(loadable).csv) — see [Known gaps](#known-gaps).

---

## There is no search tool

Worth saying first, because the plans used to carry a `tool` block implying otherwise. It has been removed.

Nothing in `src/` defines or registers an LLM-callable function. There is no `search_case_documents`, no `CaseDocumentSearchTool.cs`, and no tool definitions of any kind — `CheckPlanRunner` calls `CaseDocumentSearchService` directly in C#.

That absence is the design. The plan decides which searches run, so the model cannot: it is called only at the end, to judge a pack it did not assemble. Two runs of the same check over the same case therefore retrieve exactly the same evidence, and a finding can be reproduced.

---

## Anatomy of a query plan

The worked example is [CHK-004.query-plan.json](CHK-004.query-plan.json) — among the smallest of the ten, and shown complete.

### 1. The envelope — what check this is

```jsonc
{
  "planVersion": "1.0",
  "checkId": "CHK-004",
  "checkName": "Knowledge, Experience and Product Complexity",
  "primaryCategories": [
    "B",
    "C",
    "D",
    "G",
    "H",
    "I"
  ]
}
```

| Element | Drives | Purpose | How it is used |
|---|---|---|---|
| `planVersion` | **Load** | Pins the plan to a format | Checked against `CheckQueryPlanLoader.SupportedPlanVersion`. A plan at any other version is refused by name and listed in the run output, rather than half-read |
| `checkId` | Both | Identity | Matches the plan to its CSV check (whitespace squeezed on both sides), and labels every finding, log entry and matrix row |
| `checkName` | Verification | The heading the assessor sees | Rendered into the check header |
| `retrieval.resultsPerCall` | **Retrieval** | Per-plan candidate-pool width | Overrides `AppSettings.MaxSearchResults` upward for this check's queries. Unset in every shipped plan — see [Per-plan retrieval width](#per-plan-retrieval-width) |
| `primaryCategories` | Build-time only | Category codes from the check's *Primary Document Categories* column | Read by [CheckPlanLint](../../../src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanLint.cs) rule **L2**, which runs in the test suite and fails the build. Nothing reads it at run time |

`appliesTo`, `regulatoryBasis`, `tool` and `budget` used to sit here. All four are gone — the first two duplicated the checks CSV the prompt actually reads, and the last two were never deserialised at all.

### 2. `triggerProbe` — establishing that the check applies

```jsonc
"triggerProbe": {
  "triggerField": "checkTriggers.isAdvisedCase",
  "queries": [
    {
      "id": "T4.1",
      "text": "knowledge and experience of investments and pensions",
      "side": "Either",
      "targetCategories": [
        "I",
        "B",
        "D"
      ],
      "expectSignals": [
        "knowledge",
        "experience",
        "familiar",
        "understanding"
      ],
      "priority": "Core"
    }
  ],
  "absentWhen": "Never.",
  "onAbsent": "Continue"
}
```

| Element | Drives | Purpose | How it is used |
|---|---|---|---|
| `applicability` | **Retrieval** | The rules the case must satisfy for this check to run | **ANDed** — every rule must pass. Each rule looks for one of its accepted values at one of its `canonicalPaths` in the stored model, case-insensitively. A rule can only ever narrow a check: it cannot rescue one another rule has ruled out. Evaluated before any group is gathered, so an inapplicable check costs nothing — not one embedding, not one vector query |
| `triggerField` | **Retrieval** | The `checkTriggers` field this probe settles | Resolved from the **stored model first** — decided when the report was read in full, which beats a similarity search. Where the model has no value, the fallback is whether the probe found any passage. Its answer can eliminate every search the check would have run |
| `queries` | **Retrieval** | Cheap searches that corroborate the trigger | Run before any group is gathered. Assertion-side probe queries are skipped, as everywhere else |
| `absentWhen` | Verification | Why the check would not apply | Quoted verbatim in the N/A summary, so the reader sees the reason rather than a bare verdict |
| `onAbsent` | **Retrieval** | What to do when the trigger is missing | `Skip` settles the check with **zero retrieval**; `ContinueWithReducedScope` proceeds and says so in the header, so an overlay is not read as excused; `Continue` proceeds silently |

`presentWhen` and `note` used to sit here and were read by nothing.

#### Applicability rules

```jsonc
"triggerProbe": {
  "triggerField": "checkTriggers.isAdvisedCase",
  "applicability": [
    {
      "advisedCase": [
        "true"
      ],
      "canonicalPaths": [
        "/checkTriggers/isAdvisedCase"
      ]
    },
    {
      "goalTypes": [
        "Investment",
        "Pension",
        "Retirement",
        "RetirementObjective"
      ],
      "canonicalPaths": [
        "/objectives[]/objectiveType"
      ]
    }
  ],
  "absentWhen": "No investment or pension objective is stated anywhere in the report.",
  "onAbsent": "Continue"
}
```

A rule is named by its own key, so it reads as a sentence: `goalTypes: ["Investment", "Pension"]` over `/objectives[]/objectiveType` is *"the client's goals include an investment or a pension"*. The name is carried into the N/A summary, so a skipped check says **which** condition it failed rather than only that one did.

The values are matched case-insensitively against everything found at the rule's paths — a fan-out path such as `/objectives[]/objectiveType` yields one value per objective, and the rule passes if any of them matches. A rule with no accepted values is never satisfied rather than vacuously true, because a rule that silently disables itself is the worst possible failure in something that decides whether a check runs at all.

**Where the vocabulary is enforced matters more than it looks.** These value sets duplicate the "One of:" lists in the canonical schema, and that duplication is deliberate. An enum in the *schema* is enforced by `CanonicalModelValidator.StripEnumViolations`, which deletes values it does not recognise — extraction has been observed writing `RetirementObjective` where the schema documents `Pension`, and an enum would have thrown that away, leaving the objective untyped. Enforced *here*, an unrecognised value means the check does not apply; enforced *there*, it means the data disappears. Same vocabulary, opposite blast radius.

That is also why the goal-type rule sits on CHK-002, whose `onAbsent` is `Continue`, and not on CHK-003, which can `Skip`. A check that can skip should not depend on a vocabulary nothing enforces.

---

### 3. `queryGroups[]` — one group per requirement

**Each group is split by role.** `retrieval` decides what the assessor is allowed to see; `verification` decides what it may make of it; `declares` is a build-time assertion about the group, checked by lint and read by nothing at run time. The split is structural because the two failure modes are not alike: a `retrieval` mistake is invisible in the output — the check reports confidently on a pack that never held the answer — while a `verification` mistake shows up in the finding's own reasoning.

Group `G4.1`, in full:

```jsonc
{
  "groupId": "G4.1",
  "requirement": "Client's stated product and investment knowledge level",
  "declares": {
    "assertionCategories": [
      "I"
    ],
    "evidenceCategories": [
      "B",
      "C",
      "D"
    ]
  },
  "retrieval": {
    "canonicalPaths": [
      "/knowledgeAndExperience/perClient[]/statedLevel"
    ],
    "queries": [
      {
        "id": "Q4.1.1",
        "text": "you confirmed you are comfortable with how investments work and understand the risks and rewards",
        "side": "Assertion",
        "targetCategories": [
          "I"
        ],
        "expectSignals": [
          "comfortable",
          "understanding",
          "reasonable",
          "limited",
          "experience"
        ],
        "priority": "Core"
      },
      {
        "id": "Q4.1.2",
        "text": "investment knowledge level none limited good extensive recorded on the fact find",
        "side": "Evidence",
        "targetCategories": [
          "B",
          "D"
        ],
        "expectSignals": [
          "knowledge",
          "None",
          "Limited",
          "Good",
          "Extensive",
          "level"
        ],
        "priority": "Core"
      },
      {
        "id": "Q4.1.3",
        "text": "how would you describe your understanding of financial products and markets",
        "side": "Evidence",
        "targetCategories": [
          "D",
          "C"
        ],
        "expectSignals": [
          "understanding",
          "describe",
          "familiar",
          "aware"
        ],
        "priority": "Supplementary"
      }
    ]
  },
  "verification": {
    "limb": "Consistency",
    "comparison": {
      "method": "ValueMatch",
      "falsePositiveGuards": [
        "A report describing the client as comfortable while the fact find records 'limited' knowledge is a real mismatch — but check whether the fact find predates an explanation given at the meeting.",
        "Self-assessed comfort is not the same as assessed knowledge; treat them as distinct data points."
      ]
    },
    "sufficiency": {
      "ifAssertionAbsent": "Note as a gap; the report should state the knowledge level it relied on.",
      "ifEvidenceAbsent": "Potential Concern: knowledge level asserted with no assessment on file.",
      "minEvidenceCategories": 1
    }
  }
}
```

**Group identity**

| Element | Drives | Purpose | How it is used |
|---|---|---|---|
| `groupId` | Both | Stable handle for the requirement | The unit of one model call and one finding. Also compared against the id the model echoed back, so a group the assessor renamed is counted rather than trusted |
| `requirement` | Verification | What this group answers | The question in the prompt, the finding's own label, and this group's row in the coverage matrix. Like `groupId`, it is taken from the plan and the model's echo is checked against it — a requirement the assessor can restate is not auditable |

**`declares` — build-time only**

| Element | Purpose | How it is used |
|---|---|---|
| `assertionCategories` | Categories the report's own claim comes from | Almost always `["I"]`. Lint **L2** counts these as used |
| `evidenceCategories` | Categories the corroboration should come from | Lint **L1** fails the build where a category declared here is targeted by no query. **Also holds the first reserved slots** in the 12-passage cap, ahead of the merely-targeted ones — the group's statement of where its answer lives decides who keeps a slot when the cap bites |

**`retrieval` — what the assessor gets to see**

| Element | Purpose | How it is used |
|---|---|---|
| `canonicalPaths` | Where the answer lives in the canonical model | Merged with the queries' own paths and resolved against the stored extract. This *is* the assertion side — retrieval from the model rather than the vector store |
| `evidenceSections` | Named headings the evidence sits under | Substring-matched against passage text: a rank key, and each named section **reserves a slot** in the 12-passage cap — but only among targeted categories, so a hint promotes within the request rather than widening it. Routing *within* a category, which is where a large multi-section document otherwise defeats a correct category |
| `queries[]` | The searches themselves | See below |

**`retrieval.queries[]`**

| Element | Drives | Purpose | How it is used |
|---|---|---|---|
| `id` | Verification | Handle for the query | Named in the prompt when all of its `expectSignals` miss, so the assessor sees which question came back empty |
| `text` | **Retrieval** | The string that gets embedded | The single most consequential field in the plan: it *is* the query. Written in **document vocabulary, not check vocabulary** — the embedding matches document language. CHK-003's check text says *"risk reconciliation"*; no report writes that, so the plan searches `"we agreed a risk rating rather than the questionnaire result did not fully reflect"` — how the override actually reads on the page |
| `side` | **Retrieval** | `Assertion` \| `Evidence` \| `Either` | **Decides whether the query runs at all.** `Assertion` is never embedded — its `canonicalPaths` resolve against the stored model. `Evidence` and `Either` go to the vector store (`IsEvidenceSearch` is true for both, because an "Either" phrasing still has an evidence half to find) |
| `targetCategories` | **Retrieval** | Categories a useful hit should carry | **Doubles the query** — see [Retrieval](#how-a-query-actually-reaches-qdrant) — then reserves a slot per category in the cap. The only plan field that reaches the Qdrant filter |
| `expectSignals` | Verification | Concrete tokens showing the search landed | **Evaluated during retrieval but changes nothing about it.** Case-insensitive substring match over the hits; the *result* is a prompt line naming the queries that came back empty, so the model can treat that data point as absent from the case file rather than unretrieved |
| `canonicalPaths` | **Retrieval** | Per-query paths, merged with the group's | The de-duplicated union is what the runner resolves from the stored model |
| `priority` | **Retrieval** | `Core` \| `Supplementary` | **Honoured** when `AppSettings.CoreQueriesOnly` is set: a Supplementary query is then not run at all. 41 of 310 queries are marked Supplementary, so the switch is the cheapest retrieval saving available — and the plan authors chose what it costs, rather than a score threshold choosing for them. Off by default |

**`verification` — what the assessor may make of it**

Most fields here are strings interpolated into the group's prompt, and nothing branches on them — `limb` included. The exception is `comparison.method`: `ValueMatch` and `RangeMatch` now select a code-side comparator as well as printing.

| Element | Purpose | How it is used |
|---|---|---|
| `limb` | `Consistency` \| `Appropriateness` \| `Both` | One line of the group prompt. Consistency groups diff report against file; appropriateness groups apply rules to the merged model |
| `comparison.method` | What kind of agreement counts | Printed, so a narrative match is not scored like a numeric one. For `ValueMatch` and `RangeMatch` it also **selects a code-side comparator** — see [Figures checked in code](#figures-checked-in-code) |
| `comparison.tolerance` | Where near-enough is enough | Printed |
| `comparison.falsePositiveGuards` | The known ways *this* comparison goes wrong | One prompt line each. **The field that earns its keep** — see [below](#falsepositiveguards) |
| `sufficiency.ifAssertionAbsent` / `ifEvidenceAbsent` / `ifBothAbsent` | What to conclude when one side or both come back empty | Makes "nothing found" a *decidable* outcome rather than a stalled group |
| `sufficiency.minEvidenceCategories` | How many distinct categories must corroborate | **Compared in code.** The runner counts the categories the pack reached and, where it falls short, states the shortfall in the prompt as established fact and records it on the finding — so a thinly evidenced requirement says so whether or not the model mentioned it. Model-only groups are exempt: they have no evidence side by design |

A group whose queries are *all* assertion-side is **model-only** (`IsModelOnly`): CHK-001 G1.8 internal contradictions, CHK-007 G7.6 cost arithmetic, CHK-008 G8.5 prominence. All three compare the report against itself, so the runner tells the assessor no evidence was sought. An empty evidence section means "nothing to retrieve here" for these groups and "we looked and found nothing" everywhere else, and confusing the two turns a sound finding into a spurious evidence gap.

### 4. `decision` — rolling up

```jsonc
{
  "noIssue": "Evidence complete, consistent and supports the outcome. Knowledge level and experience in the report match the fact find and risk assessment, the recommended product's complexity is proportionate to that level, and comprehension is corroborated in category C where the product is complex.",
  "potentialConcern": "Material clarification or additional evidence required. The report overstates the client's knowledge relative to the file, recommends a complex product to a client recorded as having limited knowledge, or offers no evidence of comprehension where the product warrants it.",
  "notApplicable": "Not applicable — this check applies to all advised cases.",
  "severityHints": [
    "Highest: complex product recommended to a client the file records as having no or limited relevant knowledge.",
    "High: the report's description of the client's knowledge contradicts the fact find.",
    "Moderate: no record of client questions or explanations where the product has unusual features.",
    "Low: knowledge level stated in the report but not graded in the file."
  ]
}
```

| Element | Purpose | How it is used |
|---|---|---|
| `noIssue` / `potentialConcern` / `notApplicable` | The three-way finding, per check rather than generically | Rendered into the check header at the front of *every* group's prompt — identical text each time, so the provider's prefix cache covers it |
| `severityHints` | Ordered guidance on how bad a given failure is | Keeps severity comparable between runs and between checks |
| `overlayInstruction` | Free text appended to every group's header as `Overlay:` | Carries the CHK-002..009 vulnerability overlay, the `Do not assess` scope exclusions, and CHK-010's cross-check instruction |

---

## What each field actually drives

A query plan does two unrelated jobs, and almost every field belongs to exactly one of them.

**Retrieval** decides *what the assessor is allowed to see*. These fields run before any model call, and a mistake in one is invisible in the output — the check reports confidently on a pack that never contained the answer.

**Verification** decides *what the assessor makes of it*. These fields are strings interpolated into a prompt. A mistake here produces a bad judgement about the right evidence, which at least shows up in the finding's own reasoning.

The split is worth holding onto because the two failure modes need different fixes, and because the retrieval half is much smaller than the plan's size suggests.

| | Fields | Count |
|---|---|---:|
| **Retrieval** | `retrieval.queries[].text`, `.side`, `.targetCategories`, `.canonicalPaths`, `.priority`; `retrieval.canonicalPaths`; `retrieval.evidenceSections`; `triggerProbe.triggerField`, `.queries`, `.onAbsent` | 10 |
| **Verification** | `checkName`; `groupId`, `requirement`; `verification.limb`, `.comparison.method`, `.tolerance`, `.falsePositiveGuards`, `.sufficiency.*` (4); `decision.*` (5); `triggerProbe.absentWhen`; `retrieval.queries[].id`, `.expectSignals` | 19 |
| **Build-time only** | `primaryCategories`, `declares.assertionCategories`, `declares.evidenceCategories` | 3 |
| **Nothing at all** | — *(all nine dead fields were removed, and `planVersion` and `priority` were wired instead)* | 0 |

`checkId` is in both halves — it matches the plan to its check before retrieval and labels every finding after it. `planVersion` sits outside both: it gates the load.

**This table used to have nine fields in the last row.** The structure was refined against this analysis: the dead fields were deleted from the schema and all ten plans, `planVersion` and `priority` were wired instead of removed, and each group was split into `retrieval` / `verification` / `declares` so the role of a field is visible from where it sits.

### The retrieval half, in order of leverage

1. **`queries[].text`** — the only field that becomes a vector. Everything else narrows, ranks or filters what that vector returns. A query written in check vocabulary rather than document vocabulary fails silently and completely.
2. **`triggerProbe.triggerField` + `onAbsent`** — the largest single lever, because `ReturnNA` deletes the entire retrieval pass. Four checks can end before a single group is gathered.
3. **`side`** — gates whether a query runs at all. An `Assertion` query is never embedded, so mislabelling an evidence query as `Assertion` removes it from retrieval without removing it from the plan.
4. **`targetCategories`** — the only field that reaches the Qdrant filter, and the only one that reserves capacity in the 12-passage cap.
5. **`evidenceSections`** — routing *within* a category, which is where a large multi-section document otherwise defeats a correct category.
6. **`canonicalPaths`** — retrieval too, just from SQLite rather than Qdrant. This is the whole assertion side.

### The verification half

Most of these are strings that end up in a prompt with nothing in the runner branching on them — `limb` reaches the model as the word "Consistency" and nothing more. `comparison.method` used to be the same, and is now the one exception: `ValueMatch` and `RangeMatch` select a code-side comparator as well as printing. `SetCoverage`, `PresenceOnly` and `NarrativeAlignment` still only print, because none of them is mechanical.

Two fields sit awkwardly across the line and are worth naming precisely:

- **`expectSignals` is evaluated during retrieval but changes nothing about it.** The substring scan runs over the hits a query already returned; its output is a prompt line naming the queries that came back empty. It cannot cause a re-search, widen a filter, or promote a passage. It exists so the model can tell *absent from the file* from *absent from the pack* — which for most checks is the finding.
- **`groupId` and `requirement` are also an audit control.** Both are taken from the plan when the finding is built, and the values the model echoed back are recorded separately and compared. A requirement the assessor can quietly restate is not auditable; the divergence is counted rather than trusted.

### Redundant fields — removed

Nine fields were read by nothing, two of them not even deserialised. All nine are gone from the schema and from the ten plans, and [a test](../../../tests/AiPromptEvaluator.Tests/CheckPlanStructureTests.cs) fails the build if one comes back. Recorded here because the reasoning is what stops them being re-added.

| Field | Was | Why it was dead |
|---|---|---|
| `tool.*` | **Discarded at load** | `CheckQueryPlan` has no `tool` property, and the deserialiser ignores unmapped members. The block describes `search_case_documents`, which does not exist |
| `budget.*` | **Discarded at load** | Same — no property, silently dropped. Its `estimatedPassages` was also wrong for years, counting one Qdrant query per plan query when there are two |
| `planVersion` | Parsed, never read | Nothing compares it. `"1.0"` is a promise the loader does not keep — a plan at any version loads identically |
| `appliesTo` | Parsed, never read | The form shows the CSV's value; `triggerProbe` decides applicability |
| `regulatoryBasis` | Parsed, never read | The prompt uses the CSV's value |
| `triggerProbe.presentWhen` | Parsed, never read | Its counterpart `absentWhen` is quoted to the assessor. There is no reason for the asymmetry beyond nobody having wired it |
| `queries[].priority` | Parsed, never read | 269 Core and 41 Supplementary, and all 310 run |
| `queries[].note`, `triggerProbe.note` | Parsed, never read | Author's asides that reach nothing |

Dead weight in a plan is not free: every one of these read as live to whoever edited the file next, and `tool.categoryFilterAvailable: false` in particular asserted something about the search path that had stopped being true.

`planVersion` and `priority` were kept and wired rather than deleted — both were meant to do something, and both now do.

### Fields that could earn more than they do

All eight have now been acted on and are struck through. Seven changed behaviour; the eighth was built and deliberately left switched off.

Ranked by what they would buy against what they would cost.

| Field | Today | What wiring it would buy |
|---|---|---|
| ~~`queries[].priority`~~ | **Done** | `AppSettings.CoreQueriesOnly` now skips Supplementary queries — 41 of 310, about 13% of retrieval. Off by default, since the full set is what the coverage matrix describes |
| ~~`sufficiency.minEvidenceCategories`~~ | **Done** | The comparison happens in code; the shortfall is stated in the prompt and recorded on the finding, so it no longer depends on the model noticing |
| ~~`declares.evidenceCategories`~~ | **Done** | Declared categories now take the first reserved slots in the cap, ahead of the merely-targeted ones |
| ~~`planVersion`~~ | **Done** | `CheckQueryPlanLoader` refuses a plan whose version it does not know, naming it in the run output instead of half-reading it |
| ~~`comparison.method`~~ | **Done** | `ValueMatch` and `RangeMatch` select a code-side comparator. `SetCoverage`, `PresenceOnly` and `NarrativeAlignment` stay prose — none of them is mechanical |
| ~~`triggerProbe.presentWhen`~~ | **Removed** | Deleted rather than wired. `absentWhen` carries the case that matters — a check that was skipped — and a second field saying why a check ran was never worth its weight |
| ~~`resultsPerCall`~~ | **Mechanism done, unused** | Now a plan-level `retrieval.resultsPerCall`. No shipped plan sets it, because the measurement did not support the rationale — see [Per-plan retrieval width](#per-plan-retrieval-width) |
| ~~`queries[].note`~~ | **Removed** | Never populated in any shipped plan, so there was nothing to surface |

**One caveat on the lint fields.** `primaryCategories`, `expectedCategories.assertion` and `expectedCategories.evidence` are marked *test-time only* rather than dead, and the distinction matters: [CheckPlanLintTests](../../../tests/AiPromptEvaluator.Tests/CheckPlanLintTests.cs) runs the lint over every shipped plan and fails the build on a violation. They constrain what can be committed, not what happens at run time. `CheckPlanLint` has no production caller.

---

## How a query actually reaches Qdrant

```
plan JSON → CheckPlanRunner → CaseDocumentSearchService → CaseDocumentStore → Qdrant
```

`CaseDocumentSearchService` embeds `query.text` **once** and runs it **twice**:

```csharp
var hits = await _store.SearchAsync(
    _caseReference, _settings.TenantId, vector, _settings.MaxSearchResults,
    categoryCodes: null, cancellationToken);

if (categoryCodes is { Count: > 0 })
{
    var targeted = await _store.SearchAsync(
        _caseReference, _settings.TenantId, vector, _settings.MaxSearchResults,
        categoryCodes, cancellationToken);

    hits = [.. targeted, .. hits];
}
```

Once unfiltered across the whole case, once with the plan's categories, targeted hits first. **`targetCategories` is not a narrowing filter.** Restricting alone would be wrong — a plan's target categories are where the evidence is *expected*, not the only place it can be, and a hard filter would silently discard a contradiction sitting in a category nobody thought to name. Searching unfiltered alone is the old behaviour, and small categories lose that similarity competition every time. The second query costs almost nothing: the embedding is the expensive part and it is shared, leaving one more vector lookup against an indexed payload field.

In the store, that filter is three `Must` conditions:

```csharp
filter.Must.Add(Conditions.Match(TenantKey, tenantId));
filter.Must.Add(Conditions.MatchKeyword(CaseKey, caseReference));
if (categoryCodes is { Count: > 0 })
    filter.Must.Add(Conditions.Match(CategoryCodeKey, categoryCodes.ToList()));
```

Tenant and case reference are **constructor-injected** into the search service and cannot be overridden per call, so a search can only ever reach the case the run is about.

### Ranking, and why there is a floor

The union is de-duplicated on **the passage text itself** — not its hash code, which is per-process seeded, so a collision would drop a genuinely distinct passage and a different one on the next launch. Then `Rank` orders by:

1. targeted category
2. not a form skeleton — an unfilled form grid is short, generic, embeds near any query and asserts nothing
3. mentions a declared `evidenceSections` heading
4. score
5. document name, then passage text — the reproducibility keys; scores collide often enough to matter, and an approximate index is under no obligation to return a tie band in a stable order

Then slots are reserved before the cap of `MaxPassagesPerGroup` (12): one per declared section, then one per category — **`declares.evidenceCategories` first**, then the merely-targeted ones. Lint holds declared to a subset of targeted, so that tier cannot widen a request; it settles who keeps a slot once the cap bites.

That floor exists because the obvious ordering silently lost whole documents. Sorting by "is this category targeted" and then by score looks like it favours targeted categories, and it does — until every candidate is targeted, which is normal once a group names four or five. The first key then returns the same value for everything, the order collapses to pure score, and `Take` keeps whichever documents happen to embed closest.

Measured, that meant the Fact Find. Its prose scores below research and report prose for almost any query, so passages retrieved *specifically because a plan asked for them* were evicted before the assessor saw them: three checks reached it in **zero groups out of nineteen** while every one of their packs sat exactly at the cap, and six of eight missed benchmark findings were facts recorded only there. Fixing the plans to ask for the category was necessary and did nothing on its own — asking is not the same as keeping.

---

## Query text in document vocabulary

Each group carries at least one `side: "Assertion"` query worded the way a suitability report writes ("*you are currently aged and married*", "*this switch is in your best interests for the following reasons*") and at least one `side: "Evidence"` query worded the way the source document writes ("*attitude to risk questionnaire result score profile*", "*transfer value quotation surrender value*"). Both sides of the diff get written even though only one of them is ever searched — the assertion phrasing is what a reader needs to see to judge whether the canonical path resolved to the right thing.

## `falsePositiveGuards`

Naïve field comparison over this domain generates mostly noise, and each guard kills a specific known false positive:

- gross vs net figures (CHK-001) — the single largest source of spurious mismatches
- weekly amounts annualised at 52, not 4.33 × 12 (CHK-001)
- provider risk scales that are not interchangeable — a 5 on 1–10 is not a 5 on 1–7 (CHK-003)
- a questionnaire result differing from the agreed rating is an *override*, not a mismatch (CHK-003)
- fees taken from the fund are not an income commitment and must not be tested against monthly surplus (CHK-005)
- pension access restrictions are irrelevant for a client already over minimum pension age (CHK-005)
- illustration growth rates are prescribed and will differ from cashflow assumptions (CHK-007)
- chunk retrieval order does not indicate page position, so prominence must be judged on headings (CHK-008)
- age alone is not vulnerability (CHK-010)

---

## Figures checked in code

`comparison.method` looked like an enum with behaviour behind it and had none. For `ValueMatch` and `RangeMatch`, [NumericComparison](../../../src/AiPromptEvaluator.Core/Services/Assessment/NumericComparison.cs) now does the arithmetic before the assessor is asked anything: every number the canonical model asserts, checked against every number in the group's passages.

```
#### Figures checked in code
Method ValueMatch: figures must agree exactly.
- £2,450.00 — corroborated by £2,450.00 in [P3].
- £110,000.00 — not matched. Nearest is £116,997.47 in [P7], 6.4% away.
- 0.93% — no comparable figure in any passage.
```

It follows the same reasoning as [DerivedFigures](../../../src/AiPromptEvaluator.Core/Services/Assessment/DerivedFigures.cs), which exists because delegating arithmetic to the assessor was measured failing both ways: a figure present in a passage but unnoticed reads as an evidence gap, and a figure absent from every passage gets confirmed from a number that resembles it.

Three properties keep it honest:

- **It never reaches a verdict.** A matched figure is not a passed requirement — the right number from the wrong document, or as at the wrong date, is still a finding, which is what `falsePositiveGuards` are for. The block says so in the prompt.
- **Near misses are named, not reported absent.** The near miss is usually *the* finding: £110,000 stated against £116,997.47 on file is the discrepancy, and "no corroborating figure" would bury it.
- **Like is compared only with like.** Money, percentages and plain numbers are separate, and a digit run touching a letter or a slash — policy numbers, NI numbers, dates — is skipped entirely. A spurious match is worse than a miss, because it tells the assessor a figure was corroborated when nothing corroborated it.

`RangeMatch` allows 1%, published in the prompt. The plans' own `tolerance` strings are prose written for a reader ("convert to a common frequency first"), and parsing them into a number would be guessing at what the author meant.

---

## Per-plan retrieval width

A plan may set `retrieval.resultsPerCall` to widen its candidate pool:

```jsonc
"retrieval": { "resultsPerCall": 16 }
```

The mechanism has a property worth knowing: **raising it costs vector lookups, not prompt tokens.** `MaxPassagesPerGroup` still caps what reaches the assessor at 12, so a wider pool changes what ranking gets to choose from and not how much the model reads. A value *below* the global setting is ignored rather than honoured, so a plan cannot quietly retrieve less than the rest of the run.

**No shipped plan sets it**, and the reason is worth recording, because the rationale that motivated the field did not survive being measured.

The argument was that the wide checks — CHK-007 and CHK-009 reach eight and seven document categories — are held to the same eight hits per query as narrow ones like CHK-010, and lose passages the narrow checks never had to. Counting the actual candidate pool per group says otherwise:

| Check | Groups | Evidence queries | Candidates per group | Cap |
|---|---:|---:|---:|---:|
| CHK-010 | 5 | 18 | ~58 | 12 |
| CHK-005 | 8 | 22 | ~44 | 12 |
| CHK-009 | 10 | 27 | ~43 | 12 |
| CHK-007 | 11 | 25 | ~36 | 12 |
| CHK-006 | 8 | 11 | ~22 | 12 |

CHK-010 has the **widest** pool per group, not the narrowest, because its five groups carry more evidence queries each than CHK-007's eleven do. And every check already produces at least 22 candidates for 12 slots, so none of them is starved at the retrieval step — the pressure is at the *cap*, which `resultsPerCall` does not touch.

What would justify setting it is evidence that a wanted passage is currently ranked below position 8 *within its own query's results* — a different measurement, and one that needs a labelled case rather than the plans alone. Until that exists, a number here would be a guess, and a guess that silently changes what a check retrieves is worse than the default it replaced.

---

## How a plan is executed

1. **`triggerProbe` runs first.** It reads the canonical model's own `checkTriggers` field and corroborates it with the probe searches. Where `onAbsent` is `ReturnNA` (CHK-003, 005, 006, 009) a missing trigger settles the check immediately, skipping the whole retrieval pass.
2. **Retrieval runs for every group concurrently.** Retrieval for one group has nothing to do with retrieval for another. Assertion queries resolve from the stored model; evidence queries are searched, de-duplicated, ranked and capped at 12. Results land in an array by position, so the pack order matches the plan's however the searches finish.
3. **Verification: one model call per group**, also concurrent. Each sees its own assertions, its own passages, its own `comparison` guards and `sufficiency` rules, and the check header — and knows nothing of its neighbours. A check with nine groups previously produced nine verdicts in one generation of several thousand lines, where each group's reasoning was conditioned on every group written before it and none could be retried alone.
4. **Citations are verified per group**, against that group's evidence only — never the check's whole evidence, or a quote lifted from a neighbouring group's passages would verify.
5. **The check's outcome is computed, never asked for.** `CheckFinding.FromGroups` rolls the group findings up, so the headline cannot disagree with them, and cannot be stated before they exist.

Where a query's `expectSignals` appear in none of its hits, the runner says so explicitly: that data point is absent from the case file, not merely unretrieved.

## Cost

297 queries across all ten checks, each returning up to `MaxSearchResults` (8) — and up to twice that where `targetCategories` is set, which is nearly everywhere, so the upper bound before de-duplication is around 4,700 passages per case. Heavy overlap is expected, since the same charges table answers parts of CHK-001, CHK-007 and CHK-009.

Little of that reaches a prompt: the runner de-duplicates within a group and caps at 12, and `triggerProbe` skips inapplicable checks entirely.

The suitability report itself is read **once**, at extraction, not per check.

## Known gaps

- ~~`Revised checks.csv` cannot be loaded by the app~~ — **resolved.** [AssessmentCheckLoader](../../../src/AiPromptEvaluator.Core/Services/Assessment/AssessmentCheckLoader.cs) now maps columns by header name, tolerates a missing `Regulatory Basis`, and skips the `Rules:` row. Either file loads. [Revised checks (loadable).csv](../../QA-Checks/Revised%20checks%20(loadable).csv) is still the one to prefer, because it folds the two workbook rules into each bound check's prompt — the loader can fix column order, not missing content.
- ~~`tool` describes a component that no longer exists~~ — **resolved.** The block is gone from the schema and all ten plans.
- ~~Nine fields read by nothing~~ — **resolved.** All nine were removed, `planVersion` and `priority` wired instead, and a test fails the build if any return. See [What each field actually drives](#what-each-field-actually-drives).
- **`CHK-004`'s revised `Applies To` reads "Client Circumstances & Suitability"** — a theme, not a case population, where every other check names one. Carried through as given; it looks like a data-entry slip in the source.
- **`CHK-010` keeps primary categories A and D**, which the revised check drops. Category D holds the risk and capability answers that identify an FG21/1 low-capability driver, and A holds the third-party authority the revised check asks about by name. Both are pinned by a regression test recording a real miss; the narrower revised list would undo it.
- **`CHK-009`'s revised Decision Logic omits Potential Concern**, giving only No Issue and N/A. The plan keeps its existing `potentialConcern` text, since a switch check that cannot raise a concern would be inert.
- **An assertion path cannot tell "the report does not say it" from "we never read it."** A
  `side: "Assertion"` query resolves against the stored canonical model, and where the section
  it reads failed to extract, the path resolves to nothing — which is exactly what a genuinely
  absent value looks like. The group then reports the data as absent from the file, which is a
  finding about the advice rather than about the run. Extraction now retries a malformed reply
  once and names any section that still failed, so the run output says which ones to distrust,
  but the plans themselves have no way to see it.
- **Query text is tuned to UK retail pension and investment reports.** Protection, mortgage and equity release cases will need their own vocabulary; the group structure holds, the phrasings do not.
- **`expectSignals` are heuristics.** They tell you a search probably landed; they do not confirm the passage is relevant. The model still has to read.
- **Chunk boundaries can split tables.** With `MaxTokensPerChunk` at 600, a wide existing-arrangements table may be cut mid-row, so a per-plan value can retrieve without its column header. Several plans query the same table from different angles for this reason.
