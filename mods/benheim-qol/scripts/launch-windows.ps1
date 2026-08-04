Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$LogDir = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'BenheimLauncher'
$LogFile = Join-Path $LogDir 'launch.log'

function Write-LaunchLog {
    param([Parameter(Mandatory = $true)][string]$Message)

    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    Add-Content -LiteralPath $LogFile -Value ("{0:u} {1}" -f (Get-Date), $Message)
}

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
            # Try the next standard Steam registry location.
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

    throw 'Valheim was not found in any Steam library. Install or repair Valheim, then open Benheim again.'
}

function Find-SteamExecutable {
    foreach ($steamRoot in Get-SteamRoots) {
        $candidate = Join-Path $steamRoot 'steam.exe'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    throw 'Steam was not found. Install or repair Steam, then open Benheim again.'
}

function Wait-ForSteamReady {
    $steamProcesses = @(Get-Process -Name 'steam' -ErrorAction SilentlyContinue)
    $startedSteam = $steamProcesses.Count -eq 0

    if ($startedSteam) {
        Write-LaunchLog 'Starting Steam.'
        Start-Process -FilePath (Find-SteamExecutable) -ArgumentList '-silent'
    }

    $deadline = (Get-Date).AddSeconds(90)
    do {
        foreach ($steamProcess in @(Get-Process -Name 'steam' -ErrorAction SilentlyContinue)) {
            try {
                if ($steamProcess.Responding) {
                    if ($startedSteam) {
                        # Steam can report a responsive process just before its
                        # client IPC is ready for a direct game launch.
                        Start-Sleep -Seconds 3
                    }
                    return
                }
            }
            catch {
                # Steam can replace its bootstrap process during cold startup.
            }
        }

        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)

    throw 'Steam did not become ready. Open Steam, sign in, and try Benheim again.'
}

try {
    $gameDir = Find-ValheimGameDir
    $valheimExecutable = Join-Path $gameDir 'valheim.exe'
    $doorstopConfig = Join-Path $gameDir 'doorstop_config.ini'
    $plugin = Join-Path $gameDir 'BepInEx\plugins\BenheimQoL\BenheimQoL.dll'

    if (-not (Test-Path -LiteralPath $valheimExecutable -PathType Leaf) -or
        -not (Test-Path -LiteralPath $doorstopConfig -PathType Leaf) -or
        -not (Test-Path -LiteralPath $plugin -PathType Leaf)) {
        throw 'Benheim is not installed correctly. Run the Windows installer again.'
    }

    $doorstopMatches = [regex]::Matches(
        (Get-Content -LiteralPath $doorstopConfig -Raw),
        '(?im)^enabled\s*=\s*false\s*$'
    )
    if ($doorstopMatches.Count -ne 1) {
        throw 'The normal Steam launch is not configured for vanilla Valheim. Run the Windows installer again.'
    }

    Write-LaunchLog 'Launching Benheim.'
    Wait-ForSteamReady
    Start-Process `
        -FilePath $valheimExecutable `
        -ArgumentList '--doorstop-enabled', 'true' `
        -WorkingDirectory $gameDir
}
catch {
    Write-LaunchLog "Launch failed: $($_.Exception.Message)"
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show(
        "Benheim could not launch.`n`n$($_.Exception.Message)",
        'Benheim',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    ) | Out-Null
    exit 1
}
