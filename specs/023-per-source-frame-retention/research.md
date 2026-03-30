# Research: Per-Source Last Frame Retention

**Branch**: `023-per-source-frame-retention` | **Date**: 2026-03-30

## Status: Complete — all NEEDS CLARIFICATION resolved

---

## R-001: In-Memory LRU Map in Kotlin/Android

**Decision**: Use a `LinkedHashMap` with `accessOrder = true`, wrapped in a
`Mutex`-guarded class, with `removeEldestEntry` overridden to cap at 10.

**Rationale**: `LinkedHashMap(initialCapacity, loadFactor, accessOrder=true)`
is the idiomatic JVM LRU container. Access-order mode moves the most-recently
used entry to the tail and the eldest (least-recently used) entry to the head.
Overriding `removeEldestEntry { size > MAX_ENTRIES }` provides O(1) capped
eviction without external dependencies. Wrapping in a `Mutex` makes it safe for
coroutine-concurrent access (viewer disconnect on IO dispatcher, source list
observation on Unconfined/Main).

**Alternatives considered**:

| Alternative | Reason NOT chosen |
|---|---|
| `androidx.collection.LruCache` | Keyed on value size in bytes, not entry count; requires estimating byte cost; overkill for a simple 10-entry count cap |
| `ConcurrentHashMap` + manual eviction | Does not natively maintain LRU order; would require a separate access-time tracker |
| Room persistence | Spec explicitly requires session-only (in-memory); Room would survive restarts unnecessarily and require a migration |
| `androidx.collection.ArrayMap` | No built-in LRU eviction; requires manual implementation equivalent to `LinkedHashMap` approach |

---

## R-002: Thumbnail Frame Capture and Scaling

**Decision**: Scale `ViewerVideoFrame` ARGB pixels to a ~320×180 `Bitmap` using
`Bitmap.createScaledBitmap` on `Dispatchers.IO` before writing to the session
cache directory as PNG.

**Rationale**:
- `ViewerVideoFrame.argbPixels` (IntArray) + `width`/`height` is the existing
  frame representation already used throughout the codebase
- `Bitmap.createBitmap(argbPixels, width, height, ARGB_8888)` then
  `Bitmap.createScaledBitmap(bitmap, thumbW, thumbH, true)` is idiomatic,
  already proven in `ViewerContinuityRepositoryImpl`
- `~320×180` at 4 bytes/pixel ≈ 225 KB per frame; 10 frames ≈ 2.25 MB — well
  within mobile memory budgets
- The target thumbnail dimensions are only approximate: the implementation
  should preserve the stream's native aspect ratio, using ~320 as the max
  width, and compute height accordingly

**Frame write path**:
1. On `Dispatchers.IO`, decode frame → create full-res Bitmap → scale to thumbnail → compress PNG → write to `cacheDir/ndi-session-previews/{sanitizedSourceId}.png`
2. Overwrite the file if it already exists for that sourceId (always one file per source)
3. On LRU eviction, delete the evicted source's PNG file
4. The in-memory map stores `sourceId → absoluteFilePath`

**Alternatives considered**:

| Alternative | Reason NOT chosen |
|---|---|
| JPEG instead of PNG | PNG is already the established format in `ViewerContinuityRepositoryImpl`; consistency preferred |
| Keep full-resolution Bitmap in memory | At 1920×1080 ARGB that is ~8 MB per source × 10 = 80 MB; unacceptable |
| Glide/Coil in-memory cache | Adds a library dependency just for a 10-entry thumb cache; not justified |
| Write to `filesDir` instead of `cacheDir` | `cacheDir` is OS-eligible for cleanup under storage pressure; appropriate for session-only data |

---

## R-003: Session Scope — Relationship with ViewerContinuityRepository

**Decision**: `PerSourceFrameRepository` is a **new, independent** domain
interface and in-memory only implementation. It does NOT replace or modify
`ViewerContinuityRepository`. Both coexist:

| Concern | Repository | Storage |
|---|---|---|
| Per-source source-list thumbnails | `PerSourceFrameRepository` (**new**) | In-memory LRU + session cache PNG files |
| Last-viewed-source restore on relaunch | `ViewerContinuityRepository` (existing) | Room `last_viewed_context` table + persistent PNG |

`NdiViewerRepositoryImpl.disconnectFromSource()` already calls
`persistViewerContinuity` for the restore path. It will additionally call
`perSourceFrameRepository.saveFrameForSource(sourceId, latestFrame)`.

**Rationale**: Merging the two concerns would require mutating the existing
`ViewerContinuityRepository` interface and its Room-backed implementation,
risking a regression in the relaunch-restore user story (US2, spec-001). Keeping
them separate follows the Single Responsibility Principle and respects the
existing tests.

---

## R-004: SourceListViewModel Integration Pattern

**Decision**: Replace the current `observeLastViewedContext()` collector in
`SourceListViewModel` with a new collector over
`perSourceFrameRepository.observeFrameMap(): Flow<Map<String, String>>`.

**Current code path** (to change):
```kotlin
// SourceListViewModel.init block — current single-context observer
viewModelScope.launch {
    SourceListDependencies.viewerContinuityRepositoryOrNull()
        ?.observeLastViewedContext()
        ?.collect { context ->
            val previewMap = buildMap {
                val sourceId = context?.sourceId
                val previewPath = context?.lastFrameImagePath
                if (!sourceId.isNullOrBlank() && !previewPath.isNullOrBlank()
                    && File(previewPath).exists()) {
                    put(sourceId, previewPath)
                }
            }
            lastViewedPreviewBySourceId.value = previewMap
            // ... update uiState
        }
}
```

**New code path**:
```kotlin
// SourceListViewModel.init block — per-source map observer
viewModelScope.launch {
    SourceListDependencies.perSourceFrameRepositoryOrNull()
        ?.observeFrameMap()
        ?.collect { frameMap ->
            lastViewedPreviewBySourceId.value = frameMap
            // ... update uiState
        }
}
```

The `enrichSourcesWithAvailability()` function and the `SourceAdapter` bitmap
loading are **unchanged** — they already consume `lastViewedPreviewBySourceId`
as a `Map<String, String>`.

**Rationale**: Minimal diff; no adapter or layout changes needed; the ViewModel
is the only touch point for the source list change.

---

## R-005: PerSourceFrameRepository Eviction Callback

**Decision**: When the LRU map evicts an entry (capacity > 10), the
`PerSourceFrameRepositoryImpl` synchronously deletes the corresponding session
PNG file. This prevents orphaned temporary files accumulating in `cacheDir`
during long sessions.

**Rationale**: The file path is in the map value at eviction time; deletion is
a single `File.delete()` call, negligible overhead. No user-visible side effect
— the source whose entry was evicted will simply show the placeholder again if
it reappears in the list.

---

## R-006: SourceListDependencies Access Pattern

**Decision**: Expose `PerSourceFrameRepository` through `SourceListDependencies`
using the existing nullable factory pattern (same pattern as
`viewerContinuityRepositoryOrNull()`). Wire the singleton instance in
`AppGraph`.

**Rationale**: Consistent with the existing service-locator approach used
throughout the feature presentation layer. No DI framework changes needed.
`AppGraph` already creates and wires all repositories; simply add:
```kotlin
val perSourceFrameRepository: PerSourceFrameRepository = PerSourceFrameRepositoryImpl(...)
```
and register it in `SourceListDependencies`.

---

## Summary of All Resolved Unknowns

| Unknown | Resolution |
|---|---|
| In-memory LRU mechanism | `LinkedHashMap(accessOrder=true)` capped at 10, `Mutex`-guarded |
| Frame capture moment | At `disconnectFromSource()` using `bridge.getLatestReceiverFrame()` (already called) |
| Thumbnail resolution | ~320×(height preserving aspect ratio), PNG in `cacheDir/ndi-session-previews/` |
| Session scope | In-memory only; discarded on process end; no Room migration |
| Source identifier key | `NdiSource.sourceId` / `ViewerSession.selectedSourceId` (existing SDK-assigned opaque ID) |
| Coexistence with ViewerContinuityRepository | Independent — both paths preserved |
| ViewModel integration | Replace single-context observer with per-source map observer |
| SourceListDependencies wiring | New `perSourceFrameRepositoryOrNull()` factory, wired in `AppGraph` |
| Eviction side-effect | Delete corresponding PNG file from `cacheDir` |
