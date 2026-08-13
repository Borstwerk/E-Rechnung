<#
.SYNOPSIS
    Erzeugt den vollständigen Releasebestand für Windows x64.

.DESCRIPTION
    Dies ist der gemeinsame maßgebliche Paketierungsweg für lokale Builds und
    GitHub Actions. Publish, MSI, portable ZIP und SHA-256-Prüfsummen werden
    zunächst in einem Stagingbereich vollständig geprüft. Erst danach wird
    der Bestand als artifacts/release veröffentlicht.

    Vollständige fachliche Tests bleiben ein vorgelagertes Freigabegate. Der
    Releasebau selbst erzwingt die vorhandenen App-, MSI-, deps-, Lizenz-,
    ZIP- und Prüfsummenprüfungen.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsDirectory = Join-Path $root 'artifacts'
$publishDirectory = Join-Path $artifactsDirectory 'publish' 'win-x64'
$releaseDirectory = Join-Path $artifactsDirectory 'release'
$stagingDirectory = Join-Path $artifactsDirectory 'release-staging'
$thirdPartySourceDirectory = Join-Path $root 'installer' 'Drittanbieterhinweise'
$msiName = 'BorstWerk-E-Rechnung-Setup.msi'
$zipName = 'BorstWerk-E-Rechnung-portable-win-x64.zip'
$builtMsi = Join-Path $root 'installer' 'EInvoiceSender.Setup' 'bin' 'Release' `
    $msiName

$releaseArtifacts = @($msiName, $zipName)
$releaseSucceeded = $false

if (-not $IsWindows) {
    throw 'Der Releasebau lässt sich nur unter Windows ausführen, weil WiX dort das MSI erzeugt.'
}

Import-Module (Join-Path $PSScriptRoot 'Release-Packaging.psm1') -Force

New-Item -ItemType Directory -Path $artifactsDirectory -Force | Out-Null

# Ein fehlgeschlagener Neubau darf keinen alten Releasebestand als aktuell
# erscheinen lassen. Kann der alte Bestand nicht vollständig entfernt werden,
# wird noch vor Publish und Installerbau abgebrochen.
Remove-DirectoryStrict -Path $releaseDirectory -AllowedRoot $artifactsDirectory
New-CleanDirectory -Path $stagingDirectory -AllowedRoot $artifactsDirectory

try {
    & (Join-Path $PSScriptRoot 'Publish.ps1') -OutputDirectory $publishDirectory

    & (Join-Path $PSScriptRoot 'Build-Installer.ps1') -SkipPublish -Rebuild

    if (-not (Test-Path -LiteralPath $builtMsi -PathType Leaf)) {
        throw "Das geprüfte MSI fehlt: $builtMsi"
    }

    # Dieselbe durch ER-020-LIC-01 festgelegte Quelle wird für die portable
    # Fassung verwendet. Das MSI bezieht sie weiterhin unmittelbar über WiX.
    Copy-ThirdPartyNotices `
        -PublishDirectory $publishDirectory `
        -SourceDirectory $thirdPartySourceDirectory
    Assert-ThirdPartyNotices `
        -PublishDirectory $publishDirectory `
        -SourceDirectory $thirdPartySourceDirectory

    $stagedZip = Join-Path $stagingDirectory $zipName
    New-PortableArchive -PublishDirectory $publishDirectory -DestinationPath $stagedZip
    Assert-PortableArchive -ArchivePath $stagedZip -PublishDirectory $publishDirectory

    Copy-Item -LiteralPath $builtMsi -Destination (Join-Path $stagingDirectory $msiName) `
        -ErrorAction Stop

    Assert-ExactTopLevelFiles -Directory $stagingDirectory -ExpectedNames $releaseArtifacts
    Write-ReleaseChecksums -Directory $stagingDirectory -ArtifactNames $releaseArtifacts
    Assert-ReleaseChecksums -Directory $stagingDirectory -ArtifactNames $releaseArtifacts

    Move-Item -LiteralPath $stagingDirectory -Destination $releaseDirectory -ErrorAction Stop
    Assert-ReleaseChecksums -Directory $releaseDirectory -ArtifactNames $releaseArtifacts
    $releaseSucceeded = $true
}
catch {
    $releaseError = $_
    $cleanupErrors = @()
    foreach ($path in @($stagingDirectory, $releaseDirectory)) {
        if (Test-Path -LiteralPath $path) {
            try {
                Remove-DirectoryStrict -Path $path -AllowedRoot $artifactsDirectory
            }
            catch {
                $cleanupErrors += $_.Exception.Message
            }
        }
    }

    if ($cleanupErrors.Count -ne 0) {
        throw "Der Releasebau ist fehlgeschlagen: $($releaseError.Exception.Message) " +
            "Zusätzlich konnten unvollständige Arbeitsdateien nicht entfernt werden: " +
            ($cleanupErrors -join ' | ')
    }

    throw $releaseError
}

if (-not $releaseSucceeded) {
    throw 'Der Releasebau endete ohne erfolgreich geprüften Releasebestand.'
}

Write-Host "Release erfolgreich: $releaseDirectory" -ForegroundColor Green
Get-ChildItem -LiteralPath $releaseDirectory -File | Sort-Object Name |
    ForEach-Object { Write-Host "  $($_.Name) ($($_.Length) Bytes)" }
Write-Host 'Prüfsummen erfolgreich verifiziert:' -ForegroundColor Green
Get-Content -LiteralPath (Join-Path $releaseDirectory 'SHA256SUMS.txt')
