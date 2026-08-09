<#
.SYNOPSIS
    Stellt die Pakete wieder her und baut die Projektmappe in Release.

.DESCRIPTION
    Der übliche Weg ist Visual Studio. Dieses Skript ist für den Fall
    gedacht, dass ein Build ohne IDE gebraucht wird - etwa vor einem
    Veröffentlichen oder in einer Pipeline.

    Bei einem Fehler endet das Skript mit einem Exitcode ungleich null.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$solution = Join-Path $PSScriptRoot '..' 'EInvoiceSender.sln'

Write-Host "Pakete werden wiederhergestellt ..." -ForegroundColor Cyan
dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw "Das Wiederherstellen der Pakete ist fehlgeschlagen." }

Write-Host "Projektmappe wird gebaut ($Configuration) ..." -ForegroundColor Cyan
dotnet build $solution -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Der Build ist fehlgeschlagen." }

Write-Host "Build erfolgreich." -ForegroundColor Green
