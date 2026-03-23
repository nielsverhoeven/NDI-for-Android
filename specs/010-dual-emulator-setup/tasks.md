# Tasks: Dual Emulator Testing Infrastructure Setup

**Feature**: 010-dual-emulator-setup  
**Spec**: specs/010-dual-emulator-setup/spec.md  
**Plan**: specs/010-dual-emulator-setup/plan.md  
**Status**: Ready for implementation

---

## Phase Structure & Dependencies

```
PHASE 2: Foundational (Blocking Prerequisites)
├─ T001-T005: Gradle validation, APK build gate, prerequisite checks
│  └─ Must complete before ANY user story (upstream blockers)

PHASE 3: US1 Provisioning (P1)
├─ T006-T010: Models, entity schemas, data models
├─ T011-T015: Provisioning service implementation + tests
└─ Delivers: Provision-Emulator, Provision-DualEmulator scripts with full test coverage

PHASE 4: US2 Relay (P1)
├─ T016-T020: Relay entity models, TCP socket helper implementation
├─ T021-T025: Relay service + health monitor + tests
└─ Delivers: Start-RelayServer, Check-RelayHealth, Monitor-RelayHealth with <100ms latency guarantee

PHASE 5: US3 Reset (P2)
├─ T026-T028: Reset service implementation + test
└─ Delivers: Reset-EmulatorState, inter-suite environment cleanup

PHASE 6: US4 Collection (P2)
├─ T029-T033: Artifact collection service + scripts + tests
└─ Delivers: Collect-Logcat, Collect-ScreenRecording, Generate-ArtifactManifest, Collect-Diagnostics

PHASE 7: US5 Documentation (P3)
├─ T034-T035: Infrastructure guide, troubleshooting guide
└─ Delivers: docs/dual-emulator-setup.md with local dev + CI/CD integration

FINAL: Polish & Cross-Cutting
├─ T036-T038: CI/CD integration, Playwright fixture hooks, end-to-end validation
└─ Final gate before production
```

---

## Dependency Graph (User Story Completion Order)

```
┌──────────────────────────────────────────────────────────────────┐
│ FOUNDATIONAL (T001-T005)                                         │
│ Gradle validation, APK build, prerequisites                      │
│ ✓ Must complete before ALL user stories                          │
└────────────────┬─────────────────────────────────────────────────┘
                 │
    ┌────────────┼────────────┐
    │            │            │
    ▼            ▼            ▼
┌─────────────────────────┐ ┌──────────────────┐ ┌────────────────┐
│ US1: Provisioning       │ │ US2: Relay (P1)  │ │ US3: Reset     │
│ (T006-T015) [P1]       │ │ (T016-T025)      │ │ (T026-T028)    │
│ ✓ Independent          │ │ ✓ Depends on US1 │ │ ✓ Depends on   │
│   from US2/US3         │ │ ✓ Can start      │ │   US1 (state)  │
│                        │ │ │  after T015    │ │   after T015   │
└────────────┬───────────┘ └────────┬─────────┘ └───────┬────────┘
             │                      │                    │
             └──────────────────────┼────────────────────┘
                                    │
         ┌──────────────────────────┼────────────────────┐
         │                          │                    │
         ▼                          ▼                    ▼
    ┌─────────────────┐    ┌──────────────────┐   ┌────────────────┐
    │ US4: Collection │    │ US5: Documentation
    │ (T029-T033)     │    │ (T034-T035)      │
    │ ✓ Depends on    │    │ ✓ Depends on     │
    │   US1+US2+US3   │    │   US1-US4        │
    └────────┬────────┘    └────────┬─────────┘
             │                      │
             └──────────┬───────────┘
                        │
                        ▼
         FINAL: E2E Integration (T036-T038)
         ✓ Feature 009 consumer validation
```

---

## Parallel Execution Examples

### User Story 1 (Provisioning) — Parallelizable Tasks

All US1 implementation tasks can run in parallel (different files, no blocking dependencies):

```
Parallel Batch 1 (while T006 completes):
  └─ T007 [P]: Create emulator helper script (emulator-adb.ps1)
  └─ T008 [P]: Create data model JSON schema (EmulatorInstance)
  └─ T009 [P]: Create error handling utilities (result-handler.ps1)

Parallel Batch 2 (after T010):
  └─ T011 [P] [US1]: Implement Provision-Emulator function
  └─ T012 [P] [US1]: Implement Provision-DualEmulator function
  └─ T013 [P] [US1]: Create Pester tests for provisioning (unit tests)

Batch 3 (blocking):
  └─ T014 [US1]: Integrate provisioning into provision-dual-emulator.ps1
  └─ T015 [US1]: Validate provisioning in CI/CD (prerequisite gate)
```

Result: **US1 completion in ~T015**, ~7 tasks total.

### User Story 2 (Relay) — Parallelizable Tasks

US2 depends on US1 completion (T015).  After provisioning is validated:

```
Parallel Batch 1 (after T015, while T016-T018 complete):
  └─ T019 [P] [US2]: Implement relay health check logic
  └─ T020 [P] [US2]: Implement relay metrics collection
  └─ T021 [P] [US2]: Create TCP socket forwarding helper

Parallel Batch 2 (after T021):
  └─ T022 [P] [US2]: Implement Start-RelayServer function
  └─ T023 [P] [US2]: Implement relay restart monitor
  └─ T024 [P] [US2]: Create Pester tests for relay (unit tests)

Batch 3 (blocking):
  └─ T025 [US2]: Integrate relay into CI/CD setup hooks
```

Result: **US2 completion in ~T025**, ~10 tasks total.

### User Story 3 (Reset) — Sequential (Small Scope)

US3 depends on US1 completion (T015).  After provisioning is validated:

```
Sequential Batch:
  └─ T026 [US3]: Implement Reset-EmulatorState function
  └─ T027 [US3]: Create reset test (Pester)
  └─ T028 [US3]: Integrate reset into test suite lifecycle
```

Result: **US3 completion in ~T028**, ~3 tasks total.

### User Story 4 (Collection) — Parallelizable Tasks

US4 depends on US1 completion (T015).  After provisioning is validated:

```
Parallel Batch 1 (after T015):
  └─ T029 [P] [US4]: Implement Collect-Logcat function
  └─ T030 [P] [US4]: Implement Collect-ScreenRecording function
  └─ T031 [P] [US4]: Implement Collect-Diagnostics function

Batch 2 (blocking):
  └─ T032 [US4]: Implement Generate-ArtifactManifest JSON report
  └─ T033 [US4]: Create Pester tests for collection
```

Result: **US4 completion in ~T033**, ~5 tasks total.

---

## Implementation Strategy: MVP to GA

### Milestone 1 (M1): Core Infrastructure MVP [~1 week]
**Target**: Provision + Relay working, feature 009 validates latency measurement

- **Phase 2**: All foundational tasks (T001-T005)
- **Phase 3**: All provisioning tasks (T006-T015)
- **Phase 4**: Start + health check relay tasks (T016-T022)
- **Test Gate**: Dual emulator provisioning succeeds 95% of the time locally + CI/CD, relay <100ms latency

**Shipped Artifacts**:
- `testing/e2e/scripts/provision-dual-emulator.ps1` (production-ready)
- `testing/e2e/scripts/start-relay-server.ps1` (production-ready)
- Pester unit tests for provisioning + relay core paths
- Feature 009 latency measurement validates dual-emulator infrastructure

### Milestone 2 (M2): Resilience + Collection [~1 week]
**Target**: Relay health monitoring, artifact collection for debugging failures

- **Phase 4**: Relay restart monitor + tests (T023-T025)
- **Phase 5**: Reset functionality (T026-T028)
- **Phase 6**: Artifact collection (T029-T033)
- **Test Gate**: Test failures generate artifact bundles in testing/e2e/artifacts/, relay restarts on failure without user intervention

**Shipped Artifacts**:
- `testing/e2e/scripts/relay-health-monitor.ps1` (background health checks)
- `testing/e2e/scripts/reset-emulator-state.ps1` (inter-suite cleanup)
- `testing/e2e/scripts/collect-test-artifacts.ps1` (post-test recovery)
- Support for nested Playwright suites with isolation

### Milestone 3 (M3): Documentation + Operational Excellence [~3 days]
**Target**: Documentation, CI/CD integration, troubleshooting guide

- **Phase 7**: Documentation (T034-T035)
- **Final Phase**: E2E integration + CI/CD glue (T036-T038)
- **Test Gate**: New developers can provision dual emulator locally in <15 min, CI/CD job artifact upload includes full provisioning/relay/artifact logs

**Shipped Artifacts**:
- `docs/dual-emulator-setup.md` (infrastructure guide)
- GitHub Actions workflow integration
- Troubleshooting guide (common failure modes + recovery)
- Dashboard placeholder for monitoring (future archive)

---

## Phase 2: Foundational (Blocking Prerequisites)

All user stories depend on completing this phase.

- [ ] T001 Verify Gradle wrapper and build toolchain version match baseline specs/010-dual-emulator-setup/validation/gradle-check.ps1
- [ ] T002 [P] Validate Android SDK emulator availability and API 32-35 images testing/e2e/scripts/helpers/validate-emulator-images.ps1
- [ ] T003 [P] Verify NDI SDK bridge APK exists and is buildable at ndi/sdk-bridge/build/outputs/apk/release/ndi-sdk-bridge-release.apk
- [ ] T004 [P] Create prerequisite validation gate (Windows PowerShell + Bash fallback) scripts/verify-e2e-dual-emulator-prereqs.ps1
- [ ] T005 Create Gradle task for building NDI SDK APK on demand build.gradle.kts (ndi/sdk-bridge/assembleRelease gate)

---

## Phase 3: US1 Automated Emulator Provisioning (Priority: P1)

### User Story Goal
Reliably provision two connected Android emulator instances with pre-configured NDI SDK and network connectivity so end-to-end tests can start immediately.

### Independent Test Criteria
✅ **PASS**: Execute provisioning script → verify two emulators discoverable via ADB → NDI SDK installed → screen recording works → provisioning status JSON returned  
✅ **FAIL**: Provisioning script exits with error code and detailed ADB logs captured

### Implementation Tasks

- [ ] T006 Create EmulatorInstance data model JSON schema specs/010-dual-emulator-setup/data-model.md (entity definition + validation rules)
- [ ] T007 [P] Create PowerShell ADB wrapper helpers (state, boot, install, screen-record) testing/e2e/scripts/helpers/emulator-adb.ps1
- [ ] T008 [P] Create PowerShell result/error handling utilities (ProvisioningResult, error codes) testing/e2e/scripts/helpers/result-handler.ps1
- [ ] T009 [P] Create JSON schema validator for EmulatorInstance entities testing/e2e/scripts/helpers/entity-validator.ps1
- [ ] T010 Create Provision-Emulator main function signature and contract testing/e2e/scripts/provision-dual-emulator.ps1
- [ ] T011 [P] [US1] Implement Provision-Emulator core logic (start, detect, boot, verify) testing/e2e/scripts/provision-dual-emulator.ps1
- [ ] T012 [P] [US1] Implement Provision-DualEmulator orchestration (atomic provision both emulators) testing/e2e/scripts/provision-dual-emulator.ps1
- [ ] T013 [P] [US1] Implement Get-EmulatorState function (ADB query, return JSON snapshot) testing/e2e/scripts/provision-dual-emulator.ps1
- [ ] T014 [US1] Create Pester unit tests for provisioning (passing cases, timeouts, port conflicts, NDI install failures) testing/e2e/scripts/tests/provision-dual-emulator.tests.ps1
- [ ] T015 [US1] Integrate provisioning into CI/CD gate: GitHub Actions workflow must call provision-dual-emulator.ps1 before feature tests .github/workflows/e2e-dual-emulator.yml

**Test Deliverables**:
- Unit tests (Pester): 15+ test cases covering happy path, error cases, idempotency
- Validation tests (Playwright): dual-emulator-provisioning.spec.ts validates provisioning status JSON structure and device discovery

---

## Phase 4: US2 TCP Relay Server Infrastructure (Priority: P1)

### User Story Goal
Communicate between emulator instances through reliable TCP-based relay server without manual network configuration so multi-device test scenarios work reproducibly.

### Independent Test Criteria
✅ **PASS**: Start relay → send test packets between emulator instances → verify <100ms round-trip latency → stop relay without errors  
✅ **FAIL**: Relay fails to start or health check reports latency >100ms

### Implementation Tasks

- [ ] T016 Create RelayServer and RelayRoute data models JSON schemas specs/010-dual-emulator-setup/data-model.md
- [ ] T017 [P] Create TCP socket forwarding implementation (raw PowerShell sockets or Node.js bridge) testing/e2e/scripts/helpers/relay-tcp-forwarder.ps1
- [ ] T018 [P] Create relay health check logic (echo test, latency measurement) testing/e2e/scripts/helpers/relay-health-check.ps1
- [ ] T019 [P] Create relay metrics collection (bytes forwarded, latency percentiles, packet loss) testing/e2e/scripts/helpers/relay-metrics.ps1
- [ ] T020 [P] Create Start-RelayServer function (start process, configure routes, initial health check) testing/e2e/scripts/start-relay-server.ps1
- [ ] T021 [P] [US2] Implement Get-RelayServer function (query relay state without modification) testing/e2e/scripts/start-relay-server.ps1
- [ ] T022 [P] [US2] Implement Stop-RelayServer function (graceful shutdown, port release) testing/e2e/scripts/start-relay-server.ps1
- [ ] T023 [US2] Implement relay health monitor loop (background process, auto-restart on failure max 3 retries) testing/e2e/scripts/relay-health-monitor.ps1
- [ ] T024 [US2] Create Pester unit tests for relay (start, stop, health check, latency verification, restart logic) testing/e2e/scripts/tests/relay-server.tests.ps1
- [ ] T025 [US2] Integrate relay into Playwright fixture hooks (auto-start before test suite, cleanup after) testing/e2e/playwright.config.ts

**Test Deliverables**:
- Unit tests (Pester): 12+ test cases covering relay lifecycle, latency guarantees, restart recovery
- Validation tests (Playwright): relay-connectivity.spec.ts validates <100ms latency, bidirectional packet delivery, health check recovery

---

## Phase 5: US3 Per-Suite Environment Reset (Priority: P2)

### User Story Goal
Reset emulator state between test suites (clear app data, reset network state, drain NDI sources) so tests don't interfere with each other.

### Independent Test Criteria
✅ **PASS**: Running app + modifying state (settings, sources) → execute reset → verify factory state  
✅ **FAIL**: Reset script fails or state is not cleared

### Implementation Tasks

- [ ] T026 [US3] Implement Reset-EmulatorState function (clear app data, stop discovery, drain sources per-emulator) testing/e2e/scripts/reset-emulator-state.ps1
- [ ] T027 [US3] Create Pester unit tests for reset (verify state clearing, bidirectional reset, idempotency) testing/e2e/scripts/tests/reset-emulator-state.tests.ps1
- [ ] T028 [US3] Integrate reset into test suite lifecycle hooks (call after each Playwright test suite completes) testing/e2e/playwright.config.ts

**Test Deliverables**:
- Unit tests (Pester): 8+ test cases covering state clearing, verification, error recovery
- Integration: Reset is called automatically between feature 009 test suite and new latency measurement suites

---

## Phase 6: US4 Artifact Recovery & CI/CD Integration (Priority: P2)

### User Story Goal
Automatically collect emulator logs, screen recordings, and NDI diagnostic data to host filesystem so test failures can be analyzed without manual device introspection.

### Independent Test Criteria
✅ **PASS**: Run tests → capture artifacts to host filesystem → verify defined artifacts collected → summary report generated with valid paths  
✅ **FAIL**: Artifact collection fails or files are missing

### Implementation Tasks

- [ ] T029 [P] [US4] Implement Collect-Logcat function (ADB logcat, last 500 lines per device, save to file) testing/e2e/scripts/collect-test-artifacts.ps1
- [ ] T030 [P] [US4] Implement Collect-ScreenRecording function (ADB screenrecord artifacts, save MP4 to host filesystem) testing/e2e/scripts/collect-test-artifacts.ps1
- [ ] T031 [P] [US4] Implement Collect-Diagnostics function (NDI SDK logs, relay metrics, provisioning logs) testing/e2e/scripts/collect-test-artifacts.ps1
- [ ] T032 [US4] Implement Generate-ArtifactManifest function (JSON summary: artifact paths, device state, provisioning metrics, checksums) testing/e2e/scripts/collect-test-artifacts.ps1
- [ ] T033 [US4] Create Pester unit tests for collection (verify artifacts exist, JSON manifest valid, disk space handling) testing/e2e/scripts/tests/collect-test-artifacts.tests.ps1

**Test Deliverables**:
- Unit tests (Pester): 10+ test cases covering logcat, screen recording, diagnostics, manifest generation
- Integration: CI/CD job stores artifacts in `testing/e2e/artifacts/` and uploads via GitHub Actions artifact action

---

## Phase 7: US5 Comprehensive Diagnostic Dashboard Documentation (Priority: P3)

### User Story Goal
Provide comprehensive documentation of emulator provisioning, relay connectivity, test coverage, and latency trends.

### Independent Test Criteria
✅ **PASS**: New developer reads docs/dual-emulator-setup.md and provisions dual emulator locally in <15 min  
✅ **FAIL**: Documentation is incomplete or instructions fail

### Implementation Tasks

- [ ] T034 [US5] Create comprehensive infrastructure guide docs/dual-emulator-setup.md (local setup, CI/CD integration, troubleshooting, architecture diagram)
- [ ] T035 [US5] Create troubleshooting guide docs/dual-emulator-setup.md appendix (common failures, recovery steps, latency debugging, relay restart procedures)

**Deliverables**:
- Infrastructure guide with architecture diagrams, local setup steps, CI/CD integration instructions
- Troubleshooting guide with common failure modes and recovery procedures

---

## Final Phase: Polish & Cross-Cutting Concerns

- [ ] T036 [P] Integrate provisioning + relay + artifact collection into GitHub Actions e2e workflow .github/workflows/e2e-dual-emulator.yml
- [ ] T037 [P] Create end-to-end validation test (provision → relay → run latency test → collect artifacts → verify success) testing/e2e/tests/support/e2e-infrastructure.spec.ts
- [ ] T038 Verify feature 009 (latency measurement) validates dual-emulator infrastructure with 95% stability specs/009-measure-ndi-latency/tasks.md reference + validation

---

## Test Strategy Summary

### Unit Tests (Pester PowerShell)
**Location**: `testing/e2e/scripts/tests/`

1. **provision-dual-emulator.tests.ps1** (US1)
   - Test provisioning happy path (start, boot, NDI install)
   - Test idempotency (reuse running emulator)
   - Test timeout handling (emulator boot >90 sec)
   - Test port conflicts (ADB port already bound)
   - Test NDI install failures

2. **relay-server.tests.ps1** (US2)
   - Test relay startup and route configuration
   - Test latency guarantees (<100ms round-trip)
   - Test health check and automatic restart
   - Test bidirectional packet delivery
   - Test graceful shutdown and port release

3. **reset-emulator-state.tests.ps1** (US3)
   - Test state clearing (app data, NDI sources)
   - Test per-emulator reset (both devices)
   - Test idempotency (reset twice without error)

4. **collect-test-artifacts.tests.ps1** (US4)
   - Test logcat collection (correct line count)
   - Test screen recording capture (MP4, expected size)
   - Test diagnostics collection (NDI logs, relay metrics)
   - Test JSON manifest generation and validation

### Validation Tests (Playwright)
**Location**: `testing/e2e/tests/support/`

1. **dual-emulator-provisioning.spec.ts** (US1)
   - Validate provisioning status JSON structure
   - Verify ADB device discovery after provisioning
   - Confirm NDI SDK APK installed and version correct

2. **relay-connectivity.spec.ts** (US2)
   - Send test packets through relay
   - Measure round-trip latency (must be <100ms)
   - Verify bidirectional communication

3. **e2e-infrastructure.spec.ts** (Final gate)
   - Full provisioning → relay → latency test → artifact collection flow
   - Verify success rate ≥95% in CI/CD runner environment

### Feature 009 Integration
- Feature 009 (measure-ndi-latency) acts as consumer of dual-emulator infrastructure
- If feature 009 latency measurement succeeds consistently, infrastructure is validated
- Reference: specs/009-measure-ndi-latency/tasks.md must update task dependencies to include feature 010 completion gate

---

## Success Criteria (Acceptance Testing)

### Completion Gate (Before shipping M1 MVP)

- [ ] **SC-001**: Provisioning completes in <2 min (first boot) or <10 sec (reuse)
  - Validation: Run provisioning script 10 times sequentially, average <10 sec for reuse instances
  
- [ ] **SC-002**: Relay <100ms round-trip latency
  - Validation: Run relay health check 100 times, 95th percentile <100ms
  
- [ ] **SC-003**: 95% of multi-device test suites pass without manual retry
  - Validation: Run feature 009 latency tests 20 times in CI/CD, 19/20 pass
  
- [ ] **SC-004**: All provisioning/relay/artifact code covered by unit tests (≥80% line coverage)
  - Validation: Run Pester tests with coverage reporting
  
- [ ] **SC-005**: Artifact collection captures all defined artifacts (logcat, recordings, diagnostics)
  - Validation: Run test suite, verify testing/e2e/artifacts/ contains all expected files
  
- [ ] **SC-006**: Infrastructure overhead <60 sec per test suite (provisioning + relay startup + reset)
  - Validation: Measure end-to-end time from script invocation to first test execution

---

## Task Count Summary

- **Phase 2 (Foundational)**: 5 tasks (T001-T005)
- **Phase 3 (US1 Provisioning)**: 10 tasks (T006-T015)
- **Phase 4 (US2 Relay)**: 10 tasks (T016-T025)
- **Phase 5 (US3 Reset)**: 3 tasks (T026-T028)
- **Phase 6 (US4 Collection)**: 5 tasks (T029-T033)
- **Phase 7 (US5 Documentation)**: 2 tasks (T034-T035)
- **Final Phase (Polish & E2E)**: 3 tasks (T036-T038)

**Total: 38 tasks**

---

## Related Specifications

- **Spec**: specs/010-dual-emulator-setup/spec.md (14 functional requirements, 8 success criteria)
- **Plan**: specs/010-dual-emulator-setup/plan.md (technical context, design decisions)
- **Data Model**: specs/010-dual-emulator-setup/data-model.md (7 core entities)
- **Contracts**: specs/010-dual-emulator-setup/contracts/ (provisioning-api.md, relay-api.md, artifact-collection-api.md)
- **Research**: specs/010-dual-emulator-setup/research.md (design decisions locked)
- **Quickstart**: specs/010-dual-emulator-setup/quickstart.md (local dev + CI/CD setup)

---

## Implementation Notes

### Design Principles
- **Simplicity First**: PowerShell orchestration over complex frameworks; raw TCP sockets over protocol overhead
- **Idempotency**: Provisioning script is safe to run multiple times on same device
- **Health-Aware**: Relay server auto-restarts on failure; emulator state is always queried fresh (not cached)
- **Failure Recovery**: All errors are captured and reported in provisioning/artifact manifests
- **Local-First**: Artifact collection to host filesystem enables local debugging before CI/CD uploads

### Known Constraints
- ADB port binding: emulator-5554 → port 5554, emulator-5556 → port 5556 (fixed)
- Relay listening port: configurable in 15000-15010 range (default 15000)
- Emulator boot timeout: 90 seconds (SC-001 requirement)
- Relay auto-restart: max 3 retries before failing the test suite
- Artifact storage: host filesystem only (testing/e2e/artifacts/), no cloud/DB storage

### Dependency Notes
- **Upstream**: Feature 009 (latency measurement) is consumer of dual-emulator infrastructure
- **Downstream**: Feature 011+ (advanced NDI scenarios) may build on dual-emulator relay patterns
- **Open**: Dashboard/monitoring (US5) deferred to future archive after infrastructure stabilizes

---

**Generated**: 2026-03-23  
**Next Step**: Await user approval → Begin Phase 2 foundational tasks (T001-T005)
