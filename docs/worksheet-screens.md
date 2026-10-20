# Worksheet menu — migration notes (phases 1 and 2: Tracking, Send to A/C, Send to A/R, Enquiry)

Scope: the **Worksheet** top menu of `mdiMain.frm`. `VB6-MenuItems.xlsx` marks six of its nine rows **Required**
(column P = `Y`): Worksheet Registration, WorkSheet Adjustment, Worksheet Enquiry, Worksheet Send to A/C, Worksheet Send to A/R and
Worksheet Tracking. The other three (the top-level row, Generate Credit/Debit Note, Tracking (Jupiter)) are not required and not built.

| Menu item | Control | VB6 form chosen | Web route | Access | Status |
|---|---|---|---|---:|---|
| Worksheet Tracking | `mnuWksTracking` | `FrmWorksheetTracking` | `/worksheet/tracking` | 24 | **Migrated** (phase 1) |
| Worksheet Send to A/C | `mnuWSToAC` | `FrmWksSendToAC` | `/worksheet/send-to-accounts` | 24 | **Migrated** (phase 1) |
| Worksheet Send to A/R | `mnuWSReturnCLM` | `FrmARRecFrCLM` | `/worksheet/send-to-ar` | 24 | **Migrated, reduced** (phase 1, see below) |
| Worksheet Enquiry | `mnuWSEnq` | `frmWorksheetEnq` (about 20,000 lines) | `/worksheet/enquiry` | 13 | **Migrated, read-only** (phase 2, see below) |
| Worksheet Registration | `mnuNewWorksheet` | `frmWorksheetRegGST2015` (22,253 code lines) | `/worksheet/registration` | 22 | Phase 3 (menu entry present, page not built) |
| Worksheet Adjustment | `mnuWSAdjust` | `frmWorksheetAdj2013` / `frmWorksheetAdjGST` (19,818 / 20,957 code lines) | `/worksheet/adjustment` | 23 | **Partly migrated** (phase 3: viewing and the Update button; Approve, notes and letters not yet, see below) |

## How the desktop wires these items (and what was decided)

- Only `mnuWSEnq` has a `_Click` handler in `mdiMain.frm` (`CheckAccess(13)`, opens `frmWorksheetEnq`). `mnuNewWorksheet`, `mnuWSAdjust` and
  `mnuWksTracking` have none, the Worksheet parent menu (`Claim`) is `Visible = 0` in the designer, and `mnuWSToAC` / `mnuWSReturnCLM` are hidden too.
- `Medix.bas` `SearchForm` (the scan-code dispatcher) opens Tracking for `LOW003` (`CheckAccess(24)`); its branches for Registration (`LOW001`, position 22) and
  Adjustment (`LOW002`, position 23) are commented out. Those positions are used here, because they are what the desktop last checked for those screens.
- Send to A/C and Send to A/R are never opened by any code (`.Show` is never called for `FrmWksSendToAC` / `FrmARRecFrCLM`). They are gated like their sibling, Tracking
  (position 24), as was done for the Case screens without a handler. `AccessRight.cs` and `access.ts` hold the values.
- Form versions: the project contains up to eight registration forms and five adjustment forms (`frmNewWorksheet…`, `frmWorksheetReg2012…`, `…GST`, `…GST2015`;
  `frmNewWSAdjust…`, `frmWorksheetAdj2012…`, `…2013`, `…GST`). The `…v2` / `…2010V2` / `…2015v2` forms are designer-only shells (no code). The newest registration form
  (`frmWorksheetRegGST2015`) hands over to `frmWorksheetAdj2013`, so that pair is the live flow; the older versions are listed in the redundancy report as removal candidates.

## What changed from the desktop, and why

| Desktop | Web | Why |
|---|---|---|
| Tracking and Send to A/C built a `WHERE` clause as text and ran it as SQL (Tracking through `dbo.WorksheetTracking`, which executes the text) | Typed parameters on `genisis.WorksheetTracking_Search` / `WorksheetAccounts_Search` / `WorksheetAr_Search` | SQL injection; the repository rule "never call a procedure that appends raw text" |
| Tracking "Ins Type" from/to read `txtINSCode1/2`, controls that do not exist (and the form has no `Option Explicit`), so the filter never applied | The filter works (`INSCode` between the two chosen types) | An obvious defect; the drop-downs were on screen but did nothing |
| Tracking group-company list: one entry per worksheet (a company repeated for each of its worksheets), loaded only after ticking a box | Distinct, sorted list loaded when a payor is chosen | Usability |
| Send to A/C grid wrote the bordx number into the Status column and left the Bordx No column empty | Status and Bordx No are separate columns | Defect |
| Send to A/C list repeated a worksheet once per hospital / member payment row (31,646 view rows for 31,208 worksheets), so totals doubled and a ticked repeat was sent twice (two remark lines) | One row per worksheet, showing its latest cheque dates | Defect found by the browser test (payor AC: 495 rows, 492 worksheets) |
| Ticking a bordxed (Tracking) or already sent (Send to A/C) worksheet was refused only in the grid | Also refused by the stored procedure (`Result = 2`) | Rules belong on the server |
| Nothing ticked after the confirmation: the desktop silently did nothing | "Please select at least one record to be updated!" before the confirmation | Usability |
| Update loops ran one statement per ticked row on up to four connections with no transaction | One transaction on the main connection (cross-database objects through `genisis.WS_*` / `Maint_*` synonyms); any refusal rolls everything back | Integrity |
| "Stop", "Check All", "Print" (report) buttons; list refilled one row at a time with `DoEvents` | Select-all header box, CSV export ("Print to File"), the whole result at once; at most 5,000 rows with a notice ("narrow the search") | Modern equivalents; the desktop loaded every row |
| `Format(txt, "mm/dd/yyyy")` string dates in SQL | `datetime` parameters; dates still typed and shown as `dd/mm/yyyy` | Safety |
| Scan code typed in the Tracking "Wks No" box (`LOC001`, `LOW003` …) opens another screen | Same shortcut for every code the web app knows (`lib/scanCodes.ts`); unmigrated screens are named in a message | Hidden interaction preserved (`SearchForm`) |
| The commented-out `CmdPrint_Click`, the hidden controls (`cmdPrint`, `lblTotal`, `Label22`, `txtNoofDay`) and the Stop button with its `intExit` / `DoEvents` loop | Not migrated | Dead code or replaced (see the redundancy report) |

### Behaviour kept exactly, including quirks

- **Tracking update** writes the same four places: `Claim` (pending date, status, user), `ClaimInvoice.CLMInvPending`, `CAS.CASPending = 'N'`
  and `CASCrossReference.CASPending = 'N'` — the case flag is cleared whichever status is chosen, as in `UpdateCLM`.
- **Send to A/C remarks** (`UpdateSentAC`): the line `dd/mm/yyyy         FILE  -  <case>-<version> SENT TO ACCOUNT ON <date>` is appended to the first
  remark page with room, chosen from the lengths of the six remark pages with the same thresholds (1,800) and order; the desktop never measured the sixth
  page, so its first rule only looks at page five. The remark line **and the whole page it is added to are trimmed and upper-cased** (`ChkStr`).
  The Listing screen (`CaseListing_Apply`) uses a different page order and is left as migrated.
- "0 - Both" in the token-status drop-down keeps status 0 and 1 only (rows with no status are excluded), as the desktop's SQL did; the default is Both.
- "No of Days (DOR - DClose)": a worksheet closed the day it was registered shows 1, not 0.
- "is empty" tests on a date column also match 1900-01-01, which is how `= ''` read a date column.

### Search layout: criteria in a panel on the right

Worksheet Tracking had about 40 criteria stacked in three full-width blocks, which put the results grid more than a screen below the top. Tracking and Send to A/C now use Case Tracking's
layout (`SearchLayout`, the same `ct-layout` styles): the main criteria, the actions and the results on the left, the other criteria in a sticky panel on the right that scrolls on its own, and
stacks under the results on a window narrower than 1,100 px. Nothing was removed or renamed; only the place changed, so every field, hint, validation and desktop behaviour is as before.

| Screen | Main part | "More filters" panel |
|---|---|---|
| Worksheet Tracking | Use Scan, Wks No, Payor, Claimability, Pending Status and its "is empty" tick, and the Update pending status block | 38 criteria in three groups: Worksheet (version, group company, plan, ins type, policy, member, I/C, bordx), Amounts (App Amt, Pay2M, Pay2H), Dates and diagnosis (DOA, DOD, DPO, first and final diagnosis), each from / to pair side by side with its "is empty" tick |
| Worksheet Send to A/C | Use Scan, Wks No, Payor, Date Sent from / to, Token Status, and the Send to accounts block | Wks Date, Wks Amt and Bordx No from / to |
| Worksheet Send to A/R | all four criteria (Use Scan, Case No, Date Sent from / to, Token Status) | none: there is nothing to put aside |

The results grid now shows on the first screen of Tracking (its top moved from below two screens of criteria to about 550 px). The browser suite checks that the panel holds exactly these criteria, that
the main part keeps the others, that the grid is on the first screen, that the panel scrolls and is sticky, that Send to A/R has no panel and that a narrow window stacks the panel under the results.

### Worksheet Send to A/R — reduced

`FrmARRecFrCLM` reads `CASOthers.CASDTC` and, on update, copies the invoice number, date and amount into `CASOthers.CASINVNo` / `InvDate` / `CASInvAmt`.
**Those four columns do not exist in this database** (and none of its 39,054 `CASOthers` rows has `CASDRC` or `CASRecStatus` set), so the desktop form
would fail on its first search here. The web screen uses the three columns that do exist: it lists case number, **Date Send to A/R** (`CASDRC`), **Passed By**
(`CASRecBy`) and status (`CASRecStatus`), and **Update** sets `CASDRC` = today, `CASRecStatus` = `1`, `CASRecBy` = the user. The "Date Returned to CLM" column and
the invoice copy are not available. `VIEW_ARReport` reads `CASDRC`, and `FrmAuditReviewReport` counts by `MONTH(CASDRC)`, so the date written is used.
If production turns out to have the four columns, the screen can be extended without changing its shape.

## Worksheet Enquiry (phase 2)

`frmWorksheetEnq` is a 20,000-line clone of the Adjustment editor: it keeps the whole editing code (`SaveClaim`, `UpdateMBMBalance`, letters, Word and Excel
automation) and hides the editing buttons in the designer (`cmdApprove`, `cmdUpdate`, `cmdEmail`, CN / DN Note, HOS CR / DR Note, Final LG, Exc Letter, Chq Letter are
`Visible = 0`). What a user can do is **Go, Search, Case Enq, MBM Enq, Export and Exit**. The web Enquiry migrates exactly that; the editing half belongs to Phase 3
(Adjustment) and the Case module (letters). Nothing on the page can change data.

Layout, as in the desktop: Case Num / Version / Clm Type / WS CLM / Ex-Gratia on top, six tabs (**Invoices & Diagnosis, Worksheet, Reimbursement Not Cover, Topup,
Remarks, LG Listing**) and the payment / address panel underneath.

| Shown | Read from |
|---|---|
| Case, member, policy, hospital, plan, claimability, balance limits | `genisis.WorksheetEnquiry_Header` over `VIEW_Claim_MBM_Case_worksheetAdjustment`, `PolicyStatus`, `AnnualLimit`, `MBMLifeTime` |
| Diagnoses (first and final, three each) | `DIAFirst`, `DIAFinal` |
| Invoices and the "not covered" items | `View_CAS_Invoice` (synonym `genisis.WS_CASInvoice`) |
| Benefit grid: limit, actual, approved, payable to hospital, due to / (from) member | `ClaimLimit`, `ClaimActual`, `ClaimApprove`, `ClaimHos`, `ClaimMember` |
| Summary rows under the grid | `Claim` (added up over the versions shown) |
| Payee, address, remarks, note, payment mode | `ClaimOthers` |
| Guarantee letters | `genisis.CaseAdj_LGList` (the Case Adjustment procedure, reused) |

The 29 grid rows map to the columns the desktop's save routines write (`saveActual`, `saveApprove`, `saveHos`, `saveMember`, `saveLimit`): row 1 Room & Board …
row 25 Emergency Sickness Treatment, 26 Others - No Co-Pay, 27 to 29 the service taxes. Amounts are stored as the text the grid showed (`1,200.00`, `-`, `(50.00)`),
so `genisis.Worksheet_Amount` parses them (the older `Util_SafeDecimal` read `1,200.00` as 0).

### The main decision: saved figures, not a recalculation

The desktop recalculated a single version's worksheet on every view (`getDatatoWS`, `getFinalTotal` and their helpers: about 3,000 lines of plan rules for approved = lower of
actual and limit, co-payment, excess, limit exceeded, top-up). The web Enquiry shows what was **saved** with the worksheet: the same tables the desktop's own Master
view reads, and the figures payment, bordx and the ASCII files use. Checked against the data: for 300 randomly chosen worksheets the actual amounts of the grid add up
to the total actual saved on the Claim row in 300 of 300, and the approved amounts to the saved total approved in 288 (the other 12 are worksheets whose approved total
the member's limit capped). An automated test repeats the check on 150 worksheets. The calculation engine is written in Phase 3, where Registration and Adjustment need it to save.

| Desktop | Web | Why |
|---|---|---|
| Version `n` is one worksheet; `Mn` adds versions 1 to n; `Master` adds them all | Same. The header is that of version n (the last version for Master); invoices are those of version n (every accepted invoice for Mn and Master) | Kept |
| Clm Type (Both / Cashless / Reimbursement) recomputed the grid from the filtered invoices | Filters the invoice list only; the grid shows the whole saved worksheet | The saved worksheet cannot be split by claim type |
| Master view (`getActual`) put `CLMPreHSAmt` in row 7 and skipped row 6; the save routines store it in row 6 and `CLMPreHosp` in row 7 | Follows the save routines | The amounts sit in the row they were saved from |
| Bal Lmt, Lifetime Lmt: live limits, with top-up balances added by about 300 lines of branches | Bal Lmt follows the same rules for members without a top-up (a "Q" member of a supplementary plan with limit status B / C and a dependant patient uses the covered person's limit); the top-up balance is shown on the Topup tab from the saved worksheet | Informational fields; the top-up figures are saved with the worksheet |
| "Per Dis. Bal Lmt" | Not shown | Its assignment is commented out in the desktop: it only ever showed 0 |
| Days/Mths column built from the plan's per-day rate text | The saved day counts (room and board, ICU, physician visits, kidney months, cash days, guardian days) | The plan text is not saved |
| Export wrote an Excel workbook (about 1,800 lines of automation) | CSV of the grid, its totals and the summary (opens in Excel); off for Master as in the desktop | Browser equivalent |
| Double-clicking a guarantee letter reprinted it through Word | The list is read-only; letters are issued and reprinted from Case Adjustment | Word automation is not migrated |
| `lstFollowUp` (follow-up list under LG Listing) | Not migrated | Never populated by the form |
| Search button opened `frmCASList` (raw SQL text) | Case Search dialog on `genisis.WorksheetEnquiry_Cases` (parameters; number, member, patient, I/C or policy; shows how many worksheets each case has) | Parameterised |
| Case Enq / MBM Enq filled the other form's number and pressed Show | Same: `/case/enquiry?cas=` and `/membership/enquiry?mbm=` (Membership Enquiry now reads the URL) | Kept |

Messages kept: "Record not found!" (no worksheet, or its case or member is missing: the desktop query joins them), "Please select a Case Number !", "Please select a
version nombor!", "GL is NOT allowed for this member" (shown after loading when the member may not have a guarantee letter), and the scan-code shortcut in Case Num.
For payor IB rows 16 and 24 are renamed Psychiatric and Maternity, as in the desktop. Bordx Ind is shown for payors MH and IB.

Endpoints (position 13): `GET api/worksheet/enquiry/cases?by=&q=` and `GET api/worksheet/enquiry/{case}?version=&claimType=`.


## Worksheet Adjustment (phase 3, partly migrated)

`frmWorksheetAdj2013` (22,719 lines) is the editing twin of the Enquiry form: the same header, six tabs and payment panel, with the buttons the Enquiry hides. The desktop
opens it from **Case > Case Review (Inpatient)** (`mnuWSInq`, position 5, listed as not required); the Worksheet > Worksheet Adjustment item (`mnuWSAdjust`) has no click
handler and is hidden, so it is gated like its siblings with position 23 (scan code LOW002). `frmWorksheetAdjGST` is the GST variant of the same form and is not opened by any menu.

| Button of the desktop form | Web | Notes |
|---|---|---|
| Go, Search, Case Enq, MBM Enq, Export, the grid, all tabs | **Migrated**: the Adjustment page is the Enquiry page | Read-only views are shared; the read endpoints accept position 13 (Enquiry) or 23 (Adjustment) |
| **Update** (`cmdUpdate_Click` / `SaveOtherInfo`) | **Migrated**: payment details, member's address, note and remarks of one version | Details below |
| Approve (`cmdApprove_Click`: `SaveClaim`, `saveActual` ... `saveLimit`, `UpdateMBMBalance`, `savePayMBMStatus`) | Not migrated | Needs the calculation engine with the plan limits and co-payment (see Phase 3 below) and writes the member's balances |
| Credit Note, Debit Note, Exc Letter (`cmdCredit_Click`, `cmdDebit_Click`, `cmdExcLetter_Click`) | **Migrated** (step 2): printable pages built from the same data | The Excel / Word templates are not in the source package; see "step 2" below |
| **Final LG** (`cmdFinalLG_Click`) | **Migrated** (step 3) | The existing guarantee letter dialog and code, with the worksheet's checks and marks; see "step 3" below |
| Email, Cheque letter, OMNI update | Not migrated | Outlook / Word automation or OMNI-only |

**Update.** It saves, for one numeric version, the last document date (only when one is given, so it cannot be cleared), pay-to, GP, bank, account number, payment mode,
attention, the member's three address lines, post code, city, state, the note, and the worksheet and credit/debit-note remarks, in one transaction
(`genisis.WorksheetAdjustment_UpdatePayment`; empty text is stored as empty text, as the desktop did; the remarks and address go to `ClaimOthers` only when the version has a row there).
The desktop's checks run in its order and only when the invoices of the version are paid to the member (`MBMHosStatus` = M, the pay-to of the last invoice): a bank without an account
number ("Please Enter Bank Account Number"), an account number without a bank ("Please Enter Bank Name"), then the address, post code, city and state ("Please Enter Member's Address",
"Please Enter Post Code", "Please Enter City", "Please Enter State"); then "Are you sure?" and "Record Save". The server repeats the checks, so they hold for any client.
Banks come from `BankAccounts` of the maintenance database (23 here), states from `STA`, payment modes are BY POST and BANK-IN. Text is held to the database column lengths.

Kept as the desktop had it, on purpose: there is **no check for a bordxed worksheet** (the desktop lets the payee details of a bordxed worksheet be changed), nothing records who
changed it (the desktop's last-updated user lines are commented out; the API log records it), and `CheckAccNo` (bank account digit check) is commented out in the desktop. Text is
trimmed before it is checked and saved.

Endpoints (position 23): `GET api/worksheet/adjustment/lookups` and `PUT api/worksheet/adjustment/{case}/{version}/payment`.

### Adjustment, step 2: credit and debit notes and the Excess letter

The desktop made these documents by filling Excel and Word **templates** (`CreditNote.xlt`, `DebitNote.xlt`, `DebitNoteCompany.xlt`, `EXCLetterMAS.DOT`, `ExcessLetterEtiqa.DOT`,
`MedixExcLetter.DOT`, `Final LG` letters) that are not in the source package. The web builds each document from the same data on a printable page of its own layout (the Print button
uses the browser's print, so it can be saved as PDF); the wording and layout of the desktop templates could not be copied.

| Button | Web | Data and rules |
|---|---|---|
| **Credit Note** (`cmdCredit_Click`) | Migrated | Enabled when Medix owes the member (amount due above zero). Shows the number the worksheet already has: credit numbers are given at registration, not here, so a credit note can be "(not numbered)", as in the desktop |
| **Debit Note** (`cmdDebit_Click`) | Migrated | Enabled when the member owes Medix. A worksheet with no debit note number gets the next one of its payor, `D` + payor + five digits (`DRSeq`, `GenerateDebitNo`), saved with today's date (`UpdateCreditDebit`); the number is taken in one atomic update, so two users cannot get the same one. A plan billed to the company (`PLNCompanyDN` = C) addresses the note to the group company |
| **Exc Letter** (`cmdExcLetter_Click`) | Migrated | Built from the case's excess record (`CAS_Excess`, written by the desktop's Approve), with the hospital's contact and fax, the patient's I/C, and for plans whose excess private type is 1 (MAS) the staff number, cost center and department. Lists the amounts that have a value, the limit exceeded, co-payment and underwriting excess, and the total (shown only above zero). Marks the case `tblRecord.ExcessLetter = 'Y'` |

**The amount due is the saved one.** The desktop worked it out live (`totalFMem`, a long expression over excess, co-payment, limit exceeded and under-writing excess). The web uses the
figures saved with the worksheet: the payable to the member when there is one, otherwise the excess. That agrees with the credit / debit numbers the desktop gave in about 94 per cent of
the versions that have one (credit numbers sit on a positive amount in 97 per cent, debit numbers on a negative one in 82 per cent); where the two differ the desktop's screen would have
enabled the other button. 9,096 saved versions have an amount due and no note number, which is normal: numbers are given when a worksheet is approved or by the separate Generate Credit/Debit
Note screen (`frmGenerateCNNote`, hidden, not required), neither of which is migrated.

**Deliberate differences.** A case with no excess record gets "There is no excess record for this case." and is not marked (the desktop opened an empty letter and marked the case anyway); a
debit note is refused for a worksheet that has nothing due from the member (the desktop only disabled the button); the notes' phone line is left out (the desktop's phone source is commented
out). The data of the notes and the letter can only be shown for worksheets whose case and member exist in the database; in the development database since the data was restored that is 31,199 of the 31,208 saved versions (see "Restored data").

### Adjustment, step 3: Final LG

Final LG (`cmdFinalLG_Click`) issues a guarantee letter for the worksheet's case. The letter itself is issued by the same code as Case Adjustment's guarantee letter
(`CaseDocumentsService.IssueGLAsync`, which now takes an optional worksheet version); what the worksheet adds is below. The dialog is the existing guarantee letter dialog
(`frmGLMenu`) pointed at the worksheet's endpoints, so its fields and its checks (`CaseGLRules.Validate`) are the same.

The checks, in the desktop's order, and what the user is asked:

1. Payor **AX** whose policy effective date is not its payor effective date: "Have you checked Take Over clause?" (No stops).
2. The member, then the patient name, must be on the worksheet ("Please Enter Membership Information", "Please Enter Patient Name").
3. The LG amount: for a worksheet paid to the hospital (H) it must be the worksheet's **final total payable to the hospital**, to the cent (the amount starts as that total); then the
   member's balance limit is tested: the amount may not be above the balance limit plus what this worksheet itself approved.
4. If either test fails the user is asked "Please check LG Amount, do you want to continue?" and **may go on** (Case Adjustment stops instead); otherwise "Are you sure?".

Issuing is one transaction: the case's other open letter is closed (not for Follow-up), a new LG number is taken or the one of the same purpose is re-issued, the LG row is written, the
version's invoices get the LG number (`UpdateInvoiceLG`) and the case is marked `tblRecord` `IsLGissued = 1`, `IsWSdone = 1`, `CaseProgress = 'FINAL GL Issued'`; then the member is sent the
mobile notification (best effort) and the letter is built. The server repeats every check except the amount warning, which is the user's to go on past, as in the desktop. Endpoints
(position 23): `GET api/worksheet/adjustment/gl-options`, `GET .../{case}/gl`, `POST .../{case}/{version}/final-lg/check`, `POST .../{case}/{version}/final-lg`.

What is not the same, or could not be checked:

- **The letter.** The desktop built it in the worksheet form's own Word routine (`funcwordLG` and `bookmarkLG`, about 2,600 lines, from a template that is not in the source package). The
  web builds it with the Case Adjustment guarantee letter (the same LG types, B for private-plan members, and fields); the two letters' wording was not compared field by field.
- **The limit test.** The desktop's own `CheckLimit` (about 800 lines: top-up, lifetime and per-disability branches) is approximated by the Case side's balance limit rule plus the
  worksheet's final approve. It has not been checked against the saved worksheets yet; the restored data (see "Restored data") makes that possible.
- **Data.** The case has to be registered with its member (the letter's context), which is true of about 1,700 cases here.

## Phase 3: worksheet calculation engine (steps 1 to 3 done)

Registration and Adjustment (`frmWorksheetRegGST2015`, `frmWorksheetAdj2013` / `frmWorksheetAdjGST`) cannot save a worksheet without the calculation the desktop runs
(`getDatatoWS`, `getFinalTotal` and their helpers, about 3,000 lines of plan rules). It is ported in steps, and **every step is checked against the worksheets the desktop
saved** before anything is built on it (the saved tables hold about 35,500 worksheet versions).

**Step 1, done: the actual amount of every row, added up from the invoices** (`genisis.WorksheetEngine_Actual`, `WorksheetEngineService.ActualAsync`; no endpoint yet).
The slot to invoice-column map was read out of the VB6 source by script (`ShowInvoice` puts each `View_CAS_Invoice` column into a list sub-item, `getDatatoWS` adds the
sub-items of each slot), not typed by hand. Result against the saved `ClaimActual` of all 35,495 versions: 99.96 to 100 per cent per slot (for example slot 1 Room & Board
is `CLMBoardAmt + CLMNewRBTax`, slot 20 is `CLMGuardianAmt + CLMNewLodgerTax`, slots 27 to 29 are the service-tax columns). `WorksheetEngineTests` repeats the check on a
sample of 400 versions on every run. The hospital part (the desktop's H1..H29, invoices with pay-to `H`) comes out of the same query.

**Step 2, done: approved, hospital and member amounts per row** (`WorksheetRules.Approve` / `Evaluate`, `genisis.WorksheetEngine_Limits`,
`WorksheetEngineService.EvaluateSavedAsync`). The desktop's `getDatatoWS` works the approved amount of a row out of its actual amount and its **limit text** with
`GetMin`: a numeric limit caps the amount, the word FULL lets all of it through, anything else (a dash, blank) approves nothing; a row the desktop flags "pay member"
(`AppStatus` Y, table `ClaimPayMBMStatus`) uses `GetMax` instead. The hospital column is the sum of the invoices paid to the hospital, and the member column is approved
minus hospital. All three rules are ported as written (including VB6's idea of "numeric": `1,000.00`, `.00`, `(50.00)`, `50-`; a percentage is not a number) and checked
against every saved worksheet, using the limit text saved with each version:

| Column | Saved versions | Agreement per row |
|---|---|---|
| Actual (step 1) | 35,495 | 99.96 to 100 per cent |
| Approved | 88,712 rows with an actual amount | 99.75 per cent overall; 99.5 per cent or better in every row except Room & Board 98.8, ICU 97.5, Monthly (kidney) 97.6 and Accident Post-Hospital row 14 99.3 |
| Hospital | 35,592 | 99.97 to 100 per cent |
| Member (approved minus hospital) | 35,592 | 99.83 to 100 per cent |

`WorksheetEngineOracleTests` repeats the whole evaluation on 400 versions on every run. What does not agree is mostly rows whose saved approved amount is above the limit text (about 1.2 per cent of Room & Board rows, 2.5 per cent of ICU rows): the desktop handles those outside this rule (limit-exceeded and annual-limit checks in CheckLimit) or the user overtyped the grid. That is the next step.
Two things the data cannot confirm: the pay-member flag is **not set in any saved worksheet**, so `GetMax` is covered by unit tests only; and for a negative actual amount the
desktop returns the limit when the amount is below it (ported as written, pinned by a test).

**Step 4 (limits from the plan): the data gap is closed.** The Registration form builds the limit text in `display_benefit`: for each grid row it reads the plan's benefit row from `BNF`
(plan, insured type, health type; `BNFRMPerDis` is the amount or the word FULL, `BNFClaimAbleAmount` the daily rate), and for Room & Board and ICU multiplies the rate by the days of the
(last) invoice; `getDatatoWS` then limits the days again by the plan's day limits and by the days already used by a linked case (`ClaimSameLimit`). This could not be checked while the
development database had benefit rows for only 126 plans; see "Restored data" and "Step 4" below.

**Step 3, done: the summary rows that follow from the grid** (`WorksheetRules.Final`, `genisis.WorksheetEngine_SummaryInputs`, `WorksheetEngineService.FinalSavedAsync`).
Probing all 31,199 saved `Claim` rows (the table is complete; only the case, member and plan tables were trimmed) showed which summary figures are plain arithmetic on figures
already ported, with the co-payment and the member's available limit (`CLMPreviousBalance`) as inputs:

| Row | Rule | Agreement over the saved versions |
|---|---|---|
| Total actual | sum of the actual column | 99.96 per cent |
| Total approved | sum of approved, less the co-payment, held to the available limit | 99.36 per cent (99.63 when the saved limit exceeded is used instead of deriving it) |
| Limit exceeded | the amount above the available limit, as a negative (zero when within it; a negative balance pays nothing) | 99.65 per cent |
| Final approve | equal to the total approved | saved final equals saved total approved in 99.99 per cent; 99.36 as the engine computes it |
| Excess | sum of the negative member amounts (what the member owes) | 99.99 per cent |
| Payable to member | sum of the positive member amounts | 97.4 per cent: kept as a figure, not relied on |

`WorksheetEngineFinalTests` repeats it on 600 versions (97 per cent agree on total approved and final, 99.3 on limit exceeded, 98.7 on excess: small differences in a grid compound
across a version). The residual 0.4 per cent are full rejections, reimbursement-flagged worksheets and hand-edited ones.

What the data does **not** support, and why these are not ported yet:

- **Ex-gratia, processing fee, major medical fee and top-up amounts are zero in every saved worksheet**, so there is nothing to check a port against; the desktop's branches for them
  stay unported until a worksheet that uses them is available.
- **Payable to hospital and the bordx amount** equal "final minus payable to member" and "final" only in 96 to 97 per cent of versions. The misses are adjustment (credit) worksheets, where
  the bordx amount follows the hospital figure and does not take the co-payment off (for example AJ00000033, versions 7 and 8). Settling that needs the rest of `getFinalTotal`
  (about 700 more lines) and the adjustment rules, so it is left for the Approve step.
- **The co-payment itself.** `getFinalTotal` takes it as (approved minus Room & Board minus taxes) times the plan's co-payment percentage, then nudges the cents for invoices from
  April 2008 on. That needs the member's plan, which the restored data now has for almost every saved version (see "Restored data"), so porting it and checking it against the saved `CLMCoPayRB` is the next engine step; until then the co-payment is an input.

### Restored data

The development `HISDB` had been trimmed to save space, so the case, member and plan tables covered only a small part of the 31,208 saved worksheet versions. A full copy was restored beside it as
`HISDB_actual_backup` (138 GB; `HISDB` is 7 GB), and what the Worksheet module needs was copied across, **adding only rows that were missing and never changing an existing row**, in one
transaction, with the encryption triggers of `MBM` / `MBMCoveredPersons` / `MBMOthersTwo` / `PLN` switched off while copying (so the already-encrypted columns arrive as they are) and on again after.
A verified backup of `HISDB` was taken first. The copy:

| Table | Rows added | Why |
|---|---|---|
| `BNF` | 376,897 (now 377,994 rows, 19,725 plans; was 126 plans) | the plan benefit rows the limits come from |
| `PLN` | 1 | plan master |
| `CAS` | 14,488 (now 17,080; saved worksheets' cases: 16,205 of 16,206) | the cases of the saved worksheets |
| `MBM`, `MBMOthers`, `MBMOthersTwo`, `MBMCoveredPersons` | 2,081 / 4,523 / 217 / 4,062 | their members and covered persons |
| `CASRemarks`, `CASRemarksTwo`, `CASRemarkThree`, `CASOthers`, `CASOthersTwo`, `CAS_UD`, `DIAFirst`, `DIAFinal` | about 12,650 each | the case's remarks, details (including the ASCII export flag) and diagnoses |

Saved versions visible to Enquiry and Adjustment went from 1,950 to 31,199 of 31,208, and versions whose member plan has benefit rows from 14 to over 31,000. `HISMaintenance` was not restored, so
its per-case tables (`CASLG`, `tblRecord`, `CASCrossReference`) still cover only part of the cases; the screens cope with a missing row, and three Case database tests that assumed a complete set now
make the missing cross-reference rows inside their own rolled-back session.

### Correction: the accuracy figures below were measured on too small a sample

The figures for steps 4 to 6 were first measured on the 31,208 worksheet versions of the trimmed `HISWorkSheet` (16,210 cases). After `HISWorkSheet_backup` was restored, 25,497 more cases were
brought in (see "More data" below: 70,426 versions, 91,108 invoices) and the same checks were run again. Some held and some did not:

| Check | First measured (31,208 versions) | On 70,426 versions |
|---|---|---|
| Plan limits on rows with an amount: plain / case dependent / Others / Room & Board / tax | 97.0 / 90.1 / 97.8 / 97.7 / 96.3 per cent | 96.5 / 80.2 / 94.2 / 94.6 / 93.7 per cent |
| Co-payment: how many saved co-payments the plan's percentage reproduces | 94.7 per cent (162 of 171) | **35 per cent (120 of 342)**; of the 145 the plan gives one for, 83 per cent are right |
| The limit before the worksheet | 76.7 per cent | 68.9 per cent |
| A worksheet from the invoices and the plan alone: approved total before the limit / total approved | 77.9 / 79.0 per cent | **68.0 / 68.7 per cent** (819 versions) |
| The summary rows from saved limits, co-payment and balance: total approved / limit exceeded | 97 / 99.4 per cent | 92.3 / 98.8 per cent |

**Why the co-payment dropped.** The first sample was mostly payors whose co-payment is the plan's co-insurance percentage. The larger one has many more of other kinds. On 888 saved co-payments only 29
per cent are explained by `PLNCoINSPerc`. The desktop has at least three mechanisms (read from `frmWorksheetRegGST2015`): the **Room & Board co-payment** (plan `CoPaymentInd`, `PLNCoPayPerc`):
(approved total less approved Room & Board, less the MRI share for plan families 2 and 3 and new SM plans, less row 28, and less the tax for major-medical plans) times the percentage, only when the room
charged exceeds the plan's limit, with a cent adjustment for invoices from April 2008; the **co-insurance** (`PLNCoIns` = Y, `PLNCoInsInd` 1, 2 or other, percentage from `AnnualLimit.AnnCoInsPerc`),
whose result goes to the **Ex-Gratia row** (capped at 3,000), not the co-payment row; and a **flat share** that the saved worksheets show for some payors (TK 20 per cent of the approved total for 40 per cent of the
sample, ST 10, AN 25, AJ 10, AX 10, XL about 19) and whose source is **not** in any of the plan, payor (`PAY.PAYCoPayment`), annual limit, member (`MBMCoPayGL`) or `MBMOthersTwo.MBMCoPayRB` columns that
were searched. Finding where TK's 20 per cent comes from is the next step for the co-payment.

### More data: `HISWorkSheet_backup`

`HISWorkSheet_backup` (6.7 GB; the working `HISWorkSheet` was 365 MB) has 1,120,736 worksheet versions and 1,434,634 invoices for 639,214 cases. It has the same tables, columns and keys as the working one,
and every row of the working one is in it, so rows were **added only where missing, never changed**, in one transaction, after a verified backup of each database
(`HISWorkSheet_before_worksheet_data_restore.bak`, `HISDB_before_second_worksheet_data_restore.bak` in SQL Server's backup folder), with the four GST triggers off while copying and on again after.

Which cases: every case with an invoice that is not yet in a worksheet or has no worksheet at all and that the full `HISDB` knows (7,380), plus a repeatable one in thirty-two sample of the worksheet cases
(18,140): 25,497 cases. Their worksheet and invoice rows came across, then their case, member, covered-person and satellite rows into `HISDB` (25,015 cases, 17,670 members and the rest).

| | Before | After |
|---|---|---|
| Worksheet versions | 31,208 | 70,426 (70,416 visible to Enquiry and Adjustment) |
| Invoices | 39,684 | 91,108 |
| Invoices not yet in a worksheet | 170 | 8,310 |
| Cases that have an invoice but no worksheet (Registration candidates) | 0 | 638 (632 with case and member data) |
| Cases in `HISDB.CAS` | 17,080 | 42,095 |

`HISMaintenance` is still trimmed.

### Step 4: limits the plan fixes (done for the rows the saved data confirms)

`genisis.WorksheetEngine_PlanBenefits` returns the benefit rows of the patient's plan (the covered person's own plan for a dependant that has one), insured type and health type that are in force on
the admission date; `PlanLimitRules.Derive` turns them into the limit text of each row. Measured against the limit text the desktop saved, over the 31,190 versions that have plan data:

| Rows | Rule | Agreement |
|---|---|---|
| Hospital supplies, theatre, pre-hospital (2), surgical, anaesthetist, pre-surgical, physician's visit, accident, ambulance, outpatient, organ (codes 103, 104, 201 to 204, 301, 302, 401 to 403, 501) | the plan's FULL or amount, as it stands | 93 to 97 per cent each; 96.6 per cent in the oracle test's sample (6,200 of 6,419 rows) |
| Room & Board | daily rate times the days of the stay, held to the plan's days; no days means no limit ("-") | 97.7 per cent in the sample (212 of 217) |
| ICU | the same with its own days; no days gives 0.00 | 96.3 per cent (156 of 162) |
| Service tax on Room & Board | 6 per cent of the Room & Board limit | 96.3 per cent (209 of 217); 98.6 per cent over all versions |

Which row of the benefit table was in force made no difference: the latest row up to the admission date and the latest row of all agree on every version. `WorksheetPlanLimitsOracleTests`
repeats the comparison on every run, on a sample of about one version in a hundred (the figures in the tables are from larger samples; the sample is small so that the suite stays fast, since the oracles read the same big tables the Tracking reports do).

**The rows whose limit depends on the case.** For post-hospitalisation (303), kidney and cancer (404), guardian (701), medical report (801), admin (803) and home nursing (804) the plain rule agreed
on only 4 to 66 per cent of all rows, but that was because the desktop shows "-" on a row that has no amount (nothing can be approved on it anyway). On the rows that **have an amount** the saved
limit is the plan's FULL / amount in 90 per cent (100 of 111 in the oracle's sample), so those rows are derived only when the row has an amount. "Others" (901) is the plan's expenses that are not
covered: with an amount in it the desktop saves 0.00 or "-" whatever the plan says, which approve the same, and the oracle compares them by that effect (268 of 274, 97.8 per cent).

| Rows that have an amount | Agreement in the oracle's sample |
|---|---|
| Plain rows | 97.0 per cent (1,076 of 1,109) |
| Post-hospitalisation, kidney / cancer, guardian, medical report, admin, home nursing | 90.1 per cent (100 of 111) |
| Others (same effect: 0.00 or "-") | 97.8 per cent (268 of 274) |
| Room & Board | 97.7 per cent (211 of 216) |
| Service tax on Room & Board | 96.3 per cent (209 of 217) |

**Not derived yet:** the post-hospital / medical-expenses row 304 (mixed in the data), the tax rows of ICU and lodger, the kidney and cancer balances of members on a lifetime plan (they come from the
member's own balances, `MBMBnfLmt`), the day caps from a linked case (`ClaimSameLimit`), and the days of the stay taken from the invoices (the oracle uses the days the desktop saved).

### Step 5: co-payment (co-insurance done)

`genisis.WorksheetEngine_PlanTerms` reads the patient's plan row (`PLN`, by plan, payor and health type) and `CoPaymentRules.CoInsurance` takes the plan's percentage (`PLNCoINSPerc`, when
`PLNCoInsSign` is Y) of the approved total, to the cent, rounding half away from zero. **It is right for 83 per cent of the versions where the plan gives a percentage, but it covers only about a third of the saved co-payments** (35 per cent of
342 in the test's sample, 29 per cent of 888 in a wider one); see "Correction" above for the other mechanisms. The ratios in the data are plan specific: 5, 10, 15, 20, 25 and 30 per cent.

**Not ported yet:** the Room & Board co-payment, the co-insurance that goes to the Ex-Gratia row, the flat shares of some payors (source not found), and the cent adjustment for invoices from April 2008.

### Step 5b: the limit before the worksheet

`genisis.WorksheetEngine_BalanceLimit` reads the patient's available limit the way the Registration screen does: whose limit depends on the plan's annual limit type
(`AnnualLimit.SuppLimitStatus`, the latest row up to the admission date) — the member's (`MBM.MBMAvailableLimit`) for the principal or a patient with cover 00, and for a dependant the first covered
person's with type B or the patient's own with type C (`MBMCoveredPersons.MBMCAvailableLimit`). Approving a worksheet takes its approved amount off that limit, so the limit before it is the
available limit **plus what the case has already approved** (`BalanceLimitRules.PreviousBalance`). Measured on each patient's latest case, to the cent against the saved `CLMPreviousBalance`:

| Patient | Reproduced |
|---|---|
| Principal or main patient | 7,846 of 10,322 (76 per cent) |
| Dependant, limit type C | 1,862 of 2,241 (83 per cent) |
| Dependant, limit type B | 35 of 41 (85 per cent) |
| Oracle test's sample, first data | 1,710 of 2,229 (76.7 per cent) |
| Oracle test's sample, 70,426 versions | 917 of 1,330 (68.9 per cent) |

The rest are limits changed after the claim (renewal, adjustment, an amount approved but not taken off), which the rule does not model. Dependants of plans with no annual limit row (421) have
no balance to read; they fall back to the member's. The top-up policy's balances are not ported.

### Step 6: a worksheet from the invoices and the plan alone

`WorksheetCalculator.CalculateAsync` puts the steps together the way Registration does when it opens a worksheet: actual amounts from the invoices, the days of the stay from the invoices, limits
from the plan's benefit rows, the co-insurance, the available limit, then the rows and the summary. Nothing comes from the saved worksheet. The plan is chosen as the desktop chooses it: a
take-over plan if the case has one; otherwise, for a member with a new plan after renewal, the current plan (rows up to the payor effective date) when the patient was admitted on or before
that date and the new plan (rows up to the new policy effective date) after it; otherwise the dependant's own plan, otherwise the member's.

**The days of the stay** are the sum over the version's invoices of `CLMBoardDay` and `CLMICUDay` (fractional days such as 7.5 exist; the first version of this step read them as whole
numbers and lost those rows). The saved room days equal that sum for 98.6 per cent of versions, the ICU days for 99.4 per cent.

**What the desktop shows when the plan has benefit rows but none for a benefit** (read from the saved worksheets, rows with an amount only): FULL for hospital supplies (276 of 276 such
rows), anaesthetist (39 of 39), physician visits (89 of 89) and accident (115 of 115); 50.00 for the medical report (35 of 35); "-" (not covered) for the others, which get no limit.

Measured over 367 saved versions (the latest version of each patient's latest case), to the cent:

| Figure | Reproduced |
|---|---|
| Approved total before the available limit (saved total approved less limit exceeded) | 286 (77.9 per cent) |
| Total approved | 290 (79.0 per cent) |

(On the larger data, 819 versions: 557 (68.0 per cent) and 563 (68.7 per cent); see "Correction".)

(Before the days and the missing-row defaults: 68.8 and 70.5 per cent over 1,130 versions.) The breakdown the test prints shows where the rest goes: the co-payment is right for 99.7 per cent of
the versions, the approved total follows whenever the limits and the co-payment are right (286 of 287), and the limits are the whole gap (the plan gives the same approved sum as the saved limits for
78 per cent). What is left of it is a long tail: ICU and lodger tax rows, row 304, benefits whose limit the plan states as a percentage, and rows the desktop fills from rules in `getDatatoWS`
that are not ported. A calculation therefore carries notes that name every row that has an amount and no limit.

### Worksheet Registration: the calculation page

`GET /api/worksheet/calculation/{case}?version=` (access position 22 or 23) and the **Worksheet Registration** page: enter a case, see its worksheet calculated from the invoices and the plan
row by row (Benefits, Limit from the plan, Limit as saved, Actual, Approved, Payable to Hosp, Due to/(frm) Mem), the summary beside what the desktop saved (a figure that differs by a cent or more is in
bold, and so is a limit whose effect differs from the saved one), the days summed from the invoices and the notes. It writes nothing. Page text: "Entering the invoices of a new worksheet and
saving it are not available yet, so nothing is written here."

**Why a new worksheet cannot be registered yet.** Registration does not create invoices: it works on the invoices of the case, and opens the separate invoice form (`frmInvoice`, which is not
on the Required list and is not migrated) to enter them; its own save writes the worksheet tables. Every invoice in the development database already belongs to a saved worksheet
(`INVWSversion` is set on all 39,684), so there is no unregistered invoice to calculate or save, and nothing to test registering a new worksheet against but the saved rows. Registering a
worksheet needs invoices to exist (entered in the desktop, or by an invoice screen once one is migrated), the calculation (done, 69 per cent) and the save below.

### Approve: what the desktop does, and what is migrated

`cmdApprove_Click` of `frmWorksheetAdj2013` (and the save of Registration) checks, in this order, and stops at the first that fails: case number ("Please enter Case Number!"); a later version
exists ("This worksheet cannot be amended"); no member ("Please select a record before proceeding!"); no invoice ("Worksheet cannot be saved without any invoice!"); no final diagnosis ("Final
Diagnosis cannot be saved without any invoice!"); no worksheet status ("Please Select Worksheet Status!"); hospital-paid amount differs from the final hospital total ("Please verify Payable to
Hospital amount"); the member-paid bank, account, address, post code, city and state checks (ported for Update); the bordx indicator for MH / IB cases ("Please Enter Bordx Indicator"); then
"Are you sure?". In one transaction it then runs: `SaveClaim("A")` (228 lines, the version row of `Claim` with its 141 columns), `SaveClaimRemarks`, `saveLimit`, `saveActual`,
`saveApprove`, `saveHos`, `saveMember`, `savePayMBMStatus` (935 lines), `SameLimitUndExcess`, the excess letter (`saveExcessLetter`, 512 lines), `UpdateInvoice`,
`UpdateCasReserve`, `UpdateMBMBalance` (606 lines: the available limit), `UpdateMBMBnfBal`, `UpdateMBMLifeTimeBal`, `UpdateCASTakeOverLmt`, `UpdateClaimTopup`, `HCreditDebit`, and
`tblRecord` (`IsWSdone`, `CaseProgress` = GL Issued); then "Record Save".

**Migrated:** the calculation the grid tables (`ClaimLimit`, `ClaimActual`, `ClaimApprove`, `ClaimHos`, `ClaimMember`) come from; the pieces that already existed (Update's payment and address
checks, Final LG's hospital-amount check, the Excess letter read, the notes and letters). **Not migrated:** every write above. Approve changes the member's available limit and the numbers
other worksheets and payments read, so it will not be ported until each write can be checked: the plan is to regenerate the saved rows of a version from the engine and compare them column by column
with the saved ones (as the limit, co-payment and balance steps are), table by table, before any of it writes to a real database.

Next: the remaining plan rules (percentage limits, ICU and lodger tax, row 304), the Room & Board co-payment, then the save procedures in the order above, each verified by regenerating saved rows.

## Code map

| Layer | Files |
|---|---|
| SQL | `sql/main/230_worksheet_tracking.sql`, `240_worksheet_send_to_accounts.sql`, `250_worksheet_send_to_ar.sql`; synonyms `genisis.WS_WKSTracking` (`VIEW_WKSTrackingNew`) and `genisis.WS_WKS2AC` (`VIEW_WKS2AC`) in `000_setup.sql`; Enquiry: `260_worksheet_enquiry.sql`, `270_worksheet_engine.sql`, `280_worksheet_adjustment.sql`, `290_worksheet_documents.sql`, `300_worksheet_final_lg.sql`, `310_worksheet_plan_limits.sql`, `320_worksheet_plan_terms.sql`, `330_worksheet_balance_limit.sql`, `340_worksheet_stay_days.sql`, `350_worksheet_calculation.sql` and the synonyms `WS_ClaimActual`, `WS_ClaimHos`, `WS_ClaimMember`, `WS_ClaimLimit`, `WS_ClaimOthers`, `WS_CASInvoice` |
| API | `Genisis.Api/Worksheet/{Tracking,SendToAccounts,SendToAr,Lookups,Enquiry,Engine,Adjustment}/*.cs` (models, FluentValidation validators, rules, `I…Repository` + repository, service), `Worksheet/WorksheetCommon.cs`, `Worksheet/WorksheetServiceCollectionExtensions.cs`; thin controllers `WorksheetTrackingController`, `WorksheetSendToAccountsController`, `WorksheetSendToArController`, `WorksheetEnquiryController`, `WorksheetAdjustmentController`, `WorksheetLookupsController` |
| UI | `GenisisUI/src/pages/worksheet/` — `WorksheetTrackingPage`, `WorksheetSendToAccountsPage`, `WorksheetSendToArPage`, `worksheetRules.ts` (pure rules, form models, Yup schemas, query mapping), `useTickList.ts`, `useWorksheetLookups.ts`, `RangeFields.tsx`; `WorksheetEnquiryPage`, `EnquiryTabs.tsx`, `EnquiryCaseDialog.tsx`, `enquiryRules.ts`; `WorksheetAdjustmentPage`, `AdjustmentEditor.tsx`, `adjustmentRules.ts`; `lib/scanCodes.ts` |
| Tests | `WorksheetScreensTests.cs` (rules, validators, services with fakes), `WorksheetDatabaseTests.cs` (real databases, every write rolled back), the architecture tests (extended to the new namespaces), `WorksheetEnquiryTests.cs`, `WorksheetAdjustmentTests.cs`, `worksheetRules.test.ts`, `enquiryRules.test.ts`, `adjustmentRules.test.ts`, `scanCodes.test.ts`, browser suites `e2e/worksheet.mjs`, `e2e/worksheet-enquiry.mjs` and `e2e/worksheet-adjustment.mjs` |

Endpoints (all need the access position in the table above): `GET api/worksheet/tracking/search`, `GET api/worksheet/tracking/group-companies`,
`POST api/worksheet/tracking/update`, `GET api/worksheet/send-to-accounts/search`, `POST api/worksheet/send-to-accounts/send`,
`GET api/worksheet/send-to-ar/search`, `POST api/worksheet/send-to-ar/send`, `GET api/worksheet/lookups/{payors,insured-types}`.

## Deployment note

Run the regenerated `sql/genisis-main.sql` on the main database (it only creates or replaces `genisis.*` objects). `apiuser` needs nothing new: the procedures are in the
`genisis` schema, which it already has `EXECUTE` on, and the views they read are in databases it can already read.

## Verification (phases 1 to 3)

`dotnet build -c Release -warnaserror` clean; `dotnet test` 634 of 634 passed on the merged branch, which now includes the Membership screens of the team branch (269 of the tests are Worksheet tests; the Upload Bordx tests need the `UploadDb`
staging database, see `sql/hosting/create-upload-db.sql`); UI lint, Prettier and typecheck clean and 131 unit tests passed; browser suites 247 checks passed in all, no console or network
errors (the four Worksheet suites are 148 of them: Tracking / Send to A/C / Send to A/R 51, Enquiry 37, Adjustment 42, Registration 18; Invoice Processing 15 besides); every new procedure was also run as `apiuser` (login impersonation,
writes rolled back). The Enquiry grid and the calculation engine are checked against the figures saved on the Claim rows (see "The main decision" and the Phase 3 tables).
