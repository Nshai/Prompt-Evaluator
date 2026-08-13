# Extraction improvement plan

Acts on [gap-analysis.md](gap-analysis.md). Ordered so that each change can ship and be measured
on its own.

> **Implemented 2026-08-13.** Items 1–9 are in the build; 10 and 11 are deliberately not, for the
> reasons given under [What was not done](#what-was-not-done). Build clean, **258 tests passing**
> (was 229). One design change was made against the plan: the identity spine is **harvested from
> the passes that already run** rather than added as a thirteenth pass — see
> [Stage 1 as built](#stage-1-as-built).
>
> The fix is verified against the artefacts of the failure itself, not only synthetic input.
> `ObservedExtractionFailureTests` replays the real 61,422-character truncated response and the
> real stored model: it recovers **4 recommendations** from the reply the pipeline discarded
> entirely, and finds **8 dangling `linkedObjectiveIds`** in the stored model that nothing had
> reported.

> **The shape of the fix.** Sectioned extraction was the right call — a 250 KB model cannot come
> back in one response. But sections were treated as independent, and they are not: they share a
> key space, an output budget and a view of the document. Three changes make them cooperate — an
> identity spine passed into every pass, a budget that fits the largest section, and salvage when
> a pass overruns anyway. Prompt wording is the smallest part of this; two of the six gaps cannot
> be fixed by prompting at all.

---

## Stage 1 — Make the identifiers join (GAP E2)

The highest-value change and the one no prompt tweak alone will achieve reliably.

### Stage 1 as built

**The extra pass turned out to be unnecessary.** The passes that *define* ids already run before
the passes that *reference* them, with one exception, so the table can be harvested from work that
happens anyway rather than paid for again.

What shipped, in [`CanonicalModelIdentity`](../../../src/AiPromptEvaluator/CanonicalModelIdentity.cs):

- **Ids are adopted after every pass**, not extracted separately. `Adopt` walks the merged model,
  finds entities whose id is not yet canonical, and rewrites it — `C1`, `OBJ1`, `EA1`, `NP1`,
  `REC1`. Naming becomes the code's decision rather than the model's, which removes the
  inconsistency at source: the observed run wrote `"John Sullivan"`, `"client-1"` and `"client-2"`
  for clients *within one model*.
- **The table is rendered into every later prompt** by `Table()`, labelled from the entity's own
  name field so the model can map an id onto the report. On the real data it produces:
  ```
  Clients:               C1 = John Sullivan
  Existing arrangements: EA1 = People's Pension   EA2 = Scottish Widows   EA3 = Aviva
                         EA4 = Zurich             EA5 = Standard Life
  ```
- **`RewriteReferences` repoints** anything written against an old id, which is what makes this
  work for passes that ran before an entity was adopted.
- **`DanglingReferences` reports what still resolves to nothing**, as an extraction failure.

**One ordering change was needed.** A recommendation carries `targetPlanId` into
`solution.recommendedPlans`, so **Solution now runs before Recommendations**. Every
cross-reference in the model now runs forwards through the pass order, which
`SectionOrder_DefinesEveryIdBeforeAPassCanReferenceIt` pins so a future reshuffle cannot
reintroduce a backward reference silently.

**Adoption is idempotent** — an entity keeps the id it was first given, so a later pass restating
it cannot renumber it out from under a reference already written against it.

<details>
<summary>The original plan: a separate spine pass</summary>

### 1a. Extract an identity spine first, then pass it to every section

Add a pass that runs before all others and does nothing but assign keys:

```csharp
new("Identity spine",
    "The clients, objectives and existing arrangements present in this report, as an id "
    + "table only. Assign ids and carry no other data.",
    ["parties", "objectives", "existingArrangements"]),
```

It returns a small object — five arrangements, three objectives, two clients is well under 2 KB —
which is then rendered into every subsequent section prompt as a fixed key table:

```
## Identifiers — use these exactly

Clients:              C1 = John Sullivan (Primary), C2 = Kim Sullivan (Spouse)
Objectives:           OBJ1 = afford lifestyle through lifetime
                      OBJ2 = retire in 2 years on £1,700 net per month
                      OBJ3 = consolidate and understand pension arrangements
Existing arrangements: EA1 = People's Pension 6197175/PP
                      EA2 = Scottish Widows ZU4225575
                      EA3 = Aviva SP50163762
                      EA4 = Zurich P10026-719-001/DL
                      EA5 = Standard Life D2301334000

Every clientId, objectiveId, arrangementId and every *Ids array must use an id from this
table verbatim. If something belongs to an entity not in the table, omit the reference —
do not coin a new id and do not use a name or a label in an id field.
```

This costs one extra pass and about 400 tokens on each of the eleven that follow. It is cheap
because the report is already the cached prefix; the table sits in the uncached tail.

**Why a table rather than a naming rule.** A rule like *"number clients C1..Cn in document order"*
gets the clients right and still fails on objectives, where two passes may disagree about whether
the report states three goals or four. A table removes the judgement entirely.

### 1b. Validate the joins after merging

Deterministic, no model call:

```csharp
// Every *Ids reference must resolve to an entity the spine declared.
internal static IReadOnlyList<string> DanglingReferences(JsonObject root)
```

Report unresolved references as extraction failures in `ExtractionResult.Failures`. On this run
that would have surfaced all seventeen `linkedObjectiveIds` immediately, instead of leaving CHK-006
to discover the absence six checks later.

</details>

**Measured on the real stored model.** `DanglingReferences` finds eight distinct broken targets
where the run reported none:

```
linkedObjectiveIds → LowerCosts                 linkedObjectiveIds → RiskManagement
linkedObjectiveIds → ConsolidationAndClarity    linkedObjectiveIds → RiskProfileAlignment
linkedObjectiveIds → EnsureAppropriateStructure linkedObjectiveIds → RetirementIncomeNeeds
linkedObjectiveIds → RiskToleranceAlignment     linkedObjectiveIds → RetireWithClearer Understanding
```

Target on the next run: zero.

---

## Stage 2 — Stop losing truncated sections (GAP E1)

### 2a. Raise the budget to fit the largest section

`ExtractionMaxTokens` is 16,000. The largest successful section used ~14.1k and Recommendations
needed roughly 17.5k to finish. Raise the default to **32,000**, which clears the observed maximum
with room for a more complex case.

```csharp
public int ExtractionMaxTokens { get; set; } = 32000;
```

This alone would have made the run succeed. It is not sufficient on its own — a case with ten
arrangements and four clients will find the new ceiling too — but it is one line and it removes
the immediate failure.

### 2b. Detect truncation and say so

The current message is wrong in a way that costs debugging time. Distinguish the three cases:

```csharp
var fragment = ParseObject(result.Response);
if (fragment is null)
{
    throw new InvalidOperationException(
        LooksTruncated(result.Response)
            ? $"The response was cut off at the {maxTokens:N0}-token output limit "
              + $"after {result.Response.Length:N0} characters. Raise ExtractionMaxTokens "
              + "or split this section."
            : "The model did not return a JSON object for this section.");
}
```

`LooksTruncated` is a fenced block whose fence never closes, or a parse error of type
*unterminated string* / *unexpected end of input*. Both were true here and neither was reported.

Better still, read the provider's finish reason where it is available rather than inferring it —
`RunRawAsync` currently discards it.

### 2c. Salvage complete array elements

Four of five recommendations were whole. Recovering them is a scan for the last balanced element:

```csharp
/// <summary>
/// The longest prefix of a truncated array response that is still valid JSON, closed off.
/// A pass that produced four of five recommendations should contribute four, not nothing.
/// </summary>
internal static JsonObject? SalvageArray(string response, string property)
```

Merge what survives and record the shortfall in `extractionReport.expectedButAbsent`, so a
downstream check can tell a partial section from a complete one. **Never silently merge a partial
array as though it were complete** — that would turn a loud failure into the quiet kind, which is
the trade the whole pipeline has been moving away from.

### 2d. Split Recommendations when the report has many

Recommendations is one property holding an array whose elements each carry rationale, benefits,
disadvantages, risks, tax implications and alternatives. It is structurally the largest thing in
the model and it grows linearly with the advice given.

Once the spine (1a) exists, the recommendation count is known before the pass runs, so the section
can be chunked — recommendations for EA1–EA3, then EA4–EA5 — with the same schema slice and the
results concatenated. Do this only if 2a proves insufficient on a larger case; chunking adds a
merge path worth avoiding until it earns its place.

---

## Stage 3 — Spend the output budget on data, not repetition (GAP E3)

Quotes are 52,500 bytes of the runtime model, 141 of them duplicates. Three prompt changes, in
descending value:

**Deduplicate.** Add to the system prompt:

```
- Quote each passage once. Where several assertions rest on the same sentence, quote it on
  the first and give only the page number on the rest. A repeated quote adds nothing the page
  number does not.
```

**Cap the length.**

```
- Keep quotes to the shortest span that carries the assertion — normally one clause, at most
  one sentence. Never quote a table row wholesale where a single cell is the evidence.
```

**Make the requirement proportionate.** The current rule demands a quote on every Stated or
Inferred provenance, which is 476 quotes on this document. The reference model carries 27 and is
more usable. Narrow it to where a quote does real work:

```
- Quote where the value is contestable: figures, dates, percentages, risk ratings, and any
  statement a check might have to weigh. For descriptive prose and boilerplate the page number
  alone is enough.
```

**Expected effect:** roughly 30–40% off the largest sections, which is more headroom than raising
the token limit buys, and it improves the model rather than merely enlarging it.

---

## Stage 4 — Make the self-report worth consulting (GAP E4)

`extractionReport` runs last and is given the document but not the eleven passes that preceded it.
It found 4 absent paths and 3 internal inconsistencies where the reference found 12 and 12.

**Give the pass the model it is reporting on.** After the other sections merge, render a compact
summary of what was extracted — property paths populated, paths left empty, and the values that
appear more than once with different figures — and put it in the prompt alongside the document:

```
## What the previous passes produced

Populated: 312 paths across 20 sections.
Empty:     /parties/clients[]/dateOfBirth, /parties/clients[]/taxStatus, …
Values recorded twice with different figures:
  /costsAndCharges/… existing Zurich charge: 0.18% (p5) and 0.93% (p11)

Report on this extraction. An empty path is `expectedButAbsent` only where the model expects a
value; say which reason applies. Two different figures for the same thing is an
`internalInconsistency` — list every one you can see, including any not listed above.
```

Most of that input is computable without a model: which paths are empty is a walk over the merged
object, and repeated-key-different-value detection is a comparison. The model is then doing the
part that needs judgement — deciding whether an empty path is silence or omission.

> **Built in part.** `SummariseExtraction` renders which sections came back populated and which
> came back empty, and the instruction above is in the prompt. The **repeated-key-different-value
> detection is not implemented** — the pass is told to list every internal inconsistency it can
> find, but is not handed a computed list of candidates to start from.
>
> That is the weaker half, and it is the half that would have caught the report's charge tables
> disagreeing between page 5 and page 11. It is left out because a useful version needs to
> compare *semantically equivalent* figures across differently-shaped subtrees, which is a real
> piece of work rather than a walk. Worth doing next; measure before assuming the prompt change
> alone was enough.

**Measure:** `internalInconsistencies` count against the benchmark's list of contradictions in
[../../test-results/expected-results-benchmark.md](../../test-results/expected-results-benchmark.md).
The report contains at least eight the extraction should see without leaving category I, including
the p5-versus-p11 charge tables and the 4/5 risk rating.

---

## Stage 5 — Validate before merging (GAP E6), and stop asking for what the code knows (GAP E5)

### 5a. Reject enum violations at the merge boundary

47 violations reached the stored model, 42 of them on `basis`, including
`"annual growth required at mid-growth rate 2.94% up to age 75"` in an enum slot.

Validate each fragment against its schema slice before `Merge`. On violation, drop the offending
property and record it, rather than failing the section — a bad `basis` should not cost the
charge figure it sits beside.

The stronger version is to stop it happening: the assessment side already constrains output with
`ChatResponseFormat.ForJsonSchema`, and `RunRawAsync` does not. Moving extraction onto the same
mechanism would eliminate both the enum drift and the markdown fences that `ParseObject` exists to
strip. Worth doing, with one caution recorded from the assessment work: **Bedrock's validator
rejects `enum` combined with a type array**, so nullable enums need `anyOf: [{enum}, {type: null}]`
before the schema will be accepted.

### 5b. Stamp `source.extractedAt` and `source.extractorModel` in code

```csharp
root["source"]!["extractedAt"] = DateTimeOffset.Now.ToString("O");
root["source"]!["extractorModel"] = _settings.SelectedModel;
```

The model wrote `"2025-01-01T00:00:00Z"` and `"claude-opus"` for a run on 2026-08-13 using
`intelliflo-claude-haiku-4-5`. Remove both from the schema slice sent to the model so it is never
asked; a field the process owns should not be a question.

---

## Consolidated prompt changes

Everything above that is prompt text, as one diff against `BuildSystemPrompt`.

**Add** (identifier contract, quote discipline):

```
- Use the identifiers given in the "Identifiers" section of the request exactly as written.
  Every clientId, objectiveId, arrangementId and every *Ids array must be an id from that
  table. If a reference has no id in the table, omit it — never coin a new id, and never put
  a name, a label or a description in an id field.
- Quote where the value is contestable: figures, dates, percentages, ratings, and any
  statement a check might have to weigh. For descriptive prose and boilerplate the page
  number alone is enough.
- Quote each passage once. Where several assertions rest on the same sentence, quote it on
  the first and give the page number on the rest.
- Keep quotes to the shortest span that carries the assertion — normally one clause, at most
  one sentence. Never quote a table row wholesale where one cell is the evidence.
```

**Replace** the existing quote rule:

```
- Every Stated or Inferred provenance needs a verbatim quote from the document and the
  page number from the nearest "<!-- page: N -->" marker above it.
```

with:

```
- Every Stated or Inferred provenance needs the page number from the nearest
  "<!-- page: N -->" marker above it, and a quote where the rules above call for one.
```

**Keep unchanged** — these are working and the runtime model honours them: never invent a value,
keep the document's own units, record contradictions rather than resolving them, boilerplate is
data with `isClientSpecific` false.

---

## Order, effort and what each is worth

| | Change | Gap | Status | Where |
| --- | --- | --- | --- | --- |
| 1 | `ExtractionMaxTokens` → 32,000 | E1 | **done** | [AppSettings.cs](../../../src/AiPromptEvaluator/AppSettings.cs) |
| 2 | Truncation detected and reported honestly | E1 | **done** | [ExtractionResponseReader.cs](../../../src/AiPromptEvaluator/ExtractionResponseReader.cs) |
| 3 | Canonical ids + id table in every prompt | E2 | **done** | [CanonicalModelIdentity.cs](../../../src/AiPromptEvaluator/CanonicalModelIdentity.cs) |
| 4 | Dangling-reference validation | E2 | **done** | `DanglingReferences` |
| 5 | Quote discipline in the prompt | E3 | **done** | `BuildSystemPrompt` |
| 6 | Salvage complete array elements | E1 | **done** | `ExtractionResponseReader.Salvage` |
| 7 | Stamp `extractedAt` / `extractorModel` in code | E5 | **done** | `StampSource`, `StripCodeOwnedFields` |
| 8 | Schema validation at the merge boundary | E6 | **done** | [CanonicalModelValidator.cs](../../../src/AiPromptEvaluator/CanonicalModelValidator.cs) |
| 9 | Feed the merged model into the self-report pass | E4 | **done** | `SummariseExtraction` |
| 10 | Structured output for extraction | E5, E6 | not done | see below |
| 11 | Chunk Recommendations by arrangement | E1 | not done | see below |

**Item 3 is the one that mattered.** Truncation is loud and was noticed within a day. Broken
identifiers are silent, and they had been quietly disabling a check through several rounds of
analysis that blamed retrieval.

### What was not done

**Item 10 — structured output.** Worth doing and deliberately not bundled here. It changes how
every extraction call is made, so it should land on its own where a regression in output quality
is attributable, not alongside eight other changes. The Bedrock caveat still stands and is
recorded in Stage 5a: nullable enums need `anyOf: [{enum}, {type: null}]` or the validator
rejects the schema.

**Item 11 — chunking Recommendations.** The plan made this conditional on items 1 and 5 proving
insufficient, and that has not been tested yet. Chunking adds a merge path, and on the evidence
so far the section needed about 17,500 tokens against a new ceiling of 32,000. If a larger case
reaches the new limit, the salvage path now keeps most of the work and says so, which is the
condition under which chunking earns its place.

### Two things to watch on the next run

**Stripping enum violations deletes data.** `StripEnumViolations` drops the offending property
rather than failing the section, which is the right trade for a bad `basis` sitting beside a good
charge figure — but it is still deletion, and it is reported in `ExtractionResult.Failures` for
that reason. If the next run reports many drops on one property, the schema's enum is more likely
wrong than the model is.

**Matching is by property name, unioned across the schema.** Where two places declare different
enums for the same property name, a value valid in either passes. That leniency is deliberate:
this drops data when it fires, so it should fire only where the schema plainly disallows the
value. It is recorded here because it is the kind of shortcut that reads as a bug later.

---

## How to tell whether this worked

Re-run extraction on ABC-99 and compare against this run:

| Measure | Now | Target |
| --- | --- | --- |
| Sections failed | 1 of 12 | 0 |
| Dangling `*Ids` references | 17 | 0 |
| Recommendations extracted | 0 | 5 |
| Total model size | 256,908 bytes | < 160,000 |
| Duplicate quote instances | 141 | 0 |
| Enum violations | 47 | 0 |
| `internalInconsistencies` | 3 | ≥ 8 |

Then re-run the checks. The single clearest signal that Stage 1 worked is **CHK-006 returning a
verdict instead of Indeterminate** — it has the evidence, and has had it all along.
