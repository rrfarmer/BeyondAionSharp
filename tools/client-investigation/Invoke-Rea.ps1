# Use the configured Codex REA runtime and provider paths from an ordinary shell.
# No machine/user environment settings are changed. Target actions are explicit CLI arguments.
param([Parameter(ValueFromRemainingArguments=$true)][string[]]$ReaArguments)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$registrationJson = & codex mcp get rea --json
if ($LASTEXITCODE -ne 0) { throw 'REA is not registered with this Codex installation.' }
$registration = ($registrationJson -join "`n") | ConvertFrom-Json
$transport = $registration.transport
if ($transport.type -ne 'stdio' -or $transport.args.Count -ne 2 -or $transport.args[1] -ne 'mcp') {
    throw 'The REA registration has changed. Inspect it before updating this CLI helper.'
}
$previous = @{}
$exitCode = 1
try {
    foreach ($setting in $transport.env.PSObject.Properties) {
        $previous[$setting.Name] = [Environment]::GetEnvironmentVariable($setting.Name,'Process')
        [Environment]::SetEnvironmentVariable($setting.Name,[string]$setting.Value,'Process')
    }
    & $transport.command $transport.args[0] @ReaArguments
    $exitCode = $LASTEXITCODE
} finally {
    foreach ($name in $previous.Keys) {
        [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')
    }
}
exit $exitCode
