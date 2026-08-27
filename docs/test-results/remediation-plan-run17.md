# Remediation plan — Run 17 and the facts/extraction gap analyses

One plan consolidating the findings of three documents:

- [Run 17 run-analysis.md](Runtime-Logs/latest/Run-17/run-analysis.md) — the scored run
- [material-facts-gap-analysis.md](material-facts-gap-analysis.md) — the 60 re-extracted facts
- [Run 17 extraction-gap-analysis.md](Runtime-Logs/latest/Run-17/extraction-gap-analysis.md) — the canonical model artefact

It supersedes [Run 17 implementation-list.md](Runtime-Logs/latest/Run-17/implementation-list.md),
written before the canonical model was examined.

---

## The architecture, stated first, because two of my earlier conclusions violated it

**The suitability report is the source of assertions. The supporting documents are the evidence those
assertions are verified against. The two must not mix.**

That is the design, it is deliberate, and it is written into the code:

> `CanonicalModelExtractor.cs:39-45` — *"The canonical model describes what the report asserts, so
> extraction reads category I and nothing else — evidence documents are what the report is later
> checked against, and **folding them in here would erase the distinction the checks depend on**."*

So the pipeline has exactly two channels, and each carries one side of every comparison:

| | Channel | Carries | Category |
| --- | --- | --- | --- |
| **Assertion side** | canonical model | what the report claims | `I` only |
| **Evidence side** | retrieval / evidence pack | what the file records | `B, C, D, E, F, G, H` — **not `I`** |

**Three corrections follow, and they shrink the plan.**

**1. `questionnaireResponses[]` is dropped.** The facts analysis called it *"the only item that moves
the benchmark."* The questionnaires are in the risk profile report — the evidence side. Adding the
field to the canonical model would either produce an empty array or breach the boundary. **The
questionnaire answers belong in retrieval, and the work is making retrieval reach them reliably.**

**2. `[I]` must come out of the evidence pack.** My previous draft argued for keeping it, on the
grounds that report-versus-report contradictions such as F7.1 travel through it. **That was wrong on
the architecture and wrong on the facts.** A report-internal contradiction is two assertions
disagreeing, and both assertions belong in the canonical model — that is what the model is for. The
report appearing on the evidence side means a group can treat the document under audit as
corroboration of itself, which is not a hazard in theory but an observed defect: Run 15's G3.2 wrote
*"the income and expenditure figures are corroborated by [P11]"* where `[P11]` **was the suitability
report.** Runs 15 and 16 both recommended removal. This plan implements it.

**3. F7.1 is an extraction gap, not a retrieval gap — and that only became visible once (2) was
settled.** See item 3. It is the most useful thing in this document.

---

## The plan

Ten items in four tiers. Tier 0 is one run and must go first. Tier 1 is the recall work. Tier 2 is
architectural. Tier 3 is tidying.

| Tier | # | Item | Kind | Recovers |
| :-: | :-: | --- | --- | --- |
| **0** | 0 | Baseline run: Haiku, `8/24`, cached extraction | measurement | attribution |
| **1** | 1 | Section hints for the file note ATR block (G3.6/G3.8) | plan data | **F3.1** |
| | 2 | Section hints for the CFL questionnaire (G3.2/G3.4) | plan data | **F2.2** |
| | 3 | **Extract the page-11 switch-charge table** | schema + extraction | **F7.1 → F9.5** |
| | 4 | A retention requirement in CHK-003 | plan data | **F3.4** |
| | 5 | Fact Find section hints for residency (G1.5/G5.2) | plan data | F1.6, F5.4 |
| **2** | 6 | **Remove `[I]` from the evidence pack** | code + plan data | false corroboration |
| | 7 | Fail loudly when a trigger contradicts its own model | code | CHK-005 class |
| | 8 | Report a group's unreached retrieval, hint or no hint | code | diagnostic gap |
| **3** | 9 | Split citation trust; give `lifeExpectancyBasis` a reader | code + plan | metric, unread fact |
| | 10 | Percentage-direction contradictions within a check | code | CHK-009 self-contradiction |

---

## Tier 0 — the baseline, before anything else

### 0. One run: Haiku, `8 / 24`, cached extraction, current plans

Run 17 changed the chat model, the plans digest **and** the canonical model against Run 16. Its
23/7/6 is a measurement of that configuration and an attribution of nothing.

**Every Tier 1 item changes the plans, and the plans digest is what defeated the extraction cache in
Run 17** — so each will also force a fresh canonical model, reintroducing the confound.

**Check whether `Services/Ai/PromptCacheBypass.cs` covers the extraction path.** If it does, the
extraction can be pinned across plan changes and every later comparison becomes single-variable. If it
does not, adding that is worth more than most items here: it is the difference between a measurable
programme and another series of uncontrolled runs.

---

## Tier 1 — recall

### How section hints work, and the trap in them

Items 1, 2, 4 and 5 use `evidenceSections`, matched by `Mentions`
(`CheckPlanRunner.cs:784-786`):

```csharp
hints.Any(h => text.Contains(h, StringComparison.OrdinalIgnoreCase))
```

**A plain case-insensitive substring test — no whitespace normalisation, no unicode folding.** Three
rules, and breaking any produces a hint that silently matches nothing:

- **No apostrophes.** The sources use curly `’`; a hint with `didn't` will not match `didn’t`.
- **Do not depend on table pipes or cell spacing** — the converted Markdown's spacing is not stable.
- **A hint only promotes within the group's `targetCategories`** (`CheckPlanRunner.cs:713`: *"A hint
  promotes within what was requested; it does not widen the request"*). Confirm the group targets the
  document's category first.

Each hint reserves one of the 24 slots (`AppSettings.cs:172`). Verify every hint against the converted
Markdown before committing.

### 1. The file note's ATR wording block → G3.6, G3.8

**F3.1**, Caught in every genuine run from Run 7 to Run 16, Missed in Run 17. The passage carrying
*"You are happy to proceed with a Risk rating of 6"* reached eight group prompts — G2.1, G2.7, G4.5,
G4.7, G6.7, G9.9, G10.3, G10.5 — and **no CHK-003 group.** Run 16's G3.6 pack had it twice; Run 17's
pack grew from 123 to 164 passages and lost it.

`CHK-003.query-plan.json` contains **zero** `evidenceSections` — the only plan besides CHK-010 with
none. Add to `G3.6.retrieval` and `G3.8.retrieval`:

```json
"evidenceSections": [
  "You are happy to proceed with a Risk rating",
  "Attitude to Risk questionnaire"
]
```

Verify against `docs/QA-Checks/TestCase-Files-Converted/C/File notes notes - updated.md:11`.

### 2. The CFL questionnaire → G3.2, G3.4

**F2.2** (Highest) has never been Caught in seventeen runs. Its evidence is the CFL questionnaire's
*"Less than 3 years"* and the Fact Find's *Time Horizon: Short Term* — **neither string appears
anywhere in Run 17's 2,546 lines.** Every run reaches the 2-year conflict from the risk profile report
instead, which the rubric scores Partial under R4: right conclusion, wrong evidence.

**This is the item the dropped `questionnaireResponses[]` was meant to serve, relocated to the correct
channel.** The questionnaire is evidence, so retrieval is not a workaround here — it is the right
mechanism, and it has to be made reliable. Hints on the CFL questionnaire block and the Fact Find's
Time Horizon row, bound to G3.2 and G3.4. F3.7 (*"My spouse… Disagree"*) rides along.

### 3. Extract the page-11 switch-charge table — F7.1 is an extraction gap

**This is the most useful finding in this document, and it was invisible until `[I]`'s role was
settled.**

F7.1 has been Partial for four runs. The benchmark's finding: the report's **two** charge tables
contradict each other — Existing Arrangements (p5) gives Zurich **0.18%** and Standard Life **0.52%**;
Pension Switch Charges (p11) gives Zurich **0.93%** and Standard Life **0.18%**.

Walking Run 17's canonical model for every charge percentage attached to those two plans returns
**exactly two rows, both from page 5**:

```
p5   0.18   Zurich fund charge           /costsAndCharges/existing/lines[5]
p5   0.52   Standard Life fund charge    /costsAndCharges/existing/lines[7]
```

**The page-11 table is not in the model at all.** `replacementAnalysis[].costsOfSwitching` holds only
`exitPenalty: 0` for all four arrangements. `costsAndCharges` has `existing`, `recommended`,
`comparison`, `reductionInYield`, `valueAssessment`, `disclosureCompleteness` — and no per-arrangement
switch-charge comparison.

So F7.1 could never have been Caught by any amount of retrieval tuning. **One side of the contradiction
was never extracted**, and every run that "reached" it was reading the p11 figures out of a retrieved
report passage — which is exactly the `[I]`-as-evidence route item 6 removes. **Remove `[I]` without
this item and F7.1 goes from Partial to Missed.** The two must land together.

The fix is on the assertion side, where it belongs:

- **Schema.** Add a per-arrangement switch-charge comparison — existing charge, recommended charge,
  difference, monetary impact — either as `replacementAnalysis[].chargeComparison` or as a scoped
  `ChargeSet` on each arrangement. The `ChargeLine` type already carries `layer`, `chargeType`,
  `percentage`, `basis` and `provenance`; this needs a container that keeps the p11 reading distinct
  from the p5 one **rather than merging them** — the whole finding is that they disagree.
- **Extraction.** Pass 9 (*Costs and charges*) or pass 10 (*Replacement analysis*) already owns this
  region. The prompt must be told the report may state a plan's existing charge in more than one place
  and that **each statement is recorded separately with its page**, never reconciled.
- **Query plans.** `canonicalPaths` on G7.4 to both charge sets, so one group holds both figures.

**F9.5 is entailed by F7.1** and should follow. Note the extractor already found the *adjacent*
contradiction unprompted — `extractionReport.internalInconsistencies` entries 1 and 2 name the p7-vs-p11
narrative mismatch — which is evidence it will state this one too once both figures are in front of it.

### 4. A retention requirement in CHK-003

**F3.4** has never been Caught and fell from Partial to **Missed** in Run 17 — sixth consecutive
analysis to name it.

**The guard bound recommended after Run 16 was already applied** (`CHK-003.query-plan.json:745`, on
G3.7). Run 17 still missed it, in a new way: **no guard fired.** G4.6 asked a different question,
reading risk 9 as a *fund-naming* problem (*"does not correspond to a named fund risk scale in
evidence"*) and then using the holding as evidence the client **did** self-direct equity investments —
the opposite of a risk mismatch. Under the rubric's *actively wrong statement* clause that is Missed
and a false negative.

The word `retained` appears **once** in all of CHK-003, inside that guard sentence. **No requirement
asks about a plan the recommendation keeps.** Add one: for each arrangement with a Retain outcome, does
its current fund align with the agreed risk rating, and does the report make an alignment claim for it?
`canonicalPaths` to the retained arrangement's fund and risk fields — those *are* in the model —
plus a hint for the People's Pension fund listing on the evidence side.

**Do not delete the G3.7 guard bound.** It is correct, and it is what stops the new requirement being
dismissed on incomparable scales.

### 5. The Fact Find's residency row → G1.5, G5.2

**F1.6 and F5.4**, Missed for the seventh and sixth consecutive run. `main residence` appears nowhere
in Run 17's output; `Tenant – private` appears nowhere.

**Lowest-confidence item in Tier 1, included with a caveat.** Run 14 showed that with the cap off,
G1.5's pack grew to 44 passages, the residency row was demonstrably in the prompt, three
purpose-written guards were present — *and the finding was still not made.* That analysis concluded:
*"F1.6 is a verification failure. Not retrieval, not the cap, not the render."*

A hint may well not fix it. Worth one attempt because it is nearly free; **if it fails, F1.6 needs its
own investigation and must not absorb a fourth guard.** Record the outcome either way.

---

## Tier 2 — architecture

### 6. Remove `[I]` from the evidence pack

**The correction that reframes this plan.** Runs 15 and 16 both recommended it; this plan implements
it, and item 3 is the prerequisite that makes it safe.

**Current state.** `[I]` is a `targetCategories` entry **202 times across all ten plans** — 25 in
CHK-001, 28 in CHK-007, 28 in CHK-009 — while only two plans name it in `evidenceCategories`. The
result in Run 17: **151 of 558 cited passages (27%) are the suitability report being used as evidence
against itself.**

**Why it is wrong, not merely redundant.** Every check is *report says X, file says Y, do they agree?*
With `[I]` in the pack, the file side can be satisfied by the report, and a group can conclude the
document under audit corroborates itself. Run 15's G3.2 did exactly that: *"the income and expenditure
figures are corroborated by [P11]"*, where `[P11]` was the suitability report. Run 16's G3.2 got it
right on the same requirement and the same passage — **so whether it fires is a coin toss**, which is
worse to leave in place than a consistent fault, because an intermittent false corroboration is harder
to catch in review.

**Change.** Remove `"I"` from every `targetCategories` and `evidenceCategories` list in the ten query
plans, and enforce it: `CheckPlanLint` should reject a plan naming category `I` on the evidence side.
The report reaches the assessor through the canonical model, which is the channel built for it.

**Sequencing, and the risk.** Any finding currently reached by reading a report passage will be lost
unless the corresponding assertion is in the model. **Item 3 is the known instance** — F7.1's p11
figures. There may be others that only a run will reveal. So:

1. Land item 3 first and confirm F7.1 improves.
2. Then land item 6 and read the delta carefully. A finding that regresses identifies an assertion the
   extraction is missing — which is **information, not a setback**: it is the same diagnostic as F7.1,
   applied to whatever else was quietly relying on the wrong channel.
3. Expect `extractionReport.internalInconsistencies` to carry more of the weight afterwards. It is
   read by exactly one group today (CHK-001) and is the natural home for report-internal
   contradictions once `[I]` is gone. Consider widening its readers in the same commit.

**This item may cost findings in the short run and is still right.** The alternative is a pipeline
whose evidence side can be satisfied by the document under audit, which makes every clean verdict
unreliable in a way no score reveals.

### 7. Fail loudly when a trigger contradicts its own model

**Three triggers in Run 17's artefact disagree with the data beside them:**

```
hasComplexProduct: false          ← same section: complexity: "Complex", six named drivers
hasVulnerabilityIndicators: false ← CHK-010's own triggerField; file full of FG21/1 indicators
```

Both harmless, for unrelated reasons: no plan reads `hasComplexProduct`, and CHK-010 is an
unconditional overlay (*"No trigger was recorded; this check applies to every case and was assessed
anyway"*). **CHK-005 was not harmless** — the same class switched it off in Runs 15 and 16, and two
analyses reported a cache artefact as a cleared check. Run 17's live extraction fixed that *instance*;
the class is untouched, and CHK-010 escaping by luck is the warning.

Add a post-extraction consistency check over known pairs — `hasComplexProduct` against
`recommendedProductComplexity[].complexity`, `hasCapitalContributionsOrWithdrawals` against
`existingArrangements[].contributions`/`withdrawals`, `hasVulnerabilityIndicators` against
`vulnerability.perClient[]`. Where a trigger is `false` and the model implies `true`: print it, and
**refuse to gate a check on it.**

This is *"make a disabled check loud"* from Runs 15, 16 and 17, narrowed from a reporting preference to
a mechanical test.

### 8. Report a group's unreached retrieval, hint or no hint

**The pipeline has a purpose-built alarm for F3.1's exact failure and it was unreachable.**
`EvictedSections` prints *"A passage carrying each was retrieved and did not survive into the pack…
This is a ranking or cap problem, not a wording one"* — and it iterates the hints a group *declared*:

> `CheckPlanRunner.cs:840-843` — `if (hints.Count == 0) { return SectionReach.None; }`

CHK-003 declared none, so both diagnostics printed nothing while a five-run finding vanished.

Item 1 makes the `Evicted` path reachable for G3.6. The residual gap is that a group with no hint gets
no signal at all. Report, per group, how much of its retrieved candidate set did not survive into the
pack — `retrieved`-minus-`delivered` is already computed at `CheckPlanRunner.cs:846-847` and simply not
summarised when `hints` is empty. A count plus the top few by score; the pack evicts most of what it
retrieves by design, so a full dump would be noise.

---

## Tier 3 — tidying

### 9. Split citation trust, and give `lifeExpectancyBasis` a reader

**Citation trust.** 146 untraceable quotes, 26% of 558 cited passages, worst in the series. Checking
all 145 rejected prose quotes as fixed strings with case, whitespace and typographic folding: **11
present, 134 absent.** Of the 46 carrying table pipes, **36 have every cell present** and fail only on
row structure — the Fact Find's three rows cited as one, with spaces inserted into the currency values.

**Do not loosen the match.** `CitationVerifier`'s doc comment records that a near-miss rule was
designed and rejected because the altered `Risk rating of 6` → `5` quote has a **96% contiguous run**:
any threshold admitting a reflowed table admits a changed digit. That judgement is correct.

Report two counts: **altered or unlocatable** (the fidelity defect; this is what can gate) and
**restructured** (every cell present, row rewritten). Classification is available where `CellsPresent`
already runs. Also normalise the 15 citations naming a `.pdf` where the indexed corpus is `.md`.

**`lifeExpectancyBasis`.** Populated in the artefact (`array(2)`) and read by **no** query-plan group —
the one genuine publish-without-a-reader, which
[fact-library.map.json](../artifacts/fact-library.map.json)'s own `$comment` calls *"worse than not
extracting it at all."* Add `canonicalPaths` on the CHK-002 time-horizon group and the CHK-005 cashflow
group; optionally add `assumedAge` so the assumption is comparable against `/parties/clients[]/age`
arithmetically. Recovers no benchmark finding.

### 10. Percentage-direction contradictions within a check

CHK-009 states the most material cost finding in both directions. G9.8: *"for Standard Life the cost
actually **increases** by 0.26% annually."* G9.7 and G9.9: *"the new solution is actually 0.08%
**CHEAPER**."* F9.3 survives on R2 because one group joins it correctly, but a reviewer reading CHK-009
top to bottom meets the wrong answer twice.

**Not a scope problem.** `CrossGroupContradictions` skips `a.Where == b.Where` — same-*group*, not
same-check — so within-check pairs are already eligible. **It is a matcher problem:** `MoneyPattern`
(`:44-46`) matches `£`-prefixed amounts only, and deliberately — *"Percentages and bare integers are
deliberately not matched: the false-positive rate on years, ages, page numbers and risk ratings would
bury the signal."* The CHK-009 contradiction is entirely in percentages.

Add a narrow charge-direction claim rather than matching percentages generally. Scope it to a sentence
carrying a charge cue (`more expensive`, `less expensive`, `cheaper`, `increase`, `saving`) *and* a
percentage; key on the arrangement named; record the **sign**; report opposite signs for one
arrangement. A direction disagreement needs no `MinimumDisagreement` and will not fire on 0.43%
against 0.44%.

**Item 3 will help here too** — much of the confusion comes from groups reading a charge figure out of
a report passage without knowing which table it came from.

---

## Not in this plan, and why

- **`questionnaireResponses[]` or any Fact Find representation in the canonical model.** Breaches the
  assertion/evidence boundary. The questionnaires are evidence; item 2 is the correct fix.
- **A `risk.personality` field.** The two spreadsheet facts are Balanced-investor *description* text,
  not client data, and `attitudeToRisk.description` holds it verbatim in the artefact.
- **Anything for the 26 `recommendation.reason` / `fund.reason` facts (43% of the spreadsheet).** All
  present as `Statement` objects with topics and quotes, all read, all reaching the output.
- **New fields for `risk.objective`, `risk.time-horizon`, `risk.strategy`.** `Statement[]` homes are
  adequate; the imprecision costs nothing measurable.
- **`maxPassagesPerGroup: 12`.** Untested and worth testing; Run 16 §1 remains the strongest
  single-variable result the project has. A measurement, queued behind item 0.
- **Prompt adherence (45 of 87 wrong requirement ids).** Tracks the model exactly — Haiku 38–52%,
  Sonnet 0% — and the plan's values are used regardless.

---

## Sequencing and expected effect

**Order:** 0 → 1, 2, 4, 5 (one commit each) → **3 → 6** (in that order, see item 6) → 7, 8 → 9, 10.

**One commit per Tier 1 item.** Items 1 and 2 both touch CHK-003; items 1 and 4 both target the same
check's recall. Landed together, a recovery cannot be attributed — the single most repeated complaint
across seventeen run analyses.

| Item | Expected | Confidence |
| --- | --- | --- |
| 1 | F3.1 Missed → Caught | High — Caught for five runs on the same evidence |
| 2 | F2.2 Partial → Caught | Medium — never Caught, but the cause is now identified |
| 3 | F7.1 Partial → Caught, F9.5 follows | Medium-high — one side of the contradiction currently absent |
| 4 | F3.4 Missed → Caught | Medium — third attempt |
| 5 | F1.6 / F5.4 unchanged | **Low** — Run 14 showed the evidence already reaches the model |
| 6 | **Possible short-term losses**, then stable | see item 6 — losses are diagnostic |
| 7–10 | No verdict change | by design |

**A full sweep would put Run 17's 23 at 27–28 of 36.** A target, not a forecast: the noise floor is ±2
findings and most of these are one-sample changes. **Item 6 is the exception to the "more is better"
reading** — if it costs findings, that identifies assertions the extraction is missing, and each one is
the same class of fix as item 3.

**Discipline that keeps this readable:** report Caught / Partial / Missed with the movement and what
else changed, per [scoring-rubric §5](scoring-rubric.md#5-reporting). Any item whose prediction fails
should be recorded as failed rather than re-attempted with another guard — that is how F1.6 accumulated
three.
