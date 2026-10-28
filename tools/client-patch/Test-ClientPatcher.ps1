param([string]$ReferenceDll = 'C:\Program Files (x86)\Beyond Aion\bin64\game.dll')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Patcher.Core.ps1')
Add-Type -TypeDefinition @'
public static class ClientPatchDiff {
    public static int[] Differences(byte[] a, byte[] b) {
        if(a.Length != b.Length) throw new System.Exception("File lengths differ");
        var result = new System.Collections.Generic.List<int>();
        for(int i=0;i<a.Length;i++) if(a[i]!=b[i]) result.Add(i);
        return result.ToArray();
    }
}
'@
$script:Passed=0
function Assert-Test([bool]$Condition,[string]$Message) {
    if (-not $Condition) { throw "TEST FAILED: $Message" }
    $script:Passed++
    Write-Output "PASS: $Message"
}
function Assert-Refused([scriptblock]$Operation,[string]$Message) {
    $failed=$false
    try { $null=& $Operation } catch { $failed=$true }
    Assert-Test $failed $Message
}

$reference = [IO.File]::ReadAllBytes($ReferenceDll)
$referenceHash = Get-BytesHash $reference
$referenceTime = [IO.File]::GetLastWriteTimeUtc($ReferenceDll)
Assert-Test ($referenceHash -eq $script:OriginalHash) 'Reference is the exact investigated original DLL'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('aion-patcher-test-'+[Guid]::NewGuid().ToString('N'))
$null=[IO.Directory]::CreateDirectory((Join-Path $testRoot 'Aion install & unicode test\bin64'))
$dll=Join-Path $testRoot 'Aion install & unicode test\bin64\game.dll'
$backup=$dll+$script:BackupSuffix
try {
    [IO.File]::WriteAllBytes($dll,$reference)
    Assert-Test ((Resolve-ClientDll (Split-Path (Split-Path $dll))) -eq $dll) 'Installation root folder resolves bin64 DLL'
    Assert-Test ((Resolve-ClientDll (Split-Path $dll)) -eq $dll) 'bin64 folder resolves directly'
    $status=Get-ClientPatchStatus $dll
    Assert-Test ($status.State -eq 'Original' -and $status.Offset -eq 0x2CB6EA) 'Inspection maps the PE RVA correctly'
    Assert-Test (@(Get-ChildItem -LiteralPath (Split-Path $dll)).Count -eq 1) 'Inspection creates no files'
    $null=Invoke-ClientPatch Apply $dll
    $patched=[IO.File]::ReadAllBytes($dll)
    $diff=[ClientPatchDiff]::Differences($reference,$patched)
    Assert-Test (($diff -join ',') -eq ((0x2CB6EA..0x2CB6F0) -join ',')) 'Exactly seven intended bytes change; all others and file length match'
    Assert-Test ((Get-BytesHash $patched) -eq $script:PatchedHash) 'Patched DLL matches independently reviewed SHA-256'
    Assert-Test ((Get-BytesHash ([IO.File]::ReadAllBytes($backup))) -eq $referenceHash) 'Backup is the exact original'
    $backupTime=[IO.File]::GetLastWriteTimeUtc($backup)
    $null=Invoke-ClientPatch Apply $dll
    Assert-Test ([IO.File]::GetLastWriteTimeUtc($backup) -eq $backupTime) 'Repeated apply never overwrites the backup'
    $null=Invoke-ClientPatch Restore $dll
    Assert-Test ([ClientPatchDiff]::Differences($reference,[IO.File]::ReadAllBytes($dll)).Length -eq 0) 'Restore returns the entire DLL byte-for-byte exactly'
    $null=Invoke-ClientPatch Restore $dll
    Assert-Test ((Get-ClientPatchStatus $dll).State -eq 'Original') 'Repeated restore is a no-op'

    [IO.File]::WriteAllBytes($dll,[byte[]](1,2,3))
    Assert-Refused { Invoke-ClientPatch Apply $dll } 'Unknown DLL rejected by apply'
    Assert-Refused { Invoke-ClientPatch Restore $dll } 'Unknown DLL rejected by restore even with a valid backup'
    Assert-Test (([IO.File]::ReadAllBytes($dll) -join ',') -eq '1,2,3') 'Rejected unknown DLL remains unchanged'
    [IO.File]::WriteAllBytes($dll,$reference)
    [IO.File]::WriteAllBytes($backup,[byte[]](4,5,6))
    Assert-Refused { Invoke-ClientPatch Apply $dll } 'Corrupt original backup blocks apply'
    Assert-Test ((Get-BytesHash ([IO.File]::ReadAllBytes($dll))) -eq $referenceHash) 'Backup refusal preserves original DLL'
    [IO.File]::Delete($backup)
    [IO.File]::WriteAllBytes($dll,$patched)
    Assert-Refused { Invoke-ClientPatch Restore $dll } 'Missing original backup blocks restore'
    [IO.File]::WriteAllBytes($backup,$reference)

    # Missing-target recovery, including the old error-1176 failure state.
    [IO.File]::Delete($dll)
    Assert-Test ((Get-ClientPatchStatus $dll).State -eq 'Missing DLL - recovery available') 'Missing DLL can be inspected from its verified backup'
    $null=Invoke-ClientPatch Restore $dll
    Assert-Test ([ClientPatchDiff]::Differences($reference,[IO.File]::ReadAllBytes($dll)).Length -eq 0) 'Missing DLL restored exactly from verified backup'

    # Fault injection changes disposable copies only. Never calls native replacement here.
    $realReplace=(Get-Item Function:Replace-ClientDllFile).ScriptBlock
    try {
        function Replace-ClientDllFile([string]$Dll,[string]$Staged,[string]$Rollback) {
            [IO.File]::Delete($Dll)
            throw 'Simulated error 1176: target missing and staged replacement remains'
        }
        Assert-Refused { Invoke-ClientPatch Apply $dll } 'Partial replacement failure is reported'
        Assert-Test (-not [IO.File]::Exists($dll)) 'Injected partial failure models the missing target'
        Assert-Test (@(Get-ChildItem -LiteralPath (Split-Path $dll) -Filter '*.tmp').Count -gt 0) 'Staged file is retained after partial failure'
        Set-Item Function:Replace-ClientDllFile $realReplace
        $null=Invoke-ClientPatch Restore $dll
        Assert-Test ([ClientPatchDiff]::Differences($reference,[IO.File]::ReadAllBytes($dll)).Length -eq 0) 'Restore succeeds exactly after the reproduced review failure'

        function Replace-ClientDllFile([string]$Dll,[string]$Staged,[string]$Rollback) {
            [IO.File]::Move($Dll,$Rollback)
            throw 'Simulated error 1177: target moved to transaction rollback'
        }
        Assert-Refused { Invoke-ClientPatch Apply $dll } 'Rollback-stage replacement failure is reported'
        Assert-Test ([ClientPatchDiff]::Differences($reference,[IO.File]::ReadAllBytes($dll)).Length -eq 0) 'Transaction rollback automatically recovers the previous DLL'
        Set-Item Function:Replace-ClientDllFile $realReplace
        $null=Invoke-ClientPatch Apply $dll
        function Replace-ClientDllFile([string]$Dll,[string]$Staged,[string]$Rollback) {
            [IO.File]::Move($Dll,$Rollback)
            throw 'Simulated restore replacement failure'
        }
        Assert-Refused { Invoke-ClientPatch Restore $dll } 'Partial failure during restore is reported'
        Assert-Test ((Get-BytesHash ([IO.File]::ReadAllBytes($dll))) -eq $script:PatchedHash) 'Failed restore recovers the prior patched DLL'
        Set-Item Function:Replace-ClientDllFile $realReplace
        $null=Invoke-ClientPatch Restore $dll
        Assert-Test ([ClientPatchDiff]::Differences($reference,[IO.File]::ReadAllBytes($dll)).Length -eq 0) 'Retry after failed restore produces the exact original DLL'
    } finally { Set-Item Function:Replace-ClientDllFile $realReplace }

    $realGuard=(Get-Item Function:Assert-ClientClosed).ScriptBlock
    try {
        function Assert-ClientClosed([string]$Dll) { throw 'Simulated running client' }
        Assert-Refused { Invoke-ClientPatch Apply $dll } 'Running-client guard prevents apply'
        Assert-Test ((Get-BytesHash ([IO.File]::ReadAllBytes($dll))) -eq $referenceHash) 'Running-client refusal leaves DLL unchanged'
    } finally { Set-Item Function:Assert-ClientClosed $realGuard }

    # Actual Windows PowerShell 5.1 WinForms construction, without showing a window.
    $windowsPs=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $uiOutput=& $windowsPs -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'ClientPatcher.ps1') -InstallFolder (Split-Path (Split-Path $dll)) -ValidateUi
    Assert-Test ($LASTEXITCODE -eq 0) 'Windows PowerShell 5.1 interface starts successfully'
    $ui=$uiOutput | ConvertFrom-Json
    Assert-Test ($ui.ApplyEnabled -and -not $ui.RestoreEnabled -and $ui.Status -like '*Original*') 'Interface enables the correct actions after read-only inspection'
    Assert-Test ((Get-BytesHash ([IO.File]::ReadAllBytes($dll))) -eq $referenceHash) 'Interface smoke test makes no DLL changes'
} finally {
    # Resolve and validate this exact disposable directory before recursive cleanup.
    $resolved=[IO.Path]::GetFullPath($testRoot)
    $temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if ($resolved.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolved).StartsWith('aion-patcher-test-')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
Assert-Test ((Get-BytesHash ([IO.File]::ReadAllBytes($ReferenceDll))) -eq $referenceHash) 'Installed DLL hash is unchanged'
Assert-Test ([IO.File]::GetLastWriteTimeUtc($ReferenceDll) -eq $referenceTime) 'Installed DLL modification time is unchanged'
Write-Output "$script:Passed checks passed. Installed client was never patched."
