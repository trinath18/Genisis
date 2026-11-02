# Contribution Billing screens

Menu path: **Contribution Billing** (legacy `mdiMain.frm`): Generate Invoice, Generate Invoice (Endorsement), MCO Reinstatement, Refund Listing and Receipt MCO/Others (the last is the Finance > Invoice Receipt (Outstanding) screen, already built). The sidebar has a Contribution Billing module (rail icon, `/contribution-billing`); the screens still to migrate are shown disabled.

| Menu path | Legacy form | Web route | API | Access position |
|---|---|---|---|---:|
| Contribution Billing > Refund Listing | `frmMCORefundListing2` ("MCO Refund Listing") | `/contribution-billing/refund-listing` | `/api/contribution-billing/refund-listing` | 21 |

## Refund Listing

Read only (`sql/main/520_contribution_refund_listing.sql`, `ContributionBilling/RefundListing/RefundListing.cs`, `RefundListingPage.tsx`). Lists the MCO fees refunded to a payor's cancelled or endorsed members, from `VIEW_MBM_Refund_Search` (the principals) and `View_MBM_Supplementary_Refund` (their dependants).

- **Criteria:** **Date Selection** (Endt. Eff Date, the endorsement effective date, or Endt. Bordx Date, the endorsement bordx date; the date boxes are named after it), **Payor**, the date range (a blank date is not applied) and **Print Status** (N - No: members without a credit note; Y - Yes: members with one). `Please Select Date Selection!` then `Please Select Payor!` as in the desktop. A payor that is not there is not found.
- **The list:** one line per principal, in member number order, with the desktop's 22 columns; the member's status follows the name ("(Cancelled)", "(Pending)"). A **family or household principal** (insurance code F or H) is followed by its supplementary members, which carry the payor and date criteria but not the print status, as in the desktop (it looked them up one principal at a time; the web reads them all at once and matches them by member number). A **Type** column says which is which.
- **Arithmetic, as the desktop coded it** (the business should confirm it is what is wanted):
  - **MCO fees Refund (Min.):** with a utilised fee at or below the minimum (50 for a family or household principal, 25 for the others and for the supplementary members), the fee less the minimum; above it, the **utilised fee itself** (not the fee less the utilised fee); 0 without a utilised fee. A missing fee counts as 0.
  - **Days Utilised:** for a payor whose `MBMCancelStatus` is Y (the screen's `MBMStatus` flag, set when the payor is chosen) a value below zero shows as 0; for the other payors the value as it is. This is the only difference between the desktop's two principal listings (`MCORefundListing` and `MCORefundListing2`, case 1 and case 2 of `MBMRefundListing`); the one filter on the cancelled status and 30 days that the first one seems to have is commented out in the source.
  - **Totals:** **Total Principal** and **Total Supp** count the lines; **Total MCO Refund** adds the principals' MCO fees refund. The desktop had a second total, "Total MCO Refund (Min.)", which added the same column and so always showed the same figure; it is not repeated. Supplementary refunds are counted, not added.
- **Print** writes the list to CSV (the desktop wrote an Excel sheet). **Records** over 5,000 principals are cut and the screen says so.
- **Not migrated: Print CR Note.** It generates a credit note number (`GenerateMCOCreditNo`), writes it to the members (`UpdateCreditNote` updates `MBMOthers` and `MBMCoveredPersonsTwo`) and prints the credit note through Excel. How credit and debit notes are numbered is an open business question (see [remaining-work.md](remaining-work.md)), so the button is left out until it is answered; the Credit Note and Credit Date columns still show the notes that were printed in the desktop.
- **Data:** the principal view has 964,608 rows (481,368 for payor DC), most with no endorsement, so a search without dates for a big payor is cut at 5,000. Payors with endorsements in the data: AX (92, all family or household), AH (78), ST (48), AY (32), WK (19), BS (17). The supplementary view has 169 rows. No credit note has been printed for them.
- **Tests:** `RefundListingTests.cs` (validator, arithmetic, service, and each criterion against direct DISTINCT counts on the views); `refundListingRules.test.ts` (5); `e2e/contribution-refund-listing.mjs` (29 checks).

## Code map

| Layer | Files |
|---|---|
| SQL | `sql/main/520_contribution_refund_listing.sql` (and `sql/genisis-main.sql`) |
| API | `Genisis.Api/ContributionBilling/RefundListing/RefundListing.cs`, `ContributionBilling/ContributionBillingServiceCollectionExtensions.cs`, `Controllers/ContributionRefundListingController.cs`, `Auth/AccessRight.cs` |
| UI | `GenisisUI/src/pages/contributionBilling/` (`refundListingRules.ts`, its test, `RefundListingPage.tsx`), the route in `App.tsx`, the Contribution Billing section and rail icon in `components/AppLayout.tsx` (new `coins` icon in `components/icons.tsx`), `auth/access.ts` |
