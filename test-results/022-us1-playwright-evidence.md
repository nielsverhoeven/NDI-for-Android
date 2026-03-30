# 022 US1 Playwright Evidence

Status: BLOCKED:ENVIRONMENT

- Scenario files exist:
  - testing/e2e/tests/022-us1-valid-server-check.spec.ts
  - testing/e2e/tests/022-us1-invalid-server-check.spec.ts
- Both tests are currently marked with test.skip due to missing emulator + endpoint environment.
- Unblock: run dual-emulator harness with reachable and unreachable discovery endpoints, then execute the two specs and capture artifacts.
