param(
    [ValidateSet("provision-dual", "provision-single", "state")]
    [string]$Action = "provision-dual",
    [string]$SourceEmulatorId = "emulator-5554",
    [string]$ReceiverEmulatorId = "emulator-5556",
    [string]$EmulatorId,
    [int]$ApiLevel = 34,
    [int]$BootTimeoutSeconds = 90,
    [string]$NdiSdkApkPath = "ndi/sdk-bridge/build/outputs/apk/release/ndi-sdk-bridge-release.apk",
    [switch]$InstallNdiSdk,
    [switch]$SkipBootIfAlreadyRunning,
    [string]$OutputPath = "testing/e2e/artifacts/runtime/provisioning-result.json"
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/helpers/result-handler.ps1"
. "$PSScriptRoot/helpers/emulator-adb.ps1"
. "$PSScriptRoot/helpers/entity-validator.ps1"

function Get-RelayPortFromSerial {
    param([Parameter(Mandatory = $true)][string]$Serial)

    if ($Serial -notmatch "emulator-(\d+)") {
        return 15000
    }

    $base = [int]$Matches[1]
    return 15000 + ($base % 10)
}

function Get-EmulatorState {
    param([Parameter(Mandatory = $true)][string]$EmulatorSerial)

    $snapshot = Get-EmulatorStateSnapshot -Serial $EmulatorSerial
    $state = if ($snapshot.adbState -eq "device" -and $snapshot.bootCompleted) { "RUNNING" } else { "IDLE" }

    return [PSCustomObject]@{
        id = $EmulatorSerial
        apiLevel = if ($snapshot.apiLevel -gt 0) { $snapshot.apiLevel } else { $ApiLevel }
        state = $state
        bootTimeMs = 0
        adbPort = [int](($EmulatorSerial -replace "emulator-", "") -replace "[^0-9]", "0")
        ndiSdkApkInstalled = $false
        relayPort = Get-RelayPortFromSerial -Serial $EmulatorSerial
        lastBootTime = (Get-Date).ToUniversalTime().ToString("o")
    }
}

function Provision-Emulator {
    param(
        [Parameter(Mandatory = $true)][string]$EmulatorSerial,
        [int]$ProvisionApiLevel = 34,
        [int]$TimeoutSeconds = 90,
        [string]$ApkPath,
        [switch]$InstallSdk,
        [switch]$AllowReuse
    )

    $started = Get-Date
    $alreadyRunning = Test-AdbDeviceConnected -Serial $EmulatorSerial
    if ($alreadyRunning -and $AllowReuse) {
        $instance = Get-EmulatorState -EmulatorSerial $EmulatorSerial
        Assert-EmulatorInstance -Instance $instance
        return $instance
    }

    if (-not (Wait-EmulatorBoot -Serial $EmulatorSerial -TimeoutSeconds $TimeoutSeconds)) {
        throw "BOOT_TIMEOUT: $EmulatorSerial did not complete boot in $TimeoutSeconds seconds"
    }

    if ($InstallSdk) {
        Install-ApkToEmulator -Serial $EmulatorSerial -ApkPath $ApkPath
    }

    $duration = [int]((Get-Date) - $started).TotalMilliseconds
    $instance = [PSCustomObject]@{
        id = $EmulatorSerial
        apiLevel = $ProvisionApiLevel
        state = "RUNNING"
        bootTimeMs = $duration
        adbPort = [int](($EmulatorSerial -replace "emulator-", "") -replace "[^0-9]", "0")
        ndiSdkApkInstalled = [bool]$InstallSdk
        relayPort = Get-RelayPortFromSerial -Serial $EmulatorSerial
        lastBootTime = (Get-Date).ToUniversalTime().ToString("o")
    }

    Assert-EmulatorInstance -Instance $instance
    return $instance
}

function Provision-DualEmulator {
    param(
        [Parameter(Mandatory = $true)][string]$SourceSerial,
        [Parameter(Mandatory = $true)][string]$ReceiverSerial
    )

    $errors = @()
    $source = $null
    $receiver = $null

    try {
        $source = Provision-Emulator -EmulatorSerial $SourceSerial -ProvisionApiLevel $ApiLevel -TimeoutSeconds $BootTimeoutSeconds -ApkPath $NdiSdkApkPath -InstallSdk:$InstallNdiSdk -AllowReuse:$SkipBootIfAlreadyRunning
    }
    catch {
        $errors += New-E2eError -Code "SOURCE_PROVISION_FAILED" -Message $_.Exception.Message
    }

    try {
        $receiver = Provision-Emulator -EmulatorSerial $ReceiverSerial -ProvisionApiLevel $ApiLevel -TimeoutSeconds $BootTimeoutSeconds -ApkPath $NdiSdkApkPath -InstallSdk:$InstallNdiSdk -AllowReuse:$SkipBootIfAlreadyRunning
    }
    catch {
        $errors += New-E2eError -Code "RECEIVER_PROVISION_FAILED" -Message $_.Exception.Message
    }

    $status = "SUCCESS"
    if ($errors.Count -gt 0 -and ($source -or $receiver)) {
        $status = "PARTIAL_SUCCESS"
    }
    elseif ($errors.Count -gt 0) {
        $status = "FAILURE"
    }

    return New-E2eResult -Operation "Provision-DualEmulator" -Status $status -Data ([PSCustomObject]@{
            sourceEmulator = $source
            receiverEmulator = $receiver
        }) -Errors $errors
}

try {
    switch ($Action) {
        "provision-single" {
            if (-not $EmulatorId) {
                throw "EmulatorId is required when Action=provision-single"
            }

            $instance = Provision-Emulator -EmulatorSerial $EmulatorId -ProvisionApiLevel $ApiLevel -TimeoutSeconds $BootTimeoutSeconds -ApkPath $NdiSdkApkPath -InstallSdk:$InstallNdiSdk -AllowReuse:$SkipBootIfAlreadyRunning
            $result = New-E2eResult -Operation "Provision-Emulator" -Status "SUCCESS" -Data $instance
            Exit-E2eWithResult -Result $result -OutputPath $OutputPath
        }

        "state" {
            if (-not $EmulatorId) {
                throw "EmulatorId is required when Action=state"
            }

            $state = Get-EmulatorState -EmulatorSerial $EmulatorId
            $result = New-E2eResult -Operation "Get-EmulatorState" -Status "SUCCESS" -Data $state
            Exit-E2eWithResult -Result $result -OutputPath $OutputPath
        }

        default {
            $result = Provision-DualEmulator -SourceSerial $SourceEmulatorId -ReceiverSerial $ReceiverEmulatorId
            Exit-E2eWithResult -Result $result -OutputPath $OutputPath
        }
    }
}
catch {
    $result = New-E2eResult -Operation "provision-dual-emulator" -Status "FAILURE" -Errors @(
        New-E2eError -Code "PROVISIONING_FAILED" -Message $_.Exception.Message
    )
    Exit-E2eWithResult -Result $result -OutputPath $OutputPath
}
