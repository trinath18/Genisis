# Case Registration — VB6 → Web migration notes

Module: **Case > Registration** (`frmCaseAdmission`, menu access position 11)
New route: `/case/registration` · API: `/api/case/registration/*` (ASP.NET Core 8) · UI: `GenisisUI` (React + TypeScript)

## Architecture

```
Browser (React)                      ASP.NET Core API                     SQL Server (MYDELL)
-----------------                    -----------------                    -------------------
CaseRegistrationPage.tsx  ──HTTP──▶  CaseRegistrationController  ──▶  genisis.* procs (HISDB)
CaseRegistrationDialogs.tsx          CaseRegistrationService            dbo.* insert procs (existing)
components/Modal.tsx,                CaseRegistrationModels.cs          genisis.Maint_* synonyms
 lib/notifyStore.ts (toasts,         (request/validation)              → HISMaintenance
 confirm), DataTable/FieldGrid
```

- Controllers stay thin; business rules live in `CaseRegistrationService` / `CaseRegistrationValidator`.
- All data access is Dapper + stored procedures; the legacy `Case_Registration` dispatchers that appended raw `@strAppend` SQL are **not** called — parameterized `genisis.CaseReg_*` procedures replace them (`sql/main/140_case_registration.sql`).
- The save reuses the existing parameterized insert procedures (`Case_RegInsertCAS2013/32013`, `Case_RegInsertCASRemark/2/3`, `Case_RegInsertDIAFirst/DIAFinal`, `Case_RegInsertUD`, `Case_RegInsertCASOthers/2`, `Case_RegInsertCASRep`, `Maint_Case_RegInsertCASRef/1`) inside **one transaction** — the desktop wrote the cross-reference outside its transaction.

## Menu + interaction mapping

| VB6 (`mdiMain.frm` / `frmCaseAdmission`) | Web app |
|---|---|
| Case > Registration (position 11) | Sidebar Case > Registration → `/case/registration`, `Access.CaseRegistration` |
| `cmdShow` (Go) / `cmdShow.Default` = Enter | Go button / Enter in membership box; partial number opens member search |
| `cmdSearch` → `frmSearchMBM` | `MemberSearchDialog` ("Search For Member") |
| `CmdDiagPre/Final/UD` → `frmSearchICD` | `CodeSearchDialog` ("Search ICD") per diagnosis group |
| `CmdDiagRepudiation` → `FrmSearchRepudiation` | `CodeSearchDialog` ("Search Repudiation") |
| `CboClaimability_Change` → `frmRepudiationSelection` | `RepudiationSelectionDialog`, auto-opened for repudiated codes |
| `cmdOPCS_Click` (idx 1/2) → `frmSearchCPT` | `ProcedureSearchDialog` "Search Procedure" → surgical / non-surgical OPCS slots |
| `cmdInvList_Click` → `frmSearchInv` | `ProcedureSearchDialog` "Search Investigation" (ProcPart 'A') → INV code slots |
| SSTab1 (12 tabs) + Case Mgmt Notes pages 1–5 | `TabStrip` + page links; tab data lazy-loaded (`SSTab1_Click` parity) |
| `Form_KeyPress` → `EnterToTab` | Enter moves focus to the next field (fields with their own Enter action are skipped) |
| `cboX_KeyPress` typed-character combo matching | native `<select>` type-ahead |
| `KeyNumber` numeric-only amount fields | `numeric` prop on the same fields |
| `~` rejected in remarks (`txtCASRemark_KeyPress`) | same rule, client + server |
| F6 `PrintScreenSnapShot` | **not migrated** (screenshot capture; browser print suffices) |
| cmdLG (`CheckLimit`, `GetLGCode`, `Case_AdjInsertCASLG`, `MobileNotification`) | `POST /lg` + `LetterModal`; balance check, LG sequence and CASLG insert run server-side in one maintenance transaction |
| cmdPrior / cmdTHDrugs / cmdDocReqAdm / cmdDocReqDis (Word `.DOT` letters) | `POST /letter` returns every bookmark field; `CaseLetters.tsx` renders a printable letter (Print → browser print). The `.DOT` templates never shipped with the source, so server-rendered letters replace Word automation |
| Post-save letters (P/E within the expiry window, R for repudiated) | queued automatically after a successful Register |
| `MobileNotification` MBMAndroidNotification row | written by `POST /lg`; the SOAP push to the legacy device service is retired |
| CPT/Inv `&Print` (Excel export), `CPT Info`/`Inv Info` (maintenance entry) | not migrated — export/maintenance screens, not registration flow |
| Policy Wording ShellExecute | tab shows the wording reference; file opening not migrated |

## Preserved behaviors / business rules

- Member load order and **every alert text** from `cmdShow_Click` (plan prompts, annual limit, agent, 90-day non-cashless, expired/suspended/cancelled, take-over clause, GL not allowed).
- Patient selection warnings (birthdate, plan age limits, spouse/child employment, pre-existing/specific-illness/waiting-period dates).
- Hospital blur checks: named-placeholder hospitals require a typed name; cashless + checking payor + private hospital + not on panel → refused (checks the **payor's own** panel; see deviations).
- Date blur checks, save validations (same messages, same order), desktop confirmations (>5 days, take-over clause, <30 days active, not discharged).
- Case number from the maintenance `CAQ2013` counter under `UPDLOCK/HOLDLOCK` inside the transaction.
- Remarks split at 1900 chars with `~\n` line markers, `Enter`→Tab navigation, masked `dd/MM/yyyy` dates.

## Intentional deviations (documented, behavior-preserving or usability fixes)

| Item | Desktop | Web | Justification |
|---|---|---|---|
| Repudiation `Attention` | required but its list is hidden → repudiated cases unsaveable | only Document + Personnel required | desktop bug made the rule unreachable |
| Panel check for non-AX payors | always compared against the AX panel | uses the payor's own panel | clear legacy bug |
| Cross-reference insert | outside the transaction | inside the transaction | prevents orphan rows on failed saves |
| `Maternity`, `R&B Days`, `LG Remark` | editable, never saved | editable, never saved (title notes it) | desktop parity |
| Raw `@strAppend` dispatchers | string-concatenated SQL | parameterized `genisis.*` procs | SQL-injection-safe, same results |

## Error handling & logging

- `CaseRegistrationValidator.Validate` → `400 {message}` with the desktop's message text.
- `CaseRegistrationException` (business-rule failures inside the save) → rollback + `400`.
- `SqlException` 8152/2628 (truncation) → friendly `400`.
- Unhandled errors → global `UseExceptionHandler` → `500 {message}`, **now logged** (`ILogger`, method + path).
- `CaseRegistrationService` logs every committed registration (`case number, member, user`) and every rolled-back failure.
- UI: workflow errors keep the desktop's message boxes; tab lists show `Loading…`/`No records`; buttons disable while a request is in flight.

## Accessibility / responsiveness improvements (new, non-deviating)

- **Modern layout**: the twips-positioned VB6 look was replaced with the app's design system — labelled `.form-field`s in responsive fieldset grids, real `<label>` association, the shared `.tabs`/`DataTable`/`FieldGrid` components, and modal `components/Modal.tsx` dialogs. Every desktop field, tab and check is preserved.
- **Toasts + confirm modal** (`lib/notifyStore.ts`, `NotifyHost` in AppLayout) replace `MsgBox`/`vbYesNo` — the same message texts, non-blocking.
- `role=dialog` + `aria-modal`, focus trap + autofocus + Escape-to-close + focus restore on all dialogs.
- List rows keyboard-operable (Enter/double-click picks), responsive grids, sticky action bar.

## Tests

182 tests passed when this was written (the whole API suite is 808 now; `dotnet test` with `GENISIS_TEST_*` env vars). The editor was later extracted into `useCaseEditor.tsx` + `CaseSections.tsx` and is shared with Case Adjustment (`docs/case-adjustment-migration.md`). Case registration coverage: validator rules with desktop messages, member-load checks order, repudiation rules, transactional save asserting every written table (rolls back), OPCS/investigation search proc.

### Browser suite

`GenisisUI/e2e/case-registration.mjs` (31 checks) drives the real screen against the ZZ test member `ZZHA0000518*01`:

* the page, an unknown member (`No record found!`), the member's load (the expired-membership alert, status Open and locked, claimability AA, patients 00, 01 and 02);
* what an expired membership does: Register stays disabled while the admission and discharge date checks (the "chk" boxes) are on, and enables once both are off, which is the desktop's back-dating route;
* the save messages in the desktop's order: Hospital, Admission Date, ICD Code (cashless is the default), Patient's Age, Week/Month/Year;
* the confirmations: a complete form asks "Are you sure", No registers nothing, Yes registers the case and shows its number and the expired-membership letter;
* the saved case read back through the API (member, patient, status, claimability, admission date, stay type, reimbursement, hospital);
* the same patient and admission date again asks "Patient has not been discharged, do you want to continue" (the same-day admission rule).

It writes one real case, so `e2e/cleanup.sql` removes it: every case opened by `E2E` on the ZZ payor, with its rows in the nine case tables and the case cross reference, and the ZZ counter put back to the highest ZZ case left. The suite starts by checking that no case of an earlier run is left. The list of tables was not assumed: the server's own update counters before and after a registration name the same ten tables and the counter, a scan of all 60 tables with a `CASNumber` column in the four databases found rows for the new case in exactly those ten and none after the cleanup, the member's available limit did not move, and the suite ran twice in a row from a clean start.

**Found by the suite, and fixed:** the validation toasts stack down from the top right over the **Register** button for a few seconds, so a click soon after a message landed on a toast (the click log showed `DIV.toast` as the target) and sent nothing; the second click worked. The same thing hid the Preview button of the new Invoice accounting screens. The toast stack no longer takes clicks (`pointer-events: none` in `index.css`, with the dismiss button still clickable), so a click goes through to the button under a message; the suite now fails if a Register click is ignored (it was 3 before the change, 0 after).

## Remaining (deferred)

- The legacy SOAP push to the mobile device service (the `MBMAndroidNotification` row is still written).
- Policy Wording file opening; Excel export buttons on search windows; CPT/Inv Info maintenance windows.
- `cboSuppExc` picks supplementary exclusion; `Maternity`/`R&B Days`/`LG Remark` remain unsaved (desktop parity).
