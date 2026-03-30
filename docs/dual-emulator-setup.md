# Dual Emulator Setup

This guide describes the dual-emulator infrastructure used by end-to-end tests in testing/e2e.

## Architecture

1. Provisioning script boots and validates two emulators.
2. Relay script starts a local TCP relay process used for connectivity checks.
3. Reset script clears app state between suites.
4. Artifact script captures logcat, diagnostics, and manifest reports.

## Local Setup

1. Verify prerequisites:

```powershell
./scripts/verify-e2e-dual-emulator-prereqs.ps1
```

2. Build bridge APK:

```powershell
./gradlew.bat buildNdiSdkBridgeRelease
```

3. Validate emulator images:

```powershell
./testing/e2e/scripts/helpers/validate-emulator-images.ps1
```

4. Provision emulators:

```powershell
./testing/e2e/scripts/provision-dual-emulator.ps1 -Action provision-dual -InstallNdiSdk -SkipBootIfAlreadyRunning
```

5. Start relay:

```powershell
./testing/e2e/scripts/start-relay-server.ps1 -Action start
```

6. Run support flow checks:

```powershell
cd testing/e2e
npx playwright test tests/support/e2e-infrastructure.spec.ts tests/support/dual-emulator-provisioning.spec.ts tests/support/relay-connectivity.spec.ts --project=android-primary
```

7. Collect artifacts and cleanup:

```powershell
./testing/e2e/scripts/collect-test-artifacts.ps1 -SessionId local-$(Get-Date -Format yyyyMMdd-HHmmss)
./testing/e2e/scripts/start-relay-server.ps1 -Action stop
./testing/e2e/scripts/reset-emulator-state.ps1
```

## CI Integration

Workflow file: .github/workflows/e2e-dual-emulator.yml.

Pipeline stages:

1. Build NDI bridge release APK.
2. Validate emulator prerequisites and API images.
3. Provision dual emulators.
4. Start relay and run support checks.
5. Run feature 009 latency consumer spec.
6. Collect and upload artifacts.

## Troubleshooting

### Emulator not detected by ADB

1. Run `adb devices`.
2. If offline, restart server with `adb kill-server; adb start-server`.
3. Re-run provisioning with `-SkipBootIfAlreadyRunning`.

### Relay health is unhealthy

1. Check `testing/e2e/artifacts/runtime/relay-result.json`.
2. Stop and restart relay:

```powershell
./testing/e2e/scripts/start-relay-server.ps1 -Action stop
./testing/e2e/scripts/start-relay-server.ps1 -Action start
```

3. Re-run health:

```powershell
./testing/e2e/scripts/start-relay-server.ps1 -Action health
```

### Missing NDI bridge artifact

1. Build with `./gradlew.bat :ndi:sdk-bridge:assembleRelease`.
2. For modern library output, confirm at `ndi/sdk-bridge/build/outputs/aar/sdk-bridge-release.aar`.
3. If APK is required by legacy workflows, add an app wrapper or use the prebuilt `ndi-sdk-bridge-release.apk` artifact path for emulator install.

### Artifact collection failed

1. Ensure `testing/e2e/artifacts` is writable.
2. Verify emulator serial IDs are correct.
3. Re-run collection with a fresh session ID.
