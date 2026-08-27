# Extraction — how the canonical model is populated

How the pipeline turns a suitability report into the canonical model that every check is later
scored against. This is a reference for the extraction stage specifically: what runs, in what order,
with which parameters, what the model is told, and what the process does around the model to keep the
result trustworthy.

The authority is the code. Where this document and the source disagree, the source is right — the
key files are named at each point so a reader can check.

- Orchestration: [`CanonicalModelExtractor.cs`](../../src/AiPromptEvaluator.Core/Services/Extraction/CanonicalModelExtractor.cs)
- The passes: [`CanonicalModelDocument.cs`](../../src/AiPromptEvaluator.Core/CanonicalModelDocument.cs) (`ExtractionSection.All`)
- The system prompt: [`Prompts.cs`](../../src/AiPromptEvaluator.Core/Constants/Prompts.cs) (`ExtractorSystem`)
- Schema slicing: [`JsonSchemaSlicer.cs`](../../src/AiPromptEvaluator.Core/Services/Extraction/JsonSchemaSlicer.cs)
- Cross-reference ids: [`CanonicalModelIdentityRegistry.cs`](../../src/AiPromptEvaluator.Core/Services/Extraction/CanonicalModelIdentityRegistry.cs)
- Reply parsing and salvage: [`ExtractionResponseReader.cs`](../../src/AiPromptEvaluator.Core/Services/Extraction/ExtractionResponseReader.cs)
- Vocabulary correction: [`CanonicalVocabulary.cs`](../../src/AiPromptEvaluator.Core/Services/Extraction/CanonicalVocabulary.cs)
- Trigger cross-check: [`TriggerConsistency.cs`](../../src/AiPromptEvaluator.Core/Services/Extraction/TriggerConsistency.cs)
- Sampling and limits: [`AppSettings.cs`](../../src/AiPromptEvaluator.Core/AppSettings.cs), [`ChatCompletionClient.cs`](../../src/AiPromptEvaluator.Core/Services/Ai/ChatCompletionClient.cs)

---

## 1. What extraction is, and why it exists

The pipeline validates a suitability report against the evidence that supports it. The report makes
assertions; the supporting documents — the fact find, the file note, the risk profile, the provider
illustrations — are what those assertions are checked against. The two are kept strictly apart.

**Extraction reads the report and produces the canonical model: a structured record of what the
report asserts.** It runs **once per case**. Everything a check later needs from the report is read
out of the stored model, so the report itself is never sent to a model again. A check run then costs
retrieval over the *evidence* documents plus one decision call — not a re-parse of a twenty-page
report per requirement.

This division is deliberate and enforced in code. The extractor reads **category `I` and nothing
else** — the folder holding the suitability report — because *"the canonical model describes what the
report asserts, and evidence documents are what the report is later checked against; folding them in
here would erase the distinction the checks depend on"*
(`CanonicalModelExtractor.SuitabilityReportCategoryCode`). The consequence to hold onto: a
questionnaire answer or a fact-find figure is **not** in the canonical model, by design — it reaches
a check through retrieval, on the evidence side.

**The governing principle for the whole stage: a canonical model that quietly reconciles two figures
has destroyed the finding before any check runs.** Extraction is optimised for faithfulness to what
the document says — including its contradictions, its gaps and its boilerplate — not for producing a
tidy record.

---

## 2. The shape of a run

One `ExtractAsync` call for a case does the following (`CanonicalModelExtractor.ExtractAsync`):

1. **Find the report.** Every indexable file under the case's category `I` folder, ordered by name
   (`FindReportFiles`). More than one is allowed; they are concatenated (§7).
2. **Load the schema.** The canonical-model JSON Schema, resolved from settings or beside the
   executable (`ResolveCanonicalSchemaPath`). The schema is the single source of truth for structure
   *and* for the closed vocabularies (§8).
3. **Read the report text once**, preserving the converter's page markers (§7).
4. **Run twelve passes in sequence** (§3), each filling a slice of the model. State accumulates in one
   growing JSON object; each pass sees what the passes before it produced.
5. **Resolve cross-references** across the finished model and report any that dangle (§6).
6. **Stamp provenance** the process owns — who extracted it and when (§9).
7. **Return** the assembled model, the per-section failures, the cost, the vocabulary corrections, and
   any trigger contradictions. **Writing it to the store is the caller's job** — a run the user cancels
   or that fails every section does not overwrite a good model with a worse one.

The passes run **sequentially, not in parallel**, and the order matters (§3). The report text is held
constant at the front of every pass so the provider's prefix cache covers the expensive part; only
the section instruction and its schema slice change from pass to pass.

---

## 3. The twelve passes

The report is not extracted in one call. A full canonical model runs to tens of thousands of tokens
of JSON, and a reply that hits the output cap halfway through an array is not partially useful — it is
unparseable. So the model is built a **section at a time**, and the section is also the unit of retry:
one section failing costs one pass, not the whole document.

The passes, in run order (`ExtractionSection.All`). Each names the top-level canonical properties it
owns:

| # | Pass | Canonical properties |
|---|------|----------------------|
| 1 | Case and parties | `source`, `case`, `parties`, `clientAuthority` |
| 2 | Financial position | `financialPosition` |
| 3 | Objectives | `objectives` |
| 4 | Risk, knowledge and vulnerability | `riskAssessment`, `knowledgeAndExperience`, `vulnerability` |
| 5 | Existing arrangements | `existingArrangements` |
| 6 | Needs analysis and research | `needsAnalysis`, `research` |
| 7 | Solution | `solution` |
| 8 | Recommendations | `recommendations` |
| 9 | Costs and charges | `costsAndCharges` |
| 10 | Replacement analysis | `replacementAnalysis` |
| 11 | Disclosures | `riskWarningsAndDisadvantages`, `taxTreatment`, `ongoingService` |
| 12 | Triggers and extraction report | `checkTriggers`, `extractionReport` |

**Every top-level property is claimed by exactly one pass** — a build test
(`ExtractionCoverageTests`) fails if the schema grows a property no pass asks for, or a pass asks for a
property the schema does not define.

Two ordering decisions are load-bearing:

- **Solution (7) runs before Recommendations (8)** because a recommendation carries a `targetPlanId`
  pointing into `solution.recommendedPlans`. Every cross-reference in the model runs *forwards*, so
  the identifier table (§6) always holds the ids a pass needs by the time that pass runs.
- **Triggers and the extraction report (12) run last** because that pass reports on the extraction as
  a whole — it needs to see what every other pass produced (§5).

---

## 4. The system prompt

The same system prompt is sent on every pass (`Prompts.ExtractorSystem`, via `BuildSystemPrompt`). It
is standing instruction; only the user prompt changes per pass (§5). It is reproduced in full because
the rules are the contract:

```
You extract structured data from UK financial services suitability reports into a
canonical JSON model. You are building the record a compliance assessor will later
rely on, so accuracy about what the document does NOT say matters as much as accuracy
about what it does.

Rules:
- Return one JSON object and nothing else. No prose, no markdown fences.
- Populate only the properties named in the request. Omit anything you cannot fill.
- Never invent a value. If the report does not state something, either omit the field
  or set its provenance assertionStatus to "Absent" — a plausible guess is worse than
  a gap, because it will read as evidence.
- assertionStatus: "Stated" when it is explicit in the text; "Inferred" when you read
  it out of narrative prose; "Derived" when you calculated it from other values;
  "Absent" when the model expects it and the report does not provide it.
- modality is a different question and is answered separately: assertionStatus says how
  you came to know the value, modality says what the report is claiming about it. A
  salary the client earns today and a retirement income projected thirty years out are
  both "Stated"; only modality separates them, and a check comparing the second against
  a fact find as though it were the first reports a discrepancy that does not exist.
  "Actual" true at the report date · "Reported" the client said so · "Intended" the
  client plans it · "Assumed" an adviser or modelling assumption · "Projected" a future
  value from an illustration or cashflow · "Recommended" part of the advice rather than
  the current position · "Conditional" holds only if a stated condition does, including
  a charge phrased as "if you were to retain the plan" · "Unknown" not determinable.
- Use the identifiers given under "Identifiers" exactly as written. Every id field and
  every *Ids array must hold an id from that table. If something you would reference is
  not in the table, omit the reference — never coin a new id, and never put a name, a
  label or a description in an id field.
- Every Stated or Inferred provenance needs the page number from the nearest
  "<!-- page: N -->" marker above it, and a quote where the rules below call for one.
- Quote where the value is contestable: figures, dates, percentages, ratings, and any
  statement a check might have to weigh. For descriptive prose and boilerplate the page
  number alone is enough.
- Quote each passage once. Where several assertions rest on the same sentence, quote it
  on the first and give only the page number on the rest. A repeated quote adds nothing
  the page number does not.
- Keep quotes to the shortest span that carries the assertion — normally one clause, at
  most one sentence. Never quote a table row wholesale where one cell is the evidence.
- Keep the document's own units. "£300 per week net" is amount 300, basis "Net",
  frequency "Weekly" — do not convert to monthly and lose the original.
- Record contradictions rather than resolving them. If the report gives two different
  figures for the same thing, capture both where the model allows it and note the
  conflict; do not quietly pick one.
- This bites hardest where the same quantity is tabulated twice. A report states a plan's
  existing charge in an early arrangements table and again in a later switch-charges
  table, and the two disagree; the model has a separate home for each, so fill each from
  its own table, with its own page. Read every cell from the table in front of you: do
  not substitute a figure extracted earlier because it looks more likely, and do not skip
  a row for disagreeing with one already recorded. Two readings quietly made to agree look
  like clean data and have destroyed the finding a check was looking for.
- Boilerplate is data. Capture generic paragraphs with isClientSpecific false rather
  than skipping them — their presence without personalisation is itself a finding.
- A description reading "One of: A, B, C." is a closed vocabulary, not a suggestion.
  Use one of those values, spelled and capitalised exactly as listed, or omit the field.
  Do not coin a variant: "RetirementObjective" where the list says "Pension" is a new
  value, not a more precise one, and every rule that reads the field will miss it.
- Where the report describes something the vocabulary has no value for, use "Other" if
  the list offers it and omit the field otherwise. Say what the report actually said in
  the neighbouring free-text or provenance field, where nothing is constrained.
- Write each property name at most once per object. A name repeated in the same object
  is invalid JSON that most parsers accept and then fail on later, and it has cost whole
  sections: one pass wrote "numberOfFundsAvailable" twice inside the same arrangement,
  with the same value both times, and five pension plans were lost with it.
- A provenance block closes exactly one object. Count the closing braces after a long
  quote before moving to the next property: one closer too many ends the parent as well,
  and everything after it is written at the wrong depth.
```

**Note that none of these rules names a provider, a client or a document from a particular case.** The
pipeline validates whichever report it is given; a prompt naming one case would bias the extractor
toward that case and away from the one in front of it. A build test (`AssessorPromptTests`) pins that
the prompt stays generic.

The rules that matter most for downstream honesty:

- **Never invent a value** — a gap is more useful than a guess, because a guess reads as evidence.
- **assertionStatus vs modality** are two separate questions: *how you came to know a value* versus
  *what the report claims about it*. This prevents a projected retirement income being compared
  against a fact-find figure as though both were current.
- **Record contradictions, do not resolve them** — the core principle, with the twice-tabulated-charge
  case called out explicitly, because that is where a well-meaning model is most tempted to reconcile.
- **Closed vocabularies must be used verbatim** — a coined variant is a value every rule that reads
  the field will miss.

---

## 5. The per-pass user prompt

Built by `BuildSectionPrompt`. Its structure is fixed, and the order is chosen to keep the prefix
cache warm:

1. **The full report text**, headed `# Suitability report for case {ref}`. Identical on every pass —
   this is the expensive prefix the provider's cache covers.
2. **The identifier table**, once any entity has been assigned an id (§6). Omitted on the first pass,
   when nothing has been keyed yet.
3. **The extraction summary**, on pass 12 only (§5a).
4. **The section instruction**: the pass name, its one-line description (from `ExtractionSection`), the
   list of top-level properties to return, and the **sliced schema** (§8).

Only items 2–4 change between passes, and they sit at the *end* of the prompt for the same reason the
cache-bypass marker does — a change near the front would invalidate the cached document that every
pass re-reads.

### 5a. The extraction-report pass sees what came before

Pass 12 is the one pass that is about the extraction rather than the report. It is given a summary
of what the previous eleven passes produced (`SummariseExtraction`):

- **Populated sections** — top-level properties that came back with content.
- **Sections that came back empty** — present but empty.
- **Sections whose extraction pass failed** — the pass errored, so the property was never written.

The distinction between *empty* and *failed* is the point. An empty section means the report was
silent on it, which for many checks is itself the finding; a failed section means the report was
never read for it, which says nothing about the advice. The model is asked to record the first as
`expectedButAbsent` where appropriate, and the second under `expectedButAbsent` with reason
`PresentButUnparseable` while lowering `overallConfidence`. Deciding *which* an empty section is — the
report saying nothing versus the extraction having missed it — is left to the model, because that is
the part that needs judgement.

---

## 6. Cross-reference identifiers

The canonical model is relational: a recommendation points at the objectives it serves
(`linkedObjectiveIds`), the arrangements it affects (`sourceArrangementIds`), and the plan it
produces (`targetPlanId`). CHK-006 is precisely the question of whether those links hold. But the
model is extracted a section at a time, in separate calls, and a call that has not seen the objectives
pass cannot know what it called them.

`CanonicalModelIdentityRegistry` keeps the ids consistent across passes, with **no extra model call**:

- **Adopt.** As soon as a pass that defines entities completes, they are assigned canonical ids —
  clients `C1, C2, …`, objectives `OBJ1, …`, existing arrangements `EA1, …`, recommended plans
  `NP1, …`, recommendations `REC1, …`. The naming is the code's decision, not the model's. An entity
  keeps the id it is first given, so a later pass restating it does not renumber it out from under a
  reference already written.
- **Table.** What has been adopted so far is rendered into every later pass's prompt, each id beside a
  human-readable label (a name, provider, scheme or policy number), with the instruction to use those
  ids exactly and coin none.
- **RewriteReferences.** After all passes, references that used an entity's pre-canonical id are
  repointed onto the canonical one.
- **DanglingReferences.** Anything that still resolves to nothing is reported as an extraction failure
  — checked against *that kind's* ids, so an objective reference holding a recommendation id (a type
  error) is caught rather than waved through because the id exists somewhere.

This exists because of an observed failure: the objectives pass assigned `OBJ-001..003` while the
recommendations pass invented `"LowerCosts"` and `"ConsolidationAndClarity"` — seventeen references,
none resolving, and no error, because each pass was internally consistent.

---

## 7. Reading the report

`ReadReportsAsync` concatenates the category `I` files, each introduced by a
`<!-- document: {filename} -->` marker and separated by a `---` rule. Crucially it **keeps the page
markers** the document converter left behind (`<!-- page: N -->`), because those are what let a
provenance record cite a page rather than a character offset — and the system prompt requires a page
number on every Stated or Inferred value.

---

## 8. Schema slicing

Sending the whole schema on every pass would spend roughly ten thousand tokens per call describing
fields the pass is not being asked to fill. `JsonSchemaSlicer.Slice` cuts a section-sized,
self-contained schema instead:

- It keeps only the top-level properties the pass owns.
- It walks the `$ref` graph from those properties transitively and includes only the `$defs` actually
  reachable — a definition that refs another pulls it in too, so the slice never dangles.
- Definitions are emitted in the schema's own order, so the same section slices to **byte-identical**
  text every run and stays prefix-cache-friendly.

`StripCodeOwnedFields` then removes `source.extractedAt` and `source.extractorModel` from the slice,
because the process fills those in (§9) and the model has no way to know them — asked for them, a run
once answered `"2025-01-01T00:00:00Z"` and `"claude-opus"`, plausible guesses in the two places the
"never invent a value" rule could not be enforced.

---

## 9. Provenance the process owns

`StampSource` writes, after the passes, the facts the model cannot know: `extractedAt` (now),
`extractorModel` (the configured model id), and `fileName` (the first report file, if the model did
not already record one). These are stamped rather than requested — see §8.

---

## 10. Parameters

Extraction sends every call through `ChatCompletionClient.RunRawAsync` with the options built by
`ChatOptions` (`AppSettings`):

| Parameter | Setting | Default | Purpose |
|-----------|---------|---------|---------|
| Model | `SelectedModel` | — | The chat model. Recorded on the stored model as `extractorModel`. |
| Max output tokens | `ExtractionMaxTokens` | **32,000** | Per-pass output cap. Headroom over the largest observed section (recommendations, ~17,500 tokens); a truncated pass is salvaged and reported rather than silently lost (§11). |
| Temperature | `Temperature` when `PinTemperature` | **0**, pinned | Pinned so the same input produces the same output. The provider default is a creative temperature — the single largest source of run-to-run disagreement. |
| Top-p | `TopP` when `PinTopP` | **1**, pinned | Nucleus sampling, pinned the same way. |
| Seed | `SamplingSeed` when `PinSeed` | **1**, pinned | Value is irrelevant; holding it constant is the point. |

Each of temperature, top-p and seed is **pinned independently and releasable on its own**, because a
gateway or model can reject one parameter without objecting to the others — Bedrock's Anthropic route
rejects `seed` outright, and some Bedrock inference profiles accept only temperature 1. Turn the
offending pin off in settings rather than abandoning the others.

`TenantId` (default 99) scopes the stored model. The canonical schema path is resolved from
`ResolveCanonicalSchemaPath`.

### Prompt caching

Two caches are in play and the design serves both:

- **The provider's prefix cache.** The report text is identical at the front of every pass, so the
  expensive prefix is written once and read back on passes 2–12.
- **The gateway's response cache.** Sits in front of an OpenAI-compatible endpoint, keyed on the
  request body; there is no header to disable it. `BypassResponseCache` (`PromptCacheBypass`), when
  on, appends **one nonce per run** to the end of each user prompt — enough to miss anything cached
  from an earlier run, placed at the end so the shared prefix within the run still hits the provider
  cache. It is off by default; turning it on gives up determinism deliberately, which is the point
  when you want a genuinely fresh sample rather than yesterday's answer served back.

---

## 11. Resilience: what happens when a pass misbehaves

A pass's reply is read by `ExtractionResponseReader`, which distinguishes three failure modes that
were previously reported identically as "the model did not return a JSON object":

**Malformed but complete** (a stray brace in an otherwise finished answer). The pass is **retried
once**. The retry prompt is not identical — it appends a "second attempt" note *and the digest of the
reply that failed*, because an observed run re-sent the identical prompt and the gateway served the
identical 41,580-character failed reply from cache, in the same second, billed twice. Varying by the
failed reply's digest changes the cache key while staying reproducible for a run that pins its
sampling. Both attempts are counted in the cost. Brackets are never auto-repaired — a wrongly re-nested
fragment would be merged and believed, which is worse than a retry.

**Truncated** (cut off at the output cap, mid-token). `LooksTruncated` detects it (an unclosed fence,
or bracket depth that never returns to zero) and `Salvage` recovers the longest valid prefix, closed
off — keeping every *whole* array element and discarding only the half-written one. An observed run
had four of five recommendations complete when the fifth was cut off; all four were being discarded.
The shortfall is recorded, never merged as though complete.

**Duplicate property names** (a name written twice in one object). This is the worst kind, because it
*parses*: `JsonNode.Parse` accepts it and detonates later when something enumerates the object. The
reader uses `JsonDocument`, which surfaces both, and **keeps the first** — the model writes in
document order, so the first is what it wrote while reading the source and the repeat is a stutter.
Where the two values differ, the first is kept and the difference is reported, because choosing would
be guessing.

A pass that **fails outright** records the failure and continues; its properties are noted as failed
so pass 12 can see they are missing (§5a). A pass that merely **lost a value** (a dropped enum, a
salvaged truncation) still **succeeded** — it is reported as a *shortfall*, not a failure. Conflating
the two once made a run announce eight broken sections when eight had succeeded and merely dropped a
value an enum did not allow. Only if **every** pass fails does the whole extraction throw.

---

## 12. Post-processing the model

Three things happen to the assembled model, in code, after the passes and before it is returned:

**Vocabulary correction** (`CanonicalVocabulary`, applied per pass before merge). Sixty-odd properties
carry a closed vocabulary as prose in their description — `"One of: Pension, Investment, …"` — rather
than as a JSON Schema `enum`. That is deliberate: an enum is *enforced* by deleting unrecognised
values, and the values it deleted were the ones the documents actually use (three runs could not reach
a charge finding because the `basis` strings never survived). So the vocabulary is guidance, read out
of the schema itself, and corrected only where a mechanical rule reaches the documented spelling:
capitalisation, spacing/separators, and a documented value wearing a generic suffix
(`PensionObjective` → `Pension`). A value that cannot be mapped is **left exactly as written and
reported** — deleting it would repeat the mistake the enums were removed to fix, and guessing would be
the "quietly pick one" the extractor is told not to do. (A separate, stricter check,
`CanonicalModelValidator.StripEnumViolations`, does drop values against the schema's *actual* enums,
where any exist.)

**Trigger consistency** (`TriggerConsistency`, on the finished model). The `checkTriggers` a pass
derived are cross-checked against the data beside them: a trigger set `false` while the model itself
implies `true` — `hasComplexProduct: false` next to a product recorded as Complex, or
`hasCapitalContributionsOrWithdrawals: false` next to an arrangement recording contributions — is
reported. It runs one way only, because a wrongly *false* trigger silently switches off a check, and a
wrongly *true* one merely runs a check that costs one call. (Downstream, `CheckPlanRunner` refuses to
gate a check off on a trigger the model contradicts.)

**What is reported back.** `ExtractionResult` carries the assembled document, the per-section failures,
the cost breakdown, the `VocabularyCorrections`, and the `TriggerContradictions`. All of it is
surfaced — the whole reason a value like `objectiveType: "RetirementObjective"` drifted unnoticed is
that nothing was looking.

---

## 13. What the extraction stage deliberately does not do

- **It does not read the evidence documents.** Category `I` only. The fact find, file note, risk
  profile and provider documents reach a check through retrieval, not through the model. This is the
  assertion/evidence split the checks depend on.
- **It does not reconcile contradictions.** Two figures for the same quantity are both kept where the
  model has room; a reconciled pair is a destroyed finding.
- **It does not repair malformed JSON structurally.** It retries once with a varied prompt, salvages a
  truncation, and keeps the first of a duplicate — but never guesses where an unmatched brace belongs.
- **It does not invent the facts the process owns.** The extracting model and timestamp are stamped by
  code, not asked of the model.
- **It does not persist anything.** The caller decides whether to store the result, so a cancelled or
  wholly failed run cannot overwrite a good model.
