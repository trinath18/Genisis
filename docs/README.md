# Documentation index and `script.txt` compliance

`script.txt` (workspace root) is the engineering brief for the migration. This page maps every deliverable it lists to a document, and states honestly where the work
meets the brief, only partly meets it, or does not meet it yet.

## Deliverables → documents

| `script.txt` deliverable | Where |
|---|---|
| Complete C# API codebase (Clean Architecture) | `Genisis/` — layered for the nine Case screens and the Worksheet screens added since; see [architecture.md](architecture.md) §6 for what is not converted yet |
| Complete Node.js + React UI codebase (modular, reusable) | `GenisisUI/` |
| Menu / submenu mapping documentation | [menu-mapping.md](menu-mapping.md) (all 241 menu controls, status per item, backlog) |
| Scalability recommendations | [scalability-recommendations.md](scalability-recommendations.md) |
| Exception handling strategy | [exception-handling-and-logging.md](exception-handling-and-logging.md) |
| Redundancy removal report | [redundancy-removal-report.md](redundancy-removal-report.md) |
| Migration summary (preserved behaviours, modernisation improvements) | [migration-summary.md](migration-summary.md) |
| Developer onboarding guide | [onboarding.md](onboarding.md) |
| Architecture diagrams, code organization, naming conventions, reusable component patterns | [architecture.md](architecture.md) |
| CI/CD with automated linting, testing and build validation | `Genisis/.github/workflows/ci.yml`, `GenisisUI/.github/workflows/ci.yml` |
| Invoice Processing (`frmInvoice`) and Worksheet Registration: how an invoice reaches a worksheet, the checks and procedures, data findings, plan | [invoice-processing.md](invoice-processing.md) |
| Per-module behaviour notes and intentional deviations | [case-registration-migration.md](case-registration-migration.md), [case-adjustment-migration.md](case-adjustment-migration.md), [case-remaining-screens-ui.md](case-remaining-screens-ui.md), [case-remaining-screens-backend.md](case-remaining-screens-backend.md), [worksheet-screens.md](worksheet-screens.md) |

## Compliance with the brief

Legend: **Met** — in place and verified · **Partly** — in place for part of the code or with a stated limit · **Open** — not done.

### Primary goals

| Goal | Status | Notes |
|---|---|---|
| Preserve all functionality, workflows, business rules | **Partly** | Preserved for the migrated screens (messages, order of checks, confirmations, data effects). 22 of the 113 menu items marked Required are migrated, one of them only partly (Worksheet Adjustment: viewing and the Update button; plus Case Inquiry, built earlier but not marked); 91 Required items are the backlog (`menu-mapping.md`). |
| Capture all menus, submenus, hidden interactions | **Met** for the menu structure (241 controls extracted, flags, handlers, access positions); hidden interactions are captured per migrated form in the module documents. |
| Healthcare-grade, accessible UI | **Partly** | Labels, `aria-invalid` / `aria-describedby`, `aria-sort`, `aria-busy`, ARIA tabs with keyboard support, focus-trapped dialogs, reduced-motion support, desktop terminology replaced (Exit → Close …). **No formal WCAG audit or contrast measurement has been run.** |
| SOLID, Clean Architecture, DRY, KISS, separation of concerns | **Partly** | Fully applied (and enforced by architecture tests) to the nine Case screens and the four Worksheet screens built so far and the first part of Worksheet Adjustment; older modules still call Dapper from services; one API project, not several assemblies. |
| Strict linting, formatting, naming | **Partly** | `.editorconfig` naming rules, warnings-as-errors and mandatory XML docs in the layered code, oxlint (0 warnings), Prettier. Prettier skips a documented list of older files (`GenisisUI/.prettierignore`). |
| Better exception handling, logging, error recovery | **Met** | Central `ApiExceptionHandler`, typed exceptions, Serilog with request and sign-in audit, unit of work with rollback, React `ErrorBoundary`, timeouts and 401 handling. |
| Remove unused / redundant / obsolete code, document removals | **Partly** | Documented in the report; the VB6 source itself is deliberately left untouched (it is the specification). |
| Scalable, modular, testable, maintainable | **Partly** | Stateless API, repository seams, modular folders; limits and next steps in `scalability-recommendations.md`. No load test has been run. |
| Familiar to existing users | **Met** for migrated screens: same captions, messages, field order and shortcuts of behaviour. |

### Backend requirements

| Requirement | Status |
|---|---|
| async/await, structured exception handling, DI | **Met** |
| Centralized logging (Serilog) | **Met** |
| Input validation with FluentValidation | **Partly** — the nine Case screens and the Worksheet screens; Registration, Plans, Benefits, Case Registration / Adjustment still use their own static validators |
| Controllers thin → Services → Repositories → DTOs | **Partly** — complete for the nine Case and the Worksheet screens (architecture-tested); older modules have services that access data directly |
| PascalCase / camelCase conventions | **Met** (`.editorconfig`) |
| XML documentation for public methods | **Partly** — compiler-enforced for `Common/`, `Data/`, the nine Case screens, the Worksheet screens and their controllers; older modules are not fully documented |
| Meaningful errors, graceful fallbacks | **Met** |
| Stateless, scalable API | **Met** (see limits in the scalability document) |
| Unit tests (xUnit) and integration tests for endpoints | **Met** for the nine Case and the Worksheet screens (rules, services with fakes, HTTP pipeline, database tests); older modules keep their existing tests |

### Frontend requirements

| Requirement | Status |
|---|---|
| React Hook Form + Yup, error messages, loading states, accessibility, responsive layout | **Met** for the new screens; older screens predate the form kit |
| Reusable buttons, forms, modals, tables, navigation | **Met** (`Button`, `Field`, `Modal`, `DataGrid`/`DataTable`, `Tabs`, `AppLayout`) |
| ESLint + Prettier | **Partly** — `oxlint` (an ESLint-compatible linter) rather than ESLint itself, plus Prettier |
| Functional components + hooks, TypeScript, no inline styles | **Met** (the one class component is React's `ErrorBoundary`, which has no hook equivalent; no `style=` attributes remain) |
| All submenu interactions implemented **and tested** | **Partly** — tested for the migrated screens (Vitest unit tests + headless-Chrome suites in `GenisisUI/e2e`); the rest of the menu is the backlog |

### Scalability / clarity / pipelines

| Requirement | Status |
|---|---|
| Modular architecture, shared utilities, future expansion | **Met** at folder / namespace level |
| CI/CD with lint, test, build | **Met** as workflow files; they have not been executed on a hosted runner from this machine (the commands were run locally) |
| Architecture diagrams, code organization, naming, component patterns, documentation set | **Met** |

## Open items against the brief

An audit of the repositories against `script.txt` (every library, document, pipeline and rule above checked in the code, not from memory). What the brief asks for and this migration does **not** yet deliver:

1. **"Preserve ALL existing functionality"** is met screen by screen, not for the whole application: 22 of the 113 menu items marked Required are migrated, 91 are not (backlog by module in [menu-mapping.md](menu-mapping.md); Claim Bordx 23, Payment 15, Maintenance 14, Membership 10, and the rest of Worksheet).
2. **The Worksheet module is not finished.** Migrated: Tracking, Send to A/C, Send to A/R, Enquiry, and Adjustment's Update, credit / debit notes, Excess letter and Final LG. **Not built: Worksheet Registration and Adjustment's Approve.** The calculation engine they need reproduces the desktop's saved totals for about 69 per cent of worksheets (measured on 819 of the 70,426 saved versions) from the invoices and the plan alone (measured, see [worksheet-screens.md](worksheet-screens.md) "Step 6"); the gap is the plan rules for benefits the plan's rows do not list, plus the Room & Board co-payment.
3. **Linting is `oxlint`, not ESLint** (ESLint-compatible rules, 0 warnings); Prettier skips a documented list of older files.
4. **No formal WCAG audit or colour-contrast measurement** has been run; the accessibility features listed above are in place.
5. **CI workflows have not run on a hosted runner**; every command they contain was run locally and passes (API build with warnings as errors and 808 tests, UI lint / format / typecheck / 181 unit tests / build, 334 browser checks).
6. **Older modules are not fully layered** (Plans, Benefits, Membership Registration still call Dapper from services), and one API project holds all layers.
7. **The browser suites and the "oracle" tests depend on the development data.** ("Oracle" here is the testing term, a trusted reference the code is compared with, here the values the desktop saved; it has nothing to do with Oracle Database: every database in this system is SQL Server.) They were corrected when the full data was restored (three Case database tests, two browser suites); a different dataset can need the same.
