param(
    [string[]]$EmulatorIds = @("emulator-5554", "emulator-5556"),
    [string[]]$Packages = @("com.ndi.app", "com.ndi.app.debug"),
    [string]$OutputPath = "testing/e2e/artifacts/runtime/reset-result.json"
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/helpers/result-handler.ps1"
. "$PSScriptRoot/helpers/emulator-adb.ps1"

function Reset-EmulatorState {
    param([Parameter(Mandatory = $true)][string]$EmulatorSerial)

    $cleared = @()
    foreach ($pkg in $Packages) {
        $packagePath = Invoke-Adb -Arguments @("-s", $EmulatorSerial, "shell", "pm", "path", $pkg) -AllowFailure
        if ($packagePath -match "^package:") {
            Invoke-Adb -Arguments @("-s", $EmulatorSerial, "shell", "pm", "clear", $pkg) -AllowFailure | Out-Null
            Invoke-Adb -Arguments @("-s", $EmulatorSerial, "shell", "am", "force-stop", $pkg) -AllowFailure | Out-Null
            $cleared += $pkg
        }
    }

    return [PSCustomObject]@{
        emulatorId = $EmulatorSerial
        packagesCleared = $cleared
        resetAt = (Get-Date).ToUniversalTime().ToString("o")
    }
}

try {
    $data = @()
    foreach ($id in $EmulatorIds) {
        $data += Reset-EmulatorState -EmulatorSerial $id
    }

    $result = New-E2eResult -Operation "Reset-EmulatorState" -Status "SUCCESS" -Data $data
    Exit-E2eWithResult -Result $result -OutputPath $OutputPath
}
catch {
    $result = New-E2eResult -Operation "Reset-EmulatorState" -Status "FAILURE" -Errors @(
        New-E2eError -Code "RESET_FAILED" -Message $_.Exception.Message
    )
    Exit-E2eWithResult -Result $result -OutputPath $OutputPath
}
