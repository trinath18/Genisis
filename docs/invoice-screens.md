# Invoice menu — the four remaining required screens

Invoice Processing (`frmInvoice`) is described in [invoice-processing.md](invoice-processing.md). The other four items the business marked **Required** are migrated here:

| Menu item | Control | Desktop form | Web route | API | Access |
|---|---|---|---:|---|---:|
| Original Invoice Update | `mniOriginalInvUpd` | `frmUpdateInvoice` (513 lines) | `/invoice/original-update` | `/api/invoice/original` | 22 * |
| Invoice MBM Accounting | `mnuInvMBMAcc` | `FrmInvoiceMBMAC` (1,556 lines) | `/invoice/member-accounting` | `/api/invoice/member-accounting` | 27 * |
| Invoice HOS Accounting | `mnuInvHOSAcc` | `FrmInvoiceHOSAC` (1,263 lines) | `/invoice/hospital-accounting` | `/api/invoice/hospital-accounting` | 27 * |
| Invoice Tracking | `mnuInvTrack` | `FrmInvoiceTracking2` (2,808 lines) | `/invoice/tracking` | `/api/invoice/tracking` | 27 |

**Access.** Only Invoice Tracking has a click handler in the desktop (`mnuInvTrack_Click`: `CheckAccess(27)`). The other three menu items have **no click handler, are hidden, and no code opens their forms** (`frmUpdateInvoice`, `FrmInvoiceMBMAC` and `FrmInvoiceHOSAC` are in the project but never shown). They are gated like their closest sibling, as Worksheet Send to A/C and Send to A/R were: Original Invoice Update like Invoice Processing (22, it edits the same invoices), the two accounting items like Invoice Tracking (27). These are decisions to confirm with the business; `AccessRight.cs` and `access.ts` hold them in one place each. (`Invoice HOS (DR to MBM)`, `Invoice HOS (CR to MBM)` and `Invoice Tracking (Jupiter)` are not required and not built.)

**How old the accounting forms are.** The two daily batch counters (`INVACHSEQ` for hospitals, `INVACMSEQ` for members) were last used on 10 August 2004 and 19 August 2005 (4 and 427 batches, at most 16 on a day), and 137 of the 91,108 invoices have ever been marked as printed. The accounting screens were therefore built against data that has not been written for fifteen years, and their writes are checked in rolled-back transactions only.

## Original Invoice Update

Go finds a case's invoices of one worksheet version; Update records the invoice number, the date the original invoice was received ("OIR Date"), the invoice date and the hospital, and marks the original as received (`INVOrigRecStatus = 'Y'`).

Kept: the messages and their order (`Please Enter Case No`, `Please Enter Case Version`, `No record found!`; `Please Enter Invoice No!`, `Please Enter Invoice Date!` — which is about the OIR date, as in the desktop — then `Are you sure?` and `Record Updated Sucessfully`), Go locking the case and version until Clear, and the hospital drop-down being enabled only for an invoice that is paid to a hospital the database knows.

Different, on purpose:

* **Several invoices.** The desktop opened a recordset on the version and edited its first row, so the other invoices of a version could never be reached; 4,899 of 75,859 versions have more than one. A picker lists them when there are several.
* **The hospital** is chosen and saved by its code; the desktop looked it up by name, which is not unique. It changes only when the code is a hospital (the desktop changed it only when the name was found).
* **A blank invoice date** leaves the date as it was; the desktop assigned the mask text to the date column and failed. (It also showed the invoice date only when the OIR date was set, a copy-and-paste slip.)
* No check for an invoice a worksheet has used: the desktop has none.

## Invoice HOS Accounting and Invoice MBM Accounting

Both list invoices that were not yet handed to accounts (print status `N - New`, `INVPrintStatus = '0'`) or were (`R - Reprint`), by date, hospital or payor and batch. **Print to File** takes the next batch of the day (`H` or `M`, the day as ddmmyyyy, a hyphen and the number of the day, from `INVACHSEQ` / `INVACMSEQ` under a lock), marks the listed invoices as printed in it (batch, user, printed mark, and the amount payable as the previous amount) and exports the list; a reprint marks nothing. **Preview** exports without marking. The list's DR and CR columns work the amount printed before against the amount payable now, with the desktop's four cases (`InvoiceAccountingRules.DebitCredit`). The MBM form also has **Update**, which gives the ticked invoices an accounting month and year.

The export is a CSV that opens in Excel (the desktop wrote to Excel through automation). Print and Update are each one transaction; a search that matches more than the 5,000 rows a list holds is refused for Print, so a print never marks invoices that were not listed (the desktop listed and marked every match).

Different, on purpose (each read out of the desktop's code):

* **Batch date criteria.** The desktop compared the text column `INVBatchDate` (`H23102026`) with a typed date as text, which cannot match; the day inside the batch is compared as a date (`genisis.InvoiceBatchDay`).
* **Hospital criterion.** The desktop compared the hospital's name with its hyphens turned into blanks, so a hospital with a hyphen in its name was never found; the hospital's code is used.
* **HOS print marks only hospital-paid invoices.** The desktop's update selected every invoice of the version, whoever it was paid to; the member print already filtered on `INVPayTo = 'M'`.
* **HOS batch number.** The desktop kept one character (`Mid(batch, 11, 1)`), so batch 12 became 1; the whole number is kept (the member print kept three).
* **MBM list.** Like the desktop it lists the worksheets with an amount payable to the member by invoice, which includes the 96 hospital-paid invoices of such worksheets (the first column says who each was paid to); the print marks only the member-paid ones, as the desktop's does, so a hospital-paid row stays "new" after a print.
* **MBM Update.** The list the ticks come from has no invoice number (its view has none) and the desktop built the ticked rows' keys from the wrong list columns, so its update could not find anything. The update goes to the member-paid invoices of each ticked worksheet version, as the print does.
* **MBM "Print" (the ASCII file) is not migrated.** Its query asks `VIEW_InvoiceAccount` for `INVNo`, `AccPeriodMonth` and `AccPeriodYear`, which that view does not have, and it never clears the line it builds, so each line would contain all the earlier ones; it could not have run on this schema. The HOS form has the same routine and no button for it.

## Invoice Tracking

Five reports over the invoices, chosen in "Rpt Selection", with the desktop's criteria (invoice number, stay type, pay type, version, pay to, bordx number, invoice and registration dates, hospital, group company, invoice amount, payor, worksheet number, DOA, DOD and bordx date; every one with an "is empty" tick except stay type, pay type and hospital): `0 Invoice Month (All Hospitals)`, `1 Invoice Month (By Hospitals)`, `2 General Listing`, `3 General Listing (Detail)` and `4 Summary (By Hospitals)`. The headers are the desktop's; Payable2MBM, which the desktop gave a width of 0, is left out of the grid and kept in the file. Changing the report clears the list, as in the desktop. Print to File downloads the report as a CSV.

Report 3 reads `VIEW_Invoice_Tracking` as it is, FULL OUTER JOIN and all, so it also lists the cases that have no invoice (5,945 of 97,711 rows); that is what the "is empty" ticks are for.

Different, on purpose:

* **The four summaries are worked out per worksheet version.** The desktop sent them to `dbo.InvoiceTracking`, which joined worksheets to invoices by **case number only**, so every invoice was counted once for every worksheet version of its case: for January 2021 (3,753 invoices adding up to 9,648,529.38) it gave 15,177 "cases" and an invoice amount of 27,309,937.05 — 4.05 times too much on average (up to 63 versions of one case). Here each invoice is counted once, and each worksheet's payable amounts once however many invoices it has; the month, hospital and listing reports add up to each other and to the invoices (tests and the browser suite check it). **This changes the figures the desktop printed; confirm it with the business.**
* **"Summary (By Hospitals)"** asked the procedure for its case 4 and read month columns that case does not return, so it failed; it gives the layout the form declares for it (hospital, payable to the hospital, payable to the member, invoice amount), with the number of worksheets added.
* **Stay type "4 - Follow-up & Pre-Hospitalisation"** built `AND (INVStayType = 'P' OR INVStayType = 'F'` without its closing bracket and failed; it works here.
* **The summaries' criteria** named columns that more than one table has (`CASNumber`, `PAYCode`), which failed in the desktop; they are typed parameters here.
* The combo boxes the form shows but never read (claimability, insured type, A/C period) are not offered.
* **Not migrated:** a scan code (`LO…`) typed into the worksheet number, which opened another form through `SearchForm` (its branches are commented out in `Medix.bas`).

## Tests

* API (`InvoiceOriginalTests`, `InvoiceAccountingTests`, `InvoiceTrackingTests`; 51): the checks and their order, debit and credit, the batch label, the services against fake repositories (commit, rollback, the refusals), and database tests against MYDELL. The database tests that write do so inside a transaction that is rolled back, and then check that nothing changed; the tracking tests check each summary against SQL on the invoices and against each other.
* UI (Vitest, 42): the checks, the query parameters, the columns of each report, the debit/credit text and the rows after a print.
* Browser (`invoice-original-update.mjs` 23 checks, `invoice-accounting.mjs` 44, `invoice-tracking.mjs` 35): the real screens against the real API; every figure is compared with the API's. The POSTs that would mark invoices or set a period, and the PUT of Original Invoice Update, are intercepted and answered by the test, so nothing is written; the downloads are real and read back.
