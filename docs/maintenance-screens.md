# Maintenance screens: Member and Suspension

Menu path: **Maintenance** (legacy `mdiMain.frm`, top menu `MaintenanceTables`). The Plan, Benefits, Entity, Accounts, Account Transaction, MMA and Security screens came from the team's `main` branch; this page covers the two built afterwards from the desktop source. What is left of the menu is in [remaining-work.md](remaining-work.md).

| Menu path | Legacy form | Web route | API | Access position |
|---|---|---|---|---:|
| Maintenance > Member | `frmMaintenanceMembers` ("Members Maintenance", tab "Valid Date Range") | `/maintenance/member` | `/api/maintenance/member-dates` | 1 * |
| Maintenance > Suspension | `frmSuspensionDetail` ("Suspension Detail") | `/maintenance/suspension` | `/api/maintenance/suspension` | 27 |
| Maintenance > Surgical Schedule | none (rebuilt on `PLNSurgOperation`, below) | `/maintenance/surgical-schedule` | `/api/maintenance/surgical-schedule` | 1 * |

\* `MaintenanceOthers` (Member) has no click handler in `mdiMain.frm`; it is gated like the other Maintenance items, position 1 (a decision, recorded in `AccessRight.cs`). Suspension is `Suspension_Click`, position 27.

## Member (Valid Date Range)

One row of `dbo.MBMValidDateRange` holds the date ranges a member's effective date, expiry date and date of birth must fall in (six dates: effective start and end, expiry start and end, DOB start and end). The page loads the row (`Record not found` when the table is empty), and **Update** checks the dates in the desktop's order (`Please Enter Effective Start Date`, `... Effective End Date`, `... Expiry Start Date`, `... Expiry End Date`, `... DOB Start Date`, `... DOB End Date`; a typed date must be a real day, `Invalid Date Format!`), asks `Do you want to save the record?` and saves (`Record Saved...`).

- **SQL** (`sql/main/430_maintenance_member_dates.sql`): `genisis.MemberDates_Get` (the first row by `ValidID`) and `genisis.MemberDates_Save` (updates the first row, or adds one when the table is empty; stamps the user and the day).
- **Kept from the desktop:** it edited the first row of the table and added a row only when the table was empty. The desktop did not check that a start is not after its end; neither does the web (the business has not asked for it).
- **Data:** the table has one row (`ValidID` 10, last changed by LAYHOON on 2016-01-15). The rolled-back database tests read it back, change it, and add a row to an emptied table, comparing a checksum of the table before and after.

## Suspension (categories and suspended members and agents)

Two tabs. **Suspension Category** keeps `dbo.SuspensionCat` (01 MEMBERSHIP, 02 AGENT in the data). **Suspension Details** keeps `dbo.SuspensionDetail`: a member (selection 01, key = member number) or an agent (02, key = agent code) with a description and a payor.

- **Search** lists the records (a category by the start of its code; a suspension by selection, member number or agent code and payor, with the payor shown as `code - name`). `Record not found` when none. Clicking a row loads it read only and enables **Edit** (the key fields lock, the description can change) and **Delete**; **Add** starts a new record; **Save** needs something added or edited; **Print** exports the list to CSV; **Clear** empties everything.
- **Category checks, in the desktop's order:** `Please Enter Code`, `Please Enter Description`, `Record Duplicated! Please Change The Corresponding Field`, `Do you want to save the record?`, `Record Saved...` / `Record Updated...`. Delete: `Please Enter Category Code`, `Please search the record`, `This record cannot be deleted because it is used in Suspension Detail table!`, `Do you want to delete this record?`, `Record Deleted... `.
- **Suspension checks, in the desktop's order:** `Please Enter Selection`, `Please Enter Member Number/Agent Code`, `Please Enter Description`, `Please Enter Payor`, `Payor Code Not Match` (for a member, the number's first two characters must be the payor's code), duplicate key, confirmation, `Record Saved...` / `Record Updated...`. Delete: `Please Enter Selection`, `Please Enter Member Number or Agent Code`, `Please search the record`, `Do you want to delete the record?`, `Record Deleted...`. Leaving the member-number box upper-cases it and fills in the payor from its first two letters (`Invalid MembershipNumber` when no payor has that code).
- **ChkStr:** codes, keys and descriptions are trimmed and upper-cased before they are sent and again on the server.
- **SQL** (`sql/main/440_maintenance_suspension.sql`): `genisis.SuspensionCat_Search / _Save / _Delete` and `genisis.SuspensionDetail_Search / _Save / _Delete`, all with typed parameters.

### Differences from the desktop

| Desktop | Web | Why |
|---|---|---|
| The category save ran under `On Error Resume Next` and wrote `CATLastUpdateUser` / `CATLastUpdateDate`, columns the table does not have (they are `LastUpdateUser` / `LastUpdateDate`), so a category was never stamped | The user and the day are stamped | The assignments failed silently; the intent was clear |
| The payor name was read one row at a time for the list; a suspension whose payor was missing from `PAY` crashed the list | One query with a left join; a missing payor shows the code alone | Robustness |
| The delete check said "Please Enter Member Number or Agent Code" when the selection was empty (it tested the selection twice) | It tests the member number or agent code | The message names the box it means |
| Text SQL (`Delete From SuspensionDetail Where MembershipNo = '...'`) | Typed parameters | Quoting and injection |
| `ExportListViewtoExcel` | CSV download | No Excel automation |
| The list columns' sort, the `m_blnDirection` flags and `lstAgentList` / `lstSuspensionList` bookkeeping | The grid sorts | Obsolete plumbing |

### Data

`SuspensionDetail` is empty in the development database and `SuspensionCat` has the two rows, so the suspension flows were checked in rolled-back database tests (add a member and an agent, find them with the payor name, change one, delete both, refuse a duplicate key and a category a suspension uses) and in the browser against faked lists.

## Tests

- API: `MemberDatesTests.cs` (11 with the architecture tests) and `SuspensionTests.cs` (15 the same way).
- UI: `memberDatesRules.test.ts` (4) and `suspensionRules.test.ts` (8).
- Browser: `e2e/maintenance-member.mjs` (14 checks) and `e2e/maintenance-suspension.mjs` (37 checks). The writes are intercepted; nothing is written.

## Code map

| Layer | Files |
|---|---|
| SQL | `sql/main/430_maintenance_member_dates.sql`, `440_maintenance_suspension.sql` (and `sql/genisis-main.sql`); they use `dbo.MBMValidDateRange`, `dbo.SuspensionCat`, `dbo.SuspensionDetail`, `dbo.PAY` of the main database |
| API | `Genisis.Api/Maintenance/MemberDates/MemberDates.cs`, `Maintenance/Suspension/Suspension.cs`, `Maintenance/MaintenanceScreensServiceCollectionExtensions.cs`, `Controllers/MaintenanceMemberDatesController.cs`, `MaintenanceSuspensionController.cs`, `Auth/AccessRight.cs` |
| UI | `GenisisUI/src/features/maintenance/memberDates/` and `suspension/` (rules, rules tests, `pages/*Screen.tsx`), the routes in `App.tsx`, the Maintenance section of `components/AppLayout.tsx`, `auth/access.ts` |

## Maintenance > Surgical Schedule (rebuilt: no form)

`mnuSurgicalOperation` has no click handler, and no form for it is in the source folder or in `vbcode.zip` (no form has "surgical" in its name or caption; `frmMMASchedule` is Account Transaction). The table the item names is `dbo.PLNSurgOperation` (payor, plan, surgical main category, sub category, percentage), which Case Registration lists on its "Surgical Schedule" tab and which nothing in the desktop writes. The screen is rebuilt on it (`sql/main/760_maintenance_surgical_schedule.sql`, `Maintenance/SurgicalSchedule/SurgicalSchedule.cs`, `Controllers/MaintenanceSurgicalScheduleController.cs`; UI `pages/maintenance/SurgicalSchedulePage.tsx` and `surgicalScheduleRules.ts`): choose a payor and type a plan code, then add, change or delete the rows of the plan's schedule.

Assumptions for the business to confirm: the categories are free text (no list of surgical categories exists in the databases); the percentage is a whole number from 0 to 100; one row per main and sub category of a plan; access position 1, like the other Maintenance items without a handler. The table has no rows in the development databases.

## Maintenance > Company Info

`sql/main/630_maintenance_company_info.sql`, `Maintenance/CompanyInfo/CompanyInfo.cs`, `pages/maintenance/CompanyInfoPage.tsx`. Legacy `FrmMaintenanceComInfo` ("Company Information"); `mnuComInfo` has no click handler and is disabled in the desktop; gated at position 1 like its siblings (a decision). The one row of `Company`: code, name, address, phone, fax, e-mail, homepage and current year. Update asks for the code, the name and the current year in that order, confirms ("Do you want to save the record?") and updates the first row, or adds it when the table is empty ("Record Saved..."); the phone and fax take what the desktop's number boxes took (digits, "-" and "."), the current year is four digits.

- **`dbo.Company` is not in any of the development databases**, so the procedures answer "The Company table is not in this database." instead of failing, and the screen shows it; the writes were tested on a copy of the table made inside a rolled-back session (the column types are those the form implies: text, and a number for the current year). Check the screen against the real table before relying on it.
- **Tests:** `CompanyInfoTests.cs`, `companyInfoRules.test.ts`, `e2e/maintenance-company-info.mjs` (the row and the PUT are intercepted).

## Maintenance > Panel Hospital Maintenance

`sql/main/640_maintenance_panel_hospitals.sql`, `Maintenance/PanelHospitals/PanelHospitalMaintenance.cs`, `pages/maintenance/PanelHospitalMaintenancePage.tsx`. `HOSPanelMaintenance` has no click handler and two forms carry its caption: `FrmMaintenancePanelHos` (a hospital panel code and its hospitals, 664 lines) and `frmPanelHospMaintenance` (payor, plan and hospital, 590 lines, the older design that `frmMBMClinics` still refers to). The first is built: it works on the panel codes Case > Panel Hospitals lists and on the same `PanelHospitals` rows. Gated at position 1 (a decision).

Choosing a panel code lists its hospitals, all ticked; unticking one and **Update** takes it out. **Add** lists the hospitals that are not in the panel; ticking and **Update** puts them in (the user and day are stamped; a hospital already in the panel is stamped again, as the desktop did). Both ask "Do you want to update the record?", change the panel in one transaction (an unknown hospital or panel code refuses the whole update) and say "Record updated !". Differences: one hospital at a time with typed parameters instead of DELETE statements built from the list rows; a hospital cannot be added and removed in one update. The other form, `frmPanelHospMaintenance`, is not migrated (see the redundancy report); ask the business whether it is still used.

- **Tests:** `PanelHospitalMaintenanceTests.cs` (validators, service, database: remove and put back, the stamps, the refusals, rollback), `panelHospitalMaintenanceRules.test.ts`, `e2e/maintenance-panel-hospitals.mjs` (the PUT is intercepted).
