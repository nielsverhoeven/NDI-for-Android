# Contract: Android E2E Execution and Reporting

## Scope

Defines required execution interfaces for local and CI e2e runs after suite rebuild.

## 1) Preflight Contract

### Command Contract

- Required command:
  - `pwsh ./scripts/verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk`
- Conditional command (dual-emulator or related profiles):
  - `pwsh ./scripts/verify-e2e-dual-emulator-prereqs.ps1 -AllowMissingNdiSdk`

### Behavior Contract

- Preflight MUST run before e2e execution.
- Any failed preflight MUST stop e2e execution and set run outcome to blocked.
- Preflight output MUST identify failed checks and remediation hints.

## 2) Suite Selection Contract

### Required Functional Coverage

- Active suite MUST include executable scenarios for:
  - settings menu
  - navigation menu
  - developer mode

### Target Policy

- Developer mode scenarios MUST run on designated developer-mode-enabled targets.
- On non-capable targets, developer mode scenarios MUST be reported as not-applicable.
- Settings and navigation scenarios remain required gates across primary CI targets.

## 3) Execution Contract

### Local Execution

- Runner entrypoint MUST be available via [testing/e2e/package.json](../../testing/e2e/package.json) scripts.
- Local run MUST produce machine-readable results and human-readable summary artifacts.

### GitHub Actions Execution

- PR/main e2e gate MUST execute in [android-ci.yml](../../.github/workflows/android-ci.yml) (or equivalent required workflow).
- Optional/extended profiles MAY run in [e2e-dual-emulator.yml](../../.github/workflows/e2e-dual-emulator.yml) and [e2e-matrix-nightly.yml](../../.github/workflows/e2e-matrix-nightly.yml).
- CI MUST upload artifacts for each run category required for triage.

## 4) Result Contract

### Allowed Outcomes

- `pass`
- `fail`
- `blocked`
- `not-applicable`

### Classification Rules

- `blocked` => environment blocker with explicit failed preflight evidence.
- `fail` => product/test failure with failing scenario identifiers and root-cause class.
- `not-applicable` => policy-sanctioned target mismatch (developer mode only).

### Gating Rules

- Required profile gates MUST fail on `fail` or `blocked` outcomes.
- Required profile gates MUST NOT fail on policy-sanctioned `not-applicable` outcomes.
- Optional/nightly profiles may report `not-applicable` without merge gating impact.

### Artifact Requirements

- JSON result payload for automated parsing.
- Markdown summary for human triage.
- Logs/screenshots/traces as available from Playwright runner.
- Failed-run triage summary including: failure timestamp, scenario ID(s), root-cause class (`product-defect`, `environment-blocker`, or `test-defect`), and first maintainer classification timestamp.

## 5) Reliability and Operability Contract

### Reliability Window Contract

- Reliability MUST be evaluated over the latest 20 unchanged-code runs for required PR-gate profiles.
- Compliance requires at least 19/20 runs without nondeterministic failures.
- Reliability computation artifacts MUST be published in machine-readable form.

### Triage SLA Contract

- For each failed required-profile run, first maintainer classification timestamp MUST be no later than 15 minutes from failure timestamp.
- If SLA is missed, the run remains valid but must be tagged as operability regression.

## 6) Playwright Agent Workflow Contract

- Scenario planning MUST include Playwright planner agent output evidence.
- Scenario/spec generation MUST include Playwright generator agent output evidence.
- Failure remediation MUST include Playwright healer agent output evidence when triaging failed scenarios.
- Agent evidence MUST be stored with run artifacts under `test-results/` or `testing/e2e/artifacts/`.

## 7) Backward Compatibility Contract

- Legacy e2e specs marked as retired MUST NOT be included in active primary suite selection.
- The rebuilt suite becomes the authoritative regression baseline for subsequent changes.
