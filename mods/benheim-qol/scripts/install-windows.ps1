Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PluginDll = if ($env:BENHEIM_QOL_DLL) { $env:BENHEIM_QOL_DLL } else { Join-Path $ScriptDir 'BenheimQoL.dll' }
$LauncherSource = Join-Path $ScriptDir 'launch-windows.ps1'
$DoorstopConfigHelpers = Join-Path $ScriptDir 'windows-doorstop-config.ps1'
$VersionSource = Join-Path $ScriptDir 'VERSION'
$PrivateDiagnosticsSource = Join-Path $ScriptDir 'PRIVATE-TEST-DIAGNOSTICS.cfg'
$BepInExUrl = if ($env:BENHEIM_QOL_BEPINEX_URL) { $env:BENHEIM_QOL_BEPINEX_URL } else { 'https://gcdn.thunderstore.io/live/repository/packages/denikson-BepInExPack_Valheim-5.4.2333.zip' }
$BepInExSha256 = if ($env:BENHEIM_QOL_BEPINEX_SHA256) { $env:BENHEIM_QOL_BEPINEX_SHA256.ToLowerInvariant() } else { '5dd24ccbcaa9260f714b200f23c4c15547e2aa5f06906cafcc0dee56db1bf716' }
$ShortcutMarker = 'BenheimQoL launcher managed by the BenheimQoL installer'
$LegacyUpdaterShortcutMarker = 'Benheim updater managed by the Benheim installer'
$LegacyUpdaterMarker = 'Benheim updater managed directory v1'
$LauncherMarker = 'Benheim launcher managed directory v1'
$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("BenheimQoL-" + [guid]::NewGuid().ToString('N'))

if (-not (Test-Path -LiteralPath $DoorstopConfigHelpers -PathType Leaf)) {
    throw 'The Benheim Doorstop configuration helper is missing beside the installer.'
}
. $DoorstopConfigHelpers

function Get-SteamRoots {
    $roots = New-Object System.Collections.Generic.List[string]
    $registryPaths = @(
        'HKCU:\Software\Valve\Steam',
        'HKLM:\Software\WOW6432Node\Valve\Steam',
        'HKLM:\Software\Valve\Steam'
    )

    foreach ($registryPath in $registryPaths) {
        try {
            $properties = Get-ItemProperty -Path $registryPath -ErrorAction Stop
            $steamRoot = $null
            if ($null -ne $properties.PSObject.Properties['SteamPath']) {
                $steamRoot = $properties.SteamPath
            }
            elseif ($null -ne $properties.PSObject.Properties['InstallPath']) {
                $steamRoot = $properties.InstallPath
            }
            if ($steamRoot -and -not $roots.Contains($steamRoot)) {
                $roots.Add($steamRoot)
            }
        }
        catch {
            # The final error names the missing game and supported override.
        }
    }

    foreach ($primaryRoot in @($roots)) {
        $libraryFile = Join-Path $primaryRoot 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path -LiteralPath $libraryFile -PathType Leaf)) {
            continue
        }

        foreach ($line in Get-Content -LiteralPath $libraryFile) {
            if ($line -match '"path"\s+"([^"]+)"') {
                $libraryRoot = $Matches[1].Replace('\\', '\')
                if (-not $roots.Contains($libraryRoot)) {
                    $roots.Add($libraryRoot)
                }
            }
        }
    }

    return $roots
}

function Find-ValheimGameDir {
    if ($env:BENHEIM_QOL_GAME_DIR) {
        return $env:BENHEIM_QOL_GAME_DIR
    }

    foreach ($steamRoot in Get-SteamRoots) {
        $candidate = Join-Path $steamRoot 'steamapps\common\Valheim'
        if (Test-Path -LiteralPath (Join-Path $candidate 'valheim.exe') -PathType Leaf) {
            return $candidate
        }
    }

    throw 'Valheim was not found in any Steam library. Install it through Steam, or set BENHEIM_QOL_GAME_DIR and run this installer again.'
}

function Move-LegacyFile {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination,
        [Parameter(Mandatory = $true)][string]$ArchivePrefix
    )

    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        return
    }

    $destinationDir = Split-Path -Parent $Destination
    New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null

    if (-not (Test-Path -LiteralPath $Destination -PathType Leaf)) {
        Move-Item -LiteralPath $Source -Destination $Destination
        return
    }

    $sourceHash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash
    $destinationHash = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
    if ($sourceHash -eq $destinationHash) {
        Remove-Item -LiteralPath $Source -Force
        return
    }

    $timestamp = Get-Date -Format 'yyyyMMddTHHmmss'
    Move-Item -LiteralPath $Source -Destination (Join-Path $destinationDir "$ArchivePrefix.$timestamp")
}

function Install-BenheimQoL {
    if ($env:OS -ne 'Windows_NT') {
        throw 'This installer requires Windows.'
    }
    if (Get-Process -Name 'valheim' -ErrorAction SilentlyContinue) {
        throw 'Valheim is running. Quit the game completely, then run this installer again.'
    }

    $gameDir = Find-ValheimGameDir
    if (-not (Test-Path -LiteralPath (Join-Path $gameDir 'valheim.exe') -PathType Leaf)) {
        throw "Valheim was not found at: $gameDir"
    }
    if (-not (Test-Path -LiteralPath $PluginDll -PathType Leaf)) {
        throw 'The Benheim plugin file is missing beside the installer.'
    }
    if (-not (Test-Path -LiteralPath $LauncherSource -PathType Leaf)) {
        throw 'The Benheim launcher file is missing beside the installer.'
    }
    if (-not (Test-Path -LiteralPath $VersionSource -PathType Leaf)) {
        throw 'The Benheim VERSION file is missing beside the installer.'
    }
    try {
        [void][version](Get-Content -LiteralPath $VersionSource -Raw).Trim()
    }
    catch {
        throw 'The Benheim VERSION file is invalid.'
    }

    $desktop = [Environment]::GetFolderPath('Desktop')
    $localAppData = [Environment]::GetFolderPath('LocalApplicationData')
    if (-not $desktop -or -not $localAppData) {
        throw 'Windows did not report the Desktop or Local AppData folder.'
    }

    $launcherRoot = Join-Path $localAppData 'BenheimLauncher'
    $launcherMarkerPath = Join-Path $launcherRoot '.benheim-managed'
    $launcherPath = Join-Path $launcherRoot 'launch-windows.ps1'
    if (Test-Path -LiteralPath $launcherRoot) {
        if (-not (Test-Path -LiteralPath $launcherRoot -PathType Container) -or
            -not (Test-Path -LiteralPath $launcherMarkerPath -PathType Leaf) -or
            (Get-Content -LiteralPath $launcherMarkerPath -Raw).Trim() -ne $LauncherMarker) {
            throw "Refusing to replace an unrelated or damaged launcher directory at: $launcherRoot"
        }
    }

    $legacyUpdaterRoot = Join-Path $localAppData 'Benheim'
    $legacyUpdaterMarkerPath = Join-Path $legacyUpdaterRoot '.benheim-managed'
    $removeLegacyUpdaterRoot = $false
    if (Test-Path -LiteralPath $legacyUpdaterRoot -PathType Container) {
        $removeLegacyUpdaterRoot =
            (Test-Path -LiteralPath $legacyUpdaterMarkerPath -PathType Leaf) -and
            (Get-Content -LiteralPath $legacyUpdaterMarkerPath -Raw).Trim() -eq $LegacyUpdaterMarker
    }

    $shortcutPath = Join-Path $desktop 'Benheim.lnk'
    $legacyShortcutPath = Join-Path $desktop 'Benheim QoL.lnk'
    $legacyUpdaterShortcutPath = Join-Path $desktop 'Update Benheim.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $removeLegacyShortcut = $false
    $removeLegacyUpdaterShortcut = $false

    if (Test-Path -LiteralPath $shortcutPath -PathType Leaf) {
        $existingShortcut = $shell.CreateShortcut($shortcutPath)
        if ($existingShortcut.Description -ne $ShortcutMarker) {
            throw "Refusing to replace an unrelated shortcut at: $shortcutPath"
        }
    }
    if (Test-Path -LiteralPath $legacyShortcutPath -PathType Leaf) {
        $removeLegacyShortcut = $shell.CreateShortcut($legacyShortcutPath).Description -eq $ShortcutMarker
    }
    if (Test-Path -LiteralPath $legacyUpdaterShortcutPath -PathType Leaf) {
        $removeLegacyUpdaterShortcut =
            $shell.CreateShortcut($legacyUpdaterShortcutPath).Description -eq $LegacyUpdaterShortcutMarker
    }

    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
    $archive = Join-Path $TempDir 'BepInExPack.zip'
    Write-Host 'Downloading the pinned BepInEx runtime...'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri $BepInExUrl -OutFile $archive

    $actualSha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $BepInExSha256) {
        throw 'BepInEx checksum mismatch; refusing to install.'
    }

    $expanded = Join-Path $TempDir 'expanded'
    Expand-Archive -LiteralPath $archive -DestinationPath $expanded
    $bepInExRoot = Join-Path $expanded 'BepInExPack_Valheim'
    if (-not (Test-Path -LiteralPath (Join-Path $bepInExRoot 'winhttp.dll') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $bepInExRoot 'BepInEx\core\BepInEx.dll') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $bepInExRoot 'doorstop_config.ini') -PathType Leaf)) {
        throw 'The BepInEx package had an unexpected layout.'
    }
    Set-DoorstopDisabled -ConfigPath (Join-Path $bepInExRoot 'doorstop_config.ini')

    if (Get-Process -Name 'valheim' -ErrorAction SilentlyContinue) {
        throw 'Valheim started during setup. Quit the game completely, then run this installer again.'
    }

    $bepInExDir = Join-Path $gameDir 'BepInEx'
    $pluginDir = Join-Path $bepInExDir 'plugins\BenheimQoL'
    if ((Test-Path -LiteralPath $pluginDir) -and -not (Test-Path -LiteralPath $pluginDir -PathType Container)) {
        throw "Expected a plugin directory but found another kind of file at: $pluginDir"
    }

    $pluginPath = Join-Path $pluginDir 'BenheimQoL.dll'
    $versionPath = Join-Path $pluginDir 'VERSION'
    $privateDiagnosticsPath = Join-Path $bepInExDir 'config\BenheimPrivateDiagnostics.cfg'
    $pluginBackup = Join-Path $TempDir 'BenheimQoL.previous.dll'
    $versionBackup = Join-Path $TempDir 'VERSION.previous'
    $privateDiagnosticsBackup = Join-Path $TempDir 'BenheimPrivateDiagnostics.previous.cfg'
    $shortcutBackup = Join-Path $TempDir 'Benheim.previous.lnk'
    $launcherBackup = Join-Path $TempDir 'launcher.previous'
    $legacyShortcutBackup = Join-Path $TempDir 'Benheim QoL.previous.lnk'
    $legacyUpdaterShortcutBackup = Join-Path $TempDir 'Update Benheim.previous.lnk'
    $legacyUpdaterRootBackup = Join-Path $TempDir 'updater.previous'
    $doorstopConfigPath = Join-Path $gameDir 'doorstop_config.ini'
    $doorstopConfigBackup = Join-Path $TempDir 'doorstop_config.previous.ini'

    $pluginHadPrevious = Test-Path -LiteralPath $pluginPath -PathType Leaf
    $versionHadPrevious = Test-Path -LiteralPath $versionPath -PathType Leaf
    $privateDiagnosticsHadPrevious = Test-Path -LiteralPath $privateDiagnosticsPath -PathType Leaf
    $shortcutHadPrevious = Test-Path -LiteralPath $shortcutPath -PathType Leaf
    $launcherHadPrevious = Test-Path -LiteralPath $launcherRoot
    $doorstopConfigHadPrevious = Save-DoorstopConfig `
        -ConfigPath $doorstopConfigPath `
        -BackupPath $doorstopConfigBackup
    $pluginReplaced = $false
    $versionReplaced = $false
    $privateDiagnosticsTouched = $false
    $launcherInstalled = $false
    $doorstopConfigTouched = $false

    if ($pluginHadPrevious) { Copy-Item -LiteralPath $pluginPath -Destination $pluginBackup }
    if ($versionHadPrevious) { Copy-Item -LiteralPath $versionPath -Destination $versionBackup }
    if ($privateDiagnosticsHadPrevious) {
        Copy-Item -LiteralPath $privateDiagnosticsPath -Destination $privateDiagnosticsBackup
    }
    if ($shortcutHadPrevious) { Copy-Item -LiteralPath $shortcutPath -Destination $shortcutBackup }

    $stagedLauncherRoot = Join-Path $TempDir 'launcher'
    New-Item -ItemType Directory -Path $stagedLauncherRoot -Force | Out-Null
    Copy-Item -LiteralPath $LauncherSource -Destination (Join-Path $stagedLauncherRoot 'launch-windows.ps1')
    Set-Content -LiteralPath (Join-Path $stagedLauncherRoot '.benheim-managed') -Value $LauncherMarker -NoNewline

    try {
        Write-Host 'Installing BepInEx and Benheim...'
        $doorstopConfigTouched = $true
        Get-ChildItem -LiteralPath $bepInExRoot -Force |
            Copy-Item -Destination $gameDir -Recurse -Force
        Set-DoorstopDisabled -ConfigPath $doorstopConfigPath

        New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
        $pluginTemp = Join-Path $pluginDir ('.BenheimQoL.dll.' + [guid]::NewGuid().ToString('N'))
        Copy-Item -LiteralPath $PluginDll -Destination $pluginTemp
        Move-Item -LiteralPath $pluginTemp -Destination $pluginPath -Force
        $pluginReplaced = $true
        $versionTemp = Join-Path $pluginDir ('.VERSION.' + [guid]::NewGuid().ToString('N'))
        Copy-Item -LiteralPath $VersionSource -Destination $versionTemp
        Move-Item -LiteralPath $versionTemp -Destination $versionPath -Force
        $versionReplaced = $true

        $privateDiagnosticsTouched = $true
        New-Item -ItemType Directory -Path (Split-Path -Parent $privateDiagnosticsPath) -Force | Out-Null
        if (Test-Path -LiteralPath $PrivateDiagnosticsSource -PathType Leaf) {
            $privateDiagnosticsTemp = Join-Path `
                (Split-Path -Parent $privateDiagnosticsPath) `
                ('.BenheimPrivateDiagnostics.cfg.' + [guid]::NewGuid().ToString('N'))
            Copy-Item -LiteralPath $PrivateDiagnosticsSource -Destination $privateDiagnosticsTemp
            Move-Item -LiteralPath $privateDiagnosticsTemp -Destination $privateDiagnosticsPath -Force
        }
        else {
            Remove-Item -LiteralPath $privateDiagnosticsPath -Force -ErrorAction SilentlyContinue
        }

        $disabledDir = Join-Path $bepInExDir 'disabled\MassFarming'
        Move-LegacyFile `
            -Source (Join-Path $bepInExDir 'plugins\MassFarming\MassFarming.dll') `
            -Destination (Join-Path $disabledDir 'MassFarming.dll') `
            -ArchivePrefix 'MassFarming.dll'
        Move-LegacyFile `
            -Source (Join-Path $bepInExDir 'config\xeio.MassFarming.cfg') `
            -Destination (Join-Path $disabledDir 'xeio.MassFarming.cfg') `
            -ArchivePrefix 'xeio.MassFarming.cfg'

        if ($launcherHadPrevious) {
            Move-Item -LiteralPath $launcherRoot -Destination $launcherBackup
        }
        Move-Item -LiteralPath $stagedLauncherRoot -Destination $launcherRoot
        $launcherInstalled = $true

        Write-Host 'Installing the Benheim desktop shortcut...'
        $stagedShortcut = Join-Path $TempDir 'Benheim.lnk'
        $shortcut = $shell.CreateShortcut($stagedShortcut)
        $shortcut.TargetPath = (Get-Command powershell.exe).Source
        $shortcut.Arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $launcherPath + '"'
        $shortcut.WorkingDirectory = [System.IO.Path]::GetTempPath()
        $shortcut.IconLocation = (Join-Path $gameDir 'valheim.exe') + ',0'
        $shortcut.Description = $ShortcutMarker
        $shortcut.Save()
        Copy-Item -LiteralPath $stagedShortcut -Destination $shortcutPath -Force

        if ($removeLegacyShortcut) {
            Move-Item -LiteralPath $legacyShortcutPath -Destination $legacyShortcutBackup
        }
        if ($removeLegacyUpdaterShortcut) {
            Move-Item -LiteralPath $legacyUpdaterShortcutPath -Destination $legacyUpdaterShortcutBackup
        }
        if ($removeLegacyUpdaterRoot) {
            Move-Item -LiteralPath $legacyUpdaterRoot -Destination $legacyUpdaterRootBackup
        }

    }
    catch {
        if ($doorstopConfigTouched) {
            Restore-DoorstopConfig `
                -ConfigPath $doorstopConfigPath `
                -BackupPath $doorstopConfigBackup `
                -HadPrevious $doorstopConfigHadPrevious
        }
        if ($pluginReplaced) {
            if ($pluginHadPrevious) {
                Copy-Item -LiteralPath $pluginBackup -Destination $pluginPath -Force
            }
            else {
                Remove-Item -LiteralPath $pluginPath -Force -ErrorAction SilentlyContinue
            }
        }
        if ($versionReplaced) {
            if ($versionHadPrevious) {
                Copy-Item -LiteralPath $versionBackup -Destination $versionPath -Force
            }
            else {
                Remove-Item -LiteralPath $versionPath -Force -ErrorAction SilentlyContinue
            }
        }
        if ($privateDiagnosticsTouched) {
            if ($privateDiagnosticsHadPrevious) {
                Copy-Item -LiteralPath $privateDiagnosticsBackup -Destination $privateDiagnosticsPath -Force
            }
            else {
                Remove-Item -LiteralPath $privateDiagnosticsPath -Force -ErrorAction SilentlyContinue
            }
        }
        if ($shortcutHadPrevious) {
            Copy-Item -LiteralPath $shortcutBackup -Destination $shortcutPath -Force
        }
        else {
            Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue
        }
        if ($launcherInstalled) {
            Remove-Item -LiteralPath $launcherRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
        if ($launcherHadPrevious -and (Test-Path -LiteralPath $launcherBackup)) {
            Move-Item -LiteralPath $launcherBackup -Destination $launcherRoot
        }
        if (Test-Path -LiteralPath $legacyShortcutBackup) {
            Move-Item -LiteralPath $legacyShortcutBackup -Destination $legacyShortcutPath
        }
        if (Test-Path -LiteralPath $legacyUpdaterShortcutBackup) {
            Move-Item -LiteralPath $legacyUpdaterShortcutBackup -Destination $legacyUpdaterShortcutPath
        }
        if (Test-Path -LiteralPath $legacyUpdaterRootBackup) {
            Move-Item -LiteralPath $legacyUpdaterRootBackup -Destination $legacyUpdaterRoot
        }
        throw
    }

    Write-Host ''
    Write-Host 'Installed Benheim.'
    Write-Host 'Open Benheim from your Desktop to play with mods.'
    Write-Host 'Use Steam Play to launch vanilla Valheim.'
    Write-Host 'Rerun this installer to update Benheim.'
    if (Test-Path -LiteralPath $PrivateDiagnosticsSource -PathType Leaf) {
        Write-Host 'This PRIVATE TEST install includes automatic typed diagnostic sharing.'
    }
}

try {
    Install-BenheimQoL
    exit 0
}
catch {
    Write-Host ''
    Write-Host ('Install failed: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
finally {
    if (Test-Path -LiteralPath $TempDir) {
        Remove-Item -LiteralPath $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
