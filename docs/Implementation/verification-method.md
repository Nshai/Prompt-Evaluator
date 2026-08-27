# Verification — how a finding is made from a fact and its evidence

How the pipeline turns a retrieved evidence pack into a finding: the comparison of what the
suitability report *asserts* against what the case file *evidences*, and the checks around that
comparison that stop a plausible-but-wrong answer from clearing a requirement.

This is the third of three: [model-extraction.md](model-extraction.md) produces the canonical model of what the
report asserts; [semantic-search.md](semantic-search.md) retrieves the supporting evidence; this
document is what happens when the two are put in front of the assessor and a finding comes back.

The authority is the code. Where this document and the source disagree, the source is right — the key
files are named at each point.

- The assessor's standing rules: [`Prompts.cs`](../../src/AiPromptEvaluator.Core/Constants/Prompts.cs) (`AssessorSystem`)
- Prompt assembly and the model call: [`CheckPlanRunner.cs`](../../src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs)
- The finding's shape and the outcome logic: [`CheckFinding.cs`](../../src/AiPromptEvaluator.Core/CheckFinding.cs) (`FindingSchema`, `GroupFinding.ParsedOutcome`)
- Citation checking: [`CitationVerifier.cs`](../../src/AiPromptEvaluator.Core/Services/Assessment/CitationVerifier.cs)
- Pre-computed arithmetic: [`DerivedFigures.cs`](../../src/AiPromptEvaluator.Core/Services/Assessment/DerivedFigures.cs), [`NumericComparison.cs`](../../src/AiPromptEvaluator.Core/Services/Assessment/NumericComparison.cs)
- Parameters: [`AppSettings.cs`](../../src/AiPromptEvaluator.Core/AppSettings.cs), [`ChatCompletionClient.cs`](../../src/AiPromptEvaluator.Core/Services/Ai/ChatCompletionClient.cs)

---

## 1. What verification is, and the one model call it is

Extraction and search are model-free once set up: extraction reads the report, search embeds and
ranks. **Verification is the one place a model exercises judgement** — it is a single chat call per
requirement that takes the assembled pack and returns a structured finding. Everything else in this
document is the scaffolding that makes that one call trustworthy: what it is shown, what shape its
answer must take, and what is checked about the answer before it is believed.

The unit is the **query group** — one requirement, one model call, one finding. A nine-group check is
nine calls, not one, and their results are consolidated afterward. The finding a group returns has two
halves that mirror the pack: **what the report says** and **what the file says**, then the
discrepancies between them, then a verdict.

The governing principle, stated in the assessor's own rules: *"A consistency requirement is met when
the report's assertion is corroborated by the evidence. It fails when they contradict each other, or
when the report asserts something no document supports."* Verification is that comparison, made once,
per requirement, with the judgement fenced in by the machinery below.

---

## 2. How the query plan frames the verification

The assessor does not decide what to weigh or how — the **query plan does that**, per requirement,
before the model is called. A plan (one per check; see [semantic-search.md §2](semantic-search.md))
splits each group into a `retrieval` block that assembles the pack and a **`verification` block** that
frames the question put to the model. The split is the whole design:

> A mistake in `retrieval` is invisible in the output, because the check reports confidently on a pack
> that never held the answer, while a mistake in `verification` at least shows up in the finding's own
> reasoning.

**No field in the `verification` block branches any code.** Each is a string interpolated into the
group's prompt (§4). What they do is constrain the assessor's judgement without making it: the plan
says what a finding on this requirement should weigh, how spurious mismatches on it arise, and what a
missing side means — and the model still decides whether the requirement is met. That division is why
the same judgement, on the same evidence, is reproducible from the plan rather than re-invented each
run.

### The elements that frame a finding, and what each is for

**Plan level — the same for every group of the check**

- **`decision`** — the outcome definitions (`noIssue`, `potentialConcern`, `notApplicable`), the
  `severityHints` that keep severity comparable between runs and between checks, and any
  `overlayInstruction` (the vulnerability overlay, scope exclusions, CHK-010's re-evaluation note).
  Rendered into the check header at the front of every group's prompt, identical each time so the
  prefix cache covers it. This is what "how to decide" means to the assessor.
- **`checkName`** and the check sheet's own text (`prompt`, `whatToLookFor`, `decisionLogic`,
  `regulatoryBasis`) — the check as authored, put in the header so the finding is grounded in the
  question the check asks rather than the model's paraphrase of it.
- **`triggerProbe.absentWhen`** — quoted verbatim in the N/A summary, so a check that does not apply
  says *why* rather than returning a bare verdict.

**Group level (`verification` block) — the requirement's known shape**

- **`requirement`** — the question put to the model, the finding's label, and the group's row in the
  coverage matrix. Taken from the plan, and the model's echo of it is checked against it, because a
  requirement the assessor can quietly restate is not auditable.
- **`limb`** — `Consistency` (diff the report against the file), `Appropriateness` (apply rules to the
  merged model), or `Both`. One line of the prompt; it tells the assessor which kind of question this
  is. No code branches on it.
- **`comparison`** — how the two sides are compared once both are retrieved:
  - **`method`** — `ValueMatch`, `SetCoverage`, `PresenceOnly`, `RangeMatch` or `NarrativeAlignment`,
    printed as a word so the model knows what kind of agreement counts. Despite being an enum, no code
    branches on it — `ValueMatch` reaches the model as the string "ValueMatch".
  - **`tolerance`** — where near-enough is enough, printed into the prompt.
  - **`falsePositiveGuards`** — one prompt line each, naming the specific ways *this* comparison
    produces spurious mismatches: gross vs net, different as-at dates, incompatible provider risk
    scales. **This is the field that earns its keep** — naive field comparison over this domain is
    mostly noise, and each guard kills one named false positive. A guard explains a *difference*
    between two documents; the prompt is explicit that it does **not** excuse a claim the report makes
    for itself (§4).
- **`issueCategories`** — the kinds of problem this requirement usually raises, from the fixed
  vocabulary, printed as a **steer and not a menu**: the assessor is told to name the kind it actually
  found (including one not listed) and to leave the field empty where the requirement is met. A plan
  that could *close* the vocabulary per group would be the plan deciding the finding, which is the one
  thing the split exists to prevent, so it can only ever tend, never bind.
- **`sufficiency`** — what to conclude when one side or both come back empty (`ifAssertionAbsent`,
  `ifEvidenceAbsent`, `ifBothAbsent`) and `minEvidenceCategories`. This makes "nothing found" a
  **decidable** outcome rather than a stalled group — for most checks, absence *is* the finding, and
  this field is where the plan says whether an absent side clears the requirement or fails it.

### Why the framing lives in the plan

The verification fields are the requirement's known shape written down once — which guards apply here,
what a missing side means here, how bad a given failure is — rather than re-derived by the model each
run. That is the same principle the retrieval side follows: the plan chooses the inputs and frames the
question, the model answers it, and neither does the other's job. It is also why a verification
mistake is recoverable in a way a retrieval mistake is not: a wrong guard or a mis-scoped sufficiency
rule shows up in the finding's own reasoning, where a reader can see it, rather than silently shaping a
pack that never reaches the page. Sections 4 to 7 are how those framed inputs are turned into a
finding and what is checked about the result.

---

## 3. The two sides the assessor compares

The pack put in front of the model has three parts, and the distinction between them is the first
thing the system prompt establishes:

- **Canonical model** — what the report asserts, read from the stored extract by JSON pointer. *"Treat
  it as an accurate record of what the report says. It is not evidence that the assertion is true."*
  This is the **assertion side**.
- **Retrieved passages** — what the rest of the case file holds, quoted verbatim from the supporting
  documents, each with an id like `[P3]` and a category. This is the **evidence side**.
- **Derived figures** — sums, percentages and frequency conversions already computed from the report's
  own values (§6). Neither assertion nor evidence: the arithmetic the report should agree with.

**A passage in category `[I]` is the suitability report itself**, and the prompt is explicit that it
belongs on the assertion side however it arrives: *"The report cannot corroborate itself: never call a
claim corroborated, confirmed or supported by an [I] passage. Two agreeing is internal consistency,
not support; two disagreeing is a finding."* This is the fact-versus-evidence line drawn sharply — a
report cannot be its own evidence, and a check that let it be would clear on the strength of the
document it is auditing.

---

## 4. The prompt the assessor is given

The assessor prompt is built in two parts so the provider's prefix cache pays off across a check's
groups (`CheckPlanRunner`):

**The system prompt** (`Prompts.AssessorSystem`) — the standing rules, identical on every call. It is
reproduced in full because it is the specification the model works to:

```
You are a financial services Quality Assurance assessor. You assess ONE requirement
against a pre-assembled evidence pack and return a structured finding.

The pack has two sides, and the distinction matters:

- CANONICAL MODEL — what the suitability report asserts. It was extracted from the
  report itself, so treat it as an accurate record of what the report says. It is not
  evidence that the assertion is true.
- RETRIEVED PASSAGES — what the rest of the case file holds, quoted verbatim from the
  supporting documents, each with an id like [P3] and a category. This is the evidence.
- DERIVED FIGURES — sums, percentages and frequency conversions already computed from
  the report's own extracted values. Settled: neither assertion nor evidence, but the
  calculation the report should agree with.

A passage in category [I] is the suitability report itself, so it belongs on the assertion
side however it arrives. The report cannot corroborate itself: never call a claim
corroborated, confirmed or supported by an [I] passage. Two agreeing is internal
consistency, not support; two disagreeing is a finding. Where only the report backs a
claim, the evidence is absent — say so.

A consistency requirement is met when the report's assertion is corroborated by the
evidence. It fails when they contradict each other, or when the report asserts
something no document supports.

Answer the fields in the order they are given. That order is the order to think in:
set out what each side says, list every discrepancy you can see, establish whether the
comparison can be made at all, reason about it, cite, say what kind of problem it is —
and decide last. Do not decide first and explain afterwards.

Rules:
- Judge only on the pack. Do not use outside knowledge of the case, and do not assume
  a document exists because it usually would.
- Put EVERY difference between the two sides in "discrepancies", before you consider
  whether any of them matters. A difference explained by a guard still goes in the list;
  say in "analysis" which guard explains it.
- If a value the comparison depends on is not in the pack, set "comparisonPerformed" to
  false and name what is missing. Do NOT estimate, derive around, or assume it. A
  comparison you could not make is a legitimate answer; an invented one is not. But where
  the report claims a fund, plan or figure is aligned, consistent or sufficient and the
  evidence that would support it is absent, the claim is unsupported — that is a Potential
  Concern, not a comparisonPerformed of false.
- Quote only text that appears in the passages given, verbatim, and name the passage id it
  came from. Quotations are checked against the pack automatically. Do not adjust one to
  fit your reasoning: if the evidence contradicts the report, that is the finding.
- Where the evidence is a TABLE and what you rely on is a row rather than a sentence,
  do not rewrite the row as prose. Leave "quote" empty, name the passage id, and list
  the values you read in "cells" — for example
  ["Savings", "JS", "Cash Account", "6,000"]. A table restated as a sentence is not a
  quotation and will be rejected, however accurately you read it. Every value you list
  is checked against that passage, so list what is there and nothing more.
- Check the report against the derived figures rather than re-deriving them. One that
  contradicts a figure the report states is a finding on its own and needs no passage to
  corroborate it: it was computed from the report's own numbers.
- Respect the false-positive guards. They describe specific ways this comparison
  produces spurious mismatches, and a finding one of them explains is not a finding.
  A guard explains a DIFFERENCE between two documents. It does not excuse an assertion
  the report makes for itself: where the report claims something is aligned, consistent
  or sufficient, a guard that makes the comparison unsafe is why that claim is
  unsupported, not a reason to drop it.
- Before you finish, read "reportSays" and "fileSays" back against each other. Any
  figure, date or label appearing on both sides with different values must appear in
  "discrepancies", or "analysis" must say why the two are not comparable. Stating both
  halves and relating them in neither field is not a finding.
- Categorise the problem, do not grade it. "issueCategories" says what KIND of thing is
  wrong, from the fixed list in the schema; severity says how bad and outcome says what
  it is. Choose every category that genuinely applies and no more, and leave it EMPTY
  where the requirement is met — a category on a passing requirement reads as a concern
  nobody raised. Distinguish: evidence the file lacks from a component the report lacks;
  two sources disagreeing from one value wrong in form; a reason that does not support the
  recommendation from something disclosed but not prominently. Where the plan names the
  categories this requirement usually raises, that is a steer and not a menu: if what you
  found is a different kind of problem, say the kind you found.
- Do not soften, hedge or omit a contradiction to make the finding read more favourably.
  Where a genuine mismatch stands after the guards, the outcome is Potential Concern.
- Return one JSON object and nothing else. No prose outside it, no markdown fences.
```

**The user prompt** — assembled per group, in two nested parts:

1. *The check header* (`BuildCheckHeader`), identical across the check's groups so it caches: the
   check id and name, its regulatory basis, the check text / *What to look for* / *Decision logic* from
   the checks sheet, the **trigger** outcome (§7 of semantic-search.md), the **decision** definitions
   and severity guidance, and what the extraction reported about its own gaps.
2. *The requirement itself* (`BuildGroupPrompt`): the group's requirement and limb, then the two
   sides —
   - **What the report asserts** — the canonical-model fragments at the group's paths, rendered as
     JSON. Paths that resolved to nothing are named explicitly, with the instruction that an absent
     path means *either* the report is silent *or* the extraction failed to capture it, and those are
     not the same.
   - **What the file evidences** — the retrieved passages, each headed `[P1] [category] document
     (score)`, cited by passage id, with a note that quotes are checked. A model-only group is told so
     ("there is no supporting document… do not treat the absence of passages as a gap"); a group whose
     searches returned nothing is told to treat the point as not evidenced.
   - then the **derived figures** (§6), the **comparison method / tolerance / guards**, the
     **sufficiency** rules for when a side is empty, any **evidence shortfall**, and the **issue-category
     steer** — each drawn from the query plan's verification block (see semantic-search.md §2).

The prompt closes by naming the fields to return, in order, and *"Decide last."*

---

## 5. The finding: a schema that enforces the order of reasoning

The model must return one JSON object conforming to `FindingSchema`, sent as a strict
response-format when `StructuredFindings` is on (the default). The schema's `required` list fixes the
**emission order**, and that order is the mechanism, not decoration:

```
groupId · requirement · reportSays · fileSays · discrepancies ·
comparisonPerformed · missingInputs · analysis · citations ·
issueCategories · severity · outcome
```

> The response schema emits properties in order and the model conditions on what it has already
> written, so "decide last" is enforced by the schema and explained in the prompt.

The model states each side, lists every discrepancy, declares whether the comparison could be made and
what was missing, reasons, cites, categorises, grades — and only then writes the outcome, by which
point it is conditioned on all of the above rather than on a verdict formed first and rationalised
after. The fields:

| Field | What it holds |
| --- | --- |
| `reportSays` / `fileSays` | The two sides, in the assessor's words |
| `discrepancies` | Every difference between them, listed before any judgement of whether it matters |
| `comparisonPerformed` | Whether the comparison could be made at all |
| `missingInputs` | What was absent, when it could not |
| `analysis` | The reasoning, including which guard explains a difference |
| `citations` | Passage id + verbatim `quote`, or `cells` for a table row (§7) |
| `issueCategories` | The kind of problem, from a fixed vocabulary; empty when the requirement is met |
| `severity` | High / Moderate / Low, or null |
| `outcome` | `NoIssue` / `PotentialConcern` / `NotApplicable` |

The `issueCategories` enum is substituted from `IssueCategory.All` at load time, so the vocabulary the
model is constrained to and the vocabulary the app recognises are one list, not two copies. Severity
is **categorise, don't grade**: the model says what *kind* of problem it is; how bad is a separate,
nullable field.

---

## 6. Arithmetic is settled before the model is asked

Delegating arithmetic to the assessor was measured to fail both ways — it misses a calculation it is
standing on, and it invents ones that look right — so the numeric half of a comparison is computed in
code first (`DerivedFigures`, `NumericComparison`):

- **`DerivedFigures`** turns the report's own extracted values into the sums, percentages and
  frequency conversions the checks turn on, and these are printed into the prompt as *settled*. The
  rule: *"Check the report against the derived figures rather than re-deriving them. One that
  contradicts a figure the report states is a finding on its own and needs no passage to corroborate
  it — it was computed from the report's own numbers."*
- **`NumericComparison`** produces the numeric half of a `ValueMatch` or `RangeMatch`, placed **above**
  the guards so the guards read as qualifications on an established result rather than as instructions
  for work still to be done.

This is why a charge stated as one percentage of the wrong fund value, or a weekly figure that does
not annualise to the stated yearly one, is caught: the correct arithmetic is in front of the model as
a fact, not left to it to notice.

---

## 7. What is checked about the answer before it is believed

The model's finding is not taken at face value. Two mechanical checks run over it, neither needing a
model, and both can **downgrade the outcome the model wrote**.

### Citation verification

Every `quote` is checked against the passages the group was actually given (`CitationVerifier`). This
exists because of a specific failure: asked to assess a risk-rating override, an assessor was handed a
file note reading *"a Risk rating of 6"* and reported, inside quotation marks, *"a Risk rating of 5"* —
making the evidence agree with the recommendation and clearing the check. Everything upstream had
worked; nobody was checking the quote. Now:

- A quote is verified against **this group's** evidence only — never the whole check's, or a quote
  lifted from a neighbouring group's passages would verify and the check would be worthless.
- Matching is deliberately generous — whitespace collapsed, typographic quotes and dashes folded,
  case ignored — because a model reflowing a quote across lines is not the failure being caught. But
  it does **not** admit a changed digit: the `6`→`5` alteration has a 96% contiguous run, and any rule
  loose enough to pass a reflowed table would pass that too, so it is not loosened.
- A **table read** is verified cell by cell (`cells`), not as prose — a row restated as a sentence is
  rejected however accurately it was read.
- An untraceable quote does not fail the run; it marks the finding, and it can downgrade the outcome
  (below).

### The outcome the app records is not always the outcome the model wrote

`GroupFinding.ParsedOutcome` is what the run uses, and it overrides the model in two cases:

- **Comparison not made → Indeterminate.** If the model set `comparisonPerformed: false` *and* listed
  no discrepancies, the outcome is Indeterminate regardless of what it wrote — a group that could not
  compare and found nothing has not cleared the requirement, it has failed to assess it.
- **Untraceable quote on a pass → Potential Concern.** If a quote could not be traced *and* the model
  said `NoIssue` or `NotApplicable`, the outcome is raised to Potential Concern. A finding that clears
  a requirement on the strength of a quotation that cannot be found in the evidence is not allowed to
  clear it.

A third, related count: **`ComparedSomething`** is true when the finding listed discrepancies, and a
group that lists differences and still answers No Issue is contradicting itself — measured at 9–15
groups per run naming 27–51 differences, previously counted by nothing. Those are now surfaced (a
*raised and not carried* count) rather than passing silently.

---

## 8. Consolidation and archiving

The per-group findings are gathered into the check's result, and the check outcomes into the run. A
check is Potential Concern if any of its requirements is; the rendered report lists the findings
requiring attention, the *raised and not carried* differences, the *figures described differently
across checks* (a cross-group pass that catches a contradiction split between two requirements that
neither could see alone), citation trust, and prompt adherence.

Every group's complete record — both prompts, the raw response, the parsed finding, the passages it
was given (in-pack and evicted), and the canonical fragments — is written to the run archive, so a
compliance report or a searched extract produced days later says exactly what the run saw. The
verification is reproducible from that archive without re-running anything.

---

## 9. Parameters

The decision call goes through the same `ChatCompletionClient.RunRawAsync` as extraction, with the
sampling pinned the same way (`ChatOptions`):

| Parameter | Setting | Default | Role |
| --- | --- | --- | --- |
| Model | `SelectedModel` | — | The assessor. One call per requirement. |
| Max output tokens | `DecisionMaxTokens` | **8,000** | Per-finding output cap. |
| Structured findings | `StructuredFindings` | **true** | Sends `FindingSchema` as a strict response-format, which is what enforces the field order (§5). Off = free-form, and the order is only asked for. |
| Temperature | `Temperature` when `PinTemperature` | **0**, pinned | A QA finding is a judgement that should not change because the sampler rolled differently; the provider default is a creative temperature. |
| Top-p | `TopP` when `PinTopP` | **1**, pinned | Pinned the same way. |
| Seed | `SamplingSeed` when `PinSeed` | **1**, pinned | Value irrelevant; holding it constant is the point. |
| Parallelism | `MaxParallelRequests` | — | One budget for the whole run — checks and requirements both fan out. |

Each of temperature, top-p and seed is releasable independently, because a gateway or model can reject
one without the others (Bedrock's Anthropic route rejects `seed`; some inference profiles accept only
temperature 1). The run's configuration line records which were pinned — e.g. *"temperature default,
top-p default, seed not pinned · findings schema-constrained"* — so a run says how reproducible it was.

There is no separate retrieval spend in this call: the report is read once at extraction and the
evidence once at search, so a check costs retrieval plus **one** decision call per requirement, not a
re-parse of the documents.

---

## 10. What verification deliberately does not do

- **It does not let the model do the arithmetic it can settle.** Derived figures are computed in code
  and presented as fact (§6).
- **It does not trust a quotation.** Every quote is checked against the pack, and an untraceable one
  on a passing finding raises the outcome (§7).
- **It does not let the report be its own evidence.** A category `[I]` passage is the assertion side;
  it cannot corroborate the report (§3).
- **It does not let a group clear a requirement it could not assess.** No comparison and no
  discrepancy is Indeterminate, not No Issue (§7).
- **It does not decide the outcome first.** The schema's field order makes the verdict the last thing
  written, conditioned on the reasoning above it (§5).
- **It does not use outside knowledge.** The finding is made on the pack alone; a document is not
  assumed to exist because it usually would.
