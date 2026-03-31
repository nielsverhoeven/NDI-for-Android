# Tasks: Fix Appearance Settings

**Input**: Design documents from `/specs/025-fix-appearance-settings/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/appearance-settings-ui-contract.md, quickstart.md

**Tests**: Tests are REQUIRED by constitution. Each user story includes failing-test-first tasks, emulator Playwright coverage for visual behavior, and regression validation.

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Confirm runtime/tooling readiness before implementation and validation.

- [X] T001 Run Android preflight and record output in test-results/025-preflight-android-prereqs.md using scripts/verify-android-prereqs.ps1
- [X] T002 Verify emulator connectivity with `adb devices` and record evidence in test-results/025-preflight-android-prereqs.md alongside scripts/verify-android-prereqs.ps1 output
- [X] T003 Verify Playwright harness command contract and record output in test-results/025-preflight-node-playwright.md using testing/e2e/scripts/validate-command-contract.ps1

**Checkpoint**: Environment is ready or blockers are explicitly documented with unblocking commands.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare feature-specific test evidence scaffolding and test suite manifests.

- [X] T004 Create feature evidence shell in test-results/025-agent-workflow-index.md with planned gate files and status placeholders
- [X] T005 [P] Add scenario IDs for appearance-mode validation to testing/e2e/tests/support/regression-suite-manifest.json
- [X] T006 [P] Add appearance reliability window template for blocked/fail classification in test-results/025-reliability-window-report.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build shared foundations needed by all user stories.

- [X] T007 Create failing unit test proving settings-save path does not trigger app theme stream update in app/src/test/java/com/ndi/app/theme/AppThemeCoordinatorTest.kt
- [X] T008 Create failing unit test for theme repository emissions after external settings writes in feature/theme-editor/data/src/test/java/com/ndi/feature/themeeditor/data/repository/ThemeEditorRepositoryImplTest.kt
- [X] T009 Implement reactive preference observation for Room-backed theme stream in feature/theme-editor/data/src/main/java/com/ndi/feature/themeeditor/data/repository/ThemeEditorRepositoryImpl.kt
- [X] T010 Add shared persistence-regression test for preserving non-owned fields during settings/theme saves in feature/theme-editor/data/src/test/java/com/ndi/feature/themeeditor/data/repository/ThemeEditorRepositoryImplTest.kt
- [X] T011 Update quickstart evidence mapping for preflight and regression gates in specs/025-fix-appearance-settings/quickstart.md

**Checkpoint**: Theme preference stream reacts to all save paths and persistence invariants are protected by tests.

---

## Phase 3: User Story 1 - Fix Theme Mode Switching (Priority: P1) 🎯 MVP

**Goal**: Theme mode Light/Dark/System saves and applies globally, including after restart.

**Independent Test**: Select each mode in Settings > Appearance, save, and verify runtime mode + persisted selection after reopen/restart.

### Tests for User Story 1 (REQUIRED)

- [X] T012 [P] [US1] Add failing ViewModel test for mode dirty/save state transitions in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsViewModelTest.kt
- [X] T013 [P] [US1] Add failing coordinator integration test for mode apply after settings-driven save in app/src/test/java/com/ndi/app/theme/AppThemeCoordinatorTest.kt
- [X] T014 [P] [US1] Add failing Playwright scenario for Light/Dark/System mode save and verification in testing/e2e/tests/025-appearance-settings-rebuild.spec.ts
- [X] T015 [US1] Run targeted US1 appearance scenarios and record evidence in test-results/025-us1-targeted-e2e.md via testing/e2e/tests/025-appearance-settings-rebuild.spec.ts
- [X] T016 [US1] Targeted e2e/preflight blocker classification checked; no blocker encountered.

### Implementation for User Story 1

- [X] T017 [US1] Update theme-mode save/apply flow in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt
- [X] T018 [US1] Ensure app-wide night mode application remains driven by repository stream in app/src/main/java/com/ndi/app/theme/AppThemeCoordinator.kt
- [X] T019 [US1] Preserve persisted mode selection state rendering in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDetailRenderer.kt
- [X] T020 [US1] Add failing unit test for mode-apply latency measurement plumbing in app/src/test/java/com/ndi/app/theme/AppThemeCoordinatorTest.kt

**Checkpoint**: User Story 1 is independently functional and verified by unit + e2e tests.

---

## Phase 4: User Story 2 - Restore Color Theme Picker (Priority: P2)

**Goal**: Appearance detail panel exposes Theme Editor entry point in compact and wide layouts.

**Independent Test**: Appearance panel shows color theme entry, navigation opens Theme Editor, accent selection persists and reflects in app styling.

### Tests for User Story 2 (REQUIRED)

- [X] T021 [P] [US2] Add failing presentation test for color-theme entry visibility in compact layout in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsScreenTest.kt
- [X] T022 [P] [US2] Add failing presentation test for color-theme entry visibility in wide layout in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsFragmentWideLayoutTest.kt
- [X] T023 [P] [US2] Add failing Playwright scenario for Theme Editor navigation and accent update in testing/e2e/tests/025-appearance-settings-rebuild.spec.ts
- [X] T024 [US2] Run targeted US2 color-theme navigation scenarios and record evidence in test-results/025-us2-targeted-e2e.md via testing/e2e/tests/025-appearance-settings-rebuild.spec.ts
- [X] T025 [US2] Targeted e2e/preflight blocker classification checked; no blocker encountered.

### Implementation for User Story 2

- [X] T026 [US2] Restore Appearance panel color-theme entry behavior in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDetailRenderer.kt
- [X] T027 [US2] Wire compact and wide settings UI to Theme Editor entry point in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt
- [X] T028 [US2] Ensure appearance detail state includes color-theme control contract in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt
- [X] T029 [US2] Validate deep-link contract stability for Theme Editor in app/src/main/res/navigation/main_nav_graph.xml

**Checkpoint**: User Story 2 is independently functional and verified by presentation + e2e tests.

---

## Phase 5: User Story 3 - E2E Validation of Appearance Settings (Priority: P3)

**Goal**: Robust e2e coverage validates hybrid theme assertions and system-follow behavior.

**Independent Test**: Run appearance-focused e2e suite and full regression with pass/block classification evidence.

### Tests for User Story 3 (REQUIRED)

- [X] T030 [P] [US3] Add failing Playwright test for hybrid Light-mode assertion (persisted state + visual token) in testing/e2e/tests/025-appearance-settings-rebuild.spec.ts
- [X] T031 [P] [US3] Add failing Playwright test for hybrid Dark-mode assertion (persisted state + visual token) in testing/e2e/tests/025-appearance-settings-rebuild.spec.ts
- [X] T032 [P] [US3] Add failing Playwright test for System Default follow-system toggle behavior in testing/e2e/tests/025-appearance-settings-rebuild.spec.ts
- [X] T033 [US3] Execute appearance e2e suite and record results in test-results/025-e2e-suite-rebuild-summary.md
- [X] T034 [US3] Execute full Playwright regression and record outcome in test-results/025-final-regression-summary.md
- [X] T035 [US3] Full regression blocker classification checked; no blocker encountered.

### Implementation for User Story 3

- [X] T036 [US3] Add reusable Android UI driver helpers for hybrid theme assertions in testing/e2e/tests/support/android-ui-driver.ts
- [X] T037 [US3] Add deterministic emulator theme-toggle helper for System Default validation in testing/e2e/tests/support/android-theme-driver.ts
- [X] T038 [US3] Update Playwright suite classification metadata for new appearance scenarios in testing/e2e/tests/support/e2e-suite-classification.spec.ts
- [X] T043 [US3] Add latency assertion helper and measurement capture for <=1s mode-apply validation in testing/e2e/tests/support/android-ui-driver.ts

**Checkpoint**: User Story 3 validation is complete with deterministic e2e evidence and regression coverage.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final hardening and delivery evidence.

- [X] T039 [P] Update appearance feature documentation in docs/testing.md
- [X] T040 [P] Update feature index and result links in test-results/025-agent-workflow-index.md
- [X] T041 Run full quickstart validation sequence and capture pass/fail summary in test-results/025-command-contract-validation.md
- [X] T042 Verify release-hardening gate remains intact with appearance changes by running app:verifyReleaseHardening and recording output in test-results/025-final-regression-summary.md

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 0 -> required before all implementation/testing phases.
- Phase 1 -> depends on Phase 0 completion.
- Phase 2 -> depends on Phase 1; blocks all user stories.
- Phase 3 (US1) -> depends on Phase 2 completion.
- Phase 4 (US2) -> depends on Phase 2 completion; can run in parallel with Phase 3 once foundation is done.
- Phase 5 (US3) -> depends on Phase 2 completion and can start after US1/US2 test hooks exist.
- Phase 6 -> depends on desired user stories being complete.

### User Story Dependencies

- US1 (P1): Starts immediately after Foundational phase.
- US2 (P2): Starts after Foundational phase; independent from US1 implementation but integrates with shared settings files.
- US3 (P3): Starts after Foundational phase; relies on behavior delivered by US1 and US2 for meaningful end-to-end validation.

### Within Each User Story

- Write tests first and confirm they fail before implementation.
- Implement minimal code to pass tests.
- Run targeted per-story e2e gates and capture evidence, then run one full regression gate in US3.

### Parallel Opportunities

- Phase 1 tasks T005 and T006 can run in parallel.
- Foundational test authoring T007 and T008 can run in parallel.
- US1 test tasks T012, T013, T014 can run in parallel.
- US2 test tasks T021, T022, T023 can run in parallel.
- US3 test tasks T030, T031, T032 can run in parallel.
- Polish documentation tasks T039 and T040 can run in parallel.

---

## Parallel Example: User Story 1

```bash
# Parallel test-first work
Task T012: SettingsViewModel failing dirty/save test
Task T013: AppThemeCoordinator failing apply-after-save test
Task T014: Playwright failing appearance mode scenario

# Then implement to green
Task T017: SettingsViewModel mode save/apply update
Task T018: Coordinator stream-application verification
Task T019: Renderer persisted selection state
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Complete Phase 0, Phase 1, and Phase 2.
2. Deliver US1 (Phase 3) to restore working Light/Dark/System mode behavior.
3. Validate with focused unit + e2e checks and regression evidence.

### Incremental Delivery

1. Deliver US1 for core mode correctness.
2. Deliver US2 for color theme entry restoration.
3. Deliver US3 for robust hybrid e2e coverage.
4. Finish with Phase 6 cross-cutting validation and release-hardening checks.

### Parallel Team Strategy

1. One engineer handles foundational stream/reactivity changes (Phase 2).
2. One engineer drives settings UI restoration (US2).
3. One engineer builds e2e harness and scenario coverage (US3).
4. Integrate through shared regression gates and evidence files.

---

## Notes

- [P] tasks are safe parallel candidates when they target different files and have no blocking dependencies.
- Each task includes an explicit file path for direct execution.
- Visual-change constitution gates are represented by dedicated Playwright coverage and regression tasks.
- Environment-blocked outcomes must be classified with reproducible evidence and unblock commands.
