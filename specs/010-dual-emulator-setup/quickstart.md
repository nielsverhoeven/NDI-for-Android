# Quick Start: Dual Emulator Testing Infrastructure

**Target Audience**: Developers (local setup), CI/CD engineers (pipeline integration)

---

## Prerequisites

### Local Development

**Windows**:
- [ ] Android SDK (emulator, ADB tools) - install via Android Studio
- [ ] PowerShell 5.0+ (Windows 10+, built-in)
- [ ] 4 GB RAM available (for dual emulators)
- [ ] 2 GB free disk space per emulator
- [ ] Java 17+ (for building app)

**macOS/Linux**:
- [ ] Android SDK (emulator, ADB tools)
- [ ] Bash 4.0+ (built-in)
- [ ] Python 3.8+ (for artifact collection helper scripts, optional)

### Verify Prerequisites

```bash
adb version                     # Check ADB is available
./gradlew --version            # Check Gradle wrapper
```

**Windows**: Run the prerequisite gate first:
```powershell
scripts/verify-android-prereqs.ps1
```

---

## Local Development Setup

### Step 1: Clone NDI SDK Bridge APK

Ensure the NDI SDK bridge is built:

```bash
./gradlew :ndi:sdk-bridge:assembleRelease
```

**Output**: `ndi/sdk-bridge/build/outputs/apk/release/ndi-sdk-bridge-release.apk`

### Step 2: Verify Emulator Images Available

Check that Android SDK has API 32-35 images:

```bash
emulator -list-avds                # List available emulator AVDs
# OR
sdkmanager --list_installed        # Check installed system images
```

If missing, install:

```bash
sdkmanager "system-images;android-34;google_apis;x86_64"
```

### Step 3: Run Provisioning Script (Local)

**Windows**:
```powershell
# Clone the provision script
$scriptPath = "testing/e2e/scripts/provision-dual-emulator.ps1"

# Provision emulator-5554 (source device)
& $scriptPath -EmulatorId "emulator-5554" `
              -ApiLevel 34 `
              -NdiSdkApkPath "ndi/sdk-bridge/build/outputs/apk/release/ndi-sdk-bridge-release.apk" `
              -InstallNdiSdk $true `
              -BootTimeoutSeconds 90

# Provision emulator-5556 (receiver device)
& $scriptPath -EmulatorId "emulator-5556" `
              -ApiLevel 34 `
              -NdiSdkApkPath "ndi/sdk-bridge/build/outputs/apk/release/ndi-sdk-bridge-release.apk" `
              -InstallNdiSdk $true `
              -BootTimeoutSeconds 90
```

**Expected Output**:
```
[14:30:00] Provisioning emulator-5554 (API 34)...
[14:30:05] Checking system image...
[14:30:10] Creating emulator instance...
[14:31:00] Waiting for boot (timeout: 90s)...
[14:31:45] Device ready! Boot time: 45000ms
[14:31:50] Installing NDI SDK APK...
[14:32:00] NDI SDK APK installed successfully
✅ Provisioning complete: emulator-5554 RUNNING
```

### Step 4: Start Relay Server

**Windows**:
```powershell
$relayScript = "testing/e2e/scripts/start-relay-server.ps1"

& $relayScript -RelayId "relay-session-abc123" `
               -ListeningPort 15000 `
               -SourceEmulatorId "emulator-5554" `
               -DestEmulatorId "emulator-5556" `
               -SourcePort 15001 `
               -DestPort 15003 `
               -HealthCheckIntervalMs 5000
```

**Expected Output**:
```
[14:32:00] Starting relay server on port 15000...
[14:32:00] Configuring route: emulator-5554:15001 → emulator-5556:15003
[14:32:00] Configuring route: emulator-5556:15003 → emulator-5554:15001
[14:32:00] Health monitoring started (interval: 5000ms)
✅ Relay server running (PID: 12345)
```

### Step 5: Run Playwright Tests

```bash
cd testing/e2e
npm test                          # Run all e2e tests
# OR
npm run test -- --grep "dual-emulator"  # Run only dual-emulator tests
```

**Expected Output**:
```
Running 5 test(s)...

  ✅ Dual Emulator Provisioning
  ✅ Relay Connectivity Check
  ✅ NDI Streaming Between Devices
  ✅ Latency Measurement (Feature 009)
  ✅ Artifact Collection

5 passed (280ms)
```

### Step 6: Collect Artifacts

**Windows**:
```powershell
$collectScript = "testing/e2e/scripts/collect-test-artifacts.ps1"

& $collectScript -SessionId "dual-emulator-e2e-2026-03-23-143000" `
                 -EmulatorIds "emulator-5554", "emulator-5556" `
                 -RelayId "relay-session-abc123" `
                 -StoragePath "testing/e2e/artifacts/session-abc123/" `
                 -GenerateManifest $true
```

**Expected Output**:
```
[14:48:00] Starting artifact collection...
[14:48:00] Collecting logcat from emulator-5554...
[14:48:05] Collected logcat-emulator-5554.log (2.1 MB)
[14:48:05] Collecting logcat from emulator-5556...
[14:48:10] Collected logcat-emulator-5556.log (1.8 MB)
[14:48:10] Collecting relay metrics...
[14:48:11] Generating manifest...
[14:48:11] Generated manifest.json
✅ Artifact collection complete (6 files, 542 MB total)
```

### Step 7: Inspect Artifacts

```bash
# List collected artifacts
ls -la testing/e2e/artifacts/session-abc123/

# View manifest
cat testing/e2e/artifacts/session-abc123/manifest.json

# Stream logcat
tail -f testing/e2e/artifacts/session-abc123/logcat-emulator-5554.log
```

### Step 8: Cleanup

**Windows**:
```powershell
# Stop relay server
Stop-Process -Id 12345

# Stop emulators
adb -s emulator-5554 emu kill
adb -s emulator-5556 emu kill
```

---

## CI/CD Pipeline Integration

### GitHub Actions Workflow

**File**: `.github/workflows/e2e-dual-emulator.yml` (example)

```yaml
name: Dual Emulator E2E Tests

on:
  push:
    branches: [ main, develop ]
    paths:
      - 'ndi/sdk-bridge/**'
      - 'feature/ndi-browser/**'
      - 'testing/e2e/**'

jobs:
  dual-emulator-e2e:
    runs-on: windows-latest  # Windows runner with emulator support
    timeout-minutes: 30

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup Android SDK
        uses: android-actions/setup-android@v3
        with:
          api-levels: 34
          ndk-version: 25.1.8937393

      - name: Install NDI SDK system image
        run: |
          sdkmanager "system-images;android-34;google_apis;x86_64"

      - name: Build NDI SDK Bridge APK
        run: ./gradlew :ndi:sdk-bridge:assembleRelease

      - name: Provision dual emulators
        run: |
          $script = "testing/e2e/scripts/provision-dual-emulator.ps1"
          & $script -SourceEmulatorId "emulator-5554" `
                    -ReceiverEmulatorId "emulator-5556" `
                    -ApiLevel 34 `
                    -NdiSdkApkPath "ndi/sdk-bridge/build/outputs/apk/release/ndi-sdk-bridge-release.apk" `
                    -BootTimeoutSeconds 90 `
                    -CiMode $true  # CI-specific verbosity + timeouts
        timeout-minutes: 5

      - name: Start relay server
        run: |
          $script = "testing/e2e/scripts/start-relay-server.ps1"
          & $script -RelayId "relay-session-${{ github.run_id }}" `
                    -ListeningPort 15000
        timeout-minutes: 1
        continue-on-error: false

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '18'

      - name: Install Playwright dependencies
        working-directory: testing/e2e
        run: |
          npm ci
          npx playwright install --with-deps

      - name: Run e2e tests
        working-directory: testing/e2e
        run: npm test
        timeout-minutes: 10

      - name: Collect artifacts
        if: always()
        run: |
          $script = "testing/e2e/scripts/collect-test-artifacts.ps1"
          & $script -SessionId "ci-run-${{ github.run_id }}" `
                    -EmulatorIds "emulator-5554", "emulator-5556" `
                    -StoragePath "testing/e2e/artifacts/ci-run-${{ github.run_id }}/" `
                    -GenerateManifest $true
        timeout-minutes: 2

      - name: Upload artifacts
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: e2e-artifacts-${{ github.run_id }}
          path: testing/e2e/artifacts/
          retention-days: 7

      - name: Report test results
        if: always()
        run: |
          $manifest = "testing/e2e/artifacts/ci-run-${{ github.run_id }}/manifest.json"
          $json = Get-Content $manifest | ConvertFrom-Json
          Write-Output "Artifacts collected: $($json.summary.totalArtifacts)"
          Write-Output "Total size: $($json.summary.totalSizeBytes / 1MB) MB"
```

### GitHub Actions Environment Variables

```bash
ANDROID_SDK_ROOT=C:/Android/sdk                    # Windows
ANDROID_SDK_ROOT=/opt/hostedtoolcache/Android/sdk # Linux

ADB_INSTALL_TIMEOUT=30
```

---

## Troubleshooting

### Provisioning Fails: Emulator Port Already In Use

**Error**: `adb: error: could not connect to TCP port 5554 (connection refused)`

**Solution**:
```powershell
# List active emulators
adb devices

# Kill stale ADB daemon
adb kill-server
adb start-server

# Try alternate port
$script = "testing/e2e/scripts/provision-dual-emulator.ps1"
& $script -SourceEmulatorId "emulator-5558" -ApiLevel 34  # Use 5558 + 5560
```

### Relay Server Latency > 100 ms (SC-002 violation)

**Error**: Health check reports `avgLatencyMs: 150`

**Solution**:
1. Check host CPU usage (should be < 80%):
   ```powershell
   Get-Counter "\Processor(_Total)\% Processor Time" | Select-Object -ExpandProperty CounterSamples
   ```

2. Check emulator RAM allocation (should have 512+ MB each):
   ```bash
   adb shell cat /proc/meminfo | grep MemTotal
   ```

3. Restart relay server:
   ```powershell
   Stop-Process -Id 12345
   Start-Sleep 2
   & $relayScript -RelayId "relay-session-retry" ...
   ```

### Screen Recording Fails: Video Not Supported

**Error**: `Device does not support screen recording`

**Solution**:
- Screen recording requires API 21+
- Verify emulator has hardware acceleration enabled:
  ```bash
  emulator -list-avds -c                           # List with hardware info
  emulator @emulator-5554 -accel on                # Start with acceleration
  ```

### Artifact Collection Timeout: Insufficient Disk Space

**Error**: `Disk space insufficient (required: 600 MB, available: 100 MB)`

**Solution**:
```powershell
# Clean old artifacts
Remove-Item testing/e2e/artifacts/* -Recurse -Older than 7 days

# Reduce logcat line limit
& $collectScript -LogcatLineLimit 250 ...  # Default: 500
```

---

## Performance Tuning

### Emulator Boot Time (SC-001)

**Target**: < 2 min first-time, < 10 sec reuse

**Current defaults**:
- Boot timeout: 90 seconds
- Parallel provisioning: enabled (both emulators boot concurrently)

**Optimization**:
```powershell
# Use snapshot for faster reuse
$script = "testing/e2e/scripts/provision-dual-emulator.ps1"
& $script -UseSnapshot $true -SkipBootIfAlreadyRunning $true
```

### Relay Latency Tuning (SC-002)

**Target**: < 100 ms round-trip

**Defaults**:
- TCP_NODELAY: enabled (disable Nagle algorithm)
- Buffer size: 64 KB
- Health check interval: 5000 ms

**Optimization** (if latency creeps above 80 ms):
```powershell
& $relayScript -TcpNoDelay $true `
               -SocketBufferSizeKb 128 `
               -HealthCheckIntervalMs 1000
```

### Concurrent Test Runs (SC-003)

**Target**: 95% pass rate without manual retry

**Strategy**:
- Each test suite gets isolated session ID
- Artifacts stored per-session (no cross-contamination)
- Relay server is singleton (1 per host), routes are multiplexed

**Scaling**:
- Maximum 1 relay server per host
- Maximum 4 emulators per relay (MVP: 2 emulators)

---

## Development Workflow

### Add New Test Case

**File**: `testing/e2e/tests/my-new-test.spec.ts`

```typescript
import { test, expect } from '@playwright/test';
import { ProvisioningSupport } from '../support/provisioning.spec';
import { RelaySupport } from '../support/relay.spec';

test.describe('My Feature Tests', () => {
  test.beforeAll(async () => {
    // Provisioning handled by fixtures (automatic)
    // Relay started by fixtures (automatic)
  });

  test('should stream NDI over relay', async () => {
    // Your test here
    // Artifacts auto-collected post-test
  });

  test.afterAll(async () => {
    // Cleanup handled by fixtures (automatic)
  });
});
```

### Extend Artifact Collection

**Add new logcat filter** (in `collect-test-artifacts.ps1`):

```powershell
# Example: Filter for NDI-specific logs
adb -s $emulatorId logcat | grep -i "ndi|streaming" > "$logcatPath.ndi-only"
```

**Add new metric** (in `relay-health-monitor.ps1`):

```powershell
# Example: Track packet loss over time
$stats = Get-RelayMetrics -RelayId $relayId
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
"$timestamp | Loss: $($stats.packetLossPercent)%" | Add-Content "relay-loss-trend.csv"
```

---

## Next Steps

1. **Verify Infrastructure**: Run `./gradlew :testing:e2e:test --include-tags="sanity"`
2. **Scale Provisioning**: Tune emulator RAM/cores per your host specs
3. **Monitor Latency**: Check relay health logs in `testing/e2e/artifacts/*/relay-metrics.json`
4. **Integrate Feature 009**: Run latency-measurement tests once infrastructure is stable
5. **CI/CD Gating**: Add e2e test results to PR checks (.github/workflows/)

---

## Support & Documentation

- **Infrastructure Guide**: [docs/dual-emulator-setup.md](../docs/dual-emulator-setup.md)
- **Data Model**: [specs/010-dual-emulator-setup/data-model.md](data-model.md)
- **API Contracts**: [specs/010-dual-emulator-setup/contracts/](contracts/)
- **CI/CD Template**: [.github/workflows/e2e-dual-emulator.yml](.github/workflows/e2e-dual-emulator.yml)

---

**Status**: ✅ **COMPLETE**
