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
| `genisis-main.sql` | All `main/` files in one script (generated): run it on the **main** database |
| `maintenance/000_setup.sql`, `maintenance/010_auth.sql` | Login and password change |
| `genisis-maintenance.sql` | All `maintenance/` files in one script (generated): run it on the **maintenance** database |

## Install or update (local, test or production)

Run both combined files: `genisis-maintenance.sql` on the maintenance database, and `genisis-main.sql` on the main database.

1. Open `genisis-main.sql` in SSMS.
2. In the toolbar's **Available Databases** box, pick the **main** database (the one with `dbo.PLN` and `dbo.MBM`).
3. If your maintenance database is not called `HISMaintenance`, change `@MaintenanceDb` near the top of the file. The worksheet and payment databases
   (`@WorkSheetDb` = `HISWorkSheet`, `@PaymentDb` = `HISPayment`) are optional: change the names if yours differ; if a database does not exist its synonyms are
   skipped and only the screens that read it (Case Transfer, Inpatient Case Listing, Claim ASCII, the audit-review payments) report it as missing.
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
