<#
.SYNOPSIS
    Baut das MSI-Paket und legt es mit Prüfsumme im Release-Ordner ab.

.DESCRIPTION
    Der Installer setzt eine fertige Veröffentlichung voraus. Fehlt sie oder
    ist sie älter als der Quelltext, wird Publish.ps1 vorher ausgeführt.

    WiX erzeugt MSI-Dateien nur unter Windows.
#>
[CmdletBinding()]
param(
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$publishDirectory = Join-Path $root 'artifacts' 'publish' 'win-x64'
$releaseDirectory = Join-Path $root 'artifacts' 'release'
$setupProject = Join-Path $root 'installer' 'EInvoiceSender.Setup' 'EInvoiceSender.Setup.wixproj'

if (-not $IsWindows) {
    throw "Der Installer lässt sich nur unter Windows bauen: WiX erzeugt MSI-Dateien nur dort."
}

if (-not $SkipPublish -or -not (Test-Path (Join-Path $publishDirectory 'EInvoiceSender.exe'))) {
    & (Join-Path $PSScriptRoot 'Publish.ps1') -OutputDirectory $publishDirectory
}

Write-Host "MSI wird gebaut ..." -ForegroundColor Cyan
dotnet build $setupProject -c Release -p:PublishDir="$publishDirectory\"
if ($LASTEXITCODE -ne 0) { throw "Der Installerbau ist fehlgeschlagen." }

$msi = Get-ChildItem -Path (Join-Path $root 'installer') -Filter '*.msi' -Recurse |
       Sort-Object LastWriteTime -Descending |
       Select-Object -First 1
if (-not $msi) { throw "Es wurde keine MSI-Datei gefunden." }

& (Join-Path $PSScriptRoot 'Test-InstallerMetadata.ps1') -MsiPath $msi.FullName

New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null

$target = Join-Path $releaseDirectory $msi.Name
Copy-Item $msi.FullName $target -Force

# Tragbare Fassung als ZIP, für Anwender ohne Installationsrechte.
$zip = Join-Path $releaseDirectory 'BorstWerk-E-Rechnung-portable-win-x64.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zip

# Prüfsummen über alle Artefakte, damit sich die Auslieferung nachweisen lässt.
$checksums = Join-Path $releaseDirectory 'SHA256SUMS.txt'
Get-ChildItem $releaseDirectory -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
} | Set-Content -Path $checksums -Encoding utf8

Write-Host "Fertig. Artefakte in $releaseDirectory" -ForegroundColor Green
Get-Content $checksums
