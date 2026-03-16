param(
    [Parameter(Mandatory = $true)]
    [string]$EmulatorASerial,

    [Parameter(Mandatory = $true)]
    [string]$EmulatorBSerial
)

$ErrorActionPreference = "Stop"

Write-Host "Running dual-emulator preflight..."
adb -s $EmulatorASerial get-state | Out-Null
adb -s $EmulatorBSerial get-state | Out-Null

Write-Host "Executing Playwright dual-emulator suite..."
Push-Location "$PSScriptRoot/.."
try {
    npm run test:dual-emulator -- --grep "@dual-emulator" --reporter=list
}
finally {
    Pop-Location
}
