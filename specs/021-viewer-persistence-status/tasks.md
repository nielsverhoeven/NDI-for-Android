# Tasks: Viewer Persistence and Stream Availability Status

**Input**: Design documents from `/specs/021-viewer-persistence-status/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/viewer-source-list-ui-contract.md, quickstart.md

**Tests**: Tests are required by constitution and included per user story with failing-test-first sequencing.

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Confirm runtime/tooling prerequisites before implementation and validation.

- [ ] T001 Run Android prerequisite gate and capture output via scripts/verify-android-prereqs.ps1
- [ ] T002 Run toolchain verification and capture output via ./gradlew.bat --version
- [ ] T003 Create preflight evidence log at test-results/021-viewer-persistence-preflight.md
- [ ] T004 Record blocked-environment handling template at test-results/021-viewer-persistence-validation.md

**Checkpoint**: Environment readiness is verified or explicit blockers are documented.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare shared scaffolding for implementation and validation evidence.

- [ ] T005 Create feature-specific Playwright test file skeleton at testing/e2e/tests/021-viewer-persistence-restore.spec.ts
- [ ] T006 Create source-list status Playwright test file skeleton at testing/e2e/tests/021-source-list-availability-status.spec.ts
- [ ] T007 [P] Create US1 regression evidence file at test-results/021-us1-playwright-regression.md
- [ ] T008 [P] Create US2 regression evidence file at test-results/021-us2-playwright-regression.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add shared contracts and persistence infrastructure required by both user stories.

**⚠️ CRITICAL**: No user story implementation starts before this phase is complete.

- [ ] T009 Extend repository contracts for viewer continuity and source status in feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
- [ ] T010 Add Room entities/DAO definitions for LastViewedContext and ConnectionHistoryState in core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt
- [ ] T011 Add Room migration and database version bump for new persistence tables in core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt
- [ ] T012 [P] Implement persistence mappers for viewer continuity state in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/mapper/ViewerContinuityMapper.kt
- [ ] T013 Implement viewer continuity repository in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/ViewerContinuityRepositoryImpl.kt
- [ ] T014 [P] Implement availability debounce state helper in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/AvailabilityDebounceTracker.kt
- [ ] T015 Wire new repositories into app graph and dependency providers in app/src/main/java/com/ndi/app/di/AppGraph.kt

**Checkpoint**: Domain and data foundations are ready for story-level behavior.

---

## Phase 3: User Story 1 - Restore Last Viewed Stream With Saved Preview (Priority: P1) 🎯 MVP

**Goal**: Restore last viewed stream context and one saved last-frame image on relaunch, including unavailable restore behavior.

**Independent Test**: View a stream until a frame renders, relaunch app, verify restored context + saved preview and no autoplay when stream unavailable.

### Tests for User Story 1 (write first and fail first)

- [ ] T016 [P] [US1] Add failing repository unit test for last viewed context persistence in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/ViewerContinuityRepositoryImplTest.kt
- [ ] T017 [P] [US1] Add failing repository unit test for single-preview replacement behavior in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/ViewerContinuityRepositoryImplTest.kt
- [ ] T018 [P] [US1] Add failing ViewModel unit test for unavailable restore no-autoplay behavior in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModelRestoreTest.kt
- [ ] T018a [P] [US1] Add failing repository unit test proving Previously Connected and last-viewed context reset after app-data clear in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/ViewerContinuityRepositoryImplTest.kt
- [ ] T019 [P] [US1] Add emulator Playwright e2e test for restore flow in testing/e2e/tests/021-viewer-persistence-restore.spec.ts
- [ ] T020 [US1] Run existing Playwright e2e regression and record results in test-results/021-us1-playwright-regression.md
- [ ] T021 [US1] Record blocked-gate evidence and unblock commands if needed in test-results/021-us1-playwright-regression.md

### Implementation for User Story 1

- [ ] T022 [US1] Persist last viewed context and successful-frame connection history from viewer session flow in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiViewerRepositoryImpl.kt
- [ ] T023 [US1] Implement preview image capture/write and bounded single-image replacement in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/ViewerContinuityRepositoryImpl.kt
- [ ] T023a [US1] Implement explicit reset path that clears LastViewedContext and ConnectionHistoryState on app-data reset lifecycle in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/ViewerContinuityRepositoryImpl.kt
- [ ] T024 [US1] Load and apply restored context in viewer state orchestration in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModel.kt
- [ ] T025 [US1] Render saved preview and unavailable restore state in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt
- [ ] T026 [US1] Add restore-state telemetry events for viewer continuity in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerTelemetry.kt
- [ ] T026a [US1] Add validation evidence task for app-data-clear reset behavior in test-results/021-us1-playwright-regression.md

**Checkpoint**: User Story 1 works independently and satisfies restore requirements.

---

## Phase 4: User Story 2 - Show Previously Connected and Availability Status (Priority: P1)

**Goal**: Show per-stream Previously Connected + Unavailable indicators and disable View Stream action for unavailable rows.

**Independent Test**: Open source list with mixed stream states and verify badges plus button enabled/disabled behavior; disabled rows never navigate.

### Tests for User Story 2 (write first and fail first)

- [ ] T027 [P] [US2] Add failing ViewModel unit test for two-miss availability transition in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelAvailabilityTest.kt
- [ ] T028 [P] [US2] Add failing ViewModel unit test for disabled navigation on unavailable source in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelAvailabilityTest.kt
- [ ] T029 [P] [US2] Add failing UI model test for Previously Connected badge visibility in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListUiStateTest.kt
- [ ] T030 [P] [US2] Add emulator Playwright e2e test for source list availability/history states in testing/e2e/tests/021-source-list-availability-status.spec.ts
- [ ] T031 [US2] Run existing Playwright e2e regression and record results in test-results/021-us2-playwright-regression.md
- [ ] T032 [US2] Record blocked-gate evidence and unblock commands if needed in test-results/021-us2-playwright-regression.md

### Implementation for User Story 2

- [ ] T033 [US2] Integrate availability debounce tracker and history state projection in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [ ] T034 [US2] Add source row UI state fields for badges and view action enablement in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [ ] T035 [US2] Render Previously Connected and Unavailable badges plus disabled action state in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
- [ ] T036 [US2] Prevent navigation from unavailable rows in source selection handling in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [ ] T037 [US2] Update source row adapter binding for badges and button state in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/adapter/SourceAdapter.kt
- [ ] T038 [US2] Add source-list telemetry for availability state changes and blocked selections in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListTelemetry.kt

**Checkpoint**: User Story 2 works independently with correct badge/action behavior.

---

## Phase 5: Polish and Cross-Cutting

**Purpose**: Final validation, documentation updates, and release-quality checks.

- [ ] T039 [P] Run module unit test suites for touched modules via ./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest
- [ ] T040 Run full Playwright suite and append consolidated evidence to test-results/021-viewer-persistence-validation.md
- [ ] T041 [P] Run release hardening gate via ./gradlew.bat :app:verifyReleaseHardening
- [ ] T042 Update feature validation notes in docs/testing.md
- [ ] T043 Run quickstart validation steps and capture final pass/fail in specs/021-viewer-persistence-status/quickstart.md

---

## Dependencies and Execution Order

### Phase Dependencies

- Phase 0 must complete before any implementation or quality gates.
- Phase 1 depends on Phase 0 and prepares shared test/evidence scaffolding.
- Phase 2 depends on Phase 1 and blocks all user-story implementation.
- Phase 3 (US1) and Phase 4 (US2) both depend on Phase 2.
- Phase 5 depends on completed stories in scope.

### User Story Dependencies

- US1: Depends only on Phase 2; no dependency on US2.
- US2: Depends only on Phase 2; no dependency on US1.

### Within Each User Story

- Tests first (fail first), then implementation, then regression evidence.
- Data/repository changes before ViewModel/UI wiring.
- UI behavior complete before Playwright regression sign-off.

## Parallel Opportunities

- Phase 1: T007 and T008 can run in parallel.
- Phase 2: T012 and T014 can run in parallel after T009/T010.
- US1: T016-T019 can run in parallel; T022-T023 can run in parallel after tests fail.
- US2: T027-T030 can run in parallel; T034 and T037 can run in parallel after ViewModel state contract is set.
- Phase 5: T039 and T041 can run in parallel.

## Parallel Example: User Story 1

- Run together: T016, T017, T018, T018a, T019.
- Then run together: T022, T023, T023a.
- Then finish with: T024, T025, T026, T026a, T020, T021.

## Parallel Example: User Story 2

- Run together: T027, T028, T029, T030.
- Then run together: T034 and T037.
- Then finish with: T033, T035, T036, T038, T031, T032.

## Implementation Strategy

### MVP First (US1)

1. Complete Phase 0, Phase 1, and Phase 2.
2. Deliver Phase 3 (US1) and validate independently.
3. Demo/deploy MVP behavior for restored stream context and preview image.

### Incremental Delivery

1. Add US1 and validate.
2. Add US2 and validate.
3. Finish with Phase 5 cross-cutting quality gates.
