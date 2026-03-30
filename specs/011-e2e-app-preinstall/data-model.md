# Data Model: E2E App Pre-Installation Gate

**Branch**: `011-e2e-app-preinstall` | **Date**: 2026-03-23  
**Spec**: specs/011-e2e-app-preinstall/spec.md

---

## Overview

This feature operates entirely within the test infrastructure layer. No Android application source code or database schema is modified. All entities below are runtime data structures used by the pre-installation script and serialized to a JSON report consumed by the Playwright harness and CI artifacts.

---

## Entities

### 1. `AppBuildArtifact`

Represents the compiled, installable APK produced by `./gradlew :app:assembleDebug`.

| Field | Type | Description | Required |
|-------|------|-------------|----------|
| `path` | `string` | Absolute filesystem path to the APK file | Yes |
| `variant` | `string` | Gradle build variant; default `"debug"` | Yes |
| `packageName` | `string` | Android application ID; default `"com.ndi.app.debug"` | Yes |
| `versionName` | `string \| null` | Human-readable version string extracted from APK metadata; `null` if artifact is missing | Yes |
| `versionCode` | `integer \| null` | Monotonically increasing build number extracted from APK metadata; `null` if artifact is missing | Yes |
| `versionIdentifier` | `string \| null` | Combined identifier `${versionName}+${versionCode}` used by reports and summaries; `null` if artifact is missing | Yes |
| `buildTimestamp` | `string (ISO 8601) \| null` | File modification timestamp of the APK at pre-flight time | Yes |
| `exists` | `boolean` | Whether the artifact file was found at `path` at pre-flight start | Yes |

**Validation rules**:
- `exists` MUST be `true` before any installation attempt; if `false`, abort with FR-003.
- If `exists` is `true`, then `versionName`, `versionCode`, `versionIdentifier`, and `buildTimestamp` MUST all be populated.
- `path` defaults to `app/build/outputs/apk/debug/app-debug.apk` unless overridden via `-ApkPath` or `APP_APK_PATH`.

---

### 2. `EmulatorInstallRecord`

Represents the per-device pre-installation outcome for a single emulator. One record exists per requested emulator.

| Field | Type | Description | Required |
|-------|------|-------------|----------|
| `serial` | `string` | ADB device serial, for example `"emulator-5554"` | Yes |
| `reachable` | `boolean` | Whether ADB can reach the device | Yes |
| `ready` | `boolean` | Whether the emulator reached install-ready state within the device deadline | Yes |
| `readinessWaitMs` | `integer` | Time spent waiting for readiness before installation begins; included in `elapsedMs` | Yes |
| `apkInstalled` | `boolean` | Whether `adb install -r` completed successfully within the remaining device budget | Yes |
| `installedVersionName` | `string \| null` | Version name confirmed from the device after install | Yes |
| `installedVersionCode` | `integer \| null` | Version code confirmed from the device after install | Yes |
| `installedVersionIdentifier` | `string \| null` | Combined installed identifier `${installedVersionName}+${installedVersionCode}` | Yes |
| `launchVerified` | `boolean` | Whether `am start -W` succeeded and returned `Status: ok` | Yes |
| `elapsedMs` | `integer` | Wall-clock milliseconds from readiness polling start to final disposition | Yes |
| `status` | `"PASS" \| "NOT_READY" \| "INSTALL_FAILED" \| "VERSION_MISMATCH" \| "LAUNCH_FAILED" \| "TIMEOUT" \| "UNREACHABLE"` | Final disposition of this device's pre-flight step | Yes |
| `errorMessage` | `string \| null` | Human-readable error details when `status != "PASS"` | Yes |

**Validation rules**:
- `status == "PASS"` requires `reachable`, `ready`, `apkInstalled`, and `launchVerified` all `true`, `elapsedMs <= 60000`, and `installedVersionIdentifier == AppBuildArtifact.versionIdentifier`.
- `status == "NOT_READY"` indicates the emulator was ADB-reachable but did not satisfy readiness checks before the 60-second device deadline.
- `status == "VERSION_MISMATCH"` indicates install succeeded but the confirmed device version identifier differs from the expected artifact version identifier.
- `status == "TIMEOUT"` indicates installation or launch verification exceeded the remaining per-device budget.
- Any non-`PASS` status MUST populate `errorMessage` with actionable detail including the device serial.

**State transitions**:
```text
[start] -> UNREACHABLE      (ADB cannot reach device)
       -> NOT_READY        (ADB reachable but readiness not reached before deadline)
       -> INSTALL_FAILED   (adb install exits non-zero)
       -> TIMEOUT          (install or launch verification overruns remaining budget)
       -> VERSION_MISMATCH (device version differs from artifact version)
       -> LAUNCH_FAILED    (am start -W does not report success)
       -> PASS             (all checks green)
```

---

### 3. `PreFlightReport`

Persisted JSON output of the pre-installation phase. Written to `testing/e2e/artifacts/runtime/preinstall-report.json` and consumed by Playwright support specs and CI artifacts.

| Field | Type | Description | Required |
|-------|------|-------------|----------|
| `reportId` | `string` | UUID v4 generated at report creation time | Yes |
| `timestamp` | `string (ISO 8601)` | UTC datetime when the report was generated | Yes |
| `buildArtifact` | `AppBuildArtifact` | Artifact metadata used for this pre-flight run | Yes |
| `devices` | `EmulatorInstallRecord[]` | Ordered array of per-device records | Yes |
| `overallStatus` | `"PASS" \| "FAIL"` | `"PASS"` only if every device record has `status == "PASS"` | Yes |
| `failureReason` | `string \| null` | Consolidated human-readable failure summary when `overallStatus == "FAIL"` | Yes |
| `totalElapsedMs` | `integer` | Wall-clock milliseconds for the overall pre-flight phase | Yes |
| `abortedBeforeInstall` | `boolean` | `true` if the run stopped before any installation attempt, for example missing artifact | Yes |

**Validation rules**:
- If `abortedBeforeInstall` is `true`, `devices` MAY be empty.
- If `abortedBeforeInstall` is `false`, `devices` MUST include one record per requested emulator.
- `overallStatus == "PASS"` iff all device records are `PASS`.
- `failureReason` MUST be non-null when `overallStatus == "FAIL"`.

**Idempotency**:
Running the pre-installation phase again with the same APK replaces the report file. The `reportId` changes, but the report reflects the actual latest run result.

---

## Entity Relationships

```text
PreFlightReport
  -> buildArtifact: AppBuildArtifact  (1:1)
  -> devices: EmulatorInstallRecord[] (1:N, one per requested emulator)
```

---

## Artifact Persistence

| Entity | Storage | Lifetime |
|--------|---------|----------|
| `AppBuildArtifact` | In memory during script execution; `path` points to disk APK | Until next build or clean |
| `EmulatorInstallRecord` | In memory during script execution; serialized into `PreFlightReport` | One per requested device per run |
| `PreFlightReport` | `testing/e2e/artifacts/runtime/preinstall-report.json` | Until next pre-flight run replaces it |

---

## Version Identifier

Per FR-006 and the clarification log, the version identifier is the combination of `versionName` and `versionCode`:

- App build: `versionName = "0.1.0"`, `versionCode = 1`
- Build identifier: `0.1.0+1`
- Debug package: `com.ndi.app.debug`
- Host-side extraction: `aapt dump badging app-debug.apk`
- Device-side confirmation: `adb -s <serial> shell dumpsys package com.ndi.app.debug`

---

## No Room or App Schema Changes

This feature introduces no new Room entities, DAOs, migrations, or Android app persistence changes. All data is ephemeral runtime state serialized to host-side JSON.
