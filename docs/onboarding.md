# Developer onboarding

Read this first, then [architecture.md](architecture.md). Everything below was verified on the development machine (Windows, SQL Server `MYDELL`, .NET 8, Node 22+).

## 1. What you are working on

A VB6 healthcare desktop application is migrated module by module to a web system that uses the **same databases**, so both run side by side:

| Repository / folder | What | Stack |
|---|---|---|
| `Genisis/` | Web API (+ serves the built UI) | ASP.NET Core 8, Dapper, FluentValidation, Serilog, xUnit |
| `GenisisUI/` | Web UI | React 19, Vite, TypeScript, React Hook Form + Yup, TanStack Query, Vitest |
| `docs/` (workspace root) | migration documentation | Markdown + Mermaid |
| `VB Code/` | the VB6 source, the **specification** of behaviour; the source of record is `D:\Team Awesome\VB Code-20261009T221049Z-1-001\VB Code` (`Documents:RootPath` in `appsettings.json` points at it) | read-only reference |

Rule of the project: **preserve all functionality, workflows and business rules** of the VB6 application; modernise the UI and the code, not the behaviour. When you are unsure how
something should behave, the VB6 form/module is the answer (`mdiMain.frm` for menus, `docs/menu-mapping.md` to find the form).

## 2. Set up

| Need | Check |
|---|---|
| .NET 8 SDK | `dotnet --version` |
| Node 22+ | `node --version` (on older shells: `PATH="/c/Program Files/nodejs:$PATH"`) |
| SQL Server access to `MYDELL` with Windows authentication | `sqlcmd -S MYDELL -E -C -Q "select @@version"` |
| Chrome (only for the browser tests) | |

Databases used: `HISDB` (main), `HISMaintenance`, `HISWorkSheet`, `HISPayment`, `HISDMArchive` (the archive database; it was named `HISDBArchive` before and the desktop connects to the new name).

### Deploy the SQL (once, and after every API update that changes `sql/`)

```
cd Genisis
sqlcmd -S MYDELL -d HISDB -E -C -b -N -i sql/genisis-main.sql
sqlcmd -S MYDELL -d HISMaintenance -E -C -b -N -i sql/genisis-maintenance.sql
```

Both scripts are idempotent. Details and the SQL conventions: `Genisis/sql/README.md`. HISDB is at **compatibility level 100**: no `TRY_CONVERT`, `FORMAT`, `IIF`, `CONCAT`, `EOMONTH`.

### Run

```
# API  -> http://localhost:5000 (Swagger at /swagger in Development)
cd Genisis/src/Genisis.Api
dotnet run --launch-profile http

# UI   -> http://localhost:5173 (proxies /api to :5000)
cd GenisisUI
npm ci
npm run dev
```

Stop the API before `dotnet build` / `dotnet test`: a running `Genisis.Api.exe` locks the output.

### Configuration (`appsettings.json`, overridable with environment variables such as `ConnectionStrings__MainDb`)

| Key | Meaning |
|---|---|
| `ConnectionStrings:MainDb`, `:Maintenance`, `:WorkSheet` | databases (all on one server; the others are reached through synonyms) |
| `Auth:JwtKey` | 32+ characters; empty = random key per start (everyone is logged out on restart) |
| `Auth:TokenHours` | token lifetime (default 10) |
| `Documents:RootPath` | folder with `ScannedDoc\<case>` and the case document folders; also the default parent of `ClaimASCI` / `CaseASCII` |
| `Ascii:ClaimFolder`, `Ascii:CaseFolder` | where the payor ASCII files are written |
| `Cors:Origins` | allowed browser origins (the Vite dev server) |
| `Serilog` | log levels and sinks (console + `logs/genisis-.log`, 30 days) |

Log in with a desktop user (`HISMaintenance.dbo.USR`, same password algorithm as `frmLogin.frm`). Menu rights are the `USR.USRAccess` flags: position *n* = `CheckAccess(n)`.

## 3. Everyday commands

| Task | Command |
|---|---|
| API build (as CI does) | `dotnet build Genisis.sln -c Release -warnaserror` |
| API tests (unit, service, integration, architecture) | `dotnet test` |
| …plus the database tests | set `GENISIS_TEST_MAINDB`, `GENISIS_TEST_MAINTENANCE`, `GENISIS_TEST_WORKSHEET` (connection strings, see `AGENTS.md`), then `dotnet test` — every write runs in a rolled-back transaction |
| Regenerate the combined SQL after editing `sql/main/*.sql` | `GENISIS_WRITE_SQL=1 dotnet test --filter Combined_scripts_are_up_to_date` |
| UI lint / format / types / tests / build | `npm run lint` · `npm run format:check` (`npm run format` fixes) · `npm run typecheck` · `npm test` · `npm run build` |
| Browser tests | `GenisisUI/e2e/README.md` |
| Publish a single Windows exe (API + UI) | `Genisis/publish-win.ps1 -UiPath ..\GenisisUI` |

CI (`.github/workflows/ci.yml` in each repository) runs build with warnings as errors, whitespace format check of the layered code, tests, lint, Prettier, typecheck and build.

## 4. Adding a screen — checklist

1. Find the VB6 form (`docs/menu-mapping.md`), read its code: every event handler, validation, message and data call. Note hidden controls and dead handlers (they go in the redundancy report).
2. `sql/main/NNN_<screen>.sql`: parameterised procedures `genisis.<Screen>_<Action>`; no `@strAppend`; deploy and regenerate the combined script.
3. `Genisis.Api/Case/<Screen>/`: models, `…Validator` (`RequestValidator<T>`, desktop messages in the desktop order), `I…Repository` + implementation, `…Service` (throw `NotFoundException` /
   `ConflictException` / `BusinessRuleException`; start the unit of work; log who did what). XML-document every public member (the build fails otherwise).
4. Register in `AddCaseScreens()`; add the controller (`[RequireAccess]`, one call per action, no `try/catch`); add the position to `AccessRight.cs` **and** `access.ts`.
5. Tests: validator rules, service tests with a fake repository, one rolled-back database test, the 401 / 403 / 400 cases in `ApiIntegrationTests`, and add the controller to `ArchitectureTests`.
6. UI: page in `src/pages/<module>/` using `Field`, `Button`, `DataGrid`, `Tabs`, `useScreenLookups`; Yup schema with the same messages; route in `App.tsx`; menu entry in `AppLayout.tsx`; a pure `…Rules.ts` with a unit test for any rule worth testing.
7. Add a suite or scenario to `GenisisUI/e2e/` for the screen's workflow, and update `docs/menu-mapping.md` (rerun the generator) and the module's migration document.

## 5. Where things are

| Question | Look at |
|---|---|
| Which web screen replaces which VB6 menu? | `docs/menu-mapping.md` |
| How are layers organised, named, tested? | `docs/architecture.md` |
| How are errors and logs handled? | `docs/exception-handling-and-logging.md` |
| What was removed from the VB6 and why? | `docs/redundancy-removal-report.md` |
| What differs from the desktop, screen by screen? | `docs/case-*-migration.md`, `docs/case-remaining-screens-*.md` |
| What should be done next to scale? | `docs/scalability-recommendations.md` |
| What was delivered overall? | `docs/migration-summary.md` |
| Workspace conventions for AI agents and people | `AGENTS.md` |

## 6. Known environment issues

- **HISDB was restored from another server.** Its column-encryption chain (database master key → `SelfSignedCertificate` → `SQLSymmetricKey` / `SQLSymmetricKey_256`) could not be opened on this
  server (SQL error 15581), so updates of members failed. The chain was rebuilt with a new database master key (the password is held by the DBA and is deliberately not in the repository), and
  `sp_configure 'nested triggers', 0` was set because the vendor's encrypt triggers update their own table and recurse until SQL Server's 32-level limit otherwise. The `*_AES` ciphertext columns
  are write-only audit copies (nothing reads them); rows written before the rebuild keep their old, unreadable ciphertext. `nested triggers = 0` is **server-wide**: tell the DBA before moving
  databases.
- **A running API locks `Genisis.Api.exe`**; stop it before building or testing.
- **`CASLG.LGNo` is `numeric` in the database** while the API models use `int`: procedures cast it (`CAST(LGNo AS int)`); do the same for any new numeric key that maps to an `int`.
- **Date strings:** the UI shows and types `dd/mm/yyyy`; the API takes and returns ISO dates; a few desktop columns store dates as text (for example the audit visit date as `mm/dd/yyyy`) and the procedures
  keep that format so the desktop still reads them.
