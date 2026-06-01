# Tasks: Fluent + Electron UX Redesign

**Input**: Design documents from `/specs/032-fluent-electron-redesign/`  
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are REQUIRED by constitution. Every user story includes failing-test-first tasks, Playwright emulator coverage, existing Playwright regression validation, and Fluent + Electron compliance evidence capture under `test-results/`.

**Organization**: Tasks are grouped by user story for independent implementation and validation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: `US1`, `US2`, `US3`
- Every task includes exact file paths

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Ensure runtime dependencies are ready before implementation/validation.

- [x] T001 Verify Android prerequisites and capture output in `test-results/032-preflight.md` using `scripts/verify-android-prereqs.ps1`
- [x] T002 Verify dual-emulator prerequisites and capture output in `test-results/032-preflight.md` using `scripts/verify-e2e-dual-emulator-prereqs.ps1`
- [x] T003 Verify device/emulator connectivity and Playwright contract (`adb devices`, `testing/e2e/scripts/validate-command-contract.ps1`) and append to `test-results/032-preflight.md`

**Checkpoint**: Environment is confirmed ready or blockers are explicitly documented.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create redesign-specific validation scaffolding and artifacts.

- [x] T004 Create feature evidence index at `test-results/032-fluent-electron-validation-index.md` listing required per-screen compliance artifacts
- [x] T005 [P] Create Playwright spec scaffold for redesign flows at `testing/e2e/tests/032-fluent-electron-redesign.spec.ts`
- [x] T006 [P] Create Fluent + Electron checklist template for this feature at `test-results/032-fluent-electron-checklist-template.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared design-language foundation used by all user stories.

**CRITICAL**: No user story implementation before this phase completes.

- [x] T007 Define Fluent + Electron token resource roles in `app/src/main/res/values/colors.xml` and `app/src/main/res/values/themes.xml`
- [x] T008 [P] Add shared presentation token helpers in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/home/HomeScreen.kt` (or a new UI helper in same module) and wire token usage baseline
- [x] T009 [P] Add top-level navigation shell baseline updates in `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationHost.kt` and `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationCoordinator.kt`
- [x] T010 Add/adjust unit tests for top-level navigation style-state invariants in `app/src/test/java/com/ndi/app/navigation/TopLevelNavViewModelTest.kt` and `app/src/test/java/com/ndi/app/navigation/TopLevelNavigationCoordinatorTest.kt` (failing first)
- [x] T011 Add contract-level artifact checklist requirements in `specs/032-fluent-electron-redesign/contracts/fluent-electron-redesign-contract.md` only if implementation reveals missing enforceable checks
- [x] T012 Run baseline unit tests (`:app:testDebugUnitTest`, `:feature:ndi-browser:presentation:testDebugUnitTest`) and capture in `test-results/032-foundation-tests.md`

**Checkpoint**: Design-language baseline is reusable and test-guarded.

---

## Phase 3: User Story 1 - Coherent Fluent + Electron Shell (Priority: P1)

**Goal**: Deliver consistent Fluent + Electron shell and state semantics across in-scope screens.

**Independent Test**: Launch and navigate Home/Source List/Viewer/Output/Settings; verify shell consistency and state hierarchy.

### Tests for User Story 1 (REQUIRED)

- [x] T013 [P] [US1] Add failing unit tests for Source List visual-state mapping in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListUiStateTest.kt`
- [x] T014 [P] [US1] Add failing unit tests for Viewer state presentation contracts in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModelTest.kt`
- [x] T015 [P] [US1] Add failing unit tests for Output visual-state presentation in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTest.kt`
- [x] T016 [P] [US1] Expand `testing/e2e/tests/032-fluent-electron-redesign.spec.ts` with shell-consistency and state-transition checks on emulator
- [x] T017 [US1] Run existing Playwright regression suite and append results to `test-results/032-us1-regression.md`
- [x] T018 [US1] Record US1 Fluent + Electron compliance evidence in `test-results/032-fluent-electron-nav-shell.md`
- [ ] T019 [US1] If blocked by environment, record blocker classification and remediation in `test-results/032-us1-regression.md`

### Implementation for User Story 1

- [x] T020 [P] [US1] Apply Fluent + Electron shell updates in `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationHost.kt`
- [x] T021 [P] [US1] Apply Source List redesign treatment in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt` and `feature/ndi-browser/presentation/src/main/res/layout/fragment_source_list.xml`
- [x] T022 [P] [US1] Apply Viewer redesign treatment in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt` and `feature/ndi-browser/presentation/src/main/res/layout/fragment_viewer.xml`
- [x] T023 [P] [US1] Apply Output redesign treatment in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt` and `feature/ndi-browser/presentation/src/main/res/layout/fragment_output_control.xml`
- [x] T024 [US1] Validate no mixed legacy/redesigned UI appears within shipped US1 flow and record in `test-results/032-fluent-electron-nav-shell.md`

**Checkpoint**: US1 shell and major surfaces are visually coherent and independently testable.

---

## Phase 4: User Story 2 - Core Flow Completion with Redesigned Components (Priority: P2)

**Goal**: Preserve task completion in source discovery/view/output/settings with redesigned components.

**Independent Test**: Execute discover -> view/output -> settings update flow and verify unchanged functional outcomes.

### Tests for User Story 2 (REQUIRED)

- [x] T025 [P] [US2] Add failing tests for Source List selection continuity in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt`
- [x] T026 [P] [US2] Add failing tests for Viewer task-completion invariants in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModelTopLevelNavTest.kt`
- [x] T027 [P] [US2] Add failing tests for Output control task flow in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTopLevelNavTest.kt`
- [x] T028 [P] [US2] Add failing tests for settings persistence behavior invariants in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsViewModelTest.kt`
- [x] T029 [P] [US2] Extend `testing/e2e/tests/032-fluent-electron-redesign.spec.ts` with end-to-end task completion checks (discover -> view/output -> settings)
- [x] T030 [US2] Run existing Playwright regression suite and capture in `test-results/032-us2-regression.md`
- [x] T031 [US2] Record US2 Fluent + Electron compliance evidence in `test-results/032-fluent-electron-source-list-viewer.md` and `test-results/032-fluent-electron-output.md`
- [ ] T032 [US2] If blocked by environment, record blocker classification and remediation in `test-results/032-us2-regression.md`

### Implementation for User Story 2

- [x] T033 [P] [US2] Refine Source List flow UI interactions in `feature/ndi-browser/presentation/src/main/res/layout/item_ndi_source.xml` and `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt`
- [x] T034 [P] [US2] Refine Viewer flow visual hierarchy in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`
- [x] T035 [P] [US2] Refine Output flow control hierarchy in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt`
- [x] T036 [P] [US2] Refine settings flow visual treatment in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt` and `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings.xml`
- [x] T037 [US2] Verify behavior contracts remain unchanged (discovery/playback/persistence) and log verification in `test-results/032-us2-behavior-contract.md`

**Checkpoint**: US2 core flows remain functionally complete with redesigned UI.

---

## Phase 5: User Story 3 - Accessible and Adaptive Redesign (Priority: P3)

**Goal**: Ensure redesigned flows are accessible and adaptive across phone/tablet/orientation/text scale.

**Independent Test**: Validate in-scope flows on phone and tablet profiles with orientation and increased text scale checks.

### Tests for User Story 3 (REQUIRED)

- [x] T038 [P] [US3] Add failing layout/adaptivity tests in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsLayoutTransitionTest.kt`
- [x] T039 [P] [US3] Add failing accessibility readability/focus tests in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsScreenTest.kt`
- [x] T040 [P] [US3] Add failing source-list adaptivity tests in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListUiStateTest.kt`
- [x] T041 [P] [US3] Extend `testing/e2e/tests/032-fluent-electron-redesign.spec.ts` with phone/tablet/orientation/text-scale scenarios
- [x] T042 [US3] Run existing Playwright regression suite and capture in `test-results/032-us3-regression.md`
- [x] T043 [US3] Record US3 Fluent + Electron compliance evidence in `test-results/032-fluent-electron-settings.md`
- [x] T044 [US3] Record accessibility/adaptive validation outcomes in `test-results/032-fluent-electron-accessibility.md`
- [ ] T045 [US3] If blocked by environment, record blocker classification and remediation in `test-results/032-us3-regression.md`

### Implementation for User Story 3

- [x] T046 [P] [US3] Implement adaptive layout refinements in `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings_three_pane.xml` and `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsLayoutResolver.kt`
- [x] T047 [P] [US3] Implement readability/focus updates in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDetailRenderer.kt`
- [x] T048 [P] [US3] Apply adaptive refinements to Source List and Viewer layouts in `feature/ndi-browser/presentation/src/main/res/layout/fragment_source_list.xml` and `feature/ndi-browser/presentation/src/main/res/layout/fragment_viewer.xml`
- [x] T049 [US3] Verify no blocked primary actions at increased text scale and record in `test-results/032-fluent-electron-accessibility.md`

**Checkpoint**: US3 adaptive/accessibility expectations are met and independently testable.

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: Finalize evidence, regression confidence, and release readiness.

- [x] T050 [P] Consolidate per-screen compliance evidence into `test-results/032-fluent-electron-regression-summary.md`
- [x] T051 Run full module/unit validation: `:app:testDebugUnitTest`, `:feature:ndi-browser:presentation:testDebugUnitTest`, `:feature:ndi-browser:data:testDebugUnitTest`; capture in `test-results/032-final-unit-suite.md`
- [x] T052 Run release hardening gate `:app:verifyReleaseHardening` and capture output in `test-results/032-release-hardening.md`
- [x] T053 [P] Update developer docs for redesign guidance in `docs/architecture.md` and `docs/README.md`
- [x] T054 [P] Verify contract/spec/plan/tasks consistency across `specs/032-fluent-electron-redesign/spec.md`, `specs/032-fluent-electron-redesign/plan.md`, `specs/032-fluent-electron-redesign/contracts/fluent-electron-redesign-contract.md`, and `specs/032-fluent-electron-redesign/tasks.md`
- [x] T055 Capture final pass/fail/blocker classification and sign-off notes in `test-results/032-fluent-electron-regression-summary.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 0 -> required before all other phases
- Phase 1 -> starts after Phase 0
- Phase 2 -> depends on Phase 1 and blocks user stories
- Phase 3 (US1) -> depends on Phase 2
- Phase 4 (US2) -> depends on Phase 2; can proceed after US1 checkpoint for safer integration
- Phase 5 (US3) -> depends on Phase 2; recommended after US1/US2 baseline stability
- Phase 6 -> after desired user stories complete

### User Story Dependencies

- **US1 (P1)**: baseline shell and key surfaces; recommended first slice
- **US2 (P2)**: depends on shared foundation and leverages US1 shell conventions
- **US3 (P3)**: depends on redesigned surfaces existing; validates adaptive/a11y quality

### Within Each User Story

- Write failing tests first
- Implement minimal changes to pass tests
- Run story-specific Playwright checks
- Run existing Playwright regression
- Record compliance evidence and blockers

---

## Parallel Opportunities

- T005 and T006 can run in parallel
- T008 and T009 can run in parallel
- Test tasks marked `[P]` in each story can run in parallel by module owner
- Implementation tasks T021/T022/T023 and T033/T034/T035 can run in parallel once shared tokens are stable
- Documentation and consistency tasks T053/T054 can run in parallel in Phase 6

---

## Implementation Strategy

### MVP First (US1)

1. Complete Phase 0, 1, 2
2. Deliver Phase 3 (US1)
3. Validate shell consistency and no mixed-flow visuals

### Incremental Delivery

1. Add US2 for core flow completion guarantees
2. Add US3 for adaptive and accessibility quality
3. Finish Phase 6 cross-cutting validation and release hardening

### Parallel Team Strategy

1. Foundation owner: token + shell baseline (Phase 2)
2. Flow owners: Source List/Viewer/Output/Settings tasks in US1 and US2
3. Quality owner: Playwright, evidence capture, adaptive/a11y validation in US3 and Phase 6

---

## Notes

- Existing tests are regression protection; only change them when redesign behavior contracts explicitly require it.
- Do not mix legacy and redesigned visuals inside any shipped in-scope flow.
- Keep repository/domain/data boundaries unchanged while implementing presentation redesign.
- Record all validation outputs and blocker classifications under `test-results/`.
