<#
.SYNOPSIS
    Prüft die fail-clean Paketierungs- und Releaseartefaktfunktionen.

.DESCRIPTION
    Verwendet ausschließlich temporäre Testdateien. Es werden weder ein echter
    Publish noch das MSI gebaut und artifacts/release bleibt unberührt.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Release-Packaging.psm1') -Force

$testRoot = Join-Path ([IO.Path]::GetTempPath()) `
    "BorstWerk-ReleasePackaging-$([Guid]::NewGuid().ToString('N'))"
$script:passed = 0

function Assert-True {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Throws {
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Action,

        [Parameter(Mandatory)]
        [string]$ExpectedMessage
    )

    try {
        & $Action
    }
    catch {
        if (-not $_.Exception.Message.Contains($ExpectedMessage, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Erwartete Fehlermeldung mit '$ExpectedMessage', erhalten: $($_.Exception.Message)"
        }
        return
    }

    throw "Die erwartete Ausnahme mit '$ExpectedMessage' ist ausgeblieben."
}

function Invoke-PackagingTest {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [scriptblock]$Action
    )

    & $Action
    $script:passed++
    Write-Host "[BESTANDEN] $Name" -ForegroundColor Green
}

function New-DummyReleaseArtifacts {
    param(
        [Parameter(Mandatory)]
        [string]$Directory
    )

    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $Directory 'BorstWerk-E-Rechnung-Setup.msi'), [byte[]](1, 2, 3))
    [IO.File]::WriteAllBytes(
        (Join-Path $Directory 'BorstWerk-E-Rechnung-portable-win-x64.zip'),
        [byte[]](4, 5, 6))
}

try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null

    Invoke-PackagingTest 'Schmutziger Releaseordner wird vollständig bereinigt' {
        $directory = Join-Path $testRoot 'dirty-release'
        New-Item -ItemType Directory -Path (Join-Path $directory 'alt') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $directory 'alt.txt') -Value 'alt'
        Set-Content -LiteralPath (Join-Path $directory 'alt' 'alt.bin') -Value 'alt'

        New-CleanDirectory -Path $directory -AllowedRoot $testRoot

        Assert-True -Condition (Test-Path -LiteralPath $directory -PathType Container) `
            -Message 'Der bereinigte Ordner fehlt.'
        Assert-True -Condition (@(Get-ChildItem -LiteralPath $directory -Force).Count -eq 0) `
            -Message 'Der bereinigte Ordner ist nicht leer.'
    }

    if ($IsWindows) {
        Invoke-PackagingTest 'Gesperrte Datei lässt die Bereinigung abbrechen' {
            $directory = Join-Path $testRoot 'locked-release'
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
            $file = Join-Path $directory 'gesperrt.bin'
            Set-Content -LiteralPath $file -Value 'gesperrt'
            $stream = [IO.File]::Open($file, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            try {
                Assert-Throws -ExpectedMessage 'gesperrt.bin' -Action {
                    Remove-DirectoryStrict -Path $directory -AllowedRoot $testRoot
                }
            }
            finally {
                $stream.Dispose()
                if (Test-Path -LiteralPath $directory) {
                    Remove-Item -LiteralPath $directory -Recurse -Force
                }
            }
        }
    }

    # -----------------------------------------------------------------------
    # Build-Installer.ps1 darf nur in das Veröffentlichungsverzeichnis des
    # Repositorys schreiben.
    #
    # Publish.ps1 löscht sein Zielverzeichnis rekursiv. Der Parameter
    # -PublishDirectory existiert nur, damit Build-Release.ps1 dasselbe
    # Verzeichnis benennen kann, das es anschließend weiterverwendet – ohne
    # Sperre wäre er ein frei adressierbarer Löschbefehl im normalen
    # Installer-Einstieg.
    #
    # Der Nachweis ist bewusst empirisch: Eine Sentinel-Datei im fremden
    # Ordner muss den Aufruf überleben. Dass irgendwo eine Prüfung im
    # Quelltext steht, sagt darüber nichts.
    # -----------------------------------------------------------------------
    Invoke-PackagingTest 'Fremdes Veröffentlichungsverzeichnis wird abgewiesen, bevor gelöscht wird' {
        $foreign = Join-Path $testRoot 'fremdes-verzeichnis'
        New-Item -ItemType Directory -Path $foreign -Force | Out-Null
        $sentinel = Join-Path $foreign 'nicht-loeschen.txt'
        Set-Content -LiteralPath $sentinel -Value 'Diese Datei muss den Aufruf überleben.'

        Assert-Throws -ExpectedMessage 'Unzulässiges Veröffentlichungsverzeichnis' -Action {
            & (Join-Path $PSScriptRoot 'Build-Installer.ps1') -PublishDirectory $foreign
        }

        $sentinelMessage = "Die Sentinel-Datei wurde entfernt: $sentinel. Der Aufruf hat " +
            'den fremden Ordner angetastet, statt vorher abzubrechen.'

        Assert-True -Condition (Test-Path -LiteralPath $sentinel -PathType Leaf) `
            -Message $sentinelMessage
        Assert-True -Condition (Test-Path -LiteralPath $foreign -PathType Container) `
            -Message "Der fremde Ordner wurde entfernt: $foreign."
    }

    # Dieselbe Sperre gegen einen Pfad, der erst nach dem Auflösen von ".."
    # aus dem Repository herausführt. Ein reiner Zeichenkettenvergleich ginge
    # hier vorbei.
    Invoke-PackagingTest 'Verzeichniswechsel über ".." wird ebenfalls abgewiesen' {
        $repositoryRoot = Split-Path -Parent $PSScriptRoot
        $traversal = Join-Path $repositoryRoot 'artifacts' 'publish' 'win-x64' '..' '..' '..'

        Assert-Throws -ExpectedMessage 'Unzulässiges Veröffentlichungsverzeichnis' -Action {
            & (Join-Path $PSScriptRoot 'Build-Installer.ps1') -PublishDirectory $traversal
        }
    }

    Invoke-PackagingTest 'Fehlendes MSI wird abgewiesen' {
        $directory = Join-Path $testRoot 'missing-msi'
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $directory 'BorstWerk-E-Rechnung-portable-win-x64.zip') -Value 'zip'
        Assert-Throws -ExpectedMessage 'BorstWerk-E-Rechnung-Setup.msi' -Action {
            Assert-ExactTopLevelFiles -Directory $directory -ExpectedNames @(
                'BorstWerk-E-Rechnung-Setup.msi',
                'BorstWerk-E-Rechnung-portable-win-x64.zip')
        }
    }

    Invoke-PackagingTest 'Fehlendes ZIP wird abgewiesen' {
        $directory = Join-Path $testRoot 'missing-zip'
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $directory 'BorstWerk-E-Rechnung-Setup.msi') -Value 'msi'
        Assert-Throws -ExpectedMessage 'BorstWerk-E-Rechnung-portable-win-x64.zip' -Action {
            Assert-ExactTopLevelFiles -Directory $directory -ExpectedNames @(
                'BorstWerk-E-Rechnung-Setup.msi',
                'BorstWerk-E-Rechnung-portable-win-x64.zip')
        }
    }

    Invoke-PackagingTest 'Unerwartete Release-Datei wird abgewiesen' {
        $directory = Join-Path $testRoot 'unexpected-file'
        New-DummyReleaseArtifacts -Directory $directory
        Set-Content -LiteralPath (Join-Path $directory 'alt-0.1.0.msi') -Value 'alt'
        Assert-Throws -ExpectedMessage 'alt-0.1.0.msi' -Action {
            Assert-ExactTopLevelFiles -Directory $directory -ExpectedNames @(
                'BorstWerk-E-Rechnung-Setup.msi',
                'BorstWerk-E-Rechnung-portable-win-x64.zip')
        }
    }

    Invoke-PackagingTest 'Drittanbieterhinweise und Lizenzordner werden vollständig übernommen' {
        $source = Join-Path $testRoot 'notice-source'
        $licenses = Join-Path $source 'Lizenzen'
        $nested = Join-Path $licenses 'Native'
        $publish = Join-Path $testRoot 'notice-publish'
        New-Item -ItemType Directory -Path $nested -Force | Out-Null
        New-Item -ItemType Directory -Path $publish -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $source 'README.md') -Value 'Hinweise'
        Set-Content -LiteralPath (Join-Path $licenses 'LICENSE.txt') -Value 'Lizenz'
        Set-Content -LiteralPath (Join-Path $nested 'NOTICE.txt') -Value 'Notice'
        Set-Content -LiteralPath (Join-Path $publish 'EInvoiceSender.exe') -Value 'App'

        Copy-ThirdPartyNotices -PublishDirectory $publish -SourceDirectory $source
        Assert-ThirdPartyNotices -PublishDirectory $publish -SourceDirectory $source
    }

    Invoke-PackagingTest 'Fehlende Lizenzdatei im portablen Bestand wird abgewiesen' {
        $source = Join-Path $testRoot 'notice-source'
        $publish = Join-Path $testRoot 'notice-publish'
        Remove-Item -LiteralPath (Join-Path $publish 'Drittanbieterhinweise' 'Lizenzen' 'LICENSE.txt')
        Assert-Throws -ExpectedMessage 'LICENSE.txt' -Action {
            Assert-ThirdPartyNotices -PublishDirectory $publish -SourceDirectory $source
        }
    }

    Invoke-PackagingTest 'ZIP entspricht exakt dem portablen Bestand an der Wurzel' {
        $source = Join-Path $testRoot 'notice-source'
        $publish = Join-Path $testRoot 'archive-publish'
        $archiveDirectory = Join-Path $testRoot 'archive-output'
        New-Item -ItemType Directory -Path $publish -Force | Out-Null
        New-Item -ItemType Directory -Path $archiveDirectory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $publish 'EInvoiceSender.exe') -Value 'App'
        Copy-ThirdPartyNotices -PublishDirectory $publish -SourceDirectory $source
        $archive = Join-Path $archiveDirectory 'portable.zip'

        New-PortableArchive -PublishDirectory $publish -DestinationPath $archive
        Assert-PortableArchive -ArchivePath $archive -PublishDirectory $publish

        $zip = [IO.Compression.ZipFile]::OpenRead($archive)
        try {
            Assert-True -Condition ($null -ne ($zip.GetEntry('EInvoiceSender.exe'))) `
                -Message 'Die Anwendung liegt nicht direkt an der ZIP-Wurzel.'
            Assert-True -Condition ($null -ne ($zip.GetEntry('Drittanbieterhinweise.md'))) `
                -Message 'Drittanbieterhinweise.md fehlt an der ZIP-Wurzel.'
        }
        finally {
            $zip.Dispose()
        }
    }

    Invoke-PackagingTest 'SHA256SUMS enthält exakt MSI und ZIP in definierter Reihenfolge' {
        $directory = Join-Path $testRoot 'checksums-valid'
        New-DummyReleaseArtifacts -Directory $directory
        $names = @(
            'BorstWerk-E-Rechnung-Setup.msi',
            'BorstWerk-E-Rechnung-portable-win-x64.zip')

        Write-ReleaseChecksums -Directory $directory -ArtifactNames $names
        Assert-ReleaseChecksums -Directory $directory -ArtifactNames $names

        $lines = [IO.File]::ReadAllLines((Join-Path $directory 'SHA256SUMS.txt'))
        Assert-True -Condition ($lines.Count -eq 2) -Message 'SHA256SUMS.txt enthält nicht genau zwei Zeilen.'
        Assert-True -Condition ($lines[0].EndsWith("  $($names[0])", [StringComparison]::Ordinal)) `
            -Message 'Das MSI steht nicht an erster Stelle.'
        Assert-True -Condition ($lines[1].EndsWith("  $($names[1])", [StringComparison]::Ordinal)) `
            -Message 'Das ZIP steht nicht an zweiter Stelle.'
        Assert-True -Condition (-not ($lines -match 'SHA256SUMS.txt')) `
            -Message 'SHA256SUMS.txt enthält sich selbst.'
    }

    Invoke-PackagingTest 'Manipuliertes MSI lässt die SHA-Verifikation scheitern' {
        $directory = Join-Path $testRoot 'checksums-tampered-msi'
        New-DummyReleaseArtifacts -Directory $directory
        $names = @(
            'BorstWerk-E-Rechnung-Setup.msi',
            'BorstWerk-E-Rechnung-portable-win-x64.zip')
        Write-ReleaseChecksums -Directory $directory -ArtifactNames $names
        Add-Content -LiteralPath (Join-Path $directory $names[0]) -Value 'manipuliert'

        Assert-Throws -ExpectedMessage 'stimmt nicht' -Action {
            Assert-ReleaseChecksums -Directory $directory -ArtifactNames $names
        }
    }

    Invoke-PackagingTest 'Manipuliertes ZIP lässt die SHA-Verifikation scheitern' {
        $directory = Join-Path $testRoot 'checksums-tampered-zip'
        New-DummyReleaseArtifacts -Directory $directory
        $names = @(
            'BorstWerk-E-Rechnung-Setup.msi',
            'BorstWerk-E-Rechnung-portable-win-x64.zip')
        Write-ReleaseChecksums -Directory $directory -ArtifactNames $names
        Add-Content -LiteralPath (Join-Path $directory $names[1]) -Value 'manipuliert'

        Assert-Throws -ExpectedMessage 'stimmt nicht' -Action {
            Assert-ReleaseChecksums -Directory $directory -ArtifactNames $names
        }
    }

    Write-Host "$script:passed Paketierungstests erfolgreich." -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
