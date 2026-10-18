# Case module — remaining screens (UI) — VB6 → Web notes

> **Status:** these screens were first built UI-only; they are now **wired to the API**. The backend, SQL objects, deviations from the desktop and
> tests are in [case-remaining-screens-backend.md](case-remaining-screens-backend.md). The "UI preview" banner and `lib/uiOnly.ts` were removed.

Scope: the Case menu items marked **Required = Y** (column P) in `VB6-MenuItems.xlsx` that were not migrated yet.
Every screen runs its client-side validation, confirmations and workflow (enable/disable) states, then sends the request.

Skipped because they already exist: Case Registration, Case Adjustment, Case Tracking.

## Menu + screen mapping

| Menu caption (VB6) | VB6 menu control | VB6 form | Web route | Access position (basis) | Page |
|---|---|---|---|---|---|
| Inpatient Case Listing | `mnuAppCase` | `FrmWksBordx` ("Select Worksheets for Bordx") | `/case/listing` | 7 (`mnuAppCase_Click`) | `CaseListingPage.tsx` |
| Case Transfer | `mnCaseTransfer` | `frmCaseTransferMBMNo` | `/case/transfer` | 12 (no handler; sibling of Adjustment) | `CaseTransferPage.tsx` |
| Panel Hospitals | `mnuPanelHospitals` | `frmPanelHospital` | `/case/panel-hospitals` | 7 (no handler; read-only listing like Enquiry) | `PanelHospitalsPage.tsx` |
| Case Audit Review | `CaseAuditReview` | `frmCaseAuditReview` | `/case/audit-review` | 12 (no handler; sibling of Adjustment) | `CaseAuditReviewPage.tsx` |
| MMA/CPT | `MMAFees` | `frmMMA` | `/case/mma-cpt` | 1 (no handler; same as other fee-schedule maintenance) | `MmaCptPage.tsx` |
| Case Pay&Claim | `CasePayClaim` | `frmCASPayClaim` | `/case/pay-claim` | 12 (no handler; sibling of Adjustment) | `CasePayClaimPage.tsx` |
| Non-Disclosure Info | `mnuNonDisclousreInfo` | `frmCASNonDisclosure` | `/case/non-disclosure` | 12 (no handler; the form is opened from Case Adjustment) | `CaseNonDisclosurePage.tsx` |
| Claim ASCII | `mnuClaimASCII` | `frmNewClaimAscii` | `/case/claim-ascii` | 18 (`mnuClaimAscii_Click`) | `AsciiExportPages.tsx` → `AsciiExportForm.tsx` |
| Case ASCII | `CaseASCII` | `frmCaseASCII` | `/case/case-ascii` | 18 (no handler; same family as Claim ASCII) | `AsciiExportPages.tsx` → `AsciiExportForm.tsx` |

Notes on the mapping:

- Six of the nine menu controls have **no `_Click` handler anywhere** in the VB6 source and are `Visible = 0`, so the desktop app
  cannot open their forms. The forms were matched by caption (e.g. "Panel Hospitals" = `frmPanelHospital`, in the `.vbp`;
  `frmPanelHospitals`, "Panel Hospital", is not in the `.vbp`). Their access positions are therefore a **decision**, listed above
  and centralised in `src/auth/access.ts` — change them there if the business wants different gating.
- The spreadsheet shows blank "Access Pos" / "Opens Form" for those six rows for the same reason. The Claim ASCII row was also
  blank because the first version of `extract-menus.js` matched handler names case-sensitively (`mnuClaimASCII` vs
  `mnuClaimAscii_Click`; VB6 is case-insensitive). The script is fixed.
- The same positions are mirrored in `Auth/AccessRight.cs` and enforced with `RequireAccess` on the API controllers.
- Sidebar: Case now has grouped links — Case Workflow, Review & Listing, Transfers & Reference, Exports.

## What is built per screen

| Screen | Layout | Workflow / validation kept from VB6 |
|---|---|---|
| Inpatient Case Listing | Selection Criteria card, command bar with counters (Records, Selected, Total Amt), tabs *Worksheets* / *Generated Bordx* | "Use Scan" toggles single Wks No + Version vs Wks No from/to; Wks No required in scan mode ("Please Enter Worksheet No"); DOA/DOD "is empty" filters (disable their date range); Bordx guards in legacy order (no records → none ticked → Bordx option) then "Do you want to update the record?"; per-row tick rules (DOD empty, hospital empty, claimability not decided, group company empty for G/H); column sort; Export to CSV ("Report cannot be printed without any record.") |
| Case Transfer | Case card, *Current Member* (read-only) and *Transfer To* cards, Transfer / Cancel / Close | Transfer card disabled until a case is loaded; transfer Member No + Cover ID required |
| Panel Hospitals | Selection Criteria, command bar with Record(s) counter, sortable 9-column list | Panel code required ("Please Select Hospital Panel Code!"); typing a new code clears the list; Print (print stylesheet) |
| Case Audit Review | Case + Member cards, tabs *Audit Review* (Invoice, Letters & Status, Audit Review, Payment Record, Hospital Visit) / *General Info* / *Case Details* (read-only) | Case No and Discount Type required; date and `hh:mm` format checks; read-only tabs mirror the disabled VB6 frames; invalid update returns to the Audit Review tab |
| MMA / CPT | One command bar (Search, Add, Edit, Delete, Save, Refresh, Print, Clear, Close) driving tabs *Procedures* / *Look Up Table*, each with entry form + sortable list + Total Records | Mode rules: Edit/Delete only with a selected row, Save only in Add/Edit, selected row locks the form until Edit, tab change returns to browse; mandatory fields; KeyNumber fields; delete/save confirmations |
| Pay & Claim | Case + date card, Add/Edit/Save, tabs *Member Details* (members list) / *Pay & Claim Details* (member, Actions, history) | Load → date defaults to today, actions locked; Add clears date + reasons; "Others" requires remarks; Case No, date (valid) required; Edit only when history exists |
| Non-Disclosure Info | Case card, *Pre-Existing Illness* and *Underlying Illness* cards | "Others" box enabled only for category 4 (pre-existing) / 3 (underlying); date format check |
| Claim ASCII / Case ASCII | One shared form: *Cases to export* (Claim only), *Export Criteria*, Generate / Clear / Close | Payor + date range required (label and messages follow the variant: Bordx Date vs Case Reg. Date); Export / Bordx WS (Endt Status) shown per variant; "Are you sure?" |

## Removed or changed VB6 elements (with justification)

| Screen | VB6 element | Decision | Why |
|---|---|---|---|
| Inpatient Case Listing | `Stop` button (`intExit` flag) | Removed | Interrupt flag for the single-threaded VB6 search loop; becomes request cancellation when the search is wired |
| | `Check All` button | Replaced | Header select-all checkbox on the grid does the same and is keyboard/screen-reader accessible |
| | Hidden `CboBTBG` + "BTBG Status", `List1`, `Text1`, `txtClaimStatus`, `lblTotal`/"of" (`Visible = 0`) | Removed | Hidden implementation plumbing; the scanned-document check behind `List1` is server-side logic |
| | `lstBordxListing` swapped in over the worksheet list | Replaced | Two tabs; selecting a bordx row still shows its Bordx No (`lblBordxNo`) |
| | Date range + "Empty Date" both usable | Changed | Ticking "is empty" disables the range (the combination always returned nothing) |
| Case Transfer | No confirmation before transferring | Added | A transfer changes the member on the case; a Yes/No confirm improves safety |
| Panel Hospitals | Search on combo selection (hidden `CmdSearch`) | Changed | Explicit **Search** button (and Enter); avoids a query on every type-ahead change |
| | `frmPanelHospitals` ("Panel Hospital", payor/plan/hospital filters + Advance Search) | Not migrated | Not in `medixhis.vbp`, no caller — dead form |
| Case Audit Review | `Prev`, `Next`, `Next Rev Case` buttons (`Visible = 0`) | Removed | Hidden; case stepping was also dropped on Case Adjustment at the user's request |
| | `lstBenefitDetails` | Removed | Belongs to no `SSTab` page and sits off-screen — never visible |
| | `txtINSCode` / "Insured Type" (`Left = 14400`), `txtINSCode1`, `txtPLNCode`, `txtHLTCOde`, `txtSRVPath` | Removed | Off-screen / hidden plumbing controls |
| | Hidden `ListView1` (PV No / PV Date / Amt Paid) | Replaced | Read-only *Payment Record* fields |
| | `lblWaitingPeriod` / `lblExpired` alerts | Deferred | Derived from loaded policy data; add with the API |
| MMA / CPT | "Bold = searchable" labelling | Replaced | Explicit text under the form title |
| | OPCS Code `*` not enforced on save | Enforced | The label says mandatory and delete already requires it |
| | Look-up save accepted an empty record | Guarded | Requires a surgeon's or anaesthetist's category |
| Pay & Claim | `cboLetterType` + "Letter Format" (`Visible = 0`, validation commented out) | Removed | Hidden and unused |
| | "Are you sure?" only inside the `chkInfoProv` branch | Changed | One confirmation for every save |
| | Word letter generation (`funcword` / `bookmark`) | Out of scope | Backend/document logic |
| Non-Disclosure Info | Opened only from `frmCaseAdjustment` (`TempCASNumber`) | Changed | Standalone page with its own Case No; "Others" text is cleared when its category changes |
| Claim / Case ASCII | "File on PC" (`C:\`, `C:\CaseASCII\`) | Removed | A browser cannot write to a local folder; the file is downloaded instead |
| | Case ASCII messages said "Bordx Date" for the Case Reg Date field | Fixed | Wording now matches the field |
| | Two near-identical forms | Merged | One `AsciiExportForm` configured per screen (DRY) |
| | Export / worksheet status validated on Case ASCII only | Changed | Required whenever shown |
| All | `Exit`, `Go`, `OK`, `MsgBox` | `Close`, `Load`, `Generate`, toast / inline error / `ask()` | Web terminology (`script.txt`) |

## Shared building blocks added

- `components/Field.tsx` — `Field`, `TextField`, `NumberField`, `TimeField`, `ReadField`, `TextAreaField`, `SelectField`, `ComboField`
  (VB6 editable combo → input + datalist), `CheckField`, `DateField` (wraps `DateInput`, the calendar picker).
- `components/Button.tsx` — the one button (variants `primary` / `ghost` / `link`, `busy` + `busyLabel`, defaults to `type="button"`); `components/ErrorBoundary.tsx` keeps a rendering crash inside the screen.
- `components/DataGrid.tsx` — sortable (`aria-sort`), optionally selectable/checkable table with an empty state that keeps the headers.
- `components/Tabs.tsx` — ARIA tabs with arrow/Home/End keys. `components/Screen.tsx` — `PageShell`, `ActionBar`, `Stat`.
- `lib/validation.ts` (Yup rules: required text, `dmyDate`, `hhmmTime`, `amount`), `lib/exportCsv.ts`.
- `pages/case/useScreenLookups.ts` — dropdown data (payors `GET api/case/payors`, panel codes, worksheet versions 1–20); only the lists a screen asks for are requested.
- Libraries added (per `script.txt`): `react-hook-form` 7.89.0, `yup` 1.7.1, `@hookform/resolvers` 5.9.1 (exact, all ≥ 7 days old).
- `parseDmy` moved to `lib/masks.ts` (re-exported from `caseShared.ts`). CSS only (no inline styles); print support via `.print-area`.

## Wiring (done)

1. Each former `notConnected(...)` call is now the request; the validation/confirm before it is unchanged.
2. `useScreenLookups` feeds payors, panel codes and worksheet versions from the API.
3. Read-only fields, grids and counters (`Records`, `Total Amt`, `Total Records`, `Total Cases`, `Export file name`) are populated from the responses.
4. Data-dependent rules are enforced by the API and shown as toasts: member/patient not found, member status DELETE, invalid Cover ID (Case Transfer);
   scanned documents present (Inpatient Case Listing); the audit review member panel is read from `MBM`. The waiting-period / expired alerts of Audit Review are still deferred.
5. Changed while wiring: the Pay & Claim "Letter Status" column was dropped (the desktop never filled it); MMA OPCS Narrative is limited to 400 characters
   (the real column size); Claim/Case ASCII show the generated file name and download the file.
