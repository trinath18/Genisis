# Payment screens

Menu path: **Payment** (legacy `mdiMain.frm`, `mnuPaymentModule`). Of its Required items three are built; two more are another door to a built screen; the rest have no form in the source tree (see [remaining-work.md](remaining-work.md)). None of the Payment menu items has a click handler in `mdiMain.frm`, so each form was matched by name and caption, and the access position is a decision (12, the one the desktop checks for Finance > Suspend / Active, the one payment screen it does check).

| Menu path | Legacy form | Web route | API | Access position |
|---|---|---|---|---:|
| Payment > Contra Allocation | `frmContraAllocation` | `/payment/contra-allocation` | `/api/payment/contra-allocation` | 12 * |
| Payment > Enquiry > Payment To Hospital | `FrmEnquiryPaymentHospital` | `/payment/enquiry-hospital` | `/api/payment/enquiry-hospital` | 12 * |
| Payment > Enquiry > Payment To Member | `FrmEnquiryPayMember` | `/payment/enquiry-member` | `/api/payment/enquiry-member` | 12 * |
| Payment > Payment Tracking | `FrmPaymentTracking` (**source missing**; rebuilt on the `PaymentTracking` procedure it called) | `/payment/tracking` | `/api/payment/tracking` | 12 * |
| Payment > Update Invoice | `frmUpdateInvoice` ("Update Invoice") | **another door** to Invoice > Original Invoice Update, `/invoice/original-update` | `/api/invoice/original-update` | 22 (that screen's) |
| Payment > To Member > Chq. Reissue | `frmPaymentMBMReissue` (source missing) | **another door** to Finance > Suspend / Active, `/finance/suspend-active` | `/api/finance/suspend-active` | 12 (that screen's) |

## Contra Allocation

`sql/main/650_payment_contra_allocation.sql`, `Payment/ContraAllocation/ContraAllocation.cs`, `pages/payment/ContraAllocationPage.tsx`. The contra rows of `HISPayment.ContraAllocation` (a claim's amount set against another claim, written by Receipt > From Member and Payor) are searched by Ref No, Status (all, allocated, not allocated), Case No (range, or **Use Scan**: one claim, added to the list), Contra Case No, Allocation No and Allocation date; the rows ticked (or **Check All**) are allocated by **Update**: one allocation number, today's date and the status A, in one transaction.

- **Differences from the desktop.** (1) The desktop's Ref No "to" test used the "from" box, so a "from" alone found nothing; both boxes are used. (2) It took the number from the first row of `HISMaintenance.ContraAllcSeq`, a counter with one row per allocation code that the bank contra, hospital adjustment, bank charge and float allocation forms also use (AA, AH, AB, AF), and wrote a six-digit number to its five-character column; in this database that is the AR row (03104) and the write cannot work. The number continues the series on `ContraAllocation` (100001, 100002 ...) and is taken under an application lock, so two people cannot get the same one; the shared counter is not touched. (3) It updated "the first row of the case and version"; the rows ticked are updated by their own key, and a row already allocated is refused ("Record has been allocated!") on the server as well. (4) The list also holds the receipt-only rows the Receipt screens write (no claim: 2,700 of 2,745 here); the desktop let them be ticked, found no claim and still said it had allocated; they cannot be ticked ("The record has no claim to allocate."). (5) **Check All** ticked the rows through a window message that skipped the allocated check; it ticks the rows that can be ticked.
- **Tests:** `ContraAllocationTests.cs` (validators, service, database: the search filters, the numbering and the stamping, the refusals, rollback), `contraAllocationRules.test.ts`, `e2e/payment-contra-allocation.mjs` (the POST is intercepted).

## Enquiry > Payment To Hospital

`sql/main/670_payment_enquiry_hospital.sql`, `Payment/EnquiryHospital/PaymentEnquiryHospital.cs`, `pages/payment/PaymentEnquiryHospitalPage.tsx`. Read only: the claims payable to hospitals (`View_PaymentHosp_Enq`, 52,059 rows: one per payment) with the 26 columns of the desktop's list. **Use Scan** adds one claim to the list (a scan code that starts with LO opens that screen); with it off a claim range is searched. Criteria: Hospital, Payor, Health Type, WS Claimability, Pymt Status (all, paid, unpaid, part paid and the two combinations), Inv No, Inv Date, PV No, PV Date, SUN PV No and SUN PV Date, each range with its "empty" box. **Print to File** writes a CSV.

- **What the desktop asked again for every row, answered with the list:** the receipts of the claim (`REceiptInfo`: SP_Payment 'Receipt' over `VIEW_Receipt`; the receipt numbers are MNRB or payor, then the member's, the amount the three added) and, for a part-paid claim, the sum of its payments (`Payment2HOS`) and what is left.
- **Differences:** the desktop's empty boxes for Inv Date and PV No tested columns the view does not have (`PVDate`, `PVNo`) and failed with an SQL error; they test the invoice date and the PV number. The empty box of SUN PV Date had no code; it works. A claim with several receipts showed the last one the view happened to return; the one with the highest number is shown (the view has no order). The total (Pay Hosp Amt) is of every row in the list, not of the last search. The search recompiles (the criteria are optional; 52,000 rows take 2 seconds unfiltered).
- **Tests:** `PaymentEnquiryHospitalTests.cs`, `paymentEnquiryHospitalRules.test.ts`, `e2e/payment-enquiry-hospital.mjs`.

## Enquiry > Payment To Member

`sql/main/680_payment_enquiry_member.sql`, `Payment/EnquiryMember/PaymentEnquiryMember.cs`, `pages/payment/PaymentEnquiryMemberPage.tsx`. Read only, three lists on tabs: **Payment To Member** (the claims with a debit / credit note and their payments, 19 columns), **Receipts** (a line for each receipt a claim has from MNRB, the payor and the member) and **Reissue** (the payments with PV status R). Criteria: Case No and Ver (Use Scan adds a claim to the lists), Member No, Member Name (contains), Payor, Health Type, WS Claimability, Pymt Status, PV No, PV Date, CR No, A/C Period month and year, SUN PV No and SUN PV Date, each range with its "empty" box.

- **Differences:** (1) the desktop asked for PV No both as a range and as equal to the "from" box, which left one number, and its A/C period year "from" test looked at the "to" box; both are plain ranges, and the period (kept as text) is compared as a number. (2) The desktop's text-built search named columns that several of its nine joined tables have (`CASNumber`, `PVNo`), which SQL Server rejects as ambiguous, so its search could not have run on this database; the select is trimmed to the five tables it needs and every column is qualified. (3) The Reissue list's patient name and pay-to columns were never filled (the select had no `PVPayTo`); the member's name and the payment's payee are shown. (4) The receipts of a claim already in the Receipts list are not added again (`ReceiptNoCheck`), as before.
- **Tests:** `PaymentEnquiryMemberTests.cs`, `paymentEnquiryMemberRules.test.ts`, `e2e/payment-enquiry-member.mjs`.

## Payment Tracking (rebuilt: no source)

`sql/main/690_payment_tracking.sql`, `Payment/Tracking/PaymentTracking.cs`, `pages/payment/PaymentTrackingPage.tsx`. Read only. `FrmPaymentTracking` is listed in `medixhis1.vbp` but its `.frm` is not in the source tree; what it did is known from what it called: `HISPayment.dbo.PaymentTracking` with a case number from 1 to 17 and a text WHERE fragment, over two views. One procedure with typed parameters replaces it.

| Payment | View (synonym) | Report types (desktop cases) |
|---|---|---|
| **To Hospital** (the screen opens on it) | `VIEW_PaymentHos_Tracking` (`genisis.Pay_TrackHospital`, on `Payment2HOS`) | By Claim 1, By Bordx 2, By Claim (Total) 3, By Hospital & PV Date 4, By Member 5, By Payor & PV Date 6, By PV No 7, By Hospital 8 |
| **To Member** | `VIEW_Payment_Tracking` (`genisis.Pay_TrackPaymentMember`, on `Payment2MBM`) | the same eight, cases 9 to 16 |

- **Criteria** (the columns the views have, since the form's boxes are not known): health type, payor, payment status (F full / P part, the values in the data), member number and name (contains), hospital (contains), claim, bordx and PV number ranges, PV date range, accounting period month and year ranges, and, for member payments, the debit note range. Every criterion applies to every report type, as the desktop appended the same fragment to all of them.
- **Counting:** a summary counts the grouping column (`COUNT(BordxRefNo)` and so on), as the procedure did, so a line with no key counts 0 cases. **Records** and **Total Amt** add up the lines listed; **Print to File** writes the list as CSV; the list is cut at 5,000 rows and says so.
- **Not reproduced:** case 17 (the invoices of a hospital payment) selected `INVBordxRefNo`, `InvNo` and `InvAmount`, which `VIEW_PaymentHos_Tracking` does not have, so it could not run.
- **Assumptions for the Payment users to confirm:** the captions of the report types and of the criteria (the form is missing); access position 12 like the other Payment screens.
- **Data here:** 24,600 hospital and 7,211 member payments (2021-02 to 2021-08). Most of them do not join a claim in this development copy of HISWorkSheet, so the member, hospital, payor and bordx columns are often empty.
- **Tests:** `PaymentTrackingTests.cs` (validator, service, database: a PV range and payor against direct counts, the hospital and PV summaries against direct counts and sums), `paymentTrackingRules.test.ts`.

## What is not built, and why

The forms of **Payment > To Hospital, To Member > Pay2MBM(Auto), Pay2MBM, Chq. Cancellation, Payor, Others and PV Update (Manual)** are not in the source tree: `medixhis1.vbp` (the older project file) lists `FrmPaymentHospital`, `FrmPaymentPayor`, `FrmPaymentOthers`, `FrmPaymentManual`, `FrmPaymentTracking`, `FrmPaymentSubAmtPaid`, `FrmPayMember`, `frmPayMemberJul08`, `FrmPaymentMBMReverse` and `frmPaymentMBMReissue`, and none of the ten is in the folder. They write payment vouchers, cheques and the accounts they feed. They can only be rebuilt from the `HISPayment` tables with the Payment users, as the Finance screens were; guessing the PV numbering, the cheque and the gain / loss rules would write wrong accounting data. The sidebar shows them disabled.
