# Tasks: Background Stream Persistence

**Input**: Design documents from /specs/005-background-stream-persistence/  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Test tasks are REQUIRED by the feature specification and constitution TDD rule. Each story starts with failing tests.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare validation scaffolding and execution baselines.

- [X] T001 Create feature validation workspace in specs/005-background-stream-persistence/validation/foundation-checkpoint.md
- [X] T002 [P] Add scenario execution notes section in specs/005-background-stream-persistence/quickstart.md
- [X] T003 [P] Capture pre-change dual-emulator baseline in specs/005-background-stream-persistence/validation/e2e-baseline.md
- [X] T004 [P] Add feature run entry placeholder in specs/005-background-stream-persistence/validation/quickstart-validation-report.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared continuity/e2e infrastructure required before user story implementation.

**CRITICAL**: No user story work starts before this phase completes.

- [X] T005 Extend stream continuity model fields for background state in core/model/src/main/java/com/ndi/core/model/navigation/TopLevelNavigationModels.kt
- [X] T006 [P] Update stream continuity repository state handling in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/StreamContinuityRepositoryImpl.kt
- [X] T007 [P] Add reusable broadcaster app-switch helpers (home/chrome/url) in testing/e2e/tests/support/android-ui-driver.ts
- [X] T008 [P] Create ordered step checkpoint helper in testing/e2e/tests/support/scenario-checkpoints.ts
- [X] T009 Wire foundational continuity dependencies in app/src/main/java/com/ndi/app/di/AppGraph.kt
- [X] T010 Add scenario checkpoint artifact output support in testing/e2e/scripts/run-dual-emulator-e2e.ps1
- [X] T011 Record foundational completion evidence in specs/005-background-stream-persistence/validation/foundation-checkpoint.md

**Checkpoint**: Foundation complete; user stories can start.

---

## Phase 3: User Story 1 - Keep Stream Alive Across App Switching (Priority: P1) MVP

**Goal**: Keep active stream running when broadcaster leaves app to Home or another app.

**Independent Test**: Start stream on emulator A, leave app on A, confirm viewer on B remains PLAYING with live updates.

### Tests for User Story 1 (write first, must fail first)

- [X] T012 [P] [US1] Add failing top-level navigation continuity test in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTopLevelNavTest.kt
- [X] T013 [P] [US1] Add failing repository continuity transition tests in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/StreamContinuityRepositoryImplTest.kt
- [X] T014 [P] [US1] Add failing leave-app stream continuity UI test in app/src/androidTest/java/com/ndi/app/navigation/StreamBackgroundContinuityUiTest.kt

### Implementation for User Story 1

- [X] T015 [US1] Implement background-transition capture semantics in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/StreamContinuityRepositoryImpl.kt
- [X] T016 [US1] Prevent implicit output stop on app-switch/top-level transitions in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
- [X] T017 [US1] Keep output UI state consistent after app-switch return in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt
- [X] T018 [US1] Emit continuity telemetry for background transitions in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputTelemetry.kt
- [X] T019 [US1] Align continuity contract documentation comments in feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
- [X] T020 [US1] Capture US1 validation evidence in specs/005-background-stream-persistence/validation/us1-background-continuity-validation.md

**Checkpoint**: US1 independently validates that stream continuity persists during app switching.

---

## Phase 4: User Story 2 - Validate Cross-App Content Propagation (Priority: P2)

**Goal**: Prove Chrome and nos.nl activity on broadcaster appears in viewer.

**Independent Test**: Run dual-emulator scenario through Chrome open and nos.nl navigation and validate both are visible in viewer.

### Tests for User Story 2 (write first, must fail first)

- [X] T021 [P] [US2] Add failing Chrome-visibility scenario assertion in testing/e2e/tests/interop-dual-emulator.spec.ts
- [X] T022 [US2] Add failing nos.nl-visibility scenario assertion in testing/e2e/tests/interop-dual-emulator.spec.ts
- [X] T023 [P] [US2] Add failing browser-checkpoint visual assertion tests in testing/e2e/tests/support/visual-assertions.spec.ts

### Implementation for User Story 2

- [X] T024 [US2] Implement broadcaster transition helpers for Home and Chrome app launch in testing/e2e/tests/support/android-ui-driver.ts
- [X] T025 [US2] Implement Chrome URL navigation helper for https://nos.nl in testing/e2e/tests/support/android-ui-driver.ts
- [X] T026 [US2] Implement step-4 Chrome-visible receiver validation in testing/e2e/tests/interop-dual-emulator.spec.ts
- [X] T027 [US2] Implement step-6 nos.nl-visible receiver validation in testing/e2e/tests/interop-dual-emulator.spec.ts
- [X] T028 [US2] Attach per-checkpoint screenshots and diagnostics for browser visibility in testing/e2e/tests/interop-dual-emulator.spec.ts
- [X] T029 [US2] Capture US2 cross-app propagation evidence in specs/005-background-stream-persistence/validation/us2-cross-app-propagation-validation.md

**Checkpoint**: US2 independently validates cross-app browser content propagation to viewer.

---

## Phase 5: User Story 3 - Deterministic Dual-Emulator Verification Flow (Priority: P3)

**Goal**: Enforce exact six-step ordering, fail-fast semantics, and explicit step-level diagnostics.

**Independent Test**: Run automated six-step scenario and verify strict ordering and failed-step reporting when forced failure occurs.

### Tests for User Story 3 (write first, must fail first)

- [X] T030 [P] [US3] Add failing ordered-step enforcement tests in testing/e2e/tests/support/scenario-checkpoints.spec.ts
- [X] T031 [P] [US3] Add failing step-level failure reporting assertions in testing/e2e/tests/interop-dual-emulator.spec.ts
- [X] T032 [US3] Add failing checkpoint artifact-shape tests in testing/e2e/tests/support/scenario-checkpoints.spec.ts

### Implementation for User Story 3

- [X] T033 [US3] Enforce strict six-step execution sequence in testing/e2e/tests/interop-dual-emulator.spec.ts
- [X] T034 [US3] Integrate fail-fast checkpoint tracking helper in testing/e2e/tests/interop-dual-emulator.spec.ts
- [X] T035 [US3] Persist checkpoint timeline artifact for each run in testing/e2e/tests/interop-dual-emulator.spec.ts
- [X] T036 [US3] Surface failed-step diagnostics in runner output in testing/e2e/scripts/run-dual-emulator-e2e.ps1
- [X] T037 [US3] Update deterministic flow instructions in specs/005-background-stream-persistence/quickstart.md
- [X] T038 [US3] Capture US3 deterministic-flow evidence in specs/005-background-stream-persistence/validation/us3-deterministic-six-step-validation.md

**Checkpoint**: US3 independently validates deterministic orchestration and diagnostics.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, release gates, and documentation sync.

- [X] T039 [P] Update dual-emulator operator guidance for background continuity scenario in testing/e2e/README.md
- [X] T040 [P] Add success-criteria evidence matrix for SC-001..SC-005 in specs/005-background-stream-persistence/validation/success-criteria-report.md
- [X] T041 Run feature quickstart validation sequence and capture evidence in specs/005-background-stream-persistence/validation/quickstart-validation-report.md
- [X] T042 Run dual-emulator scenario and capture runtime/artifact evidence in specs/005-background-stream-persistence/validation/e2e-runtime-report.md
- [X] T043 Run release hardening commands and capture matrix in specs/005-background-stream-persistence/validation/release-validation-matrix.md
- [X] T044 Remove temporary debug traces from e2e helpers/spec after validation in testing/e2e/tests/support/android-ui-driver.ts and testing/e2e/tests/interop-dual-emulator.spec.ts
- [X] T045 Update feature documentation index entry in DOCUMENTATION-INDEX.md
- [X] T046 [P] Add explicit Material 3 compliance verification evidence for touched output/view surfaces in specs/005-background-stream-persistence/validation/material3-compliance-report.md
- [X] T047 Add battery-conscious execution justification with lifecycle-bound cancellation and measurable energy-impact evidence in specs/005-background-stream-persistence/validation/battery-lifecycle-continuity-report.md
- [X] T048 Synchronize planning/design inventory to include scenario checkpoint helper artifacts in specs/005-background-stream-persistence/plan.md and specs/005-background-stream-persistence/data-model.md

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup (Phase 1): no dependencies.
- Foundational (Phase 2): depends on Setup completion and blocks all stories.
- User Stories (Phases 3-5): all depend on Foundational completion.
- Polish (Phase 6): depends on completion of desired user stories.

### User Story Dependencies

- US1 (P1): starts immediately after Phase 2; no dependency on US2 or US3.
- US2 (P2): starts after Phase 2; depends on foundational e2e helpers, not on US1 internals.
- US3 (P3): starts after Phase 2; depends on foundational checkpoint helper and can proceed independently of US1 behavior implementation details.

### Within Each User Story

- Tests must be written first and verified failing.
- Core state/model updates before orchestration and UI binding refinements.
- Story evidence task is required to close each story.

## Parallel Opportunities

- Setup: T002, T003, T004 can run in parallel.
- Foundational: T006, T007, T008 can run in parallel.
- US1 tests: T012, T013, T014 can run in parallel.
- US2 tests: T021 and T023 can run in parallel.
- US3 tests: T030 and T031 can run in parallel.
- Polish: T039, T040, and T046 can run in parallel.

## Parallel Example: User Story 1

- Task T012 in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTopLevelNavTest.kt
- Task T013 in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/StreamContinuityRepositoryImplTest.kt
- Task T014 in app/src/androidTest/java/com/ndi/app/navigation/StreamBackgroundContinuityUiTest.kt

## Parallel Example: User Story 2

- Task T021 in testing/e2e/tests/interop-dual-emulator.spec.ts
- Task T023 in testing/e2e/tests/support/visual-assertions.spec.ts

## Parallel Example: User Story 3

- Task T030 in testing/e2e/tests/support/scenario-checkpoints.spec.ts
- Task T031 in testing/e2e/tests/interop-dual-emulator.spec.ts

## Implementation Strategy

### MVP First (US1 only)

1. Complete Setup and Foundational phases.
2. Complete US1 with test-first flow.
3. Validate stream continuity on app switching before expanding scope.

### Incremental Delivery

1. Build shared foundation once (Phases 1-2).
2. Deliver US1 and validate independently.
3. Deliver US2 and validate independently.
4. Deliver US3 and validate independently.
5. Finish cross-cutting release evidence tasks.

### Parallel Team Strategy

1. Team completes foundational tasks together.
2. After foundation: one developer handles US1 while another handles US2 test scaffolding.
3. US3 deterministic checkpoint work proceeds in parallel once checkpoint helper is in place.
4. Validation owner completes Phase 6 evidence and release checks.

## Notes

- All tasks use required checklist syntax with task IDs and explicit file paths.
- Story labels are used only in user-story phases.
- Tasks preserve architecture constraints from AGENTS.md and constitution principles.
- The six-step Chrome/nos.nl scenario is encoded as a first-class delivery target, not an optional post-task.
