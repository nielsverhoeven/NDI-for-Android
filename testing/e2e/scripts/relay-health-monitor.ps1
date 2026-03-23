param(
    [int]$IntervalSeconds = 5,
    [int]$MaxRetries = 3,
    [string]$StatePath = "testing/e2e/artifacts/runtime/relay-state.json",
    [string]$OutputPath = "testing/e2e/artifacts/runtime/relay-monitor.json"
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/helpers/result-handler.ps1"

$retryCount = 0
$events = @()

while ($true) {
    try {
        $healthJson = & "$PSScriptRoot/start-relay-server.ps1" -Action health -StatePath $StatePath -OutputPath $OutputPath
        $health = $healthJson | ConvertFrom-Json

        $events += [PSCustomObject]@{
            at = (Get-Date).ToUniversalTime().ToString("o")
            status = $health.status
        }

        if ($health.status -eq "UNHEALTHY") {
            $retryCount++
            & "$PSScriptRoot/start-relay-server.ps1" -Action stop -StatePath $StatePath | Out-Null
            & "$PSScriptRoot/start-relay-server.ps1" -Action start -StatePath $StatePath | Out-Null
            if ($retryCount -gt $MaxRetries) {
                $result = New-E2eResult -Operation "Monitor-RelayHealth" -Status "FAILURE" -Data @{ retries = $retryCount; events = $events } -Errors @(
                    New-E2eError -Code "RELAY_MONITOR_EXHAUSTED" -Message "Relay became unhealthy too many times"
                )
                Exit-E2eWithResult -Result $result -OutputPath $OutputPath
            }
        }
    }
    catch {
        $events += [PSCustomObject]@{ at = (Get-Date).ToUniversalTime().ToString("o"); status = "MONITOR_ERROR"; message = $_.Exception.Message }
    }

    Start-Sleep -Seconds $IntervalSeconds
}
