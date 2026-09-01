# Control case specification — measuring false positives

**This dataset cannot measure precision at all, and nothing else can either until this exists.**
Every check on ABC-99 is expected to land on Potential Concern, so a pipeline that returned
Potential Concern unconditionally scores 10/10 at check level and every recall figure in this
repository is one-sided evidence. "More findings" and "better" are currently the same number.

That gap is now the largest one in the evidence base. Run 8 catches 28 of 36 material findings and
also writes things that are wrong — its F7.3 bullet confuses the 4.24% cost figure with a critical
yield — and no measurement anywhere counts that.

---

## 1. Build it by correction, not by invention

The obvious approach is to author a fresh, clean case. **Don't.** A second case written from
scratch differs from ABC-99 in its facts, its document set, its vocabulary, its conversion
artefacts and its length, so a difference in output has too many candidate causes to attribute.

Instead, derive **ABC-99-CLEAN** from ABC-99 by correcting the contradictions and changing nothing
else. Same documents, same structure, same conversion pipeline, same categories, same lengths to
within a line. Then:

- every check should return **No Issue**, and
- each **specific** correction is tied to the finding it removes, so a Potential Concern on the
  clean case names its own false positive.

That second property is what makes this worth the effort. A generic clean case tells you *how
many* false positives; a derived one tells you *which*, and a false positive you can name is one
you can fix.

---

## 2. The corrections

One per material finding, applied to the document the benchmark names as the *evidence* side
wherever the report is right, and to the report wherever the file is right. **The direction
matters**: correcting the wrong side changes what the case is about.

| Finding | Correction | Document |
| --- | --- | --- |
| F1.1 | Fact find `Net Basic Monthly Income` → `£1,300.00` | B |
| F1.2 | Fact find State Pension row frequency `Monthly` → `Weekly` | B |
| F1.3 | Add Kim's £1,100/month and the £20/week Sea Cadets income to the fact find's income table; total → £4,486.67 | B |
| F1.6 / F5.4 | `Residency Status: Tenant – private` → `Owner – outright`; remove the £600 rent line | B |
| F1.8 | Report's pension totals on p3 and p4 → `£116,998.47`; keep £110,185 labelled as the amount switched | I |
| F1.9 | Add Zurich, Standard Life and Scottish Widows to the fact find's Existing Pension Provision | B |
| F2.1 | Reconcile every horizon on age 72: report "within three years", risk profile Investment Period `7 years`, switch report to age 72 | I, D, G |
| F2.2 | CFL questionnaire horizon → `More than 10 years`; fact find Time Horizon → `Long Term` | D, B |
| F3.1 | File note `Risk rating of 6` → `Risk rating of 5` | C |
| F3.3 | Risk profile `Investment Period: 2 years` → `10 years` | D |
| F3.4 | Recommend switching the People's Pension, or record its risk-9 holding as a stated exception with a rationale | I |
| F3.5 | ATR answers → a coherent level-5 profile; remove the override, or evidence it in the risk report's discussion notes | D |
| F4.1 / F4.2 | Investor-experience and ATR answers → `Good understanding`; keep the report's "comfortable" claim | D |
| F4.3 | Leave as is once F4.1 is corrected — the Balanced precondition is then satisfied | — |
| F4.4 | Add a category C file note recording the drawdown explanation and the client's response | C |
| F5.1 / F5.2 | Follows from F1.1–F1.3: the fact find's disposable income becomes positive | B |
| F5.3 | Scenario asset tree: `LIQUIDATED 2ND` → retained; or raise the emergency fund above the modelled draw | F |
| F6.1 / F8.2 / F9.2 / F9.4 | Research ranking: New Solution → 1st on maturity value | G |
| F6.2 | Rewrite the three switch rationales with plan-specific reasoning | I |
| F6.3 / F9.3 | Standard Life switch: rationale → the real reason, not "Lower Costs" | I |
| F7.1 | Reconcile the p5 and p11 charge tables — one figure per plan | I |
| F7.2 | Standard Life row: `£186.19` → `£5.40` (0.18% of £3,002) | I |
| F7.3 | `4.24% more expensive` → `3.76% more expensive` | I |
| F7.5 | Critical yield → below the mid growth rate, or add forward-looking support | I, G |
| F8.1 | Move the effect-of-switching figure into the report body | I |
| F9.1 / F9.5 | Follows from F6.1 and F7.1 | — |
| F10.1 / F10.2 | Add a vulnerability assessment recording the drivers considered and the conclusion | I, B |

**Twelve of the twenty-eight rows are one field.** The expensive ones are F2.1 (three documents),
F3.5 and F6.2 (rewriting prose), and F10.1 (a section that does not exist).

---

## 3. Expected results

`docs/test-results/expected-results-control.md`, in the same shape as the benchmark:

- **All ten checks: No Issue.**
- **Zero material findings.** Any Potential Concern is a false positive and is named by the
  correction it survived.
- Gaps that are *not* contradictions stay: the report still gives no priority order for the
  objectives (F2.3), still has no loss scenario for the recommended funds (F8.3). Those legitimately
  produce findings, so the control case is not "everything passes" — it is **"nothing contradicts"**,
  and the expected-results file has to say which residual findings are correct.

That distinction is the one thing to get right. A control case scored as "any finding is a false
positive" would train the pipeline toward silence, which is the failure mode that matters least on
this dataset and most in production.

---

## 4. Scoring

Add to [scoring-rubric.md](scoring-rubric.md):

```
Precision, on the control case:
  False positive  — a Potential Concern naming a contradiction that the corrections removed.
  Correct finding — a Potential Concern naming a gap the expected-results file lists as real.
  Correct silence — No Issue on a group with nothing to report.

Report as:  FP 3 / correct 4 / silent 76      (against 83 group blocks)
```

**Report precision and recall together, always.** A run that improves one at the other's expense
has not improved. The pair is the measurement; either alone is a lever to game.

---

## 5. Effort and sequencing

Roughly a day of careful document editing, and it must be done by someone who will not
accidentally introduce a *new* contradiction — the derived case is worthless if it has defects of
its own. Two controls on that:

1. **Run the benchmark's own findings against it.** Every one of the 36 must be absent by
   construction. That is a re-adjudication of the finding list, not of the pipeline.
2. **Diff every document against its ABC-99 original.** Each hunk must map to a row in §2. An
   unexplained hunk is a new defect.

Do it **after** the pinned-sampling runs in
[optimal-configuration.md §1b](optimal-configuration.md#1b-sampling--pin-it-and-vary-the-seed-to-get-a-second-sample),
because the noise floor applies to precision too and a false-positive count without one is the
same mistake this document exists to correct.

---

## 6. What this does not solve

- **One case, corrected, is still one case.** It measures whether the pipeline manufactures
  findings on a file with none. It does not measure behaviour on a case whose defects are
  different in kind from ABC-99's.
- **The corrections encode the adjudicator's readings.** If a benchmark finding is wrong, its
  correction removes something real, and the control case inherits the error.
- **Conversion artefacts survive.** The Markdown conversions lose table structure in places, and
  the control case will lose it identically — which is the point for comparability and a limit on
  what either case can say about production PDFs.
