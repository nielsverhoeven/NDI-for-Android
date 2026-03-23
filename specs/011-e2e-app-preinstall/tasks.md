# Tasks: E2E App Pre-Installation Gate

**Input**: Design documents from `/specs/011-e2e-app-preinstall/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: Required by constitution. Failing-test-first sequencing is enforced per user story using Playwright support specs and workflow verification.

**Organization**: Tasks are grouped by user story to preserve independent implementation and validation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Parallelizable task (different files, no unfinished dependency)
- **[Story]**: User story label (`[US1]`, `[US2]`, `[US3]`) for story phases only

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare the feature scaffolding and baseline wiring used by all user stories.

- [ ] T001 Confirm feature docs index in `specs/011-e2e-app-preinstall/quickstart.md` references `plan.md`, `data-model.md`, and both contract files.
- [ ] T002 [P] Add a placeholder runtime report fixture file at `testing/e2e/artifacts/runtime/preinstall-report.json` for local schema/test bootstrapping.
- [ ] T003 [P] Add pre-install helper module scaffold in `testing/e2e/tests/support/app-preinstall.ts` with exported report path constants.
- [ ] T004 Add script scaffold with parameter block and header comments in `testing/e2e/scripts/install-app-preinstall.ps1`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared contract and helper capabilities required before any story implementation.

**CRITICAL**: No user story work starts until this phase is complete.

- [ ] T005 Add `Wait-ForEmulatorReady` helper signature and export in `testing/e2e/scripts/helpers/emulator-adb.ps1`.
- [ ] T006 [P] Add `Get-InstalledAppVersion` helper signature and export in `testing/e2e/scripts/helpers/emulator-adb.ps1`.
- [ ] T007 [P] Add `Test-AppLaunchable` helper signature and export in `testing/e2e/scripts/helpers/emulator-adb.ps1`.
- [ ] T008 Implement report type interfaces and status enums in `testing/e2e/tests/support/app-preinstall.ts` to match `contracts/pre-flight-report.contract.md`.
- [ ] T009 Implement report read/write utility functions in `testing/e2e/tests/support/app-preinstall.ts` for Playwright support specs.

**Checkpoint**: Shared helper and report contracts are in place.

---

## Phase 3: User Story 1 - Fresh App Installation Before Test Run (Priority: P1) MVP

**Goal**: Always install the latest debug APK before tests and produce a PASS pre-flight report with per-device version evidence.

**Independent Test**: Build debug APK, run pre-install step, confirm report shows PASS with matching `versionIdentifier` for both emulators before tests begin.

### Tests for User Story 1 (REQUIRED)

- [ ] T010 [P] [US1] Add failing support spec `pre-flight report exists and parses` in `testing/e2e/tests/support/app-preinstall.spec.ts`.
- [ ] T011 [P] [US1] Add failing support spec `overall status is PASS on healthy run` in `testing/e2e/tests/support/app-preinstall.spec.ts`.
- [ ] T012 [P] [US1] Add failing support spec `installed version matches expected build artifact` in `testing/e2e/tests/support/app-preinstall.spec.ts`.
- [ ] T013 [US1] Add failing support spec `each device elapsedMs is <= 60000` in `testing/e2e/tests/support/app-preinstall.spec.ts`.

### Implementation for User Story 1

- [ ] T014 [US1] Implement APK path resolution, env overrides, and default serial resolution in `testing/e2e/scripts/install-app-preinstall.ps1`.
- [ ] T015 [US1] Implement artifact existence guard and abort-before-install report writing in `testing/e2e/scripts/install-app-preinstall.ps1`.
- [ ] T016 [US1] Implement APK metadata extraction (`versionName`, `versionCode`, `versionIdentifier`) in `testing/e2e/scripts/install-app-preinstall.ps1`.
- [ ] T017 [US1] Implement per-device install loop and version confirmation flow in `testing/e2e/scripts/install-app-preinstall.ps1`.
- [ ] T018 [US1] Implement PASS/FAIL report aggregation and JSON serialization in `testing/e2e/scripts/install-app-preinstall.ps1`.
- [ ] T019 [US1] Wire local enforcement call in `testing/e2e/tests/support/global-setup-dual-emulator.ts` to invoke `install-app-preinstall.ps1`.
- [ ] T020 [US1] Add `Build app debug APK` step in `.github/workflows/e2e-dual-emulator.yml` before emulator provisioning.
- [ ] T021 [US1] Add `Install app on emulators` step in `.github/workflows/e2e-dual-emulator.yml` before any Playwright test step.
- [ ] T022 [US1] Add `Run app pre-flight support spec` step in `.github/workflows/e2e-dual-emulator.yml` before broader regression execution.

### Verification for User Story 1

- [ ] T023 [US1] Run `app-preinstall.spec.ts` and record output in `test-results/android-test-results.md`.
- [ ] T024 [US1] Validate PASS report contract fields against `contracts/pre-flight-report.contract.md` and note results in `test-results/android-test-results.md`.

**Checkpoint**: US1 provides a working pre-install gate and PASS-path reporting.

---

## Phase 4: User Story 2 - Installation Failure Aborts the Run with a Clear Error (Priority: P2)

**Goal**: Abort before tests with clear device-specific failure details for missing artifact, unreachable/not-ready devices, install failure, and timeout.

**Independent Test**: Run pre-install without APK or with invalid/unreachable serial and verify exit code 1, FAIL report, and actionable error text.

### Tests for User Story 2 (REQUIRED)

- [ ] T025 [P] [US2] Add failing support spec `missing artifact returns abortedBeforeInstall=true` in `testing/e2e/tests/support/app-preinstall.spec.ts` using a generated fail fixture report.
- [ ] T026 [P] [US2] Add failing support spec `unreachable and not-ready statuses are distinct` in `testing/e2e/tests/support/app-preinstall.spec.ts` using fixture reports.
- [ ] T027 [P] [US2] Add failing support spec `timeout status includes actionable error message` in `testing/e2e/tests/support/app-preinstall.spec.ts` using fixture reports.
- [ ] T028 [US2] Add failing workflow assertion step that pre-install steps are unconditional in `.github/workflows/e2e-dual-emulator.yml` (no `if:` guards on build/install/preflight-spec steps).

### Implementation for User Story 2

- [ ] T029 [US2] Implement missing-artifact FAIL path with nullable build metadata and empty devices array in `testing/e2e/scripts/install-app-preinstall.ps1`.
- [ ] T030 [US2] Implement `UNREACHABLE` and `NOT_READY` status handling with device-specific `errorMessage` in `testing/e2e/scripts/install-app-preinstall.ps1`.
- [ ] T031 [US2] Implement per-device deadline enforcement and `TIMEOUT` status assignment in `testing/e2e/scripts/install-app-preinstall.ps1`.
- [ ] T032 [US2] Implement `INSTALL_FAILED` status mapping from install command failures in `testing/e2e/scripts/install-app-preinstall.ps1`.
- [ ] T033 [US2] Implement consolidated FAIL summary line format and exit code 1 behavior in `testing/e2e/scripts/install-app-preinstall.ps1`.

### Verification for User Story 2

- [ ] T034 [US2] Execute missing-artifact scenario and record report/output evidence in `test-results/android-test-results.md`.
- [ ] T035 [US2] Execute unreachable/not-ready scenario and record report/output evidence in `test-results/android-test-results.md`.
- [ ] T036 [US2] Execute timeout scenario and record report/output evidence in `test-results/android-test-results.md`.

**Checkpoint**: US2 provides explicit fail-fast behavior and actionable failure reporting.

---

## Phase 5: User Story 3 - Post-Installation Launch Verification (Priority: P3)

**Goal**: Verify launchability after install and fail with a distinct launch-verification error when app start fails.

**Independent Test**: Simulate launch failure after install and verify `LAUNCH_FAILED` is reported distinctly from install failures.

### Tests for User Story 3 (REQUIRED)

- [ ] T037 [P] [US3] Add failing support spec `launch verification required for PASS` in `testing/e2e/tests/support/app-preinstall.spec.ts`.
- [ ] T038 [P] [US3] Add failing support spec `LAUNCH_FAILED is distinct from INSTALL_FAILED` in `testing/e2e/tests/support/app-preinstall.spec.ts` using fail fixture reports.
- [ ] T039 [US3] Add failing support spec `global setup rejects stale/missing preinstall report` in `testing/e2e/tests/support/app-preinstall.spec.ts`.

### Implementation for User Story 3

- [ ] T040 [US3] Implement launch verification execution (`am start -W`) and `Status: ok` success parsing in `testing/e2e/scripts/helpers/emulator-adb.ps1`.
- [ ] T041 [US3] Implement `LAUNCH_FAILED` status assignment and messaging in `testing/e2e/scripts/install-app-preinstall.ps1`.
- [ ] T042 [US3] Implement report reuse validation (fresh matching APK + serials) in `testing/e2e/tests/support/global-setup-dual-emulator.ts`.

### Verification for User Story 3

- [ ] T043 [US3] Execute launch success scenario and record PASS-path evidence in `test-results/android-test-results.md`.
- [ ] T044 [US3] Execute launch failure scenario and record distinct `LAUNCH_FAILED` evidence in `test-results/android-test-results.md`.

**Checkpoint**: US3 ensures post-install launchability guarantees before any test execution.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Finalize documentation, full regression proof, and operational consistency.

- [ ] T045 Update operator usage and troubleshooting in `testing/e2e/README.md` to include pre-install gate behavior and status meanings.
- [ ] T046 [P] Align workflow ordering documentation in `specs/011-e2e-app-preinstall/quickstart.md` with final CI step names from `.github/workflows/e2e-dual-emulator.yml`.
- [ ] T047 Run existing Playwright regression suites from `testing/e2e/tests/**/*.spec.ts` and capture pass evidence in `test-results/android-test-results.md`.
- [ ] T048 Validate idempotent rerun behavior (same APK twice) and record report comparison evidence in `test-results/android-test-results.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: starts immediately
- **Phase 2 (Foundational)**: depends on Phase 1, blocks all story work
- **Phase 3 (US1)**: depends on Phase 2, delivers MVP
- **Phase 4 (US2)**: depends on US1 orchestrator/report flow
- **Phase 5 (US3)**: depends on US1 core flow and US2 fail-state model
- **Phase 6 (Polish)**: after stories to finalize regression and docs

### User Story Dependencies

- **US1 (P1)**: first functional increment, independent after foundation
- **US2 (P2)**: extends US1 with explicit failure modes
- **US3 (P3)**: extends US1 with launch verification guarantees

### Within Each User Story

- Tests must be written first and fail.
- Script/helper implementation follows.
- Workflow wiring follows implementation.
- Story-specific verification must pass before moving on.

---

## Parallel Opportunities

- Phase 1: `T002` and `T003` can run in parallel.
- Phase 2: `T006` and `T007` can run in parallel after `T005`.
- US1 tests `T010` to `T012` can run in parallel.
- US2 tests `T025` to `T027` can run in parallel.
- US3 tests `T037` and `T038` can run in parallel.
- Polish tasks `T046` and `T047` can run in parallel.

---

## Parallel Example: User Story 1

```bash
# Parallel test authoring (US1)
T010  # report exists/parse
T011  # PASS status
T012  # version match

# Parallel CI wiring once script core is stable
T020  # build app debug APK step
T021  # install app on emulators step
T022  # run app pre-flight support spec step
```

---

## Implementation Strategy

### MVP First (US1)

1. Complete Phase 1 and Phase 2.
2. Implement and verify Phase 3 (US1).
3. Validate PASS-path behavior before continuing.

### Incremental Delivery

1. US1: guaranteed fresh install and PASS report.
2. US2: fail-fast paths and actionable diagnostics.
3. US3: launch verification guard.
4. Polish: docs, full regression, idempotency proof.

### Team Parallelization

1. One engineer focuses on PowerShell helper/orchestrator (`emulator-adb.ps1`, `install-app-preinstall.ps1`).
2. One engineer focuses on Playwright support specs and fixtures (`app-preinstall.spec.ts`, `app-preinstall.ts`).
3. One engineer focuses on CI workflow and documentation (`e2e-dual-emulator.yml`, `quickstart.md`, `README.md`).

---

## Notes

- `[P]` tasks target distinct files or independent verification artifacts.
- Every user story includes explicit failing-first test tasks.
- No Pester tasks are included; tests stay aligned with constitution and current plan.
- All task descriptions include concrete file paths.
