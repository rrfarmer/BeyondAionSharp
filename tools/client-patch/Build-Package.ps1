param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\..\run\client-patch-package'))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
$null = [IO.Directory]::CreateDirectory($output)
$stage = Join-Path $output ('stage-'+[Guid]::NewGuid().ToString('N'))
$package = Join-Path $stage 'BeyondAion-XP-Crash-Patcher'
$null = [IO.Directory]::CreateDirectory($package)
$zip = Join-Path $output 'BeyondAion-XP-Crash-Patcher.zip'
try {
    $files = @('Start Patcher.bat','ClientPatcher.ps1','Patcher.Core.ps1','README.txt')
    $checksums = @()
    foreach ($file in $files) {
        $source = Join-Path $PSScriptRoot $file
        Copy-Item -LiteralPath $source -Destination (Join-Path $package $file)
        $checksums += ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$file)
    }
    [IO.File]::WriteAllLines((Join-Path $package 'SHA256SUMS.txt'),$checksums,[Text.Encoding]::ASCII)
    # An explicit file list excludes tests, cache files, DLLs and saved backups.
    Compress-Archive -LiteralPath $package -DestinationPath $zip -Force
    Get-Item -LiteralPath $zip | Select-Object FullName,Length
    Get-FileHash -LiteralPath $zip -Algorithm SHA256
} finally {
    $resolved = [IO.Path]::GetFullPath($stage)
    if ($resolved.StartsWith($output.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolved).StartsWith('stage-')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
