# Tasks: Theme Editor Settings

**Input**: Design documents from `/specs/016-json-shortname-settings/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are REQUIRED by constitution. Every user story includes failing-test-first unit/instrumentation tasks plus emulator-run Playwright e2e coverage and existing Playwright regression validation tasks.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Initialize module scaffolding and feature validation harness.

- [ ] T001 Create feature validation evidence file in test-results/016-theme-editor-validation.md
- [ ] T002 Add theme-editor module includes in settings.gradle.kts
- [ ] T003 [P] Create domain module gradle config in feature/theme-editor/domain/build.gradle.kts
- [ ] T004 [P] Create data module gradle config in feature/theme-editor/data/build.gradle.kts
- [ ] T005 [P] Create presentation module gradle config in feature/theme-editor/presentation/build.gradle.kts
- [ ] T006 [P] Create Playwright spec scaffold for theme mode flows in testing/e2e/tests/settings-theme-mode.spec.ts
- [ ] T007 [P] Create Playwright spec scaffold for accent and persistence flows in testing/e2e/tests/settings-theme-accent-persistence.spec.ts

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add shared model/storage/repository/theme plumbing required by all user stories.

**CRITICAL**: No user story work should begin until this phase is complete.

- [ ] T008 Define theme-editor domain repository contract in feature/theme-editor/domain/src/main/java/com/ndi/feature/themeeditor/domain/repository/ThemeEditorRepository.kt
- [ ] T009 [P] Define theme-editor domain models in feature/theme-editor/domain/src/main/java/com/ndi/feature/themeeditor/domain/model/ThemeEditorModels.kt
- [ ] T010 Extend shared settings model with theme fields in core/model/src/main/java/com/ndi/core/model/NdiSettingsModels.kt
- [ ] T011 [P] Extend Room settings schema for theme fields in core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt
- [ ] T012 Implement theme-editor data repository using Room-backed settings in feature/theme-editor/data/src/main/java/com/ndi/feature/themeeditor/data/repository/ThemeEditorRepositoryImpl.kt
- [ ] T013 [P] Add theme settings normalization mapper/defaults in feature/theme-editor/data/src/main/java/com/ndi/feature/themeeditor/data/repository/ThemePreferenceMapper.kt
- [ ] T014 Add data-layer persistence tests for theme fields in feature/theme-editor/data/src/test/java/com/ndi/feature/themeeditor/data/repository/ThemeEditorRepositoryImplTest.kt
- [ ] T015 Add app-level theme application coordinator in app/src/main/java/com/ndi/app/theme/AppThemeCoordinator.kt
- [ ] T016 Wire theme-editor dependencies and coordinator into app graph in app/src/main/java/com/ndi/app/di/AppGraph.kt
- [ ] T017 [P] Subscribe host lifecycle to theme coordinator in app/src/main/java/com/ndi/app/MainActivity.kt
- [ ] T018 [P] Add settings telemetry for theme-editor interactions in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsTelemetry.kt

**Checkpoint**: Foundation ready - story work can proceed.

---

## Phase 3: User Story 1 - Choose App Theme Mode (Priority: P1) MVP

**Goal**: Provide Light/Dark/System mode selection from settings and apply mode app-wide while preserving system-follow behavior.

**Independent Test**: Open settings, switch mode among Light/Dark/System, and observe immediate app appearance behavior for each mode.

### Tests for User Story 1 (REQUIRED)

- [ ] T019 [P] [US1] Add failing viewmodel tests for mode selection single-select state in feature/theme-editor/presentation/src/test/java/com/ndi/feature/themeeditor/ThemeEditorViewModelTest.kt
- [ ] T020 [P] [US1] Add failing unit tests for app theme mode mapping in app/src/test/java/com/ndi/app/theme/AppThemeCoordinatorTest.kt
- [ ] T021 [P] [US1] Add failing instrumentation test for theme mode controls in feature/theme-editor/presentation/src/androidTest/java/com/ndi/feature/themeeditor/ThemeEditorScreenTest.kt
- [ ] T022 [P] [US1] Implement failing emulator Playwright Light/Dark/System test in testing/e2e/tests/settings-theme-mode.spec.ts

### Implementation for User Story 1

- [ ] T023 [US1] Create theme-editor viewmodel mode state/actions in feature/theme-editor/presentation/src/main/java/com/ndi/feature/themeeditor/ThemeEditorViewModel.kt
- [ ] T024 [US1] Create theme-editor fragment mode bindings in feature/theme-editor/presentation/src/main/java/com/ndi/feature/themeeditor/ThemeEditorFragment.kt
- [ ] T025 [US1] Add theme mode controls and copy in feature/theme-editor/presentation/src/main/res/layout/fragment_theme_editor.xml and feature/theme-editor/presentation/src/main/res/values/strings.xml
- [ ] T026 [US1] Add settings-to-theme-editor navigation entry in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt and app/src/main/res/navigation/main_nav_graph.xml
- [ ] T027 [US1] Apply selected mode across app host integration points in app/src/main/java/com/ndi/app/theme/AppThemeCoordinator.kt and app/src/main/java/com/ndi/app/MainActivity.kt
- [ ] T028 [US1] Record US1 validation evidence in test-results/016-theme-editor-validation.md

**Checkpoint**: User Story 1 is independently functional and testable.

---

## Phase 4: User Story 2 - Pick Accent Color (Priority: P1)

**Goal**: Provide a curated 6-8 option accent palette with single-active selection and visual application.

**Independent Test**: Open theme editor, choose each curated accent option, and verify active selection + accent update on visible accent surfaces.

### Tests for User Story 2 (REQUIRED)

- [ ] T029 [P] [US2] Add failing viewmodel tests for curated accent option state in feature/theme-editor/presentation/src/test/java/com/ndi/feature/themeeditor/ThemeEditorViewModelTest.kt
- [ ] T030 [P] [US2] Add failing unit tests for accent token mapping/application in app/src/test/java/com/ndi/app/theme/AppThemeCoordinatorTest.kt
- [ ] T031 [P] [US2] Add failing instrumentation test for palette rendering and single-select behavior in feature/theme-editor/presentation/src/androidTest/java/com/ndi/feature/themeeditor/ThemeEditorScreenTest.kt
- [ ] T032 [P] [US2] Implement failing emulator Playwright accent selection test in testing/e2e/tests/settings-theme-accent-persistence.spec.ts

### Implementation for User Story 2

- [ ] T033 [US2] Add curated accent palette constants and defaults in feature/theme-editor/domain/src/main/java/com/ndi/feature/themeeditor/domain/model/ThemeEditorModels.kt
- [ ] T034 [US2] Add accent palette controls and labels in feature/theme-editor/presentation/src/main/res/layout/fragment_theme_editor.xml and feature/theme-editor/presentation/src/main/res/values/strings.xml
- [ ] T035 [US2] Implement accent selection actions/rendering in feature/theme-editor/presentation/src/main/java/com/ndi/feature/themeeditor/ThemeEditorViewModel.kt and feature/theme-editor/presentation/src/main/java/com/ndi/feature/themeeditor/ThemeEditorFragment.kt
- [ ] T036 [US2] Apply selected accent token through app theme coordinator in app/src/main/java/com/ndi/app/theme/AppThemeCoordinator.kt
- [ ] T037 [US2] Record US2 validation evidence in test-results/016-theme-editor-validation.md

**Checkpoint**: User Story 2 is independently functional and testable.

---

## Phase 5: User Story 3 - Keep Theme Preferences (Priority: P2)

**Goal**: Persist selected mode/accent across app relaunch and maintain System mode follow behavior after restore.

**Independent Test**: Select mode+accent, relaunch app, and verify persisted selections and expected system-follow behavior.

### Tests for User Story 3 (REQUIRED)

- [ ] T038 [P] [US3] Add failing data repository persistence/restore tests for defaults and normalization in feature/theme-editor/data/src/test/java/com/ndi/feature/themeeditor/data/repository/ThemeEditorRepositoryImplTest.kt
- [ ] T039 [P] [US3] Add failing viewmodel initialization tests for persisted theme values in feature/theme-editor/presentation/src/test/java/com/ndi/feature/themeeditor/ThemeEditorViewModelTest.kt
- [ ] T040 [P] [US3] Add failing instrumentation relaunch-state test in feature/theme-editor/presentation/src/androidTest/java/com/ndi/feature/themeeditor/ThemeEditorPersistenceTest.kt
- [ ] T041 [P] [US3] Implement failing emulator Playwright persistence scenario in testing/e2e/tests/settings-theme-accent-persistence.spec.ts

### Implementation for User Story 3

- [ ] T042 [US3] Persist and reload theme mode/accent fields in feature/theme-editor/data/src/main/java/com/ndi/feature/themeeditor/data/repository/ThemeEditorRepositoryImpl.kt
- [ ] T043 [US3] Ensure viewmodel initializes from persisted state and saves updates atomically in feature/theme-editor/presentation/src/main/java/com/ndi/feature/themeeditor/ThemeEditorViewModel.kt
- [ ] T044 [US3] Restore and apply persisted appearance on startup in app/src/main/java/com/ndi/app/theme/AppThemeCoordinator.kt and app/src/main/java/com/ndi/app/MainActivity.kt
- [ ] T045 [US3] Preserve system-follow behavior on restore in app/src/main/java/com/ndi/app/theme/AppThemeCoordinator.kt
- [ ] T046 [US3] Record US3 validation evidence in test-results/016-theme-editor-validation.md

**Checkpoint**: User Story 3 is independently functional and testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final integration, regression, and release validation.

- [ ] T047 [P] Update contract verification notes in specs/016-json-shortname-settings/contracts/settings-theme-editor-contract.md
- [ ] T048 Run touched-module JVM and instrumentation validation for new modules and record outcomes in test-results/016-theme-editor-validation.md
- [ ] T049 Add theme-editor flows to regression manifest in testing/e2e/tests/support/regression-suite-manifest.json
- [ ] T050 Run full existing Playwright e2e regression suite and record outcomes in test-results/016-theme-editor-validation.md
- [ ] T051 Run release hardening validation via .\gradlew.bat verifyReleaseHardening :app:assembleRelease and record outcomes in test-results/016-theme-editor-validation.md
- [ ] T052 [P] Update maintainer validation notes in specs/016-json-shortname-settings/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 (Setup): No dependencies.
- Phase 2 (Foundational): Depends on Phase 1 and blocks all story work.
- Phases 3-5 (User Stories): Depend on Phase 2 completion.
- Phase 6 (Polish): Depends on completion of all target user stories.

### User Story Dependencies

- US1: Starts immediately after Foundational; defines navigation and core mode controls.
- US2: Starts after Foundational and can proceed in parallel with US1 where files do not overlap.
- US3: Starts after Foundational; depends on persistence paths established by US1/US2 implementation.

### Within Each User Story

- Failing tests first.
- Domain/data state updates before UI rendering bindings.
- App-level application wiring after state/model changes.
- Evidence capture after implementation and validation.

### Parallel Opportunities

- Setup tasks T003, T004, T005, T006, and T007 can run in parallel.
- Foundational tasks T009, T011, T013, T017, and T018 can run in parallel.
- US1 test tasks T019-T022 can run in parallel.
- US2 test tasks T029-T032 can run in parallel.
- US3 test tasks T038-T041 can run in parallel.
- Polish tasks T047 and T052 can run in parallel with validation runs.

---

## Parallel Example: User Story 2

Run in parallel:

- `T029` viewmodel failing tests in `feature/theme-editor/presentation/src/test/java/com/ndi/feature/themeeditor/ThemeEditorViewModelTest.kt`
- `T030` coordinator failing tests in `app/src/test/java/com/ndi/app/theme/AppThemeCoordinatorTest.kt`
- `T031` instrumentation failing tests in `feature/theme-editor/presentation/src/androidTest/java/com/ndi/feature/themeeditor/ThemeEditorScreenTest.kt`
- `T032` Playwright failing tests in `testing/e2e/tests/settings-theme-accent-persistence.spec.ts`

---

## Implementation Strategy

### MVP First (US1)

1. Complete Setup + Foundational phases.
2. Deliver US1 (theme-editor entry + mode selection + application).
3. Validate US1 independently before advancing.

### Incremental Delivery

1. Add US2 curated accent palette and validate independently.
2. Add US3 persistence/relaunch guarantees and validate independently.
3. Complete final cross-cutting regression and release checks.

### Parallel Team Strategy

- Developer A: Theme-editor domain/data + unit tests.
- Developer B: Theme-editor presentation + instrumentation tests.
- Developer C: App host integration + Playwright e2e + validation evidence.

---

## Notes

- [P] tasks indicate parallelizable work with no unresolved dependencies.
- Every user story includes failing-test-first tasks and independent validation criteria.
- Visual-change tasks explicitly include emulator-run Playwright coverage and existing-suite regression validation.
