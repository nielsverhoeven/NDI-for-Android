# Quickstart: Dual-Emulator Validation for NDI Output Screen Share

## 1. Prerequisites

Run the repo and automation prerequisites from the repository root:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
.\gradlew.bat --version
npm --prefix testing/e2e ci
```

Required machine state:

- NDI Android SDK configured for `ndi/sdk-bridge`.
- Two Android emulators can run concurrently on the same Windows host.
- `adb`, `emulator`, and `sdkmanager` are on `PATH`.
- The emulators are attached to the same multicast-capable host network segment.
- Node/npm are available for the Playwright workspace in `testing/e2e`.

## 2. Build and Install the App on Both Emulators

Build the debug APK:

```powershell
.\gradlew.bat :app:assembleDebug
```

Start two emulators and install the debug build on each:

```powershell
adb devices
adb -s <publisher-serial> install -r app/build/outputs/apk/debug/app-debug.apk
adb -s <receiver-serial> install -r app/build/outputs/apk/debug/app-debug.apk
```

Role assignment:

- Emulator A / `<publisher-serial>`: publisher, starts sharing its own screen as
  the outbound NDI source.
- Emulator B / `<receiver-serial>`: receiver, discovers and opens the publisher
  stream in the viewer.

## 3. Planned Automated End-to-End Flow

The mandatory automated scenario is:

1. Preflight
   - Verify publisher and receiver serials are different.
   - Verify both devices respond to `adb -s <serial> get-state`.
   - Verify the app is installed on both devices.
   - Abort early if multicast/network preflight fails.
2. Publisher workflow on emulator A
   - Launch the app.
   - Select the reserved local screen-share source (`device-screen:*`).
   - Navigate to output control.
   - Start output and accept the Android screen-capture consent prompt.
   - Assert output state becomes ACTIVE and the outbound stream name is visible.
3. Receiver workflow on emulator B
   - Launch the app.
   - Trigger discovery refresh.
   - Assert the publisher stream appears in the source list within timeout.
   - Open the discovered stream in the viewer.
   - Assert the viewer reaches PLAYING.
4. Stop propagation workflow
   - Stop output on emulator A.
   - Assert emulator B leaves active playback and shows recoverable UX.

Pass criteria:

- Publisher reaches ACTIVE after explicit consent.
- Receiver discovers publisher stream.
- Receiver reaches PLAYING.
- Publisher stop propagates to receiver interruption/stopped behavior.

## 4. PowerShell Launcher for the Automated Run

The host-side entry point is the dual-emulator launcher script:

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 `
  -EmulatorASerial <publisher-serial> `
  -EmulatorBSerial <receiver-serial>
```

Expected launcher responsibilities after implementation:

- Run emulator/install preflight.
- Invoke the Playwright Android suite in `testing/e2e/tests/interop-dual-emulator.spec.ts`.
- Export Playwright report artifacts and role-specific diagnostics.

## 5. Test-First Workflow

1. Write/update failing JUnit tests first for:
   - output state transitions
   - start/stop idempotency
   - interruption and retry-window behavior
   - screen-share consent gating for `device-screen:*`
2. Write/update failing Playwright Android tests first for:
   - source list -> output control navigation
   - publisher screen share consent + ACTIVE state
   - receiver discover -> play flow
   - publisher stop -> receiver interruption/stopped flow
3. Implement the minimal code to satisfy the failing tests.
4. Refactor with all layers green.

## 6. Validation Commands

Run these after implementation:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
.\gradlew.bat test
.\gradlew.bat connectedAndroidTest
.\gradlew.bat verifyReleaseHardening
.\gradlew.bat :app:assembleRelease
npm --prefix testing/e2e run test:dual-emulator
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial <publisher-serial> -EmulatorBSerial <receiver-serial>
```

Expected outcomes:

- Unit/repository tests pass.
- Platform compatibility tests pass.
- Playwright Android dual-emulator flow passes with real app/device assertions.
- Release build passes with shrinking and optimization enabled.
- Validation artifacts are recorded for both publisher and receiver roles.

## 7. Network and Environment Preconditions

- Both emulators must be online simultaneously and visible in `adb devices`.
- Both emulators must be on the same multicast-capable network segment.
- Discovery/playback timeouts must align with feature success criteria.
- If preflight fails, the E2E run is invalid and should be rerun after
  environment correction rather than treated as a product regression.
