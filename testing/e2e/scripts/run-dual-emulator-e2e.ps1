param(
    [Parameter(Mandatory = $true)]
    [string]$EmulatorASerial,

    [Parameter(Mandatory = $true)]
    [string]$EmulatorBSerial,

    [string]$AppPackage = "com.ndi.app.debug",

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
try {
    $relayScript = Join-Path $PSScriptRoot "ndi-relay-server.mjs"
    $relayProcess = Start-Process -FilePath "node" -ArgumentList "`"$relayScript`"" -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 1

    $env:EMULATOR_A_SERIAL = $EmulatorASerial
    $env:EMULATOR_B_SERIAL = $EmulatorBSerial
    $env:APP_PACKAGE = $AppPackage
    $env:DUAL_EMULATOR_SCREENSHOT_DIR = $screenshotDir
    npm run test:dual-emulator -- --grep "@dual-emulator" --reporter=list
}
finally {
    if ($relayProcess -and -not $relayProcess.HasExited) {
        Stop-Process -Id $relayProcess.Id -Force
    }
    adb -s $EmulatorASerial logcat -d -v time | Out-File -FilePath (Join-Path $artifactDir "publisher-postrun.log") -Encoding utf8
    adb -s $EmulatorBSerial logcat -d -v time | Out-File -FilePath (Join-Path $artifactDir "receiver-postrun.log") -Encoding utf8
    Capture-EmulatorScreenshot -Serial $EmulatorASerial -DestinationPath (Join-Path $screenshotDir "publisher-postrun.png")
    Capture-EmulatorScreenshot -Serial $EmulatorBSerial -DestinationPath (Join-Path $screenshotDir "receiver-postrun.png")
    Pop-Location
}
