# Tasks: NDI Output Validation with Dual-Emulator Screen Share

**Input**: Design documents from `/specs/002-stream-ndi-source/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Test tasks are REQUIRED. Follow strict TDD: write failing tests first, then implement, then refactor. End-to-end tests MUST default to Playwright unless a documented exception is approved.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare the Android-device Playwright harness and dual-emulator execution environment.

- [X] T001 Update Android-device Playwright dependencies and scripts in testing/e2e/package.json
- [X] T002 [P] Configure Android-device projects, reporters, and timeouts in testing/e2e/playwright.config.ts
- [X] T003 [P] Create reusable Android emulator fixtures in testing/e2e/tests/support/android-device-fixtures.ts
- [X] T004 [P] Expand dual-emulator launcher, preflight, and artifact capture in testing/e2e/scripts/run-dual-emulator-e2e.ps1
- [X] T005 Create harness usage and troubleshooting notes in testing/e2e/README.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add shared contracts, models, persistence, DI, permission/compliance evidence, and native bridge seams.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 Extend output and screen-capture contracts in feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
- [X] T007 [P] Add output input identity and consent-aware model types in core/model/src/main/java/com/ndi/core/model/NdiModels.kt
- [X] T008 [P] Add telemetry constants for consent, idempotency guard outcomes, and dual-emulator milestones in core/model/src/main/java/com/ndi/core/model/TelemetryEvent.kt
- [X] T009 Add Room schema and migration fields for input kind and consent state in core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt
- [X] T010 Add screen-capture dependency providers to feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputTelemetry.kt
- [X] T011 Wire screen-capture repositories and bridge dependencies in app/src/main/java/com/ndi/app/di/AppGraph.kt
- [X] T012 Extend Kotlin bridge APIs for local screen-share sender lifecycle in ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt
- [X] T013 Implement native local screen-share sender entrypoints in ndi/sdk-bridge/src/main/cpp/ndi_bridge.cpp
- [X] T014 Update native build inputs for screen-share publishing in ndi/sdk-bridge/src/main/cpp/CMakeLists.txt
- [X] T015 Produce permission-impact validation report for output/screen-capture changes in specs/002-stream-ndi-source/validation/permission-justification.md
- [X] T016 Define Material 3 compliance checklist baseline for modified output/source-list states in specs/002-stream-ndi-source/validation/material3-compliance-report.md

**Checkpoint**: Foundation ready. User story implementation can proceed.

---

## Phase 3: User Story 1 - Start Network Output from a Selected Source (Priority: P1) 🎯 MVP

**Goal**: Emulator A can select a valid input source (discoverable NDI or local screen share), start publishing, and become discoverable to emulator B.

**Independent Test**: In a dual-emulator run, emulator A selects local screen share, accepts consent, starts output, and emulator B discovers and opens the stream in viewer playback.

### Tests for User Story 1 (REQUIRED)

- [X] T017 [P] [US1] Add failing source-selection tests for discoverable and `device-screen:` identities in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt
- [X] T018 [P] [US1] Add failing consent/readiness start tests in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTest.kt
- [X] T019 [P] [US1] Add failing repository tests for pre-active readiness checks in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiOutputRepositoryContractTest.kt
- [X] T020 [P] [US1] Add failing idempotent rapid-start guard tests (`FR-006`) in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiOutputRepositoryContractTest.kt
- [X] T021 [P] [US1] Add failing stream-name conflict resolution tests for consolidated naming requirement (`FR-011`) in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/OutputSessionMapperTest.kt
- [X] T022 [P] [US1] Add failing Android-device publisher/discovery Playwright coverage in testing/e2e/tests/us1-start-output.spec.ts

### Implementation for User Story 1

- [X] T023 [US1] Inject reserved local screen-share source identity into discovery snapshots in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt
- [X] T024 [US1] Implement screen-capture consent storage and lifecycle handling in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/ScreenCaptureConsentRepositoryImpl.kt
- [X] T025 [US1] Implement readiness validation and consent-gated start flow in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt
- [X] T026 [US1] Implement idempotent rapid-start duplicate-session guard (`FR-006`) in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt
- [X] T027 [US1] Implement unique stream-name conflict resolution for outbound identities under consolidated requirement (`FR-011`) in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/mapper/OutputSessionMapper.kt
- [X] T028 [US1] Add consent request state and start orchestration to feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
- [X] T029 [P] [US1] Launch MediaProjection consent and return results in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlFragment.kt
- [X] T030 [P] [US1] Render local screen-share consent/start UI states in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt
- [X] T031 [P] [US1] Update output control layout copy and controls for consent/start flow in feature/ndi-browser/presentation/src/main/res/layout/fragment_output_control.xml
- [X] T032 [P] [US1] Add local screen-share source affordance in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
- [X] T033 [P] [US1] Update source row rendering for reserved local screen source in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/adapter/SourceAdapter.kt
- [X] T034 [US1] Add local source selection and output navigation handling in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [X] T035 [US1] Emit consent/start/idempotency telemetry events in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputTelemetry.kt

**Checkpoint**: User Story 1 is independently functional and testable.

---

## Phase 4: User Story 2 - Control and Monitor Output Session (Priority: P2)

**Goal**: The operator can monitor active output state, stop output safely, and verify stop propagation to receiver playback.

**Independent Test**: Start output on emulator A, verify active metadata, stop output, and verify emulator B transitions out of active playback.

### Tests for User Story 2 (REQUIRED)

- [X] T036 [P] [US2] Add failing stop/control state tests in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlStopStateTest.kt
- [X] T037 [P] [US2] Add failing idempotent rapid-stop duplicate-session tests in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiOutputRepositoryStopContractTest.kt
- [X] T038 [P] [US2] Add failing repository tests for active metadata and health state transitions in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiOutputRepositoryStopContractTest.kt
- [X] T039 [P] [US2] Add failing Android-device stop-propagation coverage in testing/e2e/tests/us2-stop-output.spec.ts

### Implementation for User Story 2

- [ ] T040 [US2] Persist input kind and continuity metadata in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/OutputConfigurationRepositoryImpl.kt
- [ ] T041 [US2] Publish active/stopping/stopped health transitions in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/OutputSessionCoordinator.kt
- [ ] T042 [US2] Implement idempotent rapid-stop guard behavior in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt
- [ ] T043 [US2] Implement stop-state and active metadata handling in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
- [ ] T044 [P] [US2] Render active/stop status affordances in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt
- [ ] T045 [US2] Stop local screen-share sender and clear capture state in ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt
- [ ] T046 [P] [US2] Add receiver playback stop assertions and helpers in testing/e2e/tests/support/android-device-fixtures.ts
- [ ] T047 [US2] Emit output-stop and active-session telemetry milestones in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputTelemetry.kt

**Checkpoint**: User Stories 1 and 2 are independently functional.

---

## Phase 5: User Story 3 - Recover from Interruptions Gracefully (Priority: P3)

**Goal**: When input/capture/transport is interrupted, the app surfaces recovery actions and retries within the bounded window.

**Independent Test**: During active output, simulate source/capture/network loss; verify interruption state and retry/stop recovery with receiver propagation.

### Tests for User Story 3 (REQUIRED)

- [ ] T048 [P] [US3] Add failing interruption/retry tests for capture and network loss in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputRecoveryViewModelTest.kt
- [ ] T049 [P] [US3] Add failing repository retry-window tests for interrupted sessions in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiOutputRecoveryRepositoryContractTest.kt
- [ ] T050 [P] [US3] Add failing Android-device interruption recovery coverage in testing/e2e/tests/us3-recovery-actions.spec.ts
- [ ] T051 [P] [US3] Add failing receiver interruption propagation coverage in testing/e2e/tests/us3-source-loss.spec.ts

### Implementation for User Story 3

- [ ] T052 [US3] Implement interruption detection and retry coordination in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/OutputRecoveryCoordinator.kt
- [ ] T053 [US3] Implement retry and failure mapping in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt
- [ ] T054 [US3] Surface interruption/retry/terminal stop state transitions in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
- [ ] T055 [P] [US3] Render interruption and recovery actions in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt
- [ ] T056 [US3] Emit interruption and retry telemetry for recovery outcomes in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputTelemetry.kt

**Checkpoint**: All user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Finalize Android-device interop automation, compliance evidence, and release success criteria reporting.

- [X] T057 [P] Replace placeholder interop scenario with Android-device orchestration in testing/e2e/tests/interop-dual-emulator.spec.ts
- [ ] T058 [P] Extend run metrics and artifact helpers for SC-001 through SC-006 evidence in testing/e2e/tests/support/metrics-fixtures.ts
- [ ] T059 Run full quickstart validation flow and capture outcomes in specs/002-stream-ndi-source/validation/quickstart-validation-report.md
- [ ] T060 Record dual-emulator evidence, latencies, and artifact links in specs/002-stream-ndi-source/validation/dual-emulator-e2e-report.md
- [ ] T061 Update release validation gate outcomes in specs/002-stream-ndi-source/validation/release-validation-matrix.md
- [ ] T062 Execute phone/tablet validation matrix and document FR-013 results in specs/002-stream-ndi-source/validation/device-layout-validation-report.md
- [ ] T063 Capture Material 3 compliance verification for modified UI states in specs/002-stream-ndi-source/validation/material3-compliance-report.md
- [ ] T064 Aggregate SC-001 through SC-006 outcomes (including SC-004 sample size and SC-006 pass rate) in specs/002-stream-ndi-source/validation/success-criteria-report.md
- [ ] T065 Update TOOLCHAIN-001 status after Android-device and release validation in specs/002-stream-ndi-source/validation/toolchain-currency-review.md
- [ ] T066 [P] Add API 24+ regression coverage for output flow in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/Api24CompatibilityTest.kt
- [ ] T067 Verify release-hardening coverage for the completed flow in app/build.gradle.kts

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies.
- **Phase 2 (Foundational)**: Depends on Phase 1 and blocks all user stories.
- **Phase 3 (US1)**: Depends on Phase 2.
- **Phase 4 (US2)**: Depends on Phase 2 and US1 output start pipeline.
- **Phase 5 (US3)**: Depends on Phase 2 and US1/US2 lifecycle pipeline.
- **Phase 6 (Polish)**: Depends on all desired user stories.

### User Story Dependencies

- **US1 (P1)**: Starts after foundational checkpoint; delivers MVP output-start flow.
- **US2 (P2)**: Starts after foundational checkpoint; relies on US1 output start behavior.
- **US3 (P3)**: Starts after foundational checkpoint; relies on US1/US2 lifecycle and control states.

### Within Each User Story

- Write tests first and verify they fail.
- Implement repository/native/domain behavior before final UI polish.
- Complete telemetry and validation evidence before story closure.
- Validate each story independently before moving to next priority.

## Parallel Opportunities

- Setup parallel tasks: T002-T004.
- Foundational parallel tasks: T007-T008.
- US1 parallel tasks: T017-T022, T029-T033.
- US2 parallel tasks: T036-T039, T044, T046.
- US3 parallel tasks: T048-T051, T055.
- Polish parallel tasks: T057-T058, T066.

## Parallel Example: User Story 1

```text
Task: T018 [US1] Add failing consent/readiness start tests in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTest.kt
Task: T020 [US1] Add failing idempotent rapid-start guard tests in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiOutputRepositoryContractTest.kt
Task: T021 [US1] Add failing stream-name conflict resolution tests in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/OutputSessionMapperTest.kt

Task: T030 [US1] Render local screen-share consent/start UI states in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt
Task: T032 [US1] Add local screen-share source affordance in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
Task: T033 [US1] Update source row rendering for reserved local screen source in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/adapter/SourceAdapter.kt
```

## Parallel Example: User Story 2

```text
Task: T036 [US2] Add failing stop/control state tests in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlStopStateTest.kt
Task: T037 [US2] Add failing idempotent rapid-stop duplicate-session tests in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiOutputRepositoryStopContractTest.kt
Task: T039 [US2] Add failing Android-device stop-propagation coverage in testing/e2e/tests/us2-stop-output.spec.ts

Task: T044 [US2] Render active/stop status affordances in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt
Task: T046 [US2] Add receiver playback stop assertions and helpers in testing/e2e/tests/support/android-device-fixtures.ts
```

## Parallel Example: User Story 3

```text
Task: T048 [US3] Add failing interruption/retry tests for capture and network loss in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputRecoveryViewModelTest.kt
Task: T049 [US3] Add failing repository retry-window tests for interrupted sessions in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiOutputRecoveryRepositoryContractTest.kt
Task: T050 [US3] Add failing Android-device interruption recovery coverage in testing/e2e/tests/us3-recovery-actions.spec.ts

Task: T055 [US3] Render interruption and recovery actions in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt
Task: T056 [US3] Emit interruption and retry telemetry for recovery outcomes in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputTelemetry.kt
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 and Phase 2.
2. Complete all US1 tests and implementation tasks.
3. Validate US1 independently with Android-device Playwright coverage.
4. Confirm consent-gated start, idempotent start guard, and unique stream naming.

### Incremental Delivery

1. Deliver US1 (start output from selected input source).
2. Deliver US2 (control, monitor, and stop propagation).
3. Deliver US3 (interruption and retry recovery).
4. Complete Phase 6 compliance evidence, success metrics, and release validation.

### Parallel Team Strategy

1. Team A executes setup + foundational tasks.
2. Team B implements US1 start/readiness/idempotency pipeline.
3. Team C implements US2 control/status while Team D prepares US3 failing tests.
4. Shared validation team executes dual-emulator E2E, Material 3 + permission reports, and success-criteria reporting.

## Notes

- `[P]` tasks denote file-level parallelization opportunities.
- Every task includes an explicit file path and executable action.
- Strict TDD is mandatory for all user story phases.
- Dual-emulator publish -> discover -> play -> stop validation is mandatory before closure.
- Any non-Playwright E2E path requires approved exception documentation.

[US1]: #phase-3-user-story-1---start-network-output-from-a-selected-source-priority-p1--mvp
[US2]: #phase-4-user-story-2---control-and-monitor-output-session-priority-p2
[US3]: #phase-5-user-story-3---recover-from-interruptions-gracefully-priority-p3

