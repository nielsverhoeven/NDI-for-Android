# Tasks: NDI Screen Share Output Redesign

**Input**: Design documents from `/specs/017-ndi-screen-output/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are REQUIRED by constitution. Every user story includes failing-test-first unit/e2e tasks plus Playwright regression and blocked-gate evidence tasks.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 0: Environment Preflight (Blocking)

**Purpose**: Confirm runtime/tool prerequisites before implementation and validation.

- [ ] T001 Run prerequisite gate and capture output in test-results/017-ndi-screen-output-preflight.md using scripts/verify-android-prereqs.ps1
- [ ] T002 Run dual-emulator preflight and capture output in test-results/017-ndi-screen-output-preflight.md using scripts/verify-e2e-dual-emulator-prereqs.ps1
- [ ] T003 Verify debug/release artifacts build successfully with ./gradlew.bat :app:assembleDebug :ndi:sdk-bridge:assembleRelease and record in test-results/017-ndi-screen-output-preflight.md
- [ ] T004 Record any environment blocker and concrete unblock command in test-results/017-ndi-screen-output-preflight.md

**Checkpoint**: Preflight status is PASS or explicitly documented as blocked with unblocking steps.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare shared scaffolding for output redesign and validation evidence.

- [ ] T005 Create validation report stub for this feature in test-results/017-ndi-screen-output-validation.md
- [ ] T006 Add feature-specific e2e spec shell in testing/e2e/tests/output-screen-share.spec.ts
- [ ] T007 [P] Add feature-specific test support helpers in testing/e2e/tests/support/output-screen-share-helpers.ts
- [ ] T008 [P] Add task trace section to specs/017-ndi-screen-output/quickstart.md for commands and evidence paths

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement cross-story foundation before user stories.

- [ ] T009 Add discovery-mode decision model for server/mDNS/start-blocked in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt
- [ ] T010 [P] Add actionable discovery error constants/messages in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
- [ ] T011 [P] Add per-start consent reset hook on explicit stop in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
- [ ] T012 [P] Add/align discovery endpoint read path in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryConfigRepositoryImpl.kt
- [ ] T013 Add discovery reachability check for configured server in ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt
- [ ] T014 Wire updated output/discovery dependencies through app/src/main/java/com/ndi/app/di/AppGraph.kt
- [ ] T015 Synchronize domain repository signatures and contract obligations with finalized behavior in feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt

**Checkpoint**: Shared output/discovery/consent foundation is complete and user-story work can proceed.

---

## Phase 3: User Story 1 - Start Screen Share as NDI Output (Priority: P1) 🎯 MVP

**Goal**: Distinct Output UI starts local screen sharing with consent and shows ACTIVE state.

**Independent Test**: Open Output tab, tap Share Screen, grant consent, verify ACTIVE state and receiver playback.

### Tests for User Story 1 (REQUIRED)

- [ ] T016 [P] [US1] Add failing ViewModel test for start-from-idle consent prompt flow in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTest.kt
- [ ] T017 [P] [US1] Add failing repository test for local screen-share start state transitions in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImplTest.kt
- [ ] T018 [P] [US1] Add failing Playwright e2e for Output idle->consent->ACTIVE flow in testing/e2e/tests/output-screen-share.spec.ts
- [ ] T019 [US1] Run existing Playwright regression suite and append results to test-results/017-ndi-screen-output-validation.md using npm --prefix testing/e2e run test:pr:primary
- [ ] T020 [US1] If blocked, record reproduction and unblock command in test-results/017-ndi-screen-output-validation.md

### Implementation for User Story 1

- [ ] T021 [P] [US1] Redesign output controls layout/state labels in feature/ndi-browser/presentation/src/main/res/layout/fragment_output_control.xml
- [ ] T022 [P] [US1] Implement distinct output rendering logic and CTA behavior in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt
- [ ] T023 [US1] Implement consent-gated start flow and state projection in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
- [ ] T024 [US1] Integrate UI event wiring and consent launch handling in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlFragment.kt

**Checkpoint**: User Story 1 is independently functional and demonstrable.

---

## Phase 4: User Story 2 - Consent Always Required Per Start (Priority: P2)

**Goal**: Every new start prompts consent; stop fully resets consent for next start.

**Independent Test**: Start -> grant -> stop -> start again; consent prompt appears again every time.

### Tests for User Story 2 (REQUIRED)

- [ ] T025 [P] [US2] Add failing stop-then-restart re-consent test in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlStopStateTest.kt
- [ ] T026 [P] [US2] Add failing consent-clear-on-stop repository test in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImplTest.kt
- [ ] T027 [P] [US2] Add failing Playwright e2e for stop-and-restart re-consent flow in testing/e2e/tests/output-screen-share.spec.ts
- [ ] T028 [US2] Run existing Playwright regression suite and append results to test-results/017-ndi-screen-output-validation.md using npm --prefix testing/e2e run test:pr:primary
- [ ] T029 [US2] If blocked, record reproduction and unblock command in test-results/017-ndi-screen-output-validation.md
- [ ] T029a [P] [US2] Add failing retry-window test that enforces 15-second recovery limit in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTest.kt

### Implementation for User Story 2

- [ ] T030 [US2] Ensure explicit stop clears consent and resets start prerequisites in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
- [ ] T031 [US2] Implement/confirm clearConsent invocation in stop path in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt
- [ ] T032 [P] [US2] Improve denied-consent user messaging and recovery controls in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt
- [ ] T033 [US2] Add telemetry events for consent requested/granted/denied/reset in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputTelemetry.kt
- [ ] T033a [US2] Enforce and surface retry-window behavior (15 seconds) in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt and feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt

**Checkpoint**: User Story 2 works independently without relying on later stories.

---

## Phase 5: User Story 3 - Background Stream Persistence (Priority: P3)

**Goal**: Stream remains active while app is minimized, with persistent notification and stop action.

**Independent Test**: Start stream, background app, open another app, verify receiver still shows content for >=5 minutes, then stop from notification.

### Tests for User Story 3 (REQUIRED)

- [ ] T034 [P] [US3] Add failing continuity state test for background transitions in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTopLevelNavTest.kt
- [ ] T035 [P] [US3] Add failing repository continuity/reconnect test in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/StreamContinuityRepositoryImplTest.kt
- [ ] T036 [P] [US3] Add failing Playwright e2e for background continuity and notification stop action in testing/e2e/tests/output-screen-share.spec.ts
- [ ] T037 [US3] Run existing Playwright regression suite and append results to test-results/017-ndi-screen-output-validation.md using npm --prefix testing/e2e run test:pr:primary
- [ ] T038 [US3] If blocked, record reproduction and unblock command in test-results/017-ndi-screen-output-validation.md
- [ ] T038a [P] [US3] Add timing assertion for receiver visibility within 10 seconds in testing/e2e/tests/output-screen-share.spec.ts and record evidence in test-results/017-ndi-screen-output-validation.md

### Implementation for User Story 3

- [ ] T039 [US3] Implement/align foreground continuity lifecycle handling in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
- [ ] T040 [US3] Ensure continuity state transitions on explicit stop/background events in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/StreamContinuityRepositoryImpl.kt
- [ ] T041 [US3] Implement persistent notification deep link and stop action integration in app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt
- [ ] T042 [US3] Verify output destination deep link and argument behavior for notification return path in app/src/main/res/navigation/main_nav_graph.xml

**Checkpoint**: User Story 3 is independently testable and stable.

---

## Phase 6: User Story 4 - Discovery Behavior (Priority: P4)

**Goal**: Discovery uses configured reachable server, mDNS when unconfigured, and fails start when configured server is unreachable.

**Independent Test**: Validate three scenarios: reachable configured server, no configured server, unreachable configured server.

### Tests for User Story 4 (REQUIRED)

- [ ] T043 [P] [US4] Add failing repository test for configured reachable server registration path in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImplTest.kt
- [ ] T044 [P] [US4] Add failing repository test for no-config mDNS path in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImplTest.kt
- [ ] T045 [P] [US4] Add failing repository test for unreachable configured server start failure in feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImplTest.kt
- [ ] T046 [P] [US4] Add failing Playwright e2e coverage for all three discovery modes in testing/e2e/tests/output-screen-share.spec.ts
- [ ] T047 [US4] Run existing Playwright regression suite and append results to test-results/017-ndi-screen-output-validation.md using npm --prefix testing/e2e run test:pr:primary
- [ ] T048 [US4] If blocked, record reproduction and unblock command in test-results/017-ndi-screen-output-validation.md
- [ ] T048a [P] [US4] Add failing test for full-device capture initiation path when Android APIs permit pre-selection in feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTest.kt

### Implementation for User Story 4

- [ ] T049 [US4] Implement configured-server reachability gating and start-block behavior in feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImpl.kt
- [ ] T050 [US4] Implement/align discovery endpoint application and reachability probe in ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt
- [ ] T051 [US4] Surface actionable discovery errors in output UI state and rendering in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
- [ ] T052 [US4] Add persisted settings/discovery regression coverage in feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/OutputSettingsNavigationTest.kt
- [ ] T052a [US4] Implement full-device capture initiation preference and platform-fallback handling in feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlFragment.kt and ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt

**Checkpoint**: User Story 4 behavior is deterministic and independently verifiable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final hardening, documentation, and quality gates across all stories.

- [ ] T053 [P] Update feature notes and operator guidance in docs/ndi-feature.md
- [ ] T054 [P] Add final manual validation narrative and gate outcomes in test-results/017-ndi-screen-output-validation.md
- [ ] T055 Run unit test suites and capture outputs for touched modules in test-results/017-ndi-screen-output-validation.md using ./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest
- [ ] T056 Run dual-emulator Playwright scenario and record artifacts in test-results/017-ndi-screen-output-validation.md using npm --prefix testing/e2e run test:dual-emulator
- [ ] T057 Run release hardening validation and record results in test-results/017-ndi-screen-output-validation.md using ./gradlew.bat :app:verifyReleaseHardening :app:assembleRelease
- [ ] T058 If any gate is blocked, classify blocker vs code failure and record explicit unblock steps in test-results/017-ndi-screen-output-validation.md
- [ ] T059 Perform and record Material 3 compliance verification for Output screen updates in test-results/017-ndi-screen-output-validation.md

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 0 must complete before implementation and validation work.
- Phase 1 depends on Phase 0.
- Phase 2 depends on Phase 1 and blocks all user stories.
- Phases 3-6 (US1-US4) depend on Phase 2.
- Phase 7 depends on completion of all targeted user stories.

### User Story Dependencies

- US1 (P1): Starts immediately after Phase 2; MVP baseline.
- US2 (P2): Depends on US1 consent/start-stop baseline.
- US3 (P3): Depends on US1 active-stream baseline and continuity foundation from Phase 2.
- US4 (P4): Depends on foundational discovery wiring in Phase 2 and can proceed after US1.

### Within Each User Story

- Tests MUST be written first and fail before implementation.
- ViewModel/repository behavior before UI polish.
- Story-specific Playwright coverage before story completion.
- Existing Playwright regression run and blocked-gate evidence are mandatory per story.

### Parallel Opportunities

- Phase 1 tasks marked [P] can run concurrently.
- Phase 2 tasks T010, T011, and T012 can run in parallel after T009 starts.
- In each story, [P] tests can run in parallel.
- In each story, [P] implementation tasks can run in parallel when they touch separate files.

---

## Parallel Example: User Story 1

```bash
# Run failing tests in parallel
T016: feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTest.kt
T017: feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/NdiOutputRepositoryImplTest.kt
T018: testing/e2e/tests/output-screen-share.spec.ts

# Implement UI pieces in parallel
T021: feature/ndi-browser/presentation/src/main/res/layout/fragment_output_control.xml
T022: feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlScreen.kt
```

## Parallel Example: User Story 4

```bash
# Discovery mode tests in parallel
T043: reachable configured server path test
T044: no-config mDNS path test
T045: unreachable configured server fail-start test

# Discovery implementation split by layer
T050: ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt
T051: feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
```

---

## Implementation Strategy

### MVP First (US1)

1. Complete Phase 0, 1, and 2.
2. Deliver Phase 3 (US1) fully.
3. Validate US1 independently before expanding scope.

### Incremental Delivery

1. Add US2 to lock consent-reset behavior.
2. Add US3 for background continuity and notification controls.
3. Add US4 for deterministic discovery behavior and failure semantics.
4. Finish with Phase 7 hardening and full validation gates.

### Parallel Team Strategy

1. Team completes Phases 0-2 together.
2. One engineer leads US2 consent lifecycle while another leads US3 continuity once US1 baseline is stable.
3. Discovery specialist implements US4 data/sdk-bridge tasks while UI engineer finalizes error surfaces.
