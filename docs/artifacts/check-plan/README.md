# Check query plans

A pre-computed semantic-search programme for each of the ten QA checks, executed by [CheckPlanRunner](../../../src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs) against [CaseDocumentSearchService](../../../src/AiPromptEvaluator.Core/Services/Documents/CaseDocumentSearchService.cs). Every query group corresponds to exactly one row of [check-coverage-matrix.csv](../check-coverage-matrix.csv).

These files are deployed beside the executable on build, so editing one changes what the next check run retrieves.

| File | Groups | Queries |
|---|---:|---:|
| [CHK-001](CHK-001.query-plan.json) Suitability completeness against client information | 8 | 27 |
| [CHK-002](CHK-002.query-plan.json) Objectives, needs and time-horizon alignment | 6 | 20 |
| [CHK-003](CHK-003.query-plan.json) Risk, capacity for loss and investment strategy match | 7 | 26 |
| [CHK-004](CHK-004.query-plan.json) Knowledge, experience and product complexity | 5 | 16 |
| [CHK-005](CHK-005.query-plan.json) Financial resilience, affordability and liquidity | 6 | 21 |
| [CHK-006](CHK-006.query-plan.json) Recommendation rationale and suitable alternatives | 4 | 19 |
| [CHK-007](CHK-007.query-plan.json) Costs, charges and value of the recommendation | 7 | 27 |
| [CHK-008](CHK-008.query-plan.json) Disadvantages, risks and customer understanding | 5 | 21 |
| [CHK-009](CHK-009.query-plan.json) Replacement or switch justification | 7 | 30 |
| [CHK-010](CHK-010.query-plan.json) Vulnerability, support needs and foreseeable harm | 4 | 17 |
| **Total** | **59** | **224** |

Schema: [query-plan.schema.json](query-plan.schema.json). All ten plans validate against it.

---

## The constraint that shapes every plan

`CaseDocumentStore.SearchAsync` builds its Qdrant filter from **tenant and case reference only**:

```csharp
filter.Must.Add(Conditions.Match(TenantKey, tenantId));
filter.Must.Add(Conditions.MatchKeyword(CaseKey, caseReference));
```

There is a payload index on `category_code`, but nothing uses it at query time, and `search_case_documents` exposes no category parameter — only `searchText`, `caseReference` and `tenantId`. **You cannot ask for "the fact find's view of income".** Every search competes across the whole case file, and each call returns at most `MaxSearchResults` (default 8) chunks.

Since nine of the ten checks are consistency comparisons between what the report says and what the file holds, the plans compensate in three ways:

1. **Paired queries.** Each group carries at least one `side: "Assertion"` query worded the way a suitability report writes ("*you are currently aged and married*", "*this switch is in your best interests for the following reasons*") and at least one `side: "Evidence"` query worded the way the source document writes ("*attitude to risk questionnaire result score profile*", "*transfer value quotation surrender value*"). Both sides of the diff get retrieved even though neither can be filtered for.

2. **`targetCategories` as a post-filter.** Each query declares which category codes a useful hit should carry. Hits outside that set are context, not evidence. The tool already returns `CategoryCode` and `CategoryName` on every match, so this is applied to results, not to the request.

3. **Query text in document vocabulary, not check vocabulary.** The embedding matches document language. `CHK-003`'s check text says "*risk reconciliation*"; no report ever writes that. The plan searches `"we agreed a risk rating rather than the questionnaire result did not fully reflect"` instead — which is how the override actually reads on the page.

Each query also declares `expectSignals`: the concrete tokens that indicate the search landed. If none appear across any hit, the data point is genuinely absent from the case, which for most checks is the finding rather than a failed search.

---

## Plan anatomy

```
triggerProbe    runs first; decides "N/A: trigger absent" from evidence rather than assumption
queryGroups[]   one per matrix row
  requirement     the "What to Look For" item this group answers
  limb            Consistency | Appropriateness
  canonicalPaths  where the answer lands in the canonical model
  queries[]       id, text, side, targetCategories, expectSignals, priority
  comparison      method, tolerance, falsePositiveGuards
  sufficiency     what to conclude when one side or both come back empty
decision        how group outcomes roll up to No Issue / Potential Concern / N/A
```

**`falsePositiveGuards` is the field that earns its keep.** Naïve field comparison over this domain generates mostly noise, and each guard kills a specific known false positive:

- gross vs net figures (CHK-001) — the single largest source of spurious mismatches
- weekly amounts annualised at 52, not 4.33 × 12 (CHK-001)
- provider risk scales that are not interchangeable — a 5 on 1–10 is not a 5 on 1–7 (CHK-003)
- a questionnaire result differing from the agreed rating is an *override*, not a mismatch (CHK-003)
- fees taken from the fund are not an income commitment and must not be tested against monthly surplus (CHK-005)
- pension access restrictions are irrelevant for a client already over minimum pension age (CHK-005)
- illustration growth rates are prescribed and will differ from cashflow assumptions (CHK-007)
- chunk retrieval order does not indicate page position, so prominence must be judged on headings (CHK-008)
- age alone is not vulnerability (CHK-010)

`limb` distribution follows the source CSV faithfully: CHK-001, CHK-006 and CHK-008 carry consistency groups only, because those checks are labelled "Consistency only" in the source.

---

## How a plan is executed

`CheckPlanRunner` runs the plan; the model never chooses a search. For each check:

1. **`triggerProbe`** runs first. It reads the canonical model's own `checkTriggers` field — derived when the report was read in full, which is better evidence than a similarity search — and corroborates it with the probe searches. Where `onAbsent` is `ReturnNA` (CHK-003, CHK-005, CHK-006, CHK-009) a missing trigger settles the check immediately, skipping the whole retrieval pass.
2. **Each group is gathered.** `side: "Assertion"` queries are *not* searched — their `canonicalPaths` are resolved against the stored canonical model. `Evidence` and `Either` queries are run against the vector store, de-duplicated, ranked with `targetCategories` as a post-filter, and capped at 12 passages per group.
3. **One decision call.** The model receives a pre-assembled pack — assertions, passages, `comparison` guards, `sufficiency` rules — and returns a structured finding per group. It judges evidence it did not assemble.

Three groups are assertion-only by design (`IsModelOnly`): CHK-001 G1.8 internal contradictions, CHK-007 G7.6 cost arithmetic, CHK-008 G8.5 prominence. All three compare the report against itself, so the runner tells the assessor no evidence was sought — rather than letting an empty evidence section read as a gap.

Where a query's `expectSignals` appear in none of its hits, the runner says so explicitly: that data point is absent from the case file, not merely unretrieved.

## Cost

224 queries × 8 results is an upper bound of ~1,800 passages per case across all ten checks, before de-duplication — and heavy overlap is expected, since the same charges table answers parts of CHK-001, CHK-007 and CHK-009. The runner de-duplicates within a group and caps at 12 passages; `triggerProbe` skips inapplicable checks entirely (a case with no switch skips all 30 CHK-009 queries).

`priority` (192 Core, 32 Supplementary) is carried in the plans but not yet used to filter — running Core only would cut roughly 15% of retrieval with little loss of coverage, and is the obvious next lever if cost becomes a constraint.

The suitability report itself is read **once**, at extraction, not per check.

## Known limitations

- **`targetCategories` cannot be enforced at query time.** If a case has one very large category I document, it can dominate every result set and starve the evidence side. Adding an optional `categoryCodes` filter to `SearchAsync` — the payload index already exists — would fix this properly and is the single highest-value change to the search tool.
- **Query text is tuned to UK retail pension and investment reports.** Protection, mortgage and equity release cases will need their own vocabulary; the group structure holds, the phrasings do not.
- **`expectSignals` are heuristics.** They tell you a search probably landed; they do not confirm the passage is relevant. The model still has to read.
- **Chunk boundaries can split tables.** With `MaxTokensPerChunk` at 600, a wide existing-arrangements table may be cut mid-row, so a per-plan value can retrieve without its column header. Several plans query the same table from different angles for this reason.
