# Tasks: Rebuild Android E2E Suite

**Input**: Design documents from `/specs/024-rebuild-android-e2e/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/e2e-execution-contract.md, quickstart.md

**Tests**: Tests are REQUIRED by constitution. Each user story includes failing-test-first tasks, Playwright emulator e2e coverage tasks, regression execution tasks, and blocked-gate evidence tasks.

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Ensure all runtime/tool dependencies are validated before implementation and validation tasks.

- [X] T001 Run Android prerequisite preflight in scripts/verify-android-prereqs.ps1 and capture output in test-results/024-preflight-android-prereqs.md
- [X] T002 Run dual-emulator prerequisite preflight in scripts/verify-e2e-dual-emulator-prereqs.ps1 and capture output in test-results/024-preflight-dual-emulator.md
- [X] T003 Verify e2e dependency bootstrap commands in testing/e2e/package.json and capture output in test-results/024-preflight-node-playwright.md

**Checkpoint**: Environment is validated or blockers are explicitly documented with reproduction and unblocking commands.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish feature branch test scaffolding and reporting skeleton shared by all stories.

- [X] T004 Create feature execution log template in test-results/024-execution-log-template.md
- [X] T005 [P] Add feature suite index section in testing/e2e/README.md for 024 rebuilt scenarios
- [X] T006 [P] Create placeholder artifact folder note in testing/e2e/artifacts/.gitkeep

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build shared selection/reporting foundations required before any story-specific scenario work.

**CRITICAL**: No user story implementation starts until this phase is complete.

- [ ] T007 Define rebuilt suite scenario IDs and feature-area mapping in testing/e2e/tests/support/regression-suite-manifest.json
- [ ] T008 Add canonical scenario classification assertions in testing/e2e/tests/support/e2e-suite-classification.spec.ts
- [ ] T009 Add run-result schema contract test (pass/fail/blocked/not-applicable) in testing/e2e/tests/support/ci-artifact-contract.spec.ts
- [ ] T010 Implement shared result classification helper updates in testing/e2e/scripts/helpers/result-handler.ps1
- [ ] T011 Configure primary runner to emit normalized status JSON in testing/e2e/scripts/run-primary-pr-e2e.ps1

**Checkpoint**: Suite metadata, status taxonomy, and shared runner output are stable for story execution.

---

## Phase 3: User Story 1 - Rebuild Core E2E Coverage (Priority: P1) 🎯 MVP

**Goal**: Retire the legacy active suite and establish a deterministic rebuilt core suite baseline.

**Independent Test**: Run rebuilt baseline support tests and primary profile; confirm only rebuilt scenarios are active and deterministic.

### Tests for User Story 1 (REQUIRED) ⚠️

- [ ] T012 [P] [US1] Add failing legacy-retirement guard test in testing/e2e/tests/support/e2e-suite-classification.spec.ts
- [ ] T013 [P] [US1] Add failing manifest-integrity test for rebuilt baseline in testing/e2e/tests/support/regression-suite-integrity.spec.ts
- [ ] T014 [US1] Execute failing baseline support tests and capture red-phase evidence in test-results/024-us1-red.md
- [ ] T053 [US1] Execute pre-rebuild Playwright baseline gate snapshot and record results in test-results/024-transition-baseline-pre-rebuild.md

### Implementation for User Story 1

- [ ] T015 [US1] Retire legacy active scenarios and register rebuilt baseline scenarios in testing/e2e/tests/support/regression-suite-manifest.json
- [ ] T016 [US1] Update classification constants/assertions for rebuilt baseline in testing/e2e/tests/support/e2e-suite-classification.spec.ts
- [ ] T017 [P] [US1] Implement rebuilt core settings smoke scenario in testing/e2e/tests/024-core-settings-smoke.spec.ts
- [ ] T018 [P] [US1] Implement rebuilt core navigation smoke scenario in testing/e2e/tests/024-core-navigation-smoke.spec.ts
- [ ] T019 [US1] Wire rebuilt baseline spec set into primary gate selection in testing/e2e/scripts/run-primary-pr-e2e.ps1
- [ ] T020 [US1] Run rebuilt primary baseline and record green-phase evidence in test-results/024-us1-core-rebuild.md
- [ ] T054 [US1] Produce handover comparison report between pre-rebuild and rebuilt suites in test-results/024-transition-handover-comparison.md

**Checkpoint**: Legacy active suite is retired and rebuilt core baseline runs deterministically.

---

## Phase 4: User Story 4 - Run E2E in GitHub Actions (Priority: P1)

**Goal**: Ensure rebuilt suite runs in CI with preflight-first gating and artifact-driven triage.

**Independent Test**: Execute workflow validation and local CI-equivalent commands; verify required outputs and artifact paths exist.

### Tests for User Story 4 (REQUIRED) ⚠️

- [ ] T021 [P] [US4] Add failing CI artifact contract assertion for primary gate outputs in testing/e2e/tests/support/ci-artifact-contract.spec.ts
- [ ] T022 [US4] Add failing workflow-schema validation test in testing/e2e/tests/support/ci-workflow-contract.spec.ts
- [ ] T023 [US4] Execute failing CI contract tests and capture red-phase evidence in test-results/024-us4-red.md

### Implementation for User Story 4

- [ ] T024 [US4] Update primary CI e2e gate steps and artifact uploads in .github/workflows/android-ci.yml
- [ ] T025 [US4] Align dual-emulator workflow preflight and execution wiring in .github/workflows/e2e-dual-emulator.yml
- [ ] T026 [US4] Align matrix nightly workflow profile execution in .github/workflows/e2e-matrix-nightly.yml
- [ ] T027 [US4] Implement CI status/report path contract updates in testing/e2e/scripts/run-primary-pr-e2e.ps1
- [ ] T028 [US4] Document CI run and triage flow for rebuilt suite in testing/e2e/README.md
- [ ] T029 [US4] Run CI-equivalent command sequence and capture green-phase evidence in test-results/024-us4-ci-validation.md

**Checkpoint**: GitHub Actions e2e execution contract is satisfied for rebuilt suite.

---

## Phase 5: User Story 2 - Validate Settings and Navigation Menus (Priority: P2)

**Goal**: Provide robust rebuilt scenarios for settings and top-level navigation coverage.

**Independent Test**: Run only US2 scenarios and verify destination routing, menu visibility, and persisted settings responses.

### Tests for User Story 2 (REQUIRED) ⚠️

- [ ] T030 [P] [US2] Add failing settings menu scenario spec in testing/e2e/tests/024-settings-menu-rebuild.spec.ts
- [ ] T031 [P] [US2] Add failing navigation menu scenario spec in testing/e2e/tests/024-navigation-menu-rebuild.spec.ts
- [ ] T032 [US2] Add failing US2 suite-membership assertions in testing/e2e/tests/support/e2e-suite-classification.spec.ts
- [ ] T033 [US2] Execute failing US2 scenarios and capture red-phase evidence in test-results/024-us2-red.md

### Implementation for User Story 2

- [ ] T034 [US2] Implement deterministic settings menu assertions in testing/e2e/tests/024-settings-menu-rebuild.spec.ts
- [ ] T035 [US2] Implement deterministic top-level navigation assertions in testing/e2e/tests/024-navigation-menu-rebuild.spec.ts
- [ ] T036 [P] [US2] Extend reusable menu interaction helpers in testing/e2e/tests/support/android-ui-driver.ts
- [ ] T037 [US2] Register US2 scenarios in primary gate suite selection in testing/e2e/scripts/run-primary-pr-e2e.ps1
- [ ] T038 [US2] Update manifest/classification entries for US2 scenarios in testing/e2e/tests/support/regression-suite-manifest.json
- [ ] T039 [US2] Run US2-only scenario set and record green-phase evidence in test-results/024-us2-settings-navigation.md

**Checkpoint**: Settings and navigation rebuilt scenarios pass independently and are included in primary gate.

---

## Phase 6: User Story 3 - Validate Developer Mode Flows (Priority: P3)

**Goal**: Add reliable developer mode e2e coverage with target-capability-aware not-applicable handling.

**Independent Test**: Execute developer mode scenarios on capable and non-capable targets and verify pass/not-applicable classification behavior.

### Tests for User Story 3 (REQUIRED) ⚠️

- [ ] T040 [P] [US3] Add failing developer mode scenario spec in testing/e2e/tests/024-developer-mode-rebuild.spec.ts
- [ ] T041 [US3] Add failing not-applicable policy assertion in testing/e2e/tests/support/ci-artifact-contract.spec.ts
- [ ] T042 [US3] Execute failing US3 scenarios and capture red-phase evidence in test-results/024-us3-red.md

### Implementation for User Story 3

- [ ] T043 [US3] Implement developer mode enable/disable flow assertions in testing/e2e/tests/024-developer-mode-rebuild.spec.ts
- [ ] T044 [US3] Implement target-capability gating and not-applicable emission in testing/e2e/scripts/run-primary-pr-e2e.ps1
- [ ] T045 [P] [US3] Implement matrix profile capability mapping for developer mode runs in testing/e2e/scripts/run-matrix-e2e.ps1
- [ ] T046 [P] [US3] Register US3 developer mode scenario in testing/e2e/tests/support/regression-suite-manifest.json
- [ ] T047 [US3] Run capable/non-capable developer mode validations and record green-phase evidence in test-results/024-us3-developer-mode.md

**Checkpoint**: Developer mode coverage is enforceable on capable targets and correctly not-applicable elsewhere.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final consistency, documentation, and full-regression validation across stories.

- [ ] T048 [P] Update quickstart consistency notes for rebuilt suite in specs/024-rebuild-android-e2e/quickstart.md
- [ ] T049 [P] Update feature documentation index entry in DOCUMENTATION-INDEX.md
- [ ] T050 Run full rebuilt primary regression and summarize in test-results/024-final-primary-regression.md
- [ ] T051 Run dual-emulator gate regression and summarize in test-results/024-final-dual-emulator-regression.md
- [ ] T052 Produce final pass/fail/blocked/not-applicable summary report in test-results/024-e2e-suite-rebuild-summary.md

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 0 (Preflight): Must complete first.
- Phase 1 (Setup): Depends on Phase 0.
- Phase 2 (Foundational): Depends on Phase 1 and blocks all user story work.
- Phase 3 (US1): Depends on Phase 2.
- Phase 4 (US4): Depends on Phase 2 and can proceed in parallel with later user stories once shared runner updates are coordinated.
- Phase 5 (US2): Depends on Phase 2; should merge after US1 baseline files are stable.
- Phase 6 (US3): Depends on Phase 2 and US4 CI status taxonomy wiring.
- Phase 7 (Polish): Depends on completion of selected user stories.

### User Story Dependencies

- **US1 (P1)**: Starts immediately after foundational completion.
- **US1 (P1)**: Includes transition evidence gate tasks T053 and T054 to preserve continuity proof across suite handover.
- **US4 (P1)**: Starts after foundational completion; required for CI acceptance.
- **US2 (P2)**: Depends on US1 rebuilt baseline to avoid legacy suite reintroduction.
- **US3 (P3)**: Depends on US4 status taxonomy implementation for not-applicable reporting.

### Within Each User Story

- Failing tests first (Red).
- Scenario/script implementation second (Green).
- Runner/manifest integration next.
- Independent validation evidence task last.

---

## Parallel Execution Examples

### US1 Parallel Example

```bash
# Parallelize independent core spec creation
T017 [US1] testing/e2e/tests/024-core-settings-smoke.spec.ts
T018 [US1] testing/e2e/tests/024-core-navigation-smoke.spec.ts
```

### US4 Parallel Example

```bash
# Parallelize workflow updates in distinct files
T024 [US4] .github/workflows/android-ci.yml
T025 [US4] .github/workflows/e2e-dual-emulator.yml
T026 [US4] .github/workflows/e2e-matrix-nightly.yml
```

### US2 Parallel Example

```bash
# Parallelize independent menu story test files
T030 [US2] testing/e2e/tests/024-settings-menu-rebuild.spec.ts
T031 [US2] testing/e2e/tests/024-navigation-menu-rebuild.spec.ts
```

### US3 Parallel Example

```bash
# Parallelize manifest and matrix policy work
T045 [US3] testing/e2e/scripts/run-matrix-e2e.ps1
T046 [US3] testing/e2e/tests/support/regression-suite-manifest.json
```

---

## Implementation Strategy

### MVP First (US1 + US4)

1. Complete Phase 0 to Phase 2.
2. Deliver US1 rebuilt baseline.
3. Deliver US4 CI execution contract.
4. Validate rebuilt suite works locally and in CI-equivalent flow.

### Incremental Delivery

1. Add US2 settings/navigation scenarios and validate independently.
2. Add US3 developer mode with capability-aware policy handling.
3. Run final cross-story regressions and produce summary report.

### Team Parallel Strategy

1. One stream owns runner/workflow contracts (US4).
2. One stream owns core/menu scenario rebuild (US1+US2).
3. One stream owns developer mode gating semantics (US3).
4. Merge after manifest/classification contract checks pass.
