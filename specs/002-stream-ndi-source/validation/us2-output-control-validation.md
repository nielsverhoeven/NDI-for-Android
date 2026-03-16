# US2 Output Control Validation

## Scope

Validate stop-control lifecycle, status rendering, telemetry hooks, and persistence behavior for User Story 2.

## Automated Results

| Command | Result | Notes |
|---|---|---|
| `./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest :feature:ndi-browser:data:testDebugUnitTest :app:assembleDebug` | PASS | Executed with JAVA_HOME set to JDK 21 (`C:\Program Files\Java\jdk-21.0.10`) |

## Test Coverage Added

- ViewModel stop/control tests:
  - `OutputControlStopStateTest` verifies stop transition, idempotent stop guard, failure path, and control availability mapping.
- Repository stop contract tests:
  - `NdiOutputRepositoryStopContractTest` verifies stop persistence, idempotent bridge stop, and health snapshot update.
- Playwright placeholder tests (intentionally failing pending emulator automation wiring):
  - `testing/e2e/tests/us2-output-status.spec.ts`
  - `testing/e2e/tests/us2-stop-output.spec.ts`

## Manual Sanity Checks

- Start output transitions to ACTIVE and enables Stop.
- Stop action transitions through STOPPING to STOPPED.
- STOPPED state re-enables Start and disables Stop.
- Stop telemetry events are emitted from ViewModel stop path.

## Known Gaps

- US2 Playwright scenarios are scaffolded only and still intentionally marked with `test.fail(...)` until dual-emulator automation wiring is implemented.
