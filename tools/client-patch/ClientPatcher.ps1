param([string]$InstallFolder = '', [switch]$ValidateUi)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    . (Join-Path $PSScriptRoot 'Patcher.Core.ps1')
    [Windows.Forms.Application]::EnableVisualStyles()
    $script:SelectedDll = $null
    $script:UiScriptPath = $PSCommandPath

    $form = [Windows.Forms.Form]::new()
    $form.Text = 'Beyond Aion - XP Crash Patcher'
    $form.ClientSize = [Drawing.Size]::new(720,505)
    $form.StartPosition = 'CenterScreen'
    $form.FormBorderStyle = 'FixedDialog'
    $form.MaximizeBox = $false
    $form.Font = [Drawing.Font]::new('Segoe UI',10)

    $intro = [Windows.Forms.Label]::new()
    $intro.SetBounds(20,16,680,48)
    $intro.Text = "Select your Aion installation, then inspect it. Applying creates an original backup.`r`nSupports the verified 4.8 64-bit client. Locally tested for XP updates and player visibility."
    $form.Controls.Add($intro)

    $folder = [Windows.Forms.TextBox]::new()
    $folder.SetBounds(20,78,565,28)
    $folder.AccessibleName = 'Aion installation folder'
    $form.Controls.Add($folder)
    $browse = [Windows.Forms.Button]::new()
    $browse.SetBounds(595,76,105,31)
    $browse.Text = 'Browse...'
    $form.Controls.Add($browse)

    $inspectButton = [Windows.Forms.Button]::new()
    $inspectButton.SetBounds(20,120,135,36)
    $inspectButton.Text = 'Inspect only'
    $form.Controls.Add($inspectButton)
    $applyButton = [Windows.Forms.Button]::new()
    $applyButton.SetBounds(165,120,135,36)
    $applyButton.Text = 'Apply patch'
    $applyButton.Enabled = $false
    $form.Controls.Add($applyButton)
    $restoreButton = [Windows.Forms.Button]::new()
    $restoreButton.SetBounds(310,120,135,36)
    $restoreButton.Text = 'Restore original'
    $restoreButton.Enabled = $false
    $form.Controls.Add($restoreButton)
    $adminButton = [Windows.Forms.Button]::new()
    $adminButton.SetBounds(455,120,245,36)
    $adminButton.Text = 'Restart as administrator'
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $isAdmin = ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    $adminButton.Enabled = -not $isAdmin
    $form.Controls.Add($adminButton)

    $statusLabel = [Windows.Forms.Label]::new()
    $statusLabel.SetBounds(20,172,680,48)
    $statusLabel.Text = 'No installation selected. Nothing has been changed.'
    $form.Controls.Add($statusLabel)
    $details = [Windows.Forms.TextBox]::new()
    $details.SetBounds(20,228,680,216)
    $details.Multiline = $true
    $details.ReadOnly = $true
    $details.ScrollBars = 'Both'
    $details.WordWrap = $false
    $details.Font = [Drawing.Font]::new('Consolas',9)
    $details.Text = "Close the game and its updater before applying or restoring.`r`nInspection is read only. Unknown client versions are refused."
    $form.Controls.Add($details)
    $closeButton = [Windows.Forms.Button]::new()
    $closeButton.SetBounds(595,460,105,31)
    $closeButton.Text = 'Close'
    $closeButton.DialogResult = 'Cancel'
    $form.Controls.Add($closeButton)
    $form.CancelButton = $closeButton

    function Show-PatchError([string]$Message) {
        $applyButton.Enabled = $false
        $restoreButton.Enabled = $false
        $statusLabel.ForeColor = [Drawing.Color]::DarkRed
        $statusLabel.Text = 'Operation stopped. See details below.'
        $details.Text = $Message
    }

    function Update-InstallationStatus {
        $script:SelectedDll = Resolve-ClientDll $folder.Text
        $state = Get-ClientPatchStatus $script:SelectedDll
        $applyButton.Enabled = $state.State -eq 'Original'
        $restoreButton.Enabled = $state.Backup -eq 'Verified' -and $state.State -ne 'Original'
        $statusLabel.ForeColor = [Drawing.Color]::DarkGreen
        $statusLabel.Text = "Status: $($state.State). Backup: $($state.Backup)."
        $offset = if ($null -ne $state.Offset) { '0x{0:X}' -f $state.Offset } else { '(missing DLL)' }
        $details.Text = @(
            "DLL: $script:SelectedDll", "State: $($state.State)", "SHA-256: $($state.Hash)",
            "Backup: $script:SelectedDll$script:BackupSuffix ($($state.Backup))", '',
            "File offset: $offset (RVA 0x2CB6EA)",
            'Before: 83 0D FB C6 05 01 01', 'After:  90 90 90 90 90 90 90',
            'Only these seven bytes change. File length stays the same.',
            'Original SHA-256:', $script:OriginalHash, 'Patched SHA-256:', $script:PatchedHash,
            '', 'Local XP and visibility testing passed. Keep the original backup.'
        ) -join "`r`n"
    }

    function Invoke-UiPatch([string]$Action) {
        try {
            # Re-inspect just before the action; never trust stale button state.
            Update-InstallationStatus
            $question = if ($Action -eq 'Apply') {
                "Close the game and its updater first.`r`n`r`nApply the seven-byte XP crash patch to:`r`n$script:SelectedDll`r`n`r`nA verified original backup will be kept beside it."
            } else {
                "Close the game and its updater first.`r`n`r`nRestore the original DLL exactly from its verified backup?`r`n$script:SelectedDll"
            }
            $answer = [Windows.Forms.MessageBox]::Show($form,$question,'Confirm '+$Action,'YesNo','Question','Button2')
            if ($answer -ne 'Yes') { return }
            $form.UseWaitCursor = $true
            $applyButton.Enabled = $false
            $restoreButton.Enabled = $false
            $message = Invoke-ClientPatch $Action $script:SelectedDll
            Update-InstallationStatus
            $statusLabel.Text = $message
        } catch {
            Show-PatchError ($_.Exception.Message + "`r`n`r`nFor access-denied errors, use Restart as administrator, then inspect again. No action is applied automatically after restart.")
        } finally { $form.UseWaitCursor = $false }
    }

    $folder.Add_TextChanged({
        $script:SelectedDll = $null
        $applyButton.Enabled = $false
        $restoreButton.Enabled = $false
        $statusLabel.ForeColor = [Drawing.Color]::Black
        $statusLabel.Text = 'Click Inspect only to check this installation.'
        $details.Text = ''
    })
    $browse.Add_Click({
        $picker = [Windows.Forms.FolderBrowserDialog]::new()
        try {
            $picker.Description = 'Select your Aion installation folder (the folder containing bin64).'
            $picker.ShowNewFolderButton = $false
            if ([IO.Directory]::Exists($folder.Text)) { $picker.SelectedPath=$folder.Text }
            if ($picker.ShowDialog($form) -eq 'OK') {
                $folder.Text=$picker.SelectedPath
                try { Update-InstallationStatus } catch { Show-PatchError $_.Exception.Message }
            }
        } finally { $picker.Dispose() }
    })
    $inspectButton.Add_Click({ try { Update-InstallationStatus } catch { Show-PatchError $_.Exception.Message } })
    $applyButton.Add_Click({ Invoke-UiPatch 'Apply' })
    $restoreButton.Add_Click({ Invoke-UiPatch 'Restore' })
    $adminButton.Add_Click({
        try {
            $powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
            $arguments = '-NoProfile -STA -ExecutionPolicy Bypass -File "'+$script:UiScriptPath+'"'
            if ($folder.Text) {
                $selectedFolder = Assert-PlainPath $folder.Text
                if (-not [IO.Directory]::Exists($selectedFolder)) { throw 'Select an existing installation folder first.' }
                $arguments += ' -InstallFolder "'+$selectedFolder.TrimEnd('\')+'"'
            }
            Start-Process -FilePath $powershell -ArgumentList $arguments -Verb RunAs -WindowStyle Hidden
            $form.Close()
        } catch { Show-PatchError 'Administrator restart was cancelled or failed. Nothing has been applied.' }
    })

    if ($InstallFolder) {
        $folder.Text = $InstallFolder
        try { Update-InstallationStatus } catch { Show-PatchError $_.Exception.Message }
    }
    if ($ValidateUi) {
        # Automated smoke test: construct real WinForms and inspect, never display or mutate.
        [pscustomobject]@{
            Title=$form.Text; Status=$statusLabel.Text; Details=$details.Text
            ApplyEnabled=$applyButton.Enabled; RestoreEnabled=$restoreButton.Enabled
            Controls=$form.Controls.Count
        } | ConvertTo-Json
        $form.Dispose()
        exit 0
    }
    [void]$form.ShowDialog()
    $form.Dispose()
} catch {
    $message = "Patcher could not start: $($_.Exception.Message)"
    if ($ValidateUi) { [Console]::Error.WriteLine($message); exit 1 }
    try { [void][Windows.Forms.MessageBox]::Show($message,'Beyond Aion patcher','OK','Error') }
    catch { Write-Error $message }
    exit 1
}
