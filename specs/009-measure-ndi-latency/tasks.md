# Tasks: Dual-Emulator NDI Latency Measurement

**Input**: Design documents from `/specs/009-measure-ndi-latency/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/ndi-latency-validation-contract.md`, `quickstart.md`

**Tests**: Required by constitution. Each user story includes failing-test-first tasks.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no direct dependency)
- **[Story]**: User story mapping (US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare harness and artifacts for latency-specific e2e execution.

- [X] T001 Define latency scenario suite classification in `testing/e2e/tests/support/e2e-suite-classification.spec.ts`
- [X] T002 [P] Add latency run script aliases in `testing/e2e/package.json`
- [X] T003 [P] Document latency scenario environment variables in `testing/e2e/README.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add shared helpers and evidence plumbing required by all stories.

**⚠️ CRITICAL**: No user-story implementation starts before this phase completes.

- [X] T004 Add dual-recording lifecycle helpers in `testing/e2e/tests/support/android-ui-driver.ts`
- [X] T005 [P] Add latency analysis helper module (cross-correlation core) in `testing/e2e/tests/support/latency-analysis.ts`
- [X] T006 [P] Add unit tests for latency analysis helper in `testing/e2e/tests/support/latency-analysis.spec.ts`
- [X] T007 Add step-checkpoint wiring for latency scenario in `testing/e2e/tests/support/scenario-checkpoints.ts`
- [X] T008 Extend dual-emulator runner summary to include latency artifacts in `testing/e2e/scripts/run-dual-emulator-e2e.ps1`

**Checkpoint**: Shared infrastructure complete.

---

## Phase 3: User Story 1 - Measure end-to-end stream latency (Priority: P1) 🎯 MVP

**Goal**: Execute full source-to-receiver playback scenario and produce numeric latency output.

**Independent Test**: Run US1 scenario and verify PASSED status with latency result and artifacts.

### Tests for User Story 1 (REQUIRED) ⚠️

- [X] T009 [P] [US1] Add failing e2e test for end-to-end latency happy path in `testing/e2e/tests/interop-dual-emulator.spec.ts`
- [X] T010 [P] [US1] Add failing e2e test for mandatory artifact presence in `testing/e2e/tests/interop-dual-emulator.spec.ts`

### Implementation for User Story 1

- [X] T011 [US1] Implement deterministic sequence (stream start, view start, dual recording, YouTube playback) in `testing/e2e/tests/interop-dual-emulator.spec.ts`
- [X] T012 [US1] Implement receiver active-playback verification gates in `testing/e2e/tests/interop-dual-emulator.spec.ts`
- [X] T013 [US1] Invoke latency analysis and persist structured output artifact in `testing/e2e/tests/interop-dual-emulator.spec.ts`
- [X] T014 [US1] Attach source/receiver snapshots and analysis output to Playwright results in `testing/e2e/tests/interop-dual-emulator.spec.ts`

**Checkpoint**: US1 independently passes and yields latency output.

---

## Phase 4: User Story 2 - Detect invalid measurement runs (Priority: P2)

**Goal**: Ensure invalid runs fail with explicit reasons and no false latency reporting.

**Independent Test**: Force invalid preconditions and verify invalid status with precise failed step.

### Tests for User Story 2 (REQUIRED) ⚠️

- [ ] T015 [P] [US2] Add failing e2e test for receiver-not-playing invalidation in `testing/e2e/tests/interop-dual-emulator.spec.ts`
- [ ] T016 [P] [US2] Add failing e2e test for missing/unusable recordings invalidation in `testing/e2e/tests/interop-dual-emulator.spec.ts`

### Implementation for User Story 2

- [ ] T017 [US2] Implement invalidation path that blocks latency output when playback verification fails in `testing/e2e/tests/interop-dual-emulator.spec.ts`
- [ ] T018 [US2] Implement invalidation path for unusable recording artifacts in `testing/e2e/tests/support/latency-analysis.ts`
- [ ] T019 [US2] Emit single explicit failed-step reason to checkpoints and runner summary in `testing/e2e/tests/support/scenario-checkpoints.ts`
- [ ] T020 [US2] Propagate invalid-state evidence into run-summary JSON in `testing/e2e/scripts/run-dual-emulator-e2e.ps1`

**Checkpoint**: US2 independently enforces invalid-run behavior.

---

## Phase 5: User Story 3 - Preserve baseline regression confidence (Priority: P3)

**Goal**: Keep existing regression gate mandatory while latency scenario is added.

**Independent Test**: Run latency scenario + existing regression suite and verify gate behavior.

### Tests for User Story 3 (REQUIRED) ⚠️

- [ ] T021 [P] [US3] Add failing gate test ensuring existing regression suite is still mandatory in `testing/e2e/tests/support/regression-gate.spec.ts`
- [ ] T022 [P] [US3] Add failing test for quality summary completeness (latency + regression) in `testing/e2e/tests/support/regression-manifest-consistency.spec.ts`

### Implementation for User Story 3

- [ ] T023 [US3] Update primary PR runner to include latency scenario in new-settings suite while preserving existing-regression execution in `testing/e2e/scripts/run-primary-pr-e2e.ps1`
- [ ] T024 [US3] Update summary script to report latency evidence plus existing-regression status in `testing/e2e/scripts/summarize-e2e-results.ps1`
- [ ] T025 [US3] Update CI evidence documentation format in `test-results/android-test-results.md`

**Checkpoint**: US3 independently confirms regression preservation.

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: Final validation, docs, and execution evidence.

- [ ] T026 [P] Update feature runbook with concrete commands and artifacts in `specs/009-measure-ndi-latency/quickstart.md`
- [ ] T027 [P] Update testing docs with latency scenario expectations in `docs/testing.md`
- [ ] T028 Execute full primary-profile run and record evidence in `test-results/android-test-results.md`
- [ ] T029 Execute scheduled/matrix-equivalent run and record evidence in `test-results/android-test-results.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 -> Phase 2 -> (US1, US2, US3 in parallel or priority order) -> Phase 6.

### User Story Dependencies

- US1 can start after Phase 2.
- US2 can start after Phase 2; depends on shared analysis/checkpoint infrastructure.
- US3 can start after Phase 2 and should complete before final sign-off.

### Within Each User Story

- Write failing tests first.
- Implement scenario/helper logic second.
- Wire summaries/evidence third.
- Validate independent checkpoint before next story.

### Parallel Opportunities

- `[P]` tasks in setup/foundational phases.
- US1/US2/US3 test authoring tasks in parallel.
- Documentation tasks in Phase 6 in parallel.

## Implementation Strategy

### MVP First

1. Complete Phase 1 and Phase 2.
2. Deliver US1 latency happy-path with artifacts.
3. Validate and demo MVP output.

### Incremental Delivery

1. Add US2 invalid-run correctness.
2. Add US3 regression-preservation enforcement.
3. Complete polish and final evidence capture.
