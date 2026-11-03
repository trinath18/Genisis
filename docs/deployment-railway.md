# Deploying Genisis to Railway

The API and the web UI are one container: `Dockerfile` publishes `src/Genisis.Api` (the built React UI is committed in its `wwwroot`) onto the ASP.NET Core 8 runtime image with the fonts the Leaflet & Card PDF needs.
Railway builds it from `railway.json` (builder Dockerfile, health check on `/`, restart on failure up to 5 times). **The databases are not part of the deployment**: HISDB, HISMaintenance, HISWorkSheet (and HISPayment
and the archive through HISDB's synonyms) stay on the SQL Server where they are, and the container reaches them over the network.

> **This reaches the real databases.** A deployed API that points at the development SQL Server on `MYDELL` reads and writes the same data as the local one (every save, invoice, case and membership change).
> Do not point it at a SQL Server you are not willing to have changed from the internet, and do not leave the tunnel or the firewall rule open when the deployment is not in use.

## 0. The deployment that exists, and how it is updated

Genisis is already deployed: the Railway project `extraordinary-liberation`, service `genisis`, at **https://genisis-production.up.railway.app**. It is **not** built from the repositories of this guide but from
**`nyeduruwada/vb2web`** (branch `main`; a clone sits next to `Genisis` and `GenisisUI` in the workspace), a deployment copy of both with its own root `Dockerfile` that builds the UI in a Node stage and the API in a .NET
stage into one image, and its own `railway.json`. Railway rebuilds and redeploys on every push to that branch (about 80 seconds; the old deployment keeps serving until the new one passes the health check on `/`).
Sections 1 to 6 below describe a standalone container of the API repository alone (its `Dockerfile`, which uses the UI bundle committed in `wwwroot`); they are the reference for the variables and the checks, and the
way to deploy without the copy.

**The service's settings** (variable names; the values are in Railway and nowhere else): `ASPNETCORE_ENVIRONMENT`, `Auth__JwtKey` (64 characters, stable), `ConnectionStrings__MainDb`, `__Maintenance` and `__WorkSheet` (the
SQL login `apiuser`, `Encrypt=True;TrustServerCertificate=True`), `Ascii__ClaimFolder`, `Ascii__CaseFolder`. **The database is the SQL Server on the development machine (MYDELL)**, reached through the public address of
its network on port 1433: the service and the development databases are the same data. Every change of the SQL scripts that the app needs is therefore live as soon as it is applied to the development
databases; apply it before the deployment that needs it.

**To update the deployment** (done on 2026-10-26 for Genisis `dd64c98` and GenisisUI `9f8256c`, and on 2026-10-31 for Genisis `73a3f0b` and GenisisUI `e04aafd`, `vb2web` `3ed84a4`; the second time `git rm -r Genisis GenisisUI` and `git archive HEAD | tar -x` with `--anchored --exclude=Dockerfile --exclude=railway.json --exclude=.dockerignore` replaced the trees, and the grant script was run again):

1. Commit and push the work in `Genisis` and `GenisisUI` (the tree to deploy must be committed: the copy is made from `git archive HEAD`, so nothing uncommitted or untracked travels).
2. Replace `Genisis/` and `GenisisUI/` of the `vb2web` clone with the exported trees (delete the old folders first so removed files go; do not copy the API repository's own `Dockerfile`, `.dockerignore` and `railway.json` into it:
   `vb2web` has its own at the root). Its `.gitignore` keeps `bin`, `obj`, `node_modules`, `dist`, `logs` and `.vs` out.
3. Build it the way the Dockerfile does before pushing: `npm ci && npm run build` in `GenisisUI`, then `dotnet publish Genisis/src/Genisis.Api/Genisis.Api.csproj -c Release` with `wwwroot` replaced by the UI's `dist`
   (the bundle must be the same as the one committed in the API repository), then start the output in Production against a database and check `/`, a login with a made-up user (the app's own 401 message) and a
   few protected routes (401 without a token).
4. Commit in `vb2web` in the style of its history (`Update Genisis app: ...`) and push `main`. Watch the deployment: `railway service status` (BUILDING, DEPLOYING, SUCCESS) and `railway logs --deployment`; then repeat the
   checks of step 3 against the domain (the bundle name on `/` changes with every UI build).

The Railway CLI (`@railway/cli` 5.70.0, installed globally) is logged in as the account owner; its login expires: `railway login` prints a link and a code to approve in the browser. The `Genisis` folder is linked to the
project (kept in the CLI's own settings, not in the repository).

**What was checked on 2026-10-26** before the push: the API login `apiuser` can use every object the exposed API reaches (all 373 procedures of the `genisis` schema, the tables and views behind the synonyms), except
the three procedures of the Registration save that no endpoint calls yet (`sql/hosting/grant-api-login.sql` grants them for when it does; run it then). After the deployment: `/` serves the new bundle, the login reaches the database,
the new Invoice and Case routes answer 401 where the old build answered the UI page, and the log has no error.

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
