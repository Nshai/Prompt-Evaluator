# Test Case 3, Run 2 — deep analysis

Haiku 4.5, run 2026-08-28 23:53, on the build carrying **both** rounds of the Test Case 2
improvement work. Scored against
[expected-results-benchmark.md](../expected-results-benchmark.md) across **all 83 material
findings**, not only the 58 in the scoring set.

The control is [Run 1](../Run-1/run-analysis.md) — the same case, the same model, six hours
earlier, on the build carrying only the first round.

---

## 1. What this run actually is

| | Run 1 | **Run 2** |
| --- | --- | --- |
| Chat model | intelliflo-claude-haiku-4-5 | same |
| Plans | 10@`e267d2d36592` | same |
| Settings digest | `84ca882c3ba6` | same |
| Canonical model | `76dd761defbd` | **`6802268cf6bc`** (re-extracted, £0.6610) |
| Index | inherited | **rebuilt — 585 chunks, 37 docs, £1.2729** |
| Code | first round | **first + second round** |
| Checks cost | £2.4627 | £2.3407 |

Two things confirm the build is the newer one. The settings line now reads
`Settings 84ca882c3ba6 (adjusted: BypassResponseCache, EmbeddingDimensions, EmbeddingModel,
IgnoreTriggerProbe, PinSeed, PinTemperature, PinTopP, SelectedModel, Temperature)` — the
`NonDefaultSettings` field added in the second round, absent from Run 1's line. And the group
prompts now print `#### Contradictions the extraction found in the report, on your paths`
(8 occurrences), the routed-inconsistency block.

**This is a confounded comparison and I am not going to pretend otherwise.** Three variables moved
at once: the code, the canonical model, and the index. The fingerprint holds two of them constant
by construction — plans and settings — and cannot separate the other three, because none of them
is in it.

One measurement narrows it usefully. The two retrieval extracts differ on 32,686 lines, so the
re-index genuinely re-chunked the corpus. But what reached assessors barely moved:

| | Run 1 | Run 2 |
| --- | --: | --: |
| Searches issued | 236 | 236 |
| Passages retrieved | 2,472 | 2,476 |
| Passages in packs | 1,852 | 1,855 |
| Groups assessed | 88 | 88 |

Per-check search counts are identical check for check (30/21/28/20/27/13/27/21/29/20). The
re-index changed chunk *contents* and left the *shape* of retrieval alone. That makes the code and
prompt changes the most plausible cause of the score movement below — plausible, not measured.

---

## 2. The score

| | Run 1 (corrected) | **Run 2** |
| --- | :-: | :-: |
| **Highest** (22) | 8 / 10 / 4 | **12 / 5 / 5** |
| **High** (36) | 12 / 12 / 12 | **14 / 9 / 13** |
| **Scoring set** (58) | 20 / 22 / 16 — 34% | **26 / 14 / 18 — 45%** |
| **Moderate** (21) | 5 / 4 / 12 | **9 / 1 / 11** |
| **Low** (4) | 1 / 1 / 2 | **0 / 2 / 2** |
| **Moderate + Low** (25) | 6 / 5 / 14 — 24% | **9 / 3 / 13 — 36%** |
| **All material** (83) | 26 / 27 / 30 | **35 / 17 / 31** |
| **Caught** | 31% | **42%** |
| **Missed** | 36% | **37%** |

Read as *caught / partial / missed*.

**Caught rises 11 points. Missed does not move at all — 30 to 31.** Every point of the gain came
out of the Partial column, which fell from 27 to 17. The changes did not find material the
pipeline had never reached. They finished findings it was already half-making.

Against the other case on the same model:

| | TC2 Run 4 (tuned case, round 1) | TC3 Run 1 | **TC3 Run 2** |
| --- | :-: | :-: | :-: |
| Caught, all material | 55% | 31% | **42%** |

The gap to the case the pipeline was developed against narrows from 24 points to 13. That is the
first evidence in this project that any of this work generalises. It is one run.

---

## 3. What moved

Forty-two of 83 findings changed verdict — 23 up, 19 down.

**Up (23):** F1.5, F1.6, F1.14, F1.16, F2.1, F3.8, F4.3, F4.5, F5.3, F5.5, F5.7, F6.2, F6.4, F6.9,
F7.1, F7.4, F7.6, F8.4, F8.5, F9.3, F9.7, F10.5, F10.6

**Down (19):** F1.7, F1.13, F2.5, F2.7, F3.5, F3.7, F4.1, F4.2, F5.6, F6.7, F7.2, F7.3, F7.5,
F7.7, F7.8, F7.9, F8.3, F9.6, F10.2

Nine findings went from **zero mentions to caught** — F1.6, F3.8, F4.5, F5.3, F5.5, F7.4, F7.6,
F10.5, F10.6. Four went from **caught to zero mentions** — F1.13, F2.5, F7.8, F8.3.

The noise floor measured on Test Case 2 is 3 of 78 outcomes at an *identical* fingerprint. Here the
model digest and the index both changed, so that floor does not apply and I have no floor for this
configuration. **A net of +9 sitting on 42 movements means the aggregate is worth something and the
per-finding verdicts are not.** Some of the churn is also mine: Runs 1 and 2 were adjudicated in
separate sittings, and a Partial/Missed boundary read twice does not always land in the same place.

Per check, over all material findings:

| Check | n | Run 1 | Run 2 |
| --- | :-: | :-: | :-: |
| CHK-001 Suitability completeness | 16 | 4 / 5 / 7 | 4 / 7 / 5 |
| CHK-002 Objectives and horizon | 6 | 2 / 3 / 1 | 2 / 1 / 3 |
| CHK-003 Risk and capacity for loss | 8 | 2 / 3 / 3 | 3 / 1 / 4 |
| CHK-004 Knowledge and complexity | 5 | 0 / 3 / 2 | **2 / 0 / 3** |
| CHK-005 Financial resilience | 8 | 5 / 1 / 2 | **7 / 1 / 0** |
| CHK-006 Rationale and alternatives | 9 | 2 / 4 / 3 | **5 / 0 / 4** |
| CHK-007 Costs and charges | 9 | 5 / 2 / 2 | 3 / 4 / 2 |
| CHK-008 Disadvantages and risks | 7 | 4 / 2 / 1 | **5 / 0 / 2** |
| CHK-009 Replacement justification | 9 | 1 / 3 / 5 | **1 / 3 / 5** |
| CHK-010 Vulnerability | 6 | 1 / 1 / 4 | **3 / 0 / 3** |

CHK-005 is now clean of misses. CHK-007 went backwards on count while gaining precision — three
figures it previously asserted as findings it now correctly records as unverifiable, which is
scored down and is arguably better behaviour.

**CHK-009 did not move at all** — 1 / 3 / 5 in both runs, on nine findings, five of them Highest.
The check that owns the worst defect in the case is the one check on which two rounds of
improvement work produced no change whatever.

---

## 4. The central result: the five worst misses are all Class B

In the Run 1 report I could say only that the string `internal transfer` appeared nowhere in the
output. I checked the retrieval extract this time. Every one of the five most severe surviving
misses has its evidence **in the pack, in the right group, legible.**

**F6.1 / F9.1 — the certified court order (Highest, both).** Section F of the Pension Sharing
Annex records the client's stated preference as an internal transfer; the report recommends an
external transfer and asserts she expressed no preference. The passage is in the pack **nine
times**, and it reaches the two groups whose job it is:

```
[14] Certified PSA.md [B] score 0.637 · found by Q9.4.2   → G9.4  Alternatives to the switch considered
[12] Certified PSA.md [B] score 0.621 · found by Q9.10.3  → G9.10 Material replacement information omitted
```

The text is not degraded. Both the question and the ticked box survived conversion:

> `- F. In cases where the Transferee has a choice of an internal or external transfer, if the`
> `  Transferee has indicated a preference, indicate what this is.`
> `- [x] xInternal transfer`
> `- [ ] External transfer`

G9.4 concluded that alternatives "were considered but not analysed". G9.10 listed what the report
omitted and did not list this. **Zero mentions in the output.**

**F2.2 / F9.5 — the analysis models a plan the client has rejected (Highest, High).** All four
SelectaPension runs project both arrangements *Paid Up to age 87* — no withdrawals for 32 years —
against a stated objective of drawing £25,000–£30,000 a year from 60. The string `Paid Up` appears
**202 times** in the extract, in every one of CHK-009's ten groups. Never remarked on.

**F8.3 — switched out of the best-performing plan (High).** The Nest five-year figure `48.06`
reaches eight groups including G8.1 and G8.4, the two disadvantages groups. Nothing.

**F1.7 / F9.7 — six of seven True Potential funds (High, High).** `Allianz Balanced` — the omitted
seventh holding — reaches fifteen groups including G1.7, G9.1 and G9.7. G1.7 got as far as
recording that the six listed holdings total £46,117.66 rather than the stated £46,672.80, and
stopped. Scored Partial.

**F6.5 — *"the initial advice charges would have been incurred regardless"* (High).** Quoted
verbatim in G6.4's evidence block, reaching seventeen groups. Never questioned.

And two more of the same shape: **F1.11's** Nil Rate Band paragraph (*"transferred to a surviving
spouse… on second death"*, written for a married client who is divorced) reaches nine groups
including G1.1, essential facts. **F10.7's** braille and large-print offer sits in the Client
Agreement passage that opens G1.1's pack, while G10.2 records accessibility requirements as
unavailable.

**This changes what the next piece of work should be.** After Run 1 the obvious reading was that
retrieval was not delivering. It is delivering. The assessor is reading past it. Retrieval work —
more queries, better hints, a bigger index — cannot fix any of the seven findings above.

---

## 5. Where the committed changes are visibly working

**The absence-discipline clause.** `NOT ASSESSED` blocks fell 20 → 16, untraceable citations
132 → 99 (−25%), and groups flagged `citations partly verified` 56 → 46. Four of the nine
zero-to-caught movements are cases where the run previously said "nothing corroborates this" and
now says what the file states instead:

> **F1.6 / F5.5** (G5.8) — *"£70,000 mortgage balance with 'anticipated' increase to 3.8% or 3.9%
> 'when rate ends in June'… but report does not model or discuss this interest rate increase and
> its affordability impact"*. Zero mentions in Run 1, both findings.

**Table context on window boundaries.** The single clearest gain is F7.6, the cumulative cost
disclosure, which requires reading a figure out of the middle of the Aviva illustration's charges
table:

> **F7.6** (G7.1) — *"Aviva illustration [P18] shows cumulative impact table with 'After taking all
> charges' column reducing fund from £372,000 to £337,000 over full term, representing 2.2%
> reduction in growth rate; report does not provide equivalent aggregated cumulative impact figure
> in the costs section itself"*.

That is the benchmark's F7.6 almost word for word, from a run that scored it zero six hours
earlier.

**The comparison-shape clauses.** F7.4 — the report's critical yield runs *"to age 89"* while all
four analyses run to 87 — went from zero mentions to a flat statement in G7.8: *"Report states
retirement age as 89; all provider illustrations show age 87."* F5.7 likewise, in G1.11: *"Report
narrative states 6.00% investment growth assumption but modelling documents show all illustrations
use growth rates of 0%, 2.94%, and 5.88% — 6.00% does not appear in any modelling output."*

**Derived figures fire, 33 blocks.** Their content here is thin — the sampled block reads
`- 5 — corroborated by 5 in [P12]` — because this case's arithmetic contradictions live across
documents rather than inside the model. The switching-consistency derivation that produced the
sharp result on Test Case 2 does not fire on this case at all.

**Routed extraction contradictions fire, 8 blocks** — see §7 for what that cost.

---

## 6. Where the changes were printed and did not bind

**The comparison-basis clause.** The group prompt now carries:

> *Establish what each figure is ON before calling two of them contradictory. Two values on
> different bases — one including a charge, a tax or a fee the other excludes, one at a different
> date, age or term — are not two answers to one question.*

It is printed in every group. The run's most-repeated false positive is exactly the failure it
forbids. The Fact Find is dated **21/07/2026**; the report is dated **24/03/2026**. The Fact Find
records the partner's contribution as £800 as at July; the report records £600 as at March. Five
groups — G1.4, G1.5, G1.10, G5.1, G5.3 — report this as a discrepancy, several of them naming both
dates in the same paragraph:

> *"Partner contribution: Report states £600 per month; Fact Find [P3] states £800 per month"*
> *"Cash reserves: Report states £17,000 as current; Fact Find [P8] shows £7,000 as of 21/07/2026"*

The benchmark scores the £600 as reconciling (F1.17). The clause was read, the dates were quoted,
and the conclusion was drawn anyway. **This is the clearest evidence in either run that printing a
directive is not the same as it taking effect** — and it is the same shape as the F6.1 failure:
the material is in front of the model and does not change what it writes.

**The absence clause's inverse.** G4.1 does not merely fail to find F4.1 — it affirmatively clears
it: *"The report's characterization of the client's knowledge level as 'Good' is consistent across
all available evidence… The client is not recorded as having limited, no, or poor understanding."*
The client's own questionnaire answer — *"Very little understanding / knowledge"* — is genuinely
**not** in the pack (see §8), so the assessor could not have read it. But the honest output for a
question it cannot answer is that the client's own answers were never reached, not a positive
finding of corroboration. The clause governs reporting absence; it does not govern asserting
presence.

---

## 7. False positives and wrong clearances

The benchmark's 13 **No issue** rows are a clean test. The run raises a concern on at least six of
them.

| Row | What the run says | Why it is wrong |
| --- | --- | --- |
| F1.17 | partner £600 vs £800; cash £17,000 vs £7,000 | date-basis conflation (§6), 5 groups |
| F2.6 | G2.1: *"no meeting summary explicitly corroborates this specific income figure"* | G2.8 of the same run quotes Meeting Summary [P11]: *"You aim to draw an annual pension of £25,000 to £30,000"* |
| F3.9 | Nest's *"Very Adventurous"* 9/10 treated as a conflict with the client's agreed 7/10 | that is the rating of an **existing fund**, not a second client rating; repeated in G3.1, G3.7, G3.8, G3.10, G3.11 |
| F3.10 | True Potential risk *"not numerically quantified in the evidence"* | the Defaqto analysis gives 11.30 → 6 |
| F7.10 | G7.5: *"report's stated 0.19% platform charge is not independently corroborated"* | G7.3 of the same check: *"Aviva platform charge is confirmed as 0.19% in the illustration [P21]"* |
| F9.10 | *"no exit penalties"* recorded as unverifiable | correctly stated in the report |

Two of those — F2.6 and F7.10 — are **two groups of one check contradicting each other in one
report**. Nothing in the pipeline notices.

**Two wrong clearances are new to this run**, both from the No Issue path added in the second
round. Each prints the discrepancy that justifies a concern and then returns No Issue:

> `[G7.2] No Issue`
> `  - Initial fee monetary amount: Report states £9,838.36; Client Agreement states £9,839.36`
> `    (difference of £1.00).`

That is benchmark **F7.9**, stated exactly, under a heading that tells the reader there is nothing
here. G3.6 does the same with the Nest 9/10 against the agreed 7/10, ruling it *"outside the scope
of this requirement"*.

**The routed extraction contradiction was amplified.** The extraction recorded one internal
inconsistency for this case — that a 7/10 Growth profile sits oddly with a £25k–£30k income target.
It is not on the benchmark and is a soft judgement. Routing put it in front of four checks, and it
resurfaced as a discrepancy in G1.8, G2.5, G3.10 and G6.3. The routing mechanism works; what it
routed did not deserve four appearances.

**One chart misread.** G5.5 opens *"Report asserts emergency funds of £600,000 are retained"* — a
value read off the axis label of a cashflow chart. The client's cash is £17,000.

---

## 8. What is genuinely unreachable

The benchmark's conversion note says the Markdown conversion of the risk profile report dropped
every table, and that a pipeline working from converted evidence should not be penalised for F3.3,
F3.4 or F4.1. Checked against this run's pack:

| String | In pack | Consequence |
| --- | :-: | --- |
| `Very little understanding` | **0** | F4.1, F10.3 unreachable |
| `significant outstanding debts` | **0** | F3.3 unreachable |
| `No additional notes were recorded` | **20** | F3.4 **is** reachable |

So the note is right about two of its three and wrong about F3.4 — which this run duly catches, in
G3.3 and G4.7. **F3.3, F4.1 and F10.3 are the only three misses in this run that retrieval cannot
be blamed for reaching.** Excluding them, the run is 35 caught of 80 reachable — 44%.

Everything else in §4 was in the pack.

---

## 9. Diagnostics

| | Run 1 | Run 2 |
| --- | --: | --: |
| Untraceable cited quotes | 132 | **99** |
| Groups `citations partly verified` | 56 | **46** |
| Groups `citations unverified` | 0 | **2** |
| `NOT ASSESSED` blocks | 20 | **16** |
| Groups assessed | 88 | 88 |
| Groups appearing anywhere in the report | 87 | **86** |
| Model paths absent across checks | 27 | **35** |

**Groups are being assessed and then silently dropped from the report.** Both runs lose some: Run 1
loses `G1.2`, Run 2 loses **`G6.6`** (*"the report states whether a change is recommended, who will
implement it, and the intended outcome"*) and **`G8.3` — Tax consequences of the recommendation**.
Each of those groups appears in the retrieval extract with a full pack, and then appears nowhere in
the output — not as a concern, not as No Issue, not in a cleared list. A reader has no way to know
the question was asked. `G8.3` is the group that owns benchmark F8.4; the finding is scored caught
here only because G8.7 reached it independently.

Model paths *resolved* fell (38→37, 26→23, 27→24) and paths *absent* rose 27→35 against the new
canonical model. The extraction also flagged `modality: "Comparative"` as outside the documented
vocabulary and settled two check triggers from the model — `hasComplexProduct` and
`hasVulnerabilityIndicators`, both from false to true. Trigger probes were bypassed for this run,
so every check was assessed regardless.

Nothing in the run reports a duplicate path or a failed section, unlike Run 1.

---

## 10. Correction to the Run 1 report

**The Run 1 analysis quoted totals that its own detail table does not support.** Its headline said
*25 / 22 / 36 of 83* and *19 / 17 / 22* on the scoring set. Counting the 83 rows of its own table
gives **26 / 27 / 30** and **20 / 22 / 16**. The Moderate + Low row (6 / 5 / 14) was right; the
Highest and High rows were miscounted.

The corrected Run 1 caught rate is **31%**, not 30% — the headline barely moves. What moves is the
comparison in this report: Run 1's missed rate was **36%, not 43%**, and its Partial column was
27, not 22. So the honest statement of what changed between the runs is *"partials converted to
catches, misses flat"* — not the larger improvement the uncorrected figures would have implied.

I have corrected the numbers in [Run-1/run-analysis.md](../Run-1/run-analysis.md) in place, with a
dated note. The finding-by-finding verdicts in that file were always right; only the totals were
wrong.

---

## 11. Method and confidence

Scored by reading each of the 83 material benchmark rows against the run's output, using the
benchmark's own vocabulary: **caught** = states the substance naming both sides; **partial** =
touches the area but misidentifies or buries it; **missed** = otherwise. A finding is scored caught
wherever in the output it appears, not only in the check the benchmark files it under.

Confidence is not uniform:

- **High** — the 31 misses and the 35 catches at Highest and High. Every one was verified in
  context, and every miss was additionally tested against the retrieval extract to establish
  whether the evidence was in the pack.
- **High** — the Class B claims in §4. Each rests on a passage header naming the document, the
  score, the retrieving query and the group it landed in.
- **Moderate** — the Partial/Missed boundary at Moderate and Low severity. Several of those rest on
  the sentences around a token match rather than a full reading of the group.
- **Stated, not measured** — the attribution of the gain to the code changes rather than the
  re-index or the re-extraction. §1 gives the argument; it is not an experiment.

One thing I checked and did not report as a defect: passage category codes render as `[B]`, `[C]`,
`[G]` in citations while the plans use long forms. The extract carries 259 `[C]` and zero
`[C_MeetingNotes]`, so passages are correctly coded and targeted. It is a display convention, not a
retrieval fault.

---

## 12. What this run says to do next

1. **Stop treating recall as a retrieval problem.** Seven of the ten worst surviving misses have
   their evidence in the assessor's pack, in the correct group, undegraded. The next round belongs
   in how the group prompt directs attention within a pack, not in getting more into one.
2. **The comparison-basis clause needs a mechanism, not more words.** It is printed in all 88
   groups and was violated in five of them, with both dates quoted. A date-aware guard in code —
   the two sides of a comparison carry as-at dates and the assessor is told when they differ — is
   the shape that F1.17 argues for, and it is generic.
3. **Audit the No Issue path.** Both new No Issue verdicts print a real discrepancy and clear it.
   A group that returns No Issue while listing a difference between the report and the file should
   not be able to.
4. **Find out why a group's verdict can vanish.** Three groups across two runs were assessed and
   never printed. Whatever path allows that is a correctness bug in the report, independent of
   recall.
5. **Two runs per configuration, still not done.** Forty-two of 83 verdicts moved and there is no
   noise floor for this configuration. Nothing in §3 should be treated as an effect until a second
   run of each build exists.
6. **Route the extraction's contradictions more narrowly.** One soft inconsistency reached four
   checks and became four discrepancies.

The one-line conclusion: **the second round of changes was written against Test Case 2 and it
transfers — 31% to 42% on an unseen case, closing half the gap to the tuned case — but it
transfers entirely by finishing findings the pipeline was already reaching, and the findings it
never reaches are the ones that were never a retrieval problem in the first place.**
