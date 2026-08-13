# Expected results benchmark — case ABC-99

A hand adjudication of the ten checks against the case file, independent of any pipeline run.
This is the answer key: what a competent human QA reviewer should conclude, and the specific
findings they should reach it by.

**How it was produced.** The suitability report was read in full, then each supporting document
that the checks depend on — the Fact Find, the file note, the risk profile report and its
questionnaire appendices, the pension switch report, the client agreement, and the provider
policy documents for the charge figures. Each check in
[Assessment Checks & Prompts QA CA v1.0 (Checks).csv](../QA-Checks/Assessment%20Checks%20&%20Prompts%20QA%20CA%20v1.0%20(Checks).csv)
was then applied on its own terms, using its stated *What to Look For* and *Decision Logic*.
No run output was consulted while adjudicating.

**Read the finding list, not the outcome column.** Every check lands on Potential Concern, so
check-level agreement is worth almost nothing as a measure — a pipeline that returned Potential
Concern unconditionally would score 100%. The discriminating measure is which of the 36 material
findings below a run actually reaches, and that is what
[gap-analysis.md](gap-analysis.md) scores.

---

## The case in one paragraph

John Sullivan, 69, an agency HGV driver near retirement, holds five pension arrangements worth
about £117,000. The adviser recommends switching three of them (Aviva, Zurich, Standard Life —
£110,185) into a new Aviva Platform personal pension invested 50/50 in two risk-5 multi-asset
funds, retaining the People's Pension, and encashing a £211 Scottish Widows pot. Fees are 3%
initial (£3,305.55) and 1% ongoing. The attitude to risk questionnaire scored 4; an agreed rating
of 5 was recorded after discussion.

---

## Expected outcomes

| Check | Name | Expected | Confidence |
| --- | --- | --- | --- |
| CHK-001 | Suitability completeness against client information | **Potential Concern** | Certain |
| CHK-002 | Objectives, Needs and Time-Horizon Alignment | **Potential Concern** | Certain |
| CHK-003 | Risk, Capacity for Loss and Investment Strategy Match | **Potential Concern** | Certain |
| CHK-004 | Knowledge, Experience and Product Complexity | **Potential Concern** | Certain |
| CHK-005 | Financial Resilience, Affordability and Liquidity | **Potential Concern** | Certain |
| CHK-006 | Recommendation Rationale and Suitable Alternatives | **Potential Concern** | Certain |
| CHK-007 | Costs, Charges and Value of the Recommendation | **Potential Concern** | Certain |
| CHK-008 | Disadvantages, Risks and Customer Understanding | **Potential Concern** | Certain |
| CHK-009 | Replacement or Switch Justification | **Potential Concern** | Certain |
| CHK-010 | Vulnerability, Support Needs and Foreseeable Harm | **Potential Concern** | High |

No check is N/A: the case is advised, has a suitability report, involves a switch, and CHK-010
is an overlay that applies to every case.

---

## The three defects that matter most

Before the per-check detail, the findings a reviewer would escalate first. Each is a
contradiction inside the adviser's own documents, so none requires outside knowledge.

### A. The client's recorded income does not support the advice

The Fact Find records **Total Net Monthly Income £1,430** against **Total Monthly Expenditure
£1,718**, and states the result explicitly:

> Total Monthly Disposable Income | £ -288.00

The report says the opposite — *"Your combined income comfortably meets your household
expenditure of approximately £1,700 per month"* — and reaches it by counting income the Fact Find
does not record: Kim's £1,100 per month, £20 per week from the Sea Cadets, and a State Pension of
£230 **per week** where the Fact Find records £230 **per month**. The affordability case, the
capacity-for-loss assessment and the cashflow all rest on this.

### B. The switch reduces the projected fund at every growth rate

The adviser's own pension switch report, reproduced in the report's own Appendix:

| Mid growth (2.95%) | Existing pension | New solution |
| --- | --- | --- |
| Projected value at age 75 | £127,000 | £115,000 |
| Critical yield | — | **4.8%** |
| Effect of switching | — | **£12,000 or 9.4% decrease** |

The research ranks the options by maturity value and places the recommendation **fifth**:

```
1  Zurich - Zurich pension        0.2%   £128,000
2  Existing Solution              0.3%   £127,000
3  Aviva plan                     0.5%   £126,000
4  Standard Life - SL pension     0.5%   £126,000
5  New Solution                   0.6%   £125,000
```

Doing nothing beats the recommendation on the adviser's own figures, before advice charges. The
body of the report says only *"the Aviva plan is 0.24% less expensive than your current plans"*
and leads each switch rationale with **"Lower Costs"**.

### C. The client is recorded as having no investment knowledge

The Investor Experience questionnaire, in the risk profile report:

| Question | Response |
| --- | --- |
| …how would you describe yourself? | **No understanding / knowledge** |
| Do you understand how stocks and shares work…? | **No understanding / knowledge** |
| Do you understand how pension planning and income in retirement works…? | **No understanding / knowledge** |
| Do you understand how Unit Trusts and General Investment Accounts work…? | **No understanding / knowledge** |

And in the ATR questionnaire: *"I have no understanding of investments or how they work."*

The report asserts the reverse — *"you are comfortable with how investments work and have a
reasonable understanding of potential risks & rewards"* — and uses the Balanced-investor
description, which is conditioned on *"if you have some experience in investments and a degree of
understanding of financial matters"*, to justify overriding the questionnaire from 4 to 5.

---

## Per-check expected findings

Severity uses the catalogue's own scale. **Material** findings (Highest/High) are the scored set.

### CHK-001 — Suitability completeness against client information → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F1.1 | Report states earnings of £300/week net (≈£1,300/month); Fact Find records **Net Basic Monthly Income £1,200.00** | High |
| F1.2 | Report and cashflow treat the State Pension as **£230 per week** (£11,973 pa); the Fact Find income table records **£230, frequency Monthly** | Highest |
| F1.3 | Report counts Kim's £1,100/month and £20/week from the Sea Cadets; the Fact Find records **neither**, and totals household income at £1,430/month | Highest |
| F1.4 | Report states age 69; Fact Find states **Age 70** alongside **Date of Birth 07/06/1956**, which gives 69 at both the fact-find and report dates — the Fact Find contradicts itself | Moderate |
| F1.5 | Report names the client "John Sullivan"; the Fact Find's First Name and Last Name fields are blank, with Middle Name "Alan" | Moderate |
| F1.6 | Fact Find records **Residency Status: Tenant – private**, and rent of £600/month; the report's cashflow *"incorporates the assets of your main residence"* and the IHT section discusses the Residence Nil Rate Band on property passing to descendants | High |
| F1.7 | Report: *"You have confirmed you have a valid Will in place and… it would appear to meet your financial planning objectives"*; Fact Find: **"Is it up-to-date? No"** and *"Has the client been advised to make a will? Yes"* | Moderate |
| F1.8 | Report gives pension totals of **£110,000** (p3) and **£106,000** (p4); the five plans sum to **£116,998.47**. £110,185 is the amount being *switched*, not the total held | High |
| F1.9 | The Fact Find's Existing Pension Provision records only an Aviva Platform plan and the People's Pension. **Zurich, Standard Life and Scottish Widows do not appear**, though the report tabulates all five | High |
| F1.10 | Fact Find records **Highest rate of income tax paid: 0%**; the report assumes marginal-rate taxation on encashment (£170 net of £200) | Moderate |
| F1.11 | The cashflow input list states *"In retirement, you expect your annual expenditure to be approximately £1,700"* — the figure is monthly everywhere else | Moderate |
| F1.12 | Fact Find ID verification is dated **08/12/2025** and the Aviva Platform policy start **09/12/2025**, both after the report date of 14/10/2025; the plan is valued at **23/07/2026** | Moderate |

### CHK-002 — Objectives, Needs and Time-Horizon Alignment → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F2.1 | **Five different retirement horizons across the file**: report *"retire within the next two years"* (≈71); Fact Find **Intended Retirement Age 72** and **Target Age 72**; risk profile **Investment Period 2 years**; switch report and critical yield use **age 75** (*"a term of 5 years and 6 months"*); ATR narrative uses a **10-year period** | Highest |
| F2.2 | CFL questionnaire records investment horizon **"Less than 3 years"** and the Fact Find records Time Horizon **"Short Term"**, while the report justifies a risk-5 growth strategy on a 10-year view | Highest |
| F2.3 | No priority order is stated for the three objectives, which the check asks for explicitly | Low |
| F2.4 | Report says Kim retires and draws State Pension at **66**; the cashflow inputs use **age 67** | Low |
| F2.5 | Income objective is £1,700 net **per month**; the cashflow input list states it **per annum** | Moderate |

### CHK-003 — Risk, Capacity for Loss and Investment Strategy Match → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F3.1 | The file note's ATR wording reads *"We agreed that you are a risk rating 5 rather than a 4… You are happy to proceed with a Risk rating of **6**."* The report reproduces the same paragraph with **5**. Three ratings (4, 5, 6) across the file, unreconciled | High |
| F3.2 | Report: *"you wish to increase your Risk rating to 5… you are aware that this is **higher than your agreed risk rating**"* — 5 *is* the agreed rating per the risk profile report; the sentence contradicts itself | Moderate |
| F3.3 | Risk profile report records **Investment Period: 2 years** and **Investment: £106,000**; the override to risk 5 is justified by *"the best growth potential over a 10-year period"* | Highest |
| F3.4 | The **People's Pension is retained** while invested in *"Global Investments (up to 85% shares)"* at **risk level 9** against an agreed ATR of 5. The file note records *"No alternative as Defaqto risk rates all of them as a 9"*, yet the report tells the client a 60%-shares fund *"aligns with your risk appetite"* — and stops at *"You should consider"*, making no recommendation | Highest |
| F3.5 | Every substantive ATR answer is cautious — *"Cautious"*, *"More cautious"*, *"Low risk investments"*, anxiety at a £4,000 fall on £20,000 (20%) — and the natural risk level is 4 Cautious Balanced. The upgrade rests on non-reliance and a 10-year horizon, both contradicted elsewhere | High |
| F3.6 | The risk profile report records **"No additional notes were recorded"** under every discussion heading, including Agreed risk level and Capacity for loss, so no contemporaneous record of the override discussion exists | Moderate |
| F3.7 | CFL questionnaire records *"My spouse… is likely to be able and willing to support me financially: **Disagree**"*, which cuts against a medium capacity for loss resting on Kim's income | Moderate |

### CHK-004 — Knowledge, Experience and Product Complexity → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F4.1 | Report asserts the client is *"comfortable with how investments work and [has] a reasonable understanding of potential risks & rewards"*; the Investor Experience questionnaire records **"No understanding / knowledge"** for the overall question and for stocks and shares, unit trusts, EIS/VCT **and pension planning** | Highest |
| F4.2 | ATR questionnaire: *"I have no understanding of investments or how they work"* | Highest |
| F4.3 | The Balanced-investor description is applied verbatim although its stated precondition — *"some experience in investments and a degree of understanding of financial matters"* — is contradicted by the file | High |
| F4.4 | A SIPP with flexi-access drawdown is recommended to a client recorded as having no pension knowledge, with no evidence anywhere in the file of comprehension being tested | High |

### CHK-005 — Financial Resilience, Affordability and Liquidity → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F5.1 | Fact Find states **Total Monthly Disposable Income –£288.00**; the report states income *"comfortably meets"* expenditure | Highest |
| F5.2 | The affordability case depends entirely on income the Fact Find does not record (F1.2, F1.3) | Highest |
| F5.3 | The £6,000 emergency fund is the client's entire savings, and the proposed cashflow scenario marks it **"LIQUIDATED 2ND"** — scheduled to be spent | High |
| F5.4 | The cashflow *"incorporates the assets of your main residence"* for a client who rents | High |
| F5.5 | The cashflow assumes **0% of surplus income is saved** while the report says the pension *"will be allowed to keep growing for large ad hoc spends"* | Moderate |
| F5.6 | The £3,305.55 initial fee is taken from the fund, reducing retirement provision by 3% at the point of transfer; affordability of the fee is never addressed | Moderate |

### CHK-006 — Recommendation Rationale and Suitable Alternatives → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F6.1 | The research ranks the **Existing Solution (£127,000) and retaining Zurich (£128,000) above the New Solution (£125,000)**. The report does not disclose the ranking or explain why a lower-ranked option was chosen | Highest |
| F6.2 | The switch rationale for Aviva, Zurich and Standard Life is the same five bullets, near-verbatim, differing only in the cost sentence — generic where the check requires client-specific reasoning linked to stated goals | High |
| F6.3 | **"Lower Costs"** is the lead rationale for the Standard Life switch, which the report's own table shows **increases** charges by 0.26% / £268.94 | Highest |
| F6.4 | Reasons for rejecting alternatives are thin and partly contradictory — the file note discounts an Aviva internal switch because *"Only mixed asset aviva fund available is risk rated 4 which does not meet the clients ATR"*, while the report recommends a People's Pension fund on the opposite logic | Moderate |
| F6.5 | A generic stakeholder comparison is present and adequately explained | No issue |

### CHK-007 — Costs, Charges and Value of the Recommendation → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F7.1 | The report's two charge tables contradict each other. Existing Arrangements (p5) gives **Zurich 0.18%** and **Standard Life 0.52%**; Pension Switch Charges (p11) gives **Zurich 0.93%** and **Standard Life 0.18%** | Highest |
| F7.2 | The Standard Life row is computed on the **wrong fund**: £186.19 is 0.18% of £103,439 (Zurich's value), not of Standard Life's £3,002. The stated impact of £268.94 on a £3,002 fund would be 8.96% | Highest |
| F7.3 | *"the Aviva plan is 4.24% more expensive"* = 3% initial + 1% ongoing **+** 0.24%. The like-for-like saving has been **added instead of subtracted**; the figure should be ≈3.76% | High |
| F7.4 | *"minus the initial fee, this would be equal to circa £1,101.85"* — £1,101.85 is 1% of the full £110,185, i.e. not minus the initial fee | Moderate |
| F7.5 | The critical yield of **4.8% real** against a mid growth assumption of 2.95% is justified solely by past performance, in a report that twice states past performance is no guarantee | High |
| F7.6 | *"0.24% less expensive… £264"* does not reconcile with the report's own table rows (−£2.25 − £506.85 + £268.94 = **−£240.16**, or 0.218%) | Moderate |
| F7.7 | Mid growth rate is quoted as **2.94%** in the text and **2.95%** in the table | Low |
| F7.8 | Adviser charges reconcile exactly with the Client Agreement (3%, £3,305.55, 1% ongoing) | No issue |

### CHK-008 — Disadvantages, Risks and Customer Understanding → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F8.1 | The most material disadvantage — a **£12,000 (9.4%) reduction** in projected value at age 75 — appears only in the Appendix. The body leads with *"Lower Costs"* and *"0.24% less expensive"*. This is a prominence failure under COBS 4.2.1R | Highest |
| F8.2 | The report never discloses that the research ranked taking no action above the recommendation | Highest |
| F8.3 | No loss scenario is illustrated for the recommended funds; the −30% stress test is applied to the cashflow, not to the investment | Moderate |
| F8.4 | Tax consequences are stated generically (*"taxed at their marginal rate"*) and the client's marginal rate is never established | Moderate |
| F8.5 | Features and guarantees lost are stated as none, consistent with the switch report's *"No benefits were listed for the existing products"* | No issue |

### CHK-009 — Replacement or Switch Justification → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F9.1 | The switch **reduces projected maturity value at every growth rate**, before advice charges (−1.6% at mid growth) and after them (−9.4%) | Highest |
| F9.2 | The adviser's own ranking places the existing arrangement above the recommendation (F6.1) | Highest |
| F9.3 | The Standard Life switch **increases** charges while being justified on lower costs (F6.3) | Highest |
| F9.4 | Retaining Zurich alone projects **£128,000** against **£125,000** for the recommendation; the report gives no reason for preferring the lower figure | High |
| F9.5 | Existing charge figures for Zurich and Standard Life are internally inconsistent (F7.1), so the cost comparison driving the switch cannot be relied on | High |
| F9.6 | Scottish Widows is recorded as *"Retain and Encash"* in Existing Arrangements against an Encash recommendation, and the encashment is worked on a **£200** fund yielding **£170** where the fund is **£211.32** | Moderate |

### CHK-010 — Vulnerability, Support Needs and Foreseeable Harm → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F10.1 | The file records **no understanding of investments or pensions** (F4.1, F4.2) — a financial-capability vulnerability indicator under FG21/1. The report contains no vulnerability assessment and never considers it | Highest |
| F10.2 | A 69-year-old at the point of retirement is moving effectively all of his pension provision on advice, with no vulnerability screening recorded anywhere in the file | High |
| F10.3 | The Fact Find records *"Accessible format requirement: No Requirement"* and *"Preferred delivery method: No Preference"*; neither is reflected or confirmed in the report | Low |
| F10.4 | Advice was given by recorded video call with *"Anybody else present at the Meeting? No"*; no third-party support was considered despite the recorded knowledge gap | Moderate |
| F10.5 | Power of Attorney is not in place and the report recommends putting one in place | No issue |

---

## Scoring set

The **36 material findings** (Highest and High) are the benchmark for recall:

```
F1.1  F1.2  F1.3  F1.6  F1.8  F1.9
F2.1  F2.2
F3.1  F3.3  F3.4  F3.5
F4.1  F4.2  F4.3  F4.4
F5.1  F5.2  F5.3  F5.4
F6.1  F6.2  F6.3
F7.1  F7.2  F7.3  F7.5
F8.1  F8.2
F9.1  F9.2  F9.3  F9.4  F9.5
F10.1 F10.2
```

A finding counts as **caught** when a run states the substance of the contradiction — naming both
sides and what conflicts. It counts as **partial** when a run touches the area but misidentifies
the conflict, states it without the contradiction, or buries it among unrelated points. Anything
else is **missed**.

**Missed findings are the expensive error.** A missed concern leaves no trace in the output; a
spurious one is visible and can be discarded by a reviewer. Any gate on this dataset should be set
on missed-rate, not on overall agreement.

---

## Known limits of this benchmark

- **One case, one adjudicator.** These are my readings of the documents. The findings marked
  Highest and High are contradictions between documents and should survive review; the Moderate
  and Low ones involve more judgement.
- **Every check lands on Potential Concern**, so this dataset cannot measure false positives at
  check level at all. A case with clean checks is needed for that, and should be built next.
- **Some findings are entailed by others.** F5.2 depends on F1.2 and F1.3; F9.2 restates F6.1
  from the switch side. They are scored separately because the checks are separate, but a run that
  misses the parent will usually miss the child, so recall is not 36 independent trials.
- **Document conversion is a confound.** Everything here was read from the Markdown conversions,
  which lose table structure in places. Where a figure was ambiguous I checked it against a second
  document before recording it as a finding.
