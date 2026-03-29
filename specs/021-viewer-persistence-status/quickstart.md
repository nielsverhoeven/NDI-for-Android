# Quickstart - Viewer Persistence and Stream Availability Status

## Goal

Validate persistence of last viewed stream/frame and source-list availability/history behavior with constitution-compliant test workflow.

## 1. Environment Preflight

1. Run prerequisite validation:
```powershell
scripts/verify-android-prereqs.ps1
```
2. Confirm toolchain wrapper:
```powershell
./gradlew.bat --version
```
3. Ensure required runtime dependencies:
- Emulator/device ready and reachable
- Discovery environment provides at least one stable source and one intermittently unavailable source
- Writable app-internal storage

If preflight fails, record as environment-blocked and stop e2e gate execution.

## 2. Red-Green-Refactor Sequence

1. Add failing JUnit tests for:
- last viewed context persistence lifecycle
- Previously Connected transition (only after first rendered frame)
- availability transition after two missed discovery polls
- disabled navigation path for unavailable source

2. Implement minimal repository and ViewModel changes to pass tests.

3. Refactor with module boundaries preserved (`domain` contracts, `data` implementations, `presentation` UI wiring).

## 3. Unit Test Execution

Run targeted unit tests first, then broader module tests:

```powershell
./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest
```

## 4. Playwright E2E Validation

Run new/updated emulator Playwright scenarios for:
- source row badges (Previously Connected, Unavailable)
- enabled/disabled View Stream action
- viewer restore with saved preview image
- unavailable restore behavior without autoplay

Then run full existing Playwright regression suite and confirm zero regressions.

## 5. Evidence and Gate Reporting

Record outcomes as:
- Pass: all required tests green
- Code failure: reproducible failing tests linked to code behavior
- Environment blocked: reproducible preflight/runtime dependency failure with unblocking step

## 6. Release-Hardening Check

Before merge, ensure release build hardening remains intact:

```powershell
./gradlew.bat :app:verifyReleaseHardening
```
