# Tasks: Discovery Server Settings Management

**Input**: Design documents from `/specs/018-manage-discovery-servers/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are REQUIRED by constitution. Every user story includes failing-test-first unit/instrumentation/Playwright tasks plus Playwright regression and blocked-gate evidence tasks.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Confirm runtime and tool prerequisites before implementation and emulator validation.

- [x] T001 Run prerequisite gate and capture output in test-results/018-manage-discovery-servers-preflight.md using scripts/verify-android-prereqs.ps1
- [x] T002 Run dual-emulator preflight and capture output in test-results/018-manage-discovery-servers-preflight.md using scripts/verify-e2e-dual-emulator-prereqs.ps1 -AllowMissingNdiSdk
- [x] T003 Verify debug artifact build succeeds and record output in test-results/018-manage-discovery-servers-preflight.md using ./gradlew.bat :app:assembleDebug
- [x] T004 Record any environment blocker and concrete unblock command in test-results/018-manage-discovery-servers-preflight.md

**Checkpoint**: Preflight status is PASS or explicitly documented as blocked with unblocking steps.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare shared validation artifacts and feature-specific test scaffolding.

- [x] T005 Create validation report stub in test-results/018-manage-discovery-servers-validation.md
- [x] T006 Create feature Playwright spec shell in testing/e2e/tests/settings-discovery-submenu.spec.ts
- [x] T007 [P] Create feature Playwright helper scaffold in testing/e2e/tests/support/discovery-server-helpers.ts
- [x] T008 [P] Add quickstart task trace section for commands and evidence paths in specs/018-manage-discovery-servers/quickstart.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement shared contracts, persistence, and navigation infrastructure before any user story work.

**⚠️ CRITICAL**: No user story implementation begins until this phase is complete.

- [x] T009 Extend discovery-server repository contracts in feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
- [x] T009 Extend discovery-server repository contracts in feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
- [x] T010 [P] Add discovery-server collection model types in core/model/src/main/java/com/ndi/core/model/NdiSettingsModels.kt
- [x] T011 Add discovery-server entities, DAO methods, and migration in core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt
- [x] T012 [P] Implement ordered discovery-server persistence baseline in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImpl.kt
- [x] T013 [P] Preserve legacy settings compatibility and migration mapping in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiSettingsRepositoryImpl.kt
- [x] T014 [P] Wire discovery-server repository providers in app/src/main/java/com/ndi/app/di/AppGraph.kt and feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDependencies.kt
- [x] T015 Add discovery-server submenu destination and deep-link helpers in app/src/main/res/navigation/main_nav_graph.xml and app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt

**Checkpoint**: Foundation is complete and user-story work can start.

---

## Phase 3: User Story 1 - Add Discovery Servers from Settings (Priority: P1) 🎯 MVP

**Goal**: Users can open a dedicated discovery-server submenu from Settings and add a valid server using separate hostname-or-ip and port inputs.

**Independent Test**: Open Settings, enter the Discovery Servers submenu, save a hostname-only server, and verify it appears as hostname:5959.

### Tests for User Story 1 (REQUIRED)

- [x] T016 [P] [US1] Add failing ViewModel tests for required-host validation and blank-port defaulting in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModelTest.kt
- [x] T017 [P] [US1] Add failing repository tests for add-server normalization and default-port persistence in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImplTest.kt
- [x] T018 [P] [US1] Add failing instrumentation test for opening the submenu and saving a host-only server in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsNavigationTest.kt
- [x] T019 [P] [US1] Add failing Playwright e2e for settings submenu open and host-only save defaulting to 5959 in testing/e2e/tests/settings-discovery-submenu.spec.ts
- [x] T020 [US1] Run existing Playwright regression suite and append results to test-results/018-manage-discovery-servers-validation.md using npm --prefix testing/e2e run test:pr:primary
- [x] T021 [US1] If blocked, record reproduction details and unblock command in test-results/018-manage-discovery-servers-validation.md

### Implementation for User Story 1

- [x] T022 [P] [US1] Add discovery-server submenu strings and button labels in feature/ndi-browser/presentation/src/main/res/values/strings.xml
- [x] T023 [P] [US1] Create discovery-server submenu layout and add-form UI in feature/ndi-browser/presentation/src/main/res/layout/fragment_discovery_server_settings.xml
- [x] T024 [US1] Add Settings entry-point affordance for the submenu in feature/ndi-browser/presentation/src/main/res/layout/fragment_settings.xml and feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt
- [x] T025 [US1] Implement discovery-server submenu screen controller in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsFragment.kt
- [x] T026 [US1] Implement add-server form state, validation, and save flow in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModel.kt
- [x] T027 [US1] Implement add-server persistence and legacy-endpoint import behavior in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImpl.kt and feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiSettingsRepositoryImpl.kt

**Checkpoint**: User Story 1 is independently functional and demoable.

---

## Phase 4: User Story 2 - Manage Multiple Discovery Servers (Priority: P2)

**Goal**: Users can maintain multiple distinct discovery servers, see them in an ordered list, and keep them persisted across restart.

**Independent Test**: Add three distinct servers, relaunch the app, and verify the same ordered list is still visible.

### Tests for User Story 2 (REQUIRED)

- [x] T028 [P] [US2] Add failing repository tests for duplicate rejection and ordered multi-entry persistence in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImplTest.kt
- [x] T029 [P] [US2] Add failing database migration and DAO round-trip regression tests in core/database/src/test/java/com/ndi/core/database/DiscoveryServerDaoMigrationTest.kt
- [x] T030 [P] [US2] Add failing instrumentation test for multi-server persistence across restart in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsPersistenceTest.kt
- [x] T031 [P] [US2] Add failing Playwright e2e for adding multiple servers and verifying persistence after relaunch in testing/e2e/tests/settings-discovery-submenu.spec.ts
- [x] T032 [US2] Run existing Playwright regression suite and append results to test-results/018-manage-discovery-servers-validation.md using npm --prefix testing/e2e run test:pr:primary
- [x] T033 [US2] If blocked, record reproduction details and unblock command in test-results/018-manage-discovery-servers-validation.md

### Implementation for User Story 2

- [x] T034 [P] [US2] Create discovery-server row layout in feature/ndi-browser/presentation/src/main/res/layout/item_discovery_server.xml
- [x] T035 [US2] Render ordered server rows, duplicate validation feedback, and empty-state handling in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsFragment.kt
- [x] T036 [US2] Implement ordered collection queries, insert or update behavior, delete support, and duplicate protection in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImpl.kt and core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt
- [x] T037 [US2] Expose ordered collection UI state and duplicate-error mapping in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModel.kt

**Checkpoint**: User Story 2 is independently functional and testable.

---

## Phase 4a: User Story 4 - Edit and Remove Discovery Servers (Priority: P2)

**Goal**: Users can edit an existing server's hostname or port, remove a server entirely, and drag servers to change failover priority. All changes persist across restart.

**Independent Test**: Edit a server's port, remove a different server, reorder a third, relaunch, and verify all mutations persisted correctly.

### Tests for User Story 4 (REQUIRED)

- [x] T055 [P] [US4] Add failing ViewModel tests for edit-server validation (including duplicate-update check) and delete-server state transition in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModelTest.kt
- [x] T056 [P] [US4] Add failing repository tests for updateServer, removeServer, and reorderServers persistence in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImplTest.kt
- [x] T057 [P] [US4] Add failing instrumentation test for edit, delete, and drag-reorder interactions and persistence in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsEditDeleteTest.kt
- [x] T058 [P] [US4] Add failing Playwright e2e for edit, delete, and reorder flows with persistence verification in testing/e2e/tests/settings-discovery-submenu.spec.ts
- [x] T059 [US4] Run existing Playwright regression suite and append results to test-results/018-manage-discovery-servers-validation.md using npm --prefix testing/e2e run test:pr:primary
- [x] T060 [US4] If blocked, record reproduction details and unblock command in test-results/018-manage-discovery-servers-validation.md

### Implementation for User Story 4

- [x] T061 [P] [US4] Add edit-form controls, delete affordance, and drag handles to discovery-server row layout and submenu screen in feature/ndi-browser/presentation/src/main/res/layout/item_discovery_server.xml and fragment_discovery_server_settings.xml
- [x] T062 [P] [US4] Add edit/delete/reorder string resources in feature/ndi-browser/presentation/src/main/res/values/strings.xml
- [x] T063 [US4] Implement updateServer, removeServer, and reorderServers in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImpl.kt and DAO methods in core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt
- [x] T064 [US4] Implement edit-form state machine, delete flow, drag-drop ordering, and duplicate-update rejection in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModel.kt
- [x] T065 [US4] Wire edit affordance, delete confirmation, and drag-drop gesture handler in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsFragment.kt

**Checkpoint**: User Story 4 is independently functional and testable; edit, delete, and reorder all persist after restart.

---

## Phase 5: User Story 3 - Enable and Disable Individual Servers (Priority: P3)

**Goal**: Users can toggle individual saved servers on or off, preserve toggle state across restart, and drive ordered failover only across enabled entries.

**Independent Test**: Disable one saved server, restart the app, verify the toggle state persists, and confirm runtime selection skips disabled entries and fails over across enabled ones in order.

### Tests for User Story 3 (REQUIRED)

- [x] T038 [P] [US3] Add failing ViewModel tests for per-server toggle state and no-enabled-server messaging in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModelTest.kt
- [x] T039 [P] [US3] Add failing runtime selection and ordered failover tests in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryConfigRepositoryImplTest.kt
- [x] T040 [P] [US3] Add failing instrumentation test for per-server toggle persistence after restart in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsTogglePersistenceTest.kt
- [x] T041 [P] [US3] Add failing Playwright e2e for per-server toggles and persisted enabled-state behavior in testing/e2e/tests/settings-discovery-submenu.spec.ts
- [x] T042 [US3] Run existing Playwright regression suite and append results to test-results/018-manage-discovery-servers-validation.md using npm --prefix testing/e2e run test:pr:primary
- [x] T043 [US3] If blocked, record reproduction details and unblock command in test-results/018-manage-discovery-servers-validation.md

### Implementation for User Story 3

- [x] T044 [P] [US3] Add enabled-toggle controls and status rendering in feature/ndi-browser/presentation/src/main/res/layout/item_discovery_server.xml and feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsFragment.kt
- [x] T045 [US3] Implement toggle persistence and ordered enabled-entry selection in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImpl.kt and feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryConfigRepositoryImpl.kt
- [x] T046 [US3] Apply sequential failover and all-enabled-unreachable result handling in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt and ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt
- [x] T047 [US3] Surface enabled-state actions, no-enabled-server feedback, and all-unreachable messaging in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModel.kt and feature/ndi-browser/presentation/src/main/res/values/strings.xml

**Checkpoint**: User Story 3 is independently functional and testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final hardening, documentation, and quality gates across all stories.

- [x] T048 [P] Update discovery-server operator guidance in docs/ndi-feature.md and docs/006-settings-menu-release-notes.md
- [x] T049 [P] Add final manual validation narrative and gate outcomes in test-results/018-manage-discovery-servers-validation.md
- [x] T050 Run unit test suites for touched modules and capture outputs in test-results/018-manage-discovery-servers-validation.md using ./gradlew.bat :core:database:testDebugUnitTest :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest :app:testDebugUnitTest
- [x] T051 Run feature Playwright coverage and capture artifacts in test-results/018-manage-discovery-servers-validation.md using npm --prefix testing/e2e run test -- tests/settings-discovery-submenu.spec.ts
- [x] T052 Run release hardening validation and record results in test-results/018-manage-discovery-servers-validation.md using ./gradlew.bat :app:verifyReleaseHardening :app:assembleRelease
- [x] T053 Perform and record Material 3 compliance verification for settings and discovery-server submenu updates in test-results/018-manage-discovery-servers-validation.md
- [x] T054 If any final gate is blocked, classify blocker vs code failure and record explicit unblock steps in test-results/018-manage-discovery-servers-validation.md

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 0 must complete before implementation and validation work.
- Phase 1 depends on Phase 0.
- Phase 2 depends on Phase 1 and blocks all user stories.
- Phases 3-5 depend on Phase 2.
- Phase 6 depends on completion of all targeted user stories.

### User Story Dependencies

- User Story 1 (P1): Starts immediately after Phase 2 and delivers the MVP submenu plus add flow.
- User Story 2 (P2): Starts after Phase 2 and builds on the submenu baseline to manage multiple entries.
- User Story 4 (P2): Starts after US2 is complete; requires ordered collection persistence and row layout from US2.
- User Story 3 (P3): Starts after Phase 2 and depends on persisted collection semantics established in US2.

### Within Each User Story

- Tests MUST be written first and fail before implementation.
- Persistence and repository behavior before ViewModel and UI finalization.
- Playwright feature coverage before story completion.
- Existing Playwright regression run and blocked-gate evidence are mandatory per story.

### Parallel Opportunities

- Phase 1 tasks marked [P] can run concurrently.
- Phase 2 tasks T010, T012, T013, and T014 can run in parallel after T009 starts.
- In each story, [P] tests can run in parallel.
- In each story, [P] implementation tasks can run in parallel when they touch separate files.

---

## Parallel Example: User Story 1

```bash
# Run failing US1 tests in parallel
T016: feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModelTest.kt
T017: feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImplTest.kt
T018: feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsNavigationTest.kt
T019: testing/e2e/tests/settings-discovery-submenu.spec.ts

# Build submenu UI pieces in parallel
T022: feature/ndi-browser/presentation/src/main/res/values/strings.xml
T023: feature/ndi-browser/presentation/src/main/res/layout/fragment_discovery_server_settings.xml
```

## Parallel Example: User Story 3

```bash
# Run toggle and failover tests in parallel
T038: DiscoveryServerSettingsViewModelTest toggle coverage
T039: NdiDiscoveryConfigRepositoryImplTest ordered failover coverage
T040: DiscoveryServerSettingsTogglePersistenceTest restart persistence
T041: testing/e2e/tests/settings-discovery-submenu.spec.ts

# Split runtime implementation by layer
T045: feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImpl.kt
T046: feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt and ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 0, Phase 1, and Phase 2.
2. Deliver Phase 3 (User Story 1) fully.
3. Stop and validate the submenu add flow independently before expanding scope.

### Incremental Delivery

1. Finish foundation work.
2. Deliver User Story 1 for submenu entry and add flow.
3. Deliver User Story 2 for ordered multi-server persistence.
4. Deliver User Story 3 for per-server toggles and ordered failover semantics.
5. Finish with cross-cutting validation and release-hardening gates.

### Parallel Team Strategy

1. Team completes Phases 0-2 together.
2. After Phase 2:
   - Engineer A leads submenu UX and validation flow for US1.
   - Engineer B leads repository/database collection work for US2.
   - Engineer C leads toggle and failover behavior for US3.
3. Re-converge for Phase 6 validation and release gates.
