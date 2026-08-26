# Run analysis — Run 13

Case ABC-99, 2026-08-26 19:26, scored against
[expected-results-benchmark.md](../../expected-results-benchmark.md) under
[scoring-rubric.md](../../scoring-rubric.md), and compared with
[Run 12](../Run-12/run-analysis.md), [run-analysis.md](../../run-analysis.md) (Runs 7–9) and
[run-analysis-latest.md](../../run-analysis-latest.md) (Runs 10–11).

---

## The result

**`maxSearchResults: 0` costs eight findings.** Run 13 is the same model and the same plans as
Run 12, with the search limit unbounded — and it falls from 27 of 36 to 19.

| | Run 12 | **Run 13** |
| --- | --- | --- |
| Model | `claude-haiku-4-5` | `claude-haiku-4-5` |
| Plans | `20ac87292651` | `20ac87292651` |
| `maxSearchResults` | 8 | **0 (unbounded)** |
| Canonical model | `8a0120d90ea2` | **`a2bbc38bdf38`** |
| Hits retrieved | 3,612 | **130,993** |
| **Caught / Partial / Missed** | 27 / 5 / 4 | **19 / 11 / 6** |
| Recall | 75% | **53%** |
| Cost | £2.14 | £2.21 |

**36× the retrieval, for 7p, and eight fewer findings.**

Two things changed, not one — the canonical model was re-extracted between the runs — so the
attribution below rests on mechanism rather than on a controlled comparison. The mechanism is
strong and was predicted: see §3.

---

## 1. Provenance, and a failed run worth more than this one

**Run 13 is genuine**: 259s for 145,730 output tokens = 562 tok/s, no replay banner.

**Its extraction is a replay.** Twelve sections at 0.1s each, 307,232 tokens, £0.6453 — served
from cache. No section failed. The canonical model digest is nevertheless `a2bbc38bdf38` against
Run 12's `8a0120d90ea2`, so the merged model differs despite replayed fragments, and nothing here
explains why. **That is the second uncontrolled variable in this comparison.**

**The folder also contains a 613-byte checks log from 18:17 that is the most informative artefact
in it.** It stops after its own configuration header:

```
Chat intelliflo-claude-haiku-4-5 · temperature default, top-p default, seed not pinned
Embeddings … · top unbounded/search, unbounded/group (reserving 0/section, 0/category) …
```

**Everything set to zero at once, and the run did not survive its first check.** No findings, no
summary, no error recorded in the log itself. That is the fully-unbounded configuration
[optimal-configuration §1c](../../optimal-configuration.md) warns against, and it did not degrade —
it stopped.

There is **no checks log for the 19:26 run**, so prompts cannot be inspected. Everything below is
read from the rendered output, which is what the rubric scores anyway, but it means the pack
contents cannot be checked the way Run 12's were.

---

## 2. Scores

Verdicts under the rubric. **Confidence here is lower than for Run 12** — no checks log, and a
lighter read per finding. Treat the ±2 noise floor as ±3.

| Finding | R12 | **R13** | |
| --- | :-: | :-: | --- |
| F1.1 £300/wk vs £1,200 | C | **C** | via ÷4.33, the conversion its own guard warns against |
| F1.2 State Pension weekly vs monthly | C | **C** | |
| F1.3 Kim and Sea Cadets absent | C | **M** | **↓↓** and *asserts the opposite* |
| F1.6 residence for a tenant | M | **M** | |
| F1.8 pension totals | C | **C** | |
| F1.9 plans absent from the fact find | P | **M** | **↓** |
| F2.1 five horizons | C | **C** | |
| F2.2 CFL "<3 years" | M | **M** | |
| F3.1 file note says 6 | C | **C** | |
| F3.3 Investment Period 2 years | C | **C** | |
| F3.4 People's Pension risk 9 | P | **M** | **↓** risk 9 appears nowhere |
| F3.5 cautious answers, natural 4 | C | **P** | **↓** |
| F4.1 "No understanding / knowledge" | C | **C** | |
| F4.2 ATR narrative sentence | P | **P** | |
| F4.3 Balanced precondition | C | **C** | |
| F4.4 no comprehension tested | P | **P** | |
| F5.1 −£288 | C | **C** | |
| F5.2 affordability on unrecorded income | C | **C** | |
| F5.3 emergency fund spent | C | **P** | **↓** |
| F5.4 rented residence | M | **M** | |
| F6.1 ranking above the advice | C | **C** | |
| F6.2 boilerplate rationales | C | **P** | **↓** |
| F6.3 "Lower Costs" raises them | C | **P** | **↓** |
| F7.1 two charge tables | C | **C** | |
| F7.2 row on Zurich's fund | M | **C** | **↑** the only gain |
| F7.3 4.24% adds instead of subtracts | P | **P** | |
| F7.5 critical yield | C | **C** | |
| F8.1 £12,000 in the appendix | C | **P** | **↓** |
| F8.2 ranking never disclosed | C | **C** | |
| F9.1 switch reduces maturity | C | **P** | **↓** |
| F9.2 ranking above recommendation | C | **C** | |
| F9.3 Standard Life on cost | C | **P** | **↓** |
| F9.4 Zurich £128,000 | C | **C** | |
| F9.5 charges unreliable | C | **C** | |
| F10.1 FG21/1 capability | C | **P** | **↓** |
| F10.2 no screening | C | **C** | |
| **Caught** | 27 | **19** | |
| **Partial** | 5 | **11** | |
| **Missed** | 4 | **6** | |

**One gain, nine losses.** Six of the nine fell from Caught to Partial rather than to Missed — the
area is still reached and the argument no longer closes, which is the signature of a pack that
holds adjacent evidence rather than the right evidence.

---

## 3. Why unbounded search loses findings

`maxSearchResults: 0` resolves to `int.MaxValue` as the store's limit, so each of the 229 searches
asks for every match in the collection. Retrieval went from 3,612 hits to **130,993**.

**The pack did not grow.** `maxPassagesPerGroup` is still 24, so ranking now selects 24 passages
out of roughly 570 per search rather than out of 16. Every additional candidate is a chance to
displace a passage that was there for a reason, and the displacement is systematic rather than
random: the survivors are whatever embeds closest to the query text, which is not the same as
whatever answers the requirement.

**This was measured before, at a twentieth of the scale, and it pointed the same way.** Raising the
limit from 8 to 16 bought two extra passages across a whole run while **category diversity fell** —
every category except one. Run 13 is that experiment at 36× and the conclusion holds: a larger
candidate set makes the pack narrower in what it represents, not wider.

Three losses show the mechanism plainly.

**F3.4** — the People's Pension's risk rating of 9 appears **nowhere** in Run 13's output. It was
in every pack of every previous run, including Nova's. The plan still declares the path and the
guard still asks for it; the passage lost its place to something scoring better.

**F1.3 is the one that should worry a reader**, because it did not go quiet, it went wrong:

> Report states John receives '£20 per week' from Sea Cadets; [P8] corroborates this value exactly

The Fact Find records no Sea Cadets income at all — that absence is the finding. With the whole
collection in scope, the search found the figure in the report's own cashflow and read it as
corroboration. **Unbounded retrieval makes it easier for a report to corroborate itself**, which is
the precise failure the canonical-model/evidence split exists to prevent.

**F5.3** inverts the same way: G5.5 now reports that the emergency fund *"remains in accessible
liquid savings"*, where Run 12 quoted `Savings £6,000 LIQUIDATED 2ND` and called it spent.

### The one gain

**F7.2**, the transposed Standard Life charge row, is Caught for the first time on Haiku:

> The Standard Life row … shows existing charge 0.18% (£186.19). Evidence P1 lists Standard Life
> charge as 0.52% … The £186.19 calculation at 0.18% implies a fund value …

Not attributable to the search limit. The derived figure that names the wrong-plan consequence
landed in Run 12 too and was not used; this is the second run with it and the first where Haiku
acted on it. One sample either way.

---

## 4. What this settles

**`maxSearchResults` stays at 8, and the recommendation against 0 is now measured rather than
reasoned.** [optimal-configuration §1](../../optimal-configuration.md) argued from the 8→16 result
that unbounded search would fetch the index 229 times to no benefit. It is worse than no benefit:
minus eight findings, plus a false corroboration.

**The two zeros are not equivalent, and the configuration screen should probably say so.** The
captions read `(0 = all)` on both `Results/search` and `Passages / group` as though they were the
same kind of switch. `maxPassagesPerGroup: 0` widens what the assessor reads;
`maxSearchResults: 0` widens what ranking discards. One is a diagnostic worth running and the other
costs findings.

**And everything-at-zero does not run at all** — the 18:17 log is 613 bytes.

---

## 5. What to do next

1. **Return `maxSearchResults` to 8** before any further measurement. Nothing else in this run is
   interpretable while it is 0.
2. **Re-run at 8 on the current canonical model** to separate the two variables. If it returns to
   27, the search limit carries the whole loss; if it lands short, the re-extracted model carries
   part and the model digest change needs explaining.
3. **Then measure `maxPassagesPerGroup: 0`** on its own — still the untested lever, and still
   worth about 2× the prompt rather than 36× the retrieval.
4. **F3.4 remains unfixed** and is now invisible rather than merely mis-attributed. The retention
   link in G3.7 is unchanged from the Run 12 recommendation.

---

## 6. Limits

- **Two variables moved**: the search limit and the canonical model. The mechanism argues the
  search limit carries almost all of it, and the mechanism is not a measurement.
- **No checks log for this run**, so no pack could be inspected and no hint diagnostic read. The
  scores come from the rendered output alone.
- **Lower adjudication confidence than Run 12** — treat the count as ±3 rather than ±2. The
  *direction* is far outside that; the exact 19 is not.
- **Sampling still unpinned**, so this is one sample, as every run here has been.
