# Quickstart - Discovery Service Reliability

## Goal

Validate protocol-level discovery server checks, per-server recheck UX, and developer-mode diagnostics with constitution-compliant test workflow.

## 1. Environment Preflight

1. Validate Android prerequisites:

```powershell
scripts/verify-android-prereqs.ps1
```

2. Validate dual-emulator prerequisites for Playwright e2e:

```powershell
scripts/verify-e2e-dual-emulator-prereqs.ps1
```

3. Confirm Gradle wrapper/toolchain:

```powershell
./gradlew.bat --version
```

4. Confirm runtime dependencies:
- NDI SDK is installed and discoverable by the build/runtime environment.
- At least one reachable discovery server endpoint is available.
- At least one unreachable/invalid endpoint is available for negative-path checks.

If any preflight fails, classify result as BLOCKED: ENVIRONMENT and stop before e2e gates.

## 2. Red-Green-Refactor Sequence

1. Add failing JUnit tests first for:
- protocol-level success/failure mapping in discovery server checks
- add-time status persistence and error messaging
- per-server recheck updates scoped to selected row only
- developer-mode diagnostics visibility toggle behavior

2. Implement minimal repository, ViewModel, and renderer changes to pass tests.

3. Refactor while preserving module boundaries and bridge correctness.

## 3. Unit Test Execution

Run targeted module tests first:

```powershell
./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest :ndi:sdk-bridge:testDebugUnitTest
```

If sdk-bridge has no unit test target in the current setup, run available module tests and record bridge checks via integration/e2e evidence.

## 4. Playwright E2E Validation

Run or add emulator Playwright scenarios for:
- adding valid server shows success check state
- adding invalid/unreachable server shows actionable failure details
- per-row recheck updates only targeted server row
- developer mode ON shows diagnostics; OFF hides diagnostics

Then run the full existing Playwright regression suite and verify no regressions.

## 5. Evidence and Gate Reporting

Record each gate as one of:
- PASS
- CODE FAILURE (with failing test evidence and reproduction)
- BLOCKED: ENVIRONMENT (with failing preflight output and explicit unblock step)

## 6. Release-Hardening Validation

Before merge, verify release hardening gate:

```powershell
./gradlew.bat :app:verifyReleaseHardening
```

## 7. Suggested Validation Order

1. Preflight scripts
2. Failing tests added and observed
3. Unit tests green
4. Targeted Playwright scenarios green
5. Full Playwright regression green
6. Release hardening green
