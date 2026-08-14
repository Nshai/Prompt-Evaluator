# Scoring rubric — adjudicating a run against the benchmark

How to score a run's output against the 36 material findings in
[expected-results-benchmark.md](expected-results-benchmark.md), precisely enough that two people
working separately reach the same number.

**This exists because the recall figures were not reproducible.** Two adjudications of the *same
60 responses* — byte-identical, the same run replayed — produced **39%** and **44%**. Neither was
careless; the benchmark's own definitions leave enough room for a five-point disagreement. Until
that room is closed, no comparison between two runs is trustworthy at less than ±2 findings, and a
23-point gain and a 3-point gain look the same kind of thing on a chart.

---

## 1. What is being scored

Only the **36 material findings** — the Highest and High severity ones the benchmark lists under
*Scoring set*. Moderate and Low findings are not scored, however satisfying it is to see one
caught.

**Check-level outcomes are not a measure on this case.** Every expected outcome is Potential
Concern, so a pipeline that returned Potential Concern unconditionally scores 10/10. Record it,
never lead with it.

Score against the **rendered output** (`Run-all-Checks-output.txt` or equivalent), not the raw log.
The output is what a reviewer sees; a finding that exists only in a log field nothing prints has
not been delivered.

---

## 2. The three verdicts

### Caught

**Both sides are named, and what conflicts between them is stated.**

All three are required:

1. The **report's claim** appears — the figure, date, label or assertion the report makes.
2. The **file's contradicting fact** appears — the figure, date, label or absence that contradicts
   it.
3. The output **says they conflict**, or states one as a discrepancy against the other. A sentence
   listing both without relating them is not enough.

The wording need not match the benchmark's. The *substance* must.

> **Worked example — F3.1 caught.** The benchmark says: file note reads "Risk rating of **6**",
> report says **5**, three ratings unreconciled. The run wrote: *"P6 file notes state 'You are
> happy to proceed with a Risk rating of 6' while all other evidence confirms agreed rating is
> 5."* Both sides, the conflict named. **Caught.**

### Partial

The run reached the area and did not land the finding. Any of:

- **Both halves present, never joined.** The report's claim in one place, the file's fact in
  another, and nothing relating them — *including when both are in the same group's own fields*.
- **The conflict is misidentified.** The right documents, the wrong contradiction.
- **Stated without the contradiction.** The fact is reported as context, not as a discrepancy.
- **The finding is reached and then withdrawn** — named in the reasoning and excluded by a guard,
  a missing input, or an Indeterminate verdict.

> **Worked example — F1.2 partial.** The benchmark says: report treats the state pension as £230
> **per week**, the Fact Find records £230 **monthly**. The run's G1.1 listed *both* in its own
> `fileSays` — "State Pension £230/month" from the Fact Find and the report's weekly figure — and
> raised no discrepancy between them. Both halves, in one field, unjoined. **Partial, not caught.**

### Missed

No trace. The area is not reached, or is reached and the relevant fact never appears.

An **actively wrong** statement about the area is also Missed, and should be flagged separately as
a false negative — it is worse than silence, and the count should not hide it.

---

## 3. Rules that settle the arguments

These are the specific places the two adjudications diverged. Each is now a rule, and the rule
matters more than which way it went.

**R1 — A finding may be caught under any check.** The benchmark files findings under the check
that ought to raise them; the pipeline sometimes raises them elsewhere. Score the finding, not the
filing. *(This is the largest single source of disagreement: it moved F4.2, F8.2 and F9.2.)*

**R2 — Fragmented is still caught, if one place joins it.** A finding spread over three groups
counts when at least one of them states the conflict. If every group holds one half and none
states the conflict, it is Partial under the first bullet above.

**R3 — Entailed findings are scored independently.** F5.2 depends on F1.2 and F1.3; F9.2 restates
F6.1. Score each on its own text. A run that misses the parent usually misses the child, which is
a fact about the run, not a reason to score them together.

**R4 — Right conclusion by the wrong route is Partial, not Caught.** Where the benchmark names the
evidence, reaching the conclusion from different evidence is Partial. *(F9.5: the benchmark's route
is the report's own two charge tables; a run reaching "the comparison is unreliable" from a
provider document has not found the stated finding.)*

**R5 — Severity and outcome are irrelevant to the verdict.** A finding stated inside an
Indeterminate group with severity null is still Caught if it names both sides and the conflict.
What the pipeline *did* with the finding is measured separately, in the outcome counts.

**R6 — Precision of the figure does not decide it.** "About £117,000" against a benchmark
£116,998.47 is Caught. A materially different figure — £106,000 — is not.

**R7 — Score what is printed.** If the substance is only in a log field the report does not
render, it is Partial at most.

---

## 4. Procedure

1. **Establish the run is genuine** before scoring anything. Check the replay banner and the
   wall-clock line. Scoring a replay is scoring an older run under a newer name.
2. Read the rendered output **in full**, once, before scoring anything.
3. Take the 36 findings **in benchmark order**. For each, search the output for the specific
   figures and phrases the benchmark names, then read the surrounding group in full.
4. Record for each: **verdict, where it landed (check/group), and the quoted sentence** that
   decided it. A verdict without its evidence cannot be reviewed and will not be reproduced.
5. Note every verdict you hesitated over. Those are the rubric's next rules.

---

## 5. Reporting

Always report all three counts and the movement, never recall alone:

```
Caught 24 / Partial 6 / Missed 6      (Run 1: 16 / 12 / 8)
```

**State the noise floor beside the number.** Currently ±2 findings, ±5 points, measured. A
difference smaller than that is not evidence of anything.

**State what else changed.** If the run altered retrieval settings, the extraction, the plans or
the code, say which — a recall delta across four simultaneous changes attributes to none of them.
Where a fix can be tied to a finding by *mechanism* — a category appearing in a pack for the first
time, prompt wording traceable to a commit — say that, and say it is mechanism rather than
measurement.

---

## 6. Known limits

- **One case, one benchmark, one adjudicator's readings.** The Highest and High findings are
  contradictions between documents and should survive review; that is why only they are scored.
- **This dataset cannot measure false positives at check level at all** — every check is expected
  to land on Potential Concern. Spurious findings must be counted separately, by hand, and a clean
  case is needed to measure them properly.
- **The rubric does not make adjudication mechanical.** It closes the disagreements that have
  actually occurred. Expect it to grow the next time two people disagree, and prefer adding a rule
  to arguing the instance.
