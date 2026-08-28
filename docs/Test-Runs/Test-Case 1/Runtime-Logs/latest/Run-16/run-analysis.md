# Run analysis — Run 16

Case ABC-99, 2026-08-26 21:57, scored against
[expected-results-benchmark.md](../../expected-results-benchmark.md) under
[scoring-rubric.md](../../scoring-rubric.md), and compared with
[Run 15](../Run-15/run-analysis-latest.md), [Run 14](../Run-14/run-analysis.md),
[Run 13](../Run-13/run-analysis.md), [Run 12](../Run-12/run-analysis.md),
[run-analysis.md](../../run-analysis.md) (Runs 7–9) and
[run-analysis-replays.md](../../run-analysis-replays.md) (Runs 10–11).

---

## The result

**Run 16 is the shipped configuration on Sonnet, and it is the first same-model pair this project
has ever had.** Run 15 and Run 16 differ in exactly one setting — `maxPassagesPerGroup` — and
**the cap changed one verdict out of thirty-six.**

| | Run 12 | Run 14 | Run 15 | **Run 16** |
| --- | --- | --- | --- | --- |
| Chat model | Haiku 4.5 | Haiku 4.5 | Sonnet 4.6 | **Sonnet 4.6** |
| `maxSearchResults` / `maxPassagesPerGroup` | 8 / 24 | 8 / **0** | 8 / **0** | **8 / 24** |
| Canonical model | `8a0120d90ea2` | `a2bbc38bdf38` | `cba52b288e54` | **`1b33706f1b81`** |
| Groups run | 85 | 85 | 77 | **77** |
| Retrieval | 229 / 3,612 | 229 / 3,612 | 203 / 3,196 | **203 / 3,196** |
| Pack: median / mean / max | 24 / 24 / 24 | 24 / 27 / 62 | 23 / 26.1 / 62 | **23 / 20.7 / 24** |
| **Caught / Partial / Missed** | **27 / 5 / 4** | 23 / 9 / 4 | 25 / 7 / 4 | **25 / 6 / 5** |
| Recall | **75%** | 64% | 69% | **69%** |
| Chat cost | £2.13 | £2.34 | £7.12 | **£6.60** |
| Wall clock | 325s | 145s | 552s | **551s** |

**Same 25 Caught. One finding moved — F1.9, from Partial to Missed.**

And **CHK-005 did not run again**, for the same reason and — as §2 shows — from literally the same
cached bytes.

---

## 1. The cap experiment, finally controlled

Every previous cap comparison carried a second variable. Run 13 changed the search limit *and* the
canonical model. Run 14 changed the pack cap *and* the canonical model against Run 12. **Run 15 and
Run 16 share a model, share a plan digest, share a retrieval count to the passage, and share the
same disabled check.**

What the cap actually did:

| | Run 15 (`0`) | **Run 16 (`24`)** |
| --- | --- | --- |
| Pack mean | 26.1 | **20.7** |
| Pack max | 62 | **24** |
| Passages cited per group (median) | 8.0 | **9.0** |
| **Pack utilisation** | 33% | **43%** |
| Truncated passages | 1,070 | **846** |
| Untraceable quotes | 85 | **78** |
| Output tokens | 159,123 | 160,301 |
| **Caught** | **25** | **25** |

**Capping the pack removed a mean of 5.4 passages per group and cost nothing.** It also produced a
slightly *better*-behaved run on every secondary measure: more of the pack read, fewer quotes that
could not be traced, marginally cheaper.

This is the third independent result pointing the same way, and the first one that isolates it:

- **Run 13** — unbounding *search* cost eight findings.
- **Run 14** — unbounding the *pack* cost four, against a different canonical model.
- **Run 16** — capping the pack back to 24, same model, same everything: **zero.**

**`maxPassagesPerGroup: 24` is now measured, not reasoned.** The caveat in
[optimal-configuration §1a](../../optimal-configuration.md) can be retired in both directions for
the first time: 24 is not too small, and unbounded is not better. Whether **12** would do as well
is still the open question, and it is now the cheap one to answer — the pack mean is already 20.7,
so a cap of 12 would bite on roughly half the groups.

---

## 2. CHK-005 was not skipped twice. It was skipped once, and served twice.

Run 15's analysis attributed the false `hasCapitalContributionsOrWithdrawals: false` to a
partial-cache boundary in the extraction, and asked for a clean re-run to confirm. Run 16 is not
that confirmation, and the reason matters more than the trigger.

**Run 16's twelve extraction requests are byte-identical to Run 15's, and its whole extraction
completed in two seconds** — timestamps `21:57:46` to `21:57:48`, twelve gateway responses.

| Section | R15 | R16 | |
| --- | --- | --- | --- |
| 1 Case and parties … 5 Existing arrangements | | | identical |
| **6 Needs analysis and research** | 42,725 ch | 40,891 ch | **−1,834** |
| 7 Solution, 8 Recommendations | | | identical |
| **9 Costs and charges** | 46,037 ch | 42,940 ch | **−3,097** |
| **10 Replacement analysis** | 65,734 ch | 74,418 ch | **+8,684** |
| **11 Disclosures** | 47,646 ch | 52,386 ch | **+4,740** |
| **12 Triggers and extraction report** | | | **identical** |

**Identical prompts, four different answers, both served from cache in under a second each.**

Two things follow, and they point in opposite directions.

**The trigger is not independently reproduced.** Section 12 is byte-identical between the two runs.
CHK-005 was disabled by one bad extraction, served twice. The clean re-run Run 15 asked for has
still not happened, and **cannot happen until the extraction cache is bypassed** — which is now the
blocking step for every measurement that depends on a canonical model.

**But something larger is now settled.** The canonical-model digest has changed unexplained since
Run 13, where the analysis recorded *"the merged model differs despite replayed fragments, and
nothing here explains why. That is the second uncontrolled variable in this comparison."* It is
explained now, and it is worse than a digest quirk:

> **The same extraction request returns different content on different runs, from cache, with no
> indication in any log that anything varied.**

Run 13's sections differ from Run 15's in **all twelve** positions. Run 15's differ from Run 16's
in four. Every cross-run comparison in this series that treats the canonical model as a constant
has been resting on an assumption that is false.

**What it does not disturb:** both runs resolve **197 canonical paths and leave 38 absent**, and
retrieval is identical to the passage. The variation is in prose the checks do not index. That is
luck rather than design, and it is why the effect has gone unnoticed for four runs.

---

## 3. Scores

Verdicts under the rubric, in benchmark order. **No verdict rests on R8.**

| Finding | R12 | R14 | R15 | **R16** | Where it landed |
| --- | :-: | :-: | :-: | :-: | --- |
| F1.1 £300/wk vs £1,200 | C | C | C | **C** | G1.4 — *"£1,200/month … not reconcilable with £300/week"* |
| F1.2 State Pension weekly vs monthly | C | C | C | **C** | G1.10 — *"This frequency discrepancy is material"* |
| F1.3 Kim and Sea Cadets absent | C | C | C | **C** | G1.4 — *"£1,430 … corresponds to £1,200 + £230 only"* |
| F1.6 residence for a tenant | M | M | M | **M** | "tenant" appears once, in the header |
| F1.8 pension totals | C | C | C | **C** | G1.8, G1.7 |
| F1.9 plans absent from the fact find | P | M | P | **M** | **↓ the run's only loss — §4** |
| F2.1 five horizons | C | C | C | **C** | G2.2 — 71, 66, 2yr, 75, 65, 86/88, 104/100 |
| F2.2 CFL "<3 years" | M | M | P | **P** | G3.4 reaches it from the objectives array, not the CFL |
| F3.1 file note says 6 | C | C | C | **C** | G3.6 |
| F3.3 Investment Period 2 years | C | C | C | **C** | G3.4 — *"directly contradictory"* |
| F3.4 People's Pension risk 9 | P | P | P | **P** | reached **twice** and withdrawn twice — §5 |
| F3.5 cautious answers, natural 4 | C | P | C | **C** | G3.10 |
| F4.1 "No understanding / knowledge" | C | C | C | **C** | G4.1 — *"'Limited' overstates what the questionnaire captured"* |
| F4.2 ATR narrative sentence | P | P | P | **P** | |
| F4.3 Balanced precondition | C | C | C | **C** | G4.1 discrepancy 2 |
| F4.4 no comprehension tested | P | P | C | **C** | G4.5 |
| F5.1 −£288 | C | C | P | **P** | halves in G2.7, G10.5 and G3.2; never joined |
| F5.2 affordability | C | C | M | **M** | CHK-005 not run |
| F5.3 emergency fund spent | C | P | M | **M** | `LIQUIDATED` appears nowhere |
| F5.4 rented residence | M | M | M | **M** | CHK-005 not run |
| F6.1 ranking above the advice | C | C | C | **C** | G9.4 — *"ranks 5th of 9 … Zurich 1st £128k"* |
| F6.2 boilerplate rationales | C | C | C | **C** | G6.1 — *"repeated verbatim across the Aviva, Zurich and Standard Life recommendations"* |
| F6.3 "Lower Costs" raises them | C | P | C | **C** | G6.5, G7.5 |
| F7.1 two charge tables | C | C | P | **P** | *"0.18% / 0.52% blended"* — listed and explained away |
| F7.2 row on Zurich's fund | M | C | C | **C** | G1.8, G1.7 |
| F7.3 4.24% adds instead of subtracts | P | P | P | **P** | *"the two figures measure different things"* |
| F7.5 critical yield | C | C | C | **C** | G7.5, G7.10 |
| F8.1 £12,000 in the appendix | C | C | C | **C** | G8.5 — *"appear only in the Appendix table"* |
| F8.2 ranking never disclosed | C | C | C | **C** | G9.4, G9.8 |
| F9.1 switch reduces maturity | C | P | C | **C** | G6.4 — *"at all three growth rates"* |
| F9.2 ranking above recommendation | C | C | C | **C** | G9.1 |
| F9.3 Standard Life on cost | C | P | C | **C** | CHK-009 header |
| F9.4 Zurich £128,000 | C | C | C | **C** | G9.9 |
| F9.5 charges unreliable | C | C | C | **C** | G6.2, G6.5 |
| F10.1 FG21/1 capability | C | C | C | **C** | G10.5 |
| F10.2 no screening | C | C | C | **C** | G10.5 — age 70 and the −£288 deficit as FG21/1 drivers |
| **Caught** | **27** | 23 | 25 | **25** | |
| **Partial** | 5 | 9 | 7 | **6** | |
| **Missed** | 4 | 4 | 4 | **5** | |

---

## 4. The one loss, and what it says about the cap

**F1.9 is the only verdict that moved, and it moved because a passage was evicted.**

Run 15's G1.7 reached the area:

> Fact find [P1] records a pension value of £116,555.45 as at 23/07/2026 under Aviva Platform policy
> AV2936864-001. **This policy number and value do not correspond to any of the five existing
> arrangements listed in the suitability report.**

Run 16's G1.7 covers the same requirement, at length, and the Fact Find's pension entry is not in
it at all — no `AV2936864`, no `£116,555.45`, anywhere in the output. **With the pack capped at 24,
the passage lost its place.**

This is the honest cost of the cap, and it should be stated at full weight: **one finding, at
Partial rather than Caught, in exchange for a mean of 5.4 fewer passages per group and a
utilisation rise from 33% to 43%.** It does not overturn §1 — a single Partial-to-Missed move is
half the noise floor — but it is the mechanism the cap was always expected to have, observed for
the first time on a controlled pair.

---

## 5. F3.4 was reached twice and given away twice

The retention finding has never been Caught in this series. Run 16 came closer than any run so far
and then talked itself out of it, in two separate groups.

**G6.5**, having named it exactly right:

> People's Pension is invested in Global Investments (up to 85% shares) per P13, **which is above
> risk rating 5**; the retain rationale does not address the fund mismatch within the retained plan,
> only the employer contribution benefit.

…then, four paragraphs later:

> the employer contribution point is a legitimate standalone reason to retain even where the fund
> may not align with the agreed risk rating — **the fund mismatch within the retained plan is noted
> but does not undermine the outcome recommendation for this check's purposes.**

**G3.7** disposed of it by guard:

> The existing arrangements' risk ratings are all flagged as provider-scale and not comparable to
> the client scale, which is consistent with the guard on incomparable scales. **No alignment claim
> is made for the existing arrangements, so no finding arises from the incomparability itself.**

That second sentence is false on the file — the report does claim the retained fund *"aligns with
your risk appetite"* — but no group had that assertion and the fund mismatch in the same pack.

**The retention link recommended after Runs 12, 13, 14 and 15 is still unapplied, and Run 16 shows
precisely why it is needed.** Both halves reached an assessor. The incomparable-scales guard, which
exists to stop provider ratings being compared naively, is being used to dismiss a genuine
alignment claim. **The guard needs the bound the assessor prompt already carries for the
document-difference guard**: an incomparable scale explains why two *ratings* cannot be compared;
it does not excuse a claim the report makes for itself.

---

## 6. Two things that got better, and one that did not

### The self-corroboration did not recur

Run 15's headline defect was G3.2 writing *"the income and expenditure figures are corroborated by
[P11]"* where [P11] was the suitability report. **Run 16's G3.2, on the same requirement with the
same passage, gets it right:**

> The suitability report narrative in [P11] **contains the same income/expenditure/savings detail as
> the canonical model.**

Stated as restatement, not corroboration. **The structural hazard is unchanged — 171 of 667 cited
passages (26%) still come from the report itself, flat across Runs 12, 14, 15 and 16 — but whether
it fires is a coin toss.** That makes it worse to leave in place, not better: an intermittent false
corroboration is harder to catch in review than a consistent one.

Recommendation 2 from the Run 15 analysis stands unchanged: **category `[I]` should not be
reachable by a group's evidence searches.**

### F1.3 is the cleanest it has ever been

> The fact find [P10] income table **does not show any Sea Cadets or volunteer income line**; total
> net monthly income is recorded as £1,430 which corresponds to £1,200 employment + £230 state
> pension only, with no volunteer income included.

Both halves and the arithmetic that closes them, in one sentence. Compare Run 13, which read the
same figure out of the report's own cashflow and called it corroboration.

### The client is still being misidentified

G1.10 states *"Fact find [P2] shows **Kim's** basic income as £1,200/month net"* — that is John's
employment line — and then *"Fact find [P2] records **Alan's** employment income as £300/month"*,
which is neither a name nor a figure the Fact Find records in that form. Run 15 made the same
Kim/£1,200 substitution.

**It does not change any verdict here**, because the discrepancy against the report's £1,100 exists
either way. It is worth recording because F1.5 — the Fact Find's blank First and Last Name fields
with Middle Name "Alan" — is an unscored benchmark finding, and the assessor is tripping over the
data defect without recognising it.

---

## 7. What to do next

1. **Bypass the extraction cache and re-extract.** This is now the blocking step, not one of
   several. Until it happens, `hasCapitalContributionsOrWithdrawals` cannot be confirmed as a real
   extraction defect rather than a cached one, and no canonical model in this series can be treated
   as reproducible. §2.
2. **Report extraction variance.** Identical requests returned four different sections and nothing
   said so. A run should print when its canonical model differs from the previous one for the same
   case, and by how much.
3. **Make a disabled check loud** — unchanged from Run 15. CHK-005 has now printed under
   *CHECKS CLEARED* twice.
4. **Take category `[I]` out of the evidence corpus** — unchanged from Run 15, and strengthened:
   §6 shows the failure is intermittent rather than reliable.
5. **Bound the incomparable-scales guard** in the assessor prompt, the way the document-difference
   guard is already bounded. §5.
6. **`maxPassagesPerGroup: 24` is settled. Test 12.** The pack mean is 20.7, so a cap of 12 is the
   first setting since the search limit that would actually bite, and §1 gives it a clean baseline
   to move against.
7. **F1.9 needs a section hint**, on the Fact Find's Existing Pension Provision table. It is the
   passage the cap evicted, and it is the second consecutive analysis to name it.

---

## 8. Limits

- **The cap comparison in §1 is the strongest single-variable result this project has**, and it is
  still one sample against one sample, with sampling unpinned on both.
- **Neither run is a clean 36-finding measurement.** Both had CHK-005 disabled by the same cached
  extraction, so the 25 is out of 32 reachable, not 36.
- **The digest instability in §2 is documented behaviour, not a diagnosed mechanism.** Identical
  requests, different responses, sub-second latency — that is what the logs show. Whether it is the
  gateway serving multiple cached variants, a retry writing a second entry, or something else, is
  not established here and cannot be from these artefacts.
- **Adjudication is one pass**, ±2 findings. The Run 15 → Run 16 delta of one is well inside it,
  which is the point of §1 rather than a caveat on it.
- **F3.4 and F5.1 are the two judgement calls.** Both scored Partial on the rubric's
  reached-and-withdrawn and unjoined-halves bullets respectively. A reader who scores F3.4 Caught on
  R6 reaches 26 / 5 / 5.
