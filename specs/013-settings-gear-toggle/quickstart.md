# QuickStart: Settings Gear Toggle

**Feature**: 013-settings-gear-toggle  
**Date**: March 26, 2026  

## Overview

Implement a consistent gear/cog action in the top-right of the source list,
viewer, output, and settings surfaces. Tapping the gear opens
`settingsFragment` from non-settings surfaces and closes it from the settings
surface by returning to the previous destination. While settings is open, keep
the top-right gear visible and show a visible settings header/title.

## Prerequisites

- Android prerequisites satisfied via `scripts/verify-android-prereqs.ps1`
- Debug app buildable with the current toolchain
- Two visible Android emulators available for Playwright validation when running e2e
- Existing settings feature remains intact; this feature only changes entry/exit behavior and toolbar presentation

## Test-First Workflow

1. Add or update failing JUnit coverage for navigation/toggle behavior before implementation.
2. Convert the placeholder Playwright `settings-navigation-*.spec.ts` tests from expected-fail to real assertions before wiring the UI changes.
3. Implement the minimal toolbar/layout/navigation changes needed to satisfy the tests.
4. Refactor shared settings-toggle helpers only after tests pass.

## Implementation Steps

1. Update source list, viewer, and output menus so `action_settings` uses `showAsAction="always"` with explicit gear/cog iconography (no wrench/manage icon substitutions).
2. Add a Material top app bar to the settings layout with a visible settings title/header and the same top-right gear affordance.
3. Centralize toggle routing so non-settings surfaces navigate to `settingsFragment` and the settings surface pops back to the previous destination.
4. Guard against duplicate settings navigation on rapid repeated taps.
5. Update accessibility content descriptions and Playwright selectors so the gear action is discoverable on all in-scope surfaces.
6. For source-list emulator validation, start from Home and use `Open Stream` to reach the in-scope source-list surface before asserting the gear toggle flow.

## Local Validation Commands

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-android-prereqs.ps1
.\gradlew.bat :app:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest
.\gradlew.bat :app:assembleDebug :app:verifyReleaseHardening
```

For emulator Playwright validation:

```powershell
npm run test:pr:primary
```

This run must include:

1. New `@settings` coverage proving gear visibility, gear-only iconography intent, visible settings title/header, and open/close behavior.
2. Existing regression suite coverage remaining green.

## Expected Evidence

- Passing JUnit results for new toggle/navigation tests
- Passing Playwright results for:
  - `testing/e2e/tests/settings-navigation-source-list.spec.ts`
  - `testing/e2e/tests/settings-navigation-viewer.spec.ts`
  - `testing/e2e/tests/settings-navigation-output.spec.ts`
- Evidence that the existing regression suite still passed in the same validation cycle
- Successful release-hardening verification
