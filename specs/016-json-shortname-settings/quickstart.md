# Quickstart: Theme Editor Settings

## 1. Prerequisites

From repo root:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
.\gradlew.bat --version
npm --prefix testing/e2e ci
adb devices
```

## 2. Failing-First Implementation Sequence

1. Add failing unit tests for:
   - theme mode selection state mapping,
   - accent selection state mapping,
   - persistence load/save normalization.
2. Add failing instrumentation tests for:
   - settings navigation to theme editor,
   - selected-state rendering for mode and accent options.
3. Add failing Playwright tests for:
   - switching Light/Dark/System,
   - switching curated accent options,
   - relaunch persistence verification.
4. Implement minimal code to pass tests.
5. Refactor while keeping tests green.

## 3. Validation Commands

```powershell
.\gradlew.bat :feature:theme-editor:domain:testDebugUnitTest :feature:theme-editor:data:testDebugUnitTest :feature:theme-editor:presentation:testDebugUnitTest
.\gradlew.bat :feature:theme-editor:presentation:compileDebugAndroidTestKotlin
```

Run full Playwright e2e suite:

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

## 4. Functional Validation Checklist

- Theme editor is reachable from settings.
- Light, Dark, and System each apply expected appearance behavior.
- Curated accent palette shows 6-8 options and only one active selection.
- Selected mode and accent persist across app restart.
- System mode still follows device dark/light changes.

## 5. Release-Grade Validation

```powershell
.\gradlew.bat verifyReleaseHardening :app:assembleRelease
```

Record outcomes and evidence in feature validation artifacts during implementation.

## 6. Maintainer Notes

- Added modules: `:feature:theme-editor:domain`, `:feature:theme-editor:data`, `:feature:theme-editor:presentation`.
- Verified touched-module JVM + Android-test compile gate:
   - `./gradlew.bat :feature:theme-editor:data:testDebugUnitTest :feature:theme-editor:presentation:testDebugUnitTest :feature:theme-editor:presentation:compileDebugAndroidTestKotlin :app:testDebugUnitTest`
- Verified release hardening gate:
   - `./gradlew.bat verifyReleaseHardening :app:assembleRelease`
- Full Playwright regression (`run-dual-emulator-e2e.ps1`) requires two running visible emulators and is currently blocked when emulator devices are offline.
