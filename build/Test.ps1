<#
.SYNOPSIS
    Führt alle Tests aus.

.DESCRIPTION
    Ohne Schalter laufen die Tests, die keine externen Werkzeuge brauchen;
    die Prüfungen gegen CEN-Schematron und veraPDF werden dann
    übersprungen.

    Mit -RequireExternalValidators scheitern diese Tests, statt übersprungen
    zu werden. So läuft es in der Pipeline: In einer Freigabe darf das
    Prüfgate nicht stillschweigend entfallen.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$RequireExternalValidators
)

$ErrorActionPreference = 'Stop'
$solution = Join-Path $PSScriptRoot '..' 'EInvoiceSender.sln'

if ($RequireExternalValidators) {
    $env:REQUIRE_EXTERNAL_VALIDATORS = '1'
    Write-Host "Externe Referenzvalidatoren sind verpflichtend." -ForegroundColor Cyan
}
else {
    Remove-Item Env:\REQUIRE_EXTERNAL_VALIDATORS -ErrorAction SilentlyContinue
    Write-Host "Fehlende externe Werkzeuge führen zum Überspringen, nicht zum Fehler." -ForegroundColor Yellow
}

dotnet test $solution -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Mindestens ein Test ist fehlgeschlagen." }

Write-Host "Alle Tests erfolgreich." -ForegroundColor Green
