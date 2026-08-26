# Run analysis — Run 15

Case ABC-99, 2026-08-26 21:47, scored against
[expected-results-benchmark.md](expected-results-benchmark.md) under
[scoring-rubric.md](scoring-rubric.md), and compared with
[Run 14](Runtime-Logs/latest/Run-14/run-analysis.md),
[Run 13](Runtime-Logs/latest/Run-13/run-analysis.md),
[Run 12](Runtime-Logs/latest/Run-12/run-analysis.md),
[run-analysis.md](run-analysis.md) (Runs 7–9) and
[run-analysis-replays.md](run-analysis-replays.md) (Runs 10–11).

*This file previously held the Runs 10–11 replay analysis. That analysis is unchanged and now
lives at [run-analysis-replays.md](run-analysis-replays.md); every document that cited it has been
relinked. Nothing was discarded.*

---

## The result

**Sonnet 4.6 has finally run on the current plans, and a single wrong boolean took a whole check
off the board before it started.** Run 15 scores **25 / 7 / 4** — with CHK-005 never executed.

| | Run 12 | Run 13 | Run 14 | **Run 15** |
| --- | --- | --- | --- | --- |
| Chat model | Haiku 4.5 | Haiku 4.5 | Haiku 4.5 | **Sonnet 4.6** |
| `maxSearchResults` / `maxPassagesPerGroup` | 8 / 24 | **0** / 24 | 8 / **0** | 8 / **0** |
| Canonical model | `8a0120d90ea2` | `a2bbc38bdf38` | `a2bbc38bdf38` | **`cba52b288e54`** |
| Groups run | 85 | 85 | 85 | **77** |
| Retrieval | 229 / 3,612 | 229 / 130,993 | 229 / 3,612 | **203 / 3,196** |
| Canonical paths resolved / absent | 224 / 31 | 224 / 31 | 224 / 31 | **197 / 38** |
| **Caught / Partial / Missed** | **27 / 5 / 4** | 19 / 11 / 6 | 23 / 9 / 4 | **25 / 7 / 4** |
| Recall | **75%** | 53% | 64% | **69%** |
| Chat cost | £2.13 | £2.20 | £2.34 | **£7.12** |
| Wall clock | 325s | 259s | 145s | **552s** |

**Three of the four Missed findings — F5.2, F5.3, F5.4 — belong to the check that did not run.**
On the 32 findings Run 15 was actually allowed to look for, it caught 25 and missed one.

---

## 1. The finding that matters most

**`hasCapitalContributionsOrWithdrawals: false` disabled CHK-005.**

```
CHK-005 — Financial Resilience, Affordability and Liquidity
Outcome: N/A

  Trigger absent: The recommendation involves no capital, contributions, withdrawals or charges..
  Canonical model checkTriggers.hasCapitalContributionsOrWithdrawals = false.
```

The recommendation involves a **£3,305.55 initial adviser charge deducted from the fund**, a
**Scottish Widows encashment** yielding ~£170, and **flexi-access drawdown**. The same extraction
set `hasDecumulation: true` and `hasCostsAndChargesSection: true` in the same JSON object. The
trigger is not a borderline call; it is wrong on its face.

**One boolean removed 8 groups, 26 searches and four benchmark findings**, and the report says so
in a single line under *CHECKS CLEARED* — a heading that reads as reassurance. A reviewer skimming
the output sees nine concerns and one cleared check, not a check that was never attempted.

### Where the false trigger came from

The extraction log tells the story in its timings:

```
[ 1/12] Case and parties                    (0.3s)     ← replayed from cache
[ 2/12] … [ 8/12] Recommendations           (0.1–0.2s) ← replayed from cache
[ 9/12] Costs and charges                   (200.8s)   ← freshly generated
[10/12] Replacement analysis                (200.7s)   ← freshly generated
[11/12] Disclosures                         (200.8s)   ← freshly generated
[12/12] Triggers and extraction report       (47.4s)   ← freshly generated
```

**`checkTriggers` lives in section 12, and section 12 was generated fresh.** Sections 1–8 came back
from the gateway cache in a tenth of a second each; sections 9–11 took an identical 200.7–200.8s,
which is the shape of a timeout-and-retry, not of three coincidentally equal generations.

That partial-cache boundary also explains the second number in the table: **197 canonical paths
resolved against 224 in every previous run, and 38 absent against 31**. The fresh canonical model
is not merely different from `a2bbc38bdf38` — it is **poorer**. Twenty-seven paths the checks asked
for are no longer there.

**No previous run has had a check silently skipped**, and nothing in the pipeline treats a trigger
flip as an event worth flagging. `RunAuthenticity` catches a replayed *run*; nothing catches a
partially replayed *extraction* producing a model that answers fewer questions than the last one.

---

## 2. The second finding: the report is a quarter of its own evidence

**G3.2 corroborated the report against the report.**

> The income and expenditure figures are corroborated by **[P11]**.
>
> · **Suitability Report Test 1.md [I] (P11)** — *"You are in receipt of a full State Pension of
> approximately £230 per week, continue to work as an HGV driver earning around £300 per week net,
> and receive an additional £20 per week from your role with the Sea Cadets. Kim contributes a
> further £1,100 per month…"*

Every figure in that "corroboration" is one of the two the benchmark says the Fact Find **does not
record** (F1.3) and the one it records at a different frequency (F1.2). The passage cited as file
evidence *is the assertion under test*.

This is not a Run 15 defect. It is structural, and it has been there all along:

| | Run 12 | Run 14 | **Run 15** |
| --- | --- | --- | --- |
| Cited passages, total | 592 | 548 | **620** |
| …from the suitability report | 162 (27%) | 159 (29%) | **162 (26%)** |
| …from the Fact Find | — | — | **42 (7%)** |

**The suitability report is the single most-cited evidence document in the pack, quoted nearly four
times as often as the Fact Find.** The canonical-model/evidence split exists precisely to keep the
report on the assertion side of the comparison, and the retrieval corpus puts it back on the
evidence side under category `[I]`.

Run 13 was scored down for one self-corroboration and it was attributed to unbounded search
(*"unbounded retrieval makes it easier for a report to corroborate itself"*). **That attribution
was too narrow.** Run 15 does it at `maxSearchResults: 8`, on a different model, and the share of
report-sourced passages is flat across all three runs. Unbounded search made an existing hazard
more likely to fire; it did not create it.

**This is the most actionable defect in the run**, and it is a retrieval-corpus question, not a
prompt question: category `[I]` should not be reachable by a group's evidence searches.

### A related smell in the same census

Three filename forms for the same documents appear in the citations —
`Risk profile report.md [D]` (34) and `D/Risk profile report.docx [D]` (35);
`Suitability Report Test 1.md [I]` (137), `I/Suitability Report Test 1.md [I]` (17) and
`I - Suitability Report Test 1 [I]` (8). **The index holds more than one ingest of the same
document.** That inflates candidate sets, wastes reserved slots on duplicates, and is worth an hour
with the store before any further retrieval tuning.

---

## 3. Scores

Verdicts under the rubric, in benchmark order. **No verdict in this run rests on R8** — every
Caught is stated inside a group that raised a concern.

| Finding | R12 | R13 | R14 | **R15** | Where it landed |
| --- | :-: | :-: | :-: | :-: | --- |
| F1.1 £300/wk vs £1,200 | C | C | C | **C** | G1.3 — *"£1,200/month equates to approximately £276.92/week, not £300/week"* |
| F1.2 State Pension weekly vs monthly | C | C | C | **C** | G1.2, G1.4 |
| F1.3 Kim and Sea Cadets absent | C | M | C | **C** | G1.4 — FF holds two income lines only; £1,430 total *"does not reconcile"* |
| F1.6 residence for a tenant | M | M | M | **M** | evidence reached CHK-008 again, §4 |
| F1.8 pension totals | C | C | C | **C** | G1.8 — £110,000 / £106,000 / £116,998.47 |
| F1.9 plans absent from the fact find | P | M | M | **P** | G1.7 — FF policy does *"not correspond"*, absence not named |
| F2.1 five horizons | C | C | C | **C** | G2.2 — 71, 66, 2yr, 75, 65 |
| F2.2 CFL "<3 years" | M | M | M | **P** | **↑** G2.4 names *Short Term*, never joined to the risk-5 case |
| F3.1 file note says 6 | C | C | C | **C** | G3.6, verbatim the rubric's worked example |
| F3.3 Investment Period 2 years | C | C | C | **C** | G3.4 — the strongest single group in the run |
| F3.4 People's Pension risk 9 | P | M | P | **P** | G3.7 — risk 9 named, retention link still absent |
| F3.5 cautious answers, natural 4 | C | P | P | **C** | **↑** G3.10 — *Cautious*, *More cautious*, £4,000 on £20,000 |
| F4.1 "No understanding / knowledge" | C | C | C | **C** | G4.1 |
| F4.2 ATR narrative sentence | P | P | P | **P** | |
| F4.3 Balanced precondition | C | C | C | **C** | G4.1 discrepancy 2 |
| F4.4 no comprehension tested | P | P | P | **C** | **↑** G4.5 — *"no record of client questions… no demonstration of comprehension"* |
| F5.1 −£288 | C | C | C | **P** | **↓** G2.7 has −£288; *"comfortably meets"* sits unjoined in G3.2 |
| F5.2 affordability | C | C | C | **M** | **↓↓ false negative — §2** |
| F5.3 emergency fund spent | C | P | P | **M** | **↓** `LIQUIDATED` appears nowhere |
| F5.4 rented residence | M | M | M | **M** | |
| F6.1 ranking above the advice | C | C | C | **C** | CHK-009 — *"ranks the new solution 5th of 9… below the existing solution"* |
| F6.2 boilerplate rationales | C | P | C | **C** | CHK-006 — *"verbatim identical across all three"* |
| F6.3 "Lower Costs" raises them | C | P | P | **C** | **↑** *"on the Standard Life transfer the report's own table shows cost increases (+0.26%)"* |
| F7.1 two charge tables | C | C | C | **P** | **↓** 0.18% / 0.52% listed side by side, never joined; §5 |
| F7.2 row on Zurich's fund | M | C | C | **C** | G1.8, G7.4 |
| F7.3 4.24% adds instead of subtracts | P | P | P | **P** | reached and waved through — *"they measure different things"* |
| F7.5 critical yield | C | C | C | **C** | G7.6, CHK-009 |
| F8.1 £12,000 in the appendix | C | P | C | **C** | G8.5 — *"appear only in the appendix (page 18)"* |
| F8.2 ranking never disclosed | C | C | C | **C** | CHK-009 |
| F9.1 switch reduces maturity | C | P | P | **C** | **↑** |
| F9.2 ranking above recommendation | C | C | C | **C** | |
| F9.3 Standard Life on cost | C | P | P | **C** | **↑** |
| F9.4 Zurich £128,000 | C | C | C | **C** | *"ranked 1st, £128,000… no analysis explaining why"* |
| F9.5 charges unreliable | C | C | C | **C** | p8-vs-p11, report-internal |
| F10.1 FG21/1 capability | C | P | C | **C** | |
| F10.2 no screening | C | C | C | **C** | |
| **Caught** | **27** | 19 | 23 | **25** | |
| **Partial** | 5 | 11 | 9 | **7** | |
| **Missed** | 4 | 6 | 4 | **4** | |

**Against Run 12: seven gains, five losses.** Four of the five losses (F5.1, F5.2, F5.3 and part of
F7.1) are CHK-005 being switched off. **Every one of the seven gains is Sonnet.**

---

## 4. What is a model difference and what is not

Retrieval is plan-driven and model-free, and Run 15 confirms it again: the pack is **median 23 /
mean 26.1 / max 62** against Run 14's 24 / 27 / 62 on the identical configuration. Same searches,
same ranking, different model. So the deltas below are the model.

| | Haiku (R12 / R14) | **Sonnet (R15)** |
| --- | --- | --- |
| Responses echoing the **wrong requirement id** | **32 / 85** and **36 / 85** | **0 / 77** |
| Quotes that could not be traced to the evidence | 136 / 132 | **85** |
| Passages cited per group (median) | 7.0 | **8.0** |
| Pack utilisation | ~26% | **~33%** |
| Output rate | 466 / 1,040 tok/s | 288 tok/s |
| Cost per finding caught | **£0.079** | £0.285 |

**The requirement-id figure is the one worth stopping on.** Haiku echoed the wrong requirement id
in roughly two responses in five, across three runs, consistently. Sonnet did it **zero times in
77**. The pipeline substitutes the plan's own values so no output is corrupted — but a model that
misidentifies which requirement it is answering two-fifths of the time is telling you something
about how firmly it is holding the question, and that has never been isolated before.

**Sonnet also reads more of the pack** (33% against 26%) and **produces a third fewer untraceable
citations**, both matching the Runs 7–9 measurements. Nothing here is new about Sonnet; what is new
is that it is now measured on the current plans.

### The measurement optimal-configuration §1c asked for

That section closed by saying **Sonnet had never run on the current plans**, that its 28 was from
the old ones, and that *"if the +7 that plan and prompt work gave Haiku carries across, Sonnet lands
near 33–35 and the default recommendation may be wrong."*

**It does not carry across.** Sonnet scores 25 with CHK-005 disabled; scoring only the 32 findings
it could reach, it caught 25 of 32 (78%) against Haiku's 24 of those same 32 in Run 12. That is
**one finding**, inside the noise floor.

**The Haiku default stands, and it now stands on a measurement rather than an inference.** Sonnet
costs 3.3× more, runs 3.8× longer, and buys about one finding — but it buys **no wrong requirement
ids and a third fewer unverifiable quotes**, which is exactly the *"has to be right first time"*
case §1c already described. That recommendation was right for a reason it had not measured.

### F1.6, a fourth time

`'Tenant - private'` reached an assessor again — this time in **CHK-008's** vulnerability overlay:

> The fact find (P35) records the client as **a private tenant**, aged approximately 69 …

The word "tenant" appears **twice** in the whole output: there, and in the report header. Neither
F1.6 nor F5.4 is made, and F5.4's own check never ran.

**Run 14 established F1.6 as a verification failure on Haiku with an unbounded pack. Run 15
establishes the same on Sonnet.** The evidence is present, the report's claim is present, three
guards ask for the comparison, and the best model available does not make it either. It is not a
capability problem and it is not a retrieval problem.

---

## 5. The one loss that is not CHK-005

**F7.1 fell from Caught to Partial, and it fell in an instructive way.** G7.4 wrote:

> [P1] confirms existing arrangement charges: Aviva 0.50%, **Standard Life 0.18% / 0.52%**.

Both halves of the benchmark finding, in one sentence, with nothing relating them — the rubric's
own definition of Partial. The group then went on to compare the 0.18% against the *Standard Life
illustration's* 0.5% instead, which is a different contradiction reached from a different document.

Sonnet found a **better** route to the same conclusion (the wrong-fund arithmetic, F7.2, which it
caught) and in doing so walked past the transposed-table finding sitting in its own `fileSays`.
**This is R4 working as intended, and it is the failure mode a stronger model makes more likely,
not less**: the more routes a model can see, the more freely it abandons the one the benchmark
names.

---

## 6. What to do next

1. **Fix `hasCapitalContributionsOrWithdrawals`, and make a trigger flip loud.** A check that did
   not run should not print under *CHECKS CLEARED* in the same voice as a check that passed, and a
   canonical model resolving 27 fewer paths than the previous one should be reported at the head of
   the run, not inferred from a log.
2. **Take category `[I]` out of the evidence corpus.** 26% of cited passages are the report
   corroborating itself, in every run measured. This is the single change with the clearest
   mechanism behind it, and §2 shows it is not a Run 13 artefact.
3. **De-duplicate the index.** Three filename forms for the same document are inflating every
   candidate set.
4. **Re-run Run 15's configuration on a clean canonical model** before drawing any conclusion about
   Sonnet from the 25. The model comparison in §4 is sound because retrieval is identical; the
   *score* comparison carries a disabled check.
5. **`maxPassagesPerGroup: 24` still stands.** Run 15 adds nothing for or against — it used the
   unbounded pack, and the median pack was 23.
6. **F3.4 still needs the retention link**, unchanged from the Run 12, 13 and 14 recommendations.
   Sonnet names the People's Pension and its risk 9 and still stops at incomparable scales.
7. **The four dead section hints** (`ranked by Maturity Value`, in G2.2 / G6.4 / G8.8 / G9.7) have
   now been reported identically in four consecutive runs and never fixed. F6.1, F8.2 and F9.2 are
   caught in spite of them, which is why nobody has.

---

## 7. Limits

- **Two variables moved against Run 12**: the model and the canonical model — and the canonical
  model change is not a neutral one, it disabled a check and lost 27 paths. **No single-variable
  comparison exists in this run.** The §4 model comparison is exempt because retrieval is
  demonstrably identical.
- **The 25 is not comparable to Run 12's 27** without stating that four findings were unreachable.
  On the 32 reachable findings the two runs differ by one, which is inside the floor.
- **Adjudication is one pass**, ±2 findings. Every delta discussed here except the CHK-005 losses
  is at or inside that.
- **Sampling still unpinned** — `seed not pinned` on the run's own face, as on every run in this
  series. One sample.
- **F5.1's downgrade is a judgement call.** G2.7 states the −£288 deficit and says the report does
  not address it; it does not quote *"comfortably meets"*, which sits in G3.2. Scored Partial on
  the rubric's first bullet. A reader who applies R2 more loosely reaches 26 / 6 / 4.
