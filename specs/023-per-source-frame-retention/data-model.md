# Data Model: Per-Source Last Frame Retention

**Branch**: `023-per-source-frame-retention` | **Date**: 2026-03-30

---

## Entities

### SourceFrameEntry

Represents one retained thumbnail for a single viewed NDI source within the
current session.

| Field | Type | Description |
|---|---|---|
| `sourceId` | `String` | NDI SDK-assigned opaque source identifier (map key) |
| `thumbnailFilePath` | `String` | Absolute path to the session-cache PNG file written for this source |
| `capturedAtEpochMillis` | `Long` | **INTERNAL ONLY** — Epoch millisecond timestamp when the frame was captured; used for LRU ordering in LinkedHashMap and NOT exposed in the public `observeFrameMap()` API |

**Notes**:
- This entity exists **only in memory** (the LRU map). It is never persisted to Room.
- `sourceId` is the existing `NdiSource.sourceId` / `ViewerSession.selectedSourceId`.
- The PNG file at `thumbnailFilePath` is stored in the app's `cacheDir` under
  `ndi-session-previews/{sanitizedSourceId}.png`. It is overwritten on
  subsequent captures for the same source.
- On LRU eviction (when map size exceeds 10), the PNG file is deleted.

---

### PerSourceFrameMap (in-memory)

A session-scoped `LinkedHashMap<String, SourceFrameEntry>` (access-order = true)
representing all currently retained per-source frames.

| Property | Value |
|---|---|
| Max entries | 10 |
| Eviction policy | Least-recently-viewed (LRU) |
| Eviction side-effect | Delete evicted source's PNG from `cacheDir` |
| Thread-safety | Kotlin `Mutex` — all reads and writes are `withLock` |
| Observed as | `StateFlow<Map<String, String>>` (sourceId → thumbnailFilePath) |

---

## State Transitions

```
Source never viewed
        │
        │  User opens viewer for sourceId
        ▼
Viewer session active (frame captured live in relay loop)
        │
        │  User exits viewer (disconnectFromSource called)
        ▼
Frame captured: bridge.getLatestReceiverFrame()
        │
        │  Scale to thumbnail (~320×height) → write PNG → update LRU map
        ▼
SourceFrameEntry stored in PerSourceFrameMap
        │                          │
        │                          │  Source > 10th in LRU order
        │                          ▼
        │                  Eldest entry evicted → PNG deleted
        ▼
observeFrameMap() emits updated Map<String, String>
        │
        ▼
SourceListViewModel.lastViewedPreviewBySourceId updated
        │
        ▼
enrichSourcesWithAvailability() → source.lastFramePreviewPath populated
        │
        ▼
SourceAdapter renders thumbnail in source row
```

---

## Existing Entities (Unchanged)

The following existing entities are **not modified** by this feature:

| Entity | Location | Reason Unchanged |
|---|---|---|
| `LastViewedContextEntity` | `core/database` Room table | Single-source restore-on-relaunch path; independent concern |
| `NdiSource.lastFramePreviewPath` | `core/model` | Already present; ViewModel populates it from the new map |
| `ViewerSession` | `core/model` | No change — session tracking is separate from frame retention |
| `ConnectionHistoryStateEntity` | `core/database` | History tracking for "previously connected" badge; independent |

---

## Relationships

```
PerSourceFrameMap  ──── (keyed by) ──── sourceId  ◄──── NdiSource.sourceId
                   ──── (reads from) ── NdiViewerRepositoryImpl (on disconnect)
                   ──── (observed by) ─ SourceListViewModel → SourceListUiState
                   ──── (rendered by) ─ SourceAdapter via NdiSource.lastFramePreviewPath

ViewerContinuityRepository  ──── (separate concern: relaunch restore)
Room: last_viewed_context   ──── (unchanged: single-row persist across restarts)
```

---

## Validation Rules

1. A `SourceFrameEntry` is only created when `frame != null` and
   `frame.width > 0` and `frame.height > 0` and `frame.argbPixels.isNotEmpty()`.
2. If `bridge.getLatestReceiverFrame()` returns `null` at disconnect time,
   no entry is created (or the existing entry for that source is kept).
3. `sourceId` must be non-blank; blank sourceIds are silently ignored.
4. The LRU map MUST enforce the 10-entry cap before returning from `saveFrame`.
