param(
    [switch]$CiMode,
    [switch]$AllowMissingNdiSdk
)

$ErrorActionPreference = "Stop"

function Test-CommandAvailable {
    param([string]$Name)

    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Add-Result {
    param(
        [string]$Name,
        [bool]$Ok,
        [string]$Detail
    )

    [pscustomobject]@{
        Name = $Name
        Ok = $Ok
        Detail = $Detail
    }
}

$results = @()

$requiredCommands = @("java", "javac", "adb", "sdkmanager", "avdmanager", "emulator", "cmake", "ninja")
foreach ($command in $requiredCommands) {
    $available = Test-CommandAvailable -Name $command
    $detail = if ($available) { "Found on PATH" } else { "Missing from PATH" }
    $results += Add-Result -Name "command:$command" -Ok $available -Detail $detail
}

$javaHome = $env:JAVA_HOME
$androidSdkRoot = if ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } elseif ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $null }

$results += Add-Result -Name "env:JAVA_HOME" -Ok (-not [string]::IsNullOrWhiteSpace($javaHome)) -Detail ($(if ($javaHome) { $javaHome } else { "Unset" }))
$results += Add-Result -Name "env:ANDROID_SDK_ROOT" -Ok (-not [string]::IsNullOrWhiteSpace($androidSdkRoot)) -Detail ($(if ($androidSdkRoot) { $androidSdkRoot } else { "Unset" }))

$ndiSdkCandidates = @()
if (-not [string]::IsNullOrWhiteSpace($env:NDI_SDK_DIR)) {
    $ndiSdkCandidates += $env:NDI_SDK_DIR
}

$repoLocalProperties = Join-Path $PSScriptRoot "..\local.properties"
if (Test-Path $repoLocalProperties) {
    $ndiLine = Select-String -Path $repoLocalProperties -Pattern "^ndi\.sdk\.dir=(.+)$" -ErrorAction SilentlyContinue
    if ($ndiLine) {
        $ndiSdkCandidates += (($ndiLine.Matches[0].Groups[1].Value) -replace "\\\\", "\\" -replace "C\\:", "C:")
    }
}

$ndiSdkCandidates += "C:\Program Files\NDI\NDI 6 SDK (Android)"
$ndiSdkPath = $ndiSdkCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

$ndiOk = $AllowMissingNdiSdk.IsPresent -or (-not [string]::IsNullOrWhiteSpace($ndiSdkPath))
$ndiDetail = if ($ndiSdkPath) { $ndiSdkPath } elseif ($AllowMissingNdiSdk) { "Skipped in current mode" } else { "NDI Android SDK not found" }
$results += Add-Result -Name "sdk:NDI" -Ok $ndiOk -Detail $ndiDetail

$androidPackages = @("platform-tools", "platforms;android-34", "build-tools;34.0.0")
if ($androidSdkRoot) {
    foreach ($package in $androidPackages) {
        $packagePath = Join-Path $androidSdkRoot ($package -replace ";", "\\")
        $results += Add-Result -Name "package:$package" -Ok (Test-Path $packagePath) -Detail $packagePath
    }
}

$failed = $results | Where-Object { -not $_.Ok }

Write-Host "Android prerequisite verification"
foreach ($result in $results) {
    $status = if ($result.Ok) { "PASS" } else { "FAIL" }
    Write-Host ("[{0}] {1} - {2}" -f $status, $result.Name, $result.Detail)
}

if ($failed.Count -gt 0) {
    if ($CiMode) {
        Write-Error ("Prerequisite verification failed for {0} checks." -f $failed.Count)
    } else {
        exit 1
    }
}
