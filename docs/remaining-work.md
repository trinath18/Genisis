# Remaining work: Required menu items not built

Source: the **Required** column of `VB6-MenuItems.xlsx` (113 rows marked `Y`: 108 screens and 5 parent menus) against the web routes that exist. Written 2026-10-29, after the Claim Bordx delivery screens ([claim-bordx-screens.md](claim-bordx-screens.md)).

**77 of the 108 are built, 31 are not.** Nine of the 31 are menu parents (Worksheet, Payment Advice, Bor Delivery, Monitoring, Posting > Invoice, Security, Payment > To Member) or are covered by a screen under another menu path (Logout, Posting > Member), which leaves 22 screens to build or to decide about.

## What the inventory found

1. **Only 6 of the 48 have a click handler in `mdiMain.frm`** (Logout, Close Connection, Generate Invoice, Generate Invoice (Endorsement), Refund Listing, Print Bordx). The desktop never opened the rest from the menu, so a form has to be matched to each by name and caption. Where that match is a guess it says so below, and the business should confirm it before the screen is built; building the wrong form would write the wrong columns.
2. **Most forms exist in the source tree**, but about a dozen do not (most of Payment, the Endorsement invoice listing). Like the five Finance screens with no source, those can only be rebuilt from the tables ([finance-screens.md](finance-screens.md)), and the rebuilt behaviour needs the Finance users' review.
3. **The development database lacks some tables the forms use**: `Company` (Maintenance > Company Info) is in none of the five databases, so that screen cannot be checked against data here.

## The 31, by menu

Size is the form's line count (comments and blank lines included). "Handler" = the desktop menu opens it.

### File (2)

| Item | Form | Status |
|---|---|---|
| Logout | `frmLogin` (handler) | **Covered**: the top bar's Log out button ends the session and returns to the login page |
| Close Connection | no form (`CloseConnection` handler only closes the connection) | No web meaning (the API holds no connection for a user); business to confirm it can be dropped |

### Contribution Billing (1 left; Generate Invoice, Refund Listing and MCO Reinstatement are built: [contribution-billing-screens.md](contribution-billing-screens.md); Receipt MCO/Others is covered)

| Item | Form | Size | Notes |
|---|---|---:|---|
| Generate Invoice (Endorsement) | `frmMCOFeesListingEndt` (handler, position 8) | **source missing** | rebuild from tables |
| Receipt MCO/Others | `FrmRecMCO` (handler, position 19) | | **Covered**: the Finance > Invoice Receipt (Outstanding) screen is this form |

### Authorize Claims (2)

| Item | Form | Size | Notes |
|---|---|---:|---|
| DCU Tracking > Claim Report | `FrmDCUReport1` / `FrmDCUReport2` (by name; no handler) | 17,593 / 17,591 | the largest forms in the project; a reporting screen |
| Claim Pass to A/C | no match found by name | | business to name the form |

### Claim Bordx (5 left; 17 built)

Group parents with no screen of their own: Worksheet, Payment Advice, Bor Delivery, Monitoring.

| Item | Form (by name unless noted) | Size | Notes |
|---|---|---:|---|
| Bor Delivery > Bordx Send Date | **ambiguous**: `frmUploadBordxSentDate` has no code; `FrmBordxDelivery` ("Set Bordx Delivery/Account Date", 1,370) is an older all-in-one form that also sets the MNRB steps with other columns (`BordxPAYFrStatus`, `BordxPAYRecDate`) | | business to say which |

### Payment (14; the top menu is a parent)

Nearly every form of the desktop's Payment menu is **missing from the source** (there is no `FrmPaymentHospital`, `FrmPaymentMBM`, `FrmPay2MBM`, `FrmPaymentPayor`, ...). The forms that exist are posting and enquiry forms:

| Item | Form found | Size |
|---|---|---:|
| To Hospital, To Member (Pay2MBM (Auto), Pay2MBM), Payor, Others, Update Invoice, Payment Tracking, PV Update (Manual) | none by name (`frmPVSunHISFlag` "SunHISPVChecking", 663, may be PV Update) | |
| To Member > Chq. Cancellation / Chq. Reissue | `frmPaymentMBMReissue` is the Finance > Suspend / Active screen (already built); the cancellation form is missing | |
| Contra Allocation | `frmContraAllocation` | 968 |
| Enquiry > Payment To Hospital | `FrmEnquiryPaymentHospital` | 1,767 |
| Enquiry > Payment To Member | `FrmEnquiryPayMember` | 2,454 |

These can only be rebuilt from the `HISPayment` tables, as the Finance screens were. That is a design exercise with the Payment users, not a port.

### Receipt (1 left; Receipt Tracking, From Member and Payor are built: [receipt-screens.md](receipt-screens.md); the top menu is a parent)

| Item | Form | Size |
|---|---|---:|
| Others | `FrmReceiptHOS` ("Receipt (Others)") or `FrmReceiptMNRB` ("Receipt From MNRB", 4,082): ambiguous | 1,373 |

### Posting (2 left, both covered; Invoice > Hospital is built: [posting-screens.md](posting-screens.md))

| Item | Form | Size | Notes |
|---|---|---:|---|
| Invoice (parent), Invoice > Member | `frmPostingInvoiceMBM` | 1,851 | **Covered**: Finance > SUN Posting is this form (handlers `mnuInvoicePositve` and `mnuSUNPosting`); the Posting menu entry is another door to it |

### Maintenance (4 left; Member and Suspension are built: [maintenance-screens.md](maintenance-screens.md))

| Item | Form | Size | Notes |
|---|---|---:|---|
| Panel Hospital Maintenance | `frmPanelHospMaintenance` (591) or `FrmMaintenancePanelHos` (665): ambiguous | | |
| Company Info | `FrmMaintenanceComInfo` | 495 | the `Company` table is not in the development databases: the screen can be built from the form's code (one row; code, name, address, phone, fax, e-mail, homepage, current year) but not checked against data |
| Surgical Schedule | no match found by name (`frmMMASchedule` "MMA Schedule of Fees" is Account Transaction) | | business to name the form |
| Security (parent) | | | covered by its three children (Department, Role Management, User Permission) |

## Gaps inside screens that are built

- **Worksheet Registration** ([worksheet-screens.md](worksheet-screens.md)): refuses lifetime plans (about 5% of saved worksheets: the annual-limit rule is unclear and needs the business), plans with a limit for each disability (7.8%), linked cases (4.9%), top-up members, take-over cases and kind D limits; issues no debit or credit note number; BTBG excess-letter allowances are not read.
- **Worksheet Adjustment**: Approve (re-saving an amended worksheet), e-mail, cheque letter and OMNI update are not built.
- **Calculation engine**: about 73% of total approved and 82% of plan limits match the saved worksheets; Ex-Gratia co-insurance and top-up are not ported.
- **Case**: the mobile push (SOAP), Policy Wording file opening, Excel export buttons and the CPT / Inv Info windows are deferred ([case-registration-migration.md](case-registration-migration.md)).

## Needs the business

1. Which form each ambiguous item opens (Bordx Send Date, Returned From Payor (the larger `frmReturnFrPayor`?), Receipt Others, Panel Hospital Maintenance, Surgical Schedule, Claim Pass to A/C).
2. Position 15 for the Claim Bordx items without a click handler.
3. Whether Close Connection can be dropped.
4. How a lifetime plan's annual limit should change, and the debit / credit note numbering rule.
5. The Payment menu: the source of its forms is missing, so the rules have to come from its users.
