# Gap analysis — run of 2026-08-12, case ABC-99

> **Status, after the verification pass of 2026-08-13.** GAP 1a is confirmed fixed and GAP 1b is
> not. GAP 6's indexing failure is confirmed fixed. GAP 2 is **untested** — the verification run
> predates the fix. GAP 4 is half done: the extraction report is now surfaced, the path
> reconciliation is not. GAP 3 and GAP 5 are unchanged. One new defect was found in the fix
> itself, [GAP 7](#gap-7--citation-verification-flags-formatting-as-fabrication), and it is now
> the loudest thing in the output.
>
> The figures in the body describe the original run and remain the baseline. Each gap carries a
> **Verification** note recording what the second run showed.

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

## The verification run

A second run of the same case, recorded in
[test-results/Runtime-Logs/dataset/ABC-99_20260813_001513.log](test-results/Runtime-Logs/dataset/ABC-99_20260813_001513.log), was measured against
the figures above.

| | Baseline (12 Aug) | Verification (13 Aug) |
| --- | --- | --- |
| Groups executed | 60 | 60 |
| Searches issued | 154 | 154 |
| Passages returned | 1,232 → 649 | 1,232 → 650 |
| Canonical paths absent | 37 refs / 33 paths | 37 refs / 33 paths |
| Documents never retrieved | 2 (both unindexable) | 1 (`[E] SW Further Info 4.md`, 4 KB) |
| Group outcomes | 38 No Issue, 22 Potential Concern | 4 No Issue, 40 Potential Concern, 16 Indeterminate |

**The run predates the GAP 2 fix and cannot test it.** The run began at 00:15:13; the category
filter was committed at 01:33:40. The arithmetic confirms it independently — 154 searches
returned 1,232 hits, which is 154 × 8 with no exceptions, so the second filtered query never
ran. Every retrieval figure below is therefore a re-measurement of the *unfixed* pipeline.

What the outcome column shows is GAP 1's fix working as intended and GAP 7 working as not
intended. Sixteen groups now decline to answer rather than guess, which is the behaviour that
was missing. But of the 44 groups that did answer, 41 were forced to Potential Concern by
citation verification, and most of those were forced on formatting.

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

> **Verified fixed.** In the second run G3.6 returns **Potential Concern** and states the
> contradiction plainly: *"File notes [P6] state client 'happy to proceed with a Risk rating of 6'
> whereas report [P5] … state risk rating 5 as the agreed final rating."* The digit is no longer
> altered, and the category C file note reached the group.

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

> **Not fixed.** In the second run G1.1 again returns **No Issue**, and the tolerance is still
> applied backwards: *"The discrepancy is material but within the stated tolerance of 1 year."*
> The mismatch is at least now *listed* — `discrepancies` opens with *"Report states client age
> 69; Fact Find (P6) states age 70 and date of birth 07/06/1956"* — so the reasoning-before-verdict
> ordering surfaced it and the verdict then reasoned past it. `dateOfBirth` is still absent from
> the model.
>
> The group is ultimately forced to Potential Concern, but by [GAP 7](#gap-7--citation-verification-flags-formatting-as-fabrication)
> and on an unrelated reflowed table cell — the right outcome for the wrong reason, which a
> reviewer has no way to distinguish from the right one. This is the case for computing the
> comparison in code, part 2 of the fix below, which has not been done.

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

**Written, and never yet executed against this case.** `CaseDocumentStore.SearchAsync` takes a
category filter, using the payload index that had existed unused since the collection was first
created.

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

> **Untested.** The verification run began at 00:15:13 and the fix was committed at 01:33:40, so
> it ran against the unfiltered pipeline. Its 154 searches returned 1,232 hits — 154 × 8 with no
> exceptions — which is the signature of the second query never firing.
>
> Category C drifted from 5 groups to 7 of 16 (31% → 44%), but that is run-to-run variation in an
> unchanged pipeline, not the fix. The better evidence the gap is still live is a *new* casualty:
> `[E] SW Further Info 4.md`, 4 KB, was never retrieved once in 154 searches — a small document
> losing the same competition, in the same way, that category C loses it.

**Still to do:** re-run the case on a build including `4699c18` and re-measure. The 31% figure
above is the baseline, and no measurement has yet been taken against the fix.

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

> **Unchanged, and confirmed in every particular.** The verification run again returned exactly 8
> passages for all 154 searches. `"Nothing was retrieved"` appears **zero** times in the log. The
> lowest score in the run is **0.559** and 115 passages scored below 0.60, so the weak tail is
> still being presented as evidence. 47 of the 57 retrieving groups hit the 12-passage cap.
>
> The only groups with an empty pack are G1.8, G7.6 and G8.5 — the three assertion-only groups,
> empty by design. Retrieval has still never reported an absence.
>
> Calibration data is now available: with the floor anywhere in 0.56–0.60 the threshold begins to
> bite, and the distribution is dense enough at the bottom to choose from rather than guess.

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

> **First part done.** All 60 prompts in the verification run carry a *"What the extraction said
> about itself"* section with the `expectedButAbsent`, `ambiguities` and `internalInconsistencies`
> the extraction recorded, framed exactly as this gap asked: *"Use this to tell report silence
> from extraction failure where a canonical path below is absent."*
>
> **Second part untouched.** The same 37 references across 33 distinct paths resolved to nothing,
> `dateOfBirth` among them. No path was newly populated.
>
> One consequence to note against [GAP 7](#gap-7--citation-verification-flags-formatting-as-fabrication):
> the assessor now reads the extraction report and quotes from it, but `EvidenceTextOf` does not
> include it in the haystack, so citing the section it was just told to consult is scored as
> fabrication.

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

> **Unchanged.** Nine of ten checks still fail to surface a category the catalogue names as
> primary: C missing from CHK-001, 002, 004 and 010, B from CHK-003, 007, 008 and 009, F from
> CHK-006. CHK-006 gained C and lost F; CHK-008 remains the weakest at E, F, G, H, I only. As
> expected, since this is GAP 2 seen from the other side and GAP 2 did not run.

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

> **Indexing verified fixed.** Both `[E] SW Charges Info 2.md` and `[E] Zurich Policy Info.md`
> were retrieved in the verification run, having been retrieved zero times in the baseline. 27 of
> the 28 converted documents were reached.
>
> **The reporting half is confirmed still open.** No prompt in the run mentions an indexing
> failure; the phrase does not appear in the log at all. Nothing would have told a check that a
> document it needed was missing for a reason other than the case not containing it.

---

## GAP 7 — Citation verification flags formatting as fabrication

**Severity: high.** Found by the verification run, in the fix for GAP 1a. The mechanism works —
it is what forces G3.6 and G1.1 off No Issue — but its false-positive rate has grown large enough
to drown the signal it exists to carry.

Replaying `CitationVerifier` over all 60 responses, against the same haystack the pipeline builds:

| | |
| --- | --- |
| Citations made | 280 |
| Quotes failing verification | **95 (34%)** |
| Groups forced to Potential Concern | **41 of 60** |
| Groups surviving as No Issue | **4** |

**Most of the 95 are not fabrications.** Classifying them:

- **38 differ only in punctuation.** Strip non-alphanumerics and they match the evidence exactly.
  They are markdown table rows, quoted with the cell separators the assessor saw:
  `Liabilities | Description | Opening Value (£) | Total | 0`. `Normalise` folds case, whitespace,
  smart quotes and dashes — it does not fold table punctuation, and almost every document in this
  case is tabular.
- **19 are elisions or joins** — a quote spanning two rows, or two adjacent figures joined with a
  comma. Substantial contiguous runs of each appear in the evidence.
- **The remainder include citations of the extraction report**, which [GAP 4](#gap-4--extraction-does-not-populate-fields-the-plans-depend-on)
  now instructs the assessor to consult but `EvidenceTextOf` does not include in the haystack.
  Following the instruction is scored as fabrication.

**Why this is worse than it looks.** At 41 of 60 groups flagged, "this group cited something not
in its pack" no longer distinguishes anything. G1.1 is the illustration: it is forced to Potential
Concern, correctly, but on a reflowed date-of-birth table cell — while the actual age defect it
should have been caught for goes unremarked in the same finding. A reviewer cannot tell that
outcome from a sound one, and a check that fires on two thirds of groups trains people to ignore
it. The original defect — a changed digit inside quotation marks — is exactly the kind of thing
that gets lost in a channel this noisy.

**Fix.** Two changes, both small and both to the matcher rather than the prompt:

1. **Fold table punctuation in `Normalise`.** Collapse `|` and runs of separator characters the
   way whitespace is already collapsed. This is the same argument the class already makes for
   folding smart quotes — a model reflowing a quote is not what this is looking for — applied to
   the punctuation this corpus actually contains.
2. **Add the extraction report to `EvidenceTextOf`.** It is part of the pack the group was given,
   so a quote from it is a verifiable claim about a document like any other.

Worth re-measuring after both: the residual rate is the number that says whether fabrication is
still happening, and at present it cannot be read.

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

The verification run adds two more, both new behaviour worth keeping:

- **Sixteen groups declined to answer**, setting `comparisonPerformed` to false and naming what
  was missing, where the baseline had none. This is the single most important change in the run:
  "I could not make this comparison" is the answer neither GAP 1 instance produced, and it is now
  being given.
- **All 60 prompts carry the extraction report**, so report silence and extraction failure are
  distinguishable at the point the judgement is made.

---

## Recommended order

Revised after the verification pass.

| | Gap | Change | Effort |
| --- | --- | --- | --- |
| 1 | GAP 7 | Fold table punctuation in `Normalise`; add the extraction report to `EvidenceTextOf` | small |
| 2 | GAP 1b | Compute age, arithmetic and date comparisons in code; report "comparison not possible" when an input is absent | small |
| 3 | GAP 2 | Re-run on a build including `4699c18` — the fix has never executed | — |
| 4 | GAP 3 | Minimum score threshold; let a group legitimately return no evidence | small |
| 5 | GAP 4 | Populate `dateOfBirth` and the other 32 paths | medium |
| 6 | GAP 6 | Tell each check which documents failed to index, so a tool failure never reads as an evidence gap in the case | small |
| 7 | GAP 5 | Re-measure after 3 and 4 — most of this should resolve itself | — |
| — | ~~GAP 1a~~ | ~~Verify every citation appears in that group's pack~~ — done, verified | — |
| — | ~~GAP 4a~~ | ~~Surface `extractionReport`~~ — done, verified | — |
| — | ~~GAP 6a~~ | ~~Stop losing a document too big to embed~~ — done, verified | — |

GAP 7 is now first, ahead of the defect it was introduced to fix. A verification check that fires
on two thirds of groups is not a weaker version of the check — it is a different one, which
reports nothing about fabrication and costs a reviewer the same attention. Both of its fixes are
smaller than anything else on the list.

GAP 1b follows for the reason GAP 1 led the original list: an assessor that will reason past a
contradiction it has already written down makes better retrieval worse, not better. GAP 2's fix
is written and tested but has never run, so it sits third — it needs a run, not work.

**A note on measuring this.** Both GAP 1 instances are precisely what the
[eval framework](../../eval-strategy/) is built to catch — they are *missed concerns*, the
expensive error, and both would have been invisible to an accuracy figure that averages error
types. This case makes a good first labelled dataset: the expected outcomes are now established
by hand, and `safety.missed_rate` gated at zero would fail this run.
