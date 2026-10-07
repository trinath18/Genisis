# Genisis API

ASP.NET Core 8 Web API replacing the MedixHIS VB6 desktop app, module by module. It reads and writes the existing
MedixHIS SQL Server databases (`HISDB`, `HISMaintenance`) unchanged, so the old desktop app can keep running alongside it.
The React front end lives in [GenisisUI](https://github.com/trinath18/GenisisUI).

## Run locally

Prerequisites: .NET 8 SDK and a SQL Server with the MedixHIS databases restored.

```
cd src/Genisis.Api
dotnet run --launch-profile http
```

Swagger opens at http://localhost:5000/swagger. Connection strings default to `localhost` with Windows authentication
(`appsettings.json`). To override without editing the file, set environment variables, e.g.
`ConnectionStrings__HISDB` and `ConnectionStrings__HISMaintenance`.

Set `Auth:JwtKey` (32+ characters) to keep users logged in across restarts; when empty a random key is generated at startup.

## Login

`POST /api/auth/login` checks `HISMaintenance.dbo.USR` (active users only) with the same `Crypt` algorithm as
`frmLogin.frm`, so existing MedixHIS passwords work. Menu rights come from `USR.USRAccess` exactly like the VB6
`CheckAccess` function (position 1 = Membership Registration, 2 = Adjustment, 3 = Enquiry).

## Membership Enquiry (read-only)

| Endpoint | VB6 / SQL source |
| --- | --- |
| `GET /api/membership/search?by=number\|name\|ic\|policy&q=` | `frmSearchMBM05` / `Search_MBM_05` columns |
| `GET /api/membership/{mbmNumber}` | `SearchMBMCovPersonsProc`, `PLN`, `AnnualLimit`, `MBMCLNOthers`, `MBMBnfLmt` |
| `GET /api/membership/{mbmNumber}/adjustments` | `MBMAmendHistory` |
| `GET /api/membership/{mbmNumber}/cases` | `SearchCas` |
| `GET /api/membership/{mbmNumber}/case-history` | `SearchCasHistory` |
| `GET /api/membership/{mbmNumber}/member-history` | `SearchMBMHistory06` (rewritten with parameters) |
| `GET /api/membership/{mbmNumber}/account` | `VIEW_MBMTotal_Summary` |
| `GET /api/membership/{mbmNumber}/notes` | `View_Get_MemberExclusion`, `View_Get_MemberRemarks` |

All SQL is parameterised. Stored procedures that build dynamic SQL from a string argument (`SearchMBMHistory06`,
`MBM_GetExcRemarks`) are not called; their queries are reproduced with parameters instead.

## Single Windows exe (API + UI)

```
.\publish-win.ps1 -UiPath ..\GenisisUI
```

Produces `publish\Genisis\Genisis.Api.exe` with the UI in `wwwroot`. No .NET install is needed on the target PC.

## Tests

```
dotnet test
```
