# Case Adjustment — VB6 → Web migration notes

Module: **Case > Adjustment** (`frmCaseAdjustmentNew` + `frmGLMenu` + `frmCASList`, menu access position 12)
New route: `/case/adjustment` · API: `/api/case/adjustment/*` (ASP.NET Core 8) · UI: `GenisisUI` (React + TypeScript)

## Architecture

```
Browser (React)                      ASP.NET Core API                     SQL Server (MYDELL)
-----------------                    -----------------                    -------------------
CaseAdjustmentPage.tsx  ──HTTP──▶    CaseAdjustmentController   ──▶  genisis.CaseAdj_* procs (HISDB)
CaseAdjustmentDialogs.tsx            CaseAdjustmentService               genisis.Maint_* synonyms
 (GlDialog, CaseSearchDialog)        CaseDocumentsService (GL)           → HISMaintenance
useCaseEditor.tsx +                  CaseFilesService (documents)        dbo.VIEW_WS_Listing
 CaseSections.tsx (shared            CaseAdjustmentValidator             → HISWorkSheet
  with Registration)                 (update/repudiate rules)
```

- Case Adjustment reuses the registration editor: `useCaseEditor` (form state, blur checks, member/patient context) and `CaseSections` (tab bodies) are shared; `CaseRegistrationPage` was refactored onto them. Adjustment-only tabs (LG, Worksheet, Forms, Policy Wording) live in the adjustment page.
- The update path wraps all CAS/CASRemarks/DIA/UD/Rep/CASOthers/tblRecord writes in **one transaction** — the desktop ran ~15 writes across two databases with no transaction.
- `Case_Adjustment` / `SearchCasAdjustment` legacy dispatchers that concatenated `@strAppend` SQL are **not** called — parameterized `genisis.CaseAdj_*` procedures replace them (`sql/main/150_case_adjustment.sql`).
- Access right position **12**; the shared registration lookup/search endpoints accept position 11 **or** 12 via `RequireAccess(AccessRight.CaseRegistration, AccessRight.CaseAdjustment)`.

## Menu + interaction mapping

| VB6 (`frmCaseAdjustmentNew`) | Web app |
|---|---|
| Case > Adjustment (position 12) | Sidebar Case > Adjustment → `/case/adjustment`, `Access.CaseAdjustment` |
| `cmdShow` (Go) / `cmdShow.Default` = Enter | Go button / Enter in Case No box → `GET /{cas}` |
| `cmdSearch` → `frmCASList` | `CaseSearchDialog` → `GET /search` (by case/member/policy/name/ic/employee/polsub) |
| `cmdPrev` / `cmdNext` | **not migrated** (removed per user request — the search dialog replaces case stepping; `GET /{cas}/neighbour` remains in the API) |
| SSTab1 (13 tabs) | same tab order; General Info…Surgical Schedule shared with registration; LG/Worksheet/Forms/Policy Wording are adjustment-only; lazy-loaded |
| `cmdUpdate1` + ">5 days" → field checks → "Are you sure?" | `PUT /{cas}` — same validation order/messages client-side, re-checked server-side |
| `cmdRepudiate` | `POST /{cas}/repudiate` — repudiation save + case decline + repudiation letter in one transaction |
| `cmdLG` → `frmGLMenu` (purpose rules, request details) | `GlDialog` → `GET /gl-options`, `GET /{cas}/gl` (defaults), `POST /{cas}/gl/check` (balance), `POST /{cas}/gl` (issue) |
| LG tab listing + `cmdDocReqAdm` "Delete" | `GET /{cas}/lg`, `DELETE /{cas}/lg/{lgNo}` (double-click row → "Are you sure?") |
| `cmdPrior` "Notify" (P/E letter, expiry window) | `POST /{cas}/letter/auto` — server resolves P vs E (≤45 days / past) |
| `cmdDocReqDis` "Doc Disch" | `POST /{cas}/letter/D` — letter + `PENDING DOCUMENT FROM HOSPITAL` progress |
| `cmdGPQA` / `cmdReimReq` / `cmdPrint` "Mgmt Note" | `POST /{cas}/letter/{I,S,M}` — printable letters (`LetterModal`) |
| `cmdTHDrugs` "C. Ref AA" / "C. Ref RR" | `POST /{cas}/progress/referral-accept|referral-reject` — same `tblRecord` flags + `CaseProgress` text |
| `cmdClearDoc` "Clear Doc Record" | `POST /{cas}/progress/clear-doc` |
| `cboPendingReason` (Form_Load `SearchPendingReason`) | `GET /pending-reasons` (via `Maint_` synonym EXEC) |
| "Accepted + pending" case load → claimability reset prompt | `askResetClaimability` flag → `POST /{cas}/reset-claimability` |
| Forms tab `lstDoc*` + `CmdBrowse`/`CmdCopy` + `cmdDelGL` | `GET/POST/DELETE /{cas}/documents[/{folder}/{name}]` — ScannedDoc, InternalDocuments, FinalLgs |
| Policy Wording `ShellExecute` | `GET /policy-wording/{wording}[/{name}]` — list + download |
| Word `.DOT` mail-merge letters | server-side letter data → `CaseLetters.tsx` printable preview (same bookmark fields) |

## Preserved behaviors / business rules

- Same member/patient/hospital/date field checks as registration — they run in the shared editor.
- Update confirmations in desktop order: ">5 days" (DOA→DOD), field validations, "Are you sure?".
- `txtAnaesDoc`/`txtATTDoc`/`txtCASRemark` ~ handling, `CASORemarks`+`CASORemarksTwo` merge on load, remark split on save.
- GL rules (`CaseGLRules`): required fields, purpose-block matrix vs the last GL purpose, request-mode/fax checks, same-purpose re-issue, close an open GL of a different purpose — all in one maintenance transaction (`IssueGLAsync`).
- GL number `L{cas}-{seq}` (`B` prefix for BTBG plans); `LGStatus` drives the B/L prefix in the listing, matching `lstLGListing`'s `L/B{cas}-{seq}{purpose[0]}` text.
- Progress actions write the same `IsRejected`/`IsLGissued`/`CaseProgress` strings the desktop wrote.
- `PendingReason` → `tblRecord.DocRemark`; `DocReceivedDate` → `CASDRD`; `LinkCaseNo` → `LINKCASNo`.

## Redundant / dead code in the VB6 (not migrated)

| Item | Finding |
|---|---|
| `cmdSaveAdd` "Upd &Add" | no event handler — dead button |
| `Case_Adjustment` `strAppendCase` modes | string-concatenated inline SQL; replaced by parameterized procs (same result sets) |
| Duplicated `CheckLimit` | near-copy of the registration version; one shared server balance check (`BalanceLimitAsync(adjustment: true)`) |
| `ShowButtonState`/`cmdUpdate1` duplicated field validations | client + server validator share the same message list |

## Intentional deviations (documented)

| Item | Desktop | Web | Justification |
|---|---|---|---|
| Update transaction | none — partial writes possible | all writes in one transaction | prevents half-saved cases |
| Letters | Word `.DOT` via COM | server-rendered printable letters | templates never shipped with the source |
| Forms tab | ShellExecute on `DocPath` folders | path-safe list/download/upload/delete under `Documents:RootPath` | no client file-system access in a browser |
| LG delete | "Delete" button on selected list item | double-click / Enter on the row | web grid idiom |
| Member search | n/a — member is fixed by the case | member fields read-only in the header | the case owns the member |
| SOAP device push | `MobileNotification` SOAP call | `MBMAndroidNotification` row written; push retired | legacy service decommissioned |

## Tests

182 tests pass (`dotnet test`). Adjustment coverage: `CaseAdjustmentValidator` update/repudiate rules with the desktop's messages, `CaseGLRules` validation order + purpose-block matrix, and a DB-gated test that registers a case then runs the full adjustment update inside a rolled-back transaction.
