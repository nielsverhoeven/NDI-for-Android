# Contract: Pre-Flight Installation Report Schema

**Branch**: `011-e2e-app-preinstall` | **Date**: 2026-03-23  
**Contract Type**: JSON output schema  
**Producer**: `testing/e2e/scripts/install-app-preinstall.ps1`  
**Consumers**: `testing/e2e/tests/support/app-preinstall.spec.ts`, `collect-test-artifacts.ps1`, CI artifact uploads

---

## Overview

`install-app-preinstall.ps1` writes a structured JSON report to  
`testing/e2e/artifacts/runtime/preinstall-report.json` after the pre-flight installation phase completes (whether or not it succeeded). This contract defines the schema that consumers MUST be able to parse.

---

## JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "PreFlightReport",
  "type": "object",
  "required": [
    "reportId", "timestamp", "buildArtifact", "devices",
    "overallStatus", "failureReason", "totalElapsedMs", "abortedBeforeInstall"
  ],
  "properties": {
    "reportId": {
      "type": "string",
      "format": "uuid",
      "description": "UUID v4 uniquely identifying this report run."
    },
    "timestamp": {
      "type": "string",
      "format": "date-time",
      "description": "UTC ISO 8601 timestamp when the report was generated."
    },
    "buildArtifact": {
      "type": "object",
      "required": ["path", "variant", "packageName", "versionName", "versionCode", "buildTimestamp", "exists"],
      "properties": {
        "path":           { "type": "string" },
        "variant":        { "type": "string", "enum": ["debug", "release"] },
        "packageName":    { "type": "string" },
        "versionName":    { "type": "string" },
        "versionCode":    { "type": "integer", "minimum": 1 },
        "buildTimestamp": { "type": "string", "format": "date-time" },
        "exists":         { "type": "boolean" }
      }
    },
    "devices": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": [
          "serial", "reachable", "apkInstalled",
          "installedVersionName", "installedVersionCode",
          "launchVerified", "elapsedMs", "status", "errorMessage"
        ],
        "properties": {
          "serial":               { "type": "string" },
          "reachable":            { "type": "boolean" },
          "apkInstalled":         { "type": "boolean" },
          "installedVersionName": { "type": ["string", "null"] },
          "installedVersionCode": { "type": ["integer", "null"], "minimum": 0 },
          "launchVerified":       { "type": "boolean" },
          "elapsedMs":            { "type": "integer", "minimum": 0 },
          "status": {
            "type": "string",
            "enum": ["PASS", "INSTALL_FAILED", "VERSION_MISMATCH", "LAUNCH_FAILED", "TIMEOUT", "UNREACHABLE"]
          },
          "errorMessage": { "type": ["string", "null"] }
        }
      }
    },
    "overallStatus":        { "type": "string", "enum": ["PASS", "FAIL"] },
    "failureReason":        { "type": ["string", "null"] },
    "totalElapsedMs":       { "type": "integer", "minimum": 0 },
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
    "buildTimestamp": "2026-03-23T13:55:12.000Z",
    "exists": true
  },
  "devices": [
    {
      "serial": "emulator-5554",
      "reachable": true,
      "apkInstalled": true,
      "installedVersionName": "0.1.0",
      "installedVersionCode": 1,
      "launchVerified": true,
      "elapsedMs": 18450,
      "status": "PASS",
      "errorMessage": null
    },
    {
      "serial": "emulator-5556",
      "reachable": true,
      "apkInstalled": true,
      "installedVersionName": "0.1.0",
      "installedVersionCode": 1,
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

## Example: Missing Build Artifact (FR-003)

```json
{
  "reportId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "timestamp": "2026-03-23T14:05:00.000Z",
  "buildArtifact": {
    "path": "C:\\gitrepos\\NDI-for-Android\\app\\build\\outputs\\apk\\debug\\app-debug.apk",
    "variant": "debug",
    "packageName": "com.ndi.app.debug",
    "versionName": "",
    "versionCode": 0,
    "buildTimestamp": "",
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

## Example: Installation Timeout on One Device (FR-004, FR-008)

```json
{
  "reportId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "timestamp": "2026-03-23T14:10:00.000Z",
  "buildArtifact": {
    "path": "...",
    "variant": "debug",
    "packageName": "com.ndi.app.debug",
    "versionName": "0.1.0",
    "versionCode": 1,
    "buildTimestamp": "2026-03-23T14:00:00.000Z",
    "exists": true
  },
  "devices": [
    {
      "serial": "emulator-5554",
      "reachable": true,
      "apkInstalled": true,
      "installedVersionName": "0.1.0",
      "installedVersionCode": 1,
      "launchVerified": true,
      "elapsedMs": 19800,
      "status": "PASS",
      "errorMessage": null
    },
    {
      "serial": "emulator-5556",
      "reachable": true,
      "apkInstalled": false,
      "installedVersionName": null,
      "installedVersionCode": null,
      "launchVerified": false,
      "elapsedMs": 60001,
      "status": "TIMEOUT",
      "errorMessage": "emulator-5556: Installation exceeded 60-second limit. ADB install job timed out. Verify emulator storage availability and ADB connectivity."
    }
  ],
  "overallStatus": "FAIL",
  "failureReason": "Pre-flight installation failed on 1 of 2 devices. emulator-5556: TIMEOUT — Installation exceeded 60-second limit.",
  "totalElapsedMs": 79801,
  "abortedBeforeInstall": false
}
```

---

## Invariants

- The report MUST be written even when `overallStatus == "FAIL"` (including abort-before-install).
- `overallStatus == "PASS"` iff all device `status` values are `"PASS"`.
- `failureReason` is non-null iff `overallStatus == "FAIL"`.
- `elapsedMs` per device ≤ 60000 is required for `status == "PASS"` (FR-008 / SC-003).
- The report file is replaced on each run (not appended).

---

## Breaking Change Policy

Changes to this schema that remove fields or change field types/enums are **breaking** and require updating `app-preinstall.spec.ts` and any other consumer in the same PR. Additive changes (new optional fields) are non-breaking.
