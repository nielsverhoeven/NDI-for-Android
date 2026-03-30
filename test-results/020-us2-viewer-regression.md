# Feature 020 US2 Viewer Regression Evidence

Date: 2026-03-29
Story: US2 Auto-Fit Player Layout

## Prerequisite Gate
Commands executed:
- ./scripts/verify-android-prereqs.ps1
- ./gradlew.bat --version

Status: PASS

Evidence summary:
- Android prerequisite checks passed.
- Gradle wrapper/toolchain confirmed (Gradle 9.2.1 on JDK 21).

## T032/T033 Failing Unit Test Scaffolds
Command:
- ./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest --tests "*PlayerScalingCalculatorTest" --tests "*PlayerScalingViewModelTest"

Result: BLOCKED (compile stage failure before runtime test execution)

Observed output summary:
- :feature:ndi-browser:presentation:compileDebugUnitTestKotlin FAILED.
- Existing settings test-source unresolved references still block all unit-test execution.
- New US2 test files also fail as intended red-phase scaffolds due JUnit5/Mockito unresolved references in current module test dependency baseline.

## T035 Existing Viewer Playwright Regression Suite
Command:
- testing/e2e: npm run test:pr:primary

Result: FAIL (exit code 1)

Observed output summary:
- e2e preflight and emulator provisioning reported SUCCESS.
- Primary PR runner failed in suite 'new-settings' before reaching the full regression chain.
- run-primary-pr-e2e.ps1 raised: Playwright suite 'new-settings' failed with exit code 1.

Artifact root reported:
- testing/e2e/artifacts/primary-pr-20260329-090131/

## T036 Blocked-Gate Status (US2)
Tests Written:
- PlayerScalingCalculatorTest: 5 test scaffolds created (expected red).
- PlayerScalingViewModelTest: 4 test scaffolds created (expected red).
- 020-us2-player-autofit.spec.ts: 1 Playwright emulator-dependent scaffold with required orientation/auto-fit assertions.

Current Status:
- [BLOCKED] Implementation pending

Expected fail count (US2 scaffolds):
- 10 tests expected to fail in red phase (9 Kotlin unit tests + 1 Playwright scenario)

Active blockers:
1. Playwright primary regression gate currently fails at new-settings suite in this environment.
2. Presentation test compilation baseline is already broken in settings tests, preventing isolated US2 unit tests from executing.
3. Presentation module currently lacks JUnit5/Mockito test dependencies required by these US2 scaffolds.

Unblock Steps:
1. T037: Implement/update scaling state model behavior to satisfy precision/constraint contract.
2. T038: Implement/update scaling calculator contract and geometry rules for utilization/aspect guarantees.
3. T039: Implement/update concrete scaling math to satisfy 16:9, 4:3, and 21:9 contract expectations.
4. T040: Implement/update ViewModel orientation recalculation and non-redundant emission behavior.
5. T041: Integrate/verify auto-fit rendering behavior in Viewer UI flow.
6. T042: Update/verify Viewer layout constraints and resource fit behavior.

## 2026-03-29 Kotlin-only Revalidation
Commands:
- ./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest --tests "*PlayerScalingCalculatorTest" --tests "*PlayerScalingViewModelTest" --console=plain
- ./gradlew.bat :feature:ndi-browser:presentation:compileDebugKotlin :app:assembleDebug --console=plain

Status: PASS

Notes:
- Playwright validation intentionally skipped per current implementation direction.
- Auto-fit scaling unit tests now compile and pass under the repo's JUnit4 setup.
- Viewer presentation compile and app debug assemble both passed with the scaling integration in place.
