# Expected results benchmark — Test Case 4 (A & K Stead)

A hand adjudication of the ten checks against the case file, independent of any pipeline run.
This is the answer key: what a competent human QA reviewer should conclude, and the specific
findings they should reach it by.

**How it was produced.** The suitability report
([Retirement Planning Suitability Report - A & K Stead.pdf](../../QA-Checks/Test-Cases/Test%20Case%204/I/Retirement%20Planning%20Suitability%20Report%20-%20A%20&%20K%20Stead.pdf))
was read in full, then every supporting document the checks depend on — the joint Fact Find, the
adviser's file note, all four risk profile reports and their questionnaire appendices, both Defaqto
pension switching reports, the Phoenix, Prudential and Standard Life plan information and policy
documents, the Aviva illustrations and charges data, the FE performance report and scatter chart,
all four intelliflo cashflow scenarios, the adviser's MATHS charge worksheets and the policy
research workbooks. Each check in
[Revised checks (loadable).csv](../../QA-Checks/Revised%20checks%20(loadable).csv) was then applied
on its own terms. No run output was consulted while adjudicating.

> **Conversion note.** Only folders B, C, D and E of this case have Markdown conversions, and the
> conversions of the risk profile reports lost every table (risk levels and all questionnaires). The
> suitability report itself, the switching reports, the illustrations, the FE exhibits and the
> cashflows exist only as PDF/Word originals. Everything below was read from the source files. A
> pipeline restricted to the converted evidence cannot reach most of these findings, and that gap is
> itself the first thing to report on this case.

**Read the finding list, not the outcome column.** Every check lands on Potential Concern. The
discriminating measure is which of the 55 material findings below a run actually reaches.

---

## The case in one paragraph

Angela Stead (63) and Kevin Stead (67), married, retired on 29 May 2026 after 32 years as
self-employed partners in an HGV haulage business, which is being wound up. They own their home
outright (£450,000), hold £266,000 in cash ISAs and a current account, a motor home (£40,000) and
£10,000 in the business account. Kevin's State Pension is in payment at £954.15 every four weeks
(~£1,034 a month); Angela's starts in October 2029. They hold four paid-up personal pensions —
Phoenix (Angela, unit-linked, transfer value £89,162), Prudential With-Profits (Angela £33,723;
Kevin £136,071) and Standard Life With-Profits (Kevin £25,488) — £284,444 in total. Agreed risk
levels are 4 (Angela) and 5 (Kevin). The adviser recommends transferring all four to Aviva Platform
personal pensions in two-fund multi-asset blends, and starting Angela on a phased UFPLS of £1,396.67
a month to use her Personal Allowance until October 2029. Fees are 3% initial (£8,533, taken from
the pensions) and 1% ongoing (~£2,759 a year).

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

### A. The provider's own illustration says Angela's pension runs out at 71

Page 1 of Angela's Aviva Key Features Illustration, summarising the recommended £1,396.67 monthly
UFPLS against her £122,886:

> ● In 5 years time, your plan might be worth **£44,700.00**
> ● In 10 years time, your plan might be worth **£0.00**
> ● **Your fund will run out when you are 71**

The suitability report says the strategy preserves "the majority of the pension capital for
longer-term growth", and its own Appendix projects Angela's fund at age 87 as £166,000.

### B. The Standard Life 4% guarantee is surrendered for £140 a year, with no analysis anywhere

Standard Life's plan information to the adviser:

> The plan is invested in with profits and **has a valuable guarantee which would be lost if your
> client transferred out. The guaranteed amount will grow by at least 4% a year before charges.**
> Given current economic conditions, **this is a great position for your client to be in. This type
> of guarantee is no longer available on the open market**…

The adviser's file note works through the Standard Life plan (MVR, bonus, 1% charge, no FAD, no
adviser charging) and never mentions the guarantee. The Defaqto switching report for Kevin states:

> Benefits — The following is a list of benefits that will be lost when switching:
> **No benefits were listed for the existing products**

The suitability report records the guarantee in its Existing Arrangements table and then disposes of
it in one line: *"the cost savings and enhanced flexibility justify this trade-off"*. The cost saving
is **£140 a year** (1% → 0.45% on £25,488).

### C. The appendix says the switch destroys 13–16% of the fund; the body says "significantly lower costs"

| Mid growth (2.94%), after all charges | Existing | New solution |
| --- | --- | --- |
| Angela — projected value at age 87 | £198,000 | £166,000 — **£32,000 or 16.2% decrease** |
| Angela — reduction in yield | 0.9% | **1.6%** |
| Kevin — projected value at age 85 | £227,000 | £197,000 — **£30,000 or 13.2% decrease** |
| Kevin — reduction in yield | 1.0% | **1.9%** |

Before adviser charges the same Defaqto analysis shows the switch **adding** £19,000 (+9.6%) for
Angela and £15,000 (+6.6%) for Kevin. The whole product-level benefit is consumed and reversed by
the 3% initial and 1% ongoing charges. The body of the report says: *"By switching to the Aviva
platform, you will benefit from significantly lower costs…"*

### D. The recommended Risk 4 portfolio underperformed the benchmark, with higher volatility

The report:

> Angela's Risk 4 strategy delivered 19.70% returns with 6.98% volatility… **Both track or
> outperform the benchmark as we would expect.**

The FE scatter chart it is quoting from:

| Key | Name | Performance | Volatility |
| --- | --- | --- | --- |
| A | Phoenix AL - International Pn Acc | 71.21 | 10.61 |
| B | Risk 5 - Vanguard 60% & CT Balanced | 34.60 | 7.98 |
| C | **IA Mixed Investment 20-60% Shares (benchmark)** | **22.33** | **6.63** |
| D | **Risk level 4 - Vanguard & HSBC** | **19.70** | **6.98** |

Angela's recommended strategy returns **less** than the benchmark with **more** volatility. And in
the worst year in the data, calendar 2022, the recommended Risk 4 strategy fell **−12.63%** against
the benchmark's −9.47% and the "excessively volatile" Phoenix fund's **−8.70%**.

### E. The estate already exceeds the nil rate bands, on the report's own numbers

> Currently, your Inheritance Tax liability will be £0. With combined assets of £766,000 plus your
> pension values, your total estate remains below the potential combined nil rate bands of
> £1,000,000.

£766,000 + £284,444 = **£1,050,444**. Eighteen pages later the same report states: *"Your total
assets are projected to grow from **£1,050,446** initially to **£1,253,121** by age 99."*

---

## Per-check expected findings

Severity uses the catalogue's own scale. **Material** findings (Highest/High) are the scored set.

### CHK-001 — Suitability completeness against client information → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F1.1 | The IHT conclusion contradicts the report's own arithmetic and its own cashflow: £766,000 + £284,444 = **£1,050,444**, and the Cashflow section states the initial total as **£1,050,446** rising to **£1,253,121**, against a stated allowance of £1,000,000 | Highest |
| F1.2 | Standard Life is shown as *"£24,472 (as of 31/10/2025)"*. The Standard Life plan information gives **£24,472.11 at 10 June 2026**; the Fact Find records **£21,511.71 at 31/10/2025**. The report pairs a June 2026 value with an October 2025 date, and neither source supports the pairing | High |
| F1.3 | Kevin's Prudential plan is shown at a fund value of **£124,774 (as of 20/05/2026)**; the Fact Find records **£135,000.00** at the same date | High |
| F1.4 | The report describes the business as *"in the process of being wound up, assets sold and invoices due to be paid by clients therefore there may be some income that will be attributed to you"*, but **the ~£45,000 wagon and trailer sale and the outstanding invoices appear in neither the asset schedule nor the cashflow**. The Fact Find records the trailer at £40,000, the wagon at £5,000 and £6,000 of tax due in July | High |
| F1.5 | The report states the clients are *"in good health"*. The Fact Find's Profile Notes record that **Angela had breast cancer five years ago**, successfully treated and currently clear | High |
| F1.6 | Current expenditure is stated as £2,967 (✓ Fact Find £2,966.50) and the required income as £3,500, with the **£533 difference never explained**. The Fact Find explains it — *"Expenditure is approx £2,966.50 (Not inc Savings… which would take it up to £3,966.50)"* — and the cashflow then models **£42,000 a year entirely as "essential"** expenditure | High |
| F1.7 | The Assets & Liabilities table shows **no liabilities at all** and a "NET Asset Value £766,000", while the Fact Find records **£6,000 of tax due in July 2026** plus further tax on the asset sale. The pensions are excluded from the table but relied on for the IHT conclusion | Moderate |
| F1.8 | Angela's State Pension is given as *"approximately £1,034 per month"* — Kevin's figure. The Fact Find records her **BR19 projection of £12,548** (£241.30 a week, £1,045.67 a month), which is what the cashflow uses | Moderate |
| F1.9 | The cashflow models **Kevin's** State Pension at **£12,548 from age 66** — Angela's BR19 figure — where his actual entitlement is £954.15 every four weeks (≈£12,404 a year) and he is 67 and already in payment | Moderate |
| F1.10 | Two expenditure lines drive 46% of the household budget and neither is questioned: **Building Insurance at £360 per month** (£4,320 a year) and an unexplained **"Other (Basic Quality of Living)" of £1,000 per month** | Moderate |
| F1.11 | The adviser's file note states the cash reserves as **£276,000**; the Fact Find and the report both give **£266,000** | Moderate |
| F1.12 | Both Aviva illustrations are dated **23 July 2026**, a week after the suitability report (16/07/2026), so the charges and projections the report tells the client to read pre-date the document containing them | Moderate |
| F1.13 | The ONS reference is to *"a **62-year-old** female"*; Angela is 63 (date of birth 16/10/1962) | Low |
| F1.14 | **No identity verification is recorded**: the Fact Find's ID Verification and Electronic ID Verification sections are entirely blank for both clients | Low |
| F1.15 | Ages, dates of birth, the three adult children and their independence, Wills and registered Powers of Attorney (each other and both daughters as attorneys), the mortgage-free £450,000 home, the motor home, the business account, the £266,000 cash total, Kevin's £954.15 four-weekly State Pension, the four transfer values summing to £284,444 and the paid-up-since-2008 status all reconcile with the evidence | No issue |

### CHK-002 — Objectives, Needs and Time-Horizon Alignment → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F2.1 | **The switching analysis that justifies the recommendation models the opposite of the recommendation.** Both Defaqto reports project the funds *"Paid Up"* to **age 87 (Angela, "Time to retirement 23 years and 2 months") and age 85 (Kevin)** — assuming no withdrawals — while the recommendation starts drawing £16,760 a year from Angela's fund immediately | Highest |
| F2.2 | **The risk profiling used Investment Periods of 38 years (Angela) and 33 years (Kevin)** — to ages 101 and 100 — while both Capacity for Loss questionnaires record an investment horizon of **3-9 years**, and the report states no investment term at all | Highest |
| F2.3 | Three income figures with no reconciliation: a £3,500 monthly objective, stated current expenditure of £2,967, and a cashflow modelling £42,000 a year as essential expenditure (F1.6) | High |
| F2.4 | The report gives no plan for the period **after** October 2029 beyond *"the strategy will be reviewed"*, although the adviser's own risk-profile note quantifies it: *"when Angela gets her SP they will need circa £1,500pm which is £18,000 net each year"* | Moderate |
| F2.5 | Both risk-profile goals assume an **"Annual Increase" of 2.5% (Angela) and 3% (Kevin) per year in addition to inflation** — increasing regular contributions — for clients making no contributions and taking withdrawals | Moderate |
| F2.6 | Angela's Aviva illustration records an *"Intended retirement date: **16 October 2037**"* (age 75) and an *"Intended annuity purchase age: **75**"*, against a report that starts income in August 2026 and runs its critical yield to 87 | Moderate |
| F2.7 | The five stated objectives — £3,500 net monthly income, using Angela's Personal Allowance to October 2029, reducing charges and aligning to risk profiles, simplifying administration with flexible drawdown, and preserving capital and legacy — are each recorded in the Fact Find's retirement objectives notes and the adviser's file note | No issue |

### CHK-003 — Risk, Capacity for Loss and Investment Strategy Match → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F3.1 | **Angela's own Attitude to Risk answers are materially more cautious than the report conveys.** She records *"**I have no understanding of investments or how they work**"*, *"If I thought there was a possibility of my investment losing money, I would: **Worry about the investment often**"*, *"When investing money to meet my objectives I would prefer to use: **Low risk investments**"*, and anxiety at a fall of *"**More than £1,000**"* on £20,000 — **5%**. The recommended Risk 4 portfolio fell **12.63%** in 2022 on the adviser's own data | Highest |
| F3.2 | **Capacity for loss is assessed as "medium for both of you" on the strength of cash the plan spends.** The stated basis is that the £266,000 *"provide an excellent buffer against market volatility"* and *"gives you the financial flexibility to accept some investment risk"* — while the adviser's own proposed cashflow shows **liquid assets reaching zero in 2047**, when Angela is 84 | Highest |
| F3.3 | The adviser's own risk-profile notes record two things the report omits: *"[Angela] would be concerned if any capital suffered losses of between **5 & 12.5%** and she would get worried due to her lack of experience"*, and *"**This does surprise me** as Angela thought she would be more comfortable with investment risk when we discussed it during the initial meeting"* | High |
| F3.4 | Both Capacity for Loss questionnaires record *"My spouse or partner (or another family member) is likely to be able and willing to support me financially if circumstances require: **Strongly disagree**"*, and on flexibility of retirement date Kevin records **"Strongly disagree"** and Angela **"Disagree"**. None of this is reflected in a capacity-for-loss assessment that rests on shared household resources | High |
| F3.5 | Both risk profile reports record **"No additional notes were recorded"** against *Investment target*, *Agreed risk level* and *Capacity for loss for the goal* | Moderate |
| F3.6 | The report claims the funds *"are risk-profiled solutions, meaning they are specifically designed to remain within your agreed level of risk"* and that *"should there be any portfolio drift, the risk being taken remains the same"*. Vanguard LifeStrategy is a fixed-allocation index fund, and the Defaqto switching reports show blank "Risk controlled" and Diamond rating fields for all four recommended funds | Moderate |
| F3.7 | The two Prudential with-profits funds and the Standard Life with-profits fund are rated **risk 5**, matching Kevin's agreed profile exactly. The only genuine risk mismatch in the case is Angela's Phoenix fund at risk 9, yet the closing rationale is applied to all four plans alike | Moderate |
| F3.8 | Natural and agreed risk levels match for both clients (4 and 5), with no override to explain; the Cautious Balanced and Balanced narratives are reproduced accurately from the risk profile reports; and the Defaqto switching reports independently confirm the agreed levels | No issue |

### CHK-004 — Knowledge, Experience and Product Complexity → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F4.1 | **Angela's Investor Experience questionnaire records "Do you understand how pension planning and income in retirement works…? — No understanding / knowledge"** and *"Have you invested in pension planning and income in retirement products within the last 5 years? — **No**"*. She is being placed into a phased UFPLS drawdown strategy calibrated to her Personal Allowance. Both clients record *"**Very little understanding / knowledge**"* overall | Highest |
| F4.2 | The report states *"Your existing personal pensions have been invested in **with-profits funds**, which you understand provide some investment growth potential with smoothing mechanisms"*. Angela's largest plan — the Phoenix policy, **£89,162 of her £122,886** — is **unit-linked**, as the report's own Existing Arrangements table states, invested in a single risk-9 international equity fund | High |
| F4.3 | The complexity actually being recommended — a phased UFPLS with a 25%/75% split sized to the Personal Allowance, a drawdown fund running alongside an uncrystallised fund, and an MPAA trigger — is never explained at a level the recorded knowledge would support | High |
| F4.4 | No comprehension check is recorded anywhere. The only evidence is the report's own assertion that *"I explained how multi-asset funds work… and you expressed comfort with this approach following my explanations"*, which nothing in the Fact Find or the risk profile reports corroborates | High |
| F4.5 | The Fact Find records *"**Both Angela and Kevin have no investment experience**"* and Angela's questionnaire records *"Do you check the valuation of your investment and retirement portfolio on an annual basis? — **No**"*. The report's *"limited experience with investment products"* understates both | Moderate |
| F4.6 | The statement that they have *"primarily used cash savings products and Cash ISAs over the years"* and prefer *"a straightforward, well-managed solution"* is well supported by both questionnaires and by the adviser's notes | No issue |

### CHK-005 — Financial Resilience, Affordability and Liquidity → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F5.1 | **Angela's Aviva illustration states "In 10 years time, your plan might be worth £0.00" and "Your fund will run out when you are 71".** The report never mentions it and asserts that the strategy preserves *"the majority of the pension capital for longer-term growth"* | Highest |
| F5.2 | **The proposed cashflow shows liquid assets reaching zero in 2047** (Angela 84, Kevin 88). The £266,000 on which the capacity-for-loss assessment, the emergency-fund reassurance and the alternatives analysis all rest is fully consumed inside the plan's own base case | Highest |
| F5.3 | **The 6% inflation stress test fails before Angela's stated life expectancy.** The report states a shortfall *"from 2047"*; the scenario shows pensions down to £2,748 in 2047 and liquid assets **negative £17,045 with pensions at zero in 2048** — Angela aged 85. Two lines later the report states the ONS life expectancy for a 62-year-old female is 87 and concludes *"your funds would be expected to last throughout your lifetime"* | Highest |
| F5.4 | **The report never states the size of the cash drawdown its own strategy requires.** Kevin's £1,034 plus Angela's £1,396.67 is £2,430.67 against a £3,500 target — about £1,069 a month, roughly £41,000 by October 2029. The adviser's risk-profile note states the figure (*"Prior to this they will need £2,467 per month which is £30,000 Net pa"*); the report says only *"alongside using your substantial cash reserves"* | High |
| F5.5 | **The recorded emergency fund shortfall is not addressed.** The Fact Find records £4,000 set aside against £10,500 required — a **£6,500 shortfall** — while the report describes the cash position as providing *"excellent financial security and flexibility"* | High |
| F5.6 | **The cashflow does not model the recommendation.** It applies a UFPLS of **£15,712 a year (£1,309 monthly)** — the figure from the adviser's file note, computed on a 20% rather than 25% tax-free element — not the recommended **£16,760.04 (£1,396.67 monthly)** | High |
| F5.7 | **The tax-free premise of the whole strategy is unresolved.** The report itself records that business income *"may be attributed to you in the near future"* and the Fact Find records *"we do not know what her income will be for this Tax Year as waiting for their accountant to confirm"*. The recommendation is made anyway, with a promise to *"alter the income strategy"* later | High |
| F5.8 | **The initial fee is taken from the pensions** — £3,686.57 from Angela's and £4,846.77 from Kevin's on day one. The Defaqto switch report states the consequence explicitly: *"This results in a shortfall in the death benefit immediately after the switch of £3,686.57"*. The report does not | High |
| F5.9 | The full-year Personal Allowance arithmetic does not hold for a strategy starting **6 August 2026**: £1,396.67 × 12 exactly consumes the £12,570 allowance, but only eight or nine payments fall in 2026/27 | Moderate |
| F5.10 | The cashflow gives the £49,000 Marcus Cash ISA a **4.9%** expected return where the Fact Find records **4.1%** — the £40,000 Marcus account in the same model is correctly set at 4.1% | Moderate |
| F5.11 | The cashflow models **Kevin as retiring at 65 in 2024**; both clients retired on 29 May 2026 and were drawing £3,000-£3,500 a month from the business until then | Moderate |
| F5.12 | **A Baseline (pre-advice) cashflow exists in the case file and is never shown to the client or compared with the proposed scenario**, so nothing demonstrates that the recommendation improves the outcome | Moderate |
| F5.13 | The description of liquidity — 10-15 working days to access the pension, against £266,000 of instantly accessible cash — is accurate and evidenced | No issue |

### CHK-006 — Recommendation Rationale and Suitable Alternatives → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F6.1 | **The Standard Life 4% guarantee is surrendered without quantification anywhere in the file.** The provider warned the adviser it is *"a valuable guarantee… The guaranteed amount will grow by at least 4% a year before charges… **this is a great position for your client to be in. This type of guarantee is no longer available on the open market**"*. The file note omits it; the Defaqto switching report records **"No benefits were listed for the existing products"**; the report disposes of it in one sentence for a **£140 a year** saving | Highest |
| F6.2 | **The fixed-term annuity quote in the report's own table appears to meet the stated need better than the recommendation and is dismissed generically.** The table shows *"Aviva — Till state pension — Gross income p.a. **£18,466.68** — Guaranteed Maturity Value **£68,461.91**"* — an income more than covering the £2,467 monthly bridging gap **and** returning capital at State Pension age. The report's fixed-term annuity paragraph lists only generic drawbacks and never engages with the quote | Highest |
| F6.3 | The report's closing rationale — *"you will benefit from **significantly lower costs**"* — is contradicted by its own Appendix (F7.1) and by its own "Including Adviser Fee" paragraphs | Highest |
| F6.4 | **The Prudential plans also carry a guarantee the report records as "None".** The provider's letters state *"We **guarantee not to apply a market value reduction (MVR)** in certain circumstances, for example, when payments are made because of death and **at your selected retirement date**"*, and that a charge is levied for it. Both plans are shown in the report as having no safeguarded features | High |
| F6.5 | **A cheaper platform was identified and rejected partly for the firm's own convenience, and this is not disclosed.** The file note: *"Aegon are slightly cheaper at 0.20% rather than Aviva at 0.21%… Aegon currently do not provide [ease of administration]"* and *"Decide on Aviva as they have better administration **for us** and the client"*. The Defaqto ranking places **Fidelity first**, the recommendation second, Quilter third and Aegon fourth. The report presents Aviva's 0.21% as *"transparent, competitive charges"* with no alternatives named | High |
| F6.6 | **Five options are listed and none is discussed.** Option 2 (leave in place and switch funds) is live for the Phoenix plan and the file note works through it — *"There is a managed fund, I have run this through our risk rater which has come out higher than a risk level 4. An internal fund switch has been discounted"* — but that reasoning never reaches the client | High |
| F6.7 | **The Prudential internal drawdown route is suppressed.** The report records *"Retirement Options: **None**"* for both Prudential plans; the file note records the actual position — *"the plan has to be internally moved or externally to start taking the pension"* — so an internal Prudential move enabling drawdown existed and is never presented | High |
| F6.8 | **Enhanced annuity rates are never considered** despite the recorded history of breast cancer (F1.5), in a case where guaranteed income was expressly on the table | High |
| F6.9 | **Four different comparable annuity figures appear across the file** — £5,132.77 (the report's Sustainability table), £5,949.60 and £6,122.88 (the Alternatives table) and **£8,122.60** (*"an annuity you could buy today"*, Angela's own Aviva illustration). The highest is the one the report does not show | High |
| F6.10 | The Alternatives section discounts long-term cash because *"Your objectives include growing your wealth over the medium to long term"*. No such objective is recorded anywhere, and the Fact Find records the opposite: *"they **do not want to consider investing any of these Cash ISA's**"* | Moderate |
| F6.11 | Angela's Defaqto switching report records under *Alternative comparisons* that when the Phoenix and Prudential plans were each considered on their own, *"**a choice was made to retain the existing pension**"* — the opposite of the recommendation, unexplained | Moderate |
| F6.12 | The Phoenix switch rationale — a risk-9 fund against an agreed risk 4, UFPLS only with no flexi-access drawdown, no adviser charging facility, and a provider closed to new business — is accurate, client-specific and fully evidenced by the file note and the Defaqto data | No issue |
| F6.13 | A generic stakeholder comparison is present and is supported by the switching reports (£181,000 against £217,000 for Angela) | No issue |

### CHK-007 — Costs, Charges and Value of the Recommendation → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F7.1 | The Appendix shows the recommendation reducing the projected fund by **16.2% (Angela, £32,000) and 13.2% (Kevin, £30,000)** at mid growth, with reduction in yield rising from 0.9% to 1.6% and from 1.0% to 1.9%, while the body's headline is *"significantly lower costs"* | Highest |
| F7.2 | **Before adviser charges the same analysis shows the switch adding 9.6% and 6.6%.** The entire product-level benefit is consumed and reversed by the 3% initial and 1% ongoing charges; neither the pre-charge figure nor the reversal is put to the client | Highest |
| F7.3 | **The critical yield is declared achievable on evidence showing the opposite.** *"the recommended plan would need to grow by 3.7% [Angela] / 3.8% [Kevin] per year… Based on the past performance of your recommended funds and the significant cost advantages, I believe this is **readily achievable**"*. Those critical yields are **real** figures after 2% inflation; the FE data gives Angela's strategy 19.70% over five years — **3.66% a year nominal, about 1.6% real** | Highest |
| F7.4 | **The report's own Sustainability of Income table states the gap and the report says nothing about it**: Critical Yield A **6.2%** against a *"Recommended Investment Strategy 5-year Annualised Return"* of **4.1%** | Highest |
| F7.5 | **The Prudential charge is not a deduction and is not comparable.** The provider's letters state *"The plan holder is **not paying the AMC and it is not deducted from the plan value**… We are unable to provide AMC in monetary terms."* The report's switch charges table presents 0.95% / £320 and £1,293 as current annual recurring charges and treats the difference as money saved | High |
| F7.6 | **The Standard Life 1% is not comparable either.** The plan information states *"For investment in with-profits there is no explicit fund management charge but we make deductions for our costs, **including the cost of guarantees** provided under with-profits business"*, with a dedicated *"Deductions each year for cost of guarantees"* column. Part of what the 1% buys is the guarantee being surrendered | High |
| F7.7 | **The cumulative effect of charges never reaches the report.** Angela's illustration states that charges *"reduce the assumed annual growth rate of your plan, after inflation, by **1.50%**"* (2.80% to 1.30%), with a first-year charge figure of **£1,600** | High |
| F7.8 | The Prudential letters give **two different figures for the same charge — 0.91% and 0.95%** — and the report uses the higher one throughout, which flatters the cost comparison | Moderate |
| F7.9 | The rationale sections quote Aviva's total as **"0.41% total"** for all four plans, including Kevin's two, where the report's own table gives Kevin's total as **0.45%** | Moderate |
| F7.10 | The 4.1% five-year annualised return in the Sustainability table reconciles with neither FE exhibit (19.70% over five years = 3.66% a year; the cumulative report gives 17.59% = 3.29%) | Moderate |
| F7.11 | Transaction costs are excluded from the 0.20%/0.24% investment figures and from every comparison, and no research in the file quantifies them | Moderate |
| F7.12 | The cashflow applies **0.45%** platform and fund charges to both clients' Aviva pensions, including Angela's, whose total is 0.41% | Low |
| F7.13 | The £8,533 initial fee, the ~£2,759 ongoing estimate, the 0.41%/0.45% totals, the monetary charge figures and the whole Pension Switch Charges table reconcile exactly with the adviser's MATHS worksheets, the Aviva illustrations and the Client Agreement's 3% / 1% terms — and the "Including Adviser Fee" paragraphs correctly state the first-year position (3.61% and 3.49% more expensive, £4,436 and £5,638) | No issue |

### CHK-008 — Disadvantages, Risks and Customer Understanding → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F8.1 | The illustration's *"Your fund will run out when you are 71"* appears nowhere in the report (F5.1) | Highest |
| F8.2 | The 16.2% / 13.2% reduction in projected value is confined to an appendix while the body promises *"significantly lower costs"* — a prominence failure under COBS 4.2.1R (F7.1) | Highest |
| F8.3 | ***"Both track or outperform the benchmark as we would expect"* is false for Angela's strategy on both measures.** The FE scatter gives the recommended Risk 4 strategy **19.70% performance and 6.98% volatility** against the benchmark's **22.33% and 6.63%**; the cumulative report gives the same picture (17.59% against 20.89% over five years) | Highest |
| F8.4 | **The report's central risk argument is contradicted by its own data in the worst year on record.** It says the Phoenix fund's volatility *"creates unnecessary risk in your retirement portfolio"*. In calendar 2022 the recommended Risk 4 strategy fell **12.63%**, the benchmark 9.47% and the Phoenix fund **8.70%**; over the 48-60m discrete period the figures are −7.86%, −7.57% and −6.31% | Highest |
| F8.5 | **The MPAA statement is wrong, on the recommended action.** The report says *"once your Tax-Free Cash has been depleted, any further income taken from your pension will be treated as taxable. **At that point, the Money Purchase Annual Allowance (MPAA) will be triggered**"*, and the appendix asserts *"You have not yet triggered the MPAA"*. A UFPLS triggers the MPAA on the **first** payment | High |
| F8.6 | **Angela gives up 51.5 percentage points of five-year return** (71.21% against 19.70%) and the cost of the switch is never quantified for her in money | High |
| F8.7 | The immediate £8,533 reduction in death benefits, which the switching reports state explicitly, is not disclosed (F5.8) | High |
| F8.8 | No loss scenario is illustrated for the recommended portfolios; the −30% market crash is described in a single sentence with no figures | Moderate |
| F8.9 | The Total Assets Projection is presented as demonstrating *"the sustainability of your financial plan"* when the growth from £1,050,446 to £1,253,121 is entirely in the main residence (£450,000 to £842,420 by 2050) and the spendable assets are exhausted by 2047 | Moderate |
| F8.10 | Market movement while transactions are pending, the 10-15 day access period, the absence of any guarantee of outperformance, cancellation rights, the loss of tax-free growth on withdrawn cash, the taxation of withdrawals and the £268,275 lump sum allowance are all disclosed | No issue |

### CHK-009 — Replacement or Switch Justification → Potential Concern

Applies: four personal pensions totalling £284,444 are being transferred to two new contracts.

| ID | Finding | Severity |
| --- | --- | --- |
| F9.1 | The Standard Life 4% guaranteed growth — described by the provider as valuable, no longer available on the open market, and lost on transfer — is surrendered without quantification anywhere in the file (F6.1) | Highest |
| F9.2 | The switch reduces the projected fund by 16.2% and 13.2% after charges, disclosed only in an appendix (F7.1) | Highest |
| F9.3 | The switching analysis projects both clients' funds paid up to ages 87 and 85 while the recommendation withdraws from Angela's immediately (F2.1) | Highest |
| F9.4 | The Prudential MVR-free guarantee at the selected retirement date and on death is recorded as *"Safeguarded Features: None"* in the report and as *"No benefits were listed"* in the switching reports (F6.4) | High |
| F9.5 | The existing charges driving the whole comparison are not deductions (Prudential) and are not like-for-like (Standard Life, where the charge partly buys the guarantee being given up) (F7.5, F7.6) | High |
| F9.6 | Existing plan values and valuation dates are wrong for Standard Life and for Kevin's Prudential plan (F1.2, F1.3) | High |
| F9.7 | *"Retirement Options: None"* overstates the Prudential limitation and suppresses the internal-move alternative (F6.7) | High |
| F9.8 | Cheaper and higher-ranked receiving schemes were identified in the research and are not disclosed (F6.5) | High |
| F9.9 | Angela's Phoenix plan — a single fund at risk 9 against an agreed risk 4, with no flexi-access drawdown, no adviser charging and a provider closed to new business — is a genuine, well-evidenced ground for switching, correctly identified and correctly supported by the file note and the Defaqto data | No issue |
| F9.10 | No exit penalties, transfer charges or MVRs apply to any of the four plans, and transfer values exceed fund values in every case; both are correctly stated | No issue |

### CHK-010 — Vulnerability, Support Needs and Foreseeable Harm → Potential Concern

| ID | Finding | Severity |
| --- | --- | --- |
| F10.1 | **The report states the clients are "in good health" and contains no vulnerability assessment**, in a file recording that **Angela had breast cancer five years ago**. Health is a driver of vulnerability under FG21/1 and is directly material here — guaranteed income was under consideration and enhanced annuity rates are never explored (F6.8) | Highest |
| F10.2 | **Several further drivers are on file and unaddressed**: both clients ceasing all earned income in the same month, a business being wound up with tax and asset-sale proceeds unresolved, and the file note's record that *"**They have never sought advice before and had a bad experience with the initial Advice Firm they contacted**"*. The adviser's own note adds that *"Both clients are cautious investors and **need hand holding** throughout retirement"* and are *"a little weary and new to investing"*. The report's Duty of Care section records the preference for face-to-face meetings and performs no assessment | High |
| F10.3 | Both clients record **"Very little understanding / knowledge"** of investing and Angela records **"I have no understanding of investments or how they work"** — a financial-capability indicator — while their entire pension provision is moved into market-linked funds and drawdown is started (F3.1, F4.1) | High |
| F10.4 | **The record of understanding is used asymmetrically**: *"you expressed comfort with this approach following my explanations"* carries the investment recommendation, but no check is recorded that either client understood the UFPLS mechanics, the MPAA consequence, or what the Standard Life guarantee was worth | High |
| F10.5 | No trusted contact, accessible-format requirement or reasonable adjustment is recorded anywhere, although the Prudential correspondence in the same file offers *"audio, braille or large print"* on request | Moderate |
| F10.6 | The Duty of Care section is internally inconsistent: *"As you have **not sought advice previously** and having had a **recent experience with another Financial Advice firm**"* | Low |
| F10.7 | The Fact Find records that the meeting was held face-to-face at the clients' home, with paper copies of the Initial Disclosure Information taken, run through and left with them, and that Wills and registered Powers of Attorney are in place for both with each other and both daughters as attorneys. The report reflects the POA and Will position accurately | No issue |

---

## Scoring set

The **55 material findings** (Highest and High) are the benchmark for recall:

```
F1.1  F1.2  F1.3  F1.4  F1.5  F1.6
F2.1  F2.2  F2.3
F3.1  F3.2  F3.3  F3.4
F4.1  F4.2  F4.3  F4.4
F5.1  F5.2  F5.3  F5.4  F5.5  F5.6  F5.7  F5.8
F6.1  F6.2  F6.3  F6.4  F6.5  F6.6  F6.7  F6.8  F6.9
F7.1  F7.2  F7.3  F7.4  F7.5  F7.6  F7.7
F8.1  F8.2  F8.3  F8.4  F8.5  F8.6  F8.7
F9.1  F9.2  F9.3  F9.4  F9.5  F9.6  F9.7  F9.8
F10.1 F10.2 F10.3 F10.4
```

A finding counts as **caught** when a run states the substance of the contradiction — naming both
sides and what conflicts. It counts as **partial** when a run touches the area but misidentifies the
conflict, states it without the contradiction, or buries it among unrelated points. Anything else is
**missed**.

Several findings are **cross-check duplicates by design**: F6.1/F9.1 (the Standard Life guarantee),
F7.1/F8.2/F9.2 (the 16.2% reduction), F2.1/F9.3 (the paid-up-to-87 basis), F6.4/F9.4 (the Prudential
guarantee), F7.5–F7.6/F9.5 (the non-comparable charges), F1.2–F1.3/F9.6 (the wrong plan values),
F6.7/F9.7 (the Prudential retirement options), F6.5/F9.8 (the cheaper platforms), F1.5/F6.8/F10.1
(the health history), and F3.1/F4.1/F10.3 (the recorded lack of understanding). Credit each check
where the point is stated, but recall here is not 55 independent trials.

---

## Known limits of this benchmark

- **One case, one adjudicator.** The Highest and High findings are contradictions between documents
  and should survive review; the Moderate and Low ones involve more judgement.
- **Most of this case has no Markdown conversion.** Folders F (cashflows, FE exhibits), G (switching
  reports, research workbooks), H (illustrations, factsheets, KIIDs) and I (the suitability report
  itself) exist only as originals, and the converted risk profile reports lost every table. All of
  the above was read from the source PDF, Word and Excel files. **A run scored against the converted
  evidence set alone cannot reach the majority of these findings** — the coverage gap should be
  reported before the run is scored.
- **Two `.eml` files in folder C** (*Angela & Kevin Stead - New Clients New Business*, *Retirement
  Advice Update*) were not read; they may contain further contemporaneous record.
- **Image-only content.** The Prudential and Standard Life with-profits return tables on report pages
  18-19, the cashflow charts and the FE line charts are images. Where the underlying numbers were
  recoverable from the FE data files they were used; where they were not, no finding was recorded —
  in particular the report's own statement that it has no third-party data for the with-profits
  funds is left unchallenged.
- **Every check lands on Potential Concern**, so this dataset cannot measure false positives at check
  level.
