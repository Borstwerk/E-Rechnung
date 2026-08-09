<#
.SYNOPSIS
    Veröffentlicht die Anwendung als eigenständiges Paket für win-x64.

.DESCRIPTION
    Das Ergebnis enthält die .NET-Laufzeit und läuft ohne vorherige
    Installation eines Frameworks. Es ist die Grundlage für den Installer
    und für die tragbare ZIP-Fassung.
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..' 'artifacts' 'publish' 'win-x64')
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..' 'src' 'EInvoiceSender.App' 'EInvoiceSender.App.csproj'

if (Test-Path $OutputDirectory) {
    Remove-Item $OutputDirectory -Recurse -Force
}

Write-Host "Anwendung wird veröffentlicht nach $OutputDirectory ..." -ForegroundColor Cyan
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=true `
    -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw "Das Veröffentlichen ist fehlgeschlagen." }

$exe = Join-Path $OutputDirectory 'EInvoiceSender.exe'
if (-not (Test-Path $exe)) { throw "EInvoiceSender.exe fehlt im Veröffentlichungsordner." }

Write-Host "Veröffentlichung erfolgreich: $exe" -ForegroundColor Green
