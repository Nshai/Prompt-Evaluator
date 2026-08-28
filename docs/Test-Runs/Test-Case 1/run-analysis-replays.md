# Run analysis — Runs 10 and 11

Runs of case ABC-99 on 2026-08-26 at 15:16 and 15:39, examined against
[expected-results-benchmark.md](expected-results-benchmark.md),
[scoring-rubric.md](scoring-rubric.md) and the previous analysis of Runs 7–9 in
[run-analysis.md](run-analysis.md).

---

## The finding

**Neither run is a run.** Run 10 is a recording of Run 9. Run 11 is a recording of Run 7. The
pipeline detected both itself and said so at the head of each report:

> `*** REPLAY — NOT A GENERATED RUN. 149,582 output tokens in 4.4s is 33,858 tok/s, which no model`
> `produces. The gateway served a cached response. ***`

The rubric's first procedural step is to establish a run is genuine before scoring anything,
because *"scoring a replay is scoring an older run under a newer name."* That is exactly what these
are, so **no score in this document is a measurement.** The numbers are knowable precisely — the
findings text is identical — but they are inherited from Runs 7 and 9 and carry no new information
about anything.

| | Run 10 | Run 11 |
| --- | --- | --- |
| Model | `eu.amazon.nova-2-lite-v1:0` | `intelliflo-claude-haiku-4-5` |
| **Is a replay of** | **Run 9** | **Run 7** |
| Output tokens | 84,667 — identical to Run 9 | 149,582 — identical to Run 7 |
| Wall clock | 5.3s | 4.4s |
| Implied rate | 15,897 tok/s | 33,858 tok/s |
| Reported cost | £3.8982 — identical to Run 9 | £2.0947 — identical to Run 7 |
| **Inherited score** | 7 / 15 / 14 (19%) | 20 / 10 / 6 (56%) |

---

## 1. How the replays were confirmed

The banner is the pipeline's own claim. It was checked four independent ways, because a
self-reported diagnostic is a hypothesis until something else agrees with it.

**1. The token rate is impossible.** Genuine runs on this case sit at 260–567 tok/s. Run 11
implies 33,858 and Run 10 implies 15,897 — one to two orders of magnitude beyond any model, and
consistent with the 26,000–49,000 tok/s replays already catalogued in
[gap-analysis.md §1](gap-analysis.md#1-what-the-runs-actually-are). The detector's threshold is
2,000 tok/s, chosen to sit in the gap between generating text and replaying it rather than to
model the fastest legitimate run, so both trip it by a wide margin: *"a run that trips this has not
been slow; it has been impossible."*

**2. The findings text is identical, line for line.** Normalising away the banner and the
timestamps and diffing Run 11 against Run 7 leaves **2,199 identical lines and nothing else**. The
same comparison of Run 10 against Run 9 leaves 2,031. Every finding, every discrepancy bullet,
every severity, every quotation is the same text.

The only substantive differences anywhere in either pair are the **per-check elapsed times**:

```
Run 7   (27 search(es), 432 passage(s), 38 model path(s) resolved, 7 absent,  91.3s)
Run 11  (27 search(es), 432 passage(s), 38 model path(s) resolved, 7 absent,   1.7s)

Run 9   (19 search(es), 290 passage(s), 13 model path(s) resolved, 0 absent,  63.9s)
Run 10  (19 search(es), 290 passage(s), 13 model path(s) resolved, 0 absent,   2.7s)
```

Retrieval counts are preserved exactly — searches, passages, paths resolved, paths absent — while
the time to produce them collapses by a factor of forty to fifty. That is a recording being read
back, not work being done.

**3. The checks logs are the same size to the byte.** Run 11's log is 5,797,555 bytes; Run 7's is
5,797,555. Run 10's is 5,567,843; Run 9's is 5,567,843. Their hashes differ only because the logs
embed wall-clock timestamps of equal width. Two independently generated 5.8 MB logs do not agree
to the byte.

**4. Cost agrees to the penny.** £2.0947 for 1,492,326 tokens, twice, five hours apart. Nothing
that involves sampling from a model reproduces a token count exactly.

---

## 2. The scores, and why they are not results

Because the findings text is identical, the rubric's verdicts carry over unchanged from
[run-analysis.md §2](run-analysis.md#2-scores). Stated for completeness only:

| | Caught | Partial | Missed | Recall |
| --- | :-: | :-: | :-: | :-: |
| **Run 11** (= Run 7, Haiku 4.5) | 20 | 10 | 6 | 56% |
| **Run 10** (= Run 9, Nova 2 Lite) | 7 | 15 | 14 | 19% |

Every observation in the previous analysis applies verbatim and none of it is confirmed by being
seen again. In particular:

- Run 11 reproduces Run 7's catch of the research ranking (F6.1, F9.2, F9.4) — the same sentences,
  not a second instance of finding them.
- Run 10 reproduces Run 9's **false negative on F1.2**, word for word: *"£230/week in [P10] which
  annualises to £12,040/year or £1,003.33/month — close but not exact."* A replayed false negative
  is not evidence the model reliably makes that error; it is the same error, served twice.
- The six section hints that matched nothing match nothing again, identically, because the
  retrieval was never re-executed.

**This is emphatically not a determinism test.** [run-analysis.md §7](run-analysis.md#7-what-to-do-next)
called for each model to be run twice with sampling pinned, precisely because every figure there
is one sample from an unmeasured distribution. Runs 10 and 11 do not answer that question — a
cached response is not a second draw. **Determinism remains untested**, as it has been for every
pair of apparently-identical runs in this repository.

---

## 3. What these runs did establish

### The authenticity check works, and this is the first time it has fired

[RunAuthenticity.cs](../../src/AiPromptEvaluator.Core/RunAuthenticity.cs) caught both, banner-ed
both at the head of the report and again beside the wall-clock line, and marked the summary
`SUMMARY: REPLAY (cached)`. Nothing about these two runs required a human to notice the wall clock
and reason backwards.

That is worth stating plainly against what it replaced. Twelve run logs existed before this check;
seven were replays; and **two earlier analyses drew conclusions from replays without knowing it**,
including a truncation diagnosis dated from a run that was itself a recording. The operational rule
written at the time — *"read a run's wall-clock duration before treating it as evidence; two
minutes is a run, two seconds is a recording of one"* — is now enforced by the pipeline rather than
by the reader's discipline. Runs 10 and 11 are the first evidence it works on real output.

### The gateway cache outlives a working day

This is the practically useful discovery, and it explains a long-standing gap.

| Original | Replay | Elapsed |
| --- | --- | --- |
| Run 7, 10:15:52 | Run 11, 15:39:03 | **5h 23m** |
| Run 9, 13:24:02 | Run 10, 15:16:29 | 1h 52m |

**The gateway's cache TTL is at least five and a half hours**, and re-running an unchanged
configuration inside that window returns the recording rather than a new sample. That is why no
pair of runs in this repository has ever produced a second measurement of the same configuration:
the obvious way to get one — run it again — is the one method guaranteed not to.

It also explains the extraction failure diagnosed earlier this month, where a retry re-sent an
identical prompt and received the identical malformed reply in the same second. The same cache,
the same mechanism. That case was fixed by varying the retry prompt with a digest of the reply
that failed; the assessment path has no equivalent, because it has never needed one until someone
wanted a second sample.

**To get a genuine second sample, the request has to differ.** Options, in the order I would try
them:

1. **Pin sampling and vary the seed.** `pinSeed: true` with a different `samplingSeed` changes the
   request, so it changes the cache key — and it is the measurement worth having anyway, since
   determinism cannot be assessed while temperature is at the provider default.
2. **Wait out the TTL.** Now bounded below at 5h23m and unbounded above; a poor basis for a
   testing loop.
3. **Change the run fingerprint deliberately** — a different `maxPassagesPerGroup`, for instance,
   which is the comparison the previous analysis wanted next in any case.

### One rough edge in the replay report

The cost line is printed **above** the banner:

```
Model: intelliflo-claude-haiku-4-5
Total: £2.0947 for 1,492,326 tokens — chat £2.0879 …
==============================================================================
*** REPLAY — NOT A GENERATED RUN. …
```

A reader skimming the head of the file meets a cost figure before being told the run did not
happen, and £2.0947 is Run 7's cost, not this run's. The banner's wording does cover it — *"findings,
timings and cost below are a recording"* — so this is purely an ordering problem, not an omission:
the caveat arrives one line after the number it qualifies. Emitting the banner above the cost line,
or suppressing the figure on a replay, would close it. Minor, and worth doing while the code is
open.

---

## 4. Recommendation

**Discard Runs 10 and 11 as evidence.** They should stay in the repository as the first worked
example of the authenticity check firing, and they should be labelled as replays wherever they are
referenced — but no conclusion, comparison or recall figure should rest on them.

**Nothing in the open work has moved.** The three items from
[run-analysis.md §7](run-analysis.md#7-what-to-do-next) that mattered most are all exactly where
they were:

1. **G3.7's guard still costs F3.4** — the People's Pension risk rating of 9 reaches the assessor
   and is discarded as "not comparable to the client scale" while the report claims that fund
   aligns with the client's risk appetite. One guard sentence, one Highest-severity finding, and
   the cheapest fix available.
2. **Six of ten section hints still fire on nothing**, and the test written to prevent that still
   checks existence rather than retrievability.
3. **`maxPassagesPerGroup: 24` is still unmeasured** — and per §3 above, varying it is now also a
   way to defeat the cache, so the two open questions can be answered by the same run.

The next genuine measurement should be **one model, sampling pinned, at 12 and at 24 passages per
group**. That answers the cap question, produces the first reproducible run in the project's
history, and cannot be served from cache.

---

## 5. Limits of this analysis

- **No adjudication was performed.** Scoring identical text a second time would manufacture
  agreement, and the rubric forbids scoring a replay at all. The figures in §2 are Runs 7 and 9's,
  restated under new names.
- **The cache TTL is bounded below, not measured.** 5h23m is the longest observed gap that still
  returned a recording; the true expiry is unknown and may be much longer.
- **Whether a replay is billed is unknown.** The report states the original's cost because it
  replays the original's usage figures. What the gateway actually charged for a cache read is not
  visible from these artefacts, and the £6.44 of apparent spend across these two runs may be
  entirely notional — or may not.
