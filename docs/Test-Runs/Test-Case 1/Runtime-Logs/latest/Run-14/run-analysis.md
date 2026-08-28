# Run analysis — Run 14

Case ABC-99, 2026-08-26 19:49, scored against
[expected-results-benchmark.md](../../expected-results-benchmark.md) under
[scoring-rubric.md](../../scoring-rubric.md), and compared with
[Run 13](../Run-13/run-analysis.md), [Run 12](../Run-12/run-analysis.md),
[run-analysis.md](../../run-analysis.md) and [run-analysis-replays.md](../../run-analysis-replays.md).

---

## The result

**`maxPassagesPerGroup: 0` is not the lever it looked like.** Unbounding the pack raises the mean
pack from 24 to 27 passages, costs 13% more prompt, and lands at **23 of 36**.

| | Run 12 | Run 13 | **Run 14** |
| --- | --- | --- | --- |
| `maxSearchResults` | 8 | **0** | 8 |
| `maxPassagesPerGroup` | 24 | 24 | **0** |
| Canonical model | `8a0120d90ea2` | `a2bbc38bdf38` | `a2bbc38bdf38` |
| Hits retrieved | 3,612 | 130,993 | 3,612 |
| Pack: median / mean / max | 24 / 24 / 24 | 24 / – / 24 | **24 / 27 / 62** |
| **Caught / Partial / Missed** | **27 / 5 / 4** | 19 / 11 / 6 | **23 / 9 / 4** |
| Recall | **75%** | 53% | **64%** |
| Cost | £2.14 | £2.21 | £2.35 |

**The cap was barely binding.** Half the groups never had 24 candidates to begin with; unbounding
it added a mean of three passages and a maximum of 62 in one group. For that, prompts grew from a
median 17,044 tokens to 19,344 — and **utilisation fell from 25% to 21%**.

**Both experiments are worse than the shipped configuration**, and both share a canonical model
Run 12 did not, so the comparison is not clean. What is clean: on the *same* canonical model,
`8/unbounded` (23) beats `unbounded/24` (19) — the passage cap is a far less damaging thing to
open than the search limit.

---

## 1. The finding that matters most

**The fact find's residency row reached an assessor for the first time, and no check used it.**

With the cap off, nothing can be evicted, and G1.5's pack grew to 44 passages:

```
G1.5   pack=44   'Residency Status' in prompt: True   'Tenant - private': True
```

The word "tenant" appears **once** in the whole output — in the report header, `case ABC-99
(tenant 99)`. F1.6 and F5.4 are still Missed.

This settles a question misdiagnosed three times across three analyses. The evidence half is now
demonstrably in front of the model; the report's half (*"the forecast incorporates the assets of
your main residence"*) has been there for several runs; **three guards written for exactly this
contradiction were in the prompt** — G1.11's *"a cashflow that models property for a client the
fact find records as renting is using an asset the client does not have"*, and its counterparts in
G1.5 and G5.2. Nothing joined them.

**F1.6 is a verification failure.** Not retrieval, not the cap, not the render. Every fix aimed at
getting the row in front of the model has now worked, and the finding is not made.

That also means the `reportSays`/`fileSays` reconciliation rule added to the assessor prompt does
not fire here, on the one finding it was most clearly written for.

---

## 2. Scores

| Finding | R12 | R13 | **R14** | |
| --- | :-: | :-: | :-: | --- |
| F1.1 £300/wk vs £1,200 | C | C | **C** | |
| F1.2 State Pension weekly vs monthly | C | C | **C** | |
| F1.3 Kim and Sea Cadets absent | C | M | **C** | recovered from R13 |
| F1.6 residence for a tenant | M | M | **M** | evidence present, see §1 |
| F1.8 pension totals | C | C | **C** | |
| F1.9 plans absent from the fact find | P | M | **M** | |
| F2.1 five horizons | C | C | **C** | |
| F2.2 CFL "<3 years" | M | M | **M** | |
| F3.1 file note says 6 | C | C | **C** | |
| F3.3 Investment Period 2 years | C | C | **C** | |
| F3.4 People's Pension risk 9 | P | M | **P** | right plan named at last |
| F3.5 cautious answers, natural 4 | C | P | **P** | **↓** vs R12 |
| F4.1 "No understanding / knowledge" | C | C | **C** | |
| F4.2 ATR narrative sentence | P | P | **P** | |
| F4.3 Balanced precondition | C | C | **C** | |
| F4.4 no comprehension tested | P | P | **P** | |
| F5.1 −£288 | C | C | **C** | |
| F5.2 affordability | C | C | **C** | |
| F5.3 emergency fund spent | C | P | **P** | **↓** vs R12 |
| F5.4 rented residence | M | M | **M** | |
| F6.1 ranking above the advice | C | C | **C** | |
| F6.2 boilerplate rationales | C | P | **C** | |
| F6.3 "Lower Costs" raises them | C | P | **P** | **↓** vs R12 |
| F7.1 two charge tables | C | C | **C** | |
| F7.2 row on Zurich's fund | M | C | **C** | **↑** vs R12 |
| F7.3 4.24% adds instead of subtracts | P | P | **P** | |
| F7.5 critical yield | C | C | **C** | |
| F8.1 £12,000 in the appendix | C | P | **C** | |
| F8.2 ranking never disclosed | C | C | **C** | |
| F9.1 switch reduces maturity | C | P | **P** | **↓** vs R12 |
| F9.2 ranking above recommendation | C | C | **C** | |
| F9.3 Standard Life on cost | C | P | **P** | **↓** vs R12 |
| F9.4 Zurich £128,000 | C | C | **C** | |
| F9.5 charges unreliable | C | C | **C** | |
| F10.1 FG21/1 capability | C | P | **C** | |
| F10.2 no screening | C | C | **C** | |
| **Caught** | **27** | 19 | **23** | |
| **Partial** | 5 | 11 | **9** | |
| **Missed** | 4 | 6 | **4** | |

One gain against Run 12 (F7.2), five losses (F3.5, F5.3, F6.3, F9.1, F9.3), all Caught → Partial.

---

## 3. Two results worth keeping

### F7.2 is now caught on Haiku, twice running

> Report states EA5 (Standard Life) existing charge 0.18% with amount £186.19; this £186.19 at
> 0.18% implies fund value £103,438.89, **which is Zurich's value, not Standard Life's**

That is the benchmark's finding verbatim, and until Run 13 only Sonnet had ever reached it. The
derived figure that states the wrong-plan consequence shipped before Run 12 and went unused there;
it has now been used in both runs since. **Two samples, both positive** — the strongest evidence in
this project that stating a pre-computed fact's *consequence*, not just the fact, changes what a
smaller model does with it.

### F3.4 finally names the right arrangement

> The existing arrangements carry divergent risk ratings on incomparable provider scales:
> **People's Pension Risk Level 9**, Scottish Widows Risk Level 4, Aviva Risk Level 2 …

Run 12 named Zurich, which is being switched away. This names the People's Pension, which is
retained — the benchmark's plan. It is still Partial, because the sentence stops at *"incomparable
provider scales"* and never reaches *retained against an agreed rating of 5 while the report claims
that fund aligns with the client's risk appetite*.

**The guard rewrite got the plan right and the conclusion is still one step away.** The remaining
gap is the retention link, unchanged from the Run 12 recommendation and still unapplied.

---

## 4. What the two cap experiments settle

**Neither cap is where the recall is.**

- `maxSearchResults: 0` → 36× the retrieval, **−8 findings**, and a false corroboration
  (Run 13 §3).
- `maxPassagesPerGroup: 0` → +3 passages per group on average, **−4 findings**, utilisation down
  from 25% to 21%.

Both point the same way, and it is the way the utilisation figure has pointed since it was first
measured: **the assessor already ignores three-quarters of what it is given.** Adding more of the
same makes the pack a worse sample of what matters, not a better one. That is an argument for a
relevance floor — gap R1, open since the beginning — and against any further widening.

**`maxPassagesPerGroup: 24` should stay**, and the "reasoned, not measured" caveat on it in
[optimal-configuration §1a](../../optimal-configuration.md) can be retired in one direction: 24 is
not too small. Whether 12 would do as well is still untested.

---

## 5. What to do next

1. **Run `8 / 24` on canonical model `a2bbc38bdf38`.** Three runs now share that model and none
   uses the shipped configuration, so every comparison to Run 12's 27 carries an unexplained
   variable. This is one run and it removes it.
2. **Stop widening and start filtering.** A relevance floor is the only untried direction the
   evidence supports.
3. **F1.6 is now a prompt problem** — see §1. The reconciliation rule did not fire on the finding
   it was written for, and that is worth understanding before more rules are added.
4. **F3.4 needs the retention link**, still: the concern is a plan *retained* while rated above the
   agreed profile.

---

## 6. Limits

- **The canonical model differs from Run 12** and its digest changed despite a replayed
  extraction, which nothing has explained. Part of the five losses against Run 12 may belong to it
  rather than to the cap.
- **Sampling still unpinned.** One sample, as always.
- **Adjudication is one pass**, ±2 findings. The Run 12 → Run 14 delta of four is at the edge of
  that; the Run 13 → Run 14 delta of four is too. The §4 conclusion rests on both experiments
  pointing the same way and on the utilisation figure, not on either delta alone.
