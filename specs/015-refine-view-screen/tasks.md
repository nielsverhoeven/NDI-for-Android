# Tasks: Refine View Screen Controls

**Input**: Design documents from /specs/015-refine-view-screen/
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are REQUIRED by constitution. Every user story includes failing-test-first unit and visual e2e coverage, plus existing Playwright regression validation.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Initialize feature-specific test and validation scaffolding.

- [X] T001 Create feature test evidence document scaffold in test-results/015-view-screen-controls-validation.md
- [X] T002 [P] Create Playwright spec scaffold for US1 in testing/e2e/tests/us1-view-source-filtering.spec.ts
- [X] T003 [P] Create Playwright spec scaffold for US2 and US3 in testing/e2e/tests/us2-us3-view-interactions.spec.ts

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add shared UI/test affordances required by all user stories.

**CRITICAL**: No user story work should begin until this phase is complete.

- [X] T004 Add stable source-list UI test tags for row container, action button, refresh button, loading icon, and inline refresh error in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
- [X] T005 [P] Add adapter-level binding hooks for row click enablement and action button callbacks in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/adapter/SourceAdapter.kt
- [X] T006 [P] Extend shared Android UI driver helpers for source-list action button tapping and non-button row tapping in testing/e2e/tests/support/android-ui-driver.ts
- [X] T007 Add common refresh UI state and refresh error state holders in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [X] T008 Add baseline compose/instrumentation assertions for new source-list test tags in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/source_list/SourceListScreenTest.kt

**Checkpoint**: Foundation ready - user story implementation can proceed.

---

## Phase 3: User Story 1 - View Only External Sources (Priority: P1) MVP

**Goal**: Exclude current device from visible source options while keeping other sources available.

**Independent Test**: Launch view screen on a discoverable device and verify the local device is absent while external sources remain listed.

### Tests for User Story 1 (REQUIRED)

- [X] T009 [P] [US1] Add failing ViewModel test for current-device filtering in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt
- [X] T010 [P] [US1] Add failing instrumentation test for rendered list exclusion in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/source_list/SourceListScreenTest.kt
- [X] T011 [P] [US1] Implement failing emulator Playwright scenario for current-device exclusion in testing/e2e/tests/us1-view-source-filtering.spec.ts
- [X] T012 [P] [US1] Add failing existing-suite regression manifest entry for US1 flow in testing/e2e/tests/support/regression-suite-manifest.json

### Implementation for User Story 1

- [X] T013 [US1] Implement current-device filtering in source list state mapping in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [X] T014 [US1] Implement filtered list rendering path in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
- [ ] T015 [US1] Record US1 test and e2e pass evidence in test-results/015-view-screen-controls-validation.md

**Checkpoint**: User Story 1 is independently functional and testable.

---

## Phase 4: User Story 2 - Start Viewing Through Explicit Action (Priority: P1)

**Goal**: Make only the view stream button actionable per row and remove direct output-start action from the view screen.

**Independent Test**: Verify row body taps do nothing, view stream button opens viewer, and no direct start-output button is present.

### Tests for User Story 2 (REQUIRED)

- [X] T016 [P] [US2] Add failing ViewModel/navigation event test for button-only viewer open in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt
- [X] T017 [P] [US2] Add failing instrumentation test asserting non-button row taps are inert in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/source_list/SourceListScreenTest.kt
- [X] T018 [P] [US2] Add failing instrumentation test asserting no start-output action on view screen in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/source_list/SourceListScreenTest.kt
- [X] T019 [P] [US2] Implement failing emulator Playwright scenario for button-only click behavior and output-action absence in testing/e2e/tests/us2-us3-view-interactions.spec.ts
- [X] T020 [P] [US2] Add failing existing-suite regression manifest entry for US2 flow in testing/e2e/tests/support/regression-suite-manifest.json

### Implementation for User Story 2

- [X] T021 [US2] Implement button-only row interaction policy and inert row-body taps in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/adapter/SourceAdapter.kt
- [X] T022 [US2] Remove or disable direct output-start action exposure from view screen UI in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
- [X] T023 [US2] Ensure viewer routing is emitted only from view stream button action in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [ ] T024 [US2] Record US2 test and e2e pass evidence in test-results/015-view-screen-controls-validation.md

**Checkpoint**: User Story 2 is independently functional and testable.

---

## Phase 5: User Story 3 - Refresh Feedback and Placement (Priority: P2)

**Goal**: Place refresh control bottom-left, show adjacent loading state, disable during refresh, preserve list while refreshing, and show inline non-blocking error on refresh failure.

**Independent Test**: Trigger success and failure refresh flows and verify placement, disabled state, loading indicator adjacency, preserved list behavior, and inline error behavior.

### Tests for User Story 3 (REQUIRED)

- [X] T025 [P] [US3] Add failing ViewModel test for refresh in-flight state transitions in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt
- [X] T026 [P] [US3] Add failing ViewModel test for refresh failure preserving list and inline error state in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt
- [X] T027 [P] [US3] Add failing instrumentation test for bottom-left refresh placement and adjacent loading icon in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/source_list/SourceListScreenTest.kt
- [X] T028 [P] [US3] Add failing instrumentation test for disabled refresh during in-flight and re-enabled afterward in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/source_list/SourceListScreenTest.kt
- [X] T029 [P] [US3] Implement failing emulator Playwright scenario for refresh success/failure behavior in testing/e2e/tests/us2-us3-view-interactions.spec.ts
- [X] T030 [P] [US3] Add failing existing-suite regression manifest entry for US3 flow in testing/e2e/tests/support/regression-suite-manifest.json

### Implementation for User Story 3

- [X] T031 [US3] Implement bottom-left refresh control placement and adjacent loading indicator rendering in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
- [X] T032 [US3] Implement refresh in-flight disablement and completion re-enable behavior in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [X] T033 [US3] Implement preserve-list-on-refresh and replace-on-success state transition in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [X] T034 [US3] Implement inline non-blocking refresh error feedback near refresh controls in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
- [ ] T035 [US3] Record US3 test and e2e pass evidence in test-results/015-view-screen-controls-validation.md

**Checkpoint**: User Story 3 is independently functional and testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final quality pass across stories.

- [X] T036 [P] Update feature contract verification notes in specs/015-refine-view-screen/contracts/ndi-view-screen-controls-contract.md
- [ ] T037 Run full JUnit and instrumentation regression for touched modules via .\gradlew.bat test connectedAndroidTest and record results in test-results/015-view-screen-controls-validation.md
- [ ] T038 Run full existing Playwright e2e suite regression and record pass evidence in test-results/015-view-screen-controls-validation.md
- [X] T039 Run release hardening validation via .\gradlew.bat verifyReleaseHardening :app:assembleRelease and record results in test-results/015-view-screen-controls-validation.md
- [X] T040 [P] Update quick validation notes for maintainers in specs/015-refine-view-screen/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup (Phase 1): no dependencies.
- Foundational (Phase 2): depends on Setup completion and blocks user stories.
- User stories (Phases 3-5): depend on Foundational completion.
- Polish (Phase 6): depends on completion of all targeted user stories.

### User Story Dependencies

- US1 (P1): can start immediately after Foundational completion.
- US2 (P1): can start after Foundational completion; shares source-list files with US1, so sequence or careful coordination is recommended.
- US3 (P2): can start after Foundational completion; relies on shared refresh state scaffolding from Foundational tasks.

### Within Each User Story

- Tests are written first and must fail before implementation.
- ViewModel state and policy updates precede UI rendering changes.
- Implementation completes before evidence capture.

### Parallel Opportunities

- Phase 1 scaffolding tasks T002 and T003 can run in parallel.
- Phase 2 tasks T005 and T006 can run in parallel.
- Within US1, T009-T012 can run in parallel.
- Within US2, T016-T020 can run in parallel.
- Within US3, T025-T030 can run in parallel.
- Polish tasks T036 and T040 can run in parallel with non-conflicting validation runs.

---

## Parallel Example: User Story 3

- [ ] T025 [P] [US3] Add failing ViewModel test for refresh in-flight state transitions in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt
- [ ] T026 [P] [US3] Add failing ViewModel test for refresh failure preserving list and inline error state in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt
- [ ] T027 [P] [US3] Add failing instrumentation test for bottom-left refresh placement and adjacent loading icon in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/source_list/SourceListScreenTest.kt
- [ ] T029 [P] [US3] Implement failing emulator Playwright scenario for refresh success/failure behavior in testing/e2e/tests/us2-us3-view-interactions.spec.ts

---

## Implementation Strategy

### MVP First (US1)

- Complete Phase 1 and Phase 2.
- Deliver Phase 3 (US1) completely.
- Validate US1 independently before continuing.

### Incremental Delivery

- Add US2 after US1 validation to complete interaction model refinements.
- Add US3 after US2 to complete refresh behavior and failure feedback.
- Run Phase 6 cross-cutting regression and release validation.

### Parallel Team Strategy

- Developer A: ViewModel and unit tests.
- Developer B: Compose/instrumentation updates.
- Developer C: Playwright/e2e and validation evidence.
- Coordinate sequencing on shared files in source_list package to avoid merge conflicts.

---

## Notes

- Tasks marked [P] are parallelizable when there are no incomplete dependencies.
- Every user story contains failing-test-first tasks and independent validation criteria.
- Visual UI changes include both new emulator Playwright coverage and full existing-suite regression tasks.
