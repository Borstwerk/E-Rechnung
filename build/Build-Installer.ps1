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

# Das einzige Verzeichnis, in das dieses Skript veröffentlichen darf.
$canonicalPublishDirectory = [IO.Path]::GetFullPath(
    (Join-Path $root 'artifacts' 'publish' 'win-x64'))

if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    $PublishDirectory = $canonicalPublishDirectory
}

# Publish.ps1 löscht sein Zielverzeichnis vor dem Veröffentlichen rekursiv.
# Ein von außen frei wählbarer Pfad wäre damit ein frei adressierbarer
# Löschbefehl im normalen Installer-Einstieg – ein Tippfehler oder ein falsch
# gesetzter Parameter genügte. Der Parameter existiert nur, damit
# Build-Release.ps1 dasselbe Verzeichnis benennen kann, das es anschließend
# weiterverwendet; er ist keine Einladung, woanders hin zu veröffentlichen.
#
# Verglichen wird vollständig normalisiert, damit weder ".." noch gemischte
# Trennzeichen noch ein angehängter Schrägstrich daran vorbeikommen.
#
# Diese Prüfung steht bewusst **vor** der Windows-Prüfung: Ein falscher Pfad
# ist ein falscher Pfad, gleich auf welchem System – und so lässt sich die
# Sperre auch dort nachweisen, wo gar kein MSI entstehen kann.
$requestedPublishDirectory = [IO.Path]::GetFullPath($PublishDirectory)
$separators = [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)

if (-not [string]::Equals(
        $requestedPublishDirectory.TrimEnd($separators),
        $canonicalPublishDirectory.TrimEnd($separators),
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unzulässiges Veröffentlichungsverzeichnis: $requestedPublishDirectory. " +
        "Zulässig ist ausschließlich $canonicalPublishDirectory. Der Zielordner wird " +
        "vor dem Veröffentlichen vollständig gelöscht; ein anderer Pfad wird deshalb " +
        "abgewiesen, bevor irgendetwas entfernt wird."
}

# Ab hier gilt der geprüfte, normalisierte Pfad.
$PublishDirectory = $canonicalPublishDirectory

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
