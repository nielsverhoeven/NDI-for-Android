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
    [switch]$AutoStartIfMissing,
    [switch]$SkipBootIfAlreadyRunning,
    [string]$OutputPath = "testing/e2e/artifacts/runtime/provisioning-result.json"
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/helpers/result-handler.ps1"
. "$PSScriptRoot/helpers/emulator-adb.ps1"
. "$PSScriptRoot/helpers/entity-validator.ps1"

function Test-AutoStartEnabled {
    if ($PSBoundParameters.ContainsKey("AutoStartIfMissing")) {
        return [bool]$AutoStartIfMissing
    }

    # Default behavior: auto-start emulators when missing.
    return $true
}

function Get-EmulatorExecutable {
    if ($env:ANDROID_SDK_ROOT) {
        $candidateExe = Join-Path $env:ANDROID_SDK_ROOT "emulator/emulator.exe"
        if (Test-Path -LiteralPath $candidateExe) {
            return $candidateExe
        }

        $candidate = Join-Path $env:ANDROID_SDK_ROOT "emulator/emulator"
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return "emulator"
}

function Get-AvailableAvdNames {
    $emulatorExe = Get-EmulatorExecutable
    $lines = & $emulatorExe "-list-avds" 2>$null
    return @($lines | Where-Object { $_ -and $_.Trim() } | ForEach-Object { $_.Trim() })
}

function Resolve-PreferredAvdName {
    param([Parameter(Mandatory = $true)][string]$EmulatorSerial)

    if ($EmulatorSerial -eq $SourceEmulatorId) {
        if ($env:EMULATOR_A_AVD) {
            return $env:EMULATOR_A_AVD
        }
        return "Emulator-A"
    }

    if ($EmulatorSerial -eq $ReceiverEmulatorId) {
        if ($env:EMULATOR_B_AVD) {
            return $env:EMULATOR_B_AVD
        }
        return "Emulator-B"
    }

    return $null
}

function Resolve-AvdNameForSerial {
    param([Parameter(Mandatory = $true)][string]$EmulatorSerial)

    $available = @(Get-AvailableAvdNames)
    if ($available.Count -eq 0) {
        throw "No Android Virtual Devices are available. Create AVDs first (for example Emulator-A and Emulator-B)."
    }

    $preferred = Resolve-PreferredAvdName -EmulatorSerial $EmulatorSerial
    if ($preferred -and $available -contains $preferred) {
        return $preferred
    }

    if ($EmulatorSerial -eq $SourceEmulatorId) {
        return $available[0]
    }

    if ($EmulatorSerial -eq $ReceiverEmulatorId -and $available.Count -ge 2) {
        return $available[1]
    }

    return $available[0]
}

function Start-EmulatorForSerial {
    param([Parameter(Mandatory = $true)][string]$EmulatorSerial)

    if ($EmulatorSerial -notmatch "^emulator-(\d+)$") {
        throw "Unsupported emulator serial format: $EmulatorSerial"
    }

    $port = [int]$Matches[1]
    $emulatorExe = Get-EmulatorExecutable
    $avdName = Resolve-AvdNameForSerial -EmulatorSerial $EmulatorSerial

    Write-Host "Starting emulator $EmulatorSerial using AVD '$avdName' on port $port"
    Start-Process -FilePath $emulatorExe -ArgumentList @("-avd", $avdName, "-port", "$port", "-feature", "WindowsHypervisorPlatform") -WindowStyle Normal | Out-Null
}

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
    $autoStartEnabled = Test-AutoStartEnabled
    Write-Host "Provisioning $EmulatorSerial (alreadyRunning=$alreadyRunning, autoStartIfMissing=$autoStartEnabled, allowReuse=$AllowReuse)"

    if ($alreadyRunning -and $AllowReuse) {
        $instance = Get-EmulatorState -EmulatorSerial $EmulatorSerial
        Assert-EmulatorInstance -Instance $instance
        return $instance
    }

    if (-not $alreadyRunning -and $autoStartEnabled) {
        Start-EmulatorForSerial -EmulatorSerial $EmulatorSerial
        Start-Sleep -Seconds 3
    }
    elseif (-not $alreadyRunning) {
        throw "Device $EmulatorSerial is not running and auto-start is disabled."
    }

    if (-not (Wait-EmulatorBoot -Serial $EmulatorSerial -TimeoutSeconds $TimeoutSeconds)) {
        throw "BOOT_TIMEOUT: $EmulatorSerial did not complete boot in $TimeoutSeconds seconds"
    }

    if ($InstallSdk) {
        $effectiveApkPath = $ApkPath
        if (-not (Test-Path -LiteralPath $effectiveApkPath)) {
            $alternateAarPath = Join-Path (Split-Path -Parent $effectiveApkPath) "..\aar\sdk-bridge-release.aar"
            if (Test-Path -LiteralPath $alternateAarPath) {
                Write-Warning "NDI SDK bridge artifact is AAR at '$alternateAarPath'. APK install is skipped because AAR is not installable on emulator."
            }
            else {
                Write-Warning "NDI SDK APK not found at '$effectiveApkPath'. Skipping NDI install (if you require this, build :ndi:sdk-bridge:assembleRelease with APK output path)."
            }
        }
        else {
            Install-ApkToEmulator -Serial $EmulatorSerial -ApkPath $effectiveApkPath
        }
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
