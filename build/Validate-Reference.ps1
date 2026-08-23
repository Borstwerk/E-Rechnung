<#
.SYNOPSIS
    Prüft die Golden Master mit den externen Referenzwerkzeugen.

.DESCRIPTION
    Geprüft wird gegen das offizielle CEN-Schematron und veraPDF, beide über
    die Mustangproject-CLI. Beides läuft vollständig örtlich; es werden
    keine Rechnungsdaten übertragen.

    Zwei Aussagen müssen zutreffen:
      - alle als gültig geführten Golden Master werden als gültig bestätigt,
      - alle absichtlich fehlerhaften Dateien werden beanstandet.

    Fehlt Java oder die Mustang-JAR, sagt das Skript genau das - und meldet
    einen Fehler, statt Stillschweigen als Erfolg auszugeben.
#>
[CmdletBinding()]
param(
    [string]$ReportDirectory = (Join-Path $PSScriptRoot '..' 'artifacts' 'validation')
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

$java = Get-Command java -ErrorAction SilentlyContinue
if (-not $java) {
    throw "Java wurde nicht gefunden. Die Referenzwerkzeuge brauchen eine Java-Laufzeit (Version 17 oder neuer). Die Anwendung selbst braucht kein Java."
}

$jar = Get-ChildItem -Path (Join-Path $root 'tools' 'mustang') -Filter '*.jar' -ErrorAction SilentlyContinue |
       Sort-Object Name | Select-Object -Last 1
if (-not $jar) {
    throw "Die Mustang-CLI wurde nicht gefunden. Holen Sie sie mit build/fetch-validators.sh nach tools/mustang/."
}

New-Item -ItemType Directory -Force -Path $ReportDirectory | Out-Null

Write-Host "Referenzprüfung mit $($jar.Name) ..." -ForegroundColor Cyan

$env:REQUIRE_EXTERNAL_VALIDATORS = '1'
dotnet test (Join-Path $root 'tests' 'EInvoiceSender.IntegrationTests') -c Release `
    --logger "trx;LogFileName=referenzprüfung.trx" `
    --results-directory $ReportDirectory
if ($LASTEXITCODE -ne 0) { throw "Die Referenzprüfung ist fehlgeschlagen. Bericht: $ReportDirectory" }

Write-Host "Referenzprüfung bestanden. Bericht: $ReportDirectory" -ForegroundColor Green
