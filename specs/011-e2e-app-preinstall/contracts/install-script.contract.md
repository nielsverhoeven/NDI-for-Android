# Contract: install-app-preinstall.ps1 Script Interface

**Branch**: `011-e2e-app-preinstall` | **Date**: 2026-03-23  
**Contract Type**: PowerShell script interface  
**Script**: `testing/e2e/scripts/install-app-preinstall.ps1`  
**Callers**: `.github/workflows/e2e-dual-emulator.yml`, `testing/e2e/tests/support/global-setup-dual-emulator.ts`

---

## Synopsis

```powershell
./testing/e2e/scripts/install-app-preinstall.ps1 `
  [-ApkPath <string>] `
  [-PackageName <string>] `
  [-Serials <string[]>] `
  [-TimeoutSeconds <int>] `
  [-OutputPath <string>]
```

---

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ApkPath` | `string` | `app/build/outputs/apk/debug/app-debug.apk` (relative to repo root) | Path to the APK artifact to install. Can also be set via env var `APP_APK_PATH`. |
| `PackageName` | `string` | `com.ndi.app.debug` | Android application ID of the app being installed. Must match the built APK. |
| `Serials` | `string[]` | `@("emulator-5554", "emulator-5556")` | ADB device serials to install the app on. Can also be set via env vars `EMULATOR_A_SERIAL` and `EMULATOR_B_SERIAL`. |
| `TimeoutSeconds` | `int` | `60` | Per-device install+verify timeout in seconds (FR-008). If any device exceeds this, its record gets `status = "TIMEOUT"` and overall status is `FAIL`. |
| `OutputPath` | `string` | `testing/e2e/artifacts/runtime/preinstall-report.json` | Filesystem path for the Pre-Flight Report JSON. Created if not present; replaced on each run. |

---

## Environment Variable Overrides

| Variable | Overrides Parameter | Notes |
|----------|--------------------|-|
| `APP_APK_PATH` | `ApkPath` | Explicit `-ApkPath` takes precedence over env var |
| `EMULATOR_A_SERIAL` | First entry in default `Serials` | Only applies when `-Serials` is not specified |
| `EMULATOR_B_SERIAL` | Second entry in default `Serials` | Only applies when `-Serials` is not specified |

---

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Pre-flight PASS — all devices installed and launch-verified; report written to `OutputPath` |
| `1` | Pre-flight FAIL — one or more devices failed, or artifact missing; report written with failure details |

The script ALWAYS writes the report before exiting, regardless of outcome.

---

## Stdout / Stderr Protocol

- `stdout`: Human-readable progress log (per-device install start, version info, launch verification result, summary line).
- `stderr`: Reserved for unexpected script-level errors (not device-level errors; device errors are in the report).
- Final line on `stdout`: One of:
  - `PRE-FLIGHT PASS: All N devices verified (versionName=X versionCode=Y)`
  - `PRE-FLIGHT FAIL: <consolidated reason>`

CI consumers (GitHub Actions) can use these final lines for step summary annotations.

---

## Idempotency Guarantee

Running the script multiple times with the same APK artifact MUST produce exit code `0` each time, provided emulators are reachable and the APK is valid (FR-007). The `-r` flag is always passed to `adb install`, ensuring forced replacement.

---

## Dependencies

The script sources the following helper:
- `testing/e2e/scripts/helpers/emulator-adb.ps1` — provides `Invoke-Adb`, `Install-ApkToEmulator`, `Get-EmulatorStateSnapshot`

Additions to `emulator-adb.ps1` required by this feature:
- `Get-InstalledAppVersion -Serial <string> -PackageName <string>` → `@{ versionName: string; versionCode: int }`
- `Test-AppLaunchable -Serial <string> -PackageName <string> -ActivityName <string>` → `boolean`

---

## Sequence (Happy Path)

```
1. Resolve ApkPath → abs path
2. Validate APK exists → abort with FR-003 if not
3. Extract versionName + versionCode from APK via aapt
4. For each serial in Serials:
   a. Start-Job: { Install-ApkToEmulator -Serial $s -ApkPath $p }
   b. Wait-Job -Timeout $TimeoutSeconds
   c. If job timed out → record TIMEOUT, Remove-Job -Force
   d. Get-InstalledAppVersion -Serial $s → compare to expected
   e. Test-AppLaunchable -Serial $s → launchVerified
   f. Record EmulatorInstallRecord
5. Compute overallStatus, failureReason
6. Write PreFlightReport to OutputPath
7. Exit 0 (PASS) or 1 (FAIL)
```

---

## Caller Contract (CI Step)

```yaml
# Required step ordering in .github/workflows/e2e-dual-emulator.yml:
# 1. Setup Java, Node, Android SDK
# 2. Build app debug APK         ← MUST precede this step
# 3. Provision dual emulators    ← MUST precede this step
# 4. Install app on emulators    ← THIS STEP (calls install-app-preinstall.ps1)
# 5. Run test specs              ← MUST follow this step
```

The CI step MUST use `shell: pwsh` and be invoked from the repo root so that relative paths resolve correctly.

---

## Caller Contract (global-setup-dual-emulator.ts)

```typescript
// After provisioning and before returning:
runPowerShellScript("../../scripts/install-app-preinstall.ps1");
// The script exits non-zero on FAIL → execFileSync throws → global setup fails → test run aborts
```

The TypeScript caller does not pass `-OutputPath`; the default path is used. ADB serials default to env vars `EMULATOR_A_SERIAL` / `EMULATOR_B_SERIAL` when set, otherwise use the `emulator-5554` / `emulator-5556` defaults.
