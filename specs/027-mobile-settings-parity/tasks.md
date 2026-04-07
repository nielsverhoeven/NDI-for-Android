# Tasks: Mobile Settings Parity

**Input**: Design documents from `/specs/027-mobile-settings-parity/`
**Prerequisites**: `plan.md` (required), `spec.md` (required for user stories), `research.md`, `data-model.md`, `contracts/mobile-settings-parity-ui-contract.md`, `quickstart.md`

**Tests**: Tests are REQUIRED by constitution. Every user story includes failing-test-first tasks, emulator Playwright coverage, existing Playwright regression execution, and blocked-gate evidence handling.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Confirm environment readiness before implementation and quality gates

- [ ] T001 Execute `scripts/verify-android-prereqs.ps1` and record output in `test-results/027-preflight-android-prereqs.md`
- [ ] T002 Execute `scripts/verify-e2e-dual-emulator-prereqs.ps1` and record output in `test-results/027-preflight-dual-emulator.md`
- [ ] T003 Validate Playwright harness commands from `testing/e2e/package.json` and record outcome in `test-results/027-preflight-node-playwright.md`

**Checkpoint**: Environment readiness is confirmed or blockers are explicitly documented.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare shared feature validation scaffolding

- [ ] T004 Create parity e2e spec scaffold in `testing/e2e/tests/027-mobile-settings-parity.spec.ts`
- [ ] T005 [P] Register parity suite metadata in `testing/e2e/tests/support/e2e-suite-classification.spec.ts`
- [ ] T006 [P] Create feature validation summary scaffold in `test-results/027-mobile-settings-parity-summary.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared settings infrastructure required before user story implementation

**CRITICAL**: No user story implementation starts until this phase is complete.

- [ ] T007 [P] Add/normalize stable settings accessibility and selector IDs in `feature/ndi-browser/presentation/src/main/res/values/ids.xml`
- [ ] T008 [P] Add/normalize shared settings labels for parity assertions in `feature/ndi-browser/presentation/src/main/res/values/strings.xml`
- [ ] T009 Implement shared compact-vs-wide layout decisions for required phone profiles in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsLayoutResolver.kt`
- [ ] T010 Implement shared layout-mode propagation updates in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt`
- [ ] T011 Align shared settings container structure for phone/tablet rendering in `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings.xml` and `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings_three_pane.xml`

**Checkpoint**: Foundation is ready for independent user story delivery.

---

## Phase 3: User Story 1 - Access Full Settings on Phone (Priority: P1) 🎯 MVP

**Goal**: Ensure phone users can access all improved settings sections without clipping, overlap, or missing interactions.

**Independent Test**: Launch settings on baseline and compact-height phone profiles and verify all sections are visible, selectable, and usable.

### Tests for User Story 1 (REQUIRED)

- [ ] T012 [P] [US1] Add failing section-availability assertions for compact phone mode in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsViewModelTest.kt`
- [ ] T013 [P] [US1] Add failing compact rendering assertions for settings content visibility in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsScreenTest.kt`
- [ ] T014 [P] [US1] Add failing Playwright scenarios for section visibility on both required phone profiles in `testing/e2e/tests/027-mobile-settings-parity.spec.ts`
- [ ] T015 [US1] Execute targeted parity e2e for US1 and record results in `test-results/027-us1-phone-section-visibility.md`
- [ ] T016 [US1] Execute existing Playwright regression suite (`test:pr:primary`) and record results in `test-results/027-us1-regression.md`
- [ ] T017 [US1] If validation is environment-blocked, capture blocker evidence and unblock command in `test-results/027-us1-phone-section-visibility.md`

### Implementation for User Story 1

- [ ] T018 [P] [US1] Update settings screen binding and section rendering flow for compact phones in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`
- [ ] T019 [P] [US1] Update category item selection/visibility behavior for full section access in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsCategoryAdapter.kt`
- [ ] T020 [US1] Update detail rendering to keep section content usable on small viewports in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDetailRenderer.kt`
- [ ] T021 [US1] Update compact layout to avoid clipping and preserve scroll reachability in `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings.xml`
- [ ] T022 [US1] Update settings category row sizing for mobile readability in `feature/ndi-browser/presentation/src/main/res/layout/item_settings_category.xml`

**Checkpoint**: User Story 1 is independently functional and testable on required phone profiles.

---

## Phase 4: User Story 2 - Maintain Consistent Behavior Across Screen Sizes (Priority: P2)

**Goal**: Keep settings grouping/order and selected-state behavior consistent between tablet and phone.

**Independent Test**: Compare tablet and phone settings flows and verify matching section order, labels, and restored selected state.

### Tests for User Story 2 (REQUIRED)

- [ ] T023 [P] [US2] Add failing category-order parity assertions in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsCategorySelectionTest.kt`
- [ ] T024 [P] [US2] Add failing selected-state restoration assertions in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsDetailStateFallbackTest.kt`
- [ ] T025 [P] [US2] Add failing Playwright phone-vs-tablet parity scenarios in `testing/e2e/tests/027-mobile-settings-parity.spec.ts`
- [ ] T026 [US2] Execute targeted parity e2e for US2 and record results in `test-results/027-us2-cross-screen-parity.md`
- [ ] T027 [US2] Execute existing Playwright regression suite (`test:pr:primary`) and record results in `test-results/027-us2-regression.md`
- [ ] T028 [US2] If validation is environment-blocked, capture blocker evidence and unblock command in `test-results/027-us2-cross-screen-parity.md`

### Implementation for User Story 2

- [ ] T029 [P] [US2] Implement deterministic section ordering and restored-selection behavior in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt`
- [ ] T030 [P] [US2] Implement consistent selected-state rebinding across screen contexts in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`
- [ ] T031 [US2] Align shared settings labels/subtitles used by phone and tablet in `feature/ndi-browser/presentation/src/main/res/values/strings.xml`

**Checkpoint**: User Story 2 is independently functional with consistent cross-form-factor behavior.

---

## Phase 5: User Story 3 - Use Settings Reliably in Common Mobile Contexts (Priority: P3)

**Goal**: Preserve settings usability through portrait/landscape transitions on phone.

**Independent Test**: Rotate phone between portrait and landscape while using settings and verify core actions remain available and stable.

### Tests for User Story 3 (REQUIRED)

- [ ] T032 [P] [US3] Convert orientation continuity coverage from `feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/SettingsRotationContinuityTest.kt` into failing Playwright scenarios in `testing/e2e/tests/027-mobile-settings-parity.spec.ts` (retain instrumentation file only as migration reference, not as primary gate)
- [ ] T033 [P] [US3] Add failing layout transition assertions for compact-height profile in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsLayoutTransitionTest.kt`
- [ ] T034 [P] [US3] Add failing Playwright orientation continuity scenarios in `testing/e2e/tests/027-mobile-settings-parity.spec.ts`
- [ ] T035 [US3] Execute targeted parity e2e for US3 and record results in `test-results/027-us3-orientation-continuity.md`
- [ ] T036 [US3] Execute existing Playwright regression suite (`test:pr:primary`) and record results in `test-results/027-us3-regression.md`
- [ ] T037 [US3] If validation is environment-blocked, capture blocker evidence and unblock command in `test-results/027-us3-orientation-continuity.md`

### Implementation for User Story 3

- [ ] T038 [P] [US3] Implement orientation-aware compact profile rules in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsLayoutResolver.kt`
- [ ] T039 [P] [US3] Implement selected-category continuity across configuration change in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`
- [ ] T040 [US3] Update portrait/landscape settings containers to keep controls reachable in `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings.xml` and `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings_three_pane.xml`

**Checkpoint**: User Story 3 is independently functional for phone orientation continuity.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final regression, hardening, and documentation alignment

- [ ] T041 [P] Run module unit regression commands from `specs/027-mobile-settings-parity/quickstart.md` and record output in `test-results/027-unit-regression.md`
- [ ] T042 [P] Run release hardening check (`verifyReleaseHardening`) and record output in `test-results/027-release-hardening.md`
- [ ] T043 Produce Material 3 compliance evidence for updated settings UI (components, spacing, typography, accessibility states) in `test-results/027-material3-compliance.md`
- [ ] T044 Consolidate final validation outcomes and failure classification in `test-results/027-final-regression-summary.md`
- [ ] T045 Update execution notes and evidence links in `specs/027-mobile-settings-parity/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 0 (Environment Preflight): must complete first.
- Phase 1 (Setup): depends on Phase 0.
- Phase 2 (Foundational): depends on Phase 1 and blocks all user stories.
- Phase 3 (US1): depends on Phase 2.
- Phase 4 (US2): depends on Phase 2; can run in parallel with US1 after foundation is complete.
- Phase 5 (US3): depends on Phase 2; can run in parallel with US1/US2 after foundation is complete.
- Phase 6 (Polish): depends on completion of selected user stories.

### User Story Dependencies

- US1 (P1): no dependency on other stories after Phase 2.
- US2 (P2): no strict dependency on US1; validates cross-form-factor consistency independently.
- US3 (P3): no strict dependency on US1/US2; validates orientation continuity independently.

### Within Each User Story

- Failing tests first.
- Implementation only after test failures are in place.
- Targeted parity e2e run.
- Existing Playwright regression run.
- Blocked-gate evidence task when environment constraints occur.

### Parallel Opportunities

- Setup tasks marked [P] can run concurrently.
- Foundational tasks T007 and T008 can run concurrently.
- In each user story, [P] test tasks can run concurrently.
- In each user story, [P] implementation tasks can run concurrently when touching separate files.
- US1, US2, and US3 can proceed in parallel after Phase 2 if team capacity allows.

---

## Parallel Example: User Story 1

```bash
# Run failing-test preparation in parallel:
T012 [US1] SettingsViewModelTest.kt
T013 [US1] SettingsScreenTest.kt
T014 [US1] 027-mobile-settings-parity.spec.ts

# Run implementation tasks in parallel after failing tests are captured:
T018 [US1] SettingsFragment.kt
T019 [US1] SettingsCategoryAdapter.kt
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 0 through Phase 2.
2. Deliver US1 (Phase 3) with full test cycle.
3. Validate and demo mobile section-access parity.

### Incremental Delivery

1. Deliver US1 for core phone parity.
2. Deliver US2 for tablet-phone consistency.
3. Deliver US3 for orientation reliability.
4. Finish with Phase 6 full regression and release hardening evidence.

### Parallel Team Strategy

1. Team finishes preflight, setup, and foundational phases together.
2. Then split by story track:
   - Developer A: US1
   - Developer B: US2
   - Developer C: US3
3. Merge for cross-cutting regression and final summary.
