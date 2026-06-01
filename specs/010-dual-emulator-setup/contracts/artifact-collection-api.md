# Contract: Artifact Collection API

**Version**: 1.0 | **Stability**: BETA | **Transport**: In-process (PowerShell function calls, filesystem operations)

---

## Overview

Defines the interface for collecting, organizing, and reporting test artifacts (logcat, recordings, diagnostics) to host filesystem.

---

## Core Operations

### 1. Start-ArtifactCollection

**Summary**: Initialize artifact collection for a test session.

**Input**:
```json
{
  "sessionId": "dual-emulator-e2e-2026-03-23-143000",
  "storagePath": "testing/e2e/artifacts/session-abc123/",
  "createDirectories": true,
  "emulatorIds": ["emulator-5554", "emulator-5556"],
  "enableLogcat": true,
  "enableScreenRecording": true,
  "recordingResolutionWxH": "1080x1920",
  "recordingFrameRate": 30,
  "logcatLineLimit": 500,
  "enableDiagnostics": true
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "collectionId": "artifacts-session-def456",
  "sessionId": "dual-emulator-e2e-2026-03-23-143000",
  "storagePath": "testing/e2e/artifacts/session-abc123/",
  "status": "COLLECTING",
  "collectors": {
    "logcat-emulator-5554": "STARTED",
    "logcat-emulator-5556": "STARTED",
    "screen-recording-5554": "STARTED",
    "screen-recording-5556": "STARTED",
    "relay-metrics": "STARTED"
  },
  "startTime": "2026-03-23T14:30:00Z"
}
```

**Error Cases**:
- `INVALID_STORAGE_PATH`: Path does not exist or not writable
- `EMULATOR_NOT_RUNNING`: Cannot start logcat/recording for offline emulator
- `DISK_SPACE_INSUFFICIENT`: Insufficient disk space for artifacts
- `RECORDING_NOT_SUPPORTED`: Device does not support screen recording

---

### 2. Collect-Logcat

**Summary**: Stream logcat logs from emulator to file (continuous or snapshot).

**Input**:
```json
{
  "emulatorId": "emulator-5554",
  "outputFilePath": "testing/e2e/artifacts/session-abc123/logcat-emulator-5554.log",
  "bufferSize": "all",
  "lineLimit": 500,
  "streamMode": "SNAPSHOT"
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "emulatorId": "emulator-5554",
  "outputFilePath": "testing/e2e/artifacts/session-abc123/logcat-emulator-5554.log",
  "linesCollected": 487,
  "fileSizeBytes": 2097152,
  "timeRangeStart": "2026-03-23T14:30:00Z",
  "timeRangeEnd": "2026-03-23T14:48:00Z",
  "collectionDurationMs": 1500
}
```

**Stream Modes**:
- `SNAPSHOT`: Collect current logcat once and close
- `CONTINUOUS`: Stream logcat to file until explicitly stopped (for long-running suites)
- `ROLLING_BUFFER`: Keep last N lines only (default 500)

---

### 3. Collect-ScreenRecording

**Summary**: Record device screen to MP4 video file.

**Input**:
```json
{
  "emulatorId": "emulator-5554",
  "outputFilePath": "testing/e2e/artifacts/session-abc123/screen-recording-5554.mp4",
  "resolutionWxH": "1080x1920",
  "frameRate": 30,
  "bitrateMbps": 10,
  "durationLimitSeconds": 600,
  "startRecordingImmediately": true
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "emulatorId": "emulator-5554",
  "recordingId": "recording-5554-abc123",
  "outputFilePath": "testing/e2e/artifacts/session-abc123/screen-recording-5554.mp4",
  "recordingState": "RECORDING",
  "estimatedMaxFileSizeBytes": 600000000,
  "startTime": "2026-03-23T14:30:00Z"
}
```

**Lifecycle**:
1. `Start-ScreenRecording()` → state=RECORDING
2. Playwright test runs
3. `Stop-ScreenRecording(recordingId)` → state=STOPPED, finalizes MP4

---

### 4. Stop-ScreenRecording

**Summary**: Stop active screen recording and finalize video file.

**Input**:
```json
{
  "recordingId": "recording-5554-abc123",
  "timeoutSeconds": 10
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "recordingId": "recording-5554-abc123",
  "emulatorId": "emulator-5554",
  "outputFilePath": "testing/e2e/artifacts/session-abc123/screen-recording-5554.mp4",
  "recordingState": "STOPPED",
  "durationSeconds": 180,
  "fileSizeBytes": 540000000,
  "framesRecorded": 5400,
  "stopTime": "2026-03-23T14:33:00Z"
}
```

---

### 5. Collect-Diagnostics

**Summary**: Capture diagnostic data (device properties, NDI SDK status, system info).

**Input**:
```json
{
  "emulatorId": "emulator-5554",
  "outputFilePath": "testing/e2e/artifacts/session-abc123/ndi-diagnostics-5554.json",
  "includeBuildInfo": true,
  "includePermissions": true,
  "includeServices": true
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "emulatorId": "emulator-5554",
  "outputFilePath": "testing/e2e/artifacts/session-abc123/ndi-diagnostics-5554.json",
  "diagnostics": {
    "device": {
      "model": "Android SDK built for x86_64",
      "apiLevel": 34,
      "androidVersion": "14.0",
      "buildFingerprint": "google/sdk_google_phone_x86_64/generic_x86_64:14/UPB5.230613.019/..."
    },
    "ndiSdkStatus": {
      "apkInstalled": true,
      "apkVersion": "1.0.0",
      "packageName": "com.ndi.sdk",
      "nativeLibraryAvailable": true
    },
    "permissions": {
      "INTERNET": "GRANTED",
      "CHANGE_NETWORK_STATE": "GRANTED"
    }
  },
  "collectionTime": "2026-03-23T14:48:00Z"
}
```

---

### 6. Collect-RelayMetrics

**Summary**: Export relay server performance metrics to JSON.

**Input**:
```json
{
  "relayId": "relay-session-abc123",
  "outputFilePath": "testing/e2e/artifacts/session-abc123/relay-metrics.json",
  "includeRouteDetails": true,
  "includeLatencySamples": true
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "relayId": "relay-session-abc123",
  "outputFilePath": "testing/e2e/artifacts/session-abc123/relay-metrics.json",
  "metrics": {
    "totalBytesForwarded": 52428800,
    "peakLatencyMs": 85,
    "avgLatencyMs": 42,
    "p99LatencyMs": 78,
    "packetLossPercent": 0.0
  },
  "exportTime": "2026-03-23T14:48:00Z",
  "fileSizeBytes": 8192
}
```

---

### 7. Stop-ArtifactCollection

**Summary**: Finalize artifact collection and generate manifest.

**Input**:
```json
{
  "collectionId": "artifacts-session-def456",
  "generateManifest": true,
  "generateSummary": true,
  "compressionFormat": "NONE"
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "collectionId": "artifacts-session-def456",
  "status": "COLLECTED",
  "artifacts": [
    {
      "name": "logcat-emulator-5554.log",
      "type": "LOGCAT",
      "sizeBytes": 2097152,
      "path": "testing/e2e/artifacts/session-abc123/logcat-emulator-5554.log"
    },
    {
      "name": "screen-recording-5554.mp4",
      "type": "VIDEO",
      "sizeBytes": 540000000,
      "path": "testing/e2e/artifacts/session-abc123/screen-recording-5554.mp4"
    },
    {
      "name": "relay-metrics.json",
      "type": "DIAGNOSTICS",
      "sizeBytes": 8192,
      "path": "testing/e2e/artifacts/session-abc123/relay-metrics.json"
    }
  ],
  "totalSizeBytes": 542105344,
  "manifestFile": "testing/e2e/artifacts/session-abc123/manifest.json",
  "collectionDurationMs": 1800000,
  "stopTime": "2026-03-23T14:48:00Z"
}
```

---

### 8. Generate-ArtifactManifest

**Summary**: Create JSON manifest of collected artifacts for CI/CD integration.

**Input**:
```json
{
  "collectionId": "artifacts-session-def456",
  "sessionId": "dual-emulator-e2e-2026-03-23-143000",
  "storagePath": "testing/e2e/artifacts/session-abc123/",
  "outputFilePath": "testing/e2e/artifacts/session-abc123/manifest.json",
  "includeChecksums": true,
  "includeMetadata": true
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "collectionId": "artifacts-session-def456",
  "manifestFile": "testing/e2e/artifacts/session-abc123/manifest.json",
  "manifest": {
    "version": "1.0",
    "sessionId": "dual-emulator-e2e-2026-03-23-143000",
    "generatedTime": "2026-03-23T14:48:00Z",
    "artifacts": [
      {
        "name": "logcat-emulator-5554.log",
        "type": "LOGCAT",
        "path": "testing/e2e/artifacts/session-abc123/logcat-emulator-5554.log",
        "sizeBytes": 2097152,
        "checksumSha256": "a1b2c3d4e5f6...",
        "mimeType": "text/plain",
        "metadata": {
          "lineCount": 50000,
          "timeRangeStart": "2026-03-23T14:30:00Z",
          "timeRangeEnd": "2026-03-23T14:48:00Z"
        }
      }
    ],
    "summary": {
      "totalArtifacts": 3,
      "totalSizeBytes": 542105344,
      "collectionStatus": "COMPLETE"
    }
  }
}
```

**Manifest Schema**: JSON, machine-readable for CI/CD artifact upload integration

---

### 9. Get-ArtifactCollection

**Summary**: Query status and metadata of artifact collection without modification.

**Input**:
```json
{
  "collectionId": "artifacts-session-def456"
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "collection": {
    "id": "artifacts-session-def456",
    "sessionId": "dual-emulator-e2e-2026-03-23-143000",
    "state": "COLLECTED",
    "storagePath": "testing/e2e/artifacts/session-abc123/",
    "startTime": "2026-03-23T14:30:00Z",
    "stopTime": "2026-03-23T14:48:00Z",
    "totalSizeBytes": 542105344,
    "artifactCount": 6
  }
}
```

**Read-Only**: YES - no side effects

---

## Artifact Types

| Type | Format | Max Size | Retention |
|------|--------|----------|-----------|
| LOGCAT | Text (plain) | 10 MB (configurable) | Per suite |
| VIDEO | MP4 H.264 | 500-1000 MB (configurable) | Per suite |
| SCREENSHOT | PNG/JPG | 10 MB | Per suite |
| DIAGNOSTICS | JSON | 1 MB | Per suite |
| RECOVERY_LOG | Text (plain) | 5 MB | Per suite |
| RELAY_METRICS | JSON | 1 MB | Per suite |

---

## Error Handling Contract

All operations return structured error responses:

```json
{
  "status": "FAILURE",
  "errorCode": "DISK_SPACE_INSUFFICIENT",
  "errorMessage": "Insufficient disk space for artifact collection",
  "details": {
    "requiredBytes": 600000000,
    "availableBytes": 100000000,
    "storagePath": "testing/e2e/artifacts/"
  },
  "retryable": false,
  "suggestedAction": "Free up disk space or configure smaller artifact sizes"
}
```

---

## Constraints

**Storage Limits**:
- Total artifact size per session: configurable (default 1 GB)
- Logcat size: configurable (default 10 MB, adjustable via lineLimit)
- Video size: configurable per emulator (default 500 MB)

**Timing** (SC-004):
- Artifact collection must complete within 30 seconds of test end
- Manifest generation must complete within 5 seconds

**Reliability**:
- Artifact collection must survive emulator restarts (defer logcat collection to end of suite)
- Partial failures acceptable (e.g., logcat succeeds, video fails) → return PARTIAL_SUCCESS

**CI/CD Integration**:
- Manifest JSON format for GitHub Actions artifact upload
- Relative paths (workspace-relative) in manifest for portability
- Checksum for integrity verification

---

## Dependencies

**Dependent On**:
- Emulator API (Get-EmulatorState, ADB logcat/recording commands)
- Relay API (Get-RelayMetrics for relay diagnostics)
- Filesystem (write access to testing/e2e/artifacts/)

**Depended On By**:
- CI/CD workflows: GitHub Actions artifact upload (feature 012: CI/CD Pipeline Integration)
- Failure analysis: Manual review of logs/videos post-test

---

**Status**: ✅ **COMPLETE**
