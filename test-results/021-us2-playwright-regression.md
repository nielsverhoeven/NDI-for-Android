# 021 US2 Playwright Regression

Date: 2026-03-29

## Scope

- Previously Connected and Unavailable status rendering.
- Disabled navigation behavior for unavailable sources.

## Result

Execution deferred by implementation decision while e2e scenarios are being rebuilt from the ground up.

Current non-e2e validation status:
- Source-list unit tests passed (`:feature:ndi-browser:presentation:testDebugUnitTest --tests "com.ndi.feature.ndibrowser.source_list.*"`)
- Module unit test suites passed (`:feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest`)
- Release hardening gate passed (`:app:verifyReleaseHardening`)

## Blocked-Gate Evidence Template

- Gate: US2 Playwright regression (`tests/021-source-list-availability-status.spec.ts`)
- Blocker: Intentional deferment while e2e test suite is rebuilt
- Reproduction: N/A (deferred by direction)
- Unblock command: Re-run `Push-Location testing/e2e; npx playwright test tests/021-source-list-availability-status.spec.ts; Pop-Location`
- Retry outcome: Pending after e2e rebuild completion
