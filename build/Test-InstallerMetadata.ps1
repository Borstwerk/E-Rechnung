<#
.SYNOPSIS
    Prüft die gemeinsame Version der Anwendung und die Metadaten des MSI-Pakets.

.DESCRIPTION
    Liest die Windows-Installer-Datenbank ausschließlich lesend. Erwartete
    ProductVersion, ProductCode und UpgradeCode werden aus den verbindlichen
    Projektdateien ermittelt. Zusätzlich werden Assembly-, Datei- und
    Produktversion der veröffentlichten Anwendung geprüft. Damit prüft dieses
    Skript nicht nur den Quelltext, sondern die tatsächlich erzeugten Dateien.

    Das Skript installiert oder deinstalliert nichts.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$MsiPath,

    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$ApplicationPath
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectPath = Join-Path $root 'installer' 'EInvoiceSender.Setup' 'EInvoiceSender.Setup.wixproj'
$packagePath = Join-Path $root 'installer' 'EInvoiceSender.Setup' 'Package.wxs'
$buildPropsPath = Join-Path $root 'Directory.Build.props'

if (-not $IsWindows) {
    throw 'MSI-Metadaten lassen sich mit der Windows-Installer-API nur unter Windows prüfen.'
}

function Get-SingleXmlValue {
    param(
        [Parameter(Mandatory)]
        [xml]$Document,

        [Parameter(Mandatory)]
        [string]$LocalName
    )

    $values = @(
        $Document.SelectNodes("//*[local-name()='$LocalName']") |
            ForEach-Object { $_.InnerText.Trim() } |
            Where-Object { $_ }
    )

    if ($values.Count -ne 1) {
        throw "$LocalName muss genau einmal gesetzt sein; gefunden: $($values.Count)."
    }

    return $values[0]
}

function Assert-Equal {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [AllowEmptyString()]
        [string]$Actual,

        [AllowEmptyString()]
        [string]$Expected
    )

    if (-not [string]::Equals($Actual, $Expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Name ist '$Actual'; erwartet wird '$Expected'."
    }
}

function Get-MsiRows {
    param(
        [Parameter(Mandatory)]
        [__ComObject]$Database,

        [Parameter(Mandatory)]
        [string]$Query,

        [Parameter(Mandatory)]
        [int]$FieldCount
    )

    $flags = [Reflection.BindingFlags]::InvokeMethod
    $view = $Database.GetType().InvokeMember('OpenView', $flags, $null, $Database, @($Query))

    try {
        $view.GetType().InvokeMember('Execute', $flags, $null, $view, $null) | Out-Null
        $rows = @()

        while ($true) {
            $record = $view.GetType().InvokeMember('Fetch', $flags, $null, $view, $null)
            if ($null -eq $record) {
                break
            }

            $values = @()
            for ($field = 1; $field -le $FieldCount; $field++) {
                $values += $record.GetType().InvokeMember(
                    'StringData',
                    [Reflection.BindingFlags]::GetProperty,
                    $null,
                    $record,
                    @($field))
            }

            $rows += ,$values
        }

        return $rows
    }
    finally {
        try {
            $view.GetType().InvokeMember('Close', $flags, $null, $view, $null) | Out-Null
        }
        finally {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
        }
    }
}

[xml]$project = Get-Content -LiteralPath $projectPath -Raw
[xml]$package = Get-Content -LiteralPath $packagePath -Raw
[xml]$buildProps = Get-Content -LiteralPath $buildPropsPath -Raw

$expectedVersion = Get-SingleXmlValue -Document $buildProps -LocalName 'VersionPrefix'
if ($expectedVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "VersionPrefix '$expectedVersion' ist keine dreiteilige numerische Produktversion."
}

$conditionPrefix = "'`$(VersionPrefix)' == '"
$productCodeMappings = @(
    $project.SelectNodes("//*[local-name()='ProductCode']") |
        ForEach-Object {
            $condition = $_.GetAttribute('Condition').Trim()
            if (-not $condition.StartsWith($conditionPrefix, [StringComparison]::Ordinal) `
                -or -not $condition.EndsWith("'", [StringComparison]::Ordinal)) {
                throw "ProductCode '$($_.InnerText.Trim())' besitzt keine verständliche VersionPrefix-Zuordnung."
            }

            $mappedVersion = $condition.Substring(
                $conditionPrefix.Length,
                $condition.Length - $conditionPrefix.Length - 1)
            $mappedProductCode = $_.InnerText.Trim()
            $parsedProductCode = [Guid]::Empty
            if (-not [Guid]::TryParse($mappedProductCode, [ref]$parsedProductCode)) {
                throw "ProductCode '$mappedProductCode' für Version '$mappedVersion' ist keine gültige GUID."
            }

            [pscustomobject]@{
                Version = $mappedVersion
                ProductCode = $parsedProductCode.ToString('D')
            }
        }
)

$duplicateVersions = @($productCodeMappings | Group-Object Version | Where-Object Count -ne 1)
if ($duplicateVersions.Count -ne 0) {
    throw "ProductCode-Zuordnungen enthalten Versionen nicht genau einmal: $($duplicateVersions.Name -join ', ')."
}

$duplicateProductCodes = @($productCodeMappings | Group-Object ProductCode | Where-Object Count -ne 1)
if ($duplicateProductCodes.Count -ne 0) {
    throw "ProductCodes sind mehreren Versionen zugeordnet: $($duplicateProductCodes.Name -join ', ')."
}

$currentProductCode = @($productCodeMappings | Where-Object Version -eq $expectedVersion)
if ($currentProductCode.Count -ne 1) {
    throw "Für VersionPrefix '$expectedVersion' muss genau ein fester ProductCode hinterlegt sein; gefunden: $($currentProductCode.Count)."
}

$expectedProductCode = $currentProductCode[0].ProductCode
$packageElement = $package.SelectSingleNode("/*[local-name()='Wix']/*[local-name()='Package']")
$expectedUpgradeCode = $packageElement.GetAttribute('UpgradeCode')

$resolvedMsiPath = (Resolve-Path -LiteralPath $MsiPath).Path
$resolvedApplicationPath = (Resolve-Path -LiteralPath $ApplicationPath).Path
$managedAssemblyPath = [IO.Path]::ChangeExtension($resolvedApplicationPath, '.dll')
if (-not (Test-Path -LiteralPath $managedAssemblyPath -PathType Leaf)) {
    throw "Die verwaltete Anwendungsassembly fehlt: $managedAssemblyPath"
}

$expectedBinaryVersion = "$expectedVersion.0"
$applicationFileInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($resolvedApplicationPath)
$assemblyFileInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($managedAssemblyPath)
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($managedAssemblyPath).Version.ToString()
$applicationProductVersion = ($applicationFileInfo.ProductVersion -split '\+', 2)[0]
$assemblyProductVersion = ($assemblyFileInfo.ProductVersion -split '\+', 2)[0]

Assert-Equal -Name 'Anwendung.FileVersion' -Actual $applicationFileInfo.FileVersion -Expected $expectedBinaryVersion
Assert-Equal -Name 'Anwendung.ProductVersion' -Actual $applicationProductVersion -Expected $expectedVersion
Assert-Equal -Name 'Assembly.Version' -Actual $assemblyVersion -Expected $expectedBinaryVersion
Assert-Equal -Name 'Assembly.FileVersion' -Actual $assemblyFileInfo.FileVersion -Expected $expectedBinaryVersion
Assert-Equal -Name 'Assembly.ProductVersion' -Actual $assemblyProductVersion -Expected $expectedVersion

$installer = $null
$database = $null
$summary = $null

try {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.GetType().InvokeMember(
        'OpenDatabase',
        [Reflection.BindingFlags]::InvokeMethod,
        $null,
        $installer,
        @($resolvedMsiPath, 0))

    $properties = @{}
    Get-MsiRows -Database $database -Query 'SELECT `Property`,`Value` FROM `Property`' -FieldCount 2 |
        ForEach-Object { $properties[$_[0]] = $_[1] }

    Assert-Equal -Name 'ProductVersion' -Actual $properties.ProductVersion -Expected $expectedVersion
    Assert-Equal -Name 'ProductCode' -Actual $properties.ProductCode -Expected "{$expectedProductCode}"
    Assert-Equal -Name 'UpgradeCode' -Actual $properties.UpgradeCode -Expected "{$expectedUpgradeCode}"
    Assert-Equal -Name 'ALLUSERS' -Actual $properties.ALLUSERS -Expected '2'
    Assert-Equal -Name 'MSIINSTALLPERUSER' -Actual $properties.MSIINSTALLPERUSER -Expected '1'

    $summary = $database.GetType().InvokeMember(
        'SummaryInformation',
        [Reflection.BindingFlags]::GetProperty,
        $null,
        $database,
        @(0))
    $packageCode = $summary.GetType().InvokeMember(
        'Property',
        [Reflection.BindingFlags]::GetProperty,
        $null,
        $summary,
        @(9))

    $parsedPackageCode = [Guid]::Empty
    if (-not [Guid]::TryParse($packageCode, [ref]$parsedPackageCode)) {
        throw "PackageCode '$packageCode' ist keine gültige GUID."
    }

    $upgradeRows = @(
        Get-MsiRows -Database $database `
            -Query 'SELECT `UpgradeCode`,`VersionMin`,`VersionMax`,`Attributes`,`ActionProperty` FROM `Upgrade`' `
            -FieldCount 5
    )

    if ($upgradeRows.Count -ne 2) {
        throw "Die Upgrade-Tabelle enthält $($upgradeRows.Count) statt genau zwei Zeilen."
    }

    $upgrade = @($upgradeRows | Where-Object { $_[4] -eq 'WIX_UPGRADE_DETECTED' })
    $downgrade = @($upgradeRows | Where-Object { $_[4] -eq 'WIX_DOWNGRADE_DETECTED' })
    if ($upgrade.Count -ne 1 -or $downgrade.Count -ne 1) {
        throw 'Upgrade- und Downgrade-Erkennung sind nicht jeweils genau einmal vorhanden.'
    }

    Assert-Equal -Name 'Upgrade.UpgradeCode' -Actual $upgrade[0][0] -Expected "{$expectedUpgradeCode}"
    Assert-Equal -Name 'Downgrade.UpgradeCode' -Actual $downgrade[0][0] -Expected "{$expectedUpgradeCode}"
    Assert-Equal -Name 'Upgrade.VersionMax' -Actual $upgrade[0][2] -Expected $expectedVersion
    Assert-Equal -Name 'Downgrade.VersionMin' -Actual $downgrade[0][1] -Expected $expectedVersion

    $versionMaxInclusive = 512
    if (([int]$upgrade[0][3] -band $versionMaxInclusive) -ne 0) {
        throw 'Die Upgrade-Erkennung schließt dieselbe Version ein; AllowSameVersionUpgrades darf nicht aktiv sein.'
    }

    $onlyDetect = 2
    if (([int]$downgrade[0][3] -band $onlyDetect) -eq 0) {
        throw 'Die Downgrade-Zeile muss ausschließlich der Erkennung dienen.'
    }

    $sequences = @{}
    Get-MsiRows -Database $database `
        -Query 'SELECT `Action`,`Sequence` FROM `InstallExecuteSequence`' `
        -FieldCount 2 |
        ForEach-Object { $sequences[$_[0]] = [int]$_[1] }

    foreach ($action in @('FindRelatedProducts', 'MigrateFeatureStates', 'RemoveExistingProducts', 'InstallInitialize')) {
        if (-not $sequences.ContainsKey($action)) {
            throw "$action fehlt in InstallExecuteSequence."
        }
    }

    if (-not ($sequences.FindRelatedProducts -lt $sequences.MigrateFeatureStates `
              -and $sequences.MigrateFeatureStates -lt $sequences.RemoveExistingProducts `
              -and $sequences.RemoveExistingProducts -lt $sequences.InstallInitialize)) {
        throw 'Die Reihenfolge der Aktionen für das Major Upgrade ist nicht wie erwartet.'
    }

    Write-Host 'Anwendungs- und MSI-Metadaten erfolgreich geprüft.' -ForegroundColor Green
    Write-Host "  App ProductVersion: $($applicationFileInfo.ProductVersion)"
    Write-Host "  App FileVersion:    $($applicationFileInfo.FileVersion)"
    Write-Host "  AssemblyVersion:    $assemblyVersion"
    Write-Host "  ProductVersion: $($properties.ProductVersion)"
    Write-Host "  ProductCode:    $($properties.ProductCode)"
    Write-Host "  UpgradeCode:    $($properties.UpgradeCode)"
    Write-Host "  PackageCode:    $packageCode"
}
finally {
    foreach ($comObject in @($summary, $database, $installer)) {
        if ($null -ne $comObject) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($comObject)
        }
    }
}
