# Posting screens

Menu path: **Posting** (legacy `mdiMain.frm`, top menu `mnuPosting`: Invoice > Hospital, Invoice > Member). The business marked Invoice (a parent menu), Invoice > Hospital and Invoice > Member Required. Invoice > Member is Finance > SUN Posting (`frmPostingInvoiceMBM`, built: [finance-screens.md](finance-screens.md)), so the Posting menu lists it as another door to that screen; Invoice > Hospital is built here.

| Menu path | Legacy form | Web route | API | Access position |
|---|---|---|---|---:|
| Posting > Invoice > Hospital | `frmPostingInvoiceHosp` ("Posting - HospInv") | `/posting/invoice-hospital` | `/api/posting/invoice-hospital` | 13 * |
| Posting > Invoice > Member | `frmPostingInvoiceMBM` | `/finance/sun-posting` (see Finance) | `/api/finance/sun-posting` | 13 |

\* `mnuPostInvHosp` has no click handler in `mdiMain.frm`; it is gated like Finance > SUN Posting (position 13), the same family of forms (a decision, recorded in `AccessRight.cs`).

## Posting > Invoice > Hospital

Reads and writes (`sql/main/610_posting_invoice_hospital.sql`, `Finance/SunPostingHosp/SunPostingHosp.cs`, `pages/posting/InvoiceHospitalPage.tsx`, `invoiceHospitalRules.ts`). The hospital invoices of the bordx (`VIEW_SUN_Posting_HospInv`: paid to the hospital, every payor but MH, ET, EQ, ES, EC, EM and EP; 45,064 invoices, 22,965 not posted) and the SUN journal made from them. The desktop sent a WHERE fragment to `SUN_HIS_Posting` ('InvHosp') for each of its three buttons; here every criterion is a typed parameter.

- **Search** (`SearchRecord`): health type, hospital, payor (the payors of the health type chosen), invoice date, invoice number, **Post Status** (No: neither `InvHStatus` nor `PostStatus` set; Yes: both 1), post date, **Bordx Status** (No: no bordx reference; Yes: has one), bordx date and post batch number. **Search** lists two journal lines per invoice, **Search CR** the credit lines alone, **Search DN** the debit lines alone; `No record selected ! ` when nothing matches. The footer counts the invoices and the debit and credit totals (equal) and says whether the list is posted.
- **The journal** (the desktop's lists): journal type **RC**, database MED, transaction reference the invoice number, accounting period of a posted invoice, invoice date, description the patient's name (25 characters; the covered person when the cover id is not 00), amount payable to the hospital, hospital column the hospital's account code without its first character, payor, case number (the posting list as case-version, the credit and debit lists alone), product category (SM for health type S, otherwise `PLN.PLNProCat` of the health type and payor), bordx reference (last 7 characters) and batch number. The **debit** line is to account `6000` + payor, the **credit** line to the hospital's account (`HOS.HOSAccCode`); the credit line carries the invoice index (`ClaimInvoice.INVIndex`), the key the post uses. The account codes come from the data (the hospital's `HOSAccCode`) and the desktop's code (`6000` + payor), so no journal sample was needed.
- **Ticking:** the two lines of an invoice are ticked together; a posted list cannot be ticked (`Record(s) has been posted ! `).
- **Post** (`CmdPost_Click`): in the desktop's order a ticked invoice (`Please select the record`), the invoice date to (`Please Enter Invoice Date(To)`), a bordx status other than No (`Please Change Bordx Status`), a list that is not posted already, and the accounting period (`Please Keyin for Accounting Period ! `); "Do you want to Post record(s) ?"; the posting window of Finance > SUN Posting (`TimeMain` 'POSTING', open for a user with special access); then in one transaction the next batch number (the shared `SunPosting_NextBatch`) and one `SUNPosting` row per invoice (`InvHStatus` 1, `PostStatus` 1, PostType `InvHosp`, today, the user, the accounting month and year and the batch). **Difference:** an invoice already posted as InvHosp is refused by the server (the desktop only stopped a posted list in the screen). `Record(s) Posted Sucessfully... !`; the batch's journal is listed and downloaded.
- **Journal file** (`CmdPrintFile_Click` after posting): `<batch> RC-HospInv yyyymmdd.csv` (the desktop wrote an Excel file with that name to `\\Svr-Sun3\Finance\Account Journal Import\HIS Not Posted\`; the web downloads it, and the columns are the list's). **Print to File** writes the list on the screen.
- **Not migrated:** the `Stop` button; the Check All box of the desktop, which flipped every line one by one.
- **Tests:** `SunPostingHospTests.cs` (the journal rules, the validators, the post flow and its refusals against a fake repository; the search by post status, hospital, invoice number and batch, and the post, the refusal of an invoice posted already and the rollback, in a rolled-back session, checked with a checksum); `invoiceHospitalRules.test.ts` (5); `e2e/posting-invoice-hospital.mjs` (31 checks; the post is intercepted).

## Code map

| Layer | Files |
|---|---|
| SQL | `sql/main/610_posting_invoice_hospital.sql` (and `sql/genisis-main.sql`); synonym `genisis.WS_SunPostingHospInv` |
| API | `Genisis.Api/Finance/SunPostingHosp/SunPostingHosp.cs` (reuses `SunPosting`'s journal line, CSV and window), `Controllers/PostingInvoiceHospitalController.cs`, `Auth/AccessRight.cs` |
| UI | `GenisisUI/src/pages/posting/` (`invoiceHospitalRules.ts`, its test, `InvoiceHospitalPage.tsx`), the route in `App.tsx`, the Posting section and rail icon in `components/AppLayout.tsx`, `auth/access.ts` |
