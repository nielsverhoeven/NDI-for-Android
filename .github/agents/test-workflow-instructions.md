# Test Workflow Instructions - NDI for Android

## Overview
Efficient, linear test execution with minimal terminal overhead. Avoid parallel operations that create confusion and rate limits.

## Core Principles
1. **Single Sequential Terminal**: Use ONE persistent terminal session for the entire workflow
2. **Verify Before Acting**: Always check prerequisites exist before proceeding
3. **Fail Fast**: If any step fails, stop and report clearly
4. **Reuse, Don't Rebuild**: Skip steps that are already complete
5. **Capture Outputs**: Save test results to files, don't rely on terminal parsing

## Test Workflow (Step-by-Step)

### Phase 1: Environment Setup (Run Once)
```powershell
# Single command to set environment and verify
$env:JAVA_HOME = "C:\Program Files\Java\jdk-21.0.10"
$env:ANDROID_SDK_ROOT = "$env:LOCALAPPDATA\Android\Sdk"
cd C:\gitrepos\NDI-for-Android

# Verify gradle works
.\gradlew.bat --version

# Verify emulators can be created (before attempting)
avdmanager list avd
```

**Skip this phase if**: Gradle version shows 9.2.1 and Android SDK is accessible

### Phase 2: Unit Tests (One Command)
```powershell
# Run all unit tests - wait for completion
.\gradlew.bat test --no-daemon

# Check results
if (Test-Path "app\build\reports\tests\testDebugUnitTest\index.html") {
    Write-Host "✓ Unit tests completed - check HTML report"
} else {
    Write-Host "✗ Unit tests failed - check gradle output above"
    exit 1
}
```

**Expected Outcomes**:
- All tests pass OR
- Known test failures are understood and documented
- Test reports generated in `*/build/reports/tests/`

### Phase 3: Build Debug APK (One Command)
```powershell
# Build APK for emulator testing
.\gradlew.bat assembleDebug --no-daemon

# Verify APK exists
$apk = "app\build\outputs\apk\debug\app-debug.apk"
if (Test-Path $apk) {
    Write-Host "✓ Debug APK built: $(Get-Item $apk | % Length) bytes"
} else {
    Write-Host "✗ APK not found"
    exit 1
}
```

**Skip this phase if**: APK exists and source hasn't changed

### Phase 4: Prepare Emulators (Sequential)
```powershell
# Check if emulators exist
$emulatorsNeeded = @("Emulator-A", "Emulator-B")
$existingEmulators = (avdmanager list avd | Select-String "Name:" | % { $_.Line -replace ".*Name: ", "" }).Trim()

if (-not ($emulatorsNeeded | Where-Object { $_ -in $existingEmulators })) {
    Write-Host "Creating emulators..."
    avdmanager create avd -n "Emulator-A" -k "system-images;android-34;default;x86_64" -d "pixel_5" -f
    avdmanager create avd -n "Emulator-B" -k "system-images;android-34;default;x86_64" -d "pixel_6" -f
    Write-Host "✓ Emulators created"
} else {
    Write-Host "✓ Emulators already exist"
}
```

**Skip this phase if**: Both emulators exist

### Phase 5: Start Emulators (Sequential Wait)
```powershell
# Start first emulator and wait
Write-Host "Starting Emulator-A..."
& emulator -avd "Emulator-A" -no-audio -no-window -wipe-data &
Start-Sleep -Seconds 15

# Check if online
$adbState = adb -s emulator-5554 get-state 2>$null
if ($adbState -eq "device") {
    Write-Host "✓ Emulator-A online: emulator-5554"
} else {
    Write-Host "⏳ Emulator-A still booting, waiting..."
    Start-Sleep -Seconds 20
}

# Start second emulator
Write-Host "Starting Emulator-B..."
& emulator -avd "Emulator-B" -no-audio -no-window -wipe-data &
Start-Sleep -Seconds 15

# Check if online
$adbState = adb -s emulator-5556 get-state 2>$null
if ($adbState -eq "device") {
    Write-Host "✓ Emulator-B online: emulator-5556"
} else {
    Write-Host "⏳ Emulator-B still booting, waiting..."
    Start-Sleep -Seconds 20
}

# Final verification
adb devices
```

**Critical**: 
- Serial numbers are typically `emulator-5554` (port 5554) and `emulator-5556` (port 5556)
- Boot time is 30-60 seconds - adjust sleep times as needed
- Both emulators must show "device" not "offline"

### Phase 6: Install APK (Sequential)
```powershell
$apk = "app\build\outputs\apk\debug\app-debug.apk"

# Install on Emulator-A
Write-Host "Installing APK on Emulator-A..."
adb -s emulator-5554 install -r $apk
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ APK installed on Emulator-A"
} else {
    Write-Host "✗ Installation failed"
    exit 1
}

# Install on Emulator-B
Write-Host "Installing APK on Emulator-B..."
adb -s emulator-5556 install -r $apk
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ APK installed on Emulator-B"
} else {
    Write-Host "✗ Installation failed"
    exit 1
}
```

### Phase 7: Validate Functionality
```powershell
# Test: Discovery deduplication (NdiDiscoveryRepositoryImpl)
Write-Host "Testing discovery deduplication..."
adb -s emulator-5554 shell am instrument -w com.ndi.app.debug/androidx.test.runner.AndroidJUnitRunner

# Test: Source list displays correct sources
Write-Host "Testing source list..."
adb -s emulator-5554 shell "getprop com.ndi.test.sources"

# Run E2E test with relay server
Write-Host "Starting dual-emulator test..."
cd testing\e2e
npm install --silent
npx playwright test --headed=false

Write-Host "✓ All tests completed"
```

## Test Assertions Reference

### NdiDiscoveryRepositoryContractTest
**Test**: `discoverSources_deduplicatesByCanonicalSourceId`
- **Input**: Sources with duplicate sourceIds
  - `NdiSource("source-a", "Camera A", ...)`
  - `NdiSource("source-a", "Camera A Duplicate", ...)` (duplicate)
  - `NdiSource("source-b", "Camera B", ...)`
- **Expected Output**: `["device-screen:local", "source-a", "source-b"]`
- **Logic**: 
  1. Deduplicate by sourceId (keeps first occurrence)
  2. Inject LOCAL_SCREEN_SOURCE at position 0
  3. Result has no duplicate sourceIds

## Common Issues & Fixes

| Issue | Cause | Fix |
|-------|-------|-----|
| "tail not recognized" | Windows PowerShell doesn't have tail | Use `Select-Object -Last N` |
| Rate limit errors | Too many concurrent terminal operations | Use single sequential terminal |
| Emulator won't start | Port already in use | Kill old emulator: `adb emu kill` |
| Emulator offline | Still booting | Wait longer before installation |
| APK install fails | App already running | `adb shell am force-stop com.ndi.app.debug` |
| Test assertion fails | Expected vs actual mismatch | Check test comments for injection side effects |

## Success Criteria

- ✅ Gradle version 9.2.1 available
- ✅ Both emulators boot and show "device" in `adb devices`
- ✅ APK installs successfully on both emulators
- ✅ Unit tests pass (or known failures documented)
- ✅ Deduplication test validates sourceId uniqueness
- ✅ E2E test completes with all assertions passing

## Next Run Checklist

Before running tests again:
- [ ] Check if gradle wrapper exists (skip download if yes)
- [ ] Check if emulators exist (skip creation if yes)
- [ ] Check if APK is newer than last build
- [ ] Kill stale emulator processes: `taskkill /F /IM emulator.exe`
- [ ] Clear ADB state: `adb kill-server && adb start-server`

