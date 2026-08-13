Set-StrictMode -Version Latest

function Get-GuardedPath {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$AllowedRoot
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullRoot = [IO.Path]::GetFullPath($AllowedRoot).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $rootPrefix = $fullRoot + [IO.Path]::DirectorySeparatorChar

    if ($fullPath.Equals($fullRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Der Pfad '$fullPath' liegt nicht als Unterverzeichnis innerhalb von '$fullRoot'."
    }

    return $fullPath
}

function Remove-DirectoryStrict {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$AllowedRoot
    )

    $guardedPath = Get-GuardedPath -Path $Path -AllowedRoot $AllowedRoot
    if (Test-Path -LiteralPath $guardedPath) {
        Remove-Item -LiteralPath $guardedPath -Recurse -Force -ErrorAction Stop
    }

    if (Test-Path -LiteralPath $guardedPath) {
        throw "Das Verzeichnis '$guardedPath' konnte nicht vollständig entfernt werden."
    }
}

function New-CleanDirectory {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$AllowedRoot
    )

    Remove-DirectoryStrict -Path $Path -AllowedRoot $AllowedRoot
    $guardedPath = Get-GuardedPath -Path $Path -AllowedRoot $AllowedRoot
    New-Item -ItemType Directory -Path $guardedPath -ErrorAction Stop | Out-Null

    if (@(Get-ChildItem -LiteralPath $guardedPath -Force -ErrorAction Stop).Count -ne 0) {
        throw "Das neu angelegte Verzeichnis '$guardedPath' ist nicht leer."
    }
}

function Assert-ExactTopLevelFiles {
    param(
        [Parameter(Mandatory)]
        [string]$Directory,

        [Parameter(Mandatory)]
        [string[]]$ExpectedNames
    )

    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) {
        throw "Das erwartete Verzeichnis fehlt: $Directory"
    }

    $duplicateNames = @($ExpectedNames | Group-Object | Where-Object Count -ne 1)
    if ($duplicateNames.Count -ne 0) {
        throw "Der erwartete Dateisatz enthält doppelte Namen: $($duplicateNames.Name -join ', ')."
    }

    $items = @(Get-ChildItem -LiteralPath $Directory -Force -ErrorAction Stop)
    $directories = @($items | Where-Object PSIsContainer | ForEach-Object Name | Sort-Object)
    $actualNames = @($items | Where-Object { -not $_.PSIsContainer } | ForEach-Object Name)
    $missing = @($ExpectedNames | Where-Object { $_ -notin $actualNames })
    $unexpected = @($actualNames | Where-Object { $_ -notin $ExpectedNames } | Sort-Object)

    if ($directories.Count -ne 0 -or $missing.Count -ne 0 -or $unexpected.Count -ne 0) {
        throw "Der Dateisatz in '$Directory' weicht ab. " +
            "Fehlt: $($missing -join ', '); unerwartet: $($unexpected -join ', '); " +
            "Unterverzeichnisse: $($directories -join ', ')."
    }
}

function Get-RelativeFileManifest {
    param(
        [Parameter(Mandatory)]
        [string]$Directory
    )

    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) {
        throw "Das erwartete Verzeichnis fehlt: $Directory"
    }

    return @(
        Get-ChildItem -LiteralPath $Directory -Recurse -File -Force -ErrorAction Stop |
            ForEach-Object {
                [pscustomobject]@{
                    Path = [IO.Path]::GetRelativePath($Directory, $_.FullName).Replace('\', '/')
                    Length = $_.Length
                    Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
                }
            } |
            Sort-Object Path
    )
}

function Assert-ManifestsEqual {
    param(
        [Parameter(Mandatory)]
        [object[]]$Actual,

        [Parameter(Mandatory)]
        [object[]]$Expected,

        [Parameter(Mandatory)]
        [string]$Name
    )

    $actualByPath = @{}
    foreach ($entry in $Actual) {
        if ($actualByPath.ContainsKey($entry.Path)) {
            throw "$Name enthält den Pfad '$($entry.Path)' mehrfach."
        }
        $actualByPath[$entry.Path] = $entry
    }

    $expectedByPath = @{}
    foreach ($entry in $Expected) {
        if ($expectedByPath.ContainsKey($entry.Path)) {
            throw "Der erwartete Bestand für $Name enthält den Pfad '$($entry.Path)' mehrfach."
        }
        $expectedByPath[$entry.Path] = $entry
    }

    $missing = @($expectedByPath.Keys | Where-Object { -not $actualByPath.ContainsKey($_) } | Sort-Object)
    $unexpected = @($actualByPath.Keys | Where-Object { -not $expectedByPath.ContainsKey($_) } | Sort-Object)
    if ($missing.Count -ne 0 -or $unexpected.Count -ne 0) {
        throw "$Name weicht ab. Fehlt: $($missing -join ', '); unerwartet: $($unexpected -join ', ')."
    }

    foreach ($path in $expectedByPath.Keys) {
        $actualEntry = $actualByPath[$path]
        $expectedEntry = $expectedByPath[$path]
        if ($actualEntry.Length -ne $expectedEntry.Length -or $actualEntry.Hash -ne $expectedEntry.Hash) {
            throw "$Name enthält für '$path' nicht denselben geprüften Inhalt."
        }
    }
}

function Assert-ThirdPartyNotices {
    param(
        [Parameter(Mandatory)]
        [string]$PublishDirectory,

        [Parameter(Mandatory)]
        [string]$SourceDirectory
    )

    $sourceOverview = Join-Path $SourceDirectory 'README.md'
    $sourceLicenses = Join-Path $SourceDirectory 'Lizenzen'
    $targetOverview = Join-Path $PublishDirectory 'Drittanbieterhinweise.md'
    $targetNoticeDirectory = Join-Path $PublishDirectory 'Drittanbieterhinweise'
    $targetLicenses = Join-Path $targetNoticeDirectory 'Lizenzen'

    foreach ($file in @($sourceOverview, $targetOverview)) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            throw "Die verpflichtende Drittanbieterübersicht fehlt: $file"
        }
    }

    $noticeItems = @(Get-ChildItem -LiteralPath $targetNoticeDirectory -Force -ErrorAction Stop)
    if ($noticeItems.Count -ne 1 -or -not $noticeItems[0].PSIsContainer -or
        $noticeItems[0].Name -ne 'Lizenzen') {
        throw "Der portable Hinweisordner darf ausschließlich das Unterverzeichnis 'Lizenzen' enthalten."
    }

    $sourceOverviewHash = (Get-FileHash -LiteralPath $sourceOverview -Algorithm SHA256).Hash
    $targetOverviewHash = (Get-FileHash -LiteralPath $targetOverview -Algorithm SHA256).Hash
    if ($sourceOverviewHash -ne $targetOverviewHash) {
        throw 'Drittanbieterhinweise.md entspricht nicht der festgelegten Anwenderquelle.'
    }

    $sourceManifest = @(Get-RelativeFileManifest -Directory $sourceLicenses)
    $targetManifest = @(Get-RelativeFileManifest -Directory $targetLicenses)
    Assert-ManifestsEqual -Actual $targetManifest -Expected $sourceManifest `
        -Name 'Der portable Lizenz-/Notice-Bestand'
}

function Copy-ThirdPartyNotices {
    param(
        [Parameter(Mandatory)]
        [string]$PublishDirectory,

        [Parameter(Mandatory)]
        [string]$SourceDirectory
    )

    if (-not (Test-Path -LiteralPath $PublishDirectory -PathType Container)) {
        throw "Das Publish-Verzeichnis fehlt: $PublishDirectory"
    }

    $sourceOverview = Join-Path $SourceDirectory 'README.md'
    $sourceLicenses = Join-Path $SourceDirectory 'Lizenzen'
    if (-not (Test-Path -LiteralPath $sourceOverview -PathType Leaf)) {
        throw "Die festgelegte Drittanbieterübersicht fehlt: $sourceOverview"
    }
    if (-not (Test-Path -LiteralPath $sourceLicenses -PathType Container)) {
        throw "Der festgelegte Lizenz-/Notice-Ordner fehlt: $sourceLicenses"
    }

    $targetOverview = Join-Path $PublishDirectory 'Drittanbieterhinweise.md'
    $targetNoticeDirectory = Join-Path $PublishDirectory 'Drittanbieterhinweise'
    if (Test-Path -LiteralPath $targetOverview) {
        throw "Der Publish enthält bereits eine Drittanbieterübersicht: $targetOverview"
    }
    if (Test-Path -LiteralPath $targetNoticeDirectory) {
        throw "Der Publish enthält bereits einen Drittanbieterordner: $targetNoticeDirectory"
    }

    Copy-Item -LiteralPath $sourceOverview -Destination $targetOverview -ErrorAction Stop
    New-Item -ItemType Directory -Path $targetNoticeDirectory -ErrorAction Stop | Out-Null
    Copy-Item -LiteralPath $sourceLicenses -Destination $targetNoticeDirectory `
        -Recurse -ErrorAction Stop

    Assert-ThirdPartyNotices -PublishDirectory $PublishDirectory -SourceDirectory $SourceDirectory
}

function New-PortableArchive {
    param(
        [Parameter(Mandatory)]
        [string]$PublishDirectory,

        [Parameter(Mandatory)]
        [string]$DestinationPath
    )

    if (-not (Test-Path -LiteralPath $PublishDirectory -PathType Container)) {
        throw "Das Publish-Verzeichnis fehlt: $PublishDirectory"
    }
    if (Test-Path -LiteralPath $DestinationPath) {
        throw "Das portable ZIP existiert bereits: $DestinationPath"
    }

    $destinationDirectory = Split-Path -Parent $DestinationPath
    if (-not (Test-Path -LiteralPath $destinationDirectory -PathType Container)) {
        throw "Der Zielordner für das portable ZIP fehlt: $destinationDirectory"
    }

    [IO.Compression.ZipFile]::CreateFromDirectory(
        $PublishDirectory,
        $DestinationPath,
        [IO.Compression.CompressionLevel]::Optimal,
        $false)

    if (-not (Test-Path -LiteralPath $DestinationPath -PathType Leaf)) {
        throw "Das portable ZIP wurde nicht erzeugt: $DestinationPath"
    }
}

function Assert-PortableArchive {
    param(
        [Parameter(Mandatory)]
        [string]$ArchivePath,

        [Parameter(Mandatory)]
        [string]$PublishDirectory
    )

    if (-not (Test-Path -LiteralPath $ArchivePath -PathType Leaf)) {
        throw "Das portable ZIP fehlt: $ArchivePath"
    }

    $expectedManifest = @(Get-RelativeFileManifest -Directory $PublishDirectory)
    $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $actualManifest = @(
            $archive.Entries |
                Where-Object { $_.Name } |
                ForEach-Object {
                    $hashAlgorithm = [Security.Cryptography.SHA256]::Create()
                    $stream = $_.Open()
                    try {
                        $hash = [Convert]::ToHexString($hashAlgorithm.ComputeHash($stream))
                    }
                    finally {
                        $stream.Dispose()
                        $hashAlgorithm.Dispose()
                    }

                    [pscustomobject]@{
                        Path = $_.FullName.Replace('\', '/')
                        Length = $_.Length
                        Hash = $hash
                    }
                } |
                Sort-Object Path
        )
    }
    finally {
        $archive.Dispose()
    }

    Assert-ManifestsEqual -Actual $actualManifest -Expected $expectedManifest `
        -Name 'Der Inhalt des portablen ZIP'
}

function Assert-ReleaseChecksums {
    param(
        [Parameter(Mandatory)]
        [string]$Directory,

        [Parameter(Mandatory)]
        [string[]]$ArtifactNames
    )

    $checksumName = 'SHA256SUMS.txt'
    Assert-ExactTopLevelFiles -Directory $Directory `
        -ExpectedNames @($ArtifactNames + $checksumName)

    $checksumPath = Join-Path $Directory $checksumName
    $lines = [IO.File]::ReadAllLines($checksumPath)
    if ($lines.Count -ne $ArtifactNames.Count) {
        throw "SHA256SUMS.txt enthält $($lines.Count) statt $($ArtifactNames.Count) Zeilen."
    }

    for ($index = 0; $index -lt $ArtifactNames.Count; $index++) {
        $line = $lines[$index]
        if ($line -notmatch '^([0-9a-f]{64})  ([^\\/]+)$') {
            throw "Ungültige Prüfsummenzeile: $line"
        }

        $expectedHash = $Matches[1]
        $actualName = $Matches[2]
        $expectedName = $ArtifactNames[$index]
        if ($actualName -cne $expectedName) {
            throw "SHA256SUMS.txt nennt an Position $($index + 1) '$actualName' statt '$expectedName'."
        }
        if ($actualName -eq $checksumName) {
            throw 'SHA256SUMS.txt darf sich niemals selbst aufführen.'
        }

        $artifactPath = Join-Path $Directory $actualName
        if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
            throw "Das in SHA256SUMS.txt genannte Artefakt fehlt: $actualName"
        }

        $actualHash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -cne $expectedHash) {
            throw "Die SHA-256-Prüfsumme von '$actualName' stimmt nicht."
        }
    }
}

function Write-ReleaseChecksums {
    param(
        [Parameter(Mandatory)]
        [string]$Directory,

        [Parameter(Mandatory)]
        [string[]]$ArtifactNames
    )

    Assert-ExactTopLevelFiles -Directory $Directory -ExpectedNames $ArtifactNames

    $lines = foreach ($name in $ArtifactNames) {
        $artifactPath = Join-Path $Directory $name
        $hash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $name"
    }

    $checksumPath = Join-Path $Directory 'SHA256SUMS.txt'
    [IO.File]::WriteAllLines($checksumPath, $lines, [Text.UTF8Encoding]::new($false))
    Assert-ReleaseChecksums -Directory $Directory -ArtifactNames $ArtifactNames
}

Export-ModuleMember -Function @(
    'Remove-DirectoryStrict',
    'New-CleanDirectory',
    'Assert-ExactTopLevelFiles',
    'Copy-ThirdPartyNotices',
    'Assert-ThirdPartyNotices',
    'New-PortableArchive',
    'Assert-PortableArchive',
    'Write-ReleaseChecksums',
    'Assert-ReleaseChecksums'
)
