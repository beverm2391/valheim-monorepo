function Set-DoorstopDisabled {
    param([Parameter(Mandatory = $true)][string]$ConfigPath)

    if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
        throw 'The BepInEx package did not install doorstop_config.ini.'
    }

    $content = Get-Content -LiteralPath $ConfigPath -Raw
    $pattern = '(?im)^(enabled\s*=\s*)(true|false)\s*$'
    if ([regex]::Matches($content, $pattern).Count -ne 1) {
        throw 'doorstop_config.ini had an unexpected UnityDoorstop enabled setting.'
    }

    $disabled = [regex]::Replace($content, $pattern, '${1}false')
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($ConfigPath, $disabled, $utf8WithoutBom)
}

function Save-DoorstopConfig {
    param(
        [Parameter(Mandatory = $true)][string]$ConfigPath,
        [Parameter(Mandatory = $true)][string]$BackupPath
    )

    $hadPrevious = Test-Path -LiteralPath $ConfigPath -PathType Leaf
    if ($hadPrevious) {
        Copy-Item -LiteralPath $ConfigPath -Destination $BackupPath
    }
    return $hadPrevious
}

function Restore-DoorstopConfig {
    param(
        [Parameter(Mandatory = $true)][string]$ConfigPath,
        [Parameter(Mandatory = $true)][string]$BackupPath,
        [Parameter(Mandatory = $true)][bool]$HadPrevious
    )

    if ($HadPrevious) {
        if (-not (Test-Path -LiteralPath $BackupPath -PathType Leaf)) {
            throw 'The previous doorstop_config.ini backup is missing; rollback cannot continue safely.'
        }
        Copy-Item -LiteralPath $BackupPath -Destination $ConfigPath -Force
    }
    else {
        Remove-Item -LiteralPath $ConfigPath -Force -ErrorAction SilentlyContinue
    }
}
