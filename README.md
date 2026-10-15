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
`CheckAccess` function (position 1 = Membership Registration, 2 = Adjustment, 3 = Enquiry).

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

All SQL is parameterised. Stored procedures that build dynamic SQL from a string argument (`SearchMBMHistory06`,
`MBM_GetExcRemarks`) are not called; their queries are reproduced with parameters instead.

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
