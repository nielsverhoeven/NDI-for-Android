# Tasks: Persistent Source Cache

**Input**: Design documents from `/specs/030-persist-source-cache/`
**Prerequisites**: `plan.md` (required), `spec.md` (required), `research.md`, `data-model.md`, `contracts/persistent-source-cache-contract.md`, `quickstart.md`

**Tests**: Tests are REQUIRED by constitution. This task list includes failing-test-first JUnit coverage for each story, Playwright e2e coverage for visual changes, and regression/preflight evidence tasks.

**Organization**: Tasks are grouped by user story so each story is independently implementable and testable.

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Verify environment-dependent validation prerequisites before implementation and e2e gates.

- [x] T001 Run prerequisite validation and record output in test-results/030-preflight-android-prereqs.md using scripts/verify-android-prereqs.ps1
- [x] T002 Run dual-emulator preflight (when required) and record output in test-results/030-preflight-dual-emulator.md using scripts/verify-e2e-dual-emulator-prereqs.ps1
- [x] T003 Capture active device list and discovery reachability evidence in test-results/030-preflight-runtime.md via adb devices and configured server checks

**Checkpoint**: Runtime blockers are either resolved or documented with unblock steps.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create implementation scaffolding and feature-level result placeholders.

- [x] T004 Create feature result placeholders for US1/US2/US3 and final summary in test-results/030-us1-cache-validation.md, test-results/030-us2-endpoint-handoff.md, test-results/030-us3-developer-inspection.md, and test-results/030-final-validation-summary.md
- [x] T005 [P] Add feature-030 traceability section to docs/testing.md for planned commands and evidence links
- [x] T006 [P] Validate feature-030 command and artifact names in specs/030-persist-source-cache/quickstart.md against implemented test paths and update mismatches with explicit changelog notes

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement shared persistence/domain foundations that all user stories depend on.

**⚠️ CRITICAL**: No user story work starts before this phase completes.

- [x] T007 Add cached source entities, cross-ref table, DAO interfaces, and database version/migration updates in core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt
- [x] T008 [P] Add cached-source domain models and validation-state enum in core/model/src/main/java/com/ndi/core/model/NdiModels.kt
- [x] T009 Extend repository contracts for cached source persistence/query and endpoint resolution in feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
- [x] T010 Implement Room mapping utilities for cached-source entity/domain conversion in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/mapper/CachedSourceMapper.kt
- [x] T011 Wire new repository dependencies in app/src/main/java/com/ndi/app/di/AppGraph.kt
- [ ] T012 Add migration regression test coverage for existing settings/discovery data preservation in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiSettingsRepositoryImplTest.kt

**Checkpoint**: Cached-source persistence foundation and repository contracts are in place.

---

## Phase 3: User Story 1 - Cached Source Availability In View Menu (Priority: P1) 🎯 MVP

**Goal**: Show cached sources immediately in View/Home while validation runs and keep View actions disabled until availability is confirmed.

**Independent Test**: Discover source -> relaunch app or install an update preserving app data -> open Home/View -> cached row and preview show before validation completes -> View action remains disabled during validation and only enables when available.

### Tests for User Story 1 (REQUIRED)

- [x] T013 [P] [US1] Add failing repository contract tests for cached-source merge and validation-state transitions in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiDiscoveryRepositoryContractTest.kt
- [x] T014 [P] [US1] Add failing Home dashboard gating tests for canNavigateToView during VALIDATING/UNAVAILABLE states in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/HomeDashboardRepositoryImplTest.kt
- [x] T015 [P] [US1] Add failing Source List presentation tests for cached row rendering and disabled View action in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelAvailabilityTest.kt
- [x] T016 [US1] Add failing Home ViewModel behavior tests for quick-action disable path in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/home/HomeViewModelTest.kt
- [ ] T017 [US1] Add Playwright emulator scenario for cached-source pre-validation visibility and disabled action in testing/e2e/tests/030-cached-source-validation.spec.ts

### Implementation for User Story 1

- [x] T018 [US1] Implement cached-source read/emit/validation merge flow in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt
- [ ] T019 [US1] Update source availability debounce integration with persisted validation state in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/AvailabilityDebounceTracker.kt
- [x] T020 [US1] Implement Home snapshot gating from cached validation state in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/HomeDashboardRepositoryImpl.kt
- [ ] T021 [US1] Update Source List state modeling for persisted preview + validation state in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
- [ ] T022 [US1] Render disabled/validating visual states in feature/ndi-browser/presentation/src/main/res/layout/fragment_source_list.xml and feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
- [ ] T023 [US1] Update Home quick-action rendering and disabled state behavior in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/home/HomeScreen.kt
- [ ] T024 [US1] Execute US1 tests and record evidence in test-results/030-us1-cache-validation.md
- [ ] T024a [US1] Execute update-path persistence validation (install/update while preserving app data) and record evidence in test-results/030-us1-cache-validation.md
- [ ] T025 [US1] Run existing Playwright regression suite and append results to test-results/030-us1-cache-validation.md

**Checkpoint**: US1 is independently functional and validated.

---

## Phase 4: User Story 2 - Discovery-Server-Sourced Stream Endpoints (Priority: P2)

**Goal**: Treat discovery servers as metadata registries only, persist discovered source endpoints, and always start stream/view using stored source endpoint.

**Independent Test**: Enable discovery server(s) -> discover source -> verify persisted endpoint host/port -> start stream/view -> confirm endpoint used is source endpoint, not server endpoint.

### Tests for User Story 2 (REQUIRED)

- [ ] T026 [P] [US2] Add failing repository tests for stable-id/endpoint deduplication and cross-server merge in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiDiscoveryRepositoryContractTest.kt
- [ ] T027 [P] [US2] Add failing endpoint-resolution tests for viewer startup path in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiViewerRepositoryContractTest.kt
- [ ] T028 [P] [US2] Add failing discovery-server management integration tests for source association updates in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImplTest.kt
- [ ] T029 [US2] Add Playwright emulator scenario for discovery-server endpoint handoff in testing/e2e/tests/030-discovery-endpoint-handoff.spec.ts

### Implementation for User Story 2

- [ ] T030 [US2] Persist announced source endpoints and server associations during discovery refresh in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt
- [x] T031 [US2] Implement canonical identity resolution helper (stable source ID precedence over endpoint) in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/CachedSourceIdentityResolver.kt
- [ ] T032 [US2] Update viewer stream startup to resolve endpoint from persisted cached source records in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiViewerRepositoryImpl.kt
- [ ] T033 [US2] Ensure output flow uses persisted source endpoint resolution path in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt
- [ ] T034 [US2] Add telemetry/events clarifying source-endpoint vs discovery-server usage in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerTelemetry.kt
- [ ] T035 [US2] Execute US2 tests and record evidence in test-results/030-us2-endpoint-handoff.md
- [ ] T036 [US2] Run existing Playwright regression suite and append results to test-results/030-us2-endpoint-handoff.md

**Checkpoint**: US2 is independently functional and validated.

---

## Phase 5: User Story 3 - Developer Database Inspection (Priority: P3)

**Goal**: Provide developer-mode-only read-only database inspection for cached sources and discovery associations.

**Independent Test**: Developer mode OFF hides inspection option; developer mode ON shows inspection; displayed records reflect latest persisted state after discovery/validation updates.

### Tests for User Story 3 (REQUIRED)

- [ ] T037 [P] [US3] Add failing diagnostics repository tests for cached-source inspection payload in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DeveloperDiagnosticsRepositoryImplTest.kt
- [ ] T038 [P] [US3] Add failing Settings/DiscoveryServer ViewModel tests for developer-mode visibility gating in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModelTest.kt
- [ ] T039 [P] [US3] Add failing settings screen tests for inspection option visibility in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsScreenTest.kt
- [ ] T040 [US3] Add Playwright emulator scenario for developer inspection visibility and content refresh in testing/e2e/tests/030-developer-db-inspection.spec.ts

### Implementation for User Story 3

- [ ] T041 [US3] Extend diagnostics repository to publish cached-source inspection data in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DeveloperDiagnosticsRepositoryImpl.kt
- [ ] T042 [US3] Extend developer diagnostics model with inspection dataset fields in core/model/src/main/java/com/ndi/core/model/DeveloperDiscoveryDiagnostics.kt
- [ ] T043 [US3] Add developer inspection section state and actions in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModel.kt
- [ ] T044 [US3] Render developer inspection UI in feature/ndi-browser/presentation/src/main/res/layout/fragment_discovery_server_settings.xml and feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsFragment.kt
- [ ] T045 [US3] Ensure Settings category/navigation exposure for inspection option is developer-mode-gated in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt
- [ ] T046 [US3] Execute US3 tests and record evidence in test-results/030-us3-developer-inspection.md
- [ ] T047 [US3] Run existing Playwright regression suite and append results to test-results/030-us3-developer-inspection.md

**Checkpoint**: US3 is independently functional and validated.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Finalize regression, compliance, documentation, and release-quality evidence.

- [ ] T048 [P] Add test-change traceability notes (for any modified pre-existing tests) in test-results/030-test-change-traceability.md
- [ ] T049 Run module unit regression and append results to test-results/030-unit-regression.md using ./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest
- [ ] T050 Run release hardening verification and record output in test-results/030-release-hardening.md using ./gradlew.bat :app:verifyReleaseHardening
- [ ] T051 [P] Capture Material 3 and visual-compliance verification notes in test-results/030-material3-compliance.md
- [ ] T052 Compile final validation summary with pass/fail/blocked classification in test-results/030-final-validation-summary.md

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 0 (Environment Preflight): Starts immediately and blocks final quality-gate execution if unresolved.
- Phase 1 (Setup): Depends on Phase 0 evidence capture start.
- Phase 2 (Foundational): Depends on Phase 1 completion and blocks all user stories.
- Phase 3 (US1): Depends on Phase 2 completion.
- Phase 4 (US2): Depends on Phase 2 completion; may run after US1 starts, but endpoint-handoff implementation consumes foundational cache models.
- Phase 5 (US3): Depends on Phase 2 completion; can run in parallel with late US2 work.
- Phase 6 (Polish): Depends on completion of selected user stories and their test evidence tasks.

### User Story Dependencies

- US1 (P1): No dependency on other user stories after foundational work.
- US2 (P2): No strict dependency on US1, but shares discovery/cache primitives from Phase 2.
- US3 (P3): No strict dependency on US1/US2 behavior, but inspection payload quality improves once US1/US2 are implemented.

### Within Each User Story

- Write and run failing tests first.
- Implement repository/domain changes before UI wiring.
- Run targeted story tests and Playwright scenarios.
- Run existing Playwright regression and capture evidence.

---

## Parallel Opportunities

- Phase 1: T005 and T006 can run in parallel.
- Phase 2: T008 and T010 can run in parallel after T007 starts; T011 can proceed once contracts are stable.
- US1: T013, T014, T015 can run in parallel; UI tasks T022 and T023 can run in parallel after repository behavior stabilizes.
- US2: T026, T027, T028 can run in parallel; implementation T032 and T034 can run in parallel after T030/T031.
- US3: T037, T038, T039 can run in parallel; implementation T043 and T044 can run in parallel after T041/T042.
- Polish: T048 and T051 can run in parallel with T049/T050 execution.

---

## Parallel Example: User Story 1

```bash
# Parallel failing-test-first tasks
T013 [US1] NdiDiscoveryRepositoryContractTest updates
T014 [US1] HomeDashboardRepositoryImplTest updates
T015 [US1] SourceListViewModelAvailabilityTest updates

# Parallel UI wiring tasks after repository behavior is implemented
T022 [US1] Source list validating/disabled UI updates
T023 [US1] Home quick-action disable state updates
```

## Parallel Example: User Story 2

```bash
# Parallel test tasks
T026 [US2] Discovery dedup/merge tests
T027 [US2] Viewer endpoint-resolution tests
T028 [US2] Discovery server association tests

# Parallel implementation tasks after identity resolver exists
T032 [US2] Viewer endpoint startup update
T034 [US2] Viewer telemetry endpoint-source labeling
```

## Parallel Example: User Story 3

```bash
# Parallel failing tests
T037 [US3] Developer diagnostics repository tests
T038 [US3] DiscoveryServerSettingsViewModel visibility tests
T039 [US3] SettingsScreen visibility tests

# Parallel implementation after diagnostics model extension
T043 [US3] ViewModel inspection state/actions
T044 [US3] Inspection UI rendering
```

---

## Implementation Strategy

### MVP First (US1)

1. Complete Phase 0 through Phase 2.
2. Deliver Phase 3 (US1) end-to-end.
3. Validate US1 independently using T024 and T025 before expanding scope.

### Incremental Delivery

1. Build shared persistence/domain foundation once.
2. Deliver US1 (cached visibility + disable while validating).
3. Deliver US2 (correct endpoint handoff semantics).
4. Deliver US3 (developer inspection).
5. Complete Phase 6 cross-cutting gates.

### Parallel Team Strategy

1. One engineer handles Room/domain foundation (Phase 2).
2. One engineer focuses US1 UI/repository behavior after foundation.
3. Another engineer can start US2 endpoint handoff once T030/T031 are in progress.
4. A third engineer can implement US3 settings/diagnostics once T041/T042 are ready.

---

## Notes

- `[P]` means different files and no dependency on incomplete tasks.
- `[US1]`, `[US2]`, `[US3]` labels provide traceability to specification stories.
- Existing tests are regression protection first; any required edits must be justified in T048 with requirement references.
- Environment blockers must be explicitly documented in story evidence files and carried into T052.
