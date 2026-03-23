# Contract: Emulator Provisioning API

**Version**: 1.0 | **Stability**: BETA | **Transport**: In-process (PowerShell DSC / function calls)

---

## Overview

Defines the interface for provisioning dual Android emulators, managing lifecycle, and reporting state.

---

## Core Operations

### 1. Provision-Emulator

**Summary**: Create or reuse an emulator instance with specified API level.

**Input**:
```json
{
  "emulatorId": "emulator-5554",
  "apiLevel": 34,
  "bootTimeoutSeconds": 90,
  "ndiSdkApkPath": "/ndi/sdk-bridge/build/outputs/apk/release/ndi-sdk-bridge-release.apk",
  "installNdiSdk": true,
  "resetDeviceState": false,
  "tags": ["source-device", "ndi-sender"]
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "emulatorInstance": {
    "id": "emulator-5554",
    "apiLevel": 34,
    "state": "RUNNING",
    "bootTimeMs": 45000,
    "adbPort": 5554,
    "ndiSdkApkInstalled": true,
    "relayPort": 15001,
    "lastBootTime": "2026-03-23T14:30:00Z"
  },
  "durationMs": 45000,
  "errors": []
}
```

**Error Cases**:
- `EMULATOR_NOT_AVAILABLE`: System image not available for API level
- `PORT_IN_USE`: ADB port already bound
- `BOOT_TIMEOUT`: Emulator did not reach RUNNING state within timeout
- `NDI_SDK_INSTALL_FAILED`: APK installation failed

**Idempotency**: YES - calling twice with same emulatorId is safe (reuses if running)

---

### 2. Provision-DualEmulator

**Summary**: Provision two emulators atomically for source/receiver roles.

**Input**:
```json
{
  "sourceEmulatorId": "emulator-5554",
  "receiverEmulatorId": "emulator-5556",
  "apiLevel": 34,
  "bootTimeoutSeconds": 90,
  "ndiSdkApkPath": "/ndi/sdk-bridge/build/outputs/apk/release/ndi-sdk-bridge-release.apk",
  "skipBootIfAlreadyRunning": true,
  "parallel": true
}
```

**Output**:
```json
{
  "status": "PARTIAL_SUCCESS",
  "sourceEmulator": {
    "id": "emulator-5554",
    "state": "RUNNING",
    "bootTimeMs": 45000
  },
  "receiverEmulator": {
    "id": "emulator-5556",
    "state": "RUNNING",
    "bootTimeMs": 48000
  },
  "totalDurationMs": 48000,
  "failedInstances": [],
  "errors": []
}
```

**Error Cases**:
- `PARTIAL_FAILURE`: One emulator provisioned, one failed
- `TOTAL_FAILURE`: Neither emulator provisioned
- `PORT_CONFLICT`: Both emulators cannot be assigned unique ports

**Idempotency**: YES (with skipBootIfAlreadyRunning=true)

---

### 3. Get-EmulatorState

**Summary**: Query current state of an emulator without modification.

**Input**:
```json
{
  "emulatorId": "emulator-5554"
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "emulatorInstance": {
    "id": "emulator-5554",
    "state": "RUNNING",
    "adbPort": 5554,
    "ndiSdkApkInstalled": true,
    "bootTime": "2026-03-23T14:30:00Z"
  }
}
```

**Read-Only**: YES - no side effects

---

### 4. Stop-Emulator

**Summary**: Stop an emulator instance gracefully.

**Input**:
```json
{
  "emulatorId": "emulator-5554",
  "force": false
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "emulatorId": "emulator-5554",
  "previousState": "RUNNING",
  "newState": "STOPPED",
  "gracefulShutdownMs": 2000
}
```

---

### 5. Reset-EmulatorState

**Summary**: Wipe device data while keeping instance running (for inter-suite resets).

**Input**:
```json
{
  "emulatorId": "emulator-5554",
  "preserveNdiSdk": true,
  "timeoutSeconds": 30
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "emulatorId": "emulator-5554",
  "filesDeleted": ["com.ndi.test.data", "com.example.testapp"],
  "durationMs": 5000,
  "ndiSdkRestored": true
}
```

---

### 6. Install-NdiSdk

**Summary**: Deploy NDI SDK APK to a running emulator.

**Input**:
```json
{
  "emulatorId": "emulator-5554",
  "apkPath": "/ndi/sdk-bridge/build/outputs/apk/release/ndi-sdk-bridge-release.apk",
  "forceReinstall": false,
  "timeoutSeconds": 30
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "emulatorId": "emulator-5554",
  "apkVersion": "1.0.0",
  "installTimeMs": 8000,
  "installed": true
}
```

---

### 7. Get-ProvisioningReport

**Summary**: Capture snapshot of provisioning state for analysis/logging.

**Input**:
```json
{
  "sessionId": "dual-emulator-e2e-2026-03-23-143000",
  "emulatorIds": ["emulator-5554", "emulator-5556"],
  "phase": "START"
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "report": {
    "id": "prov-session-xyz789",
    "sessionId": "dual-emulator-e2e-2026-03-23-143000",
    "timestamp": "2026-03-23T14:30:00Z",
    "phase": "START",
    "emulatorInstances": [
      {"id": "emulator-5554", "state": "RUNNING", "bootTimeMs": 45000},
      {"id": "emulator-5556", "state": "RUNNING", "bootTimeMs": 48000}
    ],
    "status": "SUCCESS",
    "errors": []
  }
}
```

---

## Error Handling Contract

All operations return structured error responses:

```json
{
  "status": "FAILURE",
  "errorCode": "BOOT_TIMEOUT",
  "errorMessage": "Emulator did not reach RUNNING state within 90 seconds",
  "details": {
    "emulatorId": "emulator-5554",
    "lastKnownState": "DEVICE_OFFLINE",
    "adbOutput": "device offline"
  },
  "retryable": true,
  "suggestedAction": "Increase bootTimeoutSeconds or check host resources"
}
```

**Retryable Errors**:
- `BOOT_TIMEOUT` (transient, retry with backoff)
- `ADB_CONNECTION_LOST` (transient)
- `PORT_IN_USE` (try alternate port)

**Non-Retryable Errors**:
- `EMULATOR_NOT_AVAILABLE` (system image missing)
- `NDI_SDK_APK_NOT_FOUND` (APK path invalid)

---

## Constraints

**Provisioning Limits**:
- Maximum 2 concurrent emulator boots per session (SC-006 overhead constraint)
- Boot timeout: 90 seconds (adjustable, SC-001 target: 2 min / <10 sec reuse)
- API levels: 32-35 only

**Resource Requirements**:
- Each emulator: 512-1024 MB RAM (configurable)
- Each emulator: ~1 GB disk space for system image
- Host must have 4 GB RAM minimum for dual emulator

---

**Status**: ✅ **COMPLETE**
