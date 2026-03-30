# Tasks: Per-Source Last Frame Retention

**Input**: Design documents from `/specs/023-per-source-frame-retention/`  
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓

**Constitution**: TDD enforced (Red-Green-Refactor). Unit tests (JUnit), Playwright e2e on dual emulators, release hardening validation. All visual UI changes verified with existing Playwright e2e regression.

**Organization**: Tasks grouped by user story (US1, US2, US3) to enable independent implementation and testing.

---

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Verify runtime dependencies ready before implementation gates

- [X] T000 Run `scripts/verify-android-prereqs.ps1` and record output in `test-results/023-preflight.md`
- [ ] T001 Run `scripts/verify-e2e-dual-emulator-prereqs.ps1` and confirm two emulators/devices are live with NDI discovery reachable; record evidence in `test-results/023-preflight.md` including exact emulator serial numbers, NDI source display names, and IP addresses for reproduction troubleshooting

**Checkpoint**: Environment confirmed ready or blocked with explicit unblocking step

---

## Phase 1: Setup (Foundational Blocking Prerequisite)

**Purpose**: Core domain/data/wiring infrastructure before any user story implementation

**⚠️ CRITICAL**: NO user story work begins until all Phase 1 tasks complete

### Tests First (TDD Red Phase) ⚠️

- [X] [P] T002 Create failing unit test file `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/PerSourceFrameRepositoryImplTest.kt` with 10 test stubs: `saveFrameForSource_validFrame_storesEntry`, `saveFrameForSource_nullFrame_noOp`, `saveFrameForSource_blankSourceId_noOp`, `saveFrameForSource_sameSource_updateExisting`, `saveFrameForSource_11Frames_evictsLRU`, `getFramePathForSource_unseenSource_returnsNull`, `getFramePathForSource_seenSource_returnsPath`, `observeFrameMap_initialEmission_emitsEmptyMap`, `observeFrameMap_afterSave_emitsUpdatedMap`, `clearAll_removesEntriesAndEmitsEmpty` (all failing)
- [X] [P] T003 Create failing unit test file `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelFrameRetentionTest.kt` with 4 test stubs: `sourcesEnriched_withFramePath`, `multipleSources_independentFramePaths`, `sourceWithoutFrame_nullPath`, `frameMapUpdate_triggersReenrichment` (all failing)

### Domain Interface

- [X] T004 Add `PerSourceFrameRepository` interface to `feature/ndi-browser/domain`/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt` with methods: `suspend fun saveFrameForSource(sourceId: String, frame: ViewerVideoFrame?)`, `suspend fun getFramePathForSource(sourceId: String): String?`, `fun observeFrameMap(): StateFlow<Map<String, String>>`, `suspend fun clearAll()`, and companion `MAX_RETAINED_SOURCES = 10`

### Implementation (TDD Green Phase)

- [X] [P] T005 Implement `PerSourceFrameRepositoryImpl` in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/PerSourceFrameRepositoryImpl.kt` with: `LinkedHashMap(16, 0.75f, accessOrder=true)` LRU backing store, `Mutex` for thread-safety, `removeEldestEntry` override for 10-entry cap + PNG file deletion on eviction, `writeThumbnail` helper to scale frame to ~320×height on `Dispatchers.IO`, session cache directory path `{cacheDir}/ndi-session-previews/`
- [X] [P] T006 Run and pass failing tests in `PerSourceFrameRepositoryImplTest.kt` (TDD Green)
- [X] [P] T007 Run and pass failing tests in `SourceListViewModelFrameRetentionTest.kt` (TDD Green)

### Integration with Existing Components

- [X] T008 [P] Modify `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiViewerRepositoryImpl.kt`: Add constructor parameter `perSourceFrameRepository: PerSourceFrameRepository?` and call `perSourceFrameRepository?.saveFrameForSource(activeSourceId, latestFrameBeforeStop)` in `disconnectFromSource()` after capturing `latestFrameBeforeStop` from `bridge.getLatestReceiverFrame()`. **CRITICAL**: `saveFrameForSource()` MUST be called AFTER `persistViewerContinuity()` completes to prevent cache file race conditions.
- [X] T009 Modify `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt`: Replace the existing `observeLastViewedContext()` collector block with new collector over `perSourceFrameRepository?.observeFrameMap()` that updates `lastViewedPreviewBySourceId` and re-enriches sources

### App Wiring

- [X] [P] T010 Modify `app/src/main/java/com/ndi/app/di/AppGraph.kt`: Add singleton instance `val perSourceFrameRepository: PerSourceFrameRepository = PerSourceFrameRepositoryImpl(sessionCacheDir = File(context.cacheDir, "ndi-session-previews"))`
- [X] [P] T011 Modify `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListDependencies.kt`: Add factory `fun perSourceFrameRepositoryOrNull(): PerSourceFrameRepository? = AppGraph.perSourceFrameRepository`
- [X] T012 Verify `AppGraph` passes compilation and all constructor injections work; run `./gradlew :app:compileDebugKotlin`

### TDD Refactor Phase

- [X] T013 Code review and refactor `PerSourceFrameRepositoryImpl` for clarity, thread-safety verification, and bitmap lifecycle correctness (recycle timely, no memory leaks)

**Checkpoint**: Foundation complete. All Phase 1 unit tests pass. Core repository, ViewModel integration, and app wiring ready. No visual behavior yet — only backend structure.

---

## Phase 2: User Story 1 — See Last Frame Per Source in Source List (Priority: P1) 🎯 MVP

**Goal**: Display a unique retained thumbnail for each previously-viewed NDI source in the source list, replacing the single global last-frame behavior.

**Independent Test**: View two distinct sources in sequence, return to the source list, confirm each source row shows its own last-captured frame thumbnail.

### Tests for User Story 1 (REQUIRED - TDD Red Phase) ⚠️

- [ ] [P] T014 Create failing Playwright e2e test file `testing/e2e/tests/023-per-source-frame-retention.spec.ts` with test `multi-source frames retained independently in list` that: opens viewer for source A, captures a frame, exits viewer, opens viewer for source B, captures a different frame, exits viewer, navigates to source list, verifies both sources have distinct thumbnails (and frame for A is still not replaced by frame from B)
- [ ] [P] T015 Create failing Playwright e2e test `source never viewed shows placeholder` that: loads source list, confirms any source that has never been viewed shows null or placeholder image (no frame)
- [ ] T016 Create failing test in `test-results/023-regression.md` recording that existing Playwright e2e suite must run and pass (template: "**BLOCKED (environment)** — Two NDI sources not available. Unblock by: starting dual emulator or physical device with NDI source available.")

### Implementation for User Story 1

- [X] T017 Verify `SourceAdapter` in `feature/ndi-browser/presentation`/src/main/java/com/ndi/feature/ndibrowser/source_list/adapter/SourceAdapter.kt` already renders `source.lastFramePreviewPath` as thumbnail via `sourcePreviewImage.setImageBitmap(BitmapFactory.decodeFile(previewPath))` — no changes needed if already present, else add the bitmap loading logic
- [ ] [P] T018 End-to-end verification: Build and deploy debug APK on dual emulator via `./gradlew assembleDebug`
- [ ] [P] T019 Run Playwright e2e test `multi-source frames retained independently in list` on emulator pair; record results in `test-results/023-us1-multi-frame.md`
- [ ] [P] T020 Run Playwright e2e test `source never viewed shows placeholder` on emulator pair; record results in `test-results/023-us1-placeholder.md`
- [ ] T021 Run full existing Playwright e2e suite via `testing/e2e/scripts/run-dual-emulator-e2e.ps1` and record all passing/blocking results in `test-results/023-regression.md`

**Checkpoint**: US1 delivered. Multiple sources each retain and display their own thumbnail. Existing e2e suite still passes (or blocked gates documented).

---

## Phase 3: User Story 2 — Frame Retention Persists Across Navigation (Priority: P1)

**Goal**: Ensure retained frames survive navigation away and back to the source list without being cleared.

**Independent Test**: View a source, navigate to settings (or another screen), return to source list, confirm the previously-viewed source still shows its thumbnail.

### Tests for User Story 2 (REQUIRED - TDD Red Phase) ⚠️

- [ ] T022 Create failing Playwright e2e test `frame persists after navigation away and back` in `testing/e2e/tests/023-per-source-frame-retention.spec.ts` that: views source A (frame captured), navigates to settings screen, returns to source list, verifies source A still shows its frame (using `frame persistence` assertion)
- [ ] [P] T023 Create failing Playwright e2e test `multi-source frames all persist after complex navigation` that: views sources A, B, C (all capture frames), navigates: list → settings → output → list → settings → list, confirms all three sources still show their frames in correct order
- [ ] T024 Create template entry in `test-results/023-us2-navigation-persistence.md` for results

### Implementation for User Story 2

- [X] T025 Verify `PerSourceFrameRepositoryImpl.observeFrameMap()` emits a `StateFlow` (hot, retained state) so that SourceListViewModel subscribers maintain the map across Fragment recreation — ensure no clearing on navigation
- [ ] [P] T026 Run Playwright e2e test `frame persists after navigation away and back` on emulator pair; record results in `test-results/023-us2-navigation-persistence.md`
- [ ] [P] T027 Run Playwright e2e test `multi-source frames all persist after complex navigation` on emulator pair; record results in `test-results/023-us2-navigation-persistence.md`
- [ ] T028 Run full existing Playwright e2e suite again and confirm all pass (or document any new blocks); update `test-results/023-regression.md`

**Checkpoint**: US2 delivered. Frames are preserved across navigation without clearing. Constitution validation on persistence semantics complete.

---

## Phase 4: User Story 3 — Frame Cleared When Source No Longer Available (Priority: P2)

**Goal**: Handle gracefully the case when a previously-viewed source disappears from the network, clearing its stale frame from the list.

**Independent Test**: View a source, disconnect the source from the network, refresh the source list, confirm the source and its frame are no longer shown.

### Tests for User Story 3 (REQUIRED - TDD Red Phase) ⚠️

- [ ] T029 Create failing Playwright e2e test `unavailable source frame removed from list` in `testing/e2e/tests/023-per-source-frame-retention.spec.ts` that: views source A (frame captured), disconnects source A from network (or stops emulator), refreshes discovery, verifies source A no longer appears in list and its frame is gone
- [ ] T030 Create failing Playwright e2e test `returned source shows existing frame until new view` that: views source A (frame F1 captured), disconnects A, reconnects A, refreshes discovery, navigates to viewer for A (source reappears), confirms A shows placeholder or old frame F1 (not A's frame from before), views A again (new frame F2 captured), exits, confirms list shows F2 (not F1)
- [ ] T031 Create template entry in `test-results/023-us3-stale-frame-handling.md` for results

### Implementation for User Story 3

- [X] T032 Ensure `SourceListViewModel.onScreenVisible()` call to refresh discovery via `discoveryRepository.startForegroundAutoRefresh()` includes cleanup logic for sources no longer in the list — verify that `enrichSourcesWithAvailability()` implicitly filters out unavailable sources (call `filterViewableSources()`)
- [X] T033 Verify that when a source is removed from `discoveryState.sources`, no stale thumbnail reference remains in `SourceListViewModel.lastViewedPreviewBySourceId` or the adapter — adapter should render only sources in current list
- [ ] [P] T034 Run Playwright e2e test `unavailable source frame removed from list` on emulator pair; record results in `test-results/023-us3-stale-frame-handling.md`
- [ ] [P] T035 Run Playwright e2e test `returned source shows existing frame until new view` on emulator pair; record results in `test-results/023-us3-stale-frame-handling.md`
- [ ] T036 Run full existing Playwright e2e suite again and confirm all pass; update `test-results/023-regression.md`

**Checkpoint**: US3 delivered. Stale frames for disconnected sources are handled gracefully. All user stories (US1, US2, US3) are functionally complete.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Release readiness, documentation, toolchain validation, and full regression coverage

### Release Hardening & Toolchain Validation

- [ ] T037 Run release build with R8/ProGuard + shrink enabled via `./gradlew :app:bundleRelease verifyReleaseHardening` and verify no new classes need keep-rules or excludes
- [X] [P] T038 Verify no new Android permissions declared in `AndroidManifest.xml` (feature uses only existing app permissions)
- [X] [P] T039 Confirm all new Kotlin classes compile without lint warnings; run `./gradlew :feature:ndi-browser:data:lintDebug` and `./gradlew :feature:ndi-browser:presentation:lintDebug`
- [ ] T040 Code review for Material 3 compliance: confirm no new UI components added (only existing `sourcePreviewImage` reused); existing layouts untouched

### Regression & Preflight Evidence

- [ ] T041 Final full run of existing Playwright e2e suite on dual emulator with both NDI sources available; record comprehensive passing evidence in `test-results/023-final-regression.md`
- [ ] [P] T042 Preflight validation: run `scripts/verify-android-prereqs.ps1` and `scripts/verify-e2e-dual-emulator-prereqs.ps1` one final time; append summary to `test-results/023-final-preflight.md`
- [ ] T043 Performance validation: measure source-list-visible time with no frames retained vs. with all 10-source cap frames retained; confirm startup time increase is ≤2 seconds per SC-005; document results (mean startup time before/after and delta) in `test-results/023-performance-impact.md`. Also confirm no background work added (frame capture only on viewer exit, in-memory only).

### Documentation

- [ ] T044 Update [docs/ndi-feature.md](docs/ndi-feature.md) to document per-source frame retention behavior, including LRU cap of 10 and session-only lifecycle
- [ ] [P] T045 Add implementation notes to [DOCUMENTATION-INDEX.md](DOCUMENTATION-INDEX.md) pointing to spec, plan, and contracts

### Final Validation

- [ ] T046 Run all unit tests (domain, data, presentation) via `./gradlew :feature:ndi-browser:test` and confirm 100% pass
- [ ] T047 Final emulator build and deploy-check: `./gradlew assembleDebug` produces valid APK with no errors
- [ ] T048 Confirm ViewerContinuityRepository single-source restore path (for relaunch) still works: run regression test `test-results/021-server-correlation-checklist-20260329.md` (or similar existing continuity test) and document no regression in `test-results/023-continuity-regression.md`

**Checkpoint**: Feature complete. All 3 user stories tested. Release hardening validated. No regressions in existing flows. Documentation updated.

---

## Dependency Graph & Parallel Execution

### Phase 1 (Blocking) Sequential → Parallel Options:
- **T002–T003**: Create test files (parallel)
- **T004**: Domain interface (sequential after T002–T003 created)
- **T005–T007**: Impl + green tests (parallel)
- **T008–T011**: Viewer + ViewModel + wiring (parallel after T004 done)
- **T012–T013**: Compilation + refactor (sequential after T011)

### Phase 2–4 (User Story Phases):
- Each phase: tests first (parallel) → implementation (parallel where possible) → regression (sequential)
- Phases themselves can overlap: e.g., US1 and US2 can run in parallel if Phase 1 is done

### Suggested MVP Scope:
- **Phase 1**: Foundation
- **Phase 2**: US1 (multi-source visible frames) ← **Minimum shippable**
- **Phase 3**: US2 (persistence across nav) ← Nice-to-have for v1.0
- **Phase 4**: US3 (stale frame handling) ← Follow-up / polish

---

## Summary

- **Total Tasks**: 48
- **MVP Tasks** (Phase 1 + Phase 2): 21 tasks, ~2–3 days
- **Full Feature** (Phases 1–5): 48 tasks, ~4–5 days (2 parallel tracks in test/impl phases)
- **Parallel Opportunities**: 20+ tasks (marked `[P]`)
- **Test-First Emphasis**: 8 failing tests created before production code; Red-Green-Refactor enforced
- **Environment Validation**: Preflight gates (Phase 0) + regression suite (Phase 5); blocked gates recorded with unblock steps
- **Zero Breaking Changes**: ViewerContinuityRepository untouched; single-source restore path preserved; existing Playwright e2e suite regression verified

**Next Step**: `/speckit.implement` or manual implementation starting at Phase 1, T002.
