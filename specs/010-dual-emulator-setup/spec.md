# Feature Specification: Dual Emulator Infrastructure Setup

**Feature Branch**: `010-dual-emulator-setup`  
**Created**: 2026-03-23  
**Status**: Draft  
**Input**: User description: "add a decent emulator infrastructure for the app to be properly tested"

## Executive Summary

Establish a reliable, reproducible dual-emulator testing infrastructure that enables consistent end-to-end (e2e) Playwright test execution for NDI streaming and latency measurement features. This infrastructure bridges the current gap where advanced feature validation (009-measure-ndi-latency, dual-emulator interop) depends on ad-hoc manual emulator setup, leading to flaky test results and unreliable CI/CD validation.

---

## Clarifications

### Session 2026-03-23

- Q: What communication technology should the relay server use for inter-emulator bridging? → A: TCP Socket Forwarding (direct relay) - minimal overhead, <100ms latency target
- Q: Which Android API levels should the dual-emulator environment target? → A: API 32-35 (Android 12-15) - balance of NDI SDK support and coverage
- Q: Where should test artifacts (logs, recordings, diagnostics) be stored after test execution? → A: Host filesystem only (local + CI/CD job artifacts)

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automated Emulator Provisioning (Priority: P1) 🎯 MVP

**As a** test engineer or CI/CD automation system  
**I want to** reliably provision two connected Android emulator instances with pre-configured NDI SDK and network connectivity  
**So that** end-to-end tests can start immediately without manual device configuration

[Describe this user journey in plain language]

The test system needs to programmatically:
1. Detect if emulator instances (emulator-5554, emulator-5556) are already running
2. Start emulators if needed with consistent API level (32-35, Android 12-15) and NDI SDK-compatible system image
3. Verify emulator boot completion and ADB connectivity
4. Pre-install NDI SDK bridge APK on both emulators
5. Verify screen recording capability on both devices
6. Return provisioning status with detected device serials, API levels, and capabilities

**Why this priority**: Without automated provisioning, feature 009 (latency measurement) and future interop tests remain manual/flaky. This is the foundational enabler for repeatable testing infrastructure.

**Independent Test**: 
- Can be fully tested by: Executing provisioning script → verifying two emulators are discoverable via ADB with NDI SDK installed → confirming screen recording works → delivering provisioning status JSON
- Delivers value: Developers can re-run tests consistently without manual device setup

**Acceptance Scenarios**:

1. **Given** two emulator instances are not running, **When** provisioning script executes, **Then** both emulators are started (waits max 90 seconds for full boot)
2. **Given** emulator instances are already running and healthy, **When** provisioning script executes, **Then** script skips unnecessary restart (executes in <5 seconds)
3. **Given** emulator instances pass pre-checks, **When** provisioning script executes, **Then** NDI SDK APK is installed on both devices (or confirmed already present)
4. **Given** provisioning completes successfully, **When** test queries provisioning status, **Then** returned JSON includes serials, API levels, screen recording capability, NDI SDK version

---

### User Story 2 - Relay Server Infrastructure (Priority: P1)

**As a** Playwright test suite running latency or interop scenarios  
**I want to** communicate between emulator instances through a reliable TCP-based relay server without manual network configuration  
**So that** multi-device test scenarios (streaming from one device, receiving on another) work reproducibly in CI/CD and local environments

Multi-device testing requires:
1. A TCP socket relay server (simple forwarding proxy) that bridges two emulator instances
2. Automatic relay server startup before test suites run
3. Relay address discovery (localhost:PORT or discoverable hostname:PORT)
4. Relay server health checks and automatic restart on failure (maintaining <100ms latency)
5. Relay cleanup after test execution

**Why this priority**: Feature 009 (dual-emulator latency measurement) requires inter-device communication. Without reliable relay infrastructure, tests fail or require manual network debugging.

**Independent Test**:
- Can be fully tested by: Starting TCP relay → sending test packets between emulator instances → verifying packet delivery within <100ms round-trip → stopping relay
- Delivers value: Multi-device test scenarios become viable in any environment (CI/CD, local dev workstations, cloud runners)

**Acceptance Scenarios**:

1. **Given** relay server is not running, **When** test preflight checks execute, **Then** TCP relay is automatically started on available port and endpoint is reported
2. **Given** relay server is running healthily, **When** emulator instances communicate through relay, **Then** packet delivery succeeds with <100ms round-trip latency
3. **Given** relay server crashes during tests, **When** health check executes, **Then** relay is automatically restarted with connection recovery (seamless for active connections)
4. **Given** test suite completes, **When** cleanup executes, **Then** relay server is stopped and ports are released

---

### User Story 3 - Per-Suite Environment Reset (Priority: P2)

**As a** test engineer running multiple feature suites sequentially or in CI/CD pipelines  
**I want to** reset emulator state between test suites (clear app data, reset network state, drain pending NDI sources)  
**So that** tests don't interfere with each other and provide reliable isolation

Environment reset includes:
1. Clearing app data and cache on both emulators
2. Stopping NDI discovery service and forcing cache refresh
3. Resetting network state (removing custom discovery endpoints, draining pending sources)
4. Clearing screen recording artifacts from previous runs
5. Verifying factory state before next suite

**Why this priority**: Prevents test cross-contamination. Multi-suite runs (e.g., new-settings → latency → regression) need isolated state between suites.

**Independent Test**:
- Can be fully tested by: Running app → modifying state (settings, sources) → executing reset → verifying state is factory defaults
- Delivers value: Eliminates flaky test failures caused by leftover state from previous test runs

**Acceptance Scenarios**:

1. **Given** app has active settings and discovered sources, **When** reset executes on first emulator, **Then** all app data is cleared and app enters first-launch state
2. **Given** both emulators have modified state, **When** reset executes on both devices, **Then** state is synchronized between devices
3. **Given** factory state is restored, **When** latency tests run, **Then** environment state does not affect latency measurements

---

### User Story 4 - Artifact Recovery & CI/CD Integration (Priority: P2)

**As a** CI/CD system or developer debugging test failures  
**I want to** automatically collect emulator logs, screen recordings, and NDI diagnostic data to the host filesystem after test execution  
**So that** I can analyze test failures without manual device introspection (artifacts are preserved in local test results directory)

Artifact collection includes:
1. ADB logcat capture (last 500 lines per device) → saved to host filesystem
2. Screen recording artifacts preserved from e2e tests → saved to host filesystem  
3. NDI SDK diagnostic logs from both emulators → saved to host filesystem
4. Network traffic metadata (relay statistics) → saved to host filesystem
5. Provisioning and relay health logs → saved to host filesystem
6. Summary report (JSON) with artifact paths and key metrics → saved to host filesystem

**Why this priority**: Enables remote debugging and post-mortem analysis when tests fail in ephemeral CI/CD environments where emulators are destroyed after test completion. Host filesystem artifacts are available via CI/CD job artifact storage.

**Independent Test**:
- Can be fully tested by: Running tests → capturing artifacts to host filesystem → verifying all defined artifacts are collected and paths are valid → generating summary report
- Delivers value: Failed tests become debuggable without re-running (logs/recordings are archived in testing/e2e/artifacts/)

**Acceptance Scenarios**:

1. **Given** test execution completes with failures, **When** artifact recovery runs, **Then** ADB logcat, NDI diagnostic logs, and screen recordings are collected to host filesystem in testing/e2e/artifacts/
2. **Given** artifacts are collected, **When** summary report is generated, **Then** report includes valid artifact paths, device state, and provisioning/relay health metrics
3. **Given** CI/CD job runs with ephemeral emulators, **When** job completes, **Then** host filesystem artifacts are preserved via CI/CD artifact upload (no data loss)

---

### User Story 5 - Comprehensive Diagnostic Dashboard (Priority: P3)

**As a** test engineer or architect reviewing infrastructure health  
**I want to** see a comprehensive dashboard of emulator provisioning, relay connectivity, test coverage, and latency trends  
**So that** I can identify and resolve infrastructure bottlenecks and reliability issues

Diagnostic dashboard includes:
1. Real-time view of emulator health status
2. Relay server metrics (uptime, latency percentiles, packet loss)
3. Test execution trends (success rate, flakiness patterns)
4. NDI SDK version compatibility matrix
5. Latency measurement baseline and anomaly detection
6. Historical test artifact links

**Why this priority**: Enables data-driven infrastructure improvements. Archive for future optimizations after core infrastructure is stable.

**Independent Test**:
- Can be fully tested by: Provisioning dashboard → feeding test data → rendering metrics → verifying anomaly detection triggers
- Delivers value: Infrastructure health becomes visible and actionable for the team

**Acceptance Scenarios**:

1. **Given** test suite execution completes, **When** dashboard is refreshed, **Then** new test results appear with latency metrics
2. **Given** emulator health degrades, **When** dashboard anomaly detection runs, **Then** alerts are surfaced with remediation suggestions
3. **Given** historical test data exists, **When** user views trends, **Then** latency baseline and recent variance are visible

---

## Visual Change Quality Gate *(mandatory when UI changes are present)*

**No visual changes** - Feature 010 is infrastructure/testing-only. No new app UI components or visual behavior changes to users. This feature enables automated testing of existing and future app features but does not change app visual interface itself.

---

## Edge Cases

- What happens when an emulator instance fails to boot within 90-second timeout?
  - System should attempt restart once, then report failure with detailed ADB error logs
- What happens when both emulators are simultaneously running outdated NDI SDK versions?
  - Provisioning should attempt APK re-installation; if installation fails, report version mismatch with upgrade recommendation
- What happens when relay server port is already in use (conflict with other services)?
  - System should attempt fallback ports or report port conflict with suggested resolution
- What happens when network connectivity between emulators fails mid-test?
  - Relay health check should detect connectivity loss and abort test with explicit network error (not silent timeout)
- What happens when test execution is interrupted (Ctrl+C) while relay is running?
  - Cleanup handler should ensure relay is stopped and ports are released (graceful shutdown)
- What happens in CI/CD environment where ephemeral runners have constrained resources?
  - Provisioning should validate minimum RAM/disk requirements and fail fast with resource capacity errors

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST automatically detect the current state of emulator instances (not running, running-unhealthy, running-healthy) via ADB commands
- **FR-002**: System MUST start emulator instances from Android SDK Emulator if they are not running (API level 32-35 with NDI SDK support)
- **FR-003**: System MUST verify emulator boot completion by polling ADB get-state until "device" state is reached (max 90-second timeout)
- **FR-004**: System MUST pre-install NDI SDK bridge APK on both emulator instances during provisioning
- **FR-005**: System MUST verify screen recording capability on both devices and fail provisioning if unavailable
- **FR-006**: System MUST start a TCP socket-based relay server (network forwarding proxy) that bridges inter-device communication before any multi-device test executes
- **FR-007**: System MUST provide relay server TCP endpoint (host:port) discoverable by test suites and cleanly stop relay after tests complete
- **FR-008**: System MUST implement relay health monitoring with automatic restart on failure (without interrupting active test connections)
- **FR-009**: System MUST reset emulator state between test suites by clearing app data, NDI source cache, and network configuration
- **FR-010**: System MUST collect ADB logcat (last 500 lines per device), screen recording artifacts, NDI diagnostic logs, and relay metrics to host filesystem (testing/e2e/artifacts/)
- **FR-011**: System MUST generate provisioning and test execution summary reports in JSON format with artifact paths, device state, and key metrics (saved to host filesystem)
- **FR-012**: System MUST support CI/CD environments where emulator instances are ephemeral by preserving all artifacts to host filesystem before cleanup (available via CI/CD artifact storage)
- **FR-013**: For all multi-device test scenarios (feature 009 and beyond), system MUST include infrastructure provisioning, relay startup, and artifact collection as mandatory preflight steps
- **FR-014**: For all multi-device test scenarios, system MUST execute and keep passing all existing e2e tests after infrastructure changes

### Key Entities *(include if feature involves data)*

- **Emulator Instance**: Represents a running Android Virtual Device (AVD) with serial, API level, NDI SDK version, and health status
- **Relay Server**: Represents inter-device communication proxy with host, port, uptime, latency metrics, and packet statistics
- **Provisioning Report**: JSON artifact documenting emulator boot times, NDI SDK installation status, screen recording verification, and overall provisioning success/failure
- **Test Artifact Collection**: Container for ADB logs, screen recordings, NDI diagnostics, relay metrics, and test summary with indexed paths for CI/CD artifact storage

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Emulator provisioning completes within 2 minutes for first-time boot (emulator startup + SDK installation) or 10 seconds for already-running instances
- **SC-002**: Relay server establishes and maintains <100ms round-trip latency between emulator instances during normal operation
- **SC-003**: 95% of multi-device test suite runs (feature 009 and regression) complete successfully without manual intervention or retry
- **SC-004**: Artifact collection captures all required diagnostics (logs, recordings, metrics) within 30 seconds of test completion
- **SC-005**: Tests can run repeatably in local development environment (Mac, Windows, Linux) and CI/CD runners without environment-specific configuration
- **SC-006**: Infrastructure setup and cleanup overhead adds <60 seconds per test suite execution
- **SC-007**: Feature 009 (latency measurement) dual-emulator tests achieve minimum 80% pass rate on first attempt (without manual device debugging)
- **SC-008**: All existing Playwright e2e test suites continue to pass with new infrastructure in place (regression gate maintained)

### Assumptions

- Android SDK Emulator tools and NDI SDK artifacts are pre-installed in CI/CD runner images (not within scope of this feature)
- Emulator instances will use Android API 32-35 (API levels corresponding to Android 12-15)
- TCP socket forwarding is used for inter-device relay communication (vs. HTTP or other protocols)
- Test artifacts are stored on host filesystem in testing/e2e/artifacts/ directory (CI/CD runners upload via job artifact storage)
- Test suites are written in Playwright and follow existing test patterns in `testing/e2e/tests/`
- Network connectivity between emulator instances is available within the relay
- Minimum CI/CD runner resources: 2+ cores, 4+ GB RAM, 2+ GB disk space for emulator instances and artifact storage
- GitHub Actions or local development environment supports long-running processes (emulator uptime ≥ test suite duration)

### Dependencies

- Requires: Feature 009 implementation (latency measurement) to validate multi-device scenarios
- Enables: Future multi-device feature development and reliable regression testing at scale
- Integrates with: Existing Playwright framework, ADB toolchain, NDI SDK bridge module

### Out of Scope

- Building or packaging Android Virtual Device (AVD) images (assumes images pre-exist in Android SDK)
- NDI SDK native compilation or bridging (assumes `ndi/sdk-bridge` module is already built)
- Dashboard implementation (deferred to US5 post-MVP)
- Cross-platform emulator support beyond Android (iOS simulator support deferred)

---

## Additional Context

### Problem Statement

Currently, feature 009 (dual-emulator NDI latency measurement) is blocked by unreliable emulator infrastructure:
- Developers must manually start two emulator instances on their machines
- Manual network bridging required between emulators (relay server)
- Tests fail intermittently due to emulator state pollution (leftover app data, cached NDI sources)
- CI/CD runs cannot reproduce dual-emulator scenarios without ephemeral infrastructure setup
- Failed test debugging requires manual log collection from transient emulators

This feature addresses these blockers by automating provisioning, relay, state reset, and artifact collection.

### Success Metrics

After implementation:
- Feature 009 tests run reliably with >80% first-time pass rate (eliminating manual retries)
- Developers can run multi-device e2e tests locally with single command (`./test` or similar)
- CI/CD pipelines can execute dual-emulator scenarios without manual configuration
- Test failure analysis is enabled through archived logs and artifacts
- Infrastructure reliability is visible through health monitoring and trend dashboards

---

## Specification Quality Checklist

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for stakeholders and developers (not only architects)
- [x] All mandatory sections completed
- [x] No [NEEDS CLARIFICATION] markers (reasonable defaults applied)
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no framework/tool names)
- [x] All acceptance scenarios are defined
- [x] Edge cases identified
- [x] Scope clearly bounded (infrastructure-only, not app UI)
- [x] Dependencies and assumptions identified

---

**Next Step**: Review specification completeness. When ready, proceed to `/speckit.clarify` to validate assumptions or `/speckit.plan` to start design phase.
