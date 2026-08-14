# Run analysis — `latest`, case ABC-99

Scored against [expected-results-benchmark.md](../../expected-results-benchmark.md).
Sources: [`checks_ABC-99_20260814_114031.log`](checks_ABC-99_20260814_114031.log) (33,315 lines,
60 prompt/response pairs) and [`output-latest.txt`](output-latest.txt) (the rendered findings).

---

## 0. Read this first — these logs are a cache replay

**The run stamped `2026-08-14 11:40:31` did not generate anything. It is a byte-identical replay of
run [`160653`](../extraction/result/checks_ABC-99_20260813_160653.log) from the previous day.**

Four independent proofs, any one of which is sufficient:

| Evidence | `160653` (2026-08-13) | `latest` (2026-08-14) |
| --- | --- | --- |
| Config line | `top 16/search, 12/group · plans 10@21fa2f6e50b5 · model 88219124f468` | **identical** |
| First group timestamp | 16:07:04 | 11:40:33 |
| Last group timestamp | 16:09:06 | **11:40:37** |
| Wall clock for 60 groups | 122 s | **4 s** |
| MD5 of the 60 JSON responses, sorted | `8a0494b895c1ad46b61f2e4c01a32dfb` | `8a0494b895c1ad46b61f2e4c01a32dfb` |

660,284 chat tokens in four seconds is 165,000 tok/s. This is precisely the signature
[gap-analysis §1](../../gap-analysis.md#1-what-the-runs-actually-are) documents, and it is now the
**eighth** replay of twelve-plus logs on record. The only thing that differs between the two logs is
the order groups were emitted in; every `reportSays`, `discrepancies`, `analysis`, `citations`,
`severity` and `outcome` field is the same byte for byte.

**The £0.9775 cost line is therefore also a replay artefact**, not spend attributable to new work.

### What this does and does not invalidate

- **Invalid**: any claim that this run tests determinism, a new configuration, or a code change made
  after 2026-08-13 16:06. It tests none of them.
- **Still valid, and the reason the rest of this document exists**: `160653` had no rendered
  `output-latest.txt` until now. This is the first time that run's 60 findings can be read as
  prose, so the per-finding adjudication below is new work on old output — it just belongs to
  `160653`, and should be filed against that run.

> **Item 2 in the gap-analysis to-do list — "one genuine run: fixed extraction at top 8" — remains
> undone.** This is not it.

---

## 1. Check-level outcome

| | Model outcome | After citation verification |
| --- | --- | --- |
| Potential Concern | 41 groups | 42 groups |
| No Issue | 9 groups | **0 groups** |
| Not Applicable / Indeterminate | 7 groups (+3 unparsed) | 18 groups |

At check level: **9 Potential Concern, 1 Indeterminate (CHK-010)** against a benchmark of ten
Potential Concerns — 9/10, the same as `160653` scored before.

Two rendering behaviours are worth naming because they are invisible in the output file:

- **Citation verification silently rewrites the verdict.** G1.5 returns `"outcome":"NoIssue"` in
  the log; the output prints it as **Potential Concern — severity Low**. Every one of the nine
  No Issue groups was converted this way, which is why the summary reads *"0 no issue"*. A reader
  of `output-latest.txt` alone cannot tell an assessed concern from a formatting artefact.
- **`NotApplicable` renders as `Indeterminate`.** G10.2 returns `NotApplicable`; the output shows
  Indeterminate. CHK-010's headline *"The trigger appears absent"* is the same substitution at
  check level.

---

## 2. Recall against the 36 material findings

Hand-adjudicated, one finding at a time, against the rendered output.

| | Count | Share |
| --- | --- | --- |
| **Caught** — substance stated, both sides named | **16** | **44%** |
| Partial — area touched, conflict misidentified or unstated | 12 | 33% |
| **Missed** — no trace in the output | **8** | 22% |

```
CAUGHT   F1.8  F2.1  F2.2  F3.1  F3.3  F4.2  F4.3  F4.4
         F5.3  F6.1  F6.2  F7.3  F7.5  F8.2  F9.1  F9.2

PARTIAL  F1.1  F1.9  F3.4  F3.5  F4.1  F6.3
         F7.1  F7.2  F8.1  F9.3  F9.4  F9.5

MISSED   F1.2  F1.3  F1.6  F5.1  F5.2  F5.4  F10.1  F10.2
```

### Where this sits against the other runs

| Run | Config | Caught | Partial | Missed | Recall |
| --- | --- | --- | --- | --- | --- |
| `095533` | top 8, pre-fix model | 20 | 7 | 9 | 56% |
| `143309` | top 1, fixed model | 8 | 13 | 15 | 22% |
| `160653` | top 16, fixed model | 14 | 12 | 10 | 39% |
| `latest` | **replay of `160653`** | 16 | 12 | 8 | **44%** |

> **The last two rows are the same 60 responses.** The five-point spread between 39% and 44% is
> **adjudication variance between two human scorers on identical bytes**, not run variance. That is
> a useful number in its own right: **±2 findings, ±5 points, is the noise floor of hand-scoring
> this benchmark.** Any future comparison of two configurations that differ by less than that is
> not measuring anything. The three findings I moved up from the earlier scoring are F4.2, F8.2 and
> F9.2, in each case because the substance appears under a *different check* than the benchmark
> files it under — see §4.

---

## 3. Per-finding adjudication

### CHK-001 — 1 caught, 2 partial, 3 missed

| ID | Verdict | Where it landed |
| --- | --- | --- |
| F1.1 £300/wk vs Fact Find £1,200/mo | Partial | The Fact Find figure is retrieved — G6.3 `fileSays` records *"net income £1,200/month basic"* — and the report's £300/week claim is quoted in G1.3 and G1.4. **Neither group ever puts them side by side.** Two halves of the contradiction, in two different checks. |
| F1.2 State Pension £230 **weekly** vs Fact Find **monthly** | **Missed** | G1.4 checks £230/wk against the report's own £11,973 pa and calls it *"consistent"*. It never reaches the Fact Find income table, so the frequency error is invisible. |
| F1.3 Kim's £1,100 and Sea Cadets £20 not in the Fact Find | **Missed** | Same cause. G1.4 compares against the intelliflo cashflow instead, and the Fact Find's £1,430 household total never appears. |
| F1.6 Tenant vs cashflow "assets of your main residence" / RNRB | **Missed** | Residency, rent and the Residence Nil Rate Band appear nowhere in 60 groups. |
| F1.8 £110,000 / £106,000 / actual £116,998 | **Caught** | G1.8, verbatim: *"a variance of between £6,812 and £6,997 or approximately 6.1-6.4%"*. Note it arrives via `extractionReport/internalInconsistencies`, not by computation — see A4 below. |
| F1.9 Zurich, Standard Life, Scottish Widows absent from the Fact Find | Partial | G1.7 raises the right area at High severity but with the wrong frame: it claims *"no evidence whatsoever is provided for Scottish Widows, Aviva, Zurich, Standard Life"*. That is false — provider policy documents for all four are retrieved and quoted in CHK-007 and CHK-009. G1.7 describes its own pack, not the case file, and then returns Indeterminate. |

### CHK-002 — 2 caught

| ID | Verdict | Where it landed |
| --- | --- | --- |
| F2.1 Five retirement horizons | **Caught**, fragmented | G2.2 (66 vs 67), G2.5 (2027 objective vs 10-year horizon to age 75), G2.6 (age 70 vs 66/71), G3.4 (10 years vs 2 years vs Short Term). Every horizon in the benchmark is named somewhere; **no group assembles them**, so the output never says "five". |
| F2.2 Short-term horizon vs risk-5 growth on a 10-year view | **Caught** | G3.4, High: *"Report asserts investment term of 10 years; evidence shows investment period of 2 years… 'Time Horizon: Short Term'"*. The cleanest finding in the run. |

### CHK-003 — 2 caught, 2 partial

| ID | Verdict | Where it landed |
| --- | --- | --- |
| F3.1 File note says risk 6, report says 5 | **Caught** | G3.6 quotes *"You are happy to proceed with a Risk rating of 6"* against the report's 5 and names the internal inconsistency inside the file note as well. **This is the finding A1 was fixed for, and the fix is holding.** |
| F3.3 Investment Period 2 years vs "10-year period" justification | **Caught** | G3.3 and G3.4 both. |
| F3.4 People's Pension retained at risk 9 against ATR 5 | Partial | G3.7 has the raw material — it lists existing ratings *"(9, 4, 2, 7, 4)"* — and then **rules them out**: *"explicitly marked as not comparable to the client scale, so no assessment… can be made"*, returning Indeterminate. The strongest retention finding in the case is discarded by a guard. |
| F3.5 Every ATR answer cautious; upgrade rests on contradicted premises | Partial | G3.3 reaches the £4,000/20% anxiety threshold and the 10-vs-2-year contradiction. But G3.6 then **clears the override**: *"The justification is present and client acknowledgement is documented."* The two halves cancel. |

### CHK-004 — 3 caught, 1 partial

| ID | Verdict | Where it landed |
| --- | --- | --- |
| F4.1 "comfortable… reasonable understanding" vs "No understanding / knowledge" | Partial | Split by extraction. The canonical model records the report's knowledge level as **"Limited"**, so G4.1 and G4.4 test *Limited* against *No understanding* and conclude the evidence is merely *"more severe"* — G4.1 goes further and finds **no contradiction at all**. The report's actual sentence sits in G4.5, which never sees the questionnaire. **The extraction softened the assertion before the assessor saw it.** |
| F4.2 "I have no understanding of investments or how they work" | **Caught** | G4.1 and G4.4 quote the questionnaire responses in full. |
| F4.3 Balanced-investor precondition contradicted | **Caught** | G4.4, precisely: *"the profile record states the client has 'some experience in investments and a degree of understanding of financial matters,' whereas the Risk Profile consistently records 'No understanding / knowledge'"*. |
| F4.4 Complex product to a client with no pension knowledge, comprehension never tested | **Caught** | G4.3 and G4.5 both. G4.5: *"the complete absence of recorded comprehension evidence… represents a material evidence gap"* — though it lands as Indeterminate rather than a concern. |

### CHK-005 — 1 caught, 3 missed. The worst check in the run.

| ID | Verdict | Where it landed |
| --- | --- | --- |
| F5.1 Fact Find disposable income **−£288** vs *"comfortably meets"* | **Missed** | G5.1 concludes the opposite: *"The report's assertions are not contradicted by the evidence."* |
| F5.2 Affordability rests on unrecorded income | **Missed** | Entailed by F1.2/F1.3, both missed. |
| F5.3 £6,000 is the whole emergency fund and is scheduled to be spent | **Caught** | G5.5 and G2.3 both track it to £0 by 2058; G5.3 notes it is *"the second priority for liquidation"*. |
| F5.4 Cashflow "incorporates the assets of your main residence" for a renter | **Missed** | Never surfaced. |

**Cause is retrieval, not reasoning.** CHK-005 reaches category B (the Fact Find) in **1 of 6
groups** — and the one that does is not the affordability group. Income, expenditure and the
−£288 line all live in category B. G5.1 states the problem itself without recognising it:
*"the planning documents do not provide a single monthly expenditure figure to verify this."*
The document that does was never in the pack.

### CHK-006 — 2 caught, 1 partial

| ID | Verdict | Where it landed |
| --- | --- | --- |
| F6.1 Research ranks Existing 2nd and Zurich 1st above the New Solution at 5th | **Caught** | G6.4: *"the report does not explain why retaining the existing solution (which ranked higher) was rejected in favor of the ranked #5 new solution, despite the existing solution's superior projected performance."* |
| F6.2 Same five bullets, near-verbatim, across three switches | **Caught** | G6.1 names all three repeated sentences and applies the boilerplate guard correctly. |
| F6.3 "Lower Costs" leads the Standard Life rationale, which **increases** charges | Partial | Both halves are present and never joined. G7.4 carries the number — *"EA5 (Standard Life)… impact +0.26% (+£268.94)"* — but returns Indeterminate; G9.5 lists it among the savings as *"+0.08% (£268.94 cost)"*. Nothing says the rationale contradicts the table. |

### CHK-007 — 2 caught, 2 partial

| ID | Verdict | Where it landed |
| --- | --- | --- |
| F7.1 Page 5 vs page 11 charge tables (Zurich 0.18/0.93, SL 0.52/0.18) | Partial | Not detected as a report-internal conflict. G7.4 instead compares the report's Zurich 0.93% against the Zurich policy document's *"¾% per year"* — a real and useful discrepancy, but a different one. The p5/p11 conflict is only reachable through `extractionReport`, and E4's repeated-key detection is the half that was never built. |
| F7.2 £186.19 is 0.18% of **Zurich's** £103,439, not Standard Life's £3,002 | Partial | **Extraordinarily close.** G7.4 back-solves the implied fund value for Zurich — *"this would require a fund value of approximately £103,430"* — and separately lists *"Current fund value for… Standard Life to verify the £186.19 figure against the 0.18% stated rate"* as a missing input. It had both numbers and did not divide. Textbook A4. |
| F7.3 "4.24% more expensive" = the saving added, not subtracted | **Caught** | G7.6: *"a plan cannot simultaneously be both more expensive and less expensive than the current plans."* Names the contradiction; does not reach the arithmetic (3% + 1% + 0.24%), and returns Indeterminate. |
| F7.5 Critical yield 4.8% against 2.95% mid growth, justified on past performance | **Caught** | G9.2: *"the recommended plan would need to grow by 4.8% per year… I do believe that this is achievable"* quoted against *"provides no evidence the recommended funds have delivered this level of performance."* |

### CHK-008 — 1 caught, 1 partial

| ID | Verdict | Where it landed |
| --- | --- | --- |
| F8.1 £12,000 (9.4%) reduction appears only in the Appendix — prominence failure | Partial | The **number** is caught, repeatedly and at High severity, but in CHK-009 (G9.5) and CHK-007 (G7.7). The **prominence** point — COBS 4.2.1R, body leads with "Lower Costs" — is never made. G8.5, the prominence group, returns Indeterminate having searched nothing: *"No supporting passages were retrieved or searched for this requirement."* |
| F8.2 Report never discloses that research ranked no action above the recommendation | **Caught** | G6.4 and G9.7, as above. |

### CHK-009 — 2 caught, 4 partial

| ID | Verdict | Where it landed |
| --- | --- | --- |
| F9.1 Switch reduces projected value at every growth rate | **Caught** | G9.5 lists all three scenarios: *"£10,400 (9.6%), £12,000 (9.4%), and £15,000 (10.0%)"* and concludes *"switching costs objectively exceed benefits by the report's own figures."* The strongest single paragraph in the run. |
| F9.2 Adviser's own ranking places the existing arrangement above the recommendation | **Caught** | G9.7 and G6.4. |
| F9.3 Standard Life switch increases charges while justified on lower cost | Partial | As F6.3. |
| F9.4 Retaining Zurich alone projects £128,000 vs £125,000 | Partial | The £128,000 ranking row is never reached. G9.1 instead catches a *different* Zurich-beats-recommendation point — *"Zurich Managed Pn at 44.32% cumulative 5yr… existing Zurich outperforms recommended Aviva by 4.3 percentage points"* — which is correct and material, but not this finding. |
| F9.5 Zurich/SL existing charge figures internally inconsistent, so the cost case is unreliable | Partial | The **conclusion** is reached — G7.4: *"the comparison cannot be reliably performed"*, *"the -0.24% cumulative saving cannot be verified"* — via the policy-document route, not the report-internal one. Right answer, wrong evidence. |

### CHK-010 — 0 of 2. A false negative, not a gap.

| ID | Verdict | Where it landed |
| --- | --- | --- |
| F10.1 No understanding of investments or pensions = FG21/1 capability indicator | **Missed** | |
| F10.2 69-year-old moving all pension provision, no vulnerability screening | **Missed** | |

CHK-010 returned Indeterminate on all four groups on the strength of
`checkTriggers.hasVulnerabilityIndicators = false`, and then argued the point affirmatively.
G10.3: *"The client is described as aged 69, in good health, with good financial resilience and
independent family circumstances — **none of which inherently signal vulnerability requiring
adjustment**… The case does not present clear FG21/1 drivers."*

**The same run holds the contradicting evidence four checks earlier.** CHK-004 quotes *"No
understanding / knowledge"* to pensions, stocks and shares, unit trusts and EIS/VCT — the textbook
FG21/1 low-capability driver. CHK-010's pack reaches categories A, B, F and I only; the risk
profile report is **category D**, and no CHK-010 plan queries it.

This is worse than a miss. A reviewer reading the output is told, in the assessor's own words, that
there are no vulnerability drivers in a file that contains one on every page of the questionnaire.

---

## 4. What the failure pattern actually is

Sorting the 8 missed and 12 partial findings by cause rather than by check:

| Cause | Findings | Gap |
| --- | --- | --- |
| **Category B (Fact Find) not in the pack** | F1.2, F1.3, F1.6, F5.1, F5.2, F5.4 | **R2** |
| **Both halves retrieved, never joined across groups** | F1.1, F6.3, F9.3, F8.1 | new — see below |
| **A guard or trigger discards a live finding** | F3.4, F3.5, F10.1, F10.2 | **A3** |
| **Numbers present, arithmetic not performed** | F7.2, F1.8 (caught only via extraction), F7.3 (partial arithmetic) | **A4** |
| **Report-internal conflict needs repeated-key detection** | F7.1, F9.5 | **E4** |
| **Extraction softened the assertion before assessment** | F4.1 | new |
| Right area, different evidence route | F1.9, F9.4 | R1/R2 |

Six of eight misses are Fact Find facts. **R2 is not one gap among thirteen; it is the single
largest determinant of recall on this case**, and it is invariant across a sixteen-fold range of
search limit.

### 4.1 R2's actual cause: the plans do not query the categories they declare

Parsing `docs/artifacts/check-plan/CHK-*.query-plan.json` — for each group, the categories its
`expectedCategories.evidence` **declares**, against the categories its queries actually
**target**. **17 of 60 groups declare a category no query asks for:**

| Check | Group | Declares | Queries target | Declared, never queried |
| --- | --- | --- | --- | --- |
| CHK-001 | G1.1 | `A B C` | `A B I` | **C** |
| CHK-002 | G2.2 | `B C F` | `B D E F I` | **C** |
| CHK-003 | G3.1 | `B D` | `D G I` | **B** |
| CHK-003 | G3.4 | `B D F` | `B D I` | **F** |
| CHK-003 | G3.7 | `D E G` | `E G H I` | **D** |
| CHK-004 | G4.4 | `C D H` | `C I` | **D H** |
| **CHK-005** | **G5.2** | `B F` | `F I` | **B** |
| CHK-005 | G5.4 | `B F` | `C F H I` | **B** |
| CHK-005 | G5.5 | `B F` | `F I` | **B** |
| CHK-006 | G6.1 | `B C F G` | `C F G I` | **B** |
| CHK-006 | G6.2 | `G H` | `G I` | **H** |
| CHK-007 | G7.1 | `E G H` | `G H I` | **E** |
| CHK-007 | G7.2 | `A H` | `A I` | **H** |
| CHK-007 | G7.6 | `G H` | `I` | **G H** |
| CHK-008 | G8.3 | `G H` | `G I` | **H** |
| CHK-009 | G9.5 | `G H` | `H I` | **G** |
| CHK-009 | G9.6 | `E G` | `G I` | **E** |

**A second and separate defect sits above this one.** CHK-007, CHK-008 and CHK-009 all name
category **B** in `primaryCategories` — and no group in any of them so much as *declares* B in its
`expectedCategories.evidence`, let alone queries it. So the mismatch is invisible to a
group-level check; the check's own declared scope and its groups' declared scope disagree.
CHK-010 is a third variant again: category **D** appears in neither its `primaryCategories`
(`A B C I`) nor any group, so nothing in the plan is internally inconsistent — the plan is simply
wrong about where the evidence lives.

Three defects, needing three different guards:

| | Symptom | Caught by |
| --- | --- | --- |
| **L1** | Group declares a category none of its queries targets (17 groups) | group-level lint |
| **L2** | Check's `primaryCategories` includes a category no group declares (B in CHK-007/008/009) | check-level lint |
| **L3** | Plan omits a category the evidence actually lives in (D for CHK-010) | **neither** — needs review against the checks catalogue |

> **A fourth, found while verifying this.** Nine copies of the plans exist in the tree and **they
> disagree**. `docs/artifacts/check-plan/CHK-001` has **9** query groups (G1.1–G1.9, matching what
> this run assessed); `src/AiPromptEvaluator/bin/Debug/.../check-plan/CHK-001` has **8** — G1.9 is
> missing. The Debug build output, the installer's Debug output and the test project's Debug output
> are all stale; the Release copies are current. **A run launched from the Debug build silently
> assesses eight of nine requirements and reports success.** My first pass at this section parsed
> the stale copy and understated the defect; the table above is from `docs/artifacts`. The plans
> need one authoritative source and a build step that copies rather than a checked-in duplicate.

**G5.2 is the finding-level proof.** Its requirement is *"Financial situation plus cashflow
supports the recommendation"* — the group that has to find the Fact Find's **−£288 disposable
income**. It declares category B as its evidence side and then queries only F and I. The document
holding the answer was never a candidate. F5.1, F5.2 and F5.4 are all lost here, and G5.1's own
output states the consequence without recognising it: *"the planning documents do not provide a
single monthly expenditure figure to verify this."*

Same story for CHK-010: F10.1 needs the Investor Experience questionnaire, which is category **D**,
and no CHK-010 query targets D. CHK-004 reaches it and quotes it four times.

**This is not tunable.** `Rank()` re-orders candidates; it cannot promote a passage that
`CaseDocumentSearchTool` was never asked for. Raising the search limit 8 → 16 bought two passages
for exactly this reason. **Six of the eight missed findings sit behind a category no query asks
for**, and the fix is a plan edit plus a lint rule, not a retrieval parameter.

### 4.2 A3 — the pipeline overrides its own assessor, and throws away the reasoning

The gap register describes A3 as *"Indeterminate fires when one input is absent"*. Measured, it is
sharper and worse than that. [`GroupFinding.ParsedOutcome`](../../../../src/AiPromptEvaluator/CheckFinding.cs#L100-L103)
reads:

```csharp
public CheckOutcome ParsedOutcome =>
    !ComparisonPerformed ? CheckOutcome.Indeterminate
    : UnverifiedQuotes.Count > 0 ? CheckOutcome.PotentialConcern
    : CheckFinding.ParseOutcome(Outcome);
```

`comparisonPerformed` is a field the **model** fills in, and the first line lets it veto the model's
own verdict. Across the 60 groups:

| | |
| --- | --- |
| Groups returning `comparisonPerformed: false` | **20** |
| …of which the model also wrote `outcome: "PotentialConcern"` | **13** |
| …of which the model also wrote `severity: "High"` | **11** |
| Discrepancies written by those 20 groups, then discarded | **71** |

**Eleven groups reached "Potential Concern, High" and were demoted to Indeterminate by a flag the
same model set.** G3.7 is F3.4's loss in one line: it lists the existing fund ratings
`(9, 4, 2, 7, 4)` in its discrepancies, writes `PotentialConcern` / `High`, sets
`comparisonPerformed: false` because it could not establish the scale mapping — and the pipeline
files the whole thing as unassessable. G7.7 does the same while its discrepancies contain
*"9.4% to 10.0% decrease in maturity value"*.

Then `Summarise` compounds it: the rendered summary prints discrepancies **only from groups whose
`ParsedOutcome` is PotentialConcern** ([CheckFinding.cs:215](../../../../src/AiPromptEvaluator/CheckFinding.cs#L215)),
so all 71 survive only in the detail body, and the Indeterminate groups contribute nothing but a
list of group ids to the check summary a reviewer actually reads.

**The distinction that is missing is between "I could not compare" and "I compared, and separately
could not close one input."** A group that names a discrepancy has, by definition, compared
something. Suggested rule: `!ComparisonPerformed && Discrepancies.Count == 0` → Indeterminate;
`!ComparisonPerformed && Discrepancies.Count > 0` → keep the model's outcome and carry the missing
inputs as a caveat.

At check level this is mostly masked, because `FromGroups` ranks PotentialConcern above
Indeterminate — **except where every group is affected, which is exactly CHK-010.** All four of its
groups set `comparisonPerformed: false`, so the check reports Indeterminate and the two benchmark
findings vanish.

### 4.3 A4 — arithmetic is asked of the model, and it is asked in the worst places

Three findings turn on a division the pipeline never performs.

**F7.2 — the near miss.** G7.4 back-solves one implied fund value correctly:
*"At 0.93% of an implied value, this would require a fund value of approximately £103,430."* It
then lists, as a **missing input**, *"Current fund value for Scottish Widows and Standard Life…
to verify the £186.19 figure against the 0.18% stated rate"* — while £103,430 sits four sentences
above it and £186.19 ÷ 0.0018 = £103,439. The finding is one division away and the group returns
Indeterminate.

**F1.8 — caught, but not by arithmetic.** The £116,997.47 total is correct in the output because
**extraction** did the sum and wrote it into `internalInconsistencies`. G1.8 quotes it. Nothing in
the assessment path adds five fund values. Remove that one extraction string and the finding
disappears.

**G5.2 — where delegated arithmetic actively produces a wrong answer.** Handed the report's own
mis-stated *"you expect your annual expenditure to be approximately £1,700"*, the model reasoned:
*"which if annual would be £141.67/month, not £1,700/month… the £1,700 monthly figure asserted in
the report is incorrect and should be £141.67 monthly."* The benchmark records this sentence as
F1.11 — a **unit typo in the report**. The assessor took it literally, propagated it, and reached a
conclusion no reviewer would accept. Delegated arithmetic does not merely fail to find things; it
manufactures.

Sums, percentages-of, implied-value back-solves and unit normalisation are all decidable in code
against the canonical model, and none of them require retrieval.

### 4.4 E4 — internal-consistency detection is a pass-through, and it is truncated in transit

G1.8 is the only group assessing the report against itself, and its own `fileSays` states the
architecture: *"There are no passages to quote, as this requirement assesses the report's internal
consistency against itself. The canonical model identifies internal inconsistencies reported by the
extraction process itself."*

So CHK-001's internal-consistency requirement returns **exactly what extraction found, and nothing
else**. Extraction found four. The benchmark counts at least eight visible without leaving the
report. The four that were found map to F1.8 (caught), the Aviva 0.06% direction conflict, the
10-year/2-year horizon conflict, and part of the Standard Life charge conflict. **The page-5 vs
page-11 Zurich table conflict (F7.1) is not among them**, and there is no second mechanism that
could find it — which is why F7.1 and F9.5 are partial.

There is a second, separate defect in the same path. The check-level prompt injects the extraction
report through [`Truncate(extraction.Json, 4000)`](../../../../src/AiPromptEvaluator/CheckPlanRunner.cs#L521).
Measured in this run's prompts, the block ends:

```
"paths": [ "/costsAndCharges/annualRecurringCharges", "/costsAndCharges/compa
... [truncated, 4,986 more characters]
```

**55% of the extraction report never reaches any assessor**, the cut lands mid-key inside the
fourth inconsistency, and everything after it — including `ambiguities` — is invisible. G1.8
survives only because it *also* receives `/extractionReport/internalInconsistencies[]` as a
canonical fragment, which is not truncated. Every other group sees the clipped version.

Repeated-key/different-value detection over the canonical model is the unbuilt half of E4, and it
is the only thing that reaches F7.1. It needs no model call: two paths, same semantic key,
different values, both quoted.

### 4.5 R1 — no floor, so "nothing relevant" is not an expressible outcome

Score distribution across the 684 passages admitted to packs:

| | |
| --- | --- |
| Minimum admitted | **0.503** |
| Median | 0.616 |
| Below 0.65 | 556 (81%) |
| Maximum | 0.765 |
| Occurrences of *"Nothing was retrieved"* | **0** |
| Searches reporting missed expected signals | **1** of 158 |

The band is narrow, so a hard threshold is a blunt instrument — but the operative fact is not the
number, it is that **every search returns its limit and the zero-hit path has never once executed.**

A correction to the gap register while here: gap-analysis states that *"every plan's
`ifEvidenceAbsent` rule is unreachable"*. Parsing the plans, **0 of 60 query groups define an
`ifEvidenceAbsent` rule at all.** The rules are not unreachable; they were never written. The
`onAbsent` field exists on `triggerProbe` only. So there is nothing for a relevance floor to
trigger *into* — the floor and the absent-evidence rules have to land together or the floor just
empties packs.

Two false positives in this run trace directly to the missing floor, both cases of boilerplate
scoring well enough to be read as evidence:

- **G9.3** cites Zurich's *"Both life cover and waiver benefits are no longer available on new
  pension plans"* — a product-range statement — as proof this client is losing life cover. The same
  document says the plan has none. The benchmark records F8.5 as **no issue**.
- **G1.7** concludes *"no evidence whatsoever is provided"* for four arrangements whose provider
  documents this same run retrieves and quotes under CHK-007 and CHK-009. The group is describing
  the limits of its own pack in language that reads as a finding about the case file.

### 4.6 N1 (new) — findings are scoped to a group and never composed

Four findings fail only because the assertion and its contradiction land in different groups,
sometimes in different checks.

F1.1 is the clearest. The Fact Find's **£1,200 net basic monthly income** is retrieved and written
into `fileSays` — in **CHK-006 G6.3**. The report's **£300/week** claim is quoted twice — in
**CHK-001 G1.3 and G1.4**. Both are in this run, in this log, four thousand lines apart, and no
component ever holds both. The same shape produces F6.3/F9.3 (the £268.94 Standard Life increase is
in G7.4; the "Lower Costs" rationale is in G6.1 and G9.2) and F8.1 (the £12,000 figure is in G9.5;
the prominence requirement is G8.5, which searched nothing).

This is structural, not a retrieval quality problem: `CheckPlanRunner` assesses each group against
its own pack and never revisits. **A contradiction split by the check catalogue is unreachable no
matter how good retrieval gets.**

The cheap version costs no retrieval and no new prompts: after all 60 findings land, index them by
the canonical paths and figures they mention, and flag pairs where one group's `reportSays` and
another's `fileSays` carry conflicting values for the same quantity. Everything it needs is already
written to `CheckFinding.Groups`.

### 4.7 N2 (new) — extraction normalisation can neutralise the finding before assessment

The report says the client is *"comfortable with how investments work and [has] a reasonable
understanding of potential risks & rewards"*. The canonical model records
`knowledgeLevel: "Limited"`.

That is a defensible normalisation, and it destroys F4.1. G4.1 and G4.4 dutifully compare *Limited*
against *"No understanding / knowledge"* and find a difference of **degree** — G4.1 concludes
outright *"There is no contradiction between what the report says and what the file evidences"*,
and G4.4 gets only as far as *"the objective evidence is more severe"*. The flat contradiction the
benchmark is testing for — the report claiming understanding the file denies — was normalised out
before any assessor saw it. The report's actual sentence does reach G4.5, which has no questionnaire
in its pack and returns Indeterminate.

The same risk applies wherever a check turns on the report's exact claim rather than its category:
risk labels, horizon labels, capacity-for-loss ratings. **Where a check tests a claim, the claim
must survive extraction verbatim alongside the enum, not instead of it.**

### 4.8 N3 (new) — the model rewrites the requirement, and the pipeline keeps its version

Comparing every response's `groupId` and `requirement` fields against the plan that produced it:

| | Count of 60 |
| --- | --- |
| Echoed a **check** id (`CHK-008`) where a **group** id (`G8.2`) was asked for | **22 (37%)** |
| Returned a `requirement` string differing from the plan's | **25 (42%)** |
| Both | 11 |

**The identifier half is harmless.** [`ParseGroup`](../../../../src/AiPromptEvaluator/CheckPlanRunner.cs#L748-L754)
overwrites it unconditionally — *"The plan is the authority on which requirement this is; the model
only echoes it"* — so nothing is misrouted and the rendered output shows correct group ids
throughout. It is a prompt-adherence signal, not a bug: **over a third of responses lost track of
which requirement they were answering** while still answering it.

**The requirement half is a real defect**, because the same method does *not* apply the same rule:

```csharp
GroupId = group.GroupId,                                  // plan wins, always
Requirement = string.IsNullOrWhiteSpace(finding.Requirement)
    ? group.Requirement
    : finding.Requirement,                                // model wins whenever it answers
```

So in 25 of 60 groups the requirement printed in the output, and stored on the finding, is the
**model's** wording rather than the catalogue's. Most are harmless expansions of a short label —
plan *"Capacity for loss assessment"* → model *"Capacity for loss assessment must be consistent
with evidence provided"*. But the mechanism permits scope change, and at least one group took it:

| | |
| --- | --- |
| Plan, G1.7 | Existing products **(pensions and investments)** are consistent with the evidence |
| Output, G1.7 | Existing products **(pensions, investments and protection plans)** are consistent with the evidence |

The assessor widened its own requirement to cover protection plans, and that widened text is what
a reviewer reads and what the audit trail records. G1.7 is the group that returned Indeterminate on
F1.9. **A QA requirement is the catalogue's to state; a finding that silently redefines what it was
asked is not auditable.**

This is also the source of the doubled headings in the output — `[G3.1] [G3.1] Risk rating and
summary…` — where the model prefixed the identifier into the requirement text and the renderer
added its own.

Fix: make `Requirement` plan-authoritative exactly as `GroupId` already is (delete the conditional),
and count both divergences per run as a prompt-adherence metric. If 37% of responses cannot echo an
identifier back correctly, that is worth knowing before it costs something.

---

## 5. Findings the run raised that the benchmark does not support

The benchmark cannot measure false positives at check level, but group level is informative.
Two clear spurious findings, both traceable to R1 (no relevance floor):

- **G9.3 — lost life cover and waiver benefits.** Repeats the error
  [gap-analysis §4 R1](../../gap-analysis.md#r1--no-relevance-floor-open) already documented:
  *"the Zurich arrangement contained life cover and waiver of premium benefits [P1], which are
  material features being lost."* The cited passage is boilerplate — *"Both life cover and waiver
  benefits are no longer available on new pension plans"* — a statement about the product range,
  not this policy. The same document says *"This plan does not have life cover."* The benchmark
  records F8.5 as **no issue**.
- **G1.7 — "no evidence whatsoever is provided" for four of five arrangements.** False on the run's
  own record; those provider documents are retrieved and quoted elsewhere in the same run. The
  group is describing the limits of its own pack in language that reads as a finding about the
  case file.

One more worth flagging as *unverifiable rather than wrong*: G7.4's *"policy documentation states
¾% (0.75%)… not 0.93%"* compares an annual management charge against a total recurring charge.
It may well be right that the figures do not reconcile, but the two are not like for like.

---

## 6. Citation verification is still the loudest signal in the output

| | |
| --- | --- |
| Citations produced | 274 |
| Quotes not found in the supplied evidence | **100 (36%)** |
| Groups carrying at least one failure | **42 of 60 (70%)** |
| Lowest-scoring passage admitted | 0.503 |
| Occurrences of *"Nothing was retrieved"* | **0** |

Unchanged from `160653`, as it must be. At 70% of groups the flag no longer discriminates, and it
is now actively harmful in the rendering: it silently converted all nine No Issue verdicts to
Potential Concern, which is why the summary line claims *"0 no issue"*.

### 6.1 The failures are not mostly formatting — a correction

gap-analysis classifies these by inspection: *"roughly 40% differ from the evidence only in
punctuation… and a fifth are elisions."* I re-ran the check mechanically instead, re-testing every
citation of checkable length against **its own group's entire prompt** (a superset of the evidence
it was verified against), then progressively relaxing the matcher:

| | Count | Share of failures |
| --- | --- | --- |
| Citations ≥ 24 chars | 265 | |
| **Absent from their own prompt** | **105** | 100% |
| …recovered by decoding JSON `\uXXXX` escapes | 7 | 7% |
| …recovered by folding table `\|` separators | 12 | 11% |
| …explicit ellipsis, model stitching two spans | 7 | 7% |
| …≥60% contiguous run present (elision / near-miss) | 39 | 37% |
| **…no substantial contiguous run** | **40** | **38%** |

**Punctuation accounts for 11%, not 40%.** Folding pipes and decoding escapes together recover 19
of 105. The two large buckets are elisions (39) and, more seriously, **40 quotes with no
substantial contiguous run in the text they cite** — the model reformatting a table into prose and
presenting the result inside quotation marks with a passage id. Samples:

```
[CHK-002/G2.4]  "Retire (Kim) age 66 in 2026, Retire (John) age 71 in 2027"
[CHK-002/G2.3]  "Year 2025 Age 69/65: Liquid Assets £6,000; Year 2057 Age 101/97: Liquid Assets £1,865"
```

Neither sentence exists in any document. Both are accurate *reads* of a timeline table — and both
are fabricated *quotations*. The verifier is right to reject them.

**This reframes the fix.** Two code changes (decode escapes, fold pipes) plus a subsequence rule
for elisions would take the failure rate from 36% to roughly 12% and make the flag discriminating
again. But the residual 38% is a genuine behaviour problem: **the model cannot quote a markdown
table verbatim, because the assertion it needs spans cells.** That wants a prompt or schema change
— cite a passage id plus a cell reference, or require a single contiguous row — not a looser
matcher. Loosening the matcher far enough to admit these would re-open the exact hole
`CitationVerifier` was built to close.

One earlier hypothesis of mine was wrong and is worth recording: I assumed G1.8's four failures
were caused by `EvidenceTextOf` omitting the extraction report. It does not — G1.8 receives
`/extractionReport/internalInconsistencies[]` as a canonical fragment, and
[`EvidenceTextOf`](../../../../src/AiPromptEvaluator/CheckPlanRunner.cs#L674) includes fragments.
The real cause is escaping. The fragment JSON reaches the prompt as:

```
"The table on page 5 sums to £116,997.47 (Peoples £6,601.55 + Scottish Widows …"
```

The model quotes it back with those characters rendered, so `Normalise` compares
`£116,997.47` against `£116,997.47` and finds nothing. Serialising canonical fragments with
`JavaScriptEncoder.UnsafeRelaxedJsonEscaping` fixes it. Same symptom, different one-line fix —
and a reminder that "the evidence does not contain the quote" has more than one cause.

---

## 7. What to do

Nothing below is new analysis of a new run — it is what this output evidences about the standing
priorities. **Sequenced, scoped and given exit criteria in
[remediation-plan.md](../../remediation-plan.md)**; this table is the register it was built from.

| | Action | Gap | Why this output argues for it |
| --- | --- | --- | --- |
| 1 | **Refuse to emit a run whose wall clock implies a replay.** Fail the run, or stamp the output `REPLAY OF <run-id>`. | — | Eight of twelve-plus logs are replays. This one carried a cost line and a fresh date and cost a full analysis pass to detect. A duration check is a few lines. |
| 2 | Add `extractionReport` to `EvidenceTextOf`; fold table punctuation in `Normalise` | A2 | 70% of groups flagged; the flag rewrites verdicts silently, and it flags the run's best finding. |
| 3 | Stop citation verification rewriting `outcome` — surface it as a separate confidence field | A2 | *"0 no issue"* is a rendering artefact, and it is not visible from the output file. |
| 4 | **Fix the query plans — see §4.1. R2 is a plan defect, not a retrieval-tuning one.** | R2 | **Six of eight missed findings are Fact Find facts, and no amount of re-ranking reaches them: no query in CHK-007, CHK-008 or CHK-009 targets category B at all.** |
| 5 | Add category D to CHK-010's queries and treat recorded no-knowledge as an FG21/1 driver | R2 + A3 | CHK-010 states in the output that no drivers exist while CHK-004 quotes four of them. No CHK-010 query targets D. |
| 6 | Let a guard that blocks a comparison still report the underlying observation | A3 | F3.4 and F3.5 were both reached and then discarded by guards. |
| 7 | Compute sums, percentages and implied fund values in code | A4 | G7.4 back-solved £103,430 for Zurich and then declared the Standard Life equivalent a missing input. |
| 8 | Cross-group contradiction pass over the 60 finished findings | **N1 (new)** | Four findings fail only because the two halves are in different groups. Cheap: it runs on output already produced. |
| 9 | Carry the report's verbatim claim alongside the normalised enum for knowledge, risk and horizon fields | **N2 (new)** | F4.1 is neutralised by `knowledgeLevel: "Limited"`. |
| 10 | **Make `Requirement` plan-authoritative in `ParseGroup`, as `GroupId` already is** — delete the `IsNullOrWhiteSpace` conditional; log both divergences as a per-run prompt-adherence count | **N3 (new)** | 25 of 60 findings print and store the **model's** wording of the requirement, not the catalogue's. G1.7 widened its own scope from *"pensions and investments"* to *"pensions, investments and protection plans"*. A requirement the assessor can redefine is not auditable. Trivial fix; also removes the doubled `[G3.1] [G3.1]` headings. |
| 11 | **Then** run item 2 of the gap-analysis list — fixed extraction at top 8, genuinely generated | — | Still the missing measurement. Nothing here supplies it. |

---

## 8. Method and limits

- Recall was adjudicated by reading `output-latest.txt` in full and testing each of the 36 material
  findings against it, without consulting the earlier scoring until afterwards.
- **The 44% figure and gap-analysis's 39% describe the same 60 responses.** Treat ±2 findings as
  the noise floor of hand-adjudication and do not read differences smaller than that as signal.
  A scoring rubric fixed enough for two people to reproduce would be worth more than another run.
- Where a benchmark finding is filed under one check but the run states it under another, I scored
  it caught. The benchmark scores findings, not checks, and its own note that *"some findings are
  entailed by others"* points the same way. A stricter reading that requires the finding to appear
  under its own check would move F4.2, F8.2 and F9.2 to partial and give 13 caught / 15 partial /
  8 missed.
- Category coverage in §4 was measured by parsing passage headers from the log, giving 684 pack
  passages across 60 groups. This reproduces gap-analysis §4's R2 table exactly (CHK-001 5/9,
  CHK-005 1/6, CHK-007/008/009 0), as it must for a replay.
