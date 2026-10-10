function Test-ResidentSpeechSource {
    param([Parameter(Mandatory = $true)][string]$Source)

    $supplied = Test-Path -LiteralPath $Source -PathType Leaf
    if ($supplied) {
        $lines = @(Get-Content -LiteralPath $Source)
        if ((Get-Item -LiteralPath $Source).Length -gt 4096 -or
            $lines.Count -ne 2 -or
            $lines[0] -cne 'BENHEIM_PRIVATE_SPEECH_V1' -or
            $lines[1] -cnotmatch '^api_key=\S+$') {
            throw 'The private resident speech configuration is invalid.'
        }
    }
    elseif (Test-Path -LiteralPath $Source) {
        throw 'The private resident speech configuration is not a file.'
    }

    return [bool]$supplied
}

function New-ResidentSpeechInstallState {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination,
        [Parameter(Mandatory = $true)][string]$Backup,
        [Parameter(Mandatory = $true)][bool]$Supplied
    )

    $hadPrevious = Test-Path -LiteralPath $Destination -PathType Leaf
    if ((Test-Path -LiteralPath $Destination) -and -not $hadPrevious) {
        throw 'Refusing to replace a non-file resident speech configuration path.'
    }

    $wasManaged = $false
    if ($hadPrevious) {
        $wasManaged =
            (Get-Content -LiteralPath $Destination -TotalCount 1) -ceq 'BENHEIM_PRIVATE_SPEECH_V1'
    }
    if ($Supplied -and $hadPrevious -and -not $wasManaged) {
        throw 'Refusing to replace an unrecognized resident speech configuration.'
    }

    return [pscustomobject]@{
        Source = $Source
        Destination = $Destination
        Backup = $Backup
        Supplied = $Supplied
        HadPrevious = $hadPrevious
        WasManaged = $wasManaged
        Touched = $false
        TempPath = $null
    }
}

function Backup-ResidentSpeechConfig {
    param([Parameter(Mandatory = $true)][pscustomobject]$State)

    if ($State.HadPrevious) {
        Copy-Item -LiteralPath $State.Destination -Destination $State.Backup
    }
}

function Install-ResidentSpeechConfig {
    param([Parameter(Mandatory = $true)][pscustomobject]$State)

    if ($State.Supplied) {
        $State.Touched = $true
        $destinationDir = Split-Path -Parent $State.Destination
        New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
        $State.TempPath = Join-Path `
            $destinationDir `
            ('.GEORGE-SPEECH.cfg.' + [guid]::NewGuid().ToString('N'))
        Copy-Item -LiteralPath $State.Source -Destination $State.TempPath
        Move-Item -LiteralPath $State.TempPath -Destination $State.Destination -Force
    }
    elseif ($State.WasManaged) {
        # Ordinary reinstall without a private config removes only a file this
        # installer previously owned; a local file with another marker survives.
        $State.Touched = $true
        Remove-Item -LiteralPath $State.Destination -Force
    }
}

function Confirm-ResidentSpeechInstall {
    param([Parameter(Mandatory = $true)][pscustomobject]$State)

    if ($State.Supplied -and
        (Get-FileHash -LiteralPath $State.Destination -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $State.Source -Algorithm SHA256).Hash) {
        throw 'The installed resident speech configuration differs from the package.'
    }
    if (-not $State.Supplied -and $State.WasManaged -and
        (Test-Path -LiteralPath $State.Destination)) {
        throw 'The previous package-managed resident speech configuration was not removed.'
    }
}

function Restore-ResidentSpeechConfig {
    param([Parameter(Mandatory = $true)][pscustomobject]$State)

    if ($State.Touched) {
        if ($State.HadPrevious) {
            Copy-Item -LiteralPath $State.Backup -Destination $State.Destination -Force
        }
        else {
            Remove-Item -LiteralPath $State.Destination -Force -ErrorAction SilentlyContinue
        }
    }
}

function Clear-ResidentSpeechTemp {
    param([Parameter(Mandatory = $true)][pscustomobject]$State)

    if ($State.TempPath -and (Test-Path -LiteralPath $State.TempPath)) {
        Remove-Item -LiteralPath $State.TempPath -Force -ErrorAction SilentlyContinue
    }
}
