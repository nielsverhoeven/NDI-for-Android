# Bottom Navigation Settings - Validation Evidence

## Scope Completed In This Run
- Phase 4 (US2): T024-T031
- Phase 5 (US3): T032-T043
- Phase 6 (Polish/Gates): T044-T048

## Phase 4 (US2) - RED Evidence (T027)

### Added US2 tests
- JUnit: `app/src/test/java/com/ndi/app/navigation/TopLevelNavViewModelTest.kt`
  - `settingsToNonSettingsTransitions_emitDeterministicEventsAndSelection`
  - `rapidSwitch_settingsAndOtherTabs_keepsSingleSelectedItemInSync`
- JUnit: `app/src/test/java/com/ndi/app/navigation/TopLevelNavigationCoordinatorTest.kt`
  - `settingsRoute_resolvesToSettingsFragmentId`
  - `settings_isRecognizedAsTopLevelDestination`
  - `navOptions_forBottomNav_enablesSingleTopAndRestoreState`
  - `isNoOp_returnsTrueForSettingsReselect`
- Playwright: `testing/e2e/tests/settings-navigation-source-list.spec.ts`
  - `@settings @us2 settings exits to home/stream/view with selected-state sync`
  - `@settings @us2 rapid tab switching keeps destination sync`
  - `@settings @us2 rotation in settings preserves state and does not crash`

### Red-phase execution evidence
- Focused Playwright command failed during emulator provisioning before test execution:
  - Command:
    - `cd testing/e2e; npx playwright test tests/settings-navigation-source-list.spec.ts tests/settings-navigation-viewer.spec.ts tests/settings-navigation-output.spec.ts --project=android-primary`
  - Failure:
    - `SOURCE_PROVISION_FAILED: error: device 'emulator-5554' not found`
    - `RECEIVER_PROVISION_FAILED: error: device 'emulator-5556' not found`

## Phase 4 (US2) - GREEN Evidence (T028-T031)

### Implementation verified
- Existing settings exit/navigation sync logic verified in:
  - `app/src/main/java/com/ndi/app/navigation/TopLevelNavViewModel.kt`
  - `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationHost.kt`
  - `app/src/main/java/com/ndi/app/MainActivity.kt`
- Settings title/header preserved in:
  - `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`

### Unit result
- Command: `./gradlew.bat :app:testDebugUnitTest --no-daemon`
- Result: `BUILD SUCCESSFUL`

## Phase 5 (US3) - RED Evidence (T035)

### Added US3 tests
- Playwright: `testing/e2e/tests/settings-navigation-source-list.spec.ts`
  - `@settings @us3 source-list has no top-right settings affordance`
- Playwright: `testing/e2e/tests/settings-navigation-viewer.spec.ts`
  - `@settings @us3 viewer has no top-right settings affordance`
- Playwright: `testing/e2e/tests/settings-navigation-output.spec.ts`
  - `@settings @us3 output and settings surfaces have no top-right settings affordance`

### Red-phase execution evidence
- Primary regression command also failed in emulator provisioning stage:
  - Command:
    - `cd testing/e2e; npm run test:pr:primary`
  - Failure:
    - `SOURCE_PROVISION_FAILED: error: device 'emulator-5554' not found`
    - `RECEIVER_PROVISION_FAILED: error: device 'emulator-5556' not found`

## Phase 5 (US3) - GREEN Implementation (T036-T043)

### Menu XML removals
- `feature/ndi-browser/presentation/src/main/res/menu/source_list_menu.xml`
- `feature/ndi-browser/presentation/src/main/res/menu/viewer_menu.xml`
- `feature/ndi-browser/presentation/src/main/res/menu/output_menu.xml`
- `feature/ndi-browser/presentation/src/main/res/menu/settings_menu.xml`

### Toolbar click-handler removals
- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt`
- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`
- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlFragment.kt`
- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`

### Regression-safety unit result
- Command: `./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest --no-daemon`
- Result: `BUILD SUCCESSFUL`

## Phase 6 (Polish & Gates)

### T044 - quickstart updated
- Updated command set in `specs/014-bottom-nav-settings/quickstart.md` for:
  - Unit tests
  - Focused Playwright
  - Primary PR regression
  - Hardening verification

### T044a - Material 3 checklist added
- Created `specs/014-bottom-nav-settings/Material3-Compliance-Verification.md`

### T045 - Unit suites
- `./gradlew.bat :app:testDebugUnitTest --no-daemon` => `BUILD SUCCESSFUL`
- `./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest --no-daemon` => `BUILD SUCCESSFUL`

### T046 - Focused Playwright bottom-nav settings scenarios
- `cd testing/e2e; npx playwright test tests/settings-navigation-source-list.spec.ts tests/settings-navigation-viewer.spec.ts tests/settings-navigation-output.spec.ts --project=android-primary`
- Status: FAILED (environment)
- Blocking cause: emulator provisioning failed (`emulator-5554`, `emulator-5556` not found)

### T047 - Existing Playwright regression suite
- `cd testing/e2e; npm run test:pr:primary`
- Status: FAILED (environment)
- Blocking cause: emulator provisioning failed (`emulator-5554`, `emulator-5556` not found)

### T048 - Release hardening verification
- Command: `Set-Location C:\gitrepos\NDI-for-Android; .\gradlew.bat :app:assembleDebug :app:verifyReleaseHardening --no-daemon`
- Result: `BUILD SUCCESSFUL`

## Summary Status
- US2 (T024-T031): Code + unit coverage complete; unit tests green.
- US3 (T032-T043): Code changes complete; Playwright assertions authored; execution blocked by emulator provisioning.
- T044-T048: Documentation/checklist/hardening complete; Playwright gates blocked by environment, all Gradle gates green.
