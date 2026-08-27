# Run analysis — Run 17

Case ABC-99, 2026-08-27 10:08, scored against
[expected-results-benchmark.md](../../expected-results-benchmark.md) under
[scoring-rubric.md](../../scoring-rubric.md), and compared with
[Run 16](../Run-16/run-analysis.md), [Run 15](../Run-15/run-analysis-latest.md),
[Run 14](../Run-14/run-analysis.md), [Run 13](../Run-13/run-analysis.md),
[Run 12](../Run-12/run-analysis.md), [run-analysis.md](../../run-analysis.md) (Runs 7–9) and
[run-analysis-replays.md](../../run-analysis-replays.md) (Runs 10–11).

---

## The result

**Run 17 is the first genuine end-to-end run in this series — extraction included — and the first
whose score is out of 36 rather than out of 32.** Every one of the twelve extraction sections was
generated live, taking 5.7 to 85.4 seconds each and 496.8 seconds in total. Every previous run
since Run 12 read its canonical model from the gateway cache, four of them in under two seconds.

That fixed the defect Runs 15 and 16 could not confirm, and it cost two findings elsewhere.

| | Run 12 | Run 14 | Run 15 | Run 16 | **Run 17** |
| --- | --- | --- | --- | --- | --- |
| Chat model | Haiku 4.5 | Haiku 4.5 | Sonnet 4.6 | Sonnet 4.6 | **Haiku 4.5** |
| `maxSearchResults` / `maxPassagesPerGroup` | 8 / 24 | 8 / **0** | 8 / **0** | 8 / 24 | **8 / 24** |
| Plans digest | `20ac87292651` | `20ac87292651` | `20ac87292651` | `20ac87292651` | **`3a88a90b0cf6`** |
| Canonical model | `8a0120d90ea2` | `a2bbc38bdf38` | `cba52b288e54` | `1b33706f1b81` | **`e9934ca9e05e`** |
| Extraction | cached | cached | part-cached | **cached, 2s** | **live, 497s** |
| Groups run | 78 | 78 | 73 | 74 | **79** |
| Retrieval (searches / passages) | 229 / 3,612 | 229 / 3,612 | 203 / 3,196 | 203 / 3,196 | **235 / 3,708** |
| Canonical paths resolved / absent | 224 / 31 | 224 / 31 | 197 / 38 | 197 / 38 | **239 / 38** |
| **CHK-005** | ran | ran | **N/A** | **N/A** | **ran** |
| **Findings reachable** | 36 | 36 | **32** | **32** | **36** |
| **Caught / Partial / Missed** | **27 / 5 / 4** | 23 / 9 / 4 | 25 / 7 / 4 | 25 / 6 / 5 | **23 / 7 / 6** |
| Recall | **75%** | 64% | 69% | 69% | **64%** |
| Citation trust (untraceable quotes) | 136 | 132 | 85 | 78 | **146** |
| Chat cost | £2.13 | £2.34 | £7.12 | £6.60 | **£2.25** |
| Wall clock (checks) | 325s | 145s | 552s | 551s | **283s** |

```
Caught 23 / Partial 7 / Missed 6        (Run 16: 25 / 6 / 5 · Run 12: 27 / 5 / 4)
```

**The noise floor is ±2 findings, ±5 points.** Against Run 16 the delta is two Caught, which sits
exactly on it and is not evidence of anything by itself. Against Run 12 — the same model, the same
retrieval settings, the nearest thing to a controlled comparison available — the delta is four, and
that is outside it.

**But the two comparisons are not equivalent, and the headline number understates the run.** Run
16's 25 was out of 32 reachable findings, because CHK-005 did not run; Run 17's 23 is out of 36. On
the 32 findings both runs could reach, Run 17 scores **21 to Run 16's 25**. On the four findings only
Run 17 could reach, it scores **2 of 4** — F5.1 and F5.2 Caught, F5.3 Partial, F5.4 Missed. Run 17
is the weaker run on comparable ground and the more complete one overall, and both statements
need saying.

Three things moved, and each has a mechanism rather than a shrug:

1. **CHK-005 came back** — the false trigger was a cached extraction artefact, now confirmed. §2.
2. **F3.1 was lost**, after being Caught in every genuine run since Run 7. The evidence reached
   eight group prompts and none of them was a CHK-003 group. §3.
3. **The live extraction dropped the client's date of birth**, which no cached run had done, and
   one group misattributed Kim's to him. §4.

---

## 1. Establishing the run is genuine

The rubric's first procedural step, and the one that voided Runs 10 and 11. Run 17 passes on every
test, and — for the first time — passes it on the extraction as well as the checks.

**Checks phase.** 283 seconds for 155,291 output tokens is **548 tok/s**, inside the 260–567 tok/s
band of genuine runs on this case and two orders of magnitude below the 15,897–33,858 tok/s that
identified the replays. No `*** REPLAY ***` banner. `SUMMARY` is clean.

**Extraction phase — this is the new part.** Compare the per-section timings:

| Section | Run 13 | Run 15 | Run 16 | **Run 17** |
| --- | --- | --- | --- | --- |
| 1 Case and parties | 0.5s | 0.3s | — | **21.5s** |
| 5 Existing arrangements | 0.1s | 0.2s | — | **54.1s** |
| 8 Recommendations | 0.1s | 0.1s | — | **85.4s** |
| 12 Triggers and extraction report | 0.1s | 47.4s | — | **18.2s** |
| **Total** | **1.8s** | 650.9s (8 of 12 cached) | **~2s** | **496.8s** |

Run 13's whole extraction took 1.8 seconds and Run 16's took two. Run 15 was a partial cache — four
sections generated, eight served. **Run 17 has no sub-second section**; the fastest is 5.7s and the
slowest 85.4s. Nothing was served from cache.

The plans digest also changed, from `20ac87292651` to `3a88a90b0cf6`, which is what defeated the
cache. That is commits `dbb9f73` and `a17f60e` rewriting eight of the ten query plans. **The
cache-bypass problem named as the blocking step in the Run 16 analysis has been solved, and solved
incidentally** — not by a deliberate bypass but by changing the plans enough to change the request.
That is worth noting because it is not a repeatable method: the next run with unchanged plans will
be served from cache again.

---

## 2. CHK-005's false trigger was a cached-extraction artefact. Confirmed.

Runs 15 and 16 both switched CHK-005 off on a trigger that read false:

> Trigger absent: The recommendation involves no capital, contributions, withdrawals or charges.
> Canonical model `checkTriggers.hasCapitalContributionsOrWithdrawals = false`.

The Run 15 analysis attributed this to a partial-cache boundary and asked for a clean re-run. Run 16
could not provide one — its extraction was byte-identical and served in two seconds, so the trigger
was *"skipped once, and served twice."* The clean re-run was named as the blocking step for the
whole series.

**Run 17 is that re-run, and the trigger is correct.** `hasCapitalContributionsOrWithdrawals`
appears nowhere in the output as a blocking condition; CHK-005 runs all eight requirements, all
eight raise a concern, and the check lands on Potential Concern as the benchmark expects.

That closes the question in the direction the Run 15 analysis suspected but could not show: **the
false trigger was an artefact of a cached extraction, not a defect in the extraction prompt or the
trigger logic.** It also means the two Sonnet runs' recall figures should be read as 25 of 32
wherever they are quoted, and Run 16's summary line — which printed CHK-005 under *CHECKS CLEARED* —
was reporting a cache defect as a clean check for the second time.

**The "make a disabled check loud" recommendation is not retired by this.** The condition that
produced two misleading reports is fixed for this case; the reporting weakness that let it pass
unnoticed twice is not. A trigger that switches off a check whose benchmark expectation is Potential
Concern should be conspicuous, not a line in the cleared list.

---

## 3. F3.1 is the run's real loss, and the mechanism is exact

**F3.1 has been Caught in every genuine run since Run 7. Run 17 misses it, and the string
`Risk rating of 6` appears nowhere in 2,546 lines of output.**

The benchmark's finding is that the file note's ATR paragraph ends *"You are happy to proceed with a
Risk rating of **6**"* where the report reproduces the same paragraph with **5** — three ratings
(4, 5, 6) across the file, unreconciled.

Run 16's G3.6 landed it cleanly:

> The file note [P11] states 'You are happy to proceed with a Risk rating of 6' in the same
> paragraph that records the agreed rating as 5, creating an internal contradiction within the
> supporting file — the suitability report states the agreed rating is 5 throughout, not 6.

Run 17's G3.6, on the same requirement, quotes the report's version instead:

> "We agreed that you are a risk rating 5 rather than a 4 as per the questionnaire. **You are happy
> to proceed with a Risk rating of 5.**"

The group then builds a different and genuine finding — that the risk profile report records *"No
additional notes were recorded"* against the override discussion — and never notices the 6.

**This is not a retrieval failure.** The passage carrying the 6 reached the model **eight times**.
It is in the prompt of G2.1, G2.7, G4.5, G4.7, G6.7, G9.9, G10.3 and G10.5 — and **not one of them
is a CHK-003 group**:

| | Run 16 | **Run 17** |
| --- | --- | --- |
| Groups whose prompt contained `Risk rating of 6` | 9 | **8** |
| **Of those, G3.6 (the risk-override requirement)** | **yes, twice** | **no** |
| G3.6 pack size | 123 passages | **164 passages** |
| Verdict | **Caught** | **Missed** |

Run 16's G3.6 pack contained the file-note passage at rank 4; Run 17's contains three file-note
passages at the same scores (P5 0.620, P7 0.594, P11 0.569) but the ones carrying the 6 — R16's P19
and P22 — have been displaced by P23. **The pack got 33% bigger and lost the passage that mattered.**

That is the same class of failure as F1.6, diagnosed in the Run 14 analysis as *"a verification
failure. Not retrieval, not the cap, not the render."* Here it is a **routing** failure, which is a
sharper and more fixable thing: the evidence is retrievable, it is being retrieved, and it is being
delivered to eight groups that have no requirement to check it while being withheld from the one
that does. Eight groups read the sentence *"a risk rating 5 rather than a 4… happy to proceed with a
Risk rating of 6"* and none of them was asked a question the contradiction answers.

**F3.1 needs the same treatment F1.9 was recommended for after Runs 15 and 16: a section hint, on
the file note's ATR wording block, bound to G3.6 and G3.8.** It is now the third consecutive
analysis to name a missing section hint as the cheapest available fix, and the first where the
finding lost was one the pipeline had previously held for five runs.

---

## 4. The live extraction is better in the aggregate and worse in one specific place

This is the cost of §1, and it should be stated at full weight because it is the first time the
extraction has been measured rather than replayed.

**Better in the aggregate.** 239 canonical paths resolved against Run 16's 197 — the most any run
has resolved — with the absent count unchanged at 38. 235 searches over 3,708 passages, also the
highest. The trigger is correct (§2). One value was corrected to the documented vocabulary
(`planType: "Personal Pension" → "PersonalPension"`) and 26 were left as written with the
consequence spelled out: *"A check matching one of these fields by value will not see them, so
either the report uses a word the model does not have or the extraction invented one."*

**Worse in one place, and it is a KYC field.** The client's date of birth is not in the canonical
model. G1.1 says so three times:

> Report states client age 69; fact find shows driving licence checked 08/12/2025 but licence expiry
> 10/06/2026, **no date of birth extracted for client in canonical model.** … ValueMatch found 69 not
> matched in passages, nearest 70 in [P1] at 1.4% away and 75 in [P12] at 8.7% away.

No cached run did this — `07/06/1956` appears six times in Run 16's output and five in Run 17's, but
in Run 17 it survives only in the retrieved passages, not in the model. And G1.3 then does something
worse than omit it:

> Fact Find (P5) shows **client age as 65 with date of birth 07/05/1960**, which as at 08/12/2025
> (ID verification date) would make client 65, not 69 as implied by the extraction.

**07/05/1960 is Kim's date of birth.** The group has assigned the spouse's DOB to the client and
built a four-year age discrepancy on it. This is the same class of defect the Run 16 analysis
recorded — *"G1.10 states 'Fact find [P2] shows Kim's basic income as £1,200/month net' — that is
John's employment line"* — and it has now happened in the opposite direction, on the opposite field,
in a different run and a different model.

**It changes no verdict**, because F1.4 (the Fact Find's age 70 against DOB 07/06/1956) is a Moderate
finding and unscored. It matters for two other reasons. First, an unextracted DOB in a suitability
QA pipeline is a gap in the most basic identity field there is, and it went unreported by the
extraction summary. Second, this is the second consecutive analysis to find the assessor swapping
John's and Kim's records, which makes F1.5 — the Fact Find's blank First and Last Name fields with
Middle Name "Alan" — the likely upstream cause rather than an unscored curiosity. **The client
identity defect in the source document is now visibly propagating into findings.**

---

## 5. Scores

Verdicts under the rubric, in benchmark order. **No verdict in this run rests on R8** — the six
*raised and not carried* blocks (G2.1, G2.4, G3.5, G6.6, G6.7, G10.2) contain nothing from the
scoring set, so the strict-reading count is unchanged at 23.

| Finding | R12 | R14 | R15 | R16 | **R17** | Where it landed in R17 |
| --- | :-: | :-: | :-: | :-: | :-: | --- |
| F1.1 £300/wk vs £1,200 | C | C | C | C | **C** | G1.4 — *"£1,300 … Fact Find records £1,200.00 monthly — a £100/month discrepancy"* |
| F1.2 State Pension weekly vs monthly | C | C | C | C | **C** | G1.4 — *"frequency mismatch of approximately £770/month"* |
| F1.3 Kim and Sea Cadets absent | C | C | C | C | **C** | G5.2 — *"total income is £1,430 only"*, and the 144% gap stated |
| F1.6 residence for a tenant | M | M | M | M | **M** | rent appears once, as a budget line in G5.2 — §6 |
| F1.8 pension totals | C | C | C | C | **C** | G1.8 — *"three different figures without reconciliation"* |
| F1.9 plans absent from the fact find | P | M | P | M | **P** | G1.7, but inverted — §6 |
| F2.1 five horizons | C | C | C | C | **C** | G2.2 — *"65, 66, 71, 75"* + G3.4's 2-year |
| F2.2 CFL "<3 years" | M | M | P | P | **P** | reached via the risk profile, never the CFL — §6 |
| **F3.1 file note says 6** | **C** | **C** | **C** | **C** | **M** | **↓ nowhere — §3** |
| F3.3 Investment Period 2 years | C | C | C | C | **C** | G3.4 — *"a material discrepancy"* |
| F3.4 People's Pension risk 9 | P | P | P | P | **M** | **↓** G4.6 reads it as a fund-naming problem — §6 |
| F3.5 cautious answers, natural 4 | C | P | C | C | **C** | G3.8 — the four cautious answers against the Balanced label |
| F4.1 "No understanding / knowledge" | C | C | C | C | **C** | G4.1 — *"directly contradict the report's assertion of 'Moderate'"* |
| F4.2 ATR narrative sentence | P | P | P | P | **M** | **↓** the sentence appears nowhere — §6 |
| F4.3 Balanced precondition | C | C | C | C | **C** | G4.4 |
| F4.4 no comprehension tested | P | P | C | C | **C** | G4.2 — *"no understanding of either stocks & shares or pension planning"* |
| F5.1 −£288 | C | C | P | P | **C** | **↑** G5.1 — *"a monthly deficit of £(288.00), not a surplus"* |
| F5.2 affordability | C | C | M | M | **C** | **↑** G5.2 — each phantom income line named |
| F5.3 emergency fund spent | C | P | M | M | **P** | **↑** G5.5 reaches depletion by the wrong route — §6 |
| F5.4 rented residence | M | M | M | M | **M** | *"main residence"* appears nowhere |
| F6.1 ranking above the advice | C | C | C | C | **C** | G9.9 (R1) — the full five-row ranking |
| F6.2 boilerplate rationales | C | C | C | C | **C** | G6.5 — *"identical or near-identical generic language"* |
| F6.3 "Lower Costs" raises them | C | P | C | C | **C** | G6.5 — *"0.26% increase, making recommended solution MORE expensive"* |
| F7.1 two charge tables | C | C | P | P | **P** | G7.4 pairs p11 against the provider, not against p5 — §6 |
| F7.2 row on Zurich's fund | M | C | C | C | **C** | G7.4 — *"which is Zurich's current value … not Standard Life's"* |
| F7.3 4.24% adds instead of subtracts | P | P | P | P | **M** | **↓** four groups attack the denominator, none the sign — §6 |
| F7.5 critical yield | C | C | C | C | **C** | G7.5 |
| F8.1 £12,000 in the appendix | C | C | C | C | **P** | **↓** G8.5 records `appearsOnlyInAppendix = false` — §6 |
| F8.2 ranking never disclosed | C | C | C | C | **C** | G9.9 (R1) |
| F9.1 switch reduces maturity | C | P | C | C | **C** | G9.5 — *"9.4% decrease … £12,000 reduction"* |
| F9.2 ranking above recommendation | C | C | C | C | **C** | G9.9 |
| F9.3 Standard Life on cost | C | P | C | C | **C** | G9.8 — but contradicted in G9.7 — §7 |
| F9.4 Zurich £128,000 | C | C | C | C | **C** | G9.9 — *"£128,000 vs new solution £125,000"* |
| F9.5 charges unreliable | C | C | C | C | **P** | **↓** reached via the provider document, not the two tables — §6 |
| F10.1 FG21/1 capability | C | C | C | C | **C** | G10.4 — *"The report's silence … is itself the finding"* |
| F10.2 no screening | C | C | C | C | **P** | **↓** the scale-of-transfer half never stated — §6 |
| **Caught** | **27** | 23 | 25 | 25 | **23** | |
| **Partial** | 5 | 9 | 7 | 6 | **7** | |
| **Missed** | 4 | 4 | 4 | 5 | **6** | |
| **Of 36 reachable?** | yes | yes | **no (32)** | **no (32)** | **yes** | |

**Three gains, all in CHK-005** — F5.1 and F5.2 from Missed to Caught, F5.3 from Missed to Partial.
All three are the check running again rather than anything the assessor learned.

**Seven losses against Run 16** — F3.1 (C→M), F3.4 (P→M), F4.2 (P→M), F7.3 (P→M), F8.1 (C→P),
F9.5 (C→P), F10.2 (C→P). Five of the seven are Haiku-versus-Sonnet regressions that Run 12 and
Run 14 also show on Haiku; two are new.

---

## 6. The individual verdicts worth arguing about

**F3.4 fell from Partial to Missed, and the way it fell is instructive.** Every previous run reached
the People's Pension risk-9 area and withdrew it — Run 16 twice, once through the incomparable-scales
guard. Run 17 does not withdraw it; it reads it as a different problem. G4.6:

> People's Pension holding shows Global Investments (up to 85% shares) with risk rating 9, but fund
> options in [P12] show Global Investments options at 60%, 85%, or 100% shares — client holds 85%
> variant which exists but **report risk rating 9 does not correspond to a named fund risk scale in
> evidence**

That is a fund-naming complaint. Worse, the next bullet uses the risk-9 holding as evidence the
client *did* self-direct equity investments — the opposite of a risk-mismatch finding. The
retention is never stated, risk 9 is never set against the agreed 5, and *"aligns with your risk
appetite"* appears nowhere. Under the rubric's *actively wrong statement about the area* clause this
is Missed and should be flagged as a false negative.

**The retention link recommended after Runs 12, 13, 14, 15 and 16 is still unapplied**, and Run 17
shows a new way for its absence to cost the finding. The guard bound recommended after Run 16 would
not have helped here, because no guard fired — the group simply asked a different question.

**F4.2, F7.3 and F9.5 are all the same failure shape: the right conclusion by the wrong route (R4).**

- **F4.2** — the ATR questionnaire's *"I have no understanding of investments or how they work"*
  appears nowhere. G3.8 quotes four other ATR answers and uses them against the Risk-5 label, which
  is F3.5's finding, scored separately under R3.
- **F7.3** — the run engages `4.24%` in four groups (G7.4, G7.6, G7.11, G8.1) and calls it
  unreliable every time, but always on the denominator: *"These figures imply a fund value of
  £110,188.68, which matches neither the existing arrangements sum … nor the stated transfer
  value."* The arithmetic sign error — that the 0.24% saving was added instead of subtracted, and the
  figure should be ≈3.76% — is never reached. `3.76` appears nowhere in the output.
- **F9.5** — the conclusion *"making the comparison unreliable"* is reached, but from the provider
  document (*"Provider documentation … shows Zurich charges 0.18% fund charge plus £2.12 monthly
  admin"*) and from the EA5 wrong-plan error, not from the report's own two tables.

F9.5's Partial is entailed by F7.1's. **The pipeline never registers that page 5 and page 11 of the
report disagree with each other.** It holds both rows — G7.4's own citation list carries the p5
Zurich 0.18% and the p11 Zurich 0.93% side by side — and relates neither to the other. That is the
rubric's first Partial bullet in its purest form: *both halves present, never joined, including when
both are in the same group's own fields.* Downstream, groups variously assert Standard Life's
existing charge as 0.52% (G7.4, G7.6, G9.7) and as 0.18% (G6.5, G7.5, G7.7, G8.5, G9.8, G9.9)
without any group noticing the report gives both.

**F8.1 fell to Partial on a rendered field, not on prose.** G8.5 does state a prominence failure and
does name the £12,000 — but it sets the prominence complaint against the critical-yield narrative
rather than against the *"Lower Costs"* / *"0.24% less expensive"* lead, and its own extracted field
reads `appearsOnlyInAppendix = false`. The group denies the placement the finding turns on while
raising an adjacent one. This is the first run to get that field wrong.

**F1.9 remains Partial, and inverted.** The benchmark's finding is that Zurich, Standard Life and
Scottish Widows are absent from the Fact Find while the report tabulates all five. G1.7 treats all
five as corroborated — *from the provider policy documents*, not the Fact Find — and raises the
mirror image: that the Fact Find's Aviva Platform entry (`AV2936864`) is a **sixth** arrangement
missing from the report. Right documents, wrong direction. Notably the passage the Run 16 analysis
identified as evicted by the cap is back in the pack here; the finding is still not made, which
moves F1.9 from a retrieval problem to the same verification class as F1.6.

**F10.2 fell to Partial on a missing half.** Age 69, imminent retirement and the absence of
screening are all present and joined in G10.4. The load-bearing element — that he is moving
*effectively all* of his pension provision on advice — is never stated, and the group's own guard
citation (*"Age alone is not vulnerability"*) leaves what survives as age plus life stage.

**F1.6 and F5.4 are Missed for the seventh and sixth consecutive run.** `main residence` appears
nowhere in 2,546 lines; `Tenant – private` appears nowhere; the £600 rent appears once, inside
G5.2's expenditure itemisation, cited as corroboration of the £1,718 total with no reference to
tenure. G1.5 goes the other way and concludes *"there is no material discrepancy"* on assets. The
Run 14 analysis established that the evidence reaches the model and three purpose-written guards are
in the prompt. Nothing has changed.

---

## 7. Two defects in the output that a reviewer would hit before the findings

### Citation trust is the worst it has been

**146 quotes could not be traced to the evidence they were drawn from** — against 78 in Run 16, 85
in Run 15, and 132–136 in the earlier Haiku runs. As a share of the 558 cited passages that is
**26%**, up from 12% on Sonnet.

Some of this is the model: Haiku has been at 132–146 in all three of its genuine runs and Sonnet at
78–85 in both of its. But the absolute figure is what a reviewer meets, and it is high enough to
matter for the verdicts. G9.5, G9.2, G7.5, G8.8 and G9.7 are all marked *citations partly verified*,
and several of the £12,000, ranking and performance quotes that F9.1, F9.2 and F9.4 were scored on
sit in the NOT FOUND lists. **Under R7 the verdicts hold — the substance is printed in the rendered
Discrepancies and narrative — but the working behind five Caught findings is not checkable.**

**The cause is a table read out as prose, and every one of the 146 is a prose quote.**

Counting them by kind: of the 146 rejections, **146 are quoted prose and none is a table read.**
Every `cells` citation in the run verified. That matters because `CitationVerifier.Normalise` folds
markdown pipes and emphasis markers to spaces, so a `cells` read survives being merged or reflowed —
the structure it would have to get wrong has already been normalised away.

What the 146 actually are is the thing the assessor prompt already forbids. Three examples, verbatim
from the NOT FOUND lists:

```
"Full Name Date of Birth 07/05/1960 Age 65 Relationship Spouse Related To Note Financially Dependent? No"
"Basic Income, Employed CDH Recruitment, Monthly, £1,200.00; State Pension, Monthly, £230.00"
"Illustration date 24 September 2025 Retirement date 7 June 2031 (age 75)"
```

None of those sentences is in any document. Each is a **table reconstructed as a sentence** — column
headers and cell values run together, sometimes across several rows, with connecting punctuation the
model supplied. 51 of the 146 still carry a pipe; 24 are over 200 characters, which is a multi-row
merge. The prompt's rule is explicit — *"A table restated as a sentence is not a quotation and will
be rejected, however accurately you read it"* — and the finding here is how often it is ignored:
**26% of cited passages, and the instruction to use `cells` instead is being read past.**

**The verifier is right and both of my earlier readings of it were wrong.** The first draft of this
section claimed two rejected quotes were verbatim-correct and the metric was at fault. The second
claimed 36 of 46 pipe-bearing rejections were merged `cells` rows whose values were all present. The
second was an artefact of my own script, which classified by counting pipe characters in the printed
report rather than by the citation's kind: the merged Fact Find row it named as the example was
**accepted**, not rejected. Checked directly, `grep -c '^        table:'` over the rejection blocks
returns zero.

So there is nothing to split. The remedy is on the model's side of the boundary — the prompt already
says what to do and a quarter of citations do not do it — and the honest reading of the figure is
that it is measuring prompt adherence on citation form, not a defect in the check.

### CHK-009 contradicts itself on the most material cost finding

G9.8 states F9.3 correctly:

> Report claims switch provides 'Lower Costs' as a benefit for all plans, but for Standard Life the
> cost actually **increases** by 0.26% annually

G9.7 and G9.9, in the same check, state the reverse:

> Report states Standard Life existing is '0.08% more expensive' but … indicating new solution is
> actually 0.08% **CHEAPER**

**F9.3 still scores Caught under R2**, because one group joins it correctly. But a reviewer reading
CHK-009 top to bottom meets both, and the wrong one twice. This is a new failure — no previous
analysis records the pipeline asserting a cost direction and its opposite inside one check — and it
is on the single most material charge finding in the case.

### Prompt adherence regressed to the Haiku baseline

**45 of 87 responses echoed the wrong requirement id**, against 0 of 77 on both Sonnet runs and
32–36 of 85 on the earlier Haiku runs. The plan's values are used regardless, so no verdict is
affected. It tracks the model, not the plans: every Haiku run is at 38–52% and every Sonnet run at
0%.

---

## 8. What this run settles, and what it does not

**Settled.**

1. **The CHK-005 trigger was a cache artefact.** §2. Runs 15 and 16 should be quoted as 25 of 32
   wherever their recall appears.
2. **A live extraction is reachable, and changing the plans is what reaches it.** §1. Not a
   repeatable method — the next unchanged-plan run will be cached again — but the first genuine
   canonical model in the series.
3. **F3.1's loss is a routing failure, not a retrieval one.** §3. The evidence reached eight group
   prompts and no CHK-003 group. This is the most precisely diagnosed single finding loss in the
   project.

**Not settled, and the run does not help.**

4. **Haiku versus Sonnet is still uncontrolled.** Run 17 changed the model *and* the plans *and* the
   canonical model against Run 16. Its 23 cannot be attributed. Against Run 12 — same model, same
   retrieval settings, but different plans and a cached extraction — the delta of four is outside the
   noise floor and still has three candidate causes.
5. **`maxPassagesPerGroup: 12` is still untested**, and Run 16's §1 result stands as the strongest
   single-variable finding in the project.
6. **Sampling is unpinned**, as in every run so far. One sample.

---

## 9. What to do next

1. **Add a section hint for the file note's ATR wording block, bound to G3.6 and G3.8.** §3. This is
   the cheapest fix available and it recovers a finding the pipeline held for five runs. It is also
   the third consecutive analysis to name a missing section hint — F1.9's was recommended after Runs
   15 and 16 and is still unapplied.
2. **Re-run `8 / 24` on Haiku with the new plans and a cached extraction.** That isolates the plans
   change from the model change, and it is one run. Without it, none of the seven losses in §5 can be
   attributed between Haiku, the new plans, and the live canonical model.
3. **Fix the client date of birth in extraction, and check the John/Kim field assignment.** §4. An
   absent DOB is a KYC gap the extraction summary did not report, and the assessor has now swapped
   the two clients' records in two consecutive runs on two different fields. F1.5 is the likely
   upstream cause and it is an unscored benchmark finding.
4. **Give F7.1 the two tables in one pack.** §6. The pipeline holds both rows and never relates
   them, and F9.5 is entailed by that. One section hint per table, bound to G7.4, would put the
   contradiction in front of a single group for the first time.
5. **Get the assessor to use `cells` for a table instead of quoting it as a sentence.** §7. All 146
   rejections are prose and none is a table read; each is a table row reconstructed as a sentence,
   which the prompt already forbids in those words. The verifier is behaving correctly and must not
   be loosened — the near-miss rule it already rejected would admit the `Risk rating of 6` → `5`
   alteration. This is a prompt-adherence problem on citation form, and the figure should be read as
   measuring that rather than as a defect in the check.
6. **Reconcile cost direction within a check before rendering.** §7. CHK-009 asserts Standard Life's
   charge change in both directions; the *figures described differently* pass already does this
   across checks and would have caught it within one.
7. **The retention link for F3.4, still.** Sixth consecutive analysis. Run 17 shows a new failure
   mode for its absence — no guard fired, the group asked a different question — so the guard bound
   recommended after Run 16 is necessary but not sufficient.

---

## 10. Limits

- **Nothing in this comparison is controlled.** Run 17 changed the chat model, the plans digest and
  the canonical model against Run 16, and the plans and the extraction against Run 12. The 23 is a
  measurement of this configuration and an attribution of nothing.
- **The 23-versus-25 headline is not like-for-like** and should not be quoted without §Result's
  second paragraph. Run 16 scored 25 of 32 reachable; Run 17 scored 23 of 36, and 21 of the same 32.
- **Adjudication is one pass, ±2 findings.** The Run 16 → Run 17 delta of two Caught sits exactly on
  the floor. The conclusions in §3 and §4 do not rest on the delta — they rest on a specific string
  being absent from the output and present in eight prompts, and on a specific field being absent
  from the model. Those are observations, not scores.
- **F5.3, F8.1 and F10.2 are the judgement calls.** All three scored Partial on the reached-by-the-
  wrong-route or unjoined-halves bullets. A reader who scores all three Caught reaches 26 / 4 / 6;
  one who scores F9.5 Caught as well reaches 27 / 3 / 6. The Missed count is the robust figure here,
  and at 6 it is the highest of any genuine run.
- **§7's citation-trust reading was wrong twice, and the corrections are worth keeping visible.**
  Draft one claimed two rejected quotes were verbatim-correct and the metric was at fault. Draft two
  claimed 36 of 46 pipe-bearing rejections were merged table rows whose cells were all present — an
  artefact of classifying by counting pipe characters in the printed report rather than by the
  citation's kind, and the row it named as the example was accepted rather than rejected. The third
  reading, checked with `grep -c '^        table:'` over the rejection blocks, is that **all 146 are
  prose and none is a table read.** Recommendation 5 went from *loosen the metric* to *split the
  count* to *fix the citation form*, and only the last one survives. Some code was written against
  the second reading and reverted; the two `lifeExpectancyBasis` readers from the same item stand.
- **The four *"ranked by Maturity Value"* section hints matched nothing again**, for the fifth
  consecutive run — G2.2, G6.4, G8.8, G9.7. That is the direct evidence route for F6.1, F8.2, F9.2
  and F9.4, all four of which are Caught only because the ranking surfaced in G9.9 through an
  unrelated search. **Mechanism, not measurement**, and four Caught findings currently rest on it.
- **Whether the live extraction is better or worse overall is one sample.** It resolved 42 more
  canonical paths than any cached run and dropped one KYC field. Both facts are from the same single
  extraction and neither generalises.
