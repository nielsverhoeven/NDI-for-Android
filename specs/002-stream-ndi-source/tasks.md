# Tasks: NDI Source Network Output and Dual-Emulator End-to-End Validation

**Input**: Design documents from `/specs/002-stream-ndi-source/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Test tasks are REQUIRED. Follow strict TDD: write failing tests first, then implement, then refactor. End-to-end tests MUST default to Playwright unless a documented exception is approved.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish Playwright-default E2E scaffolding, governance alignment, and environment checks.

- [x] T001 Add feature validation matrix for publisher/receiver emulator roles in specs/002-stream-ndi-source/validation/release-validation-matrix.md
- [x] T002 Record dual-emulator network preflight checklist in specs/002-stream-ndi-source/validation/dual-emulator-network-preflight.md
- [x] T003 [P] Bootstrap Playwright E2E workspace configuration in testing/e2e/package.json
- [x] T004 [P] Add Playwright runner configuration and projects in testing/e2e/playwright.config.ts
- [x] T005 [P] Add emulator orchestration helper scripts for Playwright runs in testing/e2e/scripts/run-dual-emulator-e2e.ps1
- [x] T006 Add Playwright exception register template in specs/002-stream-ndi-source/validation/playwright-exceptions.md
- [x] T007 Update quickstart to Playwright-first test workflow and command expectations in specs/002-stream-ndi-source/quickstart.md
- [x] T008 Capture initial quickstart execution placeholders in specs/002-stream-ndi-source/validation/quickstart-validation-report.md
- [x] T009 Define measurable phone/tablet layout matrix and pass thresholds for FR-013 in specs/002-stream-ndi-source/validation/device-layout-matrix.md
- [x] T010 Create toolchain currency tracker seed for TOOLCHAIN-001 with owner/date fields in specs/002-stream-ndi-source/validation/toolchain-currency-review.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish contracts, entities, persistence, navigation, DI boundaries, and compliance evidence before user story work.

**CRITICAL**: No user story work can begin until this phase is complete.

- [x] T011 Extend output domain contracts in feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
- [x] T012 [P] Add output session and health telemetry model events in core/model/src/main/java/com/ndi/core/model/TelemetryEvent.kt
- [x] T013 [P] Add output session/configuration model types in core/model/src/main/java/com/ndi/core/model/NdiModels.kt
- [x] T014 Add Room schema updates for output configuration/session continuity in core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt
- [x] T015 Add output route destination and arguments in app/src/main/res/navigation/main_nav_graph.xml
- [x] T016 Add output route helpers and deep-link support in app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt
- [x] T017 Wire output repositories and dependency providers in app/src/main/java/com/ndi/app/di/AppGraph.kt
- [x] T018 Add permission-impact and justification report for feature manifests in specs/002-stream-ndi-source/validation/permission-justification.md
- [x] T019 Add battery-impact validation plan and thresholds for output flows in specs/002-stream-ndi-source/validation/battery-impact-report.md
- [x] T020 Document foundational contract compliance checkpoint in specs/002-stream-ndi-source/validation/foundation-checkpoint.md

**Checkpoint**: Foundation ready. User story implementation can proceed.

---

## Phase 3: User Story 1 - Start Network Output from a Selected Source (Priority: P1) 🎯 MVP

**Goal**: Operator can select a source and start outbound NDI output discoverable on the network.

**Independent Test**: On emulator A, select source and start output; on emulator B, discover publisher stream through Playwright-driven E2E flow.

### Tests for User Story 1 (REQUIRED)

- [X] T021 [P] [US1] Add failing unit tests for output start state transitions in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTest.kt
- [X] T022 [P] [US1] Add failing repository contract tests for start/idempotency in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiOutputRepositoryContractTest.kt
- [X] T023 [P] [US1] Add failing mapper tests for outbound stream identity generation in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/OutputSessionMapperTest.kt
- [X] T024 [P] [US1] Add failing Playwright E2E test for source-list to output-control navigation in testing/e2e/tests/us1-output-navigation.spec.ts
- [X] T025 [P] [US1] Add failing Playwright E2E test for start-output discoverability in testing/e2e/tests/us1-start-output.spec.ts

### Implementation for User Story 1

- [X] T026 [US1] Implement output session mapper and stream-name conflict resolution in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/mapper/OutputSessionMapper.kt
- [X] T027 [US1] Implement native sender start bridge API in ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt
- [X] T028 [US1] Implement output repository start flow including reachability pre-check in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt
- [X] T029 [US1] Implement output control view-model start intent handling in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
- [X] T030 [P] [US1] Implement output control Material 3 UI states in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt
- [X] T031 [P] [US1] Add source-list action to open output control for selected source in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [X] T032 [US1] Add source-list output action UI affordance in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
- [X] T033 [US1] Emit output start telemetry events in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputTelemetry.kt

**Checkpoint**: User Story 1 is independently functional and testable.

---

## Phase 4: User Story 2 - Control and Monitor Output Session (Priority: P2)

**Goal**: Operator can monitor output status and stop output with immediate state feedback.

**Independent Test**: Start output, verify active status metadata, stop output, and verify receiver state through Playwright E2E.

### Tests for User Story 2 (REQUIRED)

- [X] T034 [P] [US2] Add failing unit tests for stop-output and control availability in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlStopStateTest.kt
- [X] T035 [P] [US2] Add failing repository contract tests for stop lifecycle transitions in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiOutputRepositoryStopContractTest.kt
- [X] T036 [P] [US2] Add failing Playwright E2E test for output active metadata rendering in testing/e2e/tests/us2-output-status.spec.ts
- [X] T037 [P] [US2] Add failing Playwright E2E test for stop-output propagation in testing/e2e/tests/us2-stop-output.spec.ts

### Implementation for User Story 2

- [X] T038 [US2] Implement native sender stop bridge API in ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt
- [X] T039 [US2] Implement output repository stop flow and state publishing in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt
- [X] T040 [US2] Implement output health snapshot coordinator in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/OutputSessionCoordinator.kt
- [X] T041 [US2] Implement output control stop plus status state model in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
- [X] T042 [P] [US2] Implement output active and stop status rendering in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt
- [X] T043 [US2] Persist preferred stream name and last selected input source in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/OutputConfigurationRepositoryImpl.kt
- [X] T044 [US2] Emit output stop and status telemetry events in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputTelemetry.kt
- [X] T045 [US2] Record US2 independent-test evidence in specs/002-stream-ndi-source/validation/us2-output-control-validation.md

**Checkpoint**: User Stories 1 and 2 are independently functional.

---

## Phase 5: User Story 3 - Recover from Interruptions Gracefully (Priority: P3)

**Goal**: Interruption handling surfaces retry/stop actions and restores output when possible.

**Independent Test**: During active output, simulate source/network loss and verify interruption state plus retry/stop recovery through Playwright E2E.

### Tests for User Story 3 (REQUIRED)

- [X] T046 [P] [US3] Add failing unit tests for interruption and retry-window behavior in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputRecoveryViewModelTest.kt
- [X] T047 [P] [US3] Add failing repository contract tests for interrupted output retry semantics in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiOutputRecoveryRepositoryContractTest.kt
- [X] T048 [P] [US3] Add failing Playwright E2E test for interruption recovery actions in testing/e2e/tests/us3-recovery-actions.spec.ts
- [X] T049 [P] [US3] Add failing Playwright E2E test for source-loss interruption propagation in testing/e2e/tests/us3-source-loss.spec.ts

### Implementation for User Story 3

- [X] T050 [US3] Implement output interruption detection and retry coordinator in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/OutputRecoveryCoordinator.kt
- [X] T051 [US3] Implement repository retryInterruptedOutputWithinWindow behavior in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt
- [X] T052 [US3] Implement interrupted/retrying/stopped UI state transitions in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
- [X] T053 [P] [US3] Implement recovery action components and messaging in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt
- [X] T054 [US3] Emit interruption and recovery telemetry events in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputTelemetry.kt
- [X] T055 [US3] Record US3 independent-test evidence in specs/002-stream-ndi-source/validation/us3-output-recovery-validation.md

**Checkpoint**: All user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validate Playwright-default end-to-end interoperability, release hardening, and success criteria.

- [X] T056 [P] Add dual-emulator publisher/receiver Playwright scenario in testing/e2e/tests/interop-dual-emulator.spec.ts
- [X] T057 [P] Add Playwright metrics fixture utilities for pass/fail assertions in testing/e2e/tests/support/metrics-fixtures.ts
- [X] T058 Execute and record dual-emulator Playwright run evidence in specs/002-stream-ndi-source/validation/dual-emulator-e2e-report.md
- [X] T059 Verify API 24+ compatibility regression coverage for output flow in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/Api24CompatibilityTest.kt
- [X] T060 Verify release shrink/optimization gate for output flow in app/build.gradle.kts
- [X] T061 Execute toolchain currency review and update TOOLCHAIN-001 status evidence in specs/002-stream-ndi-source/validation/toolchain-currency-review.md
- [ ] T062 Run full quickstart validation and capture command outcomes in specs/002-stream-ndi-source/validation/quickstart-validation-report.md
- [ ] T063 Execute phone/tablet layout matrix and record FR-013 pass/fail metrics in specs/002-stream-ndi-source/validation/device-layout-validation-report.md
- [ ] T064 Aggregate SC-001..SC-005 outcomes in specs/002-stream-ndi-source/validation/success-criteria-report.md
- [X] T065 Record Espresso-to-Playwright migration mapping for touched E2E flows in specs/002-stream-ndi-source/validation/playwright-migration-report.md
- [X] T066 Document approved exceptions for any remaining non-Playwright E2E coverage in specs/002-stream-ndi-source/validation/playwright-exceptions.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies.
- **Phase 2 (Foundational)**: Depends on Phase 1 and blocks all user stories.
- **Phase 3 (US1)**: Depends on Phase 2.
- **Phase 4 (US2)**: Depends on Phase 2 and US1 output start pipeline.
- **Phase 5 (US3)**: Depends on Phase 2 and US1/US2 output lifecycle pipeline.
- **Phase 6 (Polish)**: Depends on all desired user stories.

### User Story Dependencies

- **US1 (P1)**: Starts after foundational checkpoint; delivers MVP output-start flow.
- **US2 (P2)**: Starts after foundational checkpoint; relies on US1 output session start behavior.
- **US3 (P3)**: Starts after foundational checkpoint; relies on US1/US2 session lifecycle and control states.

### Within Each User Story

- Write tests first and verify they fail.
- Implement repository and domain behavior before final UI polish.
- Complete telemetry and validation evidence before story closure.
- Validate each story independently before moving to next priority.

## Parallel Opportunities

- Setup parallel tasks: T003-T005.
- Foundational parallel tasks: T012-T013.
- US1 parallel tasks: T021-T025, T030-T031.
- US2 parallel tasks: T034-T037, T042.
- US3 parallel tasks: T046-T049, T053.
- Polish parallel tasks: T056-T057.

## Parallel Example: User Story 1

```bash
Task: "T021 [US1] Add failing unit tests for output start state transitions in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTest.kt"
Task: "T022 [US1] Add failing repository contract tests for start/idempotency in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiOutputRepositoryContractTest.kt"
Task: "T025 [US1] Add failing Playwright E2E test for start-output discoverability in testing/e2e/tests/us1-start-output.spec.ts"

Task: "T030 [US1] Implement output control Material 3 UI states in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt"
Task: "T031 [US1] Add source-list action to open output control for selected source in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt"
```

## Parallel Example: User Story 2

```bash
Task: "T034 [US2] Add failing unit tests for stop-output and control availability in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlStopStateTest.kt"
Task: "T036 [US2] Add failing Playwright E2E test for output active metadata rendering in testing/e2e/tests/us2-output-status.spec.ts"
Task: "T037 [US2] Add failing Playwright E2E test for stop-output propagation in testing/e2e/tests/us2-stop-output.spec.ts"

Task: "T040 [US2] Implement output health snapshot coordinator in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/OutputSessionCoordinator.kt"
Task: "T042 [US2] Implement output active and stop status rendering in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt"
```

## Parallel Example: User Story 3

```bash
Task: "T046 [US3] Add failing unit tests for interruption and retry-window behavior in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputRecoveryViewModelTest.kt"
Task: "T048 [US3] Add failing Playwright E2E test for interruption recovery actions in testing/e2e/tests/us3-recovery-actions.spec.ts"
Task: "T049 [US3] Add failing Playwright E2E test for source-loss interruption propagation in testing/e2e/tests/us3-source-loss.spec.ts"

Task: "T050 [US3] Implement output interruption detection and retry coordinator in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/OutputRecoveryCoordinator.kt"
Task: "T053 [US3] Implement recovery action components and messaging in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt"
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 and Phase 2.
2. Complete all US1 tests and implementation tasks.
3. Validate US1 independently using Playwright and record evidence.

### Incremental Delivery

1. Deliver US1 (start output).
2. Deliver US2 (control and monitor).
3. Deliver US3 (interruption recovery).
4. Complete Phase 6 Playwright interoperability, toolchain review, phone/tablet matrix, and success-criteria reporting.

### Parallel Team Strategy

1. Team A executes setup + foundational tasks.
2. Team B implements US1 output start pipeline.
3. Team C implements US2 control/status while Team D prepares US3 failing tests.
4. Shared validation team executes dual-emulator Playwright E2E, release checks, and reporting.

## Notes

- `[P]` tasks denote file-level parallelization opportunities.
- Every task includes an explicit file path and executable action.
- Strict TDD is mandatory for all user story phases.
- Dual-emulator publish->discover->play->stop validation is mandatory before closure.
- Any non-Playwright E2E path requires approved exception documentation.
