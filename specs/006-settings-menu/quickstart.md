<!-- Last updated: 2026-03-20 -->

# Quickstart: Settings Menu and Developer Diagnostics

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Manual Test Data](#2-manual-test-data)
3. [US1 Manual Flow: Open Settings and Back](#3-us1-manual-flow-open-settings-and-back)
4. [US2 Manual Flow: Discovery Endpoint Save and Fallback](#4-us2-manual-flow-discovery-endpoint-save-and-fallback)
5. [US3 Manual Flow: Developer Mode Overlay and Redaction](#5-us3-manual-flow-developer-mode-overlay-and-redaction)
6. [Timing Verification Commands](#6-timing-verification-commands)
7. [Build and Validation Commands](#7-build-and-validation-commands)

## 1. Prerequisites

From repository root:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
./gradlew.bat --version
npm --prefix testing/e2e ci
adb devices
```

Expected environment:

- Android SDK/JDK prerequisites pass.
- At least one emulator/device is connected.
- For full e2e, two visible emulators are available.

## 2. Manual Test Data

Use these values during manual validation:

- Valid hostname: `ndi-server.local`
- Valid IPv4 with port: `192.168.1.10:5960`
- Valid bracketed IPv6 with port: `[2001:db8::10]:5960`
- Invalid unbracketed IPv6 with port: `2001:db8::10:5960`
- Invalid port: `ndi-server.local:70000`

Expected parsing behavior:

- Port omitted defaults to `5960`.
- Whitespace is trimmed.
- Invalid values show inline validation and are not saved.

## 3. US1 Manual Flow: Open Settings and Back

### Flow A: Source List -> Settings -> Back

1. Open app and navigate to Source List.
2. Tap settings action in top app bar.
3. Verify Settings screen shows:
   - Discovery input
   - Developer mode toggle
   - Save button
4. Press system Back.
5. Verify return to Source List.

### Flow B: Viewer -> Settings -> Back

1. Open Viewer screen.
2. Tap settings action.
3. Press Back.
4. Verify return to Viewer.

### Flow C: Output -> Settings -> Back

1. Open Output screen.
2. Tap settings action.
3. Press Back.
4. Verify return to Output.

## 4. US2 Manual Flow: Discovery Endpoint Save and Fallback

1. Open Settings.
2. Enter `ndi-server.local` and tap Save.
3. Close and reopen app.
4. Reopen Settings and verify value persists.
5. Enter invalid value `2001:db8::10:5960` and tap Save.
6. Verify inline validation appears (`Invalid format`) and previous valid value is retained.
7. Clear field and tap Save.
8. Verify setting persists as empty (default discovery mode intent).

Fallback warning validation:

- Runtime fallback warning production wiring is not currently complete in app composition.
- Validate fallback-warning rendering path through existing instrumentation seam (`SourceListFallbackWarningTest`) or dependency-provided warning flow in debug/test harnesses.

## 5. US3 Manual Flow: Developer Mode Overlay and Redaction

1. Open Settings.
2. Enable Developer Mode and tap Save.
3. Navigate to Source List, Viewer, and Output screens.
4. Verify overlay appears at top area.
5. Verify idle state appears when no active stream.
6. Start viewer/output session and verify status changes to active state.
7. Verify session ID is masked (`****` prefix).
8. Verify logs with IP values are redacted to `[redacted-ip]`.
9. Disable Developer Mode in Settings and tap Save.
10. Verify overlay disappears from all main screens.

## 6. Timing Verification Commands

### 6.1 Playwright Timing Assertions (Preferred)

The helper in `testing/e2e/tests/support/timingAssertions.ts` already runs 3 samples and checks median thresholds.

```powershell
$env:EMULATOR_A_SERIAL="emulator-5554"
$env:EMULATOR_B_SERIAL="emulator-5556"
$env:APP_PACKAGE="com.ndi.app.debug"
npm --prefix testing/e2e run test:dual-emulator
```

### 6.2 adb Timestamp Sampling (Manual)

Use device shell epoch milliseconds before and after action:

```powershell
$serial = "emulator-5554"
$t0 = adb -s $serial shell "date +%s%3N"
# Perform UI action: toggle Developer Mode or tap Save
$t1 = adb -s $serial shell "date +%s%3N"
[int64]$elapsed = [int64]$t1 - [int64]$t0
"Elapsed(ms): $elapsed"
```

Timing targets:

- Overlay show/hide and immediate apply checks: `<= 1000ms`
- Stream status or fallback warning propagation: `<= 3000ms`

## 7. Build and Validation Commands

```powershell
./gradlew.bat test
./gradlew.bat connectedAndroidTest
./gradlew.bat :app:assembleDebug
./gradlew.bat verifyReleaseHardening
./gradlew.bat :app:assembleRelease
```

Dual-emulator e2e runner:

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

Collect evidence under:

- `testing/e2e/artifacts/`
- `testing/e2e/test-results/`
- `app/build/reports/`
