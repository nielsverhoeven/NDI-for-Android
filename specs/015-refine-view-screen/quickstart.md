# Quickstart: Refine View Screen Controls

## 1. Prerequisites

From repository root:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
.\gradlew.bat --version
npm --prefix testing/e2e ci
adb devices
```

Expected environment:

- Android emulator/device available for UI validation.
- Debug build installable for manual checks.
- Playwright e2e tooling installed for visual regression tests.

## 2. Test-First Development Sequence

1. Add failing JUnit tests for ViewModel/state behavior:
   - current-device filtering from visible source list,
   - refresh state transitions (disable/enable, spinner visibility),
   - refresh failure preserving list and setting inline error state,
   - non-button row tap emits no navigation event.
2. Add/extend failing UI or e2e tests for user-visible behavior:
   - "view stream" button-only interaction,
   - no output-start action on View screen,
   - refresh control placement at bottom-left,
   - loading icon adjacency and disabled refresh during in-flight state.
3. Implement minimal changes to pass failing tests.
4. Refactor with all tests green.
5. Run full existing Playwright suite to ensure no regressions.

## 3. Validation Commands

```powershell
.\gradlew.bat test
.\gradlew.bat connectedAndroidTest
```

Run Playwright e2e (including existing suite and this feature's visual checks):

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

## 4. Functional Validation Checklist

- Current device is never shown as a selectable source.
- Each visible source has a "view stream" button.
- Tapping outside the button does not open viewer.
- View screen does not display a direct start-output action.
- Refresh button is visibly located bottom-left.
- During refresh: loading icon appears next to refresh button and refresh button is disabled.
- During refresh failure: inline non-blocking error appears near refresh controls while existing list remains visible.
- After refresh completion/failure: refresh button is re-enabled.

## 5. Release-Grade Validation

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
.\gradlew.bat verifyReleaseHardening
.\gradlew.bat :app:assembleRelease
```

Collect evidence (screenshots, logs, and Playwright report artifacts) under
`testing/e2e/artifacts/` and `testing/e2e/test-results/`.

## 6. Maintainer Notes

- If you need a quick compile confidence check for this feature's main code path, run:

```powershell
.\gradlew.bat :feature:ndi-browser:presentation:compileDebugKotlin
```

- Current workspace has unrelated pre-existing failures in other presentation tests (viewer/settings suites). If full test tasks fail before source-list tests execute, record the unrelated blockers in the validation log and re-run once those suites are fixed.

- For this feature, prioritize validating these files when triaging regressions:
   - `SourceListViewModel.kt`
   - `SourceListScreen.kt`
   - `SourceAdapter.kt`
   - `SourceListViewModelTest.kt`
   - `SourceListScreenTest.kt`
   - `testing/e2e/tests/us1-view-source-filtering.spec.ts`
   - `testing/e2e/tests/us2-us3-view-interactions.spec.ts`
