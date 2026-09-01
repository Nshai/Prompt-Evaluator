# Implementation list — fixes from Run 17

Derived from [run-analysis.md §9](run-analysis.md). Seven items, ordered by cost-to-benefit rather
than by the section they came from. Every claim below was traced to code before being written down;
file:line references are to the state of the tree at commit `5ac867a`.

**Two of the seven turned out to be different work than §9 described.** Item 5 was going to relabel a
metric and is now a split of one count into two, because the metric was right and the first reading
of it was wrong. Item 7's guard bound was already applied in a previous commit and did not help, so
the work is a missing requirement rather than a missing sentence. Both are marked.

| # | Fix | Kind | Est. | Recovers |
| :-: | --- | --- | --- | --- |
| 1 | Section hints for the file note's ATR block on G3.6/G3.8 | plan data | S | **F3.1** |
| 2 | Report a hint that reached the corpus but not the group | code | S | diagnostic gap |
| 3 | Client DOB and party field assignment in extraction | prompt + code | M | F1.4/F1.5, KYC |
| 4 | The two charge tables in one pack on G7.4 | plan data | S | **F7.1**, then F9.5 |
| 5 | Split citation trust into altered vs restructured | code | M | metric credibility |
| 6 | Percentage-direction contradictions within a check | code | M | CHK-009 self-contradiction |
| 7 | A retention requirement in CHK-003 | plan data | M | **F3.4** |
| — | Re-run 8/24 on Haiku with cached extraction | measurement | — | attribution |

Items 1, 4 and 7 are the three that move the score. Items 2, 5 and 6 fix things that let a defect
pass unnoticed, which is the class of work that has repeatedly cost more than the defects.

---

## 1. Section hints for the file note's ATR wording block

**Target** `docs/artifacts/check-plan/CHK-003.query-plan.json` — groups `G3.6` and `G3.8`.

**Why.** F3.1 was Caught in every genuine run from Run 7 to Run 16 and is Missed in Run 17. The
passage carrying *"You are happy to proceed with a Risk rating of 6"* reached eight group prompts —
G2.1, G2.7, G4.5, G4.7, G6.7, G9.9, G10.3, G10.5 — and **no CHK-003 group**. Run 16's G3.6 pack
contained it twice; Run 17's does not, despite the pack growing from 123 to 164 passages.

**The mechanism, confirmed in code.** `CHK-003.query-plan.json` contains **zero** `evidenceSections`
entries — the only plan besides CHK-010 that has none. Section hints are what reserve a slot against
`MaxPassagesPerGroup`:

> `CheckPlanRunner.cs:713-720` — `if (!keep.Contains(i) && (targeted.Count == 0 || targeted.Contains(ordered[i].CategoryCode)) && ordered[i].SearchedText.Contains(hint, StringComparison.OrdinalIgnoreCase))`

With no hint declared, the file-note passage competes on embedding rank alone and lost its place when
the fresh canonical model changed the ranking. `AppSettings.cs:172` sets
`ReservedSlotsPerDeclaredSection = 1`, so each hint costs exactly one of the 24 slots.

**Change.** Add to `G3.6.retrieval` and `G3.8.retrieval`:

```json
"evidenceSections": [
  "You are happy to proceed with a Risk rating",
  "Attitude to Risk questionnaire"
]
```

**Match semantics to respect.** `Mentions` (`CheckPlanRunner.cs:784-786`) is a plain
`OrdinalIgnoreCase` substring test with **no whitespace or unicode normalisation**. The source file
note uses a curly apostrophe in *"didn't"*, so the hint must avoid apostrophes entirely — the wording
above does. Verify against
`docs/QA-Checks/TestCase-Files-Converted/C/File notes notes - updated.md:11`.

**Scope caveat.** A hint only promotes *within* the group's `targetCategories`
(`CheckPlanRunner.cs:713`, and the comment there: *"A hint promotes within what was requested; it
does not widen the request"*). G3.6 and G3.8 must already target category `C`. Confirm before
assuming the hint will bite.

**Done when.** `Risk rating of 6` appears in the rendered output under CHK-003, and the run footer
reports no new unmatched hint. This is the third consecutive analysis to recommend a section hint —
F1.9's, recommended after Runs 15 and 16, is still unapplied and should go in the same commit.

---

## 2. Report a hint that reached the corpus but not the group

**Target** `src/AiPromptEvaluator.Core/Services/Assessment/CheckPlanRunner.cs:834-865`
(`UnmatchedSections`), rendered at `CheckFinding.cs:805-831`.

**Why.** Two diagnostics already exist for exactly the F3.1 failure — *"Section hints matching
nothing"* and *"Section hints retrieved but evicted"*, the latter carrying the note *"This is a
ranking or cap problem, not a wording one"*. **Neither fired**, because both iterate the hints a
group declared and G3.6 declared none:

> `CheckPlanRunner.cs:840-843` — `var hints = sections.Where(...).ToList(); if (hints.Count == 0) { return SectionReach.None; }`

The pipeline has a purpose-built alarm for this and it is unreachable for any group without a hint.
That is the same shape of failure as the CHK-005 trigger: **silence read as success.**

**Change.** Item 1 makes the existing `Evicted` path reachable for G3.6, which is most of the value.
The residual gap is that a group with no hint gets no signal at all. Add a *corpus-reach* check that
does not depend on the plan declaring anything: for each group, report when a passage that a query
retrieved as a candidate did not survive into the pack — the `retrieved`-minus-`delivered` set
already computed at lines 846-847, just not summarised when `hints` is empty.

Keep it a count plus the top few by score, not a list of every eviction; the pack evicts most of what
it retrieves by design and a full dump would be noise.

**Done when.** A group whose plan names no section still reports how much of its retrieved candidate
set it discarded, and a test asserts the counter is non-zero for a group with a 164-passage candidate
pool and a 24-slot cap.

---

## 3. Client date of birth, and the party field assignment

**Target** extraction pass 1 (*Case and parties*) —
`src/AiPromptEvaluator.Core/Services/Extraction/CanonicalModelExtractor.cs`, the prompt in
`Constants/Prompts.cs`, and `docs/artifacts/canonical-suitability-model.schema.json:470` / `:823`
(`dateOfBirth` is defined in two places — confirm which the parties array uses).

**Why.** Run 17 is the first live extraction and the first to lose the client's DOB. G1.1 says so
three times: *"no date of birth extracted for client in canonical model"*. `07/06/1956` appears in
the retrieved passages but not in the model. Then G1.3 misattributes:

> Fact Find (P5) shows **client age as 65 with date of birth 07/05/1960**

07/05/1960 is **Kim's** DOB. The group built a four-year age discrepancy on the spouse's record. Run
16 made the mirror error on income (*"Fact find [P2] shows Kim's basic income as £1,200/month net"* —
that is John's line). **Two consecutive runs, two different fields, both directions.**

**Root cause hypothesis, to be confirmed before coding.** The Fact Find's First and Last Name fields
are blank with Middle Name "Alan" — benchmark finding F1.5, unscored. If parties are keyed by name,
the client has no key and the spouse's record is the only fully-named one. **Verify this against the
extraction log before changing the prompt**, because the alternative — that the pass simply dropped a
field — needs a different fix.

**Change, in order.**
1. Confirm the mechanism from `extract_ABC-99_20260827_094950.log`, pass 1 response.
2. If it is the name-key hypothesis: the parties prompt must key on **role** (client / spouse), not
   on name, and must record a party whose name fields are blank rather than skipping it.
3. Report an absent DOB in the extraction summary. Today the summary reports vocabulary
   misses — *"26 value(s) are outside the documented vocabulary"* — but says nothing about a required
   identity field being empty. An absent client DOB in a suitability QA tool should be as loud as an
   unrecognised `basis` string.

**Done when.** `/parties[]/dateOfBirth` is populated for both parties on a live extraction, a test
pins that a party with blank name fields is still extracted with its role, and the summary prints a
line when a required identity field is missing.

**Note.** This changes no benchmark verdict — F1.4 and F1.5 are Moderate and unscored. It is on the
list because it is a KYC gap that went unreported, and because it is now demonstrably propagating
into findings.

---

## 4. The two charge tables in one pack

**Target** `docs/artifacts/check-plan/CHK-007.query-plan.json` — group `G7.4`.

**Why.** F7.1 has been Partial for four runs. The pipeline **holds both contradicting rows and never
relates them** — G7.4's own citation list carries the page-5 Zurich 0.18% and the page-11 Zurich
0.93% side by side. That is the rubric's first Partial bullet exactly: *both halves present, never
joined, including when both are in the same group's own fields.*

Downstream, groups assert Standard Life's existing charge as 0.52% (G7.4, G7.6, G9.7) and as 0.18%
(G6.5, G7.5, G7.7, G8.5, G9.8, G9.9) with no group noticing the report gives both. **F9.5 is entailed
by F7.1** and is Partial for the same reason, so this is one change for two findings.

**Confirmed.** `G7.4` declares no `evidenceSections`; CHK-007 has exactly one across the whole plan.

**Change.** Add to `G7.4.retrieval`:

```json
"evidenceSections": [
  "Existing Arrangements",
  "Pension Switch Charges"
]
```

One reserved slot each. Wording must be checked against the converted suitability report, not the
PDF — the hint match is a raw substring test with no normalisation (item 1).

**Done when.** A single group's output names both tables and states that they disagree for Zurich and
for Standard Life. F7.1 to Caught is the target; F9.5 should follow, and if it does not, its remaining
gap is a separate finding worth recording.

---

## 5. Split citation trust into altered quotations and restructured tables

**Revised from §9.** The original recommendation assumed the metric was partly wrong. It is not.

**Target** `src/AiPromptEvaluator.Core/Services/Assessment/CitationVerifier.cs` and the render at
`CheckFinding.cs:791-803`.

**Why.** 146 untraceable quotes, 26% of 558 cited passages, the worst in the series. Checking all 145
rejected prose quotes against the converted corpus as fixed strings — folding case, whitespace and
typographic punctuation — gives **11 present verbatim, 134 absent.** Of the 46 carrying table pipes,
**36 have every individual cell present** and fail only on row structure. The Fact Find's three rows:

```
| **Total Net Monthly Income** | **£1,430.00** |
| Total Monthly Expenditure | £1,718.00 |
| Total Monthly Disposable Income | £-288.00 |
```

cited by G10.4 as one row with spaces inserted into the currency values:

```
table: Total Net Monthly Income  |  £ 1,430.00  |  Total Monthly Expenditure  |  £ 1,718.00  |  …
```

**Do not loosen the match.** `CitationVerifier`'s doc comment already records that a near-miss rule
was designed and rejected, and the reason is this exact case: the altered `Risk rating of 6` → `5`
quotation has a **96% contiguous run**, so any threshold loose enough to admit a reflowed table admits
a changed digit. `CellsPresent` requires every cell deliberately — *"a row read is a claim about the
whole row."* That judgement is correct and should be left alone.

**Change.** Report two counts instead of one:

- **Altered or unlocatable** — no cell or span traceable to the cited passage. The fidelity defect the
  class was built for. This is the number that can gate.
- **Restructured** — every claimed cell present in the cited passage, but merged, reordered or
  reflowed. A presentation defect: the finding is sound and the working is checkable with effort.

Classification is available where `CellsPresent` already runs — a row whose cells all appear
individually but not as a contiguous row is the *restructured* case.

**Also.** 15 citations name a `.pdf` source where the indexed corpus is `.md` (e.g.
`Fact Find (Test 1).pdf [B] (P5)`). Small, but it means a reviewer following the citation opens a file
the pipeline did not read. Worth normalising in the same commit.

**Done when.** The footer prints both counts, a test pins a merged-row citation as *restructured* and
a digit-altered quote as *altered*, and the gate is written against the altered count only.

---

## 6. Percentage-direction contradictions within a check

**Target** `src/AiPromptEvaluator.Core/Services/Assessment/CrossGroupContradictions.cs`.

**Why.** CHK-009 states the most material cost finding in the case in both directions. G9.8 is right:

> for Standard Life the cost actually **increases** by 0.26% annually

G9.7 and G9.9 are wrong, twice:

> indicating new solution is actually 0.08% **CHEAPER**

F9.3 survives on R2 because one group joins it correctly, but a reviewer reading CHK-009 top to
bottom meets the wrong answer twice. No previous analysis records the pipeline asserting a cost
direction and its opposite inside one check.

**Why the existing pass missed it — confirmed in code.** Two reasons, and only the second needs
changing.

1. **Not a scope problem.** The pairing skips `a.Where == b.Where`
   (`CrossGroupContradictions.cs:147`), which is same-*group*, not same-check. Cross-group pairs
   within one check are already eligible, and the doc comment says so: *"Groups are compared across
   checks as well as within one."*
2. **It is a matcher problem.** `MoneyPattern` (`:44-46`) matches `£`-prefixed amounts only, and the
   comment is explicit: *"Percentages and bare integers are deliberately not matched: the
   false-positive rate on years, ages, page numbers and risk ratings would bury the signal."* The
   CHK-009 contradiction is entirely in percentages, so nothing was ever claimed for it.

**Change.** Add a narrow percentage claim type rather than matching percentages generally — the
original reasoning against them stands. Scope it to **charge-difference direction**: a sentence
carrying a charge cue (`more expensive`, `less expensive`, `cheaper`, `increase`, `saving`) *and* a
percentage. Key the claim on the arrangement named (Zurich, Standard Life, Aviva) and record the
**sign**, then report when two groups give the same arrangement opposite signs.

That is a direction disagreement, not a magnitude one, so it does not need `MinimumDisagreement` and
will not fire on 0.43% against 0.44%.

**Done when.** The *figures described differently* block reports the Standard Life pair from Run 17's
own output, and a test pins that 0.26% increase against 0.08% cheaper for one arrangement is reported
while two different arrangements with different signs are not.

---

## 7. A retention requirement in CHK-003

**Revised from §9.** The guard bound recommended after Run 16 **has already been applied** and did
not recover the finding.

**Target** `docs/artifacts/check-plan/CHK-003.query-plan.json`.

**Why.** F3.4 has never been Caught in this series and fell from Partial to **Missed** in Run 17 —
sixth consecutive analysis to name it. The finding: the People's Pension is **retained** while
invested in *"Global Investments (up to 85% shares)"* at **risk level 9** against an agreed ATR of 5,
and the report tells the client that fund *"aligns with your risk appetite"*.

**What is already in place.** `CHK-003.query-plan.json:745`, on G3.7, carries the bound recommended
after Run 16, in almost the words that analysis used:

> "Compare on one scale only; where scales differ and no mapping is given, report the incomparability
> rather than a breach. **This does not excuse an assertion the report makes for itself:** where the
> report says a specific fund or plan aligns with the client's risk appetite, that claim is testable
> on its own terms…"

**And Run 17 still missed it, in a new way: no guard fired at all.** No group withdrew the finding —
G4.6 asked a different question and read risk 9 as a *fund-naming* problem (*"does not correspond to a
named fund risk scale in evidence"*), then used the holding as evidence the client **did** self-direct
equity investments, which is the opposite of a risk mismatch. Under the rubric's *actively wrong
statement* clause that is Missed and a false negative.

**So the guard bound was necessary and is not sufficient.** The word `retained` appears **once** in the
whole of CHK-003, inside that guard sentence. No requirement in the check asks about a plan the
recommendation keeps.

**Change.** Add a requirement to CHK-003 whose subject is *retained* arrangements: for each existing
arrangement with a Retain outcome, does its current fund align with the agreed risk rating, and does
the report make an alignment claim for it? Give it `evidenceSections` for the People's Pension fund
listing and `canonicalPaths` to the retained arrangements' fund and risk fields, so both halves — the
retained plan's rating and the report's alignment claim — land in one pack. That co-location is what
no run has yet had.

**Done when.** One group's output names the People's Pension as retained, sets its risk rating against
the agreed 5, and quotes or contradicts the report's alignment claim. F3.4 to Caught.

**Note.** Do not delete the G3.7 guard bound. It is correct and it is what will stop the new
requirement being dismissed on incomparable scales.

---

## Measurement, not a fix

**Re-run `8 / 24` on Haiku with the new plans and a cached extraction.** One run, no code change.

Run 17 changed the chat model, the plans digest **and** the canonical model against Run 16, and the
plans and the extraction against Run 12. Its 23 is a measurement of this configuration and an
attribution of nothing. Seven verdicts moved and none can be assigned between Haiku, the new plans,
and the live extraction.

**This run should happen before items 1, 4 and 7 land, not after** — otherwise their effect is
measured against a baseline that already has three uncontrolled variables in it. It is the cheapest
item on this page and the only one that makes the others interpretable.

**One caveat on repeatability.** Run 17 defeated the extraction cache incidentally, by changing the
plans enough to change the request. Items 1, 4 and 7 all change plans, so each will defeat it again —
which means every one of them will also change the canonical model, and the confound this measurement
exists to remove will return. If a live extraction is not wanted per change, the extraction cache
needs a deliberate bypass (`Services/Ai/PromptCacheBypass.cs` exists and is worth checking for
whether it already covers the extraction path).

---

## Not on this list, and why

- **`maxPassagesPerGroup: 12`.** Still untested and still worth testing, but Run 16 §1 is the
  strongest single-variable result the project has and nothing in Run 17 disturbs it. It is a
  measurement, not a fix, and it queues behind the one above.
- **F1.6 / F5.4 (the tenant findings).** Missed for the seventh and sixth consecutive run. The Run 14
  analysis established the evidence reaches the model and three purpose-written guards are in the
  prompt, so this is not a retrieval or plan-data fix and no cheap change is known. It needs its own
  investigation rather than another guard sentence.
- **Prompt adherence (45 of 87 wrong requirement ids).** Tracks the model exactly — every Haiku run
  38-52%, every Sonnet run 0% — and the plan's values are used regardless, so no verdict is affected.
  Not worth code.
- **The four *"ranked by Maturity Value"* hints matching nothing**, fifth consecutive run. Worth
  noting that four Caught findings (F6.1, F8.2, F9.2, F9.4) currently rest on that table surfacing
  through an unrelated search. Fixing the hint wording would make them robust rather than lucky, but
  it recovers nothing today, so it belongs with item 1's commit rather than as its own item.
