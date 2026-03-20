# Quickstart: Settings Menu and Developer Diagnostics

## 1. Prerequisites

From repository root:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
.\gradlew.bat --version
npm --prefix testing/e2e ci
adb devices
```

Expected environment:

- Android SDK/JDK prerequisites pass.
- At least one emulator/device connected for UI validation.
- Dual emulators available for end-to-end stream validation.

## 2. Test-First Development Sequence

1. Add failing unit tests for:
   - Discovery endpoint parsing (`hostname`, `hostname:port`, `IPv4`, `IPv4:port`, `[IPv6]`, `[IPv6]:port`, invalid formats).
   - Default-port resolution when port is omitted.
   - Redaction of sensitive values in overlay log entries.
2. Add failing ViewModel tests for:
   - Settings persistence and restore.
   - Immediate discovery apply behavior.
   - Active-stream interruption behavior on endpoint change.
   - Unreachable endpoint fallback warning behavior.
   - Developer mode idle overlay state (`No active stream`).
3. Add failing UI/integration tests for:
   - Settings route entry + back behavior.
   - Overlay show/hide timing guarantees.
4. Add failing e2e checks for:
   - Discovery-setting change during active stream (immediate apply + possible interruption).
   - Fallback warning visibility when endpoint is unreachable.
   - Redacted logs shown in overlay.
5. Implement minimum code to pass tests.
6. Refactor with all tests green.

## 2.1 Timing Measurement Convention

Use this convention for SC-003, SC-004, SC-005, SC-006, SC-008, and SC-010 verification:

- Measure elapsed time using a monotonic clock (do not use wall-clock time).
- Start timing at user action dispatch (save/toggle/stream-state trigger).
- Stop timing at first observable UI state satisfying the acceptance condition.
- Run each timing test scenario at least 3 times and use the median elapsed value for pass/fail.
- Thresholds: `<=1s` for overlay/toggle/immediate-apply checks and `<=3s` for stream-status/fallback checks.

## 3. Build and Validation Commands

```powershell
.\gradlew.bat test
.\gradlew.bat connectedAndroidTest
.\gradlew.bat :app:assembleDebug
```

Optional dual-emulator e2e run:

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

## 4. Functional Validation Checklist

- Settings destination opens within 2 taps from Source List, Viewer, and Output screens.
- Discovery field accepts hostname, IPv4, or bracketed IPv6 with optional port and rejects invalid/whitespace-only values.
- Saved settings persist after app restart.
- Saving discovery setting applies immediately.
- Active stream may be interrupted when immediate apply requires it.
- Unreachable configured discovery server triggers fallback with visible warning.
- Developer Mode overlay appears/disappears within 1 second of toggle changes.
- Idle mode shows explicit `No active stream` state and recent logs.
- Overlay logs display redacted sensitive values.

## 5. Release-Grade Validation

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
.\gradlew.bat verifyReleaseHardening
.\gradlew.bat :app:assembleRelease
```

Collect execution evidence under:

- `testing/e2e/artifacts/`
- `testing/e2e/test-results/`
- `app/build/reports/`
