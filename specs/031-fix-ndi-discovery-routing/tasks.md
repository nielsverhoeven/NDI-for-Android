# Tasks: NDI Discovery Routing Reliability

**Input**: Design documents from `/specs/031-fix-ndi-discovery-routing/`
**Prerequisites**: `plan.md` (required), `spec.md` (required), `research.md`, `data-model.md`, `contracts/ndi-discovery-routing-contract.md`, `quickstart.md`

**Tests**: Tests are REQUIRED by constitution. This task list includes failing-test-first JUnit coverage for each story, Playwright e2e coverage for visual behavior changes, regression validation, and explicit environment-blocked evidence tasks.

**Organization**: Tasks are grouped by user story so each story is independently implementable and testable.

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Verify environment-dependent validation prerequisites before implementation and e2e gates.

- [ ] T001 Run Android prerequisite validation and record output in `test-results/031-preflight-android-prereqs.md` using `scripts/verify-android-prereqs.ps1`
- [ ] T002 Capture active device list plus multicast/discovery-server fixture readiness in `test-results/031-preflight-runtime.md` via `adb devices` and fixture reachability checks
- [ ] T003 Run dual-emulator preflight when required and record output in `test-results/031-preflight-dual-emulator.md` using `scripts/verify-e2e-dual-emulator-prereqs.ps1`
- [ ] T003a Run Node/npm/Playwright command-contract preflight and record output in `test-results/031-preflight-node-playwright.md` using `testing/e2e/scripts/validate-command-contract.ps1` and `npm --prefix testing/e2e exec playwright --version`

**Checkpoint**: Runtime blockers are either resolved or documented with unblock steps.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create feature-specific evidence placeholders and verify planned commands/artifacts.

- [ ] T004 Create feature-031 evidence placeholders in `test-results/031-us1-multicast-fallback.md`, `test-results/031-us2-discovery-server-routing.md`, `test-results/031-us3-cache-relaunch.md`, and `test-results/031-final-validation-summary.md`
- [ ] T005 [P] Verify command and artifact names in `specs/031-fix-ndi-discovery-routing/quickstart.md` against actual scripts/test paths and update any mismatches with brief changelog notes
- [ ] T006 [P] Add feature-031 validation references and command mapping notes to `docs/testing.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement shared discovery-routing, diagnostics, and cache-merge infrastructure that all user stories depend on.

**⚠️ CRITICAL**: No user story work starts before this phase completes.

- [ ] T007 Extend shared discovery routing and diagnostic models in `core/model/src/main/java/com/ndi/core/model/NdiModels.kt`, `core/model/src/main/java/com/ndi/core/model/DiscoveryCompatibilityModels.kt`, and `core/model/src/main/java/com/ndi/core/model/DeveloperDiscoveryDiagnostics.kt`
- [ ] T008 Update repository/domain contracts for per-run mode selection, timeout reporting, and canonical cache merge behavior in `feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt`
- [ ] T009 [P] Update persistence helpers and DAO-backed cache merge paths for endpoint/timestamp preservation in `core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt` and `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/CachedSourceRepositoryImpl.kt`
- [ ] T010 [P] Wire discovery diagnostics and cache-routing dependencies through `app/src/main/java/com/ndi/app/di/AppGraph.kt` and `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDependencies.kt`
- [ ] T011 Add migration/regression coverage for shared persistence preservation and new routing metadata in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiSettingsRepositoryImplTest.kt` and `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/CachedSourceRepositoryImplTest.kt`

**Checkpoint**: Shared routing, timeout, diagnostics, and cache foundations are in place.

---

## Phase 3: User Story 1 - Multicast Fallback Discovery (Priority: P1) 🎯 MVP

**⚠️ PREREQUISITE**: Phase 2 tasks T007–T011 must be complete before US1 implementation begins.

**Goal**: When no enabled discovery servers exist, discovery runs use multicast/mDNS and persist discovered sources without any discovery-server dependency.

**Independent Test**: Clear or disable all configured discovery servers, trigger discovery on a multicast-capable network, and verify sources appear and persist via the multicast path.

### Tests for User Story 1 (REQUIRED)

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [ ] T012 [P] [US1] Add failing repository contract tests for `enabledServerCount == 0` selecting multicast mode in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiDiscoveryRepositoryContractTest.kt`
- [ ] T013 [P] [US1] Add failing enabled-server filtering tests in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryConfigRepositoryImplTest.kt`
- [ ] T014 [P] [US1] Add failing presentation tests for multicast results surfacing without discovery-server-only gating in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt`
- [ ] T015 [US1] Add Playwright emulator scenario for multicast fallback discovery in `testing/e2e/tests/031-discovery-routing.spec.ts`
- [ ] T015a [US1] Execute newly added US1 tests in the expected red state and record failing evidence in `test-results/031-us1-red-state.md` before starting T016

### Implementation for User Story 1

- [ ] T016 [US1] Implement per-run multicast selection when no enabled servers exist in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt`
- [ ] T017 [US1] Ensure discovery endpoint configuration resets cleanly for multicast runs in `ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt`
- [ ] T018 [US1] Record run mode and selection reason for multicast runs in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DeveloperDiagnosticsLogBuffer.kt`
- [ ] T019 [US1] Preserve source-list refresh and foreground auto-refresh behavior while surfacing multicast results in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt`
- [ ] T020 [US1] Execute targeted US1 tests and record evidence in `test-results/031-us1-multicast-fallback.md`
- [ ] T021 [US1] Run existing Playwright regression coverage and append results to `test-results/031-us1-multicast-fallback.md`
- [ ] T022 [US1] If multicast fixtures are unavailable, record `BLOCKED (environment)` details and unblock steps in `test-results/031-us1-multicast-fallback.md`

**Checkpoint**: US1 is independently functional and validated.

---

## Phase 4: User Story 2 - Discovery Server Directed Lookup (Priority: P2)

**⚠️ PREREQUISITE**: Phase 2 tasks T007–T011 must be complete, and Phase 3 US1 implementation (T016–T022) must be validated before US2 implementation begins.

**Goal**: When one or more enabled discovery servers exist, discovery runs use discovery-server-only mode, enforce the 5-second timeout, emit per-server diagnostics, and route stream startup using discovered source endpoints rather than discovery-server endpoints.

**Independent Test**: Enable one or more discovery servers, trigger discovery, verify mDNS is not used for that run, confirm timeout/diagnostics behavior, and verify viewer/output startup uses persisted source endpoint host/port.

### Tests for User Story 2 (REQUIRED)

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [ ] T023 [P] [US2] Add failing timeout and no-same-run-fallback tests in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiDiscoveryRepositoryContractTest.kt`
- [ ] T024 [P] [US2] Add failing per-server diagnostics and blocker-classification tests in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DeveloperDiagnosticsRepositoryImplTest.kt` and `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DiscoveryCompatibilityMatrixRepositoryTest.kt`
- [ ] T025 [P] [US2] Add failing endpoint-resolution tests for viewer and output startup in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiViewerRepositoryContractTest.kt` and `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiOutputRepositoryContractTest.kt`
- [ ] T026 [P] [US2] Add failing canonical identity conflict tests for endpoint changes in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/CachedSourceIdentityResolverTest.kt` and `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/CachedSourceRepositoryImplTest.kt`
- [ ] T027 [US2] Extend Playwright emulator coverage for discovery-server-only mode, timeout diagnostics, and endpoint handoff in `testing/e2e/tests/031-discovery-routing.spec.ts`
- [ ] T027a [US2] Execute newly added US2 tests in the expected red state and record failing evidence in `test-results/031-us2-red-state.md` before starting T028

### Implementation for User Story 2

- [ ] T028 [US2] Implement discovery-server-only mode selection, 5-second timeout enforcement, and invalid-record filtering in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt`
- [ ] T029 [US2] Expose ordered enabled-server snapshots for per-run use in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryConfigRepositoryImpl.kt`
- [ ] T030 [US2] Apply discovery-server endpoint configuration per run and prevent mixed-mode execution in `ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt`
- [ ] T031 [US2] Update canonical cache merge behavior to preserve preview continuity while refreshing endpoint/timestamp data in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/CachedSourceIdentityResolver.kt` and `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/CachedSourceRepositoryImpl.kt`
- [ ] T032 [US2] Resolve viewer startup from persisted source endpoints only in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiViewerRepositoryImpl.kt`
- [ ] T033 [US2] Resolve output startup from persisted source endpoints only in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt`
- [ ] T034 [US2] Publish per-server timing, timeout, and endpoint-provenance diagnostics in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DeveloperDiagnosticsRepositoryImpl.kt`, `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DeveloperDiagnosticsLogBuffer.kt`, and `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerTelemetry.kt`
- [ ] T035 [US2] Execute targeted US2 tests and record evidence in `test-results/031-us2-discovery-server-routing.md`
- [ ] T036 [US2] Run existing Playwright regression coverage and append results to `test-results/031-us2-discovery-server-routing.md`
- [ ] T037 [US2] If discovery-server fixtures are unavailable or slow/unreachable, record `BLOCKED (environment)` details with endpoint/timestamps in `test-results/031-us2-discovery-server-routing.md`

**Checkpoint**: US2 is independently functional and validated.

---

## Phase 5: User Story 3 - Cached Source Reuse After Discovery (Priority: P3)

**⚠️ PREREQUISITE**: Phase 2 tasks T007–T011 must be complete, and Phase 4 US2 implementation (T028–T037) must be validated before US3 implementation begins.

**Goal**: Cached sources appear before live discovery completes after relaunch/update, survive timeout/failure paths, and maintain canonical identity continuity when endpoints change.

**Independent Test**: Discover and persist sources, relaunch or update while preserving app data, verify cached rows appear before live discovery finishes, and verify timeout/failure does not remove cached rows.

### Tests for User Story 3 (REQUIRED)

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [ ] T038 [P] [US3] Add failing relaunch cache visibility and availability-state tests in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/HomeDashboardRepositoryImplTest.kt` and `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelAvailabilityTest.kt`
- [ ] T039 [P] [US3] Add failing Home/View quick-action gating tests for cached rows during live discovery in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/home/HomeViewModelTest.kt` and `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt`
- [ ] T040 [P] [US3] Add failing cache-preservation tests for timeout/failure runs in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/CachedSourceRepositoryImplTest.kt`
- [ ] T041 [US3] Add Playwright emulator scenario for cache reuse after relaunch/update in `testing/e2e/tests/031-cache-relaunch-visibility.spec.ts`
- [ ] T041a [US3] Execute newly added US3 tests in the expected red state and record failing evidence in `test-results/031-us3-red-state.md` before starting T042

### Implementation for User Story 3

- [ ] T042 [US3] Preserve cached rows through timeout/failure and canonical endpoint refreshes in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/CachedSourceRepositoryImpl.kt` and `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt`
- [ ] T043 [US3] Emit cached source records before live discovery completion in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/HomeDashboardRepositoryImpl.kt` and `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt`
- [ ] T044 [US3] Update Home/View state handling for cached rows during validation in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/home/HomeViewModel.kt`, `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/home/HomeScreen.kt`, and `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt`
- [ ] T045 [US3] Render cached/stale/validating source states in `feature/ndi-browser/presentation/src/main/res/layout/item_ndi_source.xml`, `feature/ndi-browser/presentation/src/main/res/layout/fragment_source_list.xml`, and `feature/ndi-browser/presentation/src/main/res/layout/fragment_home_dashboard.xml`
- [ ] T046 [US3] Execute targeted US3 tests and record evidence in `test-results/031-us3-cache-relaunch.md`
- [ ] T047 [US3] Run existing Playwright regression coverage and append results to `test-results/031-us3-cache-relaunch.md`
- [ ] T048 [US3] If relaunch/update-path validation is unavailable, record `BLOCKED (environment)` details and unblock steps in `test-results/031-us3-cache-relaunch.md`

**Checkpoint**: US3 is independently functional and validated.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Finalize regression, compliance, documentation, and release-quality evidence.

- [ ] T049 [P] Record test-change traceability for any modified pre-existing tests in `test-results/031-test-change-traceability.md`
- [ ] T050 Run module unit regression and append results to `test-results/031-unit-regression.md` using `./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest`
- [ ] T051 Run Playwright primary regression profile and append results to `test-results/031-playwright-regression.md` using `npm --prefix testing/e2e run test:pr:primary`
- [ ] T052 Run release hardening verification and record output in `test-results/031-release-hardening.md` using `./gradlew.bat :app:verifyReleaseHardening`
- [ ] T053 [P] Capture Material 3 and visual-compliance verification notes in `test-results/031-material3-compliance.md`
- [ ] T054 Compile final pass/fail/blocked summary in `test-results/031-final-validation-summary.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 0 (Environment Preflight): starts immediately and blocks final quality-gate execution if unresolved.
- Phase 1 (Setup): depends on Phase 0 evidence capture start.
- Phase 2 (Foundational): depends on Phase 1 completion and blocks all user stories.
- Phase 3 (US1): depends on Phase 2 completion.
- Phase 4 (US2): depends on Phase 2 completion and can begin after US1 starts, but shares `NdiDiscoveryRepositoryImpl.kt` and cache-merge infrastructure.
- Phase 5 (US3): depends on Phase 2 completion and benefits from US1/US2 repository behavior being in place.
- Phase 6 (Polish): depends on completion of selected user stories and their evidence tasks.

### User Story Dependencies

- US1 (P1): no dependency on other user stories after foundational work.
- US2 (P2): no strict dependency on US1, but shares discovery/cache primitives from Phase 2.
- US3 (P3): no strict dependency on US1/US2, but relaunch/cache behavior is easier to validate once US1/US2 repository paths are stable.

### Within Each User Story

- Write and run failing tests first.
- Record red-state evidence before starting implementation tasks.
- Implement repository/domain behavior before UI wiring.
- Run targeted story tests and Playwright scenarios.
- Run existing Playwright regression and record evidence.

---

## Parallel Opportunities

- Phase 1: T005 and T006 can run in parallel.
- Phase 2: T009 and T010 can run in parallel after T007/T008 stabilize.
- US1: T012, T013, and T014 can run in parallel; T018 and T019 can run in parallel after T016/T017.
- US2: T023, T024, T025, and T026 can run in parallel; T032, T033, and T034 can run in parallel after T028 through T031.
- US3: T038, T039, and T040 can run in parallel; T043, T044, and T045 can run in parallel after T042.
- Polish: T049 and T053 can run in parallel with T050 through T052.

---

## Parallel Example: User Story 2

```bash
# Parallel failing-test-first tasks
T023 [US2] Timeout and no-fallback repository tests
T024 [US2] Diagnostics and blocker-classification tests
T025 [US2] Viewer/output endpoint-resolution tests
T026 [US2] Canonical identity conflict tests

# Parallel implementation after shared repository behavior lands
T032 [US2] Viewer persisted-endpoint startup update
T033 [US2] Output persisted-endpoint startup update
T034 [US2] Diagnostics/telemetry publication updates
```

## Implementation Strategy

### MVP First (US1)

1. Complete Phase 0 through Phase 2.
2. Complete T015a to capture red-state evidence.
3. Deliver Phase 3 (US1) end-to-end.
4. Validate US1 independently using T020 through T022 before expanding scope.

### Incremental Delivery

1. Build shared routing/cache foundation once.
2. Deliver US1 (multicast fallback path).
3. Deliver US2 (discovery-server-only routing, timeout, endpoint handoff).
4. Deliver US3 (relaunch cache visibility and continuity).
5. Complete Phase 6 cross-cutting gates.

### Parallel Team Strategy

1. One engineer handles shared repository/model foundation in Phase 2.
2. One engineer focuses US1 discovery-mode fallback behavior after foundation.
3. Another engineer can start US2 diagnostics/endpoint tasks once `NdiDiscoveryRepositoryImpl.kt` contracts are updated.
4. A third engineer can take US3 relaunch/UI continuity tasks once cache-preservation behavior is stabilized.

---

## Notes

- `[P]` means different files and no dependency on incomplete tasks.
- `[US1]`, `[US2]`, `[US3]` provide traceability to the specification’s user stories.
- Existing tests are regression protection first; any required edits must be justified in T049 with requirement references.
- Red-state evidence artifacts in `test-results/031-us*-red-state.md` are required before story implementation starts.
- Environment blockers must be documented in story evidence files and carried into T054.
