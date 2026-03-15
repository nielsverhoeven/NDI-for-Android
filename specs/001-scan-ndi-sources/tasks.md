# Tasks: NDI Source Discovery and Viewing

**Input**: Design documents from `/specs/001-scan-ndi-sources/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Test tasks are REQUIRED. Follow strict TDD: write failing tests first, then implement, then refactor.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish Android project baseline and remediate machine prerequisites before feature coding.

- [x] T001 Create prerequisite verification script in scripts/verify-android-prereqs.ps1
- [x] T002 Add prerequisite verification documentation in docs/android-prerequisites.md
- [x] T003 [P] Add CI preflight prerequisite check in .github/workflows/android-ci.yml
- [x] T004 Initialize Gradle settings and module includes in settings.gradle.kts
- [x] T005 [P] Initialize root build configuration in build.gradle.kts
- [x] T006 [P] Define Gradle version catalog entries in gradle/libs.versions.toml
- [x] T007 Configure project-local SDK references template in local.properties.example
- [x] T008 Create app module build configuration with minSdk 24/targetSdk 34 in app/build.gradle.kts
- [x] T009 [P] Create Android manifest baseline in app/src/main/AndroidManifest.xml
- [x] T010 [P] Add ProGuard/R8 baseline rules in app/proguard-rules.pro

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build shared architecture and native integration scaffolding required by all user stories.

**CRITICAL**: No user story work can begin until this phase is complete.

- [x] T011 Create single-activity host and navigation container in app/src/main/java/com/ndi/app/MainActivity.kt
- [x] T012 Create navigation graph with source list and viewer routes in app/src/main/res/navigation/main_nav_graph.xml
- [x] T013 [P] Create core discovery and playback state models in core/model/src/main/java/com/ndi/core/model/NdiModels.kt
- [x] T014 [P] Configure Room database and DAOs for user selection/session state in core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt
- [x] T015 Create repository interfaces contract in feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
- [x] T016 [P] Create NDI native bridge CMake configuration in ndi/sdk-bridge/src/main/cpp/CMakeLists.txt
- [x] T017 [P] Add JNI entry points for source discovery and receive lifecycle in ndi/sdk-bridge/src/main/cpp/ndi_bridge.cpp
- [x] T018 [P] Add ABI-native library packaging placeholders in ndi/sdk-bridge/src/main/jniLibs/.gitkeep
- [x] T019 Implement sdk-bridge module build configuration (NDK/CMake/jniLibs) in ndi/sdk-bridge/build.gradle.kts
- [x] T020 Implement discovery repository baseline wiring sdk-bridge and Room in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt
- [x] T021 [P] Add non-sensitive telemetry event contract in core/model/src/main/java/com/ndi/core/model/TelemetryEvent.kt
- [x] T022 Add dependency injection wiring for app/core/feature modules in app/src/main/java/com/ndi/app/di/AppGraph.kt

**Checkpoint**: Foundation ready. User stories can now be implemented and tested independently.

---

## Phase 3: User Story 1 - Discover Available NDI Sources (Priority: P1) 🎯 MVP

**Goal**: Users can discover available NDI sources with foreground auto-refresh and manual refresh.

**Independent Test**: On a network with active senders, opening the source list shows discoverable sources; with no senders, empty state is shown with retry.

### Tests for User Story 1 (REQUIRED)

- [ ] T023 [P] [US1] Add failing unit tests for discovery state transitions in feature/ndi-browser/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt
- [ ] T024 [P] [US1] Add failing unit tests for foreground-only 5-second refresh policy in feature/ndi-browser/src/test/java/com/ndi/feature/ndibrowser/source_list/DiscoveryRefreshPolicyTest.kt
- [ ] T025 [P] [US1] Add failing UI test for loading-success-empty discovery states in feature/ndi-browser/src/androidTest/java/com/ndi/feature/ndibrowser/source_list/SourceListScreenTest.kt
- [ ] T060 [P] [US1] Add tablet-focused UI verification for source list usability in feature/ndi-browser/src/androidTest/java/com/ndi/feature/ndibrowser/source_list/SourceListTabletUiTest.kt
- [ ] T026 [P] [US1] Add failing contract test for NdiDiscoveryRepository behavior in feature/ndi-browser/src/test/java/com/ndi/feature/ndibrowser/data/NdiDiscoveryRepositoryContractTest.kt

### Implementation for User Story 1

- [ ] T027 [US1] Implement SourceListViewModel with visible/hidden/manual-refresh intents in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [ ] T028 [P] [US1] Implement source list UI with Material 3 loading/empty/error states in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
- [ ] T058 [US1] Implement adaptive source list layout behavior for compact and expanded widths in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListAdaptiveLayout.kt
- [ ] T029 [US1] Implement discovery scheduler enforcing foreground-only 5-second ticks in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/DiscoveryRefreshCoordinator.kt
- [ ] T030 [US1] Implement canonical source identity mapping (sourceId over displayName) in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/mapper/NdiSourceMapper.kt
- [ ] T031 [US1] Wire source list route into app navigation graph in app/src/main/res/navigation/main_nav_graph.xml
- [ ] T032 [US1] Add source list telemetry emission for discovery events in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListTelemetry.kt

**Checkpoint**: User Story 1 is fully functional and testable independently on phone and tablet layouts.

---

## Phase 4: User Story 2 - Select and View a Source (Priority: P2)

**Goal**: Users can select one source from the list and view its feed; prior selection is highlighted on app launch without autoplay.

**Independent Test**: Select a source and verify viewer playback appears; relaunch app and verify previous source is highlighted but not auto-played.

### Tests for User Story 2 (REQUIRED)

- [ ] T033 [P] [US2] Add failing unit tests for source selection persistence and no-autoplay behavior in feature/ndi-browser/src/test/java/com/ndi/feature/ndibrowser/selection/UserSelectionStateTest.kt
- [ ] T034 [P] [US2] Add failing unit tests for ViewerViewModel connect/play transitions in feature/ndi-browser/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModelTest.kt
- [ ] T035 [P] [US2] Add failing UI test for list-selection to viewer navigation in feature/ndi-browser/src/androidTest/java/com/ndi/feature/ndibrowser/viewer/ViewerNavigationTest.kt
- [ ] T061 [P] [US2] Add tablet-focused UI verification for source selection and playback visibility in feature/ndi-browser/src/androidTest/java/com/ndi/feature/ndibrowser/viewer/ViewerTabletUiTest.kt
- [ ] T036 [P] [US2] Add failing contract test for UserSelectionRepository in feature/ndi-browser/src/test/java/com/ndi/feature/ndibrowser/data/UserSelectionRepositoryContractTest.kt

### Implementation for User Story 2

- [ ] T037 [US2] Implement UserSelectionRepository with Room persistence in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/UserSelectionRepositoryImpl.kt
- [ ] T038 [US2] Implement source preselection/highlight on list load in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourcePreselectionController.kt
- [ ] T039 [US2] Implement ViewerViewModel connect/play state machine in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModel.kt
- [ ] T040 [P] [US2] Implement viewer screen rendering and playback surface in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt
- [ ] T059 [US2] Implement adaptive viewer layout behavior for phone and tablet form factors in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerAdaptiveLayout.kt
- [ ] T041 [US2] Implement selection-to-viewer navigation argument handling in app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt
- [ ] T042 [US2] Add viewer lifecycle telemetry for playback start/stop in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerTelemetry.kt

**Checkpoint**: User Stories 1 and 2 both work independently and satisfy phone/tablet layout expectations.

---

## Phase 5: User Story 3 - Handle Source and Network Interruptions (Priority: P3)

**Goal**: Playback interruptions are handled with bounded retry and clear user recovery actions.

**Independent Test**: Interrupt source/network during playback, verify auto-retry for up to 15 seconds, then recovery actions are shown.

### Tests for User Story 3 (REQUIRED)

- [ ] T043 [P] [US3] Add failing unit tests for 15-second retry window behavior in feature/ndi-browser/src/test/java/com/ndi/feature/ndibrowser/viewer/RetryWindowPolicyTest.kt
- [ ] T044 [P] [US3] Add failing unit tests for interruption-to-recovery state transitions in feature/ndi-browser/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerInterruptionStateTest.kt
- [ ] T045 [P] [US3] Add failing UI test for interruption messaging and recovery actions in feature/ndi-browser/src/androidTest/java/com/ndi/feature/ndibrowser/viewer/ViewerRecoveryFlowTest.kt
- [ ] T046 [P] [US3] Add failing contract test for NdiViewerRepository interruption semantics in feature/ndi-browser/src/test/java/com/ndi/feature/ndibrowser/data/NdiViewerRepositoryContractTest.kt

### Implementation for User Story 3

- [ ] T047 [US3] Implement reconnect coordinator with 15-second bounded retries in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/ViewerReconnectCoordinator.kt
- [ ] T048 [US3] Implement interruption-aware repository behavior in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiViewerRepositoryImpl.kt
- [ ] T049 [US3] Implement viewer interruption UI states and recovery actions in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerRecoveryUiState.kt
- [ ] T050 [US3] Wire retry and return-to-list actions from ViewerViewModel in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModel.kt
- [ ] T051 [US3] Add interruption and recovery telemetry events in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerRecoveryTelemetry.kt

**Checkpoint**: All user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final release hardening, compliance validation, and measurable success-criteria reporting.

- [ ] T052 [P] Add no-location-permission compliance check in app/src/main/AndroidManifest.xml
- [ ] T053 Configure release minification/shrinking verification in app/build.gradle.kts
- [ ] T054 [P] Add ABI packaging and startup sanity checks for NDI bridge in ndi/sdk-bridge/src/test/java/com/ndi/sdkbridge/NativeLoadSanityTest.kt
- [ ] T055 [P] Add API 24 compatibility regression tests in feature/ndi-browser/src/androidTest/java/com/ndi/feature/ndibrowser/Api24CompatibilityTest.kt
- [ ] T056 Update feature implementation documentation in docs/ndi-feature.md
- [ ] T057 Execute quickstart validation and publish pass/fail evidence in specs/001-scan-ndi-sources/validation/quickstart-validation-report.md

### Success Criteria Measurement Tasks

- [ ] T062 [P] Add SC-001 discovery-latency benchmark task with 90th-percentile threshold check in feature/ndi-browser/src/androidTest/java/com/ndi/feature/ndibrowser/metrics/DiscoveryLatencyBenchmarkTest.kt
- [ ] T063 [P] Add SC-002 first-frame-time benchmark task with 90th-percentile threshold check in feature/ndi-browser/src/androidTest/java/com/ndi/feature/ndibrowser/metrics/FirstFrameLatencyBenchmarkTest.kt
- [ ] T064 [P] Add SC-003 interruption-recovery success-rate measurement task in feature/ndi-browser/src/androidTest/java/com/ndi/feature/ndibrowser/metrics/RecoverySuccessRateTest.kt
- [ ] T065 [P] Add SC-004 end-to-end completion-rate measurement task in feature/ndi-browser/src/androidTest/java/com/ndi/feature/ndibrowser/metrics/FlowCompletionRateTest.kt
- [ ] T066 Aggregate SC-001..SC-004 measurement outputs into specs/001-scan-ndi-sources/validation/success-criteria-report.md with explicit threshold verdicts

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: Starts immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1; blocks all user story implementation.
- **Phase 3 (US1)**: Depends on Phase 2.
- **Phase 4 (US2)**: Depends on Phase 2; may integrate with US1 outputs but remains testable via fixture-based source inputs.
- **Phase 5 (US3)**: Depends on Phase 2 and viewer pipeline from US2.
- **Phase 6 (Polish)**: Depends on completion of desired user stories.

### User Story Dependencies

- **US1 (P1)**: No dependency on other user stories once foundation is done.
- **US2 (P2)**: Requires foundational modules; remains independently testable using fixture-based discovered source inputs.
- **US3 (P3)**: Requires foundational modules and US2 viewer pipeline.

### Within Each User Story

- Tests MUST be written and failing before implementation.
- ViewModel/domain behavior before UI wiring where possible.
- Repository contract compliance before integration telemetry.
- Story checkpoint validation before moving to the next story.

## Parallel Opportunities

- Setup tasks marked [P] can run in parallel (T003, T005, T006, T009, T010).
- Foundational tasks marked [P] can run in parallel (T013, T014, T016, T017, T018, T021).
- US1 tests (T023-T026, T060) and US1 UI/adaptive work (T028, T058, T030) can run in parallel.
- US2 tests (T033-T036, T061) and US2 UI/adaptive work (T040, T059) can run in parallel.
- US3 tests (T043-T046) and telemetry wiring (T051) can run in parallel.
- Polish tasks marked [P] can run in parallel (T052, T054, T055, T062, T063, T064, T065).

## Parallel Example: User Story 1

```bash
Task: "T023 [US1] Add failing unit tests for discovery state transitions in feature/ndi-browser/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt"
Task: "T024 [US1] Add failing unit tests for foreground-only 5-second refresh policy in feature/ndi-browser/src/test/java/com/ndi/feature/ndibrowser/source_list/DiscoveryRefreshPolicyTest.kt"
Task: "T026 [US1] Add failing contract test for NdiDiscoveryRepository behavior in feature/ndi-browser/src/test/java/com/ndi/feature/ndibrowser/data/NdiDiscoveryRepositoryContractTest.kt"

Task: "T028 [US1] Implement source list UI with Material 3 loading/empty/error states in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt"
Task: "T030 [US1] Implement canonical source identity mapping in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/mapper/NdiSourceMapper.kt"
```

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1 (Setup).
2. Complete Phase 2 (Foundational).
3. Complete Phase 3 (US1).
4. Validate US1 independently against discovery success/empty/failure states.

### Incremental Delivery

1. Deliver MVP with discovery list (US1).
2. Add source selection and viewer playback (US2).
3. Add interruption resilience and recovery actions (US3).
4. Finish with release hardening and compliance checks (Phase 6).

### Parallel Team Strategy

1. Team A: Setup + foundational module scaffolding.
2. Team B: US1 discovery list and refresh behavior.
3. Team C: US2 viewer flow after foundational checkpoint.
4. Team D: US3 interruption/recovery and polish validations.

## Notes

- [P] tasks indicate no direct file-level conflict and can be parallelized.
- All tasks include explicit file paths to stay executable by an LLM agent.
- Strict TDD is mandatory: write failing tests first, then implement.
- No location permission is allowed in this feature scope.
