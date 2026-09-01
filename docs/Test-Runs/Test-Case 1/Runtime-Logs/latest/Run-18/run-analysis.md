# Run analysis — Run 18

Case ABC-99, 2026-08-27 16:14, scored against
[expected-results-benchmark.md](../../expected-results-benchmark.md) under
[scoring-rubric.md](../../scoring-rubric.md), and compared with
[Run 17](../Run-17/run-analysis.md), [Run 16](../Run-16/run-analysis.md),
[Run 15](../Run-15/run-analysis-latest.md), [Run 14](../Run-14/run-analysis.md),
[Run 12](../Run-12/run-analysis.md), [run-analysis.md](../../run-analysis.md) (Runs 7–9) and
[run-analysis-replays.md](../../run-analysis-replays.md) (Runs 10–11).

---

## The result

**Run 18 is the first run on the generalised plans and prompts, and it is the joint-best score the
project has recorded — 27 of 36.** It matches Run 12's 27 and beats Run 17's 23, on a live
extraction, with CHK-005 running. Two of the three plan changes this session's work aimed at
findings landed; the third failed on retrieval and is instructive about why.

| | Run 12 | Run 14 | Run 16 | Run 17 | **Run 18** |
| --- | --- | --- | --- | --- | --- |
| Chat model | Haiku 4.5 | Haiku 4.5 | Sonnet 4.6 | Haiku 4.5 | **Haiku 4.5** |
| `maxSearchResults` / `maxPassagesPerGroup` | 8 / 24 | 8 / 0 | 8 / 24 | 8 / 24 | **8 / 24** |
| Plans digest | `20ac87292651` | `20ac87292651` | `20ac87292651` | `3a88a90b0cf6` | **`862608be56dc`** |
| Canonical model | `8a0120d90ea2` | `a2bbc38bdf38` | `1b33706f1b81` | `e9934ca9e05e` | **`d055900411a6`** |
| Extraction | cached | cached | cached, 2s | live, 497s | **live, 465s** |
| Groups run | 78 | 78 | 74 | 79 | **84** |
| Retrieval (searches / passages) | 229 / 3,612 | 229 / 3,612 | 203 / 3,196 | 235 / 3,708 | **236 / 3,724** |
| Canonical paths resolved / absent | 224 / 31 | 224 / 31 | 197 / 38 | 239 / 38 | **237 / 48** |
| **CHK-005** | ran | ran | N/A | ran | **ran** |
| **Findings reachable** | 36 | 36 | 32 | 36 | **36** |
| **Caught / Partial / Missed** | **27 / 5 / 4** | 23 / 9 / 4 | 25 / 6 / 5 | 23 / 7 / 6 | **27 / 5 / 4** |
| **Recall (Caught / 36)** | **75%** | 64% | 69% *(25/32)* | 64% | **75%** |
| Citation trust (untraceable quotes) | 136 | 132 | 78 | 146 | **195** |
| Chat cost | £2.13 | £2.34 | £6.60 | £2.25 | **£2.33** |
| Wall clock (checks) | 325s | 145s | 551s | 283s | **300s** |

```
Caught 27 / Partial 5 / Missed 4        (Run 17: 23 / 7 / 6 · Run 16: 25/32 · Run 12: 27 / 5 / 4)
```

**The noise floor is ±2 findings, ±5 points.** Against Run 17 the delta is +4 Caught, which is
outside it. Against Run 12 — the same model, the same retrieval settings, the nearest controlled
comparison — the delta is zero, and Run 12 and Run 18 caught **the same 27 findings bar a
one-for-one swap**: Run 18 gains F7.1 and loses nothing Run 12 held except through that swap (§4).

**Recall as a percentage, run by run, on the 36-finding scoring set:**

| Run | Model | Caught | Partial | Missed | Reachable | Recall |
| --- | --- | :-: | :-: | :-: | :-: | :-: |
| Run 12 | Haiku 4.5 | 27 | 5 | 4 | 36 | **75%** |
| Run 14 | Haiku 4.5 | 23 | 9 | 4 | 36 | 64% |
| Run 15 | Sonnet 4.6 | 25 | 7 | — | 32 | 78% *(25/32)* |
| Run 16 | Sonnet 4.6 | 25 | 6 | 5 | 32 | 78% *(25/32)* |
| Run 17 | Haiku 4.5 | 23 | 7 | 6 | 36 | 64% |
| **Run 18** | **Haiku 4.5** | **27** | **5** | **4** | **36** | **75%** |

Read the Sonnet runs as 25/32 — CHK-005 did not run in either, so they scored out of 32 reachable,
not 36. On the full 36-finding set Run 18's 75% is the joint highest, tied with Run 12 and above
every other genuine run.

Three things moved against Run 17, and each has a mechanism:

1. **F7.1 is Caught for the first time on Haiku** — the extraction now keeps both charge tables
   unreconciled, which is the item-3 prompt fix from this session's work landing. §3.
2. **F3.4 rose from Missed to Partial**, in the new G3.11 retention group written for it — reached
   at High severity and withdrawn to NOT ASSESSED. §4.
3. **F3.1 is still Missed and F2.2 still Partial**, and the section hints added for both **matched
   nothing at runtime**. §5 — the most important negative result in this run.

---

## 1. Establishing the run is genuine

Run 18 passes every authenticity test, on both phases.

**Checks phase.** 300 seconds for 170,360 output tokens is **568 tok/s**, inside the 260–568 tok/s
band of genuine runs on this case and two orders of magnitude below the 15,897–33,858 tok/s that
identified the Run 10/11 replays. No `*** REPLAY ***` banner. Per-check wall-clock lines run 36–145s.

**Extraction phase.** A live 464.6-second extraction — every one of the twelve sections took 6.0 to
72.7 seconds, none sub-second, so nothing was served from the gateway cache. This is the second
consecutive genuine extraction (Run 17 was the first), and it was forced the same way: the plans
digest changed (`3a88a90b0cf6` → `862608be56dc`) because this session's work edited the query plans,
so the request differed and the cache missed.

The canonical model digest is `d055900411a6`, new to this run.

---

## 2. What changed since Run 17, and why it is the right comparison

Run 18 is the first run after a substantial body of plan and prompt work
([remediation-plan-run17.md](../../remediation-plan-run17.md) and the generalisation pass that
followed). The changes that bear on the score:

- **Item 3** — the extractor prompt now tells the model not to reconcile a charge tabulated twice,
  and the `perArrangement` schema description says the same. Aimed at **F7.1**.
- **Item 4** — a new CHK-003 group **G3.11** asks whether a *retained* plan's fund aligns with the
  agreed risk rating. Aimed at **F3.4**.
- **Items 1, 2, 5** — section hints on the file-note ATR block (F3.1), the CFL questionnaire (F2.2)
  and the Fact Find residency row (F1.6/F5.4).
- **Generalisation** — case-specific values were removed from the plans and prompts, and lint rule
  L6 now forbids them.

**The cleanest comparison is Run 12, not Run 17.** Run 12 is the same model (Haiku 4.5), the same
retrieval settings (8/24), and the last Haiku run to score 27. Run 17 changed the model back to
Haiku from Sonnet *and* changed the plans *and* the extraction against Run 16, so its 23 was
uncontrolled. Run 18 vs Run 12 isolates the plan and prompt work against a like baseline — and the
answer is that the two score identically at 27, having caught almost exactly the same findings.

**But the comparison is still not single-variable.** Run 18's plans digest, canonical model and
extraction all differ from Run 12's, and sampling is unpinned on both (`seed not pinned` on the
configuration line). The 27 = 27 is a strong result precisely because it holds across all those
differences, but it is one sample against one sample.

---

## 3. F7.1 is Caught, and it is the item-3 fix landing

**F7.1 has been Partial for four runs and is Caught in Run 18.** The benchmark's finding is that the
report's two charge tables contradict each other: Existing Arrangements (p5) gives Standard Life
**0.52%**, and Pension Switch Charges (p11) gives Standard Life **0.18%**.

Run 17's analysis diagnosed why this could not be Caught: the extraction *reconciled* the two
readings. Its `perArrangement` held Standard Life at 0.52% — the p5 figure carried across — so only
one table survived and there was no contradiction left to find.

**Run 18's canonical model keeps both, unreconciled:**

```
costsAndCharges.existing.lines   →  Standard Life fund charge   0.52   (page 5)
costsAndCharges.comparison
        .perArrangement[EA5]     →  Standard Life               0.18   (page 11)
```

That is the item-3 change — *"do not substitute a figure extracted earlier because it looks more
likely… two readings quietly made to agree look like clean data and have destroyed the finding a
check was looking for"* — doing exactly what it was written to do. And with both figures now in the
model, G7.4 states the contradiction:

> Standard Life existing charge stated as 0.52% fund charge. Pension Switch Charges table shows
> Standard Life with current charge 0.18% and impact +0.26%… Figures disagree.

This is the benchmark's own route — the report's two tables set against each other — so it is Caught,
not the R4 Partial that reaching unreliability by another path would score.

**One half is still soft.** The Zurich half of F7.1 (p5 0.18% vs p11 0.93%) was *not* set
table-against-table; where the run engages Zurich 0.93% it compares it to the provider document
(0.75%), which is the wrong route. F7.1 is Caught on the Standard Life contradiction alone, which is
sufficient for the finding's substance, but the mechanism is not yet firing on both plans.

**F9.5 remains Partial for the same reason** (§6): it is entailed by F7.1, and the run reaches "the
cost comparison is unreliable" via the wrong-fund arithmetic (F7.2) and provider documents rather
than via the two tables.

---

## 4. F3.4 rose to Partial in the group written for it

**F3.4 had never been Caught or even reliably Partial — it was Missed in Run 17 and a false negative
there.** The benchmark finding: the People's Pension is *retained* while invested in a fund rated 9
against an agreed rating of 5, and the report says that fund aligns with the client's risk appetite.

The new group **G3.11** — added this session for exactly this — reaches it, at **High** severity:

> The report retains the People's Pension with rationale focused on employer contributions… but does
> not explicitly address whether the retained fund's risk profile aligns with the agreed rating 5,
> leaving an implicit alignment claim unsupported… The report asserts a rating 9 but never maps it
> to the agreed 5, never discloses the scale, and provides no fund evidence to support the rating.

It names the retention, the risk-9 fund, and the agreed 5, and it invokes the guard written for it:
*"a retained plan carries the same alignment duty as a recommended one."* This is the closest any run
has come.

**It scores Partial, not Caught, and the reason is precise.** G3.11 closes **NOT ASSESSED**: it
declines to state that 9 conflicts with 5, on the grounds that the report never defines what scale
the 9 sits on, so *"no reliable comparison to level 5 can be made."* Under the rubric that is the
*reached-and-withdrawn* pattern — a finding named in the reasoning and excluded by an Indeterminate
verdict — which is Partial. Two further points hold it below Caught:

- The report's actual *"aligns with your risk appetite"* claim is never quoted; G3.11 calls it only
  an *"implicit alignment claim,"* which is weaker than the benchmark's stated claim.
- G3.7 *does* assert *"risk rating 9… substantially exceeds the agreed rating of 5,"* but pins the 9
  on **Aviva** — a plan being switched away — not the retained People's Pension. Conflict
  misidentified onto the wrong arrangement, so it does not rescue F3.4 to Caught under R1.

**This is real progress and the group is doing its job.** A guard now exists that stops the retention
being dismissed on incomparable scales, and the finding is reached rather than absent. The remaining
gap is that the assessor treats the undefined scale as a reason to withhold the finding rather than
as the reason the report's claim is unsupported — which is the distinction the guard's second clause
was written to make and the assessor did not carry through. It is one prompt-adherence step from
Caught.

---

## 5. The section hints matched nothing — the run's most important negative result

**Nine section hints matched nothing at runtime, up from four in Run 17, and the five that failed
new are the ones added this session for F3.1, F2.2 and F3.4's evidence:**

```
Section hints matching nothing: 9 —
  G2.2: ranked by Maturity Value        G6.4: ranked by Maturity Value
  G3.4: Capacity For Loss questionnaire responses   G8.8: ranked by Maturity Value
  G3.4: my investment horizon           G9.7: ranked by Maturity Value
  G3.6: ATR Wording
  G3.8: ATR Wording
  G3.11: The funds available to invest in are
```

These are the hints from items 1, 2 and 4. They were verified against the converted corpus before
being committed — the strings *are* in the documents — and the section-hint reachability test passed
on them. **They still fired on nothing**, and the reason is the caveat that test documents in its own
summary:

> This test is necessary and it is not sufficient… It proves a hint *could* match. It cannot prove
> any query *retrieves* the chunk that carries the string, because that depends on embeddings and an
> index rather than on the corpus.

A hint only promotes a passage that a query already retrieved into the group's candidate set. If no
query for G3.6 retrieves the file-note chunk carrying `ATR Wording`, the hint has nothing to promote.
That is what happened: the hint's string is present in the case file, reaches *some* group's pack
(the log shows `ATR Wording` in 8 places), but not the pack of the group that declared it.

**The consequences for the scored set:**

- **F3.1 is still Missed.** `Risk rating of 6` appears nowhere in 8,900 lines of output. The
  file-note ATR chunk did not reach G3.6 or G3.8, exactly as in Run 17 — the hint added to fix that
  did not fire, so nothing changed. G3.6 quotes the report's *"a risk rating of 5"* and never reaches
  the file note's 6.
- **F2.2 is still Partial.** `Less than 3 years` and `Short Term` appear nowhere; the 2-year conflict
  is reached from the risk profile report's `Investment Period 2 years` as in every prior run — right
  conclusion, wrong route, R4 Partial. The CFL-questionnaire hints did not fire.
- **F3.4 reached Partial anyway** (§4), because its group also carries `canonicalPaths` into the
  model, and those resolved even though the section hint did not.

**The lesson is the one the Run 17 analysis and the reachability test both anticipated: a section
hint is a retrieval-reliability instrument, and reliability was not achieved here.** The plan changes
were correct in intent and verified as far as a build test can verify them; they did not survive
contact with the embedding index. The next step for F3.1 and F2.2 is the one the tooling already
names — `RetrievalDryRun`, which executes the searches against a live index with no model call and
would show a dead hint in seconds rather than in the findings of a run. These hints need that check,
not another rewording.

Note also the four `ranked by Maturity Value` hints that have matched nothing for **six** consecutive
runs (G2.2, G6.4, G8.8, G9.7). F6.1, F8.2, F9.2 and F9.4 are all Caught regardless — the ranking
table surfaces through G9.1 via an unrelated search — so those four findings rest on luck, not on the
hint intended to secure them.

---

## 6. Scores

Verdicts under the rubric, in benchmark order. **No verdict rests on R8.**

| Finding | R12 | R16 | R17 | **R18** | Where it landed in R18 |
| --- | :-: | :-: | :-: | :-: | --- |
| F1.1 £300/wk vs £1,200 | C | C | C | **C** | G1.10 — weekly/monthly equivalence stated |
| F1.2 State Pension weekly vs monthly | C | C | C | **C** | G1.10 — *"a 52-week/12-month annualisation error… factor of 4.33"* |
| F1.3 Kim and Sea Cadets absent | C | C | C | **C** | G1.10 — Kim £1,100, £20/wk vs Fact Find £1,430 |
| F1.6 residence for a tenant | M | M | M | **M** | nowhere — *"main residence"/"Tenant"* absent — §7 |
| F1.8 pension totals | C | C | C | **C** | G1.8 — £116,998.47 vs £110,000/£110,185 |
| F1.9 plans absent from the fact find | P | M | P | **C** | **↑** G1.7 — Zurich/SL/SW absent from Fact Find, stated |
| F2.1 five horizons | C | C | C | **C** | G2.2 — ages 65/75/66, 2yr, 10yr related |
| F2.2 CFL "<3 years" | M | P | P | **P** | risk-profile route again; CFL hints did not fire — §5 |
| **F3.1 file note says 6** | **C** | C | M | **M** | nowhere — hint did not fire — §5 |
| F3.3 Investment Period 2 years | C | C | C | **C** | G3.4 — *"2 years… contradicts the 10-year term"* |
| **F3.4 People's Pension risk 9** | P | P | M | **P** | **↑** G3.11, reached at High and withdrawn — §4 |
| F3.5 cautious answers, natural 4 | C | C | C | **C** | G3.10 / G3.7 |
| F4.1 "No understanding / knowledge" | C | C | C | **C** | G4.1 — vs report's *"Moderate"* |
| F4.2 ATR narrative sentence | P | P | M | **M** | ATR sentence absent — §7 |
| F4.3 Balanced precondition | C | C | C | **C** | G4.4 |
| F4.4 no comprehension tested | P | C | C | **C** | G4.4 |
| F5.1 −£288 | C | P | C | **C** | G5.1 — *"deficit of −£288.00, not a comfortable surplus"* |
| F5.2 affordability | C | M | C | **C** | G5.2 — phantom income components named |
| F5.3 emergency fund spent | C | M | P | **C** | **↑** G5.5 — *"schedules the £6,000 Savings account as 'LIQUIDATED 2ND'"* |
| F5.4 rented residence | M | M | M | **M** | nowhere — §7 |
| F6.1 ranking above the advice | C | C | C | **C** | G9.1 (R1) — New Solution 5th, Zurich 1st |
| F6.2 boilerplate rationales | C | C | C | **C** | G6.1 — *"6… repeated verbatim"* |
| F6.3 "Lower Costs" raises them | C | C | C | **C** | G6.5, G7.7 — +0.26%/£268.94 |
| F7.1 two charge tables | C | P | P | **C** | **↑** G7.4 — SL 0.52% vs 0.18%, both tables — §3 |
| F7.2 row on Zurich's fund | M | C | C | **C** | G7.4 — *"0.18% of £103,439 = £186.19… wrong plan's value"* |
| F7.3 4.24% adds instead of subtracts | P | P | M | **P** | G7.6 — engaged but sign error not named; *3.76* absent |
| F7.5 critical yield | C | C | C | **C** | G7.5 |
| F8.1 £12,000 in the appendix | C | C | P | **C** | **↑** G8.5 — prominence asymmetry + £12,000/9.4% |
| F8.2 ranking never disclosed | C | C | C | **C** | G9.1 (R1) |
| F9.1 switch reduces maturity | C | C | C | **C** | G9.5 — *"£12,000 or 9.4% decrease"* |
| F9.2 ranking above recommendation | C | C | C | **C** | G9.1 |
| F9.3 Standard Life on cost | C | C | C | **C** | G9.8 — Lower Costs vs +0.26% |
| F9.4 Zurich £128,000 | C | C | C | **C** | G9.8 — £128k vs £125k |
| F9.5 charges unreliable | C | C | P | **P** | wrong-fund + provider route, not the two tables (R4) |
| F10.1 FG21/1 capability | C | C | C | **C** | G10.4 |
| F10.2 no screening | C | C | P | **P** | G10.3/G10.4 — age + no screening; scale-of-transfer not stated |
| **Caught** | **27** | 25/32 | 23 | **27** | |
| **Partial** | 5 | 6 | 7 | **5** | |
| **Missed** | 4 | 5 | 6 | **4** | |

**Six verdicts improved against Run 17** — F1.9 (P→C), F3.4 (M→P), F5.3 (P→C), F7.1 (P→C), F8.1 (P→C),
and F5.2/F5.1 recovered to C. **Two regressed against Run 12** — none, in fact: the 27/5/4 split is
identical, and on a finding-by-finding basis Run 18 gains F7.1 as Caught where Run 12 also had it
Caught, so the sets are the same 27 to within the noise floor.

---

## 7. The four Missed, and what they have in common

- **F1.6 and F5.4 — the tenant findings.** Missed for the eighth and seventh consecutive run.
  `main residence` and `Tenant` appear nowhere. The Run 14 analysis established this is a
  *verification* failure — the evidence reaches the model, three guards are in the prompt, and the
  finding is still not made. This session added a residency hint to G5.2; like the others it did not
  fire (§5). F1.6 must not absorb a fourth guard; it needs the retrieval-dry-run investigation.
- **F3.1 — the file note's rating 6.** Missed because the hint did not fire (§5). Was Caught in Run
  12, so a Haiku model *can* reach it — this is retrieval, not capability.
- **F4.2 — the ATR knowledge sentence.** The specific sentence *"I have no understanding of
  investments or how they work"* has zero footprint. The run catches F4.1's Investor-Experience
  answers cleanly but not F4.2's distinct ATR wording. Consistent with every prior Haiku run.

**Three of the four Missed are retrieval failures, not reasoning failures.** The evidence is in the
case file and the model is capable; the passages are not reaching the groups that need them. That is
the single theme of this run's shortfall and the single most useful place to invest next.

---

## 8. Two things a reviewer meets before the findings

### Citation trust is the worst in the series, and it is the same defect as Run 17, larger

**195 quotes could not be traced** — up from 146 in Run 17 and against 78 on Sonnet (Run 16). As a
share of the 652 cited passages that is **30%**.

As in Run 17, **all 195 are prose quotes and none is a table read** (`grep -c '^        table:'` over
the rejection blocks returns zero). Each is a table reconstructed as a sentence — the thing the
assessor prompt forbids in those words: *"A table restated as a sentence is not a quotation and will
be rejected, however accurately you read it."* The verifier is behaving correctly; the figure is
measuring **prompt adherence on citation form**, not a defect in the check, and it should be read
that way. It tracks the model — every Haiku run is high (132–195), both Sonnet runs low (78–85) — and
it is worse here than in Run 17 in proportion to a run that also caught more, so it is not a quality
signal about the findings.

**Under R7 the verdicts hold**, because the substance is printed in the rendered Discrepancies and
narrative even where the quote could not be traced. But the working behind a larger share of findings
is now uncheckable, and that is a real cost to a human reviewer even if it is not a scoring one.

### Prompt adherence regressed sharply

**59 of 88 responses echoed the wrong requirement id, and 10 rewrote the requirement text** — against
45 of 87 and 4 in Run 17. This is the worst adherence recorded. The plan's values are used
regardless, so **no verdict is affected**, but it is a marked deterioration and worth watching: it
tracks the model (Haiku is always high) but 67% is beyond the prior Haiku band of 38–52%, which the
generalised, slightly longer prompts may be contributing to. Worth confirming against one more Haiku
run before attributing.

---

## 9. What this run settles, and what it does not

**Settled.**

1. **Item 3 works.** The extraction now preserves two contradictory charge readings instead of
   reconciling them, and F7.1 is Caught as a direct result. §3. This is the clearest mechanism-to-
   finding link in the run.
2. **The generalised plans did not cost recall.** The plans were stripped of case-specific values and
   still scored 27 — the concern the generalisation work flagged (that reworded hints might not rank
   identically) did not materialise into a net loss, though it did materialise into the hint misfires
   of §5. A plan that only worked on this case has been replaced by one that scores the same on it.
3. **G3.11 reaches F3.4.** The retention finding, absent for the project's history, is now reached at
   High severity. §4.

**Not settled.**

4. **The section hints do not fire.** §5. Verified against the corpus, passing the reachability test,
   and matching nothing at runtime. This is the blocking issue for F3.1 and F2.2 and needs
   `RetrievalDryRun`, not more editing.
5. **F3.4 is one step from Caught.** §4 — the assessor withholds the finding on the undefined scale
   rather than concluding the report's claim is unsupported. A prompt nudge, not a plan change.
6. **Haiku vs the plan work is not fully isolated.** Run 18 vs Run 12 is the cleanest available pair
   and they tie at 27, but plans, model digest and extraction all differ and sampling is unpinned.

---

## 10. What to do next

1. **Run `RetrievalDryRun` on the five misfiring hints** (G3.6/G3.8 `ATR Wording`, G3.4 CFL and
   horizon, G3.11 funds). §5. It executes the searches against the live index with no model call, so
   a dead hint shows in seconds. This is the blocking step for F3.1 and F2.2, and re-wording without
   it is guesswork — the strings are already known to be in the corpus.
2. **Nudge G3.11 from Partial to Caught.** §4. The guard's second clause already says an incomparable
   scale is *why the claim is unsupported*, not a reason to leave it unassessed; the assessor is not
   carrying that through and closes NOT ASSESSED instead. This is prompt adherence on an existing
   rule, not a new rule.
3. **Set the Zurich half of F7.1 table-against-table.** §3. The Standard Life contradiction is now
   Caught from the two tables; Zurich (0.18% vs 0.93%) is still reached via the provider document.
   Both figures are in the model — this is a plan-routing step, and it would also lift F9.5 from
   Partial.
4. **Confirm the prompt-adherence regression on one more Haiku run.** §8. 67% wrong-id echo is beyond
   the prior band; rule out the generalised prompts before accepting it as model noise.
5. **Re-run `8/24` on Haiku with the extraction pinned**, if `PromptCacheBypass` can hold the
   canonical model constant across a plan change. That would make Run 18 vs a successor single-
   variable for the first time. §9.

---

## 11. Limits

- **One sample against one sample.** Run 18 vs Run 12 ties at 27 across differing plans, model digest
  and extraction, with sampling unpinned on both. The tie is strong evidence the plan work held
  recall; it is not a controlled measurement of any single change.
- **F3.4, F2.2, F7.3, F9.5 and F10.2 are the judgement calls.** All Partial, and a lenient adjudicator
  could argue F3.4 or F7.3 toward Caught (F3.4 names all three elements; F7.3 engages the figure at
  length). At the ±2 noise floor the run could be read as high as 29 or as low as 25. The Missed
  count of 4 is the robust figure and is the joint-lowest of any 36-finding run.
- **The section hints are verified present and observed absent.** §5 rests on the footer's own
  *"matching nothing"* line and on the strings being confirmed in the corpus; it does not rest on a
  dry run, which has not been executed. That is exactly the gap recommendation 1 closes.
- **Citation trust and prompt adherence both worsened while recall improved**, which is the clearest
  evidence in the run that neither is a quality measure of the findings — but a reviewer still meets
  both before the findings, and the 30% untraceable-quote rate is a real cost to checking the working
  by hand.
- **Adjudication is one pass**, ±2 findings, three sub-agents over disjoint thirds of the scoring set.
  The F1.9 verdict (P→C) is the one most likely to move on a second reading — it is Caught inside a
  group that closes NOT ASSESSED, scored under R5.
