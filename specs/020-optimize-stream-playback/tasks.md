# Tasks: Optimize NDI Stream Playback with Quality Controls

**Input**: Design documents from `/specs/020-optimize-stream-playback/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are REQUIRED by constitution and spec. Every user story includes failing-test-first tasks, emulator Playwright e2e coverage for visual changes, and Viewer regression runs.

**Organization**: Tasks are grouped by user story to support independent implementation, testing, and delivery.

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Verify emulator, NDI source, and toolchain readiness before coding and before quality gates.

- [X] T001 Run Android prerequisite checks with `scripts/verify-android-prereqs.ps1` and record results in `test-results/020-optimize-stream-playback-preflight.md`
- [X] T002 Run dual-emulator/NDI preflight with `scripts/verify-e2e-dual-emulator-prereqs.ps1 -AllowMissingNdiSdk` and append evidence to `test-results/020-optimize-stream-playback-preflight.md`
- [X] T003 Execute debug build gate `./gradlew :app:assembleDebug` and record pass/fail details in `test-results/020-optimize-stream-playback-preflight.md`
- [X] T004 Capture blocked-environment evidence and unblock commands in `test-results/020-optimize-stream-playback-preflight.md`

**Checkpoint**: Environment is confirmed ready or blockers are explicitly documented.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare module scaffolding and shared integration points for all stories.

- [X] T005 Create source/test package scaffolding for new playback optimization components in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/model/`
- [X] T006 [P] Create source/test package scaffolding for viewer scaling components in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/`
- [X] T007 [P] Create e2e spec placeholders for feature 020 in `testing/e2e/tests/`
- [X] T008 Add feature-020 result document stubs in `test-results/020-optimize-stream-playback-validation.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish contracts, models, DI, and telemetry hooks required by all user stories.

**CRITICAL**: User story implementation starts only after this phase completes.

- [X] T009 Add quality repository contract and domain models in `feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/QualityProfileRepository.kt`
- [X] T010 Extend viewer domain repository contract for quality/disconnection APIs in `feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt`
- [X] T011 [P] Add playback optimization data model in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/model/PlaybackOptimization.kt`
- [X] T012 [P] Add quality preference persistence model in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/model/QualityPreference.kt`
- [X] T013 [P] Add disconnection event model in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/model/DisconnectionEvent.kt`
- [X] T014 Implement SharedPreferences store for quality preferences in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/local/SharedPreferencesQualityStore.kt`
- [X] T015 Implement quality repository adapter in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/QualityProfileRepositoryImpl.kt`
- [X] T016 Wire repository dependencies for viewer feature in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerTelemetry.kt` (ViewerDependencies object)
- [X] T017 Wire application graph bindings for quality repository in `app/src/main/java/com/ndi/app/di/AppGraph.kt`
- [X] T018 Add baseline telemetry event keys for quality/degradation/recovery in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerRecoveryTelemetry.kt`

**Checkpoint**: Shared contracts/models/DI are complete; user stories can proceed.

---

## Phase 3: User Story 1 - Smooth Default Playback (Priority: P1) 🎯 MVP

**Goal**: Deliver smooth playback by default (24+ fps) with automatic quality degradation and disconnection recovery.

**Independent Test**: Open Viewer from Source List and sustain smooth playback for 60 seconds; simulate degraded network and confirm quality downgrades; simulate disconnection and confirm reconnect dialog appears within 2 seconds.

### Tests for User Story 1 (REQUIRED, write first)

- [X] T019 [P] [US1] Add failing unit tests for frame-drop downgrade thresholds in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/PlaybackOptimizationPolicyTest.kt`
- [X] T020 [P] [US1] Add failing unit tests for disconnection retry/backoff policy in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerReconnectCoordinatorTest.kt`
- [X] T021 [P] [US1] Add failing repository integration tests for quality profile application flow in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiViewerRepositoryImplQualityTest.kt`
- [X] T022 [P] [US1] Add Playwright e2e test for smooth playback and auto-degrade in `testing/e2e/tests/020-us1-smooth-playback.spec.ts`
- [X] T023 [US1] Run existing Viewer Playwright regression suite and capture evidence in `test-results/020-us1-viewer-regression.md`
- [X] T024 [US1] Record blocked-gate status and unblock steps for US1 validation in `test-results/020-us1-viewer-regression.md`

### Implementation for User Story 1

- [X] T025 [US1] Implement quality-aware playback methods in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiViewerRepositoryImpl.kt`
- [X] T026 [US1] Add codec/profile bridge APIs in `ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt`
- [X] T027 [US1] Implement native quality/degradation handling in `ndi/sdk-bridge/src/main/cpp/ndi_bridge.cpp`
- [X] T028 [US1] Update native build linkage for quality controls in `ndi/sdk-bridge/src/main/cpp/CMakeLists.txt`
- [X] T029 [US1] Add playback optimization and reconnect state machine in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModel.kt`
- [X] T030 [US1] Add disconnection dialog UI state/rendering in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`
- [X] T031 [US1] Emit smooth-playback and recovery telemetry in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerRecoveryTelemetry.kt`

**Checkpoint**: US1 is independently functional and testable as MVP.

---

## Phase 4: User Story 2 - Auto-Fit Player Layout (Priority: P2)

**Goal**: Auto-fit streamed content to player bounds while preserving aspect ratio across orientation changes.

**Independent Test**: Render 16:9, 4:3, and 21:9 streams in portrait and landscape; video stays centered, undistorted, and fills at least 90% of available player area.

### Tests for User Story 2 (REQUIRED, write first)

- [X] T032 [P] [US2] Add failing unit tests for scaling math and utilization target in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/PlayerScalingCalculatorTest.kt`
- [X] T033 [P] [US2] Add failing unit tests for orientation recalculation behavior in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/PlayerScalingViewModelTest.kt`
- [X] T034 [P] [US2] Add Playwright e2e orientation/auto-fit test in `testing/e2e/tests/020-us2-player-autofit.spec.ts`
- [X] T035 [US2] Run existing Viewer Playwright regression suite and capture evidence in `test-results/020-us2-viewer-regression.md`
- [X] T036 [US2] Record blocked-gate status and unblock steps for US2 validation in `test-results/020-us2-viewer-regression.md`

### Implementation for User Story 2

- [X] T037 [US2] Implement scaling state models in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/PlayerScalingState.kt`
- [X] T038 [US2] Implement scaling calculator contract and geometry rules in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/PlayerScalingCalculator.kt`
- [X] T039 [US2] Implement concrete scaling logic in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/PlayerScalingCalculatorImpl.kt`
- [X] T040 [US2] Implement scaling state management in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/PlayerScalingViewModel.kt`
- [X] T041 [US2] Integrate auto-fit rendering and letterbox behavior in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`
- [X] T042 [US2] Update viewer layout/resources for auto-fit constraints in `feature/ndi-browser/presentation/src/main/res/layout/fragment_viewer.xml`

**Checkpoint**: US2 is independently functional and testable.

---

## Phase 5: User Story 3 - User Quality Presets (Priority: P3)

**Goal**: Allow users to select quality presets from Viewer settings, apply them in real time, and persist the selection across sessions.

**Independent Test**: Change preset from Viewer settings within 2 taps; stream adapts without restart; after background/return the selected preset is retained and re-applied.

### Tests for User Story 3 (REQUIRED, write first)

- [X] T043 [P] [US3] Add failing unit tests for preference persistence and fallback behavior in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/local/SharedPreferencesQualityStoreTest.kt`
- [X] T044 [P] [US3] Add failing ViewModel tests for profile selection/apply workflow in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerQualitySettingsViewModelTest.kt`
- [ ] T045 [P] [US3] Add Playwright e2e test for quality menu, apply latency, and persistence in `testing/e2e/tests/020-us3-quality-settings.spec.ts`
- [ ] T046 [US3] Run existing Viewer Playwright regression suite and capture evidence in `test-results/020-us3-viewer-regression.md`
- [X] T047 [US3] Record blocked-gate status and unblock steps for US3 validation in `test-results/020-us3-viewer-regression.md`

### Implementation for User Story 3

- [X] T048 [US3] Implement quality settings menu composable with accessibility labels in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/QualitySettingsMenuComposable.kt`
- [X] T049 [US3] Integrate quality menu actions and state display in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`
- [X] T050 [US3] Implement profile selection/persistence orchestration in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModel.kt`
- [X] T051 [US3] Add/adjust quality menu item resources in `feature/ndi-browser/presentation/src/main/res/menu/viewer_menu.xml`
- [X] T052 [US3] Add accessible preset and hint strings in `feature/ndi-browser/presentation/src/main/res/values/strings.xml`
- [X] T053 [US3] Persist and rehydrate selected profile in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/QualityProfileRepositoryImpl.kt`

**Checkpoint**: US3 is independently functional and testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Complete release-readiness, documentation, and full validation.

- [X] T054 [P] Update feature documentation and developer runbook in `specs/020-optimize-stream-playback/quickstart.md`
- [X] T055 Run full feature module unit tests and capture output in `test-results/020-optimize-stream-playback-validation.md`
- [ ] T056 Run full Playwright e2e suite plus Viewer regressions and capture output in `test-results/020-optimize-stream-playback-validation.md`
- [X] T057 Run release hardening check (`:app:assembleRelease`, minify/shrink) and capture output in `test-results/020-optimize-stream-playback-validation.md`
- [X] T058 Perform telemetry and log review for quality/degradation/reconnect paths in `test-results/020-optimize-stream-playback-validation.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 0 → blocks all later phases until environment is ready or blockers are documented.
- Phase 1 → depends on Phase 0.
- Phase 2 → depends on Phase 1; blocks all user stories.
- Phases 3/4/5 → each depends on Phase 2. They may run in parallel if team capacity allows.
- Phase 6 → depends on completion of selected user stories.

### User Story Dependencies

- **US1 (P1)**: Depends only on Phase 2 foundations; delivers MVP alone.
- **US2 (P2)**: Depends only on Phase 2 foundations; can ship after US1.
- **US3 (P3)**: Depends on Phase 2 foundations and integrates with US1 playback flows.

### Within Each User Story

- Write tests first and confirm failure.
- Implement domain/data/presentation changes.
- Run Playwright new coverage and existing Viewer regression.
- Record blocker evidence if environment constraints prevent completion.

---

## Parallel Execution Examples

### User Story 1

- `T019`, `T020`, and `T021` can run in parallel.
- `T026` and `T031` can run in parallel once `T025` is in progress.

### User Story 2

- `T032`, `T033`, and `T034` can run in parallel.
- `T037` and `T038` can run in parallel before `T039`.

### User Story 3

- `T043`, `T044`, and `T045` can run in parallel.
- `T051` and `T052` can run in parallel with `T048`.

---

## Implementation Strategy

### MVP First (US1 only)

1. Complete Phases 0-2.
2. Complete Phase 3 (US1).
3. Validate US1 independently with unit + e2e + regression evidence.
4. Demo/release MVP.

### Incremental Delivery

1. Deliver US1 (smooth playback baseline).
2. Deliver US2 (auto-fit rendering).
3. Deliver US3 (quality preset controls and persistence).
4. Finish Phase 6 release-quality validation.
