# AI Prompt Evaluator

AI Prompt Evaluator is a Windows Forms desktop utility for sending prompts to an
OpenAI-compatible model, optionally enriching the prompt with local document
context, and showing the response alongside a **per-component cost breakdown** on
the main screen.

Its second screen, the **Check Evaluator**, assesses a client case file against a
catalogue of QA checks. The suitability report is parsed once into a canonical
data model; the supporting documents are indexed into a Qdrant vector store. Each
check then compares what the report *asserts* against what the case file
*evidences*, retrieving only what a pre-computed query plan asks for.

## Features

- Prompt input and response display in a Windows Forms interface
- **Cost breakdown on the main screen** — input, cached input, and output tokens
  each shown with their token count, per-million rate, and dollar cost, plus a
  running total
- Re-prices the last response instantly when you switch models
- Optional local document folder ingestion for prompt context
- **Check Evaluator** — canonical extraction of the suitability report, semantic
  indexing of the case folder, and plan-driven, retrieval-grounded assessment of
  each check into a consolidated findings report
- Any OpenAI-compatible endpoint: configurable base URL, API key, chat model and
  embedding model
- Configuration dialog with persistent app settings
- xUnit tests for the pricing/cost logic, chunk metadata, document context builder,
  canonical model store and accessor, schema slicing, and the shipped query plans
- MSI installer that opens in Visual Studio 2022 and 2026

## How the Check Evaluator works

### The shape of the problem

Nine of the ten QA checks ask the same question in different words:

> *"Where the Suitability Report includes X, is it consistent with the evidence provided?"*

That is not one question but two, and the check definitions label them: a
**consistency** limb, which is a comparison between the report and the file, and
an **appropriateness** limb, which is a judgement over the facts. The consistency
limb is the bulk of the work, and it needs both sides of a comparison to be
available at once.

Two design decisions follow.

**The suitability report is parsed once, into a canonical model.** Suitability
report templates vary by firm; the underlying advice case does not. So the report
is normalised into a schema describing the *case* — client, money, goals, risk,
existing plans, recommendation, costs — rather than the document. Every value
carries provenance (page, verbatim quote, confidence) and an `assertionStatus` of
`Stated` / `Inferred` / `Derived` / **`Absent`**, because "the report never says
this" is itself the finding for most checks. The model is stored in SQLite against
the case reference and tenant, and **the report is never sent to a model again** —
ten checks cost one parse, not ten.

**Retrieval is decided in advance, by a query plan.** Each check has a plan under
[docs/artifacts/check-plan/](docs/artifacts/check-plan/) whose query groups map
one-to-one onto the rows of the check coverage matrix. The plan chooses the
searches; the model does not. Two runs of the same check over the same case
therefore retrieve the same evidence, which is what makes a finding reproducible
and a regression visible.

### The flow

1. **Load Docs** walks the case folder and indexes every Markdown file:
   - [`MarkdownReader`](https://learn.microsoft.com/dotnet/ai/) parses the document
     into sections and elements.
   - `SemanticSimilarityChunker` splits it where the topic shifts, using the same
     `IEmbeddingGenerator<string, Embedding<float>>` the search queries with, so
     cut points and queries live in the same vector space.
   - Each chunk is written to Qdrant with **case reference, tenant id, document
     name and document category** as payload.
2. **Extract Model** parses the case's category I documents into the canonical
   model and stores it. This runs a section at a time — a full model is tens of
   thousands of tokens of JSON, and a response truncated mid-array is unparseable
   rather than partially useful. Sections are also the retry unit, so one failure
   costs one pass. `JsonSchemaSlicer` walks the schema's `$ref` graph and sends
   each pass only the definitions it reaches (7–29% of the full schema), and the
   report sits identically at the front of every prompt so the provider's prefix
   cache covers the expensive part.
3. **Run Check** / **Run All Checks** executes the plan:
   - The **trigger probe** runs first, reading the model's own `checkTriggers` and
     corroborating it by search. A missing trigger settles the check as *N/A*
     without a retrieval pass.
   - For each group, `side: "Assertion"` queries are resolved from the **stored
     canonical model** by canonical path; `Evidence` and `Either` queries are
     **searched in Qdrant**, de-duplicated, and ranked with the plan's
     `targetCategories` applied as a post-filter.
   - One decision call per check judges a pack it did not assemble, and returns a
     structured finding per group with citations.
4. The consolidated **findings report** lists every outcome, details the concerns
   in full with their evidence, folds cleared checks to a line each, and closes
   with what retrieval actually found — because *No Issue* from a run that found
   nothing is not the same claim as one corroborated across three categories.

### Design notes worth knowing

- **The search tool cannot filter by document category.** Qdrant is filtered on
  tenant and case only, so every search competes across the whole case file. The
  plans compensate by pairing an assertion-side query worded as a report writes it
  with an evidence-side query worded as the source document writes it, and the
  runner applies `targetCategories` to the results.
- **Query text is written in document vocabulary, not check vocabulary.** No
  report contains the phrase "risk reconciliation"; the plan searches *"we agreed
  a risk rating rather than the questionnaire result"* instead.
- **Failures point toward concern, never toward a pass.** An unreadable outcome
  becomes *Potential Concern*; unparseable JSON becomes *Error* carrying the raw
  response. A QA tool that turns confusion into a pass is worse than no tool.
- **Three query groups are assertion-only by design** — internal contradictions,
  charge arithmetic and the prominence of a risk section all compare the report
  against itself. The runner says so explicitly, so an empty evidence section is
  never mistaken for a gap.

Only `.md` / `.markdown` files are indexed; other formats are listed as skipped.
Re-loading a case clears its previously indexed chunks first, so an edited
document never leaves a stale passage behind. **Unload Docs** deletes the
embeddings for the current case reference and tenant; **Delete Model** removes the
stored canonical model. The two are deliberately separate — re-indexing documents
should not silently discard an extraction that cost real tokens to produce.

The case reference defaults to the case folder's name and can be set explicitly
in Configuration, which is what you want when the folder is a working copy rather
than named after the case.

## The canonical model

The schema, the query plans and the design rationale behind them live in
[docs/artifacts/](docs/artifacts/) — start with
[canonical-suitability-model.md](docs/artifacts/canonical-suitability-model.md).

The schema and the plans are **data the app reads at run time**, not documentation.
`AiPromptEvaluator.csproj` copies them beside the executable on build, so a fresh
clone works with no configuration and editing a query plan changes what the next
check run retrieves. Both locations are overridable under **Configuration →
Canonical model**.

## Cost breakdown

The billed token categories are priced separately, using each model's published
rates. Cache rates are multiples of the input rate, and the multipliers differ by
provider:

| Component        | OpenAI                | Anthropic            |
| ---------------- | --------------------- | -------------------- |
| Input (uncached) | model input rate      | model input rate     |
| Cache write      | not billed separately | 1.25x the input rate |
| Cache read       | 0.25x the input rate  | 0.1x the input rate  |
| Output           | model output rate     | model output rate    |

Rates live in [ModelPricing.cs](src/AiPromptEvaluator/ModelPricing.cs) and cover
the current OpenAI models plus Claude Sonnet 5, Sonnet 4.6 and Haiku 4.5 — an
OpenAI-compatible gateway commonly serves Anthropic models, so a run is priced
whichever provider sits behind the configured base URL. A model that is not in
the table still gets an estimate, and the UI labels it as estimated rather than
implying it is exact.

**Embeddings are priced separately, on their own model's rate.** They are a
different model on a different rate card, and a run can be almost entirely one or
the other: indexing a case is pure embedding spend, extracting the canonical model
is pure chat. The breakdown carries an `Embeddings (model)` row alongside the chat
components and the total shows the split:

```
Total: $0.1043 for 1,284,500 tokens — chat $0.0987 (49,350 tokens) · embeddings $0.0056 (1,235,150 tokens)
```

The count comes from a delegating generator wrapped around the embedding client,
so it also captures the calls `SemanticSimilarityChunker` makes internally while
deciding where to cut each document — usually the larger half of what indexing
costs, and previously invisible. **Load Docs** used to report a cost of zero; it
now reports what it actually spent, including when a load is cancelled partway.

Providers are not obliged to return usage on an embeddings response. When one
doesn't, the cost is reported as unknown rather than as zero.

## Project Structure

- [src/AiPromptEvaluator/](src/AiPromptEvaluator/) — Windows Forms application
- [src/installer/](src/installer/) — MSI installer project
- [tests/AiPromptEvaluator.Tests/](tests/AiPromptEvaluator.Tests/) — xUnit tests
- [docs/artifacts/](docs/artifacts/) — the canonical model schema and the per-check
  query plans, deployed with the app and read at run time
- `AiPromptEvaluator.slnx` — solution file

## Requirements

- Windows
- .NET 8 SDK
- Visual Studio 2022 or 2026 with the .NET desktop development workload, or VS Code
- An OpenAI-compatible API endpoint and key
- Docker, for the Qdrant vector store (Check Evaluator only)

## Getting Started

1. Clone the repository.
2. Start Qdrant:
   ```powershell
   docker run -p 6333:6333 -p 6334:6334 -v qdrant_storage:/qdrant/storage qdrant/qdrant
   ```
   Port 6334 is the gRPC API the app talks to; 6333 serves the dashboard.
3. Build the solution:
   ```powershell
   dotnet build AiPromptEvaluator.slnx -c Release
   ```
4. Run the app:
   ```powershell
   dotnet run --project src/AiPromptEvaluator/AiPromptEvaluator.csproj
   ```
5. Open **Configuration...**, enter your API key, and check the Qdrant endpoint
   with **Test** before loading a case.

## Configuration

Settings are stored in the user profile under
`%LOCALAPPDATA%\AiPromptEvaluator\settings.json`:

| Setting | Purpose |
| ------- | ------- |
| API key | Credential for the OpenAI-compatible endpoint |
| API base URL | The endpoint itself — the official API, a gateway, or a self-hosted server. Empty means `https://api.openai.com/v1` |
| Available models / selected model | The chat model list and the one in use |
| Max output tokens | Per-response output cap |
| Embedding model / dimensions | Model used for chunking and search, and its vector width — the width defines the Qdrant collection |
| Qdrant endpoint | gRPC endpoint of the vector store. Empty means `http://localhost:6334` |
| Collection | Qdrant collection holding case chunks |
| Case reference | Stamped on every chunk and used to scope every search. Empty means the case folder's name |
| Tenant id | Stamped on every chunk and applied as a filter on every search (default 99) |
| Max tokens per chunk / overlap | Upper bound on a chunk and how much of the previous one is repeated |
| Results per search | How many passages one search may return |
| Model schema | The canonical model JSON Schema used for extraction. Empty means the copy deployed beside the app |
| Check plan folder | Where the `CHK-*.query-plan.json` files live. Empty means the `check-plan` folder beside the app |
| Model database | SQLite file holding extracted canonical models. Empty means `canonical-models.db` in `%LOCALAPPDATA%\AiPromptEvaluator` |
| Extraction max tokens | Output cap for one extraction pass. Higher than the response cap because a truncated JSON section is unusable rather than merely short |
| Docling endpoint | Sidecar used to convert spreadsheets to Markdown |
| Document context folder | Folder ingested for prompt context on the main screen |
| Clarification prompt behavior | Whether ambiguous prompts get a clarifying question |

Changing the embedding model or its dimensions changes the shape of the
collection — clear the index (**Unload Docs**) and load the case again.

## Testing

```powershell
dotnet test tests/AiPromptEvaluator.Tests/AiPromptEvaluator.Tests.csproj -c Release
```

## Installer

```powershell
dotnet build src/installer/AiPromptEvaluator.Installer.csproj -c Release
```

Produces `src/installer/bin/Release/AiPromptEvaluator.msi`. See
[the installer README](src/installer/README.md) for options and the runtime
prerequisite.
