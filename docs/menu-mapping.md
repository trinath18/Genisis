# Menu / submenu mapping — VB6 `mdiMain.frm` → web application

Source: `VB6-MenuItems.xlsx` (the 241 `Begin VB.Menu` blocks of `mdiMain.frm`, extracted by `tools/extract-menus.js`, plus the **Required** column maintained by the business).
The VB6 source of record is the copy on `D:` (`D:\Team Awesome\VB Code-20261009T221049Z-1-001\VB Code`). The sheet was first extracted from the original copy as received; on 24 Oct 2026 the menu was
extracted again from the `D:` copy and compared with it row by row: the same 241 items with the same captions, hierarchy, shortcuts, access positions, time gates and forms. The two differences are the
**Visible** flag (below) and `mnuClaimASCII`, whose handler (position 18, `frmNewClaimAscii`) the earlier sheet had left blank and this page already used.
Menu captions, hierarchy, designer flags and access positions come from the designer blocks and the `*_Click` handlers; nothing is read from a database table
(only the *permission flags* — `HISMaintenance.dbo.USR.USRAccess`, position *n* = `CheckAccess(n)` — and the time gate `dbo.TimeMain` influence what a user sees).

How to read the status column

- **Migrated** — a web route and API exist and are tested.
- **Required, not migrated** — marked `Y` by the business, still to do (the backlog).
- **Not required** — not marked `Y`; kept in the spreadsheet for traceability, not built.
- **Hidden in the desktop** — the designer sets `Visible = 0` **in the original copy of the source** (134 items). The `D:` copy, the source of record, has no `Visible = 0` flags, so the build it makes shows every item and this
  label does not describe that build. It is kept because it records which items the original hid, and together with "no click handler" which ones nothing in the desktop could reach; the web access position (a `CheckAccess(n)`
  gate or a decision recorded in `AccessRight.cs`) is what decides who sees an item here, not this flag.

## Summary by top menu

| Top menu | Items | Required (Y) | Required and built | Required, not built | Hidden in the desktop |
|---|---:|---:|---:|---:|---:|
| File | 6 | 3 | 0 | 2 | 2 |
| Membership | 28 | 13 | 13 | 0 | 23 |
| Contribution Billing | 12 | 5 | 4 | 1 | 9 |
| Case | 36 | 12 | 12 | 0 | 29 |
| Invoice | 9 | 5 | 5 | 0 | 8 |
| Worksheet | 9 | 6 | 6 | 0 | 5 |
| Authorize Claims | 18 | 2 | 1 | 1 | 14 |
| Claim Bordx | 34 | 23 | 17 | 5 | 21 |
| Finance | 11 | 5 | 4 | 0 | 2 |
| Reports | 4 | 0 | 0 | 0 | 0 |
| Statistic | 2 | 0 | 0 | 0 | 1 |
| Payment | 27 | 15 | 3 | 11 | 4 |
| Receipt | 6 | 5 | 4 | 0 | 1 |
| Posting | 4 | 3 | 1 | 2 | 1 |
| Maintenance | 24 | 16 | 14 | 2 | 13 |
| View | 2 | 0 | 0 | 0 | 1 |
| Window | 4 | 0 | 0 | 0 | 0 |
| **Total** | **236** | **113** | **84** | **24** | **134** |

"Required (Y)" counts the five parent menus the business marked (File, Claim Bordx, Finance, Payment, Receipt); "built" and "not built" count the 108 items below them, 84 and 24. A built item has a web route and an API and is tested; "built" does not mean every detail of its form is ported (the gaps are in [remaining-work.md](remaining-work.md)). The figures were recounted on 2026-10-29 from the Required column of `VB6-MenuItems.xlsx` against the web routes, because the table before it still counted Membership and Maintenance as not built after the work from `main` was merged.

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
| Invoice > Invoice Processing | `mniInvoiceProcess` | frmInvoice | `/invoice/processing` | 22 | `/api/invoice` |
| Invoice > Original Invoice Update | `mniOriginalInvUpd` | frmUpdateInvoice (no click handler; never opened by the desktop code) | `/invoice/original-update` | 22 * | `/api/invoice/original` |
| Invoice > Invoice MBM Accounting | `mnuInvMBMAcc` | FrmInvoiceMBMAC (no click handler; never opened) | `/invoice/member-accounting` | 27 * | `/api/invoice/member-accounting` |
| Invoice > Invoice HOS Accounting | `mnuInvHOSAcc` | FrmInvoiceHOSAC (no click handler; never opened) | `/invoice/hospital-accounting` | 27 * | `/api/invoice/hospital-accounting` |
| Invoice > Invoice Tracking | `mnuInvTrack` | FrmInvoiceTracking2 | `/invoice/tracking` | 27 | `/api/invoice/tracking` |
| Worksheet > Worksheet Send to A/C | `mnuWSToAC` | FrmWksSendToAC | `/worksheet/send-to-accounts` | 24 * | `/api/worksheet/send-to-accounts` |
| Worksheet > Worksheet Send to A/R | `mnuWSReturnCLM` | FrmARRecFrCLM (reduced: only the CASDRC / CASRecBy / CASRecStatus columns exist) | `/worksheet/send-to-ar` | 24 * | `/api/worksheet/send-to-ar` |
| Worksheet > Worksheet Tracking | `mnuWksTracking` | FrmWorksheetTracking | `/worksheet/tracking` | 24 * | `/api/worksheet/tracking` |
| Finance > Receipt Enquiry | `mnuRcpEnq` | frmReceiptEnquiry (source missing; rebuilt from the receipt tables) | `/finance/receipt-enquiry` | 9 † | `/api/finance/receipt-enquiry` |
| Finance > Invoice Receipt (Outstanding) | `Invoice` | FrmRecMCO (source stale: rebuilt on ReceiptSummary) | `/finance/invoice-receipt` | 9 | `/api/finance/invoice-receipt` |
| Finance > Short Payment Receipt | `mnuInvRecAdj` | frmReceiptAdjustment (source missing; rebuilt) | `/finance/short-payment` | 9 | `/api/finance/invoice-receipt` |
| Finance > Reverse Receipt | `ReceiptReverse` | frmReverseReceipt (source missing; rebuilt) | `/finance/reverse-receipt` | 9 | `/api/finance/reverse-receipt` |
| Finance > Notification / Suspension | `mnuRecMCOOthMan` | frmRecMCOManual (manual receipt number; rebuilt on ReceiptSummary) | `/finance/receipt-manual` | 12 | `/api/finance/invoice-receipt` |
| Finance > Suspend / Active | `Suspend` | frmPaymentMBMReissue (source missing; rebuilt on Payment2MBM) | `/finance/suspend-active` | 12 | `/api/finance/suspend-active` |
| Finance > Electronic Banking | `mnuElectronicBank` | frmFileExportBankESP (source missing; configurable file layout) | `/finance/electronic-banking` | 10 | `/api/finance/electronic-banking` |
| Authorize Claims > DCU Tracking > Claim Report | `mnuReport1` | FrmDCUReport1 (no click handler; chosen by name; `mnuReport2` "Claim Report (Jup.)" is FrmDCUReport2, the same form, not Required) | `/authorize/dcu-report` | 16 * | `/api/authorize/dcu-report` |
| Maintenance > Company Info | `mnuComInfo` | FrmMaintenanceComInfo (no click handler, disabled in the desktop; chosen by name) | `/maintenance/company-info` | 1 * | `/api/maintenance/company-info` |
| Maintenance > Panel Hospital Maintenance | `HOSPanelMaintenance` | FrmMaintenancePanelHos (no click handler; two forms carry the caption, see [maintenance-screens.md](maintenance-screens.md)) | `/maintenance/panel-hospitals` | 1 * | `/api/maintenance/panel-hospitals` |
| Receipt > Others | `mnuOthers` | FrmReceiptHOS (no click handler; chosen by caption) | `/receipt/others` | 9 * | `/api/receipt/others` |
| Payment > Contra Allocation | `ContraAllocation` | frmContraAllocation (no click handler; chosen by name) | `/payment/contra-allocation` | 12 * | `/api/payment/contra-allocation` |
| Payment > Enquiry > Payment To Hospital | `PaymentToHospital` | FrmEnquiryPaymentHospital (no click handler; chosen by name) | `/payment/enquiry-hospital` | 12 * | `/api/payment/enquiry-hospital` |
| Payment > Enquiry > Payment To Member | `PaymentToMember` | FrmEnquiryPayMember (no click handler; chosen by name) | `/payment/enquiry-member` | 12 * | `/api/payment/enquiry-member` |
| Posting > Invoice > Hospital | `mnuPostInvHosp` | frmPostingInvoiceHosp (no click handler; chosen by name) | `/posting/invoice-hospital` | 13 * | `/api/posting/invoice-hospital` |
| Finance > SUN Posting | `mnuInvoicePositve` | frmPostingInvoiceMBM | `/finance/sun-posting` | 13 | `/api/finance/sun-posting` |
| Claim Bordx > Payment Advice > Hospital Report | `mnuPayAdviceHosRpt` | frmPayAdviceHosRpt (no click handler; chosen by name) | `/claim-bordx/hospital-report` | 15 * | `/api/claim-bordx/hospital-report` |
| Claim Bordx > Payment Advice > Generate Bordx No | `mnuPayAdviceBordx` | frmPayAdviceHosp (no click handler; chosen by name) | `/claim-bordx/payment-advice-bordx` | 15 * | `/api/claim-bordx/payment-advice-bordx` |
| Claim Bordx > Print Bordx | `mnuPrintBordx` | FrmPrintBordx | `/claim-bordx/print-bordx` | 17 | `/api/claim-bordx/print-bordx` |
| Claim Bordx > Update Bordx | `UpdateBordx` | frmUpdateWS2Bordx (no click handler; chosen by name) | `/claim-bordx/update-bordx` | 15 * | `/api/claim-bordx/update-bordx` |
| Contribution Billing > Generate Invoice | `mnuMCOInvoice` | frmMCOFeesListingNew | `/contribution-billing/generate-invoice` | 8 | `/api/contribution-billing/generate-invoice` |
| Contribution Billing > MCO Reinstatement | `mnuMCOReinstatement` | frmMCOInvoiceReinstatement (no click handler; chosen by name) | `/contribution-billing/mco-reinstatement` | 8 * | `/api/contribution-billing/mco-reinstatement` |
| Contribution Billing > Refund Listing | `mnuMCORefundListing` | frmMCORefundListing2 | `/contribution-billing/refund-listing` | 21 | `/api/contribution-billing/refund-listing` |
| Claim Bordx > Storage > Claim files | `StorageEntry` | frmStorageEntry (no click handler; chosen by name) | `/claim-bordx/storage-claim-files` | 15 * | `/api/claim-bordx/storage-entry` |
| Receipt > From Member | `mnuReceiptMember` | FrmReceiptMember (no click handler; chosen by name) | `/receipt/from-member` | 9 * | `/api/receipt/from-member` |
| Receipt > Payor | `mnuReceiptPayor` | FrmReceiptPayor (no click handler; chosen by name; `FrmReceiptPayorManual` is the Payor (Manual) item, not Required) | `/receipt/from-payor` | 9 * | `/api/receipt/from-payor` |
| Receipt > Receipt Tracking | `mnuReceiptTracking` | FrmReceiptTracking (no click handler; chosen by name) | `/receipt/tracking` | 9 * | `/api/receipt/tracking` |
| Claim Bordx > Print Recovery | `mnuPrintRecovery` | frmGenerateRecovery (no click handler; chosen by name) | `/claim-bordx/print-recovery` | 15 * | `/api/claim-bordx/recovery` |
| Claim Bordx > Monitoring > Outstanding Bordx | `mnuOSBordx` | FrmOSBordx (no click handler; chosen by name) | `/claim-bordx/outstanding-bordx` | 15 * | `/api/claim-bordx/outstanding` |
| Claim Bordx > Monitoring > Bordx Control | `mnuBordxCtrl` | FrmBordxControl (no click handler; chosen by name) | `/claim-bordx/bordx-control` | 15 * | `/api/claim-bordx/control` |
| Claim Bordx > Storage > Document | `StoDocument` | frmStorageDoc (no click handler; chosen by name) | `/claim-bordx/storage-document` | 15 * | `/api/claim-bordx/storage-documents` |
| Claim Bordx > Storage > Enquiry | `StorageEnquiry` | frmStorageEnq (no click handler; chosen by name) | `/claim-bordx/storage-enquiry` | 15 * | `/api/claim-bordx/storage` |
| Claim Bordx > Bor Delivery > Returned Fr Payor (Not App) | `mnuFrPayorCase` | FrmReturnPayorCase (no click handler) | `/claim-bordx/returned-not-approved` | 15 * | `/api/claim-bordx/return-case` |
| Claim Bordx > Worksheet > Remove Worksheet | `mnuBordxRevise` | FrmBordxRevise (no click handler; chosen by name) | `/claim-bordx/remove-worksheet` | 15 * | `/api/claim-bordx/revise` |
| Claim Bordx > Worksheet > Insert Worksheet | `mnuGenerateBordx` | FrmWksBordx (the form Case > Inpatient Case Listing opens) | `/claim-bordx/insert-worksheet` | 15 | `/api/case/listing` |
| Claim Bordx > Bor Delivery > Send to Payor | `mnuToPayor` | FrmSendtoPayor (no click handler) | `/claim-bordx/send-to-payor` | 15 * | `/api/claim-bordx/delivery/to-payor` |
| Claim Bordx > Bor Delivery > Returned From Payor | `RetfrPay` | FrmReturnPayor (no click handler; chosen by name) | `/claim-bordx/returned-from-payor` | 15 * | `/api/claim-bordx/delivery/from-payor` |
| Claim Bordx > Bor Delivery > Send to A/C | `mnuSentAC` | FrmSendtoAC (no click handler) | `/claim-bordx/send-to-accounts` | 15 * | `/api/claim-bordx/delivery/to-accounts` |
| Claim Bordx > Bordx Approval | `mnuApproval` | FrmBordxApproval (no click handler) | `/claim-bordx/approval` | 15 * | `/api/claim-bordx/delivery/approval` |
| Maintenance > Member | `MaintenanceOthers` | frmMaintenanceMembers (no click handler; chosen by name) | `/maintenance/member` | 1 * | `/api/maintenance/member-dates` |
| Maintenance > Suspension | `Suspension` | frmSuspensionDetail | `/maintenance/suspension` | 27 | `/api/maintenance/suspension` |
| Maintenance > Plan | `MaintenancePlan` | Plan maintenance forms | `/maintenance/plan` (tabs: Plan, Annual Limit, Premium, code lists, MCO fee, special note) | 1 | `/api/maintenance/{plans, annual-limits, premiums, code-lists, mco-fees, mgmt-fees, special-notes, benefit-limits}` |
| Maintenance > Benefits | `mnuBenefits` | Benefits maintenance forms | `/maintenance/benefits` | 1 | `/api/maintenance/benefits` |

† `mnuRcpEnq_Click` opens the form without an access check; the web screen is gated by position 9 like the other receipt screens.

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

## Invoice module — every menu item

| # | Menu path | Control | Access pos | Opens (VB6) | Status | Web route |
|---:|---|---|---:|---|---|---|
| 1 | Invoice | `MnuInvoices` |  |  | Not required (hidden in the desktop) |  |
| 2 | Invoice > Invoice Processing | `mniInvoiceProcess` | 22 | frmInvoice | Migrated | `/invoice/processing` |
| 3 | Invoice > Original Invoice Update | `mniOriginalInvUpd` | 22 * | frmUpdateInvoice (no click handler; never opened) | Migrated (hidden in the desktop) | `/invoice/original-update` |
| 4 | Invoice > Invoice MBM Accounting | `mnuInvMBMAcc` | 27 * | FrmInvoiceMBMAC (no click handler; never opened) | Migrated (hidden in the desktop) | `/invoice/member-accounting` |
| 5 | Invoice > Invoice HOS Accounting | `mnuInvHOSAcc` | 27 * | FrmInvoiceHOSAC (no click handler; never opened) | Migrated (hidden in the desktop) | `/invoice/hospital-accounting` |
| 6 | Invoice > Invoice HOS (DR to MBM) | `InvoiceHOSDebittoMBM` |  | frmHOSInvD2MBM (never opened) | Not required (hidden in the desktop) |  |
| 7 | Invoice > Invoice HOS (CR to MBM) | `InvoiceHOSCRtoMBM` |  | frmHOSInvC2MBM (never opened) | Not required (hidden in the desktop) |  |
| 8 | Invoice > Invoice Tracking | `mnuInvTrack` | 27 | FrmInvoiceTracking2 | Migrated (hidden in the desktop) | `/invoice/tracking` |
| 9 | Invoice > Invoice Tracking (Jupiter) | `mnuInvTrackNep` |  | FrmInvoiceTrackNep | Not required (hidden in the desktop) |  |

\* No click handler in the desktop: gated like the closest sibling (see [invoice-screens.md](invoice-screens.md)).

## Worksheet module — every menu item

| # | Menu path | Control | Access pos | Opens (VB6) | Status | Web route |
|---:|---|---|---:|---|---|---|
| 1 | Worksheet | `Claim` |  |  | Not required (hidden in the desktop) |  |
| 2 | Worksheet > Worksheet Registration | `mnuNewWorksheet` |  | frmWorksheetRegGST2015 (newest registration form in the project; no click handler) | Required, **migrated for the ordinary worksheet**: the Register tab (form and save) and the Recalculate tab; it refuses plans with a limit for each disability, linked cases, top-up, lifetime, take-over and kind D, and issues no debit or credit note number yet |  |
| 3 | Worksheet > WorkSheet Adjustment | `mnuWSAdjust` | 23 |  | Migrated | `/worksheet/adjustment` |
| 4 | Worksheet > Worksheet Enquiry | `mnuWSEnq` | 13 | frmWorksheetEnq | Migrated | `/worksheet/enquiry` |
| 5 | Worksheet > Generate Credit/Debit Note | `mnuCreditDebitNote` |  |  | Not required (hidden in the desktop) |  |
| 6 | Worksheet > Worksheet Send to A/C | `mnuWSToAC` | 24 | FrmWksSendToAC (never opened by the desktop code) | Migrated (hidden in the desktop) | `/worksheet/send-to-accounts` |
| 7 | Worksheet > Worksheet Send to A/R | `mnuWSReturnCLM` | 24 | FrmARRecFrCLM (never opened by the desktop code) | Migrated (hidden in the desktop) | `/worksheet/send-to-ar` |
| 8 | Worksheet > Worksheet Tracking | `mnuWksTracking` | 24 | FrmWorksheetTracking (scan code LOW003; no click handler) | Migrated | `/worksheet/tracking` |
| 9 | Worksheet > Worksheet Tracking (Jupiter) | `mnuWksTracking2` |  |  | Not required (hidden in the desktop) |  |

## Claim Bordx module — every menu item

| # | Menu path | Control | Access pos | Opens (VB6) | Status | Web route |
|---:|---|---|---:|---|---|---|
| 1 | Claim Bordx | `mnuBordx` |  |  | **Required, not built** (hidden in the original) |  |
| 2 | Claim Bordx > Worksheet | `mnuWSSelection` |  |  | **Required, not built** |  |
| 3 | Claim Bordx > Worksheet > Insert Worksheet | `mnuGenerateBordx` | 15 | FrmWksBordx | Migrated | /claim-bordx/insert-worksheet |
| 4 | Claim Bordx > Worksheet > Remove Worksheet | `mnuBordxRevise` |  | FrmBordxRevise | Migrated | /claim-bordx/remove-worksheet |
| 5 | Claim Bordx > Print Bordx | `mnuPrintBordx` | 17 | FrmPrintBordx | Migrated | /claim-bordx/print-bordx |
| 6 | Claim Bordx > Print Recovery | `mnuPrintRecovery` |  | frmGenerateRecovery | Migrated (hidden in the original) | /claim-bordx/print-recovery |
| 7 | Claim Bordx > Payment Advice | `mnuPaymentAdvice` |  |  | **Required, not built** (hidden in the original) |  |
| 8 | Claim Bordx > Payment Advice > Generate Bordx No | `mnuPayAdviceBordx` |  | frmPayAdviceHosp | Migrated | /claim-bordx/payment-advice-bordx |
| 9 | Claim Bordx > Payment Advice > Hospital Report | `mnuPayAdviceHosp` |  |  | **Required, not built** (hidden in the original) |  |
| 10 | Claim Bordx > Payment Advice > Generate Bordx No - AXA Affin | `mnuGenerateBordxNoAXA` |  |  | Not required (hidden in the original) |  |
| 11 | Claim Bordx > Payment Advice > Generate Bordx - AXA Affin | `mnuPayAdviceHospAXA` |  |  | Not required (hidden in the original) |  |
| 12 | Claim Bordx > Upload Approval/Sent Date | `mnuUploadApproval` |  |  | Not required (hidden in the original) |  |
| 13 | Claim Bordx > Update Bordx | `UpdateBordx` |  | frmUpdateWS2Bordx | Migrated (hidden in the original) | /claim-bordx/update-bordx |
| 14 | Claim Bordx > Bor Delivery | `mnuBordxDelivery` |  |  | **Required, not built** (hidden in the original) |  |
| 15 | Claim Bordx > Bor Delivery > Bordx Send Date | `mnuBordxSendDate` |  |  | **Required, not built** |  |
| 16 | Claim Bordx > Bor Delivery > Send to Payor | `mnuToPayor` |  | FrmSendtoPayor | Migrated (hidden in the original) | /claim-bordx/send-to-payor |
| 17 | Claim Bordx > Bor Delivery > Returned From Payor | `RetfrPay` |  | FrmReturnPayor | Migrated (hidden in the original) | /claim-bordx/returned-from-payor |
| 18 | Claim Bordx > Bor Delivery > Returned Fr Payor(Not App) | `mnuFrPayorCase` |  | FrmReturnPayorCase | Migrated (hidden in the original) | /claim-bordx/returned-not-approved |
| 19 | Claim Bordx > Bor Delivery > Send to MNRB | `mnuToMNRB` |  |  | Not required (hidden in the original) |  |
| 20 | Claim Bordx > Bor Delivery > Send to A/C | `mnuSentAC` |  | FrmSendtoAC | Migrated (hidden in the original) | /claim-bordx/send-to-accounts |
| 21 | Claim Bordx > Bor Acc Period | `BorAccPeriod` |  |  | Not required (hidden in the original) |  |
| 22 | Claim Bordx > Monitoring | `mnsBordxTrack` |  |  | **Required, not built** (hidden in the original) |  |
| 23 | Claim Bordx > Monitoring > Bordx Control | `mnuBordxCtrl` |  | FrmBordxControl | Migrated (hidden in the original) | /claim-bordx/bordx-control |
| 24 | Claim Bordx > Monitoring > Outstanding Bordx | `mnuOSBordx` |  | FrmOSBordx | Migrated | /claim-bordx/outstanding-bordx |
| 25 | Claim Bordx > Monitoring > Bordx & Worksheet | `mnuBordxTracking` |  |  | Not required (hidden in the original) |  |
| 26 | Claim Bordx > Bordx Approval | `mnuApproval` |  | FrmBordxApproval | Migrated (hidden in the original) | /claim-bordx/approval |
| 27 | Claim Bordx > Bor Tracking | `BordxTracking` |  |  | Not required (hidden in the original) |  |
| 28 | Claim Bordx > Storage | `storage` |  |  | Not required (hidden in the original) |  |
| 29 | Claim Bordx > Storage > Claim files | `StorageEntry` |  | frmStorageEntry | Migrated | /claim-bordx/storage-claim-files |
| 30 | Claim Bordx > Storage > Enquiry | `StorageEnquiry` |  | frmStorageEnq | Migrated | /claim-bordx/storage-enquiry |
| 31 | Claim Bordx > Storage > Repudiated Case | `StoRepudiateCase` |  |  | Not required |  |
| 32 | Claim Bordx > Storage > Repudiated Enquiry | `StoRepEnq` |  |  | Not required |  |
| 33 | Claim Bordx > Storage > Document | `StoDocument` |  | frmStorageDoc | Migrated | /claim-bordx/storage-document |
| 34 | Claim Bordx > Storage > Monthly DN Report | `mnuDnRpt` |  |  | Not required |  |

The Bor Delivery, Monitoring, Approval and Storage controls have no click handler in the desktop; they are gated by position 15 on the web ([claim-bordx-screens.md](claim-bordx-screens.md)).

## Backlog — items marked Required that are not built yet

24 items (recounted 2026-10-30). Which form each opens, how big it is, which have no source and which need a decision from the business are in [remaining-work.md](remaining-work.md).

**File** (2): Logout (covered by the top bar's Log out button); Close Connection

**Contribution Billing** (1): Generate Invoice (Endorsement). Generate Invoice, MCO Reinstatement and Refund Listing are built ([contribution-billing-screens.md](contribution-billing-screens.md)) (Receipt MCO/Others is the Finance > Invoice Receipt (Outstanding) form, built)

**Authorize Claims** (1): Claim Pass to A/C. DCU Tracking > Claim Report is built ([authorize-screens.md](authorize-screens.md)).

**Claim Bordx** (5, four of them parent menus): Worksheet; Payment Advice; Bor Delivery; Bor Delivery > Bordx Send Date; Monitoring; Storage > Claim files

**Payment** (11 after Contra Allocation and the two Enquiry screens; Update Invoice and Chq. Reissue are covered; [payment-screens.md](payment-screens.md); was 14): To Hospital; To Member; To Member > Pay2MBM(Auto); To Member > Pay2MBM; To Member > Chq. Cancellation; To Member > Chq. Reissue; Payor; Others; Contra Allocation; Update Invoice; Payment Tracking; Enquiry > Payment To Hospital; Enquiry > Payment To Member; PV Update (Manual)

**Receipt** (0): Others is built too ([receipt-screens.md](receipt-screens.md)).

**Posting** (2): Invoice (a parent menu); Invoice > Member (Finance > SUN Posting is this form, built). Invoice > Hospital is built ([posting-screens.md](posting-screens.md)).

**Maintenance** (2): Surgical Schedule; Security (its three children are built). Panel Hospital Maintenance and Company Info are built too. Member and Suspension are built ([maintenance-screens.md](maintenance-screens.md)).

## Notes

- Forms reached from a screen rather than from the menu (letters, GL menu, document dialogs, member lists, the case list pop-up, code search dialogs) are migrated with that screen as dialogs/tabs; they have no menu item of their own. See the per-module migration documents.
- 17 of the 80 `*_Click` handlers in `mdiMain.frm` have no matching menu control (renamed/removed in the designer): they are dead code and are not migrated (names in the [redundancy report](redundancy-removal-report.md)).
- Worksheet screens: the form chosen for each item, the access positions and every difference from the desktop are in [worksheet-screens.md](worksheet-screens.md).
- Finance screens: five of the eight have no VB6 source and one has stale source; how each was rebuilt from the production data, the
  assumptions and the open questions for the Finance users are in [finance-screens.md](finance-screens.md).
- Regenerate the spreadsheet with `node tools/extract-menus.js` (it reads `mdiMain.frm` from the `D:` source of record, or from the folder given as its first argument or in `VB6_SOURCE`, and writes `VB6-MenuItems.csv` and `.xls` to the
  current folder, not into the VB6 source); this document was generated from the sheet plus the Required column and has been edited by hand since (the Invoice module table, the backlog, the notes).
