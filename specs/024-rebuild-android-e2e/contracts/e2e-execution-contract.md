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
- `fail` => product/test failure with failing scenario identifiers.
- `not-applicable` => policy-sanctioned target mismatch (developer mode only).

### Artifact Requirements

- JSON result payload for automated parsing.
- Markdown summary for human triage.
- Logs/screenshots/traces as available from Playwright runner.

## 5) Backward Compatibility Contract

- Legacy e2e specs marked as retired MUST NOT be included in active primary suite selection.
- The rebuilt suite becomes the authoritative regression baseline for subsequent changes.
