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
| `ApkPath` | `string` | `app/build/outputs/apk/debug/app-debug.apk` | Path to the APK artifact to install. Can also be set via `APP_APK_PATH`. |
| `PackageName` | `string` | `com.ndi.app.debug` | Android application ID of the app being installed. Must match the built APK. |
| `Serials` | `string[]` | `@("emulator-5554", "emulator-5556")` | ADB device serials to target. Can also be sourced from `EMULATOR_A_SERIAL` and `EMULATOR_B_SERIAL` when `-Serials` is omitted. |
| `TimeoutSeconds` | `int` | `60` | Per-device readiness-plus-install-plus-verify budget in seconds. If the device fails before the deadline or exceeds it, the device record becomes `NOT_READY` or `TIMEOUT` and overall status is `FAIL`. |
| `OutputPath` | `string` | `testing/e2e/artifacts/runtime/preinstall-report.json` | Destination for the JSON Pre-Flight Report. The file is replaced on each run. |

---

## Environment Variable Overrides

| Variable | Overrides Parameter | Notes |
|----------|---------------------|-------|
| `APP_APK_PATH` | `ApkPath` | Explicit `-ApkPath` takes precedence |
| `EMULATOR_A_SERIAL` | First default serial | Applies only when `-Serials` is omitted |
| `EMULATOR_B_SERIAL` | Second default serial | Applies only when `-Serials` is omitted |

---

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Pre-flight PASS. All devices became ready, installed the APK, matched the expected version, and passed launch verification. |
| `1` | Pre-flight FAIL. Artifact missing, device not ready, install failed, version mismatched, or launch verification failed. |

The script MUST always write the report before exiting.

---

## Stdout / Stderr Protocol

- `stdout`: Human-readable progress log including readiness wait, install start, version confirmation, launch verification, and summary.
- `stderr`: Reserved for unexpected script-level failures, not expected device-level failures.
- Final line on `stdout` is one of:
  - `PRE-FLIGHT PASS: N/N devices verified; expected=<versionIdentifier>; devices=<serial:versionIdentifier,...>`
  - `PRE-FLIGHT FAIL: <consolidated reason>; expected=<versionIdentifier|missing>`

CI consumers may surface the final line in step summaries.

---

## Idempotency Guarantee

Running the script multiple times with the same valid APK MUST continue to produce exit code `0` when the requested devices are reachable and ready. The script always uses replacement install semantics (`adb install -r`).

---

## Dependencies

The script sources `testing/e2e/scripts/helpers/emulator-adb.ps1` and requires these helper capabilities:

- `Wait-ForEmulatorReady -Serial <string> -TimeoutSeconds <int>` -> `@{ ready: bool; readinessWaitMs: int }`
- `Install-ApkToEmulator -Serial <string> -ApkPath <string>`
- `Get-InstalledAppVersion -Serial <string> -PackageName <string>` -> `@{ versionName: string; versionCode: int }`
- `Test-AppLaunchable -Serial <string> -PackageName <string> -ActivityName <string>` -> `boolean`

---

## Happy-Path Sequence

```text
1. Resolve ApkPath to an absolute path
2. Validate the APK exists; abort-before-install if not
3. Extract versionName and versionCode from the APK
4. For each serial in Serials:
   a. Start a per-device deadline stopwatch
   b. Wait for emulator readiness within the deadline
   c. If readiness is not reached -> record NOT_READY
   d. Install the APK within the remaining deadline budget
   e. If install exceeds remaining budget -> record TIMEOUT
   f. Confirm installed version from device and compare to expected
   g. Launch verify with `am start -W`; success requires zero exit and `Status: ok`
   h. Force-stop app after successful launch verification
   i. Record EmulatorInstallRecord
5. Compute overallStatus and failureReason
6. Write PreFlightReport to OutputPath
7. Exit 0 on PASS or 1 on FAIL
```

---

## Caller Contract (CI Step)

```yaml
# Required ordering inside .github/workflows/e2e-dual-emulator.yml:
# 1. Setup Java, Node, Android SDK
# 2. Build app debug APK
# 3. Provision dual emulators
# 4. Install app on emulators     <- this script
# 5. Run support validation spec
# 6. Run existing Playwright regression suites
```

The CI step MUST use `shell: pwsh` and run from repo root so relative paths resolve correctly.

---

## Caller Contract (global-setup-dual-emulator.ts)

```typescript
// After provisioning and before returning:
// 1. Reuse an existing fresh matching preinstall-report.json when present.
// 2. Otherwise invoke the PowerShell pre-install script.
runPowerShellScript("../../scripts/install-app-preinstall.ps1");
```

The TypeScript caller uses the default `OutputPath`. ADB serials come from `EMULATOR_A_SERIAL` and `EMULATOR_B_SERIAL` when available, otherwise the emulator defaults are used.
