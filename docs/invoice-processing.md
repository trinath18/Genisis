# Invoice Processing (`frmInvoice`) and Worksheet Registration: analysis and plan

> The other four required Invoice menu items (Original Invoice Update, Invoice MBM Accounting, Invoice HOS Accounting, Invoice Tracking) are in [invoice-screens.md](invoice-screens.md).

Why this page exists: Worksheet Registration cannot be completed on its own. It works on the invoices of a case, and the screen that creates them is a separate, large form. This is what the
VB6 code does (read from `frmInvoice.frm`, `frmInvoiceGST.frm`, `frmWorksheetRegGST2015.frm`, `frmWorksheetAdj2013.frm` and `mdiMain.frm`), what the databases show, what is reusable as it
stands, and the order of work. Everything under "Measured" was run against the development databases on `MYDELL`.

## 1. How an invoice reaches a worksheet

| Step | Where | Notes |
|---|---|---|
| Invoice entry | `frmInvoice` (10,933 lines; the GST twin `frmInvoiceGST` has 12,563) | Menu **Invoice > Invoice Processing** (access position 22, marked **Required**) and **Case > Invoice Inquiry** (position 8, not required) open the same form. |
| Listing | `frmWorksheetRegGST2015` `lstInvoice` | Lists the case's invoices with their `INVWSed` flag; a double click opens `frmInvoice` on the case. Registration itself never inserts into `ClaimInvoice`. |
| Linking | Registration's save (lines 14445 to 14462) | `update ClaimInvoice set INVWSed = 1 …` for the case's invoices (with `INVBordxRefNo = 'VOID'` and `INVBordxed = 1` for the void case). |
| Worksheet | Registration / Adjustment | Calculates and saves the worksheet tables (`Claim`, `ClaimLimit`, `ClaimActual`, `ClaimApprove`, `ClaimHos`, `ClaimMember`, `ClaimPayMBMStatus`, …). |

## 2. What `frmInvoice` does when an invoice is saved

The form lists the case's invoices (`lstInvoice`, 87 columns per invoice) and has Add, Edit, Update, Remove and Clear. **Remove** is refused for an invoice that a worksheet
has used: "Selected Invoice cannot be deleted! It has been used in Worksheet!". **Update** (`cmdUpdate_Click` → `SaveClaim`) checks, in this order, and stops at the first that fails:

1. no case, or no member name: "Please select a record before proceeding! "
2. no discharge date: "Please Enter Date of Discharge! "
3. no pay type (member or hospital): "Please select who to be paid (Member / Hospital )! "
4. no stay type: "Please select a Stay Type! " (a second, unreachable "Please select a Pay Type! " follows it in the code)
5. no payee: "Please key in the person/hospital to be paid! "
6. no invoice number: "Please key in Invoice No.! "
7. no invoice date: "Please key in Invoice date! "; not a date: "Please key in correct Invoice date! "
8. paid to the member and no receipt date: "Please key in Invoice Receipt date! "; no receipt mode: "Please key in Invoice Receipt Mode! "
9. paid to the hospital and no excess amount: "Please key in Excess Amount! "
10. no initial invoice amount: "Please key in Initial Invoice Amount! "
11. a generic provider name (dental clinic, dialysis centre, foreign hospital, general practitioner, government hospital and others) without the name typed: "Please Enter Hospital Name or Clinic Name!"
12. paid to a hospital other than the case's: asks "Different Hospital. Do you want to Proceed? " (No stops)
13. a new invoice whose number the case already has: "Current Invoice No has been used before! PLease re-verify the invoice."

Then, in **one transaction** on the worksheet database: `getTotal`, `SaveInvoice`, `saveActual`, `saveActualOthers`, `saveNotCover`, `SaveINVRemarks`, and when the case has a worksheet
(`lblWs`) `saveLimit` and `SaveClaimDate` (`Claim.CLMLastDocDate`), then `SaveCASOthers`. For payor AX it reminds "Please ensure hospital discount is given!" when the hospital's `HOSDiscInd` is Y.

### Stored procedures that already exist (parameterized, so they can be called as they stand)

All are in `HISWorkSheet`; the form builds the call as text, the port will pass parameters.

| Table written | Procedure (parameters) |
|---|---|
| `ClaimInvoice` | `Insert_ClaimInvoice_2013` (24) / `_WODIR` / `_WOFolDate` / `_WO2013` for a new invoice, depending on which of the follow-up and receipt dates are given; `Update_ClaimInvoice2013` (20) and its three `_WO…2013` variants for an edit |
| `ClaimInvoiceActual` | `Insert_ClaimInvActual2010` (47) / `Update_ClaimInvActual2010` |
| `ClaimInvoiceActualOthers` | `Insert_ClaimInvoiceActOthers_New` (41) / `Update_ClaimInvoiceActOthers_New` |
| `ClaimInvoiceNotCover` | `Insert_ClaimInvoiceNotCov08` (11) / `Update_ClaimInvoiceNotCov08` |
| `CASOthers`, `CASOthersTwo` (HISDB) | `Case_AdjInvCASOthers`, `Case_AdjInvCASOthersTwo` |

The positional arguments of those calls are the form's variables (`A1`, `A1B`, `A2`, …, `txtGrossRB`, `txtRBTax`, …). About 40 of the `A…` variables are sums of finer fields computed in
the form's change handlers (for example `A5` pre-hospital = `txtXray` + `txtPreLab` + `txtPreOthers`), so the port needs those formulas as well as the procedures.

### The form's arithmetic, ported and checked (`Genisis.Api/Invoice/InvoiceRules.cs`)

The 40 change handlers that fill the sums were read out of the VB6 mechanically (`extract_aggregates`: every `A… = CDbl(x) + CDbl(y) …` line and the controls each sum uses); they give ten distinct sums and no
conflicting formula:

| Sum (form variable → column) | Parts |
|---|---|
| A5 → `CLMPreHSAmt` | X-ray + lab + others (`CLMPreXRay`, `CLMPreLab`, `CLMPreoTHERS`) |
| A5B → `CLMMPreHosAmt` | the same for the medical pre-hospital (`CLMMXRay`, `CLMMLab`, `CLMMPreOthers`) |
| A6 → `CLMSurgicalAmt` | surgical fee + physician + post-treatment + ward (`CLMSurgicalFee`, `CLMSurgPhy`, `CLMSurgPost`, `CLMSurgicalWard`) |
| A7 → `CLMAna` | fees + consultation + ward (`CLMAnaeFees`, `CLMAnaeConsultation`, `CLMAnaeWard`) |
| A9 → `CLMPhysician` | fees + ward + procedures (`CLMInHosFees`, `CLMInHosWard`, `CLMProceduresFee`) |
| A11 → `CLMAccPostAmt` | emergency + post-treatment (`CLMEmergency`, `CLMPostTreat`) |
| A19 → `CLMTax` | R&B tax + ICU tax + lodger tax (`CLMRBTax`, `CLMICUTax`, `CLMLodgerTax`) |
| A1B → `CLMTotalBoardAmt` | A1 + ART (`CLMBoardAmt` + `CLMNewRBTax`) |
| A2B → `CLMTotalICUAmt` | A2 + AIT (`CLMICUAmt` + `CLMNewICUTax`) |
| ALB → `CLMTotalGAllowAmt` | A17 + ALT (`CLMGuardianAmt` + `CLMNewLodgerTax`) |

`getTotal` adds every amount control named A… of at most four characters and takes off `A1 + A2 + ART + AIT + ALT + A17`, so the room, ICU and guardian lines are counted once, through A1B, A2B and
ALB. **Measured on saved invoices** (`InvoiceRulesOracleTests`, one invoice in 200): the sums equal the saved columns for **430 of 430** invoices that have a breakdown (in the first, wider SQL check 7 of 8,600
had the parts all zero, the total having been typed); the invoice amount equals `getTotal` for **388 of 402 (96.5 per cent)**; the others were changed after they were saved. An invoice entered without a
breakdown has its parts empty and keeps the typed sum.

### Variable to column map of the three amount procedures

The call in `SaveClaim`'s `saveActual`, `saveActualOthers` and `saveNotCover` passes the form's variables positionally; zipped with each procedure's parameters (47, 41 and 11, all matched):

* `Insert_ClaimInvActual2010` (`ClaimInvoiceActual`): `Text1` → `CLMCHospSum`; A1 → `CLMBoardAmt`; `txtGrossRB` → `CLMGrossRB`; A1B → `CLMTotalBoardAmt`; A2 → `CLMICUAmt`; A2B → `CLMTotalICUAmt`;
  A3 → `CLMServices`; A4 → `CLMTheatre`; A24 → `CLMPreHosp`; A5 → `CLMPreHSAmt`; A5B → `CLMMPreHosAmt`; A6 → `CLMSurgicalAmt`; A7 → `CLMAna`; A8 → `CLMNSPreHos`; A9 → `CLMPhysician`;
  A10 → `CLMPost`; `txtACC` → `CLMEmergency`; `txtPTreat` → `CLMPostTreat`; A11 → `CLMAccPostAmt`; A12 → `CLMAmbulance`; A13 → `CLMOutpatient`; A14 → `CLMMonthly`; A15 → `CLMMedical`;
  A16 → `CLMPhone`; A17 → `CLMGuardianAmt`; ALB → `CLMTotalGAllowAmt`; A18 → `CLMAdmin`; `txtRBTax` → `CLMRBTax`; `txtICUTax` → `CLMICUTax`; `txtLodgerTax` → `CLMLodgerTax`; A19 → `CLMTax`;
  A20 → `CLMOthers1`; `Text2` → `CLMMHospSum`; A25 → `CLMOthersNotCOPay`; `txtOhersNoCoPay` → `CLMOthersDescNoCOPay`; A20B → `CLMOthers2`; `txtOthersDesc1` → `CLMOther1Desc`;
  `txtOthersDesc2` → `CLMOthers2Desc`; A21 → `CLMCashAmt`; A22 → `CLMOrgan`; A23 → `CLMMRI`; ART → `CLMNewRBTax`; AIT → `CLMNewICUTax`; ALT → `CLMNewLodgerTax`; A26 → `CLMHomeNCAmt`; A27 → `CLMCancerAmt`.
* `Insert_ClaimInvoiceActOthers_New` (`ClaimInvoiceActualOthers`, 40 columns after the invoice index): the days and rates (`CLMBoardDay`, `CLMRB`, `CLMICUDay`, `CLMICU`), the breakdown above, `CLMINHospHyVisitDay`,
  `CLMKidneyMnth`, `CLMLodger`, `CLMLodgerday`, `CLMCashDay`, `CLMCash`, the member-paid entries (`CLMMedRptMBM`, `CLMTelChargesMBM`, `CLMGuaAllowMBM`, `CLMAdminRegMBM`, `CLMOthersMBM`) and the nine
  `…HSS` entries of the hospital supplies screen (`CLMPharmacyHSS` … `CLMMiscHSS`).
* `Insert_ClaimInvoiceNotCov08` (`ClaimInvoiceNotCover`): `PreH60`, `PreGP`, `PreMedic`, `PreMR`, `PreSpec`, `PostHosp`, `PostNotSame`, `Others`, `ReimOtherRemarks`, `INVHospName`.
* `Insert_ClaimInvoice_2013` (`ClaimInvoice`, 24): `INVWSversion` (the case's last worksheet version), `INVBordxed`, `INVWSed` (both 0), `INVNo`, `CasNumber`, `InvDate`, `INVEnteredDate`, `INVEnteredBy`,
  `INVPayee` (the hospital code, or the typed payee), `INVStayType`, `INVPAYType`, `InvPayTo`, `CLMINVRecMode`, `INVFolUpDate`, `CLMINVRecDate`, `INVAmount`, `InvPaidByMem`, `CLMUHCNo`, `InvExcessAmt`,
  `InvPayAmt`, `INVInitialActAmt`, `INVRoomType`, `UpfrontDisc`, `Discreceived`.

## 3. What Registration's save writes (`frmWorksheetRegGST2015` and `frmWorksheetAdj2013`, same routines)

| Routine | Lines (Adjustment form) | Writes |
|---|---|---|
| `SaveClaim("A")` | 228 | the version row of `Claim` (141 columns) |
| `SaveClaimRemarks` | 70 | remarks |
| `saveLimit`, `saveActual`, `saveApprove`, `saveHos` | 148, 83, 80, 79 | `ClaimLimit`, `ClaimActual`, `ClaimApprove`, `ClaimHos` (per-row figures; the calculation engine produces them) |
| `saveMember`, `savePayMBMStatus` | 339, 935 | `ClaimMember`, `ClaimPayMBMStatus` |
| `SameLimitUndExcess`, `saveExcessLetter` | 14, 512 | `ClaimSameLimit`, `CAS_Excess` |
| `UpdateInvoice`, `UpdateCasReserve` | 15, 21 | invoice and case reserve |
| `UpdateMBMBalance`, `UpdateMBMBnfBal`, `UpdateMBMLifeTimeBal`, `UpdateCASTakeOverLmt`, `UpdateClaimTopup` | 606, 66, 61, 28, 15 | the member's available limit and benefit balances |
| `HCreditDebit`, `tblRecord` update | 28 | credit / debit marks; `IsWSdone`, `CaseProgress` = GL Issued |

Approve and Registration share these; both change the member's available limit, which other worksheets and payments read. What each routine writes is taken from its name, its size and its first lines;
the exact columns are read when that routine is ported.

## 4. What the data shows

* Every invoice in the development `HISWorkSheet` belongs to a saved worksheet; only 170 of 39,684 are not yet marked `INVWSed`.
* **`HISWorkSheet` was heavily trimmed:** 16,210 cases had a worksheet, against 683,012 discharged cases in the full `HISDB`. `HISWorkSheet_backup` (the full database) was then restored and 25,497 more
  cases were brought across (70,426 versions, 91,108 invoices, 8,310 invoices not yet in a worksheet and 638 cases that have an invoice but no worksheet: real Registration candidates). See
  "More data" in [worksheet-screens.md](worksheet-screens.md). The larger data lowered the engine's accuracy from 79 to 69 per cent (the first sample had been too easy).

## 4a. The save, as built

**Write path.** Add inserts the header (one of four procedures, by whether a follow-up and a receipt date are given), reads the new key back by case and number, then writes the amounts
(`ClaimInvoiceActual`), the GST row (`ClaimInvoiceActualGST`: every GST amount is zero, since GST has been zero-rated since 2018; the invoice's own columns are passed through, as the desktop's
`frmInvoiceGST` does: 2019 to 2021 invoices have an all-zero GST row, 18 of 55,882 have a non-zero amount), the entries (`ClaimInvoiceActualOthers`, by the 2019 procedure with its 44 parameters), what is not
covered (`ClaimInvoiceNotCover`) and the case's remark. A change updates the same. The invoice amount is `getTotal`, the amount to pay is the amount less the excess for a hospital and 0 for a member (100 per
cent of 2,601 hospital invoices; 94 per cent of 1,771 member invoices), the worksheet version is the case's last completed version plus one (or 1), and the hospital sums (`CLMCHospSum`, `CLMMHospSum`) are kept
for plans 2 to 5 and new SM plans.

**The page** (`/invoice/processing`): Add, Edit, Remove and Clear; the form is drawn from `GET /api/invoice/fields` (about 100 fields in 11 groups), the sums and totals come from the server as the amounts are
typed, the entries of the plan's tax era and the case's modality are the ones enabled, and a sum can be typed only while none of its parts has a value. Different Hospital asks before saving.

**Not done as the desktop does it** (deliberate, and in the code's comments):

* Changing an invoice a worksheet has used is **refused**. The desktop then also saves the worksheet's limits (`saveLimit`, 260 lines on top of the plan engine). Removing such an invoice is refused as in the desktop.
* The desktop's `SaveCASOthers` is not called. It overwrites the case's MMA data (procedures, CPT codes, surgeon fees, attending doctors) with whatever its MMA dialog holds; the MMA / CPT screen owns that data here.
* The hospital is compared with the case's by code; the desktop compares the length of the names.
* Removing an invoice deletes all five of its rows; the desktop deletes three and leaves the entries and the GST row behind.
* Editing keeps the invoice's own pay type, upfront discount and discount received; the desktop's update procedures reset the last two.
* Negative amounts are allowed (4 of 80 saved invoices have credits).
* A `ClaimInvoiceActualOthers_New_2019` call is used, not the `_New` the source snapshot has: 123, 460 and 2,014 invoices of 2019, 2020 and 2021 have stents, balloons or implants amounts, so the desktop in
  use is newer than the source we have (`frmInvoice.frm` of the snapshot has the nine hospital-supplies entries only). Registration's source should be checked for the same.

### What the re-save test found

Loading a saved invoice, saving it again and comparing every column found: the wrong name of one update procedure (the follow-up and receipt variants), the tables spell five columns with other capitals than the procedures' parameters (`CLMPreXray`, `CLMPreOthers`,
`CLMInHosPhyVisitDay`, `CLMLodgerDay`, `preMedic`), so a change would have zeroed them; the invoice's own pay type (`INVPAYType`) was being replaced by the case's in 50 of 77; the upfront discount and the discount
received were reset; three columns (stents, balloons, implants) the snapshot's procedure does not know; saved credits made a "no negative amounts" rule wrong; and an `IsUpdate` flag the update procedure sets (expected).
Two more were found by the browser: the field kind was sent as a number and not its name (so text fields were drawn as amounts), and the layout classes were used wrongly (one narrow column).

### An incident: the first tests and the first save were not transactional

`IDbSessions.OpenAsync()` is a **read-only** session with no transaction; `BeginAsync()` has one. The invoice save and remove, and every write test, opened `OpenAsync()`. So for about seven test runs:

* **the tests wrote for real**: 50 test invoices (numbers `ZZ-…`, entered by `ZZTEST`) were left on case `ZN00007106`, with all their rows; that case's remark was overwritten with a test text; and **78 real invoices** were re-saved
  by the re-save test (the update procedure sets `IsUpdate`, the three new columns went from empty to 0, and the invoice amount, the amount to pay and a hospital sum were recomputed on a few);
* **the product's save was not one transaction** as this document said: a failure halfway would have left a header without its amounts. The save was never deployed; it was only reachable from the local API.

It was found when the number of invoices came out 50 higher than before. **Put right the same day**: a verified backup of `HISWorkSheet` was taken first
(`HISWorkSheet_before_test_damage_repair.bak`), then in one transaction the 50 test invoices were removed with their rows and the 78 real invoices (and the two remarks) were put back as `HISWorkSheet_backup` has
them, and the transaction rolls back unless **no row of the six tables differs from the backup any more** (it did not: 0 differ, and 0 still differ after the corrected tests ran). The save and the remove now use
`BeginAsync()`; `What_a_test_session_writes_is_rolled_back_and_a_save_that_fails_halfway_leaves_nothing_behind` proves a test session leaves nothing and that a save that fails after the header and the amounts are written
(a number of days no column holds) leaves nothing. **The earlier "80 of 80 unchanged" was therefore not a clean result**: after the first runs had changed real rows, later runs re-saved the changed rows. On clean data it is 79 of 80: invoice
2656665 of case ST00032453 has a saved `CLMMHospSum` of 435.00 where the rule gives 0; the hospital sums are kept for plans 2 to 5 and new SM plans, and the member's plan now is AF73 (the desktop reads the plan from the member's
cross-reference row, which this member does not have in `HISMaintenance`), so the plan at the time of the save cannot be known.

Also changed since: an empty remark is no longer added as a blank row for a case that has none (the desktop adds one; every save would leave one).

## 4b. What Registration's save is (read from the desktop, before any of it is built)

**Registration and Approve are one save.** `frmWorksheetRegGST2015` has a single write button, `cmdApprove_Click`, and Adjustment's `cmdApprove_Click` is the same sequence. After the checks (case number; member; at least one
invoice, "Worksheet cannot be saved without any invoice!"; a final diagnosis, "Worksheet cannot be saved without Final Diagnosis!"; a worksheet status; the payable-to-hospital amount equal to the hospital total for a hospital-paid
case, "Please verify Payable to Hospital amount"; bank name and account number together; for a member-paid case the address, post code, city and state) and "Are you sure?", one transaction runs, in this order:
`SaveClaim("A")`, `SaveClaimRemarks`, `saveLimit`, `saveActual`, `saveActualGST`, `saveApprove`, `saveApproveGST`, `saveHos`, `saveMember`, `savePayMBMStatus`, `saveExcessLetter` (when an invoice exists),
`saveSameLimit`, `UpdateInvoice`, `UpdateCasReserve`, then the member's balances (`UpdateMBMBalance`, or `UpdateMBMBalanceTU` for top-up members, `UpdateClaimTopup`, `UpdateUnderWriting`, `UpdateMBMBnfBal` and
`UpdateMBMLifeTimeBal` for lifetime plans, `UpdateCASTakeOverLmt`), the top-up grid, the credit or debit note (`UpdateCreditDebit`) and, for a top-up member, a duplicate claim in four databases (`SaveDuplClaim`),
then commit, `HCreditDebit`, and "Record Save". That is 25 routines and several thousand lines of VB6.

**`SaveClaim("A")` writes the version row of `Claim` through `Insert_WS_Claim_2012` (or `Insert_WS_Claim_2014V1`).** Of the 141 columns about 55 are written at registration: the case, version, dates, user and status
(`ClaimStatus` C, bordx flags, `CLMWSStatus` from the worksheet status), the totals read from the grid (actual, approved, final, payable to the hospital and to the member, limit exceeded, excess, reimburse, balance
limit, previous balance and limits, co-payment, BTBG, ex-gratia, bordx amount), the collected-by flags, the pay-to, bank and account, the stay and cash type, the last document date and the GP name. About 45 more
(payment request, payor approval, TIMS, STMB, ...) are empty at registration and filled by later steps. Several of the 55 depend on state that is not in the saved rows (annual, per-disability, top-up and lifetime
limits, and which branch applies), so they cannot be regenerated from an invoice and a plan alone yet.

**Measured on saved versions (3,243):** `CLMFinalApprove` equals `CLMTotalApproved` in all of them; `CLMTotalApproved` equals the sum of the saved approved row in 95 per cent and `CLMTotalActual` the sum of the saved actual
row in 93 per cent; `CLMPayableByMedix2H` / `2M` equal the sums of the saved hospital / member rows in only 66 / 70 per cent, because they are the grid's Final Total row, which has more in it than the 29 rows (co-payment,
excess, ex-gratia and the limit); `CLMPayableByMedix2H` + `2M` equals `CLMFinalApprove` in 95 per cent.

## 4c. Registration's save: what is built and what is not

**Built and checked** (no endpoint writes anything yet; see below):

* `RegistrationChecks` (`Worksheet/Calculation`): the checks of `cmdApprove_Click` in the desktop's order with its messages: case number, record, an invoice, a final diagnosis, a worksheet status, the payable-to-hospital
  amount equal to the invoices' for a hospital-paid worksheet, then for a member-paid one the bank and account number together, the address, post code, city and state; and the bank account's digit count
  (`BankAccounts.AccountDigits`), which the desktop checks first, shows its message at once, and lets stop the save only when nothing else does. 12 unit tests.
* `WorksheetRows` (`Worksheet/Engine`): the rows of `ClaimActual`, `ClaimApprove`, `ClaimHos` and `ClaimMember` as `saveActual`, `saveApprove`, `saveHos` and `saveMember` write them: which worksheet row goes to
  which column (rows 8 and 13 go to `CLMCOPreHSSum` / `CLMCOMPreHSSum` and rows 6, 7, 9 to 12 are not written for plan families 2 to 5 and payor IB; row 19 is `CLMTax`), and the text of each cell (the grid's
  `#,#.00`: ".50" under one, "-" for an empty cell; the member's column saves an amount under 1, which includes everything the member owes, as VB's number, "-94.4"; the approved cell of the monthly and organ rows
  is "0" when empty in the desktop in use since 2020 and was "-" before 2019).
  **Checked against the saved rows**: each saved row of a sample (about one version in twenty of those opened since July 2020: 1,786 versions) is read back into numbers and written again, and every cell compared:
  `ClaimActual`, `ClaimHos` and `ClaimMember` are **100.00 per cent of cells equal** (1,786 of 1,786 rows entirely), `ClaimApprove` 99.35 per cent (1,660 rows entirely). The rest of `ClaimApprove` is the monthly row
  saved as "-" in 111 versions where the organ row is "0", and 9 versions whose whole approved row is "0" (a status that approves nothing, not worked out). This checks the map and the formats, with the engine out of it;
  the numbers themselves come from the engine, which is right for about 73 per cent of the totals (see step 6).

* `WorksheetRows.Limits`: the row of `ClaimLimit` (`saveLimit`): the limit text of each worksheet row in the same columns, "-" for a row with none, and the six day counts the limits were worked out with
  (`CLMBoardDays`, `CLMICUDays`, `CLMINHOSPhyDays` for the itemised layout only, `CLMKIDDCDays`, `CLMCashDays`, `CLMGurdianDays`) as VB's number. Read back from 1,806 saved rows since July 2020 and written again:
  **95.77 per cent of the cells equal** (1,110 rows entirely). All of the rest is the desktop saving an empty string where it never set a variable (the kidney, cash and guardian days in about a third of the rows, and some
  empty limit cells); the web writes "0" and "-" there, which the desktop and `Worksheet_Amount` read the same, and which no rule in the data separates from the rows that have "0".
  **The days the engine can find alone:** `PlanLimitRules.LimitedDays` (the stay's days held to the days the plan allows) gives the saved Room & Board days in **1,786 of 1,805** versions (98.9 per cent) and the saved ICU days
  in 1,804 of 1,805. The physician visit, kidney, cash and guardian days are not worked out: they are held to what the case has left after earlier worksheets (`ClaimSameLimit`), which is the next piece of the limit work.

* `PlanLimitRules.LimitDays`: the six day counts together, each of the stay's days (`WorksheetEngine_StayDays` now also sums the physician visit, kidney, cash and guardian days of the invoices) held to the days the plan allows
  for its benefit (101, 102, 302, 404, 601, 701). Against the saved days: Room & Board 98.9 per cent of 1,805 versions, ICU 99.9; of the versions saved with physician visit days 46 of 55 equal, with cash days 14 of 19, with
  guardian days 1 of 1, kidney 0 of 0 (they are rare).
  **`ClaimSameLimit`** (the limits and days that are left after each worksheet of a case; the desktop reads it for a case's later worksheets and writes it with `saveSameLimit`) has a row for 35,007 cases and is not ported: in
  the sample the plan's limits equal the saved ones for later versions about as often as for first versions (83 per cent of 344, 81 per cent of 475), so it is not where the remaining limit gap is.
* `WorksheetRows.ClaimTotals`: the seven totals of the `Claim` row that follow from the grid and the summary rows (the engine's `WorksheetFinal`): `CLMTotalActual`, `CLMTotalApproved`, `CLMFinalApprove`, `CLMCoPayRB`,
  `CLMLimitExceeded`, `CLMExcess` (what the member owes) and `CLMReimburse` (payable to the member). Rebuilt from 1,786 saved grids (the four tables' numbers, the saved co-payment and available limit) and compared with the
  saved row to the cent: **99.83, 99.55, 99.55, 100.00, 99.72, 100.00 and 100.00 per cent**. The misses are versions where the limit logic differs (an annual limit of another kind, duplicate claims).
  **The Final Total row and the balance limit, found by looking at the saved rows** (the earlier note here said they were not simple sums; they depend on the cash type, not on the annual-limit kind): for a cashless worksheet
  (`CLMCashType` C, the hospital is paid) `CLMPayableByMedix2H` is the hospital row plus what the member owes (the negative member cells) in 95 per cent of 1,286 versions since July 2020, and for a reimbursement (R, the member
  is paid) it is 0 in all 438; `CLMPayableByMedix2M` is the positive member cells in 97.6 per cent (C) and 99.1 per cent (R). The cash type is the first invoice's pay-to (`WorksheetRows.CashType`: H gives C, otherwise R).
  `CLMBalanceLimit` is the **previous balance**, not the previous balance less the final approve that the source of the desktop we have computes: it equals the previous balance in 3,158 of 3,369 versions since July 2020 (94 per cent)
  and the previous less the final in 10 (before 2020: 1,041 and 11); in 61 it is the previous plus the final and in 140 something else (small differences, not explained). `ClaimTotals` writes both when the member's available limit is given.

* `ClaimVersionRow` (`Worksheet/Engine`): **the version row of `Claim`**, the 56 parameters of `Insert_WS_Claim_2012` (a test compares the names with the procedure's own). The case, the user, the dates (the version
  is opened, last updated and closed the day it is saved, as in all 33,951 versions saved since July 2020), the status (`CLMWSStatus` is the first two letters of the worksheet status; `XR` is a void worksheet: bordx
  status 1 and reference `VOID`), the constants (`ClaimStatus` C, `CLMBordxWSStatus` and `CLMBordxWSASCII` N, nothing paid yet, top-up N), the payee, bank, account, GP, stay and cash type, the collected-by choices (M in
  every saved version), and the totals of `ClaimTotals`. The rules the data supported, measured over the 33,718 closed versions saved since July 2020:
  * **Bordx amount** (`CLMBordxAmt`): the final approve for a reimbursement (9,154 of 9,154) and the amount payable to the hospital plus to the member for a cashless one (24,553 of 24,564); 99.97 per cent together.
  * **BTBG amount** (`CLMBTBGAmt`): for a member flagged BTBG (`MBMOthersTwo.MBMBTBG` = Y, not `MBM`) with a cashless worksheet, the final approve less the amount payable to the hospital when its sign is the
    opposite of the hospital's; otherwise 0. For the 3,074 cashless versions of BTBG members this equals the saved amount in 3,052 (99.3 per cent); no other member has one but 4.
  * **Previous cancer and home nursing limits**: the limit text of the monthly row (slot 17) and the home nursing row (slot 24) as a number (home nursing 33,712 of 33,713, monthly 32,605: 96.7 per cent).
  * **Previous balance and balance limit** by the kind of limit: annual (the available limit, for both: the desktop in use saves the previous balance as the balance limit), per disability (the plan's limit for both:
    2,616 of 2,625 and 2,498), none (the final approve for both: 3,142 versions).
  * Zero in every saved version, so written as 0: the company excess, the company amount, the ex-gratia, the deductible, the major medical, the top-up amounts, the take-over and the case management fees. Top-up members
    (none is saved) are not covered.
  **Checked** by rebuilding each version of a sample (1,786, about one in twenty) from its saved grid, limits and summary rows, with what the desktop reads from the screen taken from the saved row: **1,521 versions rebuild
  with every compared column equal** and every column is at least 92.8 per cent (the four columns the later steps change are not compared: bordx status and reference, last update user and date). The balance limit is the
  weakest (92.8 per cent; the misses are the previous balance plus or less the final approve, 42 and 6, and 80 others).
* `ClaimOthersRow` (`SaveClaimRemarks`, `Insert_WS_ClaimOthers_Jul08`): the remarks, the payee's address, the payment mode and the attention line; the gross room rate is added to the worksheet remarks as the desktop does
  (none of the worksheets saved since July 2020 has one). `WorksheetRows.ActualGst` (`saveActualGST`): the GST copy of the actual row, a dash in every cell: 33,945 versions have the row and all but 5 are dashes.
  **`saveApproveGST` is not run**: `ClaimApproveGST` has no row for any of the 37,000 versions saved since 2020 (2,377 of 3,923 in 2017, 577 in 2018, 5 in 2019).
* `RegistrationRepository` (`Worksheet/Calculation`): **writes a version in the desktop's order, inside the caller's transaction, and never commits**: `SaveClaim` (the version row, then the case is no longer
  pending), `SaveClaimRemarks`, `saveLimit`, `saveActual`, `saveActualGST`, `saveApprove`, `saveHos`, `saveMember`, then `UpdateInvoice` (the invoices of the version are marked as worksheeted; a void worksheet
  bordxes them under VOID) and `UpdateCasReserve` (the case's reserve is cleared). The desktop's own procedures do the inserts, reached through `genisis.WS_Insert_WS_*` synonyms; the three routines the desktop wrote as
  recordset edits are `genisis.WorksheetSave_*` (`sql/main/390_worksheet_registration_save.sql`). Nothing calls it yet. **Checked** by writing 620 saved versions again as versions 9001 and up of their cases inside a
  transaction that is rolled back: every one of the 111,600 grid cells, 34,720 columns of `Claim` and 8,060 of `ClaimOthers` read back as built, the case's pending flag and reserve were set, and afterwards
  nothing was left (no row with those versions, and a checksum of the whole `CAS` table unchanged).
  **A difference on purpose:** `Insert_WS_Claim_2012` takes the bank account number as `nvarchar(15)` though `Claim.CLMAccountNo` holds 30, so the desktop cut a longer number to 15 characters without a word (the
  test found six of its sample: 16-digit numbers; 375 of the numbers saved since July 2020 are 16 to 19 long, after Adjustment's Update put them right). The save writes the whole number right after the procedure.

* `PayMbmStatusRow` (`savePayMBMStatus`, `Worksheet/Engine`): a Y or N per worksheet row of `ClaimPayMBMStatus`: Y when the plan has a numeric limit for the row, nothing was invoiced and something is approved. The desktop's
  routine is one row behind today's grid from row 19 on (the tax row is tested for `CLMCashDays`, the home nursing row for `CLMOthers1`, the lodger tax row not at all) and the saved rows follow it (`CLMTax` is empty,
  `CLMCashDays` is N), so the map is kept as it is. **Rebuilt from 1,786 saved grids: 50,008 of 50,008 flags equal**, every row entirely; no closed version saved since July 2020 has a Y in a column it writes.
* `ExcessLetterRow` (`saveExcessLetter`): the case's letter in `CAS_Excess`, made when the worksheet leaves the member an excess or a limit exceeded and the case has a hospital-paid invoice. **The number**: the sequence
  behind it (`EXC_SEQ`) moves on at every such save while the letter row is added only when the case has none (the data confirm the source: 773 of 805 cases with two or more such versions have a sequence above 1, 803 of
  their letters end in -1). **Rebuilt from the saved member column of 1,022 sampled letters**: invoice, dates, issuer, undertaking and description equal in all; the rows the desktop in use never writes are empty in all; the
  amounts equal in 97.5 to 99.9 per cent; `ExcessAmt` in 96.5 per cent (it is the member's excess alone: the source adds the undertaking, co-payment and limit exceeded, which the total already holds). What differs: 13 per cent
  of the saved letters have NULL where the rest have 0 in the rows the member could have owed; nothing in the data separates them (not user, month, version, cash type, co-payment or payor), so the web writes the 0 of the
  other 87 per cent. 855 of 1,022 letters are entirely equal.
* `SameLimitRow` (`saveSameLimit`): the case's `ClaimSameLimit` row (one per case, added with its first worksheet and changed by every later one): the limit texts as the last worksheet showed them, services, theatre,
  surgical, anaesthetist and organ written as what is left of a numeric limit once something is approved (FULL stays FULL; VB compares the limit with the approved cell as strings, so a dash sorts before an amount and gives 0;
  the organ row shows 0 for nothing approved), post-hospitalisation worked out apart, home nursing and the not co-paid others not written. The source also reduces room and board and ICU; the saved rows keep those as shown, so the
  code does. The days that are left: for a case without a row what the plan allows less what the worksheet used, otherwise what it had left less what the worksheet used, never below none. **Checked** on the last version's saved
  grid of 1,709 sampled cases: every column has the same effect on what is approved in 98.1 to 100 per cent (1,557 rows equal in every limit); the days, worked out across all the versions of 1,352 cases from the plan's days at
  the first version, equal the saved ones in 86.5 to 100 per cent per count (939 cases in all six; the misses are plans whose day rows the benefit lookup does not find).
* The repository now writes those three as well (`genisis.WorksheetSave_PayMbmStatus`, `_NextExcessNumber`, `_ExcessLetter`, `_SameLimit`, `_SameLimitOf`), after `saveMember`, in the desktop's order. The rolled-back
  re-save of 620 versions reads back 149,420 grid, flag and same-limit cells and checks that the `CAS`, same-limit and excess-sequence tables are as they were; two more tests add an excess letter and a same-limit row to cases
  that have none and check the second save (the sequence moves on, the letter stays, the days are taken off what is left).
  **A caution for whoever edits the test file with an editor tool: the CAS checksum guard of that test was lost once between two commits and had to be put back; a script that reads, changes and writes the file is safe.**

**Not built, and why no endpoint writes yet.** Registration's save is 25 routines in one transaction (section 4b). A worksheet saved with only some of them would leave the case, the member's balances, the excess letter and
the credit or debit note out of step with it, so nothing is written until the whole sequence can be. What is left, in the order the desktop runs it: the member's balances (`UpdateMBMBalance` is 606 lines, `UpdateMBMBalanceTU`, `UpdateClaimTopup`, `UpdateUnderWriting`, `UpdateMBMBnfBal`,
`UpdateMBMLifeTimeBal`, `UpdateCASTakeOverLmt`), the credit or debit note (`UpdateCreditDebit`, `HCreditDebit`) and, for top-up members, `SaveDuplClaim` in four databases.

## 5. Order of work, and how each step is checked

1. **Invoice rules** (done): `getTotal` and the ten sums as pure code (`InvoiceRules`), checked on saved invoices: the sums 430 of 430, the invoice amount 96.5 per cent.
2. **Invoice Processing, read side** (done): `GET /api/invoice/{case}` (the case, its member, plan and hospital, and its invoices) and `GET /api/invoice/{case}/{index}` (one invoice with its amounts by worksheet row, the
   mapping the worksheet engine uses, so the rows add up to the invoice amount for 90 per cent or more of the saved invoices); the page `/invoice/processing` (position 22) lists the invoices, marks the ones no
   worksheet has used, and shows the 29 rows. It says plainly that nothing can be added, changed or removed yet. 3 database tests, 15 browser checks.
3. **Invoice Processing, write side** (done, with the limits below): `POST /api/invoice` (add, or change when `index` is given), `GET /api/invoice/{case}/{index}/form`, `DELETE /api/invoice/{case}/{index}`,
   `POST /api/invoice/calculate`, `GET /api/invoice/fields` and `/lookups`. The checks run in the order of section 2 and the writes are the legacy procedures (through `genisis.WS_*` synonyms) in one
   transaction. **Checked by saving 80 saved invoices again with their own values: 79 come back unchanged, column for column, in all four tables** (`Saving_a_saved_invoice_again_leaves_every_column_as_it_was`);
   the one that changes is a hospital sum saved under a plan the member no longer has (see below). That test found several real defects before it passed (see "What the re-save test found") and, run
   the first time without a transaction, did real damage (see "An incident").
4. **Registration save**, routine by routine in the order of section 3: regenerate the saved rows of a version from the engine and compare them with the saved ones column by column, before any of
   it writes to a real database. Registration's grid must let the user change a limit or an approved amount, as the desktop does, because the engine alone reproduces about 69 per cent.
5. **Approve** reuses 4.

## 6. Data that would change how much can be checked

* **A full `HISWorkSheet` backup** (done: `HISWorkSheet_backup`, and 25,497 cases were copied across): more saved worksheets and invoices to check the rules and the engine against, and real unregistered
  invoices (638 cases that have an invoice but no worksheet) to register in a rolled-back transaction.
* **A full `HISMaintenance` backup** (done: `HISMaintenance_actual_backup`; the per-case tables Registration writes, `tblRecord`, `CASLG` and `CASCrossReference`, and the member cross-references were copied for the cases and members held; see "More data" in [worksheet-screens.md](worksheet-screens.md)).
