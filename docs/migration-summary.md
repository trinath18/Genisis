# Migration summary

What was migrated, what behaviour was preserved on purpose, what was improved, and what is still open. Numbers were verified on 2026-10-17 against the development databases on `MYDELL`.

## 1. Scope and status

| | |
|---|---|
| Source | VB6 desktop application (`mdiMain.frm` menus, forms, modules, stored procedures) |
| Target | `Genisis` (ASP.NET Core 8 API) + `GenisisUI` (React / Vite / TypeScript) on the **same** SQL Server databases, so both can run at once |
| Menu controls analysed | 241 (5 separators, 134 hidden in the desktop) — [menu-mapping.md](menu-mapping.md) |
| Marked **Required** by the business | 113 |
| Migrated screens | 23 (Worksheet Adjustment only partly): Membership (Registration, Adjustment, Enquiry), Maintenance (Plan with its tabs, Benefits), Case (Registration, Adjustment, Enquiry, Tracking, Inpatient Case Listing, Case Transfer, Panel Hospitals, Case Audit Review, MMA/CPT, Case Pay&Claim, Non-Disclosure Info, Claim ASCII, Case ASCII), Worksheet (Tracking, Send to A/C, Send to A/R, Enquiry, Adjustment: viewing and Update only) |
| Required items still to migrate (backlog) | 91 — **the Case module's 12 Required items are all done**, and the Worksheet module is in progress (Tracking, Send to A/C, Send to A/R and Enquiry done, Adjustment partly (Update, notes, Excess letter and Final LG done; Approve remains), Registration remains, see [worksheet-screens.md](worksheet-screens.md)); the rest is in the other modules (Claim Bordx 23, Maintenance 14, Payment 15, Membership 10, …) |

## 2. Behaviour preserved on purpose

The VB6 code is the specification. For every migrated screen the following were carried over, not reinvented:

- **Messages and their order.** Validation texts are the desktop's (`Please Enter Discount Type!`, `Please enter Pay&Claim Date`, `Member status - 'DELETE'.`, `Group Company cannot be empty!` …), checked in the
  desktop's order, one at a time (the API's `RequestValidator<T>` stops at the first failing rule).
- **Confirmations and workflow states.** "Are you sure?" / "Do you want to save the record?" where the desktop asked; Add / Edit / Save / Delete enable and disable rules (MMA/CPT, Pay & Claim);
  controls that the desktop disabled until a case or record was loaded.
- **Business rules and data effects.** Case Transfer moves the case *and* gives the previous member's limit back and charges the new one (principal vs covered-person limit, cancer / kidney / home-nursing
  balances); bordx numbers are `<health type><payor><7-digit sequence>` with Single Claim / Multiple Claims grouping; the audit review status is mirrored to the cross reference; Claim and Case ASCII keep the
  fixed-width layouts (5000 / 5001 / 4975 characters), header and trailer lines, `CR CR LF` endings, ANSI encoding and extension-less file names; MMA schedule keeps Part A/B/C and the mandatory fields.
- **Permissions.** The menu rights are the same `USR.USRAccess` positions (`CheckAccess(n)`), mirrored in `AccessRight.cs` and `access.ts`; passwords use the desktop's algorithm so existing users log in unchanged.
- **Data formats.** Dates are typed and shown as `dd/mm/yyyy`; columns the desktop stores as text keep their format so the desktop still reads what the web writes.
- **Terminology.** Desktop words become web words (Exit → Close, Go → Load, MsgBox → toast / confirm) as `script.txt` asks; captions, field order and labels stay familiar.

## 3. What was improved

| Area | Before (VB6) | Now |
|---|---|---|
| Integrity | multi-statement writes (Case Adjustment alone runs ~15) with no working transaction, spread over up to four connections | one transaction across HISDB, HISMaintenance, HISWorkSheet, HISPayment (synonyms); any exception rolls back |
| SQL | raw `@strAppend` strings inside stored procedures | parameterised `genisis.*` procedures (no injection surface); HISDB compatibility level 100 respected |
| Concurrency | read-then-update sequence numbers | `UPDATE … WITH (UPDLOCK)` in one statement for bordx numbers and ASCII batches |
| Validation | repeated in several handlers | one validator per request on the server (FluentValidation) + the same messages in the browser (Yup) |
| Errors | `MsgBox` and `On Error` | typed exceptions, one `ApiExceptionHandler`, actionable messages, generic text for the unexpected, React `ErrorBoundary`, request timeout, 401 → login |
| Logging | none centralised | Serilog (console + rolling files), request log with user and timing, sign-in audit |
| Structure | forms with embedded SQL and rules | controller → service → repository → procedure; interfaces, unit of work, architecture tests |
| UI | fixed-size desktop forms | responsive layout, collapsible navigation, ARIA labels / tabs / sort, keyboard navigation, focus-trapped dialogs, busy states, calendar pickers, reduced-motion support |
| Files | `C:\` folders on the user's PC | server folder + browser download, path-safe |
| Quality gates | none | warnings-as-errors build, mandatory XML docs in the layered code, oxlint, Prettier, typecheck, unit / service / integration / architecture / database tests, headless browser suites, CI workflows |

## 4. Defects found in the desktop application (and what was done)

| Finding | Decision |
|---|---|
| Case ASCII / rejected-claim date range used `<= 'to date'` at midnight, dropping cases registered later on the last day | the "to" date includes the whole day |
| A missing patient IC was written as 30 blanks into a 20-wide field, shifting every later field | padded to 20 |
| Case ASCII reprint filtered on a column its view does not have and called a procedure that does not exist (never worked) | reprint works: exporting flags the case, "R" re-sends flagged ones |
| Case Transfer took an arbitrary `AnnualLimit` row of the plan | the row in force on the member's effective date |
| `ClaimApprove` text amounts such as `-` crashed a plain cast | `genisis.Util_SafeDecimal` |
| Inpatient listing claimability test compares with `"N"` while codes are two letters (`NN`), so it never fires | kept as in the desktop and documented; one line to change if the business wants it blocked |
| `SearchMBMCas` returns nothing for members without satellite rows | Audit Review reads the member from `MBM` directly |
| Orphan menu controls: 6 of the 9 screens built last have no click handler and are hidden, so the desktop can never open them | forms matched by caption; access positions chosen and documented |
| Worksheet Tracking built SQL text for a procedure that runs it; its Ins Type filter named controls that do not exist, so it never filtered | typed parameters; the filter works ([worksheet-screens.md](worksheet-screens.md)) |
| Worksheet Send to A/C listed a worksheet once per payment row (totals doubled, a ticked repeat was sent twice) and showed the bordx number in the Status column | one row per worksheet, separate columns |
| Worksheet Send to A/R reads and writes four `CASOthers` columns that do not exist in the database (its first search would fail) | reduced screen on the three columns that exist; full version needs the production schema |

## 5. Environment work done along the way

- HISDB (restored from another server) could not open its column-encryption chain, so member updates failed; the chain was rebuilt and `nested triggers` set to 0 (the vendor's encrypt triggers
  self-update). Details and the caveat that it is server-wide: [onboarding.md](onboarding.md) §6. The roughly 6.7 million legacy ciphertext rows were intentionally not rebuilt (nothing reads them).
- Combined, idempotent SQL scripts (`sql/genisis-main.sql`, `sql/genisis-maintenance.sql`) with a test that fails when they are stale.

## 6. Evidence

| Check | Result |
|---|---|
| `dotnet build -c Release -warnaserror` | 0 warnings, 0 errors |
| `dotnet test` (validators and rules, services with fake repositories, HTTP pipeline, architecture, database tests against MYDELL with rollback) | **945 passed, 0 failed** (945 tests, merged branch; the Upload Bordx tests need the `UploadDb` staging database, created by `sql/hosting/create-upload-db.sql`) |
| `npm run lint` (oxlint) · `npm run format:check` · `npm run typecheck` · `npm run build` | 0 warnings, clean, clean, built |
| `npm test` (Vitest: masks, validation, CSV, case helpers, listing rules, worksheet and invoice rules, scan codes, error boundary) | **223 passed** |
| Browser suites (`GenisisUI/e2e`, headless Chrome, real API and databases) | **437 checks passed** (the four Worksheet suites are 148 of them, Invoice Processing 47: 15 read, 32 write, the three other Invoice screens 102, Case Registration 32), no console or network errors; data restored afterwards |
| CI workflows | written for both repositories; the same commands were run locally, **not yet on a hosted runner** |

## 7. Open items and risks

- **92 Required menu items** remain (backlog by module in `menu-mapping.md`); the older web modules still use the earlier, non-layered style (`architecture.md` §6).
- **No WCAG audit, no load/volume test, no security review** has been run; the notes in `scalability-recommendations.md` are reasoned, not measured.
- The ASCII files were validated against the VB6 layouts (field widths, header/trailer) and by round trip, but **not diffed against a file produced by the desktop** (none was available). Compare one real
  export before sending files to a payor.
- Bordx generation was tested against real pending worksheets only inside rolled-back transactions and through intercepted requests in the browser, never committed on production data.
- Access positions of the six orphan screens are decisions (not read from the VB6 source) and should be confirmed by the business.
- `nested triggers = 0` is a server-wide setting that the vendor's triggers need; other databases on `MYDELL` are affected by it.
- Next steps, in order, are in [scalability-recommendations.md](scalability-recommendations.md).
