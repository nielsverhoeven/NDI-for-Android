# Tasks: Three-Column Settings Layout

**Input**: Design documents from /specs/019-settings-three-pane/
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are REQUIRED by constitution. Every user story includes failing-test-first unit or instrumentation tasks, Playwright emulator e2e coverage, existing Playwright regression execution, and blocked-gate evidence tasks.

**Organization**: Tasks are grouped by user story for independent implementation and validation.

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Verify runtime and tool prerequisites before implementation and visual validation.

- [ ] T001 Run prerequisite gate and capture output in test-results/019-settings-three-pane-preflight.md using scripts/verify-android-prereqs.ps1
- [ ] T002 Run dual-emulator preflight and capture output in test-results/019-settings-three-pane-preflight.md using scripts/verify-e2e-dual-emulator-prereqs.ps1 -AllowMissingNdiSdk
- [ ] T003 Verify debug build artifact creation and append output in test-results/019-settings-three-pane-preflight.md using ./gradlew.bat :app:assembleDebug
- [ ] T004 Record any environment blocker and explicit unblock command in test-results/019-settings-three-pane-preflight.md

**Checkpoint**: Preflight is PASS or explicitly BLOCKED-ENV with reproducible unblocking steps.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare feature-specific validation scaffolding and traceability artifacts.

- [ ] T005 Create feature validation report stub in test-results/019-settings-three-pane-validation.md
- [ ] T006 Create feature Playwright test shell in testing/e2e/tests/settings-three-column-layout.spec.ts
- [ ] T007 [P] Create feature Playwright helper scaffold in testing/e2e/tests/support/settings-three-column-helpers.ts
- [ ] T008 [P] Add command or evidence trace table to specs/019-settings-three-pane/quickstart.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement shared contracts and wiring required before any user story work.

**CRITICAL**: No user story implementation starts until this phase is complete.

- [ ] T009 Extend settings layout state contract and interfaces in feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
- [ ] T010 [P] Add three-pane layout state models in core/model/src/main/java/com/ndi/core/model/NdiSettingsModels.kt
- [ ] T011 [P] Add dependency provider wiring for three-pane settings components in app/src/main/java/com/ndi/app/di/AppGraph.kt and feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDependencies.kt
- [ ] T012 [P] Add or update wide-layout detection utility surface in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsLayoutResolver.kt
- [ ] T013 Add settings navigation wiring for persistent main-navigation interactions in app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt and app/src/main/res/navigation/main_nav_graph.xml
- [ ] T014 Create baseline settings instrumentation harness for wide and compact mode assertions in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/SettingsLayoutModeTest.kt

**Checkpoint**: Foundation complete and user stories may begin.

---

## Phase 3: User Story 1 - Browse and configure settings in one workspace (Priority: P1) MVP

**Goal**: Users on wide layouts get a persistent three-column settings workspace with in-place detail updates.

**Independent Test**: Open settings on a wide profile, switch multiple categories, and verify columns 1 and 2 stay visible while only column 3 updates.

### Tests for User Story 1 (REQUIRED)

- [ ] T015 [P] [US1] Add failing ViewModel tests for three-column mode state emission in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsViewModelThreeColumnTest.kt
- [ ] T016 [P] [US1] Add failing ViewModel tests for category selection updating details only in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsCategorySelectionTest.kt
- [ ] T017 [P] [US1] Add failing instrumentation test for three-column rendering in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/SettingsThreePaneUiTest.kt
- [ ] T018 [P] [US1] Add failing Playwright e2e for wide-layout three-column rendering and category switching in testing/e2e/tests/settings-three-column-layout.spec.ts
- [ ] T019 [US1] Run existing Playwright regression suite and append output in test-results/019-settings-three-pane-validation.md using npm --prefix testing/e2e run test:pr:primary
- [ ] T020 [US1] If blocked, capture blocker classification and unblock command in test-results/019-settings-three-pane-validation.md

### Implementation for User Story 1

- [ ] T021 [P] [US1] Add three-pane settings strings and empty-state messages in feature/ndi-browser/presentation/src/main/res/values/strings.xml
- [ ] T022 [P] [US1] Create three-column settings layout resources in feature/ndi-browser/presentation/src/main/res/layout/fragment_settings_three_pane.xml and feature/ndi-browser/presentation/src/main/res/layout/item_settings_category.xml
- [ ] T023 [US1] Bind and render three-pane UI state from ViewModel in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt
- [ ] T024 [US1] Implement three-pane layout decision logic and selected-category state in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt
- [ ] T060 [US1] Render column-1 main navigation entries (Home, Stream, View, Settings) with selected-state projection in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt and feature/ndi-browser/presentation/src/main/res/layout/view_settings_main_navigation_panel.xml
- [ ] T025 [US1] Implement in-place detail-panel update path and selected-category highlight state in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsCategoryAdapter.kt
- [ ] T026 [US1] Render explicit empty-state message when selected category has no adjustable options in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDetailRenderer.kt

**Checkpoint**: User Story 1 is independently functional and demo-ready.

---

## Phase 4: User Story 2 - Keep context while navigating app sections (Priority: P2)

**Goal**: Users can navigate using column-1 main navigation while preserving coherent active-state behavior and returning to three-pane settings on wide layouts.

**Independent Test**: In three-pane settings, navigate to Home, Stream, and View from column 1, then return to Settings and confirm three-pane layout restores correctly.

### Tests for User Story 2 (REQUIRED)

- [ ] T027 [P] [US2] Add failing ViewModel tests for main-navigation action dispatch and active-state sync in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsMainNavigationStateTest.kt
- [ ] T028 [P] [US2] Add failing navigation integration test for column-1 destination routing in app/src/androidTest/java/com/ndi/app/navigation/SettingsMainNavigationRoutingTest.kt
- [ ] T029 [P] [US2] Add failing instrumentation test for return-to-settings three-pane restoration in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/SettingsReturnFlowTest.kt
- [ ] T030 [P] [US2] Add failing Playwright e2e for column-1 navigation routing and settings return behavior in testing/e2e/tests/settings-three-column-layout.spec.ts
- [ ] T031 [US2] Run existing Playwright regression suite and append output in test-results/019-settings-three-pane-validation.md using npm --prefix testing/e2e run test:pr:primary
- [ ] T032 [US2] If blocked, capture blocker classification and unblock command in test-results/019-settings-three-pane-validation.md

### Implementation for User Story 2

- [ ] T033 [P] [US2] Extend main-navigation panel UI resources for routed destination affordances in feature/ndi-browser/presentation/src/main/res/layout/view_settings_main_navigation_panel.xml
- [ ] T034 [US2] Implement main-navigation click intent dispatch only in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt
- [ ] T035 [US2] Implement ViewModel-driven main-navigation routing actions and return-to-settings restoration in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt
- [ ] T036 [US2] Wire navigation helper methods for three-pane main-navigation actions in app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt
- [ ] T037 [US2] Verify navigation graph compatibility for settings re-entry in app/src/main/res/navigation/main_nav_graph.xml

**Checkpoint**: User Story 2 is independently functional and testable.

---

## Phase 5: User Story 3 - Fall back gracefully on unsupported layouts (Priority: P3)

**Goal**: Compact mode remains fully usable on non-wide layouts, with selected settings category context preserved when transitioning between modes.

**Independent Test**: Start on wide three-pane, choose a category, rotate to compact and back, and confirm the category context is preserved where supported.

### Tests for User Story 3 (REQUIRED)

- [ ] T038 [P] [US3] Add failing ViewModel tests for compact fallback mode transitions and category restoration in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsLayoutTransitionTest.kt
- [ ] T039 [P] [US3] Add failing ViewModel tests for empty-category handling during transitions in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsDetailStateFallbackTest.kt
- [ ] T040 [P] [US3] Add failing instrumentation test for rotate wide to compact to wide continuity in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/SettingsRotationContinuityTest.kt
- [ ] T041 [P] [US3] Add failing Playwright e2e for compact fallback and context preservation in testing/e2e/tests/settings-three-column-layout.spec.ts
- [ ] T042 [US3] Run existing Playwright regression suite and append output in test-results/019-settings-three-pane-validation.md using npm --prefix testing/e2e run test:pr:primary
- [ ] T043 [US3] If blocked, capture blocker classification and unblock command in test-results/019-settings-three-pane-validation.md

### Implementation for User Story 3

- [ ] T044 [P] [US3] Implement layout transition resolver for wide and compact mode switching in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsLayoutResolver.kt
- [ ] T045 [US3] Implement selected-category context persistence across mode transitions in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt
- [ ] T046 [US3] Implement compact fallback rendering path and restoration hooks in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt
- [ ] T047 [US3] Add accessibility-focused behavior for larger text scale and long category labels in feature/ndi-browser/presentation/src/main/res/layout/fragment_settings_three_pane.xml and feature/ndi-browser/presentation/src/main/res/layout/item_settings_category.xml
- [ ] T048 [US3] Ensure compact and wide detail renderers share consistent empty-state semantics in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDetailRenderer.kt

**Checkpoint**: User Story 3 is independently functional and testable.

---

## Phase 6: Polish and Cross-Cutting Concerns

**Purpose**: Final validation, documentation, and release hardening across all stories.

- [ ] T049 [P] Update feature behavior documentation in docs/ndi-feature.md and docs/006-settings-menu-release-notes.md
- [ ] T050 [P] Add final validation narrative and gate outcomes in test-results/019-settings-three-pane-validation.md
- [ ] T051 Run touched module unit test suites and append output in test-results/019-settings-three-pane-validation.md using ./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest :feature:ndi-browser:data:testDebugUnitTest :app:testDebugUnitTest
- [ ] T052 Run feature Playwright coverage and capture artifacts in test-results/019-settings-three-pane-validation.md using npm --prefix testing/e2e run test -- tests/settings-three-column-layout.spec.ts
- [ ] T053 Run release hardening gate and append output in test-results/019-settings-three-pane-validation.md using ./gradlew.bat :app:verifyReleaseHardening :app:assembleRelease
- [ ] T054 Record Material 3 compliance verification outcomes in test-results/019-settings-three-pane-validation.md
- [ ] T055 If final gates are blocked, classify blocker vs code failure and record explicit unblock commands in test-results/019-settings-three-pane-validation.md
- [ ] T056 [P] Capture baseline usability metric for SC-001 and baseline feedback metric for SC-004 in test-results/019-settings-three-pane-validation.md before implementation comparison runs
- [ ] T057 Execute usability validation run for SC-001 and append measured completion-rate and timing evidence in test-results/019-settings-three-pane-validation.md
- [ ] T058 Define and document post-release feedback collection query and one-cycle comparison method for SC-004 in docs/006-settings-menu-release-notes.md and test-results/019-settings-three-pane-validation.md
- [ ] T059 Perform post-release SC-004 comparison checkpoint and append delta against baseline in test-results/019-settings-three-pane-validation.md

---

## Dependencies and Execution Order

### Phase Dependencies

- Phase 0 blocks all subsequent phases.
- Phase 1 depends on completion of Phase 0.
- Phase 2 depends on Phase 1 and blocks all user stories.
- Phases 3 to 5 depend on Phase 2 and can proceed by priority or in parallel staffing.
- Phase 6 depends on all targeted user stories being complete.

### User Story Dependencies

- User Story 1 (P1): starts immediately after Phase 2 and delivers MVP.
- User Story 2 (P2): starts after Phase 2 and depends on foundational navigation wiring from Phase 2.
- User Story 3 (P3): starts after Phase 2 and depends on US1 three-pane state model and renderer baseline.

### Within Each User Story

- Tests must be written first and fail before implementation.
- ViewModel and state behavior precede final UI wiring.
- Story-specific Playwright test and existing Playwright regression run are required before story completion.
- Blocked-gate evidence task is required when environment constraints prevent execution.

### Parallel Opportunities

- Phase 1 tasks T007 and T008 can run in parallel.
- Phase 2 tasks T010 to T012 can run in parallel after T009 starts.
- In each story, tasks marked [P] can run concurrently when touching different files.
- Story-specific Playwright test creation can run in parallel with unit or instrumentation test authoring.

---

## Parallel Example: User Story 1

- T015, T016, T017, and T018 can run in parallel.
- T021 and T022 can run in parallel.

## Parallel Example: User Story 3

- T038, T039, T040, and T041 can run in parallel.
- T044 and T047 can run in parallel.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 0, Phase 1, and Phase 2.
2. Complete Phase 3 for User Story 1.
3. Validate User Story 1 independently before adding more scope.

### Incremental Delivery

1. Complete shared setup and foundation.
2. Deliver User Story 1.
3. Deliver User Story 2.
4. Deliver User Story 3.
5. Execute final cross-cutting validation and release gates.

### Parallel Team Strategy

1. Team completes Phases 0 to 2 together.
2. After Phase 2 completion:
   - Engineer A leads US1.
   - Engineer B leads US2.
   - Engineer C leads US3.
3. Team reconverges for Phase 6 validation and release hardening.
