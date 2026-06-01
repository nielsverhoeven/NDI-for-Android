# Playwright Exception Register

Use this file only when a required end-to-end scenario cannot be executed with Playwright.

## Approval Criteria

- Technical reason Playwright is infeasible for the scenario.
- Proposed alternative test approach.
- Reviewer approval and date.
- Exit criteria to remove the exception.

## Exceptions

| Exception ID | Scenario | Reason Playwright Not Feasible | Alternative Coverage | Approved By | Approved Date | Exit Criteria | Status |
|---|---|---|---|---|---|---|---|
| PWX-001 | Dual-emulator publish/discover/play/stop automation wiring | Emulator control and app-surface bridge endpoints are not yet wired in test harness | Unit/repository tests + manual validation checklist | Pending | Pending | Remove `test.fail(...)` from interop and US1-US3 Playwright specs and provide passing run artifacts | OPEN |
