# Remaining work: Required menu items not built

Source: the **Required** column of `VB6-MenuItems.xlsx` (113 rows marked `Y`: 108 screens and 5 parent menus) against the web routes that exist. Written 2026-10-29, after the Claim Bordx delivery screens ([claim-bordx-screens.md](claim-bordx-screens.md)).

**94 of the 108 are built, 14 are not, and no screen is left to build.** The 14 are menu parents (Worksheet, Payment Advice, Bor Delivery, Monitoring, Posting > Invoice, Security, Payment > To Member) or are covered by a screen under another menu path (Logout, Close Connection, Claim Pass to A/C, Posting > Member, Payment > Update Invoice, Payment > To Member > Chq. Reissue and Chq. Cancellation). The last four were settled on the `missing-forms` branch (2026-11-14), after the source folder was compared with `vbcode.zip` (the zip has no form the folder lacks; the folder adds only the local build set-up): Bordx Send Date is `frmUploadApproval` (built), Surgical Schedule was rebuilt on `PLNSurgOperation` (no form exists), Claim Pass to A/C opens Worksheet > Send to A/C, and Close Connection is the Log out button. The business should confirm these (below). The seven Payment screens whose forms are missing were rebuilt from the `HISPayment` data on the `payment-module` branch, with their assumptions listed for the Payment users ([payment-screens.md](payment-screens.md)); Generate Invoice (Endorsement) was rebuilt the same way on the `contribution-billing` branch ([contribution-billing-screens.md](contribution-billing-screens.md)).

## What the inventory found

1. **Only 6 of the 48 have a click handler in `mdiMain.frm`** (Logout, Close Connection, Generate Invoice, Generate Invoice (Endorsement), Refund Listing, Print Bordx). The desktop never opened the rest from the menu, so a form has to be matched to each by name and caption. Where that match is a guess it says so below, and the business should confirm it before the screen is built; building the wrong form would write the wrong columns.
2. **Most forms exist in the source tree**, but about a dozen do not (most of Payment, the Endorsement invoice listing). Like the five Finance screens with no source, those can only be rebuilt from the tables ([finance-screens.md](finance-screens.md)), and the rebuilt behaviour needs the Finance users' review.
3. **The development database lacks some tables the forms use**: `Company` (Maintenance > Company Info) is in none of the five databases, so that screen cannot be checked against data here.

## The 24, by menu

Size is the form's line count (comments and blank lines included). "Handler" = the desktop menu opens it.

### File (2)

| Item | Form | Status |
|---|---|---|
| Logout | `frmLogin` (handler) | **Covered**: the top bar's Log out button ends the session and returns to the login page |
| Close Connection | no form (`CloseConnection_Click` closes the connections, writes the Logout line of the login log and unloads every form, as Logout does) | **Covered** by the top bar's Log out button (the API holds no connection for a user) |

### Contribution Billing (none left; Generate Invoice, Generate Invoice (Endorsement), Refund Listing and MCO Reinstatement are built: [contribution-billing-screens.md](contribution-billing-screens.md); Receipt MCO/Others is covered)

| Item | Form | Size | Notes |
|---|---|---:|---|
| Generate Invoice (Endorsement) | `frmMCOFeesListingEndt` (handler, position 8) | **source missing** | **Rebuilt**: Generate Invoice by endorsement bordx date |
| Receipt MCO/Others | `FrmRecMCO` (handler, position 19) | | **Covered**: the Finance > Invoice Receipt (Outstanding) screen is this form |

### Authorize Claims (1 left, covered; DCU Tracking > Claim Report is built: [authorize-screens.md](authorize-screens.md))

| Item | Form | Size | Notes |
|---|---|---:|---|
| Claim Pass to A/C | no form has its name and `mnuClaimPass2AC` has no handler; `FrmWksSendToAC` ("Claims Send To A/C") is the form that passes claims to accounts | | **Covered**: the sidebar item opens Worksheet > Send to A/C (access position 24); business to confirm |

### Claim Bordx (4 left, all parent menus; 18 built)

Group parents with no screen of their own: Worksheet, Payment Advice, Bor Delivery, Monitoring.

| Item | Form (by name unless noted) | Size | Notes |
|---|---|---:|---|
| Bor Delivery > Bordx Send Date | `frmUploadApproval` ("Upload Send/Approval/Return", 1,278): `frmUploadBordxSentDate` is a copy of it with the code removed; `FrmBordxDelivery` is the older all-in-one form of the delivery steps, not this item | 1,278 | **Built** ([claim-bordx-screens.md](claim-bordx-screens.md)) |

### Payment (none left to build: [payment-screens.md](payment-screens.md); the top menu is a parent)

Of the ten forms the desktop's Payment menu opens, **none of eight is in the source tree**: `medixhis1.vbp` lists `FrmPaymentHospital`, `FrmPaymentPayor`, `FrmPaymentOthers`, `FrmPaymentManual`, `FrmPaymentTracking`, `FrmPaymentSubAmtPaid`, `FrmPayMember`, `frmPayMemberJul08`, `FrmPaymentMBMReverse` and `frmPaymentMBMReissue`, and the folder has none of them. They write payment vouchers, cheques and accounts, so they were rebuilt from the `HISPayment` tables and the rows they wrote (the PV numbering, cheque and words rules were inferred from the data and tested against it); the Payment users should confirm the assumptions before real payments are made.

| Item | Form | Status |
|---|---|---|
| To Hospital | `FrmPaymentHospital` | **Rebuilt** (source missing) |
| To Member (parent) | | covered by its children |
| To Member > Pay2MBM(Auto), Pay2MBM | `frmPayMemberJul08`, `FrmPayMember` | **Rebuilt** (source missing) |
| To Member > Chq. Cancellation | `FrmPaymentMBMReverse` | **Covered**: its tables have no row; a cancelled cheque is the C row Finance > Suspend / Active writes |
| To Member > Chq. Reissue | `frmPaymentMBMReissue` | **Covered**: Finance > Suspend / Active (rebuilt on Payment2MBM) is this form |
| Payor, Others | `FrmPaymentPayor`, `FrmPaymentOthers` | **Rebuilt** (source missing) |
| Update Invoice | `frmUpdateInvoice` ("Update Invoice") | **Covered**: Invoice > Original Invoice Update is this form |
| Payment Tracking | `FrmPaymentTracking` | **Rebuilt** (source missing) |
| PV Update (Manual) | `FrmPaymentManual` (by its views `VIEW_Payment2HOS_Manual` / `VIEW_Payment2MBM_Manual`) | **Rebuilt** (source missing) |

### Receipt (all built; Others is built: [receipt-screens.md](receipt-screens.md); the top menu is a parent)

### Posting (2 left, both covered; Invoice > Hospital is built: [posting-screens.md](posting-screens.md))

| Item | Form | Size | Notes |
|---|---|---:|---|
| Invoice (parent), Invoice > Member | `frmPostingInvoiceMBM` | 1,851 | **Covered**: Finance > SUN Posting is this form (handlers `mnuInvoicePositve` and `mnuSUNPosting`); the Posting menu entry is another door to it |

### Maintenance (1 left, a parent menu; Member, Suspension, Company Info, Panel Hospital Maintenance and Surgical Schedule are built: [maintenance-screens.md](maintenance-screens.md))

| Item | Form | Notes |
|---|---|---|
| Surgical Schedule | no form in the folder or the zip (`mnuSurgicalOperation`; `frmMMASchedule` "MMA Schedule of Fees" is Account Transaction); its table is `PLNSurgOperation`, the plan's schedule Case Registration lists | **Rebuilt** on that table (assumptions in [maintenance-screens.md](maintenance-screens.md)) |
| Security (parent) | | covered by its three children (Department, Role Management, User Permission) |

## Gaps inside screens that are built

- **Worksheet Registration** ([worksheet-screens.md](worksheet-screens.md)): refuses lifetime plans (about 5% of saved worksheets: the annual-limit rule is unclear and needs the business), plans with a limit for each disability (7.8%), linked cases (4.9%), top-up members, take-over cases and kind D limits; issues no debit or credit note number; BTBG excess-letter allowances are not read.
- **Worksheet Adjustment**: Approve (re-saving an amended worksheet), e-mail, cheque letter and OMNI update are not built.
- **Calculation engine**: about 73% of total approved and 82% of plan limits match the saved worksheets; Ex-Gratia co-insurance and top-up are not ported.
- **Case**: the mobile push (SOAP), Policy Wording file opening, Excel export buttons and the CPT / Inv Info windows are deferred ([case-registration-migration.md](case-registration-migration.md)).

## Needs the business

1. The forms chosen for the items without a handler: Bordx Send Date is `frmUploadApproval` (`frmUploadBordxSentDate` is its copy without code), Claim Pass to A/C is Worksheet > Send to A/C (`FrmWksSendToAC`), and Surgical Schedule is rebuilt on `PLNSurgOperation` with the assumptions in [maintenance-screens.md](maintenance-screens.md); and whether the older `frmPanelHospMaintenance` (payor, plan and hospital) is still used.
2. The Payment users to confirm the assumptions of the seven rebuilt Payment screens ([payment-screens.md](payment-screens.md)); the Contribution Billing users to confirm those of Generate Invoice (Endorsement) ([contribution-billing-screens.md](contribution-billing-screens.md)).
3. Positions: 15 for the Claim Bordx items without a click handler, 12 for the Payment screens, 16 for the DCU report, 9 for Receipt Others, 1 for Company Info and Panel Hospital Maintenance (all decisions: none has a click handler).
4. That Close Connection can be the Log out button.
5. How a lifetime plan's annual limit should change, and the debit / credit note numbering rule.
6. The real `Company` table (not in the development databases): check Company Info against it.
