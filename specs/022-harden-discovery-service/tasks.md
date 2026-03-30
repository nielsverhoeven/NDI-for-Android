# Tasks: Discovery Service Reliability

**Input**: Design documents from `/specs/022-harden-discovery-service/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/discovery-service-ui-diagnostics-contract.md ✓

**Tests**: Tests are REQUIRED by constitution. Every user story includes failing-test-first JUnit tasks (repository + ViewModel behavior) plus Playwright emulator e2e coverage for all new/updated visual flows. Full existing Playwright e2e regression is executed in the Polish phase.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are included in every task description

---

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Confirm runtime dependencies, emulators, and SDK are ready before any implementation or validation begins. Classify blockers explicitly before proceeding.

- [X] T000 Run `scripts/verify-android-prereqs.ps1` and record PASS or BLOCKED:ENVIRONMENT in test-results/022-preflight-android.md
- [X] T001 Run `scripts/verify-e2e-dual-emulator-prereqs.ps1` and record PASS or BLOCKED:ENVIRONMENT in test-results/022-preflight-e2e.md
- [X] T002 Confirm NDI SDK is installed/discoverable by the build runtime, at least one reachable discovery server endpoint and one unreachable endpoint are available; record environment state in test-results/022-preflight-ndi-endpoints.md
- [X] T002a Resolve APK artifact output path by running `./gradlew.bat :app:assembleDebug --dry-run` and record the confirmed path in test-results/022-preflight-apk-path.md; use this resolved path in Playwright install commands at T024 and T033 (Constitution XII — deterministic artifact paths required before emulator e2e runs)

**Checkpoint**: All preflight checks PASS, or blocked gates are explicitly documented with unblock steps before proceeding.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Introduce the new Room entity, data classes, and enums that all three user stories share before any story-specific work begins.

- [X] T003 Add `DiscoveryCheckType` and `DiscoveryCheckOutcome` and `DiscoveryFailureCategory` enums in `core/model/src/main/java/com/ndi/core/model/DiscoveryCheckEnums.kt`
- [X] T004 [P] Add `DiscoveryServerCheckStatus` domain data class in `core/model/src/main/java/com/ndi/core/model/DiscoveryServerCheckStatus.kt` (fields: serverId, checkType, outcome, checkedAtEpochMillis, failureCategory, failureMessage, correlationId)
- [X] T005 [P] Add `DiscoveryServerCheckStatusEntity` Room entity in `core/database/src/main/java/com/ndi/core/database/entities/DiscoveryServerCheckStatusEntity.kt` (mirrors domain model, keyed by serverId)
- [X] T006 Add `DiscoveryServerCheckStatusDao` with upsert and query-by-serverId methods in `core/database/src/main/java/com/ndi/core/database/dao/DiscoveryServerCheckStatusDao.kt`
- [X] T007 Register `DiscoveryServerCheckStatusEntity` and expose `DiscoveryServerCheckStatusDao` in `core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt`
- [X] T007a Increment Room database version in `core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt`, write a `Migration` object from the previous schema version, and add a JUnit migration test confirming existing `DiscoveryServerEntry` rows survive the upgrade in `core/database/src/test/java/com/ndi/core/database/migration/DiscoveryCheckStatusMigrationTest.kt` (Constitution IV — shared persistence addition requires migration regression test)
- [X] T007b Run `bash .specify/scripts/bash/update-agent-context.sh copilot` after data-model and contract artifacts are finalized; record script output in test-results/022-agent-context-update.md
- [ ] T007c [P] Scan `feature/ndi-browser/presentation` and `feature/ndi-browser/data` modules for existing Espresso tests; convert all found to Playwright equivalents before Phase 3–5 implementation begins (Constitution IV — Espresso tests found during feature work must be converted to Playwright)

**Checkpoint**: Room entity and migration compile; migration regression test passes; agent context is updated; no unconverted Espresso tests remain in affected modules; domain enums and data class are available to all modules.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain contract additions and correlation utility that all three user stories depend on. No user story implementation can begin until this phase is complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T008 Add `performDiscoveryServerCheck`, `recheckDiscoveryServer`, and `getDiscoveryServerCheckStatus` method signatures to the discovery server repository contract in `feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt`
- [X] T009 [P] Add `DeveloperDiagnosticsRepository` contract interface for `getDeveloperDiscoveryDiagnostics` in `feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt`
- [X] T010 [P] Add `CorrelationId` generation utility (UUID-based, single-entry-point) in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/util/CorrelationId.kt`
- [X] T011 Wire `DiscoveryServerCheckStatusDao` and placeholder repository dependencies into `app/src/main/java/com/ndi/app/di/AppGraph.kt`

**Checkpoint**: Domain contracts compile, `CorrelationId` utility is available in data module, `AppGraph` compiles with new DAO binding.

---

## Phase 3: User Story 1 - Validate Discovery Server Connectivity (Priority: P1) 🎯 MVP

**Goal**: Each time a discovery server is added, the system performs a protocol-level connection check and displays a pass/fail badge with timestamp and actionable failure reason.

**Independent Test**: Add one reachable server → confirm success badge appears within 5 s. Add one unreachable server → confirm failure badge with human-readable reason appears within 5 s. Verify third server's existing status is unaffected.

### Tests for User Story 1 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST and confirm they FAIL before any implementation begins.**

- [X] T012 [P] [US1] Write failing JUnit test: `NdiNativeBridge.performDiscoveryCheck` maps NDI protocol success → `DiscoveryCheckOutcome.SUCCESS` and handshake timeout → `FAILURE` with `HANDSHAKE_FAILED` category in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DiscoveryProtocolCheckTest.kt`
- [X] T013 [P] [US1] Write failing JUnit test: `DiscoveryServerRepositoryImpl.addServer` invokes protocol check, persists `DiscoveryServerCheckStatus`, and returns actionable user message on failure in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DiscoveryServerRepositoryImplTest.kt`
- [X] T013a [P] [US1] Write failing JUnit sub-case in `DiscoveryServerRepositoryImplTest.kt`: mock bridge returning `FAILURE` outcome → assert the `DiscoveryServerEntry` row still exists in the DAO after the failed check (FR-010 — server registration must survive a failed connectivity check)
- [X] T014 [P] [US1] Write failing JUnit test: `DiscoveryServerSettingsViewModel` emits `checkResult` UI state carrying badge text and failure detail after add completes in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModelTest.kt`
- [X] T015 [P] [US1] Write failing Playwright e2e test: adding a valid discovery server endpoint → success check badge rendered in server row within 5 s in `testing/e2e/tests/022-us1-valid-server-check.spec.ts`; use `expect(badge).toBeVisible({ timeout: 5000 })` to assert the SC-001 5-second SLA
- [X] T016 [P] [US1] Write failing Playwright e2e test: adding an unreachable discovery server endpoint → failure badge and actionable failure message visible in server row within 5 s in `testing/e2e/tests/022-us1-invalid-server-check.spec.ts`; use `expect(failureBadge).toBeVisible({ timeout: 5000 })` to assert the SC-001 5-second SLA on the failure path

### Implementation for User Story 1

- [X] T017 [US1] Add `performDiscoveryCheck(hostOrIp: String, port: Int, correlationId: String): DiscoveryServerCheckStatus` Kotlin method and corresponding C++ NDI handshake invocation in `ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt` and `ndi/sdk-bridge/src/main/cpp/ndi_bridge.cpp`
- [X] T018 [US1] Extend `DiscoveryServerRepositoryImpl.addServer` to invoke bridge protocol check, persist `DiscoveryServerCheckStatus` via `DiscoveryServerCheckStatusDao`, and surface failure reason to caller in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImpl.kt`
- [X] T019 [US1] Extend `DiscoveryServerSettingsViewModel` to observe and expose `DiscoveryServerCheckStatus` as `checkResult` UI state after each add in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModel.kt`
- [X] T020 [US1] Update `DiscoveryServerSettingsFragment` to render check badge (`Connected` / `Check failed`) with timestamp and failure reason per server row in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsFragment.kt`
- [X] T021 [P] [US1] Emit `discovery_server_add_started` and `discovery_server_add_completed` log events carrying correlationId and endpoint identity in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImpl.kt`
- [X] T022 [P] [US1] Emit `discovery_server_check_started` and `discovery_server_check_completed` log events (with correlationId and outcome) in `ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt`

### Validation for User Story 1

- [X] T023 [US1] Run `./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest :ndi:sdk-bridge:testDebugUnitTest` and record PASS in `test-results/022-us1-unit-tests.md`
- [X] T024 [US1] Run Playwright e2e US1 scenarios (T015, T016) and record PASS or BLOCKED:ENVIRONMENT with reproduction steps in `test-results/022-us1-playwright-evidence.md`

**Checkpoint**: US1 is fully functional. Add-time connection check shows pass/fail status per server. Unit tests and e2e scenarios are green (or environment-blocked with evidence).

---

## Phase 4: User Story 2 - Recheck Registered Servers (Priority: P2)

**Goal**: Every registered server row exposes a recheck action that revalidates only that server and updates its check status without affecting other rows or server registration state.

**Independent Test**: Register two servers, trigger recheck on server A only, confirm server A status + timestamp updates while server B status is unchanged.

### Tests for User Story 2 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST and confirm they FAIL before any implementation begins.**

- [X] T025 [P] [US2] Write failing JUnit test: `DiscoveryServerRepositoryImpl.recheckServer` invokes protocol check for targeted server, persists updated `DiscoveryServerCheckStatus`, and leaves all other server statuses unchanged in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DiscoveryServerRepositoryImplTest.kt`
- [X] T025a [P] [US2] Write failing JUnit sub-case in `DiscoveryServerRepositoryImplTest.kt`: register two servers, trigger `recheckServer` for server A (reachable), assert server B's `DiscoveryServerCheckStatus` in the DAO is unmodified; ensures unreachable statuses cannot mask reachable-server discovery (spec.md edge case, FR-004)
- [X] T026 [P] [US2] Write failing JUnit test: `DiscoveryServerSettingsViewModel.recheckServer` emits state update scoped only to the targeted server id in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModelTest.kt`
- [X] T027 [P] [US2] Write failing Playwright e2e test: per-server recheck button exists for each registered server row and tapping it updates only that row's badge and timestamp in `testing/e2e/tests/022-us2-per-server-recheck.spec.ts`

### Implementation for User Story 2

- [X] T028 [US2] Add `recheckServer(serverId: String, correlationId: String)` method to `DiscoveryServerRepositoryImpl` using the bridge protocol check from T017 and persisting updated status via DAO in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImpl.kt`
- [X] T029 [US2] Extend `DiscoveryServerSettingsViewModel` with `recheckServer(serverId: String)` action that invokes repository recheck and emits per-row state update in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModel.kt`
- [X] T030 [US2] Add per-server recheck button to each server list row in `DiscoveryServerSettingsFragment` wired to `recheckServer` ViewModel action in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsFragment.kt`
- [X] T031 [P] [US2] Emit `discovery_server_recheck_started` and `discovery_server_recheck_completed` log events carrying correlationId and serverId in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImpl.kt`

### Validation for User Story 2

- [X] T032 [US2] Run `./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest` and record PASS in `test-results/022-us2-unit-tests.md`
- [X] T033 [US2] Run Playwright e2e US2 scenarios (T027) and record PASS or BLOCKED:ENVIRONMENT with reproduction steps in `test-results/022-us2-playwright-evidence.md`

**Checkpoint**: US2 is fully functional. Per-server recheck button exists and updates only the targeted server. Unit tests and e2e scenarios are green (or environment-blocked with evidence).

---

## Phase 5: User Story 3 - Developer Diagnostics for Discovery Flow (Priority: P3)

**Goal**: When developer mode is enabled, the UI surfaces expanded discovery diagnostics including per-server check history, last refresh result, failure categorization, and correlated log lines. All diagnostics are hidden when developer mode is off.

**Independent Test**: Toggle developer mode ON → expanded diagnostics panel appears with per-server status rollup. Toggle developer mode OFF → panel disappears immediately. Baseline source-list behavior is unchanged in both states.

### Tests for User Story 3 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST and confirm they FAIL before any implementation begins.**

- [X] T034 [P] [US3] Write failing JUnit test: `DiscoveryServerSettingsViewModel` exposes `DeveloperDiscoveryDiagnostics` when developer mode is ON and emits null/hidden state when OFF in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModelTest.kt`
- [X] T035 [P] [US3] Write failing JUnit test: `DeveloperDiagnosticsRepositoryImpl.getDeveloperDiscoveryDiagnostics` aggregates all `DiscoveryServerCheckStatus` records and latest discovery refresh result into `DeveloperDiscoveryDiagnostics` in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DeveloperDiagnosticsRepositoryImplTest.kt`
- [X] T036 [P] [US3] Write failing Playwright e2e test: toggling developer mode ON renders the discovery diagnostics section; toggling OFF hides it without losing source-list content in `testing/e2e/tests/022-us3-developer-diagnostics.spec.ts`
- [X] T036a [P] [US3] Write failing JUnit test: `LogRedactor.redact` strips discovery server hostOrIp and port values from a raw log string before it is included in `DeveloperDiscoveryDiagnostics.recentDiscoveryLogs`; assert redacted output contains no endpoint address (security — prevents endpoint PII from appearing in developer-visible UI) in `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/util/LogRedactorTest.kt`

### Implementation for User Story 3

- [X] T037 [US3] Add `DeveloperDiscoveryDiagnostics` data class (fields: developerModeEnabled, latestDiscoveryRefreshStatus, latestDiscoveryRefreshAtEpochMillis, serverStatusRollup, recentDiscoveryLogs) in `core/model/src/main/java/com/ndi/core/model/DeveloperDiscoveryDiagnostics.kt`
- [X] T037a [US3] Add `LogRedactor` utility that replaces hostOrIp and port values in raw log strings with `[REDACTED]` before diagnostic log buffering in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/util/LogRedactor.kt`; integrate the redaction call in `DeveloperDiagnosticsRepositoryImpl` before appending to `recentDiscoveryLogs` (must be complete before T038 and T040)
- [X] T038 [US3] Create `DeveloperDiagnosticsRepositoryImpl` implementing `DeveloperDiagnosticsRepository`: aggregates `DiscoveryServerCheckStatus` records from DAO, collects discovery refresh signals, assembles `DeveloperDiscoveryDiagnostics` in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DeveloperDiagnosticsRepositoryImpl.kt`
- [X] T039 [P] [US3] Extend `DeveloperOverlayState` with `discoveryDiagnostics: DeveloperDiscoveryDiagnostics?` field in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayState.kt`
- [X] T040 [US3] Extend `DeveloperOverlayRenderer` to render per-server check outcomes, discovery refresh status, failure categorization, and redacted log lines when `discoveryDiagnostics` is non-null in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayRenderer.kt`
- [X] T041 [US3] Extend `SettingsViewModel` to observe developer mode state and route `DeveloperDiscoveryDiagnostics` into overlay state; clear on toggle-off in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt`
- [X] T042 [P] [US3] Emit `discovery_refresh_started` and `discovery_refresh_completed` log events with correlationId and outcome in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt`
- [X] T043 [P] [US3] Wire `DeveloperDiagnosticsRepositoryImpl` and its DAO/data dependencies into `app/src/main/java/com/ndi/app/di/AppGraph.kt`

### Validation for User Story 3

- [X] T044 [US3] Run `./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest` and record PASS in `test-results/022-us3-unit-tests.md`
- [X] T045 [US3] Run Playwright e2e US3 scenarios (T036) and record PASS or BLOCKED:ENVIRONMENT with reproduction steps in `test-results/022-us3-playwright-evidence.md`

**Checkpoint**: All three user stories are independently functional. Developer diagnostics appear and disappear with developer mode toggle.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Full regression, release hardening, and documentation finalization across all stories.

- [X] T046 Run full existing Playwright e2e regression suite and record PASS or BLOCKED:ENVIRONMENT in `test-results/022-playwright-regression.md`; investigate and fix any regressions before proceeding
- [X] T047 Run `./gradlew.bat :app:verifyReleaseHardening` (R8/ProGuard + shrink resources) and record PASS in `test-results/022-release-hardening.md`
- [X] T048 [P] Update `docs/ndi-feature.md` with discovery server diagnostics usage, recheck behavior, and developer mode diagnostic details

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 0 (Preflight)**: No dependencies — run immediately before any other phase
- **Phase 1 (Setup)**: Depends on Phase 0 PASS (or explicit BLOCKED documentation)
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user stories**
- **Phase 3 (US1)**: Depends on Phase 2 completion — no dependencies on US2 or US3
- **Phase 4 (US2)**: Depends on Phase 2 completion — reuses T017 bridge check from US1 but does NOT depend on US1 being complete
- **Phase 5 (US3)**: Depends on Phase 2 completion — consumes `DiscoveryServerCheckStatus` DAO from Phase 1 but does NOT depend on US1 or US2 being complete
- **Phase 6 (Polish)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: Depends only on Phase 2 — independently testable; no dependency on US2 or US3
- **US2 (P2)**: Depends on Phase 2 and reuses bridge check method introduced in T017; US2 tests can be written before T017 is implemented
- **US3 (P3)**: Depends on Phase 1 (DiscoveryServerCheckStatusEntity/DAO) and Phase 2; does not depend on US1 or US2

### Within Each User Story

- Failing tests MUST be written first and observed to fail before implementation begins
- `DiscoveryServerCheckStatusDao` (T006) must exist before any repository impl that persists status
- `CorrelationId` utility (T010) must exist before any logging task
- Bridge check method (T017) must exist before repository recheck method (T028)
- `LogRedactor` utility (T037a) must exist before `DeveloperDiagnosticsRepositoryImpl` appends log entries (T038) and before `DeveloperOverlayRenderer` renders diagnostics (T040)
- Repository methods must be complete before ViewModel changes
- ViewModel changes must be complete before Fragment/UI changes
- Unit tests must be green before running Playwright e2e scenarios

---

## Parallel Opportunities

### Phase 1 (all can run simultaneously after T003)

```
T004 DiscoveryServerCheckStatus data class
T005 DiscoveryServerCheckStatusEntity Room entity
T007c Espresso-to-Playwright conversion scan (affected presentation/data modules)
```

### Phase 2 (T009, T010 can run in parallel with T008)

```
T008 Domain contract additions
T009 DeveloperDiagnosticsRepository contract
T010 CorrelationId utility
```

### Phase 3 — failing tests can all run in parallel

```
T012 JUnit: bridge protocol check mapping
T013 JUnit: addServer persists status
T013a JUnit: server entry survives failed check (FR-010)
T014 JUnit: ViewModel emits checkResult state
T015 Playwright: valid server success badge (5 s SLA)
T016 Playwright: unreachable server failure message (5 s SLA)
```

### Phase 3 — logging tasks are parallel after T018 is complete

```
T021 Logging: add_started/completed in DiscoveryServerRepositoryImpl
T022 Logging: check_started/completed in NdiNativeBridge
```

### Phase 4 — failing tests can all run in parallel

```
T025 JUnit: recheckServer scope isolation
T025a JUnit: multi-server isolation (reachable not masked by unreachable)
T026 JUnit: ViewModel recheck scoped state
T027 Playwright: per-server recheck button
```

### Phase 5 — failing tests can all run in parallel

```
T034 JUnit: developer mode toggle state
T035 JUnit: DeveloperDiagnosticsRepositoryImpl aggregation
T036 Playwright: dev mode diagnostics toggle
T036a JUnit: LogRedactor strips endpoint PII from log strings
```

### Phase 5 — parallel after T038–T040

```
T039 Extend DeveloperOverlayState
T042 Emit discovery_refresh logs in NdiDiscoveryRepositoryImpl
T043 Wire DeveloperDiagnosticsRepositoryImpl into AppGraph
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 0: Preflight
2. Complete Phase 1: Setup (shared entities and enums)
3. Complete Phase 2: Foundational (domain contracts + correlation utility)
4. Complete Phase 3: User Story 1 (add-time connection check + badge UI)
5. **STOP and VALIDATE**: US1 unit tests and e2e scenarios pass
6. Deploy/demo MVP: discovery server add surfaces clear pass/fail connectivity

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. **US1 complete** → Protocol-level add-time check (MVP)
3. **US2 complete** → Per-server recheck button (short-circuits re-add workflow)
4. **US3 complete** → Developer diagnostics visible in developer mode
5. Polish → Full regression + release hardening → Merge-ready

### Parallel Team Strategy (if staffed)

After Phase 2 completes:

- **Developer A**: US1 (bridge check + add-time validation)
- **Developer B**: US2 (recheck action + per-row button) — can stub bridge call until T017 merges
- **Developer C**: US3 (developer diagnostics aggregation + overlay renderer)

Each developer writes failing tests first, implements, validates independently, then joins for Phase 6 regression.

---

## Notes

- `[P]` tasks involve different files and have no dependencies on incomplete work in the same phase
- `[USN]` label maps every task to its user story for traceability and independent delivery
- Failing tests MUST be written before implementation and observed to fail
- Playwright emulator e2e tests cover all new/updated visual flows (add badge, recheck button, dev diagnostics panel)
- Full existing Playwright e2e regression runs in Phase 6 before release hardening
- If NDI SDK or emulator environment is unavailable, classify gates as BLOCKED:ENVIRONMENT with unblock instructions; do not mark as CODE FAILURE
- Commit after each task or logical group to preserve clean history
- `DiscoveryServerEntry` existing persistence is reused; only `DiscoveryServerCheckStatus` persistence is new
- All check and recheck logging carries `correlationId` and `endpoint identity` for cross-layer tracing
