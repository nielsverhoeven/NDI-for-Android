# Testing E2E (Feature 024)

## Suite Overview

Feature `024-rebuild-android-e2e` rebuilds the Android Playwright e2e suite with canonical outcomes (`pass`, `fail`, `blocked`, `not-applicable`) and preflight-first gating.

### Story Profiles

- US1: Baseline rebuild and legacy retirement checks.
- US4: CI execution contracts, gating semantics, and artifact publishing.
- US2: Settings and navigation menu deterministic scenarios.
- US3: Developer-mode capability-aware scenarios.

### Required Evidence

- Preflight outputs under `test-results/024-preflight-*.md`.
- Story-level red/green evidence under `test-results/024-us*-*.md`.
- Agent workflow index in `test-results/024-agent-workflow-index.md`.

### CI Execution and Triage Flow

1. Run preflight checks before any e2e gate.
2. Execute `run-primary-pr-e2e.ps1` for required profile status and gate decision.
3. Publish `testing/e2e/artifacts/primary-status.json` for machine-readable status.
4. On `fail` or `blocked`, publish `testing/e2e/artifacts/triage-summary.json` with timestamps and root cause class.
5. Upload `test-results/024-us4-ci-validation.md` and related 024 evidence files as CI artifacts.
