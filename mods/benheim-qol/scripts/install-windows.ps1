Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PluginDll = if ($env:BENHEIM_QOL_DLL) { $env:BENHEIM_QOL_DLL } else { Join-Path $ScriptDir 'BenheimQoL.dll' }
$BepInExUrl = if ($env:BENHEIM_QOL_BEPINEX_URL) { $env:BENHEIM_QOL_BEPINEX_URL } else { 'https://gcdn.thunderstore.io/live/repository/packages/denikson-BepInExPack_Valheim-5.4.2333.zip' }
$BepInExSha256 = if ($env:BENHEIM_QOL_BEPINEX_SHA256) { $env:BENHEIM_QOL_BEPINEX_SHA256.ToLowerInvariant() } else { '5dd24ccbcaa9260f714b200f23c4c15547e2aa5f06906cafcc0dee56db1bf716' }
$ShortcutMarker = 'BenheimQoL launcher managed by the BenheimQoL installer'
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

    $bepInExDir = Join-Path $gameDir 'BepInEx'
    $pluginDir = Join-Path $bepInExDir 'plugins\BenheimQoL'
    if ((Test-Path -LiteralPath $pluginDir) -and -not (Test-Path -LiteralPath $pluginDir -PathType Container)) {
        throw "Expected a plugin directory but found another kind of file at: $pluginDir"
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

    Write-Host 'Installing BepInEx and Benheim...'
    Get-ChildItem -LiteralPath $bepInExRoot -Force |
        Copy-Item -Destination $gameDir -Recurse -Force
    New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    $pluginTemp = Join-Path $pluginDir ('.BenheimQoL.dll.' + [guid]::NewGuid().ToString('N'))
    Copy-Item -LiteralPath $PluginDll -Destination $pluginTemp
    Move-Item -LiteralPath $pluginTemp -Destination (Join-Path $pluginDir 'BenheimQoL.dll') -Force

    $disabledDir = Join-Path $bepInExDir 'disabled\MassFarming'
    Move-LegacyFile `
        -Source (Join-Path $bepInExDir 'plugins\MassFarming\MassFarming.dll') `
        -Destination (Join-Path $disabledDir 'MassFarming.dll') `
        -ArchivePrefix 'MassFarming.dll'
    Move-LegacyFile `
        -Source (Join-Path $bepInExDir 'config\xeio.MassFarming.cfg') `
        -Destination (Join-Path $disabledDir 'xeio.MassFarming.cfg') `
        -ArchivePrefix 'xeio.MassFarming.cfg'

    $desktop = [Environment]::GetFolderPath('Desktop')
    if (-not $desktop) {
        throw 'Windows did not report a Desktop folder, so the launcher shortcut could not be created.'
    }

    $shortcutPath = Join-Path $desktop 'Benheim.lnk'
    $legacyShortcutPath = Join-Path $desktop 'Benheim QoL.lnk'
    $shell = New-Object -ComObject WScript.Shell

    if (Test-Path -LiteralPath $legacyShortcutPath -PathType Leaf) {
        $legacyShortcut = $shell.CreateShortcut($legacyShortcutPath)
        if ($legacyShortcut.Description -eq $ShortcutMarker) {
            Remove-Item -LiteralPath $legacyShortcutPath -Force
        }
    }

    if (Test-Path -LiteralPath $shortcutPath -PathType Leaf) {
        $existingShortcut = $shell.CreateShortcut($shortcutPath)
        if ($existingShortcut.Description -ne $ShortcutMarker) {
            throw "Refusing to replace an unrelated shortcut at: $shortcutPath"
        }
    }

    Write-Host 'Installing the Benheim desktop shortcut...'
    $stagedShortcut = Join-Path $TempDir 'Benheim.lnk'
    $shortcut = $shell.CreateShortcut($stagedShortcut)
    $shortcut.TargetPath = Join-Path $env:WINDIR 'explorer.exe'
    $shortcut.Arguments = 'steam://rungameid/892970'
    $shortcut.WorkingDirectory = $gameDir
    $shortcut.IconLocation = (Join-Path $gameDir 'valheim.exe') + ',0'
    $shortcut.Description = $ShortcutMarker
    $shortcut.Save()
    Copy-Item -LiteralPath $stagedShortcut -Destination $shortcutPath -Force

    Write-Host ''
    Write-Host 'Installed Benheim.'
    Write-Host 'Open Benheim from your Desktop to play.'
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
