# Deploying Genisis to Railway

The API and the web UI are one container: `Dockerfile` publishes `src/Genisis.Api` (the built React UI is committed in its `wwwroot`) onto the ASP.NET Core 8 runtime image with the fonts the Leaflet & Card PDF needs.
Railway builds it from `railway.json` (builder Dockerfile, health check on `/`, restart on failure up to 5 times). **The databases are not part of the deployment**: HISDB, HISMaintenance, HISWorkSheet (and HISPayment
and the archive through HISDB's synonyms) stay on the SQL Server where they are, and the container reaches them over the network.

> **This reaches the real databases.** A deployed API that points at the development SQL Server on `MYDELL` reads and writes the same data as the local one (every save, invoice, case and membership change).
> Do not point it at a SQL Server you are not willing to have changed from the internet, and do not leave the tunnel or the firewall rule open when the deployment is not in use.

## 1. What was checked without Railway

Railway was not touched from here (no account access, no CLI, no Docker on the build machine). What was run instead:

* `dotnet publish src/Genisis.Api -c Release` with nothing but the project folder, as the Dockerfile does: it succeeds and the output holds `wwwroot/index.html` and its two assets.
* That publish run as the container will run it (Production, `ASPNETCORE_URLS` set, every setting from environment variables, an empty `Documents__RootPath`): it listens, `GET /` answers 200 (the health check), a real API
  route answers 401 without a token (`/api/invoice/...`, `/api/maintenance/plans`, `/api/case/registration/lookups`), `/api/auth/login` answers 400 to an empty body, and Swagger is off (Production).
* `railway.json` uses the keys of Railway's config-as-code reference (`build.builder`, `build.dockerfilePath`, `deploy.healthcheckPath`, `healthcheckTimeout`, `restartPolicyType`, `restartPolicyMaxRetries`).
  Railway marks config as code as deprecated, still working for existing services until 2026-12-01; the same settings can be made in the service's Settings (Builder: Dockerfile; Healthcheck path `/`; Restart policy: on
  failure, 5 retries).

Not checked: the Docker build itself and the first start on Railway's network. Read the first deployment's build and deploy logs.

## 2. Before the first deploy

1. **A SQL login for the API** (SQL authentication; a container cannot use Windows authentication). Create the login, then run `sql/hosting/grant-api-login.sql` as a sysadmin, once per environment (it is idempotent and
   grants EXECUTE on the `genisis` schema and on the older `dbo` procedures the API calls, including the eight the Registration save uses; give the login `db_datareader` and `db_datawriter` on the five databases
   as the script's header says). Use a long random password, kept only in Railway's variables.
2. **The SQL scripts of this branch applied** to the databases the API will use: `sql/genisis-main.sql` to HISDB and `sql/genisis-maintenance.sql` to HISMaintenance.
3. **A way for the container to reach port 1433.** SQL Server on this machine listens on all interfaces, but Railway cannot see a machine behind a router. Options, each with a cost you must weigh:
   * a TCP tunnel service that publishes a host and port for 1433 (the address Railway connects to): easy, but the endpoint is on the internet. Protect it with the tunnel's own authentication or allow-list if it has one;
   * a port forward on the router to a fixed address: the SQL Server is then on the internet; restrict the firewall rule to the addresses Railway connects from (check in Railway's documentation whether your plan has a
     fixed outbound address; without one an allow-list by address is not possible) and keep SQL Server patched;
   * a SQL Server that is already reachable from the cloud (a VM, Azure SQL): restore the databases there and use its address.
   Whatever is chosen: the SQL login must be the least-privileged one above, the password strong, and the connection **encrypted with a certificate Railway trusts** (`Encrypt=True;TrustServerCertificate=False`);
   `TrustServerCertificate=True` encrypts but does not prove who is on the other end.

## 3. Variables (Railway service > Variables; keep the secrets out of git and out of chat)

| Variable | Value |
|---|---|
| `ConnectionStrings__MainDb` | `Server=tcp:<host>,<port>;Database=HISDB;User Id=apiuser;Password=<secret>;Encrypt=True;TrustServerCertificate=False` |
| `ConnectionStrings__Maintenance` | the same server, `Database=HISMaintenance` |
| `ConnectionStrings__WorkSheet` | the same server, `Database=HISWorkSheet` |
| `Auth__JwtKey` | **a long random string, set once and kept** (without it the API makes a new key at every start and every user is logged out by a restart; at least 32 random characters) |
| `Auth__TokenHours` | optional; 10 by default |
| `Documents__RootPath` | empty. The path in appsettings.json is a Windows folder; on Linux the case documents, wording files and uploads are reported as not available |
| `Ascii__ClaimFolder`, `Ascii__CaseFolder` | for example `/tmp/ascii/claim` and `/tmp/ascii/case` (the container's disk is not kept between deployments; the exports are for download) |
| `Cors__Origins__0` | not needed (the UI is served by the API, same origin); set it only if another site calls the API |

`PORT` is set by Railway and `ASPNETCORE_ENVIRONMENT=Production` by the Dockerfile.

## 4. Deploy

With the Railway CLI (you log in; nothing here stores a token):

```
npm install -g @railway/cli
railway login
railway init                      # or: railway link   (an existing project)
# set the variables of section 3 in the dashboard (Variables), or one by one: railway variables --set "NAME=value"
railway up                        # builds the Dockerfile in this folder and deploys it
railway domain                    # gives the service a public address
railway logs                      # the build and the first start
```

Or from the dashboard: New Project > Deploy from GitHub repo > `trinath18/Genisis`, choose the branch (the work is on `case-module`; `main` does not have it), then add the variables and generate a domain.

## 5. After the deploy

1. Open the domain: the login page appears (`GET /` is also the health check).
2. Log in with a real user and open one screen that reads (for example Worksheet > Tracking). A login that fails with "could not connect" means the tunnel, the address or the SQL login is wrong; the log line says which.
3. Do **not** run the saves of the screens on live data until you have decided that is what you want: the deployed UI writes through the same stored procedures as the local one.

**Roll back / stop:** in Railway, Deployments > pick the earlier one > Redeploy, or remove the service's domain; close the tunnel or the firewall rule.

## 6. Differences of a Linux container

* The case documents, scanned files, policy wording and the e-mail attachments folders (`Documents:RootPath`) are not available unless a volume and a path are configured for them.
* Log files go to `/app/logs` inside the container (Serilog also writes to the console, which Railway keeps).
* PDF documents use the DejaVu fonts the image installs, not the Windows fonts the desktop used.
