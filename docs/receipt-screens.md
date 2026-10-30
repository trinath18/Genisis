# Receipt screens

Menu path: **Receipt** (legacy `mdiMain.frm`, top menu `mnuReceiptModule`: From Member, Others, Payor, Payor (Manual), Receipt Tracking). The business marked From Member, Others, Payor and Receipt Tracking Required. Only Receipt Tracking is built; the three entry forms are in [remaining-work.md](remaining-work.md) and are shown disabled in the Receipt menu. The sidebar has a Receipt module (rail icon, `/receipt`).

| Menu path | Legacy form | Web route | API | Access position |
|---|---|---|---|---:|
| Receipt > Receipt Tracking | `FrmReceiptTracking` ("Receipt Tracking") | `/receipt/tracking` | `/api/receipt/tracking` | 9 * |

\* `mnuReceiptTracking` has no click handler in `mdiMain.frm`; it is gated like Finance > Receipt Enquiry, the other screen that reads receipts, position 9 (a decision, recorded in `AccessRight.cs`).

## Receipt Tracking

Read only (`sql/main/500_receipt_tracking.sql`, `Receipt/Tracking/ReceiptTracking.cs`). Follows the receipts that came in from **members**, **payors** and **MNRB**. The desktop had twelve lists (`ReceiptTracking` cases 1 to 12 in HISPayment, called with a text WHERE fragment); there are two choices over one search, the kind of receipt (**Receipts from**: Member, which the form opens on, Payor, MNRB) and the layout (**Reports Type**):

| Reports Type | What it lists | Desktop cases (member, payor, MNRB) |
|---|---|---|
| **By Claim** (the form opens on it) | one line per receipt line: receipt number and date, amount, payor, member number and name, claim (case-version), debit note, remarks, accounting period (member list) or bordx, worksheet, debit note, remarks and accounting period (payor and MNRB lists) | 1, 4, 7 |
| **By Bordx** | each bordx, its date, the number of cases and the amount received | 2, 5, 8 |
| **By Receipt No** | each receipt, its date, the number of cases and the amount received | 3, 6, 9 |
| **By Payor** | each payor, the number of receipts and the amount received | 10, 11, 12 |

- **Criteria:** health type (S - Sihat, N - Non-Sihat), payor, bordx number range, receipt number range, receipt date range, and, for By Claim only, member number and name (contains), claim number range, accounting period month and year ranges, debit note range and amount range. A summary only uses the health type, payor, receipt date and the bordx and receipt number ranges; the other boxes are hidden, as the desktop ignored them. A blank criterion is not applied; text ranges compare as text (the desktop's month list was 1 to 12 and its year list 1999 to 3000, typed here).
- **Amount column:** the member list shows the receipt amount; the payor and MNRB lists show the **cheque amount** (`CheckAmt`), as the desktop listed it. The amount criterion is on the receipt amount in all three.
- **Messages:** `No record found!`, `Report cannot be printed without any record.`; **Print to File** exports the list on the screen to CSV. **Records** and **Total Amt** count the lines listed (and add the amount column).
- **Server:** the list is cut at 5,000 rows and says so (the member receipts are 21,812). The summary counts `COUNT(BordxNo)` / `COUNT(ReceiptNo)` as the desktop did, so a line with no receipt number counts 0 cases.
- **Differences:** the amount boxes are checked as numbers on the screen (the desktop said "Amount From is not Integer! It would be bypassed!" for a bad From box and silently ignored a bad To box). The code that built the criteria three times (once per layout) and the 12 listing routines are gone.
- **Data:** member receipts 21,812; payor receipts 23,885; MNRB receipts 10 (its summary view is empty). Summary views: 2,810, 15,826 and 0 lines.
- **Tests:** `ReceiptTrackingTests.cs` (validator, service, and each view and criterion against direct counts and sums); `e2e/receipt-tracking.mjs` (24 checks).

## Code map

| Layer | Files |
|---|---|
| SQL | `sql/main/500_receipt_tracking.sql` (and `sql/genisis-main.sql`); synonyms `genisis.Pay_TrackMember`, `Pay_TrackMember2`, `Pay_TrackPayor`, `Pay_TrackPayor2`, `Pay_TrackMnrb`, `Pay_TrackMnrb2` in `000_setup.sql` |
| API | `Genisis.Api/Receipt/Tracking/ReceiptTracking.cs`, `Receipt/ReceiptServiceCollectionExtensions.cs`, `Controllers/ReceiptTrackingController.cs`, `Auth/AccessRight.cs` |
| UI | `GenisisUI/src/pages/receipt/` (`receiptTrackingRules.ts`, its test, `ReceiptTrackingPage.tsx`), the route in `App.tsx`, the Receipt section and rail icon in `components/AppLayout.tsx`, `auth/access.ts` |
