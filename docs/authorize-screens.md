# Authorize Claims screens

Menu path: **Authorize Claims** (legacy `mdiMain.frm`, top menu `mnuAuthotizeClaims`): DCU Tracking > Claim Report (`mnuReport1`), DCU Tracking > Claim Report (Jup.) (`mnuReport2`) and Claim Pass to A/C (`mnuClaimPass2AC`). The business marked DCU Tracking > Claim Report Required; Claim Pass to A/C is Required and has no form (see [remaining-work.md](remaining-work.md)). The sidebar lists them under **Claims**.

| Menu path | Legacy form | Web route | API | Access position |
|---|---|---|---|---:|
| Authorize Claims > DCU Tracking > Claim Report | `FrmDCUReport1` ("DCU Reports") | `/authorize/dcu-report` | `/api/authorize/dcu-report` | 16 * |

\* `mnuReport1` has no click handler in `mdiMain.frm` (the form is found by name); it is gated like Claim Tracking, the other claim report (`CheckAccess(16)`), a decision recorded in `AccessRight.cs`. **Claim Report (Jup.)** (`FrmDCUReport2`, "DCU Reports (Jupiter)") is the same 17,590 lines with the connections `medixneptuneconn` and `medixneptuneconnWS` in place of `medixmasterconn` and `medixmasterconnWS`; in this source every connection string points to the same server and databases, and it is not a Required item, so it is one screen here (the removal is in [redundancy-removal-report.md](redundancy-removal-report.md)).

## DCU Reports

Read-only (`sql/main/620_dcu_report.sql`, `Authorize/Dcu/*.cs`, `DcuReportCatalog.json`, `pages/authorize/DcuReportPage.tsx`, `dcuReportRules.ts`). One form that makes 88 lists of the claims (`VIEW_CLMReportDCU`, 70,427 rows, a row per claim) or of the cases (`VIEW_CLMReportDCU2`, a row per case): **Claim / Case**, a **Report Type** (General Listing, Age Band, Diagnosis, Hospital, Month, Insured Type, Payor, Plan No, State, Efficiency (claims only)), a **Report Selection** of the type (none for Payor and State), and the **Selection Criteria** that narrow every report.

- **Selection Criteria** (`SearchRecords`): Modality Code, Stay Type, Cash Type, HLT Type (narrowing the payors, as `LoadPayor` did), Payor, Grp Company (the payor's group companies, listed when the box is ticked), INS Type, Mem Type and Supp. Type (Both, or P principal / S supplementary and C combined / S separated limit: the desktop's eight combinations), Policy Status, Data Status, Claimability, Case Status (0 open, 1 open and close, the default, 2 delete, 3 all), Case Pending, Clm Pending, Agent Code and Policy No. (contain), Plan Code, Final Diag, Claim No, Bordx No, Version (from / to), and the dates Bordx Date, DOA, DOD, DORC, Eff Date, Exp Date and Date Join (from / to). Each has an **is empty** box that lists only the rows with no value. A **Case** report has no Bordx No, Bordx Date or Version (the desktop's `Option14` cleared and disabled them): they are not shown, and are not sent.
- **Search:** one request; the list has the desktop's columns and headings (Payor, Claim No, BordxNo, ... for the claim general listing's 49; the case general listing's 48 with Not Yet Bordx and Bordx). **Stop** abandons the request (`cmdStop_Click`), **View** shows the list alone and **Back** the criteria again, **Print to File** writes the list as a CSV (the desktop exported it to Excel) and **Clear** empties the boxes and the list. Nothing found is `No record found!`; more than 5,000 rows are cut and said so (the desktop loaded every row into a list). A column of figures sorts as numbers and the others as text, as `ListView2_ColumnClick` did.
- **How a list is made:** the criteria go to the server as one XML document and are read with typed values (a date is `yyyy-mm-dd`, a version an integer) and quoted into the `WHERE` fragment in one place, `genisis.DcuReport_Where` (the desktop concatenated them, the final diagnosis, plan code, bordx number, claim number and version unquoted). `genisis.DcuReport_Run` runs the query of the report's case, the select, group by and view of each copied from the legacy `HISWorkSheet.dbo.DCUReport` (which had 120 cases and 1,900 lines; the database test runs every case through both procedures and compares the rows). The cells are made by one engine from a catalog of the 88 reports (`DcuReportCatalog.json`: the headings, the fields, the formats, the "Empty" labels, the case numbers), extracted from the 44 report routines of the form and the 22 `ListViewHeader` procedures with a script and checked by tests, instead of 44 routines that each filled a list.
- **What the desktop asked again for every row, answered once:** the ICD description of the first and final diagnosis (`GenFirstICD`, `GenFinalICD`, `DiagFinalICD`, `DiagHOSFinalICD`, `HOSFinalICD`, `PLNFinalICD`: a query to `HISDB.dbo.DCUReport` per row; here one query for the codes of the list, `DcuReport_IcdDescriptions`); the duration of stay of each group of a **Case** report (`DurationStay`, 540 lines: a query per row on `VIEW_CLMReportDCU2` and an `If` for the column; here the duration case is run once and each group takes its sum, the empty and missing keys summed as the desktop's `(x = '' OR x IS NULL)` did); the bordx amounts of each case of the case general listing (`GenBordxRefNo1`, `GenBordxRefNo2`, `DcuReport_CaseBordx`); the five ageing counts of each month of the efficiency reports (`CountDIR0to15` ... `CountBordxInv90`, 20 routines, `DcuReport_Efficiency`: one query per report).
- **The 88 reports** (the case numbers are those of `genisis.DcuReport_Run`; Claim lists have 49, Case lists 39 of them):

**General Listing**

| Selection | Desktop routine | Claim case | Case case (+ duration case) | Columns (Claim) |
|---|---|---:|---:|---|
| Without Remarks | ResultListing | 1 | 2 | Payor, Claim No, BordxNo, Bordx Date, Member No, Policy No, Covered ID, Prin. Name, Patient Name, Prin. IC No, Prin. Sex, Prin. Ageband, Prin. DOB, Patient IC No, Patient Sex, Patient Ageband, Patient DOB, Insured Type, Plan Code, Hospital, First Diag., Description, Final Diag., Description, Bill Amt, Approved Amt, Reserve Amt, Recovery Amt, Premium Amt, Case Status, Claimability, Policy Status, DOA, DOD, DOR - C, Duration, DOR - W, Payor Eff. Date, Payor Exp. Date, CPT Code 1, Procedures Desc., CPT Code 2, Procedures Desc., CPT Code 3, Procedures Desc., CPT Code 4, Procedures Desc., CPT Code 5, Procedures Desc. |

**Age Band**

| Selection | Desktop routine | Claim case | Case case (+ duration case) | Columns (Claim) |
|---|---|---:|---:|---|
| Summary | AgeGroupListing | 37 | 38 + 39 | Ageband, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Gender | AgeGenderListing | 40 | 41 + 42 | Gender, Ageband, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Insured Type | AgeInsuredListing | 10 | 11 + 12 | Payor, Insured Type, Ageband, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Race | AgeRaceListing | 43 | 44 + 45 | Race Code, Ageband, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Plan No | AgePlanListing | 46 | 47 + 48 | Insured Type, Plan Code, Age Band, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Bordx Month | AgeIllnessListing | 49 | - | Nature of Claim, Age Band, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |

**Diagnosis**

| Selection | Desktop routine | Claim case | Case case (+ duration case) | Columns (Claim) |
|---|---|---:|---:|---|
| Top 20 by Claims/Cases | FinalDiagListing | 71 | 72 + 77 | Diag. Code, Diag. Desc, No of Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| Top 20 by Amount | FinalDiagListing | 73 | 74 + 77 | Diag. Code, Diag. Desc, No of Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| General Summary | FinalDiagListing | 75 | 76 + 77 | Diag. Code, Diag. Desc, No of Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Hospital | FinalDiagbyHos | 78 | 79 + 80 | Diag. Code, Diag. Desc, Hospital, No of Claims, Total Duration, Total Actual Amt, Total Approved Amt |

**Hospital**

| Selection | Desktop routine | Claim case | Case case (+ duration case) | Columns (Claim) |
|---|---|---:|---:|---|
| Detail Listing | HOSDetailListing | 66 | 67 | Payor, Hospital, Claim No, Member No, Covered ID, Patient Name, Bill Amt, Approved Amt, Member Amt, DOA, DOD, Case Status, Claimability, Final Diag. |
| General Summary (By Payor) | HOSSumListing | 60 | 61 + 62 | Payor, Hospital, No of Claims, Total Duration, Bill Amt, Approved Amt, Average Bill, Average Approved |
| General Summary (All) | HOSSumListing2 | 63 | 64 + 65 | Hospital, No of Claims, Total Duration, Bill Amt, Approved Amt, Average Bill, Average Approved |
| Top 20 by Claims/Cases | HOSTop20 | 55 | 56 + 62 | Payor, Hospital, No of Claims, Total Duration, Bill Amt, Approved Amt, Average Bill, Average Approved |
| Top 20 by Amount (By Payor) | HOSTop20 | 55 | 56 + 62 | Payor, Hospital, No of Claims, Total Duration, Bill Amt, Approved Amt, Average Bill, Average Approved |
| Top 20 by Amount (All Payor) | HOSTop202 | 57 | 58 + 59 | Hospital, No of Claims, Total Duration, Bill Amt, Approved Amt, Average Bill, Average Approved |
| By Bordx Month | HOSByDiag | 68 | - | Hospital, Final Diag., Description, No of Claims, Total Duration, Bill Amt, Approved Amt |

**Month**

| Selection | Desktop routine | Claim case | Case case (+ duration case) | Columns (Claim) |
|---|---|---:|---:|---|
| By Reg Month | RegMonthAllPayor | 84 | 85 + 86 | Reg. Month, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Eff Month | EffMonthAllPayor | 90 | 91 + 92 | Eff. Month, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Exp Month | ExpMonthAllPayor | 96 | 97 + 98 | Exp. Month, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By DOA Month | DOAMonthAllPayor | 102 | 103 + 104 | DOA Month, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By DOD Month | DODMonthAllPayor | 108 | 109 + 110 | DOD Month, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Bordx Month | BordxMonthAllPayor | 114 | - | Bordx Month, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Reg Month (Payor) | RegMonthPayor | 87 | 88 + 89 | Payor, Reg. Month, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Eff Month (Payor) | EffMonthPayor | 93 | 94 + 95 | Payor, Eff. Month, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Exp Month (Payor) | ExpMonthPayor | 99 | 100 + 101 | Payor, Exp. Month, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By DOA Month (Payor) | DOAMonthPayor | 105 | 106 + 107 | Payor, DOA Month, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By DOD Month (Payor) | DODMonthPayor | 111 | 112 + 113 | Payor, DOD Month, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Bordx Month (Payor) | BordxMonthPayor | 115 | - | Payor, Bordx Month, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |

**Insured Type**

| Selection | Desktop routine | Claim case | Case case (+ duration case) | Columns (Claim) |
|---|---|---:|---:|---|
| Summary | INSListing | 4 | 5 + 6 | Payor, Ins Type, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Plan No | PlanInsuredListing | 7 | 8 + 9 | Payor, Insured Type, Plan Type, No of Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Ageband | AgeInsuredListing | 10 | 11 + 12 | Payor, Insured Type, Ageband, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Gender | SexInsuredListing | 13 | 14 + 15 | Payor, Insured Type, Gender, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Race | RaceInsuredListing | 16 | 17 + 18 | Payor, Insured Type, Race Code, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |

**Payor**

| Selection | Desktop routine | Claim case | Case case (+ duration case) | Columns (Claim) |
|---|---|---:|---:|---|
| (none) | PayorListing | 52 | 53 + 54 | Payor, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |

**Plan No**

| Selection | Desktop routine | Claim case | Case case (+ duration case) | Columns (Claim) |
|---|---|---:|---:|---|
| General Summary | PlanListing | 22 | 23 + 24 | Payor, Plan Type, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| Top 10 Hospitals | PlanTopHosListing | 25 | 26 + 27 | Payor, Plan Type, Hospital Name, No of Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| Top 10 Diagnosis | PlanTopDiagListing | 28 | 29 + 30 | Payor, Plan Type, Final Diag. Code, Descriptions, No of Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Ageband | PlanAgebandListing | 31 | 32 + 33 | Payor, Plan Type, Ageband, No of Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| By Insured Type | PlanInsuredListing | 7 | 8 + 9 | Payor, Insured Type, Plan Type, No of Claims, Total Duration, Total Actual Amt, Total Approved Amt |
| General Summary with Premium | PlanPremiumListing | 19 | 20 + 21 | Payor, Plan Type, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt, Gross Premium, Net Premium |
| By Bordx Month | PlanInsuredListing2 | 34 | - | Payor, Insured Type, Plan Type, Ageband, No of Claims, Total Duration, Total Actual Amt, Total Approved Amt |

**State**

| Selection | Desktop routine | Claim case | Case case (+ duration case) | Columns (Claim) |
|---|---|---:|---:|---|
| (none) | StateListing | 81 | 82 + 83 | Payor, State, Total Claims, Total Duration, Total Actual Amt, Total Approved Amt |

**Efficiency**

| Selection | Desktop routine | Claim case | Case case (+ duration case) | Columns (Claim) |
|---|---|---:|---:|---|
| Registration | DIREfficiencyRpt | 116 | - | DIR Month, Total, 0-15, 15-30, 31-60, 61-90, >90, O/S |
| Bordereaux (Claim) | BordxEfficiencyRpt | 117 | - | DOR Month, Total, 0-15, 15-30, 31-60, 61-90, >90, O/S |
| Invoice | InvEfficiencyRpt | 118 | - | Invoice Date Month, Total, 0-15, 15-30, 31-60, 61-90, >90, O/S |
| Bordereaux (Inv.) | BordxInvEfficiencyRpt | 119 | - | Invoice Date Month, Total, 0-15, 15-30, 31-60, 61-90, >90, O/S |
| O/S Bordereaux | OSBordxEfficiencyRpt | 120 | - | Reg. Month, B/F, Registered, Bordx, O/S |

## Differences from the desktop, flag them to the business

1. **Top N lists** (Hospital Top 20, Diagnosis Top 20, Plan No Top 10 Hospitals and Top 10 Diagnosis): the legacy queries were `SELECT TOP 20 ... GROUP BY` with no `ORDER BY`, so they listed any 20 groups. A Top N list is the N highest of its measure: the number of claims or cases for "by Claims/Cases" and for the Plan No lists, the approved amount for "by Amount" (the diagnosis lists had the `ORDER BY` in the desktop's commented-out SQL; the hospital ones had nothing). "Top 20 by Claims/Cases" and "Top 20 by Amount (By Payor)" ran the same query in the desktop; they now differ. **Which amount is meant (actual or approved) needs confirming.**
2. **Diagnosis > General Summary** was unordered; it is ordered by the number of claims (the desktop's commented-out SQL), and every other list is ordered by its groups (the month lists by year and month). The efficiency Outstanding report carries a balance forward month after month, which needs that order.
3. **Insured Type > By Plan No (Claim)** ran without the criteria (the legacy case 7 did not append them); it uses them.
4. **Payor and State, Case:** the duration column was always blank (`DurationStay` had no branch for them); it is filled. **Hospital Top 20, Case:** the duration came from a claim-level query (case 57) instead of the case-level one the other Case lists use; it uses the case-level one (case 62).
5. **General listing, Case:** "Not Yet Bordx" and "Bordx" showed the case's whole approved amount in both columns whenever the case had a claim of that kind; they show the approved amount of those claims.
6. **Efficiency reports:** the age-band counts ran without the criteria of the screen (so the totals and the bands did not agree), and a month with no claim in a band stopped the report with an error (`rst3!NoofClm` on an empty result); the counts use the criteria and an empty band is 0. The bands overlap at day 15 (0-15 and 15-30), as in the desktop, and O/S is the total less the five bands. The three reports that read `VIEW_CLMReportDCUNew` could not run on this database (the view selects `VIEW_CLMReportCasDCU.MBMAvailableLimit`, a column that view does not have), so `genisis.DcuClaimNew` gives the same rows without it.
7. **Group companies:** the first form listed those of every payor (`SearchGrpCompanyPay` called with an empty filter); the Jupiter form's filter by payor is what is meant. After a Case report the Claim report's Version boxes stayed disabled until the form was reopened; they are enabled again.
8. **Labels that do not match the report:** the Report Selection **By Bordx Month** of Age Band lists Nature of Claim by Age Band, that of Hospital lists Hospital by Final Diagnosis (with the ICD description) and that of Plan No lists Payor, INS Type, Plan Code and Age Band; the label is kept as the desktop had it. **The business should rename them.**
9. **Amounts and dates:** the Format strings (`#,##0.00`, `##,#0.00`, `#,##0.00;(#,##0.00)`) give the same figures; dates are written `yyyymmdd` in the query (the desktop used `mm/dd/yyyy`, which depends on the language of the login).
10. **Not migrated:** the Excel export (CSV instead), the F6 screen print, the Jupiter form (one screen), `ResultListing2` (498 lines, never called) and the 12 `ListViewHeader` procedures nothing called.

## Tests

`DcuReportTests.cs`: the catalog (88 reports, 49 Claim and 39 Case, the headings, every case known to the SQL), the criteria XML (an element each, escaping, no bordx or version for Case), the validators, the cells of every kind of column, the duration of a Case group, the ICD codes asked for once, the ageing and outstanding rows, and the service against a fake repository; against the database every report of the catalog runs in both modes, the copied queries give the rows of the legacy procedure for every case that was not changed on purpose, a ranked list is the highest of its measure and hostile criteria are only text. `dcuReportRules.test.ts` (8) and `e2e/authorize-dcu-report.mjs` (44 checks; nothing is written).

## Code map

| Layer | Files |
|---|---|
| SQL | `sql/main/620_dcu_report.sql` (and `sql/genisis-main.sql`; deploy it with a connection that has QUOTED_IDENTIFIER ON: `sqlcmd -I`); synonyms `genisis.WS_ClmReportDcu`, `WS_ClmReportDcu2`, `WS_ClmReportClaimDcu2`, `WS_ClmReportCasDcu`; `genisis.DcuClaimNew` (HISDB is at compatibility level 100, so the criteria are XML, not JSON) |
| API | `Genisis.Api/Authorize/Dcu/DcuReport.cs` (criteria, validator, XML, repository, service), `DcuReportCatalog.cs` and `.json`, `DcuReportEngine.cs` (the cells, the ageing rows), `Controllers/AuthorizeDcuReportController.cs`, `Authorize/AuthorizeServiceCollectionExtensions.cs`, `Auth/AccessRight.cs` |
| UI | `GenisisUI/src/pages/authorize/` (`dcuReportRules.ts`, its test, `DcuReportPage.tsx`), the route in `App.tsx`, the Claims section in `components/AppLayout.tsx`, `auth/access.ts` |
