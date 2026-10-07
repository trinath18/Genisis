# Builds a self-contained Windows build of Genisis (API + React UI) into .\publish\Genisis.
# Usage:  .\publish-win.ps1 -UiPath ..\GenisisUI
param([string]$UiPath = "..\GenisisUI")
$ErrorActionPreference = "Stop"

if (-not (Test-Path (Join-Path $UiPath "package.json"))) {
    throw "GenisisUI not found at '$UiPath'. Clone https://github.com/trinath18/GenisisUI and pass -UiPath <folder>."
}

Push-Location $UiPath
try { npm ci; npm run build } finally { Pop-Location }

$out = Join-Path $PSScriptRoot "publish\Genisis"
dotnet publish src/Genisis.Api -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $out
New-Item -ItemType Directory -Force (Join-Path $out "wwwroot") | Out-Null
Copy-Item -Recurse -Force (Join-Path $UiPath "dist\*") (Join-Path $out "wwwroot")

Write-Host "Done. Run $out\Genisis.Api.exe then open http://localhost:5000"
