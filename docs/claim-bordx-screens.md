# Claim Bordx screens

Menu path: **Claim Bordx** (legacy `mdiMain.frm`, top menu `mnuFinance`'s sibling in the Claim Bordx group). This page covers what is migrated; the rest of the menu is in [remaining-work.md](remaining-work.md).

## What is built

| Menu path | Legacy form | Web route | API | Status |
|---|---|---|---|---|
| Claim Bordx > Worksheet > Insert Worksheet | `FrmWksBordx` ("Select Worksheets for Bordx") | `/claim-bordx/insert-worksheet` | `/api/case/listing` | The same form Case > Inpatient Case Listing opens (`mnuGenerateBordx_Click` and `mnuAppCase_Click` both show `FrmWksBordx`), migrated with the Case module; this entry is a second door to it behind position 15 |
| Claim Bordx > Worksheet > Remove Worksheet | `FrmBordxRevise` ("Bordereaux Revise") | `/claim-bordx/remove-worksheet` | `/api/claim-bordx/revise` | Migrated (below) |
| Claim Bordx > Print Recovery | `frmGenerateRecovery` ("Generate Recovery Listing") | `/claim-bordx/print-recovery` | `/api/claim-bordx/recovery` | Migrated (below) |
| Claim Bordx > Monitoring > Outstanding Bordx | `FrmOSBordx` ("Outstanding Bordx") | `/claim-bordx/outstanding-bordx` | `/api/claim-bordx/outstanding` | Migrated (below) |
| Claim Bordx > Monitoring > Bordx Control | `FrmBordxControl` ("Bordx Control") | `/claim-bordx/bordx-control` | `/api/claim-bordx/control` | Migrated (below) |
| Claim Bordx > Payment Advice > Hospital Report | `frmPayAdviceHosRpt` ("Payment Advice- By Hospital") | `/claim-bordx/hospital-report` | `/api/claim-bordx/hospital-report` | Migrated (below), position 15 |
| Claim Bordx > Payment Advice > Generate Bordx No | `frmPayAdviceHosp` ("Payment Advice - Generate Bordx") | `/claim-bordx/payment-advice-bordx` | `/api/claim-bordx/payment-advice-bordx` | Migrated (below), position 15 |
| Claim Bordx > Print Bordx | `FrmPrintBordx` ("Print Bordx") | `/claim-bordx/print-bordx` | `/api/claim-bordx/print-bordx` | Migrated (below), position 17 |
| Claim Bordx > Update Bordx | `frmUpdateWS2Bordx` ("Update Bordx") | `/claim-bordx/update-bordx` | `/api/claim-bordx/update-bordx` | Migrated (below) |
| Claim Bordx > Storage > Claim files | `frmStorageEntry` ("Storage Entry") | `/claim-bordx/storage-claim-files` | `/api/claim-bordx/storage-entry` (writes; the lists are the Storage Enquiry's) | Migrated (below) |
| Claim Bordx > Storage > Document | `frmStorageDoc` ("Storage / Cartons") | `/claim-bordx/storage-document` | `/api/claim-bordx/storage-documents` | Migrated (below) |
| Claim Bordx > Storage > Enquiry | `frmStorageEnq` ("Storage Enquiry") | `/claim-bordx/storage-enquiry` | `/api/claim-bordx/storage` | Migrated (below) |
| Claim Bordx > Bor Delivery > Send to Payor | `FrmSendtoPayor` ("Bordx Delivery") | `/claim-bordx/send-to-payor` | `/api/claim-bordx/delivery/to-payor` | Migrated |
| Claim Bordx > Bor Delivery > Returned From Payor | `FrmReturnPayor` ("Bordx Delivery") | `/claim-bordx/returned-from-payor` | `/api/claim-bordx/delivery/from-payor` | Migrated (form chosen by name, see "Assumptions") |
| Claim Bordx > Bor Delivery > Returned Fr Payor (Not App) | `FrmReturnPayorCase` ("Bordx Delivery") | `/claim-bordx/returned-not-approved` | `/api/claim-bordx/return-case` | Migrated (below) |
| Claim Bordx > Bor Delivery > Send to A/C | `FrmSendtoAC` ("Bordx Sent to A/C") | `/claim-bordx/send-to-accounts` | `/api/claim-bordx/delivery/to-accounts` | Migrated |
| Claim Bordx > Bordx Approval | `FrmBordxApproval` | `/claim-bordx/approval` | `/api/claim-bordx/delivery/approval` | Migrated |

The other Claim Bordx items are listed in the sidebar, disabled, until they are built (see [remaining-work.md](remaining-work.md)).

## Access

`Access.ClaimBordx` / `AccessRight.ClaimBordx` = **position 15**, the position `mnuGenerateBordx_Click` checks (Print Bordx checks 17). None of the Bor Delivery, Monitoring, Approval or Storage menu controls has a click handler in `mdiMain.frm`, so the desktop never opened them from the menu; gating them like the first item of the menu is a **decision**, recorded here and in `AccessRight.cs`. A user without position 15 sees "Access Denied!" on the pages and gets 403 from the API (browser suite).

## How the four screens work

The four desktop forms are copies of one another: the same criteria block, the same checkbox list, the same "Update" flow. They are one web page (`BordxDeliveryPage`, driven by `bordxDeliveryRules.ts`), one service (`BordxDeliveryService`), one controller (`ClaimBordxDeliveryController`, route `api/claim-bordx/delivery/{kind}` where `kind` is `to-payor`, `to-accounts`, `from-payor` or `approval`) and two procedures (`genisis.BordxDelivery_Search`, `genisis.BordxDelivery_Send`, file `sql/main/400_claim_bordx_delivery.sql`).

| | Send to Payor | Returned From Payor | Send to A/C | Bordx Approval |
|---|---|---|---|---|
| Criteria | Payor, scan or range of bordx no, bordx date, bordx amount, A/C month and year | the same | Payor, scan or range of bordx no, bordx date, bordx amount, **date sent to accounts** | Payor, scan or range of bordx no, bordx date, bordx amount |
| Status drop-down | Not Sent / Sent / Both (`BordxPAYToStatus`) | Not Returned / Returned / Both (`BordxPAYDOCFRStatus`) | Not Sent / Sent / Both (`BordxSendtoACStatus`) | Not Approved / Approved / Both (`BordxAppStatus`) |
| Only lists | every bordx | bordx **already sent to accounts** (`BordxSendtoACStatus = 1`) | every bordx | every bordx |
| Update writes | `BordxPAYSentDate`, `BordxPAYToStatus = '1'`, `BordxPAYSentBy` | `BordxPAYRecDocDate`, `BordxPAYDOCFRStatus = '1'`, `BordxPAYRecDocBy`, **and** `Claim.CLMAppStatus = 'Y'` on the bordx's claims whose status is `N`, blank or NULL | `BordxSendACDate`, `BordxSendtoACStatus = 1`, `BordxSendACBy` | `BordxAppDate`, `BordxAppStatus = 1`, `BordxAppBy` |
| Date | typed ("Date") | typed ("Date") | typed ("Date") | none: the day of the update |
| List columns | Payor, Bordx No, Bordx Date, Bordx Amount, Bordx Status, Sent to Payor Status, To Payor, Sent By, A/C Period | Payor, Bordx No, Bordx Date, Bordx Amount, Bordx Status, Returned Fr Payor Status, Fr Payor, A/C Period | Payor, Bordx No, Bordx Date, Bordx Amount, Bordx Status, To Payor, Fr Payor, To MNRB, Fr MNRB, To A/C, Sent to AC Status, Sent By, A/C Period | Payor, Bordx No, Bordx Date, Bordx Amount, Bordx Status, Approved Status, Approved By, Approved Date |

### Behaviour kept from the desktop

- **Use Scan** starts ticked. In a scan the payor is checked first (`Please select a Payor`), then the bordx number (`Please Enter Bordx No`), one message at a time; each scan **adds** the bordx to the list and clears the number; scanning one that is already listed says `Data has been uploaded!`; a range search **replaces** the list.
- `No record found!` when nothing matches. The status drop-down starts on the "not done" entry.
- A bordx whose step is already done cannot be ticked: `Record has been Updated!` (the database refuses it too).
- **Update**: nothing ticked is `Please select at least one record to be updated!`, then (except Bordx Approval) a missing date is `Please Enter in date`, then `Do you want to update the record?`; success is `Record updated...`; after a range search the list is searched again.
- The A/C period criteria are compared **as text**, as the desktop's `'10' >= '3'` string comparison did (the columns are `varchar`). Bordx date and amount ranges are inclusive. A range end is ignored in a scan.
- The update of the ticked bordx is **one transaction**: a bordx that is gone or was already done stops the whole update and nothing is saved.

### Deliberate differences

| Desktop | Web | Why |
|---|---|---|
| The criteria were assembled into a `strAppend` text and run by `Bordx_Sent2Payor`, `Bordx_ReturnPayor` and `Bordx_Sent2AC` through `sp_executesql`; the update was text too | Typed parameters, `genisis.BordxDelivery_Search` / `_Send` | SQL injection and quoting bugs; the legacy procedures stay in the database for the desktop (not called by the web) |
| Returned From Payor read `VIEW_BordxReturn_Pay`, a view that repeats a bordx once per claim state and shows the **claim's** `BordxPAYRecDocDate` | One row per bordx, showing the bordx's own `BordxPAYRecDocDate` | The update writes the bordx column, so the claim's date would never show the date just recorded |
| The list order was the table's | By bordx number | A stable order |
| `Stop` button and `intExit` flag, `Check All` button | Removed; the grid's select-all and request cancellation | Interrupt plumbing of the single-threaded VB6 loop |
| "Print to File" exported the list to Excel | CSV download, the same columns | No Excel automation |
| Status columns showed `0` / `1` | `Not Sent` / `Sent` (and `Not Returned` / `Returned`, `Not Approved` / `Approved`) | The wording of the drop-down |
| The A/C period showed `/` when empty | Blank | Cosmetic |

### Assumptions the business should confirm

- **Returned From Payor** is `FrmReturnPayor`. A second form, `frmReturnFrPayor` ("Return from Payor", 2,548 lines), is in the source and no menu opens either of them. `FrmReturnPayor` was chosen because it is the same screen family (its caption, criteria and list match Send to Payor); if the business means the larger form, this screen needs redoing.
- Position 15 for the items without a click handler (above).
- A bordx has to be **sent to accounts before it can be returned from the payor** because `VIEW_BordxReturn_Pay` (what `Bordx_ReturnPayor` returns) keeps only those (`HAVING BordxSendtoACStatus = 1`). The form's own SQL comment says `BordxPAYToStatus = '1'` (sent to payor). The deployed procedure and view are what the desktop runs today, so they were followed.

## Payment Advice > Hospital Report

Read only (`sql/main/570_claim_bordx_hospital_report.sql`, `ClaimBordx/PaymentAdvice/HospitalReport.cs`, `HospitalReportPage.tsx`). The payment advice documents of a bordx of the **MH (Malaysia Airlines)** payor, and its cost allocation. The desktop filled Excel templates (`PayAdviceSummary.xlt`, `MASReport.xlt`) and Word covers (`CoverPanelPayment.DOT`, `CoverStaffPayment.DOT`, `CoverRecovery.DOT`) that are not in the source; the web writes the same sets as the sections of one CSV file.

- **Search:** both ends of the **Bordx No** range are needed (`Please enter Bordx Reference No`), then **Hospital**, bordx date, DOA and DOD ranges; nothing found is `No record found `. The list is Print Bordx's (same view, columns and total) with the bordx date. The desktop applied the DOD range to the **date of admission** (a typing slip); the date of discharge is used. Choose a row to choose its bordx.
- **Print Report** (the desktop's "Print Report" = `CmdPrintSummary`): the range is checked, then `Please Select Bordx` when no row is chosen. For the chosen bordx it writes, each only when it has claims: **Panel Hospitals** (not the MAS Wing group, paid to the hospital, hospital's state not Sabah or Sarawak), **MAS Wing** (group category MASWING SDN BHD, paid to the hospital), **AWUS (Sabah)** and **Sarawak**, each as a summary by hospital (cases, actual, paid by the member, payable to the hospital, totals) and the claims behind it with a sub total and the payee of each hospital; then **Payment to Members** (`View_Paymt_AdviceMBM`), **Recovery** (`View_MAS_MBMRecovery`, with the covered person as the patient), **Staff Claim** (the claim's tax differs from the amount payable to the member) and **Daily Allowance** (the tax equals it; the amount is the tax). The conditions are the desktop's, so a claim with **no group category** (any other payor) matches none of the hospital sets, as in the desktop.
- **CC Report** (`cmdCCRpt_Click`): the cost allocation of the bordx in the range as MAS uploads it, with the desktop's account codes and texts: a line per cost centre (account `10HEQ2510` / `95010000`, MYR, EA per case, "Cost Allocation-GHSS <month><year>"), a line per group category (`51702` `00`, "JXXXXXX INS <month><year>"), the expenses by employee in cost centre order and those of the MAS Wing groups with their sub totals, and the grand total. The desktop passed the hospital, admission and discharge dates to these views, which have no such columns (it would have failed), so only the bordx number and bordx date ranges are used. The account codes are the ones in the source; please have the finance team confirm them before the file is uploaded.
- **Data:** MH bordx numbered like `NMH00105` (2012; 78 claims with Sabah, Sarawak and recovery lines) have no `Bordx` row and are not in Print Bordx's view, so they cannot be chosen in the list (as in the desktop); their sets can still be read. Bordx NWK0024483 (panel) and NWK0024479 (Sabah) are in the list and the views and are the test data.
- **Tests:** `HospitalReportTests.cs` (validator, formatting and every section against a fake repository; the search and each set against the views); `hospitalReportRules.test.ts` (3); `e2e/claim-bordx-hospital-report.mjs` (26 checks).
- **Gate:** position 15 like the rest of Claim Bordx (the menu item has no click handler of its own).

## Payment Advice > Generate Bordx No

Reads and writes (`sql/main/560_claim_bordx_payment_advice.sql`, `ClaimBordx/PaymentAdvice/PaymentAdviceBordx.cs`, `PaymentAdviceBordxPage.tsx`). It is Inpatient Case Listing for the **MH payor's** worksheets that are flagged for a payment advice: the list is `View_MAS_PaymentAdvice` (claim closed and not bordxed, case open, payor MH, `CLMMHBordx` Y). The AXA Affin copy (`PaymentAdviceBordxAXA`, "Generate Bordx No - AXA Affin") is not Required.

- **Search:** **Use Scan** (one worksheet number and optionally the version, added to the list; `Please Enter Worksheet No` without it) or a range, **Cash Type** (H hospital, M member), **Hospital** (its name), DOA and DOD ranges and the two "is empty" boxes. The list has the desktop's columns (payor, case no, version, Wks No, cash type, hospital and approved amounts, DOA, DOD, patient, hospital, group company, plan, ICD, BTBG, insured type, claimability, cost centre, group category, hospital state).
- **Ticking a worksheet** applies the desktop's rules in its order: `Date of Discharge is Empty.`, `Hospital cannot be empty!`, `Final ICD Code cannot be empty!`, `Claimability is Not Decided!`, `Cost Centre is empty`, `Group Category is empty`, `Please ensure scanned documents are in system!` (the server checks the case folder, as for Inpatient Case Listing; skipped when the document store is not configured), and for insured type G or H `Group Company cannot be empty!`. Unlike Inpatient Case Listing the ICD code, cost centre and group category are required and **every** worksheet needs scanned documents.
- **Bordx:** `Please select a record before proceeding!`, `Please select at least one record to be updated! `, `Do you want to update the record?`; then **one bordx** for all the ticked worksheets (numbered from the payor code of the first worksheet's case number, with the sequence and the `Bordx` row of Inpatient Case Listing), every worksheet marked as bordxed exactly as Inpatient Case Listing marks it (claim, invoices, case flags, the "BORDX DONE" remark) and given payment advice number **001** (`CLMPayAdvice`), all in one transaction; `Record Saved!` and the generated bordx are listed. The worksheets are checked again on the server, and one that is no longer in the list is refused and nothing is saved.
- **Deviation:** the `Bordx` row holds the **total of all the worksheets and their number**. The desktop reset the total and the count for every worksheet before it saved the row, so the row held the last worksheet's amount and a count of 1. Its status is C when the last worksheet is paid to the hospital, R otherwise, as in the desktop.
- **Data:** the view holds **no row** in the development data (the 1,301 MH worksheets flagged Y are all bordxed), so the database tests free two of them inside a session that is rolled back, and the browser suite answers the search from fake worksheets of every kind. Please try the screen with real MH worksheets before relying on it.
- **Tests:** `PayAdviceBordxTests.cs` (validators, tick rules, amounts and the service against fakes; the search, the item, the numbering, the marking with payment advice 001 and the bordx row against the database in a rolled-back session, checked with checksums); `paymentAdviceBordxRules.test.ts` (5); `e2e/claim-bordx-payment-advice.mjs` (27 checks).
- **Gate:** position 15 like the rest of Claim Bordx (the menu item has no click handler of its own).
- **Form note:** the form validates when it is submitted, not when a field is left, because a tab-out error under the autofocused Wks No box shifted the buttons down and swallowed the click on Bordx.

## Print Bordx

Reads and writes (`sql/main/550_claim_bordx_print.sql`, `ClaimBordx/Print/PrintBordx.cs`, `PrintBordxPage.tsx`). Lists the claims of a range of bordx and writes the bordx as a report for the payor. Position **17**, the one `mnuPrintBordx` checks.

- **Search:** both ends of the **Bordx No** range are needed (`Please enter Bordx Reference No`); nothing found is `No record found `. The list is `View_PrintBordx` (closed and bordxed worksheets) with the desktop's columns: plan, claim no, patient, member no, hospital, Bordx Amt (for a BTBG claim what is payable to the hospital, for the others the approved amount), bill amount (the actual amount with the GST), approved, recovery (payable by the member), case management fee, debit / credit note ref, bordx no, **Print Status** (YES / NO), health type, payor and BTBG. **Records** and **Total Amt** count them; the total follows the desktop's rule (a BTBG claim paid to the hospital: its hospital amount; paid to the member: its member amount; the others: the **total management fee**), which looks odd and should be confirmed. The desktop's Payor and Amount boxes were not read by the search and are not on the screen; its scan-code search is not migrated.
- **Reports:** **Report Type** (single claim or multiple claims), **BTBG Status** and **Print Status** (Print New or Reprint, which the search sets from the last claim listed, as the desktop did). **Detail Preview**, **Summary Preview**, **Print Detail** and **Print Summary** write the report as a CSV file: the payor (name, address, product), the range and the choices at the top; the detail report has one line per claim with member, policy, group company, hospital, plan, dates, diagnoses and amounts and the totals; the summary has one line per bordx (claims and amounts) and the totals. **The Crystal layouts are not reproduced**: the desktop chose one of about a dozen `.rpt` files (single or multiple claims, Sihat, non-Sihat, BTBG, float plan, below 2000, XP, ...) that are not in the source, so one file layout is written whatever the choices; they decide the heading only. **Print to File** writes the list.
- **Marking:** only **Print Detail** marks the bordx as printed, as in the desktop (the summary report and the previews do not): the bordx of the range get the printed date, status and user, their worksheets `CLMPending` B and their invoices `CLMInvPending` B, in one transaction. The Print Status is checked first, with the desktop's messages: `Please select 'Print New' option for this report` (nothing printed yet, Reprint chosen) and `This report already printed, please select 'Reprint' ` (everything printed, Print New chosen).
- **Differences:** the report holds the claims of the **whole range**; the desktop reported only the health type of the last claim listed (a range of Sihat and non-Sihat bordx printed one of them and marked both). The product name is blank for a Sihat payor, as in the desktop (it looks the code SH up in `ProductCategory`, which has no SH).
- **Data:** bordx NUT0000072 to NUT0000074 (18 claims, none printed) are the test range.
- **Tests:** `PrintBordxTests.cs` (validators, arithmetic, the print flow and its checks against a fake repository; the search, the report, the payor and product, and the marking in a rolled-back session, checked with checksums); `printBordxRules.test.ts` (6); `e2e/claim-bordx-print.mjs` (30 checks; the POST that would mark the bordx is intercepted, nothing is saved).

## Update Bordx

Reads and writes (`sql/main/540_claim_bordx_update.sql`, `ClaimBordx/Update/UpdateBordx.cs`, `UpdateBordxPage.tsx`). Puts pending worksheets into a bordx whose number is typed (the list is `VIEW_WS_PendingBordx`: the worksheets in **no bordx**, and the **void** ones, whose bordx reference is `VOID`; Inpatient Case Listing reads another view, `VIEW_WS_PendingBordx2`): a new bordx, or one that exists. It is the form behind the old Insert Worksheet entry, with the dates and the MNRB receipt Inpatient Case Listing does not have.

- **Search:** **Payor**, **Amount** (approved up to 2000, or above), **Search Empty Data** (only worksheets in no bordx; on by default), case number range, bordx number range, DOA and DOD ranges. The list has the desktop's columns (payor, case, version, Wks No, actual, approved, hospital and member amounts, DOA, DOD, patient, hospital, group company, plan, ICD) and the bordx number; **Records**, **Total Amt** and **Amt Checked** count them.
- **Update:** the checks and messages are the desktop's, in its order: `Please select a record before proceeding!`, `Please select at least one record to be updated! `, `Please Enter Bordx No! `, `Please Enter Bordx Date! `; then `Do you want to update the record?`. Every ticked worksheet goes into the bordx (`BordxRefNo`, `ClaimStatus` C, `ClaimBordxStatus` 1, its invoices bordxed, or one empty invoice row when it has none), then the **Bordx** row is created (payor, date, user, printed date and user) or, when it exists, given the accounting period and the dates, and the **MNRB receipt** is written; all in one transaction, so a failure saves nothing. The A/C period (`PAYToACPeriodMth` / `YR`) and the dates sent to and received from the payor and MNRB (with their 1 status flags) are set only when given.
- **Deviations from the desktop, on purpose:**
  - A **void worksheet cannot be ticked** (`Claim is void!`) and the server refuses it (the desktop's list showed them when the Search Empty Data box was off, and put them into the bordx). A worksheet that is in a real bordx cannot be ticked either (`Claim has been bordxed!`; the list cannot hold one, the server refuses it anyway): moving it would leave the old bordx's total and count as they were (Remove Worksheet is the way to take a worksheet out of a bordx).
  - The bordx's **total and number of claims are worked out from the worksheets in it** after the update. The desktop added the new worksheets only to a bordx that had none, and its check could never be true (the worksheets had just been put into the bordx), so a bordx that already had worksheets kept its old total and count.
  - All the worksheets of a bordx must be of **one payor**, and the payor of a bordx that exists cannot change (`All the worksheets of a bordx must be of the same payor`, `Bordx ... belongs to another payor`); the desktop took the payor from the drop-down.
  - The **MNRB receipt** (`ReceiptFrMNRB`, one row per bordx: Rec No, Rec Date, Cheque Amt, Cheque No, the amount of the worksheets checked) is written **only when a Rec No is given** (`Please Enter Receipt No!` when another receipt box is filled without it). The desktop wrote the row at every update, blank, and put the receipt number into the cheque number.
  - The worksheet's bordx date (`CLMBordxDate`), the case's pending flag and the "BORDX DONE" remark, which Inpatient Case Listing sets, are not set here, as in the desktop.
- **Data:** 5,537 worksheets in the list: 1,313 in no bordx and 4,224 void (TA 1,346, MX 755, ST 600). Payors with worksheets in no bordx: TA 368, SE 299, AY 116, AH 82, ST 81.
- **Tests:** `UpdateBordxTests.cs` (validators, the service's transaction and refusals against a fake repository, and the search, the claim, invoice, bordx and receipt writes and their totals against the database in sessions that are rolled back, checked with checksums); `updateBordxRules.test.ts` (6); `e2e/claim-bordx-update.mjs` (34 checks: the API's refusals with real calls, and the screen with the writes intercepted, so nothing is saved).
- **Gate:** position 15 like the rest of Claim Bordx.

## Remove Worksheet (Bordereaux Revise)

Look a bordx up by number (`genisis.BordxRevise_Search`, the old `dbo.BordxRevise`: the worksheets of the bordx with member and patient), tick the worksheets to take out of it and remove them (`genisis.BordxRevise_Remove`, the old `RemoveFromBordx`; file `sql/main/410_claim_bordx_revise.sql`, synonym `genisis.WS_BordxWSArchive`).

- **Checks in the desktop's order:** `Please select a Bordx No` (Go), `No record found!`, `This Bordx has been sent out.` (the list is shown but Remove is disabled), then Remove: `You are going to remove a worksheet from current Bordereaux. Are you sure?`, `Record updated...`, and the form is cleared.
- **What a removal writes, per worksheet:** the bordx loses one claim (`BordxNoClaims - 1`) and the worksheet's approved amount (`BordxClaimAmt`); a `BordxWSArchive` row records the case, version, bordx, bordx date, user and day; the worksheet's invoices are un-bordxed (`INVBordxRefNo = ''`, `INVBordxed = 0`); the worksheet is free (`ClaimBordxStatus = 0`, no bordx), and one that had already gone to the payor as an ASCII file (`CLMBordxWSASCII = 'Y'`) goes back to `'N'` with status `'D'`.
- **Scan codes:** a code starting `LO` in the Bordx No box opens another screen, as in the other search screens; `LOB002` is this screen.
- **Differences:** the whole removal is one transaction (the desktop ran each statement on its own); a bordx sent out, a worksheet that is not in the bordx and a bordx or worksheet that is gone are refused in the database too (the desktop only disabled the button); removing with nothing ticked says so (the desktop did nothing); the desktop took the bordx number for the archive row and the amounts from the screen's box and the claim count from the claim's own bordx, which are the same in practice and are checked to be the same here; the archive row's day is the server's.
- **Data:** 19,318 of 19,780 bordx have worksheets, and for the 300 most recent every worksheet is listed (the procedure's inner joins to the cross-reference tables drop none). `BordxNoClaims` and `BordxClaimAmt` do **not** equal the count and sum of the claims for most of the older bordx in the development copy (3,039 of 19,556 agree on count), so the rule could not be compared with saved results; the rolled-back test checks each column the removal writes, the archive row, the un-bordxed invoices and the ASCII case, and a checksum of the four tables before and after.

## Returned Fr Payor (Not App)

One worksheet at a time (`sql/main/420_claim_bordx_return_case.sql`, `BordxReturnCase.cs`): type a claim number and leave the box, and the Version drop-down is filled with the case's versions (`Bordx_ReturnPayorCase` case 2) and the worksheet is looked up (case 1: the worksheet and its bordx, only when the bordx has been sent to the payor, otherwise **Bordx not send to Payor!**). **Approved Status** (N - No, Y - Yes; starts on No) and **Update** set `Claim.CLMAppStatus` after `Do you want to update the record?`; the screen shows `Record updated...`, puts the status back to No and looks the worksheet up again.

- **Checks:** `Please Enter Claim No`, `Please Enter Approved Status`; the desktop crashed (`CInt('')`) when Update was pressed without a worksheet looked up; the web says `Please Enter Claim No`. The database also refuses a worksheet whose bordx has not been sent to the payor, and the API only accepts N or Y.
- **Not carried over:** the autocomplete code of the Version combo and its `null` check (the browser's drop-down does this), the `CmdToPayor` name (it is the Update button).

## Storage Enquiry

Read only, in two tabs (`sql/main/450_claim_bordx_storage_enquiry.sql`, `ClaimBordx/Storage/StorageEnquiry.cs`). **Carton Details** searches `dbo.Carton` (5,866 cartons: 2,679 Medix, 3,186 Store, 1 Return) by carton number range, status (M - Medix, S - Store, R - Return) and the dates of sending, request, delivery, return and destroy. **Storage Details** searches the claim files kept in them (`VIEW_Storage_CLM`, 45,591 rows) by case number (a scan, or a range), carton range, file status (I - In, O - Out), request by (a user), the dates of request, delivery, return, registration and sending, and the payor range (the first two letters of the case number).

- **Criteria as the desktop had them:** the first box of a date pair is "on or after" and the second "on or before"; a blank box is no criterion; only files that have a carton are listed. A scan adds the case to the list and clears the box (`Please Enter Case No`; `Data has been uploaded!` when it is already listed); a range search replaces the list; no match is `No record found!`.
- **Print to File** exports the list to CSV (`Report cannot be printed without any record.` when empty). **Listing** writes the carton's **check sheet** (No, Case No-Version, Checking = file status) as CSV for the carton in the Carton No box (`Please select Carton No.`); the desktop filled the Excel template `StorageTracking.xlt` with the same three columns in blocks of 50.
- **Differences:** the view repeats a claim once per invoice payee and the desktop listed every repeat; a file is one row here. Rows are ordered by case number and version (carton number for the cartons). The list is cut at 5,000 rows and says so (all cartons are more than that). The `Stop` button, `intExit` flag and the combo auto-complete code are not carried over.
- **Gate:** position 15, like the rest of Claim Bordx (the Storage controls have no click handler in the desktop).
- **Tests:** every criterion is compared with a direct count on the tables in `StorageEnquiryTests.cs`; `e2e/claim-bordx-storage.mjs` (31 checks) covers the API, the two tabs, the scan rules and access. Nothing is written.

## Print Recovery

Read only (`sql/main/490_claim_bordx_print_recovery.sql`, `ClaimBordx/Recovery/PrintRecovery.cs`). The recoveries of a **BTBG client** (a group company whose `CompanyGrp.BTBGClient` is Y; 52 of them): worksheets whose amount payable by the member (`CLMPayableByMedix2M`) is negative, from the claim report view (`VIEW_NEWCLMReport`). Criteria: **Grp Company** (required: `Please select Group Company`), bordx number range, bordx date range and WS Reg Date range (the date the worksheet was opened). The list shows case-version, patient, member number, hospital, actual amount, the recovery, the bordx amount and the bordx reference; **Records** and **Total Amt** (the bordx amounts) count the rows listed. `No record found ` when nothing matches.

- **The recovery is shown positive** (the database holds it negative; the desktop multiplied by -1 on the list and in the listing).
- **Print** writes the recovery listing the client is sent as a CSV: the payor, policy number, its period and today's date from the first row, the rows numbered from 1, the totals under the three amount columns and the payment note ("Kindly note that payment term is 14 days and payment should be issued to MediExpress (Malaysia) Sdn Bhd"). The desktop filled one of two Excel templates chosen by the client's `BTBGRpt`: **1** the standard Recovery Listing (51 clients: 17 columns, ending with the amounts) or **2** the MDEC listing (1 client: 15 columns, with cost centre, invoice date and debit note number). The web keeps both column sets; the template files are not available to the web, so the headings are the ones the desktop's cell mapping implies, and the heading text should be checked against the template. The desktop's code read `InvDate` for layout 2 although its query did not return it, which would have failed; the web returns it.
- **Speed:** the claim report view takes about 40 seconds for one client when its diagnosis columns are asked for with everything else (the desktop waited up to 300). The procedure takes the rows first (about a second) and then looks each one's diagnoses up by case number: 5 seconds for the client with the most recoveries (246).
- **Data:** 1,414 recoveries for 106 group companies; 9 of the 52 clients have some (GAS MALAYSIA BERHAD 246, MDEC 26).
- **Gate:** position 15 like the rest of Claim Bordx (hidden in the original, no click handler).
- **Tests:** `PrintRecoveryTests.cs` compares the clients and each criterion with direct counts on the view; `e2e/claim-bordx-recovery.mjs` (22 checks).

## Outstanding Bordx

Read only (`sql/main/480_claim_bordx_outstanding.sql`, `ClaimBordx/Control/OutstandingBordx.cs`). Lists the bordereaux that were sent and have not come back, and for how long, as at a chosen day (starts on today). The desktop called `OSBordx` with nine case numbers; there are three lists over `VIEW_OS_Bordx` (one row per bordx), chosen by the payor's `HLTCode` as in Bordx Control:

| List | Rule | Desktop cases |
|---|---|---|
| **Bordx with Payor** (Sihat payors) | sent to the payor and the documents not back (`BordxPAYToStatus` 1, `BordxPAYDOCFRStatus` 0); headed DTP | 1, 2, 3 |
| **Not Paid by MNRB** (Sihat payors) | sent to MNRB and no MNRB payment status yet; headed DTMNRB | 4, 5, 6 |
| **Non-Sihat Malaysia** (the other payors) | sent to the payor and not fully paid (`BordxPAYFRStatus` P or empty); headed Date sent to Payor, No of Days O/S | 7, 8, 9 |

- **View:** Detail Listing (one payor: its lists; **All Sihat** or **All Non-Sihat**: every payor of that layout; both at the top of the Payor drop-down), General Summary (S) and General Summary (N) (the number of claims and the amount for each payor; the payor is an optional filter). `Please Enter Payor Code.` when a detail listing of one payor has none; `No record found for Payor!` when every list is empty (the desktop said it once per list).
- **Days:** from the as at day, counting the day it was sent (sent today is 1); a bordx sent more than 14 days ago goes in the Bordx >14 days column, the others in Bordx <14 days (14 itself in <14); each list has a Sub-Total and the two column totals. **Total Due** (Sihat) is the two Sihat lists added. A result of 0 shows as 1, as in the desktop (it cannot happen for a sent bordx).
- **Print** writes the lists, each under its title with its totals, to one CSV (the desktop wrote an Excel sheet).
- **Data:** nothing has been sent in the development data, so every real search is empty. The lists are checked against the view in rolled-back database tests that mark bordx as sent, and in the browser with answers served by the test script. The Bordx Amt of this view is the bordx's header amount (`Bordx.BordxClaimAmt`), which is 0 for many bordx in the data (their claims carry the amounts); Bordx Control reads the amounts from the claims. **Business to confirm** which is the amount to show.
- **Gate:** position 15 like the rest of Claim Bordx (hidden in the original, no click handler).
- **Tests:** `OutstandingBordxTests.cs` (day counts, buckets, totals, the three lists against the view with seeded bordx); `e2e/claim-bordx-outstanding.mjs` (24 checks).

## Bordx Control

Read only (`sql/main/470_claim_bordx_control.sql`, `ClaimBordx/Control/BordxControl.cs`). Follows the bordereaux of one payor, of all the Sihat payors or of all the non-Sihat payors, from the day they were sent to the payor to the day the documents (DRP) and the money came back. The Payor drop-down holds the payors with two entries on top, **All Sihat** and **All Non-Sihat** (the desktop had them as two check boxes). The payor's `HLTCode` picks the layout, as in `cboPayor_Click`: N is the **non-Sihat** layout (Bordx No, Date, Claims, Amt, Status, DTP, DRP (Doc), days, DFPayor (Money), days), anything else the **Sihat** layout (the same, then DTMNRB, DFMNRB (Money), days, Total Days). The list has a Payor column first when it holds several payors.

- **Criteria:** bordx number and date ranges; To Payor (Both, Sent, Not Yet Sent); From Payor (Doc) (Both, Received, Not Yet Received); for Sihat: To MNRB (Both, Sent, Not Yet Sent; starts on Both, which keeps only bordx that have a To MNRB status, as the desktop did) and From MNRB (Money) (Both, Paid, Partial Paid, Unpaid); for the others: From Payor (Money) (Both, Paid, Partial Paid, Unpaid). The status boxes that do not belong to the payor's layout are not shown.
- **Messages:** `Please Enter Payor Code.` (no payor and no All), `No record found!`, `Report cannot be printed without any record.`; Print to File exports the list to CSV. Records and Total Amt count the rows listed.
- **Days:** a same-day return is shown as 1 day and a missing date leaves the count blank, as the desktop showed; Total Days (Sihat) adds the two counts, a blank as 0.
- **Rows come from the same two views** (`VIEW_Bordx_ControlS2` and `N2`). They group on the claim's bordx amount, so a bordx whose claims differ in amount is listed once per amount, each time with that amount (22,468 rows for 19,780 bordx in the development data), and the Bordx Amt is not the bordx total. The web shows it exactly as the desktop did; **business to confirm** whether a row per bordx with the summed amount is wanted.
- **Data:** every payor in the development data is non-Sihat (`VIEW_Bordx_ControlS2` is empty), so the Sihat layout is checked by code and fake-repository tests only, not against saved rows; nothing has been sent, so the To Payor and To MNRB criteria find nothing.
- **Gate:** position 15 like the rest of Claim Bordx (the menu item is hidden in the original and has no click handler).
- **Tests:** `BordxControlTests.cs` compares every criterion with a direct count on the view; `e2e/claim-bordx-control.mjs` (24 checks).

## Storage > Claim files (Storage Entry)

The writing side of Storage Enquiry (`sql/main/510_claim_bordx_storage_entry.sql`, `ClaimBordx/Storage/StorageEntry.cs`, `StorageEntryPage.tsx`): register the cartons the claim files are kept in and put claim files into them. Two tabs, as in the desktop. The lists are the Enquiry's (`/api/claim-bordx/storage/cartons` and `/files`); `genisis.StorageEnquiry_StorageSearch` gained an `@includeUnstored` switch (the Entry list also shows the claims that are not stored yet), the bordx date range and the claim's bordx date and cash type columns.

**Carton Details.** Search cartons (carton number, status and the five dates, as in the Enquiry). **Add** opens a blank form; **Save** checks, in the desktop's order, the carton number (`Please Enter Carton Number`), the status (`Please Enter Carton Status`), the dates and status R (`Date Return cannot be null when the status is 'R' !`), asks `Do you want to save the record?` and adds the carton (an existing number is `Record Duplicated! Please Change The Corresponding Field`). Click a carton to see it, tick one or more and **Edit**: every ticked carton gets the same status, dates and remarks (`SaveRecByCarton`; the carton number is locked); **Update** saves them in one transaction. **Delete** removes the selected carton.

**Storage Details.** Scan a case or search a range (the same criteria as the Enquiry, the carton matched exactly, "A - All" or In / Out, request, delivery, return and bordx dates). The list shows every claim of the case with its storage row when it has one. Tick the claims that are not stored (`Claim has been stored!` for one that is; a warning from the 71st: `Files Limit Exceeded! `), **Add**, fill the carton, file status and the request details, **Save**: each ticked claim is stored in one transaction. Click a stored claim to see its row, **Edit** it or **Remove** it (`Record has been deleted!`). A claim that is stored already, a carton or claim that does not exist, or an edit of a claim that is not stored, saves nothing.

- **Date checks:** the desktop compared the carton dates as text and with `IsDate` flags, so it refused a request date without a send date, a return date without a delivery date and a destroy date without a return date, but did not check that the return came after the delivery. The web keeps those refusals and checks the order of each pair of dates. The messages are the desktop's.
- **Request By:** without one the three request dates are not kept (as before); with one the date of request is required.
- **Dropped or changed on purpose:**
  - The Carton No to box of Add (a range loop that, as written, stored one carton): one carton is added at a time.
  - `cmdDelete` of Storage Details, which deleted every version of the case typed in the box: **Remove** takes out the one selected claim.
  - Delete of a carton that has files in it: the desktop deleted it and left the files pointing at a carton that no longer exists; the web refuses (`This carton cannot be deleted because claim files are stored in it!`).
  - Remove asked "Do you want to save the record?" before deleting; it asks "Do you want to delete the record?".
  - The "Listing" button (the carton check sheet): it is on the Enquiry screen.
- **Tests:** `StorageEntryTests.cs` (validators in the desktop's order, the service's transactions, and the carton and claim-file writes against the database in sessions that are rolled back, checked with checksums); `storageEntryRules.test.ts` (7); `e2e/claim-bordx-storage-entry.mjs` (61 checks: the API's refusals with real calls, and the screen with the writes intercepted, so nothing is stored).
- **Gate:** position 15 like the rest of Claim Bordx.

## Storage Document

Puts the accounts department's documents into cartons (`sql/main/460_claim_bordx_storage_document.sql`, `ClaimBordx/Storage/StorageDocument.cs`). Choose a **document type** (B - Bordx, P - Payment Voucher, R - Receipt), search by document number range, carton, file status (All, In, Out), request by and the exact date of request, delivery or return. The list shows each document with its storage row when it has one (`dbo.StorageDocument`; 5,013 rows, all payment vouchers). Tick the documents to store, press **Add**, fill in the document type, carton, file status and, when somebody requested it, the request dates, then **Save**.

- **Checks, in the desktop's order:** `Please checked at least 1 Claim !` (Add with nothing ticked), `Please Enter Document Type`, `Please Enter Carton Number`, `Please Enter File Status`, then only when Request By is filled in: `Please Enter Date Of Request`, `Invalid Date ! Date of Delivery must greater than Date of Request`, `Invalid Date ! Date of Return must greater than Date of Delivery` (the desktop compared the dates as text, so a return date with no delivery date was refused too; the web keeps that). Without a Request By the three dates are cleared. Then `Do you want to save the record?` and `Record Saved...`.
- **Ticking:** a document that is in a carton already cannot be ticked (`Claim has been stored!`). Clicking a stored document shows its storage row (locked); **Edit** unlocks it, the button becomes **Update** and the save changes that one row (`Record Updated...`).
- **Server:** all the ticked documents are stored in one transaction. A document that is stored already is `Record Duplicated! Please Change The Corresponding Field`, and nothing is saved; the carton has to exist (`Carton Number not found`; the desktop's drop-down only allowed existing cartons). Names and remarks are trimmed and upper-cased as ChkStr did.
- **Differences:** the desktop saved document by document, so a duplicate stopped only itself and the others were saved; the web saves all or none. The "Carton Status" column of the desktop list actually held the document type; it is headed Doc Type. Print File exports the list to CSV.
- **Data:** none of the 6,756 payment vouchers in the view is stored (the 5,013 stored numbers match no voucher in the payment tables, so the desktop view never shows them); the stored-row flows are checked in rolled-back database tests and with a fake stored row in the browser.
- **Tests:** `StorageDocumentTests.cs` compares every search with direct counts and stores, finds, changes and refuses documents in a rolled-back session; `e2e/claim-bordx-storage-document.mjs` (35 checks).

## What `CLMAppStatus` means (to confirm with the business)

Returned From Payor writes `CLMAppStatus = 'Y'` (on the bordx's claims that are `N`, blank or NULL) and Returned Fr Payor (Not App) writes `'N'` or `'Y'`. In the development data the column holds only `NULL` (64,910 worksheets), `A` (5,511), `P` (4) and `R` (2), so a `Y` or `N` written by either screen is a value nothing else in the data uses. The screens are ported as the desktop coded them; if the business means approved / pending / rejected (A, P, R), the two updates need changing, and so does the condition `(N, blank, NULL)` of Returned From Payor.

## Data and verification

The development database's 19,780 bordx are all in their first state (none sent to a payor or to accounts, none returned, none approved), so the screens could **not** be compared with saved results of the desktop. What was checked instead:

- Every criterion of the search against a direct `COUNT(*)` on `Bordx` for a payor with many bordx (the database test `The_search_gives_what_the_table_holds_for_each_criterion`).
- The development data has **no bordx sent to the payor**, so Returned Fr Payor (Not App) cannot find a worksheet there; its database test marks a bordx as sent inside the transaction and checks the lookup, both updates and the refusal, and the checksum of `Claim` before and after.
- Every update (all four kinds) in a transaction that is **rolled back**, reading the columns back and comparing a checksum of the whole table, and of `Claim` for the return, before and after.
- The second update of the same bordx is refused, and a missing bordx is not found.
- In the browser, the whole flow through a list the test fakes for Returned From Payor (nothing in the data has been sent to accounts yet).

## Tests

- API: `BordxDeliveryTests.cs` (28 tests with the architecture tests: validators, service against a fake repository, and the rolled-back database tests), `BordxReviseTests.cs` (13 the same way) and `BordxReturnCaseTests.cs` (7) and `StorageEnquiryTests.cs` (9) and `StorageDocumentTests.cs` (14) and `BordxControlTests.cs` (9) and `OutstandingBordxTests.cs` (7) and `PrintRecoveryTests.cs` (5) and `StorageEntryTests.cs` (9) and `UpdateBordxTests.cs` (9). The whole API suite: **1,010 tests** passed in full (12 m 8 s) before the Storage Enquiry tests were added.
- UI: `bordxDeliveryRules.test.ts` (14 tests), `bordxReviseRules.test.ts` (5) and `bordxReturnCaseRules.test.ts` (5) and `storageEnquiryRules.test.ts` (8) and `storageDocumentRules.test.ts` (6) and `bordxControlRules.test.ts` (7) and `outstandingBordxRules.test.ts` (6) and `printRecoveryRules.test.ts` (6) and `storageEntryRules.test.ts` (7) and `updateBordxRules.test.ts` (6).
- Browser: `e2e/claim-bordx-delivery.mjs` (91 checks: API, menu, the six pages, the update, removal and status POSTs (intercepted, nothing is written), scan code, access).

## Code map

| Layer | Files |
|---|---|
| SQL | `sql/main/400_claim_bordx_delivery.sql`, `410_claim_bordx_revise.sql`, `420_claim_bordx_return_case.sql`, `450_claim_bordx_storage_enquiry.sql`, `460_claim_bordx_storage_document.sql`, `470_claim_bordx_control.sql`, `480_claim_bordx_outstanding.sql`, `490_claim_bordx_print_recovery.sql`, `510_claim_bordx_storage_entry.sql`, `540_claim_bordx_update.sql` (and `sql/genisis-main.sql`); reads `genisis.WS_Bordx` (`HISWorkSheet.dbo.Bordx`) and updates `genisis.WS_Claim` for the return |
| API | `Genisis.Api/ClaimBordx/Delivery/BordxDelivery.cs` and `Revise/BordxRevise.cs` and `ReturnCase/BordxReturnCase.cs` and `Storage/StorageEnquiry.cs` and `Storage/StorageDocument.cs` and `Control/BordxControl.cs` and `Control/OutstandingBordx.cs` and `Recovery/PrintRecovery.cs` `Storage/StorageEntry.cs` and `Update/UpdateBordx.cs` (models, validators, repository, service), `ClaimBordx/ClaimBordxServiceCollectionExtensions.cs`, `Controllers/ClaimBordxDeliveryController.cs`, `ClaimBordxReviseController.cs`, `ClaimBordxReturnCaseController.cs`, `ClaimBordxStorageController.cs`, `ClaimBordxStorageDocumentController.cs`, `ClaimBordxControlController.cs`, `ClaimBordxOutstandingController.cs`, `ClaimBordxRecoveryController.cs`, `ClaimBordxStorageEntryController.cs`, `ClaimBordxUpdateController.cs`, `Auth/AccessRight.cs` (`ClaimBordx`) |
| UI | `GenisisUI/src/pages/claimBordx/` (`BordxDeliveryPage.tsx`, `bordxDeliveryRules.ts`, `BordxRevisePage.tsx`, `bordxReviseRules.ts`, `BordxReturnCasePage.tsx`, `bordxReturnCaseRules.ts`, `StorageEnquiryPage.tsx`, `storageEnquiryRules.ts`, `StorageDocumentPage.tsx`, `storageDocumentRules.ts`, `BordxControlPage.tsx`, `bordxControlRules.ts`, `OutstandingBordxPage.tsx`, `outstandingBordxRules.ts`, `PrintRecoveryPage.tsx`, `printRecoveryRules.ts`, `StorageEntryPage.tsx`, `storageEntryRules.ts`, `UpdateBordxPage.tsx`, `updateBordxRules.ts` and their tests), the routes in `App.tsx`, the section in `components/AppLayout.tsx`, `auth/access.ts` |
