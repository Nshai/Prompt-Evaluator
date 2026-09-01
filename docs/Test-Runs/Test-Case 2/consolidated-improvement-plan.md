# Consolidated improvement plan — from Runs 3 and 4

Written from [Run 3](Run-3/run-analysis.md) (Sonnet, with the changes) and
[Run 4](Run-4/run-analysis.md) (Haiku, with the changes), read against
[Run 1](Run-1/run-analysis.md), [Run 2](Run-2/run-analysis.md) and the
[benchmark](expected-results-benchmark.md).

It supersedes the "what to do next" sections of both run analyses and updates
[findings-improvement-plan.md](../findings-improvement-plan.md), whose items are now measured
rather than proposed.

---

## What the four runs establish

Runs 3 and 4 share a retrieval extract that is **byte-identical** — 42,390 lines, two differing,
both halves of the header naming the model. That closes a 2×2 nobody had before:

| Caught, of 75 | Before the changes | After the changes | |
| --- | :-: | :-: | :-: |
| **Haiku 4.5** | 27 (Run 1) | **41 (Run 4)** | **+14** |
| **Sonnet 4.6** | 40 (Run 2) | **42 (Run 3)** | +2 |
| | **+13** | **+1** | |

**The changes are worth fourteen findings on a fixed model. Tripling the model spend is worth
one.** On the 47-finding scoring set the two models now score identically — 29 caught, 6 partial,
12 missed each — from the same pack, at £2.97 against £9.58.

Three conclusions follow, and the plan is built on them:

1. **Model capability is not the binding constraint** and has not been since Run 2. Every item
   below is a change to the pipeline, not to what runs inside it.
2. **Fourteen findings have survived two models, two configurations and four runs.** They divide
   into four classes with four different remedies (§2), and only one of those remedies is a prompt.
3. **Two of the changes have never been tested.** The index was not rebuilt for either run, and
   trigger probes were bypassed in both. Tier 0 exists to fix that before anything is measured
   against it.

---

## The constraint, carried forward

**Every change must be a rule about a kind of evidence or a shape of comparison, never about a fact
of this case.** The runs were scored against one client file; the fixes must work on a file nobody
has read yet. The test each item states for itself is the same one:

> Would this change do anything at all on a case it was not written for?

This has already paid. The absence-discipline clause was written from one questionnaire and now
governs every document type in every case — and §3.1 below is the correction to it, written the
same way.

---

## The tiers

Status as implemented on 2026-08-28. The suite goes from 921 to **939 tests**, all passing.

| Tier | # | Item | Kind | Recovers | Status |
| :-: | :-: | --- | --- | --- | --- |
| **0** | 0.1 | Rebuild the index, then re-run | operations | unblocks 1.1 | **needs a run** |
| | 0.2 | Re-run with `IgnoreTriggerProbe` off | config | attribution | **needs a run** |
| | 0.3 | Two runs per configuration before any item is accepted | protocol | all measurement | **needs a run** |
| **1** | 1.1 | Repair the fourteen dead section hints against a rebuilt index | plan data + lint | **Class A — up to 7** | **blocked on 0.1** |
| | 1.2 | Derive the like-for-like comparison in code | code | **Class C — 2 (headline)** | **done** — fires on the stored model |
| | 1.3 | Derive net-of-charges return comparisons | code | **Class D — 2** | **done** |
| | 1.4 | Surface the extraction's own internal contradictions to the assessor | code | **Class B — 3** | **done** |
| **2** | 2.1 | Absence discipline, second half: say what the document present states | prompt | 3 partials → caught | **done** |
| | 2.2 | A claim is not corroborated by the document under audit | prompt | recurring false positive | **done** |
| **3** | 3.1 | `Singular()` mishandles irregular plurals | code | output quality | **done** |
| | 3.2 | One sentence must not be both present and absent | code | false pairs | **done** |
| | 3.3 | Widen — or invert — the document-part vocabulary | code | 3 false pairs in 9 | **done** — inverted |
| **4** | 4.1 | Record the assessor's model in the settings digest note | code | reproducibility | **done** |
| | 4.2 | Promote the CHK-009 charge inconsistency to the benchmark | process | benchmark coverage | **needs verification** |
| | 4.3 | Set the model policy on auditability, not recall | policy | — | recorded above |
| **—** | 5.1 | A test holding every emitted directive to the generic rule | test | prevents regression | **done** — added |

### On the generic constraint

Every directive added here is a rule about a shape of evidence, and
`GenericDirectiveTests.NoDirectiveSentToAModelNamesAnythingFromOneCase` now enforces that
mechanically: it strips comments from every class that composes model input, extracts the string
literals, and fails on any provider, client, fund or file name drawn from the cases on file.

Verified by planting `Aviva` in a directive and confirming the test names the file and the literal.
Comments remain exempt, deliberately — the reason a rule exists is a specific failure in a specific
file, and recording it concretely is what stops the rule being deleted by someone who cannot see
what it was for.

---

## Tier 0 — make the next run interpretable

Nothing in Tier 1 can be measured until these are done. All three are configuration or process, and
together they cost one run.

### 0.1 Rebuild the index, then re-run

**Both Run 3 and Run 4 report embeddings of £0.0074 for 2,959 tokens — query embeddings only.** The
vector store was never rebuilt, so the converter's pseudo-heading promotion has not touched a single
chunk across two runs.

The proof is in the diagnostics, identical in both:

> Section hints matching nothing: 14 — G1.1: Residency Status; G1.11: cashflow modeller; **G3.4:
> Capacity For Loss questionnaire responses**; G3.4: my investment horizon; G3.6: ATR Wording; …

Four of those name the evidence behind four findings that all four runs have missed.

**Re-index, then run `RetrievalDryRun` before spending anything on an assessor.** It executes every
plan's retrieval with no model call, so a hint that still matches nothing is found in seconds
rather than in the findings of a £3–£10 run.

### 0.2 Re-run with `IgnoreTriggerProbe` off

Both runs carry `trigger probes bypassed — every check assessed`. CHK-009 therefore ran because the
probe was skipped, and **three independent routes could each have run it**:

1. the probe was bypassed;
2. the plan now accepts `RetainAndSwitchFunds`;
3. the extraction now emits `FullTransfer`, which the plan accepted all along.

CHK-009 is worth six findings. Neither run can say which change earned them, and the applicability
override and the *CHECKS NOT RUN* section are both untested for the same reason. **One line of
configuration settles all three.**

### 0.3 Two runs per configuration

The measured floor is **3 of 78 outcomes and 12 of 69 severities** between two runs with identical
fingerprints. Several conclusions in the Run 3 and Run 4 analyses sit at or near it — F1.3, F3.5,
F6.4 and F9.5 each moved between two runs sharing a pack.

An item is accepted on two runs each side, reported as a range. Below three findings, one run each
side shows nothing.

---

## Tier 1 — the fourteen that survived everything

Fourteen findings have been missed by both models in every run. They are not one problem.

### Class A — the evidence never reached an assessor (7 findings)

**F3.1, F3.8, F5.3, F5.4, F5.7, F8.2, F10.2.**

The decisive strings — `Strongly disagree`, `6.52`, `EXCLUDED FROM SCENARIO`, `£51,694`, `-11.32`,
`Doesn't trust anyone` — appear in **no run's output at all, on either model**. This is not
reasoning; it is evidence nobody was shown.

#### 1.1 Repair the dead hints against a rebuilt index

Sequence, and the order matters:

1. Re-index (0.1), so the converter's promoted headings exist as headings.
2. Re-run `SectionHintReachabilityTests` — it proves a hint *could* match somewhere in the corpus.
3. Run `RetrievalDryRun` — it proves a query actually *retrieves* the chunk carrying it. The two
   answer different questions and the gap between them has already cost a run.
4. For each hint still dead, decide which of the two failures it is: the wording is not in the
   document (fix the hint) or the wording is there and no query reaches it (fix the query).

**Generic by construction:** every step reads a plan against whatever corpus is loaded. A hint that
survives is one that works on any case whose documents carry that heading.

### Class B — the report contradicts itself (3 findings)

**F3.7** (*"managed to remain within this risk tolerance"*, unevidenced), **F6.8** (the
Recommendation Summary contradicts the recommendation tables), **F7.9** (0.41% stated against 0.42%
in the illustration).

These are report-versus-report, so retrieval cannot help: both halves are in the canonical model.

#### 1.4 Surface the extraction's internal contradictions

The extraction **already finds these**. Run 3's own output refers to them:

> The extraction ambiguity document notes the monthly expenditure discrepancy (£2,493 vs £2,206)
> and the monetary charge inconsistency (£490 implied vs £512.69 …)

`extractionReport` carries an internal-inconsistencies list, and one group reached it and used it
well. **Nothing guarantees the group that needs a given contradiction is the group that receives
it.** Route each recorded internal inconsistency to every group whose canonical paths touch the
values involved, printed the way `DerivedFigures` output is printed — as settled, not as something
to notice.

**Generic by construction:** it routes whatever the extractor recorded, for any case.

### Class C — the comparison nobody performs (2 findings)

**F7.1 / F9.2**, and this is the defining defect of the case: the post-advice-charge comparison
applies a 1% ongoing adviser charge the client does not pay to the *existing* plan, making a worse
outcome look level.

Four runs. Two models. Item 3.3 — *"where a comparison shows no difference, ask what assumption
produced that"* — was printed in that group's own *How to compare* block in both Run 3 and Run 4.

- **Run 3 quoted the decisive sentence and used it backwards**: *"[P6] confirms 'Transfer penalty:
  £0.00' and **'Subject to an existing advice charge: No'** … therefore evidences that no
  guaranteed annuity rates … apply to the ceding plan."*
- **Run 4 got nearest and still did not ask**: *"more than offset by fund charges and the addition
  of a 1% per annum advice fee. The report does not clearly explain why, given this net increase,
  the switch is justified on cost grounds."*

**A prompt clause has now failed four times. This must be derived.**

#### 1.2 Derive the like-for-like comparison

**Everything needed is already in the canonical model.** From Run 3's own extraction:

```json
{ "dimension": "Charges",
  "existingPosition":    "0.32% platform + 0.22% weighted fund = 0.54% total",
  "recommendedPosition": "0.21% platform + 0.41% fund = 0.62% total (like-for-like, excluding adviser fee)",
  "assessment": "Worse" }
```

beside `projectedValue`, `effectOfSwitchingAmount`, `effectOfSwitchingPercentage` and
`criticalYieldPercentage`.

Note what the extraction has already captured: the recommended position states its own basis —
**"excluding adviser fee"**. The contradiction is therefore decidable without retrieval:

> The report's own charge comparison rates the recommended option **Worse** by 0.08%, on a basis it
> states as excluding the adviser fee. The projection comparison shows an effect of switching of
> £0 / 0.0%. **A charge difference and a projection difference that should move together do not.**
> Either the projections are on a different basis from the charges, or an assumption has been
> applied to one side and not the other. The report does not say which.

Add it to `DerivedFigures` — which exists precisely because *"delegating arithmetic to the assessor
fails in both directions"* — as a `Figure` with the working shown.

**Generic by construction:** the shape is *a stated cost difference and a stated outcome difference
that disagree in direction or magnitude*. Every replacement, switch and transfer case in this domain
has both figures, and the check is arithmetic on values the model already holds.

### Class D — one arithmetic step nobody takes (2 findings)

**F6.3 / F9.4**: £20,000 moves out of a 4.5% cash account into a fund the adviser's own
illustration shows returning 3.4% after charges. Every run restates the report's own comparison —
4.72% gross against 4.5% — and none applies the charges.

#### 1.3 Derive net-of-charges comparisons

Wherever the model holds a return figure and a charge figure for the same product, compute the net
and state it beside any comparison the report draws. The rule is one sentence:

> A gross return compared against a net rate is not a comparison. Where both a return and a total
> charge are recorded for a product, state the net figure.

**Generic by construction:** gross-versus-net is the single most common false comparison in the
domain, and it recurs wherever a fund is set against a deposit rate.

---

## Tier 2 — convert the partials

### 2.1 Absence discipline, second half

The clause added for Run 3 stopped the assessor asserting absences it could not support — Run 2's
failure mode — and introduced one of its own. Three findings turn on a supporting document
**actively contradicting** the report, and both models now report them as gaps:

| | Report asserts | Evidence states | Run 3 / Run 4 |
| --- | --- | --- | --- |
| F1.5, F9.6 | *"UFPLS available"* | the provider form: **not available** | *"No provider document confirms UFPLS availability"* |
| F1.6 | *"Over 6,900 funds"* | the same form: **over 3000** | *"no provider document confirms this figure for either side"* |

"Nothing corroborates this" is a documentation gap. "The provider's own form says the opposite" is a
misstatement in a suitability report. **The clause says what not to claim and never says what to do
next.**

Run 4 shows the assessor is one step away — it names and quotes the contradicting document, and
still frames the result as an absence. One sentence, in the same rule:

> Where a document of that kind is in the pack, say what it states about the point. If it states
> the opposite of the report, that is the finding, and it outranks any statement about what is
> missing.

### 2.2 A claim is not corroborated by the document under audit

Run 4's `G1.10`: *"Report [P12] confirms three grandchildren"* — where `[P12]` **is the suitability
report**. Run 1 did the same on the same finding. This is the exact failure the standing prompt's
`[I]` rule was written for, and it recurs on the weaker model.

The rule exists; it is not biting where the finding is a *family detail* rather than a figure.
Strengthen it in the group prompt rather than the standing rules, where the passage ids are: when
every passage supporting a claim is category `[I]`, the evidence side is empty and the claim is
unsupported — whatever the passages say.

---

## Tier 3 — defects in the new code

All three are in `CrossGroupContradictions.Documents`, written for item 4.1 of the previous plan.
The feature earns its place — on Run 4 it caught an apparent fabrication that no single group could
see:

> **capacity for loss questionnaire**
> found: [CHK-003/G3.1] Capacity for loss questionnaire [P16] shows responses **supporting the
> medium-high capacity conclusion**
> missing: [CHK-010/G10.4] Report asserts 'Medium-High' capacity for loss but provides **no
> evidence** …

No document in this case states a capacity-for-loss rating. The pass paired the fabrication against
the truth, one check apart.

**3.1 `Singular()` mishandles irregular plurals.** Prints `fact find (entrie)` — a trailing `s` is
stripped from `entries`. Handle `ies → y` before the bare `s`.

**3.2 One sentence must not appear on both sides.** Run 3 admitted one `G3.2` sentence as *found*
under one key and *missing* under another. A sentence recorded as asserting a document's presence
must not also be admitted as denying that document's parts.

**3.3 The document-part vocabulary is a whitelist where a blacklist would be safer.** Run 4:

> found: [CHK-001/G1.1] Fact Find [P4] records Anthony Barnes with Date of Birth …
> missing: [CHK-001/G1.10] **no corroborating figure found in fact find** or supporting documents

`figure` is not in `DocumentParts`, so the denial of a *figure* was attributed to the *document*.
Three of Run 4's nine pairs are this shape against one of Run 3's nine — **the pass's precision
depends on the assessor's prose**, which is not a property to leave to chance. Treat any intervening
noun as a part, and report nothing when the object cannot be identified. A missed pair is invisible;
a false pair is read and discarded, and teaches the reader to discard the next one.

---

## Tier 4 — policy and housekeeping

### 4.1 The settings digest already separates the runs — say so

Run 3 and Run 4 print different settings digests (`3c40d101cc81`, `84ca882c3ba6`) because the
assessor model is part of the digest. That is correct and it is invisible: two runs differing only
in model show two different digests with nothing saying why. Print the changed field names beside
the digest when it moves.

### 4.2 Promote the CHK-009 charge inconsistency

Run 3's CHK-009 produced a finding the benchmark does not have, with the consequence drawn:

> the switch charges assessment table implies the new plan annual charge is £490 … but the annual
> recurring charges table states **£512.69** — a £22.69 difference … the '£63 increase' figure used
> to characterise the cost of switching is **understated**, weakening the like-for-like comparison.

Run 4 found the neighbouring one — *"At £159,792, a 1% ongoing fee would be £1,598, not £1,558"*
(benchmark F7.7, and the only run of four to state it).

Both go to [candidate-findings.md](candidate-findings.md) and into the benchmark **only after
verification against the source documents**, per the governance rule.

### 4.3 Model policy: choose on auditability

| | Sonnet (Run 3) | Haiku (Run 4) |
| --- | :-: | :-: |
| Caught, of 75 | 42 | 41 |
| Caught, of the 47 | 29 | 29 |
| Cost | £9.58 | **£2.97** |
| Untraceable quotations | **30** | 141 |
| Wrong requirement id | **0 of 88** | 61 of 88 |
| Apparent fabrications observed | none | at least one |

**On recall they are level. On whether a reviewer can follow the working they are not close.** Same
passages, 4.7× the untraceable quotations. For a workflow where every finding is read and must trace
to a document, that is the whole of the difference and it is worth more than £6.61.

Use Sonnet for runs a reviewer will act on. Use Haiku for regression runs where the count is the
measurement and nobody reads the prose — and never for a run whose findings reach a client file.

---

## Sequencing

```
0.1 re-index ──► RetrievalDryRun ──► 1.1 repair dead hints ──┐
0.2 IgnoreTriggerProbe off ──────────────────────────────────┤
                                                             ├──► 2 runs, scored (0.3)
1.2 derive like-for-like  ─┐                                 │
1.3 derive net-of-charges  ├─ code, independent of the index ┘
1.4 route internal contradictions ─┘

2.1, 2.2 prompt ──► after Tier 1, or the clauses are tuned against a pack about to change
3.1–3.3 code ─────► any time; they change no finding, only the report
```

**0.1 and 0.2 first, and together.** They cost one run and without them Tier 1 cannot be attributed:
the index change and the trigger change both alter which findings are reachable, and measuring 1.2
against a moving baseline wastes the measurement.

**Tier 1's three code items are independent of the index** and can be built in parallel with the
re-indexing.

---

## Expected position

On Run 3's base of 42 caught / 12 partial / 21 missed of 75:

| Item | Recovers | Confidence |
| --- | :-: | --- |
| 1.1 dead hints, post-index | 3–4 of Class A's 7 | Medium — the hints are the known blockage; whether the chunks then retrieve is untested |
| 1.2 like-for-like derived | 2 (F7.1, F9.2) | **High** — both inputs are in the model and the basis is stated in it |
| 1.3 net-of-charges derived | 2 (F6.3, F9.4) | Medium-high — the return is in the model; the net charge may need one extraction path |
| 1.4 internal contradictions routed | 1–3 of Class B's 3 | Medium |
| 2.1 absence clause | 3 partials → caught | **High** — Run 4 shows the assessor one step away |

**Roughly 42 → 51–53 caught of 75, and 21 → 12–14 missed, with no model change and no further
speculative spend.** The benchmark asks for the gate to be set on missed-rate, and that is close to
halving it.

---

## What is deliberately not here

**A stronger assessor.** Measured twice. Sonnet buys one finding over Haiku on an identical pack for
3.3× the cost; the changes bought fourteen on a fixed model. Model choice is now an auditability
decision (4.3), not a recall one.

**A wider pack.** Still the wrong response to "the evidence was in the pack and went unread" — it
was at rank 6 of 24 and two models read past it. Items 2.1 of the previous plan narrowed and
re-ordered the pack instead. If width moves it should move *down*, measured under 0.3.

**Case-specific plan data.** A hint naming one client's phrasing moves this case and nothing else.
Item 1.1 repairs hints against the corpus and the index, which is the generic form of the same work.

**Anything scored on a single run.** F1.3, F3.5, F6.4 and F9.5 all moved between two runs sharing a
pack. Until 0.3 is the habit, a one-finding result is a rumour.
