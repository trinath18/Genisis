# Genisis API

ASP.NET Core 8 Web API replacing the legacy VB6 desktop app, module by module. It reads and writes the existing
SQL Server databases unchanged (configured as `MainDb` and `Maintenance`), so the old desktop app can keep running alongside it.
The React front end lives in [GenisisUI](https://github.com/trinath18/GenisisUI).

## Run locally

Prerequisites: .NET 8 SDK and a SQL Server with the main and maintenance databases restored.

```
cd src/Genisis.Api
dotnet run --launch-profile http
```

Swagger opens at http://localhost:5000/swagger. Connection strings default to `localhost` with Windows authentication
(`appsettings.json`). To override without editing the file, set environment variables, e.g.
`ConnectionStrings__MainDb` and `ConnectionStrings__Maintenance`.

Set `Auth:JwtKey` (32+ characters) to keep users logged in across restarts; when empty a random key is generated at startup.

## Login

`POST /api/auth/login` checks `USR` in the Maintenance database (active users only) with the same `Crypt` algorithm as
`frmLogin.frm`, so existing desktop passwords work. Menu rights come from `USR.USRAccess` exactly like the VB6
`CheckAccess` function. Positions used by the web screens (`Auth/AccessRight.cs`):

| Position | Screen |
| --- | --- |
| 1 | Membership > Registration; also the Maintenance screens (Plan, Benefits, Entity, Accounts), as in the desktop |
| 2 | Membership > Adjustment |
| 3 | Membership > Enquiry |

Every endpoint checks its position on the server (`[RequireAccess]`); hiding a menu item in the UI is not the protection.

## Architecture

The API follows the layering of the existing screens (Plans, Registration), sized to this project rather than split into
separate projects:

| Layer | Where | Responsibility |
| --- | --- | --- |
| HTTP | `Controllers/` | Routing, access check, request validation, status codes. No SQL and no business rules. |
| Validation | `<Feature>/<Feature>Validator.cs` | Pure required-field / length / format checks, in the desktop's order and with its messages. |
| Business rules | `<Feature>/*Rules.cs`, `Membership/FeeRules.cs` | Pure calculations (fees, refunds, pro-rating, expiry). Unit tested without a database. |
| Use cases | `<Feature>/<Feature>Service.cs` behind `I<Feature>Service` | Orchestration and transactions. Larger flows are split (e.g. Adjustment: service, writer, query service). |
| Data | `sql/main/*.sql` (`genisis.*` procedures) | All SQL. C# only calls procedures with typed parameters. |

- **Constants:** code values (`Membership/MembershipCodes.cs`, `Adjustment/AdjustmentConstants.cs`),
  column sizes and messages (`*Constants.cs`) and SQL error numbers (`Common/Errors/SqlErrorNumbers.cs`)
  are named; no bare literals in the logic.
- **Data access:** Dapper and stored procedures, not Entity Framework: the schema, procedures and triggers belong to the
  desktop application and are shared with it, so the API wraps them rather than owning migrations.
- **Errors:** one format for every endpoint (below). Expected rule failures throw a `BusinessRuleException` subclass
  (`AdjustmentException`, `BenefitException`, `MmaException`) and become 400; missing records are returned
  as 404 without exceptions.

## Error responses

Errors are RFC 7807 Problem Details (`application/problem+json`), produced by `Common/Errors/ProblemDetailsExceptionHandler.cs`
and `Problems.Invalid` (validator failures):

```json
{ "title": "Request validation failed", "status": 400, "detail": "Please Enter Policy No!", "message": "Please Enter Policy No!", "traceId": "00-…" }
```

`message` repeats `detail` for the UI. Values longer than a column return 400 with a generic message; a closed database
encryption key or missing `genisis` procedures return 500 with an instruction for the DBA; a database timeout returns 504;
anything else returns 500 "Server error. Check the API log for details." and is logged with the trace id. SQL text, stack
traces and file paths are never returned.

## Membership Enquiry (read-only)

| Endpoint | VB6 / SQL source |
| --- | --- |
| `GET /api/membership/search?by=number\|name\|ic\|policy&q=` | `frmSearchMBM05` / `Search_MBM_05` columns |
| `GET /api/membership/{mbmNumber}` | `SearchMBMCovPersonsProc`, `PLN`, `AnnualLimit`, `MBMCLNOthers`, `MBMBnfLmt` |
| `GET /api/membership/{mbmNumber}/adjustments` | `MBMAmendHistory` |
| `GET /api/membership/{mbmNumber}/cases` | `SearchCas` |
| `GET /api/membership/{mbmNumber}/case-history` | `SearchCasHistory` |
| `GET /api/membership/{mbmNumber}/member-history` | `SearchMBMHistory06` (rewritten with parameters) |
| `GET /api/membership/{mbmNumber}/account` | `VIEW_MBMTotal_Summary` |
| `GET /api/membership/{mbmNumber}/notes` | `View_Get_MemberExclusion`, `View_Get_MemberRemarks` |
| `GET /api/membership/{mbmNumber}/benefits` | HIS Benefits tab, `View_BNF` (latest schedule on the payor effective date) |
| `GET /api/membership/{mbmNumber}/account-history` | Acc History tab, worksheet database `VIEW_Account_History` |
| `GET /api/membership/{mbmNumber}/fees` | Fees tab, `MBMOthers` + `MBMRefund` |
| `GET /api/membership/{mbmNumber}/non-disclosure` | Non Disclosure tab, `VIEW_Cas_SearchIll` |
| `GET /api/membership/{mbmNumber}/policy-terms` | Policy Terms tab, `PLN` terms |
| `GET /api/membership/{mbmNumber}/lifetime-limits` | LT Limit tab, `View_Search_LT*` views |
| `GET /api/membership/{mbmNumber}/policy-wording[/{file}]` | Policy Wording tab: files under `Documents:PolicyWordingPath\<wording>` (desktop `DocPath\PolicyWording`) |
| `GET /api/membership/{mbmNumber}/takeover-claims` | T/O Claims tab, `CLMMAATakaful` |
| `GET /api/membership/{mbmNumber}/worksheets` | WS tab, `SearchWorksheet` |

The tabs added in the web version (HIS Benefits to WS) are served by `Membership/EnquiryTabsService.cs`; Policy Wording files
are read by `Membership/PolicyWordingStore.cs`, which only serves plain file names with an allowed extension from inside the
configured folder.

The desktop's Print MBM / Print Report / Print History buttons (Crystal Reports) are not migrated.

All SQL is parameterised. Stored procedures that build dynamic SQL from a string argument (`SearchMBMHistory06`,
`MBM_GetExcRemarks`) are not called; their queries are reproduced with parameters instead.

## Membership > Adjustment

`/api/membership/adjustment` replaces `frmNewMBMAdjustment` and its "Others Details" form (menu position 2). Load, search,
Prev/Next, fees, adjustment history, MBM history, fee recalculation and one save endpoint per endorsement type:
A Amendment, P Plan Change, S Add / K Cancel / T Delete Supplementary, C Cancellation (with MCO / premium refunds),
X Suspend and D Delete Policy. Clinical members' new supplementaries also get their `MBMCoveredPersonsCLN` row
(clinical database tables through `genisis.Cis_*` synonyms; set `@CisDb` in `sql/main/000_setup.sql`).

Differences from the desktop: everything (main, maintenance and clinical writes) runs in one transaction; refunds use the
plan's round-up setting (the desktop only rounded if a fee field had been edited first); a name / date of birth change on a
renewal renames only the base number and its `*nn` renewals (the desktop matched `LIKE '%base%'`); a supplementary's full
relationship code is stored (the desktop kept the first letter); Add / Cancel / Delete Supplementary need at least one person.

## Maintenance > Benefits

`/api/maintenance/benefits` replaces `frmMaintenanceBenefits` (same menu right as Plan, position 1).

| Endpoint | VB6 / table |
| --- | --- |
| `GET lookups` | health, insured, category, sub category, payor and group company lists |
| `GET/POST categories`, `PUT/DELETE categories/{code}` | Category tab, `BNFCategory` |
| `GET/POST sub-categories`, `PUT/DELETE sub-categories/{code}` | Sub Category tab, `BNFSubCat` |
| `GET entry`, `POST entry` | Benefit Entry tab: one `BNF` row per sub category line that has an RM per disability |
| `GET/POST/PUT/DELETE rows` | Benefits Listing tab: one `BNF` row, keyed by sub category / insured / plan / effective date |

Differences from the desktop (desktop bugs): Delete on the listing removes only the selected row (the desktop deleted the
sub category's rows for every plan) and is refused only while a member is on that row's plan / health / insured; adding a
row is refused only when that exact key exists (the desktop refused any new row for a plan / insured / date that already had
one); RM per disability is saved on the listing and is the required amount (the desktop required days and RM / day but
never saved RM per disability); the effective date version uses the full plan code.

## Maintenance > Entity and Accounts

Both use menu position 1 and the maintenance database tables `MMAProcedures`, `MMAFees` and the view `VIEW_MMA_Schedule`
(through `genisis.Maint_*` synonyms).

| Endpoint | VB6 |
| --- | --- |
| `GET/POST /api/maintenance/entity/procedures`, `PUT/DELETE .../procedures/{index}` | `frmMMA`, Procedures tab |
| `GET/POST /api/maintenance/entity/fees`, `PUT/DELETE .../fees/{index}` | `frmMMA`, Look Up Table tab |
| `GET /api/maintenance/accounts/schedule?code=&direction=next\|prev\|find` | `frmMMA13thSchDesc` (Prev / Next; `find` jumps to a code) |

Differences from the desktop: a fee row needs at least one category and cannot be deleted while a procedure uses its surgeon
or anaesthetist category (the desktop saved empty rows and deleted without a check); OPCS Narrative is limited to the
table's 400 characters (the desktop box allowed 3000, which the database rejected); the Accounts MMA surgeon fee is the
`MMAFees` fee for the procedure's category (the desktop showed the view's `SurgeonsFees` column, which is the 14th Schedule fee).

## Database procedures

The API uses stored procedures in a `genisis` schema. Before starting a new version, run `sql/genisis-main.sql` on the
main database and `sql/genisis-maintenance.sql` on the maintenance database in SSMS. See [sql/README.md](sql/README.md).

## Single Windows exe (API + UI)

```
.\publish-win.ps1 -UiPath ..\GenisisUI
```

Produces `publish\Genisis\Genisis.Api.exe` with the UI in `wwwroot`. No .NET install is needed on the target PC.

## Tests

```
dotnet test
```
