# Settings Gear Toggle Validation

## Feature
- Spec: specs/013-settings-gear-toggle/spec.md
- Branch: 013-settings-gear-toggle

## Red Phase Evidence

### JUnit Red Run
- Command: `./gradlew.bat :app:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest`
- Result: FAILED (captured during red-to-green implementation cycle)
- Failing tests:
	- `OutputControlViewModelTest > onSettingsTogglePressed_emitsOnceUntilSettled`
	- `SettingsViewModelTest > onSettingsTogglePressed_emitsOnceUntilSettled`
	- `SourceListViewModelTest > onSettingsTogglePressed_emitsOnceUntilSettled`
	- `ViewerViewModelTest > onSettingsTogglePressed_emitsOnceUntilSettled`
- Key output: `62 tests completed, 4 failed`

### Instrumentation Red Run
- Command: `./gradlew.bat :feature:ndi-browser:presentation:compileDebugAndroidTestKotlin`
- Result: FAILED (captured during red-to-green implementation cycle)
- Key failures before fixes:
	- `DeveloperOverlayTimingTest.kt` unresolved `com.ndi.app.MainActivity`
	- `DeveloperOverlayStreamStatusTimingTest.kt` unresolved `com.ndi.app.MainActivity`
	- `SourceListFallbackWarningTest.kt` unresolved `com.ndi.app.MainActivity` and `com.ndi.app.R`

### Playwright Red Run
- Command: `npm --prefix testing/e2e run test -- tests/settings-navigation-source-list.spec.ts --project=android-primary`
- Result: FAILED
- Key failure details:
	- `Provision-DualEmulator` status `FAILURE`
	- `SOURCE_PROVISION_FAILED` and `RECEIVER_PROVISION_FAILED`
	- Global setup aborted in `tests/support/global-setup-dual-emulator.ts`

## Material 3 Verification
- Validation date: 2026-03-26
- Result: FAIL
- Static verification summary:
  - Source list, viewer, and output use `MaterialToolbar` top app bars and keep `action_settings` as `showAsAction="always"`.
  - Source list, viewer, and output still use `@android:drawable/ic_menu_manage`, which does not satisfy the task/spec requirement for explicit gear/cog-only iconography.
  - Settings swaps the persistent gear for `@android:drawable/ic_menu_close_clear_cancel`, which conflicts with the requirement that the same top-right gear remain visible while settings is open.
  - `fragment_settings.xml` exposes the settings title through the toolbar title, but there is no separate visible header/title container despite the earlier task wording.

## Regression Validation
- Latest local primary PR artifact: `testing/e2e/artifacts/primary-pr-20260326-160208/`
	- Status: existing-suite regression did not execute in the latest cycle.
	- Evidence: the runner only emitted `new-settings.json` and `results/`; `existing-regression.json` is absent because `run-primary-pr-e2e.ps1` stops after a failing `new-settings` suite.
	- Interpretation: T025 remains open because there is no passing or failing existing-suite regression result for the current repo state.

## Playwright Toggle Coverage (T025)
- Command: `npm --prefix testing/e2e run test -- tests/settings-navigation-source-list.spec.ts tests/settings-navigation-viewer.spec.ts tests/settings-navigation-output.spec.ts --project=android-primary`
- Status: FAILED on 2026-03-26
- Failures observed:
	- `settings-navigation-source-list.spec.ts`: timeout waiting for `Available NDI Sources`, then retry hit `adb shell uiautomator dump /sdcard/window_dump.xml` failure on `emulator-5554`
	- `settings-navigation-viewer.spec.ts`: retry hit `adb shell uiautomator dump /sdcard/window_dump.xml` failure on `emulator-5554`
	- `settings-navigation-output.spec.ts`: timeout waiting for `Output for source-a` on `emulator-5554`
	- Global teardown also failed with `RESET_FAILED` from `testing/e2e/scripts/reset-emulator-state.ps1`
- Coverage gap:
	- The Playwright specs currently assert navigation text only; they do not explicitly verify gear/cog-only iconography, persistent top-right gear while settings is open, or visible settings header/title treatment.
- Artifacts:
	- `testing/e2e/test-results/settings-navigation-output-5188e-s1-output---settings---back-android-primary-retry1/trace.zip`
	- `testing/e2e/test-results/settings-navigation-source-6e53d-urce-list---settings---back-android-primary-retry1/trace.zip`
	- `testing/e2e/test-results/settings-navigation-viewer-031e0-s1-viewer---settings---back-android-primary-retry1/trace.zip`

## JUnit + Instrumentation Validation (T024)
- Command: `./gradlew.bat :app:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest`
- Result: PASS on 2026-03-26
- Notes:
	- Impacted app/presentation unit suites passed with the current repo state.
	- The previously red `onSettingsTogglePressed_emitsOnceUntilSettled` tests now pass in the generated unit-test reports.
- Command: `./gradlew.bat :feature:ndi-browser:presentation:connectedDebugAndroidTest '-Pandroid.testInstrumentationRunnerArguments.class=com.ndi.feature.ndibrowser.settings.SettingsGearToggleTest,com.ndi.feature.ndibrowser.settings.SourceListSettingsNavigationTest,com.ndi.feature.ndibrowser.settings.ViewerSettingsNavigationTest,com.ndi.feature.ndibrowser.settings.OutputSettingsNavigationTest'`
- Result: PASS on 2026-03-26
- Notes:
	- Gradle reported 5/5 targeted instrumentation tests passing on each connected emulator.
	- The current instrumentation suite verifies menu-item visibility, rotation resilience, and rapid-tap close deduping.
	- T024 is still not fully satisfied because the instrumentation tests do not explicitly assert the settings header/title treatment or validate that the settings-screen action remains a gear/cog rather than a close icon.

## Release Hardening
- Command: `./gradlew.bat :app:assembleRelease :app:verifyReleaseHardening`
- Result: PASS on 2026-03-26
- Notes:
	- Release build and hardening verification completed successfully.
	- Build emitted only existing warnings (unchecked casts, Room migration parameter names, stale lint-baseline entries), not gate failures.

## Concrete Blockers
- Product code blocker: `feature/ndi-browser/presentation/src/main/res/menu/settings_menu.xml` uses a close icon and close-specific labels instead of the persistent gear required by the spec/tasks.
- Product code blocker: `feature/ndi-browser/presentation/src/main/res/menu/source_list_menu.xml`, `viewer_menu.xml`, and `output_menu.xml` still use `@android:drawable/ic_menu_manage` instead of a gear/cog-only asset.
- Test coverage blocker: the current instrumentation and Playwright suites do not explicitly validate gear/cog-only iconography or visible settings header/title behavior, even though T012/T024/T025 require those assertions.
- Harness blocker: the Playwright run is unstable because `uiautomator dump` intermittently fails on `emulator-5554`, and teardown currently fails with `RESET_FAILED`.
- Tooling blocker: `scripts/verify-android-prereqs.ps1` can exit with a PowerShell `Count` property error after all checks pass when exactly one failure is present.
- Task-tracking blocker: `specs/013-settings-gear-toggle/quickstart.md` already contains the requested local validation guidance, but T023 in `specs/013-settings-gear-toggle/tasks.md` is still unchecked and should be reconciled separately from the product-code blockers.
