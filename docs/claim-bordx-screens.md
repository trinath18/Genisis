# Claim Bordx screens

Menu path: **Claim Bordx** (legacy `mdiMain.frm`, top menu `mnuFinance`'s sibling in the Claim Bordx group). This page covers what is migrated; the rest of the menu is in [remaining-work.md](remaining-work.md).

## What is built

| Menu path | Legacy form | Web route | API | Status |
|---|---|---|---|---|
| Claim Bordx > Worksheet > Insert Worksheet | `FrmWksBordx` ("Select Worksheets for Bordx") | `/claim-bordx/insert-worksheet` | `/api/case/listing` | The same form Case > Inpatient Case Listing opens (`mnuGenerateBordx_Click` and `mnuAppCase_Click` both show `FrmWksBordx`), migrated with the Case module; this entry is a second door to it behind position 15 |
| Claim Bordx > Worksheet > Remove Worksheet | `FrmBordxRevise` ("Bordereaux Revise") | `/claim-bordx/remove-worksheet` | `/api/claim-bordx/revise` | Migrated (below) |
| Claim Bordx > Monitoring > Bordx Control | `FrmBordxControl` ("Bordx Control") | `/claim-bordx/bordx-control` | `/api/claim-bordx/control` | Migrated (below) |
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

## Bordx Control

Read only (`sql/main/470_claim_bordx_control.sql`, `ClaimBordx/Control/BordxControl.cs`). Follows the bordereaux of one payor, of all the Sihat payors or of all the non-Sihat payors, from the day they were sent to the payor to the day the documents (DRP) and the money came back. The Payor drop-down holds the payors with two entries on top, **All Sihat** and **All Non-Sihat** (the desktop had them as two check boxes). The payor's `HLTCode` picks the layout, as in `cboPayor_Click`: N is the **non-Sihat** layout (Bordx No, Date, Claims, Amt, Status, DTP, DRP (Doc), days, DFPayor (Money), days), anything else the **Sihat** layout (the same, then DTMNRB, DFMNRB (Money), days, Total Days). The list has a Payor column first when it holds several payors.

- **Criteria:** bordx number and date ranges; To Payor (Both, Sent, Not Yet Sent); From Payor (Doc) (Both, Received, Not Yet Received); for Sihat: To MNRB (Both, Sent, Not Yet Sent; starts on Both, which keeps only bordx that have a To MNRB status, as the desktop did) and From MNRB (Money) (Both, Paid, Partial Paid, Unpaid); for the others: From Payor (Money) (Both, Paid, Partial Paid, Unpaid). The status boxes that do not belong to the payor's layout are not shown.
- **Messages:** `Please Enter Payor Code.` (no payor and no All), `No record found!`, `Report cannot be printed without any record.`; Print to File exports the list to CSV. Records and Total Amt count the rows listed.
- **Days:** a same-day return is shown as 1 day and a missing date leaves the count blank, as the desktop showed; Total Days (Sihat) adds the two counts, a blank as 0.
- **Rows come from the same two views** (`VIEW_Bordx_ControlS2` and `N2`). They group on the claim's bordx amount, so a bordx whose claims differ in amount is listed once per amount, each time with that amount (22,468 rows for 19,780 bordx in the development data), and the Bordx Amt is not the bordx total. The web shows it exactly as the desktop did; **business to confirm** whether a row per bordx with the summed amount is wanted.
- **Data:** every payor in the development data is non-Sihat (`VIEW_Bordx_ControlS2` is empty), so the Sihat layout is checked by code and fake-repository tests only, not against saved rows; nothing has been sent, so the To Payor and To MNRB criteria find nothing.
- **Gate:** position 15 like the rest of Claim Bordx (the menu item is hidden in the original and has no click handler).
- **Tests:** `BordxControlTests.cs` compares every criterion with a direct count on the view; `e2e/claim-bordx-control.mjs` (24 checks).

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

- API: `BordxDeliveryTests.cs` (28 tests with the architecture tests: validators, service against a fake repository, and the rolled-back database tests), `BordxReviseTests.cs` (13 the same way) and `BordxReturnCaseTests.cs` (7) and `StorageEnquiryTests.cs` (9) and `StorageDocumentTests.cs` (14) and `BordxControlTests.cs` (9). The whole API suite: **1,010 tests** passed in full (12 m 8 s) before the Storage Enquiry tests were added.
- UI: `bordxDeliveryRules.test.ts` (14 tests), `bordxReviseRules.test.ts` (5) and `bordxReturnCaseRules.test.ts` (5) and `storageEnquiryRules.test.ts` (8) and `storageDocumentRules.test.ts` (6) and `bordxControlRules.test.ts` (7).
- Browser: `e2e/claim-bordx-delivery.mjs` (91 checks: API, menu, the six pages, the update, removal and status POSTs (intercepted, nothing is written), scan code, access).

## Code map

| Layer | Files |
|---|---|
| SQL | `sql/main/400_claim_bordx_delivery.sql`, `410_claim_bordx_revise.sql`, `420_claim_bordx_return_case.sql`, `450_claim_bordx_storage_enquiry.sql`, `460_claim_bordx_storage_document.sql`, `470_claim_bordx_control.sql` (and `sql/genisis-main.sql`); reads `genisis.WS_Bordx` (`HISWorkSheet.dbo.Bordx`) and updates `genisis.WS_Claim` for the return |
| API | `Genisis.Api/ClaimBordx/Delivery/BordxDelivery.cs` and `Revise/BordxRevise.cs` and `ReturnCase/BordxReturnCase.cs` and `Storage/StorageEnquiry.cs` and `Storage/StorageDocument.cs` and `Control/BordxControl.cs` (models, validators, repository, service), `ClaimBordx/ClaimBordxServiceCollectionExtensions.cs`, `Controllers/ClaimBordxDeliveryController.cs`, `ClaimBordxReviseController.cs`, `ClaimBordxReturnCaseController.cs`, `ClaimBordxStorageController.cs`, `ClaimBordxStorageDocumentController.cs`, `ClaimBordxControlController.cs`, `Auth/AccessRight.cs` (`ClaimBordx`) |
| UI | `GenisisUI/src/pages/claimBordx/` (`BordxDeliveryPage.tsx`, `bordxDeliveryRules.ts`, `BordxRevisePage.tsx`, `bordxReviseRules.ts`, `BordxReturnCasePage.tsx`, `bordxReturnCaseRules.ts`, `StorageEnquiryPage.tsx`, `storageEnquiryRules.ts`, `StorageDocumentPage.tsx`, `storageDocumentRules.ts`, `BordxControlPage.tsx`, `bordxControlRules.ts` and their tests), the routes in `App.tsx`, the section in `components/AppLayout.tsx`, `auth/access.ts` |
