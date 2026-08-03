Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$AssetName = if ($env:BENHEIM_UPDATE_ASSET) { $env:BENHEIM_UPDATE_ASSET } else { 'Benheim-Windows.zip' }
$ReleaseBase = if ($env:BENHEIM_UPDATE_BASE_URL) { $env:BENHEIM_UPDATE_BASE_URL.TrimEnd('/') } else { 'https://github.com/beverm2391/valheim-server/releases/latest/download' }
$ChecksumsUrl = if ($env:BENHEIM_UPDATE_SHA256SUMS_URL) { $env:BENHEIM_UPDATE_SHA256SUMS_URL } else { "$ReleaseBase/SHA256SUMS.txt" }
$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("BenheimUpdate-" + [guid]::NewGuid().ToString('N'))

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
        # The final error explains how to repair an unexpected installation.
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

    throw 'Benheim is not installed normally. Download the latest Windows package and run Install Benheim.cmd.'
}

function Update-Benheim {
    if ($env:OS -ne 'Windows_NT') {
        throw 'This updater requires Windows.'
    }
    if (Get-Process -Name 'valheim' -ErrorAction SilentlyContinue) {
        throw 'Quit Valheim completely, then open Update Benheim again.'
    }

    $gameDir = Find-ValheimGameDir
    $installedPlugin = Join-Path $gameDir 'BepInEx\plugins\BenheimQoL\BenheimQoL.dll'
    if (-not (Test-Path -LiteralPath $installedPlugin -PathType Leaf)) {
        throw 'Benheim is not installed normally. Download the latest Windows package and run Install Benheim.cmd.'
    }

    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    Write-Host 'Checking for a Benheim update...'
    $checksumsPath = Join-Path $TempDir 'SHA256SUMS.txt'
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $ChecksumsUrl -OutFile $checksumsPath
    }
    catch {
        throw 'Could not reach the Benheim release. Your current installation was not changed.'
    }

    $escapedAsset = [regex]::Escape($AssetName)
    $checksumLine = Get-Content -LiteralPath $checksumsPath |
        Where-Object { $_ -match "^([0-9A-Fa-f]{64})\s+\*?$escapedAsset$" } |
        Select-Object -First 1
    if (-not $checksumLine) {
        throw "The latest release does not contain a checksum for $AssetName. Your current installation was not changed."
    }
    $expectedSha256 = ([regex]::Match($checksumLine, '^([0-9A-Fa-f]{64})')).Groups[1].Value.ToLowerInvariant()

    $archive = Join-Path $TempDir $AssetName
    try {
        Invoke-WebRequest -UseBasicParsing -Uri "$ReleaseBase/$AssetName" -OutFile $archive
    }
    catch {
        throw 'The update download did not finish. Your current installation was not changed.'
    }

    $actualSha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $expectedSha256) {
        throw 'The update checksum did not match. Your current installation was not changed.'
    }

    $expanded = Join-Path $TempDir 'expanded'
    try {
        Expand-Archive -LiteralPath $archive -DestinationPath $expanded
    }
    catch {
        throw 'The update package could not be opened. Your current installation was not changed.'
    }

    $installers = @(Get-ChildItem -LiteralPath $expanded -Filter 'install-windows.ps1' -File -Recurse)
    if ($installers.Count -ne 1) {
        throw 'The update package has an unexpected layout. Your current installation was not changed.'
    }

    $installer = $installers[0].FullName
    $packageDir = Split-Path -Parent $installer
    $packagePlugin = Join-Path $packageDir 'BenheimQoL.dll'
    if (-not (Test-Path -LiteralPath $packagePlugin -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $packageDir 'Update Benheim.cmd') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $packageDir 'update-windows.ps1') -PathType Leaf)) {
        throw 'The update package is incomplete. Your current installation was not changed.'
    }

    $installedSha256 = (Get-FileHash -LiteralPath $installedPlugin -Algorithm SHA256).Hash
    $packageSha256 = (Get-FileHash -LiteralPath $packagePlugin -Algorithm SHA256).Hash
    if ($installedSha256 -eq $packageSha256) {
        Write-Host 'Benheim is already up to date.' -ForegroundColor Green
        return
    }

    Write-Host 'Installing the verified update...'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer
    if ($LASTEXITCODE -ne 0) {
        throw 'The installer could not finish the update. Download the latest Windows package and run Install Benheim.cmd.'
    }

    Write-Host ''
    Write-Host 'Benheim was updated. You can open Benheim normally.' -ForegroundColor Green
}

try {
    Update-Benheim
    exit 0
}
catch {
    Write-Host ''
    Write-Host ('Update failed: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
finally {
    if (Test-Path -LiteralPath $TempDir) {
        Remove-Item -LiteralPath $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
