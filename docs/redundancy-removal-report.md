# Redundancy removal report

`script.txt` asks to detect and remove dead code, unused variables, redundant functions, obsolete UI and duplicated logic, and to document every removal with its
justification. This report collects the findings and decisions for the migrated modules and says exactly what was and was not done.

**Policy.** The VB6 source is the specification of the behaviour still in production, so it is **not edited or deleted** by the migration. "Removed" below means
*not carried into the web application*; section 5 lists what can be deleted from the VB6 project once the desktop application is retired.

## 1. Menu level (`mdiMain.frm`)

Static analysis of the 241 `Begin VB.Menu` designer blocks and the `Private Sub <menu>_Click` handlers (`extract-menus.js`, `docs/menu-mapping.md`):

| Finding | Count | Decision |
|---|---:|---|
| Menu controls | 241 (5 are separators) | all mapped in `menu-mapping.md` with status |
| Controls with `Visible = 0` in the designer (never shown in the desktop app) | 134 | migrated only when the business marked them **Required**; the rest are not built |
| Controls that have a `_Click` handler | 63 | handler logic migrated for the Required ones (access position, time gate, form opened) |
| `_Click` handlers with **no** menu control of that name | 17 | dead code, not migrated: `ClaimTracking`, `ClaimTracking2`, `ClaimTracking3`, `ClaimTracking4`, `ClaimTrackingSimple`, `Logout1`, `MCOInvoice`, `mnuCaseReviewOP`, `mnuGLEntry`, `mnuInvoiceNegative`, `mnuInvoicePositve`, `mnuApprovedclaims`, `mnuclaimspayment`, `mnuSubsidiary`, `mnuUploadBordx08`, `mnuUploadBordx2010`, `OtherReports` |
| Required Case menu items with neither a handler nor a visible menu (cannot be opened in the desktop) | 6 of the 9 built in the last phase | forms matched by caption; access positions chosen deliberately (`AccessRight.cs`) |
| First export of the menus missed the handler of *Claim ASCII* (VB6 identifiers are case-insensitive) | 1 | extractor fixed |

## 2. Form and control level

Per-screen tables with every removed or changed element and its justification:

- Case Registration — `case-registration-migration.md` ("not migrated" rows: screenshot capture `PrintScreenSnapShot`, CPT/Inv Excel export and info screens, Word automation).
- Case Adjustment — `case-adjustment-migration.md` ("Redundant / dead code in the VB6" and "Intentional deviations").
- Nine remaining Case screens — `case-remaining-screens-ui.md` ("Removed or changed VB6 elements"), `case-remaining-screens-backend.md` ("Behaviour that differs from the desktop").
- Worksheet screens — `worksheet-screens.md` ("What changed from the desktop, and why").

Headline items:

| Item | Kind | Decision | Justification |
|---|---|---|---|
| `frmPanelHospitals` ("Panel Hospital" with payor/plan/hospital filters + Advance Search) | dead form | not migrated | not in `medixhis.vbp`, no caller; `frmPanelHospital` is the live form |
| Worksheet registration versions `frmNewWorksheet`, `…2010`, `…2011`, `frmWorksheetReg2012`, `…2012v2`, `frmWorksheetRegGST`; adjustment versions `frmNewWSAdjust`, `…2010`, `…2011`, `frmWorksheetAdj2012`, `…2012v2`, `frmWorksheetAdj2013`/`…GST` (one of the last two is live) | superseded versions (candidate) | not migrated; the live pair is chosen in phase 3 | each newer version is a copy of the previous with changes; `frmWorksheetRegGST2015` opens `frmWorksheetAdj2013`. Five of them (`frmNewWorksheet2011`, `frmWorksheetReg2012`, `frmNewWSAdjust2011`, `frmWorksheetAdj2012`, `frmWorksheetAdj2012v2`) are in neither `medixhis.vbp` nor `medixhis1.vbp` |
| Designer-only shells `frmNewWorksheet2010V2`, `frmNewWSAdjust2010V2`, `frmWorksheetRegGST2015v2`, `frmWorkSheet` (no code) | dead forms | not migrated | no event code at all |
| `FrmWorksheetTracking`: commented-out `CmdPrint_Click`, hidden `cmdPrint` / `lblTotal` / `Label22` / `txtNoofDay`; Stop button with its `intExit` / `DoEvents` loop | dead code / replaced | removed | no report was ever printed from it; the web screen returns the whole result at once |
| `FrmWksSendToAC`, `FrmARRecFrCLM`: the same hidden controls and Stop loop | dead code / replaced | removed | as above |
| `frmWorksheetEnq`: the editing half of the Adjustment clone (about 7,300 of its 12,800 lines of code, comments and blank lines not counted: `SaveClaim`, `saveActual` / `saveApprove` / `saveHos` / `saveMember` / `saveLimit`, `UpdateMBMBalance*`, `savePayMBMStatus`, `saveExcessLetter`, the Word letters `bookmark*` / `funcword*`, the Excel `Input*ToExcel`), reachable only through buttons the designer hides | duplicated code (not reachable) | not migrated; the editing logic is migrated once, with Adjustment (phase 3) | one editor instead of two copies |
| `frmWorksheetEnq`: `txtPerDisLmt` ("Per Dis. Bal Lmt"), `lstFollowUp`, the hidden HOS CR / DR note fields | dead controls | removed | the assignment is commented out / nothing ever fills them |
| Hidden `mnuWSToAC` / `mnuWSReturnCLM` menu controls (no handler, forms never opened) | orphan menu items | migrated anyway (Required) | the business marked them Required |
| "Upd &Add" button (`cmdSaveAdd`) | dead control | not migrated | no event handler |
| Case Audit Review hidden / off-screen controls (`lstBenefitDetails`, `txtINSCode`, `txtINSCode1`, `txtPLNCode`, `txtHLTCOde`, `txtSRVPath`, Prev / Next / Next Rev Case) | obsolete UI | removed | `Visible = 0` or `Left = 14400` off the form, belonging to no tab |
| Inpatient Case Listing `Stop` flag, `Check All` button, hidden `CboBTBG`, `List1`, `Text1`, `txtClaimStatus`, `lblTotal` | obsolete / replaced | removed or replaced by the grid's select-all and request cancellation | interrupt plumbing and hidden implementation detail |
| Pay & Claim `cboLetterType` + "Letter Format"; "Letter Status" column | unused | removed | hidden control, validation commented out; the column was never filled |
| Claim ASCII + Case ASCII (two near-identical forms) | duplicated UI | merged into one `AsciiExportForm` configured per screen | DRY; wording differences become configuration |
| "File on PC" (`C:\`, `C:\CaseASCII\`) | desktop-only | replaced by a browser download | a browser cannot write to a local folder |
| `cmdPrev` / `cmdNext` on Case Adjustment, redundant Member / Member No fields | obsolete | removed (user request) | search dialog replaces stepping; the case owns the member |
| SOAP device push (`MobileNotification`) | retired service | row written, push retired | legacy service decommissioned |

## 3. Logic duplicated in VB6 and consolidated

| Duplicate | Consolidated into |
|---|---|
| `CheckLimit` copied between Registration and Adjustment | one server balance check (`BalanceLimitAsync(adjustment: true)`) |
| Field validations repeated in `ShowButtonState` / `cmdUpdate1` | one validator (client Yup schema + server FluentValidation share the message list) |
| Two versions of the `Case_Transfer` procedure (HISDB and maintenance) | one transaction, `genisis.CaseTransfer_Apply`, over synonyms |
| Raw `@strAppend` dynamic SQL in `Search_PanelHospitals2`, `Case_Transfer`, `Case_AuditReview`, `Case_ClaimAscii`, `Case_Adjustment` | parameterised `genisis.*` procedures (also closes a SQL-injection surface) |
| Read-then-update sequence numbers (bordx, ASCII batch) | one `UPDATE … WITH (UPDLOCK)` that reads and increments in a single statement (`@seq = col = col + 1`) |
| Same `Case_SavePayClaim` / `Case_UpdatePayClaim` / `Case_AuditInsert` / `Case_AuditUpdate` pairs | one upsert procedure each |
| Copy-pasted SQL-error handling (`catch (SqlException ex) when (ex.Number is 8152 or 2628)` in five controllers) | `ApiExceptionHandler` |

## 4. Redundancy removed from the new code base (this audit)

| Removed | Why |
|---|---|
| `try/catch` in the ten screen controllers | exception → status mapping lives in `ApiExceptionHandler`; an architecture test forbids new ones |
| Hand-written validation calls in controllers and the static `…Rules.Validate` methods of the nine screens | FluentValidation validators run once, in `ValidationFilter` |
| Dapper calls inside services of the nine screens | moved behind `I…Repository` (services are now unit-tested with fakes) |
| `PanelHospitalsController` / `CaseController.Payors` reading the database from the controller | `PanelHospitalsService`, `PayorLookupService` |
| In-process file lock around ASCII writes | not needed: batch numbers make every file name unique, and it would have tied the API to one instance |
| `lib/uiOnly.ts`, the "UI preview" banner and `notConnected()` calls | the screens are wired to the API |
| Eight copies of "busy ? 'Working…' : label" button markup | `components/Button` |
| Selection rules embedded in `CaseListingPage` | `pages/case/listingRules.ts` (unit-tested) |
| 7 inline `style=` attributes in older components | CSS classes (`.confirm-text`, `.code-input`, `.search.wrap`, `.ct-input`) |
| Duplicate access-position constants | `AccessRight.cs` and `access.ts` mirror each other, one position per screen |

Unused-code detection: `dotnet build` runs with warnings as errors in CI, `noUnusedLocals` / `noUnusedParameters` are on in `tsconfig`, and `oxlint` runs `no-unused-vars`
(0 warnings).

## 5. Candidates for deletion from the VB6 project (not done)

Once the desktop application is retired (or per module, once its web replacement is accepted):

1. The 17 orphan `_Click` handlers listed in section 1.
2. The 134 hidden menu controls that the business did not mark Required, and their forms when nothing else opens them.
3. `frmPanelHospitals` and any other `.frm` not listed in `medixhis.vbp`.
4. The raw-SQL stored procedures replaced by `genisis.*` procedures (`Case_Transfer`, `Search_PanelHospitals2`, `Case_AuditReview`, `Case_ClaimAscii`, …) — only after the desktop no longer
   calls them; the web application does not.
5. The Word `.DOT` letter automation and the SOAP device push.

## 6. Not found / not done

- No automated dead-code analysis exists for VB6; findings come from the menu designer, the `.vbp`, handler-to-control matching and reading each migrated form. Other forms in the
  project (outside the migrated modules) have **not** been analysed.
- The older web modules (Registration, Plans, Benefits, Case Registration / Adjustment / Enquiry / Tracking, Membership) still contain the per-module patterns listed in
  `architecture.md` section 6; converting them removes the remaining duplication.
