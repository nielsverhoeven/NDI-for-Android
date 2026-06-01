# Data Model: Dual Emulator Testing Infrastructure

**Scope**: Entities and data structures for provisioning, relay coordination, and artifact collection.

---

## Core Entities

## JSON Schema Snippets

### EmulatorInstance (schema excerpt)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "EmulatorInstance",
  "type": "object",
  "required": ["id", "apiLevel", "state", "adbPort"],
  "properties": {
    "id": { "type": "string", "pattern": "^emulator-[0-9]+$" },
    "apiLevel": { "type": "integer", "minimum": 32, "maximum": 35 },
    "state": { "type": "string", "enum": ["PROVISIONING", "RUNNING", "IDLE", "FAILED", "STOPPED"] },
    "adbPort": { "type": "integer", "minimum": 5554, "maximum": 5568 },
    "relayPort": { "type": "integer", "minimum": 15000, "maximum": 15010 }
  }
}
```

### RelayServer (schema excerpt)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "RelayServer",
  "type": "object",
  "required": ["id", "state", "listeningPort", "routes"],
  "properties": {
    "id": { "type": "string", "minLength": 1 },
    "state": { "type": "string", "enum": ["STARTING", "RUNNING", "STOPPING", "STOPPED", "FAILED"] },
    "listeningPort": { "type": "integer", "minimum": 15000, "maximum": 15010 },
    "routes": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": ["sourceEmulatorId", "destEmulatorId", "sourcePort", "destPort", "protocol"],
        "properties": {
          "sourceEmulatorId": { "type": "string" },
          "destEmulatorId": { "type": "string" },
          "sourcePort": { "type": "integer", "minimum": 1025 },
          "destPort": { "type": "integer", "minimum": 1025 },
          "protocol": { "type": "string", "enum": ["TCP", "UDP"] }
        }
      }
    }
  }
}
```

### 1. EmulatorInstance

Represents a provisioned Android emulator instance with boot status, network state, and NDI integration.

**Fields**:
- `id` (string): Identifier (e.g., "emulator-5554", "emulator-5556")
- `apiLevel` (int): Android API level (32-35)
- `state` (enum): PROVISIONING | RUNNING | IDLE | FAILED | STOPPED
- `bootTimeMs` (long): Milliseconds from ADB command to device ready
- `adbPort` (int): ADB connection port (5554 base, +2 per instance)
- `ndiSdkApkPath` (string): Path to installed NDI SDK APK on host
- `ndiSdkApkVersion` (string): Semantic version (e.g., "1.0.0")
- `ndiSdkApkInstalled` (bool): True if APK deployed on device
- `relayPort` (int): TCP relay listening port for this emulator (15001, 15003)
- `lastBootTime` (datetime): ISO 8601 timestamp of last successful boot
- `createdTime` (datetime): ISO 8601 timestamp of instance creation
- `tags` (array<string>): Role tags ("source-device", "receiver-device", "latency-source")

**Relationships**:
- references `ProvisioningReport` (1 : N) - one emulator produces many provisioning reports (one per session)
- references `RelayServer` (0..1 : 1) - one relay server serves multiple emulators

**Validation Rules**:
- `apiLevel` must be in range [32, 35]
- `adbPort` must be in range [5554, 5568]
- `relayPort` must be unique per session and in range [15000, 15010]
- `state` must match actual ADB device status on each read (not cached)
- `ndiSdkApkPath` must exist on host filesystem (validated at install time)

**Example**:
```json
{
  "id": "emulator-5554",
  "apiLevel": 34,
  "state": "RUNNING",
  "bootTimeMs": 45000,
  "adbPort": 5554,
  "ndiSdkApkPath": "/ndi/sdk-bridge/build/outputs/apk/release/ndi-sdk-bridge-release.apk",
  "ndiSdkApkVersion": "1.0.0",
  "ndiSdkApkInstalled": true,
  "relayPort": 15001,
  "lastBootTime": "2026-03-23T14:30:00Z",
  "createdTime": "2026-03-23T14:00:00Z",
  "tags": ["source-device", "ndi-sender"]
}
```

---

### 2. RelayServer

Represents a TCP socket relay process that forwards NDI streams between emulators and/or to test harness.

**Fields**:
- `id` (string): UUID or session-ID (e.g., "relay-session-abc123")
- `state` (enum): STARTING | RUNNING | STOPPING | STOPPED | FAILED
- `listeningPort` (int): Local TCP port for ingress connections (typically 15000)
- `routes` (array<RelayRoute>): List of active forwarding rules
- `pidOrProcessHandle` (int or string): OS process ID or handle
- `startTime` (datetime): ISO 8601 timestamp of relay start
- `healthCheckIntervalMs` (int): Milliseconds between health checks (default 5000)
- `maxLatencyMs` (int): Configured maximum tolerable latency (from SC-002: 100ms)
- `uptime` (object):
  - `startEpochMs` (long): Start time in epoch milliseconds
  - `lastHealthCheckMs` (long): Epoch of last health check
  - `restartCount` (int): Number of automatic restarts since session start
- `metrics` (object):
  - `totalBytesForwarded` (long): Cumulative bytes forwarded
  - `peakLatencyMs` (int): Highest observed round-trip latency
  - `avgLatencyMs` (int): Average round-trip latency (rolling window, last 100 samples)
  - `packetLossPercent` (float): Percentage of packets lost (0-100)

**Relationships**:
- `routes` (1 : N) - one relay connects 2+ emulators via routes
- contains `HealthCheckResult` (1 : N) - history of health checks

**Validation Rules**:
- `listeningPort` must be unique per session
- `routes` must have at least 2 entries (bidirectional forwarding)
- `state` must be consistent with process alive status
- `avgLatencyMs` must not exceed `maxLatencyMs` (SC-002 enforcement)

**Example**:
```json
{
  "id": "relay-session-abc123",
  "state": "RUNNING",
  "listeningPort": 15000,
  "routes": [
    {
      "sourceEmulatorId": "emulator-5554",
      "destEmulatorId": "emulator-5556",
      "sourcePort": 15001,
      "destPort": 15003,
      "protocol": "TCP"
    },
    {
      "sourceEmulatorId": "emulator-5556",
      "destEmulatorId": "emulator-5554",
      "sourcePort": 15003,
      "destPort": 15001,
      "protocol": "TCP"
    }
  ],
  "pidOrProcessHandle": 12345,
  "startTime": "2026-03-23T14:32:00Z",
  "healthCheckIntervalMs": 5000,
  "maxLatencyMs": 100,
  "uptime": {
    "startEpochMs": 1711270320000,
    "lastHealthCheckMs": 1711270380000,
    "restartCount": 0
  },
  "metrics": {
    "totalBytesForwarded": 52428800,
    "peakLatencyMs": 85,
    "avgLatencyMs": 42,
    "packetLossPercent": 0.0
  }
}
```

---

### 3. RelayRoute

Represents a bidirectional forwarding rule within a relay server.

**Fields**:
- `id` (string): Route identifier (e.g., "route-5554-to-5556")
- `sourceEmulatorId` (string): ID of source emulator (e.g., "emulator-5554")
- `destEmulatorId` (string): ID of destination emulator (e.g., "emulator-5556")
- `sourcePort` (int): Port on source emulator (15001, 15003, etc.)
- `destPort` (int): Port on destination emulator
- `protocol` (enum): TCP | UDP (NDI uses TCP by default)
- `isActive` (bool): True if route is actively forwarding
- `packetsSent` (long): Count of packets sent on this route
- `packetsReceived` (long): Count of packets received
- `bytesForwarded` (long): Total bytes forwarded across this route
- `lastActiveTime` (datetime): ISO 8601 of last data transfer

**Validation Rules**:
- `sourcePort` and `destPort` must be > 1024 (user ports)
- Cannot create route from emulator to itself
- Protocol must match NDI streaming requirements (TCP default)

**Example**:
```json
{
  "id": "route-5554-to-5556",
  "sourceEmulatorId": "emulator-5554",
  "destEmulatorId": "emulator-5556",
  "sourcePort": 15001,
  "destPort": 15003,
  "protocol": "TCP",
  "isActive": true,
  "packetsSent": 1024,
  "packetsReceived": 1024,
  "bytesForwarded": 52428800,
  "lastActiveTime": "2026-03-23T14:35:00Z"
}
```

---

### 4. ProvisioningReport

Snapshot of provisioning operation for one or more emulator instances, created at the start and end of a test session.

**Fields**:
- `id` (string): UUID (e.g., "prov-session-xyz789")
- `sessionId` (string): Reference to test session ID (from Playwright context)
- `timestamp` (datetime): ISO 8601 when report was generated
- `phase` (enum): START | END | FAILED
- `emulatorInstances` (array<EmulatorInstance>): Snapshot of emulator states
- `relayServer` (RelayServer or null): Snapshot of relay state
- `duration` (object):
  - `startMs` (long): Session start epoch milliseconds
  - `endMs` (long): Session end epoch milliseconds (null if phase=START)
  - `totalDurationMs` (long): Calculated duration
- `status` (enum): SUCCESS | PARTIAL_FAILURE | FAILURE
- `errors` (array<string>): List of error messages (if any)
- `warnings` (array<string>): List of non-fatal warnings
- `systemMetrics` (object):
  - `hostCpuPercent` (float): CPU usage on test host during session
  - `hostMemoryMbAvailable` (int): Available memory on host
  - `hostDiskMbFree` (int): Free disk space on host
- `ndiSdkVersion` (string): NDI SDK version installed on emulators

**Relationships**:
- references `EmulatorInstance` (1 : N) - one report captures N emulator snapshots
- references `RelayServer` (1 : 0..1) - one report captures relay state
- may reference multiple `ArtifactCollection` records

**Validation Rules**:
- `phase` must be START, END, or FAILED (not other states)
- If `phase` is END, `duration.endMs` must be > `duration.startMs`
- `status` must be SUCCESS only if no errors and both emulators reached RUNNING
- `emulatorInstances` must match session configuration (e.g., 2 instances for dual setup)

**Example**:
```json
{
  "id": "prov-session-xyz789",
  "sessionId": "dual-emulator-e2e-2026-03-23-143000",
  "timestamp": "2026-03-23T14:30:00Z",
  "phase": "END",
  "emulatorInstances": [
    {
      "id": "emulator-5554",
      "apiLevel": 34,
      "state": "RUNNING",
      "bootTimeMs": 45000,
      "tags": ["source-device"]
    },
    {
      "id": "emulator-5556",
      "apiLevel": 34,
      "state": "RUNNING",
      "bootTimeMs": 48000,
      "tags": ["receiver-device"]
    }
  ],
  "relayServer": {
    "id": "relay-session-abc123",
    "state": "RUNNING",
    "avgLatencyMs": 42
  },
  "duration": {
    "startMs": 1711270320000,
    "endMs": 1711270620000,
    "totalDurationMs": 300000
  },
  "status": "SUCCESS",
  "errors": [],
  "warnings": [],
  "systemMetrics": {
    "hostCpuPercent": 42.5,
    "hostMemoryMbAvailable": 2048,
    "hostDiskMbFree": 5120
  },
  "ndiSdkVersion": "1.0.0"
}
```

---

### 5. ArtifactCollection

Represents collected logs, recordings, and diagnostic data for a test session.

**Fields**:
- `id` (string): UUID (e.g., "artifacts-session-def456")
- `sessionId` (string): Reference to test session ID
- `collectionTime` (datetime): ISO 8601 when collection completed
- `artifacts` (array<Artifact>): List of collected files/records
- `storagePath` (string): Host filesystem path (e.g., "testing/e2e/artifacts/session-abc123/")
- `totalSizeBytes` (long): Sum of all artifact sizes
- `compressionUsed` (bool): True if artifacts are compressed (zip, tar.gz)
- `manifestFile` (string): Path to manifest JSON file on host
- `status` (enum): COLLECTING | COLLECTED | ARCHIVED | FAILED
- `errors` (array<string>): Collection errors (if any)

**Relationships**:
- contains `Artifact` (1 : N) - one collection has many artifacts

**Validation Rules**:
- `storagePath` must exist on host filesystem after collection
- `totalSizeBytes` must match sum of artifacts
- `status` must be COLLECTED before external archival
- `manifestFile` must be valid JSON file

**Example**:
```json
{
  "id": "artifacts-session-def456",
  "sessionId": "dual-emulator-e2e-2026-03-23-143000",
  "collectionTime": "2026-03-23T14:50:00Z",
  "artifacts": [
    {
      "name": "logcat-emulator-5554.log",
      "type": "LOGCAT",
      "sizeBytes": 2097152,
      "path": "testing/e2e/artifacts/session-abc123/logcat-emulator-5554.log"
    },
    {
      "name": "screen-recording-source.mp4",
      "type": "VIDEO",
      "sizeBytes": 104857600,
      "path": "testing/e2e/artifacts/session-abc123/screen-recording-source.mp4"
    },
    {
      "name": "relay-metrics.json",
      "type": "DIAGNOSTICS",
      "sizeBytes": 8192,
      "path": "testing/e2e/artifacts/session-abc123/relay-metrics.json"
    }
  ],
  "storagePath": "testing/e2e/artifacts/session-abc123/",
  "totalSizeBytes": 106962944,
  "compressionUsed": false,
  "manifestFile": "testing/e2e/artifacts/session-abc123/manifest.json",
  "status": "COLLECTED",
  "errors": []
}
```

---

### 6. Artifact

Represents a single collected file or diagnostic record.

**Fields**:
- `id` (string): UUID (e.g., "artifact-ghi789")
- `name` (string): Filename or identifier (e.g., "logcat-emulator-5554.log")
- `type` (enum): LOGCAT | VIDEO | SCREENSHOT | DIAGNOSTICS | RECOVERY_LOG | RELAY_METRICS
- `path` (string): Host filesystem path (workspace-relative)
- `sizeBytes` (long): File size in bytes
- `mimeType` (string): MIME type if applicable (e.g., "video/mp4", "text/plain")
- `createdTime` (datetime): ISO 8601 when artifact was created
- `checksumSha256` (string): SHA-256 hash for integrity verification (optional)
- `relatedEmulatorId` (string or null): ID of emulator that generated this artifact
- `metadata` (object): Type-specific metadata
  - For LOGCAT: `lineCount`, `startTime`, `endTime`
  - For VIDEO: `durationSeconds`, `resolutionWxH`, `frameRate`
  - For RELAY_METRICS: `peakLatencyMs`, `avgLatencyMs`, `packetLossPercent`

**Validation Rules**:
- `path` must exist on host filesystem (verified after collection)
- `sizeBytes` must match actual file size
- `type` must match file content (inferred from path/extension)
- `mimeType` should match type (e.g., video/* for VIDEO type)

**Example**:
```json
{
  "id": "artifact-ghi789",
  "name": "logcat-emulator-5554.log",
  "type": "LOGCAT",
  "path": "testing/e2e/artifacts/session-abc123/logcat-emulator-5554.log",
  "sizeBytes": 2097152,
  "mimeType": "text/plain",
  "createdTime": "2026-03-23T14:48:00Z",
  "checksumSha256": "a1b2c3d4e5f6...",
  "relatedEmulatorId": "emulator-5554",
  "metadata": {
    "lineCount": 50000,
    "startTime": "2026-03-23T14:30:00Z",
    "endTime": "2026-03-23T14:48:00Z"
  }
}
```

---

### 7. HealthCheckResult

Represents a single health check execution for relay or emulator.

**Fields**:
- `id` (string): UUID
- `targetId` (string): ID of checked resource (relay or emulator)
- `targetType` (enum): RELAY | EMULATOR
- `timestamp` (datetime): ISO 8601 when check was performed
- `status` (enum): HEALTHY | DEGRADED | UNHEALTHY
- `latencyMs` (int): Round-trip latency for relay; connection time for emulator
- `details` (object):
  - For RELAY: `activeRoutes`, `bytesForwarded`, error messages
  - For EMULATOR: `adbConnectionTime`, `ndiSdkApkStatus`, device state

**Example**:
```json
{
  "id": "health-check-jkl012",
  "targetId": "relay-session-abc123",
  "targetType": "RELAY",
  "timestamp": "2026-03-23T14:35:00Z",
  "status": "HEALTHY",
  "latencyMs": 42,
  "details": {
    "activeRoutes": 2,
    "bytesForwarded": 52428800
  }
}
```

---

## State Transitions

### EmulatorInstance State Flow
```
PROVISIONING → RUNNING → IDLE → (RUNNING | STOPPED)
         ↓
       FAILED
```

### RelayServer State Flow
```
STARTING → RUNNING → (RUNNING | STOPPING | FAILED)
     ↓                               ↓
   FAILED                          STOPPED
```

### ProvisioningReport Phase Flow (one-way snapshot)
```
START (initial provisioning state)
  ↓
END (final state after test)
  ↓ (alternative)
FAILED (if provisioning failed)
```

---

## Constraints & Invariants

1. **Uniqueness**:
   - `EmulatorInstance.adbPort` unique per host
   - `EmulatorInstance.relayPort` unique per session
   - `RelayServer.id` unique per session

2. **Cardinality**:
   - One `RelayServer` serves up to 4 emulators (MVP: 2)
   - One `ProvisioningReport` captures all instances in a session
   - Many `Artifacts` per `ArtifactCollection`

3. **Consistency**:
   - `EmulatorInstance.state` must be validated against live ADB status on each read
   - `RelayServer.state` must be validated against live process status
   - Latency metrics must be updated from actual relay performance, not cached

---

## Storage Format

**JSON**: All entities exported as JSON for readability and CI/CD artifact storage
**PowerShell PSObject**: In-memory representation during script execution
**CSV** (Optional): Metrics can be exported to CSV for analysis / trending

**Storage Paths**:
- Reports: `testing/e2e/artifacts/{sessionId}/provisioning-summary.json`
- Relay metrics: `testing/e2e/artifacts/{sessionId}/relay-metrics.json`
- Logcat: `testing/e2e/artifacts/{sessionId}/logcat-{emulatorId}.log`
- Manifest: `testing/e2e/artifacts/{sessionId}/manifest.json`

---

**Status**: ✅ **COMPLETE**
