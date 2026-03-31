# US3 Playwright Healer Evidence

## Controlled Failure-Recovery Rehearsal
1. Triggered controlled failure path: `run-primary-pr-e2e.ps1 -Profile us3-only -Status fail`.
2. Verified triage artifact generation at `testing/e2e/artifacts/triage-summary.json`.
3. Re-ran with capability-aware path:
   - capable target (`DeveloperModeAvailable=true`) -> pass
   - non-capable target (`DeveloperModeAvailable=false`) -> not-applicable
4. Confirmed gate decision remains pass for policy-sanctioned not-applicable outcome.
