# US3 Output Recovery Validation

## Scope

Validate interruption handling, bounded retry recovery, recovery UI actions, and telemetry outcomes for User Story 3.

## Automated Results

| Command | Result | Notes |
|---|---|---|
| `./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest :feature:ndi-browser:data:testDebugUnitTest :app:assembleDebug` | PASS | Executed with JAVA_HOME set to JDK 21 (`C:\Program Files\Java\jdk-21.0.10`) |

## Test Coverage Added

- ViewModel recovery tests:
  - `OutputRecoveryViewModelTest` verifies interrupted recovery-action visibility and retry-to-active behavior.
- Repository recovery contract tests:
  - `NdiOutputRecoveryRepositoryContractTest` verifies retry success within window and stop terminal state on retry-window exhaustion.
- Playwright placeholder tests (intentionally failing pending emulator automation wiring):
  - `testing/e2e/tests/us3-recovery-actions.spec.ts`
  - `testing/e2e/tests/us3-source-loss.spec.ts`

## Behavior Verified

- Interrupted sessions surface retry controls.
- Retry requests are bounded by window seconds and transition to ACTIVE on success.
- Retry exhaustion transitions to STOPPED with interruption reason retained.
- Retry telemetry events are emitted for requested/succeeded/failed outcomes.

## Known Gaps

- US3 Playwright scenarios remain scaffolded placeholders until dual-emulator automation wiring is completed.
