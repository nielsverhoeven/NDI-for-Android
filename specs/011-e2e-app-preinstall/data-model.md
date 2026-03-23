# Data Model: E2E App Pre-Installation Gate

**Branch**: `011-e2e-app-preinstall` | **Date**: 2026-03-23  
**Spec**: specs/011-e2e-app-preinstall/spec.md

---

## Overview

This feature operates entirely within the test infrastructure layer. No Android application source code or database schema is modified. All entities below are **runtime data structures** used by the pre-installation script and persisted to a JSON report consumed by the Playwright harness.

---

## Entities

### 1. `AppBuildArtifact`

Represents the compiled, installable APK produced by `./gradlew :app:assembleDebug`.

| Field | Type | Description | Required |
|-------|------|-------------|----------|
| `path` | `string` | Absolute filesystem path to the APK file | Yes |
| `variant` | `string` | Gradle build variant; default `"debug"` | Yes |
| `packageName` | `string` | Android application ID; default `"com.ndi.app.debug"` | Yes |
| `versionName` | `string` | Human-readable version string (e.g., `"0.1.0"`), extracted from APK metadata via `aapt dump badging` | Yes |
| `versionCode` | `integer` | Monotonically increasing build number (e.g., `1`), extracted from APK metadata | Yes |
| `buildTimestamp` | `string (ISO 8601)` | File modification timestamp of the APK at pre-flight time; used to detect stale builds | Yes |
| `exists` | `boolean` | Whether the artifact file was found at `path` at pre-flight start | Yes |

**Validation rules**:
- `exists` MUST be `true` before any installation attempt; if `false`, abort with FR-003 error.
- `versionName` and `versionCode` MUST be extractable; if extraction fails, abort with FR-003 error.
- `path` defaults to `app/build/outputs/apk/debug/app-debug.apk` (relative to repo root) unless overridden via `$ApkPath` parameter or `APP_APK_PATH` environment variable.

---

### 2. `EmulatorInstallRecord`

Represents the per-device installation outcome for a single emulator. One record per registered emulator.

| Field | Type | Description | Required |
|-------|------|-------------|----------|
| `serial` | `string` | ADB device serial (e.g., `"emulator-5554"`) | Yes |
| `reachable` | `boolean` | Whether ADB can reach the device (`adb -s <serial> get-state` returns `"device"`) | Yes |
| `apkInstalled` | `boolean` | Whether `adb install -r` completed without error within the timeout window | Yes |
| `installedVersionName` | `string \| null` | `versionName` confirmed from device post-install via `adb shell dumpsys package`; `null` if install failed | Yes |
| `installedVersionCode` | `integer \| null` | `versionCode` confirmed from device post-install; `null` if install failed | Yes |
| `launchVerified` | `boolean` | Whether `am start -W -n <pkg>/<activity>` returned success; `false` if install failed or activity not found | Yes |
| `elapsedMs` | `integer` | Wall-clock milliseconds from start of install to completion (install + launch verification); must be ≤ 60000 per FR-008 | Yes |
| `status` | `"PASS" \| "INSTALL_FAILED" \| "VERSION_MISMATCH" \| "LAUNCH_FAILED" \| "TIMEOUT" \| "UNREACHABLE"` | Final disposition of this device's pre-flight step | Yes |
| `errorMessage` | `string \| null` | Human-readable error details when `status != "PASS"`; `null` on success | Yes |

**Validation rules**:
- `status == "PASS"` requires: `reachable`, `apkInstalled`, `launchVerified` all `true`, `elapsedMs ≤ 60000`, and `installedVersionCode == AppBuildArtifact.versionCode`.
- `status == "VERSION_MISMATCH"`: install succeeded but `installedVersionCode` differs from expected artifact (guards against ADB installing wrong artifact).
- `status == "TIMEOUT"`: `elapsedMs > 60000` — device is treated as a failure (FR-008).
- Any non-PASS status MUST populate `errorMessage` with actionable detail (device serial, operation that failed, suggested remediation) per FR-004 / SC-004.

**State transitions**:
```
[start] → UNREACHABLE         (if device not reachable)
       → INSTALL_FAILED       (if adb install exits non-zero or times out → TIMEOUT)
       → VERSION_MISMATCH     (if post-install version != artifact version)
       → LAUNCH_FAILED        (if am start -W returns failure)
       → PASS                 (all checks green)
```

---

### 3. `PreFlightReport`

Persisted JSON output of the pre-installation phase. Written to `testing/e2e/artifacts/runtime/preinstall-report.json` and consumed by `app-preinstall.spec.ts`.

| Field | Type | Description | Required |
|-------|------|-------------|----------|
| `reportId` | `string` | UUID v4 generated at report creation time | Yes |
| `timestamp` | `string (ISO 8601)` | UTC datetime when the report was generated | Yes |
| `buildArtifact` | `AppBuildArtifact` | Artifact metadata used for this pre-flight run | Yes |
| `devices` | `EmulatorInstallRecord[]` | Ordered array of per-device records; one entry per registered emulator | Yes |
| `overallStatus` | `"PASS" \| "FAIL"` | `"PASS"` iff every device record has `status == "PASS"`; `"FAIL"` otherwise | Yes |
| `failureReason` | `string \| null` | Consolidated human-readable failure summary when `overallStatus == "FAIL"`; includes all device-level `errorMessage` values; `null` on success | Yes |
| `totalElapsedMs` | `integer` | Wall-clock milliseconds for the entire pre-flight phase (all devices, sequential) | Yes |
| `abortedBeforeInstall` | `boolean` | `true` if the run was aborted before any installation attempt (e.g., missing artifact, FR-003) | Yes |

**Idempotency**: Running the pre-installation phase a second time with the same APK artifact replaces the report file. The `reportId` changes; all status fields reflect the actual outcome of the re-run (per FR-007).

---

## Entity Relationships

```
PreFlightReport
  └── buildArtifact: AppBuildArtifact  (1:1)
  └── devices: EmulatorInstallRecord[] (1:N, one per registered emulator)
```

---

## Artifact Persistence

| Entity | Storage | Lifetime |
|--------|---------|---------|
| `AppBuildArtifact` | In-memory during script; `path` field references disk APK | Exists until next Gradle build or clean |
| `EmulatorInstallRecord` | In-memory during script; serialized into `PreFlightReport` | One per pre-flight run |
| `PreFlightReport` | `testing/e2e/artifacts/runtime/preinstall-report.json` | Until next pre-flight run or `reset-emulator-state.ps1` cleans `artifacts/` |

---

## Version Identifier

Per FR-006 and the spec clarification (2026-03-23), the **version identifier** is the combination of `versionName` + `versionCode`:

- App build: `versionName = "0.1.0"`, `versionCode = 1` (from `app/build.gradle.kts`)
- Debug package: `com.ndi.app.debug`  
- ADB post-install extraction:  
  `adb -s <serial> shell dumpsys package com.ndi.app.debug | grep -E "versionName|versionCode"`
- Pre-install extraction from APK (for early validation):  
  `aapt dump badging app-debug.apk | grep "^package:"`

---

## No Room / Database Changes

This feature introduces no new Room entities, DAOs, or migrations. All data is ephemeral runtime state serialized to JSON on the host filesystem.
