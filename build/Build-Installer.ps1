<#
.SYNOPSIS
    Baut und prüft das MSI-Paket.

.DESCRIPTION
    Der Installer setzt eine fertige Veröffentlichung voraus. Fehlt sie oder
    wurde -SkipPublish nicht angegeben, wird Publish.ps1 vorher ausgeführt.

    Dieses Skript paketiert weder das portable ZIP noch den Releaseordner.
    Der vollständige gemeinsame Releaseweg liegt in Build-Release.ps1.

    WiX erzeugt MSI-Dateien nur unter Windows.
#>
[CmdletBinding()]
param(
    [switch]$SkipPublish,

    [switch]$Rebuild
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$publishDirectory = Join-Path $root 'artifacts' 'publish' 'win-x64'
$setupProject = Join-Path $root 'installer' 'EInvoiceSender.Setup' 'EInvoiceSender.Setup.wixproj'
$msi = Join-Path $root 'installer' 'EInvoiceSender.Setup' 'bin' 'Release' `
    'BorstWerk-E-Rechnung-Setup.msi'

if (-not $IsWindows) {
    throw "Der Installer lässt sich nur unter Windows bauen: WiX erzeugt MSI-Dateien nur dort."
}

if (-not $SkipPublish -or -not (Test-Path (Join-Path $publishDirectory 'EInvoiceSender.exe'))) {
    & (Join-Path $PSScriptRoot 'Publish.ps1') -OutputDirectory $publishDirectory
}

if (-not (Test-Path (Join-Path $publishDirectory 'EInvoiceSender.exe') -PathType Leaf)) {
    throw "Die veröffentlichte Anwendung fehlt: $publishDirectory\EInvoiceSender.exe"
}

Write-Host "MSI wird gebaut ..." -ForegroundColor Cyan
$buildArguments = @(
    'build',
    $setupProject,
    '-c',
    'Release',
    "-p:PublishDir=$publishDirectory\"
)
if ($Rebuild) {
    $buildArguments += '-t:Rebuild'
}

dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Der Installerbau ist fehlgeschlagen." }

if (-not (Test-Path -LiteralPath $msi -PathType Leaf)) {
    throw "Das erwartete MSI wurde nicht erzeugt: $msi"
}

& (Join-Path $PSScriptRoot 'Test-InstallerMetadata.ps1') `
    -MsiPath $msi `
    -ApplicationPath (Join-Path $publishDirectory 'EInvoiceSender.exe')

Write-Host "MSI erfolgreich gebaut und geprüft: $msi" -ForegroundColor Green
