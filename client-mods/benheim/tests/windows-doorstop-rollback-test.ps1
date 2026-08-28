Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '..\scripts\windows-doorstop-config.ps1')

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('BenheimDoorstopRollback-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null

try {
    $configPath = Join-Path $testRoot 'doorstop_config.ini'
    $backupPath = Join-Path $testRoot 'doorstop_config.previous.ini'

    # Include a BOM, CRLFs, comments, and trailing whitespace so equality proves
    # rollback restores the original bytes rather than reconstructing the text.
    $originalBytes = [byte[]](0xef, 0xbb, 0xbf) +
        [System.Text.Encoding]::UTF8.GetBytes("[General]`r`nenabled = true   `r`n# preserve me`r`n")
    [System.IO.File]::WriteAllBytes($configPath, $originalBytes)

    $hadPrevious = Save-DoorstopConfig -ConfigPath $configPath -BackupPath $backupPath
    if (-not $hadPrevious) { throw 'Existing config was not detected.' }
    Set-DoorstopDisabled -ConfigPath $configPath
    Restore-DoorstopConfig -ConfigPath $configPath -BackupPath $backupPath -HadPrevious $hadPrevious

    $restoredBytes = [System.IO.File]::ReadAllBytes($configPath)
    if ([System.Convert]::ToBase64String($restoredBytes) -ne [System.Convert]::ToBase64String($originalBytes)) {
        throw 'Rollback did not restore the previous config exactly.'
    }

    Remove-Item -LiteralPath $configPath -Force
    Remove-Item -LiteralPath $backupPath -Force
    $hadPrevious = Save-DoorstopConfig -ConfigPath $configPath -BackupPath $backupPath
    if ($hadPrevious) { throw 'Missing config was reported as existing.' }
    [System.IO.File]::WriteAllText($configPath, "[General]`nenabled=true`n")
    Set-DoorstopDisabled -ConfigPath $configPath
    Restore-DoorstopConfig -ConfigPath $configPath -BackupPath $backupPath -HadPrevious $hadPrevious
    if (Test-Path -LiteralPath $configPath) {
        throw 'Rollback retained a config that did not exist before installation.'
    }

    [System.IO.File]::WriteAllText($configPath, "[General]`nenabled=true`n")
    Set-DoorstopDisabled -ConfigPath $configPath
    if ((Get-Content -LiteralPath $configPath -Raw) -notmatch '(?im)^enabled\s*=\s*false\s*$') {
        throw 'Successful mutation did not retain enabled=false.'
    }

    Write-Host 'Windows Doorstop config rollback tests passed'
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}
