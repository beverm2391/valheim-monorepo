Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PluginDll = if ($env:BENHEIM_QOL_DLL) { $env:BENHEIM_QOL_DLL } else { Join-Path $ScriptDir 'BenheimQoL.dll' }
$BepInExUrl = if ($env:BENHEIM_QOL_BEPINEX_URL) { $env:BENHEIM_QOL_BEPINEX_URL } else { 'https://gcdn.thunderstore.io/live/repository/packages/denikson-BepInExPack_Valheim-5.4.2333.zip' }
$BepInExSha256 = if ($env:BENHEIM_QOL_BEPINEX_SHA256) { $env:BENHEIM_QOL_BEPINEX_SHA256.ToLowerInvariant() } else { '5dd24ccbcaa9260f714b200f23c4c15547e2aa5f06906cafcc0dee56db1bf716' }
$ShortcutMarker = 'BenheimQoL launcher managed by the BenheimQoL installer'
$UpdaterShortcutMarker = 'Benheim updater managed by the Benheim installer'
$UpdaterMarker = 'Benheim updater managed directory v1'
$UpdaterScript = Join-Path $ScriptDir 'update-windows.ps1'
$UpdaterWrapper = Join-Path $ScriptDir 'Update Benheim.cmd'
$LauncherScript = Join-Path $ScriptDir 'launch-windows.ps1'
$VersionSource = Join-Path $ScriptDir 'VERSION'
$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("BenheimQoL-" + [guid]::NewGuid().ToString('N'))

function Find-ValheimGameDir {
    if ($env:BENHEIM_QOL_GAME_DIR) {
        return $env:BENHEIM_QOL_GAME_DIR
    }

    $steamRoots = New-Object System.Collections.Generic.List[string]
    try {
        $steamRoot = (Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction Stop).SteamPath
        if ($steamRoot) {
            $steamRoots.Add($steamRoot)
        }
    }
    catch {
        # The final error names the missing game and the supported override.
    }

    if ($steamRoots.Count -gt 0) {
        $libraryFile = Join-Path $steamRoots[0] 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $libraryFile -PathType Leaf) {
            foreach ($line in Get-Content -LiteralPath $libraryFile) {
                if ($line -match '"path"\s+"([^"]+)"') {
                    $libraryRoot = $Matches[1].Replace('\\', '\')
                    if (-not $steamRoots.Contains($libraryRoot)) {
                        $steamRoots.Add($libraryRoot)
                    }
                }
            }
        }
    }

    foreach ($steamRoot in $steamRoots) {
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
    $archive = Join-Path $destinationDir "$ArchivePrefix.$timestamp"
    Move-Item -LiteralPath $Source -Destination $archive
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
    if (-not (Test-Path -LiteralPath $UpdaterScript -PathType Leaf) -or
        -not (Test-Path -LiteralPath $UpdaterWrapper -PathType Leaf) -or
        -not (Test-Path -LiteralPath $LauncherScript -PathType Leaf)) {
        throw 'The Benheim launcher or updater files are missing beside the installer.'
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

    $localAppData = [Environment]::GetFolderPath('LocalApplicationData')
    if (-not $localAppData) {
        throw 'Windows did not report a Local AppData folder, so the updater could not be installed.'
    }
    $updaterRoot = Join-Path $localAppData 'Benheim'
    $updaterRootExisted = Test-Path -LiteralPath $updaterRoot
    $updaterMarkerPath = Join-Path $updaterRoot '.benheim-managed'
    if (Test-Path -LiteralPath $updaterRoot) {
        if (-not (Test-Path -LiteralPath $updaterRoot -PathType Container) -or
            -not (Test-Path -LiteralPath $updaterMarkerPath -PathType Leaf) -or
            (Get-Content -LiteralPath $updaterMarkerPath -Raw).Trim() -ne $UpdaterMarker) {
            throw "Refusing to replace an unrelated or damaged updater directory at: $updaterRoot"
        }
    }

    $updaterVersionsRoot = Join-Path $updaterRoot 'versions'
    if ((Test-Path -LiteralPath $updaterVersionsRoot) -and
        -not (Test-Path -LiteralPath $updaterVersionsRoot -PathType Container)) {
        throw "Refusing to replace a damaged updater versions directory at: $updaterVersionsRoot"
    }
    $updaterVersion = (Get-FileHash -LiteralPath $PluginDll -Algorithm SHA256).Hash.ToLowerInvariant()
    $updaterDir = Join-Path $updaterVersionsRoot $updaterVersion
    $updaterVersionExisted = Test-Path -LiteralPath $updaterDir
    if ($updaterVersionExisted) {
        $installedUpdaterScript = Join-Path $updaterDir 'update-windows.ps1'
        $installedUpdaterWrapper = Join-Path $updaterDir 'Update Benheim.cmd'
        $installedLauncherScript = Join-Path $updaterDir 'launch-windows.ps1'
        $installedRuntimeVersion = Join-Path $updaterDir 'VERSION'
        if (-not (Test-Path -LiteralPath $installedUpdaterScript -PathType Leaf) -or
            -not (Test-Path -LiteralPath $installedUpdaterWrapper -PathType Leaf) -or
            -not (Test-Path -LiteralPath $installedLauncherScript -PathType Leaf) -or
            -not (Test-Path -LiteralPath $installedRuntimeVersion -PathType Leaf) -or
            (Get-FileHash -LiteralPath $installedUpdaterScript -Algorithm SHA256).Hash -ne
                (Get-FileHash -LiteralPath $UpdaterScript -Algorithm SHA256).Hash -or
            (Get-FileHash -LiteralPath $installedUpdaterWrapper -Algorithm SHA256).Hash -ne
                (Get-FileHash -LiteralPath $UpdaterWrapper -Algorithm SHA256).Hash -or
            (Get-FileHash -LiteralPath $installedLauncherScript -Algorithm SHA256).Hash -ne
                (Get-FileHash -LiteralPath $LauncherScript -Algorithm SHA256).Hash -or
            (Get-FileHash -LiteralPath $installedRuntimeVersion -Algorithm SHA256).Hash -ne
                (Get-FileHash -LiteralPath $VersionSource -Algorithm SHA256).Hash) {
            throw "Refusing to replace a damaged updater version at: $updaterDir"
        }
    }

    $bepInExDir = Join-Path $gameDir 'BepInEx'
    $pluginDir = Join-Path $bepInExDir 'plugins\BenheimQoL'
    if ((Test-Path -LiteralPath $pluginDir) -and -not (Test-Path -LiteralPath $pluginDir -PathType Container)) {
        throw "Expected a plugin directory but found another kind of file at: $pluginDir"
    }

    $desktop = [Environment]::GetFolderPath('Desktop')
    if (-not $desktop) {
        throw 'Windows did not report a Desktop folder, so the launcher shortcuts could not be created.'
    }

    $shortcutPath = Join-Path $desktop 'Benheim.lnk'
    $updaterShortcutPath = Join-Path $desktop 'Update Benheim.lnk'
    $legacyShortcutPath = Join-Path $desktop 'Benheim QoL.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $removeLegacyShortcut = $false

    if (Test-Path -LiteralPath $legacyShortcutPath -PathType Leaf) {
        $legacyShortcut = $shell.CreateShortcut($legacyShortcutPath)
        $removeLegacyShortcut = $legacyShortcut.Description -eq $ShortcutMarker
    }

    if (Test-Path -LiteralPath $shortcutPath -PathType Leaf) {
        $existingShortcut = $shell.CreateShortcut($shortcutPath)
        if ($existingShortcut.Description -ne $ShortcutMarker) {
            throw "Refusing to replace an unrelated shortcut at: $shortcutPath"
        }
    }

    if (Test-Path -LiteralPath $updaterShortcutPath -PathType Leaf) {
        $existingUpdaterShortcut = $shell.CreateShortcut($updaterShortcutPath)
        if ($existingUpdaterShortcut.Description -ne $UpdaterShortcutMarker) {
            throw "Refusing to replace an unrelated shortcut at: $updaterShortcutPath"
        }
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
        -not (Test-Path -LiteralPath (Join-Path $bepInExRoot 'BepInEx\core\BepInEx.dll') -PathType Leaf)) {
        throw 'The BepInEx package had an unexpected layout.'
    }

    $stagedUpdaterDir = Join-Path $TempDir 'updater'
    New-Item -ItemType Directory -Path $stagedUpdaterDir -Force | Out-Null
    Copy-Item -LiteralPath $UpdaterScript -Destination (Join-Path $stagedUpdaterDir 'update-windows.ps1')
    Copy-Item -LiteralPath $UpdaterWrapper -Destination (Join-Path $stagedUpdaterDir 'Update Benheim.cmd')
    Copy-Item -LiteralPath $LauncherScript -Destination (Join-Path $stagedUpdaterDir 'launch-windows.ps1')
    Copy-Item -LiteralPath $VersionSource -Destination (Join-Path $stagedUpdaterDir 'VERSION')

    if (Get-Process -Name 'valheim' -ErrorAction SilentlyContinue) {
        throw 'Valheim started during setup. Quit the game completely, then run this installer again.'
    }

    $pluginPath = Join-Path $pluginDir 'BenheimQoL.dll'
    $installedVersionPath = Join-Path $pluginDir 'VERSION'
    $pluginBackup = Join-Path $TempDir 'BenheimQoL.previous.dll'
    $versionBackup = Join-Path $TempDir 'VERSION.previous'
    $pluginHadPrevious = Test-Path -LiteralPath $pluginPath -PathType Leaf
    $versionHadPrevious = Test-Path -LiteralPath $installedVersionPath -PathType Leaf
    $pluginReplaced = $false
    $versionReplaced = $false
    if ($pluginHadPrevious) {
        Copy-Item -LiteralPath $pluginPath -Destination $pluginBackup
    }
    if ($versionHadPrevious) {
        Copy-Item -LiteralPath $installedVersionPath -Destination $versionBackup
    }

    $shortcutHadPrevious = Test-Path -LiteralPath $shortcutPath -PathType Leaf
    $updaterShortcutHadPrevious = Test-Path -LiteralPath $updaterShortcutPath -PathType Leaf
    $shortcutBackup = Join-Path $TempDir 'Benheim.previous.lnk'
    $updaterShortcutBackup = Join-Path $TempDir 'Update Benheim.previous.lnk'
    if ($shortcutHadPrevious) {
        Copy-Item -LiteralPath $shortcutPath -Destination $shortcutBackup
    }
    if ($updaterShortcutHadPrevious) {
        Copy-Item -LiteralPath $updaterShortcutPath -Destination $updaterShortcutBackup
    }

    try {
        Write-Host 'Installing BepInEx and Benheim...'
        Get-ChildItem -LiteralPath $bepInExRoot -Force |
            Copy-Item -Destination $gameDir -Recurse -Force
        New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
        $pluginTemp = Join-Path $pluginDir ('.BenheimQoL.dll.' + [guid]::NewGuid().ToString('N'))
        Copy-Item -LiteralPath $PluginDll -Destination $pluginTemp
        Move-Item -LiteralPath $pluginTemp -Destination $pluginPath -Force
        $pluginReplaced = $true
        $versionTemp = Join-Path $pluginDir ('.VERSION.' + [guid]::NewGuid().ToString('N'))
        Copy-Item -LiteralPath $VersionSource -Destination $versionTemp
        Move-Item -LiteralPath $versionTemp -Destination $installedVersionPath -Force
        $versionReplaced = $true

    $disabledDir = Join-Path $bepInExDir 'disabled\MassFarming'
    Move-LegacyFile `
        -Source (Join-Path $bepInExDir 'plugins\MassFarming\MassFarming.dll') `
        -Destination (Join-Path $disabledDir 'MassFarming.dll') `
        -ArchivePrefix 'MassFarming.dll'
    Move-LegacyFile `
        -Source (Join-Path $bepInExDir 'config\xeio.MassFarming.cfg') `
        -Destination (Join-Path $disabledDir 'xeio.MassFarming.cfg') `
        -ArchivePrefix 'xeio.MassFarming.cfg'

    Write-Host 'Installing the Benheim desktop shortcut...'
    $stagedShortcut = Join-Path $TempDir 'Benheim.lnk'
    $shortcut = $shell.CreateShortcut($stagedShortcut)
    $shortcut.TargetPath = (Get-Command powershell.exe).Source
    $shortcut.Arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + (Join-Path $updaterDir 'launch-windows.ps1') + '"'
    $shortcut.WorkingDirectory = $gameDir
    $shortcut.IconLocation = (Join-Path $gameDir 'valheim.exe') + ',0'
    $shortcut.Description = $ShortcutMarker
    $shortcut.Save()
    Copy-Item -LiteralPath $stagedShortcut -Destination $shortcutPath -Force

    Write-Host 'Installing the Benheim updater...'
    if (-not (Test-Path -LiteralPath $updaterDir)) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $updaterDir) -Force | Out-Null
        Move-Item -LiteralPath $stagedUpdaterDir -Destination $updaterDir
    }
    if (-not (Test-Path -LiteralPath $updaterRoot)) {
        New-Item -ItemType Directory -Path $updaterRoot -Force | Out-Null
    }
    Set-Content -LiteralPath $updaterMarkerPath -Value $UpdaterMarker -NoNewline

    $stagedUpdaterShortcut = Join-Path $TempDir 'Update Benheim.lnk'
    $updaterShortcut = $shell.CreateShortcut($stagedUpdaterShortcut)
    $updaterShortcut.TargetPath = $env:ComSpec
    $updaterShortcut.Arguments = '/c ""' + (Join-Path $updaterDir 'Update Benheim.cmd') + '""'
    $updaterShortcut.WorkingDirectory = [System.IO.Path]::GetTempPath()
    $updaterShortcut.IconLocation = (Join-Path $gameDir 'valheim.exe') + ',0'
    $updaterShortcut.Description = $UpdaterShortcutMarker
    $updaterShortcut.Save()
    Copy-Item -LiteralPath $stagedUpdaterShortcut -Destination $updaterShortcutPath -Force

    if ($removeLegacyShortcut) {
        Remove-Item -LiteralPath $legacyShortcutPath -Force
    }
    }
    catch {
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
                Copy-Item -LiteralPath $versionBackup -Destination $installedVersionPath -Force
            }
            else {
                Remove-Item -LiteralPath $installedVersionPath -Force -ErrorAction SilentlyContinue
            }
        }
        if ($shortcutHadPrevious) {
            Copy-Item -LiteralPath $shortcutBackup -Destination $shortcutPath -Force
        }
        else {
            Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue
        }
        if ($updaterShortcutHadPrevious) {
            Copy-Item -LiteralPath $updaterShortcutBackup -Destination $updaterShortcutPath -Force
        }
        else {
            Remove-Item -LiteralPath $updaterShortcutPath -Force -ErrorAction SilentlyContinue
        }
        if (-not $updaterVersionExisted -and (Test-Path -LiteralPath $updaterDir)) {
            Remove-Item -LiteralPath $updaterDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        if (-not $updaterRootExisted -and (Test-Path -LiteralPath $updaterRoot)) {
            Remove-Item -LiteralPath $updaterRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
        throw
    }

    Write-Host ''
    Write-Host 'Installed Benheim.'
    Write-Host 'Open Benheim from your Desktop to play.'
    Write-Host 'Benheim will offer stable updates before launch.'
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
