# Android Test Results

Date: 2026-03-29

## Scope
- Branch/commit: Local workspace state on 2026-03-29 (commit hash not captured in this run).
- Changed modules under validation: :feature:ndi-browser:presentation, testing/e2e.
- Related spec task IDs: Feature 020 US2 validation tasks T032-T036.

## Stage Results
- Prerequisite Gate: PASS
  - Command: ./scripts/verify-android-prereqs.ps1
  - Command: ./gradlew.bat --version
- Stage 2 (module-aware targeted unit tests for US2): FAIL
  - Command: ./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest --tests "*PlayerScalingCalculatorTest" --tests "*PlayerScalingViewModelTest"
  - Result: compilation failed before test execution.
- Stage 4 (Playwright e2e primary regression run for US2 evidence): FAIL
  - Command: Set-Location testing/e2e; npm run test:pr:primary
  - Result: run-primary-pr-e2e.ps1 failed in Playwright suite 'new-settings' with exit code 1.
- Stage 6 (release hardening checks): NOT RUN in this scoped validation request.

## Issues Found & Fixes
| Defect | Root Cause | Fix Applied | Verification |
|---|---|---|---|
| US2 targeted scaling tests could not execute | Kotlin test-source compile failures in presentation settings tests plus unresolved JUnit5/Mockito references for red-phase US2 scaffolds | Added failing US2 test scaffolds per T032/T033/T034; no implementation fixes applied in this phase | Reproduced by failing targeted Gradle test command |
| Viewer Playwright primary regression gate failed | Existing 'new-settings' Playwright suite exits with code 1 in current environment | No code fix in this validation run | Reproduced by npm run test:pr:primary |

## E2E Evidence
- Executed primary PR regression runner for US2 evidence.
- Artifact path: testing/e2e/artifacts/primary-pr-20260329-090131/.
- Detailed US2 evidence file: test-results/020-us2-viewer-regression.md.

## Release Gate Status
- Prereq script passes: [x]
- Module-aware unit tests pass for changed modules: [ ]
- Instrumentation/UI tests pass for impacted flows: [ ] Not run in this scoped request
- Dual-emulator e2e pass for source discovery/streaming/output changes: [ ]
- :app:verifyReleaseHardening passes: [ ] Not run in this scoped request

Final disposition: FAIL (blocked at module unit-test compilation stage).
