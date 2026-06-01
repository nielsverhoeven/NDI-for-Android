# Contract: Settings Menu End-to-End Emulator Validation

## 1. Scope Contract

- This feature covers end-to-end validation for settings-menu flows and mandatory regression execution of existing Playwright scenarios.
- No production UX contract changes are introduced by this feature; this is a quality-gate and validation-scope enhancement.

## 2. PR Validation Contract

### 2.1 Primary Emulator Execution

- Every pull request MUST execute the full required e2e set on one primary emulator profile.
- Required e2e set includes:
  - New settings scenarios introduced by this feature.
  - Existing Playwright regression scenarios.

### 2.2 Pass Criteria

- PR quality gate passes only if all required scenarios pass.
- Any failed scenario blocks feature sign-off unless a documented exception is approved.
- Incomplete or aborted runs are treated as failures.

## 3. Scheduled Matrix Contract

- Scheduled pipeline runs MUST execute the full required e2e set across configured matrix emulator profiles.
- Matrix run failures produce actionable diagnostics and require triage before release sign-off.

## 4. Scenario Coverage Contract

### 4.1 Settings Access Paths

Required settings access scenarios:
- Source List -> Settings -> Back -> Source List.
- Viewer -> Settings -> Back -> Viewer.
- Output Control -> Settings -> Back -> Output Control.

### 4.2 Settings Functional Behavior

Required behavior scenarios:
- Valid discovery endpoint persists across restart.
- Invalid discovery endpoint shows validation feedback and is not applied.
- Unreachable discovery endpoint flow surfaces fallback warning behavior.

### 4.3 Existing Regression Preservation

- Existing Playwright e2e scenarios remain mandatory and must be run with new scenarios in same validation cycle.

## 5. Evidence Contract

Each validation cycle MUST produce reviewer-consumable evidence containing:
- New-settings suite outcome (pass/fail counts, duration).
- Existing-regression suite outcome (pass/fail counts, duration).
- Emulator profile(s) used.
- Run completion status and links to logs/artifacts.

## 6. Failure and Exception Contract

- Emulator startup failure, execution interruption, or partial run marks gate as failed.
- Exceptions require explicit approval and documented rationale in review artifacts.
- Exceptions do not remove requirement to rerun and restore compliant evidence before merge/release completion.
