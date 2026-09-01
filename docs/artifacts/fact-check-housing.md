# Where each fact is checked

Every fact the library publishes, and the requirement that reads it. Generated from the
shipped plans, so it cannot drift from them.

**&dagger;** — no requirement read this fact before the reconciliation (7 facts).  
**&Dagger;** — read only in part; the fact reached a requirement but a material half of it did not (2 facts).

A fact is *read* when a requirement names a canonical path that is the fact's path, an
ancestor of it, or a descendant — the same test that
[FactLibraryCoverageTests](../../tests/AiPromptEvaluator.Tests/FactLibraryCoverageTests.cs)
applies on every build, so this table is a view of a property the suite already holds rather
than a claim about it.

See [fact-library-coverage.md](fact-library-coverage.md) for what was missing and why it
mattered; this document is the resulting map.

## The nine that moved

### Two needed a requirement of their own

Neither could be bolted onto a neighbour. Putting the target retirement income on the
retirement-*date* group would have given one finding two subjects, which is how a real
discrepancy comes to be reported under a requirement nobody reads.

| Fact | New requirement |
|---|---|
| `retirement.target-income` | **CHK-002 G2.8** — Target retirement income stated in the report is consistent with the evidence and with the figures the plan is modelled on |
| `client.poa-status` · `client.will-status` | **CHK-006 G6.9** — Ancillary recommendations — beneficiary nomination, Power of Attorney, will review — are stated with an owner and are consistent with what the file records |

### Five went to requirements that should already have read them

Each of these had a requirement whose subject was exactly the fact — one that argued about
performance without reading a performance figure, or justified a provider without reading its
rating. The path was simply never named.

| Fact | Housed in |
|---|---|
| `estate.iht-liability` | **CHK-008 G8.3** |
| `provider.financial-strength` | **CHK-006 G6.2** |
| `fund.past-performance` | **CHK-007 G7.5** · **CHK-009 G9.1** |
| `client.pension-sharing-order` | **CHK-001 G1.7** |
| `arrangement.charges` **&Dagger;** | **CHK-007 G7.4** · **CHK-009 G9.2** |

`arrangement.charges` was already compared in aggregate through `/costsAndCharges/existing`.
What was missing is the per-arrangement structure, which is where a platform charge disclosed
against the wrong plan shows up — in aggregate it reconciles perfectly.

### Two picked up readers as a side effect

`recommendation.withdrawal` and `alternative.annuity-quote` each gained two deliberate readers.
The others were already reading a parent path and began matching once the child existed, which
is worth knowing before reading the counts below as evidence of deliberate coverage.

## Every fact, by library section

### Client circumstances

| Fact | Canonical path | Read by |
|---|---|---|
| `client.dependents`<br>Dependents and financial dependency | `/parties/dependants` | CHK-001 G1.1 |
| `client.employment`<br>Employment status and occupation | `/parties/clients[]/employments` | CHK-001 G1.3, CHK-002 G2.2 |
| `client.health-status`<br>Stated health status | `/parties/clients[]/health` | CHK-001 G1.1 |
| `client.identity`<br>Client identity | `/parties/clients[]/firstName`<br>`/parties/clients[]/lastName`<br>`/parties/clients[]/dateOfBirth` | CHK-001 G1.1 |
| `client.marital-status`<br>Marital status | `/parties/clients[]/maritalStatus` | CHK-001 G1.1 |
| `client.pension-allowance-status`<br>Pension allowance status | `/parties/clients[]/taxStatus/annualAllowance`<br>`/parties/clients[]/taxStatus/mpaaTriggered` | CHK-001 G1.2 |
| `client.pension-sharing-order` **&dagger;**<br>Pension sharing order award | `/parties/clients[]/pensionSharingOrders` | CHK-001 G1.7 |
| `client.property`<br>Property or significant asset held | `/financialPosition/assets` | CHK-001 G1.5, CHK-001 G1.11 |
| `person.age`<br>Stated age | `/parties/clients[]/age` | CHK-001 G1.1 |

### Objectives

| Fact | Canonical path | Read by |
|---|---|---|
| `client.objective`<br>Stated client objective | `/objectives[]/summary`<br>`/objectives[]/details` | CHK-002 G2.1, CHK-002 G2.7 |
| `retirement.target-age`<br>Intended retirement age | `/objectives[]/targetAge`<br>`/parties/clients[]/employments[]/intendedRetirementAge` | CHK-001 G1.3, CHK-002 G2.2 |
| `retirement.target-income` **&dagger;**<br>Target retirement income | `/objectives[]/targetIncome` | **CHK-002 G2.8** |

### Income & expenditure

| Fact | Canonical path | Read by |
|---|---|---|
| `client.liabilities-present`<br>Outstanding liabilities | `/financialPosition/liabilities` | CHK-001 G1.5 |
| `client.liquid-savings`<br>Liquid savings held | `/financialPosition/assets`<br>`/financialPosition/emergencyFund` | CHK-001 G1.5, CHK-001 G1.9, CHK-001 G1.11, CHK-002 G2.3, CHK-005 G5.3, CHK-005 G5.5 |
| `household.expenditure`<br>Household expenditure | `/financialPosition/expenditures` | CHK-001 G1.4, CHK-001 G1.10 |
| `income.source`<br>Income source and amount | `/financialPosition/incomes` | CHK-001 G1.4, CHK-001 G1.10, CHK-005 G5.7 |

### Risk profile

| Fact | Canonical path | Read by |
|---|---|---|
| `client.investment-experience`<br>Investment knowledge and experience | `/knowledgeAndExperience/perClient[]/productExperience`<br>`/knowledgeAndExperience/perClient[]/statedLevel` | CHK-004 G4.1, CHK-004 G4.2, CHK-004 G4.4, CHK-004 G4.6 |
| `risk.attitude-rating`<br>Attitude to risk rating | `/riskAssessment/perClient[]/attitudeToRisk` | CHK-003 G3.1, CHK-003 G3.7, CHK-003 G3.8, CHK-003 G3.9, CHK-003 G3.10 |
| `risk.capacity-for-loss`<br>Capacity for loss | `/riskAssessment/perClient[]/capacityForLoss` | CHK-003 G3.2, CHK-003 G3.7, CHK-003 G3.9, CHK-003 G3.10 |

### Existing arrangements

| Fact | Canonical path | Read by |
|---|---|---|
| `arrangement.charges` **&Dagger;**<br>Arrangement charges | `/existingArrangements[]/charges`<br>`/costsAndCharges/existing` | CHK-007 G7.4, CHK-009 G9.2 |
| `arrangement.contributions`<br>Contribution status of an arrangement | `/existingArrangements[]/contributions` | CHK-001 G1.7 |
| `arrangement.exit-penalty-present`<br>Exit or transfer penalty present | `/existingArrangements[]/exitPenalties` | CHK-009 G9.6, CHK-009 G9.10 |
| `arrangement.fund-holdings`<br>Current fund holdings of an arrangement | `/existingArrangements[]/holdings` | CHK-001 G1.7, CHK-004 G4.6 |
| `arrangement.fund-value`<br>Arrangement fund/transfer value | `/existingArrangements[]/currentValue`<br>`/existingArrangements[]/transferValue` | CHK-001 G1.7 |
| `arrangement.product-features`<br>Product features of an arrangement | `/existingArrangements[]/otherFeatures`<br>`/existingArrangements[]/retirementOptionsAvailable` | CHK-008 G8.6 |

### Pension benefits

| Fact | Canonical path | Read by |
|---|---|---|
| `pension.safeguarded-benefits-present`<br>Safeguarded benefits present | `/existingArrangements[]/safeguardedBenefits` | CHK-008 G8.6, CHK-009 G9.3, CHK-009 G9.6 |

### Costs & charges

| Fact | Canonical path | Read by |
|---|---|---|
| `adviser.fee`<br>Adviser fee | `/costsAndCharges/adviserFees` | CHK-007 G7.2, CHK-007 G7.9 |
| `arrangement.projected-value`<br>Projected plan value | `/solution/recommendedPlans[]/projections` | CHK-002 G2.6, CHK-003 G3.9, CHK-004 G4.3, CHK-006 G6.2, CHK-007 G7.11, CHK-008 G8.7 |
| `switch.charge-impact`<br>Charge impact of switching | `/replacementAnalysis[]/costsOfSwitching`<br>`/costsAndCharges/comparison` | CHK-007 G7.4, CHK-007 G7.6, CHK-007 G7.11, CHK-008 G8.7, CHK-009 G9.2, CHK-009 G9.5, CHK-009 G9.8 |
| `switch.critical-yield`<br>Critical yield | `/costsAndCharges/reductionInYield` | CHK-007 G7.3, CHK-007 G7.8, CHK-007 G7.11, CHK-009 G9.5 |

### Recommendations

| Fact | Canonical path | Read by |
|---|---|---|
| `alternative.annuity-quote`<br>Annuity quote obtained | `/recommendations[]/alternativesConsidered`<br>`/research/optionsConsidered` | CHK-006 G6.4, CHK-007 G7.7, CHK-009 G9.4, CHK-009 G9.7 |
| `provider.financial-strength` **&dagger;**<br>Provider financial strength | `/solution/providerSelection/financialStrengthRating` | CHK-006 G6.2 |
| `recommendation.action`<br>Recommended action | `/recommendations[]/action`<br>`/existingArrangements[]/adviceAction` | CHK-006 G6.5, CHK-006 G6.6, CHK-006 G6.7, CHK-006 G6.8, **CHK-006 G6.9** |
| `recommendation.alternative-considered`<br>Alternative considered and outcome | `/recommendations[]/alternativesConsidered` | CHK-006 G6.4, CHK-007 G7.7, CHK-009 G9.4 |
| `recommendation.ancillary-action`<br>Ancillary recommendation | `/solution/implementation/nextSteps`<br>`/recommendations[]/action` | CHK-006 G6.6, CHK-006 G6.7, CHK-006 G6.8, **CHK-006 G6.9** |
| `recommendation.fund-allocation`<br>Recommended fund and allocation | `/solution/investmentStrategy/holdings`<br>`/solution/investmentStrategy/assetAllocation` | CHK-003 G3.5, CHK-003 G3.9, CHK-003 G3.10 |
| `recommendation.withdrawal` **&Dagger;**<br>Recommended withdrawal strategy | `/solution/recommendedPlans[]/plannedWithdrawals`<br>`/existingArrangements[]/withdrawals` | CHK-002 G2.6, CHK-004 G4.3, CHK-005 G5.6, CHK-006 G6.2, CHK-008 G8.3 |
| `transfer.consideration`<br>Transfer considered or recommended | `/replacementAnalysis`<br>`/existingArrangements[]/transferValue` | CHK-001 G1.7, CHK-008 G8.6, CHK-009 G9.1, CHK-009 G9.2, CHK-009 G9.3, CHK-009 G9.4, CHK-009 G9.5, CHK-009 G9.7, CHK-009 G9.8, CHK-009 G9.9, CHK-009 G9.10 |

### Cashflow modelling

| Fact | Canonical path | Read by |
|---|---|---|
| `cashflow.assumption`<br>Cashflow modelling assumption | `/needsAnalysis/assumptions` | CHK-001 G1.11, CHK-002 G2.7, **CHK-002 G2.8**, CHK-005 G5.2 |
| `cashflow.projection-outcome`<br>Cashflow projection outcome | `/needsAnalysis/scenarios` | CHK-001 G1.11, CHK-005 G5.2 |

### Performance

| Fact | Canonical path | Read by |
|---|---|---|
| `fund.past-performance` **&dagger;**<br>Stated past performance or rate | `/existingArrangements[]/performance` | CHK-007 G7.5, CHK-009 G9.1 |

### Estate planning

| Fact | Canonical path | Read by |
|---|---|---|
| `client.poa-status` **&dagger;**<br>Power of Attorney in place | `/parties/clients[]/estatePlanning/isPowerOfAttorneyGranted` | **CHK-006 G6.9**, CHK-010 G10.1 |
| `client.will-status` **&dagger;**<br>Valid will in place | `/parties/clients[]/estatePlanning/hasWill`<br>`/parties/clients[]/estatePlanning/isWillUpToDate` | **CHK-006 G6.9** |
| `estate.iht-liability` **&dagger;**<br>Stated IHT liability | `/parties/clients[]/estatePlanning/estimatedIhtLiability` | CHK-008 G8.3 |

## What was added, and where

Every edit the reconciliation made to the shipped plans. Listed rather than counted from the
tables above, because a group can match a fact through a path it already had — reading a
count as evidence of deliberate coverage would credit work nobody did.

| Check | Requirement | Added |
|---|---|---|
| CHK-001 | G1.7 — Existing products (pensions, investments and protection plans) are consistent with the evidence | `/parties/clients[]/pensionSharingOrders[]` + query Q1.7.9 |
| CHK-002 | G2.8 — Target retirement income stated in the report is consistent with the evidence and with the figures the plan is modelled on | **new requirement** — target retirement income, 3 queries |
| CHK-005 | G5.6 — Liquidity mismatch with access needs | `/solution/recommendedPlans[]/plannedWithdrawals[]` |
| CHK-006 | G6.2 — Justification for the particular product, provider, wrapper and investment | `/solution/providerSelection/financialStrengthRating` |
| CHK-006 | G6.4 — Alternatives considered and reasons for rejection | `/recommendations[]/alternativesConsidered[]/quote` |
| CHK-006 | G6.9 — Ancillary recommendations — beneficiary nomination, Power of Attorney, will review — are stated with an owner and are consistent with what the file records | **new requirement** — ancillary recommendations, 3 queries |
| CHK-007 | G7.4 — Cost comparison with alternative options | `/existingArrangements[]/charges` |
| CHK-007 | G7.5 — Benefit versus cost — fair value | `/existingArrangements[]/performance[]` + query Q7.5.5 |
| CHK-008 | G8.3 — Tax consequences of the recommendation | `/parties/clients[]/estatePlanning/estimatedIhtLiability`, `plannedWithdrawals[]` |
| CHK-009 | G9.1 — Comparison of existing versus recommended product and investments | `/existingArrangements[]/performance[]` |
| CHK-009 | G9.2 — Benefits and costs of switching, including where benefits outweigh higher costs | `/existingArrangements[]/charges` |
| CHK-009 | G9.4 — Alternatives to the switch considered | `/recommendations[]/alternativesConsidered[]/quote` + query Q9.4.4 |
| CHK-010 | G10.1 — Details of additional support and needs, including third-party support | `/parties/clients[]/estatePlanning/isPowerOfAttorneyGranted` |

Two new requirements, twelve paths added to existing ones, and three queries. The plan set
moves from 85 groups to 87. CHK-006 absorbed the most: a new requirement for the ancillary
estate-planning recommendations, plus the provider rating its justification requirement had
been arguing without, plus the annuity quote its alternatives requirement could not reach.

Two facts needed the canonical model extended before any plan could read them —
`/parties/clients[]/pensionSharingOrders[]` and `alternativesConsidered[]/quote` did not
exist. See [fact-library-coverage.md](fact-library-coverage.md#what-changed).

## Facts resting on a single requirement

15 of the 44 facts have exactly one reader. That is not a defect — most are
read by the requirement that exists for them — but each is a fact whose checking stops
entirely if one path is dropped from one group, and that is a one-line change nobody would
notice. The count is pinned by a test so any growth shows up in a diff.

| Fact | Sole reader |
|---|---|
| `arrangement.contributions` | CHK-001 G1.7 — Existing products (pensions, investments and protection plans) are consistent with the evidence |
| `arrangement.fund-value` | CHK-001 G1.7 — Existing products (pensions, investments and protection plans) are consistent with the evidence |
| `arrangement.product-features` | CHK-008 G8.6 — Benefits, features or protections the client is giving up |
| `client.dependents` | CHK-001 G1.1 — Essential facts (personal details) stated in the report are consistent with the evidence |
| `client.health-status` | CHK-001 G1.1 — Essential facts (personal details) stated in the report are consistent with the evidence |
| `client.identity` | CHK-001 G1.1 — Essential facts (personal details) stated in the report are consistent with the evidence |
| `client.liabilities-present` | CHK-001 G1.5 — Assets and liabilities are consistent with the evidence |
| `client.marital-status` | CHK-001 G1.1 — Essential facts (personal details) stated in the report are consistent with the evidence |
| `client.pension-allowance-status` | CHK-001 G1.2 — Tax status stated in the report is consistent with the evidence |
| `client.pension-sharing-order` | CHK-001 G1.7 — Existing products (pensions, investments and protection plans) are consistent with the evidence |
| `client.will-status` | CHK-006 G6.9 — Ancillary recommendations — beneficiary nomination, Power of Attorney, will review — are stated with an owner and are consistent with what the file records |
| `estate.iht-liability` | CHK-008 G8.3 — Tax consequences of the recommendation |
| `person.age` | CHK-001 G1.1 — Essential facts (personal details) stated in the report are consistent with the evidence |
| `provider.financial-strength` | CHK-006 G6.2 — Justification for the particular product, provider, wrapper and investment |
| `retirement.target-income` | CHK-002 G2.8 — Target retirement income stated in the report is consistent with the evidence and with the figures the plan is modelled on |

**CHK-001 G1.1 is the sole reader of five**, which makes it the most load-bearing requirement
in the set: identity, age, marital status, dependants and health all stop being checked if it
loses its paths.
