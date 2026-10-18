# Exception handling and logging strategy

Goal: the user always sees a message they can act on; a failure they cannot fix is logged with enough detail to diagnose and shown as a short generic
message; no half-finished save is ever left behind.

## 1. Backend

### Who throws what

| Situation | Thrown by | Exception | Answer |
|---|---|---|---|
| Input breaks a rule the desktop checked (required field, format, length) | `ValidationFilter` runs the `RequestValidator<T>` | — (answers directly) | **400** `{ "message": "<first message>" }` |
| Business rule broken while working (target member deleted, worksheet no longer pending) | services | `BusinessRuleException` (and `CaseListingException`, `AsciiException`, legacy `PlanException`, `RegistrationException`, `CaseRegistrationException`) | **400** + the message |
| Record asked for does not exist | services | `NotFoundException` | **404** + the message (`No record found!`, `Record not found!`) |
| Request clashes with the data (Add on a case that has a Pay & Claim record) | services | `ConflictException` | **409** + the message |
| Menu right missing | `[RequireAccess]` | — | **403** `{ "message": "Access Denied!" }` |
| Not logged in / token expired | JWT middleware | — | **401** (the UI returns to the login page) |
| Text longer than the column (SQL 8152 / 2628) | database | `SqlException` | **400** "One of the values is longer than the database allows…" |
| Deadlock victim (SQL 1205) | database | `SqlException` | **409** "Someone else is changing the same records. Wait a moment and try again." |
| Query timeout (SQL -2) | database | `SqlException` | **503** "The database took too long to answer…" |
| Stored procedures not deployed (SQL 2812) | database | `SqlException` | **500** "…missing the stored procedures this version needs. Run sql/genisis-main.sql…" |
| Anything else | anywhere | any | **500** "Server error. Check the API log for details." — the exception text is **never** sent to the client |

Rules for code:

- Controllers have no `try/catch` (enforced by an architecture test). Services throw typed exceptions; `ApiExceptionHandler` (`Common/`) is the single place that maps
  exceptions to status codes and messages, and it is unit-tested for every branch.
- Validation lives in FluentValidation validators (`RequestValidator<T>` stops at the first failing rule, like the desktop's one-MsgBox-at-a-time forms).
  The same messages are checked again in the browser (Yup), so the user sees them without a round trip and the API still protects itself.
- A write is a unit of work (`IDbSessions.BeginAsync`): an exception disposes the session, which rolls the transaction back. Multi-table, multi-database saves
  (Case Transfer, bordx generation, ASCII export) either complete or leave nothing.
- File output (ASCII) is written **inside** the transaction, before the commit: if the folder is unavailable the `AsciiException` rolls back the batch number and the
  exported flags. `IOException` / `UnauthorizedAccessException` are turned into a message that names the problem.
- Graceful fallbacks: the scanned-document check is skipped (reported as `available: false`) when `Documents:RootPath` is not configured instead of blocking every
  worksheet; the payor/panel drop-downs fall back to typing; the Audit Review member panel falls back to `MBM` when `SearchMBMCas` returns no row.

### Logging

`Serilog` is the single logging pipeline (`Program.cs`, `Serilog` section of `appsettings.json`):

- Sinks: console and rolling daily files `logs/genisis-.log`, 30 files kept. Change levels or paths in configuration, no code change.
- Request logging (`UseSerilogRequestLogging`): one line per request with method, path, status, elapsed time and the user (`User` property = `USR.USRCode`).
- Application logs use message templates with named properties, never string concatenation: `Case {CaseNumber} transferred to {Member}-{Cover} by {User}; approved amount moved {Moved}`.
  Writes log at *Information* (who did what), refused requests at *Warning* (status + message, no stack), unexpected failures at *Error* with the exception.
- Sign-in is audited: successful logins, wrong user / password and password changes are logged with the user code and the caller's IP address (an unknown user code is cut at 30 characters).
- Never logged: passwords, tokens, connection strings, the payload of login requests.
- The log line for a 5xx and the response `message` are deliberately different: the user gets a generic text, the log has the stack trace and the request path.

## 2. Frontend

- `api/client.ts` (axios): attaches the JWT; on **401** (not from `/api/auth/*`) clears the session and goes to `/login`; 90-second timeout; `errorMessage(error)` turns any failure
  into text: the API's `message`, a timeout message, "Cannot reach the Genisis API. Is it running?" when there is no response, otherwise the error's own message.
- Screens show failures as toasts (`notify(errorMessage(e), 'error')`) next to the field that caused them when there is one (React Hook Form + Yup, `aria-invalid` +
  `aria-describedby`); confirmations use `ask()` (focus-trapped modal) in the same places the desktop asked "Are you sure?".
- Loading and busy states: `Button busy busyLabel` disables the action and announces `aria-busy` while a request runs, so a double click cannot send two saves.
- `ErrorBoundary` (around the routed screen in `AppLayout`) keeps a rendering crash inside the screen: alert panel with *Try again* and *Go to the home page*; the menu, session and
  other tabs keep working; it resets itself when the route changes. The crash is written to the browser console with its component stack.
- Server-side rule: the UI is a convenience, the API revalidates everything (selection rules of the bordx listing, member/cover checks of a transfer, mandatory fields…).

## 3. Operating it

| Symptom | Look at | Typical cause |
|---|---|---|
| "Server error. Check the API log for details." | `logs/genisis-YYYYMMDD.log`, search `Unhandled error` | the stack trace is there |
| "The database is missing the stored procedures this version needs." | `sql/README.md` | `genisis-main.sql` was not run after an update |
| 403 "Access Denied!" for a user | `USR.USRAccess` position for that screen (`docs/menu-mapping.md`) | permission flag not set |
| ASCII "output folder is not configured" | `Ascii:ClaimFolder`, `Ascii:CaseFolder`, `Documents:RootPath` | missing configuration |
| Updates fail with SQL error 15581 or "maximum nesting level (32)" | "Known environment issues" in [onboarding.md](onboarding.md) | HISDB restored onto another server (database master key) |
