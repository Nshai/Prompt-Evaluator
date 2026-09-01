# Run 6 — a replay, and the controlled comparison five runs failed to arrange

Scored against [expected-results-benchmark.md](../../../expected-results-benchmark.md) using
[scoring-rubric.md](../../../scoring-rubric.md), compared against
[the Run 5 analysis](../Run-5/run-analysis.md). The changes under test are Stage 8 of
[remediation-plan.md](../../../remediation-plan.md), commit `fcafaa5`.

> **Six files are called `run-analysis.md`** — one in each run folder.

---

## 0. Which log is the run — and this one needs reading before anything else

**Run 6 is a cache replay. It is not a generated run.** The guard caught it and stamped the report:

> `*** REPLAY — NOT A GENERATED RUN. 86,317 output tokens in 3.5s is 24,844 tok/s, which no model
> produces. The gateway served a cached response. ***`

Eleventh replay on record, third to announce itself. Every headline figure below — cost, tokens,
timings — is a recording of Run 5.

| | Run 5 | Run 6 |
| --- | --- | --- |
| Output tokens | 86,317 | **86,317** |
| Chat tokens / cost | 771,273 / £1.1165 | **771,273 / £1.1165** |
| Wall clock | 153 s (563 tok/s) | **3 s (24,844 tok/s)** |
| Prompts byte-identical to Run 5 | — | **60 of 60** |
| Responses byte-identical to Run 5 | — | **60 of 60** |

### Why this replay is worth analysing anyway

**Because it is the controlled experiment [item 8.5](../../../remediation-plan.md) has been asking
for since Run 2, and it arrived by accident.**

All 60 evidence packs and all 60 model responses are byte-identical to Run 5. The *only* thing
that differs between the two reports is the code that rendered them. Five previous runs each moved
two or three variables at once and no recall delta in the series was attributable by measurement.
This one moves exactly one.

**What it can establish:** the effect of Stage 8 — citation matcher, discarded-finding surfacing,
section-hint reporting — on identical input. All three are deterministic post-processing, so this
is a clean A/B.

**What it cannot establish:** anything about model behaviour, prompt changes, or recall gains that
would require a new generation. No model call was made.

### One thing it does settle about the refactor

The user's framing is that this follows the front-end decoupling. A replay cannot speak to
generation — but **retrieval genuinely re-executed**: embeddings were billed (£0.0050, 2,007
tokens, identical to Run 5) and the search path ran, producing **165 searches, 2,614 passages, 122
canonical paths resolved / 23 absent** and **60 of 60 byte-identical evidence packs**.

That is the strongest evidence yet that the decoupling preserved behaviour, and it is measurement
rather than the circumstantial argument the Run 5 analysis had to make. It covers the retrieval,
ranking, canonical-model and prompt-assembly paths. It does not cover the chat client.

---

## 1. Headline

| | Run 4 | Run 5 | **Run 6** | |
| --- | --- | --- | --- | --- |
| **Benchmark recall (caught)** | 24/36 | 24/36 | **25/36 (69%)** | ▲ |
| Partial | 4 | 10 | **9** | |
| **Missed** | 8 | 2 | **2** | — |
| **Unverified citations** | 38 | 61 | **45** | ▲ |
| Unverified rate | 11% | 18% | **13%** | ▲ |
| Groups carrying a flagged quote | 25 | 31 | **26** | ▲ |
| Findings raised then discarded silently | 13 groups | 9 groups | **0 — now printed** | ▲▲ |
| Section declarations matching nothing | unknown | unknown | **1, named** | ▲▲ |
| Check outcomes matching expected | 10/10 | 10/10 | **10/10** | — |
| Wrong requirement id echoed | 24/60 | 29/60 | **29/60** | — |

**Recall moves for the first time since Run 2, on identical model output.** The gain is not new
reasoning — it is a finding the pipeline had already made and was deleting before it reached the
page.

---

## 2. Recall against the 36 material findings

```
CAUGHT (25)   F1.1  F1.3  F1.8  F2.1  F2.2  F3.1  F3.3  F3.5  F4.1  F4.2
              F4.3  F4.4  F5.1  F5.2  F5.3  F6.1  F6.2  F7.3  F7.5  F8.2
              F9.1  F9.2  F9.5  F10.1 F10.2

PARTIAL (9)   F1.2  F1.9  F3.4  F6.3  F7.1  F7.2  F8.1  F9.3  F9.4

MISSED (2)    F1.6  F5.4
```

**One verdict changed: F3.1, Partial → Caught.** All 24 of Run 5's caught findings were verified
still present by probing the new output for a decisive string from each; 24 of 24 survive.

### F3.1 — the finding the pipeline had already made

Run 5 scored this Partial under [rubric R7](../../../scoring-rubric.md) — score what is printed —
because CHK-003/G3.6 named the contradiction in its `discrepancies` array and then returned
`NoIssue`, and the renderer prints only groups that raised a concern. The sentence existed in the
log and nowhere in the report.

Run 6 prints it:

> **RAISED AND NOT CARRIED** — named by a group that then passed.
> **[G3.6] No Issue**
> - *P11 file notes state 'Risk rating of 6' in the ATR wording section, contradicting all other
>   sources which state the final agreed rating is 5.*

Both sides named, the conflict stated. **Caught.**

> **A rubric question this raises, and I am flagging rather than burying it.** The block is
> explicitly headed "Not findings". R5 says outcome is irrelevant to the verdict and R7 says score
> what is printed — both now point to Caught, and that is how I scored it. But a stricter reader
> could hold that a finding a report disclaims is not delivered, which would keep it Partial and
> leave recall at 24/36. **The rubric does not currently settle what a raised-and-not-carried
> block counts as, and it should.** On the stricter reading Run 6 is 24 / 10 / 2 — unchanged from
> Run 5, and Stage 8's gain is entirely in what a reviewer can see rather than in the score.

---

## 3. What Stage 8 actually did

### 3.1 Citation trust: 61 → 45, and a correction to my own projection

| | Run 5 | Run 6 |
| --- | --- | --- |
| Citations | 343 | 343 |
| **Unverified** | **61 (18%)** | **45 (13%)** |
| — of which quote failures | 34 | 19 |
| — of which table-read failures | 27 | 26 |
| Groups carrying a flag | 31 of 60 | 26 of 60 |

**I projected 37 (11%) and the real figure is 45 (13%). My projection was wrong and the reason
matters.** I measured by passing the whole user prompt as the evidence string and supplying no
passage map. The runner passes only the pack's passages, and checks table reads against *the
specific passage the citation names* rather than against the whole pack — deliberately, because
"these values are in this table" is the claim being made and finding them scattered across three
documents would not support it.

My harness was more generous than the shipped code, so it recovered table reads the runner does
not. **A measurement of a component must use the component's real inputs.** The quote-only figure
I reported (34 → 19) was correct; the rate I extrapolated from it was not.

Corrected in [remediation-plan.md](../../../remediation-plan.md) and
[required-implementation.md](../../../../required-implementation.md).

### 3.2 The two Stage 8 changes cover each other, which was not designed

The better matcher recovered CHK-002/G2.1's unverified quote. That group had stated `NoIssue`, and
the citation downgrade — a pass resting on an untraceable quote becomes a concern — was the only
reason it had been printed as a Potential Concern in Run 5, in full.

**With the quote verified, the downgrade no longer applied, G2.1 returned to No Issue, and its
entire detail block left the report** — 73 lines removed, including three discrepancies.

They did not vanish, because 8.1 caught them:

> **[G2.1] No Issue** — *Evidence in Fact Find (P7) shows current monthly expenditure of £1,718
> against net monthly income of £1,430, indicating a £288 shortfall…*

**Fixing the matcher deleted a group from the report, and the discarded-finding surfacing put its
substance back.** Neither change was designed with the other in mind. It is worth stating plainly
because it generalises: **any improvement to the citation matcher mechanically reduces what the
report prints**, since the downgrade is what promotes those groups. Without 8.1, Stage 8 would
have improved a trust metric while quietly removing content — the exact pattern this project has
now hit four times.

No scored finding was lost. F5.1 is caught in G5.1 and G5.4, both unchanged.

### 3.3 Raised and not carried: 2 groups, 5 differences — not the 9 and 27 I predicted

Both numbers are right, about different things, and the plan should say which.

- **9 groups / 27 differences** counts groups whose *stated* outcome was a pass. That is what I
  measured from Run 5's log.
- **2 groups / 5 differences** counts groups whose *parsed* outcome is a pass — after the citation
  downgrade has already promoted the rest to Potential Concern, where they print in full.

The shipped metric is the second, and it is the right one: a group promoted to Potential Concern
is not discarding anything, because the reader sees all of it. **The discarded set is smaller than
I claimed, and the claim was measuring the wrong stage of the pipeline.**

It is also unstable in a way worth knowing: as the matcher improves, fewer groups are promoted, so
*more* groups fall into raised-and-not-carried. This number will rise as citation trust improves,
and that is correct behaviour rather than a regression.

### 3.4 Section hints: the instrumentation answered the question it was built for

Run 6 reports:

> `Section hints matching nothing: 1 — G1.7: Existing Pension Provision.`

**One of seven declarations matched nothing. The other six matched a candidate passage.** That
distinguishes the two causes the Run 5 analysis could not separate, and it **corrects that
analysis**:

| Declaration | Group | Verdict | Consequence |
| --- | --- | --- | --- |
| `Current Monthly Cash Flow`, `Total Net Monthly Income`, `Total Monthly Disposable Income` | G1.4, G5.1, G5.2, G5.4 | matched, reached the pack | **4 findings caught** |
| `Residency`, `Contact Address` | G1.1, G8.1 | **matched a candidate, did not reach the pack** | F1.6, F5.4 still missed |
| `Existing Pension Provision` | G1.7 | **matched nothing at all** | F1.9 still partial |

**Run 5's analysis said the residency section "never arrived". That was wrong.** It arrived as a
candidate and was evicted before the pack was built. Reading the packs directly confirms it: the
Fact Find sections that reach G1.1 are *Personal Details*, *Contact Details*, *Family And
Dependants* and *ID Verification* — the document has no section headed `Residency` or
`Contact Address`, and the hint matched a row label inside a passage that then lost its slot.

Two different fixes follow, and conflating them would have wasted a run:

1. **G1.7's hint names a heading that does not exist** — a plan defect, fixable by correcting the
   wording to whatever the converted Fact Find actually calls that section.
2. **G1.1 and G8.1 are R3 again, at section granularity.** The passage cap and the per-category
   floor still evict a section a plan explicitly asked for. The floor reserves one slot *per
   category*; it does not reserve a slot per *declared section*.

That second one is the substantive finding of this run and belongs in the next stage.

---

## 4. Exit criteria

| Measure | Target | Run 4 | Run 5 | **Run 6** | |
| --- | --- | --- | --- | --- | --- |
| **Missed** | ≤ 3 | 8 | 2 | **2** | ✅ |
| **Recall (caught)** | ≥ 27 (75%) | 24 | 24 | **25 (69%)** | ❌ |
| Runs mis-reported as generated | 0 | 0 | 0 | **0** | ✅ |
| Extraction sections succeeding | all | 12/12 | 12/12 | *(not re-run)* | — |
| Checks reaching the Fact Find | all | 10/10 | 10/10 | **10/10** | ✅ |
| **Unverified quote rate** | < 12% | 11% | 18% | **13%** | ❌ narrowly |
| Groups carrying a flagged quote | < 25% | 42% | 52% | **43%** | ❌ |
| Findings raised then discarded silently | 0 | 13 | 9 | **0** | ✅ |
| Section declarations matching nothing | 0 | unknown | unknown | **1** | ❌ but named |
| Check outcomes matching expected | 10/10 | 10/10 | 10/10 | **10/10** | ✅ |

**Six met. Two criteria went green that had never been measurable before**, because the quantities
did not exist until Stage 8 created them.

**The unverified rate misses its target by one point.** The remaining 45 are now genuinely
worth reading: 26 are table reads checked against the specific passage cited, which is the strict
behaviour and should not be relaxed.

---

## 5. What this run establishes

1. **Stage 8 works, measured on identical input.** F3.1 recovered, citation failures down a
   quarter, and two previously invisible quantities now printed. This is the first change in the
   project measured without a confound.

2. **A finding the pipeline had already made was worth one recall point.** F3.1 required no new
   retrieval, routing or reasoning — only that the report stop deleting it. The Run 5 analysis
   argued the blind spot mattered more than the single finding; the blind spot is now closed and
   the finding came with it.

3. **The residency diagnosis in the Run 5 analysis was wrong, and the instrumentation caught it
   within one run.** "The section never arrived" became "the section arrived and was evicted",
   which is a different fix. This is the fourth time a stage's premise has been corrected by the
   next run's measurement, and the first time the correction was cheap.

4. **Improving the citation matcher removes content from the report.** The downgrade is what makes
   an unverified group print in full, so every recovered quote demotes a group. 8.1 happened to
   cover it. **This coupling should be made explicit rather than left to luck.**

5. **The decoupling refactor preserved retrieval behaviour, measured.** 60 of 60 evidence packs
   byte-identical, with embeddings billed, so the search path genuinely re-executed.

6. **Recall is 25/36 and the target is 27.** Two findings. F7.1 and F7.2 are the closest, and both
   are blocked on the same unbuilt item — [7.6](../../../remediation-plan.md), the extraction
   storing one reconciled value where the report states two.

---

## 6. What to do next

1. **Reserve a slot per declared *section*, not only per category** *(F1.6, F5.4)*. The floor
   currently guarantees one passage per targeted category; a section a plan explicitly named can
   still be evicted, which §3.4 shows is happening in G1.1 and G8.1. These are the last two
   outright misses.
2. **Fix G1.7's hint** *(F1.9)*. It names `Existing Pension Provision`, which no converted
   document contains. Correct it to the real heading — a one-line plan change, now that the run
   says which one is wrong.
3. **Build 7.6** *(F7.1, F7.2, and with them the 75% target)*. Five of the nine partials sit on
   the extraction reconciling two charge tables into one value. Unchanged in priority and now the
   only thing between 25 and 27.
4. **Settle what a raised-and-not-carried block scores as** (§2). It is worth a recall point and
   the rubric is silent.
5. **Generate a real Run 7.** This one was free and told us a great deal, but nothing here
   exercised the model. The next run should hold plans, settings and canonical model, change only
   the code, and actually generate — which after this run is a two-variable comparison at worst.

---

## 7. Method and limits

- **This is a replay and cannot be read as a new run.** Every model-dependent figure is Run 5's.
  What is measured here is the rendering and post-processing of identical responses.
- Scored from `run-checks-output.txt`. The 24 findings Run 5 caught were verified present by
  probing for a decisive string from each rather than re-adjudicated from scratch, which is sound
  only because the finding text is byte-identical — 1,364 of 1,437 lines are unchanged.
- **One verdict changed and it is the one I would most want a second reader on.** See the rubric
  question in §2; the stricter reading gives 24/36 and I have stated what it would cost.
- **Three corrections to my own earlier claims** are recorded above: the projected citation rate
  (§3.1), the discarded-discrepancy count (§3.3), and the residency diagnosis (§3.4). The first
  two were errors of method — measuring a component with inputs it does not receive, and counting
  the wrong stage of the pipeline.
- One case, one adjudicator, and no false-positive measurement is possible on this dataset.
