# Feature 015 Validation Log

## Scope

- Refine View screen controls and refresh UX.

## Task Evidence

- Implemented tasks: T001-T014, T016-T023, T025-T034, T036, T039, T040.
- Remaining open tasks: T015, T024, T035, T037-T038.

## Unit Tests

- Command attempted: `./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest --tests "com.ndi.feature.ndibrowser.source_list.SourceListViewModelTest"`
- Result: failed before executing target class due to unrelated pre-existing compile errors in viewer tests (`ViewerInterruptionStateTest`, `ViewerViewModelTest`, `ViewerViewModelTopLevelNavTest`) where fake repositories do not implement `getLatestVideoFrame()`.
- Full gate command attempted: `./gradlew.bat test connectedAndroidTest`
- Full gate result: failed during `:feature:ndi-browser:presentation:compileDebugUnitTestKotlin` for the same pre-existing viewer test fake-repository interface mismatch.

## Instrumentation Tests

- Added source-list instrumentation coverage for:
	- non-button row tap inert behavior,
	- bottom-left refresh placement with adjacent loading indicator,
	- refresh disable/re-enable transition.
- Full androidTest compilation and execution remain blocked by unrelated pre-existing errors in other instrumentation suites.

## Playwright E2E

- New scenario files added:
	- `testing/e2e/tests/us1-view-source-filtering.spec.ts`
	- `testing/e2e/tests/us2-us3-view-interactions.spec.ts`
- Regression manifest updated with both new specs.
- Full e2e command attempted: `powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556`
- Full e2e result: failed preflight because emulators `emulator-5554` and `emulator-5556` were not online/visible.

## Release Hardening

- Command: `./gradlew.bat verifyReleaseHardening :app:assembleRelease`
- Result: `BUILD SUCCESSFUL`.

## Build Verification

- Command: `./gradlew.bat :feature:ndi-browser:presentation:compileDebugKotlin`
- Result: `BUILD SUCCESSFUL`.
