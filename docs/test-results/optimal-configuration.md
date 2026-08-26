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
| `maxPassagesPerGroup` | **24** | was a hard-coded 12; the binding constraint on coverage, see [§1a](#1a-maxpassagespergroup--24) |
| `reservedSlotsPerTargetedCategory` | **1** | a floor per declared category; two would spend ten of twelve slots on the floor |
| `reservedSlotsPerDeclaredSection` | **1** | a floor per named section, taken first — three such hints moved four findings |
| `extractionReportMaxChars` | **24000** | holds a whole report; 4,000 dropped 55% of it |
| `decisionMaxTokens` | **8000** | caps how many findings one group may write |
| `maxDocumentsInContext` | **50** | the case-file listing a prompt is given |
| `extractionMaxTokens` | **32000** | 16,000 truncated the recommendations section |
| `maxTokensPerChunk` | **600** | untested — default, retained |
| `chunkOverlapTokens` | **100** | untested — default, retained |
| `maxEmbeddingInputCharacters` | **20000** | clears Titan's 50,000-char cap and OpenAI's 8,192-token one |
| `maxParallelRequests` | **6** | measured ~618 tok/s aggregate; see [§4](#4-concurrency) |
| `maxParallelChecks` | **4** | readability bound, not a throughput one |
| `pinTemperature` / `temperature` | **true / 0** | reproducibility — and every run on record has this off, see [§1b](#1b-sampling--pin-it-and-vary-the-seed-to-get-a-second-sample) |
| `pinTopP` / `topP` | **true / 1** | reproducibility |
| `pinSeed` / `samplingSeed` | **true / 1** | reproducibility; varying the seed is also the only cheap way to defeat the gateway cache |
| `structuredFindings` | **true** | schema-constrained findings; ordering is load-bearing |
| `maxTokens` | **4096** | check findings only; unrelated to extraction |
| `selectedModel` | **Haiku 4.5** by default, **Sonnet 4.6** when the run must be right first time | see [§1c](#1c-choosing-the-model--and-why-the-settings-do-not-change-with-it) |

As JSON, for `%LOCALAPPDATA%\AiPromptEvaluator\settings.json`:

```json
{
  "maxSearchResults": 8,
  "maxPassagesPerGroup": 24,
  "reservedSlotsPerTargetedCategory": 1,
  "reservedSlotsPerDeclaredSection": 1,
  "extractionReportMaxChars": 24000,
  "decisionMaxTokens": 8000,
  "maxDocumentsInContext": 50,
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

### Every cap takes 0 for unbounded

`maxSearchResults`, `maxPassagesPerGroup`, `extractionReportMaxChars`, `decisionMaxTokens` and
`maxDocumentsInContext` all read **0 as unbounded** — no limit at all. Zero rather than -1
because each of them counts things shown to a model, and zero of them is never a configuration
anyone wants: a run with no passages or no findings is not a cheaper run, it is a broken one. So
the value that would otherwise be a footgun carries the meaning that has no other spelling. A
negative value reads as unbounded too, rather than failing at the far end of a long run.

The two reserved-slot settings are **floors, not caps**, and are excluded: 0 there means no
reservation, which is a real setting and the way to measure what the floor is worth.

Unbounded is a diagnostic, not a configuration to run on. `maxPassagesPerGroup: 0` sends a
group's whole de-duplicated pool to the assessor — at `maxSearchResults: 8` and seven queries
that is up to 112 passages for one call, against 24 — and `maxSearchResults: 0` asks the vector
store for every match it holds. Use them to find out what a cap has been costing, then set a
number.

The run fingerprint prints `unbounded` rather than a figure, so a diagnostic run cannot be
mistaken for a tuned one later.

---

## 1a. `maxPassagesPerGroup` — 24

**The binding constraint on coverage, and a compile-time constant of 12 until it became a
setting.** That is why §1's experiment came out the way it did: every group already sat at the
cap, so doubling the candidate set changed almost nothing about what an assessor read.

The default moved from 12 to 24 because reserved slots ration the pack *before* ranking begins.
The widest group, CHK-008 G8.1, declares four evidence categories and two evidence sections, so
six of its twelve slots are spoken for and only six are decided by score. At 24, a seven-query
group drawing up to 112 candidates still discards 79% of them.

**24 is reasoned, not measured.** No run on record has varied this value. Treat the first run at
24 as an experiment with its own measurement and compare it against 12 before attributing any
recall change to anything else. The cost is prompt tokens, and it is linear: twice the passages
in every one of the 85 group calls.

---

## 1b. Sampling — pin it, and vary the seed to get a second sample

**The code defaults to pinning temperature, top-p and seed. Every run on record pinned none of
them.** Runs 7 to 11 all report `temperature default, top-p default, seed not pinned`, which means
the local settings file is overriding three defaults that
[AppSettings](../../src/AiPromptEvaluator.Core/AppSettings.cs) sets to `true`. Nothing in the
application did this; a configuration did, and it has quietly cost the project its ability to
measure anything twice.

```json
{ "pinTemperature": true, "temperature": 0,
  "pinTopP": true,        "topP": 1,
  "pinSeed": true,        "samplingSeed": 1 }
```

### Why this is not just tidiness

**The gateway cache makes re-running the same configuration useless as a second sample.** Runs 10
and 11 proved it: an unchanged configuration re-run **5h 23m** later returned the earlier run's
output, byte for byte, at 33,858 tok/s. See
[run-analysis-replays.md](run-analysis-replays.md).

The seed is part of the request — `ChatCompletionClient` sets `options.Seed` when `pinSeed` is on —
so **changing `samplingSeed` changes the cache key**. That makes it the only cheap way to obtain a
genuine second sample, and it is also the measurement worth having: variance cannot be assessed at
all while temperature is at the provider's default.

The run fingerprint prints the seed, so two runs at different seeds are distinguishable in their
own output:

```
Chat … · temperature 0, top-p 1, seed 1 · findings schema-constrained
```

### The protocol

Four runs, one model, nothing else changed:

| Run | `samplingSeed` | `maxPassagesPerGroup` | Answers |
| --- | --- | --- | --- |
| A | 1 | 12 | baseline at the old cap |
| B | 2 | 12 | **variance** — A vs B is the noise floor, measured rather than assumed |
| C | 1 | 24 | **the cap question** — A vs C, the only thing that changed |
| D | 2 | 24 | variance at the new cap; C vs D confirms B's figure holds |

Run them on **Sonnet**, because it is the only model whose recall is high enough for a
one-or-two-finding difference to be visible above the floor. Haiku at 20/36 and Nova at 7/36 have
too much room below them for a cap effect to be separable from noise.

**A and B are the important pair.** Until the noise floor is measured rather than estimated at
±2 findings, no comparison in this repository means what it appears to mean — including the
eight-finding gap between Haiku and Sonnet, which is large enough to survive almost any plausible
variance but has never actually been tested against one.

---

## 1c. Choosing the model — and why the settings do not change with it

**Nothing in the settings differs between Haiku and Sonnet, and that is a finding rather than an
omission.** Retrieval is plan-driven and model-free: three models on the same configuration
produced byte-identical retrieval — 223 searches, 3,520 hits, 224 canonical paths resolved, 31
absent. Every cap that matters was measured against both and neither is near it. So the model
choice is a model choice, not a configuration.

### What was measured

| | Haiku 4.5 | Sonnet 4.6 | Nova 2 Lite |
| --- | --- | --- | --- |
| Best recall | **27 / 36 (75%)** — Run 12, current plans | 28 / 36 (78%) — Run 8, **old plans** | 7 / 36 (19%) |
| Cost | £2.14 | £6.44 | £3.90 |
| **Cost per finding caught** | **£0.079** | £0.230 | £0.557 |
| Wall clock | 325s | 620s | 149s |
| Output rate | 466–532 tok/s | 260 tok/s | 567 tok/s |
| Longest single answer | 4,799 tok | 4,142 tok | 9,873 tok — **over the cap** |
| Indeterminate groups | 0–2 | **0** | 1 |
| Unverifiable quotes | 116–136 | **59** | 37 |

### The recommendation

**Use Haiku 4.5 by default.** On the current plans it catches 27 of 36 at **£0.079 per finding** —
a third of Sonnet's cost per finding and a third of its wall clock. The eight-finding advantage
Sonnet held on the old plans was mostly retrieval and prompt work, not model capability: fixing the
plans moved Haiku from 20 to 27 and closed all but one of the gap.

**Use Sonnet 4.6 when the run has to be right first time** — a real file review, a regulatory
sample, anything where a missed concern is expensive and £4 is not. Two properties beyond recall
argue for it: it produced **no Indeterminate groups at all**, and **half the unverifiable quotes**,
so a reviewer spends less time checking citations that turn out to be formatting artefacts.

**Do not use Nova 2 Lite.** Worst recall and second-highest cost — it catches a quarter of what
Haiku does for nearly twice the price. It also produced the only false negative on record, reading
the fact find's £230 as weekly and telling the reader the report was *"close but not exact"* on a
Highest-severity contradiction. A wrong answer is worse than a missing one, because the reader
acts on it.

**A tiered arrangement was considered and the evidence refuses it.** The union of all three models
on the same run was 28 — exactly what Sonnet caught alone. Haiku found nothing Sonnet missed and
Nova found nothing either of them missed, so running two models and merging buys nothing but the
bill.

### Settings, per model

```json
{ "selectedModel": "intelliflo-claude-haiku-4-5" }
```

and nothing else changes. Every value in [The short answer](#the-short-answer) applies to both:

- **`decisionMaxTokens: 8000`** suits both. Haiku's longest answer used 60% of it, Sonnet's 52%,
  and neither came within 90% of the cap on any of 85 calls. *(Nova exceeded it once, which is
  part of why it is not recommended.)*
- **`maxPassagesPerGroup: 24`** suits both. Utilisation is 26% for Haiku and 29% for Sonnet — a
  median of six cited passages out of twenty-four — so neither is starved and neither would use
  more.
- **`maxSearchResults: 8`**, and the reserved slots, are retrieval settings and identical by
  construction.

**The one place a difference might exist is concurrency, and it has not been measured.** Sonnet
generates at roughly half Haiku's rate and takes twice as long for the same work, so
`maxParallelRequests` above 6 may recover some of that. No run has tried it, and inventing a number
here would be the kind of untested recommendation this document exists to avoid.

### The measurement this table is missing

**Sonnet has never run on the current plans.** Its 28 is from the old ones. If the +7 that plan and
prompt work gave Haiku carries across, Sonnet lands near 33–35 and the default recommendation above
may be wrong. If it does not carry, these fixes substitute for model quality rather than adding to
it — which the Haiku result already hints at, since most of the gap closed without touching the
model.

That single run is the most informative one available and should be taken before this section is
relied on for anything expensive.

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
| Does raising `maxPassagesPerGroup` above 12 help? | Vary the cap at fixed `maxSearchResults: 8` | **now possible without a rebuild**; default moved to 24, unmeasured |
| Is the model deterministic at temperature 0? | Two full-duration runs, cache disabled or expired | not started |
| Do chunk size and search limit interact? | Vary `maxTokensPerChunk` at fixed `maxSearchResults: 8` | not started |
| Does the extraction fix restore check recall? | Re-run checks at 8 against the 56% baseline | not started |

**The passage cap is now the more interesting lever.** Every group at `maxSearchResults: 16` sat
at the 12-passage limit and 86% of retrieved hits were discarded, so the pack size — not the
candidate-set size — is what bounds what a check can see. That experiment has not been run.

The last row remains blocking: the extraction fixes are verified on the extraction side and have
never been measured through to the checks, because neither run that exercised them used a healthy
search configuration.
