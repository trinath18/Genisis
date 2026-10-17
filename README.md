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

### Machine-specific settings

Keep `appsettings.json` and `appsettings.Development.json` generic (`Server=localhost`, no personal folders): both are
committed and shared by everyone who clones the repo. Put values that only apply to your PC, such as a named SQL Server
instance or a local documents folder, in .NET user secrets (stored in your Windows profile, never in the repo; read
automatically when the API runs with `--launch-profile http`):

```
cd src/Genisis.Api
dotnet user-secrets set "ConnectionStrings:MainDb" "Server=MYDELL;Database=HISDB;Integrated Security=true;Encrypt=false;TrustServerCertificate=true"
dotnet user-secrets set "ConnectionStrings:Maintenance" "Server=MYDELL;Database=HISMaintenance;Integrated Security=true;Encrypt=false;TrustServerCertificate=true"
dotnet user-secrets set "Documents:RootPath" "C:\path\to\VB Code\"
```

Environment variables (`ConnectionStrings__MainDb`, `Documents__RootPath`) work too, for example on a server.

## Login

`POST /api/auth/login` checks `USR` in the Maintenance database (active users only) with the same `Crypt` algorithm as
`frmLogin.frm`, so existing desktop passwords work. Menu rights come from `USR.USRAccess` exactly like the VB6
`CheckAccess` function. Positions used by the web screens (`Auth/AccessRight.cs`):

| Position | Screen |
| --- | --- |
| 1 | Maintenance > Plan, Benefits, Entity and Accounts, as in the desktop |
| 7 | Case > Enquiry |
| 11 | Case > Registration |
| 12 | Case > Adjustment |
| 15 | Case > Tracking |

Every endpoint checks its position on the server (`[RequireAccess]`); hiding a menu item in the UI is not the protection.

## Architecture

The API follows the layering of the existing screens (Plans, Benefits), sized to this project rather than split into
separate projects:

| Layer | Where | Responsibility |
| --- | --- | --- |
| HTTP | `Controllers/` | Routing, access check, request validation, status codes. No SQL and no business rules. |
| Validation | `<Feature>/<Feature>Validator.cs` | Pure required-field / length / format checks, in the desktop's order and with its messages. |
| Business rules | `<Feature>/*Rules.cs` | Pure calculations (fees, refunds, pro-rating, expiry). Unit tested without a database. |
| Use cases | `<Feature>/<Feature>Service.cs` behind `I<Feature>Service` | Orchestration and transactions. Larger flows are split into focused classes rather than one large service. |
| Data | `sql/main/*.sql` (`genisis.*` procedures) | All SQL. C# only calls procedures with typed parameters. |

- **Constants:** code values, column sizes and messages (`*Constants.cs`) and SQL error numbers (`Common/Errors/SqlErrorNumbers.cs`)
  are named; no bare literals in the logic.
- **Data access:** Dapper and stored procedures, not Entity Framework: the schema, procedures and triggers belong to the
  desktop application and are shared with it, so the API wraps them rather than owning migrations.
- **Errors:** one format for every endpoint (below). Expected rule failures throw a `BusinessRuleException` subclass
  (e.g. `BenefitException`, `MmaException`) and become 400; missing records are returned
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

## Membership module

The Membership screens (Registration, Adjustment, Enquiry, Tracking and Upload Bordx) are kept on the
`modules/membership` branch and are not part of `main`. Merge that branch when the Membership module is scheduled.

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
## Case > Registration

`/api/case/registration` replaces `frmCaseAdmission` (menu position 11). The UI page keeps every desktop field, the same
12 tabs and the same workflow (alerts, validation order, hidden interactions) in the app's modern layout.

| Endpoint | VB6 source |
| --- | --- |
| `GET lookups` | `ComboListing`: special approval, claim nature, cash / stay type, treatment mode, hospitals, repudiation letter selections |
| `GET member-search` | Search button (`frmSearchMBM`) |
| `GET member/{mbm}` | Go button: `SearchMBMCas` plus plan prompts, annual limit, agent, exclusions; returns the desktop's alerts in order |
| `GET member/{mbm}/patient/{coverId}` | `cboPatientList_Click` |
| `GET benefits`, `member-history`, `non-disclosure`, `policy-terms`, `surgical-schedule` | Benefit, MBM'Ship History, Non-Disclosure, Policy Terms, Surgical Schedule tabs |
| `GET icd`, `GET repudiation-codes` | `frmSearchICD`, `FrmSearchRepudiation` |
| `GET procedures` | `frmSearchCPT` / `frmSearchInv` (OPCS code search; investigations are ProcPart 'A') |
| `GET mma/{code}` | `SearchMMARate` (fees next to an OPCS code) |
| `GET hospital-check` | `txtHOSCode_LostFocus` panel check |
| `POST validate` | every Register check without saving; returns the flags for the "activated < 30 days" / "not discharged" questions |
| `POST` | Register (`NewCaseAddmission`) |
| `POST letter` | `funcword`/`bookmark` letters — doc requests on admission/discharge, take-home drugs, prior/expiry notification, repudiation |
| `POST lg` | `cmdLG_Click`: `CheckLimit` balance check, `GetLGCode`, `Case_AdjInsertCASLG` and the LG / BTBG letter data |

The save runs in one transaction: case number from `CAQ2013`, then the existing `Case_RegInsertCAS2013` / `CAS32013`,
`Case_RegInsertCASRemark` / `2` / `3`, `Case_RegInsertCASRef` / `Ref1` (maintenance, through synonyms), `Case_RegInsertDIAFirst`,
`DIAFinal`, `UD`, `CASOthers`, `CASOthers2`, the balance-limit reduction and `Case_RegInsertCASRep`. The legacy
`Case_Registration` dispatchers (raw `@strAppend` SQL) are not called; their reads are reproduced with parameters.

Differences from the desktop: the cross reference is written inside the same transaction (the desktop wrote it outside);
a repudiated case needs Document and Personnel only (the desktop also required Attention, whose list is hidden, so it
could never save one); for payors other than AX the panel check accepts any panel entry for the payor (the desktop
compared every payor against the AX panel code). The Word `.DOT` mail-merge letters are replaced by server-rendered
letter data shown in a printable modal (the templates never shipped with the source); the LG button issues the letter
through `POST lg` and `Notification` produces the prior/expiry letter. Not migrated yet: the SOAP push to the legacy
mobile service (the notification row is still written), F6 screen capture, Excel export on the search windows, CPT/Inv
Info maintenance, and opening policy wording documents. See `docs/case-registration-migration.md` for the full mapping
and migration notes.

## Case > Adjustment

`/api/case/adjustment` replaces `frmCaseAdjustmentNew` (+ `frmGLMenu`, `frmCASList`; menu position 12). The UI shares the
registration editor (`useCaseEditor`/`CaseSections`) and adds the adjustment-only pieces: load/prev/next navigation,
status/claimability/pending edits, repudiation, referral progress actions, letters, GL management, and the LG /
Worksheet / Forms / Policy Wording tabs.

| Endpoint | VB6 source |
| --- | --- |
| `GET search`, `GET {cas}` (`{cas}/neighbour` exists but is unused by the UI) | `cmdSearch` (`frmCASList` → `View_Case_Search`), `cmdShow`; `cmdPrev`/`cmdNext` not migrated |
| `GET gl-options`, `GET {cas}/gl`, `POST {cas}/gl/check`, `POST {cas}/gl` | `frmGLMenu`: purpose/request/delay lists, defaults, `CheckLimit`, `Case_AdjInsertCASLG_2011` |
| `PUT {cas}`, `POST {cas}/repudiate`, `POST {cas}/reset-claimability`, `POST {cas}/progress/{action}` | `cmdUpdate1`, `cmdRepudiate`, pending-accepted prompt, C. Ref AA/RR, Clear Doc Record, Doc Disch |
| `POST {cas}/letter/{D,I,S,M,auto}` | `funcword` letters: Doc Req Discharge, GP/QA, Reim Req, Mgmt Note, Notify (P/E) |
| `GET/DELETE {cas}/lg[/{lgNo}]`, `GET {cas}/worksheet` | LG tab listing + Delete, Worksheet tab (`VIEW_WS_Listing`) |
| `GET/POST/DELETE {cas}/documents[...]`, `GET policy-wording/...` | Forms tab document folders + Browse/Copy/Delete GL, Policy Wording `ShellExecute` |
| `GET pending-reasons`, `POST {cas}/notes-template` | `SearchPendingReason` combo, Case Mgmt Notes template fill |

The update runs in one transaction (the desktop ran ~15 uncommitted writes across two databases). Not migrated: the
"Upd &Add" button (no handler in the desktop), the SOAP device push, and client-side folder browsing (replaced by
document list/upload/download endpoints). See `docs/case-adjustment-migration.md` for the full mapping.

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
