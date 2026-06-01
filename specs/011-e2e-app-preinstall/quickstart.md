# Quickstart: E2E App Pre-Installation Gate

**Branch**: `011-e2e-app-preinstall` | **Date**: 2026-03-23

---

## What This Feature Does

Before any e2e test executes, this feature guarantees that the latest debug APK is freshly installed on every registered emulator. Each device gets up to 60 seconds to become ready, install the APK, confirm the expected version, and pass launch verification. A structured Pre-Flight Report is written after each run.

---

## Feature Documentation Index

- `specs/011-e2e-app-preinstall/plan.md`
- `specs/011-e2e-app-preinstall/data-model.md`
- `specs/011-e2e-app-preinstall/contracts/install-script.contract.md`
- `specs/011-e2e-app-preinstall/contracts/pre-flight-report.contract.md`

---

## Prerequisites

- Android emulators provisioned by Feature 010
- Android SDK on `PATH`, including `adb` and `aapt` or `aapt2`
- Node 20 and npm
- PowerShell 7 or Windows PowerShell 5.1
- Repo opened at the workspace root

---

## Local Developer Workflow

### Step 1 - Build the debug APK

```powershell
./gradlew.bat :app:assembleDebug
```

Expected output path:

```text
app/build/outputs/apk/debug/app-debug.apk
```

### Step 2 - Provision emulators

```powershell
./testing/e2e/scripts/provision-dual-emulator.ps1 -Action provision-dual -InstallNdiSdk -SkipBootIfAlreadyRunning
```

### Step 3 - Run the pre-install gate manually (optional verification)

```powershell
./testing/e2e/scripts/install-app-preinstall.ps1
```

Expected summary format:

```text
PRE-FLIGHT PASS: 2/2 devices verified; expected=0.1.0+1; devices=emulator-5554:0.1.0+1,emulator-5556:0.1.0+1
```

Report location:

```text
testing/e2e/artifacts/runtime/preinstall-report.json
```

### Step 4 - Run Playwright

```powershell
Set-Location testing/e2e
npm ci
npx playwright test --project=android-primary
```

`global-setup-dual-emulator.ts` enforces pre-install automatically. If Step 3 already produced a fresh matching report for the current APK and serials, global setup reuses it instead of reinstalling.

---

## Run Only the Pre-Flight Validation Spec

```powershell
Set-Location testing/e2e
npx playwright test tests/support/app-preinstall.spec.ts --project=android-primary
```

This support spec reads the Pre-Flight Report and validates the expected device states and version identifiers.

---

## Override APK Path

```powershell
$env:APP_APK_PATH = "app/build/outputs/apk/release/app-release.apk"
./testing/e2e/scripts/install-app-preinstall.ps1
```

Or explicitly:

```powershell
./testing/e2e/scripts/install-app-preinstall.ps1 -ApkPath "app/build/outputs/apk/release/app-release.apk"
```

---

## CI Workflow Order

The feature extends `.github/workflows/e2e-dual-emulator.yml` with this effective order:

```text
1. Checkout
2. Setup Java 21
3. Setup Node 20
4. Setup Android SDK
5. Verify prereqs
6. Build NDI bridge release APK
7. Build app debug APK                 <- new
8. Validate emulator images
9. Provision dual emulators
10. Install app on emulators           <- new
11. Start relay server
12. Install test dependencies
13. Run support validation specs
14. Run app pre-flight support spec    <- new
15. Run existing Playwright regression suites
16. Collect artifacts
17. Stop relay and reset
18. Upload artifacts
```

The build and pre-install steps are required gates and must not be bypassed.

---

## Read the Pre-Flight Report

```powershell
Get-Content testing/e2e/artifacts/runtime/preinstall-report.json | ConvertFrom-Json | Format-List
```

Or from Node:

```typescript
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const reportPath = resolve(process.cwd(), "artifacts/runtime/preinstall-report.json");
const report = JSON.parse(readFileSync(reportPath, "utf-8"));

console.log(report.overallStatus, report.buildArtifact.versionIdentifier);
for (const device of report.devices) {
  console.log(device.serial, device.status, device.installedVersionIdentifier);
}
```

---

## Troubleshooting

| Symptom | Likely Cause | Resolution |
|---------|--------------|------------|
| `APK artifact not found` | `assembleDebug` was not run | Run `./gradlew.bat :app:assembleDebug` first |
| `UNREACHABLE` | Emulator is not connected to ADB | Re-run provisioning and verify `adb devices` |
| `NOT_READY` | Emulator boot did not complete within the 60-second device budget | Check `adb -s <serial> shell getprop sys.boot_completed`; reprovision if needed |
| `TIMEOUT` | Install or launch verification exceeded the remaining budget | Check emulator responsiveness, storage, and ADB health |
| `VERSION_MISMATCH` | Wrong APK variant or stale artifact path | Verify `APP_APK_PATH` or `-ApkPath` points at the intended APK |
| `LAUNCH_FAILED` | Install succeeded but the app could not launch cleanly | Check `am start -W` output and clear app state before retrying |

---

## Key Files

| File | Role |
|------|------|
| `testing/e2e/scripts/install-app-preinstall.ps1` | Pre-install orchestrator |
| `testing/e2e/scripts/helpers/emulator-adb.ps1` | Readiness, install, version, and launch helpers |
| `testing/e2e/tests/support/app-preinstall.spec.ts` | Failing-first Playwright support spec |
| `testing/e2e/tests/support/global-setup-dual-emulator.ts` | Local Playwright enforcement hook |
| `testing/e2e/artifacts/runtime/preinstall-report.json` | Runtime pre-flight report |
| `.github/workflows/e2e-dual-emulator.yml` | CI gate ordering |
| `specs/011-e2e-app-preinstall/contracts/pre-flight-report.contract.md` | JSON report contract |
| `specs/011-e2e-app-preinstall/contracts/install-script.contract.md` | Script contract |
