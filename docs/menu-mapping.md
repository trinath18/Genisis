# Menu / submenu mapping — VB6 `mdiMain.frm` → web application

Source: `VB6-MenuItems.xlsx` (the 241 `Begin VB.Menu` blocks of `mdiMain.frm`, extracted by `extract-menus.js`, plus the **Required** column maintained by the business).
Menu captions, hierarchy, designer flags and access positions come from the designer blocks and the `*_Click` handlers; nothing is read from a database table
(only the *permission flags* — `HISMaintenance.dbo.USR.USRAccess`, position *n* = `CheckAccess(n)` — and the time gate `dbo.TimeMain` influence what a user sees).

How to read the status column

- **Migrated** — a web route and API exist and are tested.
- **Required, not migrated** — marked `Y` by the business, still to do (the backlog).
- **Not required** — not marked `Y`; kept in the spreadsheet for traceability, not built.
- **Hidden in the desktop** — the designer sets `Visible = 0`, so the menu is not shown to users in the desktop app either.

## Summary by top menu

| Top menu | Items | Required (Y) | Migrated | Required, not migrated | Hidden in the desktop |
|---|---:|---:|---:|---:|---:|
| File | 6 | 3 | 0 | 3 | 2 |
| Membership | 28 | 13 | 3 | 10 | 23 |
| Contribution Billing | 12 | 5 | 0 | 5 | 9 |
| Case | 36 | 12 | 13 | 0 | 29 |
| Invoice | 9 | 5 | 0 | 5 | 8 |
| Worksheet | 9 | 6 | 5 | 1 | 5 |
| Authorize Claims | 18 | 2 | 0 | 2 | 14 |
| Claim Bordx | 34 | 23 | 0 | 23 | 21 |
| Finance | 11 | 5 | 0 | 5 | 2 |
| Reports | 4 | 0 | 0 | 0 | 0 |
| Statistic | 2 | 0 | 0 | 0 | 1 |
| Payment | 27 | 15 | 0 | 15 | 4 |
| Receipt | 6 | 5 | 0 | 5 | 1 |
| Posting | 4 | 3 | 0 | 3 | 1 |
| Maintenance | 24 | 16 | 2 | 14 | 13 |
| View | 2 | 0 | 0 | 0 | 1 |
| Window | 4 | 0 | 0 | 0 | 0 |
| **Total** | **236** | **113** | **23** | **91** | **134** |

(Separators are not counted. A top menu is itself an item; the "Required" flag on it means its sub-items are in scope.)

## Migrated screens (all phases)

| Menu path | VB6 menu control | VB6 form(s) | Web route | Access position | API |
|---|---|---|---|---:|---|
| Membership > Registration | `MembershipRegistration` | Membership registration form | `/membership/registration` | 1 | `/api/membership/registration` |
| Membership > Adjustment | `MembershipAdjustment` | Membership adjustment form | `/membership/adjustment` | 2 | `/api/membership` |
| Membership > Enquiry | `MembershipEnquiry` | Membership enquiry form | `/membership/enquiry` | 3 | `/api/membership` |
| Case > Case Registration | `CaseEntry` | frmCaseAdmission | `/case/registration` | 11 | `/api/case/registration` |
| Case > Case Adjustment | `CaseAdjustment` | frmCaseAdjustmentNew + frmGLMenu | `/case/adjustment` | 12 | `/api/case/adjustment` |
| Case > Case Inquiry | `CASInMBM` | frmCaseInquiry | `/case/enquiry` | 7 | `/api/case` |
| Case > Inpatient Case Listing | `mnuAppCase` | FrmWksBordx | `/case/listing` | 7 | `/api/case/listing` |
| Case > Case Transfer | `mnCaseTransfer` | frmCaseTransferMBMNo | `/case/transfer` | 12 * | `/api/case/transfer` |
| Case > Panel Hospitals | `mnuPanelHospitals` | frmPanelHospital | `/case/panel-hospitals` | 7 * | `/api/case/panel-hospitals` |
| Case > Case Audit Review | `CaseAuditReview` | frmCaseAuditReview | `/case/audit-review` | 12 * | `/api/case/audit-review` |
| Case > Case Tracking | `CaseTracking` | FrmCaseTracking | `/case/tracking` | 15 | `/api/case` |
| Case > MMA/CPT | `MMAFees` | frmMMA | `/case/mma-cpt` | 1 * | `/api/case/mma-cpt` |
| Case > Case Pay&Claim | `CasePayClaim` | frmCASPayClaim | `/case/pay-claim` | 12 * | `/api/case/pay-claim` |
| Case > Non-Disclousre Info | `mnuNonDisclousreInfo` | frmCASNonDisclosure | `/case/non-disclosure` | 12 * | `/api/case/non-disclosure` |
| Case > Claim ASCII | `mnuClaimASCII` | frmNewClaimAscii | `/case/claim-ascii` | 18 | `/api/case/claim-ascii` |
| Case > Case ASCII | `CaseASCII` | frmCaseASCII | `/case/case-ascii` | 18 * | `/api/case/case-ascii` |
| Worksheet > WorkSheet Adjustment | `mnuWSAdjust` | frmWorksheetAdj2013 (partly: viewing as the Enquiry and the Update button for payment details and remarks; Approve, credit/debit notes and letters not migrated) | `/worksheet/adjustment` | 23 | `/api/worksheet/adjustment` |
| Worksheet > Worksheet Enquiry | `mnuWSEnq` | frmWorksheetEnq (read-only: the editing half of the desktop form is Worksheet Adjustment) | `/worksheet/enquiry` | 13 | `/api/worksheet/enquiry` |
| Worksheet > Worksheet Send to A/C | `mnuWSToAC` | FrmWksSendToAC | `/worksheet/send-to-accounts` | 24 * | `/api/worksheet/send-to-accounts` |
| Worksheet > Worksheet Send to A/R | `mnuWSReturnCLM` | FrmARRecFrCLM (reduced: only the CASDRC / CASRecBy / CASRecStatus columns exist) | `/worksheet/send-to-ar` | 24 * | `/api/worksheet/send-to-ar` |
| Worksheet > Worksheet Tracking | `mnuWksTracking` | FrmWorksheetTracking | `/worksheet/tracking` | 24 * | `/api/worksheet/tracking` |
| Maintenance > Plan | `MaintenancePlan` | Plan maintenance forms | `/maintenance/plan` (tabs: Plan, Annual Limit, Premium, code lists, MCO fee, special note) | 1 | `/api/maintenance/{plans, annual-limits, premiums, code-lists, mco-fees, mgmt-fees, special-notes, benefit-limits}` |
| Maintenance > Benefits | `mnuBenefits` | Benefits maintenance forms | `/maintenance/benefits` | 1 | `/api/maintenance/benefits` |

\* The menu control has no `_Click` handler in `mdiMain.frm` (most are also `Visible = 0`), so the desktop menu can never open the form. Its access position is a decision recorded in `GenisisUI/src/auth/access.ts` and `Genisis.Api/Auth/AccessRight.cs`, not a value read from the VB6 menu code.

## Case module — every menu item

| # | Menu path | Control | Access pos | Opens (VB6) | Status | Web route |
|---:|---|---|---:|---|---|---|
| 1 | Case | `Admission` |  |  | Not required |  |
| 2 | Case > Case Registration | `CaseEntry` | 11 | frmCaseAdmission | Migrated (hidden in the desktop) | `/case/registration` |
| 3 | Case > Case Adjustment | `CaseAdjustment` | 12 | frmCaseAdjustmentNew | Migrated (hidden in the desktop) | `/case/adjustment` |
| 4 | Case > Case Inquiry | `CASInMBM` | 7 | frmCaseInquiry | Migrated (hidden in the desktop) | `/case/enquiry` |
| 5 | Case > Invoice Inquiry | `mnuInvoiceInquiry` | 8 | frmInvoice | Not required (hidden in the desktop) |  |
| 6 | Case > Upload Claim ASCII (IP) | `mnuUploadCLMASCII` | 6 | frmClaimASCIIUpoaldTIMS | Not required |  |
| 7 | Case > Upload Claim ASCII (OP) | `UplClaimASCIIOP` | 6 | frmClaimAsciiTIMSOP | Not required |  |
| 8 | Case > Case Review (Inpatient) | `mnuWSInq` | 5 | frmWorksheetAdj2013 | Not required |  |
| 9 | Case > Case Review (Outpatient) | `mnuCASReviewOP` | 5 | frmInvoiceOP | Not required |  |
| 10 | Case > Inpatient Case Listing | `mnuAppCase` | 7 | FrmWksBordx | Migrated | `/case/listing` |
| 11 | Case > Outpatient Case Listing | `mnuOPCaseListing` | 7 | frmWorksheetsAppOP | Not required |  |
| 12 | Case > Case Transfer | `mnCaseTransfer` | 12 |  | Migrated (hidden in the desktop) | `/case/transfer` |
| 13 | Case > Panel Hospitals | `mnuPanelHospitals` | 7 |  | Migrated (hidden in the desktop) | `/case/panel-hospitals` |
| 14 | Case > Temp Case Transfer | `mnTempCase` |  |  | Not required (hidden in the desktop) |  |
| 15 | Case > Lifetime Lmt Upgrade | `mnuMBMLifetimeLmt` |  |  | Not required (hidden in the desktop) |  |
| 16 | Case > Case Audit Review | `CaseAuditReview` | 12 |  | Migrated (hidden in the desktop) | `/case/audit-review` |
| 17 | Case > Audit Review Tracking | `mnuARTracking` |  |  | Not required (hidden in the desktop) |  |
| 18 | Case > Case Tracking | `CaseTracking` | 15 | FrmCaseTracking | Migrated (hidden in the desktop) | `/case/tracking` |
| 19 | Case > Case Tracking (Jupiter) | `CaseTracking2` |  |  | Not required (hidden in the desktop) |  |
| 20 | Case > MMA/CPT | `MMAFees` | 1 |  | Migrated (hidden in the desktop) | `/case/mma-cpt` |
| 21 | Case > Panel Hospital - OMNI | `mnuPanelHOSPMNI` |  |  | Not required (hidden in the desktop) |  |
| 22 | Case > Case Pay&Claim | `CasePayClaim` | 12 |  | Migrated (hidden in the desktop) | `/case/pay-claim` |
| 23 | Case > Non-Disclousre Info | `mnuNonDisclousreInfo` | 12 |  | Migrated (hidden in the desktop) | `/case/non-disclosure` |
| 24 | Case > Correspondence | `Correspondence` |  |  | Not required (hidden in the desktop) |  |
| 25 | Case > Send to Bordx Unit | `mnuCase2Claim` |  |  | Not required (hidden in the desktop) |  |
| 26 | Case > Send to Claims (Rep) | `mnuCase2ClaimRep` |  |  | Not required (hidden in the desktop) |  |
| 27 | Case > CRU Send to Claims | `mnuCRUSend2Claim` |  |  | Not required (hidden in the desktop) |  |
| 28 | Case > LG Tracking | `mnuLGTracking` |  |  | Not required (hidden in the desktop) |  |
| 29 | Case > Claim ASCII | `mnuClaimASCII` | 18 |  | Migrated (hidden in the desktop) | `/case/claim-ascii` |
| 30 | Case > Case ASCII | `CaseASCII` | 18 |  | Migrated (hidden in the desktop) | `/case/case-ascii` |
| 31 | Case > Claim ASCII(MedGen) | `mnuClaimASCIIMedGen` |  |  | Not required (hidden in the desktop) |  |
| 32 | Case > Claim ASCII (MedGen - New) | `mnuClaimASCIIMedGenNew` |  |  | Not required (hidden in the desktop) |  |
| 33 | Case > Claims ASCII (Etiqa) | `mnuClaimASCIIEtiqa` |  |  | Not required (hidden in the desktop) |  |
| 34 | Case > Claim ASCII (AXA Affin) | `mnuAXAAffin` |  |  | Not required (hidden in the desktop) |  |
| 35 | Case > Case ASCII (Zurich) | `CaseASCIIZur` |  |  | Not required (hidden in the desktop) |  |
| 36 | Case > Claims ASCII (Zurich) | `mnuClaimsASCIIZurichGen` |  |  | Not required (hidden in the desktop) |  |

## Worksheet module — every menu item

| # | Menu path | Control | Access pos | Opens (VB6) | Status | Web route |
|---:|---|---|---:|---|---|---|
| 1 | Worksheet | `Claim` |  |  | Not required (hidden in the desktop) |  |
| 2 | Worksheet > Worksheet Registration | `mnuNewWorksheet` |  | frmWorksheetRegGST2015 (newest registration form in the project; no click handler) | Required, **partly migrated**: the calculation page only (nothing is saved, no invoice entry) |  |
| 3 | Worksheet > WorkSheet Adjustment | `mnuWSAdjust` | 23 |  | Migrated | `/worksheet/adjustment` |
| 4 | Worksheet > Worksheet Enquiry | `mnuWSEnq` | 13 | frmWorksheetEnq | Migrated | `/worksheet/enquiry` |
| 5 | Worksheet > Generate Credit/Debit Note | `mnuCreditDebitNote` |  |  | Not required (hidden in the desktop) |  |
| 6 | Worksheet > Worksheet Send to A/C | `mnuWSToAC` | 24 | FrmWksSendToAC (never opened by the desktop code) | Migrated (hidden in the desktop) | `/worksheet/send-to-accounts` |
| 7 | Worksheet > Worksheet Send to A/R | `mnuWSReturnCLM` | 24 | FrmARRecFrCLM (never opened by the desktop code) | Migrated (hidden in the desktop) | `/worksheet/send-to-ar` |
| 8 | Worksheet > Worksheet Tracking | `mnuWksTracking` | 24 | FrmWorksheetTracking (scan code LOW003; no click handler) | Migrated | `/worksheet/tracking` |
| 9 | Worksheet > Worksheet Tracking (Jupiter) | `mnuWksTracking2` |  |  | Not required (hidden in the desktop) |  |

## Backlog — items marked Required that are not migrated yet

**File** (2): Logout; Close Connection

**Membership** (10): Leaflet & Card; Card Sending Mode; Member Reinstatement; MCO Pending; Deletion; Tracking; Generate Membership ASCII(Pluto); Upload Bordx; Temp MBM Reg; Temp MBM Adj

**Contribution Billing** (5): Generate Invoice; Generate Invoice (Endorsement); MCO Reinstatement; Refund Listing; Receipt MCO/Others

**Invoice** (5): Invoice Processing (read side migrated at `/invoice/processing`: a case's invoices and an invoice's amounts by worksheet row; adding, changing and removing are not); Original Invoice Update; Invoice MBM Accounting; Invoice HOS Accounting; Invoice Tracking

**Worksheet** (1): Worksheet Registration

**Authorize Claims** (2): DCU Tracking > Claim Report; Claim Pass to A/C

**Claim Bordx** (22): Worksheet; Worksheet > Insert Worksheet; Worksheet > Remove Worksheet; Print Bordx; Print Recovery; Payment Advice; Payment Advice > Generate Bordx No; Payment Advice > Hospital Report; Update Bordx; Bor Delivery; Bor Delivery > Bordx Send Date; Bor Delivery > Send to Payor; Bor Delivery > Returned From Payor; Bor Delivery > Returned Fr Payor(Not App); Bor Delivery > Send to A/C; Monitoring; Monitoring > Bordx Control; Monitoring > Outstanding Bordx; Bordx Approval; Storage > Claim files; Storage > Enquiry; Storage > Document

**Finance** (4): Receipt Enquiry; Invoice Receipt (Outstanding); Short Payment Receipt; Reverse Receipt

**Payment** (14): To Hospital; To Member; To Member > Pay2MBM(Auto); To Member > Pay2MBM; To Member > Chq. Cancellation; To Member > Chq. Reissue; Payor; Others; Contra Allocation; Update Invoice; Payment Tracking; Enquiry > Payment To Hospital; Enquiry > Payment To Member; PV Update (Manual)

**Receipt** (4): From Member; Others; Payor; Receipt Tracking

**Posting** (3): Invoice; Invoice > Hospital; Invoice > Member

**Maintenance** (14): Entity; Accounts; Account Transaction; Entity; Member; Panel Hospital Maintenance; Suspension; Company Info; Surgical Schedule; MMA; Security; Security > Department; Security > Role Management; Security > User Permission

## Notes

- Forms reached from a screen rather than from the menu (letters, GL menu, document dialogs, member lists, the case list pop-up, code search dialogs) are migrated with that screen as dialogs/tabs; they have no menu item of their own. See the per-module migration documents.
- 17 of the 80 `*_Click` handlers in `mdiMain.frm` have no matching menu control (renamed/removed in the designer): they are dead code and are not migrated (names in the [redundancy report](redundancy-removal-report.md)).
- Worksheet screens: the form chosen for each item, the access positions and every difference from the desktop are in [worksheet-screens.md](worksheet-screens.md).
- Regenerate the spreadsheet with `node extract-menus.js` in the VB Code folder; this document is generated from it plus the Required column.
