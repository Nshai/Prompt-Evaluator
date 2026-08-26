# Run analysis — Run 12

Case ABC-99, 2026-08-26 17:31, scored against
[expected-results-benchmark.md](../../expected-results-benchmark.md) under
[scoring-rubric.md](../../scoring-rubric.md), and compared with
[run-analysis.md](../../run-analysis.md) (Runs 7–9) and
[run-analysis-replays.md](../../run-analysis-replays.md) (Runs 10–11).

---

## The result

**Haiku on the changed plans reaches 27 of 36. The same model on the old plans reached 20.**

| | Run 7 | **Run 12** | Run 8 |
| --- | --- | --- | --- |
| Model | `claude-haiku-4-5` | **`claude-haiku-4-5`** | `claude-sonnet-4-6` |
| Plans | `fb7a2d0ab61d` | **`20ac87292651`** | `fb7a2d0ab61d` |
| **Caught / Partial / Missed** | 20 / 10 / 6 | **27 / 5 / 4** | 28 / 5 / 3 |
| Recall | 56% | **75%** | 78% |
| Cost | £2.09 | £2.14 | £6.44 |
| **Cost per finding caught** | £0.105 | **£0.079** | £0.230 |

**+7 findings for 4p.** This is the cleanest comparison the project has: same model, same
canonical model (`8a0120d90ea2`), same settings, same embeddings. Only the plans and the assessor
system prompt differ, and both changed together.

It also nearly closes the model gap. Haiku on the new plans is **within one finding of Sonnet on
the old ones, at a third of the cost**. The eight-finding advantage Sonnet held was not eight
findings of model capability; most of it was retrieval and prompt work that any model could use.

---

## 1. Provenance

Genuine: **325s for 151,187 output tokens = 466 tok/s**, against the 2,000 tok/s replay threshold.
No banner.

Held constant against Run 7: the canonical model (`8a0120d90ea2`, extracted 10:03), `top 8/search,
24/group (reserving 1/section, 1/category)`, `extraction cap 32,000`, the embedding model, and the
assessor model itself.

Changed: the plan digest, verified to match the current working tree exactly, and the assessor
system prompt — `DERIVED FIGURES` appears in all **85** calls.

Retrieval moved slightly with the nine added queries: **229 searches and 3,612 hits**, against 223
and 3,520. Canonical paths resolved and absent are unchanged at 224 and 31.

**Sampling is still unpinned** — `temperature default, top-p default, seed not pinned`. Every
figure here is one sample. The 7-finding gap is far outside the ±2 noise floor; nothing smaller
should be read.

---

## 2. Scores

| Finding | R7 | **R12** | Movement |
| --- | :-: | :-: | --- |
| F1.1 £300/wk vs £1,200 | C | **C** | |
| F1.2 State Pension weekly vs monthly | C | **C** | |
| F1.3 Kim and Sea Cadets absent | C | **C** | |
| F1.6 residence modelled for a tenant | M | **M** | |
| F1.8 pension totals | C | **C** | |
| F1.9 three plans absent from the fact find | M | **P** | **↑** membership guard fires |
| F2.1 five horizons | C | **C** | |
| F2.2 CFL "<3 years" / "Short Term" | M | **M** | |
| F3.1 file note says 6 | C | **C** | |
| F3.3 Investment Period 2 years | C | **C** | |
| F3.4 People's Pension risk 9 | P | **P** | guard fixed, wrong plan |
| F3.5 cautious answers, natural 4 | C | **C** | |
| F4.1 "No understanding / knowledge" | C | **C** | |
| F4.2 ATR narrative sentence | P | **P** | |
| F4.3 Balanced precondition | C | **C** | |
| F4.4 no comprehension tested | P | **P** | |
| F5.1 −£288 vs "comfortably meets" | C | **C** | |
| F5.2 affordability on unrecorded income | C | **C** | |
| F5.3 emergency fund scheduled to be spent | P | **C** | **↑** |
| F5.4 cashflow includes a rented residence | M | **M** | |
| F6.1 research ranks doing nothing higher | C | **C** | |
| F6.2 boilerplate rationales | C | **C** | |
| F6.3 "Lower Costs" on a switch that raises them | P | **C** | **↑** |
| F7.1 two charge tables contradict | P | **C** | **↑** |
| F7.2 Standard Life row on Zurich's fund | M | **M** | |
| F7.3 4.24% adds instead of subtracts | C | **P** | **↓ regression** |
| F7.5 critical yield on past performance | C | **C** | |
| F8.1 £12,000 only in the appendix | P | **C** | **↑** |
| F8.2 ranking never disclosed | M | **C** | **↑↑** |
| F9.1 switch reduces maturity | P | **C** | **↑** |
| F9.2 ranking above the recommendation | C | **C** | |
| F9.3 Standard Life sold on cost | P | **C** | **↑** |
| F9.4 Zurich £128,000 vs £125,000 | C | **C** | |
| F9.5 charge figures unreliable | P | **C** | **↑** |
| F10.1 FG21/1 capability driver | C | **C** | |
| F10.2 no screening recorded | C | **C** | |
| **Caught** | 20 | **27** | |
| **Partial** | 10 | **5** | |
| **Missed** | 6 | **4** | |

**Eight findings gained, one lost.** No finding was scored under R8 in this run.

---

## 3. What moved, and why

### Two of the six dead section hints now fire

The run's own diagnostics: **"Section hints matching nothing: 4"**, down from six —
`Residency Status` and `LIQUIDATED` both reported as reaching a passage for the first time.
**Only `LIQUIDATED` actually did**; see §5 for the diagnostic defect behind the other. The four
`ranked by Maturity Value` hints are still dead, unchanged, because nothing was done to them.

The change was writing query text to resemble the **chunk** rather than the question. The
liquidation order lives in a scenario asset tree of headings and two-column tables, and asking
*"order in which savings and investments are liquidated in the cashflow scenario"* never retrieved
it; asking in the tree's own vocabulary — surplus account, cash account, expected return — did.

### F5.3, and the guard that was written with it

G5.5 now quotes the tree directly and reaches the finding:

> The proposed scenario shows Liquid Assets declining from £6,000 … indicating the emergency fund
> is being spent down, yet the report d[oes not] acknowledge this depletion

and cites the guard added beside the query by name. Query and guard landing together is the
pattern worth repeating: retrieval put the page in the pack, the guard told the assessor what it
meant.

### Five of the eight gains are the derived-figure change, not the plans

F6.3, F7.1, F8.1, F9.1, F9.3 and F9.5 all turn on charge or projection arithmetic, and none of
them needed new retrieval. The evidence was in every pack already — it was in every pack in Run 7
too. What changed is that the derived figure now states its own consequence, and the system prompt
names the derived figures as a third side of the pack rather than leaving them unmentioned.

F9.3 is the clearest:

> Report claims 'lower costs' as benefit but Standard Life arrangement shows +0.18% cost increase
> (higher cost), not lower

That is the benchmark's finding, from a model that had the same numbers and did not draw the
conclusion five hours earlier.

### F8.2 went from Missed to Caught

The disclosure failure — the report never saying the research ranked doing nothing above the
advice — was reached by no model in any previous run. CHK-009 G9.1 now states the ranking with
every figure, and CHK-008 G8.7 states the non-disclosure against it.

---

## 4. The regression

**F7.3 fell from Caught to Partial, and it is worth more attention than one finding.**

Run 7 wrote:

> The report presents contradictory cost comparisons: a 4.24% cost increase (including adviser
> fees) versus a 0.24% cost reduction (recurring charges only). These cannot both be true in the
> same comparison framework.

Run 12 handles both figures — and separately, against implied fund values:

> Report states Aviva plan requires 4.24% outperformance … calculated against an implied fund value
> of £110,188.68 which does not match any stated arrangement
>
> Report states ongoing annual recurring charges are 0.24% less expensive, but derived fund values
> used in that calculation (£110,184.09) do not align with…

**The new derived-figure emphasis pulled the assessor toward the arithmetic it was handed and away
from the contradiction between two sentences on one page.** Both bullets are true and neither is
the finding. This is the predictable cost of telling a model that pre-computed facts are
authoritative, and it should be watched: five findings gained by that change, one lost to it.

---

## 5. The fix that half-worked, and the one that did not

### F3.4 — the guard is fixed and landed on the wrong plan

G3.7 no longer discards every retained arrangement as incomparable. It now says:

> Existing arrangement Zurich … maps to a provider risk scale rating of 7, which sits above the
> agreed client rating of 5. No explicit statement that this retained arrange[ment]…

The mechanism works — a plan rated above the agreed profile is now a finding rather than a scale
mismatch to be excused. But **Zurich is being switched away, not retained**, and the benchmark's
finding is the People's Pension at risk 9, which *is* retained. Right reasoning, wrong arrangement.
Still Partial, and the remaining gap is that nothing connects "retained" to "rated above profile".

### F1.6 and F5.4 — still retrieval, and the diagnostic said otherwise

**Corrected after publication.** This section first read that the residency passage now reaches the
pack, making F1.6 a verification failure. That was wrong, and the error came from trusting this
run's own diagnostic.

The run reported *"Section hints matching nothing: 4"*, down from six, with `Residency Status` no
longer among them. Reading the prompts directly:

- the only `Tenant` in G1.5, G1.11 or G5.2 is `Tenant: 99`, the case id in the prompt header;
- `Residency Status` appears in G1.11's prompt **only inside the guard text written for it**;
- G1.5's pack holds five category-B fact find passages and none carries the row.

**The fact find's `Residency Status | Tenant - private` reached no assessor.** F1.6 and F5.4 remain
a retrieval failure — the same one, now misdiagnosed twice.

The cause was in the diagnostic. `UnmatchedSections` compared hints against the de-duplicated
*candidates*, before ranking and before the cap, so a hint whose passage was retrieved and then
evicted was reported as having matched. It has been changed to measure against the pack and to
separate the two cases, which need opposite answers — reword the hint, or widen the pack.

A test had encoded the old behaviour deliberately, arguing that reporting an eviction would send a
reader to check wording that was already correct. True, and it left the reader with no report at
all. That test is now inverted.

**The other five hints were verified the same way and are genuinely in their packs**, including
`LIQUIDATED`: F5.3's catch below is real. Only this one was mis-reported.

Three guards for this finding already existed during the run — G1.11's *"a cashflow that models
property for a client the fact find records as renting is using an asset the client does not
have"*, and its counterparts in G1.5 and G5.2. They ran against evidence that was never there.

---

## 6. The four still missed

| | Why |
| --- | --- |
| **F1.6 / F5.4** | Both halves retrieved, never joined. A verification failure, not a retrieval one — see §5. |
| **F2.2** | The CFL questionnaire's "Less than 3 years" and the fact find's "Short Term" appear nowhere. No query asks for either, and the horizon findings are all reached through the risk report's Investment Period instead. |
| **F7.2** | `£186.19` appears in no bullet. The derived figure naming the transposition is in every prompt and only Sonnet has ever used it — the strengthened wording did not bring Haiku with it. |

F1.9 moved to Partial: the membership guard is quoted and reasoned from, but reaches the inverse of
the benchmark — a fact-find plan absent from the report rather than three report plans absent from
the fact find.

---

## 7. What to do next

1. **Fix F1.6/F5.4 in retrieval, and verify it with the dry run rather than the run's own hint
   report.** The guards it needs already exist and ran; what is missing is the passage. `Q1.5.5`
   retrieves something from the fact find and not the address table — the next attempt should be
   checked against the pack before another assessment run is spent on it.
2. **Watch F7.3.** One regression against five gains is a good trade, but the mechanism —
   derived arithmetic crowding out a same-page contradiction — will recur wherever both exist.
3. **Give F3.4 the missing link:** the concern is a plan *retained* while rated above the agreed
   profile. G3.7 now finds the rating and not the retention.
4. **Score Sonnet on the new plans.** Run 8 was 28/36 on the old ones. If the same +7 carries, it
   lands near 33–35; if it does not, that tells you these fixes substitute for model quality rather
   than adding to it — which the Haiku result already hints at.
5. **Pin sampling.** Still not done, and every number here remains one sample.

---

## 8. Limits

- **One adjudicator, one pass**, ±2 findings. The 7-finding gain survives that; the F7.3 regression
  is a single finding and does not.
- **Two changes moved together.** The plans and the system prompt changed in the same commit range,
  so no gain here attributes cleanly to one. §3 separates them by *mechanism* — five gains needed
  no new retrieval and two needed nothing else — but that is reasoning, not measurement.
- **Citation trust got worse**: 136 unverifiable quotes against Run 7's 116, on a run with 6% more
  retrieval. Still around 38% of all quotes, still unmoved by anything tried, still gap A2.
- **Precision remains unmeasurable.** All ten checks returned Potential Concern, as always. Run 12
  produces 381 discrepancy bullets and some are wrong; nothing counts them. See
  [control-case-spec.md](../../control-case-spec.md).
