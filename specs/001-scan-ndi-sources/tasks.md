# Tasks: NDI Source Discovery and Viewing

**Input**: Design documents from `/specs/001-scan-ndi-sources/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Test tasks are REQUIRED. Follow strict TDD: write failing tests first, then implement, then refactor.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Align shared project scaffolding and prerequisite gates with Constitution 1.1.0 and blocker tracking.

- [X] T001 Align prerequisite package checks with the current repo baseline in scripts/verify-android-prereqs.ps1
- [X] T002 Update local setup guidance for baseline and blocker policy in docs/android-prerequisites.md
- [X] T003 [P] Align CI Java/toolchain setup and preflight gate in .github/workflows/android-ci.yml
- [X] T004 [P] Create and populate `TOOLCHAIN-001` blocker record (owner, affected components, target resolution date, target resolution cycle) in specs/001-scan-ndi-sources/validation/toolchain-blockers.md
- [X] T005 Record release-readiness validation matrix for phone and tablet in specs/001-scan-ndi-sources/validation/release-validation-matrix.md
- [X] T006 [P] Add feature-level validation command checklist in specs/001-scan-ndi-sources/validation/quickstart-validation-report.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Ensure shared contracts, data model, navigation, and repository boundaries are complete before story work.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T007 Ensure canonical source and viewer entities match the latest model in core/model/src/main/java/com/ndi/core/model/NdiModels.kt
- [X] T008 [P] Ensure telemetry event schema covers discovery/playback/recovery flows in core/model/src/main/java/com/ndi/core/model/TelemetryEvent.kt
- [X] T009 Ensure Room persistence model supports selection continuity in core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt
- [X] T010 [P] Align repository interfaces with contract semantics in feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
- [X] T011 Ensure app graph wiring preserves repository-only data access in app/src/main/java/com/ndi/app/di/AppGraph.kt
- [X] T012 [P] Verify source-list and viewer routes remain single-activity contract-compliant in app/src/main/res/navigation/main_nav_graph.xml
- [X] T013 Ensure native bridge interface remains isolated to sdk-bridge module in ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt
- [X] T014 Add/update foundational contract compliance notes in specs/001-scan-ndi-sources/validation/foundation-checkpoint.md

**Checkpoint**: Foundation ready. User story implementation can proceed.

---

## Phase 3: User Story 1 - Discover Available NDI Sources (Priority: P1) 🎯 MVP

**Goal**: Users can discover NDI sources with manual refresh and foreground-only periodic refresh.

**Independent Test**: With active senders, the source list shows results and refreshes every 5 seconds while visible; without senders, a clear empty state appears.

### Tests for User Story 1 (REQUIRED)

- [X] T015 [P] [US1] Add failing unit tests for discovery state transitions in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt
- [X] T016 [P] [US1] Add failing unit tests for visibility-bound refresh policy in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DiscoveryRefreshPolicyTest.kt
- [X] T017 [P] [US1] Add failing repository contract test for discovery semantics in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiDiscoveryRepositoryContractTest.kt
- [X] T018 [P] [US1] Add failing UI test for loading, success, empty, and failure list states in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/source_list/SourceListScreenTest.kt
- [X] T019 [P] [US1] Add failing tablet UI discovery test in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/source_list/SourceListTabletUiTest.kt

### Implementation for User Story 1

- [X] T020 [US1] Implement source list state machine and intents in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [X] T021 [P] [US1] Implement source list Material 3 UI states in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
- [X] T022 [P] [US1] Implement foreground refresh coordinator in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/DiscoveryRefreshCoordinator.kt
- [X] T023 [US1] Implement canonical identity mapping (sourceId-first) in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/mapper/NdiSourceMapper.kt
- [X] T024 [US1] Implement discovery repository flow and overlap prevention in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt
- [X] T025 [P] [US1] Implement adaptive source list layout behavior in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListAdaptiveLayout.kt
- [X] T026 [US1] Emit discovery telemetry events from source list flow in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListTelemetry.kt
- [X] T027 [US1] Record US1 independent-test evidence in specs/001-scan-ndi-sources/validation/us1-discovery-validation.md

**Checkpoint**: User Story 1 is independently functional and testable.

---

## Phase 4: User Story 2 - Select and View a Source (Priority: P2)

**Goal**: Users select one source, view playback, and see previous selection highlighted on relaunch without autoplay.

**Independent Test**: Select a source and verify viewer playback; relaunch and verify highlight-only preselection without auto-navigation.

### Tests for User Story 2 (REQUIRED)

- [X] T028 [P] [US2] Add failing unit tests for selection persistence and no-autoplay behavior in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/selection/UserSelectionStateTest.kt
- [X] T029 [P] [US2] Add failing unit tests for viewer connect/play transitions in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModelTest.kt
- [X] T030 [P] [US2] Add failing repository contract test for selection persistence in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/UserSelectionRepositoryContractTest.kt
- [X] T031 [P] [US2] Add failing navigation UI test from list to viewer in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/viewer/ViewerNavigationTest.kt
- [X] T032 [P] [US2] Add failing tablet viewer rendering test in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/viewer/ViewerTabletUiTest.kt

### Implementation for User Story 2

- [X] T033 [US2] Implement Room-backed selection repository in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/UserSelectionRepositoryImpl.kt
- [X] T034 [US2] Implement preselection/highlight behavior on source list load in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourcePreselectionController.kt
- [X] T035 [US2] Implement viewer state machine for selected source playback in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModel.kt
- [X] T036 [P] [US2] Implement viewer playback UI states in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt
- [X] T037 [P] [US2] Implement adaptive viewer layout for phone/tablet in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerAdaptiveLayout.kt
- [X] T038 [US2] Implement navigation argument handling for selected source in app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt
- [X] T039 [US2] Implement viewer lifecycle telemetry events in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerTelemetry.kt
- [X] T040 [US2] Ensure no-autoplay behavior on launch path in app/src/main/java/com/ndi/app/MainActivity.kt
- [X] T041 [US2] Record US2 independent-test evidence in specs/001-scan-ndi-sources/validation/us2-selection-viewer-validation.md

**Checkpoint**: User Stories 1 and 2 are independently functional.

---

## Phase 5: User Story 3 - Handle Source and Network Interruptions (Priority: P3)

**Goal**: Interruptions trigger bounded auto-retry and then explicit recovery actions.

**Independent Test**: Interrupt active playback and verify retry attempts are bounded at 15 seconds, then recovery actions are displayed.

### Tests for User Story 3 (REQUIRED)

- [X] T042 [P] [US3] Add failing unit tests for bounded retry window policy in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/RetryWindowPolicyTest.kt
- [X] T043 [P] [US3] Add failing unit tests for interruption state transitions in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerInterruptionStateTest.kt
- [X] T044 [P] [US3] Add failing repository contract test for interruption semantics in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiViewerRepositoryContractTest.kt
- [X] T045 [P] [US3] Add failing UI test for recovery actions after unresolved interruption in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/viewer/ViewerRecoveryFlowTest.kt

### Implementation for User Story 3

- [X] T046 [US3] Implement reconnect coordinator with 15-second bound in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/ViewerReconnectCoordinator.kt
- [X] T047 [US3] Implement interruption-aware viewer repository behavior in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiViewerRepositoryImpl.kt
- [X] T048 [US3] Implement interruption and recovery UI state model in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerRecoveryUiState.kt
- [X] T049 [US3] Wire retry and return-to-list intents in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModel.kt
- [X] T050 [US3] Emit interruption and recovery telemetry events in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerRecoveryTelemetry.kt
- [X] T051 [US3] Record US3 independent-test evidence in specs/001-scan-ndi-sources/validation/us3-recovery-validation.md

**Checkpoint**: All user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Complete release hardening, constitutional compliance, and measurable outcomes.

- [X] T052 Verify no-location-permission policy in app/src/main/AndroidManifest.xml
- [X] T053 Verify release shrinking/optimization guard in app/build.gradle.kts
- [X] T054 [P] Add/refresh native bridge load sanity tests in ndi/sdk-bridge/src/test/java/com/ndi/sdkbridge/NativeLoadSanityTest.kt
- [X] T055 [P] Add/refresh API 24 compatibility regression tests in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/Api24CompatibilityTest.kt
- [X] T056 Execute quickstart validation and capture results in specs/001-scan-ndi-sources/validation/quickstart-validation-report.md
- [X] T057 [P] Add discovery latency benchmark and threshold assertion in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/metrics/DiscoveryLatencyBenchmarkTest.kt
- [X] T058 [P] Add first-frame latency benchmark and threshold assertion in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/metrics/FirstFrameLatencyBenchmarkTest.kt
- [X] T059 [P] Add interruption-recovery success-rate measurement test in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/metrics/RecoverySuccessRateTest.kt
- [X] T060 [P] Add end-to-end flow completion-rate measurement test in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/metrics/FlowCompletionRateTest.kt
- [X] T061 Aggregate SC-001..SC-005 outcomes and constitutional toolchain status in specs/001-scan-ndi-sources/validation/success-criteria-report.md
- [X] T062 Refresh `TOOLCHAIN-001` blocker record after validation (status, owner, affected components, target resolution date, target resolution cycle) in specs/001-scan-ndi-sources/validation/toolchain-blockers.md
- [X] T063 [P] Update feature implementation notes for release readiness in docs/ndi-feature.md
- [X] T064 [P] Add pull request checklist item requiring test-first evidence links in .github/PULL_REQUEST_TEMPLATE.md
- [X] T065 Add feature-level TDD evidence summary (failing tests before implementation and passing tests after) in specs/001-scan-ndi-sources/validation/tdd-evidence-report.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies.
- **Phase 2 (Foundational)**: Depends on Phase 1 and blocks all user stories.
- **Phase 3 (US1)**: Depends on Phase 2.
- **Phase 4 (US2)**: Depends on Phase 2 and may reuse US1 outputs.
- **Phase 5 (US3)**: Depends on Phase 2 and US2 viewer pipeline.
- **Phase 6 (Polish)**: Depends on all desired user stories.

### User Story Dependencies

- **US1 (P1)**: Starts after foundational checkpoint; no dependency on US2/US3.
- **US2 (P2)**: Starts after foundational checkpoint; independently testable with fixture discovery data.
- **US3 (P3)**: Starts after foundational checkpoint but functionally relies on viewer pipeline from US2.

### Within Each User Story

- Write tests first and verify they fail.
- Implement ViewModel/domain logic before final UI integration where possible.
- Keep repository contract behavior consistent before telemetry wiring.
- Validate each story independently before moving on.

## Parallel Opportunities

- Setup parallel tasks: T003, T004, T006.
- Foundational parallel tasks: T008, T010, T012, T013.
- US1 parallel tasks: T015-T019, T021-T023, T025.
- US2 parallel tasks: T028-T032, T036-T037.
- US3 parallel tasks: T042-T045.
- Polish parallel tasks: T054-T055, T057-T060, T063-T064.

## Parallel Example: User Story 1

```bash
Task: "T015 [US1] Add failing unit tests for discovery state transitions in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt"
Task: "T016 [US1] Add failing unit tests for visibility-bound refresh policy in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DiscoveryRefreshPolicyTest.kt"
Task: "T017 [US1] Add failing repository contract test for discovery semantics in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiDiscoveryRepositoryContractTest.kt"

Task: "T021 [US1] Implement source list Material 3 UI states in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt"
Task: "T022 [US1] Implement foreground refresh coordinator in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/DiscoveryRefreshCoordinator.kt"
Task: "T023 [US1] Implement canonical identity mapping (sourceId-first) in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/mapper/NdiSourceMapper.kt"
```

## Parallel Example: User Story 2

```bash
Task: "T028 [US2] Add failing unit tests for selection persistence and no-autoplay behavior in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/selection/UserSelectionStateTest.kt"
Task: "T031 [US2] Add failing navigation UI test from list to viewer in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/viewer/ViewerNavigationTest.kt"
Task: "T032 [US2] Add failing tablet viewer rendering test in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/viewer/ViewerTabletUiTest.kt"

Task: "T036 [US2] Implement viewer playback UI states in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt"
Task: "T037 [US2] Implement adaptive viewer layout for phone/tablet in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerAdaptiveLayout.kt"
```

## Parallel Example: User Story 3

```bash
Task: "T042 [US3] Add failing unit tests for bounded retry window policy in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/RetryWindowPolicyTest.kt"
Task: "T043 [US3] Add failing unit tests for interruption state transitions in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerInterruptionStateTest.kt"
Task: "T045 [US3] Add failing UI test for recovery actions after unresolved interruption in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/viewer/ViewerRecoveryFlowTest.kt"

Task: "T046 [US3] Implement reconnect coordinator with 15-second bound in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/ViewerReconnectCoordinator.kt"
Task: "T048 [US3] Implement interruption and recovery UI state model in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerRecoveryUiState.kt"
Task: "T050 [US3] Emit interruption and recovery telemetry events in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerRecoveryTelemetry.kt"
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 and Phase 2.
2. Complete all US1 tests and implementation tasks.
3. Validate discovery behavior independently using T027 evidence output.

### Incremental Delivery

1. Deliver US1 (discovery).
2. Deliver US2 (selection + playback).
3. Deliver US3 (interruption recovery).
4. Complete Phase 6 release hardening and success criteria reporting.

### Parallel Team Strategy

1. Team A handles setup/foundational tasks.
2. Team B handles US1 while Team C prepares US2 tests after foundation.
3. Team C implements US2 viewer pipeline, then Team D executes US3 recovery flow.
4. Shared release team executes Phase 6 benchmarks, reports, and blocker governance updates.

## Notes

- `[P]` tasks denote file-level parallelization opportunities.
- Every task references explicit file paths and can be executed directly.
- Strict TDD is mandatory throughout all story phases.
- Location permission remains out of scope for this feature.
- `TOOLCHAIN-001` must remain explicitly tracked until resolved with both target resolution date and target resolution cycle.

