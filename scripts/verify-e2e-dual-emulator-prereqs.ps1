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

function Add-AndroidSdkToolsToPath {
    $androidSdkRoot = if ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } elseif ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $null }
    if (-not $androidSdkRoot -or -not (Test-Path -LiteralPath $androidSdkRoot)) {
        return
    }

    $toolPaths = @(
        "platform-tools",
        "emulator",
        "cmdline-tools/latest/bin",
        "cmdline-tools/bin",
        "tools/bin"
    ) | ForEach-Object { Join-Path $androidSdkRoot $_ }

    foreach ($path in $toolPaths | Where-Object { Test-Path -LiteralPath $_ }) {
        if (-not ($env:PATH -split ";" | Where-Object { $_ -eq $path })) {
            $env:PATH = "$path;$env:PATH"
        }
    }
}

Add-AndroidSdkToolsToPath

$checks = @()

$checks += [PSCustomObject]@{ name = "adb"; ok = (Test-CommandAvailable -Name "adb") }
$checks += [PSCustomObject]@{ name = "emulator"; ok = (Test-CommandAvailable -Name "emulator") }
$checks += [PSCustomObject]@{ name = "sdkmanager"; ok = (Test-CommandAvailable -Name "sdkmanager") }

$apkPath = Join-Path $root "ndi/sdk-bridge/build/outputs/apk/release/ndi-sdk-bridge-release.apk"
$aarPath = Join-Path $root "ndi/sdk-bridge/build/outputs/aar/sdk-bridge-release.aar"
$apkExists = Test-Path -LiteralPath $apkPath
$aarExists = Test-Path -LiteralPath $aarPath

if ($apkExists) {
    $checks += [PSCustomObject]@{ name = "ndi-sdk-artifact"; ok = $true; path = $apkPath; type = "apk" }
}
elseif ($aarExists) {
    $checks += [PSCustomObject]@{ name = "ndi-sdk-artifact"; ok = $true; path = $aarPath; type = "aar"; warning = "library-artifact-only" }
}
elseif ($AllowMissingNdiSdk) {
    $checks += [PSCustomObject]@{ name = "ndi-sdk-artifact"; ok = $true; warning = "missing-but-allowed" }
}
else {
    $checks += [PSCustomObject]@{ name = "ndi-sdk-artifact"; ok = $false; path = $apkPath; type = "apk"; detail = "Expected APK or AAR is missing." }
}

$failed = @($checks | Where-Object { -not $_.ok })
$status = if ($failed.Count -eq 0) { "SUCCESS" } else { "FAILURE" }

$errors = @()
if ($failed.Count -gt 0) {
    $errors += New-E2eError -Code "PREREQ_FAILED" -Message "One or more prerequisites are missing." -Details @{ failed = $failed }
}

$result = New-E2eResult -Operation "verify-e2e-dual-emulator-prereqs" -Status $status -Data ([PSCustomObject]@{
        checks = $checks
        apkPath = if ($apkExists) { $apkPath } elseif ($aarExists) { $aarPath } else { $apkPath }
    }) -Errors $errors

Exit-E2eWithResult -Result $result -OutputPath $OutputPath
