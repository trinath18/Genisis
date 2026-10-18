# Scalability recommendations

What already scales, what limits scale today, and what to do next, in priority order.

## Already in place

- **Stateless API.** Identity is a JWT (`usr_access` flags inside); no session state, no in-memory caches that matter for correctness. Any number of instances can run behind a
  load balancer with the same `Auth:JwtKey`.
- **Short database work.** One connection per request, opened late and disposed with the unit of work; sequences (bordx numbers, ASCII batches) are read and incremented by a single
  `UPDATE … WITH (UPDLOCK)`, so two instances cannot hand out the same number once the sequence row exists.
- **Server-side paging by design for listings** is capped (`MaxRows` 5,000 for worksheet and MMA searches) instead of returning whole tables.
- **Modular code.** A screen is a folder of model / validator / repository / service plus one controller, registered in one place; architecture tests keep the layering intact.
- **Single deployable and separate frontend.** The UI is a static build that the API can serve (`wwwroot`) or a CDN/web server can host; `VITE_API_URL` points it anywhere.
- **Observability hooks.** Structured Serilog logs with the user and elapsed time on every request.

## Limits today

| Limit | Why it matters | Where |
|---|---|---|
| Output files and scanned documents on the API server's disk | a second instance would not see them | `Ascii:*Folder`, `Documents:RootPath` |
| ASCII export and bordx generation run inside the request | a very large export holds a request and a transaction open | `ClaimAsciiService`, `ListingService` |
| `TokenService` signs with a configured key; empty key = random per start | restarting (or scaling out without a shared key) logs everyone out | `Auth:JwtKey` |
| Cross-database synonyms | HISDB, HISMaintenance, HISWorkSheet, HISPayment must stay on **one SQL Server instance** | `sql/main/000_setup.sql` |
| Older modules call Dapper from services and use static validators | no seam for caching or for unit tests without a database | Registration, Plans, Benefits, Case Registration / Adjustment / Tracking |
| One API project | everything compiles and deploys together | `Genisis.Api` |
| First number of a payor / day: the sequence row is created with a plain `INSERT` and the vendor tables (`Claim_BordxSEQ2013`, `CLMAsciiSEQ`, `CASAsciiSEQ`) have no unique key on it | two simultaneous first exports of the same payor could both get `00001`; add a unique index on (payor, date) — a schema change to vendor tables, so it needs the DBA | `genisis.Ascii_NextBatch`, `genisis.CaseListing_NextBordxNo` |
| HISDB compatibility level 100 | no newer T-SQL (`TRY_CONVERT`, `STRING_SPLIT`…); new code must avoid it | `sql/README.md` |

## Recommendations (in order)

1. **Finish the layered conversion** of the older modules using the pattern of the nine Case screens: repository interface + `IDbSessions`, FluentValidation validators,
   exceptions derived from `BusinessRuleException`. The central exception handler already covers their exceptions, so each module can be converted on its own, one controller
   at a time, with the existing tests as the safety net.
2. **Split the API into assemblies** once most modules are converted: `Genisis.Domain` (rules and models), `Genisis.Application` (services + repository interfaces),
   `Genisis.Infrastructure` (repositories, `DbSessions`, file stores, Serilog), `Genisis.Api` (controllers, composition root). The architecture tests become assembly-reference
   rules, which is stricter and cheaper to check.
3. **Move files off the web server**: write ASCII files and read scanned documents through the `IAsciiFileStore` / `IScannedDocuments` seams that already exist, backed by a
   shared file share or object storage. Nothing else changes.
4. **Make long jobs asynchronous** when volumes grow: enqueue "generate bordx" / "export ASCII" (a hosted `BackgroundService` or a queue), return a job id, let the UI poll and
   then download. The services already do their work in one method with a clear result, so wrapping them is mechanical.
5. **Cache reference data** that changes rarely (payors, panel codes, MMA fee table, code lists) with `IMemoryCache` behind the repository interface (or `HybridCache`),
   invalidated by the screens that edit them. The repository seam means no controller or service changes.
6. **Add keyset paging** (`after` token) to the largest searches (worksheet listing, case tracking) before the row cap becomes a business limitation; add the matching
   server-side sort to the grid.
7. **Health, metrics and tracing**: `/health` (database + folders), OpenTelemetry for request and SQL timings, and a correlation id in the Serilog `LogContext`.
8. **Database**: raise HISDB's compatibility level once the vendor desktop app is retired (or validated), add covering indexes for the listing and tracking filters based on
   the actual query plans, and keep the new `genisis.*` procedures as the only data-access surface of the API.
9. **Front end**: route-level code splitting (`React.lazy`) — the production bundle is one ~690 KB chunk — and a shared API-hook layer (React Query) for the screens that still
   call axios directly.
10. **Pipelines**: the CI workflows build, test and format-check both repositories; add a deployment stage that runs `sql/genisis-*.sql` first, then rolls the API, then the UI,
    and a smoke test (`/api/auth/login` + one GET per module) before switching traffic.

## Capacity notes

- The heaviest operations are the worksheet search (views over `Claim`, `ClaimInvoice`, `CAS`), bordx generation (one transaction per bordx) and the ASCII exports (a few
  thousand fixed-width lines, written once). They have only been exercised on the development database (a few hundred pending worksheets, a handful of exported claims);
  no load or volume test has been run, so measure with production-sized data before sizing hardware.
- Scaling out is limited by SQL Server, not by the API: keep transactions short (they are), avoid long-running reads inside a write session, and watch lock waits on the
  sequence tables (`Claim_BordxSEQ2013`, `CLMAsciiSEQ`, `CASAsciiSEQ`).
