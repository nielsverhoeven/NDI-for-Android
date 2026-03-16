# Quickstart: NDI Output Feature with Dual-Emulator End-to-End Validation

## 1. Prerequisites

1. Run prerequisite gate:
   - `pwsh ./scripts/verify-android-prereqs.ps1`
2. Verify Gradle/toolchain baseline:
   - `./gradlew --version`
3. Confirm NDI SDK is available to `ndi/sdk-bridge` via local SDK path config.
4. Ensure two Android emulators can run concurrently on the same host.

## 2. Build and Install

1. Build debug app:
   - `./gradlew :app:assembleDebug`
2. Start two emulators (example names):
   - Emulator A: publisher role
   - Emulator B: receiver role
3. Install debug APK on both emulator instances:
   - `adb -s <emulatorA-serial> install -r app/build/outputs/apk/debug/app-debug.apk`
   - `adb -s <emulatorB-serial> install -r app/build/outputs/apk/debug/app-debug.apk`

## 3. Two-Emulator End-to-End Flow (Mandatory)

Goal: Validate cross-feature interoperability where this feature publishes and
previous feature captures.

1. On Emulator A (publisher role):
   - Launch app.
   - Open source list and select source representing Emulator A screen feed.
   - Open output control and start outbound NDI output.
   - Confirm output state becomes ACTIVE and outbound stream identity is visible.

2. On Emulator B (receiver role):
   - Launch app.
   - Open source list and trigger discovery.
   - Confirm stream published by Emulator A appears in list.
   - Select discovered stream and open viewer.
   - Confirm viewer reaches PLAYING state.

3. Stop propagation validation:
   - On Emulator A, stop outbound output.
   - On Emulator B, confirm viewer transitions to interrupted/stopped behavior
     with recoverable UX (retry or back to list).

Pass criteria:

- Publisher ACTIVE observed on Emulator A.
- Receiver DISCOVERED and PLAYING observed on Emulator B.
- Publisher stop reflected on receiver state transition.

## 4. Automated Test-First Workflow

1. Write failing unit tests first for:
   - output state transitions
   - duplicate start/stop guarding
   - interruption and retry-window behavior
2. Write failing Playwright end-to-end tests first for:
   - source list -> output control navigation
   - dual-emulator publish -> discover -> play scenario
   - publisher stop -> receiver interruption/stopped scenario
3. Implement minimal code to pass tests.
4. Refactor with tests green.

## 5. Validation Commands

Run after implementation:

- `pwsh ./scripts/verify-android-prereqs.ps1`
- `./gradlew test`
- `npm --prefix testing/e2e ci`
- `npm --prefix testing/e2e run test:dual-emulator`
- `./gradlew connectedAndroidTest`
- `./gradlew verifyReleaseHardening`
- `./gradlew :app:assembleRelease`

Expected outcomes:

- Unit and Playwright dual-emulator tests pass.
- Connected Android tests pass for compatibility regressions.
- Dual-emulator E2E scenario is reproducible and passes.
- Release build passes with shrinking/optimization enabled.
- No unauthorized permission additions.

## 6. Network Preconditions for Reliable E2E

- Both emulators must be on the same multicast-capable network segment.
- Discovery timeout and playback timeout must be configured consistently with
  success criteria in the feature spec.
- If network preflight fails, E2E run is invalid and should be rerun after
  topology correction.
