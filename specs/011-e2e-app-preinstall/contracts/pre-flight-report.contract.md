# Contract: Pre-Flight Installation Report Schema

**Branch**: `011-e2e-app-preinstall` | **Date**: 2026-03-23  
**Contract Type**: JSON output schema  
**Producer**: `testing/e2e/scripts/install-app-preinstall.ps1`  
**Consumers**: `testing/e2e/tests/support/app-preinstall.spec.ts`, `collect-test-artifacts.ps1`, CI artifact uploads

---

## Overview

`install-app-preinstall.ps1` writes a structured JSON report to `testing/e2e/artifacts/runtime/preinstall-report.json` after the pre-flight phase completes, whether the run passed or failed. Consumers MUST treat this schema as the source of truth for device-level pre-install state.

---

## JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "PreFlightReport",
  "type": "object",
  "required": [
    "reportId",
    "timestamp",
    "buildArtifact",
    "devices",
    "overallStatus",
    "failureReason",
    "totalElapsedMs",
    "abortedBeforeInstall"
  ],
  "properties": {
    "reportId": {
      "type": "string",
      "format": "uuid"
    },
    "timestamp": {
      "type": "string",
      "format": "date-time"
    },
    "buildArtifact": {
      "type": "object",
      "required": [
        "path",
        "variant",
        "packageName",
        "versionName",
        "versionCode",
        "versionIdentifier",
        "buildTimestamp",
        "exists"
      ],
      "properties": {
        "path": { "type": "string" },
        "variant": { "type": "string", "enum": ["debug", "release"] },
        "packageName": { "type": "string" },
        "versionName": { "type": ["string", "null"] },
        "versionCode": { "type": ["integer", "null"], "minimum": 1 },
        "versionIdentifier": { "type": ["string", "null"] },
        "buildTimestamp": { "type": ["string", "null"], "format": "date-time" },
        "exists": { "type": "boolean" }
      }
    },
    "devices": {
      "type": "array",
      "minItems": 0,
      "items": {
        "type": "object",
        "required": [
          "serial",
          "reachable",
          "ready",
          "readinessWaitMs",
          "apkInstalled",
          "installedVersionName",
          "installedVersionCode",
          "installedVersionIdentifier",
          "launchVerified",
          "elapsedMs",
          "status",
          "errorMessage"
        ],
        "properties": {
          "serial": { "type": "string" },
          "reachable": { "type": "boolean" },
          "ready": { "type": "boolean" },
          "readinessWaitMs": { "type": "integer", "minimum": 0 },
          "apkInstalled": { "type": "boolean" },
          "installedVersionName": { "type": ["string", "null"] },
          "installedVersionCode": { "type": ["integer", "null"], "minimum": 1 },
          "installedVersionIdentifier": { "type": ["string", "null"] },
          "launchVerified": { "type": "boolean" },
          "elapsedMs": { "type": "integer", "minimum": 0 },
          "status": {
            "type": "string",
            "enum": [
              "PASS",
              "NOT_READY",
              "INSTALL_FAILED",
              "VERSION_MISMATCH",
              "LAUNCH_FAILED",
              "TIMEOUT",
              "UNREACHABLE"
            ]
          },
          "errorMessage": { "type": ["string", "null"] }
        }
      }
    },
    "overallStatus": { "type": "string", "enum": ["PASS", "FAIL"] },
    "failureReason": { "type": ["string", "null"] },
    "totalElapsedMs": { "type": "integer", "minimum": 0 },
    "abortedBeforeInstall": { "type": "boolean" }
  }
}
```

---

## Example: Successful Pre-Flight Run

```json
{
  "reportId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "timestamp": "2026-03-23T14:00:00.000Z",
  "buildArtifact": {
    "path": "C:\\gitrepos\\NDI-for-Android\\app\\build\\outputs\\apk\\debug\\app-debug.apk",
    "variant": "debug",
    "packageName": "com.ndi.app.debug",
    "versionName": "0.1.0",
    "versionCode": 1,
    "versionIdentifier": "0.1.0+1",
    "buildTimestamp": "2026-03-23T13:55:12.000Z",
    "exists": true
  },
  "devices": [
    {
      "serial": "emulator-5554",
      "reachable": true,
      "ready": true,
      "readinessWaitMs": 3200,
      "apkInstalled": true,
      "installedVersionName": "0.1.0",
      "installedVersionCode": 1,
      "installedVersionIdentifier": "0.1.0+1",
      "launchVerified": true,
      "elapsedMs": 18450,
      "status": "PASS",
      "errorMessage": null
    },
    {
      "serial": "emulator-5556",
      "reachable": true,
      "ready": true,
      "readinessWaitMs": 4100,
      "apkInstalled": true,
      "installedVersionName": "0.1.0",
      "installedVersionCode": 1,
      "installedVersionIdentifier": "0.1.0+1",
      "launchVerified": true,
      "elapsedMs": 20130,
      "status": "PASS",
      "errorMessage": null
    }
  ],
  "overallStatus": "PASS",
  "failureReason": null,
  "totalElapsedMs": 38580,
  "abortedBeforeInstall": false
}
```

---

## Example: Missing Build Artifact

```json
{
  "reportId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "timestamp": "2026-03-23T14:05:00.000Z",
  "buildArtifact": {
    "path": "C:\\gitrepos\\NDI-for-Android\\app\\build\\outputs\\apk\\debug\\app-debug.apk",
    "variant": "debug",
    "packageName": "com.ndi.app.debug",
    "versionName": null,
    "versionCode": null,
    "versionIdentifier": null,
    "buildTimestamp": null,
    "exists": false
  },
  "devices": [],
  "overallStatus": "FAIL",
  "failureReason": "APK artifact not found at app/build/outputs/apk/debug/app-debug.apk. Run './gradlew :app:assembleDebug' before executing the e2e test suite.",
  "totalElapsedMs": 12,
  "abortedBeforeInstall": true
}
```

---

## Example: Emulator Not Ready Within Deadline

```json
{
  "reportId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "timestamp": "2026-03-23T14:10:00.000Z",
  "buildArtifact": {
    "path": "C:\\gitrepos\\NDI-for-Android\\app\\build\\outputs\\apk\\debug\\app-debug.apk",
    "variant": "debug",
    "packageName": "com.ndi.app.debug",
    "versionName": "0.1.0",
    "versionCode": 1,
    "versionIdentifier": "0.1.0+1",
    "buildTimestamp": "2026-03-23T14:00:00.000Z",
    "exists": true
  },
  "devices": [
    {
      "serial": "emulator-5554",
      "reachable": true,
      "ready": true,
      "readinessWaitMs": 1800,
      "apkInstalled": true,
      "installedVersionName": "0.1.0",
      "installedVersionCode": 1,
      "installedVersionIdentifier": "0.1.0+1",
      "launchVerified": true,
      "elapsedMs": 19800,
      "status": "PASS",
      "errorMessage": null
    },
    {
      "serial": "emulator-5556",
      "reachable": true,
      "ready": false,
      "readinessWaitMs": 60000,
      "apkInstalled": false,
      "installedVersionName": null,
      "installedVersionCode": null,
      "installedVersionIdentifier": null,
      "launchVerified": false,
      "elapsedMs": 60000,
      "status": "NOT_READY",
      "errorMessage": "emulator-5556: Emulator did not become install-ready within 60 seconds. Verify boot completion and ADB responsiveness."
    }
  ],
  "overallStatus": "FAIL",
  "failureReason": "Pre-flight installation failed on 1 of 2 devices. emulator-5556: NOT_READY - Emulator did not become install-ready within 60 seconds.",
  "totalElapsedMs": 79800,
  "abortedBeforeInstall": false
}
```

---

## Invariants

- The report MUST be written even when `overallStatus == "FAIL"`, including abort-before-install.
- `devices` may be empty only when `abortedBeforeInstall == true`.
- `overallStatus == "PASS"` iff all device `status` values are `"PASS"`.
- `failureReason` is non-null iff `overallStatus == "FAIL"`.
- `elapsedMs` per device includes readiness wait, installation, version confirmation, and launch verification.
- `elapsedMs <= 60000` is required for any `PASS` device record.
- The report file is replaced on each run, never appended.

---

## Breaking Change Policy

Any change that removes fields, changes types, or changes enum values is breaking and requires updating all report consumers in the same change. Additive optional fields are non-breaking.
