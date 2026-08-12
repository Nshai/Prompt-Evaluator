# Artifacts — Canonical Suitability Model v1.0

Derived from the QA assessment checks (CHK-001 … CHK-010), the A–I document category taxonomy, the example suitability report, and Intelliflo Office resource shapes in `consolidated-swagger.json`.

**These are not documentation only — the app reads two of them at run time.** `AiPromptEvaluator.csproj` copies `canonical-suitability-model.schema.json` and `check-plan/*.query-plan.json` beside the executable on build, so a fresh clone works with no configuration. Editing a query plan here changes what the next check run retrieves. Both locations are overridable under Settings → Canonical model.

| File | What it is |
|---|---|
| [canonical-suitability-model.md](canonical-suitability-model.md) | The design document: why the model is shaped this way, structure, provenance approach, extraction guidance, limitations. **Start here.** |
| [canonical-suitability-model.schema.json](canonical-suitability-model.schema.json) | JSON Schema (draft 2020-12) — the machine-readable contract for LLM extraction output. **Deployed with the app.** |
| [check-coverage-matrix.csv](check-coverage-matrix.csv) | Every check requirement → canonical path → evidence categories → trigger. 60 rows covering all 10 checks, both consistency and appropriateness limbs |
| [io-resource-mapping.md](io-resource-mapping.md) | Canonical field → Intelliflo Office schema mapping, including which enumerations are copied verbatim and which constructs deliberately have no Office equivalent |
| [examples/suitability-report-test-1.extract.json](examples/suitability-report-test-1.extract.json) | Worked instance from a five-plan pension consolidation report; validates against the schema |
| [check-plan/](check-plan/) | Semantic-search query plans, one per check — 59 query groups, 224 queries. **Deployed with the app.** |

## How the app uses this

`Extract Model` parses the case's category I documents into an instance of the schema and stores it in SQLite against the case reference and tenant. `Run Check` then works from that stored model — the suitability report is never sent to a model a second time.

Each query plan splits every requirement into two sides, and the runner executes them from different places:

- `side: "Assertion"` — what the report claims → resolved from the stored canonical model by canonical path
- `side: "Evidence"` / `"Either"` — what the rest of the case file holds → retrieved from the vector store

The plan chooses the searches, not the model, so two runs over the same case retrieve the same evidence.

## The one-paragraph version

Nine of the ten checks ask the same question — *"is what the suitability report says consistent with the evidence provided?"* — so the model is deliberately **not** a suitability-report schema. It is a model of the **advice case** (client, money, goals, risk, existing plans, recommendation, costs), populated once from the report and once from the supporting evidence, so the consistency limb of every check becomes a field-level diff between two instances of the same shape and the appropriateness limb becomes rules over the merged instance. Every entity carries provenance (page, quote, confidence) and an `assertionStatus` of Stated / Inferred / Derived / **Absent**, because "the report never says this" is itself the finding for most checks.

## Validation

```bash
pip install jsonschema
python -c "import json,jsonschema; jsonschema.Draft202012Validator(json.load(open('canonical-suitability-model.schema.json'))).validate(json.load(open('examples/suitability-report-test-1.extract.json')))"
```
