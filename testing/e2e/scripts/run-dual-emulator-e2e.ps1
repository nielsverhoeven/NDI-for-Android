param(
    [Parameter(Mandatory = $true)]
    [string]$EmulatorASerial,

    [Parameter(Mandatory = $true)]
    [string]$EmulatorBSerial,

    [string]$AppPackage = "com.ndi.app.debug",

    [string]$ScenarioCheckpointArtifactPath,

    [switch]$PreflightOnly
)

$ErrorActionPreference = "Stop"

function Capture-EmulatorScreenshot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Serial,
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    $remotePath = "/sdcard/ndi-e2e-screen.png"
    adb -s $Serial shell screencap -p $remotePath | Out-Null
    adb -s $Serial pull $remotePath $DestinationPath | Out-Null
    adb -s $Serial shell rm $remotePath | Out-Null
}

function Resolve-LatencyArtifactPath {
    param(
        [string]$ExplicitPath,
        [string[]]$Candidates
    )

    if ($ExplicitPath) {
        $fullPath = [System.IO.Path]::GetFullPath($ExplicitPath)
        if (Test-Path $fullPath) {
            return $fullPath
        }
    }

    foreach ($candidate in $Candidates) {
        if (-not $candidate) {
            continue
        }

        $fullCandidate = [System.IO.Path]::GetFullPath($candidate)
        if (Test-Path $fullCandidate) {
            return $fullCandidate
        }
    }

    return $null
}

function Get-EmulatorPortFromSerial {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Serial
    )

    if ($Serial -notmatch "^emulator-(\d+)$") {
        throw "Unsupported emulator serial format: $Serial"
    }

    return [int]$Matches[1]
}

function Get-AvdNameFromSerial {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Serial
    )

    $avdLines = adb -s $Serial emu avd name
    $avdName = ($avdLines | Where-Object { $_ -and $_.Trim() -ne "OK" } | Select-Object -First 1)

    if (-not $avdName) {
        throw "Unable to resolve AVD name for $Serial"
    }

    return $avdName.Trim()
}

function Wait-ForEmulatorOnline {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Serial,
        [int]$TimeoutSeconds = 180
    )

    $started = Get-Date
    while (((Get-Date) - $started).TotalSeconds -lt $TimeoutSeconds) {
        $state = ""
        try {
            $state = (adb -s $Serial get-state).Trim()
        }
        catch {
            $state = ""
        }

        if ($state -eq "device") {
            return
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for $Serial to come online."
}

function Test-PackageManagerReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Serial
    )

    try {
        $androidPackage = (adb -s $Serial shell pm path android).Trim()
        return $androidPackage -and $androidPackage.StartsWith("package:")
    }
    catch {
        return $false
    }
}

function Wait-ForEmulatorBootComplete {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Serial,
        [int]$TimeoutSeconds = 240,
        [int]$StableSeconds = 6
    )

    $started = Get-Date
    $stableSince = $null

    while (((Get-Date) - $started).TotalSeconds -lt $TimeoutSeconds) {
        $state = ""
        try {
            $state = (adb -s $Serial get-state).Trim()
        }
        catch {
            $state = ""
        }

        if ($state -ne "device") {
            $stableSince = $null
            Start-Sleep -Seconds 2
            continue
        }

        $sysBootComplete = ""
        $devBootComplete = ""
        $bootAnimState = ""
        try {
            $sysBootComplete = (adb -s $Serial shell getprop sys.boot_completed).Trim()
            $devBootComplete = (adb -s $Serial shell getprop dev.bootcomplete).Trim()
            $bootAnimState = (adb -s $Serial shell getprop init.svc.bootanim).Trim().ToLowerInvariant()
        }
        catch {
            $stableSince = $null
            Start-Sleep -Seconds 2
            continue
        }

        $bootFlagReady = ($sysBootComplete -eq "1" -or $devBootComplete -eq "1")
        $bootAnimationStopped = [string]::IsNullOrWhiteSpace($bootAnimState) -or $bootAnimState -eq "stopped"
        $packageManagerReady = Test-PackageManagerReady -Serial $Serial

        if ($bootFlagReady -and $bootAnimationStopped -and $packageManagerReady) {
            if (-not $stableSince) {
                $stableSince = Get-Date
            }

            if (((Get-Date) - $stableSince).TotalSeconds -ge $StableSeconds) {
                return
            }
        }
        else {
            $stableSince = $null
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for $Serial to report boot-complete and package-manager readiness."
}

function Get-VisibleEmulatorWindowProcesses {
    return Get-Process -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ProcessName -like "qemu-system-*" -and
            $_.MainWindowHandle -ne 0 -and
            $_.MainWindowTitle -like "Android Emulator*"
        }
}

function Focus-EmulatorWindows {
    param(
        [Parameter(Mandatory = $true)]
        [array]$WindowProcesses
    )

    if (-not ("Native.Win32Show" -as [type])) {
        Add-Type -Namespace Native -Name Win32Show -MemberDefinition '[System.Runtime.InteropServices.DllImport("user32.dll")]public static extern bool ShowWindowAsync(System.IntPtr hWnd, int nCmdShow);[System.Runtime.InteropServices.DllImport("user32.dll")]public static extern bool SetForegroundWindow(System.IntPtr hWnd);' | Out-Null
    }

    foreach ($proc in $WindowProcesses) {
        [Native.Win32Show]::ShowWindowAsync($proc.MainWindowHandle, 9) | Out-Null
        [Native.Win32Show]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
    }
}

function Ensure-VisibleEmulatorWindows {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SerialA,
        [Parameter(Mandatory = $true)]
        [string]$SerialB
    )

    $visibleWindowProcesses = Get-VisibleEmulatorWindowProcesses
    if ($visibleWindowProcesses.Count -ge 2) {
        Write-Host "Visible emulator windows already detected."
        Focus-EmulatorWindows -WindowProcesses $visibleWindowProcesses
        return
    }

    Write-Host "Detected no visible emulator windows. Restarting emulators with visible windows..."

    $avdA = Get-AvdNameFromSerial -Serial $SerialA
    $avdB = Get-AvdNameFromSerial -Serial $SerialB
    $portA = Get-EmulatorPortFromSerial -Serial $SerialA
    $portB = Get-EmulatorPortFromSerial -Serial $SerialB

    adb -s $SerialA emu kill | Out-Null
    adb -s $SerialB emu kill | Out-Null
    Start-Sleep -Seconds 4

    Start-Process -FilePath "emulator" -ArgumentList @("-avd", $avdA, "-port", "$portA", "-feature", "WindowsHypervisorPlatform") -WindowStyle Normal | Out-Null
    Start-Process -FilePath "emulator" -ArgumentList @("-avd", $avdB, "-port", "$portB", "-feature", "WindowsHypervisorPlatform") -WindowStyle Normal | Out-Null

    Wait-ForEmulatorOnline -Serial $SerialA
    Wait-ForEmulatorOnline -Serial $SerialB

    $windowCheckStart = Get-Date
    while (((Get-Date) - $windowCheckStart).TotalSeconds -lt 60) {
        $windowed = Get-VisibleEmulatorWindowProcesses
        if ($windowed.Count -ge 2) {
            Write-Host "Visible emulator windows confirmed."
            Focus-EmulatorWindows -WindowProcesses $windowed
            return
        }

        Start-Sleep -Seconds 2
    }

    throw "Emulators relaunched but visible GUI windows were not detected."
}

function Resolve-MajorFromSdk {
    param(
        [Parameter(Mandatory = $true)]
        [int]$SdkInt,
        [Parameter(Mandatory = $true)]
        [string]$Release
    )

    $sdkToMajor = @{
        32 = 12
        33 = 13
        34 = 14
        35 = 15
        36 = 16
    }

    $releaseMajor = 0
    [void][int]::TryParse(($Release -split '\.')[0], [ref]$releaseMajor)
    if ($releaseMajor -gt 0) {
        return $releaseMajor
    }

    if ($sdkToMajor.ContainsKey($SdkInt)) {
        return $sdkToMajor[$SdkInt]
    }

    return $SdkInt
}

function Get-AndroidVersionProfile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Serial,
        [Parameter(Mandatory = $true)]
        [string]$Role
    )

    $sdkInt = [int](adb -s $Serial shell getprop ro.build.version.sdk).Trim()
    $release = (adb -s $Serial shell getprop ro.build.version.release).Trim()
    $codename = (adb -s $Serial shell getprop ro.build.version.codename).Trim()
    $majorVersion = Resolve-MajorFromSdk -SdkInt $sdkInt -Release $release

    return [PSCustomObject]@{
        role = $Role
        serial = $Serial
        sdkInt = $sdkInt
        release = $release
        codename = $codename
        majorVersion = $majorVersion
    }
}

function Get-SupportedVersionWindow {
    $highestMajor = 16
    $windowSize = 5
    $lowestMajor = $highestMajor - ($windowSize - 1)

    return [PSCustomObject]@{
        highestSupportedMajor = $highestMajor
        lowestSupportedMajor = $lowestMajor
        windowSize = $windowSize
    }
}

function Assert-SupportedVersionOrFailFast {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Profile,
        [Parameter(Mandatory = $true)]
        [object]$Window
    )

    $supported = $Profile.majorVersion -ge $Window.lowestSupportedMajor -and $Profile.majorVersion -le $Window.highestSupportedMajor
    if ($supported) {
        return
    }

    throw "Unsupported Android version for $($Profile.role) ($($Profile.serial)): SDK=$($Profile.sdkInt), major=$($Profile.majorVersion), release=$($Profile.release). Supported major range: $($Window.lowestSupportedMajor)-$($Window.highestSupportedMajor)."
}

Write-Host "Running dual-emulator preflight..."
if ($EmulatorASerial -eq $EmulatorBSerial) {
    throw "Publisher and receiver emulator serials must be different."
}

# Verify emulators are online and visible
Write-Host "Checking emulator connectivity..."
$stateA = adb -s $EmulatorASerial get-state
$stateB = adb -s $EmulatorBSerial get-state

if ($stateA -ne "device" -or $stateB -ne "device") {
    Write-Host "ERROR: One or both emulators are not online or not visible."
    Write-Host "Publisher ($EmulatorASerial) state: $stateA"
    Write-Host "Receiver ($EmulatorBSerial) state: $stateB"
    Write-Host ""
    Write-Host "IMPORTANT: Both emulators must be running with GUI windows VISIBLE."
    Write-Host "Launch emulators in separate terminal windows:"
    Write-Host "  Terminal 1: emulator -avd Emulator-A -feature WindowsHypervisorPlatform"
    Write-Host "  Terminal 2: emulator -avd Emulator-B -feature WindowsHypervisorPlatform"
    Write-Host ""
    Write-Host "Keep emulator windows visible (do not minimize) during test execution."
    throw "Emulators not ready or not visible. See instructions above."
}

Ensure-VisibleEmulatorWindows -SerialA $EmulatorASerial -SerialB $EmulatorBSerial

$stateA = (adb -s $EmulatorASerial get-state).Trim()
$stateB = (adb -s $EmulatorBSerial get-state).Trim()
if ($stateA -ne "device" -or $stateB -ne "device") {
    throw "Emulators failed to remain online after visibility enforcement."
}

Write-Host "Waiting for emulator boot-complete and package-manager readiness..."
Wait-ForEmulatorBootComplete -Serial $EmulatorASerial
Wait-ForEmulatorBootComplete -Serial $EmulatorBSerial

$publisherPackage = adb -s $EmulatorASerial shell pm path $AppPackage
if (-not $publisherPackage -or -not $publisherPackage.StartsWith("package:")) {
    throw "Package $AppPackage is not installed on publisher $EmulatorASerial."
}

$receiverPackage = adb -s $EmulatorBSerial shell pm path $AppPackage
if (-not $receiverPackage -or -not $receiverPackage.StartsWith("package:")) {
    throw "Package $AppPackage is not installed on receiver $EmulatorBSerial."
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$artifactDir = Join-Path $PSScriptRoot "..\artifacts\dual-emulator-$timestamp"
New-Item -Path $artifactDir -ItemType Directory -Force | Out-Null
$screenshotDir = Join-Path $artifactDir "screenshots"
New-Item -Path $screenshotDir -ItemType Directory -Force | Out-Null
$checkpointArtifactPath = if ($ScenarioCheckpointArtifactPath) {
    [System.IO.Path]::GetFullPath($ScenarioCheckpointArtifactPath)
}
else {
    Join-Path $artifactDir "scenario-checkpoints.json"
}
$checkpointArtifactDir = Split-Path -Path $checkpointArtifactPath -Parent
if (-not [string]::IsNullOrWhiteSpace($checkpointArtifactDir)) {
    New-Item -Path $checkpointArtifactDir -ItemType Directory -Force | Out-Null
}

$checkpointStub = [PSCustomObject]@{
    runStartedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    publisherSerial = $EmulatorASerial
    receiverSerial = $EmulatorBSerial
    status = "PENDING"
}
$checkpointStub | ConvertTo-Json -Depth 4 | Out-File -FilePath $checkpointArtifactPath -Encoding utf8

$publisherProfile = Get-AndroidVersionProfile -Serial $EmulatorASerial -Role "publisher"
$receiverProfile = Get-AndroidVersionProfile -Serial $EmulatorBSerial -Role "receiver"
$supportedWindow = Get-SupportedVersionWindow

$versionDiagnostics = [PSCustomObject]@{
    supportWindow = $supportedWindow
    publisher = $publisherProfile
    receiver = $receiverProfile
}
$versionDiagnostics | ConvertTo-Json -Depth 6 | Out-File -FilePath (Join-Path $artifactDir "android-version-diagnostics.json") -Encoding utf8

Assert-SupportedVersionOrFailFast -Profile $publisherProfile -Window $supportedWindow
Assert-SupportedVersionOrFailFast -Profile $receiverProfile -Window $supportedWindow

adb -s $EmulatorASerial logcat -d -v time | Out-File -FilePath (Join-Path $artifactDir "publisher-preflight.log") -Encoding utf8
adb -s $EmulatorBSerial logcat -d -v time | Out-File -FilePath (Join-Path $artifactDir "receiver-preflight.log") -Encoding utf8
Capture-EmulatorScreenshot -Serial $EmulatorASerial -DestinationPath (Join-Path $screenshotDir "publisher-preflight.png")
Capture-EmulatorScreenshot -Serial $EmulatorBSerial -DestinationPath (Join-Path $screenshotDir "receiver-preflight.png")

if ($PreflightOnly) {
    Write-Host "Preflight checks passed. Artifacts: $artifactDir"
    exit 0
}

Write-Host ""
Write-Host "=========================================="
Write-Host "IMPORTANT: Keep emulator windows VISIBLE during test execution"
Write-Host "=========================================="
Write-Host "The test harness will capture screenshots and validate visual stream content."
Write-Host "Minimizing or hiding emulator windows will cause validation failures."
Write-Host ""
Write-Host "Executing Playwright dual-emulator suite..."
Push-Location "$PSScriptRoot/.."
$relayProcess = $null
$suiteCompleted = $false
$suiteFailure = $null
$suiteStartedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
$summaryPath = Join-Path $artifactDir "run-summary.json"
try {
    $relayScript = Join-Path $PSScriptRoot "ndi-relay-server.mjs"
    $relayProcess = Start-Process -FilePath "node" -ArgumentList "`"$relayScript`"" -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 1

    $env:EMULATOR_A_SERIAL = $EmulatorASerial
    $env:EMULATOR_B_SERIAL = $EmulatorBSerial
    $env:APP_PACKAGE = $AppPackage
    $env:DUAL_EMULATOR_SCREENSHOT_DIR = $screenshotDir
    $env:DUAL_EMULATOR_CHECKPOINT_PATH = $checkpointArtifactPath
    npm run test:dual-emulator -- --grep "@dual-emulator" --reporter=list
    if ($LASTEXITCODE -ne 0) {
        throw "Playwright dual-emulator suite failed with exit code $LASTEXITCODE"
    }
    $suiteCompleted = $true
}
catch {
    $suiteFailure = $_.Exception.Message
}
finally {
    if ($relayProcess -and -not $relayProcess.HasExited) {
        Stop-Process -Id $relayProcess.Id -Force
    }
    adb -s $EmulatorASerial logcat -d -v time | Out-File -FilePath (Join-Path $artifactDir "publisher-postrun.log") -Encoding utf8
    adb -s $EmulatorBSerial logcat -d -v time | Out-File -FilePath (Join-Path $artifactDir "receiver-postrun.log") -Encoding utf8
    Capture-EmulatorScreenshot -Serial $EmulatorASerial -DestinationPath (Join-Path $screenshotDir "publisher-postrun.png")
    Capture-EmulatorScreenshot -Serial $EmulatorBSerial -DestinationPath (Join-Path $screenshotDir "receiver-postrun.png")

    # Surface failed-step diagnostics from the checkpoint artifact (T036).
    if (Test-Path $checkpointArtifactPath) {
        try {
            $cpJson = Get-Content $checkpointArtifactPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
            if ($cpJson.failedStepName) {
                Write-Host ""
                Write-Host "=========================================="
                Write-Host "FAILED STEP: $($cpJson.failedStepName) (step $($cpJson.failedStepIndex))"
                $failedCheckpoint = $cpJson.checkpoints | Where-Object { $_.status -eq "FAILED" } | Select-Object -First 1
                if ($failedCheckpoint -and $failedCheckpoint.failureReason) {
                    Write-Host "REASON: $($failedCheckpoint.failureReason)"
                }
                Write-Host "Checkpoint artifact: $checkpointArtifactPath"
                Write-Host "=========================================="
            }
        }
        catch {
            # Checkpoint file may be partial on early failure; skip silently.
        }
    }

    $summary = [PSCustomObject]@{
        startedAtUtc = $suiteStartedAtUtc
        finishedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        completed = $suiteCompleted
        failure = $suiteFailure
        checkpointArtifactPath = $checkpointArtifactPath
        screenshotDirectory = $screenshotDir
        latencyArtifacts = [PSCustomObject]@{
            sourceRecordingPath = (Resolve-LatencyArtifactPath -ExplicitPath $env:DUAL_EMULATOR_SOURCE_RECORDING_PATH -Candidates @(
                (Join-Path $artifactDir "recordings/source-recording.mp4"),
                (Join-Path $artifactDir "recordings/publisher-recording.mp4")
            ))
            receiverRecordingPath = (Resolve-LatencyArtifactPath -ExplicitPath $env:DUAL_EMULATOR_RECEIVER_RECORDING_PATH -Candidates @(
                (Join-Path $artifactDir "recordings/receiver-recording.mp4")
            ))
            analysisArtifactPath = (Resolve-LatencyArtifactPath -ExplicitPath $env:DUAL_EMULATOR_LATENCY_ANALYSIS_PATH -Candidates @(
                (Join-Path $artifactDir "latency-analysis.json"),
                (Join-Path $artifactDir "analysis/latency-analysis.json")
            ))
        }
    }
    $summary | ConvertTo-Json -Depth 4 | Out-File -FilePath $summaryPath -Encoding utf8

    Pop-Location
}

if (-not $suiteCompleted) {
    $message = if ($suiteFailure) { $suiteFailure } else { "Playwright suite did not complete." }
    throw "Dual-emulator run failed or was partial: $message. Summary: $summaryPath"
}
