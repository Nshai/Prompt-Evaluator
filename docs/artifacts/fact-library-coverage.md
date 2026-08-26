# Fact library coverage

Reconciling the published fact library (`docs/Fact Library - 2026-08-25.html`, 44 definitions)
against the canonical model and the ten query plans.

**Before this analysis: 37 of 44 facts were read by a requirement. Seven were not.** Two of those
seven had no field in the canonical model to live in at all. The plans now read 44 of 44, the model
carries all 44, and a build-time test holds it there.

---

## Why an unread fact is worse than an unextracted one

The pipeline extracts a fact, writes it into the canonical model, and prints it in the compliance
report as what the report asserts. If no requirement names its path, nothing ever compares it with
the file. The number then has *the appearance of having been checked and the substance of having
been typed* — which is a worse position than never extracting it, because a reviewer reading the
canonical-facts pane has no way to tell the two apart.

That is the whole basis of the audit below: not "is the fact mentioned somewhere" but "does a
requirement name a canonical path that reaches it".

## Method

The bridge between a library definition and a plan is the canonical model.

1. Parse all 44 definitions out of the library (id, section, description, extraction guidance,
   signal phrases).
2. Resolve the canonical model schema to a full path list — 3,217 paths once `$ref`s are followed.
3. Map each fact onto the model path(s) that carry it, checking each against the schema so the
   mapping is grounded rather than assumed.
4. For each of the 87 query groups, collect every canonical path it names, group-level and
   per-query.
5. A fact is **read** when some group names a path that is the fact's path, an ancestor of it, or
   a descendant of it.

Keyword matching was used only to shortlist. It is unreliable in both directions here, and one case
shows why: `client.will-status` appeared to be covered by fourteen groups, every one of them
matching the modal verb *will* — "who **will** implement it", "what action **will** be taken". The
fact was not covered anywhere.

---

## What was found

### Seven facts no requirement read

| Fact | What it is | Why it mattered |
|---|---|---|
| `retirement.target-income` | The quantified retirement income the client is aiming at | The figure the cashflow model, the withdrawal strategy and the whole retirement recommendation rest on. Neither the report's number nor the file's was ever compared |
| `client.will-status` | Whether a valid will is in place | Not covered at all; the apparent coverage was the modal verb |
| `client.poa-status` | Whether a registered Power of Attorney exists | CHK-010 searched for a POA as *support authority* under the vulnerability overlay, and read `vulnerability/…/thirdPartySupport`. The estate-planning assertion was never read |
| `estate.iht-liability` | The stated current IHT liability | CHK-008 G8.3 discusses IHT consequences and reads `taxTreatment`; the stated liability figure was never read |
| `provider.financial-strength` | Credit rating, AUM, as-at date for a recommended provider | G6.2 searches the phrase "financial strength" in text and justifies the provider choice, but never read the rating the report asserts |
| `fund.past-performance` | Stated performance figures and rates | `/existingArrangements[]/performance` was read by **no** group. G7.5 and G8.2 argue from performance in prose while the figures behind it went unchecked |
| `client.pension-sharing-order` | A share of an ex-spouse's pension awarded on divorce | No home in the model, so nothing to read |

### Two more facts read only in part

- **`arrangement.charges`** — G7.4 compared the *aggregate* (`/costsAndCharges/existing`) and no
  group read `/existingArrangements[]/charges`, the per-arrangement structure. A platform charge
  disclosed against the wrong plan reconciles perfectly in aggregate.
- **`recommendation.withdrawal`** — `/solution/recommendedPlans[]/plannedWithdrawals` was read by
  no group, so the recommended withdrawal method, amount and tax split were unchecked.

### Two facts with nowhere to live in the model

Both were quietly lossy rather than loudly broken, which is why neither had been noticed:

- **`client.pension-sharing-order`** — the nearest field was `taxStatus/protections`, which is
  Lifetime Allowance protection. A pension credit is an asset awarded by a court order; a
  protection is a tax status. Putting one in the other would make the field mean two things.
- **`alternative.annuity-quote`** — `Alternative` recorded that an annuity was considered and
  rejected, and `comparativeCost` held a single money amount. The quote's gross income per annum,
  term basis and guaranteed maturity value had nowhere to go, so a report stating "an annuity would
  pay £4,120 p.a." asserted a checkable number no check could reach.

---

## What changed

### Canonical model (2 additions)

| Addition | Carries |
|---|---|
| `/parties/clients[]/pensionSharingOrders[]` (`PensionSharingOrder`) | Ceding provider, awarded value, whether the value is stated as an estimate, implementation status, order date |
| `/recommendations[]/alternativesConsidered[]/quote` (`AlternativeQuote`) | Quoting provider, term basis, gross annual income, guaranteed maturity value, payment frequency, quote date |

`isValueEstimated` is a field rather than a rounding convention on purpose: a pension credit is
approximate until the receiving scheme confirms it, so the report's figure and the order's figure
differ legitimately, and *an estimate presented as settled* is the thing worth catching.

Everything else the library publishes was already modelled. No other extension was required.

### Query plans (2 new requirements, 14 paths, 3 queries)

Two facts needed a requirement of their own. Bolting the target retirement income onto the
retirement-*date* group would have given one finding two subjects, which is how a real discrepancy
comes to be reported under a requirement nobody reads:

- **CHK-002 G2.8** — *Target retirement income stated in the report is consistent with the evidence
  and with the figures the plan is modelled on.* Guards for the two ways this produces a false
  positive: a target in today's terms against a modelled figure escalated for inflation, and a
  target that includes an existing DB pension against one that excludes it.
- **CHK-006 G6.9** — *Ancillary recommendations — beneficiary nomination, Power of Attorney, will
  review — are stated with an owner and are consistent with what the file records.* Its first guard
  is the modal verb, since that is exactly what made this fact look covered.

The rest were paths an existing requirement should already have been naming:

| Group | Added |
|---|---|
| CHK-001 G1.7 | `pensionSharingOrders[]` + query Q1.7.9 |
| CHK-005 G5.6, CHK-008 G8.3 | `plannedWithdrawals[]` |
| CHK-006 G6.2 | `providerSelection/financialStrengthRating` |
| CHK-006 G6.4, CHK-009 G9.4 | `alternativesConsidered[]/quote` (+ query Q9.4.4) |
| CHK-007 G7.4, CHK-009 G9.2 | `existingArrangements[]/charges` |
| CHK-007 G7.5, CHK-009 G9.1 | `existingArrangements[]/performance[]` (+ query Q7.5.5) |
| CHK-008 G8.3 | `estatePlanning/estimatedIhtLiability` |
| CHK-010 G10.1 | `estatePlanning/isPowerOfAttorneyGranted` |

The plan set moves from 85 groups to 87.

### The guard

`docs/artifacts/fact-library.map.json` maps each published fact to the model paths that carry it,
and is copied to the build output. `FactLibraryCoverageTests` then asserts on every build that:

- every published fact is read by at least one requirement;
- every published fact has a home in the canonical model schema;
- the map covers the whole library (44, no duplicates, none with an empty path list);
- the number of facts resting on a *single* requirement has not grown.

Verified to bite: removing `targetIncome` from G2.8 fails the first test by name.

---

## The other half: what the extractor is told

Plan coverage answers *is this fact checked*. A second pass asked *is this fact extracted well*,
because the extraction prompt is built from a slice of the model schema — which makes a schema
`description` the only per-field channel that reaches the model at all.

**Modality did not exist.** The library validates every fact instance against an eight-value
modality allowlist, and 30 of the 44 facts allow three or more. The model carried only
`provenance.assertionStatus` (Stated/Inferred/Derived/Absent), which answers *how we know it*, not
*what kind of claim it is*. A salary earned today and a retirement income projected thirty years
out were both "Stated" and otherwise identical.

That was worst where it was least visible. `income.source` allows reported · actual · projected ·
assumed · unknown **and is the only consistency-reconciled fact in the library** — the sole member
of the SR-internal repeated-values check. The one fact whose job is catching a report contradicting
itself could not tell a projection from a present figure. `ExpenditureItem` (reported · assumed ·
projected) had no separator either.

Modality is now a field on `Provenance`, beside `assertionStatus` — that object is already the
claim's epistemic metadata rather than its source, and it is 1:1 with a claim, so one field gets
uniform coverage without touching sixty definitions. Its vocabulary is documented in the schema's
own prose, so `CanonicalVocabulary` reads it from there and corrects near-misses like every other
vocabulary, rather than a second copy living in code. The extractor prompt now states plainly that
modality and assertionStatus are different questions, because conflating them is silent: both
fields get filled, both look reasonable, and a projection is compared against a fact find as though
it were a fact.

**28 of 44 facts reached the extractor with no guidance.** The library carries a paragraph of
extraction guidance for every fact; the model saw a field name and a type. What was being dropped
was not padding — *"a statement that children are financially independent is a claim with
`financiallyDependent` false, not an absence"*, *"'every four weeks' is not 'per month'"*, *"never
round a composite risk label to an adjacent one"*, *"do not copy full addresses into the claim
value"*. All 44 now carry guidance on their path, distilled to the rule that changes what gets
written.

This costs prompt length: the twelve section slices grow from 161,097 to 190,967 characters in
total, about **+7,500 input tokens per extraction run**. The document itself dominates each prompt
and is prefix-cached across the twelve passes; this addition is in the part billed at full rate.
That is the trade, made deliberately — a cheaper prompt that mis-types a projection as a current
income costs a false finding on every run that follows.

**Nothing pinned section coverage.** All 21 extractable top-level properties are claimed by exactly
one of the twelve passes (`modelVersion` is stamped in code), which was true and unguarded: adding
a property to the schema and forgetting a pass means it is never populated, every pass still
reports success, and the plans read a path that is permanently absent — indistinguishable from a
report that does not mention the subject.

`ExtractionCoverageTests` now holds all three: every top-level property claimed by exactly one
pass, every published fact carrying a description, and the modality vocabulary present in the
schema, parsed as a vocabulary, and explained in the prompt. Verified to bite — stripping the
`incomes` description, flattening the modality vocabulary and dropping `recommendedPlans` each
fail by name.

## What is still worth watching

**Fifteen facts have exactly one reader.** That is not a defect — most are read by the requirement
that exists for them — but each is a fact whose checking stops entirely if a path is dropped from
one group. The last test pins the count so any growth shows up in a diff.

| Fact | Sole reader |
|---|---|
| `client.identity`, `person.age`, `client.marital-status`, `client.dependents`, `client.health-status` | CHK-001 G1.1 |
| `client.pension-allowance-status` | CHK-001 G1.2 |
| `client.liabilities-present` | CHK-001 G1.5 |
| `client.pension-sharing-order`, `arrangement.contributions`, `arrangement.fund-value` | CHK-001 G1.7 |
| `retirement.target-income` | CHK-002 G2.8 |
| `provider.financial-strength` | CHK-006 G6.2 |
| `client.will-status` | CHK-006 G6.9 |
| `arrangement.product-features` | CHK-008 G8.6 |
| `estate.iht-liability` | CHK-008 G8.3 |

CHK-001 G1.1 alone is the only reader of five facts, which makes it the single most load-bearing
requirement in the set.

**Only one fact is consistency-reconciled.** `income.source` is the sole member of the SR-internal
repeated-values check, read by CHK-001 G1.4 and G1.10. Whether that set should be larger is a
question for the library rather than the plans, but a single member is a thin basis for a mechanism
built to catch a report contradicting itself.

**Coverage here means a requirement reads the fact, not that it reads it well.** This audit proves
no published fact is invisible to the plans. It says nothing about whether the comparison each
group performs is the right one — that is what the run analyses in `docs/test-results/` measure,
against scored benchmark cases.
