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

**Purpose**: Create feature scaffolding and shared resource placeholders.

- [ ] T001 Create settings presentation package skeleton in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`
- [ ] T002 [P] Create settings UI layout skeleton in `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings.xml`
- [ ] T003 [P] Add baseline strings for settings and developer diagnostics in `feature/ndi-browser/presentation/src/main/res/values/strings.xml`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core contracts and shared infrastructure required before user stories.

**⚠️ CRITICAL**: No user story implementation begins until this phase is complete.

- [ ] T004 Extend domain contracts for settings persistence and diagnostics in `feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt`
- [ ] T005 Add settings/diagnostic model types in `core/model/src/main/java/com/ndi/core/model/NdiSettingsModels.kt`
- [ ] T006 Implement settings persistence entities, DAO methods, and migration update in `core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt`
- [ ] T007 [P] Implement settings repository data layer in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiSettingsRepositoryImpl.kt`
- [ ] T008 [P] Implement developer diagnostics repository data layer in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DeveloperDiagnosticsRepositoryImpl.kt`
- [ ] T009 Wire new repositories and dependency providers in `app/src/main/java/com/ndi/app/di/AppGraph.kt`
- [ ] T010 Add settings/developer dependency locator object in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDependencies.kt`
- [ ] T011 Add telemetry event constants for settings and overlay lifecycle in `core/model/src/main/java/com/ndi/core/model/TelemetryEvent.kt`

**Checkpoint**: Foundation complete. US1, US2, and US3 can now proceed.

---

## Phase 3: User Story 1 - Access and Navigate Settings Menu (Priority: P1) 🎯 MVP

**Goal**: Users can open Settings from main flow, see required controls, and return safely.

**Independent Test**: From a main screen, open Settings, verify discovery input + developer mode toggle are visible, then back navigation returns to previous screen.

### Tests for User Story 1

- [ ] T012 [P] [US1] Add nav helper unit tests for settings route IDs/requests in `app/src/test/java/com/ndi/app/navigation/NdiNavigationSettingsTest.kt`
- [ ] T013 [P] [US1] Add fragment UI test for settings screen rendering and back behavior in `feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/SettingsNavigationTest.kt`

### Implementation for User Story 1

- [ ] T014 [US1] Add settings destination and actions in `app/src/main/res/navigation/main_nav_graph.xml`
- [ ] T015 [US1] Add settings navigation request/ID helpers in `app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt`
- [ ] T016 [US1] Implement settings screen UI controller in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`
- [ ] T017 [US1] Add Settings entry point controls on Home dashboard in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/home/HomeScreen.kt`
- [ ] T018 [US1] Add Settings entry point view element in `feature/ndi-browser/presentation/src/main/res/layout/fragment_home_dashboard.xml`
- [ ] T019 [US1] Handle Settings navigation event dispatch in `app/src/main/java/com/ndi/app/MainActivity.kt`

**Checkpoint**: US1 is independently functional and demoable.

---

## Phase 4: User Story 2 - Configure NDI Discovery Server (Priority: P2)

**Goal**: Users can save valid discovery endpoint values, apply changes immediately, and get fallback warnings when endpoint is unreachable.

**Independent Test**: Save `hostname/IP` or `hostname/IP:port`, verify persistence across restart, confirm immediate apply, and verify fallback warning when endpoint is unreachable.

### Tests for User Story 2

- [ ] T020 [P] [US2] Add repository tests for discovery endpoint parsing/default-port/fallback in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiSettingsRepositoryImplTest.kt`
- [ ] T021 [P] [US2] Add viewmodel tests for validation and immediate-apply interruption behavior in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsViewModelTest.kt`
- [ ] T022 [P] [US2] Add e2e scenario for discovery-setting change during active stream in `testing/e2e/tests/settings-discovery-config.spec.ts`

### Implementation for User Story 2

- [ ] T023 [US2] Implement settings form state, validation, and save/apply pipeline in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt`
- [ ] T024 [US2] Bind discovery input validation and save actions in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`
- [ ] T025 [US2] Apply discovery endpoint updates immediately with active-session interruption support in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt`
- [ ] T026 [US2] Persist discovery settings and developer mode snapshot mapping in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiSettingsRepositoryImpl.kt`
- [ ] T027 [US2] Surface discovery fallback warning state in source list UI flow in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt`
- [ ] T028 [US2] Render fallback warning banner/message in source list screen in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt`
- [ ] T029 [US2] Emit telemetry for settings save/apply/fallback events in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsTelemetry.kt`

**Checkpoint**: US2 is independently functional and testable.

---

## Phase 5: User Story 3 - Developer Mode Overlay and Redacted Diagnostics (Priority: P3)

**Goal**: Developer Mode toggles a top-of-screen diagnostics overlay on main screens with stream status, idle state, recent logs, and redaction.

**Independent Test**: Enable Developer Mode, verify overlay appears on Source List/Viewer/Output, verify idle state and log feed, verify sensitive values are redacted, disable mode and verify overlay is removed.

### Tests for User Story 3

- [ ] T030 [P] [US3] Add overlay state transition unit tests (disabled/idle/active) in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayStateMapperTest.kt`
- [ ] T031 [P] [US3] Add sensitive-value redaction unit tests in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/OverlayLogRedactorTest.kt`
- [ ] T032 [P] [US3] Add instrumentation test for overlay visibility on Source/Viewer/Output screens in `feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayUiTest.kt`

### Implementation for User Story 3

- [ ] T033 [US3] Implement overlay state models and mapper in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayState.kt`
- [ ] T034 [US3] Implement log redaction utility for overlay rendering in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/OverlayLogRedactor.kt`
- [ ] T035 [US3] Create reusable overlay include layout in `feature/ndi-browser/presentation/src/main/res/layout/include_developer_overlay.xml`
- [ ] T036 [US3] Integrate overlay rendering into source list screen in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt`
- [ ] T037 [US3] Integrate overlay rendering into viewer screen in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`
- [ ] T038 [US3] Integrate overlay rendering into output screen in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt`
- [ ] T039 [US3] Connect developer-mode state and diagnostics stream to all screen viewmodels in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDependencies.kt`
- [ ] T040 [US3] Emit telemetry for overlay visibility and redaction events in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsTelemetry.kt`

**Checkpoint**: US3 is independently functional and testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final hardening, docs, and validation evidence across stories.

- [ ] T041 [P] Update feature walkthrough and operator notes in `docs/ndi-feature.md`
- [ ] T042 [P] Update feature quickstart verification steps in `specs/006-settings-menu/quickstart.md`
- [ ] T043 Run full validation matrix and record evidence in `test-results/android-test-results.md`
- [ ] T044 Run release hardening checks and capture results in `specs/006-settings-menu/checklists/requirements.md`

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
- **US2 (P2)**: Independent after foundational work; may reuse US1 settings navigation shell.
- **US3 (P3)**: Independent after foundational work; may consume settings persistence from US2.

### Within Each User Story

- Tests must be written first and fail before implementation.
- Data/model and repository updates precede UI wiring.
- ViewModel/state integration precedes final rendering and telemetry polish.

### Parallel Opportunities

- Setup tasks marked `[P]` (`T002`, `T003`) can run concurrently.
- Foundational tasks marked `[P]` (`T007`, `T008`) can run concurrently after contracts are defined.
- US1 tests (`T012`, `T013`) can run in parallel.
- US2 tests (`T020`, `T021`, `T022`) can run in parallel.
- US3 tests (`T030`, `T031`, `T032`) can run in parallel.
- US3 screen integrations (`T036`, `T037`, `T038`) can run in parallel.

---

## Parallel Example: User Story 2

```bash
# Run US2 test authoring in parallel:
Task: "T020 [US2] repository tests in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiSettingsRepositoryImplTest.kt"
Task: "T021 [US2] viewmodel tests in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsViewModelTest.kt"
Task: "T022 [US2] e2e scenario in testing/e2e/tests/settings-discovery-config.spec.ts"
```

## Parallel Example: User Story 3

```bash
# Implement overlay integration on three screens in parallel after shared overlay artifacts exist:
Task: "T036 [US3] source list integration in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt"
Task: "T037 [US3] viewer integration in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt"
Task: "T038 [US3] output integration in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt"
```

---

## Implementation Strategy

### MVP First (US1)

1. Complete Phase 1 and Phase 2.
2. Deliver Phase 3 (US1) settings navigation and base settings UI.
3. Validate US1 independently before expanding scope.

### Incremental Delivery

1. Foundation complete (Phases 1-2).
2. Ship US1 (navigation/access).
3. Ship US2 (discovery configuration and immediate apply behavior).
4. Ship US3 (developer diagnostics overlay and redaction).
5. Finish with Phase 6 cross-cutting validation/docs.

### Parallel Team Strategy

1. Team aligns on foundational contracts/database wiring.
2. After Phase 2:
   - Developer A leads US1 navigation/UI shell.
   - Developer B leads US2 discovery settings + repository flow.
   - Developer C leads US3 overlay diagnostics + redaction.
3. Merge and stabilize through Phase 6 validation.
