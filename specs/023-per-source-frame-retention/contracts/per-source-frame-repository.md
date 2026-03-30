# Contract: PerSourceFrameRepository

**Branch**: `023-per-source-frame-retention` | **Date**: 2026-03-30  
**Module**: `:feature:ndi-browser:domain`  
**Interface file**: `feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt`

---

## Purpose

`PerSourceFrameRepository` is the domain boundary for session-scoped per-source
thumbnail frame management. It replaces the previous implicit behavior where
`ViewerContinuityRepository` only kept one frame for the last-viewed source.

---

## Interface: `PerSourceFrameRepository`

```kotlin
/**
 * Session-scoped, in-memory store of the last captured thumbnail frame
 * for each NDI source viewed at least once. Keyed by NDI SDK-assigned
 * opaque source ID. Capped at [MAX_RETAINED_SOURCES] entries (LRU eviction).
 */
interface PerSourceFrameRepository {

    /**
     * Save or update the thumbnail frame for [sourceId].
     * If [frame] is null or has invalid dimensions, the call is a no-op.
     * Evicts the least-recently-viewed source entry when the cap is exceeded.
     *
     * Must be called from a coroutine context (suspending).
     */
    suspend fun saveFrameForSource(sourceId: String, frame: ViewerVideoFrame?)

    /**
     * Returns the absolute file path of the retained thumbnail PNG for
     * [sourceId], or null if no frame has been captured for that source.
     */
    suspend fun getFramePathForSource(sourceId: String): String?

    /**
     * Returns a hot [StateFlow] that emits the current map of
     * sourceId → thumbnailFilePath for all retained sources.
     * Emits on every save or eviction.
     */
    fun observeFrameMap(): StateFlow<Map<String, String>>

    /**
     * Clears all retained frames and deletes associated session cache files.
     * Called on app data clear.
     */
    suspend fun clearAll()

    companion object {
        /** Maximum number of per-source frames held in memory simultaneously. */
        const val MAX_RETAINED_SOURCES = 10
    }
}
```

---

## Method Contracts

### `saveFrameForSource(sourceId, frame)`

| Precondition | Outcome |
|---|---|
| `sourceId` is blank | No-op; map unchanged |
| `frame` is null | No-op; existing entry for sourceId (if any) preserved |
| `frame.width ≤ 0` or `frame.height ≤ 0` or `frame.argbPixels.isEmpty()` | No-op |
| Valid `sourceId` + valid `frame`, map size < 10 | Entry added; `observeFrameMap()` emits updated map |
| Valid `sourceId` + valid `frame`, map size == 10, sourceId is new | LRU entry evicted (its PNG deleted); new entry added; `observeFrameMap()` emits |
| Valid `sourceId` + valid `frame`, sourceId already in map | Entry updated (promoted to MRU); PNG overwritten; `observeFrameMap()` emits |

### `getFramePathForSource(sourceId)`

- Returns `String` if sourceId has a retained frame and the file exists.
- Returns `null` if no frame has been captured, if the sourceId is blank, or if the PNG file was deleted externally.

### `observeFrameMap()`

- Emits `Map<String, String>` where key = sourceId, value = absolute PNG file path.
- Emits immediately on collection with the current state (hot `StateFlow`).
- Never emits a map containing a path to a non-existent file.

### `clearAll()`

- Removes all entries from the LRU map.
- Deletes all PNG files from the session cache directory.
- `observeFrameMap()` emits an empty map.

---

## Implementation Contract

| Property | Value |
|---|---|
| Backing structure | `LinkedHashMap(16, 0.75f, accessOrder = true)` |
| Thread-safety | Kotlin `Mutex` wrapping all LRU map accesses |
| Dispatcher | All file I/O on `Dispatchers.IO` |
| PNG path pattern | `{cacheDir}/ndi-session-previews/{sanitizedSourceId}.png` |
| Thumbnail dimensions | Width = min(sourceWidth, 320); height preserves aspect ratio |
| Lifecycle | Singleton within the app process — discarded on process termination |

---

## Callers

| Caller | Method called | When |
|---|---|---|
| `NdiViewerRepositoryImpl.disconnectFromSource()` | `saveFrameForSource(sourceId, latestFrame)` | After `bridge.stopReceiver()`, using `bridge.getLatestReceiverFrame()` result |
| `SourceListViewModel.init` | `observeFrameMap()` | On ViewModel creation; collected in `viewModelScope` |
| `AppGraph` cleanup path | `clearAll()` | On app data clear (same as `ViewerContinuityRepository.resetPersistedStateOnAppDataClear`) |

---

## Non-Goals

- Persisting frames across full app restarts (out of scope; see Assumptions in spec)
- Storing more than the last frame per source (no frame history)
- Sharing frame data with `ViewerContinuityRepository` (independent concerns)
- Reporting eviction events to the UI (silent eviction; placeholder shown)
