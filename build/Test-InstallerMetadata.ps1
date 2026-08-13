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
$applicationProjectPath = Join-Path $root 'src' 'EInvoiceSender.App' 'EInvoiceSender.App.csproj'
$packagePath = Join-Path $root 'installer' 'EInvoiceSender.Setup' 'Package.wxs'
$buildPropsPath = Join-Path $root 'Directory.Build.props'
$thirdPartyDocumentPath = Join-Path $root 'docs' 'THIRD-PARTY-NOTICES.md'
$thirdPartySourcePath = Join-Path $root 'installer' 'Drittanbieterhinweise'
$licenseRtfPath = Join-Path $root 'installer' 'EInvoiceSender.Setup' 'Lizenzhinweise.rtf'
$desktopFeatureId = 'Desktopverkn' + 'uepfung'
$desktopComponentId = 'DesktopVerkn' + 'uepfung'
$startmenuComponentId = 'StartmenuVerkn' + 'uepfung'

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

function Assert-SetEqual {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string[]]$Actual,

        [Parameter(Mandatory)]
        [string[]]$Expected
    )

    $missing = @($Expected | Where-Object { $_ -notin $Actual } | Sort-Object)
    $unexpected = @($Actual | Where-Object { $_ -notin $Expected } | Sort-Object)
    if ($missing.Count -ne 0 -or $unexpected.Count -ne 0) {
        throw "$Name weicht ab. Fehlt: $($missing -join ', '); unerwartet: $($unexpected -join ', ')."
    }
}

function Get-MarkedMarkdownTable {
    param(
        [Parameter(Mandatory)]
        [string]$Content,

        [Parameter(Mandatory)]
        [string]$Marker
    )

    $start = "<!-- ${Marker}:start -->"
    $end = "<!-- ${Marker}:end -->"
    $startIndex = $Content.IndexOf($start, [StringComparison]::Ordinal)
    $endIndex = $Content.IndexOf($end, [StringComparison]::Ordinal)
    if ($startIndex -lt 0 -or $endIndex -le $startIndex) {
        throw "Die Tabellenmarker $start und $end fehlen oder stehen in falscher Reihenfolge."
    }

    return $Content.Substring($startIndex + $start.Length, $endIndex - $startIndex - $start.Length)
}

function Get-DocumentedRuntimePackages {
    param(
        [Parameter(Mandatory)]
        [string]$Content
    )

    $table = Get-MarkedMarkdownTable -Content $Content -Marker 'runtime-packages'
    return @(
        $table -split "`r?`n" |
            Where-Object { $_.TrimStart().StartsWith('| `', [StringComparison]::Ordinal) } |
            ForEach-Object {
                $cells = @($_.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() })
                if ($cells.Count -ne 5) {
                    throw "Ungültige Runtimepaket-Zeile in THIRD-PARTY-NOTICES.md: $_"
                }

                [pscustomobject]@{
                    Id = $cells[0].Trim('`')
                    Version = $cells[2]
                    InDepsJson = $cells[4] -eq 'ja'
                }
            }
    )
}

function Get-DocumentedRuntimePacks {
    param(
        [Parameter(Mandatory)]
        [string]$Content
    )

    $table = Get-MarkedMarkdownTable -Content $Content -Marker 'runtime-packs'
    return @(
        $table -split "`r?`n" |
            Where-Object { $_.TrimStart().StartsWith('| `', [StringComparison]::Ordinal) } |
            ForEach-Object {
                $cells = @($_.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() })
                if ($cells.Count -ne 4) {
                    throw "Ungültige Runtimepack-Zeile in THIRD-PARTY-NOTICES.md: $_"
                }

                [pscustomobject]@{
                    Id = $cells[0].Trim('`')
                    Version = $cells[1]
                }
            }
    )
}

function Get-LongMsiFileName {
    param(
        [Parameter(Mandatory)]
        [string]$FileName
    )

    $parts = $FileName.Split('|', 2)
    return $parts[$parts.Length - 1]
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
[xml]$applicationProject = Get-Content -LiteralPath $applicationProjectPath -Raw

$expectedVersion = Get-SingleXmlValue -Document $buildProps -LocalName 'VersionPrefix'
$expectedRuntimeFrameworkVersion = Get-SingleXmlValue `
    -Document $applicationProject -LocalName 'RuntimeFrameworkVersion'
if ($expectedVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "VersionPrefix '$expectedVersion' ist keine dreiteilige numerische Produktversion."
}
if ($expectedRuntimeFrameworkVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "RuntimeFrameworkVersion '$expectedRuntimeFrameworkVersion' ist keine dreiteilige numerische Runtimeversion."
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

$depsPath = [IO.Path]::ChangeExtension($resolvedApplicationPath, '.deps.json')
if (-not (Test-Path -LiteralPath $depsPath -PathType Leaf)) {
    throw "Die Abhängigkeitsbeschreibung der Anwendung fehlt: $depsPath"
}

$thirdPartyDocument = Get-Content -LiteralPath $thirdPartyDocumentPath -Raw
$documentedPackages = @(Get-DocumentedRuntimePackages -Content $thirdPartyDocument)
$documentedDepsPackages = @($documentedPackages | Where-Object InDepsJson)
$documentedRuntimePacks = @(Get-DocumentedRuntimePacks -Content $thirdPartyDocument)
$deps = Get-Content -LiteralPath $depsPath -Raw | ConvertFrom-Json -AsHashtable
$actualDepsPackages = @(
    $deps.libraries.GetEnumerator() |
        Where-Object { $_.Value.type -eq 'package' } |
        ForEach-Object { $_.Key }
)
$actualRuntimePacks = @(
    $deps.libraries.GetEnumerator() |
        Where-Object { $_.Value.type -eq 'runtimepack' } |
        ForEach-Object { $_.Key }
)
$expectedDepsPackages = @($documentedDepsPackages | ForEach-Object { "$($_.Id)/$($_.Version)" })
$expectedRuntimePacks = @($documentedRuntimePacks | ForEach-Object { "$($_.Id)/$($_.Version)" })

foreach ($runtimePack in $documentedRuntimePacks) {
    Assert-Equal -Name "Dokumentierte Runtimepack-Fassung $($runtimePack.Id)" `
        -Actual $runtimePack.Version -Expected $expectedRuntimeFrameworkVersion
}

Assert-SetEqual -Name 'NuGet-Laufzeitpakete im deps.json' `
    -Actual $actualDepsPackages -Expected $expectedDepsPackages
Assert-SetEqual -Name 'Self-contained Runtimepacks im deps.json' `
    -Actual $actualRuntimePacks -Expected $expectedRuntimePacks

foreach ($obsolete in @('Serilog', 'Microsoft.Extensions.Hosting')) {
    if ($actualDepsPackages | Where-Object { $_.StartsWith($obsolete, [StringComparison]::OrdinalIgnoreCase) }) {
        throw "Der nicht vorgesehene Runtimebestandteil $obsolete ist im gebauten deps.json enthalten."
    }
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
    Assert-Equal -Name 'WIXUI_INSTALLDIR' -Actual $properties.WIXUI_INSTALLDIR -Expected 'INSTALLFOLDER'
    Assert-Equal -Name 'INSTALLDESKTOPSHORTCUT' -Actual $properties.INSTALLDESKTOPSHORTCUT -Expected '1'
    Assert-Equal -Name 'ARPNOMODIFY' -Actual $properties.ARPNOMODIFY -Expected '1'

    # ER-020-INS-02: Das gebaute MSI muss die getrennten Featurezustände,
    # die native Checkbox und ihre ControlEvents tatsächlich enthalten. Eine
    # reine Quelltextprüfung würde Linker- oder UI-Tabellendrift übersehen.
    $featureRows = @(
        Get-MsiRows -Database $database `
            -Query 'SELECT `Feature`,`Title`,`Level` FROM `Feature`' `
            -FieldCount 3 |
            ForEach-Object {
                [pscustomobject]@{
                    Id = $_[0]
                    Title = $_[1]
                    Level = $_[2]
                }
            }
    )
    $mainFeature = @($featureRows | Where-Object Id -eq 'Hauptfunktion')
    $desktopFeature = @($featureRows | Where-Object Id -eq $desktopFeatureId)
    if ($mainFeature.Count -ne 1 -or $desktopFeature.Count -ne 1) {
        throw "Haupt- und Desktopfeature müssen genau einmal vorhanden sein. Haupt: $($mainFeature.Count), Desktop: $($desktopFeature.Count)."
    }
    Assert-Equal -Name 'Hauptfunktion.Level' -Actual $mainFeature[0].Level -Expected '1'
    Assert-Equal -Name 'Desktopverknüpfung.Level' -Actual $desktopFeature[0].Level -Expected '1'

    $featureComponents = @(
        Get-MsiRows -Database $database `
            -Query 'SELECT `Feature_`,`Component_` FROM `FeatureComponents`' `
            -FieldCount 2 |
            ForEach-Object { "{0}|{1}" -f $_[0], $_[1] }
    )
    if ("Hauptfunktion|$startmenuComponentId" -notin $featureComponents) {
        throw 'Der Startmenüeintrag ist im gebauten MSI nicht mehr Bestandteil der Hauptfunktion.'
    }
    if ("$desktopFeatureId|$desktopComponentId" -notin $featureComponents) {
        throw 'Die Desktopkomponente ist im gebauten MSI nicht mehr Bestandteil des Desktopfeatures.'
    }
    if ("Hauptfunktion|$desktopComponentId" -in $featureComponents `
        -or "$desktopFeatureId|$startmenuComponentId" -in $featureComponents) {
        throw 'Startmenü- und Desktopkomponente sind im gebauten MSI nicht mehr unabhängig.'
    }

    $shortcutRows = @(
        Get-MsiRows -Database $database `
            -Query 'SELECT `Shortcut`,`Directory_`,`Component_`,`Name`,`Target`,`WkDir`,`Icon_` FROM `Shortcut`' `
            -FieldCount 7 |
            ForEach-Object {
                [pscustomobject]@{
                    Id = $_[0]
                    Directory = $_[1]
                    Component = $_[2]
                    Name = Get-LongMsiFileName -FileName $_[3]
                    Target = $_[4]
                    WorkingDirectory = $_[5]
                    Icon = $_[6]
                }
            }
    )
    $startmenuShortcut = @($shortcutRows | Where-Object Id -eq 'StartmenuEintrag')
    $desktopShortcut = @($shortcutRows | Where-Object Id -eq 'DesktopEintrag')
    if ($startmenuShortcut.Count -ne 1 -or $desktopShortcut.Count -ne 1) {
        throw "Startmenü- und Desktopshortcut müssen genau einmal vorhanden sein. Startmenü: $($startmenuShortcut.Count), Desktop: $($desktopShortcut.Count)."
    }
    Assert-Equal -Name 'Startmenü.Directory' -Actual $startmenuShortcut[0].Directory -Expected 'StartmenuOrdner'
    Assert-Equal -Name 'Startmenü.Component' -Actual $startmenuShortcut[0].Component -Expected $startmenuComponentId
    Assert-Equal -Name 'Desktop.Directory' -Actual $desktopShortcut[0].Directory -Expected 'DesktopFolder'
    Assert-Equal -Name 'Desktop.Component' -Actual $desktopShortcut[0].Component -Expected $desktopComponentId
    foreach ($shortcut in @($startmenuShortcut[0], $desktopShortcut[0])) {
        Assert-Equal -Name "$($shortcut.Id).Name" -Actual $shortcut.Name -Expected 'BorstWerk E-Rechnung'
        Assert-Equal -Name "$($shortcut.Id).Target" -Actual $shortcut.Target -Expected '[INSTALLFOLDER]EInvoiceSender.exe'
        Assert-Equal -Name "$($shortcut.Id).WorkingDirectory" -Actual $shortcut.WorkingDirectory -Expected 'INSTALLFOLDER'
        Assert-Equal -Name "$($shortcut.Id).Icon" -Actual $shortcut.Icon -Expected 'BorstWerkEInvoice.ico'
    }

    $desktopComponentRows = @(
        Get-MsiRows -Database $database `
            -Query "SELECT ``Directory_``,``KeyPath`` FROM ``Component`` WHERE ``Component``='$desktopComponentId'" `
            -FieldCount 2
    )
    if ($desktopComponentRows.Count -ne 1) {
        throw "Die Desktopkomponente ist $($desktopComponentRows.Count)-mal statt genau einmal vorhanden."
    }
    Assert-Equal -Name 'Desktopkomponente.Directory' -Actual $desktopComponentRows[0][0] -Expected 'DesktopFolder'
    $desktopRegistryRows = @(
        Get-MsiRows -Database $database `
            -Query "SELECT ``Registry``,``Root``,``Name``,``Component_`` FROM ``Registry`` WHERE ``Component_``='$desktopComponentId'" `
            -FieldCount 4
    )
    if ($desktopRegistryRows.Count -ne 1) {
        throw "Der Registry-KeyPath der Desktopkomponente ist $($desktopRegistryRows.Count)-mal statt genau einmal vorhanden."
    }
    Assert-Equal -Name 'Desktop-Registry.Root' -Actual $desktopRegistryRows[0][1] -Expected '1'
    Assert-Equal -Name 'Desktop-Registry.Name' -Actual $desktopRegistryRows[0][2] -Expected 'Desktop'
    Assert-Equal -Name 'Desktop-Registry.Component' -Actual $desktopRegistryRows[0][3] -Expected $desktopComponentId
    Assert-Equal -Name 'Desktopkomponente.KeyPath' -Actual $desktopComponentRows[0][1] -Expected $desktopRegistryRows[0][0]

    $desktopControls = @(
        Get-MsiRows -Database $database `
            -Query "SELECT ``Control``,``Type``,``Property``,``Text`` FROM ``Control`` WHERE ``Dialog_``='DesktopShortcutDlg'" `
            -FieldCount 4
    )
    $desktopCheckBox = @($desktopControls | Where-Object { $_[0] -eq 'DesktopShortcutCheckBox' })
    if ($desktopCheckBox.Count -ne 1) {
        throw "Der Desktopdialog enthält $($desktopCheckBox.Count) statt genau einer erwarteten Checkbox."
    }
    Assert-Equal -Name 'Desktopcheckbox.Type' -Actual $desktopCheckBox[0][1] -Expected 'CheckBox'
    Assert-Equal -Name 'Desktopcheckbox.Property' -Actual $desktopCheckBox[0][2] -Expected 'INSTALLDESKTOPSHORTCUT'
    Assert-Equal -Name 'Desktopcheckbox.Text' -Actual $desktopCheckBox[0][3] -Expected 'Desktop-Verknüpfung erstellen'

    $checkBoxRows = @(
        Get-MsiRows -Database $database `
            -Query "SELECT ``Property``,``Value`` FROM ``CheckBox`` WHERE ``Property``='INSTALLDESKTOPSHORTCUT'" `
            -FieldCount 2
    )
    if ($checkBoxRows.Count -ne 1) {
        throw "Die CheckBox-Tabelle enthält $($checkBoxRows.Count) statt genau einer Desktopoption."
    }
    Assert-Equal -Name 'Desktopcheckbox.Value' -Actual $checkBoxRows[0][1] -Expected '1'

    $controlEvents = @(
        Get-MsiRows -Database $database `
            -Query 'SELECT `Dialog_`,`Control_`,`Event`,`Argument`,`Condition`,`Ordering` FROM `ControlEvent`' `
            -FieldCount 6 |
            ForEach-Object {
                [pscustomobject]@{
                    Dialog = $_[0]
                    Control = $_[1]
                    Event = $_[2]
                    Argument = $_[3]
                    Condition = $_[4]
                    Ordering = $_[5]
                }
            }
    )
    $desktopFeatureEvents = @(
        $controlEvents | Where-Object {
            $_.Dialog -eq 'DesktopShortcutDlg' -and $_.Control -eq 'Next' `
                -and $_.Event -in @('AddLocal', 'Remove')
        }
    )
    if ($desktopFeatureEvents.Count -ne 2) {
        throw "Der Desktopdialog enthält $($desktopFeatureEvents.Count) statt genau zwei nativen Featureereignissen."
    }
    $addLocal = @($desktopFeatureEvents | Where-Object Event -eq 'AddLocal')
    $remove = @($desktopFeatureEvents | Where-Object Event -eq 'Remove')
    if ($addLocal.Count -ne 1 -or $remove.Count -ne 1) {
        throw 'AddLocal und Remove müssen jeweils genau einmal vorhanden sein.'
    }
    Assert-Equal -Name 'Desktop.AddLocal.Argument' -Actual $addLocal[0].Argument -Expected $desktopFeatureId
    Assert-Equal -Name 'Desktop.AddLocal.Condition' -Actual $addLocal[0].Condition -Expected 'INSTALLDESKTOPSHORTCUT = "1"'
    Assert-Equal -Name 'Desktop.Remove.Argument' -Actual $remove[0].Argument -Expected $desktopFeatureId
    Assert-Equal -Name 'Desktop.Remove.Condition' -Actual $remove[0].Condition -Expected 'NOT INSTALLDESKTOPSHORTCUT'

    $desktopDialogEntrances = @($controlEvents | Where-Object Argument -eq 'DesktopShortcutDlg')
    if ($desktopDialogEntrances.Count -ne 2) {
        throw "Der Desktopdialog besitzt $($desktopDialogEntrances.Count) statt genau zwei Navigationspfaden."
    }
    $desktopForward = @($desktopDialogEntrances | Where-Object {
            $_.Dialog -eq 'InstallDirDlg' -and $_.Control -eq 'Next' -and $_.Event -eq 'NewDialog'
        })
    $desktopBackward = @($desktopDialogEntrances | Where-Object {
            $_.Dialog -eq 'VerifyReadyDlg' -and $_.Control -eq 'Back' -and $_.Event -eq 'NewDialog'
        })
    if ($desktopForward.Count -ne 1 -or $desktopBackward.Count -ne 1) {
        throw 'Der Desktopdialog darf nur zwischen InstallDirDlg und VerifyReadyDlg liegen.'
    }
    foreach ($entrance in @($desktopForward[0], $desktopBackward[0])) {
        if (-not $entrance.Condition.Contains('NOT Installed AND NOT WIX_UPGRADE_DETECTED', [StringComparison]::Ordinal)) {
            throw "Der Pfad $($entrance.Dialog).$($entrance.Control) zeigt die Desktopoption nicht ausschließlich bei echter Erstinstallation: $($entrance.Condition)"
        }
    }

    $requiredStandardNavigation = @(
        @('ExitDialog', 'Finish', 'EndDialog', 'Return', '1'),
        @('WelcomeDlg', 'Next', 'NewDialog', 'LicenseAgreementDlg', 'NOT Installed'),
        @('WelcomeDlg', 'Next', 'NewDialog', 'VerifyReadyDlg', 'Installed AND PATCH'),
        @('LicenseAgreementDlg', 'Back', 'NewDialog', 'WelcomeDlg', '1'),
        @('LicenseAgreementDlg', 'Next', 'NewDialog', 'InstallDirDlg', 'LicenseAccepted = "1"'),
        @('InstallDirDlg', 'Back', 'NewDialog', 'LicenseAgreementDlg', '1'),
        @('MaintenanceWelcomeDlg', 'Next', 'NewDialog', 'MaintenanceTypeDlg', '1'),
        @('MaintenanceTypeDlg', 'RepairButton', 'NewDialog', 'VerifyReadyDlg', '1'),
        @('MaintenanceTypeDlg', 'RemoveButton', 'NewDialog', 'VerifyReadyDlg', '1'),
        @('MaintenanceTypeDlg', 'Back', 'NewDialog', 'MaintenanceWelcomeDlg', '1'),
        @('VerifyReadyDlg', 'Back', 'NewDialog', 'MaintenanceTypeDlg', 'Installed AND NOT PATCH'),
        @('VerifyReadyDlg', 'Back', 'NewDialog', 'WelcomeDlg', 'Installed AND PATCH')
    )
    foreach ($expectedNavigation in $requiredStandardNavigation) {
        $matchingNavigation = @(
            $controlEvents | Where-Object {
                $_.Dialog -eq $expectedNavigation[0] `
                    -and $_.Control -eq $expectedNavigation[1] `
                    -and $_.Event -eq $expectedNavigation[2] `
                    -and $_.Argument -eq $expectedNavigation[3] `
                    -and $_.Condition -eq $expectedNavigation[4]
            }
        )
        if ($matchingNavigation.Count -ne 1) {
            throw "Der Standarddialogpfad $($expectedNavigation -join ' | ') ist $($matchingNavigation.Count)-mal statt genau einmal vorhanden."
        }
    }

    $msiFiles = @(
        Get-MsiRows -Database $database -Query 'SELECT `File`,`Component_`,`FileName` FROM `File`' -FieldCount 3 |
            ForEach-Object {
                [pscustomobject]@{
                    Id = $_[0]
                    Component = $_[1]
                    Name = Get-LongMsiFileName -FileName $_[2]
                }
            }
    )
    $mainNotice = @($msiFiles | Where-Object Id -eq 'DrittanbieterhinweisDatei')
    if ($mainNotice.Count -ne 1) {
        throw "Die MSI-Dateitabelle enthält den expliziten Drittanbieterhinweis $($mainNotice.Count)-mal statt genau einmal."
    }
    Assert-Equal -Name 'MSI-Hinweisdatei' -Actual $mainNotice[0].Name -Expected 'Drittanbieterhinweise.md'
    $mainNoticeDirectory = @(
        Get-MsiRows -Database $database `
            -Query "SELECT ``Directory_`` FROM ``Component`` WHERE ``Component``='$($mainNotice[0].Component)'" `
            -FieldCount 1
    )
    if ($mainNoticeDirectory.Count -ne 1) {
        throw "Die Komponente der MSI-Hinweisdatei besitzt $($mainNoticeDirectory.Count) statt genau eines Zielordners."
    }
    Assert-Equal -Name 'Zielordner der MSI-Hinweisdatei' `
        -Actual $mainNoticeDirectory[0][0] -Expected 'INSTALLFOLDER'

    $expectedLicenseNames = @(
        Get-ChildItem -LiteralPath (Join-Path $thirdPartySourcePath 'Lizenzen') -Recurse -File |
            ForEach-Object Name |
            Sort-Object -Unique
    )
    $actualMsiFileNames = @($msiFiles | ForEach-Object Name | Sort-Object -Unique)
    $missingLicenseNames = @($expectedLicenseNames | Where-Object { $_ -notin $actualMsiFileNames })
    if ($missingLicenseNames.Count -ne 0) {
        throw "Im MSI fehlen vorgesehene Lizenz-/Notice-Dateien: $($missingLicenseNames -join ', ')."
    }

    $licenseRows = @(
        Get-MsiRows -Database $database `
            -Query "SELECT ``Text`` FROM ``Control`` WHERE ``Dialog_``='LicenseAgreementDlg' AND ``Control``='LicenseText'" `
            -FieldCount 1
    )
    if ($licenseRows.Count -ne 1) {
        throw "Der MSI-Lizenzdialog enthält $($licenseRows.Count) statt genau eines Lizenztexts."
    }

    $expectedLicenseRtf = (Get-Content -LiteralPath $licenseRtfPath -Raw).Trim()
    $actualLicenseRtf = $licenseRows[0][0].Trim()
    Assert-Equal -Name 'RTF im MSI-Lizenzdialog' -Actual $actualLicenseRtf -Expected $expectedLicenseRtf
    foreach ($requiredText in @('BorstWerk E-Rechnung', 'PdfPig', 'BouncyCastle.Cryptography', 'Microsoft.NETCore.App')) {
        if (-not $actualLicenseRtf.Contains($requiredText, [StringComparison]::Ordinal)) {
            throw "Der RTF-Text im MSI enthält die Pflichtangabe '$requiredText' nicht."
        }
    }
    foreach ($obsolete in @('Serilog', 'Microsoft.Extensions.Hosting')) {
        if ($actualLicenseRtf.Contains($obsolete, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Der RTF-Text im MSI behauptet den veralteten Runtimebestandteil '$obsolete'."
        }
    }

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

    $migrateFeatures = 1
    if (([int]$upgrade[0][3] -band $migrateFeatures) -eq 0) {
        throw 'Das Major Upgrade muss den vorhandenen Desktop-Featurezustand migrieren.'
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
    Write-Host "  Runtimepakete:  $($actualDepsPackages.Count)"
    Write-Host "  Runtimepacks:   $($actualRuntimePacks.Count)"
    Write-Host "  Runtimepatch:   $expectedRuntimeFrameworkVersion"
    Write-Host "  Hinweisdatei:   $($mainNotice[0].Name)"
    Write-Host "  Lizenzdateien:  $($expectedLicenseNames.Count) vorgesehen und im MSI gefunden"
    Write-Host '  Desktopoption:   Level 1, standardmäßig aktiviert, native AddLocal/Remove-Ereignisse'
}
finally {
    foreach ($comObject in @($summary, $database, $installer)) {
        if ($null -ne $comObject) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($comObject)
        }
    }
}
