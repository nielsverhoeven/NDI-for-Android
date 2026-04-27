# Tasks: Fluent Button Radius Alignment

**Input**: Design documents from `/specs/033-fluent-button-radius/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/fluent-button-radius-contract.md

**Tests**: Required by constitution. Each user story includes failing-test-first tasks, emulator-run Playwright checks, existing Playwright regression execution, and explicit evidence capture.

**Organization**: Tasks are grouped by user story so each story is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no blocking dependency)
- **[Story]**: `US1`, `US2`, `US3`
- Each task includes concrete file paths

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Confirm runtime/tooling readiness before any implementation or quality gates.

- [ ] T001 Run `scripts/verify-android-prereqs.ps1` and capture output in `test-results/033-button-radius-preflight.md`
- [ ] T002 Run `scripts/verify-e2e-dual-emulator-prereqs.ps1` and append output to `test-results/033-button-radius-preflight.md`
- [ ] T003 Validate `adb devices` plus `testing/e2e/scripts/validate-command-contract.ps1` and append results to `test-results/033-button-radius-preflight.md`
- [ ] T004 If preflight fails, record `BlockedEnvironment` classification and concrete remediation steps in `test-results/033-button-radius-regression.md`

**Checkpoint**: Environment is ready or blockers are explicitly documented.

---

## Phase 1: Setup (Shared)

**Purpose**: Add feature scaffolding for tests and evidence.

- [ ] T005 Create feature Playwright spec scaffold at `testing/e2e/tests/033-fluent-button-radius.spec.ts`
- [ ] T006 [P] Create feature evidence summary scaffold at `test-results/033-button-radius-flow-evidence.md`
- [ ] T007 [P] Add task-level gate checklist headings to `test-results/033-button-radius-regression.md` and `test-results/033-button-radius-release-hardening.md`

---

## Phase 2: Foundational Styling Baseline (Blocking)

**Purpose**: Introduce one canonical less-rounded button corner profile reused across all included flows.

**CRITICAL**: User-story work starts only after this phase.

- [ ] T011 Add failing tests for top-level style invariants in `app/src/test/java/com/ndi/app/theme/AppThemeCoordinatorTest.kt` (must run and fail before T008-T010)
- [ ] T008 Define canonical button shape token/style (8dp radius) in `app/src/main/res/values/themes.xml`
- [ ] T009 [P] Add shared geometry token declaration in `app/src/main/res/values/dimens.xml` (create if missing) and reference in `app/src/main/res/values/themes.xml`
- [ ] T010 Apply default button style mapping for app theme in `app/src/main/res/values/themes.xml` and feature overrides in `feature/ndi-browser/presentation/src/main/res/values/styles.xml` (create if missing)
- [ ] T012 Run `./gradlew.bat :app:testDebugUnitTest -x lint` and record baseline in `test-results/033-foundation-tests.md`

**Checkpoint**: A single reusable button corner profile exists and is test-guarded.

---

## Phase 3: User Story 1 - Fluent-Aligned Primary Actions (Priority: P1) 🎯 MVP

**Goal**: All primary/secondary action buttons in scope use less-rounded Fluent-aligned shape while preserving behavior.

**Independent Test**: Traverse Home, Source List, Viewer, Output, Settings and verify button shape update with unchanged outcomes.

### Tests for User Story 1 (REQUIRED)

- [ ] T013 [P] [US1] Add failing Source List visual-shape assertions in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListUiStateTest.kt`
- [ ] T014 [P] [US1] Add failing Viewer button-state shape assertions in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModelTest.kt`
- [ ] T015 [P] [US1] Add failing Output control shape assertions in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTest.kt`
- [ ] T016 [P] [US1] Add failing Settings action-button shape assertions in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsScreenTest.kt`
- [ ] T017 [P] [US1] Implement emulator flow checks in `testing/e2e/tests/033-fluent-button-radius.spec.ts` for Home, Source List, Viewer, Output, Settings button geometry
- [ ] T018 [US1] Run existing Playwright regression profile (`npm --prefix testing/e2e run test:pr:primary`) and capture results in `test-results/033-button-radius-regression.md`
- [ ] T019 [US1] Record Fluent compliance evidence for US1 in `test-results/033-button-radius-flow-evidence.md`

### Implementation for User Story 1

- [ ] T020 [P] [US1] Update Home buttons in `feature/ndi-browser/presentation/src/main/res/layout/fragment_home_dashboard.xml`
- [ ] T021 [P] [US1] Update Source List refresh/action buttons in `feature/ndi-browser/presentation/src/main/res/layout/fragment_source_list.xml` and `feature/ndi-browser/presentation/src/main/res/layout/item_ndi_source.xml`
- [ ] T022 [P] [US1] Update Viewer buttons in `feature/ndi-browser/presentation/src/main/res/layout/fragment_viewer.xml`
- [ ] T023 [P] [US1] Update Output buttons in `feature/ndi-browser/presentation/src/main/res/layout/fragment_output_control.xml`
- [ ] T024 [P] [US1] Update Settings buttons in `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings.xml` and `feature/ndi-browser/presentation/src/main/res/layout/view_settings_main_navigation_panel.xml`
- [ ] T025 [US1] Validate no behavior changes in button-triggered navigation/actions via `app/src/test/java/com/ndi/app/navigation/TopLevelNavigationCoordinatorTest.kt` and record in `test-results/033-button-radius-flow-evidence.md`

**Checkpoint**: P1 is shippable as MVP with visual update and behavior parity.

---

## Phase 4: User Story 2 - Cross-Screen Consistency (Priority: P2)

**Goal**: Remove mixed legacy/new corner styles across all included flows and variants.

**Independent Test**: Cross-screen sweep confirms uniform shape in all included button variants and states.

### Tests for User Story 2 (REQUIRED)

- [ ] T026 [P] [US2] Add failing consistency checks for Source List + Viewer in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListUiStateTest.kt` and `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModelTopLevelNavTest.kt`
- [ ] T027 [P] [US2] Add failing consistency checks for Output + Settings in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTopLevelNavTest.kt` and `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsMainNavigationStateTest.kt`
- [ ] T028 [P] [US2] Extend `testing/e2e/tests/033-fluent-button-radius.spec.ts` with mixed-style detection checks across included flows
- [ ] T029 [US2] Re-run existing Playwright regression profile and append results to `test-results/033-button-radius-regression.md`
- [ ] T030 [US2] Record US2 consistency evidence in `test-results/033-button-radius-flow-evidence.md`

### Implementation for User Story 2

- [ ] T031 [P] [US2] Align discovery-server settings button shapes in `feature/ndi-browser/presentation/src/main/res/layout/fragment_discovery_server_settings.xml` and `feature/ndi-browser/presentation/src/main/res/layout-sw600dp/fragment_discovery_server_settings.xml`
- [ ] T032 [P] [US2] Align discovery server row icon-button shapes in `feature/ndi-browser/presentation/src/main/res/layout/item_discovery_server.xml`
- [ ] T033 [P] [US2] Normalize button shape/style overrides only in these in-scope files by replacing non-canonical button shape/style references with the canonical 8dp profile: `feature/ndi-browser/presentation/src/main/res/layout/fragment_home_dashboard.xml`, `feature/ndi-browser/presentation/src/main/res/layout/fragment_source_list.xml`, `feature/ndi-browser/presentation/src/main/res/layout/item_ndi_source.xml`, `feature/ndi-browser/presentation/src/main/res/layout/fragment_viewer.xml`, `feature/ndi-browser/presentation/src/main/res/layout/fragment_output_control.xml`, `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings.xml`, and `feature/ndi-browser/presentation/src/main/res/layout/view_settings_main_navigation_panel.xml`
- [ ] T034 [US2] Verify strict uniform corner profile requirement (FR-013) and log outcome in `test-results/033-button-radius-flow-evidence.md`

**Checkpoint**: All included flows show one consistent less-rounded button profile.

---

## Phase 5: User Story 3 - Usability Preservation with Updated Shape (Priority: P3)

**Goal**: Preserve button usability/readability/focus affordance in compact and wide layouts after shape update.

**Independent Test**: Validate compact/wide and dark/light scenarios with no blocked primary actions.

### Tests for User Story 3 (REQUIRED)

- [ ] T035 [P] [US3] Add failing adaptive-layout tests in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsLayoutTransitionTest.kt`
- [ ] T036 [P] [US3] Add failing readability/focus tests in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsFragmentWideLayoutTest.kt` and `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsScreenTest.kt`
- [ ] T037 [P] [US3] Extend `testing/e2e/tests/033-fluent-button-radius.spec.ts` with compact/wide + dark/light button usability checks
- [ ] T038 [US3] Re-run existing Playwright regression profile and append results to `test-results/033-button-radius-regression.md`
- [ ] T039 [US3] Record US3 usability/adaptive evidence in `test-results/033-button-radius-flow-evidence.md`

### Implementation for User Story 3

- [ ] T040 [P] [US3] Tune wide-layout button container spacing where needed in `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings_three_pane.xml`
- [ ] T041 [P] [US3] Tune compact-layout button spacing where needed in `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings.xml` and `feature/ndi-browser/presentation/src/main/res/layout/fragment_output_control.xml`
- [ ] T042 [US3] Verify no blocked primary actions and document outcome in `test-results/033-button-radius-flow-evidence.md`

**Checkpoint**: Updated shape remains usable across target layouts and states.

---

## Phase 6: Final Validation & Polish

**Purpose**: Close feature gates and produce final sign-off evidence.

- [ ] T043 Run `./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest -x lint` and capture in `test-results/033-button-radius-regression.md`
- [ ] T044 Run `./gradlew.bat :app:testDebugUnitTest -x lint` and capture in `test-results/033-button-radius-regression.md`
- [ ] T045 Run `./gradlew.bat :app:verifyReleaseHardening` and capture in `test-results/033-button-radius-release-hardening.md`
- [ ] T046 Consolidate final pass/fail/blocked gate statuses in `test-results/033-button-radius-flow-evidence.md`
- [ ] T047 Verify spec/plan/contracts/tasks consistency in `specs/033-fluent-button-radius/spec.md`, `specs/033-fluent-button-radius/plan.md`, `specs/033-fluent-button-radius/contracts/fluent-button-radius-contract.md`, and `specs/033-fluent-button-radius/tasks.md`
- [ ] T048 Create FR-011 test-change traceability ledger in `test-results/033-test-change-traceability.md` mapping every modified pre-existing automated test to triggering requirement ID(s), or explicitly record `No pre-existing tests changed`

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 0 blocks all phases.
- Phase 1 depends on Phase 0.
- Phase 2 depends on Phase 1 and blocks all user stories.
- Phase 3 (US1) depends on Phase 2.
- Phase 4 (US2) depends on Phase 2; recommended after US1 checkpoint.
- Phase 5 (US3) depends on Phase 2; recommended after US1 and US2 stabilize.
- Phase 6 depends on completion of intended user stories.

### User Story Dependencies

- **US1 (P1)**: First shippable slice (MVP).
- **US2 (P2)**: Builds on US1 visual baseline to remove mixed-style drift.
- **US3 (P3)**: Validates usability/adaptivity after baseline is stable.

### Within Each User Story

- Add failing tests first.
- Complete and record failing-test evidence before starting implementation tasks.
- Implement minimal visual-only updates to pass tests.
- Run feature Playwright checks.
- Run existing Playwright regression suite.
- Record compliance and blocker evidence.

---

## Parallel Opportunities

- T006 and T007 can run in parallel.
- T009 and T010 are sequential because both update `app/src/main/res/values/themes.xml`.
- In US1, T013-T017 can run in parallel with each other; T020-T024 can run in parallel only after T013-T017 have failed and been recorded.
- In US2, T026-T028 can run in parallel with each other; T031-T033 can run in parallel only after T026-T028 have failed and been recorded.
- In US3, T035-T037 can run in parallel with each other; T040-T041 can run in parallel only after T035-T037 have failed and been recorded.

---

## Implementation Strategy

### MVP First

1. Complete Phases 0-2.
2. Deliver US1 (Phase 3).
3. Validate and demo.

### Incremental Delivery

1. Deliver US2 consistency hardening.
2. Deliver US3 usability/adaptive checks.
3. Complete final validation and sign-off (Phase 6).

### Notes

- Existing tests are regression protection and should not be edited unless directly required by this feature contract.
- This feature is visual-only: no behavior/navigation/state-transition changes are allowed.
- If any gate is blocked by environment, classify as `BlockedEnvironment` with remediation commands in evidence files.
