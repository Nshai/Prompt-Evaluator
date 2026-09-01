# Candidate findings — Test Case 2

Observations a run produced that the [benchmark](expected-results-benchmark.md) does not contain,
held here until somebody verifies them **against the source documents**.

## Why they are not in the benchmark

A benchmark scores the pipeline. Folding the pipeline's own output into it makes it no longer
independent of the thing it measures, and the failure is invisible: the benchmark keeps looking
like an answer key while quietly becoming a transcript. Every subsequent run then scores well
against findings it wrote itself, and the one number everybody trusts stops meaning anything.

So the rule is a queue, not a gate:

1. A run surfaces something the benchmark lacks. It goes here, with the group that raised it.
2. Somebody opens the **source document** and checks it — not the run, not the retrieval extract,
   the document. The evidence recorded is the document and page, never the group id.
3. Verified, it moves into the benchmark as an ordinary row. Unverified, it stays here. Disproved,
   it stays here struck through, because a candidate that looked good and was wrong is worth as
   much to the next reader as one that was right.

The cost of the queue is a delay. The cost of skipping it is not knowing, later, which rows of a
benchmark were independent and which were not — and by then no one can tell them apart.

## Status

**None of the below has been verified against source documents.** They are quoted from
[Run 2](Run-2/run-analysis.md) and are candidates only.

| # | Candidate | Raised by | Bears on |
| :-: | --- | --- | --- |
| 1 | Cheaper funds at the same risk level existed in the adviser's own research — 0.10% and 0.29% against the recommended 0.41%, all at risk level 4 — and the report does not say why they were rejected | `G6.4` | Strengthens F6.2 |
| 2 | No recommendation states who will implement it: `responsibleParty` is `NotStated` for all five, and the report never says whether the adviser will arrange the change | `G6.6`, `G6.8` | **Contradicts F6.11**, which records this as *No issue* |
| 3 | A recommended fund's KIID states a five-year minimum holding period against a five-year stated term and a client planning to draw at 60 | `G3.9`, `G8.2`, `G8.8` | New |
| 4 | £60,000 → £57,120 → £52,326 is never reconciled anywhere in the report; the cashflow uses a third figure | nine groups | Benchmark treats the first step as internally consistent |
| 5 | The source of the £2,493 monthly figure is traceable: it is the cashflow's expenditure **including** tax and NI (£29,915 ÷ 12), where regular expenses excluding it are £26,480 ÷ 12 = £2,206.67 | `G5.1`, which misreads it | F1.1's core stands; the benchmark should record where the figure comes from rather than calling it unexplained |

### Note on candidate 2

This is the only one that contradicts the benchmark rather than adding to it, so it is the only
one that changes a score rather than raising the ceiling. It should be settled first, and it is
the easiest to settle: the check's own *What to Look For* asks explicitly whether the adviser will
arrange or implement the change, and either the report says so or it does not.

## Note on the two denominators

~~The benchmark's per-check tables and its scoring block do not count the same set… nothing in the
document says which it is.~~ **Withdrawn — the benchmark says so explicitly**, in the line
introducing the scoring block: *"The **47 material findings** (Highest and High) are the benchmark
for recall."* 20 Highest + 27 High = 47. There is no ambiguity and nothing to fix.

What is true, and worth stating plainly because it is easy to misread:

| Set | Rows | What it is |
| --- | :-: | --- |
| Scoring set | **47** | Highest + High. The benchmark for recall, by the benchmark's own definition |
| All material findings | **75** | The above plus 22 Moderate and 6 Low |
| Examined but passing | 15 | Recorded *No issue* — a run is right to stay silent |
| Withdrawn | 1 | F3.3 |

Both runs were originally scored against the 47. They have since been scored against all 75 as
well; the result is in [Run 2's analysis](Run-2/run-analysis.md#11-the-full-benchmark).

The scoring set is also **not 47 independent trials** — the benchmark says so, naming four
cross-check duplicates by design. A recall percentage on it is a useful comparator between runs
and is not a probability of catching an arbitrary defect.

## Note on severity vocabulary

The benchmarks use **Highest** as a severity; the findings schema has only High, Moderate and Low.
Twenty rows in this case carry it. Nothing breaks — the value is only ever read by people — but a
benchmark severity that no run can emit cannot be compared with a run's, so a scorer can match
these rows by identity and never by severity.
