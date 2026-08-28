# Semantic search — how evidence is found for a fact

How the pipeline retrieves the supporting evidence a check weighs a report's assertion against. This
is the counterpart to [model-extraction.md](model-extraction.md): extraction produces the canonical model of what
the report *asserts*; semantic search finds what the *rest of the case file* records, so a check can
tell whether the two agree.

The authority is the code. Where this document and the source disagree, the source is right — the key
files are named at each point.

- Query text and ranking: [`CheckPlanRunner.cs`](../../src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs) (`GatherAsync`, `Rank`)
- The search itself: [`CaseDocumentSearchService.cs`](../../src/AiPromptEvaluator.Core/Services/Documents/CaseDocumentSearchService.cs)
- The vector store: [`CaseDocumentStore.cs`](../../src/AiPromptEvaluator.Core/Services/Documents/CaseDocumentStore.cs)
- Indexing and chunking: [`CaseDocumentIndexer.cs`](../../src/AiPromptEvaluator.Core/Services/Documents/CaseDocumentIndexer.cs)
- The query plans that drive it: [`docs/artifacts/check-plan/`](../artifacts/check-plan/) and [`query-plan.schema.json`](../artifacts/check-plan/query-plan.schema.json)
- Embeddings: [`AiClientFactory.cs`](../../src/AiPromptEvaluator.Core/Services/Ai/AiClientFactory.cs)
- Parameters: [`AppSettings.cs`](../../src/AiPromptEvaluator.Core/AppSettings.cs)
- The diagnostic: [`RetrievalDryRun.cs`](../../src/AiPromptEvaluator.Core/Services/Assessment/RetrievalDryRun.cs)

---

## 1. What semantic search is here, and what it is not

A check has two sides. The **assertion** side — what the report claims — is read from the stored
canonical model by JSON pointer, never re-searched. The **evidence** side — what the fact find, file
note, risk profile, provider illustrations and research record — is retrieved by semantic search over
the indexed case documents. A finding is the comparison of the two.

**The single most important thing to hold onto: semantic search makes no model call.** It embeds a
query, does a vector lookup, and ranks the results with deterministic code. The only LLM in the whole
retrieval path is the embedding model, and only to turn text into a vector. This is why retrieval is
reproducible — *"searches are chosen by the check's query plan, not by the model, so two runs of the
same check over the same case retrieve the same evidence, and a finding can be reproduced"*
(`CaseDocumentSearchService`) — and why the dry-run diagnostic (§10) can execute a whole run's
retrieval in seconds for the price of the embeddings.

**There is no retrieval "system prompt."** The closest thing to one is the query *text* in the plan,
which is the string that gets embedded. That is where the wording that matters lives, and §4 covers
how it is written. The prompt that consumes the retrieved pack belongs to the assessor and is
documented with the check run, not here.

The case-folder categories the search filters and cites by:

| Code | Category | | Code | Category |
| --- | --- | --- | --- | --- |
| A | Client Authority | | F | Needs Analysis |
| B | Know Your Client | | G | Research |
| C | Meetings & Communications | | H | Solution Design |
| D | Risk, Capacity & Experience | | I | Recommendations & Advice |
| E | Existing Plans & Portfolios | | | |

Category **I** is the suitability report — the assertion side. Everything else is evidence.

---

## 2. The query plans: the programme every search follows

**There is no LLM-callable search tool. The query plans choose every search, so the model cannot.**
That sentence is the reason the plans exist and the reason retrieval is reproducible: a plan is *"a
pre-computed semantic-search programme for one QA check"* (`query-plan.schema.json`), executed by
`CheckPlanRunner` against `CaseDocumentSearchService`, and two runs over the same case retrieve the
same evidence because the same plan asks for it both times.

There is **one plan per check**, ten in all (`CHK-001` … `CHK-010`), loaded from
[`docs/artifacts/check-plan/`](../artifacts/check-plan/) by `CheckQueryPlanLoader`, which refuses any
plan not declaring `planVersion` `1.0` by name rather than half-reading it. Between them the plans
hold **88 query groups** — one per requirement — and the [check-coverage matrix](../artifacts/check-coverage-matrix.csv)
is generated from them, so the plans are the single source of truth for what each check looks for.

### The one split that matters: retrieval vs verification

Every group is divided into a **`retrieval`** block and a **`verification`** block, and the division
is load-bearing:

> A mistake in `retrieval` is invisible in the output, because the check reports confidently on a pack
> that never held the answer, while a mistake in `verification` at least shows up in the finding's own
> reasoning.

Only the `retrieval` block drives semantic search, and this document covers that half. The
`verification` block — `decision`, `comparison` and its guards, `issueCategories`, `sufficiency` — is
what the assessor is *allowed to make* of the pack once it has been retrieved; none of it touches
search, and it is documented where it belongs, in
[verification-method.md §2](verification-method.md). The rest of this section is the retrieval half.

### The elements that drive a search, and what each is for

Reading roughly outermost to innermost:

**Plan level**

- **`checkId` / `checkName`** — match the plan to its check and label every finding, prompt-log entry
  and matrix row.
- **`triggerProbe`** — *the largest single lever in the plan*, because it runs before any group is
  gathered and can eliminate the whole retrieval pass. Its `applicability` rules are ANDed and
  evaluated **against the stored canonical model, not by searching** — whether a case has pension
  objectives is a fact the extraction settled when it read the report in full, so a check that does
  not apply costs *nothing*: not one embedding, not one vector query. `triggerField` names the
  `checkTriggers` value that settles it; `onAbsent` decides what a missing trigger does (`Skip`
  settles with zero retrieval; `ContinueWithReducedScope` proceeds and says so). The probe's own
  `queries` are a cheap search fallback, used only where neither the field nor the applicability rules
  could decide.
- **`retrieval.resultsPerCall`** (plan-wide) — lets a check widen its candidate pool above
  `MaxSearchResults` where it needs to; a value *below* the global setting is ignored, so a plan
  cannot quietly retrieve less than the rest of the run. No shipped plan sets it.
- **`primaryCategories`** — build-time only, read by lint rule L2; nothing reads it at run time.

**Group level (`retrieval` block)**

- **`queries`** — the searches themselves (§4): each a `text` to embed, a `side` deciding whether it
  is searched at all, `targetCategories`, `expectSignals` and a `priority`. This is where the actual
  retrieval happens.
- **`canonicalPaths`** — the assertion side: JSON pointers resolved against the stored model rather
  than searched, merged with the paths on the group's Assertion queries.
- **`evidenceSections`** — headings the evidence sits under, used as a ranking key and a reserved slot
  (§7). Routing *within* a targeted category, for when a large multi-section document would otherwise
  let the wrong section win the slot.
- **`declares.evidenceCategories`** — where the group *says* the corroboration lives. Lint holds it to
  a subset of the queries' `targetCategories`; at run time it decides only *which category keeps a
  reserved slot when the cap bites* (§7), never what is searched.

The group's other fields — `requirement`, `limb`, `comparison`, `issueCategories`, `sufficiency` —
are the `verification` block, and drive no search; they frame the finding and are documented in
[verification-method.md §2](verification-method.md).

### Why the plan is pre-computed rather than model-chosen

Letting a model choose its own searches would make retrieval a second place the model's judgement
enters, and an unreproducible one — the same check over the same case could retrieve different
evidence on different runs, and a finding could not be reproduced. Fixing the searches in the plan
puts the model's judgement in exactly one place (the assessor weighing a retrieved pack) and makes
the evidence side a pure function of the plan, the index and the settings. That is what the dry-run
diagnostic (§10) exploits: because nothing about retrieval depends on a model, a whole run's searches
can be replayed in seconds to see what they return.

The cost of pre-computing is that a plan can be *wrong* — a query in the wrong vocabulary, a hint that
matches nothing, a category never searched — and, per the split above, wrong silently. Sections 4, 7
and 10 are largely about the specific ways that has happened and what now catches each one.

---

## 3. Indexing: how documents become searchable

Before anything can be searched, the case folder is indexed once (`CaseDocumentIndexer`, the "Load
Docs" action). Only Markdown is indexed; anything else is reported as skipped.

**Chunking is semantic, not fixed-width.** `SemanticSimilarityChunker` embeds the document's elements
and cuts where the topic shifts, so a finding and the sentence that supports it stay in one chunk. It
is driven by *the same embedding generator the search uses* (`CreateChunker`), so the cut points and
the queries live in the same vector space. A fixed-width splitter would still index and still search;
the regression would show only as worse retrieval, so a test pins the strategy.

Chunk sizing (`AppSettings`):

| Parameter | Default | Meaning |
| --- | --- | --- |
| `MaxTokensPerChunk` | **600** | Upper bound on a chunk, tokenised with `o200k_base` (tiktoken) |
| `ChunkOverlapTokens` | **100** | Tokens repeated between adjacent chunks, so a fact on a boundary is whole in one of them |
| `MaxEmbeddingInputCharacters` | **20,000** | Largest element sent as one embedding call |

Each chunk is composed as `heading context` + `content`, so a passage lifted from the middle of a
report still says what section it is about (`Compose`). It is stamped with case reference, tenant,
document name, category code and chunk index, then embedded in batches of 64 and upserted to Qdrant.

**Two real-world defences worth knowing**, because they change what is searchable:

- **Malformed structure falls back to plain text.** A converted policy document once parsed as a
  single 156,384-character element, which the embedder refused, and the whole document was lost from
  the case. So the largest element is measured; a document the reader could not break up is re-read as
  bounded, blank-line-separated paragraphs (`ReadDocumentAsync` → `AsPlainText`). Losing heading
  structure costs something; losing the document costs more.
- **HTML entities are decoded first** (`&amp;`, `&#124;`) — otherwise the Markdown reader rejects them
  and the file is dropped.

Re-indexing a case deletes its existing chunks first (`DeleteCaseAsync`), so an edited document does
not leave stale passages behind. Point ids are derived from the chunk's natural key, so a re-index
replaces rather than duplicates.

---

## 4. The query: where the search actually comes from

A group in a query plan carries one or more **queries**, and each query has (`query-plan.schema.json`,
`Query`):

- **`text`** — *"the single most consequential field in the plan, because it is the query."* It is
  the string that gets embedded. Written in the **vocabulary of the target document, not the check**:
  the embedding matches document language, so a query phrased in check vocabulary *"fails silently and
  completely."* For the affordability requirement, the evidence query is not "assess affordability"
  but `disposable income surplus available each month after expenditure` — the words a fact find uses.
- **`side`** — `Assertion`, `Evidence` or `Either`. Decides whether the query is searched at all.
  **Assertion queries are never embedded**; their `canonicalPaths` resolve against the stored model.
  **Evidence and Either both go to the vector store**, because an Either phrasing still has an evidence
  half to find (`IsEvidenceSearch => Side != "Assertion"`).
- **`targetCategories`** — the categories this query expects its evidence in. This is the one plan
  field that reaches the Qdrant filter (§5), and it also reserves a slot in ranking (§7).
- **`expectSignals`** — a verification field, evaluated during retrieval (§8).
- **`priority`** — `Core` or `Supplementary`. When `CoreQueriesOnly` is set, Supplementary queries are
  not run at all — roughly one query in seven, the cheapest retrieval saving available
  (`QueriesToRun`).

A worked example — CHK-005 G5.1, *"Affordability of initial and ongoing commitments"*:

| Query | Side | Target cats | Text (embedded) |
| --- | --- | --- | --- |
| Q5.1.1 | Assertion | I | *initial adviser fee and ongoing annual charge…* (resolved from the model, not searched) |
| Q5.1.2 | Either | I, H | *monthly contribution you will pay into the plan regular premium* |
| Q5.1.4 | Evidence | B, F | *disposable income surplus available each month after expenditure* |
| Q5.1.5 | Either | I, A | *fees will be paid from the plan rather than directly by the client…* |

Three of these five are searched (the two Either and the one Evidence); the two Assertion queries are
answered from the canonical model.

---

## 5. The search: one embedding, two lookups

For each evidence query, `CaseDocumentSearchService.SearchAsync`:

1. **Embeds the query text once**, raw — no `query:`/`passage:` prefix, no instruction wrapper. The
   query and the chunks were embedded by the same model, so they compare directly.
2. **Runs two vector lookups against Qdrant** with that one vector:
   - **unfiltered** — across the whole case (still scoped to case + tenant);
   - **category-filtered** — restricted to the query's `targetCategories`, when it names any.
3. **Prepends the targeted hits to the unfiltered ones**, then de-duplicates further down.

**Why two lookups rather than one filtered search.** A target category is where the evidence is
*expected*, not the only place it can be. A hard filter would silently discard a contradiction sitting
in a category nobody thought to name. Searching unfiltered alone was the original behaviour, and small
categories lose that competition every time — in a real run the Meetings & Communications category
(one short file note) reached 5 of 57 requirement groups while 16 of them asked for it, because it was
outscored by the long policy documents beside it. Running both, targeted-first, is what makes a small
category reachable while still letting evidence surface from anywhere. The second lookup is nearly
free: the embedding is the expensive part and it is shared.

Every result carries `SearchedText`, `DocumentName`, `CategoryCode`, `CategoryName` and a **`Score`
kept at full precision** — rounding it here manufactured ties that ranking then had to break
arbitrarily.

### The store

Qdrant, one collection, cosine distance (matching how the embedding models are normalised). Every
point carries the tenant, case, document, category and chunk index as payload, with payload **indexes
on tenant, case and category_code** so a search narrows to one case within one tenant — and can
filter by category — before the vectors are compared (`CaseDocumentStore`). A search's filter always
`Must` match tenant and case; category is added only for the targeted lookup.

---

## 6. From hits to a group's candidate set

A group runs all its evidence queries and merges the results (`CheckPlanRunner.GatherAsync`):

- **De-duplication.** The same chunk comes back for several wordings of the same question; the first
  is kept and the rest dropped, so the pack is evidence rather than repetition. The key is the
  document name length-prefixed plus the text itself — deliberately *not* a hash code, because
  `string.GetHashCode()` is seeded per process and a collision would drop a distinct passage on one
  launch and a different one on the next, making the pack differ between sessions before the assessor
  is even called.
- **Attribution kept for the archive.** Which query found which passage is recorded, because a reader
  of a finding asks exactly that, and once the lists are merged there is no way back to it. The pack
  the assessor sees is a flat ranked list.
- **Empty searches recorded.** A query that returned nothing is kept with a zero count — the only
  place that fact survives — because "assessed from the report alone" and "searched and found nothing"
  are opposite findings.

---

## 7. Ranking: which candidates reach the assessor

The merged candidates are ordered and capped by `CheckPlanRunner.Rank`. **The category filter cannot
happen in Qdrant across both lookups, so `targetCategories` is applied here as a ranking key**, and a
per-group cap keeps the best N. The order (highest priority first):

1. **Targeted category** — a passage from a category the group's queries name outranks a
   better-scoring one from a category they do not.
2. **Not a form skeleton** — a blank form grid sinks below anything carrying a value. Once every
   declared category was guaranteed a slot, the passage it admitted turned out, in every measured
   case, to be a short generic form skeleton that embeds near any query and asserts nothing
   (`ContentDensity.IsFormSkeleton`). Ranked, not filtered at indexing, because an unfilled section is
   sometimes the finding itself.
3. **Section-hint match** — a passage containing a declared `evidenceSection` string outranks one that
   does not. A category is one document and a document is a dozen unrelated sections, so without this
   the slot goes to whichever section embeds best for the group's wording — which once delivered the
   client's disposable income to four groups with no use for it and none of the groups that needed it.
4. **Score** — the vector similarity, descending.
5. **Document name, then passage text** — the tie-breakers that make the cut *reproducible*. Scores
   collide often enough to matter, and a slice through a tie band would otherwise depend on the order
   an approximate index happened to return.

### The cap and reserved slots

`MaxPassagesPerGroup` (default **24**) is the cap. When more candidates than the cap survive, slots are
**reserved** before "the best of the rest" fills up:

- **One slot per declared `evidenceSection`** (`ReservedSlotsPerDeclaredSection`, default **1**),
  served first — a plan naming *"Current Monthly Cash Flow"* has asked for something more specific than
  a category, and a section slot usually satisfies the category slot as a side effect. Matched only
  within `targetCategories`, so a hint promotes within the request rather than widening it.
- **One slot per targeted category** (`ReservedSlotsPerTargetedCategory`, default **1**), declared
  categories first. `declares.evidenceCategories` decides *who keeps a slot when the cap bites* — lint
  keeps declared a subset of targeted, so this never widens a request.

**Why reserved slots exist.** Ranking by "targeted, then score" looks like it favours targeted
categories, and does — until *every* candidate is targeted, which is normal once a group names four or
five categories. The first key then returns the same value for everything, the order collapses to pure
score, and the cap keeps whichever documents embed closest to the query. Measured, that meant the Fact
Find was evicted before the assessor saw it: three checks reached it in zero of nineteen groups while
every pack sat exactly at the cap, and six of eight missed benchmark findings were facts recorded only
there. Asking for the category (fixing the plans) was necessary and did nothing on its own, *because
asking is not the same as keeping*. The reserved slot is what keeps it.

Passages are emitted in rank order, so a reader meets the best evidence first whether or not it got in
on the floor.

---

## 8. `expectSignals`: verification during retrieval

`expectSignals` is a list of substrings a query expects to find in its hits. After the query runs, the
hits are scanned case-insensitively for any of them (`SignalPresent`). It is **not** a filter and
cannot re-search, widen anything or promote a passage — it only observes what already came back.

Its purpose is to tell the difference between *retrieved and absent* and *not retrieved*. Where none of
a query's expected signals appear in its hits, the runner tells the assessor the data point is **absent
from the case file** rather than merely unretrieved — and for most checks that absence is the finding.

Because `expectSignals` drives an "absent from the case file" claim, a signal carrying a value from one
case would report a false absence on the next; the plans are held generic (lint rule L6), so signals
are domain wording (`per annum`, `surplus`, `more expensive`) rather than one case's figures.

---

## 9. Parameters

Everything the search reads from `AppSettings`:

| Parameter | Default | Role |
| --- | --- | --- |
| `EmbeddingModel` | `text-embedding-3-small` | The model that embeds queries and chunks. Recorded on the run's configuration line. |
| `EmbeddingDimensions` | **1536** | Vector width. Defines the Qdrant collection; a mismatch is caught by a probe embedding before indexing. |
| `EmbeddingBaseUrl` / `EmbeddingApiKey` | (fall back to OpenAI) | Where embeddings are fetched from. |
| `QdrantEndpoint` | `http://localhost:6334` | gRPC endpoint (not the 6333 REST port). |
| `QdrantCollection` | `case_documents` | Collection searched. |
| `MaxSearchResults` | **8** | Hits per lookup. A plan may ask for *more* (a wider pool) but not fewer; `0` = unbounded. |
| `MaxPassagesPerGroup` | **24** | The per-group cap after ranking. `0` = unbounded. |
| `ReservedSlotsPerTargetedCategory` | **1** | Slots held per targeted category when the cap bites. |
| `ReservedSlotsPerDeclaredSection` | **1** | Slots held per declared section hint. |
| `MaxTokensPerChunk` / `ChunkOverlapTokens` | **600 / 100** | Chunk sizing at index time. |
| `TenantId` | **99** | Scopes every search and every stored chunk. |
| `CoreQueriesOnly` | false | When set, Supplementary queries are not run. |

**On `MaxSearchResults` and `MaxPassagesPerGroup` being separate.** The first bounds how many hits each
*lookup* returns; the second bounds how many survive *ranking* into one group's pack. A plan can widen
the pool per call (`resultsPerCall`) but never narrow it below the run's floor — a plan quietly
retrieving less than the rest of the run is the kind of difference nothing in the output would show.
An unbounded setting outranks any per-plan figure, in the other direction: asking for 40 where the run
asks for everything is asking for less.

There is **no temperature, top-p or seed** in retrieval — those are chat sampling parameters, and
search calls no chat model. Retrieval's determinism comes from the deterministic ranking and the
reproducible tie-breakers, not from pinned sampling.

---

## 10. Diagnosing retrieval without a run

`RetrievalDryRun` executes every plan's retrieval — the embeddings and the two lookups per query, the
de-duplication, and the same `Rank` with the same settings — and reports what came back empty, **with
no model call**. Because retrieval is deterministic and model-free, a defect in the plans' retrieval
can be found in seconds for the price of the embeddings, rather than in the findings of a run that
costs pounds.

It exists for a failure that is silent by construction. A declared `evidenceSection` that matches no
retrieved passage reserves nothing and changes nothing; a query whose `expectSignals` never arrive has
searched for something it did not find. Neither raises an error. The dry run distinguishes:

- **a section matching no retrieved passage** — no query for that group retrieved the chunk carrying
  the string; the fix is a query closer to the chunk, not a reworded hint;
- **a section retrieved but evicted** — the chunk was retrieved and ranking dropped it; the fix is a
  reserved slot or a higher cap;
- **a query whose signals never arrived**, and **a group that retrieved nothing at all**.

A checking test that a hint's string appears in a document of a category the group searches proves the
hint *could* match; it cannot prove any query *retrieves* the chunk holding it, and hints have passed
that check while firing on nothing. Only executing the searches answers the question, which is why the
dry run exists and why it is the first step when a hint fires on nothing. The searched extract it
produces — the passages each group actually retrieved, in pack order — can be downloaded to see what a
group got instead of the chunk it wanted (see [model-extraction.md](model-extraction.md)'s counterpart tooling).

---

## 11. What the search deliberately does not do

- **It does not call a model.** The only LLM is the embedder, and only to vectorise text. Ranking,
  de-duplication and category handling are code.
- **It does not read the suitability report.** Category I is the assertion side, answered from the
  canonical model; searching it as evidence would let the report corroborate itself.
- **It does not hard-filter to target categories.** It searches both filtered and unfiltered, so
  evidence in an unnamed category can still surface.
- **It does not interpret the query.** The text is embedded as written; a query in check vocabulary
  rather than document vocabulary fails silently, which is why the query text is the field that most
  rewards care.
- **It does not let a plan retrieve less than the run's floor**, or a case-specific value into a
  signal — the first would be invisible, the second would report a false absence on the next case.
