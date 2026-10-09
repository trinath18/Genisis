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
| `genisis-main.sql` | All `main/` files in one script (generated): run it on the **main** database |
| `maintenance/000_setup.sql`, `maintenance/010_auth.sql` | Login and password change |
| `genisis-maintenance.sql` | All `maintenance/` files in one script (generated): run it on the **maintenance** database |

## Install or update (local, test or production)

Run both combined files: `genisis-maintenance.sql` on the maintenance database, and `genisis-main.sql` on the main database.

1. Open `genisis-main.sql` in SSMS.
2. In the toolbar's **Available Databases** box, pick the **main** database (the one with `dbo.PLN` and `dbo.MBM`).
3. If your maintenance database is not called `HISMaintenance`, change `@MaintenanceDb` near the top of the file.
4. Press **F5**. The Messages tab ends with `Done.`
5. Open `genisis-maintenance.sql`, pick the **maintenance** database (the one with `dbo.USR`), and press **F5**.

Run it again after every API update that changes this folder. It is safe to run more than once. If it is run on the
wrong database or an old SQL Server (it needs SQL Server 2016 SP1 or later), it stops with a message saying what to change.
If an API version is started before the script has been run, it answers with
"The database is missing the stored procedures this version needs".

## Conventions

- One file per screen, `CREATE OR ALTER PROCEDURE genisis.<Screen>_<Action>`.
- The API keeps validation and the transaction; procedures do the reads and writes and never commit.
- Guarded writes return a `Result` column: `0` done, `1` record not found, `2` refused (duplicate or still in use).
- Text parameters are `nvarchar(4000)` so a value that is too long still fails instead of being cut short.
- After editing a file in `main/`, rebuild the combined file: `GENISIS_WRITE_SQL=1 dotnet test --filter Combined_main_script_is_up_to_date`.
