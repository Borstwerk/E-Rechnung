<#
.SYNOPSIS
    Veroeffentlicht die Anwendung als eigenstaendiges Paket fuer win-x64.

.DESCRIPTION
    Das Ergebnis enthaelt die .NET-Laufzeit und laeuft ohne vorherige
    Installation eines Frameworks. Es ist die Grundlage fuer den Installer
    und fuer die tragbare ZIP-Fassung.
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

Write-Host "Anwendung wird veroeffentlicht nach $OutputDirectory ..." -ForegroundColor Cyan
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=true `
    -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw "Das Veroeffentlichen ist fehlgeschlagen." }

$exe = Join-Path $OutputDirectory 'EInvoiceSender.exe'
if (-not (Test-Path $exe)) { throw "EInvoiceSender.exe fehlt im Veroeffentlichungsordner." }

Write-Host "Veroeffentlichung erfolgreich: $exe" -ForegroundColor Green
