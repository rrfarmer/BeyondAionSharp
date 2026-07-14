[CmdletBinding()]
param(
    [string]$Solution = "AionServer.slnx",
    [string]$BaselinePath = "scripts/ci/warning-baseline.json",
    [switch]$NoRestore,
    [switch]$UpdateBaseline
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))

function Resolve-RepositoryPath([string]$Path)
{
    if ([System.IO.Path]::IsPathRooted($Path))
    {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

$solutionPath = Resolve-RepositoryPath $Solution
$resolvedBaselinePath = Resolve-RepositoryPath $BaselinePath

if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf))
{
    throw "Solution was not found: $solutionPath"
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue))
{
    throw "The dotnet CLI was not found on PATH."
}
$activeSdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0)
{
    throw "The active .NET SDK version could not be determined."
}

$buildArguments = @(
    "build",
    $solutionPath,
    "-t:Rebuild",
    "--nologo",
    "-v:minimal",
    "-consoleLoggerParameters:NoSummary;ForceNoAlign"
)
if ($NoRestore)
{
    $buildArguments += "--no-restore"
}

$previousLanguage = $env:DOTNET_CLI_UI_LANGUAGE
$env:DOTNET_CLI_UI_LANGUAGE = "en"
$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
Push-Location $repoRoot
try
{
    # Keep the thousands of legacy warnings out of routine CI output. They are printed when the
    # build fails; successful builds get a compact per-code summary below.
    $buildOutput = @(& dotnet @buildArguments 2>&1 | ForEach-Object { $_.ToString() })
    $buildExitCode = $LASTEXITCODE
}
finally
{
    Pop-Location
    $ErrorActionPreference = $previousErrorActionPreference
    $env:DOTNET_CLI_UI_LANGUAGE = $previousLanguage
}

if ($buildExitCode -ne 0)
{
    [Console]::Error.WriteLine("The warning-baseline rebuild failed (exit code $buildExitCode).")
    $diagnostics = @($buildOutput | Where-Object { $_ -match "(?i)\berror\s+[A-Z]{2,}\d{4}\s*:" } | Sort-Object -Unique)
    if ($diagnostics.Count -eq 0)
    {
        $diagnostics = @($buildOutput | Select-Object -Last 100)
    }
    foreach ($diagnostic in $diagnostics)
    {
        [Console]::Error.WriteLine($diagnostic)
    }
    exit $buildExitCode
}

# A solution Rebuild can report the same project-reference warning more than once. Count unique
# diagnostic records so the baseline measures source warning sites rather than MSBuild graph shape.
$warningDiagnosticPattern = "(?i)(?:^|:\s*)warning(?:\s+[A-Z]{2,}\d{4})?\s*:"
$warningLines = @(
    $buildOutput |
        Where-Object { $_ -match $warningDiagnosticPattern } |
        ForEach-Object { $_.Trim() } |
        Sort-Object -Unique
)

$currentCounts = @{}
foreach ($line in $warningLines)
{
    if ($line -match "(?i)\bwarning\s+(?<code>[A-Z]{2,}\d{4})\s*:")
    {
        $code = $Matches.code.ToUpperInvariant()
    }
    else
    {
        # Do not silently lose toolchain warnings that omit a diagnostic id.
        $code = "UNCLASSIFIED"
    }

    if ($currentCounts.ContainsKey($code))
    {
        $currentCounts[$code]++
    }
    else
    {
        $currentCounts[$code] = 1
    }
}

$currentTotal = $warningLines.Count
Write-Host "Warning inventory: $currentTotal unique warning sites across $($currentCounts.Count) codes."
foreach ($code in @($currentCounts.Keys | Sort-Object))
{
    Write-Host ("  {0,-8} {1,6}" -f $code, $currentCounts[$code])
}

if ($UpdateBaseline)
{
    $orderedCounts = [ordered]@{}
    foreach ($code in @($currentCounts.Keys | Sort-Object))
    {
        $orderedCounts[$code] = $currentCounts[$code]
    }

    $baseline = [ordered]@{
        schemaVersion = 1
        solution = $Solution
        build = "dotnet build -t:Rebuild -v:minimal"
        sdkVersion = $activeSdkVersion
        total = $currentTotal
        codes = $orderedCounts
    }
    $baselineDirectory = Split-Path -Parent $resolvedBaselinePath
    [System.IO.Directory]::CreateDirectory($baselineDirectory) | Out-Null
    $json = $baseline | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText(
        $resolvedBaselinePath,
        $json + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
    Write-Host "Updated warning baseline: $resolvedBaselinePath"
    exit 0
}

if (-not (Test-Path -LiteralPath $resolvedBaselinePath -PathType Leaf))
{
    throw "Warning baseline was not found: $resolvedBaselinePath"
}

$baseline = Get-Content -LiteralPath $resolvedBaselinePath -Raw | ConvertFrom-Json
if ($baseline.schemaVersion -ne 1)
{
    throw "Unsupported warning baseline schema version: $($baseline.schemaVersion)"
}
if ([string]::IsNullOrWhiteSpace($baseline.sdkVersion) -or $activeSdkVersion -ne [string]$baseline.sdkVersion)
{
    throw "Warning baseline requires .NET SDK $($baseline.sdkVersion); active SDK is $activeSdkVersion."
}

$allowedCounts = @{}
foreach ($property in $baseline.codes.PSObject.Properties)
{
    $allowedCounts[$property.Name.ToUpperInvariant()] = [int]$property.Value
}

$violations = [System.Collections.Generic.List[string]]::new()
foreach ($code in @($currentCounts.Keys | Sort-Object))
{
    $actual = [int]$currentCounts[$code]
    if (-not $allowedCounts.ContainsKey($code))
    {
        $violations.Add("New warning code ${code}: $actual site(s).")
    }
    elseif ($actual -gt $allowedCounts[$code])
    {
        $violations.Add("Warning count increased for ${code}: $($allowedCounts[$code]) -> $actual.")
    }
}

$allowedTotal = [int]$baseline.total
if ($currentTotal -gt $allowedTotal)
{
    $violations.Add("Total warning count increased: $allowedTotal -> $currentTotal.")
}

if ($violations.Count -gt 0)
{
    foreach ($violation in $violations)
    {
        [Console]::Error.WriteLine($violation)
    }
    [Console]::Error.WriteLine("Fix the warnings. Use -UpdateBaseline only to ratchet reviewed reductions.")
    exit 1
}

$reductions = [System.Collections.Generic.List[string]]::new()
foreach ($code in @($allowedCounts.Keys | Sort-Object))
{
    $actual = if ($currentCounts.ContainsKey($code)) { [int]$currentCounts[$code] } else { 0 }
    if ($actual -lt $allowedCounts[$code])
    {
        $reductions.Add("${code}: $($allowedCounts[$code]) -> $actual")
    }
}

if ($reductions.Count -gt 0 -or $currentTotal -lt $allowedTotal)
{
    Write-Host "Warning debt decreased; ratchet the checked-in baseline after reviewing the fixes:"
    foreach ($reduction in $reductions)
    {
        Write-Host "  $reduction"
    }
    Write-Host "  total: $allowedTotal -> $currentTotal"
}

Write-Host "Warning baseline passed."
