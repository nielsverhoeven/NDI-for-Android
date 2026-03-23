param(
    [string]$OutputPath = "testing/e2e/artifacts/prereqs.json",
    [switch]$AllowMissingNdiSdk
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
. "$root/testing/e2e/scripts/helpers/result-handler.ps1"

function Test-CommandAvailable {
    param([Parameter(Mandatory = $true)][string]$Name)
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

$checks = @()

$checks += [PSCustomObject]@{ name = "adb"; ok = (Test-CommandAvailable -Name "adb") }
$checks += [PSCustomObject]@{ name = "emulator"; ok = (Test-CommandAvailable -Name "emulator") }
$checks += [PSCustomObject]@{ name = "sdkmanager"; ok = (Test-CommandAvailable -Name "sdkmanager") }

$apkPath = Join-Path $root "ndi/sdk-bridge/build/outputs/apk/release/ndi-sdk-bridge-release.apk"
$apkExists = Test-Path -LiteralPath $apkPath
if (-not $apkExists -and $AllowMissingNdiSdk) {
    $checks += [PSCustomObject]@{ name = "ndi-sdk-apk"; ok = $true; warning = "missing-but-allowed" }
}
else {
    $checks += [PSCustomObject]@{ name = "ndi-sdk-apk"; ok = $apkExists }
}

$failed = @($checks | Where-Object { -not $_.ok })
$status = if ($failed.Count -eq 0) { "SUCCESS" } else { "FAILURE" }

$errors = @()
if ($failed.Count -gt 0) {
    $errors += New-E2eError -Code "PREREQ_FAILED" -Message "One or more prerequisites are missing." -Details @{ failed = $failed }
}

$result = New-E2eResult -Operation "verify-e2e-dual-emulator-prereqs" -Status $status -Data ([PSCustomObject]@{
        checks = $checks
        apkPath = $apkPath
    }) -Errors $errors

Exit-E2eWithResult -Result $result -OutputPath $OutputPath
