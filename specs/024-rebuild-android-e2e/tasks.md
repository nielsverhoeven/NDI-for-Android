# Tasks: Rebuild Android E2E Suite

**Input**: Design documents from /specs/024-rebuild-android-e2e/
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/e2e-execution-contract.md, quickstart.md

**Tests**: Tests are REQUIRED by constitution. Every user story uses failing-test-first sequencing and includes Playwright emulator coverage, regression evidence, and blocked-gate evidence where applicable.

**Organization**: Tasks are grouped by user story so each story remains independently implementable and testable.

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Validate runtime/tool readiness before implementation and validation.

- [ ] T001 Run Android prerequisite preflight via scripts/verify-android-prereqs.ps1 and record output in test-results/024-preflight-android-prereqs.md
- [ ] T002 Run dual-emulator preflight via scripts/verify-e2e-dual-emulator-prereqs.ps1 and record output in test-results/024-preflight-dual-emulator.md
- [ ] T003 Validate Playwright bootstrap commands in testing/e2e/package.json and record output in test-results/024-preflight-node-playwright.md

**Checkpoint**: Environment readiness is confirmed or blockers are documented with exact unblock commands.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish shared feature scaffolding used by all stories.

- [ ] T004 Create feature execution log template in test-results/024-execution-log-template.md
- [ ] T005 [P] Create suite overview section for feature 024 in testing/e2e/README.md
- [ ] T006 [P] Ensure artifact placeholder exists in testing/e2e/artifacts/.gitkeep
- [ ] T007 Create Playwright agent evidence index template in test-results/024-agent-workflow-index.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build shared manifest/reporting/gating foundations before any user story implementation.

**CRITICAL**: No user story work starts until this phase completes.

- [ ] T008 Define canonical scenario IDs and feature-area map in testing/e2e/tests/support/regression-suite-manifest.json
- [ ] T009 Add canonical taxonomy assertions in testing/e2e/tests/support/e2e-suite-classification.spec.ts
- [ ] T010 Add result-schema contract test for pass/fail/blocked/not-applicable in testing/e2e/tests/support/ci-artifact-contract.spec.ts
- [ ] T011 Add workflow contract test for required profile gating semantics in testing/e2e/tests/support/ci-workflow-contract.spec.ts
- [ ] T012 Implement shared result classification helper updates in testing/e2e/scripts/helpers/result-handler.ps1
- [ ] T013 Implement normalized status JSON emission in testing/e2e/scripts/run-primary-pr-e2e.ps1
- [ ] T014 Add triage-summary schema helper in testing/e2e/scripts/helpers/triage-summary.ps1
- [ ] T015 Add reliability-window computation helper (20-run, 19-pass threshold) in testing/e2e/scripts/helpers/reliability-window.ps1
- [ ] T016 Add validated command-contract script for local+CI-equivalent execution in testing/e2e/scripts/validate-command-contract.ps1
- [ ] T017 Run foundational support tests and capture baseline evidence in test-results/024-foundational-baseline.md

**Checkpoint**: Shared taxonomy, reporting, command contract, and reliability helpers are ready for story delivery.

---

## Phase 3: User Story 1 - Rebuild Core E2E Coverage (Priority: P1) 🎯 MVP

**Goal**: Retire legacy active suite content and establish deterministic rebuilt baseline coverage.

**Independent Test**: Run rebuilt baseline scenarios only and verify legacy scenarios are excluded while settings/navigation smoke checks pass deterministically.

### Tests for User Story 1 (REQUIRED) ⚠️

- [ ] T018 [US1] Produce Playwright planner scenario plan for baseline rebuild in test-results/024-us1-planner.md
- [ ] T019 [P] [US1] Add failing legacy-retirement guard test in testing/e2e/tests/support/e2e-suite-classification.spec.ts
- [ ] T020 [P] [US1] Add failing manifest-integrity test for rebuilt baseline in testing/e2e/tests/support/regression-suite-integrity.spec.ts
- [ ] T021 [US1] Execute failing US1 support tests and capture red-phase evidence in test-results/024-us1-red.md
- [ ] T022 [US1] Execute pre-rebuild Playwright baseline snapshot and record evidence in test-results/024-transition-baseline-pre-rebuild.md

### Implementation for User Story 1

- [ ] T023 [US1] Retire legacy active scenarios and register rebuilt baseline scenarios in testing/e2e/tests/support/regression-suite-manifest.json
- [ ] T024 [US1] Update baseline classification constants/assertions in testing/e2e/tests/support/e2e-suite-classification.spec.ts
- [ ] T025 [US1] Produce Playwright generator output for baseline scenarios in test-results/024-us1-generator.md
- [ ] T026 [P] [US1] Implement rebuilt core settings smoke scenario in testing/e2e/tests/024-core-settings-smoke.spec.ts
- [ ] T027 [P] [US1] Implement rebuilt core navigation smoke scenario in testing/e2e/tests/024-core-navigation-smoke.spec.ts
- [ ] T028 [US1] Wire rebuilt baseline scenario selection in testing/e2e/scripts/run-primary-pr-e2e.ps1
- [ ] T029 [US1] Execute rebuilt baseline run and capture green-phase evidence in test-results/024-us1-core-rebuild.md
- [ ] T030 [US1] Produce pre-vs-post handover comparison report in test-results/024-transition-handover-comparison.md

**Checkpoint**: Legacy suite is retired and rebuilt baseline is deterministic and enforceable.

---

## Phase 4: User Story 4 - Run E2E in GitHub Actions (Priority: P1)

**Goal**: Enforce CI execution with preflight-first gates, canonical outcomes, and artifact-driven triage.

**Independent Test**: Run CI-equivalent sequence and verify workflow wiring, gate semantics, artifact outputs, and command-contract validation.

### Tests for User Story 4 (REQUIRED) ⚠️

- [ ] T031 [US4] Add failing CI artifact contract assertions for required output set in testing/e2e/tests/support/ci-artifact-contract.spec.ts
- [ ] T032 [US4] Add failing workflow contract assertions for fail/blocked/not-applicable gating in testing/e2e/tests/support/ci-workflow-contract.spec.ts
- [ ] T033 [US4] Add failing triage-SLA contract assertion in testing/e2e/tests/support/ci-artifact-contract.spec.ts
- [ ] T034 [US4] Execute failing US4 contract tests and capture red-phase evidence in test-results/024-us4-red.md

### Implementation for User Story 4

- [ ] T035 [US4] Update primary CI e2e gate and artifact uploads in .github/workflows/android-ci.yml
- [ ] T036 [US4] Align dual-emulator workflow preflight and execution in .github/workflows/e2e-dual-emulator.yml
- [ ] T037 [US4] Align nightly matrix workflow profiles in .github/workflows/e2e-matrix-nightly.yml
- [ ] T038 [US4] Implement required profile gate semantics and status reporting in testing/e2e/scripts/run-primary-pr-e2e.ps1
- [ ] T039 [US4] Implement triage-summary artifact generation in testing/e2e/scripts/run-primary-pr-e2e.ps1
- [ ] T040 [US4] Implement validated command-contract invocation path in testing/e2e/scripts/run-primary-pr-e2e.ps1
- [ ] T041 [US4] Document CI execution and triage flow in testing/e2e/README.md
- [ ] T042 [US4] Validate command-contract path and record evidence in test-results/024-command-contract-validation.md
- [ ] T043 [US4] Run CI-equivalent sequence and capture green-phase evidence in test-results/024-us4-ci-validation.md

**Checkpoint**: CI execution contract is satisfied with canonical statuses, gate rules, and triage artifacts.

---

## Phase 5: User Story 2 - Validate Settings and Navigation Menus (Priority: P2)

**Goal**: Deliver robust rebuilt settings and navigation menu scenarios with deterministic behavior.

**Independent Test**: Run US2-only scenarios and verify route transitions, menu visibility, and persisted setting outcomes.

### Tests for User Story 2 (REQUIRED) ⚠️

- [ ] T044 [US2] Produce Playwright planner scenario plan for menu flows in test-results/024-us2-planner.md
- [ ] T045 [P] [US2] Add failing settings menu scenario spec in testing/e2e/tests/024-settings-menu-rebuild.spec.ts
- [ ] T046 [P] [US2] Add failing navigation menu scenario spec in testing/e2e/tests/024-navigation-menu-rebuild.spec.ts
- [ ] T047 [US2] Add failing US2 suite-membership assertions in testing/e2e/tests/support/e2e-suite-classification.spec.ts
- [ ] T048 [US2] Execute failing US2 scenarios and capture red-phase evidence in test-results/024-us2-red.md

### Implementation for User Story 2

- [ ] T049 [US2] Produce Playwright generator output for menu scenarios in test-results/024-us2-generator.md
- [ ] T050 [US2] Implement deterministic settings menu assertions in testing/e2e/tests/024-settings-menu-rebuild.spec.ts
- [ ] T051 [US2] Implement deterministic navigation menu assertions in testing/e2e/tests/024-navigation-menu-rebuild.spec.ts
- [ ] T052 [P] [US2] Extend menu interaction and bounded wait helpers in testing/e2e/tests/support/android-ui-driver.ts
- [ ] T053 [US2] Register US2 scenario set in testing/e2e/tests/support/regression-suite-manifest.json
- [ ] T054 [US2] Wire US2 profile selection in testing/e2e/scripts/run-primary-pr-e2e.ps1
- [ ] T055 [US2] Run US2-only validation and capture green-phase evidence in test-results/024-us2-settings-navigation.md

**Checkpoint**: Settings and navigation coverage pass independently and are correctly selectable in suite profiles.

---

## Phase 6: User Story 3 - Validate Developer Mode Flows (Priority: P3)

**Goal**: Add reliable developer-mode coverage with capability-aware not-applicable handling.

**Independent Test**: Run developer-mode scenarios on capable and non-capable targets and verify pass/not-applicable behavior with required profile gating preserved.

### Tests for User Story 3 (REQUIRED) ⚠️

- [ ] T056 [US3] Produce Playwright planner scenario plan for developer-mode flows in test-results/024-us3-planner.md
- [ ] T057 [P] [US3] Add failing developer-mode scenario spec in testing/e2e/tests/024-developer-mode-rebuild.spec.ts
- [ ] T058 [US3] Add failing not-applicable policy assertion in testing/e2e/tests/support/ci-artifact-contract.spec.ts
- [ ] T059 [US3] Execute failing US3 scenarios and capture red-phase evidence in test-results/024-us3-red.md

### Implementation for User Story 3

- [ ] T060 [US3] Produce Playwright generator output for developer-mode scenarios in test-results/024-us3-generator.md
- [ ] T061 [US3] Implement developer-mode enable/disable assertions in testing/e2e/tests/024-developer-mode-rebuild.spec.ts
- [ ] T062 [US3] Implement target-capability gating and not-applicable emission in testing/e2e/scripts/run-primary-pr-e2e.ps1
- [ ] T063 [P] [US3] Implement matrix capability mapping in testing/e2e/scripts/run-matrix-e2e.ps1
- [ ] T064 [P] [US3] Register developer-mode scenario metadata in testing/e2e/tests/support/regression-suite-manifest.json
- [ ] T065 [US3] Run capable/non-capable target validation and capture evidence in test-results/024-us3-developer-mode.md
- [ ] T066 [US3] Produce Playwright healer remediation evidence from a controlled failure-recovery rehearsal in test-results/024-us3-healer.md

**Checkpoint**: Developer mode scenarios enforce required behavior on capable targets and report policy-sanctioned not-applicable elsewhere.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validate cross-story quality bars and finalize evidence/reporting.

- [ ] T067 [P] Update quickstart command-contract and verification notes in specs/024-rebuild-android-e2e/quickstart.md
- [ ] T068 [P] Update feature documentation index reference in DOCUMENTATION-INDEX.md
- [ ] T069 Implement reliability-window report generation in testing/e2e/scripts/helpers/reliability-window.ps1
- [ ] T070 Run 20-run reliability window evaluation and publish report in test-results/024-reliability-window-report.md
- [ ] T071 Execute failed-run triage drill and publish 15-minute SLA evidence in test-results/024-triage-sla-validation.md
- [ ] T072 Run full rebuilt primary and dual-emulator regressions and summarize in test-results/024-final-regression-summary.md
- [ ] T073 Produce final suite summary (pass/fail/blocked/not-applicable, reliability, triage SLA, agent evidence) in test-results/024-e2e-suite-rebuild-summary.md

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 0 (Preflight): Must complete first.
- Phase 1 (Setup): Depends on Phase 0.
- Phase 2 (Foundational): Depends on Phase 1 and blocks all user story work.
- Phase 3 (US1): Depends on Phase 2.
- Phase 4 (US4): Depends on Phase 2 and can proceed in parallel with later stories after shared script conflicts are coordinated.
- Phase 5 (US2): Depends on Phase 2 and should merge after US1 baseline stabilization.
- Phase 6 (US3): Depends on Phase 2 and US4 gate/status contracts.
- Phase 7 (Polish): Depends on completion of selected user stories and CI contract wiring.

### User Story Dependencies

- US1 (P1): Starts immediately after foundational completion.
- US4 (P1): Starts after foundational completion and is required for CI acceptance.
- US2 (P2): Depends on baseline selection stability from US1.
- US3 (P3): Depends on taxonomy/gating contract implementation from US4.

### Within Each User Story

- Write failing tests first (red).
- Implement scenarios/scripts second (green).
- Integrate manifest/runner contracts next.
- Capture independent validation evidence last.

---

## Parallel Execution Examples

### US1 Parallel Example

```bash
# Parallelize independent baseline scenario implementations
T026 [US1] testing/e2e/tests/024-core-settings-smoke.spec.ts
T027 [US1] testing/e2e/tests/024-core-navigation-smoke.spec.ts
```

### US4 Parallel Example

```bash
# Parallelize workflow updates in independent files
T035 [US4] .github/workflows/android-ci.yml
T036 [US4] .github/workflows/e2e-dual-emulator.yml
T037 [US4] .github/workflows/e2e-matrix-nightly.yml
```

### US2 Parallel Example

```bash
# Parallelize independent menu scenario files
T045 [US2] testing/e2e/tests/024-settings-menu-rebuild.spec.ts
T046 [US2] testing/e2e/tests/024-navigation-menu-rebuild.spec.ts
```

### US3 Parallel Example

```bash
# Parallelize matrix capability mapping and manifest metadata updates
T063 [US3] testing/e2e/scripts/run-matrix-e2e.ps1
T064 [US3] testing/e2e/tests/support/regression-suite-manifest.json
```

---

## Implementation Strategy

### MVP First (US1 + US4)

1. Complete Phase 0 through Phase 2.
2. Deliver US1 rebuilt baseline and transition evidence.
3. Deliver US4 CI gate and artifact contracts.
4. Validate command-contract, CI-equivalent run, and baseline determinism.

### Incremental Delivery

1. Add US2 settings/navigation coverage and validate independently.
2. Add US3 developer-mode coverage with capability-aware not-applicable behavior.
3. Complete reliability, triage SLA, and final cross-story regression reporting.

### Team Parallel Strategy

1. Stream A owns manifest/classification and baseline scenarios (US1 + US2).
2. Stream B owns workflows, gating contracts, and triage/reliability plumbing (US4).
3. Stream C owns developer-mode capability mapping and policy behavior (US3).
4. Coordinate merges around shared files: testing/e2e/scripts/run-primary-pr-e2e.ps1 and testing/e2e/tests/support/regression-suite-manifest.json.
