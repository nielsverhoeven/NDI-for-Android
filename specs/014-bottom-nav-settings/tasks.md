# Tasks: Bottom Navigation Settings Access

**Input**: Design documents from /specs/014-bottom-nav-settings/
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/bottom-nav-settings-ui-contract.md, quickstart.md

**Tests**: Tests are REQUIRED by constitution and this feature changes visible UI behavior. Each user story includes failing-test-first tasks plus emulator-run Playwright coverage and existing-suite regression validation.

**Organization**: Tasks are grouped by user story to preserve independent implementation and independent validation.

## Format: [ID] [P?] [Story] Description

- [P]: Task can run in parallel (different files, no dependency on incomplete tasks)
- [Story]: User story label for story-phase tasks only (US1, US2, US3)
- Every task description includes concrete file path(s)

## Phase 1: Setup (Shared Navigation Scaffolding)

**Purpose**: Prepare shared resources and validation artifacts needed by all stories.

- [ ] T001 Create feature validation evidence file with red/green/regression sections in test-results/bottom-nav-settings-validation.md
- [ ] T002 [P] Add Settings tab label and accessibility strings in app/src/main/res/values/strings.xml
- [ ] T003 [P] Add bottom-nav Settings icon resource in app/src/main/res/drawable/ic_nav_settings.xml
- [ ] T004 [P] Add Settings item to top-level bottom-nav menu in app/src/main/res/menu/top_level_navigation_menu.xml

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Extend shared top-level navigation model and host wiring before story-specific behavior.

**CRITICAL**: No user story work begins until this phase is complete.

- [ ] T005 Extend TopLevelDestination model to include SETTINGS in core/model/src/main/java/com/ndi/core/model/navigation/TopLevelNavigationModels.kt
- [ ] T006 Map SETTINGS destination id in app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt
- [ ] T007 Update destination-id resolution and top-level checks for SETTINGS in app/src/main/java/com/ndi/app/navigation/TopLevelNavigationHost.kt
- [ ] T008 Update top-level selection UI state/events to include SETTINGS in app/src/main/java/com/ndi/app/navigation/TopLevelNavViewModel.kt
- [ ] T009 Update destination observation and event handling for settings destination in app/src/main/java/com/ndi/app/MainActivity.kt
- [ ] T010 [P] Add failing JUnit coverage for SETTINGS enum mapping and destination id routing in app/src/test/java/com/ndi/app/navigation/NdiNavigationSettingsTest.kt
- [ ] T011 [P] Add failing JUnit coverage for SETTINGS selection state and events in app/src/test/java/com/ndi/app/navigation/TopLevelNavViewModelTest.kt
- [ ] T012 [P] Add failing JUnit coverage for SETTINGS nav item resolution and top-level checks in app/src/test/java/com/ndi/app/navigation/TopLevelNavigationCoordinatorTest.kt
- [ ] T013 Update shared Android UI driver helpers for bottom-nav settings selection in testing/e2e/tests/support/android-ui-driver.ts
- [ ] T014 Update suite classification metadata for bottom-nav settings feature coverage in testing/e2e/tests/support/e2e-suite-classification.spec.ts

**Checkpoint**: Shared navigation foundation complete; user stories can proceed.

---

## Phase 3: User Story 1 - Open Settings From Bottom Navigation (Priority: P1) MVP

**Goal**: Users can open settings from Home, Stream, and View through a dedicated Settings bottom-nav item.

**Independent Test**: From each non-settings tab (Home, Stream, View), select Settings and verify settings is visible and selected-state highlights Settings.

### Tests for User Story 1 (REQUIRED)

- [ ] T015 [P] [US1] Add failing Playwright coverage for Home to Settings bottom-nav entry in testing/e2e/tests/settings-navigation-source-list.spec.ts
- [ ] T016 [P] [US1] Add failing Playwright coverage for Stream to Settings bottom-nav entry in testing/e2e/tests/settings-navigation-output.spec.ts
- [ ] T017 [P] [US1] Add failing Playwright coverage for View to Settings bottom-nav entry in testing/e2e/tests/settings-navigation-viewer.spec.ts
- [ ] T018 [US1] Record US1 red-phase evidence for unit and Playwright runs in test-results/bottom-nav-settings-validation.md

### Implementation for User Story 1

- [ ] T019 [US1] Add NavigateToSettings event and emit path in app/src/main/java/com/ndi/app/navigation/TopLevelNavViewModel.kt
- [ ] T020 [US1] Handle NavigateToSettings in activity event routing in app/src/main/java/com/ndi/app/MainActivity.kt
- [ ] T021 [US1] Ensure settings destination maps to selected bottom-nav state in app/src/main/java/com/ndi/app/MainActivity.kt
- [ ] T022 [US1] Ensure settings destination remains canonical in navigation graph in app/src/main/res/navigation/main_nav_graph.xml
- [ ] T023 [US1] Update top-level navigation telemetry assertions for Settings transitions in app/src/main/java/com/ndi/app/navigation/TopLevelNavigationTelemetry.kt

**Checkpoint**: Settings can be opened from Home/Stream/View via bottom navigation and state highlight is correct.

---

## Phase 4: User Story 2 - Leave Settings Through Bottom Navigation (Priority: P2)

**Goal**: Users can leave settings by selecting Home, Stream, or View from bottom navigation with synchronized selected-state behavior.

**Independent Test**: Open settings, then select each non-settings tab and verify destination and selected highlight match.

### Tests for User Story 2 (REQUIRED)

- [x] T024 [P] [US2] Add failing JUnit coverage for settings-to-non-settings transitions in app/src/test/java/com/ndi/app/navigation/TopLevelNavViewModelTest.kt
- [x] T025 [P] [US2] Add failing JUnit coverage for settings nav-host route resolution in app/src/test/java/com/ndi/app/navigation/TopLevelNavigationCoordinatorTest.kt
- [x] T026 [P] [US2] Add failing Playwright coverage for Settings to Home/Stream/View exit behavior in testing/e2e/tests/settings-navigation-source-list.spec.ts
- [x] T026a [P] [US2] Add failing Playwright coverage for rapid tab switching (Settings ↔ Home/Stream/View) state synchronization in testing/e2e/tests/settings-navigation-source-list.spec.ts
- [x] T026b [P] [US2] Add failing Playwright coverage for rotation/device-orientation changes while in Settings (verify no crashes, state preserved) in testing/e2e/tests/settings-navigation-source-list.spec.ts
- [x] T027 [US2] Record US2 red-phase evidence for unit and Playwright runs (including rapid-switch and rotation scenarios) in test-results/bottom-nav-settings-validation.md

### Implementation for User Story 2

- [x] T028 [US2] Implement settings-to-home/stream/view event emission and no-op reselect rules in app/src/main/java/com/ndi/app/navigation/TopLevelNavViewModel.kt
- [x] T029 [US2] Implement settings-to-home/stream/view navigation dispatch in app/src/main/java/com/ndi/app/navigation/TopLevelNavigationHost.kt
- [x] T030 [US2] Keep destination observer synchronization for settings exit and deep-link starts in app/src/main/java/com/ndi/app/MainActivity.kt
- [x] T031 [US2] Preserve settings header/title visibility while active in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt

**Checkpoint**: Settings exit via bottom navigation is deterministic and selected state remains synchronized.

---

## Phase 5: User Story 3 - Remove Top-Right Settings Entry Affordance (Priority: P3)

**Goal**: Remove top-right settings entry controls on in-scope screens while keeping bottom-nav settings access.

**Independent Test**: Source list, viewer, output, and settings surfaces show no top-right settings entry control; settings remains reachable through bottom nav.

### Tests for User Story 3 (REQUIRED)

- [x] T032 [P] [US3] Add failing Playwright assertion for no top-right settings affordance on source-list context in testing/e2e/tests/settings-navigation-source-list.spec.ts
- [x] T033 [P] [US3] Add failing Playwright assertion for no top-right settings affordance on viewer context in testing/e2e/tests/settings-navigation-viewer.spec.ts
- [x] T034 [P] [US3] Add failing Playwright assertion for no top-right settings affordance on output and settings contexts in testing/e2e/tests/settings-navigation-output.spec.ts
- [x] T035 [US3] Record US3 red-phase evidence for Playwright checks in test-results/bottom-nav-settings-validation.md

### Implementation for User Story 3

- [x] T036 [P] [US3] Remove top-right settings action from source list menu in feature/ndi-browser/presentation/src/main/res/menu/source_list_menu.xml
- [x] T037 [P] [US3] Remove top-right settings action from viewer menu in feature/ndi-browser/presentation/src/main/res/menu/viewer_menu.xml
- [x] T038 [P] [US3] Remove top-right settings action from output menu in feature/ndi-browser/presentation/src/main/res/menu/output_menu.xml
- [x] T039 [P] [US3] Remove top-right settings action from settings menu in feature/ndi-browser/presentation/src/main/res/menu/settings_menu.xml
- [x] T040 [US3] Remove source-list toolbar settings click handling in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
- [x] T041 [US3] Remove viewer toolbar settings click handling in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt
- [x] T042 [US3] Remove output toolbar settings click handling in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlFragment.kt
- [x] T043 [US3] Remove settings-screen top-right settings click handling while preserving title/header in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt

**Checkpoint**: No in-scope top-right settings entry remains; bottom-nav access is canonical.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, documentation alignment, and release gate evidence.

- [x] T044 [P] Update quickstart validation notes for final command set in specs/014-bottom-nav-settings/quickstart.md
- [x] T044a Verify Material Design 3 compliance of bottom-nav Settings item (colors, typography, spacing, ripple effects, dark mode contrast) in a PR checklist: specs/014-bottom-nav-settings/Material3-Compliance-Verification.md
- [x] T045 Run unit test suite for app and presentation navigation changes and capture pass output in test-results/bottom-nav-settings-validation.md
- [x] T046 Run emulator Playwright bottom-nav settings scenarios and capture pass output in test-results/bottom-nav-settings-validation.md
- [x] T047 Run existing Playwright regression suite and capture pass output in test-results/bottom-nav-settings-validation.md
- [x] T048 Run release-hardening verification and capture results in test-results/bottom-nav-settings-validation.md

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 (Setup): no dependencies.
- Phase 2 (Foundational): depends on Phase 1 and blocks all user stories.
- Phase 3 (US1): depends on Phase 2.
- Phase 4 (US2): depends on Phase 2; can run after US1 foundational event plumbing exists.
- Phase 5 (US3): depends on Phase 2; should land after US1 so canonical bottom-nav entry already works.
- Phase 6 (Polish): depends on completion of all targeted user stories.

### User Story Dependencies

- US1 (P1): first MVP slice after foundational completion.
- US2 (P2): depends on shared settings destination support from foundational and validated US1 entry path.
- US3 (P3): depends on US1 bottom-nav entry path so removal of top-right affordance does not reduce access.

### Within Each User Story

- Write failing tests first and record red-phase evidence before implementation tasks.
- Complete navigation model/event wiring before screen-level bindings.
- Validate story independently before moving to next priority.

### Parallel Opportunities

- Setup tasks T002-T004 are parallelizable.
- Foundational tests T010-T012 are parallelizable.
- Playwright story tests T015-T017, T024-T026b, and T032-T034 are parallelizable.
- Menu-resource removal tasks T036-T039 are parallelizable.
- Final validation tasks T045-T049 can run in parallel where infrastructure allows.

---

## Parallel Example: User Story 1

```bash
Task: T015 [US1] Home to Settings Playwright entry coverage in testing/e2e/tests/settings-navigation-source-list.spec.ts
Task: T016 [US1] Stream to Settings Playwright entry coverage in testing/e2e/tests/settings-navigation-output.spec.ts
Task: T017 [US1] View to Settings Playwright entry coverage in testing/e2e/tests/settings-navigation-viewer.spec.ts
```

## Parallel Example: User Story 3

```bash
Task: T036 [US3] Remove source-list top-right settings action in feature/ndi-browser/presentation/src/main/res/menu/source_list_menu.xml
Task: T037 [US3] Remove viewer top-right settings action in feature/ndi-browser/presentation/src/main/res/menu/viewer_menu.xml
Task: T038 [US3] Remove output top-right settings action in feature/ndi-browser/presentation/src/main/res/menu/output_menu.xml
Task: T039 [US3] Remove settings top-right settings action in feature/ndi-browser/presentation/src/main/res/menu/settings_menu.xml
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Complete Setup and Foundational phases.
2. Deliver US1 entry behavior and selection synchronization.
3. Validate US1 independently as the MVP.

### Incremental Delivery

1. Add US2 exit behavior and synchronization safeguards (including rapid-switch and rotation edge cases).
2. Add US3 top-right affordance removals once bottom-nav path is stable.
3. Run Material 3 compliance verification and full polish validation.
4. Run release hardening checks.

### Parallel Team Strategy

1. Developer A: foundational model and app navigation wiring (T005-T009).
2. Developer B: failing tests and Playwright updates per story (T010-T018, T024-T027, T032-T035).
3. Developer C: presentation/menu cleanup tasks (T036-T043).
4. Merge and run final validation tasks (T045-T049).
