# Developer Quickstart: Per-Source Last Frame Retention

**Branch**: `023-per-source-frame-retention` | **Date**: 2026-03-30

---

## Overview

This guide describes the implementation plan for replacing the single-slot
global last-frame store with a per-source LRU thumbnail cache so that every
viewed NDI source retains its own last frame in the source list.

**No new Gradle modules. No Room migrations. No new UI layouts.**

---

## What Changes Where

| Layer | File | Change |
|---|---|---|
| Domain | `NdiRepositories.kt` | Add `PerSourceFrameRepository` interface |
| Data | `PerSourceFrameRepositoryImpl.kt` | **New file** — LRU in-memory impl |
| Data | `NdiViewerRepositoryImpl.kt` | Call `perSourceFrameRepo.saveFrameForSource()` in `disconnectFromSource()` |
| Presentation | `SourceListViewModel.kt` | Replace single-context observer with `observeFrameMap()` collector |
| App wiring | `AppGraph.kt` | Instantiate and inject `PerSourceFrameRepositoryImpl` |
| App wiring | `SourceListDependencies.kt` | Expose `perSourceFrameRepositoryOrNull()` factory |
| Tests (first) | `PerSourceFrameRepositoryImplTest.kt` | **New file** — TDD unit tests |
| Tests (first) | `SourceListViewModelFrameRetentionTest.kt` | **New file** — ViewModel unit tests |
| e2e | `per-source-frame-retention.spec.ts` | **New file** — Playwright multi-source e2e |

---

## Step 1: Write Failing Tests First (TDD Red Phase)

Before writing any production code, create the two unit test files.

### `PerSourceFrameRepositoryImplTest.kt`

Key tests to write:
- `saveFrameForSource with valid frame stores entry in map`
- `saveFrameForSource with null frame is no-op`
- `saveFrameForSource with blank sourceId is no-op`
- `saveFrameForSource for same source updates existing entry`
- `saveFrameForSource with 11 frames evicts least-recently-viewed`
- `getFramePathForSource returns null for unseen source`
- `getFramePathForSource returns path for seen source`
- `observeFrameMap emits empty map initially`
- `observeFrameMap emits updated map after save`
- `clearAll removes all entries and emits empty map`

### `SourceListViewModelFrameRetentionTest.kt`

Key tests:
- `sources enriched with per-source frame path from repository`
- `multiple sources each have independent frame path`
- `source without frame has null lastFramePreviewPath`
- `frame map update triggers source list re-enrichment`

---

## Step 2: Add `PerSourceFrameRepository` to Domain

In `NdiRepositories.kt`, add the interface (see contract in
`contracts/per-source-frame-repository.md`).

```kotlin
interface PerSourceFrameRepository {
    suspend fun saveFrameForSource(sourceId: String, frame: ViewerVideoFrame?)
    suspend fun getFramePathForSource(sourceId: String): String?
    fun observeFrameMap(): StateFlow<Map<String, String>>
    suspend fun clearAll()
    companion object { const val MAX_RETAINED_SOURCES = 10 }
}
```

---

## Step 3: Implement `PerSourceFrameRepositoryImpl`

New file in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/`.

**Skeleton**:

```kotlin
class PerSourceFrameRepositoryImpl(
    private val sessionCacheDir: File,
) : PerSourceFrameRepository {

    private val mutex = Mutex()
    private val _frameMap = MutableStateFlow<Map<String, String>>(emptyMap())
    override fun observeFrameMap(): StateFlow<Map<String, String>> = _frameMap.asStateFlow()

    // accessOrder=true → LRU order; removeEldestEntry caps at MAX
    private val lruMap = object : LinkedHashMap<String, String>(16, 0.75f, true) {
        override fun removeEldestEntry(eldest: Map.Entry<String, String>): Boolean {
            val shouldEvict = size > MAX_RETAINED_SOURCES
            if (shouldEvict) {
                runCatching { File(eldest.value).delete() }
            }
            return shouldEvict
        }
    }

    override suspend fun saveFrameForSource(sourceId: String, frame: ViewerVideoFrame?) {
        if (sourceId.isBlank() || frame == null) return
        if (frame.width <= 0 || frame.height <= 0 || frame.argbPixels.isEmpty()) return

        val path = withContext(Dispatchers.IO) {
            writeThumbnail(sourceId, frame)
        } ?: return

        mutex.withLock {
            lruMap[sourceId] = path
            _frameMap.value = lruMap.toMap()
        }
    }

    // ... writeThumbnail: create Bitmap, scale to ~320×height, write PNG
}
```

**Thumbnail scaling** (inside `writeThumbnail`):
```kotlin
val bitmap = Bitmap.createBitmap(frame.argbPixels, frame.width, frame.height, ARGB_8888)
val scale = MAX_THUMB_WIDTH.toFloat() / frame.width.toFloat()
val thumbW = (frame.width * scale.coerceAtMost(1f)).toInt()
val thumbH = (frame.height * scale.coerceAtMost(1f)).toInt()
val thumb = Bitmap.createScaledBitmap(bitmap, thumbW, thumbH, true)
bitmap.recycle()
val outFile = File(sessionCacheDir, "preview_${sanitize(sourceId)}.png")
outFile.parentFile?.mkdirs()
outFile.outputStream().use { thumb.compress(PNG, 100, it) }
thumb.recycle()
outFile.absolutePath
```

where `MAX_THUMB_WIDTH = 320`.

---

## Step 4: Update `NdiViewerRepositoryImpl.disconnectFromSource()`

In the existing `disconnectFromSource()` block, after capturing `latestFrameBeforeStop` and before or after calling `persistViewerContinuity`, add:

```kotlin
if (activeSourceId.isNotBlank()) {
    perSourceFrameRepository?.saveFrameForSource(activeSourceId, latestFrameBeforeStop)
}
```

`perSourceFrameRepository` is injected as a nullable constructor parameter
(same pattern as `viewerContinuityRepository`).

---

## Step 5: Update `SourceListViewModel`

Replace the existing `observeLastViewedContext()` collector:

```kotlin
// REMOVE:
viewModelScope.launch {
    SourceListDependencies.viewerContinuityRepositoryOrNull()
        ?.observeLastViewedContext()
        ?.collect { context -> ... }
}

// ADD:
viewModelScope.launch {
    SourceListDependencies.perSourceFrameRepositoryOrNull()
        ?.observeFrameMap()
        ?.collect { frameMap ->
            lastViewedPreviewBySourceId.value = frameMap
            val enrichedSources = enrichSourcesWithAvailability()
            _uiState.update { current -> current.copy(sources = enrichedSources) }
        }
}
```

Note: `lastViewedPreviewBySourceId` already has type `MutableStateFlow<Map<String,String>>` — no type change needed.

---

## Step 6: Wire in `AppGraph` and `SourceListDependencies`

In `AppGraph.kt`:
```kotlin
val perSourceFrameRepository: PerSourceFrameRepository =
    PerSourceFrameRepositoryImpl(sessionCacheDir = File(context.cacheDir, "ndi-session-previews"))
```

In `SourceListDependencies`:
```kotlin
fun perSourceFrameRepositoryOrNull(): PerSourceFrameRepository? =
    AppGraph.perSourceFrameRepository
```

In `NdiViewerRepositoryImpl.Factory` or constructor injection:
```kotlin
// Add optional parameter:
private val perSourceFrameRepository: PerSourceFrameRepository? = null,
```

---

## Validation Checklist

Before marking each task done:

- [ ] Failing unit tests exist and fail (Red phase complete)
- [ ] `PerSourceFrameRepositoryImpl` unit tests pass (Green phase)
- [ ] `SourceListViewModelFrameRetentionTest` passes
- [ ] `ViewerContinuityRepositoryImpl` existing tests still pass (regression)
- [ ] `./gradlew :feature:ndi-browser:data:test` passes
- [ ] `./gradlew :feature:ndi-browser:presentation:test` passes
- [ ] `scripts/verify-e2e-dual-emulator-prereqs.ps1` passes (preflight)
- [ ] Playwright e2e: two sources show independent thumbnails
- [ ] Playwright e2e: existing e2e suite still passes
- [ ] `./gradlew assembleRelease verifyReleaseHardening` passes (R8/ProGuard)

---

## Environment Requirements

- Two emulators or one emulator + one device, both on the same network segment
- NDI SDK prerequisites: `scripts/verify-android-prereqs.ps1`
- Dual-emulator preflight: `scripts/verify-e2e-dual-emulator-prereqs.ps1`
- If only one NDI source is available: record multi-source e2e as **blocked (environment)**
