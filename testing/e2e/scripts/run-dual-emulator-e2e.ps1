param(
    [Parameter(Mandatory = $true)]
    [string]$EmulatorASerial,

    [Parameter(Mandatory = $true)]
    [string]$EmulatorBSerial,

    [string]$AppPackage = "com.ndi.app.debug",

    [switch]$PreflightOnly
)

$ErrorActionPreference = "Stop"

Write-Host "Running dual-emulator preflight..."
if ($EmulatorASerial -eq $EmulatorBSerial) {
    throw "Publisher and receiver emulator serials must be different."
}

adb -s $EmulatorASerial get-state | Out-Null
adb -s $EmulatorBSerial get-state | Out-Null

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

adb -s $EmulatorASerial logcat -d -v time | Out-File -FilePath (Join-Path $artifactDir "publisher-preflight.log") -Encoding utf8
adb -s $EmulatorBSerial logcat -d -v time | Out-File -FilePath (Join-Path $artifactDir "receiver-preflight.log") -Encoding utf8

if ($PreflightOnly) {
    Write-Host "Preflight checks passed. Artifacts: $artifactDir"
    exit 0
}

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
    npm run test:dual-emulator -- --grep "@dual-emulator" --reporter=list
}
finally {
    if ($relayProcess -and -not $relayProcess.HasExited) {
        Stop-Process -Id $relayProcess.Id -Force
    }
    adb -s $EmulatorASerial logcat -d -v time | Out-File -FilePath (Join-Path $artifactDir "publisher-postrun.log") -Encoding utf8
    adb -s $EmulatorBSerial logcat -d -v time | Out-File -FilePath (Join-Path $artifactDir "receiver-postrun.log") -Encoding utf8
    Pop-Location
}
