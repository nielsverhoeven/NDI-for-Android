# Tasks: NDI Discovery Server Compatibility

**Input**: Design documents from `/specs/029-ndi-server-compatibility/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are REQUIRED by constitution. Each user story includes failing-test-first tasks. Existing automated tests remain regression protection unless directly impacted by requirements.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Confirm runtime dependencies before implementation and validation

- [X] T001 Verify Android prerequisites and capture evidence in test-results/029-preflight-android-prereqs.md
- [X] T002 Verify target device/emulator visibility with adb and record evidence in test-results/029-preflight-adb-devices.md
- [X] T003 Verify target discovery server endpoints/version availability and seed target list in specs/029-ndi-server-compatibility/validation/server-targets.md
- [X] T004 Verify Playwright/Node preconditions for diagnostics scenarios in test-results/029-preflight-node-playwright.md

**Checkpoint**: Environment is ready or blockers are explicitly documented

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create compatibility-validation scaffolding and artifact locations

- [X] T005 Create compatibility matrix evidence file scaffold in test-results/029-compatibility-matrix.md
- [X] T006 [P] Create scenario catalog for baseline/venue/older targets in specs/029-ndi-server-compatibility/validation/scenarios.md
- [X] T007 [P] Create e2e helper scaffold for compatibility runs in testing/e2e/helpers/discovery-compatibility.ts

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add shared compatibility domain and wiring used by all stories

- [X] T008 Add compatibility model types in core/model/src/main/java/com/ndi/core/model/DiscoveryCompatibilityModels.kt
- [X] T009 Extend discovery repository contracts for compatibility outputs in feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
- [X] T010 Implement compatibility classifier skeleton in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryCompatibilityClassifier.kt
- [X] T011 [P] Add classifier baseline tests in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DiscoveryCompatibilityClassifierTest.kt
- [X] T012 Wire classifier dependency in app/src/main/java/com/ndi/app/di/AppGraph.kt

**Checkpoint**: Shared compatibility infrastructure is in place for story work

---

## Phase 3: User Story 1 - Reliable Venue Discovery (Priority: P1) 🎯 MVP

**Goal**: Make venue discovery work for supported older server versions without masking partial failures

**Independent Test**: Point app to supported older target and verify discover + open-stream flow succeeds while mixed-version failures are not reported as full success

### Tests for User Story 1 (REQUIRED)

- [X] T013 [P] [US1] Add failing mixed-server partial-success contract tests in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiDiscoveryRepositoryContractTest.kt
- [X] T014 [P] [US1] Add failing discovery-only vs incompatible boundary tests in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DiscoveryCompatibilityClassifierTest.kt
- [X] T015 [US1] Add failing source-list status propagation tests in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt

### Implementation for User Story 1

- [X] T016 [US1] Implement mixed-version compatibility aggregation in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt
- [X] T017 [US1] Add compatibility-aware endpoint probing support in ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt
- [X] T018 [US1] Propagate compatibility outcomes to source-list state in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [X] T019 [US1] Emit partial-compatibility telemetry for discovery outcomes in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListTelemetry.kt
- [X] T020 [US1] Execute US1 validation flow and record evidence in test-results/029-us1-venue-discovery.md
- [X] T043 [US1] Record FR-013 traceability for pre-existing test updates (T013/T015) in test-results/029-us1-test-change-traceability.md

**Checkpoint**: Venue-compatible older servers discover sources and open streams; mixed outcomes are not silently reported as full success

---

## Phase 4: User Story 2 - Version Validation Matrix (Priority: P2)

**Goal**: Produce explicit compatibility outcomes for every in-scope target version

**Independent Test**: Run matrix validation across available targets and verify each target has compatible/limited/incompatible/blocked status with evidence

### Tests for User Story 2 (REQUIRED)

- [X] T021 [P] [US2] Add failing matrix status taxonomy tests in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DiscoveryCompatibilityMatrixRepositoryTest.kt
- [X] T022 [P] [US2] Add failing compatibility diagnostics repository tests in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DeveloperDiagnosticsRepositoryImplTest.kt

### Implementation for User Story 2

- [X] T023 [US2] Implement compatibility matrix repository in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryCompatibilityMatrixRepository.kt
- [X] T024 [US2] Persist per-target compatibility records in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt
- [X] T025 [US2] Surface matrix summaries to diagnostics stream in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DeveloperDiagnosticsRepositoryImpl.kt
- [X] T026 [US2] Add matrix execution helper script in testing/e2e/scripts/run-discovery-compatibility-matrix.ps1
- [X] T027 [US2] Execute matrix against available versions and record results in test-results/029-compatibility-matrix.md
- [X] T028 [US2] Record blocked targets with repro and unblock steps in test-results/029-compatibility-matrix.md
- [X] T044 [US2] Record FR-013 traceability for pre-existing test updates (T022) in test-results/029-us2-test-change-traceability.md

**Checkpoint**: Every obtainable in-scope version has an explicit compatibility record with evidence

---

## Phase 5: User Story 3 - Actionable Compatibility Diagnostics (Priority: P3)

**Goal**: Provide actionable compatibility guidance through existing diagnostics surfaces

**Independent Test**: Trigger unsupported/limited/blocked scenarios and verify diagnostics identify category and next action

### Tests for User Story 3 (REQUIRED)

- [X] T029 [P] [US3] Add failing compatibility diagnostics mapping tests in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayStateMapperTest.kt
- [X] T030 [P] [US3] Add failing diagnostics rendering tests in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModelTest.kt
- [X] T031 [P] [US3] Add failing Playwright diagnostics scenario in testing/e2e/tests/029-discovery-compatibility-diagnostics.spec.ts

### Implementation for User Story 3

- [X] T032 [US3] Extend diagnostics model for compatibility guidance in core/model/src/main/java/com/ndi/core/model/DeveloperDiscoveryDiagnostics.kt
- [X] T033 [US3] Map compatibility diagnostics into overlay state in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayState.kt
- [X] T034 [US3] Render actionable compatibility messages in existing diagnostics UI in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayRenderer.kt
- [X] T035 [US3] Update app-level diagnostics composition for compatibility details in app/src/main/java/com/ndi/app/di/AppGraph.kt
- [X] T036 [US3] Execute targeted diagnostics e2e and capture evidence in test-results/029-us3-compatibility-diagnostics.md
- [X] T037 [US3] Run existing Playwright regression suite and capture evidence in test-results/029-us3-regression.md
- [X] T045 [US3] Record FR-013 traceability for pre-existing test updates (T029/T030/T037) in test-results/029-us3-test-change-traceability.md

**Checkpoint**: Existing diagnostics surfaces provide actionable compatibility guidance for non-compatible scenarios

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, hardening, and documentation

- [X] T038 [P] Update compatibility validation guidance in docs/testing.md
- [X] T039 [P] Update NDI feature operations guidance in docs/ndi-feature.md
- [X] T040 Run feature-scoped unit test regression and record output in test-results/029-unit-regression.md
- [X] T041 Run release hardening verification and record output in test-results/029-release-hardening.md
- [X] T042 Run quickstart validation end-to-end and publish final summary in test-results/029-final-validation-summary.md

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 0 must complete before all implementation phases.
- Phase 1 depends on Phase 0 and prepares shared artifacts.
- Phase 2 depends on Phase 1 and blocks all user story implementation.
- Phase 3 (US1) depends on Phase 2 and is the MVP slice.
- Phase 4 (US2) depends on Phase 2 and reuses US1 compatibility outputs.
- Phase 5 (US3) depends on Phase 2 and integrates US1/US2 diagnostics outputs.
- Phase 6 depends on completion of all selected user stories.

### User Story Dependencies

- US1 (P1): No dependency on other user stories after foundational phase.
- US2 (P2): Depends on compatibility outcomes produced by US1 implementation.
- US3 (P3): Depends on compatibility classification and matrix outputs from US1 and US2.

### Within Each User Story

- Write failing tests first.
- Implement minimal production changes to pass tests.
- Re-run impacted regressions.
- Record evidence for pass/blocked outcomes.

## Parallel Execution Examples

### User Story 1

- T013 and T014 can run in parallel (different test files).
- T016 and T017 should run sequentially before T018.

### User Story 2

- T021 and T022 can run in parallel.
- T023 and T026 can run in parallel after tests exist.

### User Story 3

- T029, T030, and T031 can run in parallel.
- T032 and T033 can run in parallel before T034/T035 integration.

## Implementation Strategy

### MVP First

1. Complete Phase 0 through Phase 2.
2. Deliver US1 (Phase 3) and validate venue discovery behavior.
3. Pause for operator validation before expanding matrix scope.

### Incremental Delivery

1. Add US2 matrix recording and evidence capture.
2. Add US3 actionable diagnostics using existing surfaces.
3. Finalize with Phase 6 regression and release hardening checks.

### Team Parallelization

1. One developer focuses on data-layer compatibility logic (US1/US2 core).
2. One developer focuses on diagnostics mapping/rendering (US3).
3. One developer runs matrix/e2e validation and maintains evidence artifacts.
