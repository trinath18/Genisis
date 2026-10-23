# Database scripts

The API calls stored procedures in a `genisis` schema instead of sending SQL text. This folder holds those procedures.
They are new objects only: no existing table, view or procedure is changed, so the desktop app keeps working
against the same database.

| File | What it is |
| --- | --- |
| `main/000_setup.sql` | Checks the server and database, creates the `genisis` schema and the synonyms used to read the maintenance database |
| `main/010_plan.sql` | Maintenance > Plan, Plan tab |
| `main/020_annual_limit.sql` | Maintenance > Plan, Annual Limit tab |
| `main/030_premium.sql` | Maintenance > Plan, Premium tab |
| `main/040_membership.sql` | Membership > Enquiry |
| `main/050_registration_numbers.sql` … `main/080_registration_save.sql` | Membership > Registration |
| `main/090_code_lists.sql` | Maintenance > Plan, Health Type, Insured Type, Product Category, Supp. MCO Status and Special Note tabs |
| `main/100_benefit_mgmt_fee.sql` | Maintenance > Plan, Annual Limit (Benefit) and Case Mgmt. Fee tabs |
| `main/110_mco_fee.sql` | Maintenance > Plan, MCO Fee tab |
| `main/120_benefits.sql` | Maintenance > Benefits: Category, Sub Category, Benefit Entry and Benefits Listing tabs |
| `main/130_case_enquiry.sql`, `main/140_case_registration.sql`, `main/150_case_adjustment.sql` | Case > Enquiry / Tracking, Registration, Adjustment |
| `main/160_panel_hospitals.sql` | Case > Panel Hospitals (and the shared payor list) |
| `main/165_case_non_disclosure.sql` | Case > Non-Disclosure Info |
| `main/170_case_pay_claim.sql` | Case > Pay & Claim |
| `main/180_mma.sql` | Case > MMA/CPT |
| `main/190_case_audit_review.sql` | Case > Case Audit Review |
| `main/200_case_transfer.sql` | Case > Case Transfer |
| `main/210_case_listing.sql` | Case > Inpatient Case Listing (bordx) |
| `main/220_case_ascii.sql` | Case > Claim ASCII and Case ASCII |
| `main/230_worksheet_tracking.sql` | Worksheet > Tracking (and the insured-type list) |
| `main/240_worksheet_send_to_accounts.sql` | Worksheet > Send to A/C |
| `main/250_worksheet_send_to_ar.sql` | Worksheet > Send to A/R |
| `main/260_worksheet_enquiry.sql` | Worksheet > Worksheet Enquiry (saved worksheet, read-only) |
| `main/270_worksheet_engine.sql` | Worksheet calculation engine, steps 1 and 2: actual amounts from the invoices, the saved limit text and the summary inputs (used by Registration and Adjustment, in progress) |
| `main/280_worksheet_adjustment.sql` | Worksheet > Worksheet Adjustment: the Update button (payment details and remarks), bank / state lists |
| `main/290_worksheet_documents.sql` | Worksheet > Worksheet Adjustment: credit / debit note (debit number from `DRSeq`) and the Excess letter data |
| `main/300_worksheet_final_lg.sql` | Worksheet > Worksheet Adjustment: Final LG marks (invoice LG reference, case "FINAL GL Issued") |
| `main/310_worksheet_plan_limits.sql` | Worksheet engine step 4: the benefit rows (BNF) in force for the patient's plan on the admission date |
| `main/320_worksheet_plan_terms.sql` | Worksheet engine step 5: the plan's co-payment terms (co-insurance percentage and sign, Room & Board co-payment) |
| `main/330_worksheet_balance_limit.sql` | Worksheet engine step 5b: the patient's available limit (member's or covered person's, by the plan's annual limit type) |
| `main/340_worksheet_stay_days.sql` | Worksheet engine step 7: the days of the stay, summed from the invoices |
| `main/350_worksheet_calculation.sql` | Worksheet Registration calculation page: row captions, the figures saved with a version, a case's versions |
| `main/360_finance_receipt_enquiry.sql` | Finance > Receipt Enquiry (read only: ReceiptSummary, ReceiptfrPAY lines, fee invoice, SUN postings, reversals) |
| `main/361_finance_invoice_receipt.sql` | Finance > Invoice Receipt, Short Payment Receipt, Notification / Suspension: invoice balances, fee receipts, receipt numbers from `RecSeq` |
| `main/363_finance_reverse_receipt.sql` | Finance > Reverse Receipt: a negative receipt (and negative claim lines) under a new number |
| `main/364_finance_suspend_active.sql` | Finance > Suspend / Active: stop (C / E) and re-issue (R, new PV no.) member payment vouchers in `Payment2MBM` |
| `main/365_finance_electronic_banking.sql` | Finance > Electronic Banking: ONLINE member / hospital payments (read only; the API writes the file) |
| `main/370_finance_sun_posting.sql` | Finance > SUN Posting (member invoices, `InvMBM`): typed search replacing `SUN_HIS_Posting`, batch number, posting rows |
| `main/130_mma.sql` | Maintenance > Entity (Procedures, Look Up Table) and Maintenance > Accounts; tables in the maintenance database |
| `main/140_adjustment.sql` | Membership > Adjustment (all endorsement types) |
| `main/150_enquiry_tabs.sql` | Membership > Enquiry: HIS Benefits, Acc History, Fees, Non Disclosure, Policy Terms, LT Limit, Policy Wording, T/O Claims, WS |
| `main/160_bordx.sql` | Membership > Upload Bordx (verify, upload through UploadDb staging, cancellation, reinstatement, Generate ASCII). Needs the UploadDb staging database: `hosting/create-upload-db.sql` (a restore of HISDB does not contain it) |
| `main/170_tracking.sql` | Membership > Tracking: `genisis.Tracking_Run` (filters as typed parameters) and the drop-down lists |
| `main/171_tracking_cases.sql` | Membership > Tracking: `genisis.Tracking_CaseText`, the fixed query text of each report (generated by `tools/Generate-TrackingCases.ps1`) |
| `main/180_reinstatement.sql` | Membership > Member Reinstatement |
| `main/181_mco_pending.sql` | Membership > MCO Pending (maintenance database `MCOPending`) |
| `main/182_pluto.sql` | Membership > Upload Membership Excel (Pluto) and Generate Membership ASCII (Pluto) |
| `main/183_card_control.sql` | Membership > Card Sending Mode |
| `main/184_deletion.sql` | Membership > Deletion (copies to the archive database first) |
| `main/185_leaflet_card.sql` | Membership > Leaflet & Card |
| `main/186_temp_registration.sql` | Membership > Temp MBM Reg (previous membership as a renewal) |
| `main/187_temp_adjustment.sql` | Membership > Temp MBM Adj (temporary payor check) |
| `main/400_account_transaction.sql` | Maintenance > Account Transaction (MMA Schedule of Fees search) |
| `main/410_security.sql` | Maintenance > Security: Department (GRP), Role Management and User Permission (USR) |
| `main/420_mma_entry.sql` | Maintenance > MMA (Procedures/OPCS): schedule rates of up to six OPCS codes |
| `genisis-main.sql` | All `main/` files in one script (generated): run it on the **main** database |
| `maintenance/000_setup.sql`, `maintenance/010_auth.sql` | Login and password change |
| `genisis-maintenance.sql` | All `maintenance/` files in one script (generated): run it on the **maintenance** database |

## Install or update (local, test or production)

Run both combined files: `genisis-maintenance.sql` on the maintenance database, and `genisis-main.sql` on the main database.

1. Open `genisis-main.sql` in SSMS.
2. In the toolbar's **Available Databases** box, pick the **main** database (the one with `dbo.PLN` and `dbo.MBM`).
3. If your maintenance database is not called `HISMaintenance`, change `@MaintenanceDb` near the top of the file. The worksheet, payment and archive databases
   (`@WorkSheetDb` = `HISWorkSheet`, `@PaymentDb` = `HISPayment`, `@ArchiveDb` = `HISDMArchive`) are optional: change the names if yours differ; if a database does not exist its synonyms are
   skipped and only the screens that read it (Case Transfer, Inpatient Case Listing, Claim ASCII, the audit-review payments, Membership > Deletion) report it as missing.
4. Press **F5**. The Messages tab ends with `Done.`
5. Open `genisis-maintenance.sql`, pick the **maintenance** database (the one with `dbo.USR`), and press **F5**.

Run it again after every API update that changes this folder. It is safe to run more than once. If it is run on the
wrong database or an old SQL Server (it needs SQL Server 2016 SP1 or later), it stops with a message saying what to change
and does not print `Done.`.
If an API version is started before the script has been run, it answers with
"The database is missing the stored procedures this version needs".

## Conventions

- One file per screen, `CREATE OR ALTER PROCEDURE genisis.<Screen>_<Action>`.
- The API keeps validation and the transaction; procedures do the reads and writes and never commit.
- Guarded writes return a `Result` column: `0` done, `1` record not found, `2` refused (duplicate or still in use).
- Text parameters are `nvarchar(4000)` so a value that is too long still fails instead of being cut short.
- HISDB runs at **compatibility level 100**: do not use `TRY_CONVERT`, `FORMAT`, `IIF`, `CONCAT`, `EOMONTH`, `STRING_SPLIT` in `main/` scripts.
- Other databases are reached through synonyms (`Maint_*`, `WS_*`, `Pay_*`) so a save stays one transaction on the main connection.
- After editing a file in `main/`, rebuild the combined file: `GENISIS_WRITE_SQL=1 dotnet test --filter Combined_main_script_is_up_to_date`.
