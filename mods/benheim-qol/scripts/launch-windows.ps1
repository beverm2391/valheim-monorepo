Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$InstalledVersionPath = Join-Path $ScriptDir 'VERSION'
$UpdateScript = Join-Path $ScriptDir 'update-windows.ps1'
$LatestVersionUrl = if ($env:BENHEIM_UPDATE_VERSION_URL) { $env:BENHEIM_UPDATE_VERSION_URL } else { 'https://github.com/beverm2391/valheim-server/releases/latest/download/VERSION' }
$LogDir = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Benheim'
$LogFile = Join-Path $LogDir 'launch.log'

function Write-LaunchLog {
    param([Parameter(Mandatory = $true)][string]$Message)
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    Add-Content -LiteralPath $LogFile -Value ("{0:u} {1}" -f (Get-Date), $Message)
}

function Show-ChoiceDialog {
    param(
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][string]$Message,
        [Parameter(Mandatory = $true)][string]$PrimaryText,
        [Parameter(Mandatory = $true)][string]$SecondaryText
    )

    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $form = New-Object System.Windows.Forms.Form
    $form.Text = $Title
    $form.ClientSize = New-Object System.Drawing.Size(430, 150)
    $form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
    $form.MaximizeBox = $false
    $form.MinimizeBox = $false
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
    $form.TopMost = $true

    $label = New-Object System.Windows.Forms.Label
    $label.Location = New-Object System.Drawing.Point(20, 18)
    $label.Size = New-Object System.Drawing.Size(390, 58)
    $label.Text = $Message
    $form.Controls.Add($label)

    $secondary = New-Object System.Windows.Forms.Button
    $secondary.Location = New-Object System.Drawing.Point(88, 94)
    $secondary.Size = New-Object System.Drawing.Size(150, 32)
    $secondary.Text = $SecondaryText
    $secondary.DialogResult = [System.Windows.Forms.DialogResult]::No
    $form.Controls.Add($secondary)

    $primary = New-Object System.Windows.Forms.Button
    $primary.Location = New-Object System.Drawing.Point(248, 94)
    $primary.Size = New-Object System.Drawing.Size(162, 32)
    $primary.Text = $PrimaryText
    $primary.DialogResult = [System.Windows.Forms.DialogResult]::Yes
    $form.Controls.Add($primary)

    $form.AcceptButton = $primary
    $form.CancelButton = $secondary
    return $form.ShowDialog()
}

function Get-LatestVersion {
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $response = Invoke-WebRequest -UseBasicParsing -Uri $LatestVersionUrl -TimeoutSec 4
        return [version]$response.Content.Trim()
    }
    catch {
        Write-LaunchLog "Update check skipped: $($_.Exception.Message)"
        return $null
    }
}

try {
    Write-LaunchLog 'Launching Benheim.'

    if ((Test-Path -LiteralPath $InstalledVersionPath -PathType Leaf) -and
        (Test-Path -LiteralPath $UpdateScript -PathType Leaf)) {
        $installedVersion = [version](Get-Content -LiteralPath $InstalledVersionPath -Raw).Trim()
        $latestVersion = Get-LatestVersion

        if ($null -ne $latestVersion -and $latestVersion -gt $installedVersion) {
            $choice = Show-ChoiceDialog `
                -Title 'Benheim update available' `
                -Message "Benheim $latestVersion is available. You have $installedVersion." `
                -PrimaryText 'Update and launch' `
                -SecondaryText 'Launch current version'

            if ($choice -eq [System.Windows.Forms.DialogResult]::Yes) {
                Write-LaunchLog "Updating Benheim $installedVersion to $latestVersion."
                & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $UpdateScript
                if ($LASTEXITCODE -ne 0) {
                    Write-LaunchLog "Update failed with exit code $LASTEXITCODE."
                    $failureChoice = Show-ChoiceDialog `
                        -Title 'Benheim update failed' `
                        -Message 'The update could not finish. Your current Benheim installation was not changed.' `
                        -PrimaryText 'Launch current version' `
                        -SecondaryText 'Cancel'
                    if ($failureChoice -ne [System.Windows.Forms.DialogResult]::Yes) {
                        exit 0
                    }
                }
            }
        }
    }

    Start-Process 'steam://rungameid/892970'
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
