# Gap analysis — run of 2026-08-12, case ABC-99

> **Status.** GAP 1 is addressed (citation verification, an `Indeterminate` outcome,
> reasoning-before-verdict ordering, one call per requirement), as is GAP 2 (category filtering)
> and GAP 6's indexing failure. GAP 3 and 4 remain open, as does the reporting half of GAP 6;
> GAP 5 should largely resolve with GAP 2 and needs re-measuring. The figures below describe the
> run as it was, and are the baseline the next run should be measured against.

Derived from the actual run recorded in [logs.md](logs.md), against the ten checks in
[Assessment Checks & Prompts QA CA v1.0 (Checks).csv](QA-Checks/Assessment%20Checks%20&%20Prompts%20QA%20CA%20v1.0%20(Checks).csv),
the ten query plans in [artifacts/check-plan](artifacts/check-plan), and the 29 converted
documents in [Feasability-test-case-converted](QA-Checks/Feasability-test-case-converted).

## What the run did

| | |
| --- | --- |
| Checks executed | 10 of 10 |
| Query groups executed | 60 of 60 (57 with retrieval, 3 assertion-only by design) |
| Searches issued | 154 |
| Passages returned | 1,232 → 649 after de-duplication and the 12-per-group cap |
| Canonical paths resolved | 108 |
| Canonical paths absent | 37 |
| Group outcomes | 38 No Issue, 22 Potential Concern |
| Check outcomes | 9 Potential Concern, 1 No Issue (CHK-005) |

**The pipeline itself is sound.** Every plan loaded, every group ran, nothing errored, no group
returned zero evidence. It also found real defects — the £1,300 vs £1,200 monthly income
discrepancy, and a material contradiction where the report justifies a higher risk rating on a
10-year horizon while the risk profile document records a 2-year investment period.

The gaps below are about what it *missed* and *why*.

---

## GAP 1 — The assessor manufactures reconciliations to reach No Issue

**Severity: critical.** Two independent instances, in different checks, on different evidence
types. Both converted a Potential Concern into a No Issue. This is the finding that undermines
trust in every other output.

### 1a — A quotation was altered

The category C file note contains, in consecutive sentences:

> We agreed that you are a risk rating **5** rather than a **4** as per the questionnaire.
> You are happy to proceed with a Risk rating of **6**.

Four, then five, then six. This is precisely what CHK-003 **G3.6** exists to catch — *"Where
agreed rating differs from questionnaire result, the override is justified"*.

That text **was in the evidence pack.** I verified it: the passage containing "Risk rating of 6"
was retrieved and presented to the assessor.

G3.6 returned **No Issue**, and its explanation reads:

> Client acknowledgement is explicit: 'You are happy to proceed with a Risk rating of **5**.'

The quote is presented as verbatim. The source says 6. The assessor changed the digit, which
made the evidence consistent with the recommendation, and cleared the check.

This is not a retrieval failure, a plan failure or an extraction failure. Everything upstream
worked. **A fabricated citation is worse than a missed finding**, because a missed finding leaves
no trace and this leaves a false one — a reviewer reading the QA output sees a documented,
consented override that never happened.

### 1b — Impossible arithmetic was invented to dismiss an age mismatch

CHK-001 **G1.1** — *"Essential facts (personal details) stated in the report are consistent with
the evidence"*.

| Source | States |
| --- | --- |
| Fact Find (B), completed 19/09/2025 | `Date of Birth 07/06/1956` and `Age 70` |
| Suitability Report (I), dated 14/10/2025 | "You are currently aged **69**" |

A date of birth of 7 June 1956 gives **69** at both dates. The Fact Find's own Age field
contradicts its own Date of Birth field, and the report's 69 is the arithmetically correct
figure — so the finding here is a data-quality defect in the Fact Find on a KYC essential fact.
Either way it is not a No Issue.

G1.1 returned **No Issue**, on this reasoning:

> DOB 07/06/1956 (age 70 as at fact find date 22/09/2025, reconciling to 69 at report date
> 14/10/2025 within the 1-year tolerance)

Three errors in one sentence, all pushing toward a pass:

1. **Age cannot decrease.** Nobody is 70 in September and 69 in October.
2. **The tolerance was applied backwards.** The plan's tolerance reads *"Age may differ by 1 year
   where the fact find predates the report"* — the earlier document should show the **younger**
   age. Here it shows the older one, a direction the tolerance does not cover.
3. **The fact find date is wrong.** Stated as 22/09/2025; the document says 19/09/2025.

**The plan had already anticipated this exact comparison.** G1.1 carries the guard *"The report
often states age while the fact find states date of birth — derive before comparing, using the
report date."* Deriving from the DOB gives 69, matches the report, and identifies the Fact Find's
Age field as the error.

**An extraction gap made that guard impossible to follow.** `/parties/clients[]/dateOfBirth` is one of the
37 unresolved canonical paths (GAP 4). The model supplied `age` but not `dateOfBirth`, so the
assertion side held nothing to derive from. The assessor closed the gap by inventing arithmetic
rather than reporting that it could not perform the comparison.

### What this means

Two instances is a pattern, not an outlier. Both bent evidence rather than reported a conflict,
and both moved a Potential Concern to a No Issue — the expensive direction. The system prompt
already instructs the assessor to *"Highlight every mismatch you identify explicitly and without
masking it"* and to state Potential Concern whenever a genuine mismatch is found. Instruction
alone is not holding.

**What's missing:** nothing verifies a finding against the pack it was drawn from. Quotes are
asked for verbatim and never checked; derived figures are asked for and never recomputed.

**Fix, in two parts — both deterministic, neither needing a model call:**

1. **Verify every citation.** Confirm each `quote` appears in the passages actually supplied for
   that group. A quote that does not is a fabrication: fail the group rather than flag it. Catches
   1a outright.
2. **Compute the facts the comparison turns on rather than asking for them.** Age from date of
   birth against the report date, arithmetic on charge tables, date ordering — these are the
   comparisons the checks keep failing, and they are all cheaper and safer computed in code than
   delegated. Where a required input is absent from the model, the group should report *"the
   comparison could not be performed"*, which is a finding in its own right and the one thing
   neither instance produced.

Until these land, a No Issue from this pipeline cannot be relied on — and the retrieval fixes
below make the problem larger, not smaller, since more evidence is more material available to be
reconciled away.

---

## GAP 2 — Category C is structurally starved of retrieval

**Severity: high.** The most decisive document in the case is the one the search almost never
returns.

| | Category | Docs | Groups targeting it | Groups it reached | |
| --- | --- | --- | --- | --- | --- |
| A | Client Authority | 2 | 5 | 15 | 300% |
| B | Know Your Client | 1 | 24 | 20 | 83% |
| **C** | **Meetings & Communications** | **1** | **16** | **5** | **31%** |
| D | Risk/Capacity/Experience | 1 | 8 | 16 | 200% |
| E | Existing Plans & Portfolios | 12 | 12 | 25 | 208% |
| F | Needs Analysis | 2 | 21 | 23 | 109% |
| G | Research | 3 | 22 | 38 | 172% |
| H | Solution Design | 5 | 17 | 34 | 200% |
| I | Recommendations & Advice | 1 | 60 | 33 | 55%\* |

\* *I is lower by design — the report's assertions reach the assessor through the canonical
model rather than through retrieval.*

Category C is a single 4.9 KB file note. It carries the ATR override reasoning, the capacity for
loss assessment, and the client's recorded understanding — the evidence for CHK-003, CHK-004 and
CHK-010. Sixteen query groups target it. It reached five.

**Root cause.** `CaseDocumentStore.SearchAsync` filters on tenant and case only. Every search is
an unfiltered top-8 across the whole case file, so one small document competes against twelve
category E policy documents and eight large G/H documents — 20 of the 29 files. The categories
with the most text win, and the table shows exactly that: every over-surfaced category is a
document-heavy one, and both under-surfaced categories (B and C) have a single file.

The plans anticipated this and compensate with `targetCategories` as a post-filter. **The
compensation cannot work**, because post-filtering only reorders passages that are already in the
candidate set. If C never enters the top 8, no amount of re-ranking retrieves it.

Twelve groups explicitly wanted C and never received it:

```
CHK-002  G2.1 G2.3 G2.4 G2.5        CHK-004  G4.1 G4.4 G4.5
CHK-006  G6.1 G6.3                  CHK-010  G10.1 G10.2 G10.3 G10.4
```

All four CHK-010 groups — the vulnerability and foreseeable-harm overlay — missed the one
document recording what was actually discussed with the client.

**Fixed.** `CaseDocumentStore.SearchAsync` takes a category filter, using the payload index that
had existed unused since the collection was first created.

The plan's categories are applied as a *second* search rather than as a restriction. Each query
is embedded once and run twice — once restricted to the categories the plan names, once across
the whole case — and the results merged with the targeted hits first. Restricting alone would be
wrong: target categories are where evidence is *expected*, not the only place it can be, and a
hard filter would silently discard a contradiction sitting somewhere nobody thought to name.
The second query costs almost nothing, since the embedding is the expensive part and is shared.

`Search_CanBeRestrictedToDocumentCategories` runs against a real Qdrant and reproduces this gap
in miniature: one weakly-matching note in category C against three strong matches in category E.
It asserts the unfiltered search misses the note — the defect itself — and that the targeted one
finds it.

**Still to do:** re-run the case and re-measure. The 31% figure above is the baseline.

---

## GAP 3 — "No evidence" is unrepresentable

**Severity: high.** Every one of the 154 searches returned exactly 8 passages. 154 × 8 = 1,232,
with no exceptions.

That is a dense vector search doing what it is told: return the nearest *k*, regardless of
whether anything is actually relevant. There is no score floor. Consequences:

**The zero-hit branch is dead code.** `CheckPlanRunner` has a carefully written path for *"Nothing
was retrieved. The searches ran and returned no relevant passage, so treat this as the case file
not evidencing the point."* It fired zero times in this run and cannot fire under the current
implementation.

**The plans' sufficiency rules cannot trigger.** Every group carries `ifEvidenceAbsent`
instructions. Retrieval can never report evidence as absent, so those rules are unreachable from
the retrieval side.

**The system prompt makes a promise it does not keep.** It tells the assessor *"The pack tells you
which searches found nothing; say which one you are relying on."* The pack never says this.

**The pack is always full.** 84% of groups hit the 12-passage cap; 47% of retrieved passages were
discarded as duplicates. Whether a group found strong corroboration or nothing relevant, the
assessor receives a dozen passages and no way to tell the difference. The weakest scores in the
run sit around 0.60 — plausible-looking but well below anything that should count as evidence.

**Fix.** Apply a minimum score threshold before a passage enters the pack, and let groups legitimately
return nothing. Calibrate the threshold from this run's score distribution rather than guessing.
The 6 `expectSignals` misses already recorded are the closest thing to a working absence signal —
worth extending rather than leaving as the only one.

---

## GAP 4 — Extraction does not populate fields the plans depend on

**Severity: medium.** 37 canonical path references across 33 distinct paths resolved to nothing.
Six groups received an assertion side that was entirely empty:

| Check | Group | Requirement | Paths that resolved to nothing |
| --- | --- | --- | --- |
| CHK-001 | G1.2 | Tax status consistent with evidence | the whole `taxStatus` subtree, `territorialProfile` |
| CHK-001 | G1.1 | Essential personal facts consistent | `dateOfBirth` — see [GAP 1b](#1b--impossible-arithmetic-was-invented-to-dismiss-an-age-mismatch) |
| CHK-006 | G6.1 | Client-specific rationale per recommendation | `rationale[].text`, `rationale[].isClientSpecific`, `linkedObjectiveIds` |
| CHK-006 | G6.3 | Personalised language linked to circumstances | `rationale[].isClientSpecific` |
| CHK-007 | G7.7 | Alternatives offer better cost or benefit | `alternativesConsidered[].comparativeCost` |
| CHK-010 | G10.1 | Additional support and third-party support | `supportNeeds[]`, `thirdPartySupport` |
| CHK-010 | G10.2 | Communication preferences and formats | `communicationPreferences` |

CHK-006 is the clearest case: both groups that judge whether the advice rationale is
*client-specific* had nothing at all on the assertion side, because the extraction never captured
the rationale text or the flag. The check ran and reached an outcome; it just did so without the
thing it was built to examine.

**`dateOfBirth` shows how an extraction gap becomes a wrong answer.** G1.1's guard requires the
client's age to be derived from date of birth against the report date. The model held `age` but
not `dateOfBirth`, so the derivation was impossible — and rather than say so, the assessor
invented one. An absent path is not only missing information; it is an invitation to fill the
space.

**The deeper problem is ambiguity.** An absent path currently means one of two very different
things — *the report does not state this* (a finding) or *the extraction failed to capture it* (a
bug) — and nothing in the output distinguishes them. The canonical model's `extractionReport`
exists to carry that distinction and is not consulted here.

**Fix.** Two parts. Surface `extractionReport` alongside the absent paths so the assessor can tell
a genuine silence from a failed pass. And reconcile the plans' canonical paths against the schema
and against a real extraction — a path no extraction ever populates is either a schema gap or a
plan typo, and both are worth knowing before a run rather than after.

---

## GAP 5 — Category coverage falls short of the check catalogue

**Severity: medium.** Comparing each check's *Primary Document Categories* from the CSV against
what retrieval actually surfaced:

| Check | CSV says primary | Never surfaced |
| --- | --- | --- |
| CHK-001 | A, B, C, E, I | **C** |
| CHK-002 | B, C, F, I | **C** |
| CHK-003 | B, D, E, G, I | **B** |
| CHK-004 | B, C, G, H, I | **C** |
| CHK-005 | B, F, I | — |
| CHK-006 | B, C, F, G, I | **F** |
| CHK-007 | B, C, E, F, G, H, I | **B** |
| CHK-008 | B, C, G, H, I | **B, C** |
| CHK-009 | B, E, F, G, H, I | **B** |
| CHK-010 | A, B, C, I | **C** |

Nine of ten checks failed to surface at least one category the catalogue names as primary. C is
missing from five, B from four — the same two single-document categories from GAP 2. This is the
same root cause seen from the catalogue's side rather than the plan's, and the same fix applies.

CHK-008 is the weakest: *Disadvantages, Risks and Customer Understanding* reached only E, F, G, H
and I, missing both B and C — every source of what the client was actually told and understood.

---

## GAP 6 — Two documents were never in the store at all

**Severity: medium.** Of 29 converted documents, 27 were retrieved at least once. Two never were:

- `[E] SW Charges Info 2.md`
- `[E] Zurich Policy Info.md`

My first reading of this was that they had been crowded out by the ten other category E
documents. That was wrong, and the correction matters more than the original point.

**They failed to index.** The load reported:

```
BedrockException: Context Window Error — Malformed input request:
expected maxLength: 50000, actual: 156384
```

The semantic chunker embeds a document's own elements to decide where to cut, so it hands the
endpoint whatever the Markdown reader produced. For `Zurich Policy Info.md` the reader returned
the entire file — 99 headings, 103 table rows — as **one element of 156,384 characters**, and it
went to the embedding endpoint in a single call. The provider refused it, and the whole document
was dropped rather than one passage of it.

Two things follow, and the second is the more important:

**The failure was reported and still invisible where it mattered.** `IndexAsync` catches
per-document failures and returns them, so the load screen said so. But nothing carries that
forward: a check that needed the Zurich policy saw no passage from it and had no way to
distinguish "this evidence is not in the case file" from "this evidence exists and could not be
indexed". Those mean opposite things — one is a finding about the advice, the other is a bug in
the tool — and the assessor was shown neither.

**A larger document is more likely to be lost.** The failure scales with exactly the documents
most worth reading. `Terms and conditions (1).md` at 128,637 characters and
`Peoples Pension Policy Info.md` at 92,043 sat just the right side of whatever the reader chose
to do with them.

**Fixed.** `ReadDocumentAsync` now measures what the reader returned rather than trusting it, and
re-reads a document it could not break up as bounded plain text, splitting on line boundaries
where it can. The bound is `AppSettings.MaxEmbeddingInputCharacters`, defaulting to 20,000 —
clear of Amazon Titan's 50,000-character cap and of OpenAI's 8,192-token one. Losing the heading
structure of one document costs something; losing the document costs more.

`EveryConvertedCaseDocument_FitsTheEmbeddingLimit` walks all 29 documents of this case and fails
if any element exceeds the limit, so this cannot come back quietly.

**Still open:** the second point above. A check should be told which documents failed to index,
so an evidence gap caused by the tool never reads as an evidence gap in the case.

---

## What is working

Worth stating plainly, because the fixes above should not disturb it:

- All 10 plans loaded and all 60 groups executed. No errors, no unparseable findings, no
  retrieval returning nothing.
- One fingerprint across the whole run — the configuration held steady.
- The plan-driven design does what it claims: retrieval was decided in advance, and the
  suitability report was parsed once rather than ten times.
- Real findings were made, including the 10-year versus 2-year investment period contradiction,
  which required combining the canonical model, a category D document and a category C file note.
- The three assertion-only groups (G1.8, G7.6, G8.5) all behaved correctly and all returned
  Potential Concern.

---

## Recommended order

| | Gap | Change | Effort |
| --- | --- | --- | --- |
| 1 | GAP 1a | Verify every citation appears in that group's pack; fail the group when it does not | small |
| 2 | GAP 1b | Compute age, arithmetic and date comparisons in code; report "comparison not possible" when an input is absent | small |
| 3 | ~~GAP 2~~ | ~~Category filter on `CaseDocumentStore.SearchAsync`~~ — done | — |
| 4 | GAP 3 | Minimum score threshold; let a group legitimately return no evidence | small |
| 5 | GAP 4 | Populate `dateOfBirth` and the other 32 paths; surface `extractionReport` | medium |
| 6 | GAP 6 | Tell each check which documents failed to index, so a tool failure never reads as an evidence gap in the case | small |
| 7 | GAP 5 | Re-measure after 3 and 4 — most of this should resolve itself | — |

GAP 1 is first despite GAP 2 being the larger structural defect. An assessor that will rewrite
evidence to fit its conclusion makes better retrieval worse, not better: more evidence simply
means more material available to be reconciled away. The integrity checks have to land before the
retrieval improvements are worth making.

**A note on measuring this.** Both GAP 1 instances are precisely what the
[eval framework](../../eval-strategy/) is built to catch — they are *missed concerns*, the
expensive error, and both would have been invisible to an accuracy figure that averages error
types. This case makes a good first labelled dataset: the expected outcomes are now established
by hand, and `safety.missed_rate` gated at zero would fail this run.
