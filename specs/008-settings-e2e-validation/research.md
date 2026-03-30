# Phase 0 Research: Settings Menu End-to-End Emulator Validation

## Decision 1: Use Playwright as the single e2e framework for this feature

- Decision: Implement new end-to-end coverage using Playwright only, aligned with constitution policy.
- Rationale: The constitution requires Playwright as default e2e framework and mandates migration away from Espresso when feature work touches those paths.
- Alternatives considered: Espresso-only additions (rejected: constitution mismatch), mixed framework additions for same flows (rejected: maintenance overhead and duplicate assertions).

## Decision 2: PR quality gate uses one primary emulator profile

- Decision: Every PR run executes full new-plus-existing e2e suite on one primary emulator profile.
- Rationale: Provides deterministic and timely feedback in normal CI budgets.
- Alternatives considered: full matrix on every PR (rejected: cost and runtime pressure), release-only e2e validation (rejected: late regression discovery).

## Decision 3: Scheduled matrix runs validate cross-profile compatibility

- Decision: Run scheduled/nightly full e2e matrix across multiple emulator profiles.
- Rationale: Captures API/device-profile compatibility drift while preserving PR velocity.
- Alternatives considered: no matrix coverage (rejected: reduced confidence across supported runtime profiles).

## Decision 4: Regression gating applies to existing Playwright suite

- Decision: Existing Playwright scenarios are mandatory in the same validation cycle as new settings scenarios.
- Rationale: Feature acceptance requires proving no regressions in prior behavior.
- Alternatives considered: run only changed/new tests (rejected: insufficient protection for adjacent flows).

## Decision 5: Evidence must separate new-flow and regression outcomes

- Decision: Validation artifacts record per-suite outcomes so reviewers can confirm both new settings coverage and existing-suite pass status.
- Rationale: Supports auditable quality-gate review and clear merge decisions.
- Alternatives considered: single aggregate pass/fail only (rejected: weak traceability and debugging detail).

## Decision 6: Failure handling is fail-fast with actionable diagnostics

- Decision: Emulator startup failures, aborted runs, and partial suite execution fail the gate and require rerun to completion.
- Rationale: Prevents false-positive sign-off and improves reliability of quality evidence.
- Alternatives considered: auto-skip failed emulator sessions (rejected: non-deterministic quality outcomes).

## Decision 7: Assertions target user outcomes, not fragile UI internals

- Decision: Prefer stable user-visible assertions for settings navigation, persistence, validation, and fallback behavior.
- Rationale: Reduces flakiness from layout/text implementation details and locale/device variance.
- Alternatives considered: implementation-coupled assertions (rejected: brittle tests and high maintenance).

## Clarification Resolution Status

All technical context requirements for planning are resolved. No NEEDS CLARIFICATION markers remain.
