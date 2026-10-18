# Case module — remaining screens (API + SQL) — VB6 → Web notes

Follows `case-remaining-screens-ui.md`: the nine Case screens that were built UI-only are now backed by the API and the real
databases. Nothing in the UI is a preview any more (`lib/uiOnly.ts` and the "UI preview" banner are gone).

All writes run in **one MainDb transaction**. The maintenance, worksheet and payment databases are reached from HISDB through
`genisis.Maint_*`, `genisis.WS_*` and `genisis.Pay_*` synonyms (`sql/main/000_setup.sql`), so a save that touches several databases
commits or rolls back as a whole. The desktop ran the same writes on up to four connections with no working transaction.
`HISWorkSheet` / `HISPayment` are optional: if the database is absent the synonym is skipped and the screens that read it say so.

## Endpoints

All routes are under `api/case`, require a login, and are gated by `RequireAccess` (positions in `Auth/AccessRight.cs`, mirrored in
`GenisisUI/src/auth/access.ts`).

| Screen | Endpoint | VB6 behaviour it replaces | Position |
|---|---|---|---|
| (shared) | `GET payors` | `SearchPayor` / `PayorComboListing` combos | 7 or 18 |
| Panel Hospitals | `GET panel-hospitals/codes`, `GET panel-hospitals?code=` | `GetPanelHosCode`, `PanelListing` (`Search_PanelHospitals2`) | 7 |
| Non-Disclosure Info | `GET non-disclosure/{cas}`, `PUT non-disclosure/{cas}` | `CasDetails`, `CmdUpdate_Click` (`CASOthersTwo`) | 12 |
| Pay & Claim | `GET pay-claim/{cas}`, `PUT pay-claim/{cas}` (`isNew` = Add) | `showMBM`, `ShowMBMDetails`, `showPayClaim`, `SavePayClaim` | 12 |
| MMA / CPT | `GET/POST procedures`, `PUT/DELETE procedures/{index}`, `GET/POST fees`, `PUT/DELETE fees/{index}` under `mma-cpt` | `ProcListing`/`SaveProc`/`DeleteProc`, `LookListing`/`SaveLook`/`DeleteLook` | 1 |
| Case Audit Review | `GET audit-review/{cas}`, `PUT audit-review/{cas}` | `cmdShow_Click`, `cmdUpdate1_Click` (`Case_AuditReview`, `Case_AuditInsert/Update`) | 12 |
| Case Transfer | `GET transfer/{cas}`, `GET transfer/member?memberNo=&coverId=`, `POST transfer/{cas}` | `CmdGo_Click`, `DisplayTransMBMInfo`, `cmdSave_Click` / `TransferProcess` / `ReCalculatePrevMBMLmt` (`Case_Transfer`) | 12 |
| Inpatient Case Listing | `GET listing/search`, `POST listing/documents`, `POST listing/bordx` | `WorksheetTrackingListing(2)`, `ItemCheck` + `LoadDoc`, `CmdBordx_Click` / `UpdateRecord` / `SaveCLMBordx` | 7 |
| Claim ASCII | `POST claim-ascii/generate`, `GET claim-ascii/files/{name}` | `PrincipalASCII`, `PrincipalASCIIRejectCase`, `UpdateWSAscii` (`Case_ClaimAscii`, `Case_CLMAscUpdate`) | 18 |
| Case ASCII | `POST case-ascii/generate`, `GET case-ascii/files/{name}` | `PrincipalASCII` of `frmCaseASCII` | 18 |

Six of the nine menu items have no `_Click` handler in `mdiMain.frm`; their positions are decisions (see the UI doc) and live only in
`AccessRight.cs` / `access.ts`.

## Code layout (the layered pattern)

Each screen is a folder `Genisis.Api/Case/<Screen>/` with its models, FluentValidation validators, repository (interface + Dapper implementation) and service; the controller in
`Controllers/` has one call per action and no data access or `try/catch`. Services start the unit of work (`IDbSessions`), throw `NotFoundException` / `ConflictException` /
`BusinessRuleException`, and `ApiExceptionHandler` turns them into 404 / 409 / 400 with the message to show. Registration is one call: `AddCaseScreens()`. The rules, diagrams and the
checklist for the next screen are in [architecture.md](architecture.md) and [onboarding.md](onboarding.md); the error and logging strategy in
[exception-handling-and-logging.md](exception-handling-and-logging.md).

| Screen | Folder |
|---|---|
| Panel Hospitals, payor lookup | `Case/PanelHospitals/`, `Case/Lookups/` |
| Non-Disclosure Info | `Case/NonDisclosure/` |
| Pay & Claim | `Case/PayClaim/` |
| MMA / CPT | `Case/Mma/` |
| Case Audit Review | `Case/AuditReview/` |
| Case Transfer | `Case/Transfer/` |
| Inpatient Case Listing | `Case/Listing/` (`IScannedDocuments` abstracts the document folder) |
| Claim ASCII, Case ASCII | `Case/Ascii/` (`IAsciiFileStore` abstracts the output folder; `AsciiFormat` holds the fixed-width layouts) |

## SQL objects (all `CREATE OR ALTER`, safe to re-run, `sql/main/*.sql`)

| File | Objects |
|---|---|
| `160_panel_hospitals.sql` | `Case_Payors`, `PanelHospitals_Codes`, `PanelHospitals_List` |
| `165_case_non_disclosure.sql` | `NonDisclosure_Get`, `NonDisclosure_Save` |
| `170_case_pay_claim.sql` | `PayClaim_Members`, `PayClaim_Case`, `PayClaim_Get`, `PayClaim_Save` |
| `180_mma.sql` | `Mma_ProcedureSearch/Save/Delete`, `Mma_FeeSearch/Save/Delete` |
| `190_case_audit_review.sql` | `CaseAudit_Get`, `CaseAudit_Save` |
| `200_case_transfer.sql` | `Util_SafeDecimal` (function), `CaseTransfer_Case/Member/MoveLimit/Apply` |
| `210_case_listing.sql` | `CaseListing_Search/Item/NextBordxNo/Apply/SaveBordx` |
| `220_case_ascii.sql` | `Ascii_NextBatch`, `AsciiClaim_Accepted/Rejected/MarkExported`, `AsciiCase_Rows/MarkExported` |
| `000_setup.sql` | new synonyms `Maint_HosPanelCode`, `Maint_MMAFees`, `WS_Claim/ClaimInvoice/ClaimApprove/Bordx/BordxSEQ/CLMAsciiSEQ/PendingBordx/PendingBordxIB/CLMReport/CLMReportCas`, `Pay_Payment2HOS` |

No existing table, view or procedure was changed. The legacy procedures that build SQL from `@strAppend` (`Search_PanelHospitals2`,
`Case_Transfer`, `Case_AuditReview`, `Case_ClaimAscii`) are not called; `Case_SavePayClaim` / `Case_UpdatePayClaim` /
`Case_AuditInsert` / `Case_AuditUpdate` are replaced by upserts that keep the desktop's column rules. **HISDB is at compatibility
level 100**: no `TRY_CONVERT`, `FORMAT`, `IIF`, `CONCAT`, `EOMONTH`, `STRING_SPLIT` in `main/` scripts.

## Configuration

- `Ascii:ClaimFolder`, `Ascii:CaseFolder` — where the payor files are written on the server. Empty = `ClaimASCI` / `CaseASCII` under
  `Documents:RootPath` (the desktop's `SSRVPath`). The browser also downloads the file (the desktop's second copy went to `C:\`).
- `Documents:RootPath` is also what the Inpatient Case Listing scanned-document check reads (`ScannedDoc\<case>`). If it is not
  configured the check is skipped (reported as `available: false`) instead of blocking every worksheet.

## Behaviour that differs from the desktop (and why)

**Everything**
- Parameterised procedures instead of concatenated SQL (injection, quoting bugs).
- Real transactions; sequences (`Claim_BordxSEQ2013`, `CLMAsciiSEQ`, `CASAsciiSEQ`) are taken with one locked `UPDATE`, not read-then-update,
  so two users can no longer be given the same bordx or batch number.
- Server-side re-validation of everything the UI checks.

**Non-Disclosure Info** — the desktop only updated an existing `CASOthersTwo` row and reported success otherwise; the save inserts the row when missing.
"Others" text is dropped unless its category is Others (4 pre-existing / 3 underlying).

**Pay & Claim** — one record per case, so Add is refused (409) if a record exists and Edit if none does. The live `CASPayClaim` table has
`AgentName` / `CasRemarks` columns that the VB6 source in the repo does not know; they are left untouched. The "Letter Status" column was never
populated by the desktop and was removed. Others-remarks limit is 80 (column size; the form allowed more).

**MMA / CPT** — OPCS Narrative is limited to 400 characters (the real `OPCSNarrative` size; the form allowed 3000 and the save would have failed).
Lists are capped at 5000 rows (the table has ~2700).

**Case Audit Review** — the case, diagnoses and repudiation reuse the Case Adjustment load. `SearchMBMCas` returns nothing for a member that has no
`MBMOthers` / `MBMOthersTwo` / `MBMTwo` row, so the member panel is read straight from `MBM` instead. The visit date stays an `mm/dd/yyyy` string like
the desktop; blank amounts are stored as 0, blank dates as NULL; the audit status is also written to `CASCrossReference.CASARStatus`. Payments come from
`HISPayment.Payment2HOS`. "Amt O/S" is shown empty (no code in the desktop ever filled it).

**Case Transfer** — one transaction instead of unrelated writes. The limit give-back / charge follows `ReCalculatePrevMBMLmt` / `TransferProcess`
(principal limit when the plan shares it or the patient is the principal, otherwise the covered person's; cancer / kidney / home-nursing balances by the
benefit status). Differences: the `AnnualLimit` row is the one in force on the member's payor effective date (the desktop took an arbitrary row of the plan);
`ClaimApprove` text amounts are parsed safely (`Util_SafeDecimal`: `'-'` crashed a plain cast); transferring to the member the case is already on is refused;
the transfer is refused if the previous member is not on `MBM` (nothing to give the limit back to); `TransRemarks` keeps its last 2000 characters.

**Inpatient Case Listing** — the selection rules run again on the server; the scanned-document test is a server check (group worksheets — health type N,
insured type G/H — are exempt, as in the desktop). The claimability test keeps the desktop's literal `"N"`: claimability codes are two letters (`NN`), so that rule
never fires in the desktop either — change `ListingRules.SelectionProblem` to `NN` if the business wants "Not Decided" blocked. The bordx number is
`<health type><payor><7-digit sequence>` from the first worksheet's payor; Multiple Claims shares one number, Single Claim takes one per worksheet; the generated
list shows each bordx's total (the desktop showed a running total on every row). Scan mode accumulates scanned worksheets and ignores duplicates.

**Claim ASCII / Case ASCII** — layouts are ported field by field (`Case/AsciiFormat.cs`, covered by width tests: accepted 5000, rejected 5001, case 4975 incl. the CR).
Kept: records end CR + CRLF, header uses sender `MX` for accepted files and the payor for rejected ones, file name `<payor><yymmdd>(<batch>)` without extension,
ANSI (Latin-1) text. Changed:
- the "to" date is inclusive of the whole day (the desktop compared `<= 'to date'` at midnight, which dropped cases registered later that day);
- a missing patient IC is padded to its 20-character width (the desktop wrote 30 blanks and shifted every later field);
- Claim ASCII flags only the exported worksheet version (`CLMBordxWSASCII = Y`); "New" = `N`, "Reprint" = `Y`; "1 - Deleted" selects `CLMBordxWSStatus = D`
  (the desktop returned nothing for it);
- Case ASCII: the desktop's reprint filter named a column its view does not have and its `Case_AscUpdate` procedure does not exist, so reprint never worked.
  Exporting now sets `CASOthersTwo.CASASCIIGenStatus = Y`; "N - New" still exports every case in range like the desktop, "R - Reprint" exports the flagged ones;
  endorsement 1 / 2 filter `CASASCIIAmendStatus` D / A (no data uses them yet).
- The file is written to the server folder (inside the transaction, before commit) and downloaded by the browser; only plain file names inside the folder are served.

## Tests

- `dotnet test`:
  - validators and pure rules (`CaseScreensRulesTests`): the desktop's messages and their order, file layouts and widths;
  - service tests against **fake repositories** (`CaseScreensServiceTests`): bordx numbering for Single / Multiple Claims, transfer result codes, ASCII flow, Pay & Claim conflict mapping — no database needed;
  - HTTP pipeline in memory (`ApiIntegrationTests`, `WebApplicationFactory`): 401 without a token and 403 without the menu right on every endpoint, 400 with the first validation message,
    404 / 409 through the exception handler (database-backed part runs when `GENISIS_TEST_MAINDB` is set);
  - architecture rules (`ArchitectureTests`) and the exception handler (`ApiExceptionHandlerTests`);
  - database tests against MYDELL (`CaseScreensDatabaseTests`; set `GENISIS_TEST_MAINDB` etc., see `AGENTS.md`). Every write test runs in a session that is rolled back
    (MMA inserts only consume an identity value; ASCII files go to a temp folder).
- Browser tests (`GenisisUI/e2e`, headless Chrome against a real API instance and the development databases): panel hospitals, non-disclosure, pay & claim, MMA procedures and fees,
  audit review, transfer (and back), listing rules / selection / bordx request, claim and case ASCII generation and download. They write to the ZZ test case and a few real rows and
  `e2e/cleanup.sql` restores them (see `e2e/README.md`).
