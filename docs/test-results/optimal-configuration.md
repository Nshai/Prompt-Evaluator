# Optimal configuration

The settings to run this pipeline with, and the evidence for each. Every value below is either
measured on case ABC-99 or reasoned from a specific observed failure — where a setting has not
been tested, it says so rather than inventing a recommendation.

**Read this alongside** [expected-results-benchmark.md](expected-results-benchmark.md) (what a
correct run should find) and [gap-analysis.md](gap-analysis.md) (what runs
actually found).

> **Two settings account for almost all of the variation observed so far.**
> `MaxSearchResults` moved benchmark recall from **56% to 22%** when it dropped from 8 to 1, and
> `ExtractionMaxTokens` at 16,000 silently lost a whole section of the canonical model. Neither
> failure announced itself. Get these two right before tuning anything else.

---

## The short answer

| Setting | Use | Why |
| --- | --- | --- |
| `maxSearchResults` | **8** | 1 costs 34 points of recall, 16 buys nothing; see [§1](#1-maxsearchresults--8) |
| `extractionMaxTokens` | **32000** | 16,000 truncated the recommendations section |
| `maxTokensPerChunk` | **600** | untested — default, retained |
| `chunkOverlapTokens` | **100** | untested — default, retained |
| `maxEmbeddingInputCharacters` | **20000** | clears Titan's 50,000-char cap and OpenAI's 8,192-token one |
| `maxParallelRequests` | **6** | measured ~618 tok/s aggregate; see [§4](#4-concurrency) |
| `maxParallelChecks` | **4** | readability bound, not a throughput one |
| `pinTemperature` / `temperature` | **true / 0** | reproducibility |
| `pinTopP` / `topP` | **true / 1** | reproducibility |
| `pinSeed` / `samplingSeed` | **true / 1** | reproducibility, where the provider honours it |
| `structuredFindings` | **true** | schema-constrained findings; ordering is load-bearing |
| `maxTokens` | **4096** | check findings only; unrelated to extraction |

As JSON, for `%LOCALAPPDATA%\AiPromptEvaluator\settings.json`:

```json
{
  "maxSearchResults": 8,
  "extractionMaxTokens": 32000,
  "maxTokensPerChunk": 600,
  "chunkOverlapTokens": 100,
  "maxEmbeddingInputCharacters": 20000,
  "maxParallelRequests": 6,
  "maxParallelChecks": 4,
  "pinTemperature": true,  "temperature": 0,
  "pinTopP": true,         "topP": 1,
  "pinSeed": true,         "samplingSeed": 1,
  "structuredFindings": true,
  "maxTokens": 4096
}
```

---

## 1. `maxSearchResults` — 8

**The single most consequential setting in the application.** It caps how many passages one
planned search may return, and since the two-pass category search issues an unfiltered and a
filtered query per search, a group receives up to `2 × maxSearchResults` per search before
de-duplication and the 12-per-group cap.

Measured on the same case, same plans, same code:

| Value | Hits from 154 searches | Passages after cap | Benchmark recall |
| --- | --- | --- | --- |
| 8 | 2,436 | 682 | **56%** (20 of 36 material findings) |
| **1** | **308** | **209** | **22%** (8 of 36) |

Nine of ten checks lost ground at 1. CHK-009 went from 5 findings to 2, CHK-004 from 4 to 2, and
**CHK-005 caught none of its four**. The findings read as starvation throughout — *"no passage
quantifies"*, *"passages do not contain"*.

**Do not run below 8.** At low values the two-pass search also degrades in a way that is easy to
miss. At 1 it returns one targeted plus one unfiltered hit, and **99 of the 154 searches (64%)
returned the same chunk from both passes** — so for most searches the targeted query added nothing
at all. The design assumes enough headroom that targeted hits *supplement* the unfiltered ones;
below that, they merely duplicate them, and the category fix stops working.

### 16 was tested, and is worse than 8

The obvious next move — double it, and hope category B benefits — was measured. It does not work,
and the reason is worth understanding because it applies to any further increase.

| | top 8 | top 16 |
| --- | --- | --- |
| Hits from 154 searches | 2,436 | **4,791** |
| Passages after cap | 682 | **684** |
| Hits discarded | 72% | **86%** |
| Groups at the 12-passage cap | 56 / 57 | **57 / 57** |
| Category-group touches | 157 | **150** |

**Twice the retrieval cost bought two extra passages.** `MaxPassagesPerGroup` is 12, every group
was already at or near it, and the cap does not care how large the candidate set was.

Worse, **category diversity fell**. A bigger candidate set means the top twelve are drawn from
whichever documents score best, so each group sees *fewer* distinct categories, not more:

| Category | top 8 | top 16 |
| --- | --- | --- |
| A Client Authority | 9 | 6 |
| **B Know Your Client** | 16 | **15** |
| C Meetings & Communications | 10 | 9 |
| G Research | 25 | 22 |
| H Solution Design | 19 | 16 |
| I Recommendations | 45 | 40 |

Every category fell except Needs Analysis. **Category B did not improve, and CHK-007, CHK-008 and
CHK-009 still receive nothing from the Fact Find.**

The lesson generalises: **once groups are at the passage cap, the search limit stops being the
binding constraint.** Raising it adds candidates that are then thrown away, and biases what
survives toward whatever scores highest — which is not the same as what the check needs. The fix
for category B is a per-category floor in the pack, or a relevance threshold that clears out weak
matches to make room, not a larger candidate set.

**Do not raise this above 8 without first raising `MaxPassagesPerGroup`**, and treat that as a
separate experiment with its own measurement.

---

## 2. `extractionMaxTokens` — 32,000

Caps the output of one extraction pass. At **16,000** the recommendations section was cut off
mid-string at 61,422 characters, with four of five recommendations already complete, and the
whole section was discarded.

Measured section sizes on this report:

| Section | At 16,000 | After the quote-discipline prompt change |
| --- | --- | --- |
| Recommendations | 61,422 (truncated) | **32,202** (complete) |
| Replacement analysis | 56,410 | 46,044 |
| Costs and charges | 47,751 | 44,724 |

**32,000 with the current prompt leaves roughly a 40% margin** on the largest section. Both halves
matter: the raised cap alone would have made the run succeed, and the prompt change alone would
have brought recommendations under the *old* cap.

A truncated pass is now salvaged to its last complete array element and reported, so overrunning
is no longer total loss — but headroom is cheaper than salvage. **A case with more clients, more
arrangements or more recommendations will need more.** Raise it before assuming the salvage path
will cover you; the UI allows up to 128,000.

---

## 3. Sampling — temperature 0, top-p 1, seed pinned

Pin all three. The provider default is a creative temperature, which was the largest single source
of run-to-run disagreement before this was addressed.

Each is releasable on its own, because a gateway can reject one without objecting to the others —
a Bedrock inference profile for Claude Sonnet 5, for instance, accepts only temperature 1. Unpin
the one that is refused, not all three.

> **A caution about verifying this.** Pinned sampling is necessary for reproducibility and has
> **not been shown sufficient**. Every apparently-identical pair of runs in this dataset is one
> real generation and one gateway cache hit — a 2-second run reproducing a 130-second one. Before
> treating two runs as independent evidence of determinism, **check their wall-clock duration**.
> See [gap-analysis.md §1](gap-analysis.md#1-what-the-runs-actually-are).

---

## 4. Concurrency

`maxParallelRequests` is a **run-wide budget**, not a per-level one. Checks and their requirement
groups both fan out, and bounding each level separately multiplies into a request count neither
setting names.

**6** is the measured working value: genuine runs sustained ~618 tokens/second aggregate across
60 groups, or roughly 100 tok/s per stream, which is normal for the model in use. Raise it only
against a provider whose rate limits you know; the failure mode is throttling mid-run, which
costs the whole run rather than one call.

`maxParallelChecks` at **4** bounds how many checks report progress at once. This is a readability
constraint on the live board, not a throughput one — the real limit is the request budget above.

---

## 5. Chunking and embedding

`maxTokensPerChunk` 600 and `chunkOverlapTokens` 100 are the defaults and **have never been
varied**. They are recorded here for completeness, not recommended on evidence. Chunk size plausibly
interacts with `maxSearchResults` — smaller chunks mean more of them are needed to carry the same
argument — and that interaction is untested.

`maxEmbeddingInputCharacters` **20,000** is evidence-based. A 156,384-character element was sent to
Amazon Titan in one call and refused (`expected maxLength: 50000`), losing the whole document.
20,000 clears Titan's 50,000-character cap and OpenAI's 8,192-token limit with room for
multi-byte characters. `EveryConvertedCaseDocument_FitsTheEmbeddingLimit` walks all 28 documents
of this case and fails if any element exceeds it.

**`embeddingDimensions` must match both the model and the existing Qdrant collection.** Changing
it silently invalidates every stored vector; the collection has to be recreated and the case
re-indexed. This is not a tuning knob.

---

## 6. Verifying a configuration took effect

Every run log now opens with the settings it used:

```
[RUN CONFIGURATION]
Chat claude-haiku-4-5 · temperature 0, top-p default, seed not pinned · findings schema-constrained
Embeddings text-embedding-3-small · top 8/search, 12/group · extraction cap 32,000 tok ·
plans 10@abc123def456 · model 0011223344ff (schema v1.0)
```

**Read it before analysing any run.** The investigation that produced this document began with a
run whose retrieval had collapsed, and establishing why meant inferring `maxSearchResults` from
the arithmetic of hit counts and then finding that the local settings file belonged to a different
environment altogether.

Two arithmetic checks that catch a misconfiguration in seconds:

- **Hits ÷ searches** should be `2 × maxSearchResults` where plans carry target categories. 16 is
  healthy; 2 means the limit is 1.
- **Extraction sections** should all close their JSON. A section reported as failed for
  truncation names the cap it hit.

---

## 7. Known gaps that no setting fixes

Configuration cannot close these. They are recorded so a good configuration is not mistaken for a
solved pipeline.

- **No relevance floor.** Every search returns exactly its limit regardless of whether anything is
  relevant; the lowest-scoring passage admitted scored 0.503. `"Nothing was retrieved"` has never
  once appeared in a log. One consequence seen in practice: an unfilled transfer questionnaire was
  read as evidence that benefits were being lost, in a document that states plainly elsewhere that
  the plan has no life cover.
- **Category B starvation.** Four checks — CHK-005, 007, 008, 009 — receive no passage from the
  Fact Find, which the catalogue names as a primary category for all four. Seven of the ten
  material findings missed at `maxSearchResults: 8` are Fact Find facts.
- **Citation verification over-fires.** 38% of quotes fail against their own evidence pack, most
  on markdown table punctuation.
- **Derived figures are not computed.** Sums and percentages that a check turns on are delegated to
  the model, which is why the pension totals and the mis-based Standard Life charge went unnoticed.

---

## What still needs measuring

| Question | How | Status |
| --- | --- | --- |
| Does `maxSearchResults: 16` help category B? | Compare category reach and recall at 8 and 16 | **answered — no**, see [§1](#16-was-tested-and-is-worse-than-8) |
| Does raising `MaxPassagesPerGroup` above 12 help? | Vary the cap at fixed `maxSearchResults: 8` | not started — now the more promising lever |
| Is the model deterministic at temperature 0? | Two full-duration runs, cache disabled or expired | not started |
| Do chunk size and search limit interact? | Vary `maxTokensPerChunk` at fixed `maxSearchResults: 8` | not started |
| Does the extraction fix restore check recall? | Re-run checks at 8 against the 56% baseline | not started |

**The passage cap is now the more interesting lever.** Every group at `maxSearchResults: 16` sat
at the 12-passage limit and 86% of retrieved hits were discarded, so the pack size — not the
candidate-set size — is what bounds what a check can see. That experiment has not been run.

The last row remains blocking: the extraction fixes are verified on the extraction side and have
never been measured through to the checks, because neither run that exercised them used a healthy
search configuration.
