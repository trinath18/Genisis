# Invoice Processing (`frmInvoice`) and Worksheet Registration: analysis and plan

Why this page exists: Worksheet Registration cannot be completed on its own. It works on the invoices of a case, and the screen that creates them is a separate, large form. This is what the
VB6 code does (read from `frmInvoice.frm`, `frmInvoiceGST.frm`, `frmWorksheetRegGST2015.frm`, `frmWorksheetAdj2013.frm` and `mdiMain.frm`), what the databases show, what is reusable as it
stands, and the order of work. Everything under "Measured" was run against the development databases on `MYDELL`.

## 1. How an invoice reaches a worksheet

| Step | Where | Notes |
|---|---|---|
| Invoice entry | `frmInvoice` (10,933 lines; the GST twin `frmInvoiceGST` has 12,563) | Menu **Invoice > Invoice Processing** (access position 22, marked **Required**) and **Case > Invoice Inquiry** (position 8, not required) open the same form. |
| Listing | `frmWorksheetRegGST2015` `lstInvoice` | Lists the case's invoices with their `INVWSed` flag; a double click opens `frmInvoice` on the case. Registration itself never inserts into `ClaimInvoice`. |
| Linking | Registration's save (lines 14445 to 14462) | `update ClaimInvoice set INVWSed = 1 …` for the case's invoices (with `INVBordxRefNo = 'VOID'` and `INVBordxed = 1` for the void case). |
| Worksheet | Registration / Adjustment | Calculates and saves the worksheet tables (`Claim`, `ClaimLimit`, `ClaimActual`, `ClaimApprove`, `ClaimHos`, `ClaimMember`, `ClaimPayMBMStatus`, …). |

## 2. What `frmInvoice` does when an invoice is saved

The form lists the case's invoices (`lstInvoice`, 87 columns per invoice) and has Add, Edit, Update, Remove and Clear. **Remove** is refused for an invoice that a worksheet
has used: "Selected Invoice cannot be deleted! It has been used in Worksheet!". **Update** (`cmdUpdate_Click` → `SaveClaim`) checks, in this order, and stops at the first that fails:

1. no case, or no member name: "Please select a record before proceeding! "
2. no discharge date: "Please Enter Date of Discharge! "
3. no pay type (member or hospital): "Please select who to be paid (Member / Hospital )! "
4. no stay type: "Please select a Stay Type! " (a second, unreachable "Please select a Pay Type! " follows it in the code)
5. no payee: "Please key in the person/hospital to be paid! "
6. no invoice number: "Please key in Invoice No.! "
7. no invoice date: "Please key in Invoice date! "; not a date: "Please key in correct Invoice date! "
8. paid to the member and no receipt date: "Please key in Invoice Receipt date! "; no receipt mode: "Please key in Invoice Receipt Mode! "
9. paid to the hospital and no excess amount: "Please key in Excess Amount! "
10. no initial invoice amount: "Please key in Initial Invoice Amount! "
11. a generic provider name (dental clinic, dialysis centre, foreign hospital, general practitioner, government hospital and others) without the name typed: "Please Enter Hospital Name or Clinic Name!"
12. paid to a hospital other than the case's: asks "Different Hospital. Do you want to Proceed? " (No stops)
13. a new invoice whose number the case already has: "Current Invoice No has been used before! PLease re-verify the invoice."

Then, in **one transaction** on the worksheet database: `getTotal`, `SaveInvoice`, `saveActual`, `saveActualOthers`, `saveNotCover`, `SaveINVRemarks`, and when the case has a worksheet
(`lblWs`) `saveLimit` and `SaveClaimDate` (`Claim.CLMLastDocDate`), then `SaveCASOthers`. For payor AX it reminds "Please ensure hospital discount is given!" when the hospital's `HOSDiscInd` is Y.

### Stored procedures that already exist (parameterized, so they can be called as they stand)

All are in `HISWorkSheet`; the form builds the call as text, the port will pass parameters.

| Table written | Procedure (parameters) |
|---|---|
| `ClaimInvoice` | `Insert_ClaimInvoice_2013` (24) / `_WODIR` / `_WOFolDate` / `_WO2013` for a new invoice, depending on which of the follow-up and receipt dates are given; `Update_ClaimInvoice2013` (20) and its three `_WO…2013` variants for an edit |
| `ClaimInvoiceActual` | `Insert_ClaimInvActual2010` (47) / `Update_ClaimInvActual2010` |
| `ClaimInvoiceActualOthers` | `Insert_ClaimInvoiceActOthers_New` (41) / `Update_ClaimInvoiceActOthers_New` |
| `ClaimInvoiceNotCover` | `Insert_ClaimInvoiceNotCov08` (11) / `Update_ClaimInvoiceNotCov08` |
| `CASOthers`, `CASOthersTwo` (HISDB) | `Case_AdjInvCASOthers`, `Case_AdjInvCASOthersTwo` |

The positional arguments of those calls are the form's variables (`A1`, `A1B`, `A2`, …, `txtGrossRB`, `txtRBTax`, …). About 40 of the `A…` variables are sums of finer fields computed in
the form's change handlers (for example `A5` pre-hospital = `txtXray` + `txtPreLab` + `txtPreOthers`), so the port needs those formulas as well as the procedures.

### `getTotal`: the invoice amount

`getTotal` adds every control whose name starts with `A` and is at most four characters long (the aggregates `A1`, `A1B`, `A2`, … `ALB`, `ART`, `AIT`, `ALT`) and takes off
`A1 + A2 + ART + AIT + ALT + A17` (amounts that are already inside other aggregates: the room, ICU and lodger lines also appear with their tax; what `A17` stands for is read when this is ported). **Measured:** over 6,000 saved invoices the invoice amount equals the
sum of the 29 grid rows the worksheet engine takes from the invoice, to the cent, for 5,835 (97.3 per cent); 303 invoices have an amount of zero, and the rest are invoices entered without a breakdown.

## 3. What Registration's save writes (`frmWorksheetRegGST2015` and `frmWorksheetAdj2013`, same routines)

| Routine | Lines (Adjustment form) | Writes |
|---|---|---|
| `SaveClaim("A")` | 228 | the version row of `Claim` (141 columns) |
| `SaveClaimRemarks` | 70 | remarks |
| `saveLimit`, `saveActual`, `saveApprove`, `saveHos` | 148, 83, 80, 79 | `ClaimLimit`, `ClaimActual`, `ClaimApprove`, `ClaimHos` (per-row figures; the calculation engine produces them) |
| `saveMember`, `savePayMBMStatus` | 339, 935 | `ClaimMember`, `ClaimPayMBMStatus` |
| `SameLimitUndExcess`, `saveExcessLetter` | 14, 512 | `ClaimSameLimit`, `CAS_Excess` |
| `UpdateInvoice`, `UpdateCasReserve` | 15, 21 | invoice and case reserve |
| `UpdateMBMBalance`, `UpdateMBMBnfBal`, `UpdateMBMLifeTimeBal`, `UpdateCASTakeOverLmt`, `UpdateClaimTopup` | 606, 66, 61, 28, 15 | the member's available limit and benefit balances |
| `HCreditDebit`, `tblRecord` update | 28 | credit / debit marks; `IsWSdone`, `CaseProgress` = GL Issued |

Approve and Registration share these; both change the member's available limit, which other worksheets and payments read. What each routine writes is taken from its name, its size and its first lines;
the exact columns are read when that routine is ported.

## 4. What the data shows

* Every invoice in the development `HISWorkSheet` belongs to a saved worksheet; only 170 of 39,684 are not yet marked `INVWSed`.
* **`HISWorkSheet` is heavily trimmed:** 16,210 cases have a worksheet, against 683,012 discharged cases in the full `HISDB` (`HISDB_actual_backup`). So the 79 per cent the engine reaches was
  measured on about 2 per cent of the worksheets, and there is almost nothing to test registering a new worksheet against.

## 5. Order of work, and how each step is checked

1. **Invoice rules**: `getTotal` and the ~40 aggregates as pure code, checked by the oracle above on every saved invoice (the aggregate fields reproduce the saved `ClaimInvoiceActual` columns).
2. **Invoice Processing, read side**: the case's invoices with every field, over `View_CAS_Invoice`.
3. **Invoice Processing, write side**: the 13 checks in order, then the procedures above in the form's order inside one transaction; checked by saving a saved invoice's own values again (in a
   rolled-back transaction) and comparing every column, then by the same on invoices of the ZZ test case.
4. **Registration save**, routine by routine in the order of section 3: regenerate the saved rows of a version from the engine and compare them with the saved ones column by column, before any of
   it writes to a real database. Registration's grid must let the user change a limit or an approved amount, as the desktop does, because the engine alone reproduces about 79 per cent.
5. **Approve** reuses 4.

## 6. Data that would change how much can be checked

* **A full `HISWorkSheet` backup**, restored beside the current one (as `HISDB_actual_backup` was) so that rows can be copied across: about 40 times more saved worksheets and invoices to
  check the rules and the engine against, and real unregistered invoices (cases that have an invoice but no worksheet) to register in a rolled-back transaction.
* **A full `HISMaintenance` backup**, for the same reason on the per-case tables Registration writes (`tblRecord`, `CASLG`, `CASCrossReference`).
