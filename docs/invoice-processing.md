# Invoice Processing (`frmInvoice`) and Worksheet Registration: analysis and plan

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

## 5. Order of work, and how each step is checked

1. **Invoice rules** (done): `getTotal` and the ten sums as pure code (`InvoiceRules`), checked on saved invoices: the sums 430 of 430, the invoice amount 96.5 per cent.
2. **Invoice Processing, read side** (done): `GET /api/invoice/{case}` (the case, its member, plan and hospital, and its invoices) and `GET /api/invoice/{case}/{index}` (one invoice with its amounts by worksheet row, the
   mapping the worksheet engine uses, so the rows add up to the invoice amount for 90 per cent or more of the saved invoices); the page `/invoice/processing` (position 22) lists the invoices, marks the ones no
   worksheet has used, and shows the 29 rows. It says plainly that nothing can be added, changed or removed yet. 3 database tests, 15 browser checks.
3. **Invoice Processing, write side** (done, with the limits below): `POST /api/invoice` (add, or change when `index` is given), `GET /api/invoice/{case}/{index}/form`, `DELETE /api/invoice/{case}/{index}`,
   `POST /api/invoice/calculate`, `GET /api/invoice/fields` and `/lookups`. The checks run in the order of section 2 and the writes are the legacy procedures (through `genisis.WS_*` synonyms) in one
   transaction. **Checked by saving 80 saved invoices again with their own values: all 80 come back unchanged, column for column, in all four tables** (`Saving_a_saved_invoice_again_leaves_every_column_as_it_was`);
   that test found several real defects before it passed (see "What the re-save test found").
4. **Registration save**, routine by routine in the order of section 3: regenerate the saved rows of a version from the engine and compare them with the saved ones column by column, before any of
   it writes to a real database. Registration's grid must let the user change a limit or an approved amount, as the desktop does, because the engine alone reproduces about 69 per cent.
5. **Approve** reuses 4.

## 6. Data that would change how much can be checked

* **A full `HISWorkSheet` backup** (done: `HISWorkSheet_backup`, and 25,497 cases were copied across): more saved worksheets and invoices to check the rules and the engine against, and real unregistered
  invoices (638 cases that have an invoice but no worksheet) to register in a rolled-back transaction.
* **A full `HISMaintenance` backup**, for the same reason on the per-case tables Registration writes (`tblRecord`, `CASLG`, `CASCrossReference`).
