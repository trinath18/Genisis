# Payment screens

Menu path: **Payment** (legacy `mdiMain.frm`, `mnuPaymentModule`). Of its Required items nine are built; three more are another door to a built screen; the rest have no form in the source tree (see [remaining-work.md](remaining-work.md)). None of the Payment menu items has a click handler in `mdiMain.frm`, so each form was matched by name and caption, and the access position is a decision (12, the one the desktop checks for Finance > Suspend / Active, the one payment screen it does check).

| Menu path | Legacy form | Web route | API | Access position |
|---|---|---|---|---:|
| Payment > Contra Allocation | `frmContraAllocation` | `/payment/contra-allocation` | `/api/payment/contra-allocation` | 12 * |
| Payment > Enquiry > Payment To Hospital | `FrmEnquiryPaymentHospital` | `/payment/enquiry-hospital` | `/api/payment/enquiry-hospital` | 12 * |
| Payment > Enquiry > Payment To Member | `FrmEnquiryPayMember` | `/payment/enquiry-member` | `/api/payment/enquiry-member` | 12 * |
| Payment > Payment Tracking | `FrmPaymentTracking` (**source missing**; rebuilt on the `PaymentTracking` procedure it called) | `/payment/tracking` | `/api/payment/tracking` | 12 * |
| Payment > To Hospital | `FrmPaymentHospital` (**source missing**; rebuilt on `VIEW_Payment2HOSNew` and the rows of `Payment2HOS`) | `/payment/to-hospital` | `/api/payment/to-hospital` | 12 * |
| Payment > Payor | `FrmPaymentPayor` (**source missing**; rebuilt on `VIEW_Payment_Payor` and the rows of `Payment2PAY`) | `/payment/to-payor` | `/api/payment/to-payor` | 12 * |
| Payment > Others | `FrmPaymentOthers` (**source missing**; rebuilt on the `PaymentOthers` table, which has no row) | `/payment/others` | `/api/payment/others` | 12 * |
| Payment > To Member > Pay2MBM | `FrmPayMember` (**source missing**; rebuilt on `VIEW_Payment2MBMNew` and the rows of `Payment2MBM`) | `/payment/to-member` | `/api/payment/to-member` (source `dn`) | 12 * |
| Payment > To Member > Pay2MBM(Auto) | `frmPayMemberJul08` (**source missing**; rebuilt on `VIEW_Payment2MBMJul08`) | `/payment/to-member-auto` | `/api/payment/to-member` (source `auto`) | 12 * |
| Payment > Update Invoice | `frmUpdateInvoice` ("Update Invoice") | **another door** to Invoice > Original Invoice Update, `/invoice/original-update` | `/api/invoice/original-update` | 22 (that screen's) |
| Payment > To Member > Chq. Reissue | `frmPaymentMBMReissue` (source missing) | **another door** to Finance > Suspend / Active, `/finance/suspend-active` | `/api/finance/suspend-active` | 12 (that screen's) |
| Payment > To Member > Chq. Cancellation | `FrmPaymentMBMReverse` (**source missing**) | **another door** to Finance > Suspend / Active, `/finance/suspend-active` (Stop with status C) | `/api/finance/suspend-active` | 12 (that screen's) |

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

## To Hospital (rebuilt: no source)

`sql/main/700_payment_to_hospital.sql`, `Payment/ToHospital/PaymentToHospital.cs`, `Payment/PvAmountWords.cs`, `pages/payment/PaymentToHospitalPage.tsx`. `FrmPaymentHospital` is listed in `medixhis1.vbp` but its `.frm` is missing. What it read is known (SP_Payment 'Pay2Hosp' over `VIEW_Payment2HOSNew`); what it wrote is read from the 24,600 rows of `HISPayment.Payment2HOS`. The screen lists the claims not yet paid to their hospital, the claims ticked are paid by **Generate PV**, and the vouchers written are listed (**Print to File** writes them as CSV).

- **The list:** one line per claim (the view has one per invoice) where `CLMPayableByMedix2H` is not 0 and the claim has no `Payment2HOS` row. The view already leaves out payors MH, EQ, ET, EM, EC, ES, EP and MY and claims with no member. The criteria are payor, hospital (code or part of the name), case number, bordx number and bordx date. A claim cannot be ticked when its payment is held (`CASCrossReference.CASPayPending` = Y; the remark is shown), when its payor has no PV prefix, or when nothing is payable.
- **The vouchers, as the data has them:**
  - One PV per hospital (the invoices' payee, `INVPayee`) and PV prefix, with one row per claim. No PV in the data has two rows of one claim.
  - The PV number is the payor's `PAY.PVInd` plus the next number of its counter: CB uses `PVNSeq2013`, CC uses `PVMedicaSeq2013`, CG uses `PVMedicaMXSeq`, eight digits. This holds for 24,388 of the 24,393 PVs. The counter row is locked until the transaction ends. A number already on `Payment2HOS`, `Payment2MBM`, `Payment2PAY` or `PaymentOthers` is skipped, because the forms share the counters. `genisis.Payment_NextPv` is shared by the other Payment screens.
  - `PVHOSChequeAmt` is the PV total on every row and `HOSPaidAmt` is the claim's amount.
  - `PVHOSDate` and `PVHOSChequeDate` are both the PV date. `HOSAccPeriodMth` and `HOSAccPeriodYr` are its month (two digits) and its year.
  - `PVPayHOSStatus` is F and `PVStatus` is A. The charges, gain/loss, discount, excess and Medix payment are 0. `INVNo` is empty (as on every row).
  - `PVPayee` is the hospital's payee (`HOS.HOSPayee`, or the hospital name when the payee is empty). `HOSRemark` is the remark typed, which is empty on 97% of rows.
  - **ONLINE** (98% of rows): the cheque number is the PV date as ddMMyyyy and `PVChequeStatus` is Y. **CHEQUE**: the first cheque number is typed, and each later PV takes the next number (MBB026496, MBB026497, ...), as the cheques of one day run in the data.
  - The amount in words is `CurrencyConversion.bas` ported as it is, double spaces included. It agrees with the words of the vouchers already written; the test allows 1 in 200 to differ. A leading "And" is dropped and " Only" is added. Text longer than 55 characters keeps the ringgit on `PVAmtWording` and puts the cents on `PVAmtWordingTwo`.
- **All or nothing:** a claim that is paid meanwhile, held, not payable, or of a payor with no prefix stops the run, and nothing is written. A PV that adds up to 0 or less is refused.
- **Difference:** on the desktop, a long amount with no cents lost its " Only" (136 vouchers). Here it is written on the second line.
- **Assumptions for the Payment users to confirm** (the form is missing):
  1. The grouping is one PV per hospital and prefix.
  2. Held claims are refused.
  3. Cheque numbers are typed and then counted up. `ChequeType` and `PVAccNo` (the bank accounts per prefix) are not used; `PVBank` and `PVAccountNo` are empty on almost every row.
  4. No part payment is offered. F is written, P has not been seen, and the gain/loss, discount and bank charge columns are left at 0.
  5. Printing the PV and the cheque is not built. The desktop printed them through `frmPrintPVRange` and `frmPrintChequeRange`.
  6. Access position 12.
- **Tests:**
  - `PaymentToHospitalTests.cs`: the words against fixed amounts and against every hospital voucher on the database, the validator, cheque numbering, the grouping and refusals with a fake repository, and, on the database, the search and a PV numbered and written (rolled back).
  - `paymentToHospitalRules.test.ts`.

## Payor (rebuilt: no source)

`sql/main/710_payment_to_payor.sql`, `Payment/ToPayor/PaymentToPayor.cs`, `pages/payment/PaymentToPayorPage.tsx`. `FrmPaymentPayor` is missing. It read `VIEW_Payment_Payor`: the reversed claims, whose `CLMTotalApproved` is below 0. It left 552 rows in `Payment2PAY`, all from 2007 to 2009; the form has not been used since. The screen lists the reversed claims not yet paid back, and **Generate PV** pays back the claims ticked by cheque. The criteria are payor, case number, bordx number and close date.

- **The vouchers, as the data has them:**
  - One PV per payor and PV prefix, one row per claim.
  - `PVPAYto` is the payor code (the first two letters of the case number).
  - `PVPAYChequeAmt` is the PV total on every row (552 of 552); `PAYPaidAmt` is the claim's amount.
  - `PVPAYDate` and `PVPAYChequeDate` are both the PV date, and the accounting period is its month and year.
  - `PVPayPAYStatus` is F, `PVStatus` is A and `PVBankCharges` is 0. The gain/loss, discount, difference, excess and Medix payment are left empty, as on every row.
  - Every row has a cheque number: the first one is typed, and each later PV takes the next number.
  - The PV number comes from `genisis.Payment_NextPv` (see To Hospital). The 2007-2009 rows carry the older five-digit numbers (CB58259).
  - There are no words: `Payment2PAY` has no wording columns.
- **All or nothing:** a claim that is paid back meanwhile, not reversed, of a payor with no prefix, or with nothing to pay back stops the run.
- **Assumptions for the Payment users to confirm:**
  1. The amount paid back is the reversed amount (-`CLMTotalApproved`). The claims of the rows written are not in this copy of HISWorkSheet, so this cannot be checked.
  2. Payment is by cheque only. The table has no payment mode, and every row has a cheque.
  3. There is one PV per payor.
  4. Access position 12.
- **Tests:**
  - `PaymentToPayorTests.cs`: the validator, the grouping, cheque numbering and refusals with a fake repository, and, on the database, the search and a PV written (rolled back).
  - `paymentToPayorRules.test.ts`.

## Others (rebuilt: no source, no data)

`sql/main/720_payment_others.sql`, `Payment/Others/PaymentOthers.cs`, `pages/payment/PaymentOthersPage.tsx`. `FrmPaymentOthers` is missing. `HISPayment.PaymentOthers` has no row, and no view or procedure reads it, so the screen is built on the table's columns alone. It does two things:

- **Save** writes a payment voucher to any payee.
  - The fields are PV type (CB, CC or CG), PV date, Pay To, amount, cheque number and date (the PV date when left empty), the payee's bank and account, a case number when the payment is for a claim, bank charges and a remark.
  - The PV number is the next of its type's counter (`genisis.Payment_NextPv`).
  - `PaidAmt` = `PVChequeAmt` (one row per PV). The accounting period is the PV date's month and year. `PVStatus` is A, and the gain/loss, discount and difference are 0.
- **Search** lists the vouchers by PV number, PV date, payee (contains) and case number. **Print to File** writes the list.
- **Assumptions for the Payment users to confirm:** everything about this screen, since there is nothing to compare with:
  1. The payment is by cheque only.
  2. A voucher cannot be changed or cancelled once written.
  3. The case number is not checked against the claims.
  4. Access position 12.
- **Tests:**
  - `PaymentOthersTests.cs`: the validator, the service, and, on the database, a voucher written and found again (rolled back).
  - `paymentOthersRules.test.ts`.

## To Member > Pay2MBM and Pay2MBM(Auto) (rebuilt: no source)

`sql/main/730_payment_to_member.sql`, `Payment/ToMember/PaymentToMember.cs`, `pages/payment/PaymentToMemberPage.tsx` (one page, two routes). `FrmPayMember` and `frmPayMemberJul08` are missing. What they read is known, and what they wrote is read from the 7,211 rows of `Payment2MBM`.

| Screen | List (view) | Claims |
|---|---|---|
| **Pay2MBM** | `VIEW_Payment2MBMNew` (SP_Payment 'Pay2MBM') | The claims with a debit/credit note. Payors MH and AX are left out unless the plan pays members (`PLNPymtInd` Y). Claims with nothing payable are listed too. |
| **Pay2MBM(Auto)** | `VIEW_Payment2MBMJul08` | The claims with an amount payable to the member (`CLMPayableByMedix2M` not 0). The list adds the payor's receipt and the claim's pay mode. |

Both list only the claims with no `Payment2MBM` row. The criteria are payor, member number and name, case number, D/N number and close date. A claim clicked shows its payee, bank and account (`CLMPayTo` or the member's name, `CLMBank`, `CLMAccountNo`), which can be changed before the PV is written.

- **Generate PV:** one PV per claim; 618 of the 631 PVs in the data have one row.
  - `PVChequeAmt` = `MBMPaidAmt` = the amount payable.
  - The prefix is the payor's `PAY.PVInd` (634 of 636), numbered by `genisis.Payment_NextPv`.
  - `PVChequeDate` is the PV date. The accounting period is its month (two digits) and year.
  - `PVPayMBMStatus` is F and `PVStatus` is A. The charges, gain/loss, discount, difference and Medix payment are 0.
  - **ONLINE**: the cheque number is the PV date as ddMMyyyy and `PVChequeStatus` is Y. **CHEQUE**: the first cheque number is typed and counted up.
  - The amount in words is the same routine as To Hospital.
  - A claim with nothing payable, held, of a payor with no prefix, with no payee, or done meanwhile stops the run, and nothing is written.
- **Void:** closes the claims ticked without paying the member, as the desktop's 6,542 VOID rows do. `PVNo` and `PVChequeNo` are VOID, `MBMPaidAmt` is 0, there is no status, and the month is not padded (as in the data). The remark is optional (CASE DECLINED, CONTRA WITH NEGATIVE BORDX ... in the data). Any claim of the list can be voided; 5,551 of the VOID rows are of claims with an amount payable.
- **Assumptions for the Payment users to confirm:**
  1. Pay2MBM and Pay2MBM(Auto) differ only by their list. The desktop's other differences are not known: the Auto form's e-mail and SMS columns (`MBMEmail`, `ZUStatus`) are not used.
  2. There is one PV per claim, even for one member's several claims.
  3. Void is offered on both screens.
  4. The SUN posting of member PVs (`frmPostingPay2MBM`) and printing are not part of these screens.
  5. Access position 12.
- **Tests:**
  - `PaymentToMemberTests.cs`: validators, payee changes, cheque numbering, refusals and Void with a fake repository, and, on the database, both lists and a PV and a VOID written (rolled back).
  - `paymentToMemberRules.test.ts`.

## To Member > Chq. Cancellation (another door)

`FrmPaymentMBMReverse` is missing. Its own tables, `Payment2MBMReverse` and `PaymentMBMReverse`, have no row, and no view, procedure or form in the source reads them. The cheques cancelled in the data are rows of `Payment2MBM`: the same PV number, the negative amount, the original cheque, status C (cancelled) or E (expired), 26 rows. Finance > Suspend / Active writes exactly these rows ("Stop", status C or E), and SUN posting (`frmPostingPay2MBM`) reads them. So the menu item opens that screen, as Chq. Reissue does, and no second writer of the same rule is added.

- **Assumption for the Payment users to confirm:** a cheque cancellation is the C row of Suspend / Active, and nothing has to go to the two reverse tables. If the users did record cancellations there, the form needs to be rebuilt on them.

## What is not built, and why

The form of **Payment > PV Update (Manual)** are not in the source tree: `medixhis1.vbp` (the older project file) lists `FrmPaymentHospital`, `FrmPaymentPayor`, `FrmPaymentOthers`, `FrmPaymentManual`, `FrmPaymentTracking`, `FrmPaymentSubAmtPaid`, `FrmPayMember`, `frmPayMemberJul08`, `FrmPaymentMBMReverse` and `frmPaymentMBMReissue`, and none of the ten is in the folder. They write payment vouchers, cheques and the accounts they feed. They can only be rebuilt from the `HISPayment` tables with the Payment users, as the Finance screens were; guessing the PV numbering, the cheque and the gain / loss rules would write wrong accounting data. The sidebar shows them disabled.
