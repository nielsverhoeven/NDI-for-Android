# Feature Specification: E2E App Pre-Installation Gate

**Feature Branch**: `011-e2e-app-preinstall`  
**Created**: 2026-03-23  
**Status**: Draft  
**Input**: User description: "adjust the e2e testing procedure to make sure that before running the tests, the latest version of the app is installed on the emulators"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fresh App Installation Before Test Run (Priority: P1)

A developer triggers the e2e test suite (locally or via CI/CD) expecting to validate current code changes. Before any test begins, the test harness automatically installs the latest built app on every target emulator. The developer does not need to manually manage app installations — the harness handles it as a guaranteed pre-flight step.

**Why this priority**: This is the core value of the feature. Without a guaranteed fresh installation, tests may silently run against stale code, producing misleading results. This story alone constitutes the minimum viable improvement.

**Independent Test**: Can be fully verified by triggering a test run immediately after a new build and confirming (via version identifier in the test report) that each emulator received the newly built app before any test executed.

**Acceptance Scenarios**:

1. **Given** a fresh app build exists and two emulators are running, **When** the test suite is launched, **Then** the latest app version is installed on both emulators before the first test starts.
2. **Given** an older version of the app is already installed on a running emulator, **When** the test suite is launched, **Then** the older version is replaced by the latest version before any test runs.
3. **Given** the app installation completes successfully, **When** the first test begins, **Then** the test report includes the installed app version identifier for each emulator.

---

### User Story 2 - Installation Failure Aborts the Run with a Clear Error (Priority: P2)

A developer or CI/CD operator runs the test suite, but the installation step encounters a problem — for example, the APK has not been built yet, or an emulator is unreachable. Rather than allowing the test suite to run against an unknown or missing app state, the system stops immediately and reports a specific, actionable error message identifying which device failed and the likely cause.

**Why this priority**: Silent failures (running tests with no app or the wrong app) are harder to diagnose than explicit failures. Fast, clear failure reporting reduces debugging time significantly and prevents misleading test results from entering CI/CD records.

**Independent Test**: Can be fully tested by launching a test run when no APK artifact exists and verifying that the run is aborted with a message referencing the missing build artifact, without any test cases executing.

**Acceptance Scenarios**:

1. **Given** no APK build artifact is present, **When** the test suite is launched, **Then** the run is aborted before any test executes and an error message indicates that a build is required.
2. **Given** an emulator is not reachable when installation is attempted, **When** the test suite is launched, **Then** the run is aborted and the error message identifies which emulator was unreachable.
3. **Given** installation fails on one emulator but succeeds on another, **When** the run is aborted, **Then** the error message identifies the specific failing device and the partial installation state.

---

### User Story 3 - Post-Installation Launch Verification (Priority: P3)

After installing the app, the test harness confirms that the app can actually be launched on each emulator before handing control to the test suite. This catches corrupt or incomplete installations that appear to succeed but would cause every test to fail when the app cannot open.

**Why this priority**: Installation success does not guarantee app launchability. Adding a launch verification step before tests begin prevents an entire test run from failing for a recoverable, infra-level reason rather than a code defect.

**Independent Test**: Can be tested by deliberately corrupting an APK installation (simulating a partial install) and verifying that the harness detects the non-launchable state and aborts with an appropriate error before any test executes.

**Acceptance Scenarios**:

1. **Given** the app was installed successfully, **When** the pre-flight verification step runs, **Then** the system confirms the app launches on each emulator before the first test begins.
2. **Given** installation completed but the app fails to launch, **When** launch verification runs, **Then** the test run is aborted with an error indicating a post-installation verification failure (distinct from an installation failure).
3. **Given** launch verification passes on all emulators, **When** the test suite begins, **Then** the test report records that pre-flight verification succeeded.

---

### Visual Change Quality Gate

No visual change — this feature modifies the test harness infrastructure only; it does not alter any visible UI behavior in the app or add new app screens.

### Edge Cases

- **Missing build artifact**: APK does not exist when the test harness runs — system must abort with a clear "build required" message.
- **Emulator not yet fully booted**: Emulator is starting but not ready to accept installations — system must wait up to a bounded timeout, then abort with a clear "emulator not ready" message.
- **App running during reinstallation**: The app is already open on the emulator when a new install is triggered — system must handle forced replacement without hanging.
- **Insufficient emulator storage**: Not enough disk space on the emulator to install the new APK — system must abort with a storage-related error message.
- **Corrupt/partial install**: Installation process exits without error but the app is in an unlaunchable state — caught by post-installation launch verification (US3).
- **Multiple emulators, partial failure**: Installation succeeds on one emulator but fails on another — system must abort the entire run, not allow partial execution.
- **Idempotent re-run**: Same APK version re-installed when already present — system must succeed without error and proceed normally.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Before any e2e test runs, the system MUST install the latest available app build on every registered target emulator as a mandatory pre-flight step.
- **FR-002**: The pre-flight installation step MUST replace any pre-existing app installation on each emulator, including any version already present, ensuring no stale state is retained.
- **FR-003**: If a required app build artifact is absent, the system MUST abort the test run before any test executes and present an actionable error that identifies the missing artifact and directs the operator to perform a build.
- **FR-004**: If installation fails on any registered emulator, the system MUST abort the entire test run before any test executes and report which device failed along with the failure reason.
- **FR-005**: After completing installation on each emulator, the system MUST verify that the installed app can be launched; if launch verification fails on any device, the test run MUST be aborted with a distinct post-installation verification error.
- **FR-006**: The system MUST log the installed app version identifier for each emulator and include these records in the test run summary report.
- **FR-007**: The pre-installation step MUST be idempotent — executing it multiple times with the same build artifact must succeed each time without requiring manual reset.
- **FR-008**: The pre-installation step MUST complete on each emulator within **60 seconds**; if this limit is exceeded, the device is treated as a failure and the run is aborted (aligned with SC-003).
- **FR-009**: The CI/CD workflow MUST include both (a) a build step that invokes the existing Gradle task to produce the debug APK artifact, and (b) the pre-installation step as a required gate that executes immediately after the build step and before test execution, with no mechanism to bypass either step.
- **FR-010**: The system MUST execute and keep passing all existing e2e test suites after the pre-installation step is integrated.
- **FR-011**: Before attempting installation on each registered emulator, the pre-flight gate MUST wait for the emulator to report readiness for up to the same bounded **60-second** timeout window; if the emulator does not become ready within that window, the run MUST abort with an emulator-not-ready error for that device.

### Key Entities

- **App Build Artifact**: The compiled, installable application package produced by the build step. The default artifact is the **debug APK variant** (`assembleDebug`); an alternate variant may be specified via an explicit override parameter but is not required for standard operation. The **version identifier** is the combination of `versionName` and `versionCode` values read from the installed artifact's metadata or ADB-logged output, used to confirm correct installation and populate per-device records in the Pre-Flight Report.
- **Emulator Device Registry**: The set of emulator instances that must receive the app before any test is run; each entry includes the device serial, reachability status, installation status, and launch verification result.
- **Pre-Flight Report**: A structured summary produced after the pre-installation phase documenting per-device installation status, version identifiers installed, launch verification outcomes, and elapsed time; included in the overall test run report.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of e2e test run attempts start with the confirmed correct app version freshly installed on every target emulator with no manual intervention required.
- **SC-002**: Zero test run failures are attributed to stale, missing, or incorrect app installations after this feature is in place.
- **SC-003**: The pre-installation phase completes within 60 seconds per emulator under normal conditions (emulator ready, artifact present).
- **SC-004**: Installation failures and missing-artifact errors produce messages that allow any developer or CI/CD operator to identify and resolve the cause within 5 minutes, without needing to inspect raw device logs.
- **SC-005**: All existing e2e test suites continue to pass at their pre-feature pass rates after the pre-installation gate is integrated.
- **SC-006**: The pre-installation step succeeds idempotently — re-running the test suite twice in a row without a rebuild produces the same outcome both times.

### Assumptions

- The CI/CD integration added by this feature includes a build step that invokes the existing Gradle `assembleDebug` task to produce the debug APK artifact; the build logic itself is not modified. Locally, the debug APK is used by default unless an operator explicitly overrides the variant.
- The test harness has device-level access to register app installations and query app launch status on emulators (via existing ADB-level tooling).
- Emulator lifecycle management remains covered by Feature 010, but the pre-installation gate performs a bounded readiness wait of up to 60 seconds per emulator before treating the device as unavailable.
- There is exactly one canonical app artifact per build; the harness does not need to resolve between multiple competing artifacts. The installed app version identifier is the combination of `versionName` and `versionCode` extracted from artifact metadata or ADB logcat output immediately after installation.
- App data clearing between test suites is handled by Feature 010 (FR-009); this feature only adds the installation guarantee, not per-test state reset.
- All registered emulators receive the same version of the app; variant or flavor selection is out of scope.
- CI/CD runner environments that run e2e tests provide filesystem access to the built artifact from a preceding build job.

### Dependencies

- **Requires**: Feature 010 (Dual Emulator Setup) — emulators must be provisioned, running, and accessible before the installation step runs.
- **Requires**: An existing build job/step that produces the installable app artifact before the test suite is triggered.
- **Integrates with**: Existing Playwright e2e test harness in `testing/e2e/`.
- **Integrates with**: The existing dual-emulator e2e workflow (`.github/workflows/e2e-dual-emulator.yml`) and accompanying scripts under `testing/e2e/` — pre-installation is implemented as an **extension of the existing workflow and scripts**, not a separate pipeline. New steps are inserted into the existing job sequence.
- **Enables**: All future e2e features can rely on a guaranteed, verified fresh app installation as a baseline invariant.

### Out of Scope

- Modifying the build logic or compilation configuration — the existing Gradle build is used as-is. (Note: adding the CI workflow step that invokes `assembleDebug` to produce the APK artifact is **in scope** as part of workflow integration; only the build logic itself is out of scope.)
- Emulator lifecycle management — starting, stopping, or provisioning emulators (covered by Feature 010).
- Clearing app data between individual tests (covered by Feature 010, FR-009).
- Installing test infrastructure dependencies or NDI SDK artifacts on emulators (covered by Feature 010).
- Support for physical device testing (emulators only).
- Selecting among multiple build flavors or variants for standard operation — the debug APK variant is the canonical default. Support for alternate variants requires an explicit override parameter and is not part of the default flow.

---

## Additional Context

### Problem Statement

The current e2e test procedure does not guarantee that emulators have the latest version of the app installed before tests begin. This creates several failure modes:

- Developers who run tests after building a new version may unknowingly test against a previously installed, outdated version if the old install was not manually cleared.
- CI/CD runs that skip or fail a build step may execute tests against a missing or stale app with no early warning.
- Test failures caused by a wrong app version are difficult to distinguish from genuine code regressions, increasing debugging time.
- There is no standardized record in test reports of which app version was tested, making it hard to correlate failures with specific builds.

This feature closes these gaps by making app installation a mandatory, verified, and reported pre-flight step in every test run.

### Success Metrics

After implementation:

- Test operators can confirm from the test report which app version was installed on each emulator for every run.
- No test run proceeds with an unverified or stale app installation.
- Build-related test failures (missing artifact, wrong version) are caught at the pre-flight gate rather than mid-test-suite.
- Developers and CI/CD pipelines share the same installation guarantee with no special manual steps required locally.

---

## Clarifications

### Session 2026-03-23

- Q: Is adding a CI build step for the app APK in scope for this feature's workflow integration? → A: Yes — the CI/CD integration includes a workflow step that invokes the existing `assembleDebug` Gradle task to produce the APK artifact before installation; the build logic itself is not changed.
- Q: Which APK variant should be used as the default for local and CI execution? → A: Debug APK variant (`assembleDebug`) is the default for both local and CI execution; an alternate variant may be specified via explicit override parameter but is not required.
- Q: How should this feature integrate with CI — as an extension of the existing dual-emulator workflow or as a separate pipeline? → A: Extension of the existing dual-emulator e2e workflow (`.github/workflows/e2e-dual-emulator.yml`) and scripts under `testing/e2e/`; no separate pipeline is introduced.
- Q: What constitutes the installed app version identifier for per-device logging (FR-006)? → A: The combination of `versionName` and `versionCode` extracted from installed artifact metadata or ADB logcat output immediately after installation.
- Q: What is the explicit timeout value for FR-008 (per-emulator installation time limit)? → A: 60 seconds per emulator, aligned with SC-003.
- Q: How should the pre-install gate handle emulators that are still booting when readiness is first checked? → A: The pre-install gate waits for emulator readiness for up to the same bounded 60-second timeout, then aborts the run with a device-specific emulator-not-ready error if readiness is not reached.

