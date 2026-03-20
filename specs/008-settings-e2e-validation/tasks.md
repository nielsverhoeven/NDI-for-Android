# Tasks: Settings Menu End-to-End Emulator Validation

**Input**: Design documents from `/specs/008-settings-e2e-validation/`
**Prerequisites**: `plan.md` (required), `spec.md` (required for user stories), `research.md`, `data-model.md`, `contracts/settings-e2e-validation-contract.md`, `quickstart.md`

**Tests**: Tests are required by constitution and this feature explicitly delivers e2e quality gates.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare Playwright harness and reporting scaffolding for emulator-gated validation.

- [ ] T001 Define primary and matrix emulator project profiles in `testing/e2e/playwright.config.ts`
- [ ] T002 [P] Add PR and scheduled matrix npm scripts in `testing/e2e/package.json`
- [ ] T003 [P] Document required environment variables and run modes in `testing/e2e/README.md`
- [ ] T004 Create e2e run classification helper in `testing/e2e/tests/support/e2e-suite-classification.spec.ts`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement mandatory quality-gate execution and evidence plumbing before story-specific tests.

**⚠️ CRITICAL**: No user story implementation begins until this phase is complete.

- [ ] T005 Implement fail-fast/partial-run detection in `testing/e2e/scripts/run-dual-emulator-e2e.ps1`
- [ ] T006 [P] Create primary PR gate runner in `testing/e2e/scripts/run-primary-pr-e2e.ps1`
- [ ] T007 [P] Create scheduled matrix runner in `testing/e2e/scripts/run-matrix-e2e.ps1`
- [ ] T008 [P] Create e2e summary evidence script in `testing/e2e/scripts/summarize-e2e-results.ps1`
- [ ] T009 Wire PR primary-profile gate and artifact upload in `.github/workflows/android-ci.yml`
- [ ] T010 Create scheduled matrix workflow in `.github/workflows/e2e-matrix-nightly.yml`
- [ ] T011 Add required evidence section template in `test-results/android-test-results.md`

Dependency note: Complete `T006`, `T007`, and `T008` before wiring `T009` and `T010`.

**Checkpoint**: Foundation complete; user-story coverage work can proceed.

---

## Phase 3: User Story 1 - Validate Settings Access Paths (Priority: P1) 🎯 MVP

**Goal**: Ensure settings can be opened and exited from all required entry screens on emulator(s).

**Independent Test**: Run new US1 Playwright scenarios on primary emulator profile and verify all pass with navigational return-state assertions.

### Tests for User Story 1 (REQUIRED) ⚠️

- [ ] T012 [P] [US1] Add Source List -> Settings -> Back e2e scenario in `testing/e2e/tests/settings-navigation-source-list.spec.ts`
- [ ] T013 [P] [US1] Add Viewer -> Settings -> Back e2e scenario in `testing/e2e/tests/settings-navigation-viewer.spec.ts`
- [ ] T014 [P] [US1] Add Output -> Settings -> Back e2e scenario in `testing/e2e/tests/settings-navigation-output.spec.ts`
- [ ] T015 [P] [US1] Add stable navigation assertion helpers in `testing/e2e/tests/support/scenario-checkpoints.spec.ts`

### Implementation for User Story 1

- [ ] T016 [US1] Add shared open-settings action helpers in `testing/e2e/tests/support/android-ui-driver.spec.ts`
- [ ] T017 [US1] Classify US1 scenarios as new-settings suite in `testing/e2e/tests/support/e2e-suite-classification.spec.ts`
- [ ] T018 [US1] Include US1 specs in PR runner selection in `testing/e2e/scripts/run-primary-pr-e2e.ps1`
- [ ] T019 [US1] Record US1 validation evidence format in `test-results/android-test-results.md`

**Checkpoint**: US1 is independently functional and testable.

---

## Phase 4: User Story 2 - Validate Settings Functional Behavior (Priority: P2)

**Goal**: Ensure persisted valid settings, rejected invalid settings, and fallback-warning behavior are covered on emulator(s).

**Independent Test**: Run US2 scenarios on emulator(s) and verify pass/fail criteria for persistence, validation feedback, and fallback behavior.

### Tests for User Story 2 (REQUIRED) ⚠️

- [ ] T020 [P] [US2] Add valid discovery persistence e2e scenario in `testing/e2e/tests/settings-valid-discovery-persistence.spec.ts`
- [ ] T021 [P] [US2] Add invalid discovery validation e2e scenario in `testing/e2e/tests/settings-invalid-discovery-validation.spec.ts`
- [ ] T022 [P] [US2] Strengthen fallback warning scenario assertions in `testing/e2e/tests/settings-discovery-fallback.spec.ts`
- [ ] T023 [P] [US2] Add app relaunch fixture utilities for persistence checks in `testing/e2e/tests/support/android-device-fixtures.spec.ts`

### Implementation for User Story 2

- [ ] T024 [US2] Extend existing settings apply scenario assertions in `testing/e2e/tests/settings-discovery-config.spec.ts`
- [ ] T025 [US2] Update timing threshold use for US2 checks in `testing/e2e/tests/support/timingAssertions.ts`
- [ ] T026 [US2] Classify US2 scenarios as new-settings suite in `testing/e2e/tests/support/e2e-suite-classification.spec.ts`
- [ ] T027 [US2] Include US2 specs in PR runner selection in `testing/e2e/scripts/run-primary-pr-e2e.ps1`
- [ ] T028 [US2] Capture US2 suite evidence output in `test-results/android-test-results.md`

**Checkpoint**: US2 is independently functional and testable.

---

## Phase 5: User Story 3 - Protect Existing End-to-End Coverage (Priority: P3)

**Goal**: Ensure all pre-existing Playwright scenarios are executed and must remain passing alongside new settings scenarios.

**Independent Test**: Execute complete existing regression suite and fail gate on any missing, skipped, failed, or incomplete scenario.

### Tests for User Story 3 (REQUIRED) ⚠️

- [ ] T029 [P] [US3] Define existing-regression suite manifest in `testing/e2e/tests/support/regression-suite-manifest.json`
- [ ] T030 [P] [US3] Add manifest consistency meta-test in `testing/e2e/tests/support/regression-manifest-consistency.spec.ts`
- [ ] T031 [P] [US3] Add regression gate completeness test in `testing/e2e/tests/support/regression-gate.spec.ts`

### Implementation for User Story 3

- [ ] T032 [US3] Execute manifest-defined existing suite in PR runner in `testing/e2e/scripts/run-primary-pr-e2e.ps1`
- [ ] T033 [US3] Aggregate matrix profile outcomes and fail on incomplete runs in `testing/e2e/scripts/run-matrix-e2e.ps1`
- [ ] T034 [US3] Publish separate new-settings and existing-regression artifacts in `.github/workflows/android-ci.yml`
- [ ] T035 [US3] Add scheduled matrix workflow trigger and profile matrix in `.github/workflows/e2e-matrix-nightly.yml`
- [ ] T036 [US3] Document mandatory regression-pass gate policy in `testing/e2e/README.md`
- [ ] T037 [US3] Capture US3 regression evidence and exception handling log format in `test-results/android-test-results.md`
- [ ] T038 [US3] Enforce waiver metadata validation for both required approver roles in `testing/e2e/scripts/summarize-e2e-results.ps1`

**Checkpoint**: US3 is independently functional and testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final hardening, runbook alignment, and validation evidence.

- [ ] T039 [P] Update feature quickstart with final executed commands in `specs/008-settings-e2e-validation/quickstart.md`
- [ ] T040 [P] Update testing guidance for PR vs matrix gates in `docs/testing.md`
- [ ] T041 Run full primary-profile e2e gate and record evidence in `test-results/android-test-results.md`
- [ ] T042 Run scheduled-matrix-equivalent dry run locally (or CI replay) and record evidence in `test-results/android-test-results.md`
- [ ] T043 Validate release hardening + quality gate references in `docs/ndi-feature.md`
- [ ] T044 Capture and publish failing-test-first evidence for this feature PR flow in `test-results/android-test-results.md`
- [ ] T045 Capture SC-004 timestamped first-cycle regression-detection evidence in `test-results/android-test-results.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies, start immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1 completion; blocks all user stories.
- **Phase 3 (US1)**: Depends on Phase 2 completion.
- **Phase 4 (US2)**: Depends on Phase 2 completion; can proceed in parallel with US1.
- **Phase 5 (US3)**: Depends on Phase 2 completion; can proceed in parallel with US1 and US2.
- **Phase 6 (Polish)**: Depends on completion of US1, US2, and US3 for final full-suite evidence tasks (`T041`, `T042`, `T044`, `T045`).

### User Story Dependencies

- **US1 (P1)**: Independent after foundational work.
- **US2 (P2)**: Independent after foundational work.
- **US3 (P3)**: Independent after foundational work, but validates outcomes from US1 and US2 in full-suite runs.

### Within Each User Story

- Tests MUST be written and fail before implementation.
- Scenario definitions precede runner/wiring updates.
- Runner updates precede evidence capture and sign-off.

### Parallel Opportunities

- Setup tasks marked `[P]` (`T002`, `T003`) can run concurrently.
- Foundational tasks marked `[P]` (`T006`, `T007`, `T008`) can run concurrently after `T005`; run `T009` and `T010` after those scripts are in place.
- US1 test tasks (`T012`-`T015`) can run in parallel.
- US2 test tasks (`T020`-`T023`) can run in parallel.
- US3 test tasks (`T029`-`T031`) can run in parallel.
- Polish documentation tasks (`T039`, `T040`) can run in parallel.

---

## Parallel Example: User Story 1

```bash
# Run US1 test authoring tasks in parallel:
Task: "T012 [US1] settings navigation source-list e2e spec"
Task: "T013 [US1] settings navigation viewer e2e spec"
Task: "T014 [US1] settings navigation output e2e spec"
```

## Parallel Example: User Story 3

```bash
# Run US3 regression-gate tasks in parallel:
Task: "T029 [US3] regression suite manifest"
Task: "T030 [US3] manifest consistency meta-test"
Task: "T031 [US3] regression gate completeness test"
```

---

## Implementation Strategy

### MVP First (US1)

1. Complete Phase 1 and Phase 2.
2. Deliver Phase 3 (US1) settings access-path coverage on emulator.
3. Validate US1 independently before expanding scope.

### Incremental Delivery

1. Foundation complete (Phases 1-2).
2. Deliver US1 navigation-path coverage.
3. Deliver US2 functional behavior coverage.
4. Deliver US3 full existing-suite regression gate.
5. Finish with Phase 6 cross-cutting validation and documentation.

### Parallel Team Strategy

1. Team completes foundational runner/workflow plumbing.
2. After Phase 2:
   - Developer A leads US1 navigation e2e coverage.
   - Developer B leads US2 behavior e2e coverage.
   - Developer C leads US3 regression gate and matrix workflow.
3. Merge and stabilize through Phase 6 evidence collection.
