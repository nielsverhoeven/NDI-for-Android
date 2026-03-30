param(
    [ValidateSet("start", "get", "stop", "health")]
    [string]$Action = "start",
    [string]$RelayId = "relay-default",
    [int]$ListeningPort = 15000,
    [int]$HealthCheckIntervalMs = 5000,
    [int]$MaxLatencyMs = 500,
    [string]$StatePath = "testing/e2e/artifacts/runtime/relay-state.json",
    [string]$MetricsPath = "testing/e2e/artifacts/runtime/relay-metrics.json",
    [string]$OutputPath = "testing/e2e/artifacts/runtime/relay-result.json"
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/helpers/result-handler.ps1"
. "$PSScriptRoot/helpers/relay-health-check.ps1"

function Get-PwshExecutable {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwsh) { return $pwsh.Source }
    $powershell = Get-Command powershell -ErrorAction SilentlyContinue
    if ($powershell) { return $powershell.Source }
    throw "Neither pwsh nor powershell is available"
}

function Save-RelayState {
    param([Parameter(Mandatory = $true)][object]$State)

    $dir = Split-Path -Parent $StatePath
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $State | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $StatePath -Encoding UTF8
}

function Load-RelayState {
    if (-not (Test-Path -LiteralPath $StatePath)) {
        return $null
    }

    return Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
}

function Start-RelayServer {
    $psExe = Get-PwshExecutable
    $relayScript = Join-Path $PSScriptRoot "helpers/relay-tcp-forwarder.ps1"

    $process = Start-Process -FilePath $psExe -ArgumentList @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $relayScript,
        "-RunServer",
        "-ListenPort",
        "$ListeningPort",
        "-MetricsPath",
        $MetricsPath
    ) -PassThru -WindowStyle Hidden

    Start-Sleep -Milliseconds 400
    $health = Invoke-RelayEchoCheck -TargetHost "127.0.0.1" -Port $ListeningPort -EchoCount 3 -MaxLatencyMs $MaxLatencyMs
    if ($health.status -eq "UNHEALTHY") {
        Write-Warning "Initial relay health check failed. Continuing with degraded relay state for non-relay-dependent suites."
    }

    $state = [PSCustomObject]@{
        id = $RelayId
        state = if ($health.status -eq "UNHEALTHY") { "DEGRADED" } else { "RUNNING" }
        listeningPort = $ListeningPort
        pidOrProcessHandle = $process.Id
        startTime = (Get-Date).ToUniversalTime().ToString("o")
        healthCheckIntervalMs = $HealthCheckIntervalMs
        maxLatencyMs = $MaxLatencyMs
        metricsPath = $MetricsPath
        restartCount = 0
        initialHealth = $health
    }
    Save-RelayState -State $state
    return $state
}

function Get-RelayServer {
    $state = Load-RelayState
    if (-not $state) {
        throw "Relay state file not found"
    }

    $health = Invoke-RelayEchoCheck -TargetHost "127.0.0.1" -Port ([int]$state.listeningPort) -EchoCount 1 -MaxLatencyMs ([int]$state.maxLatencyMs)
    $state | Add-Member -NotePropertyName "health" -NotePropertyValue $health -Force
    return $state
}

function Stop-RelayServer {
    $state = Load-RelayState
    if (-not $state) {
        return [PSCustomObject]@{ id = $RelayId; state = "STOPPED" }
    }

    if ($state.pidOrProcessHandle) {
        Stop-Process -Id ([int]$state.pidOrProcessHandle) -Force -ErrorAction SilentlyContinue
    }

    $state.state = "STOPPED"
    Save-RelayState -State $state
    return $state
}

try {
    switch ($Action) {
        "start" {
            $relay = Start-RelayServer
            $result = New-E2eResult -Operation "Start-RelayServer" -Status "SUCCESS" -Data $relay
        }
        "get" {
            $relay = Get-RelayServer
            $result = New-E2eResult -Operation "Get-RelayServer" -Status "SUCCESS" -Data $relay
        }
        "health" {
            $state = Load-RelayState
            if (-not $state) {
                throw "Relay state file not found"
            }

            $health = Invoke-RelayEchoCheck -TargetHost "127.0.0.1" -Port ([int]$state.listeningPort) -EchoCount 3 -MaxLatencyMs ([int]$state.maxLatencyMs)
            $result = New-E2eResult -Operation "Check-RelayHealth" -Status $health.status -Data $health
        }
        "stop" {
            $relay = Stop-RelayServer
            $result = New-E2eResult -Operation "Stop-RelayServer" -Status "SUCCESS" -Data $relay
        }
    }

    Exit-E2eWithResult -Result $result -OutputPath $OutputPath
}
catch {
    $result = New-E2eResult -Operation "start-relay-server" -Status "FAILURE" -Errors @(
        New-E2eError -Code "RELAY_OPERATION_FAILED" -Message $_.Exception.Message
    )
    Exit-E2eWithResult -Result $result -OutputPath $OutputPath
}
