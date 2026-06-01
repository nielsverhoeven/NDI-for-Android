# Tasks: Three-Screen NDI Navigation (Home, Stream, View)

**Input**: Design documents from `/specs/003-three-screen-navigation/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Test tasks are REQUIRED by `CR-002`; follow TDD (write failing tests first, then implement).

**Organization**: Tasks are grouped by user story to keep each story independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: User story label (`US1`, `US2`, `US3`) for story-phase tasks only
- Every task includes an exact file path

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare project scaffolding and validation surfaces for three-screen navigation work.

- [X] T001 [#4] Create the validation directory and seed checkpoint document in `specs/003-three-screen-navigation/validation/foundation-checkpoint.md`
- [X] T002 [#5] [P] Add top-level navigation string and menu resources in `app/src/main/res/values/strings.xml` and `app/src/main/res/menu/top_level_navigation_menu.xml`
- [X] T003 [#6] [P] Add adaptive navigation shell layout resources in `app/src/main/res/layout/activity_main.xml` and `app/src/main/res/layout-sw600dp/activity_main.xml`
- [X] T004 [#7] Add Home destination placeholder UI resources in `feature/ndi-browser/presentation/src/main/res/layout/fragment_home_dashboard.xml`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build shared contracts, models, telemetry hooks, and app wiring required by all user stories.

**CRITICAL**: No user story work starts before this phase is complete.

- [X] T005 [#8] Extend navigation/domain contracts for top-level destination, dashboard snapshot, and continuity repositories in `feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt`
- [X] T006 [#9] [P] Add navigation and dashboard model types in `core/model/src/main/java/com/ndi/core/model/navigation/TopLevelNavigationModels.kt`
- [X] T007 [#10] [P] Add top-level navigation telemetry event IDs in `core/model/src/main/java/com/ndi/core/model/TelemetryEvent.kt`
- [X] T008 [#11] Add app-level dependency provider for top-level navigation coordination in `app/src/main/java/com/ndi/app/di/AppGraph.kt`
- [X] T009 [#12] Add app navigation route constants/builders for Home, Stream, and View in `app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt`
- [X] T010 [#13] Add top-level and deep-link-preserving destinations/actions in `app/src/main/res/navigation/main_nav_graph.xml`
- [X] T011 [#14] Implement launch-context resolver (launcher vs recents vs deep link) in `app/src/main/java/com/ndi/app/navigation/LaunchContextResolver.kt`
- [X] T012 [#15] Implement top-level navigation telemetry facade in `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationTelemetry.kt`

**Checkpoint**: Foundation complete; user story phases can proceed.

---

## Phase 3: User Story 1 - Navigate Between Core Screens (Priority: P1) 🎯 MVP

**Goal**: Provide deterministic top-level navigation between Home, Stream, and View with adaptive Material 3 controls and active selection state.

**Independent Test**: From each top-level screen, navigate to the other two and verify active destination highlight plus no duplicate destination stacking after repeated taps.

### Tests for User Story 1 (REQUIRED)

- [X] T013 [#16] [P] [US1] Add failing unit tests for deterministic destination selection and reselection no-op behavior in `app/src/test/java/com/ndi/app/navigation/TopLevelNavViewModelTest.kt`
- [X] T014 [#17] [P] [US1] Add failing unit tests for duplicate-stack prevention nav options in `app/src/test/java/com/ndi/app/navigation/TopLevelNavigationCoordinatorTest.kt`
- [X] T015 [#18] [P] [US1] Add failing phone/tablet UI flow tests for top-level destination highlighting in `app/src/androidTest/java/com/ndi/app/navigation/TopLevelNavigationUiTest.kt`

### Implementation for User Story 1

- [X] T016 [#19] [US1] Implement `TopLevelNavViewModel` selection state/events in `app/src/main/java/com/ndi/app/navigation/TopLevelNavViewModel.kt`
- [X] T017 [#20] [US1] Implement navigation coordinator with `launchSingleTop`/state-restore rules in `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationCoordinator.kt`
- [X] T018 [#21] [P] [US1] Implement adaptive bottom-nav vs rail rendering and destination binding in `app/src/main/java/com/ndi/app/MainActivity.kt`
- [X] T019 [#22] [P] [US1] Add navigation host chrome wiring for selected-state updates in `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationHost.kt`
- [X] T020 [#23] [US1] Emit selected/reselected/failed navigation telemetry events in `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationTelemetry.kt`
- [X] T021 [#24] [US1] Integrate top-level routing callbacks with existing Stream and View fragments in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt` and `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt`
- [X] T022 [#25] [US1] Record US1 navigation validation evidence in `specs/003-three-screen-navigation/validation/us1-top-level-navigation-validation.md`

**Checkpoint**: US1 delivers a stable, adaptive top-level navigation shell and is independently testable.

---

## Phase 4: User Story 2 - Use Home Dashboard as App Entry Point (Priority: P2)

**Goal**: Add Home as launcher-default destination with dashboard status and explicit actions to Stream and View.

**Independent Test**: Launch from launcher icon and verify Home appears first with dashboard summary; tap Home actions and confirm routing to Stream/View; validate Recents/task-restore reopens the last top-level destination across Home/Stream/View.

### Tests for User Story 2 (REQUIRED)

- [X] T023 [#26] [P] [US2] Add failing unit tests for launcher-home default and home action navigation events in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/home/HomeViewModelTest.kt`
- [X] T024 [#27] [P] [US2] Add failing repository tests for home dashboard snapshot aggregation in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/HomeDashboardRepositoryImplTest.kt`
- [X] T025 [#28] [P] [US2] Add failing UI test for launcher entry to Home and quick actions in `app/src/androidTest/java/com/ndi/app/navigation/HomeEntryUiTest.kt`
- [X] T047 [#50] [P] [US2] Add failing unit tests for FR-004a restore-destination resolution matrix (Recents/task-restore -> Home/Stream/View) in `app/src/test/java/com/ndi/app/navigation/LaunchContextResolverTest.kt`
- [X] T048 [#51] [P] [US2] Add failing instrumentation tests for Recents/task-restore matrix reopening last top-level destination across Home/Stream/View in `app/src/androidTest/java/com/ndi/app/navigation/RecentsRestoreMatrixUiTest.kt`

### Implementation for User Story 2

- [X] T026 [#29] [US2] Implement home dashboard repository aggregation logic in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/HomeDashboardRepositoryImpl.kt`
- [X] T027 [#30] [US2] Implement `HomeViewModel` and action event contract in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/home/HomeViewModel.kt`
- [X] T028 [#31] [P] [US2] Implement Home fragment/screen rendering with dashboard + quick actions in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/home/HomeScreen.kt`
- [X] T029 [#32] [P] [US2] Register Home dependencies via service-locator pattern in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/home/HomeTelemetry.kt`
- [X] T030 [#33] [US2] Wire Home destination and launcher entry policy in `app/src/main/res/navigation/main_nav_graph.xml` and `app/src/main/java/com/ndi/app/MainActivity.kt`
- [X] T031 [#34] [US2] Record US2 dashboard/entry validation evidence in `specs/003-three-screen-navigation/validation/us2-home-dashboard-validation.md`

**Checkpoint**: US2 delivers Home dashboard entry behavior independently.

---

## Phase 5: User Story 3 - Set Up and View NDI Streams Across Dedicated Pages (Priority: P3)

**Goal**: Preserve existing Stream/View flows under three-screen IA, including deep links and continuity semantics.

**Independent Test**: Navigate Home -> Stream to control output, then Stream/View switches preserve required continuity and deep links still enter valid top-level state.

### Tests for User Story 3 (REQUIRED)

- [X] T032 [#35] [P] [US3] Add failing unit tests for stream continuity on top-level navigation away and process-death contextual restore in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTopLevelNavTest.kt`
- [X] T033 [#36] [P] [US3] Add failing unit tests for View stop-only (no pause fallback) and no-autoplay restore behavior on top-level navigation and relaunch in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModelTopLevelNavTest.kt`
- [X] T034 [#37] [P] [US3] Add failing UI/deep-link tests for `ndi://viewer/{sourceId}` and `ndi://output/{sourceId}` with top-level chrome visible in `app/src/androidTest/java/com/ndi/app/navigation/DeepLinkTopLevelNavigationUiTest.kt`

### Implementation for User Story 3

- [X] T035 [#38] [US3] Implement top-level navigation continuity adapters for Stream output state in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/StreamContinuityRepositoryImpl.kt`
- [X] T036 [#39] [US3] Implement top-level navigation continuity adapters for View selection/playback state in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/ViewContinuityRepositoryImpl.kt`
- [X] T037 [#40] [US3] Apply leave-View stop-only and no-autoplay enforcement in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModel.kt`
- [X] T038 [#41] [US3] Preserve foreground-only discovery refresh while integrated with top-level shell in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt`
- [X] T039 [#42] [US3] Preserve deep-link routing compatibility with top-level shell state in `app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt` and `app/src/main/res/navigation/main_nav_graph.xml`
- [X] T040 [#43] [US3] Record US3 continuity and deep-link validation evidence in `specs/003-three-screen-navigation/validation/us3-stream-view-continuity-validation.md`

**Checkpoint**: US3 preserves Stream/View behavior guarantees inside the three-screen architecture.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final compliance, release gating, and full-feature validation evidence.

- [X] T041 [#44] [P] Add e2e flow coverage for Home -> Stream -> View -> Home across phone/tablet projects in `testing/e2e/tests/three-screen-navigation.spec.ts`
- [X] T042 [#45] Run quickstart validation commands and capture outcomes in `specs/003-three-screen-navigation/validation/quickstart-validation-report.md`
- [X] T043 [#46] Run adaptive layout matrix (phone bottom nav, tablet rail) and capture results in `specs/003-three-screen-navigation/validation/device-layout-validation-report.md`
- [X] T044 [#47] Validate top-level telemetry payload constraints (non-sensitive only) in `specs/003-three-screen-navigation/validation/telemetry-compliance-report.md`
- [X] T045 [#48] Execute release hardening verification and record outputs in `specs/003-three-screen-navigation/validation/release-validation-matrix.md`
- [X] T046 [#49] Update success-criteria evidence (SC-001..SC-005) in `specs/003-three-screen-navigation/validation/success-criteria-report.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies.
- **Phase 2 (Foundational)**: Depends on Phase 1; blocks all story work.
- **Phase 3 (US1)**: Depends on Phase 2; delivers MVP shell routing.
- **Phase 4 (US2)**: Depends on Phase 2 and integrates with US1 shell.
- **Phase 5 (US3)**: Depends on Phase 2 and integrates with US1 shell + existing Stream/View flows.
- **Phase 6 (Polish)**: Depends on all selected story phases.

### User Story Dependencies

- **US1 (P1)**: Starts immediately after foundational checkpoint; no dependency on US2/US3.
- **US2 (P2)**: Depends on foundational tasks and US1 top-level shell surfaces.
- **US3 (P3)**: Depends on foundational tasks and US1 navigation shell; reuses existing output/viewer flows.

### Within Each User Story

- Write tests first and confirm they fail.
- Implement ViewModel/repository contracts before UI integration.
- Wire telemetry before closing story validation.
- Capture story-specific validation evidence before moving forward.

## Parallel Opportunities

- Setup: T002-T003 can run in parallel.
- Foundational: T006-T007 can run in parallel after T005 starts.
- US1: T013-T015 parallel test authoring; T018-T019 parallel UI wiring.
- US2: T023-T025 and T047-T048 parallel tests; T028-T029 parallel Home presentation wiring.
- US3: T032-T034 parallel tests.
- Polish: T041 and T044 can run in parallel with validation document updates.

## Parallel Example: User Story 1

```text
Task: T013 [US1] Add failing unit tests for deterministic destination selection in app/src/test/java/com/ndi/app/navigation/TopLevelNavViewModelTest.kt
Task: T014 [US1] Add failing unit tests for duplicate-stack prevention in app/src/test/java/com/ndi/app/navigation/TopLevelNavigationCoordinatorTest.kt
Task: T015 [US1] Add failing phone/tablet UI tests in app/src/androidTest/java/com/ndi/app/navigation/TopLevelNavigationUiTest.kt

Task: T018 [US1] Implement adaptive bottom-nav vs rail rendering in app/src/main/java/com/ndi/app/MainActivity.kt
Task: T019 [US1] Add selected-state host binding in app/src/main/java/com/ndi/app/navigation/TopLevelNavigationHost.kt
```

## Parallel Example: User Story 2

```text
Task: T023 [US2] Add failing launcher-home default tests in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/home/HomeViewModelTest.kt
Task: T024 [US2] Add failing dashboard snapshot repository tests in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/HomeDashboardRepositoryImplTest.kt
Task: T025 [US2] Add failing launcher-entry UI test in app/src/androidTest/java/com/ndi/app/navigation/HomeEntryUiTest.kt
Task: T047 [US2] Add failing Recents restore matrix unit tests in app/src/test/java/com/ndi/app/navigation/LaunchContextResolverTest.kt
Task: T048 [US2] Add failing Recents restore matrix instrumentation tests in app/src/androidTest/java/com/ndi/app/navigation/RecentsRestoreMatrixUiTest.kt

Task: T028 [US2] Implement Home screen rendering in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/home/HomeScreen.kt
Task: T029 [US2] Register Home dependencies service-locator style in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/home/HomeTelemetry.kt
```

## Parallel Example: User Story 3

```text
Task: T032 [US3] Add failing stream continuity tests in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTopLevelNavTest.kt
Task: T033 [US3] Add failing view continuity tests in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModelTopLevelNavTest.kt
Task: T034 [US3] Add failing deep-link top-level UI tests in app/src/androidTest/java/com/ndi/app/navigation/DeepLinkTopLevelNavigationUiTest.kt
```

## Implementation Strategy

### MVP First (US1 only)

1. Complete Phase 1 and Phase 2.
2. Deliver Phase 3 (US1) with failing tests first, then implementation.
3. Validate adaptive nav and duplicate-stack prevention before expanding scope.

### Incremental Delivery

1. Build foundation once (Phases 1-2).
2. Deliver US1 (navigation shell), validate, and demo.
3. Deliver US2 (Home dashboard and launcher behavior), validate, and demo.
4. Deliver US3 (continuity + deep links), validate, and demo.
5. Finish compliance/release evidence in Phase 6.

### Parallel Team Strategy

1. Team A completes foundational app wiring (Phase 2).
2. Team B handles US1 shell + tests while Team C starts US2 repository/dashboard tests.
3. Team D focuses on US3 continuity/deep-link tests after shell APIs stabilize.
4. Validation owner runs Phase 6 evidence collection in parallel with final bug fixes.

## Notes

- All tasks follow the required checklist format (`- [ ] T### ...`).
- Story labels are present only for user-story phases.
- Paths keep architecture boundaries from `AGENTS.md` (composition in `app`, contracts in `domain`, impl in `data`, no direct presentation DB access).
- Deep links and no-autoplay/foreground-discovery continuity are explicitly preserved in US3 tasks.
- FR-004a restore matrix coverage is explicitly tracked via US2 test tasks T047-T048.


