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
| 4 | Membership > Upload Bordx |
| 14 | Membership > Tracking |

Every endpoint checks its position on the server (`[RequireAccess]`); hiding a menu item in the UI is not the protection.

## Architecture

The API follows the layering of the existing screens (Plans, Registration), sized to this project rather than split into
separate projects:

| Layer | Where | Responsibility |
| --- | --- | --- |
| HTTP | `Controllers/` | Routing, access check, request validation, status codes. No SQL and no business rules. |
| Validation | `<Feature>/<Feature>Validator.cs` | Pure required-field / length / format checks, in the desktop's order and with its messages. |
| Business rules | `<Feature>/*Rules.cs`, `Membership/FeeRules.cs` | Pure calculations (fees, refunds, pro-rating, expiry). Unit tested without a database. |
| Use cases | `<Feature>/<Feature>Service.cs` behind `I<Feature>Service` | Orchestration and transactions. Larger flows are split (e.g. Bordx: verifier, uploader, member importer, ASCII exporter; Adjustment: service, writer, query service). |
| Data | `sql/main/*.sql` (`genisis.*` procedures) | All SQL. C# only calls procedures with typed parameters. |

- **Constants:** code values (`Membership/MembershipCodes.cs`, `Adjustment/AdjustmentConstants.cs`), the Bordx column layout
  (`Bordx/BordxLayout.cs`), column sizes and messages (`*Constants.cs`) and SQL error numbers (`Common/Errors/SqlErrorNumbers.cs`)
  are named; no bare literals in the logic.
- **Data access:** Dapper and stored procedures, not Entity Framework: the schema, procedures and triggers belong to the
  desktop application and are shared with it, so the API wraps them rather than owning migrations.
- **Errors:** one format for every endpoint (below). Expected rule failures throw a `BusinessRuleException` subclass
  (`BordxException`, `AdjustmentException`, `BenefitException`, `MmaException`) and become 400; missing records are returned
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

## Membership > Tracking

`/api/membership/tracking` replaces `FrmNewMBMTracking` (menu position 14): all 22 report types / 83 selections, every filter
(value, from / to range and "blank" check box) and the MBM type / principal type options. `meta` lists reports and filter
fields, `run` returns up to 5,000 rows with the footer totals, `export` returns every row as CSV (Print to File).

The desktop sent a concatenated WHERE fragment to `dbo.MBMTracking`. In the web version all SQL is in stored procedures:
`genisis.Tracking_CaseText` holds each case's fixed query text (`sql/main/171_tracking_cases.sql`, generated from the live
procedure by `tools/Generate-TrackingCases.ps1` together with `Tracking/TrackingCaseKeys.g.cs`; rerun it if `MBMTracking`
changes), and `genisis.Tracking_Run` (`sql/main/170_tracking.sql`) adds the filters as typed parameters, so no value reaches
the SQL text. C# keeps only the report catalog, the validator and the CSV writer. Supplementary counts are matched to the
principal rows in one pass instead of one query per row. Differences: the Employee No "from" filter is applied (the desktop
built it into an unused variable); the Cancel Date "blank" filter works (the desktop's had an unbalanced bracket);
"General Listing (Prin. Name)" uses the views' `MBMCCoverID` / `MBMMemberType` columns (the desktop named columns those
views do not have, so it failed for Principal or Supplementary).

## Membership > Upload Bordx

`/api/membership/bordx` replaces `FrmNewMBMEndt10` (menu position 4) for the standard layout and its ZURICH / TAK variant;
payors with an AXA layout (`PAY.BordxFormatType` 4 / 5) are refused. `verify` and `upload` take the file as multipart form
data; `ascii` returns the Generate ASCII file. Uploads go through the desktop's own UploadDb staging (`TmpUpload_*`,
then `SaveUploadDate`) inside one transaction with a per-run staging key, so a failure or Stop imports nothing.

Differences from the desktop: one transaction for the whole file (the desktop committed member by member); renewal /
reinstatement files (header status R / I) are only processed for ZURICH / TAK, as in the desktop, but no batch number is
consumed for other payors; the reinstatement row is written with its GST columns (the desktop's insert had more values than
columns and failed); the cancellation history row carries the member's name (the desktop wrote a blank name); a
supplementary's blank clinical dates fall back to the principal's (the desktop's test was inverted); Generate ASCII
writes each member's own employee no., group company and dates (the desktop carried them over from the previous member
when blank) and matches each supplementary's payor numbers by cover ID. The current `TmpUpload_MBM_HIS`,
`TmpUpload_MBMCovPersonsNew` and `TmpUpload_MBMOthers_2015` have required parameters the desktop does not pass (so the
desktop upload fails against this database); the API sends them blank.

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

## Case screens (Panel Hospitals, Non-Disclosure, Pay & Claim, MMA/CPT, Audit Review, Transfer, Listing, Claim/Case ASCII)

Nine more Case menu items are served under `/api/case/{panel-hospitals,non-disclosure,pay-claim,mma-cpt,audit-review,transfer,listing,claim-ascii,case-ascii}`
(plus `GET /api/case/payors`). Writes run in one transaction across HISDB / HISMaintenance / HISWorkSheet through synonyms. The two ASCII exports write the
payor file to `Ascii:ClaimFolder` / `Ascii:CaseFolder` (default `ClaimASCI` / `CaseASCII` under `Documents:RootPath`) and the UI downloads it.
Endpoint-by-endpoint mapping, SQL objects, access positions and every deliberate difference from the desktop are in
[../docs/case-remaining-screens-backend.md](../docs/case-remaining-screens-backend.md).

## Database procedures

The API uses stored procedures in a `genisis` schema. Before starting a new version, run `sql/genisis-main.sql` on the
main database and `sql/genisis-maintenance.sql` on the maintenance database in SSMS. See [sql/README.md](sql/README.md).

## Single Windows exe (API + UI)

```
.\publish-win.ps1 -UiPath ..\GenisisUI
```

Produces `publish\Genisis\Genisis.Api.exe` with the UI in `wwwroot`. No .NET install is needed on the target PC.

## Logging and errors

Serilog writes to the console and to `logs/genisis-.log` (daily, 30 files; configure in the `Serilog` section). Every request is logged with its status, time and user; sign-ins are audited.
Controllers do not catch exceptions: services throw `NotFoundException` / `ConflictException` / `BusinessRuleException` (400 / 404 / 409 with the message to show) and
`ApiExceptionHandler` maps database errors (too-long text, deadlock, timeout, missing procedures) and hides everything else behind "Server error. Check the API log for details."
See [../docs/exception-handling-and-logging.md](../docs/exception-handling-and-logging.md).

## Tests

```
dotnet test
```

Unit, service (fake repositories), integration (`WebApplicationFactory`) and architecture tests need no database; the database tests run when `GENISIS_TEST_MAINDB`
(and `GENISIS_TEST_MAINTENANCE`, `GENISIS_TEST_WORKSHEET`) are set and roll every write back. CI: `.github/workflows/ci.yml`.
Start with [../docs/onboarding.md](../docs/onboarding.md) and [../docs/architecture.md](../docs/architecture.md).
