# Quickstart: Bottom Navigation Settings Access

**Feature**: 014-bottom-nav-settings  
**Date**: March 26, 2026

## Overview

Replace top-right settings entry affordances with a dedicated Settings bottom navigation item. Users can enter settings from Home/Stream/View via bottom navigation and leave settings via any non-settings bottom navigation item.

## Prerequisites

- Android prerequisites pass using scripts/verify-android-prereqs.ps1.
- Local toolchain and Gradle wrapper are healthy.
- Emulator environment available for Playwright validation.

## Test-First Workflow

1. Add or update failing JUnit tests for destination-to-selected-state synchronization, repeated-tap de-duplication, and settings enter/exit behavior.
2. Add or update failing Playwright specs for bottom-nav settings entry/exit flows.
3. Implement minimal navigation and UI affordance changes required to pass tests.
4. Refactor only after all tests pass.

## Implementation Steps

1. Add the Settings item to bottom navigation model/wiring.
2. Route Settings selection to settings destination using canonical app navigation helper paths.
3. Remove top-right settings actions from source list, viewer, output, and settings surfaces.
4. Keep selected bottom-nav state synchronized with active destination under rapid switching and rotation.
5. Verify settings screen retains visible title/header while active.

## Local Validation Commands

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-android-prereqs.ps1
.\gradlew.bat --version
.\gradlew.bat :app:testDebugUnitTest --no-daemon
.\gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest --no-daemon
```

For focused Playwright bottom-nav settings validation (US1/US2/US3):

```powershell
npx playwright test tests/settings-navigation-source-list.spec.ts tests/settings-navigation-viewer.spec.ts tests/settings-navigation-output.spec.ts --project=android-primary
```

For Playwright regression gate:

```powershell
npm run test:pr:primary
```

For release hardening gate:

```powershell
.\gradlew.bat :app:assembleDebug :app:verifyReleaseHardening --no-daemon
```

## Expected Evidence

- Passing JUnit coverage for bottom-nav settings entry/exit and state synchronization.
- Passing Playwright coverage for bottom-nav settings scenarios in testing/e2e/tests.
- Passing existing Playwright regression suite in same validation cycle.
- Successful release hardening verification remains intact.
