# Quickstart: E2E App Pre-Installation Gate

**Branch**: `011-e2e-app-preinstall` | **Date**: 2026-03-23

---

## What This Feature Does

Before any e2e test executes, this feature guarantees that the **latest debug APK** is freshly installed on every registered emulator and launch-verified. A structured Pre-Flight Report is written after each run.

---

## Prerequisites

- Android emulators booted and ADB-reachable (Feature 010 handles this)
- Android SDK on `PATH` (includes `adb`, `aapt`/`aapt2`)
- Node 20 + npm (for Playwright)
- PowerShell 7 (`pwsh`)
- Repo cloned at repo root

---

## Local Developer Workflow

### Step 1 — Build the debug APK

```powershell
# From repo root
./gradlew.bat :app:assembleDebug
# Output: app/build/outputs/apk/debug/app-debug.apk
```

### Step 2 — Boot emulators (Feature 010)

```powershell
./testing/e2e/scripts/provision-dual-emulator.ps1 -Action provision-dual -InstallNdiSdk -SkipBootIfAlreadyRunning
```

### Step 3 — Run the pre-installation step manually (optional, for verification)

```powershell
./testing/e2e/scripts/install-app-preinstall.ps1
# Outputs:
#   PRE-FLIGHT PASS: All 2 devices verified (versionName=0.1.0 versionCode=1)
# Report: testing/e2e/artifacts/runtime/preinstall-report.json
```

### Step 4 — Run e2e tests

```powershell
cd testing/e2e
npm ci
# The pre-install runs automatically via global-setup-dual-emulator.ts when:
#   DUAL_EMULATOR_AUTOMATION != "0" (the default)
npx playwright test --project=android-primary
```

The global setup calls `install-app-preinstall.ps1` automatically. If you ran Step 3 manually, the global setup re-runs the install (idempotent — no harm, same result).

---

## Run Only the Pre-Flight Validation Spec

```powershell
cd testing/e2e
npx playwright test tests/support/app-preinstall.spec.ts --project=android-primary
```

This spec reads the Pre-Flight Report and asserts all devices passed with the correct version.

---

## Skip Pre-Installation (Development / Debugging Only)

```powershell
# Disables the entire DUAL_EMULATOR_AUTOMATION hook, including pre-install
$env:DUAL_EMULATOR_AUTOMATION = "0"
npx playwright test ...
```

> **Warning**: Disabling automation means no install guarantee. Tests may run against a stale or missing app.

---

## Override APK Path (Non-Default Variant)

```powershell
# Via environment variable (applies to both script and global-setup)
$env:APP_APK_PATH = "app/build/outputs/apk/release/app-release.apk"
./testing/e2e/scripts/install-app-preinstall.ps1

# Or via explicit parameter
./testing/e2e/scripts/install-app-preinstall.ps1 -ApkPath "app/build/outputs/apk/release/app-release.apk"
```

---

## CI / CD Workflow

The pre-installation gate is integrated into `.github/workflows/e2e-dual-emulator.yml` as explicit steps (in order):

```
1. Checkout
2. Setup Java 21
3. Setup Node 20
4. Setup Android SDK
5. Verify prereqs
6. Build NDI bridge release APK      (existing)
7. Build app debug APK               ← NEW (assembleDebug)
8. Validate emulator images (32-35)
9. Provision dual emulators
10. Install app on emulators          ← NEW (install-app-preinstall.ps1)
11. Start relay server
12. Install test dependencies (npm ci)
13. Run support validation specs
14. Run app pre-flight spec           ← NEW (app-preinstall.spec.ts)
15. Run latency consumer suite (feature 009)
16. Collect artifacts
17. Stop relay and reset
18. Upload artifacts
```

Steps 7 and 10 are the additions from this feature. Steps cannot be bypassed — there is no `if:` condition on either step per FR-009.

---

## Reading the Pre-Flight Report

```powershell
Get-Content testing/e2e/artifacts/runtime/preinstall-report.json | ConvertFrom-Json | Format-List
```

Or in Node.js / Playwright:

```typescript
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const reportPath = resolve(__dirname, "../../../../testing/e2e/artifacts/runtime/preinstall-report.json");
const report = JSON.parse(readFileSync(reportPath, "utf-8"));

console.log(`Overall: ${report.overallStatus}`);
for (const device of report.devices) {
  console.log(`  ${device.serial}: ${device.status} (${device.installedVersionName} / ${device.installedVersionCode})`);
}
```

---

## Troubleshooting

| Symptom | Likely Cause | Resolution |
|---------|-------------|------------|
| `APK artifact not found` | `assembleDebug` not run | Run `./gradlew.bat :app:assembleDebug` first |
| `TIMEOUT` on device | Emulator storage full or ADB hanging | Check emulator disk space; run `adb -s <serial> shell df /data`; reboot emulator |
| `UNREACHABLE` on device | Emulator not started or ADB not connected | Run Feature 010 provisioning script; verify with `adb devices` |
| `LAUNCH_FAILED` | Corrupt install or missing activity | Wipe emulator app data (`adb -s <serial> shell pm clear com.ndi.app.debug`) and re-run |
| `VERSION_MISMATCH` | Installing wrong APK variant | Check `$ApkPath` parameter or `APP_APK_PATH` env var points to the correct APK |
| `PRE-FLIGHT FAIL` in CI | Any above | Check `testing/e2e/artifacts/runtime/preinstall-report.json` in the uploaded CI artifact; the `failureReason` field provides a 5-minute-resolution-target message (SC-004) |

---

## Key Files

| File | Role |
|------|------|
| `testing/e2e/scripts/install-app-preinstall.ps1` | Pre-installation orchestrator script |
| `testing/e2e/scripts/helpers/emulator-adb.ps1` | ADB helper (extended with version + launch verification) |
| `testing/e2e/tests/support/app-preinstall.ts` | TypeScript fixture for reading/asserting the report |
| `testing/e2e/tests/support/app-preinstall.spec.ts` | Playwright pre-flight validation spec (`@preinstall`) |
| `testing/e2e/tests/support/global-setup-dual-emulator.ts` | Calls pre-install script as part of global setup |
| `testing/e2e/artifacts/runtime/preinstall-report.json` | Pre-Flight Report output (created at runtime) |
| `.github/workflows/e2e-dual-emulator.yml` | CI workflow (extended with build + install steps) |
| `specs/011-e2e-app-preinstall/contracts/pre-flight-report.contract.md` | JSON schema contract for the report |
| `specs/011-e2e-app-preinstall/contracts/install-script.contract.md` | Script interface contract |
