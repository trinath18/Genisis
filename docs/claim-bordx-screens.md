# Claim Bordx screens

Menu path: **Claim Bordx** (legacy `mdiMain.frm`, top menu `mnuFinance`'s sibling in the Claim Bordx group). This page covers what is migrated; the rest of the menu is in [remaining-work.md](remaining-work.md).

## What is built

| Menu path | Legacy form | Web route | API | Status |
|---|---|---|---|---|
| Claim Bordx > Worksheet > Insert Worksheet | `FrmWksBordx` ("Select Worksheets for Bordx") | `/claim-bordx/insert-worksheet` | `/api/case/listing` | The same form Case > Inpatient Case Listing opens (`mnuGenerateBordx_Click` and `mnuAppCase_Click` both show `FrmWksBordx`), migrated with the Case module; this entry is a second door to it behind position 15 |
| Claim Bordx > Bor Delivery > Send to Payor | `FrmSendtoPayor` ("Bordx Delivery") | `/claim-bordx/send-to-payor` | `/api/claim-bordx/delivery/to-payor` | Migrated |
| Claim Bordx > Bor Delivery > Returned From Payor | `FrmReturnPayor` ("Bordx Delivery") | `/claim-bordx/returned-from-payor` | `/api/claim-bordx/delivery/from-payor` | Migrated (form chosen by name, see "Assumptions") |
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

## Data and verification

The development database's 19,780 bordx are all in their first state (none sent to a payor or to accounts, none returned, none approved), so the screens could **not** be compared with saved results of the desktop. What was checked instead:

- Every criterion of the search against a direct `COUNT(*)` on `Bordx` for a payor with many bordx (the database test `The_search_gives_what_the_table_holds_for_each_criterion`).
- Every update (all four kinds) in a transaction that is **rolled back**, reading the columns back and comparing a checksum of the whole table, and of `Claim` for the return, before and after.
- The second update of the same bordx is refused, and a missing bordx is not found.
- In the browser, the whole flow through a list the test fakes for Returned From Payor (nothing in the data has been sent to accounts yet).

## Tests

- API: `BordxDeliveryTests.cs` (28 tests with the architecture tests: validators, service against a fake repository, and the rolled-back database tests).
- UI: `bordxDeliveryRules.test.ts` (14 tests).
- Browser: `e2e/claim-bordx-delivery.mjs` (56 checks: API, menu, the four pages, the update POSTs (intercepted, nothing is written), access).

## Code map

| Layer | Files |
|---|---|
| SQL | `sql/main/400_claim_bordx_delivery.sql` (and `sql/genisis-main.sql`); reads `genisis.WS_Bordx` (`HISWorkSheet.dbo.Bordx`) and updates `genisis.WS_Claim` for the return |
| API | `Genisis.Api/ClaimBordx/Delivery/BordxDelivery.cs` (models, validators, repository, service), `ClaimBordx/ClaimBordxServiceCollectionExtensions.cs`, `Controllers/ClaimBordxDeliveryController.cs`, `Auth/AccessRight.cs` (`ClaimBordx`) |
| UI | `GenisisUI/src/pages/claimBordx/` (`BordxDeliveryPage.tsx`, `bordxDeliveryRules.ts`, `bordxDeliveryRules.test.ts`), the routes in `App.tsx`, the section in `components/AppLayout.tsx`, `auth/access.ts` |
