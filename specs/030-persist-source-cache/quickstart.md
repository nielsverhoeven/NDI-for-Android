# Quickstart: Validate Persistent Source Cache

## Goal

Validate that cached discovered sources survive app restarts/updates, that View actions stay disabled until validation completes, that discovery-server flows store and use source endpoints correctly, and that developer-only database inspection exposes current persisted state.

Command validation note (2026-04-19): quickstart command and artifact names were validated against the feature task plan; no command-path mismatches were found.

## 1. Environment Preflight

1. Run prerequisite validation:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
```

2. Confirm Gradle/toolchain resolution:

```powershell
./gradlew.bat --version
```

3. Confirm runtime availability:

```powershell
adb devices
```

4. If using dual-emulator e2e coverage, verify that harness prerequisites are ready:

```powershell
pwsh ./scripts/verify-e2e-dual-emulator-prereqs.ps1
```

Required runtime conditions:

- At least one emulator/device is online.
- At least one NDI source is reachable.
- If discovery servers are enabled for the scenario, at least one configured discovery server is reachable.
- App data must be preserved between the discovery run and the relaunch/update validation step.

If any preflight step fails, record the run as environment-blocked and do not continue to the final quality gates.

## 2. Red-Green-Refactor Sequence

1. Add failing JUnit tests for:
   - canonical cached-source upsert and deduplication
   - persisted validation-state transitions
   - Home dashboard `canNavigateToView` gating
   - developer-mode-only database inspection visibility
   - migration preservation for existing settings and discovery tables
2. Implement the minimum repository, DAO, ViewModel, and UI changes needed to pass.
3. Refactor with repository boundaries and existing service-locator wiring preserved.

## 3. Unit Test Execution

Run the affected module tests first:

```powershell
./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest
```

If Room migration coverage is split into another module, include the corresponding targeted task before broadening to `./gradlew.bat test`.

## 4. Manual and Emulator Validation Flows

### Scenario A: Cold start with persisted cached data

1. Discover at least one source.
2. Exit the app while preserving app data.
3. Relaunch the app and open the Home/View flow.
4. Verify cached source rows appear before validation finishes.
5. Verify the View/open action is disabled while validation is in progress.
6. Verify the action becomes enabled only after the source is confirmed available.

### Scenario B: Cached source becomes unavailable

1. Persist a source in cache.
2. Make that source unreachable.
3. Relaunch the app.
4. Verify the cached row still appears.
5. Verify validation completes to an unavailable state and the View/open action remains disabled.

### Scenario C: Discovery-server endpoint handoff

1. Enable one or more discovery servers.
2. Run discovery.
3. Verify stored cached-source data contains the announced source IP/port.
4. Start viewing the source.
5. Verify playback/output resolution uses the stored source endpoint, not the discovery-server endpoint.

### Scenario D: Developer database inspection

1. Open Settings with developer mode disabled and verify no database inspection option is shown.
2. Enable developer mode.
3. Open the developer section and verify the inspection surface lists cached sources, preview references, validation state, and discovery-server associations.
4. Trigger another discovery/validation cycle and verify the inspection surface reflects updated persisted state.

## 5. Playwright and Regression Gates

Run targeted emulator Playwright scenarios for the new visible flows, then run the existing regression suite:

```powershell
npm --prefix testing/e2e run test:pr:primary
```

If the scenario requires the dual-emulator harness, execute the existing harness script with the active emulator serials and capture the generated artifacts.

## 6. Release-Hardening Gate

Before merge, confirm release optimization and shrink settings remain intact:

```powershell
./gradlew.bat :app:verifyReleaseHardening
```

## 7. Evidence Reporting

Record each gate as one of:

- Pass: expected behavior validated.
- Code failure: reproducible feature defect with failing command/test evidence.
- Environment blocked: prerequisite or runtime dependency missing, with the exact failed precondition and unblock command.

Recommended evidence bundle:

- prerequisite script results
- targeted unit test results
- Playwright output or harness artifact directory
- release-hardening result
- screenshots or logs for developer inspection and disabled-action states when relevant