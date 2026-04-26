# Data Model: NDI Discovery Routing Reliability

**Branch**: `031-fix-ndi-discovery-routing` | **Date**: 2026-04-26

## Core Entities

### DiscoveryModeSnapshot

Represents immutable mode selection metadata for one discovery run.

| Field | Type | Description |
|---|---|---|
| `runId` | `String` | Unique identifier for a discovery run. |
| `startedAtEpochMillis` | `Long` | Run start timestamp. |
| `enabledServerCount` | `Int` | Enabled discovery servers at run start. |
| `mode` | `MULTICAST` or `DISCOVERY_SERVER` | Selected mode for entire run. |
| `modeSelectionReason` | `String` | Deterministic reason string (`enabledServerCount==0` or `enabledServerCount>=1`). |

Validation rules:

1. Mode is selected once per run (FR-016).
2. `enabledServerCount == 0` implies `MULTICAST`; `>=1` implies `DISCOVERY_SERVER`.
3. Mode cannot change mid-run.

### DiscoveredSourceEndpoint

Represents one discovery output record used for cache upsert and routing.

| Field | Type | Description |
|---|---|---|
| `canonicalSourceId` | `String` | Stable identity for upsert/merge. |
| `displayName` | `String` | Source display name. |
| `endpointHost` | `String` | Streaming endpoint host/IP from source record. |
| `endpointPort` | `Int` | Streaming endpoint port from source record. |
| `discoveredAtEpochMillis` | `Long` | Timestamp when discovered in run. |
| `originMode` | `MULTICAST` or `DISCOVERY_SERVER` | Discovery mode that produced the record. |
| `originServerId` | `String?` | Discovery server identifier when server mode is used. |

Validation rules:

1. In discovery-server mode, endpoint values come from source records, never server endpoint (FR-003, FR-004).
2. Missing/invalid host or port records are excluded with diagnostics.

### CachedSourceRecord (existing persisted entity behavior)

Represents persisted continuity source data used on relaunch and startup.

| Field | Type | Description |
|---|---|---|
| `canonicalSourceId` | `String` | Cache identity key. |
| `displayName` | `String` | Most recent name. |
| `endpointHost` | `String` | Latest authoritative host. |
| `endpointPort` | `Int` | Latest authoritative port. |
| `lastDiscoveredAtEpochMillis` | `Long` | Latest discovery timestamp. |
| `retainedPreviewImagePath` | `String?` | Optional continuity preview path. |
| `validationState` | `NOT_YET_VALIDATED`/`VALIDATING`/`AVAILABLE`/`UNAVAILABLE` | Availability state model. |

Conflict rule:

- Same canonical identity with different endpoint updates existing row endpoint fields and latest timestamp, preserving preview continuity metadata (FR-018).

### DiscoveryRunResult

Represents user/system-visible outcome of a run.

| Field | Type | Description |
|---|---|---|
| `runId` | `String` | Discovery run identifier. |
| `mode` | `MULTICAST` or `DISCOVERY_SERVER` | Mode used for run. |
| `durationMillis` | `Long` | End-to-end run duration. |
| `status` | `SUCCESS`/`TIMEOUT`/`FAILURE` | Result category. |
| `sourceCount` | `Int` | Returned usable source count. |
| `diagnosticCode` | `String?` | Explicit timeout/failure code. |
| `diagnosticMessage` | `String?` | Human-readable explanation. |

Validation rules:

1. Discovery-server runs exceeding 5000ms must result in `TIMEOUT` with explicit diagnostics (FR-005, FR-017).
2. No same-run fallback to multicast from timeout/failure path (FR-017).

### DiscoveryServerDiagnosticRecord

Represents per-server observability in discovery-server mode.

| Field | Type | Description |
|---|---|---|
| `runId` | `String` | Parent discovery run id. |
| `serverId` | `String` | Discovery server identifier. |
| `endpoint` | `String` | Server endpoint string. |
| `attemptStartedAtEpochMillis` | `Long` | Attempt start time. |
| `durationMillis` | `Long` | Server response duration. |
| `status` | `SUCCESS`/`TIMEOUT`/`UNREACHABLE`/`ERROR` | Per-server result. |
| `errorDetail` | `String?` | Failure detail if applicable. |

Validation rules:

1. Discovery-server mode must emit enough per-server diagnostics to identify slow/unreachable servers (FR-019).
2. Diagnostics are retained for blocker reporting (SC-005).

## Relationships

```text
DiscoveryModeSnapshot 1 --- 1 DiscoveryRunResult
DiscoveryRunResult 1 --- * DiscoveredSourceEndpoint
DiscoveryRunResult 1 --- * DiscoveryServerDiagnosticRecord
DiscoveredSourceEndpoint * --- 1 CachedSourceRecord (upsert/merge by canonicalSourceId)
```

## State Transitions

```text
RUN_START
  -> Mode selected from enabledServerCount
    -> MULTICAST path OR DISCOVERY_SERVER path
      -> SUCCESS (sources persisted/merged)
      -> TIMEOUT (diagnostic reason, no same-run fallback)
      -> FAILURE (diagnostic reason, no same-run fallback)
```

## Persistence and Reporting Notes

1. Cache updates preserve existing continuity metadata while applying latest endpoint/timestamp.
2. Run and per-server diagnostics must be available to distinguish environment blockers from code failures in validation reporting.
3. Cached rows remain visible on relaunch while live discovery/validation proceeds (SC-004).
