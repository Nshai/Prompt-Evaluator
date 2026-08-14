# Estimated cost analysis - Haiku 4.5 vs Sonnet 4.6

Pre-run estimate for assessing the 10 QA checks in `docs/example-use-case` against the A-I test-case documents, on `claude-haiku-4-5` and `claude-sonnet-4-6`.

> **These are estimates, not measurements.** Claude's tokenizer is not public, so input tokens are derived from measured character counts using a chars-per-token heuristic. Actual billed tokens come back on each API response and will differ. Treat the range, not the midpoint, as the honest answer.

## Method

| Input | Value | Source |
|---|---|---|
| Checks | 10 | `Assessment Checks v1.csv` |
| Documents | 28 across categories A-I | `Feasability-testCases/` |
| Extracted text | 659,075 chars | PyMuPDF / python-docx / openpyxl |
| Prompt chars (all checks) | 2,931,087 | measured after assembly |
| Chars per token | 3.6-4.2 (mid 3.9) | heuristic for dense business English |
| Scanned pages | 51 page-instances @ ~2,000 tokens | 3 image-only PDFs, reused across 3 checks |
| Output per response | 500-1200 tokens (mid 800) | structured verdict format, `max_tokens=2000` |
| Prompt caching | not used | each check sends a different document set |

Documents are sent once per check that references their category, so documents used by several checks are paid for several times. That repetition, not the corpus size, is what drives the bill: the corpus is ~0.66M chars but ~2.93M chars get sent.

## Rates

| Model | Input $/MTok | Output $/MTok | Context |
|---|---:|---:|---:|
| `claude-haiku-4-5` | 1.00 | 5.00 | 200K |
| `claude-sonnet-4-6` | 3.00 | 15.00 | 1M |

## Estimated input size per check

| Check | Categories | Docs | Prompt chars | Scanned pages | Est. input tokens (low-high) |
|---|---|---:|---:|---:|---:|
| CHK-001 | A,B,C,I | 5 | 120,079 | - | 28,590 - 33,355 |
| CHK-002 | B,C,F,I | 5 | 108,400 | - | 25,810 - 30,111 |
| CHK-003 | B,D,E,G,I | 18 | 337,209 | 17 | 114,288 - 127,669 |
| CHK-004 | B,C,G,H,I | 11 | 378,895 | - | 90,213 - 105,249 |
| CHK-005 | B,F,I | 4 | 104,029 | - | 24,769 - 28,897 |
| CHK-006 | B,C,F,G,I | 8 | 177,329 | - | 42,221 - 49,258 |
| CHK-007 | B,C,E,F,G,H,I | 25 | 605,392 | 17 | 178,141 - 202,164 |
| CHK-008 | B,C,G,H,I | 11 | 378,523 | - | 90,125 - 105,145 |
| CHK-009 | B,E,F,G,H,I | 24 | 600,780 | 17 | 177,043 - 200,883 |
| CHK-010 | A,B,C,I | 5 | 120,451 | - | 28,679 - 33,459 |
| **Total** | | | **2,931,087** | **51** | **799,879 - 916,190** |

## Estimated cost per check

Midpoint assumptions. Each check is run once per model.

| Check | Haiku in | Haiku out | Haiku total | Sonnet in | Sonnet out | Sonnet total |
|---|---:|---:|---:|---:|---:|---:|
| CHK-001 | $0.0308 | $0.0040 | $0.0348 | $0.0924 | $0.0120 | $0.1044 |
| CHK-002 | $0.0278 | $0.0040 | $0.0318 | $0.0834 | $0.0120 | $0.0954 |
| CHK-003 | $0.1205 | $0.0040 | $0.1245 | $0.3614 | $0.0120 | $0.3734 |
| CHK-004 | $0.0972 | $0.0040 | $0.1012 | $0.2915 | $0.0120 | $0.3035 |
| CHK-005 | $0.0267 | $0.0040 | $0.0307 | $0.0800 | $0.0120 | $0.0920 |
| CHK-006 | $0.0455 | $0.0040 | $0.0495 | $0.1364 | $0.0120 | $0.1484 |
| CHK-007 | $0.1892 | $0.0040 | $0.1932 | $0.5677 | $0.0120 | $0.5797 |
| CHK-008 | $0.0971 | $0.0040 | $0.1011 | $0.2912 | $0.0120 | $0.3032 |
| CHK-009 | $0.1880 | $0.0040 | $0.1920 | $0.5641 | $0.0120 | $0.5761 |
| CHK-010 | $0.0309 | $0.0040 | $0.0349 | $0.0927 | $0.0120 | $0.1047 |
| **Total** | | | **$0.8936** | | | **$2.6807** |

## Range

| Scenario | Haiku 4.5 | Sonnet 4.6 | Both |
|---|---:|---:|---:|
| Low (fewer tokens, terse answers) | $0.8249 | $2.4746 | $3.2995 |
| Mid (planning figure) | $0.8936 | $2.6807 | $3.5742 |
| High (more tokens, long answers) | $0.9762 | $2.9286 | $3.9048 |

**Planning figure: $3.5742 for the full run** ($0.8936 Haiku + $2.6807 Sonnet 4.6).

## Observations

1. **Sonnet 4.6 costs 3.0x Haiku 4.5 here.** The rate card is 3x on both input and output, and this workload is input-dominated (~99.1% of tokens are input), so the blended ratio tracks the input ratio almost exactly. Output length barely moves the total.
2. **Two checks are 44% of the spend.** CHK-007 and CHK-009 pull in 6-7 document categories each (189,229 and 188,046 tokens), against 19% for the five smallest checks combined. Trimming the document set for these two is the single highest-leverage saving.
3. **Context risk on Haiku:** CHK-007, CHK-009 may exceed the 200K context window at the high end of the estimate. Sonnet 4.6 (1M) is unaffected.
4. **Scanned PDFs add ~102,000 tokens per model** ($0.1020 Haiku, $0.3060 Sonnet) - the three image-only Standard Life PDFs, charged in each of the 3 checks that use category E. Excluding them saves that much but assesses those checks on incomplete evidence.

## Ways to cut the bill

| Lever | Saving | Trade-off |
|---|---|---|
| Haiku only | ~75% | Loses the model comparison |
| Prompt caching on shared categories | up to ~50% of input | Only helps if checks are ordered to share a cache prefix; 5-min TTL |
| Drop the 3 scanned PDFs | ~$0.4080 | CHK-003/007/009 assessed on incomplete evidence |
| Pre-filter documents per check | large | Needs a retrieval step; risks dropping evidence |
| Batch API | 50% | Not latency-sensitive here, so this is nearly free money |

---

*Generated before the run. Replace with measured `usage` figures once the checks have been executed.*
