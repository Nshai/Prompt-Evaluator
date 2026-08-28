# Next runs — what to execute after the Run 18 changes

This records what was implemented against the Run 18 "what to do next" list, and the two remaining
items that are **measurement runs** the app has to perform against the live gateways — they cannot be
done from the code side. It is written so the runs are reproducible and their results attributable.

The code changes that precede these runs (all landed, 807 tests green):

- **Item 1 — a "Dry Run Retrieval" button.** Executes every plan's retrieval against the live index
  with no model call, and reports which section hints and queries reached no passage. This is the
  blocking diagnostic for F3.1 and F2.2. Run it before rewording any hint.
- **Item 2 — the assessor prompt bound.** A value the report itself needed and did not supply is now
  a Potential Concern, not a `comparisonPerformed: false`. Aimed at lifting G3.11 (F3.4) from
  Partial to Caught.
- **Item 3 — the G7.4 charge-table guard, strengthened.** It now says to compare a plan's two report
  tables *for every plan*, not only the first, before reaching for a provider document. Aimed at the
  Zurich half of F7.1 and at F9.5.
- **Downloadable searched extract.** "Save extract..." on the response toolbar writes the passages a
  run's searches retrieved, per group — from a dry run held in memory, or from the latest archived
  check run. What to read when a hint fires on nothing and the question is what the group got instead.

All three plan/prompt changes are generic — no case, provider or client is named, and lint rule L6
plus the prompt-generality test hold that.

---

## The correction that reframes items 4 and 5

Run 18's analysis (§10 item 5) proposed "re-run 8/24 on Haiku with the extraction pinned, if
PromptCacheBypass can hold the canonical model constant across a plan change." Reading the code
changes the framing:

**A plan change does not force re-extraction, and never did.** The canonical model is stored in
SQLite keyed on `(tenantId, caseReference)` (`CanonicalModelStore`), and a check run loads it with
`LoadAsync` and reuses it (`CheckEvaluatorForm.DetectExistingModelAsync`). A check run **never
re-extracts** — extraction happens only when someone clicks "Extract Model". Runs 17 and 18
re-extracted because that button was clicked, not because the plans changed.

So the extraction is already pinnable across a plan edit, with no code change and without
`PromptCacheBypass`: **edit the plans, then run the checks without re-extracting.** The stored model
from Run 18 (`d055900411a6`) stays constant, and the only variable between Run 18 and the next run is
the plan edits. That is the single-variable comparison item 5 asked for.

`PromptCacheBypass` is a different tool for a different job: it varies the *prompt* per run so the
gateway's response cache misses, which is what you want when you need a genuinely fresh *sample* of
the same configuration (item 4). It does not touch extraction.

---

## Run A — the single-variable plan-change measurement (item 5)

**Question:** did items 2 and 3 move F3.4, F7.1's Zurich half, and F9.5, with nothing else changed?

**Procedure:**

1. Do **not** click "Extract Model". Confirm the loaded model is Run 18's — the status line should
   read *extracted 2026-08-27 16:06* with digest `d055900411a6`. If the store has been cleared,
   re-extract once and treat that as the new baseline (noting the model digest changed).
2. Leave settings at Haiku 4.5, `maxSearchResults 8`, `maxPassagesPerGroup 24`.
3. Click **Run All Checks**.
4. Archive the run as Run-19 alongside its `Run-output.txt`, checks log, and the canonical model
   (unchanged, but archive it so the run is self-describing).

**What is controlled:** the canonical model is byte-identical to Run 18's, the model and retrieval
settings are unchanged, and only the plans differ (the two guard/prompt edits). This is the first
comparison in the project where a recall delta can be attributed to a plan change alone.

**Predictions, stated in advance:**

| Finding | Run 18 | Predicted | Rests on |
| --- | --- | --- | --- |
| F3.4 | Partial | **Caught** | item 2 — the assessor should now conclude "unsupported" rather than NOT ASSESSED on the undefined scale |
| F7.1 (Zurich half) | Caught (SL only) | **Caught, both plans** | item 3 — the guard now names every plan |
| F9.5 | Partial | **Caught or Partial** | item 3 — entailed by F7.1; may follow, may need its own step |
| F3.1, F2.2 | Missed / Partial | **unchanged** | their hints still misfire — Run A does not touch retrieval; see Run C |

If F3.4 or F7.1's Zurich half does **not** move, the change reached the prompt and the model did not
carry it through — which is prompt adherence, and the next step is wording, not mechanism. Record it
either way; a prediction that fails is the useful result.

**Caveat:** sampling is unpinned (`seed not pinned`), so a one-finding move is inside the ±2 noise
floor. Two or more moving together, in the direction predicted, is the signal.

---

## Run B — confirm the prompt-adherence regression (item 4)

**Question:** Run 18's 59/88 wrong-requirement-id echo (67%) is beyond the prior Haiku band of
38–52%. Is that the generalised, slightly longer prompts, or model noise?

**Procedure:**

1. This needs a **fresh sample**, not a cache replay. Tick **Bypass cache** before running, so every
   prompt carries a run marker and the gateway must regenerate. (Without this, an unchanged
   configuration inside the cache TTL returns Run 18's bytes and measures nothing.)
2. Haiku 4.5, `8/24`, same plans as Run A.
3. **Run All Checks**, and read the footer's *Prompt adherence* line.

**Reading it:**

- **Back in the 38–52% band** → Run 18's 67% was sampling noise; nothing to do.
- **Still ~67%** → the generalised prompts are contributing. The prompts grew when case-specific
  values were replaced with domain-general wording; the next step would be to check whether the
  longer requirement text is what the model is echoing wrongly. No verdict depends on this — the
  plan's values are used regardless — but it is worth knowing before the prompts grow further.

Run B can be the same execution as Run A if you tick Bypass cache for Run A; the adherence line is
in every run's footer. Keeping them separate is cleaner only if you want Run A on the warm cache for
speed.

---

## Run C — diagnose the misfiring hints (item 1), no gateway spend on chat

**Question:** why do the five section hints added for F3.1, F2.2 and F3.4's evidence match nothing at
run time, when their strings are in the corpus and pass the reachability test?

**Procedure:**

1. Load the case documents (the index must be populated — "Load Docs" if it is not).
2. Click **Dry Run Retrieval**. It embeds each query once and searches; no chat model is called, so
   this costs embeddings only and returns in seconds.
3. Read the report. For each of `G3.6 ATR Wording`, `G3.8 ATR Wording`,
   `G3.4 Capacity For Loss questionnaire responses`, `G3.4 my investment horizon`,
   `G3.11 The funds available to invest in are`, it will say one of:
   - **matching no retrieved passage** — no query for that group retrieves the chunk carrying the
     string. The fix is a query whose text is closer to the chunk, not a reworded hint.
   - **retrieved but evicted** — the chunk was retrieved and ranking dropped it. The fix is a
     reserved slot or a higher cap, not the query.
4. Click **Save extract...** to download the passages each group actually retrieved, and read what
   G3.6 got *instead* of the ATR-wording chunk. That tells you which query to adjust.

This is the item Run 18 called blocking: rewording a hint without it is guesswork, because the string
is already known to be in the document. The dry run says whether the problem is the query or the cap,
which are opposite fixes.

---

## Order

Run C first — it is free of chat spend and tells you whether the F3.1/F2.2 hints can be fixed by
query wording before you spend a run finding out they still miss. Then Run A (which can carry Run B's
adherence reading if you bypass the cache), to measure items 2 and 3 against a pinned extraction.

Archive each as Run-19, Run-20, … with its output, and score it against
[expected-results-benchmark.md](../../expected-results-benchmark.md) under
[scoring-rubric.md](../../scoring-rubric.md), reporting Caught / Partial / Missed with the movement
and what changed — one variable named per run.
