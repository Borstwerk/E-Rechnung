<#
.SYNOPSIS
    Veröffentlicht die Anwendung frisch, baut daraus das MSI und prüft es.

.DESCRIPTION
    Dieses Skript veröffentlicht bei **jedem** Aufruf neu. Einen Schalter, um
    den Publish zu überspringen, gibt es bewusst nicht mehr: Am 25.08.2026
    entstand auf diesem Weg ein MSI mit der Produktversion 0.2.0, dessen
    Programmbestand aus einem älteren Quellstand stammte. Ein Build, der sich
    bei der Frische seiner Eingaben nicht sicher ist, muss abbrechen – oder,
    wie hier, die Unsicherheit gar nicht erst zulassen.

    Publish.ps1 löscht seinen Zielordner vor dem Veröffentlichen vollständig.
    Damit kann kein liegengebliebener Bestand ins Paket geraten.

    Das Skript paketiert weder das portable ZIP noch den Releaseordner. Der
    vollständige gemeinsame Releaseweg liegt in Build-Release.ps1.

    WiX erzeugt MSI-Dateien nur unter Windows.
#>
[CmdletBinding()]
param(
    # Zielverzeichnis der Veröffentlichung. Build-Release.ps1 gibt hier
    # denselben Pfad vor, den es anschließend für ZIP und Prüfsummen
    # verwendet – so ist per Konstruktion dasselbe Verzeichnis gemeint und
    # nicht nur zufällig dasselbe berechnet worden.
    [string]$PublishDirectory,

    [switch]$Rebuild
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    $PublishDirectory = Join-Path $root 'artifacts' 'publish' 'win-x64'
}
$setupProject = Join-Path $root 'installer' 'EInvoiceSender.Setup' 'EInvoiceSender.Setup.wixproj'
$msi = Join-Path $root 'installer' 'EInvoiceSender.Setup' 'bin' 'Release' `
    'BorstWerk-E-Rechnung-Setup.msi'

if (-not $IsWindows) {
    throw "Der Installer lässt sich nur unter Windows bauen: WiX erzeugt MSI-Dateien nur dort."
}

& (Join-Path $PSScriptRoot 'Publish.ps1') -OutputDirectory $PublishDirectory

if (-not (Test-Path (Join-Path $PublishDirectory 'EInvoiceSender.exe') -PathType Leaf)) {
    throw "Die veröffentlichte Anwendung fehlt: $PublishDirectory\EInvoiceSender.exe"
}

Write-Host "MSI wird gebaut ..." -ForegroundColor Cyan

# BorstWerkInstallerBuild ist der Nachweis, dass der Bau über diesen Weg
# läuft. Ohne ihn bricht das WiX-Projekt sofort ab, damit ein direkter Aufruf
# von dotnet build oder ein Bau aus Visual Studio heraus keinen alten
# Publish-Bestand paketieren kann.
$buildArguments = @(
    'build',
    $setupProject,
    '-c',
    'Release',
    '-p:BorstWerkInstallerBuild=true',
    "-p:PublishDir=$PublishDirectory\"
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
    -ApplicationPath (Join-Path $PublishDirectory 'EInvoiceSender.exe')

Write-Host "MSI erfolgreich gebaut und geprüft: $msi" -ForegroundColor Green
