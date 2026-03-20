# Foundation Checkpoint

Date: 2026-03-17
Feature: 004-fix-three-screen-nav

## Setup Completion

- [x] Validation directory created.
- [x] Baseline template captured in e2e-baseline.md.
- [x] Quickstart includes feature-specific execution checklist.

## Foundational Completion Criteria

- [x] Navigation models updated for deterministic view/back policy.
- [x] Navigation telemetry builders include view-flow and support-window events.
- [x] E2E helpers include rolling latest-five support-window evaluation.
- [x] E2E helpers include consent-flow branching helpers.
- [x] Navigation graph and route helpers enforce view-root/viewer policy.

## Evidence Links

- Baseline: ./e2e-baseline.md
- Quickstart: ../quickstart.md

## Notes

Implemented artifacts:

- `core/model/.../TopLevelNavigationModels.kt`: explicit view-flow transition/back-policy session models.
- `core/model/TelemetryEvent.kt` + `app/.../TopLevelNavigationTelemetry.kt`: view-flow/support-window telemetry builders.
- `testing/e2e/tests/support/android-device-fixtures.ts`: rolling latest-five support-window helpers.
- `testing/e2e/tests/support/android-ui-driver.ts`: reusable consent-flow variant helpers with <=1000ms delay enforcement.
- `app/.../NdiNavigation.kt` + `app/.../main_nav_graph.xml`: explicit view<->viewer action IDs and deterministic back-route hooks.

Validation:

- `:app:testDebugUnitTest` passed after foundational changes.
