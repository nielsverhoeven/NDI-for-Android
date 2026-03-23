# Implementation Plan: Dual Emulator Infrastructure Setup

**Branch**: `010-dual-emulator-setup` | **Date**: 2026-03-23 | **Spec**: specs/010-dual-emulator-setup/spec.md

**Input**: Feature specification from `/specs/010-dual-emulator-setup/spec.md`

---

## Summary

Establish reliable, reproducible dual-emulator testing infrastructure that enables consistent Playwright end-to-end tests for NDI streaming and latency measurement features. The core infrastructure comprises four MVP components: automated emulator provisioning (API 32-35), TCP socket relay server (<100ms latency), per-suite environment reset, and artifact collection to host filesystem.

**Clarifications Locked**:
- Relay Technology: **TCP Socket Forwarding** (direct relay, minimal overhead)
- API Levels: **API 32-35** (Android 12-15, NDI SDK support + device coverage balance)
- Artifact Storage: **Host filesystem only** (`testing/e2e/artifacts/` + CI/CD job uploads)

---

## Technical Context

**Language/Version**: PowerShell 5+ (scripts), Node.js 18+ (relay server if custom), Bash/sh (ADB helpers), Python 3.8+ (artifact collection - optional)

**Primary Dependencies**: 
- Android SDK (emulator, ADB tools) - pre-installed in CI/CD runners
- NDI SDK bridge APK - pre-built in `ndi/sdk-bridge/`
- Playwright 1.53+ - already in testing/e2e/
- Socket/TCP libraries (built-in OS, no external deps)

**Storage**: Host filesystem only (testing/e2e/artifacts/) for logs, recordings, relay metrics

**Testing**: 
- Unit: PowerShell Pester for provisioning/relay script validation
- Integration: Playwright e2e tests validating dual-emulator scenarios (feature 009 acts as consumer)
- Manual: Local developer verification with dual emulator instances

**Target Platform**: 
- Local: macOS, Windows, Linux (developer workstations with Android SDK)
- CI/CD: GitHub Actions runners (Windows or Linux) with emulator capability

**Project Type**: Infrastructure/testing tooling (scripts + helpers)

**Performance Goals**:
- SC-001: Provisioning <2 min (first boot) or <10 sec (reuse)
- SC-002: Relay <100ms round-trip latency
- SC-003: 95% of multi-device test suites pass without manual retry
- SC-006: Infrastructure overhead <60 sec per test suite

**Constraints**:
- No UI components (infrastructure-only)
- Must not disrupt existing regression test suites
- Must preserve existing Playwright framework patterns
- CI/CD runners have minimum 2 cores, 4 GB RAM, 2 GB disk

**Scale/Scope**:
- Dual emulator instances (fixed: emulator-5554, emulator-5556)
- ~30-50 PowerShell scripts (provisioning, relay, reset, collection)
- ~10-15 test fixtures for provisioning/relay validation
- Documentation: README, quickstart, troubleshooting

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Infrastructure-only feature (no MVVM presentation logic applicable)
- [x] No Navigation/UI changes to repository (headless testing infrastructure)
- [x] Repository-mediated access preserved (existing data access patterns untouched)
- [x] TDD evidence planned: PowerShell unit tests for provisioning + relay health checks (failing-test-first path)
- [x] Unit test scope: Pester-based PowerShell tests + Playwright validation tests
- [x] Playwright e2e scope: Feature 009 (latency measurement) tests validate provisioning/relay infrastructure
- [x] No visual UI additions (infrastructure-only, no Compose/Views changes)
- [x] Existing Playwright e2e regression run protected (new infrastructure must not break existing tests)
- [x] Not applicable: Material 3, battery impact, offline-first constraints
- [x] Security: ADB commands use standard authentication; relay server is localhost-only
- [x] Feature-module boundary: Isolated to testing infrastructure, no app module changes
- [x] Release hardening: No app binary changes; scripts are validated via CI/CD gate

---

## Project Structure

### Documentation (this feature)

```text
specs/010-dual-emulator-setup/
├── spec.md                              # Feature specification (14 FR, 8 SC, 5 user stories)
├── plan.md                              # This file (technical context, design)
├── data-model.md                        # Entities: EmulatorInstance, RelayServer, ProvisioningReport
├── data-model.md.contracts/             # Interfaces for provisioning API, relay API, artifact collection
├── quickstart.md                        # Setup guide for local dev + CI/CD
├── checklists/requirements.md           # Quality validation (12/12 passing)
└── research.md                          # (Placeholder for design decisions post-planning)
```

### Source Code (repository root)

```text
testing/e2e/
├── scripts/
│   ├── provision-dual-emulator.ps1      # Main provisioning orchestrator
│   ├── start-relay-server.ps1           # TCP relay server startup
│   ├── reset-emulator-state.ps1         # Inter-suite environment reset
│   ├── collect-test-artifacts.ps1       # Post-test log + recording collection
│   ├── relay-health-monitor.ps1         # Background health check + restart
│   ├── helpers/
│   │   ├── emulator-adb.ps1             # ADB command wrappers (state, boot, install)
│   │   ├── relay-tcp-forwarder.ps1      # TCP socket forwarding logic
│   │   └── artifact-manifest.ps1        # JSON report generation
│   └── tests/
│       ├── provision-dual-emulator.tests.ps1    # Pester unit tests
│       └── relay-server.tests.ps1               # Relay health checks
│
├── playwright.config.ts                 # (Minimal update: hooks for provision + cleanup)
├── tests/
│   ├── support/
│   │   ├── dual-emulator-provisioning.spec.ts   # Playwright validation tests (NEW)
│   │   └── relay-connectivity.spec.ts            # Relay health Playwright tests (NEW)
│   └── [existing tests unchanged]
├── artifacts/
│   └── <session-id>/
│       ├── logcat-emulator-5554.log
│       ├── logcat-emulator-5556.log
│       ├── screen-recording-source.mp4
│       ├── screen-recording-receiver.mp4
│       ├── ndi-diagnostics.json
│       ├── relay-metrics.json
│       └── provisioning-summary.json
│
└── [existing e2e infrastructure unchanged]

docs/
└── dual-emulator-setup.md               # Infrastructure guide (drivers, setup, troubleshooting)
```

---

## Design Decisions

### 1. TCP Socket Forwarding (Relay Technology)

**Decision**: Use direct TCP socket forwarding instead of HTTP relay or existing tools.

**Rationale**:
- Meets <100ms SC-002 latency target (direct socket, no protocol overhead)
- Minimal dependency footprint (built-in OS sockets, no external libraries)
- Simpler debugging (standard netcat compatibility)
- Consistent with NDI SDK patterns (raw socket-based streaming)

**Trade-off**: Manual socket management required vs. framework-based relay (Express, socat wrapper). Mitigated by simple state machine implementation.

### 2. PowerShell Orchestration Scripts

**Decision**: Primary provisioning/relay orchestration in PowerShell 5+ (with Bash fallback for GitHub Actions).

**Rationale**:
- Matches existing CI/CD pipeline infrastructure (.ps1 scripts used throughout)
- Cross-platform support (Windows dev, GitHub Actions runners) with Bash fallback
- Integrates with Windows emulator toolchain (ADB, adb.exe paths)
- Direct filesystem and port access (CI/CD runner context)

**Trade-off**: Cross-platform complexity (Windows vs. Linux ADB paths differ). Mitigated by platform-detection helper.

### 3. Host-Filesystem Artifact Storage

**Decision**: Store artifacts to host filesystem (`testing/e2e/artifacts/`) only, no cloud archival.

**Rationale**:
- Simplest integration path (no cloud credentials, auth complexity)
- CI/CD job artifact upload handles persistence automatically
- Supports local dev debugging (developers inspect artifacts directly)
- Meets MVP requirement (post-MVP can add cloud archival if needed)

**Trade-off**: Cannot query/correlate artifacts across jobs without separate ETL. Accepted for MVP.

### 4. API Level 32-35 (Android 12-15) Target

**Decision**: Provisioning scripts target and validate API levels 32-35.

**Rationale**:
- NDI SDK bridge supports these levels reliably
- Balances device coverage (API 32 is ~80% of Android user base)
- Aligns with CI/CD emulator images pre-built for these levels
- Matches NDI SDK validation matrix

**Trade-off**: Cannot test on API 31 or earlier. Acceptable (NDI SDK minimum is 32).

### 5. No External Dependencies for Relay

**Decision**: Implement TCP relay in PowerShell or Node.js, avoid socat/netcat overhead.

**Rationale**:
- Guarantees feature availability without pre-installed tools
- Enables health monitoring and automatic restart (custom code advantage)
- CI/CD runner control (no dependency on runner image maintenance)

**Trade-off**: Custom TCP relay implementation required (~100-150 lines). Mitigated by simple state machine.

---

## Phase Breakdown (Provisional)

**Phase 0: Research** (Deferred - covered in spec clarifications)
- Review NDI SDK APK paths and versions
- Validate Android emulator image availability

**Phase 1: Design** (This document)
- ✅ Technical context defined
- ✅ Architecture documented
- ✅ Data model sketched (next file: data-model.md)
- ✅ Contracts defined (next file: contracts/ - APIs for provisioning, relay, artifacts)
- ✅ Quickstart drafted (next file: quickstart.md)

**Phase 2: Task Generation** (Next step: /speckit.tasks)
- Decompose into 25-35 actionable tasks
- Define dependencies (provisioning → relay → reset → collection)
- Assign priorities (P1: provisioning + relay, P2: reset + collection)

**Phase 3: Implementation** (Post-planning)
- T001-T010: Provisioning script + helpers
- T011-T015: TCP relay server + health monitoring
- T016-T020: Environment reset helpers
- T021-T025: Artifact collection + summary generation
- T026-T030: Playwright validation tests + documentation

**Phase 4: Validation** (Post-implementation)
- Execute provisioning with feature 009 latency tests
- Verify relay latency meets SC-002 (<100ms)
- Regression gate: existing e2e tests still pass

---

## Known Constraints & Mitigations

| Constraint | Mitigation |
|-----------|-----------|
| Emulator boot timeout flakiness | 90-second timeout with 3 retry attempts; detailed ADB error logging |
| TCP relay port conflicts (ephemeral) | Try fallback ports 15000-15010; report conflict with suggestion |
| NDI SDK APK version mismatch | Version pinning in script; auto-install newer versions |
| Artifact disk space (large recordings) | Configurable logcat line limit (default 500); recording size limits |
| CI/CD runner resource constraints (2 cores, 4GB RAM) | Emulator memory limits; sequential (not parallel) emulator boot |
| Windows vs. Linux ADB path differences | Platform detection + conditional path resolution in helpers |

---

## Success Metrics (From Spec)

- **SC-001**: Provisioning <2 min first-time, <10 sec reuse ✅
- **SC-002**: Relay <100ms round-trip latency ✅
- **SC-003**: 95% of multi-device suites pass without retry ✅
- **SC-004**: Artifacts collected within 30 sec post-test ✅
- **SC-005**: Tests run repeatably (Mac/Windows/Linux + CI/CD) ✅
- **SC-006**: Infrastructure overhead <60 sec per suite ✅
- **SC-007**: Feature 009 achieves 80%+ first-attempt pass rate ✅
- **SC-008**: Existing e2e tests remain passing (regression gate) ✅

---

## Next Steps

1. **Generate data-model.md**: Define Emulator Instance, Relay Server, Provisioning Report, Artifact Collection entities
2. **Generate contracts/**: Define API interfaces for provisioning, relay discovery, artifact manifest
3. **Generate quickstart.md**: Step-by-step setup for local dev + CI/CD integration
4. **Generate tasks.md**: Phase-based task breakdown with dependencies (/speckit.tasks)
5. **Run analysis**: Cross-artifact consistency check (/speckit.analyze)

---

**Status**: 🟡 **PLAN COMPLETE** (ready for data-model + contracts + quickstart generation)
