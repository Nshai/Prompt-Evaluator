# Expected results benchmark — Test Case 3 (Anness)

A hand adjudication of the ten checks against the case file, independent of any pipeline run.
This is the answer key: what a competent human QA reviewer should conclude, and the specific
findings they should reach it by.

**How it was produced.** The suitability report
([Suitability report 2026.pdf](../../QA-Checks/Test-Cases/Test%20Case%203/I/Suitability%20report%202026.pdf))
was read in full, then every supporting document the checks depend on — the Fact Find, all four
meeting summaries, the certified Pension Sharing Annex and Final Order, the risk profile report and
its three questionnaire appendices, the Defaqto existing-holdings analyses for Nest (both versions)
and True Potential, the seven True Potential fund factsheets and the Nest fund document, all four
SelectaPension switching analyses, the FE performance and scatter exhibits and the risk-7 fund
research, the adviser's weighted-charge worksheet, the Aviva charges document, illustration, key
features and terms, the Vanguard and HSBC factsheets and KIIDs, the ONS life-expectancy screenshot
and the Client Agreement. Each check in
[Revised checks (loadable).csv](../../QA-Checks/Revised%20checks%20(loadable).csv) was then applied
on its own terms. No run output was consulted while adjudicating.

> **Conversion note.** The Markdown conversion of `Risk profile report 16 03 2026 11 27.docx`
> dropped every table in the document, including the risk levels and all three questionnaires. The
> findings below use the text recovered directly from the source `.docx`. A pipeline working only
> from the converted Markdown cannot reach F3.3, F3.4 or F4.1 and should not be penalised for them
> on a converted-evidence run — but the conversion gap is itself worth reporting.

**Read the finding list, not the outcome column.** Every check lands on Potential Concern, so
check-level agreement is worth almost nothing as a measure. The discriminating measure is which of
the 44 material findings below a run actually reaches.

---

## The case in one paragraph

Melanie Anness, 55, an accountant, divorced (final order 12 November 2025). She lives with her
partner Tony, who contributes £600 a month; they keep separate finances. She owns her home
(£430,000) with a £70,000 mortgage and holds £17,000 in savings. A pension sharing order gives her
**33.4% of her ex-husband's Quilter CEV**, estimated at £260,000; she also holds a True Potential
personal pension of £46,672.80 and a Nest occupational pension of £21,272.64. Her agreed risk level
is 7 (Growth) on a 1–10 scale. She wants to retire at 60 on £25,000–£30,000 a year until the State
Pension at 67, with flexible rather than guaranteed income. The adviser recommends a new Aviva
Platform pension to receive the pension credit, with the True Potential and Nest plans transferred
in — £327,945.44 in total — invested 50/50 in Vanguard LifeStrategy 80% Equity and HSBC Global
Strategy Dynamic. Fees are 3% initial (£9,838.36, taken from the pension) and 1% ongoing.

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
| CHK-010 | Vulnerability, Support Needs and Foreseeable Harm | **Potential Concern** | Certain |

No check is N/A.

---

## The five defects that matter most

### A. The court order records the opposite preference to the one the report relies on

The certified Pension Sharing Annex, section F — *"In cases where the Transferee has a choice of an
internal or external transfer, if the Transferee has indicated a preference, indicate what this
is"* — is ticked:

> **[x] Internal transfer**

The suitability report recommends an **external** transfer of the pension credit to Aviva, and
justifies it in part with:

> You mentioned during our meeting that you had no ties or preference to leave the funds with
> Quilter.

The report neither cites nor addresses the recorded preference. The 18 March 2026 meeting summary
also shows the client had not yet located the annexe, and that the recommendation meeting was
scheduled for **30 March** — six days after the report is dated.

### B. Four switching analyses were run; the body of the report quotes the two friendliest numbers

All four SelectaPension runs are in the case file, all dated 24/03/2026, all covering only True
Potential and Nest:

| Run | Combined effect on the fund | Critical yield uplift |
| --- | --- | --- |
| All charges, **including** the existing TP advice charge | **−12.42%** | +0.43% |
| All charges, **excluding** the existing TP advice charge | **−20.75%** | +0.76% |
| Initial adviser charge only | +8.97% | −0.27% |
| No charges at all | +12.34% | −0.38% |

The report's Appendix reproduces the first of these (Existing Schemes £121,807 against Aviva
£106,000 at 2.94%, −12.42%, reduction in yield 1.5%). The body of the report says instead:

> your proposed plan **can underperform** your current plan by 0.01% per annum to age 89
> …
> By switching to this scheme, you will benefit from **overall lower costs**…

The "0.01%" is the True Potential *sub-table* of the inc-advice-charge run. The combined −12.42%
never appears outside the appendix.

### C. The switching analysis excludes 79% of the money and there is no Quilter evidence at all

Every SelectaPension run covers £46,672.80 + £21,272.64 = **£67,945.44**. The £260,000 pension
credit is not in any of them. Nor is Quilter in the Pension Switch Charges table. The only Quilter
figure anywhere in the case is the report's own assertion that *"If you were to retain the pension
with Quilter the platform charges would be 0.27%"* — there is no Quilter illustration, plan
information form, charges schedule, fund list or projection in the file, and the report's own
Existing Arrangements table records **Investment Funds: N/A** and **Risk Level: N/A** for it.

### D. The inheritance is more than doubled, and everything rests on it

| Source | Amount | Timing |
| --- | --- | --- |
| Fact-find meeting, 20/02/2026 | *"at least **£150,000**"*, a quarter share of a £1.4m property | *"timeline remains uncertain due to ongoing legal matters"* |
| Meeting, 21/07/2026 | *"approximately **£150,000**"*, and she and her sister *"may need to contribute £20,000 each"* | contested; ex-partner still in the property |
| Fact Find asset record | *"Potential Inheritance **£150,000**"* | *"Estimated to be in 2-3 years time"* |
| **Suitability report** | *"an inheritance of **£350,000**"* | *"next year"* (cashflow inputs) / *"two years' time"* (chart commentary) |

The capacity-for-loss conclusion (*"your pension funds along with the inheritance will be sufficient
to cover expenditure in retirement, even following a fall in the market"*) and the cashflow's
adequacy conclusion both depend on it.

### E. The report states that the client runs out of money at 81 and draws no conclusion from it

> I also conducted a stress test based on a high inflation rate of 6%. The cashflow shows that
> should this high level of inflation continue at the assumed rate, **your assets could be depleted
> at age 81**.

The next sentence quotes the ONS life expectancy for a 55-year-old female as **87**. Nothing in the
report reconciles the two, changes the recommendation, or revisits the "moderate" capacity for
loss. No cashflow model output for any scenario exists in the case file.

---

## Per-check expected findings

Severity uses the catalogue's own scale. **Material** findings (Highest/High) are the scored set.

### CHK-001 — Suitability completeness against client information → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F1.1 | Report: *"expecting to receive an inheritance of **£350,000**"*. The fact-find meeting records *"at least **£150,000**"* as a quarter share of a £1.4m property; the Fact Find asset record and the July meeting both record £150,000 | Highest |
| F1.2 | The report gives two different timings for that inheritance in the same section — cashflow inputs say *"You receive an inheritance of £350,000 **next year**"*, the chart commentary says *"the inheritance which we assumed you receive in **two years' time**"* — while the evidence records *"2-3 years time, maybe sooner"* and an unresolved legal dispute with the occupant of the property | Highest |
| F1.3 | Report: *"You have recently altered your employment status, going from **employed to self-employed** with the same company. You expect annual earnings to be around **£45,000** gross"*. The contemporaneous fact-find meeting (20/02/2026) records her as an employed accountant at **Bridgeport Interiors Limited on £42,000**, take-home ≈£2,600/month, with no mention of self-employment | High |
| F1.4 | The Assets & Liabilities table shows only the £430,000 house, £17,000 cash and the £70,000 mortgage, giving a Net Asset Value of £377,000. **The £327,945 of pension assets the report is advising on are omitted**, and two paragraphs later the report relies on that total to conclude *"your assets are below your allowance of £500,000"* — having just explained that from 6 April 2027 unused defined contribution pensions fall into the estate | High |
| F1.5 | The mortgage is modelled at **£750 per month for the next 7 years**. The fact-find meeting records a contractual payment of **£476** with an overpayment stated as **£250** in one section and **£750** in another of the same document; the Fact Find records **£600** and a term to **age 65** (10 years). £750 × 84 months does not repay £70,000 with interest | High |
| F1.6 | The known mortgage rate reset is not mentioned anywhere: *"your current mortgage rate is a favourable 1.24%, but you anticipate an increase to around 3.8% or 3.9% when your current rate ends in June"* | High |
| F1.7 | The True Potential Existing Arrangements table lists **six funds totalling £41,055.26** against a stated fund value of **£46,672.80**. The seventh holding — **True Potential Allianz Balanced, £5,617.54 (12.01%)** — appears in the Defaqto holdings analysis and in the adviser's own weighted-charge worksheet but is omitted from the report | High |
| F1.8 | The Quilter row states a **Fund Value of "c. £260,000"**. The certified Pension Sharing Annex specifies a **percentage — 33.4% of the member's CEV** — not an amount; the fact-find meeting records *"£270,000 or more"*; the March meeting records the funds as already transferred and *"currently held in cash"*. A £7,800 initial fee is calculated on the £260,000 figure | High |
| F1.9 | **The only Fact Find in the case file is dated 21/07/2026 — four months after the report** — and documents a different advice event (a £40,000 tax-free cash withdrawal and £1,900/month income). There is no contemporaneous, signed fact find behind the 24/03/2026 advice; the contemporaneous record is a set of AI-generated meeting summaries | High |
| F1.10 | The report is dated **24 March 2026**, but the meeting summary of 18/03/2026 records that the presentation of recommendations was arranged for **Monday 30 March** and that the client had *"[not yet] located the pension sharing annexe document"*. Statements attributed to what the client *"mentioned during our meeting"* cannot rest on a meeting that had not happened | High |
| F1.11 | The IHT narrative is written for a married client: *"We each have a Nil Rate Band… Any unused NRB can be transferred to a **surviving spouse or civil partner**, meaning up to £1 million can be passed on tax-free **on second death**"*. She is divorced with no spouse; her allowance is the £500,000 the report itself states later | Moderate |
| F1.12 | The Power of attorney section recommends putting a POA in place *"to ensure that in the event of your incapacity, **your spouse** or relative could act on your behalf"*, for a client whose divorce is the reason for the advice and who the file records is already appointing her two daughters through a solicitor | Moderate |
| F1.13 | The Feb 2026 meeting records that the client is a **type 2 diabetic** who has lost three stones and reduced her alcohol intake. The report records nothing about her health | Moderate |
| F1.14 | *"You expect to receive the Full State Pension of **£230.21 per week** from age 67"* — £230.21 is the **2024/25** full new State Pension rate, in a report dated March 2026. The only evidence is a client-supplied screenshot; the Fact Find records only *"expecting full state pension"* with no BR19 | Moderate |
| F1.15 | The certified Final Order records the divorce as final on **12 November 2025** (conditional order 12 June 2024); the Fact Find records *"Divorced — Since 01/11/2024"* | Low |
| F1.16 | Four different adviser names appear across the file for one case: **James Poka** (suitability report, Feb/Mar meetings), **Morgan Seddon** (Fact Find, July meeting), **Jonathan Gill** (SelectaPension analyses) and the report signed as *Head of Advice* | Low |
| F1.17 | House value £430,000, mortgage £70,000, cash £17,000, partner's £600 monthly contribution, two adult daughters, and the True Potential and Nest fund values all reconcile with the evidence | No issue |

### CHK-002 — Objectives, Needs and Time-Horizon Alignment → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F2.1 | **Six inconsistent time horizons.** Retirement at 60 (5 years) per the objectives; the risk profile projections were run over **10 years on £300,000**; the Capacity for Loss questionnaire records an investment horizon of **15+ years**; the 18/03/2026 meeting records *"your investment horizon is **five to ten years**"*; the switching analysis runs to **age 87**; the report describes that analysis as running *"to age **89**"*; the cashflow runs to age 100. The report states no investment term at all | Highest |
| F2.2 | **The switching analysis models a plan the client has said she will not follow.** All four SelectaPension runs project both the existing and the new arrangements **"Paid Up" to age 87** — i.e. no withdrawals for 32 years — while the stated objective is to draw £25,000–£30,000 a year from age 60 | Highest |
| F2.3 | The retirement income target does not reconcile across the report: the objective is *"£25,000 to £30,000"* a year; the cashflow models *"In retirement, your personal expenditure is **£2,000 net each month**"* (£24,000, below the range); and the report states current expenditure of **£2,986 a month** (£35,832). Nothing bridges the three | High |
| F2.4 | The client's primary objective is a flexible retirement income from 60, and the fact-find meeting records a detailed discussion of drawdown, tax-free cash phasing and personal-allowance use. **The report makes no recommendation on how that income will be produced** — no drawdown strategy, no phasing, no tax-free cash plan. It recommends only a wrapper and two funds | High |
| F2.5 | The report asserts *"your Nest plan will become paid up"*, which underpins the Nest transfer. The fact-find meeting records the opposite constraint: *"you are **unable to transfer your Nest pension while your employer continues to contribute to it**"*. The assertion depends entirely on the unevidenced move to self-employment (F1.3) | High |
| F2.6 | The consolidation objective, the intention to work part-time after 60, the preference for flexible over guaranteed income, and the State Pension at 67 are all recorded in the evidence and correctly reflected | No issue |
| F2.7 | No priority order is given for the four objectives, which the check asks for explicitly | Low |

### CHK-003 — Risk, Capacity for Loss and Investment Strategy Match → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F3.1 | Capacity for loss is assessed as **moderate** on the basis that *"your pension funds along with the inheritance will be sufficient to cover expenditure in retirement, **even following a fall in the market**"*. The inheritance is overstated by £200,000, contested in court and of uncertain timing (F1.1, F1.2) | Highest |
| F3.2 | The report's own stress test states that *"your assets could be **depleted at age 81**"* under 6% inflation, six years before the life expectancy of 87 it quotes immediately afterwards. The capacity-for-loss conclusion is not revisited | Highest |
| F3.3 | Two adverse Capacity for Loss questionnaire answers are not reflected anywhere: *"I have or expect to incur **significant outstanding debts** during the period of my investment: **Strongly agree**"* and *"My spouse or partner (or another family member) is likely to be able and willing to support me financially if circumstances require: **Disagree**"* | High |
| F3.4 | The risk profile report records **"No additional notes were recorded"** against every discussion heading — Attitude to Risk, Investor Experience, Capacity for loss questionnaire, Investment target, Agreed risk level, Capacity for loss for the goal and Other notes. There is no contemporaneous record of the discussion the report describes (*"this was discussed and agreed"*, *"Based on our conversations and analysis"*) | High |
| F3.5 | **Two conflicting Defaqto risk analyses of the Nest plan are on file.** The first records the NEST Higher Risk Fund at a 10-year volatility of **17.68, mapping to risk level 9 (Very Adventurous)**. A second file, named *"nest defaqto for Existing Holdings **ammended**"*, records **13.21 → risk level 8**, but for a different, manually entered seven-fund look-through portfolio rather than the NEST Higher Risk Fund. The report states **8** and explains neither the amendment nor the discrepancy | High |
| F3.6 | The risk-level projections that produced the agreed risk level were run on an **Investment of £300,000 over a 10-year Investment Period with no withdrawals**; the actual recommendation is £327,945 with income drawn from year 5 | Moderate |
| F3.7 | The report states no asset allocation for the recommendation. The fact-find meeting records *"At a risk level of seven, approximately **80% of your investments would be allocated to growth assets**… The remaining 20% would be invested in defensive assets"* — which appears nowhere in the report | Moderate |
| F3.8 | The report claims the recommended funds *"reflect your risk profile both individually and combined. This means that should there be any portfolio drift, the risk being taken remains the same"* and describes them as *"risk-profiled solutions… specifically designed to remain within your agreed level of risk"*. Vanguard LifeStrategy 80% Equity is a fixed-allocation index fund and HSBC Global Strategy Dynamic is discretionary; no document on file rates either as risk-controlled or risk-targeted | Moderate |
| F3.9 | Natural risk level 7 and agreed risk level 7 match, with no override to explain; the Growth narrative is reproduced accurately; the *"adventurous"* self-description matches the questionnaire answer *"I would describe my investment approach as: Adventurous"* | No issue |
| F3.10 | The True Potential portfolio's risk level of 6 is correctly stated and properly evidenced (10-year volatility 11.30 → 6 Balanced Growth) | No issue |

### CHK-004 — Knowledge, Experience and Product Complexity → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F4.1 | The report opens its Knowledge & Experience section with *"Your background in accounting and currency trading has equipped you with a **solid understanding of financial matters**"*. The client's own Investor Experience questionnaire (16/01/2026) records: *"When it comes to your experience and understanding of investing, how would you describe yourself? — **Very little understanding / knowledge**"*. The report's sentence is lifted verbatim from the adviser-written Profile Notes, not from the client's answers | Highest |
| F4.2 | The report drops the annuity option on stated preference alone. The fact-find meeting records it as under active discussion *and* expressly records that her health *"is an important consideration in your financial planning, **particularly concerning products like annuities that may offer enhanced rates based on health factors**"*. A client recorded as a type 2 diabetic is never told an enhanced annuity might be available | High |
| F4.3 | No evidence anywhere in the file that the client's comprehension of the recommendation was tested or supported; the risk profile report records **"No additional notes were recorded"** against Investor Experience | High |
| F4.4 | The recommendation involves receiving a **pension credit from a pension sharing order** — an area with its own rules on disqualifying pension credits, tax-free cash entitlement and the lump sum allowance — plus a plan to draw flexible income from 55/60. None of this is explained to the client beyond generic appendices | High |
| F4.5 | The report omits the questionnaire answers that qualify the claimed experience: *"Have you invested in **stocks and shares** within the last 5 years? **No**"*, *"…**Unit Trusts and General Investment Accounts**…? **No**"*, and the meeting note *"you do not currently maintain a stocks and shares portfolio"*. The currency-trading experience relied on is historic | Moderate |
| F4.6 | The statements that she has *"invested in pensions without taking any advice within the last 5 years"*, *"regularly check the value of your funds"* and has *"a fair degree of knowledge and understanding in relation to pension planning"* all match the questionnaire | No issue |

### CHK-005 — Financial Resilience, Affordability and Liquidity → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F5.1 | The cashflow conclusion — *"your assets are sufficient to make up your income shortfall throughout your lifetime"* — rests on a £350,000 inheritance the evidence values at £150,000 and describes as contested (F1.1) | Highest |
| F5.2 | The report discloses that under its own 6% inflation stress test *"your assets could be **depleted at age 81**"* and then draws no conclusion, offers no mitigation and changes nothing | Highest |
| F5.3 | **No cashflow model output exists anywhere in the case file.** The report describes charts, an input list, a 6% inflation scenario and a 2007/8-style market crash scenario; none of the underlying documents is present, so none of the modelling can be verified | High |
| F5.4 | The three expenditure and income figures do not reconcile: £2,986 a month stated as current expenditure, £2,237 a month plus a £750 mortgage payment in the cashflow inputs, and £2,000 a month modelled in retirement against a £25,000–£30,000 objective (F2.3) | High |
| F5.5 | The mortgage rate reset from 1.24% to ~3.9% in June 2026, recorded in the fact-find meeting, is absent from the report and from the cashflow (F1.6) | High |
| F5.6 | The **£9,838.36 initial fee is taken from the pension fund** at outset, a 3% reduction in retirement provision. The report states the amount in the Our charges section but never shows its effect on the projections, and the cashflow models the pension growing at 6% *"before charges"* with no mention of the deduction | High |
| F5.7 | The cashflow assumes **6.00% per annum growth** on the Aviva pension — the most optimistic assumption in the file. The switching analysis uses a mid rate of 2.94% real and the Aviva illustration a 4.9% medium rate (2.8% after inflation). The adequacy conclusion is produced by the highest of the three | High |
| F5.8 | Emergency fund provision is never assessed. The client holds £17,000 of which £2,500 is already committed; the Fact Find records a required reserve of £24,000 against £7,000 held, a **£17,000 shortfall**. The report's only statement is *"You are comfortable with the concept as you have liquid cash should you require instance access funds"* | Moderate |
| F5.9 | Liquidity of the recommendation (10–15 working days to access), the partner's £600 contribution, the £2,500 sofa purchase and the State Pension from 67 are correctly stated and evidenced | No issue |

### CHK-006 — Recommendation Rationale and Suitable Alternatives → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F6.1 | The certified Pension Sharing Annex records, at section F, the client's stated preference as an **internal transfer**. The report recommends an external transfer to Aviva and asserts *"You mentioned during our meeting that you had no ties or preference to leave the funds with Quilter"*, without citing or addressing the recorded preference | Highest |
| F6.2 | **There is no Quilter evidence of any kind in the case file** — no illustration, plan information form, charges schedule, fund list, feature comparison or projection. The single figure used, *"the platform charges would be 0.27%"*, is unsupported, and the report's own table records **Investment Funds: N/A** and **Risk Level: N/A**. £260,000 (79% of the money) is moved on that basis | Highest |
| F6.3 | **The switching analysis excludes the £260,000 pension credit entirely.** All four SelectaPension runs cover only True Potential and Nest (£67,945.44), and Quilter is absent from the Pension Switch Charges table. The appendix is nonetheless presented as the critical yield for the recommendation | Highest |
| F6.4 | **Four switching analyses were run with materially different answers and the body reports the two most favourable.** Combined effect on the fund: −12.42% (all charges inc. existing OAC), **−20.75%** (all charges ex. existing OAC), +8.97% (initial charge only), +12.34% (no charges). The body reports *"can underperform… by 0.01%"* (the True Potential sub-table of the inc-OAC run) and *"will need to outperform… by 1.23%"* (Nest), and concludes *"overall lower costs"* | Highest |
| F6.5 | *"Had we recommended you retained the pension funds within Quilter, **the initial advice charges would have been incurred regardless** of the chosen provider"* is offered as a reason to move. It is a statement that the adviser's fee is unavoidable, not a client benefit, and it does not follow from the Client Agreement, which charges 3% *"of the value of the funds that we advise on"* | High |
| F6.6 | **Five options are listed and none is discussed.** Option 1 (leave untouched) is the one the adviser's own analysis says is better by 12.42%; Option 2 (leave in place and switch funds) is live for True Potential and the report acknowledges it in passing (*"Whilst we could look at alternative funds within this plan"*) before dismissing it without analysis | High |
| F6.7 | The annuity option is dropped on stated preference alone, in a case where the client had it under active consideration and where the adviser's own meeting note flagged that her health could secure an enhanced rate (F4.2) | High |
| F6.8 | No fund selection rationale beyond generic CIP text. The FE risk-7 research compares the two recommended funds against one Aviva alternative and the sector benchmark, and the Aviva fund's five-year return (46.39%) sits between them — no reason is given for the 50/50 split or for preferring these two | Moderate |
| F6.9 | The stakeholder comparison is boilerplate and, unlike the other charge comparisons, is supported by no stakeholder projection anywhere in the case file | Moderate |
| F6.10 | The Nest switch rationale — 6 funds only, no servicing adviser permitted, no flexi-access drawdown, member-managed auto-enrolment scheme — is accurate and evidenced | No issue |
| F6.11 | The reason the client sought independent advice (*"your previous financial adviser at True Potential was unresponsive"*) is recorded in the evidence and is a legitimate non-financial ground for the True Potential switch, but the report does not use it | No issue |

### CHK-007 — Costs, Charges and Value of the Recommendation → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F7.1 | The combined **−12.42% reduction in projected fund value** and the **+0.43% critical yield uplift** appear only in the appendix, only for one of four variants, while the body states the opposite conclusion (F6.4) | Highest |
| F7.2 | **Every cost comparison in the report omits the new 1% ongoing adviser charge.** "Like for Like" compares True Potential's 0.98% with Aviva's 0.39% and Nest's 0.30% with 0.39%. Including the new charge the client moves to **1.39%** on the whole £327,945 — from 1.48% (True Potential, including its existing 0.50% advice charge), from **0.30%** (Nest) and from **0.27%** (Quilter). That is roughly **£3,181 a year**, a figure the report states in a different section and never brings into any comparison | Highest |
| F7.3 | The report gives **two different Aviva platform charges**: 0.19% in the Quilter rationale and in the Annual recurring charges table, and **0.21%** in the True Potential rationale — where the stated £88.68 saving is in fact computed on 0.19% (0.40% − 0.21% = 0.19%) | High |
| F7.4 | The report states the critical yield comparison runs *"to age **89**"*. All four SelectaPension analyses run to **age 87** | High |
| F7.5 | **No evidence supports the Quilter 0.27% platform charge, the True Potential 0.40% platform charge or the True Potential 0.50% ongoing advice charge.** Only the True Potential weighted fund charge is evidenced — the adviser's worksheet totals £272.72 on £46,672.80 = 0.584%, matching the stated 0.58% | High |
| F7.6 | **The cumulative effect of charges is never disclosed.** The Aviva illustration shows all charges reducing the fund at age 60 from **£372,000 to £337,000** — growth after inflation cut from 2.8% to 0.6%, a reduction in yield of **2.2%** — against a report that presents the total charge as 0.39% and says *"The charges are competitive"* | High |
| F7.7 | The **3% initial fee is charged on a pension credit whose amount is not yet known** (33.4% of a CEV). The report's only qualification is *"this figure may fluctuate depending on transfer values"* | Moderate |
| F7.8 | Transaction costs are excluded from the 0.20% investment management figure and from every comparison, and no research document in the file quantifies them for either recommended fund | Moderate |
| F7.9 | The Client Agreement records the initial fee as **£9,839.36**; the report and the Aviva charges document give **£9,838.36** | Low |
| F7.10 | The ongoing fee of *"circa £3,181.08 for the first year"* reconciles exactly (1% of £318,107.08 after the initial fee), and the fund charges of 0.19% and 0.20% match the Aviva charges document and illustration | No issue |
| F7.11 | The Nest charge of 0.30% is corroborated by the Defaqto weighted AMC of 0.3% | No issue |

### CHK-008 — Disadvantages, Risks and Customer Understanding → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F8.1 | The −12.42% projected reduction never appears in the body, which concludes *"you will benefit from overall lower costs, greater investment flexibility, improved retirement options, and continued professional oversight"*. A prominence failure under COBS 4.2.1R | Highest |
| F8.2 | The report states that under its own stress test *"your assets could be depleted at age 81"* and, two sentences later, that *"your funds would be expected to last throughout your lifetime"*. The two statements are left standing side by side | Highest |
| F8.3 | **The client is being switched out of the best-performing plan she holds.** The report's own table shows Nest at 48.06% over five years against 45.91% for the recommended blend (Defaqto gives the Nest fund 59.28%). The report explains this by risk but never quantifies the expected cost of derisking, while separately offering *"The funds have performed well in comparison with their peers"* as a rationale | High |
| F8.4 | **The tax consequences for this client are never worked.** The fact-find meeting expressly records that *"taking taxable income from her pension would limit future contributions due to the money purchase annual allowance"*; the report's appendix mentions the MPAA only generically and asserts *"You have not yet triggered the MPAA"*, with no analysis of what drawing income from 60 would do to that, to her personal allowance, or to her ability to contribute from part-time earnings | High |
| F8.5 | No loss scenario is illustrated for the recommended portfolio. The market-crash stress test is described in a single sentence with no figures, and no supporting document for it exists in the file | Moderate |
| F8.6 | The 6% inflation and market-crash scenarios are described but the ONS screenshot's own longevity data — *"95 years: 1 in 4 chance"*, *"99 years: 1 in 10 chance"* — is not used, and the report's adequacy conclusion rests on average life expectancy alone | Moderate |
| F8.7 | *"Does not contain any safeguarded benefits"* is stated for all three arrangements. For Nest and True Potential this is at least a stated position; for Quilter no plan information document of any kind exists to support it | Moderate |
| F8.8 | Market movement while transactions are pending, the 10–15 working day access period, the absence of any guarantee of outperformance, cancellation rights, and the previous provider's freedom to refuse the plan back are all disclosed | No issue |

### CHK-009 — Replacement or Switch Justification → Potential Concern

Applies: three arrangements are being replaced or transferred — a £260,000 pension credit directed
away from Quilter, the £46,672.80 True Potential plan and the £21,272.64 Nest plan.

| ID | Finding | Severity |
| --- | --- | --- |
| F9.1 | The certified court order records the client's preference as an **internal transfer**; the recommendation is external, and the report asserts the opposite preference (F6.1) | Highest |
| F9.2 | **No comparison of any kind exists for 79% of the money.** Quilter appears in neither the switching analysis nor the Pension Switch Charges table, and no Quilter charge, fund, feature or projection evidence is on file (F6.2, F6.3) | Highest |
| F9.3 | Four switching analyses with conflicting answers; the two most favourable per-plan figures are in the body and the unfavourable combined result is confined to the appendix (F6.4) | Highest |
| F9.4 | The charges comparison driving the switch **excludes the new 1% ongoing adviser charge**, which is the largest single cost of the recommendation (F7.2) | Highest |
| F9.5 | The comparison projects both the existing and the new arrangements **paid up to age 87**, although the client intends to draw £25,000–£30,000 a year from age 60 (F2.2) | High |
| F9.6 | The Nest transfer rests on the assertion that the plan *"will become paid up"*, which the fact-find meeting contradicts and which depends on an unevidenced change of employment status (F2.5, F1.3) | High |
| F9.7 | The True Potential existing holdings are incompletely stated — six of seven funds, £41,055.26 against £46,672.80 (F1.7) | High |
| F9.8 | *"The receiving scheme provides more flexible retirement options"* is given as a switch reason. It is true for Nest (which the report itself records as offering no flexi-access drawdown) but the report's own table records Quilter as already offering *"Flexi-access drawdown, full and partial UFPLS"* | Moderate |
| F9.9 | The pension sharing charges apportioned to the client by the court order (**Transferee 33.4%**) are not mentioned anywhere in the report | Moderate |
| F9.10 | No exit penalties or transfer charges apply to True Potential or Nest, and this is correctly stated | No issue |

### CHK-010 — Vulnerability, Support Needs and Foreseeable Harm → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F10.1 | **The report contains no vulnerability assessment of any kind**, in a file recording several FG21/1 drivers at once: a **divorce finalised four months before the advice** (and the reason for it), the **death of her mother** — who *"suffered from Parkinson's and dementia"* — with the estate in contested litigation, and a change of employment status | Highest |
| F10.2 | The client is recorded in the fact-find meeting as a **type 2 diabetic**. The report says nothing about her health, and drops the annuity option that the same note said her health could improve (F4.2) | High |
| F10.3 | The client's own questionnaire records **"Very little understanding / knowledge"** of investing — a financial-capability indicator — and the report asserts the opposite (F4.1) | High |
| F10.4 | The report recommends setting up a Power of Attorney so that *"your **spouse** or relative could act on your behalf"*, for a divorced client whom the file records as already appointing her two daughters through a solicitor. The report's own opening paragraph states the daughters' role, then contradicts it | Moderate |
| F10.5 | All contact was by telephone and Teams with no third party present, and the report is dated six days before the meeting at which the recommendations were to be presented (F1.10). No communication needs, accessible-format requirements or reasonable adjustments are recorded or confirmed anywhere | Moderate |
| F10.6 | No trusted contact, no comprehension check and no measures to support informed decision-making are recorded, in a case turning on a court order the client had not yet located and a £327,945 irreversible transfer | Moderate |
| F10.7 | The Client Agreement and the Aviva charges document both offer alternative formats (large print, Braille, audio) on request | No issue |

---

## Scoring set

The **44 material findings** (Highest and High) are the benchmark for recall:

```
F1.1  F1.2  F1.3  F1.4  F1.5  F1.6  F1.7  F1.8  F1.9  F1.10
F2.1  F2.2  F2.3  F2.4  F2.5
F3.1  F3.2  F3.3  F3.4  F3.5
F4.1  F4.2  F4.3  F4.4
F5.1  F5.2  F5.3  F5.4  F5.5  F5.6  F5.7
F6.1  F6.2  F6.3  F6.4  F6.5  F6.6  F6.7
F7.1  F7.2  F7.3  F7.4  F7.5  F7.6
F8.1  F8.2  F8.3  F8.4
F9.1  F9.2  F9.3  F9.4  F9.5  F9.6  F9.7
F10.1 F10.2 F10.3
```

A finding counts as **caught** when a run states the substance of the contradiction — naming both
sides and what conflicts. It counts as **partial** when a run touches the area but misidentifies the
conflict, states it without the contradiction, or buries it among unrelated points. Anything else is
**missed**.

Several findings are **cross-check duplicates by design**: F6.1/F9.1 (the internal-transfer
preference), F6.2/F9.2 (the absent Quilter evidence), F6.3/F6.4/F7.1/F8.1/F9.3 (the four switching
analyses), F7.2/F9.4 (the omitted adviser charge), F3.1/F5.1 (the inheritance), F3.2/F5.2/F8.2 (the
depletion at 81), and F4.1/F10.3 (the knowledge contradiction). Credit each check where the point is
stated, but recall here is not 44 independent trials.

---

## Known limits of this benchmark

- **One case, one adjudicator.** The Highest and High findings are contradictions between documents
  and should survive review; the Moderate and Low ones involve more judgement.
- **The Fact Find post-dates the report by four months**, and captures a later advice event. Where a
  finding depends on it alone, that is flagged in the text; where the Feb/Mar 2026 meeting summaries
  corroborate the point, the finding is stated without qualification. Findings that would have
  depended solely on July 2026 facts (for example the £7,000 credit card and £13,000 personal loan)
  have deliberately **not** been recorded as omissions from the March report.
- **Conversion losses.** The risk profile report's tables were recovered from the source `.docx`
  because the Markdown conversion dropped them entirely (see the note at the top). Four `.eml` files
  in folders C and F contain only image attachments — the State Pension forecast screenshot in
  particular could not be read, so F1.14 is stated conservatively.
- **Image-only exhibits.** The cashflow charts, the FE scatter chart and the Nest/Vanguard/HSBC
  chart graphics exist only as images. Where the scatter's underlying numbers were recoverable from
  the structured export they were used; where they were not, no finding was recorded.
- **Every check lands on Potential Concern**, so this dataset cannot measure false positives at
  check level.
