Set-StrictMode -Version Latest

$ValidEmulatorStates = @("PROVISIONING", "RUNNING", "IDLE", "FAILED", "STOPPED")

$ValidRelayStates = @("STARTING", "RUNNING", "STOPPING", "STOPPED", "FAILED")

function Test-EmulatorInstance {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Instance
    )

    $errors = @()
    if (-not $Instance.id) { $errors += "id is required" }
    if (-not ($Instance.apiLevel -is [int]) -or $Instance.apiLevel -lt 32 -or $Instance.apiLevel -gt 36) {
        $errors += "apiLevel must be an integer in range 32-36"
    }
    if (-not ($Instance.adbPort -is [int]) -or $Instance.adbPort -lt 5554 -or $Instance.adbPort -gt 5568) {
        $errors += "adbPort must be in range 5554-5568"
    }
    if ($Instance.state -notin $ValidEmulatorStates) {
        $errors += "state must be one of: $($ValidEmulatorStates -join ', ')"
    }
    if ($Instance.PSObject.Properties.Name -contains "relayPort") {
        if (-not ($Instance.relayPort -is [int]) -or $Instance.relayPort -lt 15000 -or $Instance.relayPort -gt 15010) {
            $errors += "relayPort must be in range 15000-15010"
        }
    }

    return [PSCustomObject]@{
        isValid = ($errors.Count -eq 0)
        errors = $errors
    }
}

function Test-RelayServer {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Relay
    )

    $errors = @()
    if (-not $Relay.id) { $errors += "id is required" }
    if ($Relay.state -notin $ValidRelayStates) {
        $errors += "state must be one of: $($ValidRelayStates -join ', ')"
    }
    if (-not ($Relay.listeningPort -is [int]) -or $Relay.listeningPort -lt 15000 -or $Relay.listeningPort -gt 15010) {
        $errors += "listeningPort must be in range 15000-15010"
    }
    if (-not $Relay.routes -or $Relay.routes.Count -lt 1) {
        $errors += "at least one route is required"
    }

    return [PSCustomObject]@{
        isValid = ($errors.Count -eq 0)
        errors = $errors
    }
}

function Assert-EmulatorInstance {
    param([Parameter(Mandatory = $true)][object]$Instance)

    $validation = Test-EmulatorInstance -Instance $Instance
    if (-not $validation.isValid) {
        throw "Invalid EmulatorInstance: $($validation.errors -join '; ')"
    }
}

function Assert-RelayServer {
    param([Parameter(Mandatory = $true)][object]$Relay)

    $validation = Test-RelayServer -Relay $Relay
    if (-not $validation.isValid) {
        throw "Invalid RelayServer: $($validation.errors -join '; ')"
    }
}
