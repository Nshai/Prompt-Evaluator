# AI Prompt Evaluator

AI Prompt Evaluator is a Windows Forms desktop utility for sending prompts to an
OpenAI-compatible model, optionally enriching the prompt with local document
context, and showing the response alongside a **per-component cost breakdown** on
the main screen.

Its second screen, the **Check Evaluator**, assesses a client case file against a
catalogue of QA checks. Case documents are indexed into a Qdrant vector store and
the model retrieves the passages it needs through a search tool, rather than
having the whole case file attached to every check.

## Features

- Prompt input and response display in a Windows Forms interface
- **Cost breakdown on the main screen** — input, cached input, and output tokens
  each shown with their token count, per-million rate, and dollar cost, plus a
  running total
- Re-prices the last response instantly when you switch models
- Optional local document folder ingestion for prompt context
- **Check Evaluator** — semantic indexing of a case folder and tool-driven,
  retrieval-grounded assessment of each check
- Any OpenAI-compatible endpoint: configurable base URL, API key, chat model and
  embedding model
- Configuration dialog with persistent app settings
- xUnit tests for the pricing/cost logic, chunk metadata, and document context builder
- MSI installer that opens in Visual Studio 2022 and 2026

## How the Check Evaluator works

1. **Load Docs** walks the case folder and indexes every Markdown file:
   - [`MarkdownReader`](https://learn.microsoft.com/dotnet/ai/) parses the document
     into sections and elements.
   - `SemanticSimilarityChunker` splits it where the topic shifts, using the same
     `IEmbeddingGenerator<string, Embedding<float>>` the search queries with, so
     cut points and queries live in the same vector space.
   - Each chunk is written to Qdrant with **case reference, tenant id, document
     name and document category** as payload.
2. **Run Check** sends the check's prompt to the model with one tool available,
   `search_case_documents(searchText, caseReference?, tenantId?)`. Each call
   embeds the search text, filters Qdrant by case and tenant, and returns the
   matching passages — across as many documents as match — each with its case
   reference, tenant id, document name and category.
3. The model searches as many times as it needs, then states the finding, citing
   the documents it relied on. The searches it ran are appended to the response so
   the run can be audited.

Only `.md` / `.markdown` files are indexed; other formats are listed as skipped.
Re-loading a case clears its previously indexed chunks first, so an edited
document never leaves a stale passage behind. **Unload Docs** deletes the
embeddings for the current case reference and tenant — other cases, and the same
case under another tenant, are untouched.

The case reference defaults to the case folder's name and can be set explicitly
in Configuration, which is what you want when the folder is a working copy rather
than named after the case.

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
implying it is exact. Embedding calls made during indexing and search are billed
by the provider but are not shown in this breakdown.

## Project Structure

- [src/AiPromptEvaluator/](src/AiPromptEvaluator/) — Windows Forms application
- [src/installer/](src/installer/) — MSI installer project
- [tests/AiPromptEvaluator.Tests/](tests/AiPromptEvaluator.Tests/) — xUnit tests
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
| Results per search | How many passages one tool call may return |
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
