# Feature 020 US1 Viewer Regression Evidence

Date: 2026-03-29
Story: US1 Smooth Default Playback

## Prerequisite Gate
Commands executed:
- ./scripts/verify-android-prereqs.ps1
- ./gradlew.bat --version

Status:
- PASS

Notes:
- Android prerequisite verification passed (JAVA_HOME, ANDROID_SDK_ROOT, NDI SDK, adb, sdkmanager, emulator, cmake, ninja all detected).
- Gradle wrapper/toolchain verified: Gradle 9.2.1 on JDK 21.
- Module graph confirmed in settings.gradle.kts: :app, :core:model, :core:database, :core:testing, :feature:ndi-browser:{domain,data,presentation}, :ndi:sdk-bridge.

## US1 Unit Tests: FAIL
Command executed:
- ./gradlew :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest --tests "*PlaybackOptimizationPolicyTest" --tests "*ViewerReconnectCoordinatorTest" --tests "*NdiViewerRepositoryImplQualityTest"

Expected:
- 12 passing tests (3 + 4 + 5)

Actual:
- 0 passed
- 12 not executed (compilation failed before test execution)

Failure evidence summary:
- :feature:ndi-browser:data:compileDebugUnitTestKotlin FAILED
- :feature:ndi-browser:presentation:compileDebugUnitTestKotlin FAILED
- New US1 test files failed to compile with unresolved JUnit5/Mockito symbols (for example unresolved jupiter/mockito imports).
- Additional presentation test compilation failures were also present in settings-related tests (unresolved symbols in settings test sources), which block test task execution.

## Viewer Regression Tests: FAIL
Command executed:
- ./gradlew :feature:ndi-browser:presentation:testDebugUnitTest --tests "*ViewerTest*" --tests "*ViewerViewModelTest*"

Actual:
- 0 passed
- 0 failed at runtime
- Test execution blocked by compile failure in :feature:ndi-browser:presentation:compileDebugUnitTestKotlin

Failure evidence summary:
- Compilation stopped in settings and viewer test sources before Viewer baseline tests could run.

## Build Status: PASS
Command executed:
- ./gradlew :app:assembleDebug

Actual:
- BUILD SUCCESSFUL
- Debug APK exported to exports/ndi-for-android-0.7.25.apk

## Overall Gate Status: FAIL
Reason:
- Required US1 unit and Viewer regression gates did not execute due compile-time test-source failures.

## Unblock/Next Steps if FAIL
- Fix test dependencies/import usage for new US1 tests in data and presentation modules (JUnit5/Mockito visibility and configured test engine alignment).
- Resolve existing settings test compile errors in presentation test sources that currently block all filtered test runs.
- Re-run the US1 targeted command and confirm 12/12 pass.
- Re-run Viewer regression command and confirm filtered Viewer tests execute and pass.
- Keep :app:assembleDebug as a final confirmation gate.

## 2026-03-29 Kotlin-only Revalidation
Command executed:
- ./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest --tests "*PlaybackOptimizationPolicyTest" --tests "*NdiViewerRepositoryImplQualityTest"
- ./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest --tests "*ViewerReconnectCoordinatorTest" --console=plain
- ./gradlew.bat :feature:ndi-browser:presentation:compileDebugKotlin :app:assembleDebug --console=plain

Status:
- PASS

Notes:
- Playwright validation intentionally skipped per current implementation direction.
- Data and presentation targeted Kotlin unit tests passed after converting the feature tests to the repo's JUnit4 setup.
- Debug assemble remained successful after the US1 code path changes.
