# Run analysis — Run 20

Case ABC-99, 2026-08-28 11:58, scored against
[expected-results-benchmark.md](../../expected-results-benchmark.md) under
[scoring-rubric.md](../../scoring-rubric.md), and compared with
[Run 18](../Run-18/run-analysis.md), Run 19 (unscored — same plans, see §2),
[Run 17](../Run-17/run-analysis.md), [Run 16](../Run-16/run-analysis.md),
[Run 12](../Run-12/run-analysis.md).

---

## The result

**Run 20 scores 25 of 36 — two below Run 18's 27, which is inside the ±2 noise floor and is not
by itself evidence of anything. The composition, however, moved a long way: three findings rose
and five fell.** Two of the three that rose — **F3.1 and F3.4 — had never both been Caught in the
same run**, and F3.4 had never been Caught at all. The five that fell are all in areas that were
solid for several runs.

The finding that matters most in this run is not in the score. It is in
`retrieval-extract_ABC-99_20260828-115314.txt`, which is the first artefact in the series that
shows *which passages reached which assessor's pack*. **Every one of the four Missed findings had
its evidence in the pack of a group that should have made it.** Run 18 §5 and §7 attributed the
same shortfall to retrieval and recommended `RetrievalDryRun`. That diagnosis is wrong, and this
run's own evidence refutes it. §5.

| | Run 12 | Run 16 | Run 17 | Run 18 | Run 19 | **Run 20** |
| --- | --- | --- | --- | --- | --- | --- |
| Chat model | Haiku 4.5 | Sonnet 4.6 | Haiku 4.5 | Haiku 4.5 | Haiku 4.5 | **Haiku 4.5** |
| `maxSearchResults` / `maxPassagesPerGroup` | 8 / 24 | 8 / 24 | 8 / 24 | 8 / 24 | 8 / 24 | **8 / 24** |
| Plans digest | `20ac8729…` | `20ac8729…` | `3a88a90b…` | `862608be…` | `09cf0a31…` | **`09cf0a31…`** |
| Canonical model | `8a0120d9…` | `1b33706f…` | `e9934ca9…` | `d0559004…` | `2773d42d…` | **`fbc4e932…`** |
| Extraction | cached | cached, 2s | live, 497s | live, 465s | live, 601s | **live, 210s** |
| Extraction cost | — | — | — | — | £0.7003 | **£0.6800** |
| Retrieval (searches / passages) | 229 / 3,612 | 203 / 3,196 | 235 / 3,708 | 236 / 3,724 | 236 / 3,724 | **236 / 3,724** |
| Canonical paths resolved / absent | 224 / 31 | 197 / 38 | 239 / 38 | 237 / 48 | 242 / 43 | **250 / 35** |
| Requirements printed as concerns | — | — | — | 84 | 84 | **83 of 88** |
| **CHK-005** | ran | N/A | ran | ran | ran | **ran** |
| **Findings reachable** | 36 | 32 | 36 | 36 | 36 | **36** |
| **Caught / Partial / Missed** | 27 / 5 / 4 | 25 / 6 / 5 | 23 / 7 / 6 | **27 / 5 / 4** | not scored | **25 / 7 / 4** |
| **Recall (Caught / 36)** | 75% | 78% *(25/32)* | 64% | **75%** | — | **69%** |
| Citation trust (untraceable quotes) | 136 | 78 | 146 | 195 | 155 | **162** |
| Prompt adherence (wrong id / rewrote) | — | — | 45 / 4 | 59 / 10 | 58 / 4 | **61 / 12** |
| Chat cost | £2.13 | £6.60 | £2.25 | £2.33 | £2.24 | **£2.43** |
| Wall clock (checks) | 325s | 551s | 283s | 300s | 278s | **314s** |

```
Caught 25 / Partial 7 / Missed 4     (Run 18: 27 / 5 / 4 · Run 12: 27 / 5 / 4 · Run 17: 23 / 7 / 6)
```

**The noise floor is ±2 findings, ±5 points.** The −2 against Run 18 sits exactly on it. The
**Missed count of 4 is unchanged**, and the benchmark's own instruction is that the gate should sit
on missed-rate, not on overall agreement. On that measure Run 20 is level with the two best runs in
the series.

---

## 1. Establishing the run is genuine

Run 20 passes every authenticity test on both phases.

**Checks phase.** 314 seconds for 179,272 output tokens is **572 tok/s**, inside the 260–576 tok/s
band of genuine runs on this case and two orders of magnitude below the 15,897–33,858 tok/s that
identified the Run 10/11 replays. No `*** REPLAY ***` banner. Per-check wall-clock lines run
34.4–151.2s across the ten checks.

**Extraction phase.** A live 210.2-second extraction, cache bypassed by run marker
(*"Response cache bypassed — every section carries a run marker"*), no section sub-second, and the
canonical model digest `fbc4e932966a` is new to this run. The 210s is **2.9× faster than Run 19's
601s** on the same plans, which is commit `3ab2209` (*"Run the extraction passes in dependency waves
rather than one at a time"*) landing — the section timings show wave structure, with sections 4–9
all sharing a 66.5s completion and 10–11 sharing 64.6s.

---

## 2. What changed, and why Run 19 is the right comparison

**Run 19 and Run 20 carry the identical plans digest `09cf0a31b103`.** No query plan changed between
them. The differences are confined to code and prompts:

- `3ab2209` — extraction runs in dependency waves. Extraction 601s → 210s, cost £0.7003 → £0.6800.
- `53dd8f0` — mechanical check triggers settled in code, not asked of the model. Run 19 reported
  `hasComplexProduct` **false and contradicted by the model** (assessed anyway, nothing lost);
  Run 20 reports it **settled to true** from the model. The defect Run 19 flagged is closed.
- Vocabulary violations fell from **19 to 2**. Only `modality: "Inferred"` and
  `feeChargingType: "PercentOfAum"` remain outside the documented vocabulary, against a Run 19 list
  that included nine separate invented `basis` strings. This is a substantial extraction-quality
  gain and it is not visible in the recall score at all.

**Retrieval is now deterministic across the pair.** Run 19 and Run 20 both report exactly
**236 searches, 3,724 passages, 1,838 of 2,341 reaching an assessor, 503 (21%) dropped**. Identical
to the passage. Only canonical-path resolution differs (242/43 vs 250/35), which follows the
different canonical model. This is the first evidence in the series that the retrieval stage is
reproducible given fixed plans, and it means a Run 19 ↔ Run 20 delta isolates the *extraction and
the assessor*, not the search.

**Run 19 is not scored here.** Adjudicating it in full is a second 36-finding pass and was out of
scope; the spot checks in §6 are marked as spot checks and should not be read as a score.

**Against Run 18 the comparison is not single-variable** — plans, canonical model, extraction
strategy, trigger handling and code all differ, and sampling is unpinned on both
(`seed not pinned`).

---

## 3. F3.4 is Caught for the first time in the project's history

The benchmark finding: the People's Pension is *retained* while invested in a fund rated 9 against
an agreed rating of 5, and the report asserts that fund aligns with the client's risk appetite.
It was Missed in Run 17, Partial in Runs 12, 16 and 18, and never Caught.

**G3.11 states it outright:**

> The report asserts the People's Pension (EA1) is retained with advice 'Retain this scheme as the
> workplace pension arrangement with ongoing contributions.' The fund held is 'Global Investments
> (up to 85% shares)' with a stated risk rating of 9. The agreed client risk rating is 5 …
> **no explanation is offered for how a risk 9 fund remains suitable when the client's agreed
> profile is risk 5** … either value represents a significant divergence from the agreed rating of
> 5 … **This is a material gap in suitability documentation.**

Both sides named, the conflict stated, and the group closes **Potential Concern**, not NOT ASSESSED.
This is precisely the step Run 18 §4 identified as missing: *"the assessor withholds the finding on
the undefined scale rather than concluding the report's claim is unsupported."* In Run 20 the
assessor carries the guard's second clause through — it notes the scale is undefined, cross-checks
the KIID (which rates the fund 6 on a 1–7 scale), and concludes that **either reading diverges from
5**, which is the reasoning the guard was written to produce.

Recommendation 2 from Run 18's *What to do next* has landed, and it landed as a prompt-adherence
improvement with no plan change — the plans digest is unchanged from Run 19.

---

## 4. F3.1 is Caught again, in a group nobody aimed at it

`Risk rating of 6` has appeared in the reasoning of exactly two runs: Run 12 and now Run 20. In
Run 17, 18 and 19 it appeared nowhere in the reasoning (in Run 19 it appears only as a citation
quote, never in a discrepancy or narrative).

**G6.7 — *"Recommendations that present multiple possible outcomes without stating which is
recommended and why"* — makes it:**

> File notes state risk rating agreed at 5 but P3 also contains text 'You are happy to proceed with
> a Risk rating of 6' which contradicts the agreed level of 5 shown elsewhere in the same passage.

Both sides, the conflict named. **Caught under R1** (a finding may be caught under any check).

**The two hints written for it still fired on nothing.** `G3.6: ATR Wording` and `G3.8: ATR Wording`
are in the *"Section hints matching nothing"* line for the third consecutive run. And the retrieval
extract confirms why the hint is irrelevant here: the file-note chunk carrying `Risk rating of 6`
reached the packs of **G2.1, G2.7, G4.5, G4.7, G6.7, G9.9, G10.3 and G10.5** — and **not** G3.6 or
G3.8. The finding was caught by a group that was never asked to look for it, on a passage an
ordinary search happened to rank.

That is the same mechanism as the four `ranked by Maturity Value` hints, which have now matched
nothing for **eight consecutive runs** while F6.1, F8.2, F9.2 and F9.4 are Caught regardless. The
extract shows the ranking table reaching G8.3, G8.7, G9.1, G9.6, G9.8 and G9.9 packs by search
alone. **Four scored findings rest on a search ranking that nothing guarantees**, and this run
proves it rather than infers it.

---

## 5. The Missed findings are reasoning failures, not retrieval failures — and this is now proved

This is the run's most important result, and it reverses the conclusion of Run 18 §7.

The searched extract records, for every requirement, which retrieved passages reached the
assessor's pack and which were evicted. Mapping the benchmark's own evidence strings through it:

| Evidence string | Reached a pack in | Verdict |
| --- | --- | --- |
| `main residence` | G1.1, G1.3, **G1.4**, G1.5, **G1.6**, G1.11, G2.3, G2.4, G2.6, G2.8, **G5.2**, G8.3, G10.4 | **F1.6 / F5.4 Missed** |
| `Residence Nil Rate` | **G1.4**, **G1.6**, G2.4, G8.3 | **F1.6 Missed** |
| `Tenant` | G1.1, G1.5, **G5.2**, G2.4, G2.7, G6.9, G7.2, G7.7, G7.9, G8.1, G10.1, G10.2, G10.5 | **F1.6 / F5.4 Missed** |
| `Less than 3 years` | **G2.2**, G3.1, G3.2, G3.3, G3.10, G4.1, G4.2, G4.4, G10.1, G10.4 | **F2.2 Partial** |
| `no understanding of investments` | G3.1, G3.2, **G3.6**, G3.7, G3.8, G3.10, **G4.1**, **G4.2** | **F4.2 Missed** |
| `LIQUIDATED` | G1.6, G2.4, G5.1, **G5.5**, G8.4 | **F5.3 Partial** |

**Both halves of F1.6 arrived in the same pack.** G1.4 and G1.6 each received a passage containing
`main residence` *and* a passage containing `Residence Nil Rate`, and G5.2 received `Tenant`. G5.2's
narrative even prints the fact — *"The Fact Find records the client as a 'Tenant - private'"* — and
uses it as context for a vulnerability sentence rather than setting it against the cashflow's claim
to incorporate the assets of a main residence. `main residence` does not appear in the rendered
output once, in 2,785 lines, despite reaching thirteen packs.

**F4.2's exact sentence reached G4.1 and G4.2 and was not used.** Those two groups caught F4.1
cleanly off the Investor Experience answers in the same pack.

**F5.3 was reached, quoted, and reasoned away.** G5.5 prints the table
(`Savings | £6,000 | LIQUIDATED 2ND`), invokes the guard written for it — *"A cashflow that
schedules the emergency fund to be spent has not retained it"* — and then concludes the opposite:
*"This decline is minimal and does not indicate that the savings account is being liquidated
year-on-year."* Reached and withdrawn, which is Partial.

**What this settles.** Run 18 §7 said *"Three of the four Missed are retrieval failures, not
reasoning failures … the passages are not reaching the groups that need them"*, and Run 18's first
recommendation was a `RetrievalDryRun` on the misfiring hints. The searched extract shows the
passages *are* reaching the groups that need them. The section hints did not fire, but the hints
were never the binding constraint — ordinary search delivered the evidence anyway. **A dry run on
those five hints would confirm the hints are dead and would not move a single finding.** The work
belongs in the assessor prompt, not in retrieval.

The one place retrieval is genuinely implicated is F3.1's *intended* route: `Risk rating of 6` did
not reach G3.6 or G3.8, only eight other groups. But since G6.7 caught it, even that is a
reliability problem rather than a recall one.

---

## 6. Scores

Verdicts under the rubric, in benchmark order. **No verdict rests on R8.**

| Finding | R12 | R17 | R18 | **R20** | Where it landed in R20 |
| --- | :-: | :-: | :-: | :-: | --- |
| F1.1 £300/wk vs £1,200 | C | C | C | **C** | G1.10 — *"£300 per week = £15,600 annually … ≠ £1,200 monthly"* |
| F1.2 State Pension weekly vs monthly | C | C | C | **C** | G1.10 — *"report £230 per week; Fact Find £230 per month … incompatible"* |
| F1.3 Kim and Sea Cadets absent | C | C | C | **C** | G1.10 — both named as unsupported against Fact Find £1,430 |
| F1.6 residence for a tenant | M | M | M | **M** | `main residence` absent from output; evidence in 13 packs — §5 |
| F1.8 pension totals | C | C | C | **P ↓** | £116,998.47 set only against charge-derived £110,185/£115,195; the report's own p3/p4 totals never engaged (R4) |
| F1.9 plans absent from the fact find | P | P | C | **M ↓** | G1.7 compared the five plans to *provider* documents and passed them as consistent; the Fact Find's Existing Pension Provision is never checked |
| F2.1 five horizons | C | C | C | **C** | G2.2 — 2027/2yr, 66, 71, 75, 2-year investment period named as conflicts |
| F2.2 CFL "<3 years" | M | P | P | **P** | risk-profile 2-year route again (R4); `Less than 3 years` reached 10 packs unused — §5 |
| **F3.1 file note says 6** | C | M | M | **C ↑** | **G6.7** — file note 6 vs agreed 5, conflict stated — §4 |
| F3.3 Investment Period 2 years | C | C | C | **C** | G3.4 (High) — *"Report asserts 10 years; [P3] states 'Investment Period: 2 years'"* |
| **F3.4 People's Pension risk 9** | P | M | P | **C ↑** | **G3.11** — retained, risk 9, agreed 5, *"material gap"* — §3 |
| F3.5 cautious answers, natural 4 | C | C | C | **C** | G3.6 / G4.4 / G4.7 |
| F4.1 "No understanding / knowledge" | C | C | C | **C** | G4.1 — six discrepancy lines against the report's *"some experience"* |
| F4.2 ATR narrative sentence | P | M | M | **M** | sentence reached G4.1/G4.2 packs, unused — §5 |
| F4.3 Balanced precondition | C | C | C | **C** | G4.4 — precondition quoted and contradicted |
| F4.4 no comprehension tested | P | C | C | **C** | G4.5 / G4.7 |
| F5.1 −£288 | C | C | C | **C** | CHK-005 summary + G5.4 — *"expenditure exceeds income by £288 per month"* vs *"comfortably meets"* |
| F5.2 affordability | C | C | C | **C** | G5.2 — phantom components itemised, *"The income reconciliation fails"* |
| F5.3 emergency fund spent | C | P | C | **P ↓** | G5.5 quoted `LIQUIDATED 2ND`, invoked the guard, concluded against it — §5 |
| F5.4 rented residence | M | M | M | **M** | `Tenant` printed in G5.2 as context only — §5 |
| F6.1 ranking above the advice | C | C | C | **C** | G9.9 — 5th (£125k) vs 2nd (£127k), *"not explained"* |
| F6.2 boilerplate rationales | C | C | C | **C** | G6.1 *"stated identically verbatim"*, G6.5 *"appear verbatim … boilerplate"* |
| F6.3 "Lower Costs" raises them | C | C | C | **P ↓** | the +0.26% / £268.94 increase (G9.2, G7.4, G7.6) and the *"Lower Costs"* rationale (G6.5, G7.7, G9.8) are never joined in one group; the run treats £268.94 as an *arithmetic error* (F7.2) rather than a real increase |
| F7.1 two charge tables | C | P | C | **C** | G1.8, G7.3, G7.5, G9.9 — SL 0.52% (p5) vs 0.18% (p11), both tables named |
| F7.2 row on Zurich's fund | M | C | C | **C** | G7.3/G7.4/G7.6 — *"£268.94 at 0.26% implies £103,438.46, which is Zurich's value"* |
| F7.3 4.24% adds instead of subtracts | P | M | P | **P** | G7.6 engages both figures; the sign error is never named, `3.76` absent from the output |
| F7.5 critical yield | C | C | C | **C** | G7.5 — 4.8% *"exceeds the high growth rate (5.88%)… unlikely to break even"* |
| F8.1 £12,000 in the appendix | C | P | C | **C** | G9.1 (−£12,000 / −9.4%) + G8.5 (*"prominence hierarchy favours benefits over risks"*) + G6.4 |
| F8.2 ranking never disclosed | C | C | C | **C** | G9.9 / G9.4 — *"no explicit statement … that the option of doing nothing was considered"* |
| F9.1 switch reduces maturity | C | C | C | **C** | G9.5 — *"£97,600 vs £108,000 low; £115,000 vs £127,000 mid"* |
| F9.2 ranking above recommendation | C | C | C | **C** | G9.8 / G9.9 |
| F9.3 Standard Life on cost | C | C | C | **P ↓** | same split as F6.3 |
| F9.4 Zurich £128,000 | C | C | C | **C** | G9.8 — *"Zurich £128,000 (1st) vs New Solution £125,000 (5th)"* |
| F9.5 charges unreliable | C | P | P | **C ↑** | **G9.9** — the two tables named and *"undermining the cost-benefit comparison"*; benchmark's own route |
| F10.1 FG21/1 capability | C | C | C | **C** | G10.4 / G10.5 |
| F10.2 no screening | C | P | P | **P** | G10.5 names age 69 and the absence of screening; the scale of the transfer is still not stated |
| **Caught** | 27 | 23 | 27 | **25** | |
| **Partial** | 5 | 7 | 5 | **7** | |
| **Missed** | 4 | 6 | 4 | **4** | |

**Movement against Run 18: three up, five down.**

- **Up:** F3.1 (M→C), F3.4 (P→C), F9.5 (P→C).
- **Down:** F1.8 (C→P), F1.9 (C→M), F5.3 (C→P), F6.3 (C→P), F9.3 (C→P).

**The five that fell have a common shape.** In four of them the run reaches the material and then
resolves it the *wrong way round* rather than not reaching it at all:

- **F1.9** — G1.7 chose the provider statements as its comparison set and concluded *"No plan
  present in the report is absent from the provider evidence."* True, and not the finding. The Fact
  Find was never used as the comparison base.
- **F5.3** — the guard fired and the assessor argued past it (§5).
- **F6.3 / F9.3** — the run's own F7.2 analysis (the £268.94 row is computed on Zurich's fund) leads
  it to treat the Standard Life increase as an arithmetic artefact rather than as the charge rise
  that contradicts *"Lower Costs"*. **A more sophisticated reading of the arithmetic cost two
  findings.** Both are near-miss judgement calls: a lenient adjudicator reading G6.5 ("*Report
  rationale for switches cites 'lower costs' but … Standard Life … 0.52% … 0.18% … undermining the
  lower-cost rationale*") together with G9.2's +0.26% bullet under R2 could score either as Caught,
  which would put the run at 27.
- **F1.8** — the same total (£116,998.47) is used throughout, but only against charge-derived fund
  values, never against the report's stated p3/p4 pension totals.

**Spot checks on Run 19** (not a score): `Risk rating of 6` appears only inside a citation block,
never in reasoning; `main residence`, `Less than 3 years`, `no understanding of investments` and
`Residence Nil Rate` have **zero** footprint; `LIQUIDATED` is reached and reasoned away as in
Run 20, and mis-read as *"LIQUIDATED 1ST"*. Run 19 looks like a weaker sibling of Run 20 on the
findings this section discriminates on, on identical plans.

---

## 7. Extraction quality

The extraction is the strongest in the series on every measure except one.

**Good.**

- **210.2s, £0.6800** — 2.9× faster and slightly cheaper than Run 19 on the same plans.
- **Vocabulary violations 19 → 2.** Nine invented `basis` strings, four invented `dimension`
  strings and `direction: "RecommendedCheaper"` are all gone. Only `modality: "Inferred"` and
  `feeChargingType: "PercentOfAum"` remain.
- **`overallConfidence` 0.92, `expectedButAbsent` only 3, `ambiguities` 4.**
- **The item-3 fix holds for a third run.** `internalInconsistencies[2]` keeps both Standard Life
  charge readings unreconciled — *"0.52% fund charge on page 5 … 0.18% recurring charge on page 11
  … These cannot both be the full recurring charge"* — with the paths and both values. F7.1 is
  Caught by four separate groups off the back of it, and F9.5 recovered to Caught. This remains the
  clearest mechanism-to-finding link the project has.
- **Check triggers now settled in code.** `hasComplexProduct` was settled to true from the model.
  Run 19's *"the trigger is wrong and the extraction that derived it should be looked at"* warning
  is gone.

**One real defect, and it is a fabrication.** The three objectives are extracted with
`priorityRank: 1, 2, 3`. **The report states no priority order** — that is the benchmark's F2.3
(Low, unscored). G2.1 then reports *"three client objectives with explicit priority ranking"* as
fact. The extraction has invented structure the source does not carry, and the check that should
have raised the omission instead asserted the opposite. F2.3 is not in the scoring set so this does
not move the number, but it is the kind of error the scoring set cannot see and a reviewer would
care about.

**One arithmetic slip the pipeline caught on itself.** `totals.totalMonthlyIncomeNet` is 4386.67
with an extraction note deriving £3,486.67 + £1,000. G1.10 flags this — *"£1,300 + £86.67 + £1,100
+ £1,000 + £1,000 = £4,486.67, not £4,386.67 … Internal arithmetic error in the extraction itself"*
— and prints it as a discrepancy in the report. The self-catch is good; presenting it as a report
defect is a false positive.

---

## 8. Two things a reviewer meets before the findings

### Citation trust: 162 untraceable quotes, and it is still not a quality signal

**162 quotes could not be traced**, 159 of them printed across **61 of the 83 rendered groups
(73%)**. As in Runs 17 and 18, **all of them are prose quotes and not one is a table read** — the
programmatic count of `table:` lines inside rejection blocks is zero, in both Run 19 (155) and
Run 20 (159). Every rejection is a table restated as a sentence, which the assessor prompt forbids
in those words: *"A table restated as a sentence is not a quotation and will be rejected, however
accurately you read it."*

The verifier is behaving correctly. The figure measures **prompt adherence on citation form**. It
tracks the model — every Haiku run sits at 132–195, both Sonnet runs at 78–85 — and under R7 the
verdicts hold, because the substance is printed in the rendered Discrepancies. But 73% of groups
carrying the marker is a genuine cost to a human reviewer, and it has now been stable-to-worsening
for four runs with no intervention.

### Prompt adherence is the worst recorded

**61 of 88 responses (69%) echoed the wrong requirement id, and 12 rewrote the requirement text** —
against 59/10 in Run 18 and 58/4 in Run 19. The plan's values are used regardless, so **no verdict
is affected**, but the rewrite count has tripled against Run 19 on identical plans. Run 18 asked for
one more Haiku run to rule out the generalised prompts; Runs 19 and 20 both sit at 66–69%, well
above the prior Haiku band of 38–52%, so the regression is **confirmed and persistent**, not noise.
It arrived with the generalised plans at Run 18 and has not recovered.

### Two visible factual errors in the output

G7.6 and G8.1 both describe the client as **"a client aged 55"** — G8.1 as *"a client now aged 55
heading to retirement at 75"* inside a High-severity disadvantage finding. The client is 69. The
assessor has taken the normal minimum pension age (55, quoted from the Aviva Key Features) for the
client's age. G8.2, in the same check, has it right (*"the client is 69 at the date the plan is
implemented"*). Two High findings carry a wrong client age on their face; neither is a scored
benchmark finding, but both are the first thing a reviewer would challenge.

---

## 9. What this run settles, and what it does not

**Settled.**

1. **The Missed findings are not a retrieval problem.** §5. The searched extract shows the evidence
   for F1.6, F5.4, F4.2 and F5.3 arriving in the packs of the groups that should have made them,
   with both halves of F1.6 in the same pack. Run 18's recommendation 1 — a `RetrievalDryRun` on
   the five misfiring hints — would confirm the hints are dead and move no finding. This is the
   clearest negative result in the run and it redirects the next session's work.
2. **G3.11 now reaches Caught.** §3. The prompt-adherence nudge Run 18 asked for has landed with no
   plan change, and F3.4 is Caught for the first time.
3. **Retrieval is reproducible on fixed plans.** Runs 19 and 20 report identical search, passage and
   pack-selection counts. Any future delta on fixed plans is attributable to extraction or assessor.
4. **The wave-based extraction is a clear win.** 601s → 210s, cost slightly down, vocabulary
   violations 19 → 2, triggers settled, model size essentially unchanged. No finding is traceable
   to a loss from it.
5. **The prompt-adherence regression is real.** §8. Three consecutive Haiku runs at 66–69% against a
   prior band of 38–52%. It arrived with the generalised prompts.
6. **Four scored findings rest on luck.** §4. The `ranked by Maturity Value` hints have matched
   nothing for eight runs; the extract now shows by name which packs the ranking table reaches by
   ordinary search. F6.1, F8.2, F9.2 and F9.4 depend on that ranking holding.

**Not settled.**

7. **Whether Run 20 is genuinely below Run 18.** −2 is on the noise floor, the Missed count is
   identical at 4, and two of the five regressions (F6.3, F9.3) are single-group-join judgement
   calls that would put the run at 27 under a lenient reading. Both runs are one sample.
8. **Whether the assessor can be made to *use* evidence that reaches it.** Every remaining shortfall
   is now of this shape. Adding guards has not worked: F1.6 has absorbed four and G5.5's F5.3 guard
   fired and was argued past in this very run.
9. **F2.3 and the invented priority ranking.** §7 — a fabrication in an unscored area. How much
   else the extraction infers rather than reads is unmeasured, and the scoring set cannot see it.

---

## 10. What to do next

1. **Stop treating the Missed findings as retrieval.** §5. Cancel the `RetrievalDryRun` step for
   F1.6/F5.4/F2.2 — the extract already answers the question those hints were meant to answer. The
   work is in the assessor: G1.4, G1.6 and G5.2 all receive `main residence`, `Residence Nil Rate`
   and `Tenant` and none joins them. A single instruction to test a residency claim against the
   recorded tenure, in the group that holds both, is the smallest change that could move two
   findings.
2. **Fix G5.5's argue-past.** §5. The guard is correct and the assessor overrode it with
   *"the decline is minimal."* The guard needs a clause saying a scheduled liquidation order is the
   test, not the year-on-year balance trajectory. This is one Caught finding, recoverable, and it
   was Caught in Runs 12 and 18.
3. **Rejoin the "Lower Costs" contradiction.** §6. The run states the Standard Life +0.26% / £268.94
   increase and separately states the *"Lower Costs"* rationale, in different groups, and its own
   (correct) F7.2 analysis stops it treating the increase as real. G9.8 held both in Run 18 and does
   not here. Two findings.
4. **Give G1.7 the Fact Find as a comparison base.** §6. F1.9 fell from Caught to Missed because the
   group compared the five plans to provider statements instead of to the Fact Find's Existing
   Pension Provision. This is a plan-routing step, not a prompt one.
5. **Correct the client-age error.** §8. Two High-severity findings assert the client is 55. Guard
   the assessor against reading a normal-minimum-pension-age quote as a client age.
6. **Stop the extraction inventing `priorityRank`.** §7. The report states no priority order; the
   extraction supplies one and a check then asserts it as fact. Either the field should be left
   absent or the extractor prompt needs the same *"do not reconcile, do not supply"* discipline that
   item 3 gave the charge tables — which is the one extraction change in this project that
   demonstrably moved a finding.
7. **Address the 69% wrong-id echo.** §8. Confirmed over three runs, no longer a candidate for
   noise. It costs no verdicts today but it is the assessor ignoring the plan's own identifiers,
   which is the same failure mode as §5.

---

## 11. Limits

- **One sample against one sample.** Run 20 vs Run 18 differs on plans, canonical model, extraction
  strategy and code, with sampling unpinned on both. Run 20 vs Run 19 is the tighter pair (identical
  plans, identical retrieval counts) but Run 19 is not scored, so the pair supports mechanism claims
  in §2 and §7 and not a recall comparison.
- **F6.3, F9.3, F1.8, F2.2, F7.3 and F10.2 are the judgement calls.** All Partial. F6.3 and F9.3
  could each be read as Caught under a generous application of R2, which would put the run at 27 —
  level with Run 18. At the ±2 noise floor this run reads between 25 and 27 and should not be
  described as a regression without a second sample.
- **§5 rests on the searched extract, which is new.** The mapping of evidence strings to
  in-pack/evicted status is mechanical (2,341 passages, 88 requirement headers, parsed
  programmatically), but it is the first run in which this artefact has been read this way, and the
  eviction marking has not been independently validated against the pack the assessor actually saw.
- **This dataset cannot measure false positives at check level.** Every check is expected to land on
  Potential Concern and 83 of 88 requirements did. The specific false positives named in §7 and §8
  were found by hand and are not a systematic count. The control case in
  [control-case-spec.md](../../control-case-spec.md) remains the missing instrument.
- **Adjudication is one pass**, ±2 findings. F1.8 and F5.3 are the two verdicts most likely to move
  on a second reading — F1.8 because the £110,185 / £116,998 mismatch is stated repeatedly, just
  never against the report's own narrative totals; F5.3 because the group both quotes the evidence
  and disclaims it in the same paragraph.
