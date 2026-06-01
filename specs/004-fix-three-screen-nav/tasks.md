# Tasks: Three-Screen Navigation Repairs and E2E Compatibility

**Input**: Design documents from `/specs/004-fix-three-screen-nav/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Test tasks are REQUIRED by the specification and quickstart (test-first behavior for routing, highlight state, and version-aware e2e flows).

**Organization**: Tasks are grouped by user story to keep each story independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: User story label (`US1`, `US2`, `US3`) for story-phase tasks only
- Every task includes an exact file path

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare validation and operator scaffolding for feature delivery.

- [X] T001 Create feature validation directory and seed checkpoint doc in `specs/004-fix-three-screen-nav/validation/foundation-checkpoint.md`
- [X] T002 [P] Capture pre-change dual-emulator runtime baseline in `specs/004-fix-three-screen-nav/validation/e2e-baseline.md`
- [X] T003 [P] Add feature-specific execution checklist section in `specs/004-fix-three-screen-nav/quickstart.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build shared models, contracts, and helpers required by all user stories.

**CRITICAL**: No user story work starts before this phase is complete.

- [X] T004 Extend canonical navigation state/back-policy models in `core/model/src/main/java/com/ndi/core/model/navigation/TopLevelNavigationModels.kt`
- [X] T005 [P] Add/extend top-level navigation telemetry event builders in `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationTelemetry.kt`
- [X] T006 [P] Add rolling latest-five support-window helpers in `testing/e2e/tests/support/android-device-fixtures.ts`
- [X] T007 [P] Add reusable consent-flow branch helpers in `testing/e2e/tests/support/android-ui-driver.ts`
- [X] T008 Wire View-root and Viewer route/back policy in `app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt`
- [X] T009 Update top-level/deep-link route actions for View flow in `app/src/main/res/navigation/main_nav_graph.xml`
- [X] T010 Record foundational readiness evidence in `specs/004-fix-three-screen-nav/validation/foundation-checkpoint.md`

**Checkpoint**: Foundation complete; user story phases can proceed.

---

## Phase 3: User Story 1 - View Flow Routes to Viewer Correctly (Priority: P1) 🎯 MVP

**Goal**: Ensure View source selection opens viewer, and back behavior returns to View root then Home.

**Independent Test**: From View root select a source, verify Viewer opens, press Back once to View root, press Back again to Home.

### Tests for User Story 1 (REQUIRED)

- [X] T011 [P] [US1] Add failing View-source-to-Viewer routing unit tests in `app/src/test/java/com/ndi/app/navigation/TopLevelNavigationCoordinatorTest.kt`
- [X] T012 [P] [US1] Add failing back-policy unit tests for Viewer -> View root -> Home in `app/src/test/java/com/ndi/app/navigation/TopLevelNavViewModelTest.kt`
- [X] T013 [P] [US1] Add failing View-to-Viewer/back-stack instrumentation tests in `app/src/androidTest/java/com/ndi/app/navigation/ViewToViewerNavigationUiTest.kt`

### Implementation for User Story 1

- [X] T014 [US1] Correct View source selection route builder to Viewer destination in `app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt`
- [X] T015 [US1] Update View source selection handler to emit Viewer navigation only in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt`
- [X] T016 [US1] Enforce deterministic back transitions for Viewer/View root in `app/src/main/res/navigation/main_nav_graph.xml`
- [X] T017 [US1] Align coordinator/viewmodel back policy with View flow contract in `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationCoordinator.kt` and `app/src/main/java/com/ndi/app/navigation/TopLevelNavViewModel.kt`
- [X] T018 [US1] Record US1 routing/back validation evidence in `specs/004-fix-three-screen-nav/validation/us1-view-routing-validation.md`

**Checkpoint**: US1 delivers correct View routing and deterministic back behavior independently.

---

## Phase 4: User Story 2 - Navigation Icons and Highlight State Are Consistent (Priority: P2)

**Goal**: Ensure Home/Stream/View icons are correct and active destination highlighting is always accurate.

**Independent Test**: Open Home, Stream, and View via taps and deep links; verify icon mapping and exactly one correct active highlight each time.

### Tests for User Story 2 (REQUIRED)

- [X] T019 [P] [US2] Add failing icon mapping tests for Home/Stream/View in `app/src/test/java/com/ndi/app/navigation/TopLevelNavViewModelTest.kt`
- [X] T020 [P] [US2] Add failing Stream-highlight tests for setup/control screens in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTopLevelNavTest.kt`
- [X] T021 [P] [US2] Add failing destination-highlight instrumentation tests (tap + deep link) in `app/src/androidTest/java/com/ndi/app/navigation/TopLevelDestinationHighlightUiTest.kt`

### Implementation for User Story 2

- [X] T022 [US2] Correct top-level icon mapping in `app/src/main/res/menu/top_level_navigation_menu.xml`
- [X] T023 [US2] Ensure selected destination derives from canonical nav state in `app/src/main/java/com/ndi/app/navigation/TopLevelNavViewModel.kt`
- [X] T024 [US2] Render singular active highlight state in `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationHost.kt`
- [X] T025 [US2] Keep Stream active on stream setup/control surfaces in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt` and `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt`
- [X] T026 [US2] Record US2 icon/highlight validation evidence in `specs/004-fix-three-screen-nav/validation/us2-icon-highlight-validation.md`

**Checkpoint**: US2 delivers consistent iconography and destination highlighting independently.

---

## Phase 5: User Story 3 - E2E Validation Adapts to Android Version and Runs Faster (Priority: P3)

**Goal**: Keep one unified dual-emulator suite that branches per detected device version, fails fast on unsupported versions, and enforces <=1s static delays.

**Independent Test**: Run dual-emulator suite on mixed supported versions and verify version diagnostics, per-device branch behavior, and <=1s static delay policy.

### Tests for User Story 3 (REQUIRED)

- [X] T027 [P] [US3] Add failing support-window and fail-fast helper tests in `testing/e2e/tests/support/android-device-fixtures.spec.ts`
- [X] T028 [P] [US3] Add failing unified-suite version-branch assertions in `testing/e2e/tests/interop-dual-emulator.spec.ts`
- [X] T029 [P] [US3] Add failing timing-policy tests for <=1000ms static delays in `testing/e2e/tests/support/android-ui-driver.spec.ts`

### Implementation for User Story 3

- [X] T030 [US3] Implement runtime rolling latest-five support-window evaluation in `testing/e2e/tests/support/android-device-fixtures.ts`
- [X] T031 [US3] Implement unsupported-version fail-fast diagnostics with non-zero outcome in `testing/e2e/tests/support/android-device-fixtures.ts`
- [X] T032 [US3] Implement per-device consent-flow branching helpers in `testing/e2e/tests/support/android-ui-driver.ts`
- [X] T033 [US3] Refactor unified dual-emulator suite to use per-role runtime branching in `testing/e2e/tests/interop-dual-emulator.spec.ts`
- [X] T034 [US3] Enforce <=1000ms intentional static waits in `testing/e2e/tests/support/android-ui-driver.ts` and `testing/e2e/tests/interop-dual-emulator.spec.ts`
- [X] T035 [US3] Surface version diagnostics and fail-fast context in runner output at `testing/e2e/scripts/run-dual-emulator-e2e.ps1`
- [X] T036 [US3] Record US3 version-aware e2e validation evidence in `specs/004-fix-three-screen-nav/validation/us3-version-aware-e2e-validation.md`

**Checkpoint**: US3 delivers deterministic and faster version-aware dual-emulator validation independently.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final compliance checks, runtime evidence, and release-gate verification.

- [X] T037 [P] Update dual-emulator operator guidance for version diagnostics and visible windows in `testing/e2e/README.md`
- [X] T038 [P] Update quickstart command matrix and expected diagnostics in `specs/004-fix-three-screen-nav/quickstart.md`
- [X] T042 [P] Validate Material Design 3 compliance for updated top-level navigation surfaces and capture evidence in `specs/004-fix-three-screen-nav/validation/material3-compliance-report.md`
- [X] T039 Run full feature validation sequence and capture outputs in `specs/004-fix-three-screen-nav/validation/quickstart-validation-report.md`
- [X] T040 Run release hardening checks and capture outputs in `specs/004-fix-three-screen-nav/validation/release-validation-matrix.md`
- [X] T041 Update measurable success criteria evidence (SC-001..SC-006) in `specs/004-fix-three-screen-nav/validation/success-criteria-report.md`
- [X] T043 Execute repeated-run viewer-back reliability matrix and confirm >=98% pass rate for SC-002 in `specs/004-fix-three-screen-nav/validation/us1-back-reliability-matrix.md`
- [X] T044 Execute post-change dual-emulator benchmark with baseline-parity methodology; validate revised SC-006 (unified suite with runtime version branching and >=98% pass rate) in `specs/004-fix-three-screen-nav/validation/e2e-runtime-improvement-report.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup; blocks all user stories.
- **User Stories (Phases 3-5)**: Depend on Foundational completion.
- **Polish (Phase 6)**: Depends on selected story completion.

### User Story Dependencies

- **US1 (P1)**: Starts after foundational checkpoint; no dependency on US2/US3.
- **US2 (P2)**: Starts after foundational checkpoint; can proceed independently of US1 implementation details.
- **US3 (P3)**: Starts after foundational checkpoint; independent from app UI stories except shared diagnostics conventions.

### Within Each User Story

- Write tests first and confirm they fail.
- Implement route/state logic before UI binding adjustments.
- Implement helper/policy logic before suite-level orchestration.
- Capture story-specific validation evidence before closing the story.

## Parallel Opportunities

- Setup: T002 and T003 can run in parallel.
- Foundational: T005, T006, and T007 can run in parallel.
- US1: T011, T012, and T013 can run in parallel.
- US2: T019, T020, and T021 can run in parallel.
- US3: T027, T028, and T029 can run in parallel.
- Polish: T037, T038, and T042 can run in parallel.

## Parallel Example: User Story 1

```text
Task: T011 [US1] Add failing View-source-to-Viewer routing unit tests in app/src/test/java/com/ndi/app/navigation/TopLevelNavigationCoordinatorTest.kt
Task: T012 [US1] Add failing back-policy unit tests in app/src/test/java/com/ndi/app/navigation/TopLevelNavViewModelTest.kt
Task: T013 [US1] Add failing instrumentation tests in app/src/androidTest/java/com/ndi/app/navigation/ViewToViewerNavigationUiTest.kt
```

## Parallel Example: User Story 2

```text
Task: T019 [US2] Add failing icon mapping tests in app/src/test/java/com/ndi/app/navigation/TopLevelNavViewModelTest.kt
Task: T020 [US2] Add failing Stream-highlight tests in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTopLevelNavTest.kt
Task: T021 [US2] Add failing highlight instrumentation tests in app/src/androidTest/java/com/ndi/app/navigation/TopLevelDestinationHighlightUiTest.kt
```

## Parallel Example: User Story 3

```text
Task: T027 [US3] Add failing support-window helper tests in testing/e2e/tests/support/android-device-fixtures.spec.ts
Task: T028 [US3] Add failing unified-suite branching assertions in testing/e2e/tests/interop-dual-emulator.spec.ts
Task: T029 [US3] Add failing timing-policy tests in testing/e2e/tests/support/android-ui-driver.spec.ts
```

## Implementation Strategy

### MVP First (US1 only)

1. Complete Phase 1 and Phase 2.
2. Deliver Phase 3 (US1) test-first.
3. Validate View routing/back behavior before expanding scope.

### Incremental Delivery

1. Build shared foundation once (Phases 1-2).
2. Deliver US1 and validate independently.
3. Deliver US2 and validate independently.
4. Deliver US3 and validate independently.
5. Run polish/release evidence tasks.

### Parallel Team Strategy

1. Team A completes foundational app and e2e helper tasks.
2. Team B executes US1 while Team C executes US2 in parallel after foundation.
3. Team D executes US3 helper + suite tasks in parallel with UI story stabilization.
4. Validation owner completes Phase 6 evidence and release checks.

## Notes

- All tasks follow required checklist format (`- [ ] T### ...`).
- Story labels appear only in user-story phases.
- Tasks preserve architecture constraints in AGENTS.md (app composition in `app`, repository contracts in `domain`, no direct presentation DB access).
- E2E tasks preserve one unified suite with runtime per-device branching and fail-fast behavior for unsupported Android versions.
