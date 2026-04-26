# Data Model: Persistent Source Cache

**Branch**: `030-persist-source-cache` | **Date**: 2026-04-19

## New Entities

### CachedSourceEntity

Represents one canonical persisted NDI source available for cached display, validation, and endpoint resolution.

| Field | Type | Description |
|---|---|---|
| `cacheKey` | `String` | Primary key. Computed as stable SDK/source ID when available; otherwise normalized `host:port` endpoint string. |
| `stableSourceId` | `String?` | SDK/source identifier when present in discovery results. |
| `lastObservedSourceId` | `String?` | Most recent runtime `NdiSource.sourceId` used by the current discovery implementation. |
| `displayName` | `String` | Last announced display name. Never used as canonical identity. |
| `endpointHost` | `String` | Last known stream host/IP announced for the source. |
| `endpointPort` | `Int` | Last known stream port announced for the source. |
| `endpointKey` | `String` | Normalized `host:port` string retained for diagnostics and fallback identity checks. |
| `validationState` | `CachedSourceValidationState` | Persisted validation state: `NOT_YET_VALIDATED`, `VALIDATING`, `AVAILABLE`, `UNAVAILABLE`. |
| `lastAvailableAtEpochMillis` | `Long?` | Last time the source was positively confirmed reachable. |
| `lastValidatedAtEpochMillis` | `Long?` | Last time validation completed for this row. |
| `lastValidationStartedAtEpochMillis` | `Long?` | Last time startup/foreground validation began. |
| `firstCachedAtEpochMillis` | `Long` | First time the source entered the cache. |
| `lastDiscoveredAtEpochMillis` | `Long` | Last time discovery returned metadata for this row. |
| `retainedPreviewImagePath` | `String?` | App-internal file path for the latest retained preview image. |
| `lastPreviewCapturedAtEpochMillis` | `Long?` | Timestamp for the retained preview file. |
| `updatedAtEpochMillis` | `Long` | Last mutation timestamp for the row. |

Validation rules:

1. `cacheKey` must be non-blank and deterministic from the canonical identity rule.
2. `displayName` may change across discoveries without changing identity.
3. `endpointHost` and `endpointPort` must reflect the actual source endpoint, never the discovery-server endpoint.
4. Missing or unreadable preview files must not invalidate the row; `retainedPreviewImagePath` may be nulled during cleanup.

### CachedSourceDiscoveryServerCrossRef

Represents the association between one cached source and one persisted discovery-server entry.

| Field | Type | Description |
|---|---|---|
| `cacheKey` | `String` | Foreign key to `CachedSourceEntity.cacheKey`. |
| `discoveryServerId` | `String` | Foreign key to existing `DiscoveryServerEntity.id`. |
| `firstObservedAtEpochMillis` | `Long` | First time this server announced the source. |
| `lastObservedAtEpochMillis` | `Long` | Most recent time this server announced the source. |

Composite primary key: `cacheKey + discoveryServerId`

Notes:

- A source may be associated with zero discovery servers when discovered via default-network discovery.
- Multiple enabled discovery servers may map to the same cached source row.

### CachedSourceRecord

Repository/domain projection consumed by ViewModels and diagnostics.

| Field | Type | Description |
|---|---|---|
| `cacheKey` | `String` | Canonical identifier exposed to app logic. |
| `displayName` | `String` | Cached presentation label. |
| `endpointAddress` | `String` | Derived from `endpointHost:endpointPort`. |
| `validationState` | `CachedSourceValidationState` | UI-facing availability/validation status. |
| `retainedPreviewImagePath` | `String?` | Preview file reference for list/dashboard rendering. |
| `lastAvailableAtEpochMillis` | `Long?` | Recency information for operator context. |
| `lastValidatedAtEpochMillis` | `Long?` | Used for diagnostics and stale-state display. |
| `discoveryServerIds` | `List<String>` | Persisted discovery-server provenance. |

### CachedSourceValidationState

Enum persisted in Room and mapped into UI state.

| Value | Meaning |
|---|---|
| `NOT_YET_VALIDATED` | Row exists from prior persistence but has not yet been validated in the current app session. |
| `VALIDATING` | Live validation is currently running; View action must be disabled. |
| `AVAILABLE` | Validation completed successfully and the source can be opened. |
| `UNAVAILABLE` | Validation completed and the source is not currently reachable; row remains visible as cached context. |

## Existing Entities Reused

| Entity | Location | Relationship to this feature |
|---|---|---|
| `LastViewedContextEntity` | `core/database` | Continues to store the last restored viewer context; should reference a cached-source row by canonical identity or latest source identifier. |
| `ConnectionHistoryStateEntity` | `core/database` | Continues to track first/last successful frame history and can enrich cached-source presentation. |
| `UserSelectionEntity` | `core/database` | Continues to store last selected source and should align with canonical cached-source identity where possible. |
| `DiscoveryServerEntity` | `core/database` | Remains the authoritative list of configured discovery servers used by the cross-reference table. |

## Relationships

```text
DiscoveryServerEntity 1 --- * CachedSourceDiscoveryServerCrossRef * --- 1 CachedSourceEntity

CachedSourceEntity 1 --- 0..1 LastViewedContextEntity
CachedSourceEntity 1 --- 0..1 UserSelectionEntity (selected source mapping)
CachedSourceEntity 1 --- 0..1 ConnectionHistoryStateEntity
```

## State Transitions

```text
Never discovered
    |
    | discovery returns metadata
    v
CachedSourceEntity created with NOT_YET_VALIDATED
    |
    | app enters View/Home or foreground refresh starts
    v
VALIDATING
  |         |
  |         | validation fails / source absent after debounce
  |         v
  |      UNAVAILABLE
  |
  | validation succeeds
  v
AVAILABLE
  |
  | later refresh marks source stale/unreachable
  v
UNAVAILABLE
  |
  | source rediscovered
  v
VALIDATING -> AVAILABLE
```

## Persistence and Migration Notes

1. Add a Room migration from the current database version to a new version that creates `cached_sources` and the cross-reference table.
2. Do not delete or repurpose existing continuity tables; this feature complements them.
3. Existing preview capture paths can be reused, but persisted cache rows must tolerate missing files after cleanup or reinstall restore.
4. Source rediscovery updates the existing row in place based on canonical identity instead of inserting duplicates.