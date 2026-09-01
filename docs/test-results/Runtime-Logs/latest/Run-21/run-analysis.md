# Run analysis — Run 21

Case ABC-99, 2026-08-28 13:36, **Sonnet 4.6 on both extraction and checks**, scored against
[expected-results-benchmark.md](../../expected-results-benchmark.md) under
[scoring-rubric.md](../../scoring-rubric.md), and compared with
[Run 20](../Run-20/run-analysis.md) (Haiku, identical plans), Run 19 (unscored, identical plans),
[Run 18](../Run-18/run-analysis.md), [Run 16](../Run-16/run-analysis.md) (the previous Sonnet run)
and [Run 12](../Run-12/run-analysis.md).

---

## The result

**Run 21 scores 30 of 36 — 83% recall, the highest the project has recorded, and the first result
that clears the previous best by more than the noise floor.** The previous best on the full
36-finding set was 27 (Runs 12 and 18). Even on the strictest reading of the two judgement calls
(§7) the run lands at 29; on the most permissive it lands at 31. **Every reading beats 27.**

It also produced the cleanest experiment in the project's history. Run 20 and Run 21 ran the same
plans (`09cf0a31b103`) against the same index, and their searched extracts are **identical except
for the one header line that names the model**:

```
diff Run-20/retrieval-extract  Run-21/retrieval-extract
  2 lines differ:
    < Case ABC-99 (tenant 99), model intelliflo-claude-haiku-4-5,  run 2026-08-28 11:53.
    > Case ABC-99 (tenant 99), model intelliflo-claude-sonnet-4-6, run 2026-08-28 13:28.
```

All 2,341 passages, in the same order, with the same scores, the same query attribution and the
same eviction marks. **Run 20 vs Run 21 is a pure model comparison on byte-identical evidence.**
Everything in §5 follows from that.

| | Run 12 | Run 16 | Run 18 | Run 19 | Run 20 | **Run 21** |
| --- | --- | --- | --- | --- | --- | --- |
| Chat model | Haiku 4.5 | Sonnet 4.6 | Haiku 4.5 | Haiku 4.5 | Haiku 4.5 | **Sonnet 4.6** |
| Extraction model | Haiku 4.5 | Haiku 4.5 | Haiku 4.5 | Haiku 4.5 | Haiku 4.5 | **Sonnet 4.6** |
| `maxSearchResults` / `maxPassagesPerGroup` | 8 / 24 | 8 / 24 | 8 / 24 | 8 / 24 | 8 / 24 | **8 / 24** |
| Plans digest | `20ac8729…` | `20ac8729…` | `862608be…` | `09cf0a31…` | `09cf0a31…` | **`09cf0a31…`** |
| Canonical model | `8a0120d9…` | `1b33706f…` | `d0559004…` | `2773d42d…` | `fbc4e932…` | **`a85ec8f3…`** |
| Extraction | cached | cached, 2s | live, 465s | live, 601s | live, 210s | **live, 544s** |
| Canonical model size | — | — | — | 364,692 ch | 348,781 ch | **469,842 ch** |
| Vocabulary violations | — | — | — | 19 | 2 | **0** |
| Retrieval (searches / passages) | 229 / 3,612 | 203 / 3,196 | 236 / 3,724 | 236 / 3,724 | 236 / 3,724 | **236 / 3,724** |
| Pack selection (reached / retrieved) | — | — | — | 1,838 / 2,341 | 1,838 / 2,341 | **1,838 / 2,341** |
| Canonical paths resolved / absent | 224 / 31 | 197 / 38 | 237 / 48 | 242 / 43 | 250 / 35 | **234 / 51** |
| Requirements printed as concerns | — | — | 84 | 84 | 83 of 88 | **82 of 88** |
| NOT ASSESSED blocks | — | — | 15 | 9 | 8 | **0** |
| **CHK-005** | ran | N/A | ran | ran | ran | **ran** |
| **Findings reachable** | 36 | 32 | 36 | 36 | 36 | **36** |
| **Caught / Partial / Missed** | 27 / 5 / 4 | 25 / 6 / 5 | 27 / 5 / 4 | not scored | 25 / 7 / 4 | **30 / 3 / 3** |
| **Recall (Caught / 36)** | 75% | 78% *(25/32)* | 75% | — | 69% | **83%** |
| Citation trust (untraceable quotes) | 136 | 78 | 195 | 155 | 162 | **77** |
| Groups flagged on citations | — | — | — | 57 | 61 of 83 (73%) | **34 of 82 (41%)** |
| Prompt adherence (wrong id / rewrote) | — | — | 59 / 10 | 58 / 4 | 61 / 12 | **0 / 6** |
| Chat cost | £2.13 | £6.60 | £2.33 | £2.24 | £2.43 | **£7.44** |
| Extraction cost | — | — | — | £0.70 | £0.68 | **£2.49** |
| **Total cost** | — | — | — | **£2.94** | **£3.11** | **£9.93** |
| Wall clock (checks) | 325s | 551s | 300s | 278s | 314s | **529s** |

```
Caught 30 / Partial 3 / Missed 3     (Run 20: 25 / 7 / 4 · Run 18: 27 / 5 / 4 · Run 16: 25/32)
Range across the two judgement calls: 29–31.  Noise floor ±2.  Previous best: 27.
```

**The Missed count fell to 3** — the lowest recorded on the 36-finding set, and the first time it has
moved below 4 since the benchmark was written.

---

## 1. Establishing the run is genuine

**Checks phase.** 529 seconds for 171,193 output tokens is **324 tok/s**. That is below the 260–576
tok/s Haiku band, as expected for a larger model, and two orders of magnitude below the
15,897–33,858 tok/s that identified the Run 10/11 replays. No `*** REPLAY ***` banner. Per-check
wall-clock lines run 204–217s at the top end, consistent with a slower model doing the same work.

**Extraction phase.** A live 544.4-second extraction, cache bypassed by run marker, every section
between 34.4s and 201.9s, none sub-second. The canonical model digest `a85ec8f32bd7` is new. The
wave structure introduced at Run 20 is visible (sections 4–9 sharing a 201.9s completion), so the
speed-up from `3ab2209` is still in place — Sonnet is simply slower per wave.

**One caution on Run 20 vs Run 21 as a controlled pair.** Sampling is unpinned on both
(`seed not pinned`), and the model changed on *both* the extraction and the assessment. The
experiment isolates "Haiku everywhere" against "Sonnet everywhere"; it does not separate the
extractor's contribution from the assessor's. §5 attributes specific findings to one or the other
by mechanism where the evidence supports it, and says so.

---

## 2. Where the gain came from

Six findings rose against Run 20 and one fell.

| | Run 20 (Haiku) | Run 21 (Sonnet) |
| --- | :-: | :-: |
| F1.8 pension totals £110k / £106k / £116,998 | P | **C** |
| F1.9 three plans absent from the fact find | M | **C** |
| F5.3 emergency fund LIQUIDATED 2ND | P | **C** |
| F6.3 "Lower Costs" raises Standard Life's charges | P | **C** |
| F9.3 Standard Life switch on cost | P | **C** |
| F10.2 no vulnerability screening | P | **C** |
| **F3.1 file note says 6** | **C** | **P ↓** |

Four of the six gains trace to **the extraction**, and can be shown to by mechanism. The Sonnet
extraction records **8 internal inconsistencies against Haiku's 5**, and three of the new ones are
exactly the findings that rose:

```
internalInconsistencies[0]  Zurich   0.18% + £2.12/mo (p5)  |  0.93% (p11)          ← new
internalInconsistencies[2]  totals   £110,000 (p3) | £106,000 (p4)
                                     | £117,998.47 sum | £110,185 (p9)              ← new
internalInconsistencies[4]  SL       0.08% more expensive (p8 narrative)
                                     | 0.26% more expensive (p11 table)             ← new
```

- **The pension-totals inconsistency drives F1.8.** Run 20's model did not carry it and its groups
  only ever set £116,998.47 against charge-derived fund values. Run 21's G1.7, G1.8, G1.10, G3.2,
  G3.10, G6.1, G6.3, G6.4 and G8.1 all state the p3/p4/sum contradiction directly.
- **The Standard Life direction-of-cost inconsistency drives F6.3 and F9.3.** G9.8 states it
  outright: *"The report's stated justification of 'lower costs' for Standard Life is directly
  contradicted by the report's own switch charges table."* Run 20 had the +0.26% figure and the
  "Lower Costs" claim in different groups and never joined them.
- **The Zurich charge inconsistency completes F7.1.** §4.

The other two gains — F1.9 and F5.3 — came from the **assessor**, on evidence that was in Run 20's
packs too. §5.

---

## 3. F7.1 is Caught on both halves for the first time

Run 18's third recommendation was: *"Set the Zurich half of F7.1 table-against-table. The Standard
Life contradiction is now Caught from the two tables; Zurich (0.18% vs 0.93%) is still reached via
the provider document."* Runs 19 and 20 did not close it — Run 20's G7.4 still compared Zurich's
0.93% to the provider's 0.75%, which is the wrong route.

Run 21 closes it, and it closes it in the extraction rather than in a plan:

> **[G1.7]** Zurich charge on page 5 is stated as '0.18% fund + £2.12/month admin'; the switch
> charges table on page 11 states Zurich current annual recurring charge as 0.93% — these are
> materially inconsistent and cannot both be the correct total charge.
>
> **[G1.7]** Standard Life charge on page 5 is stated as '0.52% fund charge'; the switch charges
> table on page 11 states Standard Life current annual recurring charge as 0.18% — these are
> directly contradictory.

Both plans, both tables, in one group, with the page numbers. The same pairing is repeated in G1.8,
G1.10, G7.3, G7.5, G7.6, G8.1, G9.8 and G9.9. **F9.5 follows from it** — G9.9 states that the charge
arithmetic makes *"the entire Standard Life cost comparison unreliable"*, which is the benchmark's
own route rather than the provider-document detour that held F9.5 at Partial in Runs 17 and 18.

F7.2 (the £268.94 row computed on Zurich's £103,439 rather than Standard Life's £3,002) is Caught in
nine separate groups, more comprehensively than any prior run.

---

## 4. F3.1 is the one that fell, and it fell in an interesting way

Run 20 caught F3.1 in G6.7 — a group nobody aimed at it — after the two hints written for it
(`G3.6: ATR Wording`, `G3.8: ATR Wording`) fired on nothing for the third run. Those hints fire on
nothing again here, for the fourth run.

In Run 21 the file note's rating 6 reaches the reasoning twice, and is **denied both times**:

> **[G4.7] Discrepancies** — The file note (P8) records the ATR wording as containing an apparent
> internal inconsistency ('You are happy to proceed with a Risk rating of 6' followed immediately by
> confirmation of risk rating 5), suggesting a possible data quality issue in the recorded
> understanding, **though this does not directly contradict the report's stated ATR of 5.**
>
> **[G4.7] narrative** — The ATR wording anomaly in P8 … is noted but **does not materially alter
> the finding; the report consistently states ATR 5.**
>
> **[G6.7, raised and not carried]** — File note [P3] contains an internal ATR inconsistency
> (references to risk ratings 4, 5 and 6 in sequence), but this does not translate into an
> unresolved recommendation outcome in the report.

Both sides are named. The word "inconsistency" is used. And the assessor then explicitly says the
two do not conflict — which fails the third test for Caught (*"the output says they conflict"*) and
matches the Partial bullet *"reached and then withdrawn"*.

**Scored under R8 it is Caught**, and the rubric requires that to be stated: the G6.7 block is a
*raised and not carried* block, it is printed, it names ratings 4, 5 and 6 and calls them an
inconsistency. **Scoring F3.1 under R8 gives Run 21 a score of 31/2/3 (86%).** This is the only
verdict in the run that turns on R8, and it is recorded here so the alternative can be computed
without re-adjudicating.

Sonnet's behaviour here is the mirror of its strength elsewhere: it reasons past a surface anomaly to
a conclusion about materiality. That is right most of the time and wrong here, because the
benchmark's finding *is* the unreconciled anomaly.

---

## 5. The Run 20 vs Run 21 experiment settles the reasoning-vs-retrieval question

Run 20's analysis established, from the searched extract, that every Missed finding's evidence
reached the pack of a group that should have made it, and concluded the shortfall was a reasoning
failure rather than a retrieval one. **Run 21 tests that conclusion directly, because the packs are
byte-identical.** If the shortfall were retrieval, a better model could not recover anything.

It recovered five of the seven that were short, from the same passages:

| Evidence string | Packs it reached (identical in both runs) | Run 20 | Run 21 |
| --- | --- | :-: | :-: |
| `LIQUIDATED` | G1.6, G2.4, **G5.5**, G5.1, G8.4 | P | **C** |
| fact-find pension schedule | **G1.7** | M | **C** |
| p3/p4 totals | **G1.7**, G1.8, G1.10 | P | **C** |
| SL cost direction | **G6.5**, G9.8, G9.9 | P | **C** |
| age + no screening | **G10.5** | P | **C** |

**F5.3 is the cleanest illustration.** Both runs received `Savings | £6,000 | LIQUIDATED 2ND` in
G5.5's pack. Run 20 quoted it, invoked the guard written for it, and argued past it —
*"This decline is minimal and does not indicate that the savings account is being liquidated
year-on-year."* Run 21 states the finding:

> No separate emergency fund is identified in the file; the £6,000 savings is the only liquid asset,
> and **it is listed as the second asset to be liquidated in the drawdown order, meaning it would be
> accessed early if a shortfall arose.**

Same passage, same group, same guard. Different conclusion.

**F1.9 is the same story on G1.7.** Run 20's G1.7 chose the provider statements as its comparison set
and passed the arrangements as consistent. Run 21's G1.7 uses the Fact Find:

> The fact find retrieved passages show no entries for Scottish Widows ZU4225575, Zurich
> P10026-719-001/DL or Standard Life D2301334000 — **three of the five arrangements are not listed in
> the visible fact-find pension schedule.**

It then adds something no run has found before — the Fact Find records an Aviva Platform pension
`AV2936864-001` at £116,555.45 against the report's `SP50163762` at £3,744.36, *"different policy
numbers and materially different values; no reconciliation is provided."*

**What is left is genuinely hard, not merely unreached.** The three Missed findings are the same
three the project has been carrying:

- **F1.6 and F5.4 — the tenant findings. Missed for the ninth and eighth consecutive run, and this
  time actively dismissed.** G5.2 states: *"The guard on modelled main residence does not apply
  (the clients are renters and no property is modelled)."* The assessor established that the clients
  rent — which is the finding's premise — and used it to rule the guard out instead of to challenge
  the report's claim that the cashflow *"incorporates the assets of your main residence"*. Under the
  rubric an actively wrong statement about the area is Missed **and should be flagged as a false
  negative**, which it is here. `Residence Nil Rate` reached G1.4, G1.6, G2.4 and G8.3 in both runs
  and appears nowhere in either output.
- **F4.2 — the ATR knowledge sentence.** *"I have no understanding of investments or how they work"*
  has zero footprint for the sixth consecutive run, despite reaching G3.6, G4.1 and G4.2. Both models
  catch F4.1's Investor Experience answers from the same packs and neither reaches the ATR wording.

**The section hints are still dead.** The same nine matched nothing, for the fourth consecutive run,
across two different models. They are not a model problem and they are not, on this evidence, a
recall problem either — the four `ranked by Maturity Value` hints have now failed for nine runs while
F6.1, F8.2, F9.2 and F9.4 are Caught in both models off the ranking table that ordinary search
delivers to G8.3, G8.7, G9.1, G9.6, G9.8 and G9.9.

---

## 6. Scores

Verdicts under the rubric, in benchmark order. **One verdict (F3.1) turns on R8 and is recorded in
§4; the headline 30 does not use R8.**

| Finding | R16 | R18 | R20 | **R21** | Where it landed in R21 |
| --- | :-: | :-: | :-: | :-: | --- |
| F1.1 £300/wk vs £1,200 | C | C | C | **C** | G1.3 — *"£300/week… fact find records £1,200.00 (£276.92/week)… none of these three figures agree"* |
| F1.2 State Pension weekly vs monthly | C | C | C | **C** | G1.10 — *"report… £230/week; fact find records £230.00 monthly — frequency mismatch"* |
| F1.3 Kim and Sea Cadets absent | C | C | C | **C** | G1.10 — Sea Cadets *"no corresponding entry"*; £1,430 vs derived £2,487. Kim half mis-attributed — §8 |
| F1.6 residence for a tenant | M | M | M | **M** | actively dismissed in G5.2 — §5 |
| F1.8 pension totals | C | C | P | **C ↑** | G1.7/G1.8/G1.10 — £110,000 (p3) / £106,000 (p4) / £116,998.47 / £110,185 |
| F1.9 plans absent from the fact find | M | C | M | **C ↑** | G1.7 — three arrangements absent from the fact-find schedule — §5 |
| F2.1 five horizons | C | C | C | **C** | G2.2/G2.5/G3.4 — 66, 71, 75, 2yr, 10yr set against each other |
| F2.2 CFL "<3 years" | P | P | P | **P** | risk-profile 2-year route again (R4); `Less than 3 years` reached 10 packs unused |
| **F3.1 file note says 6** | C | M | C | **P ↓** | G4.7 names it and denies the conflict; G6.7 R8 block — §4 |
| F3.3 Investment Period 2 years | C | C | C | **C** | G3.4/G3.6/G3.10 — *"£106,000 over 2 years, not a 10-year term"* |
| F3.4 People's Pension risk 9 | P | P | C | **C** | G3.11 (High) — *"a material mismatch of 4 risk levels"*, plus the *"should consider"* non-recommendation |
| F3.5 cautious answers, natural 4 | C | C | C | **C** | G3.7 — *"consistently cautious in character… conflicts with the upward-adjusted rating of 5"* |
| F4.1 "No understanding / knowledge" | C | C | C | **C** | G4.2 — *"a direct contradiction"* |
| F4.2 ATR narrative sentence | P | M | M | **M** | zero footprint; reached G3.6/G4.1/G4.2 — §5 |
| F4.3 Balanced precondition | C | C | C | **C** | G4.1/G4.4 — precondition quoted, contradicted |
| F4.4 no comprehension tested | C | C | C | **C** | G4.4/G4.5/G4.7 |
| F5.1 −£288 | P | C | C | **C** | G5.1/G5.4 — deficit vs *"comfortably meets"* |
| F5.2 affordability | M | C | C | **C** | G5.2/G5.4 — £1,430 vs £29,840 cashflow inputs, *"the report does not reconcile this gap"* |
| F5.3 emergency fund spent | M | C | P | **C ↑** | G5.5 — *"listed as the second asset to be liquidated"* — §5 |
| F5.4 rented residence | M | M | M | **M** | dismissed with F1.6 — §5 |
| F6.1 ranking above the advice | C | C | C | **C** | G9.8 — full ranking reproduced: Zurich 1st £128k … New Solution 5th £125k |
| F6.2 boilerplate rationales | C | C | C | **C** | G6.1 — four bullets quoted *"word-for-word across Aviva, Zurich and Standard Life"* |
| F6.3 "Lower Costs" raises them | C | C | P | **C ↑** | G6.5 — *"leads with 'Lower Costs'… the new solution is actually more expensive… undermining the stated rationale"* |
| F7.1 two charge tables | P | C | C | **C** | **both halves** — Zurich 0.18/0.93 and SL 0.52/0.18, G1.7 — §3 |
| F7.2 row on Zurich's fund | C | C | C | **C** | G7.3/G7.5/G7.6/G8.1/G9.8/G9.9 |
| F7.3 4.24% adds instead of subtracts | P | P | P | **P** | G7.6 engages both figures; the sign error unnamed, `3.76` absent |
| F7.5 critical yield | C | C | C | **C** | G7.5 — 4.8% vs 2.94/2.95%, achievability *"asserted without an independent document"* |
| F8.1 £12,000 in the appendix | C | C | C | **C** | G8.5 — *"relegated to the appendix on page 18"* vs benefits on pages 6–8 |
| F8.2 ranking never disclosed | C | C | C | **C** | G8.7/G9.8 — *"the report does not acknowledge this relative ranking"* |
| F9.1 switch reduces maturity | C | C | C | **C** | G6.4/G9.7 — £115,000 vs £127,000, £12,000 / 9.4% |
| F9.2 ranking above recommendation | C | C | C | **C** | G9.8 |
| F9.3 Standard Life on cost | C | C | P | **C ↑** | G9.8 — *"a claim that is false per the report's own table"* |
| F9.4 Zurich £128,000 | C | C | C | **C** | G9.8 |
| F9.5 charges unreliable | C | P | C | **C** | G9.9 — both charge inconsistencies, *"the entire Standard Life cost comparison unreliable"* |
| F10.1 FG21/1 capability | C | C | C | **C** | G10.1/G10.4 |
| F10.2 no screening | C | P | P | **C ↑** | G10.5 — age 70, past retirement, *"consolidating multiple pension arrangements"*, no screening anywhere in the file |
| **Caught** | 25/32 | 27 | 25 | **30** | |
| **Partial** | 6 | 5 | 7 | **3** | |
| **Missed** | 5 | 4 | 4 | **3** | |

**The two judgement calls, and the range.** F10.2 is scored up from the Partial it held in Runs 18
and 20 on a specific new element: those runs had age and the absence of screening but nothing on the
scale of the transfer, and G10.5 now adds *"consolidating multiple pension arrangements ahead of
retirement"*. A strict reader who holds F10.2 at Partial scores the run **29**. A reader who applies
R8 to F3.1 (§4) scores it **31**. The headline is 30, the range is 29–31, and **every point in that
range is above the previous best of 27 by more than the ±2 noise floor.**

---

## 7. Extraction quality: the best model the project has produced, with one arithmetic slip

| | Run 19 (Haiku) | Run 20 (Haiku) | **Run 21 (Sonnet)** |
| --- | :-: | :-: | :-: |
| Wall clock | 601s | 210s | 544s |
| Cost | £0.7003 | £0.6800 | **£2.4912** |
| Model size | 364,692 ch | 348,781 ch | **469,842 ch** |
| Vocabulary violations | 19 | 2 | **0** |
| `internalInconsistencies` | — | 5 | **8** |
| `expectedButAbsent` | — | 3 | **8** |
| `ambiguities` | — | 4 | **4** |
| `overallConfidence` | — | 0.92 | **0.82** |
| Check triggers settled | 1 (contradicted) | 1 | **2** |

**Zero vocabulary violations** — the extract output carries no vocabulary section at all, against
Haiku's 2 in Run 20 and 19 in Run 19. Two triggers settled from the model rather than one
(`hasComplexProduct` and `hasCapitalContributionsOrWithdrawals`).

**Lower confidence, higher accuracy.** `overallConfidence` fell from Haiku's 0.92 to 0.82 and
`expectedButAbsent` rose from 3 to 8, while the score rose by five. Haiku's 0.92 was
over-confidence, and the run's own scores are the evidence.

**Three new internal inconsistencies, three findings.** §2. The extraction now sees the Zurich charge
contradiction, the four-way pension-total contradiction and the Standard Life direction-of-cost
contradiction, and each maps to a finding that rose. It also catches two that no prior run recorded:
the **performance/volatility chart caption swap** on page 13 (A=Aviva/D=Zurich against
D=Aviva/A=Zurich) and the **£110,185 vs £110,185.60** transfer-total rounding.

**The one defect is arithmetic.** `internalInconsistencies[2]` states the sum of the five plans as
**£117,998.47**. The correct figure is £116,998.47 — a £1,000 slip. It propagates into seven places in
the output as *"£116,998.47 or £117,998.47 per derived figures"*, where the groups hedge between the
model's figure and the correct derived one. It did not cost a verdict, because the surrounding
contradiction is stated with the right figure, but it is a fabricated number rendered to a reviewer.

**`priorityRank` is still invented.** Both models extract `priorityRank: 1, 2, 3` on the three
objectives where the report states no priority order — benchmark F2.3, Low severity and unscored.
Run 21 is better than Run 20 here: where Run 20's G2.1 asserted *"three client objectives with
explicit priority ranking"* as fact, Run 21's G2.5 at least challenges it — *"no evidence of
client-authored prioritisation beyond the generic fact find declaration"*. The field should still not
be populated.

---

## 8. What a reviewer meets before the findings

### Citation trust halved, and prompt adherence is perfect

**77 untraceable quotes, in 34 of 82 rendered groups (41%)** — against Run 20's 162 in 61 of 83
(73%), and the best figure in the series alongside Run 16's 78. As in every prior run, **all 77 are
prose and not one is a table read**. The defect is the same (a table restated as a sentence, which
the assessor prompt forbids); Sonnet simply does it half as often. The measure continues to track the
model, not the findings.

**Prompt adherence: 0 of 88 responses echoed the wrong requirement id.** Against Haiku's 58–61 across
Runs 18–20, that is not an improvement, it is the elimination of the problem. Six responses rewrote
the requirement text, down from 12. Run 18 flagged this as a possible effect of the generalised
prompts and Run 20 confirmed it as persistent across three Haiku runs; Run 21 establishes it was
**never the prompts — it was the model.** The generalised prompts are exonerated.

**No group closed NOT ASSESSED**, against 8 in Run 20 and 15 in Run 18. Sonnet does not withhold on
missing inputs; it states what it can and qualifies it. That accounts for part of the recall gain
directly — F3.4 and F9.5 were both lost to withheld verdicts in earlier runs.

### The one serious false positive is a person-attribution error, and it is systematic

**The Fact Find's `Basic Income | Employed, CDH Recruitment | Monthly | £1,200.00` row is John's** —
he is the HGV driver employed by CDH Recruitment, and the benchmark's F1.1 turns on it. **G1.3 gets
this right.** Six other groups — **G1.4, G1.10, G1.11, G2.7, G2.8 and G5.2** — attribute it to Kim,
and build a confident finding on it:

> **[G1.4]** Kim's employment income: report states £1,100/month net, fact find states £1,200/month
> net — a £100/month difference exceeding 5% tolerance.
>
> **[G2.8]** The Kim income discrepancy (£1,100 vs £1,200) … **the most material finding.**

This discrepancy does not exist. Worse, G5.2 draws the mirror-image conclusion — *"these are Kim's
figures only (John's HGV/sea-cadets income is not in the fact find income table)"* — which is exactly
backwards. G1.2 makes the same class of error on the tax row (*"this entry appears to be for the
spouse (Kim)"* on `Highest rate of income tax paid 0%`, which is John's).

**This is a more damaging failure mode than anything in Run 20.** Run 20's comparable error was a
stray wrong number (*"a client aged 55"* in two groups, from a minimum-pension-age quote). Run 21's
error is a fabricated discrepancy, stated confidently, propagated across six groups and two checks,
and promoted to "the most material finding" in one of them. A reviewer following it would go looking
for a £100 error in Kim's income that is not there. Neither model's cross-check section caught it,
and the *Figures described differently in different checks* section prints the two contradictory
readings side by side without flagging them.

**Two smaller items.** The run uses "Alan" (the Fact Find's middle name, first/last names blank —
benchmark F1.5) in 17 places and "John" in 83 without reconciling them; and it uses age 69 (G2.2) and
age 70 (G10.5) without noting that the Fact Find contradicts itself (benchmark F1.4). Both are
unscored Moderate findings the run gets close to and does not make.

### One finding the benchmark does not have

G8.8 raises something no prior run has, and it looks real: the Standard Life policy document records
a **Leaver Discount of 0.4800%** which *"may not be available from other pension providers upon
transfer"*, and the report never mentions it. It also notes the Zurich documentation's statement that
*"life cover and waiver benefits are no longer available on new pension plans"* and that the report
does not address whether the Zurich plan carries any. The benchmark's F8.5 marks features-and-
guarantees-lost as *No issue*, on the switch report's *"No benefits were listed for the existing
products"*. **These are candidates for the benchmark rather than false positives**, and they should be
adjudicated by hand before the next scoring pass.

---

## 9. What this run settles, and what it does not

**Settled.**

1. **The remaining shortfall was reasoning, and a better model recovers most of it.** §5. On
   byte-identical packs, Sonnet recovered five findings Haiku left short, including two — F5.3 and
   F1.9 — where Haiku had the exact passage in the exact group and reasoned to the opposite
   conclusion. This closes the question Run 20 opened and Run 18 got wrong.
2. **The prompt-adherence regression was the model, not the generalised prompts.** §8. Three Haiku
   runs at 66–69% wrong-id echo; Sonnet at 0%. Run 18's open question is answered and the plans are
   cleared.
3. **F7.1 is Caught on both halves**, and it came from the extraction, not from plan routing. §3.
   Run 18's recommendation 3 is closed by a mechanism nobody proposed.
4. **Retrieval is fully deterministic on fixed plans, across models.** The Run 20 and Run 21 searched
   extracts differ by one header line. Retrieval can now be held constant while anything else varies,
   which makes every future model or prompt change measurable.
5. **The section hints are irrelevant, not merely dead.** §5. Four runs, two models, the same nine
   hints matching nothing, and the findings they were written for behaving independently of them.
6. **Sonnet costs 3.2× as much for +5 findings.** £9.93 against £3.11 all-in; £0.33 per finding
   against £0.12. §10.

**Not settled.**

7. **Whether the gain is 29, 30 or 31.** §6. Two judgement calls, one in each direction. The range
   clears 27 on every reading, which is the claim that matters, but the point estimate is not firm.
8. **Extraction versus assessor.** The experiment swapped both. §2 attributes four gains to the
   extraction by mechanism (new `internalInconsistencies` entries) and two to the assessor (identical
   packs, different conclusion), but a Sonnet-extraction/Haiku-checks run would settle it properly.
9. **The three remaining Missed.** F1.6, F5.4 and F4.2 survive a model upgrade. F1.6 is now *actively
   dismissed* rather than silently absent, which is worse. These are not capability-limited in the
   ordinary sense — the evidence is in the pack and the model is capable — so the next attempt has to
   be a prompt or plan change aimed precisely at them.
10. **Whether the person-attribution error is systematic to Sonnet or a one-run artefact.** §8. It
    appears in six groups on one run with unpinned sampling.

---

## 10. What to do next

1. **Adopt Sonnet for extraction; decide on the assessor separately.** §2, §7. The extraction gains —
   zero vocabulary violations, three new internal inconsistencies that map directly to findings, two
   triggers settled — cost £2.49 against £0.68 and are worth it on this evidence. **Run
   Sonnet-extraction with Haiku-checks next.** It is the single highest-value run available: it splits
   the two contributions, and if most of the gain is in the extraction it buys most of Run 21's recall
   at close to Run 20's cost.
2. **Fix the person-attribution error before anything else.** §8. A fabricated discrepancy promoted to
   "the most material finding" across six groups is a worse reviewer outcome than a missed finding,
   and the benchmark cannot see it. The Fact Find's income rows carry an owner; the assessor prompt
   should require the owner to be stated whenever an income figure is set against the report, and the
   cross-check section should flag two groups assigning the same row to different people.
3. **Attack F1.6/F5.4 as a dismissal, not an absence.** §5. G5.2 now *establishes* the clients rent
   and uses it to switch the guard off. The guard's logic is inverted: renting is the reason the
   report's *"incorporates the assets of your main residence"* claim is wrong, not the reason the
   guard does not apply. This is one clause in one guard and it has now cost nine runs.
4. **Correct the £117,998.47 slip.** §7. Add a derived-figure cross-check on any sum the extractor
   states against the arrangements it claims to sum.
5. **Adjudicate G8.8's Leaver Discount and Zurich waiver findings by hand.** §8. If they hold they
   belong in the benchmark, and F8.5's *No issue* needs revisiting.
6. **Stop populating `priorityRank`.** §7. Both models invent it; the report states no priority order.
   This is the same discipline item 3 gave the charge tables.
7. **Retire the nine dead section hints, or fix them with a dry run.** §5. Four runs, two models, no
   effect. They are noise in the footer and they give false assurance about four scored findings.

---

## 11. Limits

- **The experiment swapped two things at once.** Sonnet ran both the extraction and the checks. §5's
  reasoning-versus-retrieval conclusion is robust (identical packs, different conclusions from the
  same passages), but the split between extractor and assessor in §2 rests on mechanism — which
  `internalInconsistencies` entries exist — not on a controlled measurement. Recommendation 1 closes
  this.
- **Sampling is unpinned on both runs.** Run 20 and Run 21 are one sample each. The +5 exceeds the ±2
  noise floor, and the finding-by-finding movement has a mechanism for six of the seven changes, but
  it is still one pair.
- **F10.2 and F3.1 are the judgement calls**, in opposite directions, and both are argued in §4 and
  §6. Range 29–31.
- **The benchmark may be short two findings.** §8 — G8.8's Leaver Discount and the Zurich waiver
  benefits. If those are real, Run 21's recall is measured against an incomplete answer key and every
  prior run's is too.
- **This dataset still cannot measure false positives at check level.** Every check is expected to
  land on Potential Concern and 82 of 88 requirements did. The person-attribution error in §8 was
  found by hand. The control case in [control-case-spec.md](../../control-case-spec.md) remains the
  missing instrument, and Run 21 is the strongest argument yet for building it: the run that scores
  best on recall also produces the most confidently-stated wrong finding in the series.
- **Adjudication is one pass**, ±2 findings. F3.1 and F10.2 are the two most likely to move on a
  second reading; F1.3 is the third, because its Kim half rests on the mis-attribution in §8 while
  its Sea Cadets half and its £1,430 total are stated correctly.
