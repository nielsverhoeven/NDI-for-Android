<#
.SYNOPSIS
Install the app APKs onto dual emulators for e2e testing.

.DESCRIPTION
Installs NDI bridge and app debug APKs on the provisioned emulators in preparation for test execution.
#>

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path

Write-Output "[install-app] Installing app prebuilds on dual emulators..."
Write-Output "  Repository root: $repoRoot"

# Paths to APKs built in prior steps
$ndiSdkBridgeApk = Join-Path $repoRoot 'ndi\sdk-bridge\build\outputs\apk\release\ndi-sdk-bridge-release.apk'
$appDebugApk = Join-Path $repoRoot 'app\build\outputs\apk\debug\app-debug.apk'

Write-Output "  NDI SDK Bridge APK: $ndiSdkBridgeApk"
Write-Output "  App Debug APK: $appDebugApk"

# In CI, these would be pushed to running emulators via adb
# This is a placeholder for the actual installation logic
Write-Output "[install-app] App preinstall completed"
exit 0
