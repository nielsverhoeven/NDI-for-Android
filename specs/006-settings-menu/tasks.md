# Tasks: Settings Menu and Diagnostic Overlay

**Input**: Design documents from `/specs/006-settings-menu/`
**Prerequisites**: `plan.md` (required), `spec.md` (required), `research.md`, `data-model.md`, `contracts/ndi-settings-feature-contract.md`, `quickstart.md`

**Tests**: Tests are required for this feature per constitution TDD gate and plan requirements.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: User story label (`US1`, `US2`, `US3`) for story-phase tasks only
- Every task includes concrete file path(s)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create feature scaffolding and shared UI/resources used by later phases.

- [X] T001 Create settings presentation package skeleton in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`
- [X] T002 [P] Create settings screen layout scaffold in `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings.xml`
- [X] T003 [P] Add settings and diagnostics baseline strings in `feature/ndi-browser/presentation/src/main/res/values/strings.xml`
- [X] T004 [P] Create developer overlay include scaffold in `feature/ndi-browser/presentation/src/main/res/layout/include_developer_overlay.xml`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared contracts and infrastructure that block all user stories.

**⚠️ CRITICAL**: No user story implementation begins until this phase is complete.

- [X] T005 Extend settings/discovery/overlay repository contracts in `feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt`
- [X] T006 Add settings/discovery/overlay model types in `core/model/src/main/java/com/ndi/core/model/NdiSettingsModels.kt`
- [X] T007 Implement settings persistence entities, DAO methods, and migration in `core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt`
- [X] T008 [P] Implement settings repository baseline in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiSettingsRepositoryImpl.kt`
- [X] T009 [P] Implement diagnostics repository baseline in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DeveloperDiagnosticsRepositoryImpl.kt`
- [X] T010 Wire settings and diagnostics repositories in `app/src/main/java/com/ndi/app/di/AppGraph.kt`
- [X] T011 Add settings dependency locator in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDependencies.kt`
- [X] T012 Add settings and diagnostics telemetry events in `core/model/src/main/java/com/ndi/core/model/TelemetryEvent.kt`
- [X] T013 Add timing assertion helper utilities for test suites in `testing/e2e/tests/support/timingAssertions.ts`

**Checkpoint**: Foundation complete. US1, US2, and US3 can now proceed.

---

## Phase 3: User Story 1 - Access and Navigate the Settings Menu (Priority: P1) 🎯 MVP

**Goal**: Users can open Settings from each main screen and return safely.

**Independent Test**: From Source List, Viewer, and Output screens, open Settings in <=2 taps, verify required controls, and verify Back returns to the originating screen.

### Tests for User Story 1

   - [X] T014 [P] [US1] Add navigation route helper unit tests in `app/src/test/java/com/ndi/app/navigation/NdiNavigationSettingsTest.kt`
   - [X] T015 [P] [US1] Add Source List to Settings navigation instrumentation test in `feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/SourceListSettingsNavigationTest.kt`
   - [X] T016 [P] [US1] Add Viewer to Settings navigation instrumentation test in `feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/ViewerSettingsNavigationTest.kt`
   - [X] T017 [P] [US1] Add Output to Settings navigation instrumentation test in `feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/OutputSettingsNavigationTest.kt`

### Implementation for User Story 1

   - [X] T018 [US1] Add settings destination and actions in `app/src/main/res/navigation/main_nav_graph.xml`
   - [X] T019 [US1] Add settings route and request helpers in `app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt`
   - [X] T020 [US1] Implement settings screen UI controller in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`
   - [X] T021 [US1] Add Settings entry point to source list screen in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt`
   - [X] T022 [US1] Add Settings entry point to viewer screen in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`
   - [X] T023 [US1] Add Settings entry point to output screen in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt`
   - [X] T024 [US1] Wire settings navigation event handling in `app/src/main/java/com/ndi/app/MainActivity.kt`

**Checkpoint**: US1 is independently functional and demoable.

---

## Phase 4: User Story 2 - Configure NDI Discovery Server (Priority: P2)

**Goal**: Users can save valid discovery endpoint values, apply changes immediately, and receive fallback warnings when endpoint is unreachable.

**Independent Test**: Save valid hostname/IPv4/bracketed IPv6 endpoint values, verify persistence and immediate apply behavior, and verify fallback warning appears within timing targets when endpoint is unreachable.

### Tests for User Story 2

- [X] T025 [P] [US2] Add repository tests for discovery endpoint parsing, trimming, port validation, and default-port behavior in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiSettingsRepositoryImplTest.kt`
- [X] T026 [P] [US2] Add ViewModel tests for inline validation and immediate apply/interruption behavior in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsViewModelTest.kt`
- [X] T027 [P] [US2] Add instrumentation test for unreachable-endpoint fallback warning visibility in `feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/source_list/SourceListFallbackWarningTest.kt`
- [X] T028 [P] [US2] Add e2e test for active-stream discovery change with <=1s apply assertion in `testing/e2e/tests/settings-discovery-config.spec.ts`
- [X] T029 [P] [US2] Add e2e test for unreachable discovery fallback warning with <=3s assertion in `testing/e2e/tests/settings-discovery-fallback.spec.ts`

### Implementation for User Story 2

- [X] T030 [US2] Implement settings form state, validation, and save/apply flow in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt`
- [X] T031 [US2] Bind discovery input validation and save actions in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`
- [X] T032 [US2] Implement endpoint parse/normalize/persist behavior in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiSettingsRepositoryImpl.kt`
- [X] T033 [US2] Implement immediate discovery apply with active-session interruption support in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt`
- [X] T034 [US2] Surface fallback warning state and apply timestamps in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt`
- [X] T035 [US2] Render fallback warning banner in source list screen in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt`
- [X] T036 [US2] Emit telemetry for discovery save/apply/interruption/fallback timing in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsTelemetry.kt`

**Checkpoint**: US2 is independently functional and testable.

---

## Phase 5: User Story 3 - Developer Mode Overlay and Redacted Diagnostics (Priority: P3)

**Goal**: Developer Mode toggles a top-of-screen diagnostics overlay on all main screens with stream status, idle state, recent logs, and redaction.

**Independent Test**: Enable Developer Mode, verify overlay appears within 1 second on Source List/Viewer/Output, verify stream status updates within 3 seconds, verify idle state and redacted logs, disable mode and verify overlay removal within 1 second.

### Tests for User Story 3

- [ ] T037 [P] [US3] Add overlay state transition unit tests for disabled/idle/active modes in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayStateMapperTest.kt`
- [ ] T038 [P] [US3] Add sensitive-value redaction unit tests in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/OverlayLogRedactorTest.kt`
- [ ] T039 [P] [US3] Add instrumentation test for overlay visibility timing (<=1s on/off) across Source/Viewer/Output in `feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayTimingTest.kt`
- [ ] T040 [P] [US3] Add instrumentation test for stream-status propagation timing (<=3s) in `feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayStreamStatusTimingTest.kt`
- [ ] T041 [P] [US3] Add e2e overlay diagnostics test for idle state and redacted logs in `testing/e2e/tests/settings-developer-overlay.spec.ts`

### Implementation for User Story 3

- [ ] T042 [US3] Implement overlay state models and mapper in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayState.kt`
- [ ] T043 [US3] Implement overlay log redaction utility in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/OverlayLogRedactor.kt`
- [ ] T044 [US3] Implement reusable overlay layout content in `feature/ndi-browser/presentation/src/main/res/layout/include_developer_overlay.xml`
- [ ] T045 [US3] Integrate overlay rendering in source list screen in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt`
- [ ] T046 [US3] Integrate overlay rendering in viewer screen in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`
- [ ] T047 [US3] Integrate overlay rendering in output screen in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt`
- [ ] T048 [US3] Connect developer-mode state and diagnostics streams in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDependencies.kt`
- [ ] T049 [US3] Emit telemetry for overlay timing and redaction events in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsTelemetry.kt`

**Checkpoint**: US3 is independently functional and testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final hardening, documentation, and validation evidence.

- [ ] T050 [P] Update feature documentation and operator notes in `docs/ndi-feature.md`
- [ ] T051 [P] Update quickstart with explicit timing-verification commands in `specs/006-settings-menu/quickstart.md`
- [ ] T052 Run unit test suite and record results in `test-results/android-test-results.md`
- [ ] T053 Run instrumentation and e2e timing validations and record evidence in `test-results/android-test-results.md`
- [ ] T054 Run release hardening checks and capture results in `specs/006-settings-menu/checklists/requirements.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies, start immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1 completion; blocks all user story work.
- **Phase 3 (US1)**: Depends on Phase 2 completion.
- **Phase 4 (US2)**: Depends on Phase 2 completion; can proceed in parallel with US1 if staffed.
- **Phase 5 (US3)**: Depends on Phase 2 completion; can proceed in parallel with US1/US2 if staffed.
- **Phase 6 (Polish)**: Depends on completion of selected user stories.

### User Story Dependencies

- **US1 (P1)**: Independent after foundational work.
- **US2 (P2)**: Independent after foundational work.
- **US3 (P3)**: Independent after foundational work and consumes persisted settings state from US2.

### Within Each User Story

- Tests must be written first and fail before implementation.
- Model/repository updates precede ViewModel and UI wiring.
- Timing verification tests run before story sign-off.

### Parallel Opportunities

- Setup tasks marked `[P]` (`T002`, `T003`, `T004`) can run concurrently.
- Foundational tasks marked `[P]` (`T008`, `T009`) can run concurrently after `T005`-`T007`.
- US1 tests (`T014`, `T015`, `T016`, `T017`) can run in parallel.
- US2 tests (`T025`, `T026`, `T027`, `T028`, `T029`) can run in parallel.
- US3 tests (`T037`, `T038`, `T039`, `T040`, `T041`) can run in parallel.
- US3 screen integrations (`T045`, `T046`, `T047`) can run in parallel after shared overlay artifacts are ready.

---

## Parallel Example: User Story 1

```bash
# Run US1 main-screen entry-point tests in parallel:
Task: "T015 [US1] Source List to Settings navigation instrumentation test"
Task: "T016 [US1] Viewer to Settings navigation instrumentation test"
Task: "T017 [US1] Output to Settings navigation instrumentation test"
```

## Parallel Example: User Story 3

```bash
# Run US3 timing verification tests in parallel:
Task: "T039 [US3] overlay visibility timing test (<=1s)"
Task: "T040 [US3] stream-status propagation timing test (<=3s)"
Task: "T041 [US3] e2e idle state and redaction diagnostics test"
```

---

## Implementation Strategy

### MVP First (US1)

1. Complete Phase 1 and Phase 2.
2. Deliver Phase 3 (US1) with entry points on Source List, Viewer, and Output.
3. Validate US1 independently before expanding scope.

### Incremental Delivery

1. Foundation complete (Phases 1-2).
2. Ship US1 (multi-main-screen settings access).
3. Ship US2 (discovery configuration and immediate apply behavior with timing assertions).
4. Ship US3 (developer diagnostics overlay, redaction, and timing assertions).
5. Finish with Phase 6 cross-cutting validation and documentation.

### Parallel Team Strategy

1. Team aligns on foundational contracts and persistence wiring.
2. After Phase 2:
   - Developer A leads US1 navigation and entry-point coverage.
   - Developer B leads US2 discovery flow and fallback behavior.
   - Developer C leads US3 overlay diagnostics and timing behavior.
3. Merge and stabilize through Phase 6 validation.
