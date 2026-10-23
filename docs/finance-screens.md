# Finance screens — VB6 → web

The eight items of the desktop **Finance** menu (`mdiMain.frm`), migrated to the React UI (`GenisisUI/src/features/finance/*`) and the
API (`Genisis.Api/Finance/*`, `Controllers/Finance*Controller.cs`, procedures `sql/main/360…370`). All database changes are additive: new
`genisis.*` procedures and synonyms; no existing table, view or procedure is changed, and the desktop keeps working on the same data.

Only **SUN Posting** has usable VB6 source. Five forms are not in the source at all, and the Invoice Receipt forms in the source write tables
that have had no rows since 2010. Those screens were **rebuilt from the production data**: the tables, numbering, signs and status codes the
real rows show, plus the sibling forms that do exist. Every assumption is listed below for confirmation by the Finance users.

| Menu item | VB6 form | Source | Web route | Access | Writes |
|---|---|---|---|---:|---|
| Receipt Enquiry | frmReceiptEnquiry | missing | `/finance/receipt-enquiry` | 9 | – |
| Invoice Receipt (Outstanding) | FrmRecMCO | stale (RECMCO, last row 2010) | `/finance/invoice-receipt` | 9 | ReceiptSummary, RecSeq |
| Short Payment Receipt | frmReceiptAdjustment | missing | `/finance/short-payment` | 9 | ReceiptSummary, RecSeq |
| Reverse Receipt | frmReverseReceipt | missing | `/finance/reverse-receipt` | 9 | ReceiptSummary, ReceiptfrPAY, RecSeq |
| Notification / Suspension | frmRecMCOManual | stale | `/finance/receipt-manual` | 12 | ReceiptSummary |
| Suspend / Active | frmPaymentMBMReissue | missing | `/finance/suspend-active` | 12 | Payment2MBM, PV sequences |
| Electronic Banking | frmFileExportBankESP | missing | `/finance/electronic-banking` | 10 | – (file only) |
| SUN Posting | frmPostingInvoiceMBM | **present** | `/finance/sun-posting` | 13 | SUNPosting, SunPostBatch |

## SUN Posting (ported from source)

- Search = legacy `SUN_HIS_Posting 'InvMBM'` over `HISWorkSheet.dbo.VIEW_SUN_Posting_MBMInv`, with every criterion a typed parameter
  instead of a WHERE fragment from the screen. Verified row-for-row against the legacy procedure (306 / 1,796 / 23 rows on three filters).
- Journal: two lines per invoice; MJC for post type P, MJD for N (amounts shown positive). Line 1 member, account 8200+payor (6200+payor
  for an N post of a bordx reversal); line 2 patient, 6000+payor, opposite side. Search CR / DN: one line, stored amount. Product category SM
  for health type S, else `PLN.PLNProCat`.
- Post: the desktop's checks and messages in order; batch number `SunPostBatch` + 1 taken with a lock; one `SUNPosting` row per invoice
  (InvMStatus/PostStatus 1, PostType InvMBM); all in one transaction. **Change:** an invoice already posted as InvMBM is refused by the
  database (the desktop only checked the screen).
- Time gate (`TimeMain` POSTING, `USR.SpecialAccess`) kept as the desktop had it: posting is closed until after the end time unless the
  user has special access.
- Journal file: CSV download with the desktop's columns and name (`<batch> MJC-Member yyyymmdd`), instead of a copy to `\\Svr-Sun3`. The
  post type of an existing batch is read from its amounts when not given.

## Receipt data (evidence for the rebuilt receipt screens)

- Receipt header: `HISPayment.dbo.ReceiptSummary` (20k rows to 2021). No VB form or procedure writes it, so only the missing forms did.
- Receipt numbers: `RecSeq` per prefix (RA, RB, RC, RG), the same generator as `GenerateSRecNo` in `Medix.bas`.
- Claim receipts (IPCLM / OPCLM): header + one `ReceiptfrPAY` line per case / version / bordx (`CheckAmt` = amount allocated).
- Fee receipts (IPFEE / OPFEE): header only; `ReceiptDesc` = the member MCO invoice number (`MBMMCOInvoice.MCOInvNo`), sometimes
  `<payor>-<invoice>`; `RecPayCode` = the invoice's payor (prefix of its membership numbers). Partial payments are common.
- Reversals: negative receipts (RB47998, RB53907), never deletes. Write-off / gain-loss / discount columns are always 0.
- SUN: `RecPayStatus` 1 + `RecBatchNo` once posted (same batch series as SUN Posting).

## Receipt Enquiry

Read only. Search by receipt no., date, type, payor, cheque no., SUN batch, posted, description, or a case allocated on the receipt. A receipt
shows its claim lines, the fee invoice it names (by `ReceiptDesc`, also the part after the last `-`), its SUN postings, allocated /
unallocated amounts and the receipt that reversed it.

## Invoice Receipt (Outstanding) / Notification / Suspension / Short Payment Receipt

One screen in three modes. Lists member MCO invoices with bill (sum of `BillMCOAmt`), received (fee receipts whose description is the invoice
no. or ends with `-<invoice no>`) and outstanding. Recording a receipt saves an unposted `ReceiptSummary` row: type IPFEE / OPFEE, description
= invoice no., payor = invoice payor, cheque ONLINE by default.

- Invoice Receipt: next number of the chosen prefix (RB by default) from `RecSeq`, locked; numbers already used are skipped.
- Notification / Suspension (the manual form): the receipt number is typed; a used number is refused.
- Short Payment Receipt: only invoices with a receipt already and a balance still owed, to receipt the balance.
- An amount above the balance, or a receipt on an invoice with nothing outstanding, asks for confirmation (over-payments exist in the data).
- Searching one invoice number shows it whatever its balance; "Outstanding only" applies to ranges.

## Reverse Receipt

A new receipt with the negative amount, numbered from `RecSeq` with the original's prefix (RB if the original's is not a sequence prefix),
description `REV <original no>-<original description>` (names the original; a reversed fee receipt still counts against its invoice). A claim
receipt's `ReceiptfrPAY` lines are copied negative under the new number. Refused: already reversed, a reversal, no number, not positive.

## Suspend / Active

Member payment vouchers (`Payment2MBM`), status codes from `frmPostingPay2MBM`: A Active, C Cancel, E Expired, R Reissue. As the data shows
(e.g. ST00073162):

- Stop (C / E): a row with the same PV no., negative amount, original cheque no. / date, action date as PV date, cheque status Y.
- Re-issue (R): a row with a new PV no. of the same prefix (CB `PVNSeq2013`, CC `PVMedicaSeq2013`, CG `PVMedicaMXSeq`, locked, skipping
  numbers used by member or hospital PVs), positive amount, new cheque no. / TT reference, remark; bank / account optional.
- Only an active (A / R) positive voucher can be stopped once; only a stopped one re-issued once. Account numbers are masked on screen.

## Electronic Banking

No bank file layout exists in the code or databases (the `WebMedix_*Maybank*` procedures are a Maybank-staff payroll list, and their data
is empty). Electronic payments are the ONLINE vouchers: `Payment2MBM` (bank / account on the voucher) and `Payment2HOS` (one PV no. per
payment). `PVChequeNo` holds the crediting date (ddmmyyyy). The screen lists one row per PV (stopped member payments left out), checks the
account length against `HISMaintenance.dbo.BankAccounts`, and builds a file of the ticked payments. Accounts are masked on screen and only
written to the file. Nothing is marked as exported.

The layout is configuration, section `Finance:BankFile` (defaults shown):

```json
"Finance": { "BankFile": { "Delimiter": ",", "IncludeHeader": true, "DateFormat": "dd/MM/yyyy", "FileNamePrefix": "ONLINE-PAYMENTS", "Extension": "csv",
  "Columns": [ "CreditDate", "PvNo", "PayeeType", "Payee", "Bank", "Account", "Amount", "Cases" ] } }
```

Columns may be any of CreditDate, PvNo, PvDate, CreditRef, PayeeType, Payee, Bank, Account, Amount, Cases.

## Open questions for the Finance users (the safe default in brackets)

1. Receipts: `ReceiptSummary` has no user / date columns. [The user is recorded in the API log only.]
2. Invoice payor when an invoice mixes payors (e.g. S34041: CT and ST members). [The first payor code alphabetically, which matched the
   receipts Finance entered.]
3. Is GST part of the invoice total? (`GSTAmt` equals `BillMCOAmt` in the data.) [Not added.]
4. Short Payment: is the shortfall ever written off or contra'd (the `VOID` / "CONTRA WITH NEGATIVE BORDX" lines)? [Not built: the screen
   receipts the balance only.]
5. Reverse Receipt: should a reversal also reset `Claim.PVRecPayorStatus`, `Bordx.BordxPAYRecDate` / `BordxPAYFRStatus` and add a case remark,
   as the receipt-from-payor form does on save? [Not changed.]
6. Suspend / Active: how is `PVAllocNo` (AR…) numbered on a re-issue? [Left blank.] Is "Suspend / Active" exactly this stop / re-issue
   workflow? [Yes, from the form name and the data.]
7. Electronic Banking: the bank's file layout; where hospital bank accounts are kept (none on the hospital vouchers in this database); should
   exported payments be marked? [Configurable CSV; hospital payments without an account cannot be selected; nothing is marked.]
8. SUN Posting: the posting time gate closes posting until after `TimeMain.EndTime`, which reads backwards. [Kept exactly as the desktop.]

## Tests

- API: `FinanceSunPostingTests`, `FinanceReceiptEnquiryTests`, `FinanceInvoiceReceiptTests`, `FinanceReverseReceiptTests`,
  `FinanceSuspendActiveTests`, `FinanceElectronicBankingTests` (rules, validation messages, units of work with fakes; database tests that roll
  back; HTTP access 401 / 403 and first messages); the architecture tests cover every Finance controller and namespace.
- UI: vitest for `journalRules`, `receiptRules`, `reverseRules`, `voucherRules`, `bankRules`.
- Browser: `GenisisUI/e2e/finance.mjs` (24 checks over the eight screens, read only).
- The Finance searches use `OPTION (RECOMPILE)` like the other catch-all searches: without it a plan cached for a wide search was reused
  for narrow ones and ran for minutes.
