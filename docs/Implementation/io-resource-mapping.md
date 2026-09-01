# Canonical Model → Intelliflo Office resource mapping

Source of truth: `consolidated-swagger.json` (`components.schemas`, 1,869 definitions).

The canonical model borrows Office **names and enumerations** so that (a) extracted values can be diffed directly against an Office client record and (b) an accepted recommendation can be pushed into Office as a proposal without a semantic translation layer. Enumerations marked **verbatim** are copied exactly from the swagger and must not be extended without a version bump.

---

## 1. Case and parties

| Canonical path | Office schema | Office fields used | Enum verbatim |
|---|---|---|---|
| `case` | `ServiceCase` | `reference`, `category`, `status`, `adviser`, `client`, `jointClient`, `isJoint`, `objectives`, `plans` | — |
| `case.adviser` | `NamedAdviserRef` / `AdviserRef` | `name` | — |
| `parties.clients[]` | `Client` | `category` → `clientCategory`, `person`, `taxReferenceNumber` | `clientCategory` ✔ |
| `parties.clients[]` (person fields) | `PersonValue` | `title`, `firstName`, `lastName`, `dateOfBirth`, `gender`, `maritalStatus`, `niNumber`, `hasWill`, `isWillUptoDate`, `isPowerOfAttorneyGranted` | `gender` ✔ |
| `…territorialProfile` | `TerritorialProfileValue` | `ukResident`, `ukDomicile`, `countryOfResidence` | — |
| `…health` | `HealthProfileValue` | `inGoodHealth`, `isSmoker`, `healthNotes` | `isSmoker` ✔ |
| `…estatePlanning` | `PersonValue` + `EstatePlanning` | `hasWill`, `isWillUptoDate`, `isPowerOfAttorneyGranted` | — |
| `…employments[]` | `Employment` / `BaseEmployment` | `employmentStatus`, `occupation`, `employer`, `startsOn`, `intendedRetirementAge` | `employmentStatus` ✔ |
| `…statePension` | `StatePension` / `StatePensionEntitlement` | `pensionAge`, `pensionAmount` | — |
| `…taxStatus` | `PensionEntitlement` + `FinanceAndTax` + `PensionAllowance` | `moneyPurchaseAnnualAllowanceTriggeredToDate` → `mpaaTriggered`, `totalLumpSum` → `lumpSumAllowanceUsed` | — |
| `parties.dependants[]` | `Dependant` | `name`, `dateOfBirth`, `relationshipType`, `isFinanciallyDependant`, `financialDependencyEndsOn`, `notes` | `relationshipType` ✔ |
| `clientAuthority` | `ClientAgreement` | `status`, `template`, `completedAt`, `statement` | — |

---

## 2. Financial position

| Canonical path | Office schema | Office fields used | Enum verbatim |
|---|---|---|---|
| `financialPosition.incomes[]` | `BaseIncome` / `Income` | `category`, `description`, `gross`, `net`, `frequency`, `includeInAffordability`, `startsOn`, `endsOn`, `monthlyEquivalent` | `frequency` ✔ |
| `financialPosition.expenditures[]` | `Expenditure` | `category`, `description`, `net` → `amount`, `frequency`, `isEssential`, `isConsolidated`, `monthlyEquivalent` | `frequency` ✔ |
| `financialPosition.assets[]` | `Asset` | `assetType`, `description`, `currentValue`, `valuedOn`, `owners` | `assetType` ✔ (subset — plan-only types omitted) |
| `financialPosition.liabilities[]` | `Liability` | `description`, `amount`, `interestRate`, `repaymentType`, `endedOn` | `repaymentType` ✔ |
| `financialPosition.emergencyFund` | `EmergencyFund` | `committedAmount` → `heldAmount`, `requiredAmount`, `shortfall` | — |
| `Money` | `CurrencyValue` | `currency`, `amount` | — |
| `Frequency` | `BaseIncome.frequency` | — | ✔ plus `OneOff`, `Unspecified` |

---

## 3. Objectives

| Canonical path | Office schema | Office fields used | Enum verbatim |
|---|---|---|---|
| `objectives[]` | `BaseObjective` | `summary`, `details`, `status`, `timeHorizon`, `applicants`, `plans`, `lastReviewedAt` | `timeHorizon` ✔, `status` ✔ |
| `objectives[].objectiveType` | `BaseObjective.discriminator` | — | ✔ |
| `objectives[]` retirement fields | `RetirementObjective` | `targetAge`, `term`, `income`, `lumpSum` | — |
| `objectives[]` investment fields | `InvestmentObjective` | `targetAmount`, `targetOn`, `term`, `frequency` | — |
| `objectives[]` protection fields | `ProtectionObjective` | `coverAmount`, `term`, `isWholeOfLife` | — |

`priorityRank`, `isClientStated`, `needLevelRisk` and `shortfall` have no Office equivalent — they exist because CHK-002 and CHK-003 test them explicitly.

---

## 4. Risk, capacity and experience

| Canonical path | Office schema | Office fields used | Notes |
|---|---|---|---|
| `riskAssessment…attitudeToRisk.assessedCode` | `AtrGeneratedRiskProfile.code` | `code`, `title`, `description`, `lowerBand`, `upperBand`, `riskTolerance` | questionnaire output |
| `riskAssessment…attitudeToRisk.agreedCode` | `AtrChosenRiskProfile.code` | `code`, `chosenOn`, `chosenBy`, `title` | adopted profile |
| `…scaleName` | `NamedRiskProfileRef` / `RiskProfile.name` | `name`, `description` | |
| `riskAssessment` (case link) | `ServiceCase.client1RiskProfileRef` / `ClientRiskProfileRef` | `atrRef`, `riskProfile` | |
| `…esgOrEthicalPreferences` | `ClientInvestmentPreference` | `questions` | Office stores Q&A; canonical stores the outcome |
| `capacityForLoss` | `RiskCapacityQuestionDocument` / `RiskCapacityAnswer` | question/answer pairs | Office has no CFL *rating* field; canonical adds one |
| `riskAssessment…reconciliation` | — | — | No Office equivalent; required by CHK-003 |
| `vulnerability.perClient[]` | `ClientVulnerabilityDocument` | `hasVulnerability`, `type`, `categories`, `notes`, `assesedOn`, `reviewOn`, `vulnerabilityActionTaken` | canonical `categories` constrained to FG21/1 four drivers |

---

## 5. Existing arrangements

| Canonical path | Office schema | Office fields used | Enum verbatim |
|---|---|---|---|
| `existingArrangements[]` | `BasePlan` | `policyNumber`, `productName`, `productProvider`, `planType`, `startsOn`, `owners`, `isPreExisting`, `currentStatus`, `riskProfile` | — |
| `…` pension specifics | `PensionContributionDrawdownPlan` | `selectedRetirementAge`, `applicablePenalties`, `enhancedTaxFreeCash`, `guaranteedAnnuityRate`, `hasProtectedPcls`, `optionsAvailableAtRetirementNotes`, `otherBenefitsAndMaterialFeaturesNotes`, `lifeStyleStrategyNotes` | — |
| `…currentValue` / `…transferValue` | `PlanValuation` | `value`, `surrenderTransferValue`, `valuedOn` | — |
| `…contributions[]` | `Contribution` / `BaseContribution` | `contributionType`, `frequency`, `gross`, `net`, `startsOn` | `contributionType` ✔ |
| `…withdrawals[]` | `Withdrawal` / `BaseWithdrawal` | `withdrawalType`, `frequency`, amounts | `withdrawalType` ✔ plus `PCLS`, `UFPLS` |
| `…holdings[]` | `GetPlanHolding` + `Fund` / `BaseFund` | `fund`, `units`, `sector`, `benchmark`, `expenseRatio` | — |
| `…holdings[].ongoingChargeFigure` / `.transactionCosts` | `FundCharges` | `ongoingChargeFee`, `transactionCosts` | — |
| `…assetAllocation[]` | `AssetBreakdownAllocation` / `AtrAssetClass` | — | — |
| `…charges` | `PlanCharge` | `type`, `totalAmount`, `chargedOn`, `note` | `chargeType` ✔ |
| `…safeguardedBenefits` | `PensionContributionDrawdownPlan` fields + `PensionProtectionCertificates` | `guaranteedAnnuityRate`, `enhancedTaxFreeCash`, `hasProtectedPcls`, `guaranteedGrowthRate` | canonical promotes these to a first-class block for CHK-009 |

---

## 6. Recommendations and solution

| Canonical path | Office schema | Office fields used | Enum verbatim |
|---|---|---|---|
| `recommendations[]` | `Recommendation2Document` | `name`, `status`, `serviceCase`, `category`, `subCategory`, `owners`, `notes`, `priority`, `requirements`, `proposals` | — |
| `recommendations[].linkedObjectiveIds` | `Recommendation2Document.requirements[]` → `ObjectiveRef` | — | — |
| `recommendations[].proposalType` | `BaseRecommendation2Proposal.discriminator` | — | ✔ (1:1 — a canonical recommendation is an Office proposal) |
| `solution.recommendedPlans[]` | `BasePlan` + `NewBusinessPlanValue` | `productName`, `productProvider`, `planType`, `owners` | — |
| `…regularContributions[]` | `ProposedContributionValue` | — | — |
| `…plannedWithdrawals[]` | `ProposedWithdrawalValue` | — | — |
| `…selectedRetirementAge` | `NewBusinessPensionProposal.retirementAge` | — | — |
| `…projections[]` | `InvestmentPlan.maturityLow/Medium/HighValue` | — | canonical keeps growth-rate basis explicit |
| `solution.investmentStrategy` | `PortfolioModel` / `BasePortfolioModel` | `funds`, `benchmark`, `investmentManagementStyle`, `modelRiskProfile`, `portfolioAllocations`, `assetModel`, `riskReference` | `managementStyle` ✔ (Active/Passive) |
| `solution.investmentStrategy.strategyType` | `NewBusinessPensionProposal.investmentStrategyType` | ModelPortfolio / MISModelPortfolio | extended for MultiAssetFund / DFM / InvestmentPathway |
| `solution.providerSelection` | `ProductProvider` / `NamedProductProviderRef` | `name` | — |
| `replacementAnalysis[]` | `ExistingBusinessSwitchProposal` / `ExistingBusinessTransferRolloverProposal` | `action`, `productClosure`, `investmentStrategy` | canonical adds the comparison/lost-benefits payload CHK-009 requires |

---

## 7. Costs and charges

| Canonical path | Office schema | Office fields used | Enum verbatim |
|---|---|---|---|
| `costsAndCharges.*.lines[].chargeType` | `PlanCharge.type` | OneOffCharge / OngoingCharge / TransactionCost / IncidentalCost | ✔ |
| `costsAndCharges.adviserFees[].feeCategory` | `FeeType.category` | InitialFee / OngoingFee / AdHocFee | ✔ |
| `costsAndCharges.adviserFees[].feeChargingType` | `FeeDetail.feeChargingType` / `PlanFeeChargingType` | — | ✔ (condensed: tiered variants collapsed to `TieredPercentOfAum`) |
| `costsAndCharges.adviserFees[].paidBy` | `FeePaymentType.paidBy` | Provider / Platform | ✔ plus `Client` |
| `costsAndCharges.adviserFees[].vatTreatment` | `Charges.vat` | Inclusive / Exclusive / NonVatable | ✔ |
| `costsAndCharges.adviserFees[]` amounts | `PlanFee` | `feePercentage`, `net`, `vatRate`, `recurring`, `tierings`, `status` | — |
| `…holdings[].ongoingChargeFigure` | `FundCharges.ongoingChargeFee` | — | — |
| `costsAndCharges.reductionInYield[]` | — | — | No Office equivalent; sourced from illustrations (category H) |

---

## 8. Canonical constructs with no Office counterpart

These exist solely to make the checks executable and auditable. They should **not** be written back to Office.

| Construct | Why it exists |
|---|---|
| `Provenance` (on every entity) | A QA finding must cite page and quote to be actionable |
| `assertionStatus` (Stated/Inferred/Derived/Absent) | Separates "the report doesn't say it" from "we couldn't parse it" — different check outcomes |
| `Statement` (`text`, `topic`, `isClientSpecific`) | Office has no representation of narrative rationale; CHK-006/008 test exactly that |
| `RiskReconciliation` | CHK-003 tests the questionnaire-vs-agreed override and its justification |
| `ReplacementAnalysis` | CHK-009's bullets become named fields so absence is a null, not a prose judgement |
| `costsAndCharges.disclosureCompleteness` | CHK-007's "all material costs disclosed" test, one boolean per cost layer |
| `checkTriggers` | Makes the "N/A: trigger absent" outcome computable once rather than re-judged ten times |
| `extractionReport` | Extraction self-assessment: gaps, ambiguities, internal contradictions |
| `ExistingArrangement.riskRating.isComparableToClientScale` | Provider risk scales are not interchangeable; prevents false CHK-003 hits |
