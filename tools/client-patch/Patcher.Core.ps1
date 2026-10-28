# Native Windows / Windows PowerShell 5.1. No client is launched by this script.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:OriginalHash = 'd8645dadfb91e1c2af08a6c9ae313e3d5e07e59ed9b1d004dddcb7b516047296'
$script:PatchedHash = '0b103f3bdfa9a83b2a733f1b734ccf9ec35f444360f0757dd9375bed4ecad0ff'
$script:PatchRva = 0x2CB6EA
$script:OriginalBytes = [byte[]](0x83,0x0D,0xFB,0xC6,0x05,0x01,0x01)
$script:PatchedBytes = [byte[]](0x90,0x90,0x90,0x90,0x90,0x90,0x90)
$script:BackupSuffix = '.xp-address-check.original.bak'

function Get-BytesHash([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Assert-PlainPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $walk = $full
    while ($walk) {
        if (Test-Path -LiteralPath $walk) {
            $item = Get-Item -LiteralPath $walk -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Choose the actual installation folder, without links or junctions.'
            }
        }
        $parent = [IO.Path]::GetDirectoryName($walk)
        if ($parent -eq $walk) { break }
        $walk = $parent
    }
    return $full
}

function Resolve-ClientDll([string]$InstallFolder) {
    if ([string]::IsNullOrWhiteSpace($InstallFolder)) { throw 'Select your Aion installation folder first.' }
    $folder = Assert-PlainPath $InstallFolder
    if (-not [IO.Directory]::Exists($folder)) { throw 'The selected installation folder does not exist.' }
    foreach ($candidate in @((Join-Path $folder 'bin64\game.dll'), (Join-Path $folder 'game.dll'))) {
        if ([IO.File]::Exists($candidate) -or [IO.File]::Exists($candidate + $script:BackupSuffix)) {
            return (Assert-PlainPath $candidate)
        }
    }
    throw 'No supported client found. Select the Aion folder containing bin64, or select bin64 itself.'
}

function Get-PatchOffset([byte[]]$Bytes) {
    try {
        if ($Bytes.Length -lt 64 -or $Bytes[0] -ne 0x4D -or $Bytes[1] -ne 0x5A) { throw 'DOS header' }
        $pe = [BitConverter]::ToInt32($Bytes,0x3C)
        if ($pe -lt 64 -or $pe -gt $Bytes.Length - 136) { throw 'PE header offset' }
        if ([BitConverter]::ToUInt32($Bytes,$pe) -ne 0x4550) { throw 'PE signature' }
        $count = [BitConverter]::ToUInt16($Bytes,$pe+6)
        $optionalSize = [BitConverter]::ToUInt16($Bytes,$pe+20)
        $opt = $pe+24
        if ([BitConverter]::ToUInt16($Bytes,$pe+4) -ne 0x8664 -or $optionalSize -lt 112 -or
            [BitConverter]::ToUInt16($Bytes,$opt) -ne 0x20B -or
            [BitConverter]::ToUInt64($Bytes,$opt+24) -ne 0x10000000) { throw 'expected AMD64 PE32+ build' }
        $matches = @()
        for ($i=0; $i -lt $count; $i++) {
            $s = $opt+$optionalSize+40*$i
            if ($s -gt $Bytes.Length-40) { throw 'section table outside file' }
            $va = [long][BitConverter]::ToUInt32($Bytes,$s+12)
            $size = [long][BitConverter]::ToUInt32($Bytes,$s+16)
            $raw = [long][BitConverter]::ToUInt32($Bytes,$s+20)
            if ($va -le $script:PatchRva -and $script:PatchRva+7 -le $va+$size) {
                $offset = $raw+$script:PatchRva-$va
                $flags = [BitConverter]::ToUInt32($Bytes,$s+36)
                if (($flags -band 0x20000000) -eq 0 -or $offset+7 -gt $Bytes.Length) { throw 'non-executable patch region' }
                $matches += [int]$offset
            }
        }
        if ($matches.Count -ne 1) { throw 'ambiguous or missing patch region' }
        return $matches[0]
    } catch { throw "Unsupported PE layout: $($_.Exception.Message)" }
}

function Get-DllIdentity([byte[]]$Bytes) {
    $hash = Get-BytesHash $Bytes
    if ($hash -eq $script:OriginalHash) { $state='Original'; $expected=$script:OriginalBytes }
    elseif ($hash -eq $script:PatchedHash) { $state='Patched'; $expected=$script:PatchedBytes }
    else { throw "Unsupported or modified game.dll. No changes made. SHA-256: $hash" }
    $offset = Get-PatchOffset $Bytes
    for ($i=0; $i -lt 7; $i++) {
        if ($Bytes[$offset+$i] -ne $expected[$i]) { throw 'Instruction bytes disagree with the supported build.' }
    }
    return [pscustomobject]@{ State=$state; Hash=$hash; Offset=$offset }
}

function Read-OriginalBackup([string]$Dll) {
    $backup = Assert-PlainPath ($Dll+$script:BackupSuffix)
    if (-not [IO.File]::Exists($backup)) { throw 'The verified original backup is missing. Restore cannot continue.' }
    $bytes = [IO.File]::ReadAllBytes($backup)
    if ((Get-BytesHash $bytes) -ne $script:OriginalHash) { throw 'The original backup is damaged or belongs to another client. No files overwritten.' }
    return ,$bytes
}

function Get-ClientPatchStatus([string]$Dll) {
    $Dll = Assert-PlainPath $Dll
    $backupState = 'Missing'
    if ([IO.File]::Exists($Dll+$script:BackupSuffix)) { $null = Read-OriginalBackup $Dll; $backupState='Verified' }
    if (-not [IO.File]::Exists($Dll)) {
        if ($backupState -ne 'Verified') { throw 'game.dll and its verified backup are both missing.' }
        return [pscustomobject]@{ State='Missing DLL - recovery available'; Hash=''; Offset=$null; Backup=$backupState; Dll=$Dll }
    }
    $identity = Get-DllIdentity ([IO.File]::ReadAllBytes($Dll))
    return [pscustomobject]@{ State=$identity.State; Hash=$identity.Hash; Offset=$identity.Offset; Backup=$backupState; Dll=$Dll }
}

function Assert-ClientClosed([string]$Dll) {
    foreach ($process in @(Get-Process | Where-Object { $_.ProcessName -in @('aion','aion.bin') })) {
        try { $image = $process.Path } catch { throw 'Close all Aion clients before applying or restoring.' }
        if (-not $image) { throw 'Cannot inspect a running Aion client. Close all Aion clients first.' }
        if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($image)) -eq [IO.Path]::GetDirectoryName($Dll)) {
            throw 'Close the Aion client in this installation before applying or restoring.'
        }
    }
}

function Write-NewVerifiedFile([string]$Path, [byte[]]$Bytes, [string]$ExpectedHash) {
    $stream = [IO.File]::Open($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($Bytes,0,$Bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
    if ((Get-BytesHash ([IO.File]::ReadAllBytes($Path))) -ne $ExpectedHash) { throw "File verification failed: $Path" }
}

function Replace-ClientDllFile([string]$Dll, [string]$Staged, [string]$Rollback) {
    # A separate transaction backup handles ReplaceFileW partial failures.
    # File.Replace preserves Windows file permissions; never ignore merge errors.
    [IO.File]::Replace($Staged,$Dll,$Rollback,$false)
}

function Complete-ClientDllWrite([string]$Dll, [byte[]]$Before, [byte[]]$After) {
    $afterHash = Get-BytesHash $After
    $beforeHash = if ($null -eq $Before) { $null } else { Get-BytesHash $Before }
    $tag = [Guid]::NewGuid().ToString('N')
    $staged = "$Dll.xp-$tag.tmp"
    $rollback = "$Dll.xp-$tag.rollback"
    $success = $false
    try {
        Write-NewVerifiedFile $staged $After $afterHash
        Assert-ClientClosed $Dll
        $null = Assert-PlainPath $Dll
        $null = Read-OriginalBackup $Dll
        if ($null -eq $Before) {
            if ([IO.File]::Exists($Dll)) { throw 'A DLL appeared during recovery; it will not be overwritten.' }
            [IO.File]::Move($staged,$Dll) # No overwrite; safe recovery of a missing DLL.
        } else {
            if (-not [IO.File]::Exists($Dll) -or (Get-BytesHash ([IO.File]::ReadAllBytes($Dll))) -ne $beforeHash) {
                throw 'The DLL changed during preparation. It will not be overwritten.'
            }
            Replace-ClientDllFile $Dll $staged $rollback
        }
        if ((Get-BytesHash ([IO.File]::ReadAllBytes($Dll))) -ne $afterHash) { throw 'Post-write DLL verification failed.' }
        $success = $true
    } catch {
        $reason = $_.Exception.Message
        # Never discard the replacement or rollback files on a partial failure.
        # Restore the transaction's previous DLL only if its path is absent.
        if (-not [IO.File]::Exists($Dll) -and [IO.File]::Exists($rollback) -and $beforeHash) {
            try {
                if ((Get-BytesHash ([IO.File]::ReadAllBytes($rollback))) -eq $beforeHash) {
                    [IO.File]::Move($rollback,$Dll)
                }
            } catch { $reason += " Recovery: $($_.Exception.Message)" }
        }
        throw "Operation failed: $reason`r`nThe original backup is retained. Use Inspect, then Restore. Recovery files (if present): $staged ; $rollback"
    } finally {
        if ($success) {
            foreach ($temporary in @($staged,$rollback)) {
                if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
            }
        }
    }
}

function Invoke-ClientPatch([ValidateSet('Apply','Restore')][string]$Action, [string]$Dll) {
    $Dll = Assert-PlainPath $Dll
    $lockHash = Get-BytesHash ([Text.Encoding]::UTF8.GetBytes($Dll.ToLowerInvariant()))
    $mutex = [Threading.Mutex]::new($false, ('Local\BeyondAionXpPatch-'+$lockHash))
    $acquired = $false
    try {
        try { $acquired = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $acquired=$true }
        if (-not $acquired) { throw 'Another patch operation is already using this installation.' }
        Assert-ClientClosed $Dll
        $status = Get-ClientPatchStatus $Dll
        if ($Action -eq 'Apply') {
            if ($status.State -eq 'Patched') {
                $null=Read-OriginalBackup $Dll
                return 'Already patched. No changes made.'
            }
            if ($status.State -ne 'Original') { throw 'Restore the missing DLL before applying the patch.' }
            $before = [IO.File]::ReadAllBytes($Dll)
            $identity = Get-DllIdentity $before
            $after = [byte[]]$before.Clone()
            [Array]::Copy($script:PatchedBytes,0,$after,$identity.Offset,7)
            if ((Get-BytesHash $after) -ne $script:PatchedHash) { throw 'Proposed DLL does not match the reviewed patch.' }
            if (-not [IO.File]::Exists($Dll+$script:BackupSuffix)) {
                Write-NewVerifiedFile ($Dll+$script:BackupSuffix) $before $script:OriginalHash
            }
            $null=Read-OriginalBackup $Dll
            Complete-ClientDllWrite $Dll $before $after
            return 'Patch applied and verified. Start a fresh client to test it. Keep the backup.'
        }
        if ($status.State -eq 'Original') { return 'Already original. No changes made.' }
        $original = Read-OriginalBackup $Dll
        $before = $null
        if ([IO.File]::Exists($Dll)) { $before = [IO.File]::ReadAllBytes($Dll) }
        Complete-ClientDllWrite $Dll $before $original
        return 'Original DLL restored and verified exactly. The backup has been retained.'
    } finally {
        if ($acquired) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}
