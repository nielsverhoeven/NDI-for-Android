# Quickstart: Validate NDI Discovery Routing Reliability

## Goal

Validate deterministic discovery-mode routing, 5-second timeout enforcement with explicit diagnostics, canonical cache merge behavior, and relaunch cache visibility for feature 031.

## 1. Preflight

Run required prerequisite checks:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
adb devices
```

Run Node/npm/Playwright preflight before any Playwright gate:

```powershell
pwsh -ExecutionPolicy Bypass -File ./testing/e2e/scripts/validate-command-contract.ps1
npm --prefix testing/e2e exec playwright --version
```

For dual-emulator scenarios:

```powershell
pwsh ./scripts/verify-e2e-dual-emulator-prereqs.ps1
```

If any prerequisite fails, classify as `BLOCKED (environment)` and stop before final quality gates.

## 2. TDD Execution Order

1. Add failing JUnit tests for mode selection, timeout/no-fallback, canonical cache merge, and diagnostics classification.
2. Execute the new story-scoped tests in the expected red state and capture the failure evidence before changing production code.
3. Implement minimal repository/data-layer changes to satisfy tests.
4. Refactor while preserving repository boundaries and existing regression behavior.

## 3. Unit Test Commands

Run targeted data and presentation module tests:

```powershell
./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest
```

Run broader test suite if needed:

```powershell
./gradlew.bat test
```

## 4. Functional Validation Scenarios

### Scenario A: No enabled discovery servers -> multicast mode

1. Disable all configured discovery servers.
2. Trigger discovery.
3. Verify run mode is multicast/mDNS.
4. Verify discovered sources persist to cache.

### Scenario B: Enabled discovery server(s) -> server-only mode

1. Enable one or more discovery servers.
2. Trigger discovery.
3. Verify mDNS/multicast is not executed for that run.
4. Verify endpoint host/port values are sourced from discovery records, not server endpoint values.

### Scenario C: Timeout behavior

1. Use a slow/unreachable discovery server fixture.
2. Trigger discovery-server mode.
3. Verify run returns timeout within 5 seconds boundary behavior.
4. Verify explicit timeout diagnostics are emitted.
5. Verify same-run multicast fallback does not occur.

### Scenario D: Canonical cache merge

1. Persist source with canonical identity and endpoint A.
2. Rediscover same canonical identity with endpoint B.
3. Verify only one canonical row remains and endpoint updates to B.
4. Verify preview continuity fields remain preserved.

### Scenario E: Relaunch cache visibility

1. Discover and persist one or more sources.
2. Relaunch app with data preserved.
3. Verify cached rows appear before live discovery completes.
4. Verify timeout/failure runs do not delete cached rows.

## 5. Playwright and Regression Gates

Run required e2e profile(s):

```powershell
npm --prefix testing/e2e run test:pr:primary
```

If dual-emulator flow is required by scenario, run the existing dual-emulator harness and capture artifacts:

```powershell
pwsh -ExecutionPolicy Bypass -File ./testing/e2e/scripts/provision-dual-emulator.ps1
pwsh -ExecutionPolicy Bypass -File ./testing/e2e/scripts/run-primary-pr-e2e.ps1 -Profile pr-primary
```

## 6. Release Hardening Gate

```powershell
./gradlew.bat :app:verifyReleaseHardening
```

## 7. Evidence Recording

For each gate, report one status:

- Pass
- Code failure
- BLOCKED (environment)

Capture evidence for:

- Preflight outputs
- Red-state failing-test outputs captured before implementation
- Unit test command outputs
- Playwright/harness artifact paths
- Timeout/per-server diagnostics excerpts
- Cache visibility/relaunch verification notes
