# Expected results benchmark — Test Case 2 (Barnes)

A hand adjudication of the ten checks against the case file, independent of any pipeline run.
This is the answer key: what a competent human QA reviewer should conclude, and the specific
findings they should reach it by.

**How it was produced.** The suitability report
([Suitability Report - Barnes.pdf](../../QA-Checks/Test-Cases/Test%20Case%202/I/Suitability%20Report%20-%20Barnes.pdf))
was read in full, then every supporting document the checks depend on — the Fact Find, the
adviser's file note, the risk profile report and its three questionnaire appendices, the Aviva
client report and plan information form, the policy research sheet, the pension switching report
and research, the ISA/GIA/PPP research and review reports, all five cashflow scenarios, the FE
Analytics performance and volatility exhibits, the three Aviva illustrations, the two fund
factsheets and the KIID, and the Client Agreement. Each check in
[Revised checks (loadable).csv](../../QA-Checks/Revised%20checks%20(loadable).csv) was then applied
on its own terms, using its stated *What to Look For* and *Decision Logic*. No run output was
consulted while adjudicating.

**Read the finding list, not the outcome column.** Every check lands on Potential Concern, so
check-level agreement is worth almost nothing as a measure — a pipeline that returned Potential
Concern unconditionally would score 100%. The discriminating measure is which of the 47 material
findings below a run actually reaches.

---

## The case in one paragraph

Anthony ("Tony") Barnes, 55, recently retired from a factory job, married to Yvette, 59. He has a
£22,000 pa Aon defined benefit pension, one Aviva Platform personal pension worth £79,092.32, a
mortgage-free house worth £400,000, and £190,000 of cash including a £100,000 inheritance from his
late father. His agreed risk level is 4 (Cautious Balanced) on a 1–10 scale; his existing pension
funds map to 6. The adviser recommends keeping the pension where it is but moving it under TFAS
agency (platform charge 0.32% → 0.21%), switching it 100% into the Aviva Insured Funds Multi-Asset
Plus Fund II, adding a £3,600 gross (£2,880 net) contribution, investing £57,120 of the inheritance
in a new Aviva GIA and transferring a £20,000 Moneybox Cash ISA into an Aviva Stocks & Shares ISA,
both in the CT Universal MAP Cautious Fund. Fees are 3% initial (£4,794.37, taken wholly from the
GIA) and 1% ongoing across all three wrappers.

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

No check is N/A. CHK-009 in particular **applies**: the case involves a 100% fund switch of the
whole pension, a change of servicing agency, and a Cash ISA to Stocks & Shares ISA transfer. A run
that returns N/A for CHK-009 on the grounds that "the pension is being retained" is wrong.

---

## The four defects that matter most

Each is a contradiction inside the adviser's own documents, so none requires outside knowledge.

### A. The switch is worth less than doing nothing, and the comparison that hides it is rigged

The adviser's own Defaqto switching report, before advice charges:

| Mid growth (2.94%) | Existing pension | New solution |
| --- | --- | --- |
| Projected value at age 60 | **£86,800** | **£86,700** |
| Effect of switching | — | **£100 or 0.1% decrease** |

and the ranking by maturity value places the recommendation second:

```
1  Existing Solution                                   0.6%   £86,800
2  New Solution                                        0.7%   £86,700
3  Aviva - Pension Portfolio - Core / Aviva Platform   0.7%   £86,700
4  Standard Life - Active Money SIPP Level 2           0.7%   £86,400
```

Neither the decrease nor the ranking appears anywhere in the suitability report. What the report
does reproduce, as its Appendix, is the *after advice charges* table, where both columns are
identical (£83,300, RIY 1.7%, "£0 or 0.0% decrease", critical yield 2.9% against a 2.94% growth
rate). That equality exists only because a **1% ongoing adviser charge has been applied to the
existing plan as well as the new one** — a charge the client does not pay. The Defaqto source says
the 1% "has been considered as part of the **new solution** projections"; the suitability report
rewords this to "both the new and old solution projections". The switch report separately records
the existing plan as "Subject to an existing advice charge: **No**", the policy research sheet
records "Current Adviser charge: No", and the Aviva client report shows £0.00 of adviser charges
over three years against £637.92 of Aviva charges.

### B. "Lower Costs" is the lead rationale for a recommendation that costs more

The report's first switch rationale is *"Lower Costs - Moving to our agency terms will reduce your
platform charge from 0.32% to 0.21%"*. Its own Pension Switch Charges table, six paragraphs later:

| Plan | Existing annual charge | Impact of recommendation |
| --- | --- | --- |
| Aviva Pension Portfolio - AV2075926-001 | 0.54% / £427 | **+0.08% / +£63** |

The platform saving of 0.11% is more than cancelled by the fund charge rising from 0.22% to 0.41%.
Adding the 1% ongoing adviser charge, which is new, takes the client from 0.54% to 1.62%.

### C. The client is recorded as having no understanding of the product he is being sold

The Investor Experience questionnaire in the risk profile report:

| Question | Response |
| --- | --- |
| …how would you describe yourself? | **Very little understanding / knowledge** |
| Do you understand how **Unit Trusts and General Investment Accounts** work…? | **No understanding / knowledge** |
| Do you understand how stocks and shares work…? | **No understanding / knowledge** |
| Do you understand how **pension planning and income in retirement** works…? | **Very little understanding / knowledge** |
| Have you invested in Unit Trusts and General Investment Accounts within the last 5 years? | **No** |

The recommendation is a £57,120 General Investment Account holding 100% of a unit trust, plus a
SIPP with flexi-access drawdown. The report quotes the first three answers, omits the pension one,
and never addresses the mismatch. There is no vulnerability assessment anywhere in the file.

### D. The recommended ISA returns less than the cash account it replaces

The report's Alternative Solutions section:

> We also considered whether you should retain your current Moneybox ISA. The current interest rate
> is circa 4.5% however, this is variable… The recommended ISA funds have provided an average
> performance of 4.72% over a 5-year period. Therefore, there is potential to achieve greater growth
> by investing these funds.

4.72% is gross of everything. The Aviva ISA illustration, in the same case file, states that **all
charges reduce growth from 5.0% to 3.4%** — 0.21% platform, 0.29% fund, 0.22% transaction costs and
1% adviser. The 4.72% figure is also arithmetically loose: it is 23.61 ÷ 5 from the report's own
five-year cumulative table; the compound equivalent is 4.33%, and the ISA research report gives
31.28% for the same fund over five years. And the adviser's own cashflow gives cash and the
invested funds the *same* 4.5% gross return, charging 1.5% only to the invested ones.

---

## Per-check expected findings

Severity uses the catalogue's own scale. **Material** findings (Highest/High) are the scored set.

### CHK-001 — Suitability completeness against client information → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F1.1 | Report: *"current regular expenditure is approximately **£2,493 per month** covering essential costs… plus discretionary spending of approximately £800 per month… plus around £5,000 annually on holidays"* (≈£3,710/month). The Fact Find and every cashflow record **£2,206.67 per month / £26,480 pa in total**, of which the £800 entertainment and £5,000 holidays are already components; essential items sum to £990. The report's own cashflow section states *"approximately £2,206 per month"* | Highest |
| F1.2 | Report: *"you decided that this was not suited to you and have **since stopped working altogether**"*. The Fact Find records him as **Employed, Occupation Driver, Gross Basic £14,000 pa, Net Basic Monthly £890**, and builds Total Net Monthly Income £2,590 and disposable income £383.33 on that income continuing. The cashflow models **no employment income for him at all** | Highest |
| F1.3 | Report, in one paragraph: *"We are unsure of your earnings this tax year… Therefore, we have recommended the maximum you can contribute"* followed by *"However, **your current earnings are sufficient** to support the recommended contribution level"*. The file note records *"We need to assess earnings for this year… Does he have a P60/P45? – none"* | Highest |
| F1.4 | Yvette's income of **£28,000 gross pa** is recorded nowhere in the evidence — the Fact Find income table contains only Anthony's two entries. The cashflow input the report describes (*"£28,000 gross per annum, pro-rata for 3 days per week"*) is not what was modelled: the model uses a flat **£16,800** from 2025, matching the "Yvette reduced inc calcs" worksheet | High |
| F1.5 | Existing Arrangements: *"Retirement Options: Full flexi-access drawdown **and UFPLS** available"*. The Aviva plan information form states *"Uncrystallised fund pension lump sum is **not applicable** to this plan"* and the policy research sheet records *"Full and partial UFPLS available: **no**"* | High |
| F1.6 | Existing Arrangements and the platform benefits list: *"Over **6,900** funds available"*. The Aviva plan information form states *"We have over **3000** funds with more than 110 fund managers"* and the policy research sheet records *"Number of funds available: 3000"* | Moderate |
| F1.7 | The Fact Find answers *"Do you have any existing final salary schemes? **No**"* while its own income table records *"Private Pension **Aon Final Salary** — Monthly — £1,833.00 gross / £1,700.00 net"*. The client's largest income source has no scheme documentation on file, yet the cashflow asserts 2% escalation and 50% spouse's continuation | High |
| F1.8 | The Fact Find records the Aviva plan at **£88,590.88**, valuation date **23/07/2026** and policy start date **09/03/2026** — both after the report date. Every other document records **£79,092.32 at 23/11/2025** and a start date of **27/09/2013** | Moderate |
| F1.9 | *"You both expect to receive the full State Pension of £11,973 per annum from age 67"* — the Fact Find's State Pension Entitlement section is entirely blank with no BR19; the policy research sheet records only *"SP forecast Y / Amount: Full"* for Anthony. Nothing is on file for Yvette | Moderate |
| F1.10 | *"have three adult children and three grandchildren. All your children are financially independent"* — the Fact Find's Family and Dependants section records only Yvette. The IHT section's Residence Nil Rate Band claim depends on property passing to direct descendants | Moderate |
| F1.11 | Report states the client is aged **55** and Yvette **59**, consistent with the recorded dates of birth (06/01/1970, 06/10/1966) and with the cashflow. The **Fact Find contradicts itself**, recording Age 56 and Yvette 58 against the same dates of birth | Low |
| F1.12 | *"You have confirmed **you each** have a valid Will in place"* and *"You confirm you have a **registered** Power of Attorney covering your financial affairs and health and welfare"*. The Fact Find records Will Yes / up-to-date Yes and *"Power Of Attorney Granted? Yes"* with no attorney named, for Anthony only. Yvette was never fact-found | Moderate |
| F1.13 | *"You currently **jointly** hold £190,000 in liquid assets"* — all six assets, including *"Wife's MoneyBox Cash ISA"*, are recorded in the Fact Find under Owner: Anthony Barnes | Low |
| F1.14 | Asset totals (£400,000 + £190,000 + £79,092 = £669,092), the £190,000 cash breakdown, the mortgage-free owner-occupier status, the £22,000 DB pension and the £10,000 emergency fund all reconcile with the Fact Find | No issue |

### CHK-002 — Objectives, Needs and Time-Horizon Alignment → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F2.1 | **Five different retirement/horizon positions across the file**: the report says he *"recently retired"* and targets age 60; the Fact Find records Intended Retirement Age 60, Target Age 60 and Time Horizon **"Medium Term"**; the risk profile report ran its projections over an **Investment Period of 5 years**; the pension switch report uses **4 years** to age 60; the CFL questionnaire records **"3-9 years"**; and the **cashflow models Anthony's "Retire" event at age 67 in 2037**. The suitability report never states an investment term for any of the three wrappers | Highest |
| F2.2 | Yvette's position is stated three ways: *"intending on reducing her hours to 3 days per week from **next year**"*, *"Yvette plans to fully retire when you turn 60"* (2030), and a cashflow carrying both a salary ending in 2030 and a *"Retire (Yvette)"* event at **age 67 in 2033**, with income modelled at the reduced £16,800 from **2025** | High |
| F2.3 | Two of the five "agreed" objectives appear in no evidence document: *"simplify your administration by keeping your pension, ISA and GIA on one Aviva Platform"* and *"take advantage of the pension annual allowance with a contribution of £3,600 gross"*. The Fact Find, the file note and the policy research sheet record four objectives only — review the Aviva pension, invest the inheritance tax-efficiently, be fully retired by 60, and not take excessive risk | High |
| F2.4 | The Cash ISA transfer is presented as a client objective (*"You want to… consider transferring your Cash ISA"*) and then recommended. The file note records *"Possibly move ISAs – **not likely though**"* and the Fact Find records *"Potentially interested in transferring his cash ISA to S&S ISA but **would have to be really worth his while - not dead set on doing this**"* | High |
| F2.5 | The report never reconciles the £2,200 net monthly income objective with the expenditure it states elsewhere. On the report's own £2,493 + £800 + £5,000 pa figure the target is £1,500/month short; on the Fact Find's £2,206.67 it leaves nothing | High |
| F2.6 | The stated intention to spend *"circa £40,000 of your inheritance on renovations"* is carried into the arithmetic (£100,000 − £40,000 = £60,000 to invest, less the £2,880 net contribution = £57,120) but the renovation spend is not modelled anywhere in the cashflow | Moderate |
| F2.7 | Liquidity and access requirements — the 10-15 working day disinvestment period against £40,000 of unrestricted savings and a £10,000 emergency fund with a recorded shortfall of £0 — are stated and supported | No issue |

### CHK-003 — Risk, Capacity for Loss and Investment Strategy Match → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F3.1 | Capacity for loss is stated as **Medium-High**, resting on guaranteed income and Yvette's position. The Capacity for Loss questionnaire records *"My spouse or partner… is likely to be able and willing to support me financially if circumstances require: **Strongly disagree**"* and *"It would be relatively easy for me to cut my spending in retirement: **Disagree**"* | Highest |
| F3.2 | No capacity-for-loss rating or discussion exists anywhere in the evidence. The risk profile report records **"No additional notes were recorded"** under *Capacity for loss questionnaire*, *Capacity for loss for the goal*, *Agreed risk level*, *Investor Experience* and *Investment target*. The report's *"Based on our conversations and analysis, I have determined that your capacity for loss is Medium-High"* has no contemporaneous record behind it | High |
| F3.3 | ~~The stated CFL rationale is contradicted by the adviser's own modelling~~ — **WITHDRAWN 2026-08-28.** The Current Situation cashflow statement shows income deficits of £7,101 / £7,052 / £7,002 in 2030–2032, met from liquid assets: Yvette's salary ends in 2030 and her State Pension starts in 2033, which is exactly the period the report describes. The original entry conflated the *asset* shortfall (none, correctly) with the *income* deficit (real). The residual point — the report never quantifies the period or the draw against it — is Low, and is covered by F5.1 | ~~High~~ Withdrawn |
| F3.4 | The projections that produced the agreed risk level were run on an **Investment Period of 5 years and an Investment of £160,000** as a single goal. The recommendation splits that money across a pension held to at least age 60, a GIA and an ISA described as medium-to-long-term and modelled to age 100+, with no term stated for any of them | High |
| F3.5 | Alternative Solutions states *"One fund is aligned to a risk profile of **5** and the other to a risk profile of **7**"*. No document rates the individual Vanguard funds; the only evidenced rating is the combined **6** (10-year volatility 12.34 → risk level 6, per the Personal Pension Plan review report), which the report also states elsewhere | Moderate |
| F3.6 | The fund tables present both recommended funds as *"Risk Rating 4"* alongside an agreed risk level of 4 **on a 1–10 scale**. Aviva rate their funds 1–7 and the CT KIID rates that fund *"4"* on the 1–7 SRRI scale. The conclusion happens to hold — the Defaqto research independently maps both portfolios to 4 on the client's scale — but the report equates ratings from different scales without saying so | Moderate |
| F3.7 | The report tells the client the funds *"are managed to remain within this risk tolerance"* and are *"risk-profiled solutions… specifically designed to remain within your agreed level of risk"*. The CT KIID describes discretionary active management within a long-term volatility range, and the Defaqto research records no Diamond rating and no risk-controlled marker for either fund | Moderate |
| F3.8 | The recommended CT fund's realised volatility (6.52) is **higher** than the Risk Level 4 benchmark (6.30) and higher than the recommended pension fund (6.25), which the report does not mention while asserting the recommendation is the lower-risk option | Low |
| F3.9 | The natural risk level (4) and the agreed risk level (4) match, with no override to explain, and the Cautious Balanced narrative is reproduced accurately from the risk profile report | No issue |
| F3.10 | The existing holdings' risk level of 6 against an agreed 4 is correctly stated and is properly evidenced by the Personal Pension Plan review report | No issue |

### CHK-004 — Knowledge, Experience and Product Complexity → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F4.1 | A **£57,120 General Investment Account invested 100% in a unit trust** is recommended to a client whose Investor Experience questionnaire records *"Do you understand how **Unit Trusts and General Investment Accounts** work…? **No understanding / knowledge**"* and *"Have you invested in Unit Trusts and General Investment Accounts within the last 5 years? **No**"*. The report reproduces the first answer and never returns to it | Highest |
| F4.2 | The report's Knowledge & Experience section omits the questionnaire answer that bears on the pension recommendation: *"Do you understand how **pension planning and income in retirement** works…? **Very little understanding / knowledge**"*. The recommendation is a SIPP with flexi-access drawdown, and the report devotes pages to drawdown, the MPAA, lump sum allowances and pension IHT | High |
| F4.3 | No evidence anywhere in the file that the client's comprehension was tested or that any step was taken to support understanding. The risk profile report records **"No additional notes were recorded"** against Investor Experience | High |
| F4.4 | The Attitude to Risk section asserts as fact *"You have limited or moderate experience of investments and do not find financial matters particularly easy to understand"*. This is verbatim from the generic Defaqto Cautious Balanced description, where it is **conditional** (*"If you have limited or moderate experience…"*). The client's own answers are *"very little understanding / knowledge"* and *"I have a basic understanding of what investments are but limited knowledge on how they work"* | Moderate |
| F4.5 | The report's technical sections — GIA dividend/interest/CGT rates, lifetime and annual allowances, MPAA, investment pathways, the CIP explanation — are generic and unpersonalised, in a report for a client recorded as having no understanding of the underlying vehicles | Moderate |
| F4.6 | The stated experience that *is* reproduced (cash accounts, self-directed shares, pension planning through an adviser, annual valuation checks) matches the questionnaire | No issue |

### CHK-005 — Financial Resilience, Affordability and Liquidity → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F5.1 | If the report's own statement that the client has *"stopped working altogether"* is correct, the affordability position reverses: the Fact Find's £383.33 monthly surplus is derived from Total Net Monthly Income £2,590 **including £890 of employment income**. Without it, income is £1,700 against expenditure of £2,206.67 — a **deficit of ~£507 per month** before Yvette's income, which the Fact Find does not record. The report states the position *"leaves you with surplus income each month"* | Highest |
| F5.2 | The *"Cashflow - Current Situation"* is not the current situation. The Baseline Scenario already contains the recommended arrangements — **Aviva GIA £52,326** (i.e. net of the £4,794.37 initial fee), an **Aviva Stocks & Shares ISA of £20,000** at 0.5% + 1% fees, and a pension of **£82,692** including the £3,600 contribution. **No pre-advice baseline exists anywhere in the file**, so nothing in the modelling demonstrates that the recommendation improves the client's position | Highest |
| F5.3 | The report tells the client the assets chart shows *"your property value, investment portfolios, and liquid assets"* and that *"your wealth should continue to grow"*. Both property entries in the cashflow are marked **"EXCLUDED FROM SCENARIO"** and the Property column is £0 in every one of the 50 projected years | High |
| F5.4 | The cashflow gives the Chartered savings account and Yvette's Moneybox Cash ISA a **4.5% expected return with no charges** — the same gross return as the GIA and ISA, which bear 1.5% of charges. On the adviser's own assumptions the client is worse off investing, which contradicts the report's rationale for doing so | High |
| F5.5 | The £4,794.37 initial fee is taken **entirely from the GIA**, reducing it from £57,120 to £52,325.63 — **8.4% of that account** on day one, for advice largely concerning the pension and the ISA. The report presents this as being done *"to ensure the maximum amount is invested tax-efficiently in your pension and ISA"* and never states the effect on the GIA | High |
| F5.6 | The affordability narrative and the cashflow rest on **different expenditure figures** (F1.1), so neither can be relied on | High |
| F5.7 | The pension is **never drawn** in any scenario — it still holds £51,694 at age 99 — and the £2,200 target income is met throughout from the DB pension, Yvette's salary and the State Pensions. The report's rationale that the pension changes keep it *"on track to support your retirement goals"*, and its flexi-access drawdown discussion, are unsupported by its own modelling | Moderate |
| F5.8 | The Higher Inflation (6% general and State Pension), Market Crash (−30% in 2028, no recovery) and both predecease scenarios are correctly described: none produces a shortfall in any year, and the funds last past age 100 | No issue |
| F5.9 | Emergency fund provision (£10,000 held, £10,000 required, £0 shortfall) and the statement that the client retains sufficient liquid capital are supported | No issue |

### CHK-006 — Recommendation Rationale and Suitable Alternatives → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F6.1 | The adviser's own switching research **ranks the Existing Solution first (£86,800) and the New Solution second (£86,700)** on maturity value at age 60 before advice charges, recording the effect of switching as *"£100 or 0.1% decrease"*. The suitability report reproduces only the post-advice-charge table and discloses neither the ranking nor the decrease | Highest |
| F6.2 | **"Lower Costs"** is the lead rationale for the pension changes while the report's own Pension Switch Charges table on the same page shows the recommendation is **0.08% / £63 a year more expensive**; the platform saving of 0.11% is outweighed by the fund charge rising from 0.22% to 0.41% | Highest |
| F6.3 | The Cash ISA transfer is justified by comparing the Moneybox rate of *"circa 4.5%"* against the fund's *"average performance of 4.72% over a 5-year period"* — a gross-of-everything comparison. The Aviva ISA illustration in the same file shows **all charges reducing growth from 5.0% to 3.4%** | Highest |
| F6.4 | The switching research models the existing plan as *"Current Aviva"*, a Personal Pension Plan with **"Platform: None"**, and recommends switching to *"Pension Portfolio - Choice"* on the Aviva Platform. The client is **already** in the Aviva Platform Pension Portfolio (AV2075926-001, opened 27/09/2013), and the actual recommendation is an agency change plus a fund switch inside that plan. The research does not analyse what is being recommended | High |
| F6.5 | The £3,600 gross contribution is recommended on self-contradictory grounds (F1.3). £3,600 is the **non-earner** limit; the Fact Find records £14,000 of gross earnings, and the file note records that no P60/P45 was obtained. The report neither establishes relevant earnings nor explains why the lower limit was used | High |
| F6.6 | The report lists five options considered (leave untouched / switch funds in situ / switch to another existing arrangement / switch to the employer's scheme / new plan) and never says which was rejected or why. The employer scheme option is live on the Fact Find — *"Does your employer currently operate a pension scheme? **Yes** / Are you a member? **No** / Are you or will you become eligible to join? **Yes**"* — and the file note flags it (*"Has he been enrolled into a workplace scheme yet?"*). It is not discussed | High |
| F6.7 | The 4.72% performance figure is a simple average (23.61 ÷ 5) of the report's own five-year cumulative return; the compound equivalent is 4.33%, and the ISA research report gives 31.28% over five years for the same fund. Three inconsistent figures for one number that carries the ISA rationale | Moderate |
| F6.8 | The Recommendation Summary and the recommendation tables are internally inconsistent about which fund goes where: the summary allocates the Aviva fund to the pension and the CT fund to the GIA and ISA, while the *Investments* block under Recommendations lists the Aviva fund for the GIA/ISA, and the second *"Recommended Solution - Pension"* heading actually covers the GIA and ISA | Moderate |
| F6.9 | The GIA research was run on **£60,000** while the recommendation invests £57,120; no research covers the £2,880 net pension contribution | Low |
| F6.10 | A generic stakeholder comparison is present and is properly evidenced by the switching research (generic stakeholder £83,700 against the new solution's £86,700) | No issue |
| F6.11 | Every existing arrangement has a stated outcome, and the report states clearly what will be arranged and by whom (including posting the Expression of Wish form) | No issue |

### CHK-007 — Costs, Charges and Value of the Recommendation → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F7.1 | The critical yield appendix states the 1% ongoing adviser charge *"has been considered as part of **both the new and old** solution projections"*. The Defaqto source says *"as part of the **new solution** projections"*, and records the existing plan as *"Subject to an existing advice charge: **No**"*; the policy research records *"Current Adviser charge: No"*; the Aviva client report shows **£0.00** adviser charges over three years. Loading a charge the client does not pay onto the existing plan is what produces the "£0 or 0.0% decrease" and the 2.9% critical yield | Highest |
| F7.2 | The report's body contradicts its own appendix: *"including our 1% annual fee, the total cost is approximately **1.62%** annually compared to your current arrangement of 0.54%… your proposed plan will need to **outperform your current plan by approximately 1.08% annually**"*, against an appendix showing an effect of switching of £0 / 0.0% at every growth rate | Highest |
| F7.3 | The ISA and GIA totals are stated as **0.5%** (0.21% platform + 0.29% fund). The adviser's own ISA and GIA research reports give the CT fund's total cost as **0.51%** — 0.29% ongoing plus **0.22% transaction costs** — making the real totals 0.72%. The exclusion is disclosed in a paragraph, but the understated totals are then used in the value comparisons | High |
| F7.4 | The critical yield of 2.9% is justified by *"the past performance of your recommended funds"* in a report that twice states past performance is no guarantee — and the critical yield is a **real** figure after 2% inflation while the quoted past performance is nominal | High |
| F7.5 | The initial fee of £4,794.37 is 3% of £159,812, a base that includes the **£79,092 already held in the existing pension** (which stays in the same plan) and the **£20,000 Cash ISA being transferred**. The report never explains what initial advice work is being paid for on assets that are not newly invested | High |
| F7.6 | The cumulative effect of charges never reaches the body of the report. The illustrations show all charges reducing the GIA from £88,700 to **£69,900** over ten years (growth after inflation 4.5% → 2.0%), the ISA 5.0% → 3.4%, and the pension 2.6% → 0.7%. The report says only *"The charges are competitive"* and *"this small increase in charges is justified"* | High |
| F7.7 | The ongoing fee is stated as *"approximately £1,558 for the upcoming year (based on total platform value of **£155,812** after initial fee deduction)"*. £159,812 − £4,794.37 = **£155,017.63**, and the three illustrations give £826.92 + £200.00 + ~£523 ≈ **£1,550**. Neither figure reconciles | Moderate |
| F7.8 | Growth assumptions differ across the file with no reconciliation: the cashflow uses **4.5%** for all Aviva Platform investments (and for cash), the switch report and appendix **2.94% real**, the pension illustration a **4.7%** medium rate (2.6% after inflation), the GIA illustration **4.5%** and the ISA illustration **5.0%** | Moderate |
| F7.9 | The recommended pension fund's charge is stated as 0.41%; the Aviva pension illustration records the OCF/TER as **0.42%** | Low |
| F7.10 | Adviser charges reconcile exactly with the Client Agreement (3% initial with a £1,350 minimum, £4,794; 1% ongoing with a £100 minimum) and with all three illustrations | No issue |
| F7.11 | The existing charge of 0.54% (0.32% platform + 0.22% weighted fund) is correctly stated and is corroborated by the policy research sheet, the switch report and the historic Aviva charges of £637.92 over three years | No issue |

### CHK-008 — Disadvantages, Risks and Customer Understanding → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F8.1 | The most material fact in the switching analysis — that the recommendation projects **below** doing nothing (£86,700 against £86,800) and is ranked second behind the existing solution — appears nowhere in the report, which leads instead with *"Lower Costs"*. A prominence failure under COBS 4.2.1R | Highest |
| F8.2 | The report states *"Although your current funds have outperformed the recommended funds, they have **suffered much steeper losses**, which is demonstrated in the charts above"*. The charts show the opposite for the fund recommended for the GIA and ISA: in calendar 2022 the current strategy fell **9.58%**, the Aviva fund **8.73%** and the **CT fund 11.32%**; over the 36-48m period the figures are **−6.15%, −5.97% and −7.40%** | Highest |
| F8.3 | The client gives up **15 percentage points of five-year performance** (45.28% against 30.12%) to reduce volatility from 8.45 to 6.25. The trade-off is stated only as a qualitative reassurance and is never quantified in money | High |
| F8.4 | The tax consequences of a £57,120 GIA are set out only generically. The report never establishes the client's marginal rate, never estimates the dividend or interest yield, and never notes that the £3,000 CGT exemption and £500 dividend allowance are likely to be exceeded — for a client whose stated objective was *"doesn't want to pay income tax on interest"* | High |
| F8.5 | No loss scenario is illustrated for the recommended investments. The −30% stress test is applied inside the cashflow, not shown as a monetary loss on the £159,812 being invested | Moderate |
| F8.6 | The Aviva fund's factsheet records that it *"makes significant use of derivative instruments for investment purposes"* and carries foreign exchange, emerging markets, fixed interest and derivative risk warnings; the CT KIID adds leverage, liquidity, credit and smaller companies risk. None reaches the report | Moderate |
| F8.7 | Features and benefits lost are stated as none, consistent with the switch report's *"No benefits were listed for the existing products"* and the policy research sheet's nil entries for GARs, GMP, guaranteed growth rates, exit penalties, waiver and life cover | No issue |
| F8.8 | Cancellation rights, market movement while transactions are pending, and the 10-15 working day access delay are all disclosed | No issue |

### CHK-009 — Replacement or Switch Justification → Potential Concern

Applies: a 100% fund switch of the whole £79,092 pension, a change of servicing agency, and a
£20,000 Cash ISA to Stocks & Shares ISA transfer.

| ID | Finding | Severity |
| --- | --- | --- |
| F9.1 | The switch **reduces projected maturity value before advice charges** (£86,800 → £86,700) and the adviser's own ranking places taking no action above the recommendation; neither is disclosed (F6.1, F8.1) | Highest |
| F9.2 | The post-advice-charge comparison that *is* disclosed is invalid, because a 1% adviser charge the client does not currently pay has been applied to the existing plan (F7.1) | Highest |
| F9.3 | **"Lower Costs"** leads the switch rationale where total charges rise by 0.08% / £63 a year before the new 1% adviser charge (F6.2) | Highest |
| F9.4 | The ISA transfer moves £20,000 out of a 4.5% cash account into a fund the adviser's own illustration shows returning 3.4% after charges, justified on a gross comparison (F6.3) | Highest |
| F9.5 | The switching analysis models the wrong existing product — a non-platform *"Current Aviva"* personal pension rather than the Aviva Platform Pension Portfolio the client actually holds (F6.4) | High |
| F9.6 | The existing arrangement is described inaccurately in the two respects that bear directly on the switch decision — UFPLS availability and the size of the fund range (F1.5, F1.6) — so the feature comparison supporting the switch cannot be relied on | High |
| F9.7 | The report gives no reason for preferring the recommended solution over the third-ranked *Aviva Pension Portfolio - Core*, which projects the same £86,700 at a lower stated cost position in the same table | Moderate |
| F9.8 | Transfer value equals fund value, there are no exit penalties, no MVA, and no safeguarded benefits — all correctly stated and evidenced by the Aviva plan information form and the policy research sheet | No issue |

### CHK-010 — Vulnerability, Support Needs and Foreseeable Harm → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F10.1 | The report contains **no vulnerability assessment of any kind**, in a file recording several FG21/1 indicators: the client's own answers of *"very little understanding / knowledge"* of investing and *"no understanding / knowledge"* of unit trusts, GIAs and shares (financial capability); a **recent bereavement** — the £100,000 being invested is his late father's estate — and a **recent retirement** after 35 years of shift work, followed by a failed return to work (life events) | Highest |
| F10.2 | The only Attitude to Risk note recorded anywhere in the file is *"**Doesn't trust anyone.** Has worked long and hard for [it]"*, carried from the Fact Find into the risk profile report. It is never reflected, tested or addressed in the suitability report | High |
| F10.3 | The report asserts *"You confirm you have a **registered** Power of Attorney covering your financial affairs and health and welfare"*. The Fact Find records only *"Power Of Attorney Granted? Yes"* with no attorney named and no registration evidence. A registered health-and-welfare POA for a healthy 55-year-old is unusual and is neither corroborated nor explored | Moderate |
| F10.4 | All contact was by recorded telephone with *"Anybody else present at the Meeting? **No**"*, and the ATR questionnaire was completed by the client alone at home (*"Client did not have time to complete an ATRQ today, so have sent him an email version to complete himself on Defaqto"*). No third-party support or trusted contact was considered despite the recorded knowledge gap | Moderate |
| F10.5 | Yvette's income, retirement date, Cash ISA and life expectancy are all relied on in the plan, and 50% of the DB pension is modelled as continuing to her, yet she was never fact-found, never risk-profiled and is not a party to the advice | Moderate |
| F10.6 | The Fact Find records *"Accessible format requirement: No Requirement"* and *"Preferred delivery method: No Preference"*; neither is confirmed in the report, and no measures to support understanding are recorded anywhere in the file | Low |
| F10.7 | The Client Agreement and the Important Information document both offer reasonable adjustments on request | No issue |

---

## Scoring set

The **47 material findings** (Highest and High) are the benchmark for recall:

```
F1.1  F1.2  F1.3  F1.4  F1.5  F1.7
F2.1  F2.2  F2.3  F2.4  F2.5
F3.1  F3.2  F3.4
F4.1  F4.2  F4.3
F5.1  F5.2  F5.3  F5.4  F5.5  F5.6
F6.1  F6.2  F6.3  F6.4  F6.5  F6.6
F7.1  F7.2  F7.3  F7.4  F7.5  F7.6
F8.1  F8.2  F8.3  F8.4
F9.1  F9.2  F9.3  F9.4  F9.5  F9.6
F10.1 F10.2
```

A finding counts as **caught** when a run states the substance of the contradiction — naming both
sides and what conflicts. It counts as **partial** when a run touches the area but misidentifies the
conflict, states it without the contradiction, or buries it among unrelated points. Anything else is
**missed**.

**Missed findings are the expensive error.** A missed concern leaves no trace in the output; a
spurious one is visible and can be discarded by a reviewer. Any gate on this dataset should be set
on missed-rate, not on overall agreement.

Note that several findings are **cross-check duplicates by design**, because the checks overlap:
F6.1/F8.1/F9.1 are the same suppressed ranking seen from three angles, F6.2/F9.3 the same "Lower
Costs" claim, F6.3/F9.4 the same ISA comparison, and F7.1/F9.2 the same rigged adviser-charge
assumption. A run should be credited on each check where it states the point, but recall on this
case is not 47 independent trials.

---

## Known limits of this benchmark

- **One case, one adjudicator.** These are readings of the documents. The Highest and High findings
  are contradictions between documents and should survive review; the Moderate and Low ones involve
  more judgement.
- **Every check lands on Potential Concern**, so this dataset cannot measure false positives at
  check level.
- **Two documents could not be read.** The email in folder C (*RE_ Quick Questions for Advice Case*)
  has no Markdown conversion, and several charts (the volatility scatter, the cashflow bar charts,
  the Aviva fund's 1–7 risk-rating graphic) exist only as images. Findings that would depend on them
  — chiefly the exact Aviva risk-rating number behind F3.6 — have been stated conservatively.
- **Document conversion is a confound.** Everything here was read from the Markdown conversions,
  which flatten table structure in places. Where a figure was ambiguous it was checked against a
  second document before being recorded as a finding.
