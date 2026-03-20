# Agent Operating Instructions - Improved Test Workflow

> **Use this for ALL future test execution tasks in NDI-for-Android**

## Core Operating Principles (Non-Negotiable)

### 1. **One Terminal Session Per Workflow**
- Set environment variables once at the start
- Chain related commands with `;` on a single line
- DO NOT spawn background processes for phases that have dependencies
- Wait for each phase to complete before proceeding to the next

### 2. **Validate → Execute → Verify → Report**
Strict sequence for each phase:
```
IF prerequisite exists → SKIP phase
ELSE → EXECUTE phase
   → WAIT for completion (don't assume)
   → CHECK for success indicator
   → IF failed → STOP and REPORT
   → ELSE → PROCEED to next phase
```

### 3. **Minimize Token Usage**
- Read test output completely before re-running
- Don't re-run failed tests without understanding the failure
- Use cached results (gradle, APK, emulators) when safe
- Combine multiple commands on one line
- Only print summaries, not full build logs

## Phase-Based Execution Template

### Phase Setup
```powershell
$env:JAVA_HOME = "C:\Program Files\Java\jdk-21.0.10"
$env:ANDROID_SDK_ROOT = "$env:LOCALAPPDATA\Android\Sdk"
cd C:\gitrepos\NDI-for-Android
```

### Phase 1: Verify Prerequisites
```powershell
# Check gradle
.\gradlew.bat --version
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Gradle not available" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Gradle ready"
```

### Phase 2: Unit Tests (Required)
```powershell
Write-Host "Running unit tests..." -ForegroundColor Yellow
$output = & .\gradlew.bat test --no-daemon 2>&1

if ($output -match "BUILD SUCCESSFUL") {
    Write-Host "✓ Tests PASSED" -ForegroundColor Green
} else {
    Write-Host "✗ Tests FAILED" -ForegroundColor Red
    Write-Host "See: app/build/reports/tests/testDebugUnitTest/index.html"
    exit 1
}
```

### Phase 3: Build APK (If needed)
```powershell
$apkPath = "app\build\outputs\apk\debug\app-debug.apk"
if (-not (Test-Path $apkPath)) {
    Write-Host "Building APK..." -ForegroundColor Yellow
    & .\gradlew.bat assembleDebug --no-daemon > $null
    if (-not (Test-Path $apkPath)) {
        Write-Host "✗ APK build failed" -ForegroundColor Red
        exit 1
    }
}
Write-Host "✓ APK ready"
```

### Phase 4: Emulator Setup (If needed)
```powershell
Write-Host "Checking emulators..." -ForegroundColor Yellow

# Check existing
$exists = avdmanager list avd 2>&1 | Select-String "Name:" | Measure-Object
if ($exists.Count -lt 2) {
    Write-Host "Creating emulators..." -ForegroundColor Yellow
    avdmanager create avd -n "Emulator-A" -k "system-images;android-34;default;x86_64" -d "pixel_5" -f
    avdmanager create avd -n "Emulator-B" -k "system-images;android-34;default;x86_64" -d "pixel_6" -f
}
Write-Host "✓ Emulators ready"
```

### Phase 5: E2E Testing (Optional)
```powershell
# Start emulators
emulator -avd "Emulator-A" -no-audio -no-window &
emulator -avd "Emulator-B" -no-audio -no-window &

# Wait for boot
Start-Sleep -Seconds 45

# Verify online
$state = adb -s emulator-5554 get-state 2>$null
if ($state -ne "device") {
    Write-Host "✗ Emulator not online after wait" -ForegroundColor Red
    exit 1
}

# Install APK and run tests
adb -s emulator-5554 install -r $apkPath
cd testing\e2e
npx playwright test
```

## Common Pitfalls to Avoid

| ❌ DON'T | ✅ DO |
|---------|------|
| Start gradle, emulator, and tests all at once | Run gradle, wait, then emulator, wait, then tests |
| Use `tail`, `grep`, `Select-String` with pipes excessively | Use `Select-Object -Last N`, `Where-Object` |
| Assume silent completion means success | Check `$LASTEXITCODE` or look for "SUCCESS" in output |
| Create 10+ terminal sessions | Use 1-2 persistent sessions per workflow |
| Run tests multiple times without reading failures | Read failure output, understand root cause, fix once |
| Skip reporting success criteria | Always show what passed and what failed |

## Test Assertion Best Practices

### Pattern 1: Deduplication Tests
```kotlin
@Test
fun testDeduplication() {
    // INPUT: [dup-a, dup-a, unique-b]
    val result = function(input)
    // EXPECTED: [unique-a, unique-b] (deduped by key)
    assertEquals(listOf("unique-a", "unique-b"), result)
}
```

**Important**: Comment the input, transformation, and expected output

### Pattern 2: Injection Tests  
```kotlin
@Test
fun testInjection() {
    // INPUT: [item-a, item-b]
    // TRANSFORMATION: injected = "INJECTED"; result = [injected] + input
    // EXPECTED: [INJECTED, item-a, item-b]
    assertEquals(listOf("INJECTED", "item-a", "item-b"), result)
}
```

**Critical**: Include injected items in expected result

### Pattern 3: Combined (Dedup + Inject)
```kotlin
@Test
fun testDeduplicationAndInjection() {
    // INPUT: [dup-a, dup-a, unique-b]
    val deduped = input.distinctBy { it }  // [dup-a, unique-b]
    val result = (listOf("INJECTED") + deduped)  // [INJECTED, dup-a, unique-b]
    
    // EXPECTED: [INJECTED, dup-a, unique-b]
    assertEquals(listOf("INJECTED", "dup-a", "unique-b"), result)
}
```

## Reporting Format

When tests complete, report in this format:

```
═══════════════════════════════════════════════════════
TEST VALIDATION REPORT
═══════════════════════════════════════════════════════

PHASE 1: Environment ✓
  ✓ Java: 21.0.10
  ✓ Android SDK: Configured
  ✓ Gradle: 9.2.1

PHASE 2: Unit Tests ✓
  ✓ All tests passed
  • 24 tests executed
  • 0 failures
  • Reports: app/build/reports/tests/testDebugUnitTest/

PHASE 3: Functionality Validation ✓
  ✓ Deduplication working
    • Input: [source-a, source-a(dup), source-b]
    • Output: [device-screen:local, source-a, source-b]
    • Deduplication: ✓ (duplicates removed)
    • Injection: ✓ (LOCAL_SCREEN at position 0)

PHASE 4: APK Build ✓
  ✓ Debug APK built (12.3 MB)

═══════════════════════════════════════════════════════
✓ VALIDATION COMPLETE - All functionality working
═══════════════════════════════════════════════════════
```

## Quick Reference

### Start Validation
```powershell
cd C:\gitrepos\NDI-for-Android
.\scripts\validate-tests.ps1
```

### Manual Minimal Validation
```powershell
# Setup
$env:JAVA_HOME = "C:\Program Files\Java\jdk-21.0.10"
$env:ANDROID_SDK_ROOT = "$env:LOCALAPPDATA\Android\Sdk"
cd C:\gitrepos\NDI-for-Android

# Test
.\gradlew.bat test --no-daemon

# Done if PASSED
```

### Kill Stale Processes (if needed)
```powershell
taskkill /F /IM emulator.exe
adb kill-server
```

---

**Effective Use of This Guide**: Before running tests, check:
- [ ] Using one terminal session?
- [ ] Environment set once at start?
- [ ] Following phase sequence (don't parallelize)?
- [ ] Checking success indicators before moving on?
- [ ] Recording output in structured format?

If all checked, expected outcome: Clean test run in 3-5 minutes without rate limits.

